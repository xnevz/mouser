package com.mouser.companion

import com.mouser.companion.model.InputMotionPacket
import com.mouser.companion.model.VirtualKey
import com.mouser.companion.network.BeaconParser
import com.mouser.companion.network.ButtonState
import com.mouser.companion.network.SequenceCounter
import com.mouser.companion.network.SubpixelAccumulator
import com.mouser.companion.network.parsePin
import com.mouser.companion.sensors.GyroAirMouse
import org.junit.Assert.*
import org.junit.Test

class ProtocolTest {

    @Test
    fun beaconFromDesktop_isParsed() {
        // Exact format produced by Mouser.Core PeerDiscoveryService.CreateBeaconPayload.
        val payload = """MOUS_DISC:{"Id":"7f3c","Host":"STUDIO-PC","Os":"Windows","MPort":58491,"CPort":0}"""
        val host = BeaconParser.parse(payload, "192.168.1.3", now = 42)!!
        assertEquals("7f3c", host.deviceId)
        assertEquals("STUDIO-PC", host.hostName)
        assertEquals("Windows", host.operatingSystem)
        assertEquals("192.168.1.3", host.ipAddress)
        assertEquals(58491, host.port)
        assertEquals(42L, host.lastSeenTimestamp)
    }

    @Test
    fun malformedBeacons_areRejected() {
        assertNull(BeaconParser.parse("MOUSER_PEER:host:58490", "1.2.3.4"))
        assertNull(BeaconParser.parse("MOUS_DISC:{not json", "1.2.3.4"))
        assertNull(BeaconParser.parse("""MOUS_DISC:{"Host":"x","MPort":1}""", "1.2.3.4")) // no id
        assertNull(BeaconParser.parse("""MOUS_DISC:{"Id":"a","MPort":0}""", "1.2.3.4"))  // no port
    }

    @Test
    fun pairPacket_carriesTheFullPin() {
        for (pin in listOf(0, 65535, 65536, 482913, 999999)) {
            val bytes = InputMotionPacket.pair(1, pin).toByteArray()
            assertEquals(pin, InputMotionPacket.fromByteArray(bytes)!!.pinValue)
        }
    }

    @Test
    fun pairPacket_bytesMatchTheDesktopLayout() {
        // 482913 = 0x00075E61 -> DeltaX 0x0007, DeltaY 0x5E61 (little-endian), same split as C# CreatePair.
        val bytes = InputMotionPacket.pair(0x0102, 482913).toByteArray()
        assertArrayEquals(
            byteArrayOf(0x4D, 0x4F, 0x55, 0x53, 0x01, 0x06, 0x02, 0x01, 0x07, 0x00, 0x61, 0x5E, 0x00, 0x00),
            bytes
        )
    }

    @Test
    fun sequence_wrapsAtSixteenBits() {
        val counter = SequenceCounter(start = 65534)
        assertEquals(65535, counter.next().toInt() and 0xFFFF)
        assertEquals(0, counter.next().toInt() and 0xFFFF)
        assertEquals(1, counter.next().toInt() and 0xFFFF)
    }

    @Test
    fun buttonState_tracksIndependentButtons() {
        val state = ButtonState()
        assertEquals(0x01.toByte(), state.set(InputMotionPacket.BUTTON_LEFT, true))
        assertEquals(0x03.toByte(), state.set(InputMotionPacket.BUTTON_RIGHT, true))
        assertEquals(0x02.toByte(), state.set(InputMotionPacket.BUTTON_LEFT, false))
        assertEquals(0x00.toByte(), state.set(InputMotionPacket.BUTTON_RIGHT, false))
    }

    @Test
    fun subpixelAccumulator_keepsSlowMovement() {
        val acc = SubpixelAccumulator()
        // Ten 0.3 px moves would all round to 0 without accumulation.
        val total = (1..10).sumOf { acc.add(0.3f) }
        assertEquals(3, total)

        val neg = SubpixelAccumulator()
        assertEquals(-3, (1..10).sumOf { neg.add(-0.3f) })
    }

    @Test
    fun pinInput_acceptsCommonFormats() {
        assertEquals(482913, parsePin("482913"))
        assertEquals(482913, parsePin("482 913"))
        assertEquals(1234, parsePin("001234"))
        assertNull(parsePin("12345"))
        assertNull(parsePin("12a456"))
    }

    @Test
    fun mediaKeys_useRealWindowsVirtualKeyCodes() {
        assertEquals(0xB3, VirtualKey.MEDIA_PLAY_PAUSE.toInt())
        assertEquals(0xAF, VirtualKey.VOLUME_UP.toInt())
        assertEquals(0x67, VirtualKey.numpadDigit(7).toInt())
    }

    @Test
    fun gyro_deadzoneSuppressesTremor() {
        assertEquals(0f to 0f, GyroAirMouse.toPointerDelta(0.01f, -0.02f, 30f, 0.03f))
        val (dx, dy) = GyroAirMouse.toPointerDelta(0.5f, -0.2f, 30f, 0.03f)
        assertEquals(-15f, dx, 0.001f) // turning left moves the pointer left
        assertEquals(6f, dy, 0.001f)
    }
}
