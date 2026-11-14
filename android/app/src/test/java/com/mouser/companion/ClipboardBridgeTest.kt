package com.mouser.companion

import com.mouser.companion.network.ClipboardBridge
import org.junit.Assert.*
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream

class ClipboardBridgeTest {

    @Test
    fun bit7Encoding_roundtripsCorrectly() {
        val testValues = listOf(0, 1, 127, 128, 255, 300, 16384, 2097151)
        for (v in testValues) {
            val baos = ByteArrayOutputStream()
            ClipboardBridge.write7BitEncodedInt(baos, v)
            val bytes = baos.toByteArray()
            val restored = ClipboardBridge.read7BitEncodedInt(ByteArrayInputStream(bytes))
            assertEquals(v, restored)
        }
    }

    @Test
    fun stringSerialization_roundtripsCorrectly() {
        val strings = listOf("", "a", "hello world", "Special characters: é, ç, 日本語, 🚀")
        for (s in strings) {
            val baos = ByteArrayOutputStream()
            ClipboardBridge.writeString(baos, s)
            val restored = ClipboardBridge.readString(ByteArrayInputStream(baos.toByteArray()))
            assertEquals(s, restored)
        }
    }

    @Test
    fun payloadSerialization_deserializesText() {
        val text = "Copy text from Android companion!"
        val bytes = ClipboardBridge.serializePayload(text, "Pixel 8")
        val restoredText = ClipboardBridge.deserializeText(bytes)
        assertEquals(text, restoredText)
    }

    @Test
    fun encryptAndDecryptContainer_roundtripsExactBytes() {
        val key = ByteArray(32) { (it + 1).toByte() }
        val plaintext = "Secret clipboard data".toByteArray(Charsets.UTF_8)
        val container = ClipboardBridge.encryptContainer(plaintext, key)

        assertEquals(12 + 16 + plaintext.size, container.size)
        val decrypted = ClipboardBridge.decryptContainer(container, key)
        assertArrayEquals(plaintext, decrypted)
    }

    @Test(expected = Exception::class)
    fun decryptContainer_withWrongKey_fails() {
        val key1 = ByteArray(32) { 1 }
        val key2 = ByteArray(32) { 2 }
        val plaintext = "Data".toByteArray(Charsets.UTF_8)
        val container = ClipboardBridge.encryptContainer(plaintext, key1)
        ClipboardBridge.decryptContainer(container, key2)
    }

    @Test
    fun frameFormat_roundtripsCorrectly() {
        val deviceId = "android-phone-123"
        val payload = "payload data".toByteArray(Charsets.UTF_8)
        val baos = ByteArrayOutputStream()
        ClipboardBridge.writePlainFrame(baos, deviceId, ClipboardBridge.FRAME_TYPE_CLIP, payload)

        val bais = ByteArrayInputStream(baos.toByteArray())
        val frame = ClipboardBridge.readFrame(bais)
        assertNotNull(frame)
        assertEquals(ClipboardBridge.FRAME_TYPE_CLIP, frame!!.first)
        assertArrayEquals(payload, frame.second)
    }
}
