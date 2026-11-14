package com.mouser.companion.model

import kotlin.math.abs
import kotlin.math.hypot

sealed interface ClassifiedGesture {
    data class Move(val dx: Float, val dy: Float) : ClassifiedGesture
    data class Scroll(val dx: Float, val dy: Float) : ClassifiedGesture
    data class Zoom(val deltaNotches: Int) : ClassifiedGesture
    data object Tap : ClassifiedGesture
    data object SecondaryTap : ClassifiedGesture
    data object ThreeFingerSwipeLeft : ClassifiedGesture
    data object ThreeFingerSwipeRight : ClassifiedGesture
    data object ThreeFingerSwipeUp : ClassifiedGesture
    data object ThreeFingerSwipeDown : ClassifiedGesture
    data class DragStateChanged(val isDragging: Boolean) : ClassifiedGesture
}

data class PointerSample(
    val id: Long,
    val x: Float,
    val y: Float,
    val isDown: Boolean,
    val timestampMs: Long
)

class GestureClassifier(
    var pointerSpeed: Float = 1.5f,
    var scrollSpeed: Float = 1.0f,
    var reverseScroll: Boolean = false,
    var tapToClick: Boolean = true,
    var dragLock: Boolean = false,
    val tapSlop: Float = 20f,
    val tapTimeoutMs: Long = 300L,
    val swipeThreshold: Float = 80f,
    val pinchThreshold: Float = 30f
) {
    private var lastPointers: List<PointerSample> = emptyList()
    private var downPointers: List<PointerSample> = emptyList()
    private var downTimeMs: Long = 0L
    private var maxPointerCount: Int = 0
    private var totalTravel: Float = 0f
    private var initialPinchDist: Float = 0f
    private var lastPinchDist: Float = 0f
    private var threeFingerTravelX: Float = 0f
    private var threeFingerTravelY: Float = 0f
    private var threeFingerFired: Boolean = false
    private var isDragging: Boolean = false
    private var lastTapTimeMs: Long = 0L

    fun processFrame(pointers: List<PointerSample>): List<ClassifiedGesture> {
        val active = pointers.filter { it.isDown }
        val events = mutableListOf<ClassifiedGesture>()

        if (active.isEmpty()) {
            // Touch ended
            if (lastPointers.isNotEmpty()) {
                val duration = (pointers.firstOrNull()?.timestampMs ?: 0L) - downTimeMs
                if (totalTravel < tapSlop && duration < tapTimeoutMs && tapToClick) {
                    if (maxPointerCount == 1) {
                        val now = pointers.firstOrNull()?.timestampMs ?: 0L
                        if (dragLock && now - lastTapTimeMs < tapTimeoutMs) {
                            isDragging = !isDragging
                            events.add(ClassifiedGesture.DragStateChanged(isDragging))
                        } else {
                            events.add(ClassifiedGesture.Tap)
                        }
                        lastTapTimeMs = now
                    } else if (maxPointerCount >= 2) {
                        events.add(ClassifiedGesture.SecondaryTap)
                    }
                }
            }
            downPointers = emptyList()
            lastPointers = emptyList()
            maxPointerCount = 0
            totalTravel = 0f
            initialPinchDist = 0f
            lastPinchDist = 0f
            threeFingerTravelX = 0f
            threeFingerTravelY = 0f
            threeFingerFired = false
            return events
        }

        if (downPointers.isEmpty() || lastPointers.isEmpty()) {
            // Touch started
            downPointers = active
            lastPointers = active
            downTimeMs = active.first().timestampMs
            maxPointerCount = active.size
            totalTravel = 0f
            threeFingerTravelX = 0f
            threeFingerTravelY = 0f
            threeFingerFired = false
            if (active.size >= 2) {
                val d = hypot(active[0].x - active[1].x, active[0].y - active[1].y)
                initialPinchDist = d
                lastPinchDist = d
            }
            return events
        }

        maxPointerCount = maxOf(maxPointerCount, active.size)

        if (active.size == 1 && lastPointers.size == 1) {
            val dx = (active[0].x - lastPointers[0].x) * pointerSpeed
            val dy = (active[0].y - lastPointers[0].y) * pointerSpeed
            totalTravel += hypot(dx, dy)
            events.add(ClassifiedGesture.Move(dx, dy))
        } else if (active.size == 2 && lastPointers.size == 2) {
            // Check for pinch vs scroll
            val currentDist = hypot(active[0].x - active[1].x, active[0].y - active[1].y)
            val pinchDiff = currentDist - lastPinchDist
            if (abs(currentDist - initialPinchDist) > pinchThreshold && abs(pinchDiff) > 8f) {
                val notches = if (pinchDiff > 0) 1 else -1
                events.add(ClassifiedGesture.Zoom(notches))
                lastPinchDist = currentDist
            } else {
                val avgDx = ((active[0].x - lastPointers[0].x) + (active[1].x - lastPointers[1].x)) / 2f
                val avgDy = ((active[0].y - lastPointers[0].y) + (active[1].y - lastPointers[1].y)) / 2f
                val dir = if (reverseScroll) -1f else 1f
                events.add(ClassifiedGesture.Scroll(avgDx * scrollSpeed, avgDy * scrollSpeed * dir))
                totalTravel += hypot(avgDx, avgDy)
            }
        } else if (active.size == 3 && lastPointers.size == 3) {
            val avgDx = ((active[0].x - lastPointers[0].x) + (active[1].x - lastPointers[1].x) + (active[2].x - lastPointers[2].x)) / 3f
            val avgDy = ((active[0].y - lastPointers[0].y) + (active[1].y - lastPointers[1].y) + (active[2].y - lastPointers[2].y)) / 3f
            threeFingerTravelX += avgDx
            threeFingerTravelY += avgDy
            totalTravel += hypot(avgDx, avgDy)

            if (!threeFingerFired) {
                if (abs(threeFingerTravelX) > swipeThreshold && abs(threeFingerTravelX) > abs(threeFingerTravelY)) {
                    threeFingerFired = true
                    if (threeFingerTravelX < 0) {
                        events.add(ClassifiedGesture.ThreeFingerSwipeLeft)
                    } else {
                        events.add(ClassifiedGesture.ThreeFingerSwipeRight)
                    }
                } else if (abs(threeFingerTravelY) > swipeThreshold && abs(threeFingerTravelY) > abs(threeFingerTravelX)) {
                    threeFingerFired = true
                    if (threeFingerTravelY < 0) {
                        events.add(ClassifiedGesture.ThreeFingerSwipeUp)
                    } else {
                        events.add(ClassifiedGesture.ThreeFingerSwipeDown)
                    }
                }
            }
        }

        lastPointers = active
        return events
    }
}
