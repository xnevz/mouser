package com.mouser.companion.ui.components

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.FileDownload
import androidx.compose.material.icons.filled.FileUpload
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.mouser.companion.model.Macro
import com.mouser.companion.model.MacroStep
import com.mouser.companion.model.VirtualKey

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun MacroPadView(
    isConnected: Boolean,
    macros: List<Macro>,
    onRunMacro: (Macro) -> Unit,
    onSaveMacro: (Macro) -> Unit,
    onDeleteMacro: (String) -> Unit,
    onResetDefaults: () -> Unit,
    onExportJson: () -> Unit,
    onImportJson: () -> Unit,
    modifier: Modifier = Modifier
) {
    var editingMacro by remember { mutableStateOf<Macro?>(null) }
    var isNewMacro by remember { mutableStateOf(false) }

    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                "Macro Pad",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                IconButton(onClick = onImportJson) {
                    Icon(Icons.Default.FileDownload, contentDescription = "Import JSON", tint = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                IconButton(onClick = onExportJson) {
                    Icon(Icons.Default.FileUpload, contentDescription = "Export JSON", tint = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                IconButton(onClick = onResetDefaults) {
                    Icon(Icons.Default.Refresh, contentDescription = "Reset Defaults", tint = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                IconButton(
                    onClick = {
                        editingMacro = Macro(label = "New Macro", steps = emptyList())
                        isNewMacro = true
                    }
                ) {
                    Icon(Icons.Default.Add, contentDescription = "Add Macro", tint = MaterialTheme.colorScheme.primary)
                }
            }
        }

        Text(
            "Tap to trigger, hold to customize.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )

        LazyVerticalGrid(
            columns = GridCells.Fixed(2),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
            modifier = Modifier.weight(1f)
        ) {
            items(macros, key = { it.id }) { macro ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(80.dp)
                        .clip(RoundedCornerShape(12.dp))
                        .combinedClickable(
                            onClick = { if (isConnected) onRunMacro(macro) },
                            onLongClick = {
                                editingMacro = macro
                                isNewMacro = false
                            }
                        ),
                    colors = CardDefaults.cardColors(
                        containerColor = if (isConnected) MaterialTheme.colorScheme.surfaceVariant
                        else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                    ),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(8.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.Center
                        ) {
                            Text(
                                text = macro.label,
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                textAlign = TextAlign.Center
                            )
                            Text(
                                text = "${macro.steps.size} steps",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                            )
                        }
                    }
                }
            }
        }
    }

    editingMacro?.let { macro ->
        MacroEditDialog(
            macro = macro,
            isNew = isNewMacro,
            onDismiss = { editingMacro = null },
            onSave = { saved ->
                onSaveMacro(saved)
                editingMacro = null
            },
            onDelete = { id ->
                onDeleteMacro(id)
                editingMacro = null
            }
        )
    }
}

@Composable
fun MacroEditDialog(
    macro: Macro,
    isNew: Boolean,
    onDismiss: () -> Unit,
    onSave: (Macro) -> Unit,
    onDelete: (String) -> Unit
) {
    var label by remember { mutableStateOf(macro.label) }
    var steps by remember { mutableStateOf(macro.steps) }
    var newText by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (isNew) "Add Macro" else "Edit Macro") },
        text = {
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                OutlinedTextField(
                    value = label,
                    onValueChange = { label = it },
                    label = { Text("Label") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )

                Text("Steps (${steps.size}):", style = MaterialTheme.typography.labelMedium)

                steps.forEachIndexed { index, step ->
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        val desc = when (step) {
                            is MacroStep.Key -> "Key: 0x${step.vk.toString(16).uppercase()}"
                            is MacroStep.Combo -> "Combo: ${step.vks.joinToString("+") { "0x" + it.toString(16).uppercase() }}"
                            is MacroStep.Text -> "Text: \"${step.text}\""
                            is MacroStep.Delay -> "Delay: ${step.ms}ms"
                        }
                        Text(desc, style = MaterialTheme.typography.bodySmall, modifier = Modifier.weight(1f))
                        IconButton(onClick = { steps = steps.filterIndexed { i, _ -> i != index } }) {
                            Icon(Icons.Default.Delete, contentDescription = "Remove step", tint = MaterialTheme.colorScheme.error)
                        }
                    }
                }

                HorizontalDivider(color = MaterialTheme.colorScheme.outline)

                Text("Quick Add Step:", style = MaterialTheme.typography.labelSmall)
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    FilledTonalButton(
                        onClick = { steps = steps + MacroStep.Combo(listOf(VirtualKey.CONTROL, 'C'.code.toShort())) },
                        contentPadding = PaddingValues(horizontal = 8.dp)
                    ) {
                        Text("Ctrl+C")
                    }
                    FilledTonalButton(
                        onClick = { steps = steps + MacroStep.Combo(listOf(VirtualKey.CONTROL, 'V'.code.toShort())) },
                        contentPadding = PaddingValues(horizontal = 8.dp)
                    ) {
                        Text("Ctrl+V")
                    }
                    FilledTonalButton(
                        onClick = { steps = steps + MacroStep.Key(VirtualKey.RETURN) },
                        contentPadding = PaddingValues(horizontal = 8.dp)
                    ) {
                        Text("Enter")
                    }
                    FilledTonalButton(
                        onClick = { steps = steps + MacroStep.Delay(100) },
                        contentPadding = PaddingValues(horizontal = 8.dp)
                    ) {
                        Text("100ms")
                    }
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    OutlinedTextField(
                        value = newText,
                        onValueChange = { newText = it },
                        placeholder = { Text("Text to type") },
                        singleLine = true,
                        modifier = Modifier.weight(1f)
                    )
                    Button(
                        onClick = {
                            if (newText.isNotBlank()) {
                                steps = steps + MacroStep.Text(newText)
                                newText = ""
                            }
                        },
                        enabled = newText.isNotBlank()
                    ) {
                        Text("Add")
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = { onSave(macro.copy(label = label.ifBlank { "Macro" }, steps = steps)) }
            ) {
                Text("Save")
            }
        },
        dismissButton = {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                if (!isNew) {
                    TextButton(
                        onClick = { onDelete(macro.id) },
                        colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error)
                    ) {
                        Text("Delete")
                    }
                }
                TextButton(onClick = onDismiss) {
                    Text("Cancel")
                }
            }
        }
    )
}
