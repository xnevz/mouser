package com.mouser.companion.model

/**
 * Pure state machine managing application lock timing (Task C6).
 * Locks when returning to foreground if backgrounded for more than [timeoutMs].
 */
class LockState(
    val timeoutMs: Long = 30_000L,
    private val clock: () -> Long = { System.currentTimeMillis() }
) {
    var isEnabled: Boolean = false
    var isLocked: Boolean = false
        private set

    private var backgroundTimestamp: Long = 0L

    fun onBackgrounded() {
        if (!isEnabled) return
        backgroundTimestamp = clock()
    }

    fun onForegrounded() {
        if (!isEnabled) return
        if (backgroundTimestamp > 0L) {
            val elapsed = clock() - backgroundTimestamp
            if (elapsed >= timeoutMs) {
                isLocked = true
            }
        }
    }

    fun unlock() {
        isLocked = false
        backgroundTimestamp = 0L
    }

    fun lock() {
        if (isEnabled) {
            isLocked = true
        }
    }
}
