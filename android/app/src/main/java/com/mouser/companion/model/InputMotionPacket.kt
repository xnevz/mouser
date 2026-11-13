package com.mouser.companion.model

import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * 14-byte packet matching Mouser.Core.Networking.InputMotionPacket (little-endian).
 *
 * Field usage per type:
 *  MOTION  deltaX/deltaY = pointer delta, buttonFlags = full button state mask
 *  BUTTON  buttonFlags = full button state mask (host diffs it into press/release)
 *  KEY     deltaX = Windows virtual-key code, deltaY = 1 down / 0 up
 *  PING    host answers with PONG echoing sequenceId/deltaX/deltaY; PONG buttonFlags = 1 if paired
 *  SCROLL  deltaX = horizontal, deltaY = vertical wheel delta (120 = one notch, positive = right/up)
 *  PAIR    deltaX/deltaY = high/low 16 bits of the 6-digit PIN; host answers PAIR_ACK (buttonFlags 1 = accepted)
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

    /** PIN carried by a PAIR packet. */
    val pinValue: Int get() = ((deltaX.toInt() and 0xFFFF) shl 16) or (deltaY.toInt() and 0xFFFF)

    companion object {
        const val EXPECTED_MAGIC = 0x53554F4D // "MOUS" in little-endian
        const val CURRENT_VERSION: Byte = 0x01
        const val PACKET_SIZE = 14

        const val TYPE_MOTION: Byte = 0x01
        const val TYPE_BUTTON: Byte = 0x02
        const val TYPE_KEY: Byte = 0x03
        const val TYPE_PING: Byte = 0x04
        const val TYPE_SCROLL: Byte = 0x05
        const val TYPE_PAIR: Byte = 0x06
        const val TYPE_PAIR_ACK: Byte = 0x07
        const val TYPE_PONG: Byte = 0x08

        const val BUTTON_LEFT: Byte = 0x01
        const val BUTTON_RIGHT: Byte = 0x02
        const val BUTTON_MIDDLE: Byte = 0x04

        const val WHEEL_DELTA = 120

        fun pair(sequenceId: Short, pin: Int) = InputMotionPacket(
            packetType = TYPE_PAIR,
            sequenceId = sequenceId,
            deltaX = (pin shr 16).toShort(),
            deltaY = (pin and 0xFFFF).toShort()
        )

        fun fromByteArray(data: ByteArray, length: Int = data.size): InputMotionPacket? {
            if (length < PACKET_SIZE) return null
            val buffer = ByteBuffer.wrap(data, 0, length).order(ByteOrder.LITTLE_ENDIAN)
            val magic = buffer.int
            val version = buffer.get()
            if (magic != EXPECTED_MAGIC || version != CURRENT_VERSION) return null

            return InputMotionPacket(
                magic = magic,
                version = version,
                packetType = buffer.get(),
                sequenceId = buffer.short,
                deltaX = buffer.short,
                deltaY = buffer.short,
                buttonFlags = buffer.get(),
                modifiers = buffer.get()
            )
        }
    }
}

/** Windows virtual-key codes: the key format the desktop host expects on the wire. */
object VirtualKey {
    const val BACK: Short = 0x08
    const val TAB: Short = 0x09
    const val RETURN: Short = 0x0D
    const val ESCAPE: Short = 0x1B
    const val SPACE: Short = 0x20
    const val LEFT: Short = 0x25
    const val UP: Short = 0x26
    const val RIGHT: Short = 0x27
    const val DOWN: Short = 0x28
    const val NUMPAD0: Short = 0x60
    const val MULTIPLY: Short = 0x6A
    const val ADD: Short = 0x6B
    const val SUBTRACT: Short = 0x6D
    const val DECIMAL: Short = 0x6E
    const val VOLUME_MUTE: Short = 0xAD
    const val VOLUME_DOWN: Short = 0xAE
    const val VOLUME_UP: Short = 0xAF
    const val MEDIA_NEXT: Short = 0xB0
    const val MEDIA_PREV: Short = 0xB1
    const val MEDIA_PLAY_PAUSE: Short = 0xB3

    fun numpadDigit(digit: Int): Short = (NUMPAD0 + digit).toShort()
}
