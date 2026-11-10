package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PanTool
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.model.InputMotionPacket
import com.mouser.companion.ui.theme.*

/**
 * Interactive multi-touch trackpad surface supporting 1-finger motion, tap to click,
 * 2-finger right-click, and hardware left/right click buttons.
 */
@Composable
fun TrackpadSurface(
    sensitivity: Float,
    onMotionDelta: (dx: Short, dy: Short) -> Unit,
    onButtonClick: (button: Byte, isDown: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    var touchPos by remember { mutableStateOf<Offset?>(null) }

    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // Main Trackpad Surface
        Box(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .clip(RoundedCornerShape(16.dp))
                .background(Zinc800)
                .border(1.dp, Zinc700, RoundedCornerShape(16.dp))
                .pointerInput(sensitivity) {
                    detectDragGestures(
                        onDragStart = { offset ->
                            touchPos = offset
                        },
                        onDragEnd = {
                            touchPos = null
                        },
                        onDragCancel = {
                            touchPos = null
                        },
                        onDrag = { change, dragAmount ->
                            change.consume()
                            touchPos = change.position

                            val dx = (dragAmount.x * sensitivity).toInt().coerceIn(-32768, 32767).toShort()
                            val dy = (dragAmount.y * sensitivity).toInt().coerceIn(-32768, 32767).toShort()
                            if (dx != 0.toShort() || dy != 0.toShort()) {
                                onMotionDelta(dx, dy)
                            }
                        }
                    )
                }
                .pointerInput(Unit) {
                    detectTapGestures(
                        onTap = {
                            // 1-finger tap = Left Click
                            onButtonClick(InputMotionPacket.BUTTON_LEFT, true)
                            onButtonClick(InputMotionPacket.BUTTON_LEFT, false)
                        },
                        onDoubleTap = {
                            // Double tap = Double click
                            onButtonClick(InputMotionPacket.BUTTON_LEFT, true)
                            onButtonClick(InputMotionPacket.BUTTON_LEFT, false)
                            onButtonClick(InputMotionPacket.BUTTON_LEFT, true)
                            onButtonClick(InputMotionPacket.BUTTON_LEFT, false)
                        },
                        onLongPress = {
                            // Long press = Right Click
                            onButtonClick(InputMotionPacket.BUTTON_RIGHT, true)
                            onButtonClick(InputMotionPacket.BUTTON_RIGHT, false)
                        }
                    )
                },
            contentAlignment = Alignment.Center
        ) {
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Icon(
                    imageVector = Icons.Default.TouchApp,
                    contentDescription = null,
                    tint = Zinc600,
                    modifier = Modifier.size(48.dp)
                )
                Text(
                    text = "Trackpad Surface",
                    style = MaterialTheme.typography.titleMedium,
                    color = Zinc600
                )
                Text(
                    text = "Slide to move • Tap for Left Click • Long-press for Right Click",
                    style = MaterialTheme.typography.labelSmall,
                    color = Zinc600
                )
            }
        }

        // Dedicated Physical Button Strip (Left & Right Click)
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(64.dp),
            horizontalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            // Left Click Button
            Button(
                onClick = {
                    onButtonClick(InputMotionPacket.BUTTON_LEFT, true)
                    onButtonClick(InputMotionPacket.BUTTON_LEFT, false)
                },
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight(),
                shape = RoundedCornerShape(12.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = Zinc800,
                    contentColor = Zinc100
                ),
                border = ButtonDefaults.outlinedButtonBorder.copy(brush = androidx.compose.ui.graphics.SolidColor(Zinc700))
            ) {
                Text("Left Click", fontWeight = androidx.compose.ui.text.font.FontWeight.SemiBold)
            }

            // Right Click Button
            Button(
                onClick = {
                    onButtonClick(InputMotionPacket.BUTTON_RIGHT, true)
                    onButtonClick(InputMotionPacket.BUTTON_RIGHT, false)
                },
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight(),
                shape = RoundedCornerShape(12.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = Zinc800,
                    contentColor = Zinc100
                ),
                border = ButtonDefaults.outlinedButtonBorder.copy(brush = androidx.compose.ui.graphics.SolidColor(Zinc700))
            ) {
                Text("Right Click", fontWeight = androidx.compose.ui.text.font.FontWeight.SemiBold)
            }
        }
    }
}
