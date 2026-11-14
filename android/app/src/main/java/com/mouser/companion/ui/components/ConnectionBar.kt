package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.SettingsEthernet
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.model.DiscoveredHost
import com.mouser.companion.model.HostConnectionState
import com.mouser.companion.model.HostTarget
import com.mouser.companion.network.parsePin
import com.mouser.companion.ui.theme.*

const val DEFAULT_HOST_PORT = 58491

/** Top bar with live connection status; opens the host picker and the PIN prompt. */
@Composable
fun ConnectionBar(
    connectionState: HostConnectionState,
    discoveredHosts: Map<String, DiscoveredHost>,
    discoveryError: String?,
    themeMode: String = "System",
    onThemeModeChanged: (String) -> Unit = {},
    isAppLockEnabled: Boolean = false,
    onToggleAppLock: (Boolean) -> Unit = {},
    onConnect: (HostTarget) -> Unit,
    onSubmitPin: (Int) -> Unit,
    onDisconnect: () -> Unit,
    modifier: Modifier = Modifier
) {
    var showHosts by remember { mutableStateOf(connectionState is HostConnectionState.Disconnected) }

    val defaultMutedColor = MaterialTheme.colorScheme.onSurfaceVariant
    val (statusColor, statusText) = when (connectionState) {
        is HostConnectionState.Connected -> EmeraldAccent to buildString {
            append(connectionState.target.hostName)
            connectionState.latencyMs?.let { append(" • $it ms") }
        }
        is HostConnectionState.Connecting -> AmberAccent to "Connecting to ${connectionState.target.hostName}…"
        is HostConnectionState.NeedsPin -> AmberAccent to "Enter PIN for ${connectionState.target.hostName}"
        is HostConnectionState.Unreachable -> RoseAccent to "${connectionState.target.hostName} not responding…"
        is HostConnectionState.Disconnected -> defaultMutedColor to "Not connected"
        is HostConnectionState.Error -> RoseAccent to connectionState.message
    }

    Surface(color = MaterialTheme.colorScheme.surface, modifier = modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier.fillMaxWidth().statusBarsPadding().padding(horizontal = 16.dp, vertical = 10.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(
                modifier = Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(12.dp))
                    .clickable { showHosts = true }
                    .padding(vertical = 4.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text("Mouser", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onSurface)
                Box(modifier = Modifier.size(8.dp).background(statusColor, CircleShape))
                Text(statusText, style = MaterialTheme.typography.labelSmall, color = statusColor, maxLines = 1)
            }
            IconButton(onClick = { showHosts = true }) {
                Icon(Icons.Default.SettingsEthernet, contentDescription = "Choose computer", tint = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }

    if (connectionState is HostConnectionState.NeedsPin) {
        PinDialog(
            hostName = connectionState.target.hostName,
            wrongPin = connectionState.wrongPin,
            onSubmit = onSubmitPin,
            onCancel = onDisconnect
        )
    } else if (showHosts) {
        HostSelectionDialog(
            connectionState = connectionState,
            discoveredHosts = discoveredHosts,
            discoveryError = discoveryError,
            themeMode = themeMode,
            onThemeModeChanged = onThemeModeChanged,
            isAppLockEnabled = isAppLockEnabled,
            onToggleAppLock = onToggleAppLock,
            onDismiss = { showHosts = false },
            onConnect = {
                onConnect(it)
                showHosts = false
            },
            onDisconnect = {
                onDisconnect()
                showHosts = false
            }
        )
    }
}

@Composable
fun HostSelectionDialog(
    connectionState: HostConnectionState,
    discoveredHosts: Map<String, DiscoveredHost>,
    discoveryError: String?,
    themeMode: String = "System",
    onThemeModeChanged: (String) -> Unit = {},
    isAppLockEnabled: Boolean = false,
    onToggleAppLock: (Boolean) -> Unit = {},
    onDismiss: () -> Unit,
    onConnect: (HostTarget) -> Unit,
    onDisconnect: () -> Unit
) {
    var manualIp by remember { mutableStateOf("") }
    var manualPort by remember { mutableStateOf(DEFAULT_HOST_PORT.toString()) }
    val port = manualPort.toIntOrNull()?.takeIf { it in 1..65535 }
    val canConnectManually = manualIp.isNotBlank() && port != null

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Settings & Connection", color = MaterialTheme.colorScheme.onSurface) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.fillMaxWidth()) {
                // Theme chooser chips (Step 5)
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Text("Theme:", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    listOf("System", "Light", "Dark").forEach { mode ->
                        FilterChip(
                            selected = themeMode == mode,
                            onClick = { onThemeModeChanged(mode) },
                            label = { Text(mode) }
                        )
                    }
                }

                // App Lock Toggle (Task C6)
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text("App Lock", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface)
                        Text("Require biometric/PIN after 30s in background", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Switch(
                        checked = isAppLockEnabled,
                        onCheckedChange = onToggleAppLock
                    )
                }

                HorizontalDivider(color = MaterialTheme.colorScheme.outline)

                Text(
                    "Computers running Mouser on this Wi-Fi network:",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                when {
                    discoveryError != null -> Text(discoveryError, style = MaterialTheme.typography.labelSmall, color = RoseAccent)
                    discoveredHosts.isEmpty() -> Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        CircularProgressIndicator(modifier = Modifier.size(14.dp), strokeWidth = 2.dp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Text("Searching… Open Mouser on your computer.", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }

                for (host in discoveredHosts.values.sortedBy { it.hostName.lowercase() }) {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                        modifier = Modifier.fillMaxWidth().clickable { onConnect(host.toTarget()) }
                    ) {
                        Row(
                            modifier = Modifier.padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            Icon(Icons.Default.Computer, contentDescription = null, tint = BluePrimary)
                            Column {
                                Text(host.hostName, color = MaterialTheme.colorScheme.onSurface, fontWeight = FontWeight.Bold)
                                Text("${host.operatingSystem} • ${host.ipAddress}", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp)
                            }
                        }
                    }
                }

                HorizontalDivider(color = MaterialTheme.colorScheme.outline)
                Text("Or enter the address shown in Mouser on your computer:", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)

                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = manualIp,
                        onValueChange = { manualIp = it.trim() },
                        label = { Text("IP address") },
                        placeholder = { Text("e.g. 192.168.1.20") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                        modifier = Modifier.weight(2f)
                    )
                    OutlinedTextField(
                        value = manualPort,
                        onValueChange = { manualPort = it.filter(Char::isDigit).take(5) },
                        label = { Text("Port") },
                        singleLine = true,
                        isError = port == null,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier.weight(1f)
                    )
                }
            }
        },
        confirmButton = {
            Button(
                onClick = { onConnect(HostTarget(manualIp, manualIp, port ?: DEFAULT_HOST_PORT)) },
                enabled = canConnectManually,
                colors = ButtonDefaults.buttonColors(containerColor = BluePrimary)
            ) { Text("Connect") }
        },
        dismissButton = {
            if (connectionState !is HostConnectionState.Disconnected) {
                TextButton(onClick = onDisconnect) { Text("Disconnect", color = RoseAccent) }
            } else {
                TextButton(onClick = onDismiss) { Text("Close", color = MaterialTheme.colorScheme.onSurfaceVariant) }
            }
        },
        containerColor = MaterialTheme.colorScheme.surface
    )
}

@Composable
fun PinDialog(hostName: String, wrongPin: Boolean, onSubmit: (Int) -> Unit, onCancel: () -> Unit) {
    var text by remember { mutableStateOf("") }
    val pin = parsePin(text)

    AlertDialog(
        onDismissRequest = onCancel,
        title = { Text("Pair with $hostName", color = MaterialTheme.colorScheme.onSurface) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text(
                    "Enter the 6-digit PIN shown in Mouser on the computer (Phone Remote page).",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                OutlinedTextField(
                    value = text,
                    onValueChange = { text = it.filter { c -> c.isDigit() }.take(6) },
                    label = { Text("PIN") },
                    singleLine = true,
                    isError = wrongPin,
                    supportingText = if (wrongPin) {
                        { Text("Wrong PIN. After 5 wrong tries the computer waits a minute.") }
                    } else null,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(onClick = { pin?.let(onSubmit) }, enabled = pin != null, colors = ButtonDefaults.buttonColors(containerColor = BluePrimary)) {
                Text("Pair")
            }
        },
        dismissButton = { TextButton(onClick = onCancel) { Text("Cancel", color = MaterialTheme.colorScheme.onSurfaceVariant) } },
        containerColor = MaterialTheme.colorScheme.surface
    )
}
