package com.example.ui.theme

import androidx.compose.ui.graphics.Color

// --- Sleek Interface Theme Palette ---
val SleekBg = Color(0xFFFDFBFF)                 // App Main Background
val SleekSurface = Color(0xFFF3F0F5)            // Navigation & Tile Backgrounds
val SleekBorder = Color(0xFFC4C6D0)             // Dividers and Card outlines
val SleekDivider = Color(0xFFE1E2EC)            // Subtle dividers

val SleekPrimary = Color(0xFF0061A4)            // Primary Blue
val SleekSecondaryText = Color(0xFF44474E)      // Secondary label/info text
val SleekPrimaryText = Color(0xFF1A1C1E)        // Main dark body/header text

// Interactive Highlights
val SleekHighlightBg = Color(0xFFD1E4FF)        // Active tabs, pill selections, highlight containers
val SleekHighlightText = Color(0xFF001D36)      // High contrast text inside highlights
val SleekPurple = Color(0xFF6750A4)             // Visual accents & focused borders
val SleekDanger = Color(0xFFBA1A1A)             // Error state / Delete action

// --- Code Editor Syntax Highlighting (Sleek Light Mode SCM Syntax) ---
val CodeBackground = Color(0xFFF6F3F7)          // Clean pale slate/lavender background
val CodeTextNormal = Color(0xFF1A1C1E)          // Normal code text
val CodeComment = Color(0xFF70757A)             // Muted comments
val CodeString = Color(0xFF1B5E20)              // Soft Forest Green
val CodeOpcode = Color(0xFF0061A4)              // Sleek Royal Blue
val CodeLabel = Color(0xFFB06000)               // Soft Burnt Orange
val CodeKeyword = Color(0xFFB3261E)             // Crimson Flow Keywords
val CodeVariable = Color(0xFF6750A4)            // Sleek Violet Variables
val CodeNumber = Color(0xFFE37400)              // Muted Gold Numbers

// --- Compatibility color mappings for old code ---
val CyberGreen = SleekPrimary                    // Map classic green actions to Sleek Blue
val CyberBlue = SleekPurple                     // Map classic blue indicators to Sleek Purple
val RetroGold = Color(0xFFB06000)               // Map yellow warnings to clean deep gold
val CarbonGray = SleekSurface                   // Map old carbon background to Sleek Surface
val SlateDark = SleekBg                         // Map old slate dark back to Sleek Bg
val BorderGray = SleekBorder                    // Map old borders to Sleek Border
