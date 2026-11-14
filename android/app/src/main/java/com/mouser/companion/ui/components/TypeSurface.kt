package com.mouser.companion.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.mouser.companion.model.VirtualKey

@Composable
fun TypeSurface(
    isConnected: Boolean,
    onSendText: (String) -> Unit,
    onLiveDiff: (old: String, new: String) -> Unit,
    onKey: (virtualKey: Short) -> Unit,
    modifier: Modifier = Modifier
) {
    var text by remember { mutableStateOf("") }
    var liveMode by remember { mutableStateOf(false) }

    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        if (!isConnected) {
            Text(
                "Connect to a computer to type or dictate.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }

        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column {
                        Text(
                            "Live typing",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.SemiBold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            "Send keystrokes immediately as you type or dictate",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = liveMode,
                        onCheckedChange = {
                            liveMode = it
                            if (it) {
                                text = ""
                            }
                        },
                        enabled = isConnected
                    )
                }

                OutlinedTextField(
                    value = text,
                    onValueChange = { newText ->
                        if (liveMode && isConnected) {
                            onLiveDiff(text, newText)
                        }
                        text = newText
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 120.dp, max = 200.dp),
                    placeholder = {
                        Text(
                            if (liveMode) "Type or tap the keyboard mic to dictate live..."
                            else "Type or dictate text here, then press Send..."
                        )
                    },
                    shape = RoundedCornerShape(12.dp),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = MaterialTheme.colorScheme.primary,
                        unfocusedBorderColor = MaterialTheme.colorScheme.outline
                    )
                )

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    OutlinedButton(
                        onClick = { text = "" },
                        enabled = text.isNotEmpty(),
                        shape = RoundedCornerShape(8.dp)
                    ) {
                        Text("Clear")
                    }

                    if (!liveMode) {
                        Button(
                            onClick = {
                                if (text.isNotEmpty() && isConnected) {
                                    onSendText(text)
                                    text = ""
                                }
                            },
                            enabled = isConnected && text.isNotEmpty(),
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            Icon(Icons.AutoMirrored.Filled.Send, contentDescription = "Send text")
                            Spacer(Modifier.width(8.dp))
                            Text("Send")
                        }
                    }
                }
            }
        }

        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    "Navigation & Edit Keys",
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    FilledTonalButton(
                        onClick = { onKey(VirtualKey.BACK) },
                        enabled = isConnected,
                        shape = RoundedCornerShape(8.dp)
                    ) {
                        Icon(Icons.Default.Backspace, contentDescription = "Backspace")
                        Spacer(Modifier.width(4.dp))
                        Text("Backspace")
                    }

                    FilledTonalButton(
                        onClick = { onKey(VirtualKey.RETURN) },
                        enabled = isConnected,
                        shape = RoundedCornerShape(8.dp)
                    ) {
                        Icon(Icons.Default.KeyboardReturn, contentDescription = "Enter")
                        Spacer(Modifier.width(4.dp))
                        Text("Enter")
                    }
                }

                HorizontalDivider(color = MaterialTheme.colorScheme.outline)

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    IconButton(
                        onClick = { onKey(VirtualKey.LEFT) },
                        enabled = isConnected
                    ) {
                        Icon(Icons.AutoMirrored.Filled.KeyboardArrowLeft, contentDescription = "Left Arrow")
                    }
                    IconButton(
                        onClick = { onKey(VirtualKey.UP) },
                        enabled = isConnected
                    ) {
                        Icon(Icons.Default.KeyboardArrowUp, contentDescription = "Up Arrow")
                    }
                    IconButton(
                        onClick = { onKey(VirtualKey.DOWN) },
                        enabled = isConnected
                    ) {
                        Icon(Icons.Default.KeyboardArrowDown, contentDescription = "Down Arrow")
                    }
                    IconButton(
                        onClick = { onKey(VirtualKey.RIGHT) },
                        enabled = isConnected
                    ) {
                        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = "Right Arrow")
                    }
                }
            }
        }
    }
}
