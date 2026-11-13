package com.mouser.companion

import com.mouser.companion.model.InputMotionPacket
import org.junit.Assert.*
import org.junit.Test

class InputMotionPacketTest {

    @Test
    fun testSerializationAndDeserialization() {
        val original = InputMotionPacket(
            magic = InputMotionPacket.EXPECTED_MAGIC,
            version = InputMotionPacket.CURRENT_VERSION,
            packetType = InputMotionPacket.TYPE_MOTION,
            sequenceId = 42,
            deltaX = -120,
            deltaY = 350,
            buttonFlags = (InputMotionPacket.BUTTON_LEFT.toInt() or InputMotionPacket.BUTTON_RIGHT.toInt()).toByte(),
            modifiers = 0x01
        )

        val bytes = original.toByteArray()
        assertEquals(InputMotionPacket.PACKET_SIZE, bytes.size)

        val deserialized = InputMotionPacket.fromByteArray(bytes)
        assertNotNull(deserialized)
        assertEquals(original.magic, deserialized!!.magic)
        assertEquals(original.version, deserialized.version)
        assertEquals(original.packetType, deserialized.packetType)
        assertEquals(original.sequenceId, deserialized.sequenceId)
        assertEquals(original.deltaX, deserialized.deltaX)
        assertEquals(original.deltaY, deserialized.deltaY)
        assertEquals(original.buttonFlags, deserialized.buttonFlags)
        assertEquals(original.modifiers, deserialized.modifiers)
    }

    @Test
    fun testInvalidMagicRejection() {
        val corruptedBytes = ByteArray(14) { 0 }
        val result = InputMotionPacket.fromByteArray(corruptedBytes)
        assertNull(result)
    }
}
