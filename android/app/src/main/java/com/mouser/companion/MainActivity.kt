package com.mouser.companion

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Gamepad
import androidx.compose.material.icons.filled.Navigation
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import com.mouser.companion.model.HostConnectionState
import com.mouser.companion.ui.components.*
import com.mouser.companion.ui.theme.*

class MainActivity : ComponentActivity() {

    private val viewModel: MouserViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            val themeMode by viewModel.themeMode.collectAsState()
            MouserTheme(mode = themeMode) {
                val connectionState by viewModel.connectionState.collectAsState()
                val discoveredHosts by viewModel.discoveredHosts.collectAsState()
                val discoveryError by viewModel.discoveryError.collectAsState()
                val currentTab by viewModel.currentTab.collectAsState()
                val trackpadSensitivity by viewModel.trackpadSensitivity.collectAsState()
                val airMouseSensitivity by viewModel.airMouseSensitivity.collectAsState()
                val isAirMouseTriggerHeld by viewModel.isAirMouseTriggerHeld.collectAsState()
                val isConnected = connectionState is HostConnectionState.Connected

                Scaffold(
                    topBar = {
                        ConnectionBar(
                            connectionState = connectionState,
                            discoveredHosts = discoveredHosts,
                            discoveryError = discoveryError,
                            themeMode = themeMode,
                            onThemeModeChanged = viewModel::setThemeMode,
                            onConnect = viewModel::connect,
                            onSubmitPin = viewModel::submitPin,
                            onDisconnect = viewModel::disconnect
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
                                onMove = viewModel::sendMotion,
                                onScroll = viewModel::sendScroll,
                                onClick = viewModel::click,
                                onButton = viewModel::setButton
                            )
                            AppNavigationTab.AIR_MOUSE -> AirMouseSurface(
                                hasGyroscope = viewModel.hasGyroscope,
                                isConnected = isConnected,
                                isTriggerHeld = isAirMouseTriggerHeld,
                                onTriggerChanged = viewModel::setAirMouseTriggerHeld,
                                sensitivity = airMouseSensitivity,
                                onSensitivityChanged = viewModel::setAirMouseSensitivity,
                                onButton = viewModel::setButton
                            )
                            AppNavigationTab.MACROS -> MediaMacrosView(
                                isConnected = isConnected,
                                onKey = viewModel::tapKey
                            )
                        }
                    }
                }
            }
        }
    }
}
