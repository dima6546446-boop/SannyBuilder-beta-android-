package com.example.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val SunnyBuilderColorScheme = lightColorScheme(
    primary = SleekPrimary,
    onPrimary = Color.White,
    secondary = SleekPurple,
    onSecondary = Color.White,
    tertiary = SleekHighlightBg,
    onTertiary = SleekHighlightText,
    background = SleekBg,
    onBackground = SleekPrimaryText,
    surface = SleekBg,              // Background matches body container
    onSurface = SleekPrimaryText,
    surfaceVariant = SleekSurface,   // Nav and tiles match SleekSurface
    onSurfaceVariant = SleekSecondaryText,
    outline = SleekBorder
)

@Composable
fun MyApplicationTheme(
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = SunnyBuilderColorScheme,
        typography = Typography,
        content = content
    )
}
