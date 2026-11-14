package com.mouser.companion

import com.mouser.companion.model.ClassifiedGesture
import com.mouser.companion.model.GestureClassifier
import com.mouser.companion.model.PointerSample
import org.junit.Assert.*
import org.junit.Test

class GestureClassifierTest {

    @Test
    fun singleFingerMove_producesMoveGesture() {
        val classifier = GestureClassifier(pointerSpeed = 1.0f)
        val p1 = PointerSample(1, 100f, 100f, true, 1000L)
        val p2 = PointerSample(1, 110f, 120f, true, 1016L)

        classifier.processFrame(listOf(p1))
        val gestures = classifier.processFrame(listOf(p2))

        assertEquals(1, gestures.size)
        assertTrue(gestures[0] is ClassifiedGesture.Move)
        val move = gestures[0] as ClassifiedGesture.Move
        assertEquals(10f, move.dx, 0.001f)
        assertEquals(20f, move.dy, 0.001f)
    }

    @Test
    fun tap_producesTapGesture() {
        val classifier = GestureClassifier(tapSlop = 20f, tapTimeoutMs = 300L)
        val p1 = PointerSample(1, 100f, 100f, true, 1000L)
        val p2 = PointerSample(1, 101f, 101f, false, 1100L)

        classifier.processFrame(listOf(p1))
        val gestures = classifier.processFrame(listOf(p2))

        assertEquals(1, gestures.size)
        assertEquals(ClassifiedGesture.Tap, gestures[0])
    }

    @Test
    fun twoFingerTap_producesSecondaryTap() {
        val classifier = GestureClassifier(tapSlop = 20f, tapTimeoutMs = 300L)
        val p1 = PointerSample(1, 100f, 100f, true, 1000L)
        val p2 = PointerSample(2, 140f, 100f, true, 1000L)
        val p1Up = PointerSample(1, 100f, 100f, false, 1100L)
        val p2Up = PointerSample(2, 140f, 100f, false, 1100L)

        classifier.processFrame(listOf(p1, p2))
        val gestures = classifier.processFrame(listOf(p1Up, p2Up))

        assertEquals(1, gestures.size)
        assertEquals(ClassifiedGesture.SecondaryTap, gestures[0])
    }

    @Test
    fun twoFingerScroll_producesScrollGesture() {
        val classifier = GestureClassifier(scrollSpeed = 1.0f, reverseScroll = false)
        val p1 = PointerSample(1, 100f, 100f, true, 1000L)
        val p2 = PointerSample(2, 150f, 100f, true, 1000L)
        val p1Move = PointerSample(1, 100f, 120f, true, 1016L)
        val p2Move = PointerSample(2, 150f, 120f, true, 1016L)

        classifier.processFrame(listOf(p1, p2))
        val gestures = classifier.processFrame(listOf(p1Move, p2Move))

        assertEquals(1, gestures.size)
        assertTrue(gestures[0] is ClassifiedGesture.Scroll)
        val scroll = gestures[0] as ClassifiedGesture.Scroll
        assertEquals(0f, scroll.dx, 0.001f)
        assertEquals(20f, scroll.dy, 0.001f)
    }

    @Test
    fun twoFingerPinch_producesZoomGesture() {
        val classifier = GestureClassifier(pinchThreshold = 20f)
        val p1 = PointerSample(1, 100f, 100f, true, 1000L)
        val p2 = PointerSample(2, 200f, 100f, true, 1000L) // initial dist = 100
        val p1Spread = PointerSample(1, 80f, 100f, true, 1016L)
        val p2Spread = PointerSample(2, 230f, 100f, true, 1016L) // dist = 150 (> 20 diff)

        classifier.processFrame(listOf(p1, p2))
        val gestures = classifier.processFrame(listOf(p1Spread, p2Spread))

        assertEquals(1, gestures.size)
        assertTrue(gestures[0] is ClassifiedGesture.Zoom)
        val zoom = gestures[0] as ClassifiedGesture.Zoom
        assertEquals(1, zoom.deltaNotches) // zoom in
    }

    @Test
    fun threeFingerSwipe_producesDirectionalSwipe() {
        val classifier = GestureClassifier(swipeThreshold = 50f)
        val p1 = PointerSample(1, 100f, 100f, true, 1000L)
        val p2 = PointerSample(2, 150f, 100f, true, 1000L)
        val p3 = PointerSample(3, 200f, 100f, true, 1000L)
        val p1Swipe = PointerSample(1, 40f, 100f, true, 1050L) // -60 x
        val p2Swipe = PointerSample(2, 90f, 100f, true, 1050L)
        val p3Swipe = PointerSample(3, 140f, 100f, true, 1050L)

        classifier.processFrame(listOf(p1, p2, p3))
        val gestures = classifier.processFrame(listOf(p1Swipe, p2Swipe, p3Swipe))

        assertEquals(1, gestures.size)
        assertEquals(ClassifiedGesture.ThreeFingerSwipeLeft, gestures[0])
    }
}
