package com.mouser.companion.model

/** A desktop to connect to: from discovery, or typed in manually (then [deviceId] is null). */
data class HostTarget(
    val hostName: String,
    val address: String,
    val port: Int,
    val deviceId: String? = null
) {
    /** Key used to remember the PIN for this host. */
    val pinKey: String get() = deviceId ?: "$address:$port"
}

sealed interface HostConnectionState {
    data object Disconnected : HostConnectionState
    data class Connecting(val target: HostTarget) : HostConnectionState
    data class NeedsPin(val target: HostTarget, val wrongPin: Boolean = false) : HostConnectionState
    data class Connected(val target: HostTarget, val latencyMs: Long? = null) : HostConnectionState
    /** Paired, but the host stopped answering pings (Wi-Fi drop, PC asleep, app closed). Still retrying. */
    data class Unreachable(val target: HostTarget) : HostConnectionState
    data class Error(val message: String) : HostConnectionState
}

data class DiscoveredHost(
    val deviceId: String,
    val hostName: String,
    val operatingSystem: String,
    val ipAddress: String,
    val port: Int,
    val lastSeenTimestamp: Long = System.currentTimeMillis()
) {
    fun toTarget() = HostTarget(hostName, ipAddress, port, deviceId)
}
