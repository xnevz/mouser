package com.mouser.companion.network

import com.mouser.companion.model.InputMotionPacket
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.util.concurrent.atomic.AtomicInteger

/**
 * Ultra-low latency UDP client transmitting 14-byte InputMotionPackets to the desktop Mouser server.
 */
class UdpClient(
    private val scope: CoroutineScope
) {
    private var socket: DatagramSocket? = null
    private var serverAddress: InetAddress? = null
    private var serverPort: Int = 0
    private val sequenceCounter = AtomicInteger(0)

    var packetsSentCount: Long = 0
        private set

    @Synchronized
    fun connect(hostAddress: String, port: Int) {
        disconnect()
        serverAddress = InetAddress.getByName(hostAddress)
        serverPort = port
        socket = DatagramSocket().apply {
            sendBufferSize = 64 * 1024
            soTimeout = 2000
        }
    }

    @Synchronized
    fun disconnect() {
        try {
            socket?.close()
        } catch (_: Exception) {}
        socket = null
        serverAddress = null
        serverPort = 0
    }

    fun isConnected(): Boolean = socket != null && !socket!!.isClosed && serverAddress != null

    fun sendMotion(dx: Short, dy: Short, buttons: Byte = 0, modifiers: Byte = 0) {
        val nextSeq = (sequenceCounter.incrementAndGet() and 0x7FFF).toShort()
        val packet = InputMotionPacket(
            sequenceId = nextSeq,
            deltaX = dx,
            deltaY = dy,
            buttonFlags = buttons,
            modifiers = modifiers
        )
        sendPacket(packet)
    }

    fun sendButtonClick(button: Byte, isDown: Boolean) {
        val nextSeq = (sequenceCounter.incrementAndGet() and 0x7FFF).toShort()
        val flags = if (isDown) button else 0
        val packet = InputMotionPacket(
            packetType = InputMotionPacket.TYPE_BUTTON,
            sequenceId = nextSeq,
            buttonFlags = flags
        )
        sendPacket(packet)
    }

    fun sendKey(keyCode: Short, modifiers: Byte = 0, isDown: Boolean = true) {
        val nextSeq = (sequenceCounter.incrementAndGet() and 0x7FFF).toShort()
        val packet = InputMotionPacket(
            packetType = InputMotionPacket.TYPE_KEY,
            sequenceId = nextSeq,
            deltaX = keyCode,
            deltaY = if (isDown) 1 else 0,
            modifiers = modifiers
        )
        sendPacket(packet)
    }

    private fun sendPacket(packet: InputMotionPacket) {
        val currentSocket = socket ?: return
        val currentTarget = serverAddress ?: return
        if (currentSocket.isClosed) return

        scope.launch(Dispatchers.IO) {
            try {
                val data = packet.toByteArray()
                val datagram = DatagramPacket(data, data.size, currentTarget, serverPort)
                currentSocket.send(datagram)
                packetsSentCount++
            } catch (_: Exception) {
                // Low-latency UDP drops are ignored
            }
        }
    }
}
