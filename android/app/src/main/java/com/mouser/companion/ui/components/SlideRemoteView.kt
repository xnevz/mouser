package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.model.VirtualKey
import com.mouser.companion.ui.theme.RoseAccent
import kotlinx.coroutines.delay

@Composable
fun SlideRemoteView(
    isConnected: Boolean,
    onKey: (virtualKey: Short) -> Unit,
    onLaser: (isOn: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    var timerRunning by remember { mutableStateOf(false) }
    var elapsedSeconds by remember { mutableLongStateOf(0L) }
    var notesText by remember { mutableStateOf("") }
    var isLaserActive by remember { mutableStateOf(false) }

    LaunchedEffect(timerRunning) {
        while (timerRunning) {
            delay(1000)
            elapsedSeconds++
        }
    }

    val minutes = elapsedSeconds / 60
    val seconds = elapsedSeconds % 60
    val timeFormatted = "%02d:%02d".format(minutes, seconds)

    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp)
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        if (!isConnected) {
            Text(
                "Connect to a computer to control presentation slides.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }

        // Timer Card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column {
                    Text("Presentation Timer", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(
                        timeFormatted,
                        style = MaterialTheme.typography.headlineMedium,
                        fontWeight = FontWeight.Bold,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }

                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilledTonalIconButton(
                        onClick = { timerRunning = !timerRunning }
                    ) {
                        Icon(
                            if (timerRunning) Icons.Default.Pause else Icons.Default.PlayArrow,
                            contentDescription = if (timerRunning) "Pause timer" else "Start timer"
                        )
                    }

                    IconButton(
                        onClick = {
                            timerRunning = false
                            elapsedSeconds = 0L
                        }
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Reset timer")
                    }
                }
            }
        }

        // Start (F5) and End (Esc) Row
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Button(
                onClick = { onKey(VirtualKey.F5) },
                enabled = isConnected,
                modifier = Modifier.weight(1f).height(48.dp),
                shape = RoundedCornerShape(12.dp),
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
            ) {
                Icon(Icons.Default.Slideshow, contentDescription = null)
                Spacer(Modifier.width(8.dp))
                Text("Start (F5)")
            }

            OutlinedButton(
                onClick = { onKey(VirtualKey.ESCAPE) },
                enabled = isConnected,
                modifier = Modifier.weight(1f).height(48.dp),
                shape = RoundedCornerShape(12.dp)
            ) {
                Icon(Icons.Default.Stop, contentDescription = null)
                Spacer(Modifier.width(8.dp))
                Text("End (Esc)")
            }
        }

        // Big Previous & Next Navigation Buttons
        Row(
            modifier = Modifier.fillMaxWidth().height(160.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            FilledTonalButton(
                onClick = { onKey(VirtualKey.LEFT) },
                enabled = isConnected,
                modifier = Modifier.weight(1f).fillMaxHeight(),
                shape = RoundedCornerShape(20.dp),
                colors = ButtonDefaults.filledTonalButtonColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                    Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Previous Slide", modifier = Modifier.size(48.dp))
                    Spacer(Modifier.height(8.dp))
                    Text("Previous", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                }
            }

            Button(
                onClick = { onKey(VirtualKey.RIGHT) },
                enabled = isConnected,
                modifier = Modifier.weight(1f).fillMaxHeight(),
                shape = RoundedCornerShape(20.dp),
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                    Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = "Next Slide", modifier = Modifier.size(48.dp))
                    Spacer(Modifier.height(8.dp))
                    Text("Next", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                }
            }
        }

        // Laser Pointer Hold Button
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(64.dp)
                .clip(RoundedCornerShape(16.dp))
                .background(if (isLaserActive) RoseAccent else MaterialTheme.colorScheme.surface)
                .border(2.dp, if (isLaserActive) Color.Red else MaterialTheme.colorScheme.outline, RoundedCornerShape(16.dp))
                .semantics { contentDescription = "Hold for laser pointer" }
                .pointerInput(isConnected) {
                    if (!isConnected) return@pointerInput
                    detectTapGestures(onPress = {
                        isLaserActive = true
                        onLaser(true)
                        try {
                            tryAwaitRelease()
                        } finally {
                            isLaserActive = false
                            onLaser(false)
                        }
                    })
                },
            contentAlignment = Alignment.Center
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Box(
                    modifier = Modifier
                        .size(12.dp)
                        .clip(CircleShape)
                        .background(if (isLaserActive) Color.White else RoseAccent)
                )
                Text(
                    text = if (isLaserActive) "LASER POINTER ACTIVE" else "HOLD FOR LASER POINTER",
                    fontWeight = FontWeight.Bold,
                    color = if (isLaserActive) Color.White else MaterialTheme.colorScheme.onSurface
                )
            }
        }

        // Slide Notes
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Speaker Notes", style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.onSurface)
                OutlinedTextField(
                    value = notesText,
                    onValueChange = { notesText = it },
                    placeholder = { Text("Type slide cues or notes here...") },
                    modifier = Modifier.fillMaxWidth().heightIn(min = 100.dp, max = 180.dp),
                    shape = RoundedCornerShape(12.dp)
                )
            }
        }
    }
}
