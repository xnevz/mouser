package com.mouser.companion.model

/** A desktop to connect to: from discovery, or typed in manually (then [deviceId] is null). */
data class HostTarget(
    val hostName: String,
    val address: String,
    val port: Int,
    val deviceId: String? = null,
    val controlPort: Int = 4243
) {
    /** Key used to remember pairing for this host. */
    val targetKey: String get() = deviceId ?: "$address:$port"
}

sealed interface HostConnectionState {
    data object Disconnected : HostConnectionState
    data class Connecting(val target: HostTarget) : HostConnectionState
    data class ConfirmPairingCode(val target: HostTarget, val code: String) : HostConnectionState
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
    val controlPort: Int = 4243,
    val lastSeenTimestamp: Long = System.currentTimeMillis()
) {
    fun toTarget() = HostTarget(hostName, ipAddress, port, deviceId, controlPort)
}
