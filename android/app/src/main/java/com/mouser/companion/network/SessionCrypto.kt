package com.mouser.companion.network

import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.atomic.AtomicLong
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/**
 * Directional AES-256-GCM symmetric session encryption with 64-bit sliding window replay protection (Task F5).
 */
class SessionCrypto(
    pairKey: ByteArray,
    isInitiator: Boolean,
    sessionId: Long
) {
    private val sendKey: ByteArray
    private val recvKey: ByteArray
    private val sendCounter = AtomicLong(0L)
    private var highestReceivedCounter: Long = 0L
    private var window: Long = 0L
    private val lock = Any()

    init {
        require(pairKey.size == 32) { "PairKey must be exactly 32 bytes" }

        val salt = ByteBuffer.allocate(8).order(ByteOrder.BIG_ENDIAN).putLong(sessionId).array()
        val ka2b = PairingHandshake.hkdf(pairKey, 32, salt, "mouser-a2b".toByteArray(Charsets.UTF_8))
        val kb2a = PairingHandshake.hkdf(pairKey, 32, salt, "mouser-b2a".toByteArray(Charsets.UTF_8))

        sendKey = if (isInitiator) ka2b else kb2a
        recvKey = if (isInitiator) kb2a else ka2b
    }

    fun seal(plaintext: ByteArray): Pair<Long, ByteArray> {
        val counter = sendCounter.incrementAndGet()
        val nonce = ByteBuffer.allocate(12).order(ByteOrder.BIG_ENDIAN)
            .putInt(0)
            .putLong(counter)
            .array()

        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        val spec = GCMParameterSpec(128, nonce)
        cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(sendKey, "AES"), spec)
        val cipherAndTag = cipher.doFinal(plaintext)

        return Pair(counter, cipherAndTag)
    }

    fun tryOpen(counter: Long, cipherAndTag: ByteArray): ByteArray? {
        if (counter <= 0 || cipherAndTag.size < 16) return null

        synchronized(lock) {
            if (highestReceivedCounter > 0L && counter <= highestReceivedCounter) {
                val diff = highestReceivedCounter - counter
                if (diff >= 64) return null // outside sliding window
                val bit = 1L shl diff.toInt()
                if ((window and bit) != 0L) return null // replayed packet
            }
        }

        val nonce = ByteBuffer.allocate(12).order(ByteOrder.BIG_ENDIAN)
            .putInt(0)
            .putLong(counter)
            .array()

        val decrypted = try {
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            val spec = GCMParameterSpec(128, nonce)
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(recvKey, "AES"), spec)
            cipher.doFinal(cipherAndTag)
        } catch (_: Exception) {
            return null
        }

        synchronized(lock) {
            if (highestReceivedCounter == 0L) {
                highestReceivedCounter = counter
                window = 1L
            } else if (counter > highestReceivedCounter) {
                val diff = counter - highestReceivedCounter
                window = if (diff < 64) {
                    (window shl diff.toInt()) or 1L
                } else {
                    1L
                }
                highestReceivedCounter = counter
            } else {
                val diff = highestReceivedCounter - counter
                val bit = 1L shl diff.toInt()
                window = window or bit
            }
        }

        return decrypted
    }
}
