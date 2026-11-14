package com.mouser.companion.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

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
    onSurfaceVariant = Zinc400,
    outline = Zinc700
)

private val LightColorScheme = lightColorScheme(
    primary = BluePrimary,
    secondary = EmeraldAccent,
    tertiary = IndigoAccent,
    background = Slate50,
    surface = SlateSurface,
    surfaceVariant = SlateRaised,
    onPrimary = Color.White,
    onBackground = SlateText,
    onSurface = SlateText,
    onSurfaceVariant = SlateMuted,
    outline = SlateBorder
)

@Composable
fun MouserTheme(
    mode: String = "System",
    content: @Composable () -> Unit
) {
    val dark = when (mode) {
        "Dark" -> true
        "Light" -> false
        else -> isSystemInDarkTheme()
    }
    val scheme = if (dark) DarkColorScheme else LightColorScheme

    MaterialTheme(
        colorScheme = scheme,
        typography = Typography,
        content = content
    )
}
