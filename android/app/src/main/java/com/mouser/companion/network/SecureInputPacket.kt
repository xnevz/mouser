package com.mouser.companion.network

import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * 51-byte authenticated UDP datagram for Protocol v2 low-latency encrypted input (Task F5).
 */
object SecureInputPacket {
    const val PACKET_SIZE = 51
    const val VERSION: Byte = 2
    const val SENDER_ID_HASH_SIZE = 4
    const val CIPHER_AND_TAG_SIZE = 30
    const val HEADER_SIZE = 21

    fun hashDeviceId(deviceId: String): Int {
        var hash = 2166136261L.toInt()
        for (b in deviceId.toByteArray(Charsets.UTF_8)) {
            hash = hash xor (b.toInt() and 0xFF)
            hash *= 16777619
        }
        return hash
    }

    fun computeSenderIdHash(deviceId: String): ByteArray {
        val bytes = ByteArray(SENDER_ID_HASH_SIZE)
        ByteBuffer.wrap(bytes).order(ByteOrder.BIG_ENDIAN).putInt(hashDeviceId(deviceId))
        return bytes
    }

    fun write(
        senderIdHash: ByteArray,
        sessionId: Long,
        counter: Long,
        cipherAndTag: ByteArray
    ): ByteArray {
        require(senderIdHash.size == SENDER_ID_HASH_SIZE) { "SenderIdHash must be 4 bytes" }
        require(cipherAndTag.size == CIPHER_AND_TAG_SIZE) { "CipherAndTag must be 30 bytes" }

        val buffer = ByteArray(PACKET_SIZE)
        buffer[0] = VERSION
        System.arraycopy(senderIdHash, 0, buffer, 1, 4)

        val bb = ByteBuffer.wrap(buffer).order(ByteOrder.BIG_ENDIAN)
        bb.position(5)
        bb.putLong(sessionId)
        bb.putLong(counter)

        System.arraycopy(cipherAndTag, 0, buffer, 21, 30)
        return buffer
    }

    fun tryRead(data: ByteArray): DecodedSecureHeader? {
        if (data.size != PACKET_SIZE || data[0] != VERSION) return null

        val bb = ByteBuffer.wrap(data).order(ByteOrder.BIG_ENDIAN)
        val senderIdHash = data.copyOfRange(1, 5)
        bb.position(5)
        val sessionId = bb.long
        val counter = bb.long
        val cipherAndTag = data.copyOfRange(21, 51)

        return DecodedSecureHeader(senderIdHash, sessionId, counter, cipherAndTag)
    }

    data class DecodedSecureHeader(
        val senderIdHash: ByteArray,
        val sessionId: Long,
        val counter: Long,
        val cipherAndTag: ByteArray
    ) {
        override fun equals(other: Any?): Boolean {
            if (this === other) return true
            if (javaClass != other?.javaClass) return false
            other as DecodedSecureHeader
            return senderIdHash.contentEquals(other.senderIdHash) &&
                    sessionId == other.sessionId &&
                    counter == other.counter &&
                    cipherAndTag.contentEquals(other.cipherAndTag)
        }

        override fun hashCode(): Int {
            var result = senderIdHash.contentHashCode()
            result = 31 * result + sessionId.hashCode()
            result = 31 * result + counter.hashCode()
            result = 31 * result + cipherAndTag.contentHashCode()
            return result
        }
    }
}
