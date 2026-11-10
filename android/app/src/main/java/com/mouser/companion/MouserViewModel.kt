package com.mouser.companion

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.mouser.companion.model.DiscoveredHost
import com.mouser.companion.model.HostConnectionState
import com.mouser.companion.network.HostDiscoveryClient
import com.mouser.companion.network.UdpClient
import com.mouser.companion.sensors.GyroAirMouse
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

enum class AppNavigationTab {
    TRACKPAD,
    AIR_MOUSE,
    MACROS
}

class MouserViewModel(application: Application) : AndroidViewModel(application) {

    private val udpClient = UdpClient(viewModelScope)
    private val discoveryClient = HostDiscoveryClient(viewModelScope)

    private val _connectionState = MutableStateFlow<HostConnectionState>(HostConnectionState.Disconnected)
    val connectionState: StateFlow<HostConnectionState> = _connectionState.asStateFlow()

    val discoveredHosts: StateFlow<Map<String, DiscoveredHost>> = discoveryClient.discoveredHosts

    private val _currentTab = MutableStateFlow(AppNavigationTab.TRACKPAD)
    val currentTab: StateFlow<AppNavigationTab> = _currentTab.asStateFlow()

    private val _trackpadSensitivity = MutableStateFlow(1.5f)
    val trackpadSensitivity: StateFlow<Float> = _trackpadSensitivity.asStateFlow()

    private val _airMouseSensitivity = MutableStateFlow(30.0f)
    val airMouseSensitivity: StateFlow<Float> = _airMouseSensitivity.asStateFlow()

    private val _isAirMouseTriggerHeld = MutableStateFlow(false)
    val isAirMouseTriggerHeld: StateFlow<Boolean> = _isAirMouseTriggerHeld.asStateFlow()

    private val gyroAirMouse = GyroAirMouse(application) { dx, dy ->
        sendMotion(dx, dy)
    }

    init {
        discoveryClient.startDiscovery()
        gyroAirMouse.start()
    }

    fun selectTab(tab: AppNavigationTab) {
        _currentTab.value = tab
    }

    fun connectToHost(ipAddress: String, port: Int) {
        _connectionState.value = HostConnectionState.Connecting(ipAddress, port)
        viewModelScope.launch {
            try {
                udpClient.connect(ipAddress, port)
                _connectionState.value = HostConnectionState.Connected(
                    hostName = "Desktop Host",
                    hostAddress = ipAddress,
                    port = port
                )
            } catch (e: Exception) {
                _connectionState.value = HostConnectionState.Error(e.message ?: "Connection failed")
            }
        }
    }

    fun disconnect() {
        udpClient.disconnect()
        _connectionState.value = HostConnectionState.Disconnected
    }

    fun sendMotion(dx: Short, dy: Short) {
        udpClient.sendMotion(dx, dy)
    }

    fun sendButtonClick(button: Byte, isDown: Boolean) {
        udpClient.sendButtonClick(button, isDown)
    }

    fun sendKey(keyCode: Short, isDown: Boolean) {
        udpClient.sendKey(keyCode, isDown = isDown)
    }

    fun setTrackpadSensitivity(value: Float) {
        _trackpadSensitivity.value = value
    }

    fun setAirMouseSensitivity(value: Float) {
        _airMouseSensitivity.value = value
        gyroAirMouse.sensitivity = value
    }

    fun setAirMouseTriggerHeld(held: Boolean) {
        _isAirMouseTriggerHeld.value = held
        gyroAirMouse.isTriggerHeld = held
    }

    override fun onCleared() {
        super.onCleared()
        gyroAirMouse.stop()
        discoveryClient.stopDiscovery()
        udpClient.disconnect()
    }
}
