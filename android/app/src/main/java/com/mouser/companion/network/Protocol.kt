package com.mouser.companion.network

import com.mouser.companion.model.DiscoveredHost
import org.json.JSONException
import org.json.JSONObject
import kotlin.math.roundToInt

/** Parses the desktop's discovery beacon: `MOUS_DISC:{"Id":..,"Host":..,"Os":..,"MPort":..,"CPort":..}`. */
object BeaconParser {
    const val PREFIX = "MOUS_DISC:"

    fun parse(payload: String, senderIp: String, now: Long = System.currentTimeMillis()): DiscoveredHost? {
        if (!payload.startsWith(PREFIX)) return null
        return try {
            val json = JSONObject(payload.substring(PREFIX.length))
            val id = json.optString("Id")
            val port = json.optInt("MPort", 0)
            if (id.isEmpty() || port !in 1..65535) return null
            DiscoveredHost(
                deviceId = id,
                hostName = json.optString("Host").ifEmpty { senderIp },
                operatingSystem = json.optString("Os"),
                ipAddress = senderIp,
                port = port,
                lastSeenTimestamp = now
            )
        } catch (_: JSONException) {
            null
        }
    }
}

/** Monotonic 16-bit sequence numbers with wrap-around, as the host's sequence filter expects. */
class SequenceCounter(start: Int = 0) {
    private var value = start and 0xFFFF

    @Synchronized
    fun next(): Short {
        value = (value + 1) and 0xFFFF
        return value.toShort()
    }
}

/** Current mouse button mask. Every motion/button packet carries the full mask so the host can't miss a release. */
class ButtonState {
    @Volatile
    var mask: Byte = 0
        private set

    @Synchronized
    fun set(button: Byte, isDown: Boolean): Byte {
        val m = mask.toInt()
        mask = (if (isDown) m or button.toInt() else m and button.toInt().inv()).toByte()
        return mask
    }

    @Synchronized
    fun clear() {
        mask = 0
    }
}

/**
 * Converts fractional deltas into whole steps without losing the remainder, so slow finger or
 * gyro movement still moves the pointer instead of rounding to zero every frame.
 */
class SubpixelAccumulator {
    private var remainder = 0f

    fun add(delta: Float): Int {
        val total = remainder + delta
        val whole = if (total >= 0) total.toInt() else -((-total).toInt())
        remainder = total - whole
        return whole
    }

    fun reset() {
        remainder = 0f
    }
}

/** Parses a PIN typed as "123 456", "123-456" or "123456". */
fun parsePin(text: String): Int? {
    val digits = text.filter { it.isDigit() }
    if (digits.length != 6 || digits.length != text.count { !it.isWhitespace() && it != '-' }) return null
    return digits.toInt()
}

fun Float.toShortClamped(): Short = roundToInt().coerceIn(Short.MIN_VALUE.toInt(), Short.MAX_VALUE.toInt()).toShort()

fun Int.toShortClamped(): Short = coerceIn(Short.MIN_VALUE.toInt(), Short.MAX_VALUE.toInt()).toShort()
