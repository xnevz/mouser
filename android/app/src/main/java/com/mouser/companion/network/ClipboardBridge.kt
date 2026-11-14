package com.mouser.companion.network

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.EOFException
import java.io.InputStream
import java.io.OutputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.security.SecureRandom
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

object ClipboardBridge {
    private val MAGIC = byteArrayOf(0x4D, 0x43, 0x48, 0x31) // "MCH1"
    const val FRAME_TYPE_CLIP: Byte = 1
    const val FRAME_TYPE_CLIP_REQUEST: Byte = 12

    fun write7BitEncodedInt(out: OutputStream, value: Int) {
        var v = value
        while (v >= 0x80) {
            out.write((v or 0x80) and 0xFF)
            v = v ushr 7
        }
        out.write(v and 0xFF)
    }

    fun read7BitEncodedInt(input: InputStream): Int {
        var count = 0
        var shift = 0
        while (shift != 35) {
            val b = input.read()
            if (b == -1) throw EOFException("Unexpected end of stream")
            count = count or ((b and 0x7F) shl shift)
            shift += 7
            if ((b and 0x80) == 0) return count
        }
        throw IllegalArgumentException("Invalid 7-bit encoded int")
    }

    fun writeString(out: OutputStream, s: String) {
        val bytes = s.toByteArray(Charsets.UTF_8)
        write7BitEncodedInt(out, bytes.size)
        out.write(bytes)
    }

    fun readString(input: InputStream): String {
        val len = read7BitEncodedInt(input)
        val bytes = ByteArray(len)
        var read = 0
        while (read < len) {
            val n = input.read(bytes, read, len - read)
            if (n == -1) throw EOFException("Unexpected end of stream while reading string")
            read += n
        }
        return String(bytes, Charsets.UTF_8)
    }

    fun serializePayload(text: String, deviceName: String = "Android"): ByteArray {
        val baos = ByteArrayOutputStream()
        // Id: string
        writeString(baos, UUID.randomUUID().toString())
        // TimestampUtc: long (little-endian)
        val ts = ByteBuffer.allocate(8).order(ByteOrder.LITTLE_ENDIAN).putLong(System.currentTimeMillis()).array()
        baos.write(ts)
        // ContentType: int (0 = PlainText, little-endian)
        val ct = ByteBuffer.allocate(4).order(ByteOrder.LITTLE_ENDIAN).putInt(0).array()
        baos.write(ct)
        // TextContent: string
        writeString(baos, text)
        // HtmlContent: string
        writeString(baos, "")
        // SourceDevice: string
        writeString(baos, deviceName)
        // IsFavorite: boolean
        baos.write(0)
        // SourceApp: string
        writeString(baos, "")
        // MediaPath: string
        writeString(baos, "")
        // MediaHash: string
        writeString(baos, "")
        // Width: int
        baos.write(ByteBuffer.allocate(4).order(ByteOrder.LITTLE_ENDIAN).putInt(0).array())
        // Height: int
        baos.write(ByteBuffer.allocate(4).order(ByteOrder.LITTLE_ENDIAN).putInt(0).array())
        // MediaBytes: long
        baos.write(ByteBuffer.allocate(8).order(ByteOrder.LITTLE_ENDIAN).putLong(0L).array())

        return baos.toByteArray()
    }

    fun deserializeText(bytes: ByteArray): String {
        val bais = ByteArrayInputStream(bytes)
        // Id
        readString(bais)
        // TimestampUtc (8 bytes)
        val tsBuf = ByteArray(8)
        bais.read(tsBuf)
        // ContentType (4 bytes)
        val ctBuf = ByteArray(4)
        bais.read(ctBuf)
        // TextContent
        return readString(bais)
    }

    /**
     * Encrypts plaintext into container: [Nonce (12B)][Tag (16B)][Ciphertext]
     * matching Mouser.Core.Clipboard.ClipboardCryptoEngine.
     */
    fun encryptContainer(plaintext: ByteArray, key: ByteArray): ByteArray {
        require(key.size == 32) { "Key must be 32 bytes" }
        val nonce = ByteArray(12).also { SecureRandom().nextBytes(it) }
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, nonce))
        val cipherAndTag = cipher.doFinal(plaintext)
        val ciphertextLen = cipherAndTag.size - 16
        val tag = cipherAndTag.copyOfRange(ciphertextLen, cipherAndTag.size)
        val ciphertext = cipherAndTag.copyOfRange(0, ciphertextLen)

        val result = ByteArray(12 + 16 + ciphertextLen)
        System.arraycopy(nonce, 0, result, 0, 12)
        System.arraycopy(tag, 0, result, 12, 16)
        System.arraycopy(ciphertext, 0, result, 28, ciphertextLen)
        return result
    }

    /**
     * Decrypts container: [Nonce (12B)][Tag (16B)][Ciphertext]
     * matching Mouser.Core.Clipboard.ClipboardCryptoEngine.
     */
    fun decryptContainer(container: ByteArray, key: ByteArray): ByteArray {
        require(key.size == 32) { "Key must be 32 bytes" }
        require(container.size >= 28) { "Container too short" }
        val nonce = container.copyOfRange(0, 12)
        val tag = container.copyOfRange(12, 28)
        val ciphertext = container.copyOfRange(28, container.size)

        val cipherAndTag = ByteArray(ciphertext.size + 16)
        System.arraycopy(ciphertext, 0, cipherAndTag, 0, ciphertext.size)
        System.arraycopy(tag, 0, cipherAndTag, ciphertext.size, 16)

        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, nonce))
        return cipher.doFinal(cipherAndTag)
    }

    fun writePlainFrame(out: OutputStream, deviceId: String, type: Byte, body: ByteArray) {
        val sender = deviceId.toByteArray(Charsets.UTF_8)
        val head = ByteArray(MAGIC.size + 1 + 1 + sender.size + 4)
        System.arraycopy(MAGIC, 0, head, 0, MAGIC.size)
        head[4] = type
        head[5] = sender.size.toByte()
        System.arraycopy(sender, 0, head, 6, sender.size)
        val lenBytes = ByteBuffer.allocate(4).order(ByteOrder.BIG_ENDIAN).putInt(body.size).array()
        System.arraycopy(lenBytes, 0, head, 6 + sender.size, 4)
        out.write(head)
        if (body.isNotEmpty()) {
            out.write(body)
        }
        out.flush()
    }

    fun readFrame(input: InputStream): Pair<Byte, ByteArray>? {
        val head = ByteArray(6)
        var read = 0
        while (read < 6) {
            val n = input.read(head, read, 6 - read)
            if (n == -1) {
                if (read == 0) return null
                throw EOFException("Unexpected end of stream")
            }
            read += n
        }

        for (i in 0..3) {
            if (head[i] != MAGIC[i]) throw IllegalArgumentException("Not a Mouser frame")
        }
        val type = head[4]
        val senderLen = head[5].toInt() and 0xFF

        val senderBytes = ByteArray(senderLen)
        read = 0
        while (read < senderLen) {
            val n = input.read(senderBytes, read, senderLen - read)
            if (n == -1) throw EOFException("Unexpected EOF reading sender")
            read += n
        }

        val lenBytes = ByteArray(4)
        read = 0
        while (read < 4) {
            val n = input.read(lenBytes, read, 4 - read)
            if (n == -1) throw EOFException("Unexpected EOF reading length")
            read += n
        }
        val length = ByteBuffer.wrap(lenBytes).order(ByteOrder.BIG_ENDIAN).int

        val body = ByteArray(length)
        read = 0
        while (read < length) {
            val n = input.read(body, read, length - read)
            if (n == -1) throw EOFException("Unexpected EOF reading body")
            read += n
        }

        return Pair(type, body)
    }

    fun sendClip(hostAddress: String, controlPort: Int, pairKey: ByteArray, ownDeviceId: String, text: String, deviceName: String = "Android") {
        val payload = serializePayload(text, deviceName)
        val sealed = encryptContainer(payload, pairKey)
        Socket().use { socket ->
            socket.soTimeout = 5000
            socket.connect(InetSocketAddress(hostAddress, controlPort), 5000)
            val out = socket.getOutputStream()
            writePlainFrame(out, ownDeviceId, FRAME_TYPE_CLIP, sealed)
        }
    }

    fun requestClip(hostAddress: String, controlPort: Int, pairKey: ByteArray, ownDeviceId: String): String? {
        Socket().use { socket ->
            socket.soTimeout = 5000
            socket.connect(InetSocketAddress(hostAddress, controlPort), 5000)
            val out = socket.getOutputStream()
            val inp = socket.getInputStream()
            val sealedEmpty = encryptContainer(ByteArray(0), pairKey)
            writePlainFrame(out, ownDeviceId, FRAME_TYPE_CLIP_REQUEST, sealedEmpty)
            val reply = readFrame(inp) ?: return null
            if (reply.first == FRAME_TYPE_CLIP) {
                val decrypted = decryptContainer(reply.second, pairKey)
                return deserializeText(decrypted)
            }
            return null
        }
    }
}
