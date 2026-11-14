package com.mouser.companion.network

/**
 * Exponential backoff with configurable initial and max delays.
 * Default progression: 1s, 2s, 4s, 8s (capped at [maxDelayMs]).
 */
class Backoff(
    val initialDelayMs: Long = 1000L,
    val maxDelayMs: Long = 8000L,
    val multiplier: Double = 2.0
) {
    private var currentDelayMs: Long = initialDelayMs
    var attempts: Int = 0
        private set

    val currentDelay: Long
        get() = currentDelayMs

    /**
     * Returns the delay to wait for the current attempt, then increases
     * the delay for the next attempt up to [maxDelayMs].
     */
    fun nextDelayMs(): Long {
        val delay = currentDelayMs
        attempts++
        val next = (currentDelayMs * multiplier).toLong()
        currentDelayMs = if (next > maxDelayMs || next < 0) maxDelayMs else next
        return delay
    }

    /**
     * Resets delay back to [initialDelayMs] and attempt count to 0.
     */
    fun reset() {
        currentDelayMs = initialDelayMs
        attempts = 0
    }
}
