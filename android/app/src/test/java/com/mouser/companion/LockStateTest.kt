package com.mouser.companion

import com.mouser.companion.model.LockState
import org.junit.Assert.*
import org.junit.Test

class LockStateTest {

    @Test
    fun whenNotEnabled_doesNotLock() {
        var currentTime = 1_000_000L
        val lock = LockState(timeoutMs = 30_000L, clock = { currentTime })
        lock.isEnabled = false

        lock.onBackgrounded()
        currentTime += 60_000L // 60 seconds later
        lock.onForegrounded()

        assertFalse(lock.isLocked)
    }

    @Test
    fun whenEnabled_underTimeout_doesNotLock() {
        var currentTime = 1_000_000L
        val lock = LockState(timeoutMs = 30_000L, clock = { currentTime })
        lock.isEnabled = true

        lock.onBackgrounded()
        currentTime += 10_000L // 10 seconds later
        lock.onForegrounded()

        assertFalse(lock.isLocked)
    }

    @Test
    fun whenEnabled_overTimeout_locks() {
        var currentTime = 1_000_000L
        val lock = LockState(timeoutMs = 30_000L, clock = { currentTime })
        lock.isEnabled = true

        lock.onBackgrounded()
        currentTime += 35_000L // 35 seconds later (> 30s)
        lock.onForegrounded()

        assertTrue(lock.isLocked)
    }

    @Test
    fun unlock_clearsLockedState() {
        var currentTime = 1_000_000L
        val lock = LockState(timeoutMs = 30_000L, clock = { currentTime })
        lock.isEnabled = true

        lock.onBackgrounded()
        currentTime += 40_000L
        lock.onForegrounded()
        assertTrue(lock.isLocked)

        lock.unlock()
        assertFalse(lock.isLocked)
    }

    @Test
    fun lock_forcesLock_whenEnabled() {
        val lock = LockState(timeoutMs = 30_000L)
        lock.isEnabled = true

        lock.lock()
        assertTrue(lock.isLocked)
    }
}
