package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.filled.VolumeDown
import androidx.compose.material.icons.automirrored.filled.VolumeMute
import androidx.compose.material.icons.automirrored.filled.VolumeUp
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.model.VirtualKey
import com.mouser.companion.ui.theme.*

private data class PadKey(val label: String, val code: Short, val accent: Boolean = false)

private val keypad = listOf(
    listOf(PadKey("7", VirtualKey.numpadDigit(7)), PadKey("8", VirtualKey.numpadDigit(8)), PadKey("9", VirtualKey.numpadDigit(9)), PadKey("⌫", VirtualKey.BACK, true)),
    listOf(PadKey("4", VirtualKey.numpadDigit(4)), PadKey("5", VirtualKey.numpadDigit(5)), PadKey("6", VirtualKey.numpadDigit(6)), PadKey("+", VirtualKey.ADD)),
    listOf(PadKey("1", VirtualKey.numpadDigit(1)), PadKey("2", VirtualKey.numpadDigit(2)), PadKey("3", VirtualKey.numpadDigit(3)), PadKey("−", VirtualKey.SUBTRACT)),
    listOf(PadKey("0", VirtualKey.numpadDigit(0)), PadKey(".", VirtualKey.DECIMAL), PadKey("×", VirtualKey.MULTIPLY), PadKey("↵", VirtualKey.RETURN, true))
)

/** Media keys, arrow keys and a numeric keypad. Media keys work on Windows hosts; macOS ignores them. */
@Composable
fun MediaMacrosView(
    isConnected: Boolean,
    onKey: (virtualKey: Short) -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        if (!isConnected) {
            Text("Connect to a computer to use these keys.", style = MaterialTheme.typography.bodyMedium, color = Zinc400)
        }

        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Zinc800)
        ) {
            Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text("Media", style = MaterialTheme.typography.titleMedium, color = Zinc100)
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                    MediaButton(Icons.Default.SkipPrevious, "Previous track", isConnected) { onKey(VirtualKey.MEDIA_PREV) }
                    MediaButton(Icons.Default.PlayArrow, "Play or pause", isConnected) { onKey(VirtualKey.MEDIA_PLAY_PAUSE) }
                    MediaButton(Icons.Default.SkipNext, "Next track", isConnected) { onKey(VirtualKey.MEDIA_NEXT) }
                }
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                    MediaButton(Icons.AutoMirrored.Filled.VolumeMute, "Mute", isConnected) { onKey(VirtualKey.VOLUME_MUTE) }
                    MediaButton(Icons.AutoMirrored.Filled.VolumeDown, "Volume down", isConnected) { onKey(VirtualKey.VOLUME_DOWN) }
                    MediaButton(Icons.AutoMirrored.Filled.VolumeUp, "Volume up", isConnected) { onKey(VirtualKey.VOLUME_UP) }
                }
                HorizontalDivider(color = Zinc700)
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                    MediaButton(Icons.AutoMirrored.Filled.KeyboardArrowLeft, "Left arrow", isConnected) { onKey(VirtualKey.LEFT) }
                    MediaButton(Icons.Default.KeyboardArrowUp, "Up arrow", isConnected) { onKey(VirtualKey.UP) }
                    MediaButton(Icons.Default.KeyboardArrowDown, "Down arrow", isConnected) { onKey(VirtualKey.DOWN) }
                    MediaButton(Icons.AutoMirrored.Filled.KeyboardArrowRight, "Right arrow", isConnected) { onKey(VirtualKey.RIGHT) }
                    MediaButton(Icons.Default.SpaceBar, "Space", isConnected) { onKey(VirtualKey.SPACE) }
                }
            }
        }

        Card(
            modifier = Modifier.fillMaxWidth().weight(1f),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Zinc800)
        ) {
            Column(modifier = Modifier.padding(16.dp).fillMaxSize(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Number pad", style = MaterialTheme.typography.titleMedium, color = Zinc100)
                for (row in keypad) {
                    Row(modifier = Modifier.fillMaxWidth().weight(1f), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        for (key in row) {
                            Button(
                                onClick = { onKey(key.code) },
                                enabled = isConnected,
                                modifier = Modifier.weight(1f).fillMaxHeight(),
                                shape = RoundedCornerShape(10.dp),
                                colors = ButtonDefaults.buttonColors(containerColor = if (key.accent) BluePrimary else Zinc700)
                            ) {
                                Text(key.label, fontSize = 18.sp, fontWeight = FontWeight.Bold, color = Zinc100)
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun MediaButton(icon: ImageVector, label: String, enabled: Boolean, onClick: () -> Unit) {
    IconButton(
        onClick = onClick,
        enabled = enabled,
        modifier = Modifier.size(52.dp).background(Zinc700, RoundedCornerShape(12.dp))
    ) {
        Icon(icon, contentDescription = label, tint = if (enabled) Zinc100 else Zinc600, modifier = Modifier.size(26.dp))
    }
}
