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

enum class AppNavigationTab {
    TRACKPAD,
    AIR_MOUSE,
    MACROS
}

class MouserViewModel(application: Application) : AndroidViewModel(application) {

    private val prefs = application.getSharedPreferences("mouser", Context.MODE_PRIVATE)

    private val connection = MouserConnection(viewModelScope) { target, pin ->
        prefs.edit()
            .putInt("pin:${target.pinKey}", pin)
            .putString("last.name", target.hostName)
            .putString("last.address", target.address)
            .putInt("last.port", target.port)
            .putString("last.deviceId", target.deviceId)
            .apply()
    }
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
        val pin = prefs.getInt("pin:${target.pinKey}", -1).takeIf { it >= 0 }
        connection.connect(target, pin)
    }

    fun submitPin(pin: Int) = connection.submitPin(pin)

    fun disconnect() {
        prefs.edit().remove("last.address").apply()
        connection.disconnect()
    }

    /** Pointer motion in screen pixels (fractional values accumulate). */
    fun sendMotion(dx: Float, dy: Float) {
        connection.sendMotion(motionX.add(dx).toShortClamped(), motionY.add(dy).toShortClamped())
    }

    /** Scroll by finger distance in pixels; positive dy = finger moved down = content scrolls up (natural). */
    fun sendScroll(dxPixels: Float, dyPixels: Float) {
        // ~1 wheel notch (120) per 40 px of finger travel.
        val h = scrollX.add(-dxPixels * 3f)
        val v = scrollY.add(dyPixels * 3f)
        connection.sendScroll(h.toShortClamped(), v.toShortClamped())
    }

    fun setButton(button: Byte, isDown: Boolean) = connection.setButton(button, isDown)
    fun click(button: Byte) = connection.click(button)
    fun tapKey(virtualKey: Short) = connection.tapKey(virtualKey)

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
