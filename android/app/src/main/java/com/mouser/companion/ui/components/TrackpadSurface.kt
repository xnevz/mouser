package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.mouser.companion.model.ClassifiedGesture
import com.mouser.companion.model.GestureClassifier
import com.mouser.companion.model.InputMotionPacket
import com.mouser.companion.model.PointerSample

/**
 * Laptop-style trackpad surface with multi-touch gesture support:
 * - 1 finger: pointer move
 * - Tap: left-click
 * - 2-finger tap: right-click
 * - 2-finger move: scroll (with reverse scroll support)
 * - 2-finger pinch/spread: Ctrl+wheel zoom
 * - 3-finger swipe: desktop switching / Task view / Show desktop
 * - Double-tap drag lock
 *
 * Responsively adapts between portrait and landscape/tablet layouts.
 */
@Composable
fun TrackpadSurface(
    isConnected: Boolean,
    sensitivity: Float,
    onSensitivityChanged: (Float) -> Unit,
    scrollSensitivity: Float = 1.0f,
    onScrollSensitivityChanged: (Float) -> Unit = {},
    reverseScroll: Boolean = false,
    onReverseScrollChanged: (Boolean) -> Unit = {},
    hapticsEnabled: Boolean = true,
    onHapticsEnabledChanged: (Boolean) -> Unit = {},
    tapToClick: Boolean = true,
    onTapToClickChanged: (Boolean) -> Unit = {},
    dragLock: Boolean = false,
    onDragLockChanged: (Boolean) -> Unit = {},
    airMouseSensitivity: Float = 30f,
    onAirMouseSensitivityChanged: (Float) -> Unit = {},
    onMove: (dx: Float, dy: Float) -> Unit,
    onScroll: (dx: Float, dy: Float) -> Unit,
    onZoom: (deltaNotches: Int) -> Unit = {},
    onClick: (button: Byte) -> Unit,
    onButton: (button: Byte, isDown: Boolean) -> Unit,
    onSwipeLeft: () -> Unit = {},
    onSwipeRight: () -> Unit = {},
    onSwipeUp: () -> Unit = {},
    onSwipeDown: () -> Unit = {},
    modifier: Modifier = Modifier
) {
    val haptics = LocalHapticFeedback.current
    var showSettings by remember { mutableStateOf(false) }

    val classifier = remember(sensitivity, scrollSensitivity, reverseScroll, tapToClick, dragLock) {
        GestureClassifier(
            pointerSpeed = sensitivity,
            scrollSpeed = scrollSensitivity,
            reverseScroll = reverseScroll,
            tapToClick = tapToClick,
            dragLock = dragLock
        )
    }

    BoxWithConstraints(modifier = modifier.fillMaxSize().padding(16.dp)) {
        val isLandscape = maxWidth > maxHeight
        if (isLandscape) {
            Row(
                modifier = Modifier.fillMaxSize(),
                horizontalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                TrackpadTouchBox(
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                    isConnected = isConnected,
                    classifier = classifier,
                    hapticsEnabled = hapticsEnabled,
                    onMove = onMove,
                    onScroll = onScroll,
                    onZoom = onZoom,
                    onClick = onClick,
                    onButton = onButton,
                    onSwipeLeft = onSwipeLeft,
                    onSwipeRight = onSwipeRight,
                    onSwipeUp = onSwipeUp,
                    onSwipeDown = onSwipeDown
                )
                Column(
                    modifier = Modifier.widthIn(min = 160.dp, max = 220.dp).fillMaxHeight(),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    HoldButton("Left", Modifier.weight(1f).fillMaxWidth()) { down ->
                        if (down && hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                        onButton(InputMotionPacket.BUTTON_LEFT, down)
                    }
                    HoldButton("Middle", Modifier.height(48.dp).fillMaxWidth()) { down ->
                        if (down && hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                        onButton(InputMotionPacket.BUTTON_MIDDLE, down)
                    }
                    HoldButton("Right", Modifier.weight(1f).fillMaxWidth()) { down ->
                        if (down && hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                        onButton(InputMotionPacket.BUTTON_RIGHT, down)
                    }
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Icon(Icons.Default.Speed, contentDescription = "Pointer speed", tint = MaterialTheme.colorScheme.onSurfaceVariant)
                        Slider(
                            value = sensitivity,
                            onValueChange = onSensitivityChanged,
                            valueRange = 0.5f..4f,
                            modifier = Modifier.weight(1f)
                        )
                        IconButton(onClick = { showSettings = true }) {
                            Icon(Icons.Default.Settings, contentDescription = "Open companion settings", tint = MaterialTheme.colorScheme.primary)
                        }
                    }
                }
            }
        } else {
            Column(
                modifier = Modifier.fillMaxSize(),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                TrackpadTouchBox(
                    modifier = Modifier.weight(1f).fillMaxWidth(),
                    isConnected = isConnected,
                    classifier = classifier,
                    hapticsEnabled = hapticsEnabled,
                    onMove = onMove,
                    onScroll = onScroll,
                    onZoom = onZoom,
                    onClick = onClick,
                    onButton = onButton,
                    onSwipeLeft = onSwipeLeft,
                    onSwipeRight = onSwipeRight,
                    onSwipeUp = onSwipeUp,
                    onSwipeDown = onSwipeDown
                )
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    Icon(Icons.Default.Speed, contentDescription = "Pointer speed", tint = MaterialTheme.colorScheme.onSurfaceVariant)
                    Slider(
                        value = sensitivity,
                        onValueChange = onSensitivityChanged,
                        valueRange = 0.5f..4f,
                        modifier = Modifier.weight(1f)
                    )
                    Text("%.1f×".format(sensitivity), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurface)

                    IconButton(onClick = { showSettings = true }) {
                        Icon(Icons.Default.Settings, contentDescription = "Open companion settings", tint = MaterialTheme.colorScheme.primary)
                    }
                }

                Row(
                    modifier = Modifier.fillMaxWidth().height(64.dp),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    HoldButton("Left", Modifier.weight(1f)) { down ->
                        if (down && hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                        onButton(InputMotionPacket.BUTTON_LEFT, down)
                    }
                    HoldButton("Right", Modifier.weight(1f)) { down ->
                        if (down && hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                        onButton(InputMotionPacket.BUTTON_RIGHT, down)
                    }
                }
            }
        }
    }

    if (showSettings) {
        SettingsSheet(
            pointerSpeed = sensitivity,
            onPointerSpeedChange = onSensitivityChanged,
            scrollSpeed = scrollSensitivity,
            onScrollSpeedChange = onScrollSensitivityChanged,
            airMouseSensitivity = airMouseSensitivity,
            onAirMouseSensitivityChange = onAirMouseSensitivityChanged,
            reverseScroll = reverseScroll,
            onReverseScrollChange = onReverseScrollChanged,
            hapticsEnabled = hapticsEnabled,
            onHapticsEnabledChange = onHapticsEnabledChanged,
            tapToClick = tapToClick,
            onTapToClickChange = onTapToClickChanged,
            dragLock = dragLock,
            onDragLockChange = onDragLockChanged,
            onDismiss = { showSettings = false }
        )
    }
}

@Composable
private fun TrackpadTouchBox(
    modifier: Modifier,
    isConnected: Boolean,
    classifier: GestureClassifier,
    hapticsEnabled: Boolean,
    onMove: (dx: Float, dy: Float) -> Unit,
    onScroll: (dx: Float, dy: Float) -> Unit,
    onZoom: (deltaNotches: Int) -> Unit,
    onClick: (button: Byte) -> Unit,
    onButton: (button: Byte, isDown: Boolean) -> Unit,
    onSwipeLeft: () -> Unit,
    onSwipeRight: () -> Unit,
    onSwipeUp: () -> Unit,
    onSwipeDown: () -> Unit
) {
    val haptics = LocalHapticFeedback.current

    Box(
        modifier = modifier
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, if (isConnected) MaterialTheme.colorScheme.outline else MaterialTheme.colorScheme.surface, RoundedCornerShape(16.dp))
            .semantics { contentDescription = "Trackpad multi-touch gesture surface" }
            .pointerInput(classifier) {
                awaitEachGesture {
                    do {
                        val event = awaitPointerEvent()
                        val samples = event.changes.map {
                            PointerSample(it.id.value, it.position.x, it.position.y, it.pressed, it.uptimeMillis)
                        }
                        val gestures = classifier.processFrame(samples)
                        for (g in gestures) {
                            when (g) {
                                is ClassifiedGesture.Move -> onMove(g.dx, g.dy)
                                is ClassifiedGesture.Scroll -> onScroll(g.dx, g.dy)
                                is ClassifiedGesture.Zoom -> onZoom(g.deltaNotches)
                                is ClassifiedGesture.Tap -> {
                                    if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                    onClick(InputMotionPacket.BUTTON_LEFT)
                                }
                                is ClassifiedGesture.SecondaryTap -> {
                                    if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                    onClick(InputMotionPacket.BUTTON_RIGHT)
                                }
                                is ClassifiedGesture.ThreeFingerSwipeLeft -> {
                                    if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                    onSwipeLeft()
                                }
                                is ClassifiedGesture.ThreeFingerSwipeRight -> {
                                    if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                    onSwipeRight()
                                }
                                is ClassifiedGesture.ThreeFingerSwipeUp -> {
                                    if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                    onSwipeUp()
                                }
                                is ClassifiedGesture.ThreeFingerSwipeDown -> {
                                    if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                    onSwipeDown()
                                }
                                is ClassifiedGesture.DragStateChanged -> {
                                    if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                    onButton(InputMotionPacket.BUTTON_LEFT, g.isDragging)
                                }
                            }
                        }
                        event.changes.forEach { it.consume() }
                    } while (event.changes.any { it.pressed })

                    // Release frame
                    val finalSamples = event.changes.map {
                        PointerSample(it.id.value, it.position.x, it.position.y, false, it.uptimeMillis)
                    }
                    val releaseGestures = classifier.processFrame(finalSamples)
                    for (g in releaseGestures) {
                        when (g) {
                            is ClassifiedGesture.Tap -> {
                                if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                onClick(InputMotionPacket.BUTTON_LEFT)
                            }
                            is ClassifiedGesture.SecondaryTap -> {
                                if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                onClick(InputMotionPacket.BUTTON_RIGHT)
                            }
                            is ClassifiedGesture.DragStateChanged -> {
                                if (hapticsEnabled) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                onButton(InputMotionPacket.BUTTON_LEFT, g.isDragging)
                            }
                            else -> {}
                        }
                    }
                }
            },
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.padding(24.dp)
        ) {
            Icon(Icons.Default.TouchApp, contentDescription = "Touch gestures active", tint = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.size(44.dp))
            Text(
                text = if (isConnected) "1 finger move • 2 scroll & pinch • 3 swipe" else "Connect to a computer to start",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center
            )
            if (isConnected) {
                Text(
                    text = "Tap to click • 2-finger tap right-click • hold below to drag",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center
                )
            }
        }
    }
}

/** A mouse button: sends down on press and up on release, so it can be held for dragging. */
@Composable
fun HoldButton(label: String, modifier: Modifier = Modifier, onStateChange: (isDown: Boolean) -> Unit) {
    var pressed by remember { mutableStateOf(false) }
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(if (pressed) MaterialTheme.colorScheme.surfaceVariant else MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outline, RoundedCornerShape(12.dp))
            .semantics { contentDescription = "$label mouse button" }
            .pointerInput(Unit) {
                detectTapGestures(onPress = {
                    pressed = true
                    onStateChange(true)
                    try {
                        tryAwaitRelease()
                    } finally {
                        pressed = false
                        onStateChange(false)
                    }
                })
            },
        contentAlignment = Alignment.Center
    ) {
        Text(label, color = MaterialTheme.colorScheme.onSurface, fontWeight = FontWeight.SemiBold)
    }
}
