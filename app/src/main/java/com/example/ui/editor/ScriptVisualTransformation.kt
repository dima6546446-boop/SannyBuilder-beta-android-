package com.example.ui.editor

import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation
import com.example.ui.theme.*

class ScriptVisualTransformation : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText {
        return TransformedText(
            text = highlightScriptCode(text.text),
            offsetMapping = OffsetMapping.Identity
        )
    }
}

fun highlightScriptCode(text: String): AnnotatedString {
    val builder = AnnotatedString.Builder(text)
    
    // Combined regex for CLEO SCM syntax tokenization
    val regex = Regex(
        "(//[^\\n]*)|" +                                 // Group 1: Comment
        "(\"[^\"]*\"|'[^']*')|" +                        // Group 2: String
        "(\\{\\$[^}]+\\})|" +                            // Group 3: Directive (e.g. {$CLEO .cs})
        "(\\b[0-9A-Fa-f]{4}:)|" +                        // Group 4: Opcode (e.g. 0247:)
        "(:[A-Za-z0-9_]+|@[A-Za-z0-9_]+)|" +             // Group 5: Label (e.g. :LOAD_LOOP, @LOAD_LOOP)
        "(\\$[A-Za-z0-9_]+)|" +                          // Group 6: Variable (e.g. $PLAYER_CHAR)
        "(#[A-Za-z0-9_]+)|" +                            // Group 7: Model (e.g. #HYDRA)
        "(\\b\\d+(?:\\.\\d+)?\\b)|" +                    // Group 8: Number
        "(\\b(?:wait|if|jf|jump|thread|end_thread)\\b)"  // Group 9: Flow Keywords (wait, if, jf, jump)
    )

    regex.findAll(text).forEach { match ->
        val range = match.range
        val start = range.first
        val end = range.last + 1

        when {
            // Comment
            match.groups[1] != null -> {
                builder.addStyle(SpanStyle(color = CodeComment, fontFamily = FontFamily.Monospace), start, end)
            }
            // String
            match.groups[2] != null -> {
                builder.addStyle(SpanStyle(color = CodeString, fontFamily = FontFamily.Monospace), start, end)
            }
            // Directive
            match.groups[3] != null -> {
                builder.addStyle(SpanStyle(color = RetroGold, fontWeight = FontWeight.Bold, fontFamily = FontFamily.Monospace), start, end)
            }
            // Opcode
            match.groups[4] != null -> {
                builder.addStyle(SpanStyle(color = CodeOpcode, fontWeight = FontWeight.Bold, fontFamily = FontFamily.Monospace), start, end)
            }
            // Label
            match.groups[5] != null -> {
                builder.addStyle(SpanStyle(color = CodeLabel, fontWeight = FontWeight.SemiBold, fontFamily = FontFamily.Monospace), start, end)
            }
            // Variable
            match.groups[6] != null -> {
                builder.addStyle(SpanStyle(color = CodeVariable, fontFamily = FontFamily.Monospace), start, end)
            }
            // Model
            match.groups[7] != null -> {
                builder.addStyle(SpanStyle(color = RetroGold, fontWeight = FontWeight.Medium, fontFamily = FontFamily.Monospace), start, end)
            }
            // Number
            match.groups[8] != null -> {
                builder.addStyle(SpanStyle(color = CodeNumber, fontFamily = FontFamily.Monospace), start, end)
            }
            // Flow Keyword
            match.groups[9] != null -> {
                builder.addStyle(SpanStyle(color = CodeKeyword, fontWeight = FontWeight.Bold, fontFamily = FontFamily.Monospace), start, end)
            }
        }
    }

    return builder.toAnnotatedString()
}
