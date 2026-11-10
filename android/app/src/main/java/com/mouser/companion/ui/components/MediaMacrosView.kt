package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.ui.theme.*

/**
 * Media remote and full numeric keypad macro panel.
 */
@Composable
fun MediaMacrosView(
    onSendKey: (keyCode: Short, isDown: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Media Controller Card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Zinc800)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    text = "Media Remote",
                    style = MaterialTheme.typography.titleMedium,
                    color = Zinc100
                )

                // Playback row
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    MediaButton(icon = Icons.Default.SkipPrevious, label = "Prev") {
                        onSendKey(0x10, true); onSendKey(0x10, false) // Prev track
                    }
                    MediaButton(icon = Icons.Default.PlayArrow, label = "Play/Pause") {
                        onSendKey(0x11, true); onSendKey(0x11, false) // Play/pause
                    }
                    MediaButton(icon = Icons.Default.SkipNext, label = "Next") {
                        onSendKey(0x12, true); onSendKey(0x12, false) // Next track
                    }
                }

                Divider(color = Zinc700, thickness = 1.dp)

                // Volume row
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    MediaButton(icon = Icons.Default.VolumeMute, label = "Mute") {
                        onSendKey(0x13, true); onSendKey(0x13, false)
                    }
                    MediaButton(icon = Icons.Default.VolumeDown, label = "Vol -") {
                        onSendKey(0x14, true); onSendKey(0x14, false)
                    }
                    MediaButton(icon = Icons.Default.VolumeUp, label = "Vol +") {
                        onSendKey(0x15, true); onSendKey(0x15, false)
                    }
                }
            }
        }

        // Quick Numeric Keypad
        Card(
            modifier = Modifier.fillMaxWidth().weight(1f),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Zinc800)
        ) {
            Column(
                modifier = Modifier.padding(16.dp).fillMaxSize(),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text(
                    text = "Numeric Keypad (Numpad)",
                    style = MaterialTheme.typography.titleMedium,
                    color = Zinc100
                )

                val keypad = listOf(
                    listOf("7", "8", "9", "⌫"),
                    listOf("4", "5", "6", "+"),
                    listOf("1", "2", "3", "-"),
                    listOf("0", ".", "=", "↵")
                )

                for (row in keypad) {
                    Row(
                        modifier = Modifier.fillMaxWidth().weight(1f),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        for (key in row) {
                            Button(
                                onClick = {
                                    val code: Short = when (key) {
                                        "⌫" -> 0x08 // Backspace
                                        "↵" -> 0x0D // Enter
                                        else -> key[0].code.toShort()
                                    }
                                    onSendKey(code, true)
                                    onSendKey(code, false)
                                },
                                modifier = Modifier.weight(1f).fillMaxHeight(),
                                shape = RoundedCornerShape(10.dp),
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = if (key in listOf("⌫", "↵")) BluePrimary else Zinc700
                                )
                            ) {
                                Text(
                                    text = key,
                                    fontSize = 18.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = Zinc100
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun MediaButton(
    icon: ImageVector,
    label: String,
    onClick: () -> Unit
) {
    IconButton(
        onClick = onClick,
        modifier = Modifier
            .size(56.dp)
            .background(Zinc700, RoundedCornerShape(12.dp))
    ) {
        Icon(
            imageVector = icon,
            contentDescription = label,
            tint = Zinc100,
            modifier = Modifier.size(28.dp)
        )
    }
}
