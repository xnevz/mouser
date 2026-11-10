package com.mouser.companion.model

import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * 14-byte high-frequency binary packet layout matching Mouser.Core.Networking.InputMotionPacket.
 * All multi-byte integers are serialized in Little-Endian.
 *
 * Layout:
 * - Magic: UInt32 (4 bytes) = 0x53554F4D ("MOUS")
 * - Version: UInt8 (1 byte) = 0x01
 * - PacketType: UInt8 (1 byte) = 0x01 (Motion), 0x02 (Button), 0x03 (Key), 0x04 (Ping)
 * - SequenceId: UInt16 (2 bytes)
 * - DeltaX: Int16 (2 bytes)
 * - DeltaY: Int16 (2 bytes)
 * - ButtonFlags: UInt8 (1 byte) - Bit 0: Left, Bit 1: Right, Bit 2: Middle
 * - Modifiers: UInt8 (1 byte) - Bit 0: Cmd, Bit 1: Shift, Bit 2: Ctrl, Bit 3: Alt
 */
data class InputMotionPacket(
    val magic: Int = EXPECTED_MAGIC,
    val version: Byte = CURRENT_VERSION,
    val packetType: Byte = TYPE_MOTION,
    val sequenceId: Short = 0,
    val deltaX: Short = 0,
    val deltaY: Short = 0,
    val buttonFlags: Byte = 0,
    val modifiers: Byte = 0
) {
    fun toByteArray(): ByteArray {
        val buffer = ByteBuffer.allocate(PACKET_SIZE).order(ByteOrder.LITTLE_ENDIAN)
        buffer.putInt(magic)
        buffer.put(version)
        buffer.put(packetType)
        buffer.putShort(sequenceId)
        buffer.putShort(deltaX)
        buffer.putShort(deltaY)
        buffer.put(buttonFlags)
        buffer.put(modifiers)
        return buffer.array()
    }

    companion object {
        const val EXPECTED_MAGIC = 0x53554F4D // "MOUS" in little-endian
        const val CURRENT_VERSION: Byte = 0x01
        const val PACKET_SIZE = 14

        const val TYPE_MOTION: Byte = 0x01
        const val TYPE_BUTTON: Byte = 0x02
        const val TYPE_KEY: Byte = 0x03
        const val TYPE_PING: Byte = 0x04

        const val BUTTON_LEFT: Byte = 0x01
        const val BUTTON_RIGHT: Byte = 0x02
        const val BUTTON_MIDDLE: Byte = 0x04

        const val MOD_CMD: Byte = 0x01
        const val MOD_SHIFT: Byte = 0x02
        const val MOD_CTRL: Byte = 0x04
        const val MOD_ALT: Byte = 0x08

        fun fromByteArray(data: ByteArray): InputMotionPacket? {
            if (data.size < PACKET_SIZE) return null
            val buffer = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN)
            val magic = buffer.int
            val version = buffer.get()
            if (magic != EXPECTED_MAGIC || version != CURRENT_VERSION) return null

            val packetType = buffer.get()
            val sequenceId = buffer.short
            val deltaX = buffer.short
            val deltaY = buffer.short
            val buttonFlags = buffer.get()
            val modifiers = buffer.get()

            return InputMotionPacket(
                magic = magic,
                version = version,
                packetType = packetType,
                sequenceId = sequenceId,
                deltaX = deltaX,
                deltaY = deltaY,
                buttonFlags = buttonFlags,
                modifiers = modifiers
            )
        }
    }
}
