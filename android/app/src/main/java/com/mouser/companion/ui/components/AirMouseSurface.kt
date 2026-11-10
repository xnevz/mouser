package com.mouser.companion.ui.components

import android.view.MotionEvent
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Navigation
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.ExperimentalComposeUiApi
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.input.pointer.pointerInteropFilter
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.model.InputMotionPacket
import com.mouser.companion.ui.theme.*

/**
 * 3D Gyroscope Air Mouse interface with hold-to-point trigger, tilt horizon, and click triggers.
 */
@OptIn(ExperimentalComposeUiApi::class)
@Composable
fun AirMouseSurface(
    isTriggerHeld: Boolean,
    onTriggerChanged: (Boolean) -> Unit,
    sensitivity: Float,
    onSensitivityChanged: (Float) -> Unit,
    onButtonClick: (button: Byte, isDown: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        // Horizon / Pointer Visualizer Card
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 16.dp),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Zinc800)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Navigation,
                        contentDescription = null,
                        tint = if (isTriggerHeld) EmeraldAccent else Zinc400,
                        modifier = Modifier.size(20.dp)
                    )
                    Text(
                        text = if (isTriggerHeld) "Air Mouse Active (Tracking)" else "Air Mouse Idle",
                        style = MaterialTheme.typography.titleMedium,
                        color = if (isTriggerHeld) EmeraldAccent else Zinc400
                    )
                }

                Text(
                    text = "Hold the trigger button below and wave your phone like a laser pointer.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = Zinc400
                )

                // Sensitivity Slider
                Row(
                    modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Icon(imageVector = Icons.Default.Speed, contentDescription = null, tint = Zinc400)
                    Slider(
                        value = sensitivity,
                        onValueChange = onSensitivityChanged,
                        valueRange = 10f..60f,
                        modifier = Modifier.weight(1f)
                    )
                    Text(
                        text = "${sensitivity.toInt()}x",
                        style = MaterialTheme.typography.labelSmall,
                        color = Zinc100
                    )
                }
            }
        }

        // Center: Ergonomic "HOLD TO POINT" Trigger Button
        Box(
            modifier = Modifier
                .size(200.dp)
                .clip(CircleShape)
                .background(if (isTriggerHeld) BluePrimary else Zinc800)
                .border(
                    width = 3.dp,
                    color = if (isTriggerHeld) EmeraldAccent else Zinc700,
                    shape = CircleShape
                )
                .pointerInteropFilter { motionEvent ->
                    when (motionEvent.action) {
                        MotionEvent.ACTION_DOWN -> {
                            onTriggerChanged(true)
                            true
                        }
                        MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                            onTriggerChanged(false)
                            true
                        }
                        else -> false
                    }
                },
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(
                    imageVector = Icons.Default.Navigation,
                    contentDescription = null,
                    tint = if (isTriggerHeld) Zinc100 else Zinc400,
                    modifier = Modifier.size(40.dp)
                )
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = if (isTriggerHeld) "TRACKING" else "HOLD TO POINT",
                    fontWeight = FontWeight.Bold,
                    fontSize = 15.sp,
                    color = if (isTriggerHeld) Zinc100 else Zinc400
                )
            }
        }

        Spacer(modifier = Modifier.height(24.dp))

        // Bottom Action Bar: Left & Right Click
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(60.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Button(
                onClick = {
                    onButtonClick(InputMotionPacket.BUTTON_LEFT, true)
                    onButtonClick(InputMotionPacket.BUTTON_LEFT, false)
                },
                modifier = Modifier.weight(1f).fillMaxHeight(),
                shape = RoundedCornerShape(12.dp),
                colors = ButtonDefaults.buttonColors(containerColor = Zinc800)
            ) {
                Text("Left Click", color = Zinc100, fontWeight = FontWeight.SemiBold)
            }

            Button(
                onClick = {
                    onButtonClick(InputMotionPacket.BUTTON_RIGHT, true)
                    onButtonClick(InputMotionPacket.BUTTON_RIGHT, false)
                },
                modifier = Modifier.weight(1f).fillMaxHeight(),
                shape = RoundedCornerShape(12.dp),
                colors = ButtonDefaults.buttonColors(containerColor = Zinc800)
            ) {
                Text("Right Click", color = Zinc100, fontWeight = FontWeight.SemiBold)
            }
        }
    }
}
