package com.mouser.companion

import com.mouser.companion.network.FileTransferClient
import org.junit.Assert.*
import org.junit.Test
import java.io.ByteArrayInputStream

class FileTransferClientTest {

    @Test
    fun encodeHeader_formatsExpectedNewlineSeparatedString() {
        val transferId = "12345678"
        val fileName = "photo.jpg"
        val totalBytes = 1048576L
        val sha256Hex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"

        val headerBytes = FileTransferClient.encodeHeader(transferId, fileName, totalBytes, sha256Hex)
        val text = String(headerBytes, Charsets.UTF_8)
        val parts = text.split("\n")

        assertEquals(4, parts.size)
        assertEquals(transferId, parts[0])
        assertEquals(fileName, parts[1])
        assertEquals(totalBytes.toString(), parts[2])
        assertEquals(sha256Hex, parts[3])
    }

    @Test
    fun computeSha256_computesCorrectChecksumForKnownInput() {
        val input = "Hello, Mouser file transfer!".toByteArray(Charsets.UTF_8)
        val (size, hash) = FileTransferClient.computeSha256(ByteArrayInputStream(input))

        assertEquals(input.size.toLong(), size)
        assertNotNull(hash)
        assertEquals(64, hash.length)
    }

    @Test
    fun fileOver2GB_isRefusedWithException() {
        assertThrows(IllegalArgumentException::class.java) {
            FileTransferClient.sendFile(
                hostAddress = "127.0.0.1",
                controlPort = 58492,
                pairKey = ByteArray(32),
                ownDeviceId = "phone",
                fileName = "huge.iso",
                totalBytes = FileTransferClient.MAX_FILE_BYTES + 1,
                sha256Hex = "hash",
                openInputStream = { ByteArrayInputStream(ByteArray(0)) }
            )
        }
    }
}
