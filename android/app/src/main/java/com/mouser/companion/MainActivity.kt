package com.mouser.companion

import android.os.Bundle
import android.view.WindowManager
import androidx.activity.viewModels
import androidx.activity.compose.setContent
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Gamepad
import androidx.compose.material.icons.filled.Keyboard
import androidx.compose.material.icons.filled.Navigation
import androidx.compose.material.icons.filled.Slideshow
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import com.mouser.companion.model.HostConnectionState
import com.mouser.companion.ui.components.*
import com.mouser.companion.ui.theme.*

class MainActivity : FragmentActivity() {

    private val viewModel: MouserViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
            if (checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS) != android.content.pm.PackageManager.PERMISSION_GRANTED) {
                requestPermissions(arrayOf(android.Manifest.permission.POST_NOTIFICATIONS), 101)
            }
        }

        setContent {
            val themeMode by viewModel.themeMode.collectAsState()
            val isLocked by viewModel.isLocked.collectAsState()
            val isAppLockEnabled by viewModel.isAppLockEnabled.collectAsState()

            LaunchedEffect(isAppLockEnabled) {
                if (isAppLockEnabled) {
                    window.setFlags(
                        WindowManager.LayoutParams.FLAG_SECURE,
                        WindowManager.LayoutParams.FLAG_SECURE
                    )
                } else {
                    window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE)
                }
            }

            MouserTheme(mode = themeMode) {
                if (isLocked) {
                    LockScreen(onUnlockClick = { showBiometricPrompt() })
                } else {
                    val connectionState by viewModel.connectionState.collectAsState()
                    val discoveredHosts by viewModel.discoveredHosts.collectAsState()
                    val discoveryError by viewModel.discoveryError.collectAsState()
                    val currentTab by viewModel.currentTab.collectAsState()
                    val trackpadSensitivity by viewModel.trackpadSensitivity.collectAsState()
                    val scrollSensitivity by viewModel.scrollSensitivity.collectAsState()
                    val reverseScroll by viewModel.reverseScroll.collectAsState()
                    val hapticsEnabled by viewModel.hapticsEnabled.collectAsState()
                    val tapToClick by viewModel.tapToClick.collectAsState()
                    val dragLock by viewModel.dragLock.collectAsState()
                    val airMouseSensitivity by viewModel.airMouseSensitivity.collectAsState()
                    val isAirMouseTriggerHeld by viewModel.isAirMouseTriggerHeld.collectAsState()
                    val macros by viewModel.macros.collectAsState()
                    val batteryLevel by viewModel.batteryLevel.collectAsState()
                    val isConnected = connectionState is HostConnectionState.Connected

                    Scaffold(
                        topBar = {
                            ConnectionBar(
                                connectionState = connectionState,
                                discoveredHosts = discoveredHosts,
                                discoveryError = discoveryError,
                                themeMode = themeMode,
                                onThemeModeChanged = viewModel::setThemeMode,
                                isAppLockEnabled = isAppLockEnabled,
                                onToggleAppLock = viewModel::setAppLockEnabled,
                                phoneBatteryPercent = batteryLevel,
                                onConnect = viewModel::connect,
                                onDisconnect = viewModel::disconnect,
                                onSendClipboard = {
                                    val cm = getSystemService(CLIPBOARD_SERVICE) as? android.content.ClipboardManager
                                    val clipText = cm?.primaryClip?.getItemAt(0)?.text?.toString()
                                    if (clipText.isNullOrEmpty()) {
                                        android.widget.Toast.makeText(this@MainActivity, "Clipboard is empty", android.widget.Toast.LENGTH_SHORT).show()
                                    } else {
                                        viewModel.sendClipboardToHost(clipText) { _, msg ->
                                            android.widget.Toast.makeText(this@MainActivity, msg, android.widget.Toast.LENGTH_SHORT).show()
                                        }
                                    }
                                },
                                onGetClipboard = {
                                    viewModel.getClipboardFromHost { success, result ->
                                        if (success) {
                                            val cm = getSystemService(CLIPBOARD_SERVICE) as? android.content.ClipboardManager
                                            cm?.setPrimaryClip(android.content.ClipData.newPlainText("Mouser", result))
                                            android.widget.Toast.makeText(this@MainActivity, "Copied from PC", android.widget.Toast.LENGTH_SHORT).show()
                                        } else {
                                            android.widget.Toast.makeText(this@MainActivity, result, android.widget.Toast.LENGTH_SHORT).show()
                                        }
                                    }
                                }
                            )
                        },
                        bottomBar = {
                            NavigationBar(
                                containerColor = MaterialTheme.colorScheme.surface
                            ) {
                                NavigationBarItem(
                                    selected = currentTab == AppNavigationTab.TRACKPAD,
                                    onClick = { viewModel.selectTab(AppNavigationTab.TRACKPAD) },
                                    icon = { Icon(Icons.Default.TouchApp, contentDescription = "Trackpad") },
                                    label = { Text("Trackpad") },
                                    colors = NavigationBarItemDefaults.colors(
                                        selectedIconColor = BluePrimary,
                                        selectedTextColor = BluePrimary,
                                        unselectedIconColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        unselectedTextColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        indicatorColor = MaterialTheme.colorScheme.surfaceVariant
                                    )
                                )

                                NavigationBarItem(
                                    selected = currentTab == AppNavigationTab.AIR_MOUSE,
                                    onClick = { viewModel.selectTab(AppNavigationTab.AIR_MOUSE) },
                                    icon = { Icon(Icons.Default.Navigation, contentDescription = "Air Mouse") },
                                    label = { Text("Air Mouse") },
                                    colors = NavigationBarItemDefaults.colors(
                                        selectedIconColor = EmeraldAccent,
                                        selectedTextColor = EmeraldAccent,
                                        unselectedIconColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        unselectedTextColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        indicatorColor = MaterialTheme.colorScheme.surfaceVariant
                                    )
                                )

                                NavigationBarItem(
                                    selected = currentTab == AppNavigationTab.SLIDES,
                                    onClick = { viewModel.selectTab(AppNavigationTab.SLIDES) },
                                    icon = { Icon(Icons.Default.Slideshow, contentDescription = "Slides") },
                                    label = { Text("Slides") },
                                    colors = NavigationBarItemDefaults.colors(
                                        selectedIconColor = RoseAccent,
                                        selectedTextColor = RoseAccent,
                                        unselectedIconColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        unselectedTextColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        indicatorColor = MaterialTheme.colorScheme.surfaceVariant
                                    )
                                )

                                NavigationBarItem(
                                    selected = currentTab == AppNavigationTab.MACROS,
                                    onClick = { viewModel.selectTab(AppNavigationTab.MACROS) },
                                    icon = { Icon(Icons.Default.Gamepad, contentDescription = "Keys") },
                                    label = { Text("Keys") },
                                    colors = NavigationBarItemDefaults.colors(
                                        selectedIconColor = IndigoAccent,
                                        selectedTextColor = IndigoAccent,
                                        unselectedIconColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        unselectedTextColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        indicatorColor = MaterialTheme.colorScheme.surfaceVariant
                                    )
                                )

                                NavigationBarItem(
                                    selected = currentTab == AppNavigationTab.TYPE,
                                    onClick = { viewModel.selectTab(AppNavigationTab.TYPE) },
                                    icon = { Icon(Icons.Default.Keyboard, contentDescription = "Type") },
                                    label = { Text("Type") },
                                    colors = NavigationBarItemDefaults.colors(
                                        selectedIconColor = MaterialTheme.colorScheme.primary,
                                        selectedTextColor = MaterialTheme.colorScheme.primary,
                                        unselectedIconColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        unselectedTextColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                        indicatorColor = MaterialTheme.colorScheme.surfaceVariant
                                    )
                                )
                            }
                        },
                        containerColor = MaterialTheme.colorScheme.background
                    ) { innerPadding ->
                        Box(
                            modifier = Modifier
                                .fillMaxSize()
                                .padding(innerPadding)
                        ) {
                            when (currentTab) {
                                AppNavigationTab.TRACKPAD -> TrackpadSurface(
                                    isConnected = isConnected,
                                    sensitivity = trackpadSensitivity,
                                    onSensitivityChanged = viewModel::setTrackpadSensitivity,
                                    scrollSensitivity = scrollSensitivity,
                                    onScrollSensitivityChanged = viewModel::setScrollSensitivity,
                                    reverseScroll = reverseScroll,
                                    onReverseScrollChanged = viewModel::setReverseScroll,
                                    hapticsEnabled = hapticsEnabled,
                                    onHapticsEnabledChanged = viewModel::setHapticsEnabled,
                                    tapToClick = tapToClick,
                                    onTapToClickChanged = viewModel::setTapToClick,
                                    dragLock = dragLock,
                                    onDragLockChanged = viewModel::setDragLock,
                                    airMouseSensitivity = airMouseSensitivity,
                                    onAirMouseSensitivityChanged = viewModel::setAirMouseSensitivity,
                                    onMove = viewModel::sendMotion,
                                    onScroll = viewModel::sendScroll,
                                    onZoom = viewModel::handleZoom,
                                    onClick = viewModel::click,
                                    onButton = viewModel::setButton,
                                    onSwipeLeft = viewModel::handleThreeFingerSwipeLeft,
                                    onSwipeRight = viewModel::handleThreeFingerSwipeRight,
                                    onSwipeUp = viewModel::handleThreeFingerSwipeUp,
                                    onSwipeDown = viewModel::handleThreeFingerSwipeDown
                                )
                                AppNavigationTab.AIR_MOUSE -> AirMouseSurface(
                                    hasGyroscope = viewModel.hasGyroscope,
                                    isConnected = isConnected,
                                    isTriggerHeld = isAirMouseTriggerHeld,
                                    onTriggerChanged = viewModel::setAirMouseTriggerHeld,
                                    sensitivity = airMouseSensitivity,
                                    onSensitivityChanged = viewModel::setAirMouseSensitivity,
                                    onButton = viewModel::setButton,
                                    onLaserChanged = viewModel::setLaser
                                )
                                AppNavigationTab.SLIDES -> SlideRemoteView(
                                    isConnected = isConnected,
                                    onKey = viewModel::tapKey,
                                    onLaser = viewModel::setLaser
                                )
                                AppNavigationTab.MACROS -> MediaMacrosView(
                                    isConnected = isConnected,
                                    onKey = viewModel::tapKey,
                                    macros = macros,
                                    onRunMacro = viewModel::runMacro,
                                    onSaveMacro = viewModel::saveMacro,
                                    onDeleteMacro = viewModel::deleteMacro,
                                    onResetDefaults = viewModel::resetDefaultMacros,
                                    onExportJson = {
                                        val json = viewModel.exportMacrosJson()
                                        val sendIntent = android.content.Intent().apply {
                                            action = android.content.Intent.ACTION_SEND
                                            putExtra(android.content.Intent.EXTRA_TEXT, json)
                                            type = "application/json"
                                        }
                                        startActivity(android.content.Intent.createChooser(sendIntent, "Export Macros JSON"))
                                    },
                                    onImportJson = {
                                        val cm = getSystemService(CLIPBOARD_SERVICE) as? android.content.ClipboardManager
                                        val clipText = cm?.primaryClip?.getItemAt(0)?.text?.toString()
                                        if (!clipText.isNullOrBlank()) {
                                            viewModel.importMacrosJson(clipText)
                                            android.widget.Toast.makeText(this@MainActivity, "Imported macros from clipboard", android.widget.Toast.LENGTH_SHORT).show()
                                        } else {
                                            android.widget.Toast.makeText(this@MainActivity, "Copy JSON to clipboard first to import", android.widget.Toast.LENGTH_SHORT).show()
                                        }
                                    }
                                )
                                AppNavigationTab.TYPE -> TypeSurface(
                                    isConnected = isConnected,
                                    onSendText = viewModel::typeText,
                                    onLiveDiff = viewModel::applyTypeDiff,
                                    onKey = viewModel::tapKey
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    override fun onStart() {
        super.onStart()
        viewModel.onAppForegrounded()
        if (viewModel.isLocked.value) {
            showBiometricPrompt()
        }
    }

    override fun onStop() {
        super.onStop()
        viewModel.onAppBackgrounded()
    }

    private fun showBiometricPrompt() {
        val executor = ContextCompat.getMainExecutor(this)
        val prompt = BiometricPrompt(
            this,
            executor,
            object : BiometricPrompt.AuthenticationCallback() {
                override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) {
                    super.onAuthenticationSucceeded(result)
                    viewModel.unlock()
                }
            }
        )

        val promptInfo = BiometricPrompt.PromptInfo.Builder()
            .setTitle("Unlock Mouser")
            .setSubtitle("Confirm your fingerprint, face, or device PIN")
            .setAllowedAuthenticators(
                BiometricManager.Authenticators.BIOMETRIC_STRONG or
                BiometricManager.Authenticators.DEVICE_CREDENTIAL
            )
            .build()

        prompt.authenticate(promptInfo)
    }
}
