package com.mouser.companion

import android.app.Application
import android.content.Context
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.mouser.companion.model.DiscoveredHost
import com.mouser.companion.model.HostConnectionState
import com.mouser.companion.model.HostTarget
import com.mouser.companion.network.HostDiscoveryClient
import com.mouser.companion.network.MouserConnection
import com.mouser.companion.network.SubpixelAccumulator
import com.mouser.companion.network.toShortClamped
import com.mouser.companion.sensors.GyroAirMouse
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.Dispatchers
import com.mouser.companion.network.ClipboardBridge

enum class AppNavigationTab {
    TRACKPAD,
    AIR_MOUSE,
    MACROS,
    TYPE
}

class MouserViewModel(application: Application) : AndroidViewModel(application) {

    private val prefs = application.getSharedPreferences("mouser", Context.MODE_PRIVATE)

    private val connection = MouserConnection(viewModelScope)
    private val discoveryClient = HostDiscoveryClient(application, viewModelScope)

    val connectionState: StateFlow<HostConnectionState> = connection.state
    val discoveredHosts: StateFlow<Map<String, DiscoveredHost>> = discoveryClient.discoveredHosts
    val discoveryError: StateFlow<String?> = discoveryClient.error

    private val _currentTab = MutableStateFlow(AppNavigationTab.TRACKPAD)
    val currentTab: StateFlow<AppNavigationTab> = _currentTab.asStateFlow()

    private val _trackpadSensitivity = MutableStateFlow(prefs.getFloat("trackpad.sensitivity", 1.5f))
    val trackpadSensitivity: StateFlow<Float> = _trackpadSensitivity.asStateFlow()

    private val _airMouseSensitivity = MutableStateFlow(prefs.getFloat("air.sensitivity", 30f))
    val airMouseSensitivity: StateFlow<Float> = _airMouseSensitivity.asStateFlow()

    private val _isAirMouseTriggerHeld = MutableStateFlow(false)
    val isAirMouseTriggerHeld: StateFlow<Boolean> = _isAirMouseTriggerHeld.asStateFlow()

    val themeMode = MutableStateFlow(prefs.getString("theme", "System") ?: "System")

    fun setThemeMode(m: String) {
        prefs.edit().putString("theme", m).apply()
        themeMode.value = m
    }

    val macros = MutableStateFlow<List<com.mouser.companion.model.Macro>>(com.mouser.companion.model.MacroStore.load(prefs))

    fun saveMacro(macro: com.mouser.companion.model.Macro) {
        val current = macros.value.toMutableList()
        val index = current.indexOfFirst { it.id == macro.id }
        if (index >= 0) {
            current[index] = macro
        } else {
            current.add(macro)
        }
        macros.value = current
        com.mouser.companion.model.MacroStore.save(prefs, current)
    }

    fun deleteMacro(id: String) {
        val current = macros.value.filter { it.id != id }
        macros.value = current
        com.mouser.companion.model.MacroStore.save(prefs, current)
    }

    fun resetDefaultMacros() {
        macros.value = com.mouser.companion.model.MacroRunner.defaultMacros
        com.mouser.companion.model.MacroStore.save(prefs, com.mouser.companion.model.MacroRunner.defaultMacros)
    }

    fun importMacrosJson(json: String) {
        val imported = com.mouser.companion.model.MacroStore.fromJson(json)
        if (imported.isNotEmpty()) {
            macros.value = imported
            com.mouser.companion.model.MacroStore.save(prefs, imported)
        }
    }

    fun exportMacrosJson(): String = com.mouser.companion.model.MacroStore.toJson(macros.value)

    fun runMacro(macro: com.mouser.companion.model.Macro) {
        if (isLocked.value) return
        val actions = com.mouser.companion.model.MacroRunner.expandMacro(macro)
        viewModelScope.launch(Dispatchers.IO) {
            for (action in actions) {
                when (action) {
                    is com.mouser.companion.model.MacroAction.KeyAction -> connection.sendKey(action.vk, action.isDown)
                    is com.mouser.companion.model.MacroAction.CharAction -> connection.sendChar(action.char, action.isDown)
                    is com.mouser.companion.model.MacroAction.DelayAction -> kotlinx.coroutines.delay(action.ms)
                }
            }
        }
    }

    val lockState = com.mouser.companion.model.LockState()
    val isAppLockEnabled = MutableStateFlow(prefs.getBoolean("app_lock", false))
    val isLocked = MutableStateFlow(false)

    init {
        lockState.isEnabled = isAppLockEnabled.value
    }

    fun setAppLockEnabled(enabled: Boolean) {
        prefs.edit().putBoolean("app_lock", enabled).apply()
        isAppLockEnabled.value = enabled
        lockState.isEnabled = enabled
        if (!enabled) {
            unlock()
        }
    }

    fun onAppBackgrounded() {
        lockState.onBackgrounded()
    }

    fun onAppForegrounded() {
        lockState.onForegrounded()
        isLocked.value = lockState.isLocked
    }

    fun unlock() {
        lockState.unlock()
        isLocked.value = false
    }

    fun lockNow() {
        lockState.lock()
        isLocked.value = lockState.isLocked
    }

    private val motionX = SubpixelAccumulator()
    private val motionY = SubpixelAccumulator()
    private val scrollX = SubpixelAccumulator()
    private val scrollY = SubpixelAccumulator()

    private val gyroAirMouse = GyroAirMouse(application) { dx, dy -> sendMotion(dx, dy) }

    val hasGyroscope: Boolean get() = gyroAirMouse.isAvailable

    init {
        gyroAirMouse.sensitivity = _airMouseSensitivity.value
        discoveryClient.startDiscovery()
        lastHost()?.let { connect(it) }
    }

    fun selectTab(tab: AppNavigationTab) {
        _currentTab.value = tab
        if (tab != AppNavigationTab.AIR_MOUSE) setAirMouseTriggerHeld(false)
    }

    fun connect(target: HostTarget) {
        val savedHex = prefs.getString("pairkey:${target.targetKey}", null)
        val pairKey = savedHex?.chunked(2)?.map { it.toInt(16).toByte() }?.toByteArray()
        val ownDeviceId = prefs.getString("own.deviceId", null) ?: java.util.UUID.randomUUID().toString().replace("-", "").also {
            prefs.edit().putString("own.deviceId", it).apply()
        }
        connection.connect(target, pairKey, ownDeviceId)
    }

    fun disconnect() {
        prefs.edit().remove("last.address").apply()
        connection.disconnect()
    }

    /** Pointer motion in screen pixels (fractional values accumulate). */
    fun sendMotion(dx: Float, dy: Float) {
        if (isLocked.value) return
        connection.sendMotion(motionX.add(dx).toShortClamped(), motionY.add(dy).toShortClamped())
    }

    /** Scroll by finger distance in pixels; positive dy = finger moved down = content scrolls up (natural). */
    fun sendScroll(dxPixels: Float, dyPixels: Float) {
        if (isLocked.value) return
        // ~1 wheel notch (120) per 40 px of finger travel.
        val h = scrollX.add(-dxPixels * 3f)
        val v = scrollY.add(dyPixels * 3f)
        connection.sendScroll(h.toShortClamped(), v.toShortClamped())
    }

    fun setButton(button: Byte, isDown: Boolean) {
        if (isLocked.value) return
        connection.setButton(button, isDown)
    }

    fun click(button: Byte) {
        if (isLocked.value) return
        connection.click(button)
    }

    fun tapKey(virtualKey: Short) {
        if (isLocked.value) return
        connection.tapKey(virtualKey)
    }

    fun typeText(text: String) {
        if (isLocked.value) return
        connection.typeText(text)
    }

    fun applyTypeDiff(old: String, new: String) {
        if (isLocked.value) return
        val actions = com.mouser.companion.model.diffToKeys(old, new)
        for (action in actions) {
            when (action) {
                is com.mouser.companion.model.Action.Backspace -> connection.tapKey(com.mouser.companion.model.VirtualKey.BACK)
                is com.mouser.companion.model.Action.CharUnit -> connection.typeChar(action.char)
            }
        }
    }

    fun sendClipboardToHost(text: String, onComplete: (Boolean, String) -> Unit) {
        val state = connectionState.value
        if (state !is HostConnectionState.Connected) {
            onComplete(false, "Not connected")
            return
        }
        val target = state.target
        val savedHex = prefs.getString("pairkey:${target.targetKey}", null)
        val pairKey = savedHex?.chunked(2)?.map { it.toInt(16).toByte() }?.toByteArray()
        if (pairKey == null) {
            onComplete(false, "Not paired")
            return
        }
        val ownDeviceId = prefs.getString("own.deviceId", null) ?: "android-phone"

        viewModelScope.launch(Dispatchers.IO) {
            try {
                ClipboardBridge.sendClip(
                    hostAddress = target.address,
                    controlPort = target.controlPort,
                    pairKey = pairKey,
                    ownDeviceId = ownDeviceId,
                    text = text,
                    deviceName = android.os.Build.MODEL ?: "Android Phone"
                )
                withContext(Dispatchers.Main) {
                    onComplete(true, "Sent to ${target.hostName}")
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    onComplete(false, "Failed to send: ${e.message}")
                }
            }
        }
    }

    fun getClipboardFromHost(onComplete: (Boolean, String) -> Unit) {
        val state = connectionState.value
        if (state !is HostConnectionState.Connected) {
            onComplete(false, "Not connected")
            return
        }
        val target = state.target
        val savedHex = prefs.getString("pairkey:${target.targetKey}", null)
        val pairKey = savedHex?.chunked(2)?.map { it.toInt(16).toByte() }?.toByteArray()
        if (pairKey == null) {
            onComplete(false, "Not paired")
            return
        }
        val ownDeviceId = prefs.getString("own.deviceId", null) ?: "android-phone"

        viewModelScope.launch(Dispatchers.IO) {
            try {
                val clip = ClipboardBridge.requestClip(
                    hostAddress = target.address,
                    controlPort = target.controlPort,
                    pairKey = pairKey,
                    ownDeviceId = ownDeviceId
                )
                withContext(Dispatchers.Main) {
                    if (clip != null) {
                        onComplete(true, clip)
                    } else {
                        onComplete(false, "No clipboard content on ${target.hostName}")
                    }
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    onComplete(false, "Failed to get clipboard: ${e.message}")
                }
            }
        }
    }

    fun setTrackpadSensitivity(value: Float) {
        _trackpadSensitivity.value = value
        prefs.edit().putFloat("trackpad.sensitivity", value).apply()
    }

    fun setAirMouseSensitivity(value: Float) {
        _airMouseSensitivity.value = value
        gyroAirMouse.sensitivity = value
        prefs.edit().putFloat("air.sensitivity", value).apply()
    }

    fun setAirMouseTriggerHeld(held: Boolean) {
        if (_isAirMouseTriggerHeld.value == held) return
        _isAirMouseTriggerHeld.value = held
        if (held) gyroAirMouse.start() else gyroAirMouse.stop()
    }

    private fun lastHost(): HostTarget? {
        val address = prefs.getString("last.address", null) ?: return null
        return HostTarget(
            hostName = prefs.getString("last.name", null) ?: address,
            address = address,
            port = prefs.getInt("last.port", 58491),
            deviceId = prefs.getString("last.deviceId", null)
        )
    }

    override fun onCleared() {
        super.onCleared()
        gyroAirMouse.stop()
        discoveryClient.stopDiscovery()
        connection.disconnect()
    }
}
