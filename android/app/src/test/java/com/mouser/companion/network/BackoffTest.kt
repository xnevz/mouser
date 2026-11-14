package com.mouser.companion.network

import org.junit.Assert.assertEquals
import org.junit.Test

class BackoffTest {

    @Test
    fun defaultProgression_is1s2s4s8sCapped() {
        val backoff = Backoff()
        assertEquals(0, backoff.attempts)
        assertEquals(1000L, backoff.currentDelay)

        assertEquals(1000L, backoff.nextDelayMs())
        assertEquals(1, backoff.attempts)
        assertEquals(2000L, backoff.currentDelay)

        assertEquals(2000L, backoff.nextDelayMs())
        assertEquals(2, backoff.attempts)
        assertEquals(4000L, backoff.currentDelay)

        assertEquals(4000L, backoff.nextDelayMs())
        assertEquals(3, backoff.attempts)
        assertEquals(8000L, backoff.currentDelay)

        assertEquals(8000L, backoff.nextDelayMs())
        assertEquals(4, backoff.attempts)
        assertEquals(8000L, backoff.currentDelay)

        // Capped at 8000ms
        assertEquals(8000L, backoff.nextDelayMs())
        assertEquals(5, backoff.attempts)
        assertEquals(8000L, backoff.currentDelay)
    }

    @Test
    fun reset_restoresInitialState() {
        val backoff = Backoff()
        backoff.nextDelayMs()
        backoff.nextDelayMs()
        backoff.nextDelayMs()
        assertEquals(3, backoff.attempts)
        assertEquals(8000L, backoff.currentDelay)

        backoff.reset()
        assertEquals(0, backoff.attempts)
        assertEquals(1000L, backoff.currentDelay)
        assertEquals(1000L, backoff.nextDelayMs())
        assertEquals(1, backoff.attempts)
    }

    @Test
    fun customParameters_respected() {
        val backoff = Backoff(initialDelayMs = 500L, maxDelayMs = 2000L, multiplier = 2.0)
        assertEquals(500L, backoff.nextDelayMs())
        assertEquals(1000L, backoff.nextDelayMs())
        assertEquals(2000L, backoff.nextDelayMs())
        assertEquals(2000L, backoff.nextDelayMs())
    }

    @Test
    fun customMultiplier_appliesCorrectly() {
        val backoff = Backoff(initialDelayMs = 1000L, maxDelayMs = 10000L, multiplier = 3.0)
        assertEquals(1000L, backoff.nextDelayMs())
        assertEquals(3000L, backoff.nextDelayMs())
        assertEquals(9000L, backoff.nextDelayMs())
        assertEquals(10000L, backoff.nextDelayMs())
    }
}
