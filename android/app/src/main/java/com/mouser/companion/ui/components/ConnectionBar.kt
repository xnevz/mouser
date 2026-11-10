package com.mouser.companion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.SettingsEthernet
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mouser.companion.model.DiscoveredHost
import com.mouser.companion.model.HostConnectionState
import com.mouser.companion.ui.theme.*

/**
 * Top bar showing live connection status and opening the host pairing dialog.
 */
@Composable
fun ConnectionBar(
    connectionState: HostConnectionState,
    discoveredHosts: Map<String, DiscoveredHost>,
    onConnectToHost: (ip: String, port: Int) -> Unit,
    onDisconnect: () -> Unit,
    modifier: Modifier = Modifier
) {
    var showDialog by remember { mutableStateOf(false) }

    Surface(
        color = Zinc800,
        modifier = modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            // App Title & Status
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text(
                    text = "Mouser",
                    style = MaterialTheme.typography.titleMedium,
                    color = Zinc100
                )

                // Status Indicator Pill
                val (statusColor, statusText) = when (connectionState) {
                    is HostConnectionState.Connected -> Pair(EmeraldAccent, "Connected: ${connectionState.hostAddress}")
                    is HostConnectionState.Connecting -> Pair(AmberAccent, "Connecting...")
                    is HostConnectionState.Disconnected -> Pair(RoseAccent, "Disconnected")
                    is HostConnectionState.Error -> Pair(RoseAccent, "Error")
                }

                Row(
                    modifier = Modifier
                        .clip(RoundedCornerShape(12.dp))
                        .background(statusColor.copy(alpha = 0.15f))
                        .clickable { showDialog = true }
                        .padding(horizontal = 10.dp, vertical = 4.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    Box(
                        modifier = Modifier
                            .size(8.dp)
                            .background(statusColor, CircleShape)
                    )
                    Text(
                        text = statusText,
                        style = MaterialTheme.typography.labelSmall,
                        color = statusColor
                    )
                }
            }

            // Connect / Settings Button
            IconButton(
                onClick = { showDialog = true },
                modifier = Modifier.size(36.dp)
            ) {
                Icon(
                    imageVector = Icons.Default.SettingsEthernet,
                    contentDescription = "Server Settings",
                    tint = Zinc400
                )
            }
        }
    }

    if (showDialog) {
        HostSelectionDialog(
            discoveredHosts = discoveredHosts,
            onDismiss = { showDialog = false },
            onConnect = { ip, port ->
                onConnectToHost(ip, port)
                showDialog = false
            },
            onDisconnect = {
                onDisconnect()
                showDialog = false
            }
        )
    }
}

@Composable
fun HostSelectionDialog(
    discoveredHosts: Map<String, DiscoveredHost>,
    onDismiss: () -> Unit,
    onConnect: (ip: String, port: Int) -> Unit,
    onDisconnect: () -> Unit
) {
    var manualIp by remember { mutableStateOf("192.168.1.100") }
    var manualPort by remember { mutableStateOf("58490") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Text("Connect to Mouser Desktop", color = Zinc100)
        },
        text = {
            Column(
                verticalArrangement = Arrangement.spacedBy(12.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = "Discovered Hosts on Local Network:",
                    style = MaterialTheme.typography.bodyMedium,
                    color = Zinc400
                )

                if (discoveredHosts.isEmpty()) {
                    Text(
                        text = "Searching for nearby hosts via mDNS/UDP...",
                        style = MaterialTheme.typography.labelSmall,
                        color = Zinc400
                    )
                } else {
                    for ((_, host) in discoveredHosts) {
                        Card(
                            colors = CardDefaults.cardColors(containerColor = Zinc700),
                            modifier = Modifier
                                .fillMaxWidth()
                                .clickable { onConnect(host.ipAddress, host.port) }
                        ) {
                            Row(
                                modifier = Modifier.padding(12.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                Icon(imageVector = Icons.Default.Computer, contentDescription = null, tint = BluePrimary)
                                Column {
                                    Text(host.hostName, color = Zinc100, fontWeight = androidx.compose.ui.text.font.FontWeight.Bold)
                                    Text("${host.ipAddress}:${host.port}", color = Zinc400, fontSize = 12.sp)
                                }
                            }
                        }
                    }
                }

                Divider(color = Zinc700)

                Text(
                    text = "Or Enter IP Address Manually:",
                    style = MaterialTheme.typography.bodyMedium,
                    color = Zinc400
                )

                OutlinedTextField(
                    value = manualIp,
                    onValueChange = { manualIp = it },
                    label = { Text("Host IP Address") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )

                OutlinedTextField(
                    value = manualPort,
                    onValueChange = { manualPort = it },
                    label = { Text("Port (Default: 58490)") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val port = manualPort.toIntOrNull() ?: 58490
                    onConnect(manualIp.trim(), port)
                },
                colors = ButtonDefaults.buttonColors(containerColor = BluePrimary)
            ) {
                Text("Connect")
            }
        },
        dismissButton = {
            TextButton(onClick = onDisconnect) {
                Text("Disconnect", color = RoseAccent)
            }
        },
        containerColor = Zinc800
    )
}
