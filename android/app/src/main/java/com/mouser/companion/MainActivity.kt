package com.mouser.companion

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Gamepad
import androidx.compose.material.icons.filled.Navigation
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.mouser.companion.ui.components.*
import com.mouser.companion.ui.theme.*

class MainActivity : ComponentActivity() {

    private val viewModel: MouserViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MouserTheme {
                val connectionState by viewModel.connectionState.collectAsState()
                val discoveredHosts by viewModel.discoveredHosts.collectAsState()
                val currentTab by viewModel.currentTab.collectAsState()
                val trackpadSensitivity by viewModel.trackpadSensitivity.collectAsState()
                val airMouseSensitivity by viewModel.airMouseSensitivity.collectAsState()
                val isAirMouseTriggerHeld by viewModel.isAirMouseTriggerHeld.collectAsState()

                Scaffold(
                    topBar = {
                        ConnectionBar(
                            connectionState = connectionState,
                            discoveredHosts = discoveredHosts,
                            onConnectToHost = { ip, port -> viewModel.connectToHost(ip, port) },
                            onDisconnect = { viewModel.disconnect() }
                        )
                    },
                    bottomBar = {
                        NavigationBar(
                            containerColor = Zinc800
                        ) {
                            NavigationBarItem(
                                selected = currentTab == AppNavigationTab.TRACKPAD,
                                onClick = { viewModel.selectTab(AppNavigationTab.TRACKPAD) },
                                icon = { Icon(Icons.Default.TouchApp, contentDescription = "Trackpad") },
                                label = { Text("Trackpad") },
                                colors = NavigationBarItemDefaults.colors(
                                    selectedIconColor = BluePrimary,
                                    selectedTextColor = BluePrimary,
                                    unselectedIconColor = Zinc400,
                                    unselectedTextColor = Zinc400,
                                    indicatorColor = Zinc700
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
                                    unselectedIconColor = Zinc400,
                                    unselectedTextColor = Zinc400,
                                    indicatorColor = Zinc700
                                )
                            )

                            NavigationBarItem(
                                selected = currentTab == AppNavigationTab.MACROS,
                                onClick = { viewModel.selectTab(AppNavigationTab.MACROS) },
                                icon = { Icon(Icons.Default.Gamepad, contentDescription = "Macros") },
                                label = { Text("Macros") },
                                colors = NavigationBarItemDefaults.colors(
                                    selectedIconColor = IndigoAccent,
                                    selectedTextColor = IndigoAccent,
                                    unselectedIconColor = Zinc400,
                                    unselectedTextColor = Zinc400,
                                    indicatorColor = Zinc700
                                )
                            )
                        }
                    },
                    containerColor = Zinc900
                ) { innerPadding ->
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(innerPadding)
                    ) {
                        when (currentTab) {
                            AppNavigationTab.TRACKPAD -> {
                                TrackpadSurface(
                                    sensitivity = trackpadSensitivity,
                                    onMotionDelta = { dx, dy -> viewModel.sendMotion(dx, dy) },
                                    onButtonClick = { button, isDown -> viewModel.sendButtonClick(button, isDown) }
                                )
                            }
                            AppNavigationTab.AIR_MOUSE -> {
                                AirMouseSurface(
                                    isTriggerHeld = isAirMouseTriggerHeld,
                                    onTriggerChanged = { held -> viewModel.setAirMouseTriggerHeld(held) },
                                    sensitivity = airMouseSensitivity,
                                    onSensitivityChanged = { s -> viewModel.setAirMouseSensitivity(s) },
                                    onButtonClick = { button, isDown -> viewModel.sendButtonClick(button, isDown) }
                                )
                            }
                            AppNavigationTab.MACROS -> {
                                MediaMacrosView(
                                    onSendKey = { keyCode, isDown -> viewModel.sendKey(keyCode, isDown) }
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}
