package com.mouser.companion.network

import java.io.InputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.security.MessageDigest
import java.util.UUID

object FileTransferClient {
    const val MAX_FILE_BYTES = 2L * 1024 * 1024 * 1024 // 2 GB limit per G3 card
    const val CHUNK_SIZE = 64 * 1024 // 64 KB

    const val FRAME_TYPE_FILE_START: Byte = 2
    const val FRAME_TYPE_FILE_CHUNK: Byte = 3
    const val FRAME_TYPE_FILE_END: Byte = 4

    fun encodeHeader(transferId: String, fileName: String, totalBytes: Long, sha256Hex: String): ByteArray {
        val s = "$transferId\n$fileName\n$totalBytes\n$sha256Hex"
        return s.toByteArray(Charsets.UTF_8)
    }

    fun computeSha256(stream: InputStream): Pair<Long, String> {
        val digest = MessageDigest.getInstance("SHA-256")
        val buffer = ByteArray(CHUNK_SIZE)
        var total = 0L
        while (true) {
            val read = stream.read(buffer)
            if (read == -1) break
            total += read
            digest.update(buffer, 0, read)
        }
        val hex = digest.digest().joinToString("") { "%02x".format(it) }
        return Pair(total, hex)
    }

    fun sendFile(
        hostAddress: String,
        controlPort: Int,
        pairKey: ByteArray,
        ownDeviceId: String,
        fileName: String,
        totalBytes: Long,
        sha256Hex: String,
        openInputStream: () -> InputStream,
        onProgress: ((sentBytes: Long, totalBytes: Long) -> Unit)? = null
    ) {
        if (totalBytes > MAX_FILE_BYTES) {
            throw IllegalArgumentException("File size exceeds 2 GB limit.")
        }

        Socket().use { socket ->
            socket.soTimeout = 30000
            socket.connect(InetSocketAddress(hostAddress, controlPort), 5000)
            val out = socket.getOutputStream()
            val inp = socket.getInputStream()

            // 1. Send FileStart
            val transferId = UUID.randomUUID().toString().replace("-", "")
            val headerBytes = encodeHeader(transferId, fileName, totalBytes, sha256Hex)
            val sealedHeader = ClipboardBridge.encryptContainer(headerBytes, pairKey)
            ClipboardBridge.writePlainFrame(out, ownDeviceId, FRAME_TYPE_FILE_START, sealedHeader)

            // 2. Stream Chunks
            openInputStream().use { fileIn ->
                val buffer = ByteArray(CHUNK_SIZE)
                var sent = 0L
                while (true) {
                    val read = fileIn.read(buffer)
                    if (read == -1) break
                    val chunk = if (read == buffer.size) buffer else buffer.copyOfRange(0, read)
                    val sealedChunk = ClipboardBridge.encryptContainer(chunk, pairKey)
                    ClipboardBridge.writePlainFrame(out, ownDeviceId, FRAME_TYPE_FILE_CHUNK, sealedChunk)
                    sent += read
                    onProgress?.invoke(sent, totalBytes)
                }
            }

            // 3. Send FileEnd
            val sealedEnd = ClipboardBridge.encryptContainer(ByteArray(0), pairKey)
            ClipboardBridge.writePlainFrame(out, ownDeviceId, FRAME_TYPE_FILE_END, sealedEnd)

            // 4. Read final ack from host (1 = verified & saved, 2 = declined)
            val finalAck = inp.read()
            if (finalAck == 2) throw IllegalStateException("File was declined by the computer.")
            if (finalAck != 1) throw IllegalStateException("Computer did not accept or verify the file (ack=$finalAck).")
        }
    }
}
