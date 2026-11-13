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
 * UDP session with one Mouser desktop: pairing, keep-alive pings and ordered input packets.
 *
 * @param savePin called after the host accepts a PIN so the next connection pairs automatically.
 */
class MouserConnection(
    private val scope: CoroutineScope,
    private val savePin: (HostTarget, Int) -> Unit
) {
    companion object {
        const val PING_INTERVAL_MS = 1_000L
        /** No pong for this long means the host is unreachable. */
        const val PONG_TIMEOUT_MS = 3_500L
    }

    private val _state = MutableStateFlow<HostConnectionState>(HostConnectionState.Disconnected)
    val state: StateFlow<HostConnectionState> = _state.asStateFlow()

    private val sequence = SequenceCounter()
    private val buttons = ButtonState()
    private val pingSentAt = ConcurrentHashMap<Short, Long>()

    // A single thread keeps packets in order; per-packet coroutines could reorder them and the host drops late ones.
    private var sender: ExecutorService? = null
    private var socket: DatagramSocket? = null
    private var target: HostTarget? = null
    private var pin: Int? = null
    private var sessionJobs = emptyList<Job>()
    @Volatile private var lastPongAt = 0L
    @Volatile private var paired = false

    val isPaired: Boolean get() = paired

    fun connect(host: HostTarget, knownPin: Int?) {
        disconnect()
        target = host
        pin = knownPin
        _state.value = HostConnectionState.Connecting(host)

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

            if (knownPin != null) sendPair(knownPin) else _state.value = HostConnectionState.NeedsPin(host)
            launch { pingLoop() }
            receiveLoop(s)
        })
    }

    fun submitPin(newPin: Int) {
        pin = newPin
        target?.let { _state.value = HostConnectionState.Connecting(it) }
        sendPair(newPin)
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

    private fun releaseButtons() {
        if (buttons.mask != 0.toByte()) {
            buttons.clear()
            send(InputMotionPacket(packetType = InputMotionPacket.TYPE_BUTTON, sequenceId = sequence.next()), force = true)
        }
    }

    private fun sendPair(value: Int) = send(InputMotionPacket.pair(sequence.next(), value), force = true)

    private fun send(packet: InputMotionPacket, force: Boolean = false) {
        if (!force && !paired) return
        val s = socket ?: return
        val data = packet.toByteArray()
        try {
            sender?.execute {
                try {
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

    internal fun handleReply(packet: InputMotionPacket) {
        val t = target ?: return
        when (packet.packetType) {
            InputMotionPacket.TYPE_PAIR_ACK -> {
                if (packet.buttonFlags == 1.toByte()) {
                    paired = true
                    lastPongAt = System.currentTimeMillis()
                    pin?.let { savePin(t, it) }
                    _state.value = HostConnectionState.Connected(t)
                } else {
                    paired = false
                    _state.value = HostConnectionState.NeedsPin(t, wrongPin = pin != null)
                }
            }

            InputMotionPacket.TYPE_PONG -> {
                lastPongAt = System.currentTimeMillis()
                val sentAt = pingSentAt.remove(packet.sequenceId)
                val hostSaysPaired = packet.buttonFlags == 1.toByte()
                if (hostSaysPaired) {
                    if (paired) _state.value = HostConnectionState.Connected(t, sentAt?.let { System.currentTimeMillis() - it })
                } else if (paired || _state.value is HostConnectionState.Unreachable) {
                    // Host restarted, came back, or changed its PIN: retry the saved PIN; the ack decides what's next.
                    paired = false
                    val p = pin
                    if (p != null) {
                        _state.value = HostConnectionState.Connecting(t)
                        sendPair(p)
                    } else {
                        _state.value = HostConnectionState.NeedsPin(t)
                    }
                }
            }
        }
    }
}
