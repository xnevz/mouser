package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.PointerEvent
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.mouser.companion.model.InputMotionPacket
import com.mouser.companion.ui.theme.*

/**
 * Touch trackpad: 1 finger moves, 2 fingers scroll, tap = left click, 2-finger tap = right click.
 * The buttons below are press-and-hold, so holding Left while moving a finger drags.
 */
@Composable
fun TrackpadSurface(
    isConnected: Boolean,
    sensitivity: Float,
    onSensitivityChanged: (Float) -> Unit,
    onMove: (dx: Float, dy: Float) -> Unit,
    onScroll: (dx: Float, dy: Float) -> Unit,
    onClick: (button: Byte) -> Unit,
    onButton: (button: Byte, isDown: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    val haptics = LocalHapticFeedback.current
    val currentSensitivity by rememberUpdatedState(sensitivity)

    Column(
        modifier = modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Box(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .clip(RoundedCornerShape(16.dp))
                .background(Zinc800)
                .border(1.dp, if (isConnected) Zinc700 else Zinc800, RoundedCornerShape(16.dp))
                .semantics { contentDescription = "Trackpad" }
                .pointerInput(Unit) {
                    val slop = viewConfiguration.touchSlop
                    awaitEachGesture {
                        val first = awaitFirstDown(requireUnconsumed = false)
                        var maxPointers = 1
                        var travelled = 0f
                        var lastCentroid: Offset? = null
                        var lastCount = 0
                        var event: PointerEvent
                        do {
                            event = awaitPointerEvent()
                            val pressed = event.changes.filter { it.pressed }
                            maxPointers = maxOf(maxPointers, pressed.size)
                            if (pressed.isNotEmpty()) {
                                val centroid = pressed
                                    .fold(Offset.Zero) { acc, c -> acc + c.position } / pressed.size.toFloat()
                                val previous = lastCentroid
                                // Skip the frame where a finger lands or lifts so the centroid jump isn't sent as motion.
                                if (previous != null && pressed.size == lastCount) {
                                    val delta = centroid - previous
                                    travelled += delta.getDistance()
                                    if (pressed.size == 1) {
                                        onMove(delta.x * currentSensitivity, delta.y * currentSensitivity)
                                    } else {
                                        onScroll(delta.x, delta.y)
                                    }
                                }
                                lastCentroid = centroid
                                lastCount = pressed.size
                            }
                            event.changes.forEach { it.consume() }
                        } while (event.changes.any { it.pressed })

                        val duration = event.changes.first().uptimeMillis - first.uptimeMillis
                        if (travelled < slop && duration < 300) {
                            haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                            onClick(if (maxPointers >= 2) InputMotionPacket.BUTTON_RIGHT else InputMotionPacket.BUTTON_LEFT)
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
                Icon(Icons.Default.TouchApp, contentDescription = null, tint = Zinc600, modifier = Modifier.size(44.dp))
                Text(
                    text = if (isConnected) "Move with one finger • scroll with two" else "Connect to a computer to start",
                    style = MaterialTheme.typography.titleMedium,
                    color = Zinc400,
                    textAlign = TextAlign.Center
                )
                if (isConnected) {
                    Text(
                        text = "Tap to click • two-finger tap to right-click • hold Left below to drag",
                        style = MaterialTheme.typography.labelSmall,
                        color = Zinc600,
                        textAlign = TextAlign.Center
                    )
                }
            }
        }

        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Icon(Icons.Default.Speed, contentDescription = "Pointer speed", tint = Zinc400)
            Slider(
                value = sensitivity,
                onValueChange = onSensitivityChanged,
                valueRange = 0.5f..4f,
                modifier = Modifier.weight(1f)
            )
            Text("%.1f×".format(sensitivity), style = MaterialTheme.typography.labelSmall, color = Zinc100)
        }

        Row(
            modifier = Modifier.fillMaxWidth().height(64.dp),
            horizontalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            HoldButton("Left", Modifier.weight(1f)) { down ->
                if (down) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                onButton(InputMotionPacket.BUTTON_LEFT, down)
            }
            HoldButton("Right", Modifier.weight(1f)) { down ->
                if (down) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                onButton(InputMotionPacket.BUTTON_RIGHT, down)
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
            .fillMaxHeight()
            .clip(RoundedCornerShape(12.dp))
            .background(if (pressed) Zinc700 else Zinc800)
            .border(1.dp, Zinc700, RoundedCornerShape(12.dp))
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
        Text(label, color = Zinc100, fontWeight = FontWeight.SemiBold)
    }
}
