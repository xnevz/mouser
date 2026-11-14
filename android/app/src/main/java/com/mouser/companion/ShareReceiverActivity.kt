package com.mouser.companion

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.provider.OpenableColumns
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.lifecycleScope
import com.mouser.companion.model.HostTarget
import com.mouser.companion.network.ClipboardBridge
import com.mouser.companion.network.FileTransferClient
import com.mouser.companion.ui.theme.MouserTheme
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class ShareReceiverActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val intent = intent
        val action = intent?.action
        val type = intent?.type

        if (action != Intent.ACTION_SEND || type == null) {
            finish()
            return
        }

        val sharedText = intent.getStringExtra(Intent.EXTRA_TEXT)
        val sharedUri = intent.getParcelableExtra<Uri>(Intent.EXTRA_STREAM)

        if (sharedText == null && sharedUri == null) {
            Toast.makeText(this, "Nothing to share", Toast.LENGTH_SHORT).show()
            finish()
            return
        }

        val prefs = getSharedPreferences("mouser", Context.MODE_PRIVATE)
        val themeMode = prefs.getString("theme", "System") ?: "System"
        val ownDeviceId = prefs.getString("own.deviceId", null) ?: "android-phone"

        // Load paired computers from SharedPreferences
        val pairedTargets = mutableListOf<Pair<HostTarget, ByteArray>>()
        val allPrefs = prefs.all
        for ((key, value) in allPrefs) {
            if (key.startsWith("pairkey:") && value is String) {
                val targetKey = key.removePrefix("pairkey:")
                val pairKey = value.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
                val hostName = prefs.getString("name:$targetKey", null) ?: targetKey
                val address = prefs.getString("address:$targetKey", null) ?: prefs.getString("last.address", null) ?: ""
                val port = prefs.getInt("port:$targetKey", 58491)
                val cport = prefs.getInt("cport:$targetKey", 58492)
                if (address.isNotEmpty()) {
                    pairedTargets.add(HostTarget(hostName, address, port, targetKey, cport) to pairKey)
                }
            }
        }

        // If no specifically indexed targets, fallback to last connected host if paired
        if (pairedTargets.isEmpty()) {
            val lastAddr = prefs.getString("last.address", null)
            val lastName = prefs.getString("last.name", "Computer") ?: "Computer"
            val lastDeviceId = prefs.getString("last.deviceId", null)
            val lastPort = prefs.getInt("last.port", 58491)
            val lastCport = prefs.getInt("last.cport", 58492)
            if (lastAddr != null && lastDeviceId != null) {
                val pairKeyHex = prefs.getString("pairkey:$lastDeviceId", null)
                if (pairKeyHex != null) {
                    val pairKey = pairKeyHex.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
                    pairedTargets.add(HostTarget(lastName, lastAddr, lastPort, lastDeviceId, lastCport) to pairKey)
                }
            }
        }

        setContent {
            MouserTheme(mode = themeMode) {
                var isSending by remember { mutableStateOf(false) }
                var progressText by remember { mutableStateOf("") }

                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(24.dp),
                        verticalArrangement = Arrangement.spacedBy(16.dp)
                    ) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = "Send to computer",
                                style = MaterialTheme.typography.headlineSmall,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onBackground
                            )
                            IconButton(onClick = { finish() }) {
                                Icon(Icons.Default.Close, contentDescription = "Close")
                            }
                        }

                        if (sharedText != null) {
                            Text(
                                text = "Sharing text: \"${sharedText.take(60)}${if (sharedText.length > 60) "…" else ""}\"",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        } else if (sharedUri != null) {
                            Text(
                                text = "Sharing file/image",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }

                        HorizontalDivider(color = MaterialTheme.colorScheme.outline)

                        if (isSending) {
                            Column(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 32.dp),
                                horizontalAlignment = Alignment.CenterHorizontally,
                                verticalArrangement = Arrangement.spacedBy(16.dp)
                            ) {
                                CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
                                Text(
                                    text = progressText,
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onBackground
                                )
                            }
                        } else if (pairedTargets.isEmpty()) {
                            Text(
                                text = "No paired computers found. Please pair with a computer in the Mouser app first.",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.error
                            )
                        } else {
                            Text(
                                text = "Select destination:",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )

                            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                                items(pairedTargets) { (target, key) ->
                                    Card(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .clip(RoundedCornerShape(12.dp))
                                            .clickable {
                                                isSending = true
                                                progressText = "Sending to ${target.hostName}…"
                                                sendSharedContent(
                                                    target = target,
                                                    pairKey = key,
                                                    ownDeviceId = ownDeviceId,
                                                    text = sharedText,
                                                    uri = sharedUri,
                                                    onProgress = { progressText = it },
                                                    onDone = { success, msg ->
                                                        isSending = false
                                                        Toast.makeText(this@ShareReceiverActivity, msg, Toast.LENGTH_SHORT).show()
                                                        if (success) finish()
                                                    }
                                                )
                                            },
                                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                                    ) {
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .padding(16.dp),
                                            verticalAlignment = Alignment.CenterVertically,
                                            horizontalArrangement = Arrangement.spacedBy(16.dp)
                                        ) {
                                            Icon(
                                                Icons.Default.Computer,
                                                contentDescription = null,
                                                tint = MaterialTheme.colorScheme.primary
                                            )
                                            Column(modifier = Modifier.weight(1f)) {
                                                Text(
                                                    text = target.hostName,
                                                    style = MaterialTheme.typography.titleMedium,
                                                    color = MaterialTheme.colorScheme.onSurface
                                                )
                                                Text(
                                                    text = "${target.address}:${target.controlPort}",
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                                )
                                            }
                                            Icon(
                                                Icons.Default.Send,
                                                contentDescription = "Send",
                                                tint = MaterialTheme.colorScheme.primary
                                            )
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private fun sendSharedContent(
        target: HostTarget,
        pairKey: ByteArray,
        ownDeviceId: String,
        text: String?,
        uri: Uri?,
        onProgress: (String) -> Unit,
        onDone: (Boolean, String) -> Unit
    ) {
        lifecycleScope.launch(Dispatchers.IO) {
            try {
                if (text != null) {
                    ClipboardBridge.sendClip(
                        hostAddress = target.address,
                        controlPort = target.controlPort,
                        pairKey = pairKey,
                        ownDeviceId = ownDeviceId,
                        text = text,
                        deviceName = android.os.Build.MODEL ?: "Android Phone"
                    )
                    withContext(Dispatchers.Main) {
                        onDone(true, "Sent clip to ${target.hostName}")
                    }
                } else if (uri != null) {
                    var fileName = "shared_file"
                    var fileSize = -1L
                    contentResolver.query(uri, null, null, null, null)?.use { cursor ->
                        val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                        val sizeIndex = cursor.getColumnIndex(OpenableColumns.SIZE)
                        if (cursor.moveToFirst()) {
                            if (nameIndex != -1) fileName = cursor.getString(nameIndex)
                            if (sizeIndex != -1) fileSize = cursor.getLong(sizeIndex)
                        }
                    }

                    if (fileSize > FileTransferClient.MAX_FILE_BYTES) {
                        withContext(Dispatchers.Main) {
                            onDone(false, "Files over 2 GB cannot be sent.")
                        }
                        return@launch
                    }

                    withContext(Dispatchers.Main) {
                        onProgress("Calculating checksum…")
                    }

                    val (computedSize, sha256) = contentResolver.openInputStream(uri)!!.use {
                        FileTransferClient.computeSha256(it)
                    }

                    val totalBytes = if (fileSize > 0) fileSize else computedSize
                    if (totalBytes > FileTransferClient.MAX_FILE_BYTES) {
                        withContext(Dispatchers.Main) {
                            onDone(false, "Files over 2 GB cannot be sent.")
                        }
                        return@launch
                    }

                    withContext(Dispatchers.Main) {
                        onProgress("Streaming $fileName…")
                    }

                    FileTransferClient.sendFile(
                        hostAddress = target.address,
                        controlPort = target.controlPort,
                        pairKey = pairKey,
                        ownDeviceId = ownDeviceId,
                        fileName = fileName,
                        totalBytes = totalBytes,
                        sha256Hex = sha256,
                        openInputStream = { contentResolver.openInputStream(uri)!! },
                        onProgress = { sent, total ->
                            val pct = if (total > 0) (sent * 100 / total).toInt() else 0
                            launch(Dispatchers.Main) {
                                onProgress("Sending $fileName ($pct%)…")
                            }
                        }
                    )

                    withContext(Dispatchers.Main) {
                        onDone(true, "Sent $fileName to ${target.hostName}")
                    }
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    onDone(false, "Transfer failed: ${e.message}")
                }
            }
        }
    }
}
