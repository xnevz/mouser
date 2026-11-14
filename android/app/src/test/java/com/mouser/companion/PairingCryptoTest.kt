package com.mouser.companion

import com.mouser.companion.network.PairingHandshake
import com.mouser.companion.network.SecureInputPacket
import com.mouser.companion.network.SessionCrypto
import org.junit.Assert.*
import org.junit.Test
import java.nio.ByteBuffer
import java.nio.ByteOrder

class PairingCryptoTest {

    private fun bytesToHex(bytes: ByteArray): String {
        return bytes.joinToString("") { "%02X".format(it) }
    }

    @Test
    fun knownAnswerVectors_matchesCSharpExactly() {
        val shared = ByteArray(32) { 0x42.toByte() }
        val salt = "device-a:device-b".toByteArray(Charsets.UTF_8)
        val okm = PairingHandshake.hkdf(shared, 64, salt, "mouser-pair-v2".toByteArray(Charsets.UTF_8))
        val pairKey = okm.copyOfRange(0, 32)

        val codeBytes = okm.copyOfRange(32, 36)
        val codeNumber = (ByteBuffer.wrap(codeBytes).order(ByteOrder.BIG_ENDIAN).int.toLong() and 0xFFFFFFFFL) % 1_000_000L
        val confirmationCode = String.format("%03d %03d", codeNumber / 1000, codeNumber % 1000)

        assertEquals("C1443AA2A327BA87B58D82F938255D9389842B5AF2409672B416731F04448E1E", bytesToHex(pairKey))
        assertEquals("543 901", confirmationCode)

        val session = SessionCrypto(pairKey, isInitiator = true, sessionId = 0x0102030405060708L)
        val plaintext = byteArrayOf(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14)
        val (counter, sealedMsg) = session.seal(plaintext)

        assertEquals(1L, counter)
        assertEquals("34314783A3478246C66E2A647D4EEC15C8604327B0DBCDB23DEC7670FA08", bytesToHex(sealedMsg))

        // Recipient opens it
        val bobSession = SessionCrypto(pairKey, isInitiator = false, sessionId = 0x0102030405060708L)
        val decrypted = bobSession.tryOpen(counter, sealedMsg)
        assertNotNull(decrypted)
        assertArrayEquals(plaintext, decrypted)
    }

    @Test
    fun handshake_aliceAndBobDeriveSameKeyAndCode() {
        val alice = PairingHandshake.start()
        val bob = PairingHandshake.start()

        val aliceResult = alice.complete(bob.publicKey, "phone-id", "pc-id")
        val bobResult = bob.complete(alice.publicKey, "pc-id", "phone-id")

        assertEquals(32, aliceResult.pairKey.size)
        assertEquals(32, bobResult.pairKey.size)
        assertArrayEquals(aliceResult.pairKey, bobResult.pairKey)
        assertEquals(aliceResult.confirmationCode, bobResult.confirmationCode)
        assertTrue(aliceResult.confirmationCode.matches(Regex("""^\d{3} \d{3}$""")))
    }

    @Test
    fun sessionCrypto_antiReplayWindow_blocksReplays() {
        val pairKey = ByteArray(32) { 0x07.toByte() }
        val alice = SessionCrypto(pairKey, isInitiator = true, sessionId = 42L)
        val bob = SessionCrypto(pairKey, isInitiator = false, sessionId = 42L)

        val (c1, p1) = alice.seal("hello".toByteArray())
        val (c2, p2) = alice.seal("world".toByteArray())

        // Receive out of order
        val decrypted2 = bob.tryOpen(c2, p2)
        assertNotNull(decrypted2)
        assertArrayEquals("world".toByteArray(), decrypted2)

        val decrypted1 = bob.tryOpen(c1, p1)
        assertNotNull(decrypted1)
        assertArrayEquals("hello".toByteArray(), decrypted1)

        // Replay of p1 must fail
        assertNull(bob.tryOpen(c1, p1))
    }

    @Test
    fun secureInputPacket_serializationAndDeserialization() {
        val senderHash = SecureInputPacket.computeSenderIdHash("test-device")
        val sessionId = 9876543210L
        val counter = 55L
        val cipherAndTag = ByteArray(30) { (it + 1).toByte() }

        val packetBytes = SecureInputPacket.write(senderHash, sessionId, counter, cipherAndTag)
        assertEquals(51, packetBytes.size)

        val parsed = SecureInputPacket.tryRead(packetBytes)
        assertNotNull(parsed)
        assertArrayEquals(senderHash, parsed!!.senderIdHash)
        assertEquals(sessionId, parsed.sessionId)
        assertEquals(counter, parsed.counter)
        assertArrayEquals(cipherAndTag, parsed.cipherAndTag)
    }
}
