package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Navigation
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.model.InputMotionPacket
import com.mouser.companion.ui.theme.*

/** Hold-to-point gyroscope mouse with press-and-hold click buttons. */
@Composable
fun AirMouseSurface(
    hasGyroscope: Boolean,
    isConnected: Boolean,
    isTriggerHeld: Boolean,
    onTriggerChanged: (Boolean) -> Unit,
    sensitivity: Float,
    onSensitivityChanged: (Float) -> Unit,
    onButton: (button: Byte, isDown: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier.fillMaxSize().padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Zinc800)
        ) {
            Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(
                    text = when {
                        !hasGyroscope -> "This phone has no gyroscope"
                        isTriggerHeld -> "Pointing"
                        else -> "Air mouse"
                    },
                    style = MaterialTheme.typography.titleMedium,
                    color = if (isTriggerHeld) EmeraldAccent else Zinc100
                )
                Text(
                    text = if (hasGyroscope) "Hold the big button and turn the phone like a laser pointer. Release to stop."
                    else "Use the Trackpad tab instead.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = Zinc400
                )
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    Icon(Icons.Default.Speed, contentDescription = "Pointer speed", tint = Zinc400)
                    Slider(value = sensitivity, onValueChange = onSensitivityChanged, valueRange = 10f..60f, modifier = Modifier.weight(1f))
                    Text("${sensitivity.toInt()}", style = MaterialTheme.typography.labelSmall, color = Zinc100)
                }
            }
        }

        val enabled = hasGyroscope && isConnected
        Box(
            modifier = Modifier
                .size(200.dp)
                .clip(CircleShape)
                .background(if (isTriggerHeld) BluePrimary else Zinc800)
                .border(3.dp, if (isTriggerHeld) EmeraldAccent else Zinc700, CircleShape)
                .semantics { contentDescription = "Hold to point" }
                .pointerInput(enabled) {
                    if (!enabled) return@pointerInput
                    detectTapGestures(onPress = {
                        onTriggerChanged(true)
                        try {
                            tryAwaitRelease()
                        } finally {
                            onTriggerChanged(false)
                        }
                    })
                },
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Default.Navigation, contentDescription = null, tint = if (enabled) Zinc100 else Zinc600, modifier = Modifier.size(40.dp))
                Spacer(Modifier.height(8.dp))
                Text(
                    text = when {
                        !isConnected -> "NOT CONNECTED"
                        isTriggerHeld -> "POINTING"
                        else -> "HOLD TO POINT"
                    },
                    fontWeight = FontWeight.Bold,
                    fontSize = 15.sp,
                    color = if (enabled) Zinc100 else Zinc600
                )
            }
        }

        Spacer(Modifier.height(24.dp))

        Row(modifier = Modifier.fillMaxWidth().height(64.dp), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            HoldButton("Left", Modifier.weight(1f)) { onButton(InputMotionPacket.BUTTON_LEFT, it) }
            HoldButton("Right", Modifier.weight(1f)) { onButton(InputMotionPacket.BUTTON_RIGHT, it) }
        }
    }
}
