package com.mouser.companion.model

sealed interface HostConnectionState {
    data object Disconnected : HostConnectionState
    data class Connecting(val host: String, val port: Int) : HostConnectionState
    data class Connected(
        val hostName: String,
        val hostAddress: String,
        val port: Int,
        val pingLatencyMs: Long = 0,
        val packetsSent: Long = 0
    ) : HostConnectionState
    data class Error(val message: String) : HostConnectionState
}

data class DiscoveredHost(
    val hostName: String,
    val ipAddress: String,
    val port: Int,
    val lastSeenTimestamp: Long = System.currentTimeMillis()
)
