package com.mouser.companion.network

import android.content.Context
import android.net.wifi.WifiManager
import com.mouser.companion.model.DiscoveredHost
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetSocketAddress
import java.net.SocketTimeoutException

/**
 * Listens for the UDP broadcast beacons that every Mouser desktop sends once a second.
 */
class HostDiscoveryClient(
    context: Context,
    private val scope: CoroutineScope
) {
    companion object {
        const val DISCOVERY_PORT = 58490
        /** Desktops beacon every second; one that is silent this long is gone. */
        const val HOST_TIMEOUT_MS = 5_000L
    }

    private val wifi = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager

    private val _discoveredHosts = MutableStateFlow<Map<String, DiscoveredHost>>(emptyMap())
    val discoveredHosts: StateFlow<Map<String, DiscoveredHost>> = _discoveredHosts.asStateFlow()

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error.asStateFlow()

    private var listenerJob: Job? = null
    private var socket: DatagramSocket? = null
    private var multicastLock: WifiManager.MulticastLock? = null

    fun startDiscovery() {
        if (listenerJob?.isActive == true) return

        // Many devices filter broadcast packets in Wi-Fi power save unless a multicast lock is held.
        multicastLock = wifi.createMulticastLock("mouser-discovery").apply {
            setReferenceCounted(false)
            acquire()
        }

        listenerJob = scope.launch(Dispatchers.IO) {
            val s = try {
                DatagramSocket(null).apply {
                    reuseAddress = true
                    broadcast = true
                    soTimeout = 1000
                    bind(InetSocketAddress(DISCOVERY_PORT))
                }
            } catch (e: Exception) {
                _error.value = "Can't listen for computers (${e.message}). Enter the address manually."
                return@launch
            }
            socket = s
            _error.value = null

            val buffer = ByteArray(2048)
            val packet = DatagramPacket(buffer, buffer.size)
            while (isActive && !s.isClosed) {
                try {
                    s.receive(packet)
                    val payload = String(packet.data, 0, packet.length, Charsets.UTF_8)
                    val ip = packet.address?.hostAddress ?: continue
                    BeaconParser.parse(payload, ip)?.let { host ->
                        _discoveredHosts.update { it + (host.deviceId to host) }
                    }
                } catch (_: SocketTimeoutException) {
                    // fall through to pruning
                } catch (_: Exception) {
                    if (!isActive || s.isClosed) break
                }
                prune()
            }
        }

        // Prune even when no packets arrive at all.
        scope.launch {
            while (listenerJob?.isActive == true) {
                delay(1000)
                prune()
            }
        }
    }

    private fun prune(now: Long = System.currentTimeMillis()) {
        _discoveredHosts.update { hosts -> hosts.filterValues { now - it.lastSeenTimestamp < HOST_TIMEOUT_MS } }
    }

    fun stopDiscovery() {
        listenerJob?.cancel()
        listenerJob = null
        try {
            socket?.close()
        } catch (_: Exception) {
        }
        socket = null
        multicastLock?.let { if (it.isHeld) it.release() }
        multicastLock = null
        _discoveredHosts.value = emptyMap()
    }
}
