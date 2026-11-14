package com.mouser.companion.network

import java.math.BigInteger
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.security.*
import java.security.interfaces.ECPrivateKey
import java.security.interfaces.ECPublicKey
import java.security.spec.*
import javax.crypto.KeyAgreement
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

data class PairingResult(val pairKey: ByteArray, val confirmationCode: String) {
    override fun equals(other: Any?): Boolean {
        if (this === other) return true
        if (javaClass != other?.javaClass) return false
        other as PairingResult
        return pairKey.contentEquals(other.pairKey) && confirmationCode == other.confirmationCode
    }

    override fun hashCode(): Int {
        var result = pairKey.contentHashCode()
        result = 31 * result + confirmationCode.hashCode()
        return result
    }
}

/**
 * P-256 ECDH ephemeral key exchange + SAS confirmation code generation (Task F5).
 */
class PairingHandshake private constructor(
    private val keyPair: KeyPair,
    val publicKey: ByteArray
) {
    companion object {
        fun start(): PairingHandshake {
            val kpg = KeyPairGenerator.getInstance("EC")
            kpg.initialize(ECGenParameterSpec("secp256r1"))
            val pair = kpg.generateKeyPair()

            val ecPub = pair.public as ECPublicKey
            val w = ecPub.w
            val raw = ByteArray(65)
            raw[0] = 0x04 // uncompressed ANSI X9.62
            padTo32(w.affineX.toByteArray()).copyInto(raw, 1)
            padTo32(w.affineY.toByteArray()).copyInto(raw, 33)

            return PairingHandshake(pair, raw)
        }

        fun padTo32(bytes: ByteArray): ByteArray {
            if (bytes.size == 32) return bytes
            val padded = ByteArray(32)
            if (bytes.size > 32) {
                // Strip leading sign zero byte if present
                System.arraycopy(bytes, bytes.size - 32, padded, 0, 32)
            } else {
                System.arraycopy(bytes, 0, padded, 32 - bytes.size, bytes.size)
            }
            return padded
        }

        fun hkdf(ikm: ByteArray, length: Int, salt: ByteArray, info: ByteArray): ByteArray {
            // Step 1: Extract PRK = HMAC-SHA256(salt, ikm)
            val effectiveSalt = if (salt.isEmpty()) ByteArray(32) else salt
            val mac = Mac.getInstance("HmacSHA256")
            mac.init(SecretKeySpec(effectiveSalt, "HmacSHA256"))
            val prk = mac.doFinal(ikm)

            // Step 2: Expand OKM = T(1) || T(2) || ...
            val result = ByteArray(length)
            var generated = 0
            var t = ByteArray(0)
            var round: Byte = 1

            while (generated < length) {
                mac.init(SecretKeySpec(prk, "HmacSHA256"))
                mac.update(t)
                mac.update(info)
                mac.update(round)
                t = mac.doFinal()

                val toCopy = minOf(t.size, length - generated)
                System.arraycopy(t, 0, result, generated, toCopy)
                generated += toCopy
                round++
            }

            return result
        }
    }

    fun complete(peerPublicKey: ByteArray, ownDeviceId: String, peerDeviceId: String): PairingResult {
        require(peerPublicKey.size == 65 && peerPublicKey[0] == 0x04.toByte()) {
            "Invalid peer public key: expected 65-byte uncompressed ANSI X9.62 point"
        }

        val x = BigInteger(1, peerPublicKey.copyOfRange(1, 33))
        val y = BigInteger(1, peerPublicKey.copyOfRange(33, 65))
        val point = ECPoint(x, y)

        val kf = KeyFactory.getInstance("EC")
        val ecPub = keyPair.public as ECPublicKey
        val pubSpec = ECPublicKeySpec(point, ecPub.params)
        val peerKey = kf.generatePublic(pubSpec)

        val ka = KeyAgreement.getInstance("ECDH")
        ka.init(keyPair.private)
        ka.doPhase(peerKey, true)
        val sharedSecret = ka.generateSecret()

        val minId = if (ownDeviceId < peerDeviceId) ownDeviceId else peerDeviceId
        val maxId = if (ownDeviceId < peerDeviceId) peerDeviceId else ownDeviceId
        val salt = "$minId:$maxId".toByteArray(Charsets.UTF_8)

        val okm = hkdf(sharedSecret, 64, salt, "mouser-pair-v2".toByteArray(Charsets.UTF_8))
        val pairKey = okm.copyOfRange(0, 32)

        val codeBytes = okm.copyOfRange(32, 36)
        val codeNumber = (ByteBuffer.wrap(codeBytes).order(ByteOrder.BIG_ENDIAN).int.toLong() and 0xFFFFFFFFL) % 1_000_000L
        val confirmationCode = String.format("%03d %03d", codeNumber / 1000, codeNumber % 1000)

        return PairingResult(pairKey, confirmationCode)
    }
}
