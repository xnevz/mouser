package com.mouser.companion.network

import com.mouser.companion.model.DiscoveredHost
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress

/**
 * Service that broadcasts discovery queries and listens for Mouser desktop instances on the local network.
 */
class HostDiscoveryClient(
    private val scope: CoroutineScope
) {
    companion object {
        const val DISCOVERY_PORT = 58490
        const val DISCOVERY_HEADER = "MOUSER_PEER:"
        const val DISCOVERY_QUERY = "MOUSER_DISCOVER_QUERY"
    }

    private val _discoveredHosts = MutableStateFlow<Map<String, DiscoveredHost>>(emptyMap())
    val discoveredHosts: StateFlow<Map<String, DiscoveredHost>> = _discoveredHosts.asStateFlow()

    private var listenerJob: Job? = null
    private var socket: DatagramSocket? = null

    fun startDiscovery() {
        if (listenerJob?.isActive == true) return

        listenerJob = scope.launch(Dispatchers.IO) {
            try {
                socket = DatagramSocket(null).apply {
                    reuseAddress = true
                    broadcast = true
                    bind(java.net.InetSocketAddress(DISCOVERY_PORT))
                    soTimeout = 3000
                }

                // Send immediate broadcast ping
                sendDiscoveryBroadcast()

                val buffer = ByteArray(1024)
                val packet = DatagramPacket(buffer, buffer.size)

                while (isActive && socket != null && !socket!!.isClosed) {
                    try {
                        socket?.receive(packet)
                        val message = String(packet.data, 0, packet.length, Charsets.UTF_8).trim()
                        if (message.startsWith(DISCOVERY_HEADER)) {
                            parsePeerAnnouncement(message, packet.address.hostAddress ?: "")
                        }
                    } catch (_: java.net.SocketTimeoutException) {
                        // Periodic broadcast retry
                        sendDiscoveryBroadcast()
                    } catch (_: Exception) {
                        if (!isActive) break
                    }
                }
            } catch (_: Exception) {
                // Port in use or socket error
            }
        }
    }

    private fun sendDiscoveryBroadcast() {
        try {
            val queryBytes = DISCOVERY_QUERY.toByteArray(Charsets.UTF_8)
            val broadcastAddress = InetAddress.getByName("255.255.255.255")
            val queryPacket = DatagramPacket(queryBytes, queryBytes.size, broadcastAddress, DISCOVERY_PORT)
            socket?.send(queryPacket)
        } catch (_: Exception) {}
    }

    private fun parsePeerAnnouncement(payload: String, senderIp: String) {
        // Expected format: MOUSER_PEER:{Hostname}:{Port}:{PairingPin}
        val parts = payload.removePrefix(DISCOVERY_HEADER).split(":")
        if (parts.size >= 2) {
            val hostName = parts[0]
            val port = parts[1].toIntOrNull() ?: 58490
            val host = DiscoveredHost(
                hostName = hostName,
                ipAddress = senderIp,
                port = port,
                lastSeenTimestamp = System.currentTimeMillis()
            )

            _discoveredHosts.update { current ->
                current + (senderIp to host)
            }
        }
    }

    fun stopDiscovery() {
        listenerJob?.cancel()
        listenerJob = null
        try {
            socket?.close()
        } catch (_: Exception) {}
        socket = null
    }
}
