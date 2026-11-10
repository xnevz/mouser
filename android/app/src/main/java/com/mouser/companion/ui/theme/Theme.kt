package com.mouser.companion.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val DarkColorScheme = darkColorScheme(
    primary = BluePrimary,
    secondary = EmeraldAccent,
    tertiary = IndigoAccent,
    background = Zinc900,
    surface = Zinc800,
    surfaceVariant = Zinc700,
    onPrimary = Zinc100,
    onBackground = Zinc100,
    onSurface = Zinc100,
    onSurfaceVariant = Zinc400
)

@Composable
fun MouserTheme(
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = DarkColorScheme,
        typography = Typography,
        content = content
    )
}
