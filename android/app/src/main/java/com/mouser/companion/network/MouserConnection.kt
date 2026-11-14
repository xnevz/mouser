package com.mouser.companion.network

import com.mouser.companion.model.HostConnectionState
import com.mouser.companion.model.HostTarget
import com.mouser.companion.model.InputMotionPacket
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.SocketTimeoutException
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

/**
 * UDP session with one Mouser desktop: keep-alive pings and ordered encrypted input packets.
 */
class MouserConnection(
    private val scope: CoroutineScope
) {
    companion object {
        const val PING_INTERVAL_MS = 2_000L
        /** No pong for this long means the host is unreachable. */
        const val PONG_TIMEOUT_MS = 6_000L
    }

    private val _state = MutableStateFlow<HostConnectionState>(HostConnectionState.Disconnected)
    val state: StateFlow<HostConnectionState> = _state.asStateFlow()

    private val sequence = SequenceCounter()
    private val buttons = ButtonState()
    private val pingSentAt = ConcurrentHashMap<Short, Long>()
    val backoff = Backoff(1000L, 8000L, 2.0)

    // A single thread keeps packets in order; per-packet coroutines could reorder them and the host drops late ones.
    private var sender: ExecutorService? = null
    private var socket: DatagramSocket? = null
    private var target: HostTarget? = null
    private var sessionJobs = emptyList<Job>()
    @Volatile private var lastPongAt = 0L
    @Volatile private var paired = false
    @Volatile var sessionCrypto: SessionCrypto? = null
    @Volatile var sessionId: Long = 0L
    @Volatile var senderIdHash: ByteArray = ByteArray(4)

    fun initSessionCrypto(pairKey: ByteArray, ownDeviceId: String, hostDeviceId: String, currentSessionId: Long) {
        sessionId = currentSessionId
        senderIdHash = SecureInputPacket.computeSenderIdHash(ownDeviceId)
        val isInitiator = ownDeviceId < hostDeviceId
        sessionCrypto = SessionCrypto(pairKey, isInitiator, currentSessionId)
        paired = true
    }

    val isPaired: Boolean get() = paired

    fun connect(host: HostTarget, pairKey: ByteArray? = null, ownDeviceId: String? = null) {
        disconnect()
        target = host
        _state.value = HostConnectionState.Connecting(host)

        if (pairKey != null && ownDeviceId != null && host.deviceId != null) {
            initSessionCrypto(pairKey, ownDeviceId, host.deviceId, System.currentTimeMillis())
        }

        sessionJobs = listOf(scope.launch(Dispatchers.IO) {
            val s = try {
                DatagramSocket().apply {
                    soTimeout = 1000
                    connect(InetAddress.getByName(host.address), host.port)
                }
            } catch (e: Exception) {
                _state.value = HostConnectionState.Error("Can't reach ${host.address}: ${e.message}")
                return@launch
            }
            socket = s
            sender = Executors.newSingleThreadExecutor()
            lastPongAt = System.currentTimeMillis()

            launch { pingLoop() }
            receiveLoop(s)
        })
    }

    fun disconnect() {
        if (paired) releaseButtons()
        sessionJobs.forEach { it.cancel() }
        sessionJobs = emptyList()
        sender?.shutdown()
        sender = null
        socket?.close()
        socket = null
        target = null
        paired = false
        backoff.reset()
        pingSentAt.clear()
        _state.value = HostConnectionState.Disconnected
    }

    fun sendMotion(dx: Short, dy: Short) {
        if (dx == 0.toShort() && dy == 0.toShort()) return
        send(InputMotionPacket(packetType = InputMotionPacket.TYPE_MOTION, sequenceId = sequence.next(),
            deltaX = dx, deltaY = dy, buttonFlags = buttons.mask))
    }

    fun setButton(button: Byte, isDown: Boolean) {
        val mask = buttons.set(button, isDown)
        send(InputMotionPacket(packetType = InputMotionPacket.TYPE_BUTTON, sequenceId = sequence.next(), buttonFlags = mask))
    }

    fun click(button: Byte) {
        setButton(button, true)
        setButton(button, false)
    }

    fun sendScroll(horizontal: Short, vertical: Short) {
        if (horizontal == 0.toShort() && vertical == 0.toShort()) return
        send(InputMotionPacket(packetType = InputMotionPacket.TYPE_SCROLL, sequenceId = sequence.next(),
            deltaX = horizontal, deltaY = vertical))
    }

    fun sendKey(virtualKey: Short, isDown: Boolean) {
        send(InputMotionPacket(packetType = InputMotionPacket.TYPE_KEY, sequenceId = sequence.next(),
            deltaX = virtualKey, deltaY = if (isDown) 1 else 0))
    }

    fun tapKey(virtualKey: Short) {
        sendKey(virtualKey, true)
        sendKey(virtualKey, false)
    }

    fun sendChar(codeUnit: Char, isDown: Boolean) {
        send(InputMotionPacket(
            packetType = InputMotionPacket.TYPE_CHAR,
            sequenceId = sequence.next(),
            deltaX = codeUnit.code.toShort(),
            deltaY = if (isDown) 1 else 0,
            buttonFlags = if (isDown) 1 else 0
        ))
    }

    fun typeChar(codeUnit: Char) {
        sendChar(codeUnit, true)
        sendChar(codeUnit, false)
    }

    fun typeText(text: String) {
        for (i in 0 until text.length) {
            typeChar(text[i])
        }
    }

    fun setLaser(isOn: Boolean) {
        send(InputMotionPacket(
            packetType = InputMotionPacket.TYPE_LASER,
            sequenceId = sequence.next(),
            deltaY = if (isOn) 1 else 0,
            buttonFlags = if (isOn) 1 else 0
        ))
    }

    private fun releaseButtons() {
        if (buttons.mask != 0.toByte()) {
            buttons.clear()
            send(InputMotionPacket(packetType = InputMotionPacket.TYPE_BUTTON, sequenceId = sequence.next()), force = true)
        }
    }

    private fun send(packet: InputMotionPacket, force: Boolean = false) {
        if (!force && !paired) return
        val s = socket ?: return
        val exec = sender ?: return
        try {
            exec.execute {
                try {
                    val crypto = sessionCrypto
                    val data = if (crypto != null && packet.packetType != InputMotionPacket.TYPE_PING) {
                        val raw = packet.toByteArray()
                        val (counter, cipherAndTag) = crypto.seal(raw)
                        SecureInputPacket.write(senderIdHash, sessionId, counter, cipherAndTag)
                    } else {
                        packet.toByteArray()
                    }
                    s.send(DatagramPacket(data, data.size))
                } catch (_: Exception) {
                    // Wi-Fi hiccup: the next ping notices if the host is really gone.
                }
            }
        } catch (_: java.util.concurrent.RejectedExecutionException) {
            // Disconnected while sending.
        }
    }

    private suspend fun pingLoop() {
        while (kotlinx.coroutines.currentCoroutineContext().isActive) {
            val seq = sequence.next()
            pingSentAt[seq] = System.currentTimeMillis()
            send(InputMotionPacket(packetType = InputMotionPacket.TYPE_PING, sequenceId = seq), force = true)
            if (pingSentAt.size > 32) pingSentAt.keys.sorted().take(16).forEach { pingSentAt.remove(it) }

            val t = target
            val current = _state.value
            val waitingOnHost = current is HostConnectionState.Connected || current is HostConnectionState.Connecting
            if (t != null && waitingOnHost && System.currentTimeMillis() - lastPongAt > PONG_TIMEOUT_MS) {
                _state.value = HostConnectionState.Unreachable(t)
                reconnect()
            }
            delay(PING_INTERVAL_MS)
        }
    }

    private fun receiveLoop(s: DatagramSocket) {
        val buffer = ByteArray(64)
        val datagram = DatagramPacket(buffer, buffer.size)
        while (!s.isClosed) {
            try {
                s.receive(datagram)
            } catch (_: SocketTimeoutException) {
                continue
            } catch (_: Exception) {
                // ICMP port unreachable (PC app closed) surfaces here on connected sockets; keep listening.
                if (s.isClosed) return
                continue
            }
            val packet = InputMotionPacket.fromByteArray(buffer, datagram.length) ?: continue
            handleReply(packet)
        }
    }

    fun reconnect() {
        val t = target ?: return
        scope.launch(Dispatchers.IO) {
            val delayMs = backoff.nextDelayMs()
            delay(delayMs)
            try {
                socket?.close()
                val newSocket = DatagramSocket().apply {
                    soTimeout = 1000
                    connect(InetAddress.getByName(t.address), t.port)
                }
                socket = newSocket
                lastPongAt = System.currentTimeMillis()
                val seq = sequence.next()
                pingSentAt[seq] = System.currentTimeMillis()
                send(InputMotionPacket(packetType = InputMotionPacket.TYPE_PING, sequenceId = seq), force = true)
            } catch (_: Exception) {
            }
        }
    }

    internal fun handleReply(packet: InputMotionPacket) {
        val t = target ?: return
        when (packet.packetType) {
            InputMotionPacket.TYPE_PONG -> {
                lastPongAt = System.currentTimeMillis()
                backoff.reset()
                val sentAt = pingSentAt.remove(packet.sequenceId)
                if (paired) {
                    _state.value = HostConnectionState.Connected(t, sentAt?.let { System.currentTimeMillis() - it })
                }
            }
        }
    }
}
