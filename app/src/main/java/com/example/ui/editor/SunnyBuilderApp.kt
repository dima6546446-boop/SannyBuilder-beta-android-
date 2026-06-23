package com.example.ui.editor

import android.content.Context
import android.net.Uri
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.*
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.HelpOutline
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.data.Opcode
import com.example.data.OpcodeDatabase
import com.example.data.ScriptProject
import com.example.ui.theme.*
import com.example.ui.viewmodel.ChatMessage
import com.example.ui.viewmodel.CompilerReport
import com.example.ui.viewmodel.SunnyBuilderViewModel
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SunnyBuilderApp(viewModel: SunnyBuilderViewModel) {
    val context = LocalContext.current
    var selectedTab by remember { mutableStateOf(2) } // Default to Dashboard (Projects Tab)
    
    val activeTitle by viewModel.activeTitle.collectAsStateWithLifecycle()
    val activeCode by viewModel.activeCode.collectAsStateWithLifecycle()
    val activeProjectId by viewModel.activeProjectId.collectAsStateWithLifecycle()
    
    val projects by viewModel.projects.collectAsStateWithLifecycle()
    val filteredOpcodes by viewModel.filteredOpcodes.collectAsStateWithLifecycle()
    val searchQuery by viewModel.searchQuery.collectAsStateWithLifecycle()
    val selectedCategory by viewModel.selectedCategory.collectAsStateWithLifecycle()
    
    val compilerReport by viewModel.compilerReport.collectAsStateWithLifecycle()
    
    val aiMessages by viewModel.aiMessages.collectAsStateWithLifecycle()
    val isAiLoading by viewModel.isAiLoading.collectAsStateWithLifecycle()

    val decompilerHex by viewModel.decompilerHex.collectAsStateWithLifecycle()
    val decompilerLogs by viewModel.decompilerLogs.collectAsStateWithLifecycle()
    val decompiledCode by viewModel.decompiledCode.collectAsStateWithLifecycle()
    val isDecompiling by viewModel.isDecompiling.collectAsStateWithLifecycle()

    var showRenameDialog by remember { mutableStateOf(false) }
    var tempRenameTitle by remember { mutableStateOf("") }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        // Drawer menu icon from template
                        Icon(
                            imageVector = Icons.Default.Menu,
                            contentDescription = "Menu",
                            tint = SleekPrimaryText,
                            modifier = Modifier
                                .size(24.dp)
                                .clickable {
                                    Toast.makeText(context, "SunnyBuilder v1.0 • Sleek Style", Toast.LENGTH_SHORT).show()
                                }
                        )
                        Spacer(modifier = Modifier.width(12.dp))
                        Column {
                            Text(
                                "SunnyBuilder",
                                color = SleekPrimaryText,
                                fontWeight = FontWeight.Bold,
                                fontSize = 18.sp,
                                fontFamily = FontFamily.SansSerif
                            )
                            Text(
                                "GTA SCM & CLEO Studio",
                                color = SleekSecondaryText,
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Medium
                            )
                        }
                    }
                },
                actions = {
                    IconButton(onClick = {
                        tempRenameTitle = activeTitle
                        showRenameDialog = true
                    }) {
                        Icon(Icons.Default.Edit, contentDescription = "Rename script", tint = SleekPrimary)
                    }
                    IconButton(onClick = {
                        viewModel.saveCurrentProject()
                        Toast.makeText(context, "Скрипт сохранен!", Toast.LENGTH_SHORT).show()
                    }) {
                        Icon(Icons.Default.Save, contentDescription = "Save script", tint = SleekPrimary)
                    }
                    IconButton(onClick = {
                        viewModel.createNewProject()
                        Toast.makeText(context, "Создан новый файл", Toast.LENGTH_SHORT).show()
                        selectedTab = 0 // Switch to Editor directly
                    }) {
                        Icon(Icons.Default.Add, contentDescription = "New script", tint = SleekPrimary)
                    }
                    Spacer(modifier = Modifier.width(6.dp))
                    // Rounded sleek avatar badge "SB"
                    Box(
                        modifier = Modifier
                            .size(32.dp)
                            .clip(CircleShape)
                            .background(SleekHighlightBg),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "SB",
                            color = SleekHighlightText,
                            fontWeight = FontWeight.Bold,
                            fontSize = 11.sp
                        )
                    }
                    Spacer(modifier = Modifier.width(12.dp))
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = SleekBg,
                    titleContentColor = SleekPrimaryText
                )
            )
        },
        bottomBar = {
            NavigationBar(
                containerColor = SleekSurface,
                tonalElevation = 4.dp,
                modifier = Modifier.border(
                    BorderStroke(1.dp, SleekBorder),
                    RoundedCornerShape(topStart = 16.dp, topEnd = 16.dp)
                )
            ) {
                NavigationBarItem(
                    selected = selectedTab == 2, // Dashboard/Projects tab is the first in HTML layout
                    onClick = { selectedTab = 2 },
                    icon = { Icon(Icons.Default.Home, contentDescription = "Home") },
                    label = { Text("Главная", fontSize = 10.sp) },
                    colors = NavigationBarItemDefaults.colors(
                        selectedIconColor = SleekHighlightText,
                        selectedTextColor = SleekHighlightText,
                        indicatorColor = SleekHighlightBg,
                        unselectedIconColor = SleekSecondaryText,
                        unselectedTextColor = SleekSecondaryText
                    )
                )
                NavigationBarItem(
                    selected = selectedTab == 0,
                    onClick = { selectedTab = 0 },
                    icon = { Icon(Icons.Default.Code, contentDescription = "Editor") },
                    label = { Text("Редактор", fontSize = 10.sp) },
                    colors = NavigationBarItemDefaults.colors(
                        selectedIconColor = SleekHighlightText,
                        selectedTextColor = SleekHighlightText,
                        indicatorColor = SleekHighlightBg,
                        unselectedIconColor = SleekSecondaryText,
                        unselectedTextColor = SleekSecondaryText
                    )
                )
                NavigationBarItem(
                    selected = selectedTab == 4,
                    onClick = { selectedTab = 4 },
                    icon = { Icon(Icons.Default.SettingsBackupRestore, contentDescription = "Decompiler") },
                    label = { Text("Декомпилятор", fontSize = 10.sp) },
                    colors = NavigationBarItemDefaults.colors(
                        selectedIconColor = SleekHighlightText,
                        selectedTextColor = SleekHighlightText,
                        indicatorColor = SleekHighlightBg,
                        unselectedIconColor = SleekSecondaryText,
                        unselectedTextColor = SleekSecondaryText
                    )
                )
                NavigationBarItem(
                    selected = selectedTab == 1,
                    onClick = { selectedTab = 1 },
                    icon = { Icon(Icons.Default.Layers, contentDescription = "Opcodes") },
                    label = { Text("Опкоды", fontSize = 10.sp) },
                    colors = NavigationBarItemDefaults.colors(
                        selectedIconColor = SleekHighlightText,
                        selectedTextColor = SleekHighlightText,
                        indicatorColor = SleekHighlightBg,
                        unselectedIconColor = SleekSecondaryText,
                        unselectedTextColor = SleekSecondaryText
                    )
                )
                NavigationBarItem(
                    selected = selectedTab == 3,
                    onClick = { selectedTab = 3 },
                    icon = { Icon(Icons.Default.SmartToy, contentDescription = "AI Assistant") },
                    label = { Text("Ассистент", fontSize = 10.sp) },
                    colors = NavigationBarItemDefaults.colors(
                        selectedIconColor = SleekHighlightText,
                        selectedTextColor = SleekHighlightText,
                        indicatorColor = SleekHighlightBg,
                        unselectedIconColor = SleekSecondaryText,
                        unselectedTextColor = SleekSecondaryText
                    )
                )
            }
        },
        containerColor = SleekBg
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
        ) {
            when (selectedTab) {
                0 -> EditorTab(
                    title = activeTitle,
                    code = activeCode,
                    compilerReport = compilerReport,
                    onCodeChange = { viewModel.updateCode(it) },
                    onCompile = { viewModel.compileActiveScript() },
                    onSave = { viewModel.saveCurrentProject() },
                    onClearReport = { viewModel.clearCompilerReport() }
                )
                1 -> OpcodesTab(
                    searchQuery = searchQuery,
                    selectedCategory = selectedCategory,
                    opcodesList = filteredOpcodes,
                    onQueryChange = { viewModel.setOpcodeQuery(it) },
                    onCategoryChange = { viewModel.setOpcodeCategory(it) },
                    onInsertOpcode = { opcodeText ->
                        viewModel.insertTextAtSelection(activeCode.length, "\n$opcodeText\n")
                        Toast.makeText(context, "Вставлено в редактор!", Toast.LENGTH_SHORT).show()
                        selectedTab = 0
                    }
                )
                2 -> ProjectsTab(
                    projects = projects,
                    activeProjectId = activeProjectId,
                    activeTitle = activeTitle,
                    activeCode = activeCode,
                    compilerReport = compilerReport,
                    onLoadProject = { project ->
                        viewModel.loadProject(project)
                        Toast.makeText(context, "Загружен скрипт: ${project.title}", Toast.LENGTH_SHORT).show()
                        selectedTab = 0
                    },
                    onDeleteProject = { project ->
                        viewModel.deleteProject(project)
                        Toast.makeText(context, "Удалено", Toast.LENGTH_SHORT).show()
                    },
                    onSwitchTab = { selectedTab = it }
                )
                3 -> AssistantTab(
                    messages = aiMessages,
                    isLoading = isAiLoading,
                    onSendMessage = { viewModel.sendAiPrompt(it) },
                    onLoadCode = { generatedCode ->
                        viewModel.updateCode(generatedCode)
                        Toast.makeText(context, "Код скопирован в редактор!", Toast.LENGTH_SHORT).show()
                        selectedTab = 0
                    },
                    onClearChat = { viewModel.clearChat() }
                )
                4 -> DecompilerTab(
                    decompilerHex = decompilerHex,
                    decompilerLogs = decompilerLogs,
                    decompiledCode = decompiledCode,
                    isDecompiling = isDecompiling,
                    onDecompile = { hex, useAi -> viewModel.runDecompilation(hex, useAi) },
                    onLoadDecompiled = { code ->
                        viewModel.updateCode(code)
                        Toast.makeText(context, "Декомпилированный код загружен в редактор!", Toast.LENGTH_SHORT).show()
                        selectedTab = 0
                    }
                )
            }
        }
    }

    // --- Rename File Dialog ---
    if (showRenameDialog) {
        AlertDialog(
            onDismissRequest = { showRenameDialog = false },
            title = { Text("Переименовать файл", color = SleekPrimaryText, fontWeight = FontWeight.Bold, fontFamily = FontFamily.SansSerif) },
            text = {
                OutlinedTextField(
                    value = tempRenameTitle,
                    onValueChange = { tempRenameTitle = it },
                    label = { Text("Название скрипта") },
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = SleekPrimary,
                        unfocusedBorderColor = SleekBorder,
                        focusedLabelColor = SleekPrimary,
                        unfocusedLabelColor = SleekSecondaryText,
                        focusedTextColor = SleekPrimaryText,
                        unfocusedTextColor = SleekPrimaryText
                    ),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            },
            confirmButton = {
                TextButton(onClick = {
                    if (tempRenameTitle.isNotBlank()) {
                        viewModel.updateTitle(tempRenameTitle)
                        viewModel.saveCurrentProject()
                        showRenameDialog = false
                    }
                }) {
                    Text("OK", color = SleekPrimary, fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(onClick = { showRenameDialog = false }) {
                    Text("Отмена", color = SleekSecondaryText)
                }
            },
            containerColor = SleekSurface
        )
    }
}

// ================= EDITOR TAB =================

@Composable
fun EditorTab(
    title: String,
    code: String,
    compilerReport: CompilerReport?,
    onCodeChange: (String) -> Unit,
    onCompile: () -> Unit,
    onSave: () -> Unit,
    onClearReport: () -> Unit
) {
    val scrollState = rememberScrollState()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        // Active file header
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Default.Description,
                    contentDescription = "Active File",
                    tint = SleekPrimary,
                    modifier = Modifier.size(18.dp)
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text(
                    text = title,
                    color = SleekPrimaryText,
                    fontWeight = FontWeight.Bold,
                    fontSize = 14.sp,
                    fontFamily = FontFamily.SansSerif,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
            Text(
                text = "${code.lines().size} строк",
                color = SleekSecondaryText,
                fontSize = 12.sp,
                fontFamily = FontFamily.Monospace
            )
        }

        // Quick Tray Toolbar
        QuickInsertTray(
            onInsert = { insertion ->
                onCodeChange(code + insertion)
            }
        )

        Spacer(modifier = Modifier.height(8.dp))

        // Code Editor Box (Line Numbers & Text Field Scrollable Row)
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f)
                .background(CodeBackground, RoundedCornerShape(12.dp))
                .border(BorderStroke(1.dp, SleekBorder), RoundedCornerShape(12.dp))
                .padding(12.dp)
        ) {
            val lines = code.split("\n")
            
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(scrollState)
            ) {
                // Line Numbers
                Column(
                    modifier = Modifier
                        .width(28.dp)
                        .padding(end = 8.dp),
                    horizontalAlignment = Alignment.End
                ) {
                    for (i in 1..maxOf(1, lines.size)) {
                        Text(
                            text = "$i",
                            color = SleekSecondaryText.copy(alpha = 0.5f),
                            fontFamily = FontFamily.Monospace,
                            fontSize = 13.sp,
                            textAlign = TextAlign.End,
                            lineHeight = 20.sp
                        )
                    }
                }

                // Vertical Divider Line
                Box(
                    modifier = Modifier
                        .width(1.dp)
                        .fillMaxHeight()
                        .background(SleekBorder)
                )

                // Editable Script area
                BasicTextField(
                    value = code,
                    onValueChange = onCodeChange,
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(start = 12.dp),
                    textStyle = TextStyle(
                        color = SleekPrimaryText,
                        fontFamily = FontFamily.Monospace,
                        fontSize = 13.sp,
                        lineHeight = 20.sp
                    ),
                    visualTransformation = ScriptVisualTransformation(),
                    cursorBrush = SolidColor(SleekPurple),
                    keyboardOptions = KeyboardOptions(
                        capitalization = KeyboardCapitalization.None,
                        autoCorrectEnabled = false,
                        keyboardType = KeyboardType.Ascii
                    )
                )
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        // Editor Buttons Control Layout
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            OutlinedButton(
                onClick = onSave,
                modifier = Modifier.weight(1f),
                colors = ButtonDefaults.outlinedButtonColors(contentColor = SleekPrimary),
                border = BorderStroke(1.dp, SleekPrimary),
                shape = RoundedCornerShape(8.dp)
            ) {
                Icon(Icons.Default.Save, contentDescription = "Save")
                Spacer(modifier = Modifier.width(6.dp))
                Text("Сохранить", fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Bold)
            }

            Button(
                onClick = onCompile,
                modifier = Modifier.weight(1f),
                colors = ButtonDefaults.buttonColors(
                    containerColor = SleekPrimary,
                    contentColor = Color.White
                ),
                shape = RoundedCornerShape(8.dp)
            ) {
                Icon(Icons.Default.Build, contentDescription = "Compile")
                Spacer(modifier = Modifier.width(6.dp))
                Text("Скомпилить", fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Bold)
            }
        }

        // Compiler Reports Panel
        compilerReport?.let { report ->
            Spacer(modifier = Modifier.height(12.dp))
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 200.dp)
                    .border(
                        BorderStroke(1.dp, if (report.success) SleekPrimary else SleekDanger),
                        RoundedCornerShape(12.dp)
                    ),
                colors = CardDefaults.cardColors(
                    containerColor = if (report.success) SleekHighlightBg else Color(0xFFFDE8E8)
                ),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                imageVector = if (report.success) Icons.Default.CheckCircle else Icons.Default.Error,
                                contentDescription = "Status",
                                tint = if (report.success) SleekPrimary else SleekDanger,
                                modifier = Modifier.size(16.dp)
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                text = if (report.success) "СБОРКА УСПЕШНА" else "ОШИБКА СБОРКИ",
                                color = if (report.success) SleekHighlightText else SleekDanger,
                                fontWeight = FontWeight.Bold,
                                fontSize = 12.sp,
                                fontFamily = FontFamily.SansSerif
                            )
                        }
                        IconButton(
                            onClick = onClearReport,
                            modifier = Modifier.size(24.dp)
                        ) {
                            Icon(Icons.Default.Close, contentDescription = "Close logs", tint = SleekSecondaryText, modifier = Modifier.size(14.dp))
                        }
                    }

                    if (report.success && report.outputFileName != null) {
                        Text(
                            text = "Размер файла: ${report.fileSizeInBytes} Б | Выходной файл: ${report.outputFileName}",
                            color = SleekHighlightText.copy(alpha = 0.8f),
                            fontSize = 11.sp,
                            fontFamily = FontFamily.Monospace,
                            modifier = Modifier.padding(vertical = 4.dp)
                        )
                    }

                    Divider(color = SleekBorder, modifier = Modifier.padding(vertical = 6.dp))

                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                    ) {
                        items(report.logs) { log ->
                            val color = when {
                                log.startsWith("[SUCCESS]") -> SleekPrimary
                                log.startsWith("[WARN]") -> SleekPurple
                                log.startsWith("[ERROR]") -> SleekDanger
                                else -> SleekPrimaryText
                            }
                            Text(
                                text = log,
                                color = color,
                                fontSize = 11.sp,
                                fontFamily = FontFamily.Monospace,
                                modifier = Modifier.padding(bottom = 2.dp)
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun QuickInsertTray(onInsert: (String) -> Unit) {
    val items = listOf(
        "{\$CLEO .cs}" to "{\$CLEO .cs}\n",
        "thread" to "thread 'THREADNAME'\n",
        "wait 0 ms" to "0001: wait 0 ms\n",
        "wait 100 ms" to "0001: wait 100 ms\n",
        "Player Actor" to "${'$'}PLAYER_ACTOR",
        "Player Char" to "${'$'}PLAYER_CHAR",
        "if ... jf" to "if\n  \njf @LABEL",
        "jump @" to "jump @LABEL",
        "load model" to "0247: load_model #MODEL\n",
        "model ready" to "0248: model #MODEL available\n"
    )

    LazyRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        contentPadding = PaddingValues(vertical = 4.dp)
    ) {
        items(items) { (label, value) ->
            SuggestionChip(
                onClick = { onInsert(value) },
                label = {
                    Text(
                        label,
                        color = SleekPrimary,
                        fontFamily = FontFamily.Monospace,
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold
                    )
                },
                border = BorderStroke(1.dp, SleekPrimary.copy(alpha = 0.3f)),
                colors = SuggestionChipDefaults.suggestionChipColors(
                    containerColor = SleekSurface
                )
            )
        }
    }
}

// ================= OPCODES LOOKUP TAB =================

@Composable
fun OpcodesTab(
    searchQuery: String,
    selectedCategory: String,
    opcodesList: List<Opcode>,
    onQueryChange: (String) -> Unit,
    onCategoryChange: (String) -> Unit,
    onInsertOpcode: (String) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        // Search Input
        OutlinedTextField(
            value = searchQuery,
            onValueChange = onQueryChange,
            placeholder = { Text("Поиск опкодов, ключевых слов...", color = SleekSecondaryText) },
            leadingIcon = { Icon(Icons.Default.Search, contentDescription = null, tint = SleekPrimary) },
            trailingIcon = if (searchQuery.isNotEmpty()) {
                {
                    IconButton(onClick = { onQueryChange("") }) {
                        Icon(Icons.Default.Clear, contentDescription = "Clear search", tint = SleekSecondaryText)
                    }
                }
            } else null,
            colors = OutlinedTextFieldDefaults.colors(
                focusedBorderColor = SleekPrimary,
                unfocusedBorderColor = SleekBorder,
                focusedTextColor = SleekPrimaryText,
                unfocusedTextColor = SleekPrimaryText
            ),
            singleLine = true,
            modifier = Modifier.fillMaxWidth()
        )

        Spacer(modifier = Modifier.height(12.dp))

        // Categories list
        LazyRow(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(OpcodeDatabase.categories) { category ->
                val selected = selectedCategory == category
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(16.dp))
                        .background(if (selected) SleekPrimary else SleekSurface)
                        .clickable { onCategoryChange(category) }
                        .padding(horizontal = 14.dp, vertical = 6.dp)
                        .border(
                            1.dp,
                            if (selected) SleekPrimary else SleekBorder,
                            RoundedCornerShape(16.dp)
                        )
                ) {
                    Text(
                        text = category,
                        color = if (selected) Color.White else SleekPrimaryText,
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold,
                        fontFamily = FontFamily.SansSerif
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        // Opcode lists representation
        if (opcodesList.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(
                        Icons.AutoMirrored.Filled.HelpOutline,
                        contentDescription = "Not found",
                        tint = SleekSecondaryText,
                        modifier = Modifier.size(48.dp)
                    )
                    Spacer(modifier = Modifier.height(12.dp))
                    Text(
                        "Опкоды не найдены",
                        color = SleekPrimaryText,
                        fontWeight = FontWeight.Bold,
                        fontFamily = FontFamily.SansSerif
                    )
                    Text(
                        "Попробуйте изменить запрос.",
                        color = SleekSecondaryText,
                        fontSize = 12.sp,
                        textAlign = TextAlign.Center
                    )
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                items(opcodesList) { opcode ->
                    OpcodeCard(
                        opcode = opcode,
                        onInsert = { onInsertOpcode(opcode.code + ": " + opcode.syntax) }
                    )
                }
            }
        }
    }
}

@Composable
fun OpcodeCard(opcode: Opcode, onInsert: () -> Unit) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .border(BorderStroke(1.dp, SleekBorder), RoundedCornerShape(12.dp)),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .background(SleekHighlightBg, RoundedCornerShape(6.dp))
                            .border(BorderStroke(1.dp, SleekPrimary.copy(alpha = 0.2f)), RoundedCornerShape(6.dp))
                            .padding(horizontal = 8.dp, vertical = 3.dp)
                    ) {
                        Text(
                            text = opcode.code,
                            color = SleekHighlightText,
                            fontWeight = FontWeight.Bold,
                            fontFamily = FontFamily.Monospace,
                            fontSize = 11.sp
                        )
                    }
                    Spacer(modifier = Modifier.width(8.dp))
                    Box(
                        modifier = Modifier
                            .background(SleekSurface, RoundedCornerShape(6.dp))
                            .padding(horizontal = 8.dp, vertical = 3.dp)
                    ) {
                        Text(
                            text = opcode.category,
                            color = SleekPurple,
                            fontSize = 9.sp,
                            fontWeight = FontWeight.Bold,
                            fontFamily = FontFamily.SansSerif
                        )
                    }
                }

                IconButton(
                    onClick = onInsert,
                    modifier = Modifier.size(28.dp)
                ) {
                    Icon(
                        Icons.Default.Input,
                        contentDescription = "Insert into editor",
                        tint = SleekPrimary,
                        modifier = Modifier.size(16.dp)
                    )
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            Text(
                text = opcode.syntax,
                color = SleekPrimaryText,
                fontWeight = FontWeight.Bold,
                fontFamily = FontFamily.Monospace,
                fontSize = 13.sp,
                modifier = Modifier.padding(bottom = 4.dp)
            )

            Text(
                text = opcode.description,
                color = SleekSecondaryText,
                fontSize = 11.sp,
                lineHeight = 15.sp
            )
        }
    }
}

// ================= PROJECTS LIST / HOME TAB =================

@Composable
fun ProjectsTab(
    projects: List<ScriptProject>,
    activeProjectId: Int?,
    activeTitle: String,
    activeCode: String,
    compilerReport: CompilerReport?,
    onLoadProject: (ScriptProject) -> Unit,
    onDeleteProject: (ScriptProject) -> Unit,
    onSwitchTab: (Int) -> Unit
) {
    val scrollState = rememberScrollState()
    
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(scrollState)
            .padding(16.dp)
    ) {
        // --- 1. Active Project Card (High Contrast Blue) ---
        val statusText = when {
            compilerReport == null -> "READY"
            compilerReport.success -> "SUCCESSFUL"
            else -> "ERROR"
        }
        val progressPercent = when {
            compilerReport == null -> 84
            compilerReport.success -> 100
            else -> 45
        }
        
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 16.dp),
            colors = CardDefaults.cardColors(containerColor = SleekHighlightBg),
            shape = RoundedCornerShape(24.dp),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Column(modifier = Modifier.padding(20.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.Top
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "ACTIVE PROJECT",
                            color = SleekHighlightText.copy(alpha = 0.7f),
                            fontWeight = FontWeight.Bold,
                            fontSize = 11.sp,
                            letterSpacing = 1.sp
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = activeTitle,
                            color = SleekHighlightText,
                            fontWeight = FontWeight.Bold,
                            fontSize = 22.sp,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                    }
                    
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(100.dp))
                            .background(Color.White.copy(alpha = 0.5f))
                            .padding(horizontal = 12.dp, vertical = 6.dp)
                    ) {
                        Text(
                            text = statusText,
                            color = SleekHighlightText,
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
                
                Spacer(modifier = Modifier.height(20.dp))
                
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "Optimization Progress",
                        color = SleekHighlightText,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                    Text(
                        text = "$progressPercent%",
                        color = SleekHighlightText,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
                Spacer(modifier = Modifier.height(8.dp))
                // Progress Bar: SleekPrimary over SleekHighlightText 10%
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(8.dp)
                        .clip(CircleShape)
                        .background(SleekHighlightText.copy(alpha = 0.1f))
                ) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth(progressPercent / 100f)
                            .fillMaxHeight()
                            .clip(CircleShape)
                            .background(SleekPrimary)
                    )
                }
            }
        }

        // --- 2. Grid metrics (Editor & Library) ---
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 20.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            // Editor tab shortcut
            Card(
                modifier = Modifier
                    .weight(1f)
                    .height(110.dp)
                    .clickable { onSwitchTab(0) }
                    .border(BorderStroke(1.dp, Color.Transparent), RoundedCornerShape(16.dp)),
                colors = CardDefaults.cardColors(containerColor = SleekSurface),
                shape = RoundedCornerShape(16.dp)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(14.dp),
                    verticalArrangement = Arrangement.SpaceBetween
                ) {
                    Icon(
                        imageVector = Icons.Default.Code,
                        contentDescription = "Editor",
                        tint = SleekPurple,
                        modifier = Modifier.size(24.dp)
                    )
                    Column {
                        Text(
                            "Editor",
                            color = SleekPrimaryText,
                            fontWeight = FontWeight.Bold,
                            fontSize = 14.sp
                        )
                        Text(
                            "${activeCode.lines().size} Lines",
                            color = SleekSecondaryText,
                            fontSize = 11.sp
                        )
                    }
                }
            }

            // Library tab shortcut
            Card(
                modifier = Modifier
                    .weight(1f)
                    .height(110.dp)
                    .clickable { onSwitchTab(1) }
                    .border(BorderStroke(1.dp, Color.Transparent), RoundedCornerShape(16.dp)),
                colors = CardDefaults.cardColors(containerColor = SleekSurface),
                shape = RoundedCornerShape(16.dp)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(14.dp),
                    verticalArrangement = Arrangement.SpaceBetween
                ) {
                    Icon(
                        imageVector = Icons.Default.Layers,
                        contentDescription = "Library",
                        tint = SleekPurple,
                        modifier = Modifier.size(24.dp)
                    )
                    Column {
                        Text(
                            "Library",
                            color = SleekPrimaryText,
                            fontWeight = FontWeight.Bold,
                            fontSize = 14.sp
                        )
                        Text(
                            "${OpcodeDatabase.opcodes.size} OP Codes",
                            color = SleekSecondaryText,
                            fontSize = 11.sp
                        )
                    }
                }
            }
        }

        // --- 3. Recent Scripts Section ---
        Text(
            text = "Recent Scripts",
            color = SleekSecondaryText,
            fontWeight = FontWeight.Bold,
            fontSize = 13.sp,
            modifier = Modifier.padding(horizontal = 4.dp, vertical = 8.dp)
        )
        
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .border(BorderStroke(1.dp, SleekBorder), RoundedCornerShape(20.dp)),
            colors = CardDefaults.cardColors(containerColor = Color.White),
            shape = RoundedCornerShape(20.dp)
        ) {
            if (projects.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(32.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text("No scripts found. Create one above!", color = SleekSecondaryText)
                }
            } else {
                Column {
                    projects.forEachIndexed { index, project ->
                        val isActive = project.id == activeProjectId
                        ProjectRow(
                            project = project,
                            isActive = isActive,
                            onLoad = { onLoadProject(project) },
                            onDelete = { onDeleteProject(project) }
                        )
                        if (index < projects.size - 1) {
                            Divider(color = SleekDivider, thickness = 1.dp)
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun ProjectRow(
    project: ScriptProject,
    isActive: Boolean,
    onLoad: () -> Unit,
    onDelete: () -> Unit
) {
    val dateString = remember(project.lastModified) {
        val formatter = SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault())
        formatter.format(Date(project.lastModified))
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onLoad() }
            .background(if (isActive) SleekHighlightBg.copy(alpha = 0.4f) else Color.Transparent)
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        // Gray square icon container matching Tailwind background
        Box(
            modifier = Modifier
                .size(40.dp)
                .clip(RoundedCornerShape(12.dp))
                .background(SleekSurface),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Default.Description,
                contentDescription = null,
                tint = SleekSecondaryText,
                modifier = Modifier.size(20.dp)
            )
        }
        
        Spacer(modifier = Modifier.width(14.dp))
        
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = project.title,
                color = SleekPrimaryText,
                fontWeight = FontWeight.SemiBold,
                fontSize = 14.sp,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = "Modified $dateString",
                color = SleekSecondaryText,
                fontSize = 11.sp
            )
        }

        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            // Load trigger indicator
            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    .clickable { onLoad() },
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Default.Input,
                    contentDescription = "Open",
                    tint = SleekPrimary,
                    modifier = Modifier.size(18.dp)
                )
            }
            
            // Delete button
            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    .clickable { onDelete() },
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Default.Delete,
                    contentDescription = "Delete",
                    tint = SleekDanger,
                    modifier = Modifier.size(16.dp)
                )
            }
        }
    }
}

// ================= AI SCRIPT ASSISTANT TAB =================

@Composable
fun AssistantTab(
    messages: List<ChatMessage>,
    isLoading: Boolean,
    onSendMessage: (String) -> Unit,
    onLoadCode: (String) -> Unit,
    onClearChat: () -> Unit
) {
    var textState by remember { mutableStateOf("") }
    val listState = rememberLazyListState()
    val scope = rememberCoroutineScope()

    // Scroll to the latest message whenever it comes
    LaunchedEffect(messages.size, isLoading) {
        if (messages.isNotEmpty()) {
            listState.animateScrollToItem(messages.size - 1)
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        // Assistant Chat Header
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.SmartToy, contentDescription = null, tint = SleekPrimary)
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    "SunnyBuilder AI Ассистент",
                    color = SleekPrimaryText,
                    fontWeight = FontWeight.Bold,
                    fontSize = 14.sp
                )
            }
            IconButton(
                onClick = onClearChat,
                modifier = Modifier.size(28.dp)
            ) {
                Icon(Icons.Default.DeleteSweep, contentDescription = "Clear chat", tint = SleekSecondaryText)
            }
        }

        Divider(color = SleekBorder, modifier = Modifier.padding(bottom = 12.dp))

        // Chats lists
        LazyColumn(
            state = listState,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth(),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            items(messages) { message ->
                ChatBubble(
                    message = message,
                    onLoadCode = onLoadCode
                )
            }

            if (isLoading) {
                item {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 4.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            modifier = Modifier
                                .size(32.dp)
                                .clip(CircleShape)
                                .background(SleekSurface),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(Icons.Default.SmartToy, contentDescription = null, tint = SleekPrimary, modifier = Modifier.size(16.dp))
                        }
                        Spacer(modifier = Modifier.width(8.dp))
                        Card(
                            colors = CardDefaults.cardColors(containerColor = SleekSurface),
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Row(modifier = Modifier.padding(12.dp, 8.dp), verticalAlignment = Alignment.CenterVertically) {
                                CircularProgressIndicator(
                                    color = SleekPrimary,
                                    modifier = Modifier.size(12.dp),
                                    strokeWidth = 2.dp
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text(
                                    "Думаю над кодом...",
                                    color = SleekSecondaryText,
                                    fontSize = 11.sp
                                )
                            }
                        }
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        // Suggestions block
        SuggestedPromptsRow(
            onPromptClick = { prompt ->
                onSendMessage(prompt)
            }
        )

        Spacer(modifier = Modifier.height(8.dp))

        // Input Field Control Row
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            OutlinedTextField(
                value = textState,
                onValueChange = { textState = it },
                placeholder = { Text("Спроси AI о коде CLEO...", color = SleekSecondaryText, fontSize = 13.sp) },
                colors = OutlinedTextFieldDefaults.colors(
                    focusedBorderColor = SleekPrimary,
                    unfocusedBorderColor = SleekBorder,
                    focusedTextColor = SleekPrimaryText,
                    unfocusedTextColor = SleekPrimaryText
                ),
                maxLines = 3,
                keyboardOptions = KeyboardOptions(
                    imeAction = ImeAction.Send,
                    capitalization = KeyboardCapitalization.Sentences
                ),
                modifier = Modifier.weight(1f)
            )

            IconButton(
                onClick = {
                    if (textState.isNotBlank()) {
                        onSendMessage(textState)
                        textState = ""
                    }
                },
                modifier = Modifier
                    .clip(CircleShape)
                    .background(SleekPrimary)
                    .size(48.dp)
            ) {
                Icon(
                    Icons.AutoMirrored.Filled.Send,
                    contentDescription = "Send",
                    tint = Color.White,
                    modifier = Modifier.size(18.dp)
                )
            }
        }
    }
}

@Composable
fun SuggestedPromptsRow(onPromptClick: (String) -> Unit) {
    val prompts = listOf(
        "Как спавнить Hydra в CLEO?",
        "Создай бессмертие актера",
        "Скрипт на выдачу денег",
        "Объясни опкод 00E1"
    )

    LazyRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        contentPadding = PaddingValues(vertical = 2.dp)
    ) {
        items(prompts) { prompt ->
            SuggestionChip(
                onClick = { onPromptClick(prompt) },
                label = {
                    Text(
                        prompt,
                        color = SleekPrimary,
                        fontSize = 11.sp,
                        fontFamily = FontFamily.SansSerif
                    )
                },
                border = BorderStroke(1.dp, SleekBorder),
                colors = SuggestionChipDefaults.suggestionChipColors(
                    containerColor = SleekSurface
                )
            )
        }
    }
}

@Composable
fun ChatBubble(
    message: ChatMessage,
    onLoadCode: (String) -> Unit
) {
    val isUser = message.sender == "user"
    
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (isUser) Arrangement.End else Arrangement.Start,
        verticalAlignment = Alignment.Top
    ) {
        if (!isUser) {
            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    .background(SleekHighlightBg)
                    .border(1.dp, SleekPrimary.copy(alpha = 0.2f), CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Default.SmartToy, contentDescription = null, tint = SleekPrimary, modifier = Modifier.size(16.dp))
            }
            Spacer(modifier = Modifier.width(8.dp))
        }

        Column(
            horizontalAlignment = if (isUser) Alignment.End else Alignment.Start,
            modifier = Modifier.weight(1f, fill = false)
        ) {
            Card(
                colors = CardDefaults.cardColors(
                    containerColor = if (isUser) SleekHighlightBg else Color.White
                ),
                shape = RoundedCornerShape(
                    topStart = if (isUser) 12.dp else 4.dp,
                    topEnd = if (isUser) 4.dp else 12.dp,
                    bottomStart = 12.dp,
                    bottomEnd = 12.dp
                ),
                border = BorderStroke(
                    1.dp,
                    if (isUser) SleekPrimary.copy(alpha = 0.2f) else SleekBorder
                ),
                modifier = Modifier.widthIn(max = 280.dp)
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Text(
                        text = message.text,
                        color = if (isUser) SleekHighlightText else SleekPrimaryText,
                        fontSize = 12.sp,
                        lineHeight = 18.sp
                    )
                }
            }

            // Extract code blocks and show load trigger
            val codeBlocks = remember(message.text) { extractGtaCodeBlocks(message.text) }
            codeBlocks.forEach { code ->
                Spacer(modifier = Modifier.height(8.dp))
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .border(BorderStroke(1.dp, SleekBorder), RoundedCornerShape(12.dp)),
                    colors = CardDefaults.cardColors(containerColor = CodeBackground),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Column(modifier = Modifier.padding(10.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                "Сгенерированный CLEO код",
                                color = SleekPrimary,
                                fontSize = 10.sp,
                                fontWeight = FontWeight.Bold,
                                fontFamily = FontFamily.SansSerif
                            )
                            TextButton(
                                onClick = { onLoadCode(code) },
                                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 2.dp),
                                modifier = Modifier.height(24.dp)
                            ) {
                                Icon(Icons.Default.Input, contentDescription = null, tint = SleekPrimary, modifier = Modifier.size(12.dp))
                                Spacer(modifier = Modifier.width(4.dp))
                                Text("ЗАГРУЗИТЬ", color = SleekPrimary, fontSize = 10.sp, fontWeight = FontWeight.Bold, fontFamily = FontFamily.SansSerif)
                            }
                        }
                        
                        Divider(color = SleekBorder, modifier = Modifier.padding(vertical = 4.dp))
                        
                        Text(
                            text = code,
                            color = CodeTextNormal,
                            fontFamily = FontFamily.Monospace,
                            fontSize = 10.sp,
                            lineHeight = 14.sp,
                            modifier = Modifier.verticalScroll(rememberScrollState())
                        )
                    }
                }
            }
        }

        if (isUser) {
            Spacer(modifier = Modifier.width(8.dp))
            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    .background(SleekSurface)
                    .border(1.dp, SleekBorder, CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Default.Person, contentDescription = null, tint = SleekSecondaryText, modifier = Modifier.size(16.dp))
            }
        }
    }
}

// Helper to extract SCM/GTA code blocks from markdown ```gta ... ```
fun extractGtaCodeBlocks(text: String): List<String> {
    val regex = Regex("```(?:gta|scm|gta-scm|cleo|cs)?\\n(.*?)```", RegexOption.DOT_MATCHES_ALL)
    return regex.findAll(text).map { it.groupValues[1].trim() }.toList()
}

// ================= DECOMPILER TAB =================

@Composable
fun DecompilerTab(
    decompilerHex: String,
    decompilerLogs: List<String>,
    decompiledCode: String,
    isDecompiling: Boolean,
    onDecompile: (String, Boolean) -> Unit,
    onLoadDecompiled: (String) -> Unit
) {
    val context = LocalContext.current
    var hexInputState by remember { mutableStateOf(decompilerHex) }
    var useAiMode by remember { mutableStateOf(true) }
    val logsListState = rememberLazyListState()

    val filePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        uri?.let {
            try {
                val inputStream = context.contentResolver.openInputStream(it)
                val bytes = inputStream?.readBytes()
                inputStream?.close()
                if (bytes != null) {
                    val hexString = bytes.joinToString("") { "%02X".format(it) }
                    hexInputState = hexString
                    Toast.makeText(context, "Файл прочитан! Размер: ${bytes.size} байт", Toast.LENGTH_SHORT).show()
                }
            } catch (e: Exception) {
                Toast.makeText(context, "Ошибка импорта: ${e.localizedMessage}", Toast.LENGTH_LONG).show()
            }
        }
    }

    // Scroll to latest log item
    LaunchedEffect(decompilerLogs.size, isDecompiling) {
        if (decompilerLogs.isNotEmpty()) {
            logsListState.animateScrollToItem(decompilerLogs.size - 1)
        }
    }

    val presets = listOf(
        Triple("Spawn Infernus (PC)", "02470104EA0302480104EA03032B0104EA03", "Спавн спорткара Infernus в PC CLEO"),
        Triple("Actor Godmode (PC)", "02AB01020304010001010064", "Бессмертие (Godmode) главного героя на PC"),
        Triple("Infinite Ammo (PC)", "01120204EA03010001010000", "Бесконечные патроны на PC"),
        Triple("Empty Template", "0001010064", "Шаблон с wait 100 ms")
    )

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
            .verticalScroll(rememberScrollState())
    ) {
        // Tab Header
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.padding(bottom = 12.dp)
        ) {
            Icon(
                imageVector = Icons.Default.SettingsBackupRestore,
                contentDescription = null,
                tint = SleekPrimary,
                modifier = Modifier.size(24.dp)
            )
            Spacer(modifier = Modifier.width(8.dp))
            Column {
                Text(
                    "Универсальный Декомпилятор CLEO",
                    color = SleekPrimaryText,
                    fontWeight = FontWeight.Bold,
                    fontSize = 18.sp,
                    fontFamily = FontFamily.SansSerif
                )
                Text(
                    "Восстановление исходников PC и Mobile CLEO скриптов из байт-кода",
                    color = SleekSecondaryText,
                    fontSize = 11.sp
                )
            }
        }

        // --- 1. Quick Presets Section ---
        Text(
            "Быстрые SCM Пресеты (PC / Mobile)",
            color = SleekSecondaryText,
            fontWeight = FontWeight.Bold,
            fontSize = 12.sp,
            modifier = Modifier.padding(vertical = 8.dp)
        )

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            presets.take(3).forEach { (title, hex, desc) ->
                Card(
                    modifier = Modifier
                        .weight(1f)
                        .height(84.dp)
                        .clickable {
                            hexInputState = hex
                            Toast.makeText(context, "Пресет загружен!", Toast.LENGTH_SHORT).show()
                        },
                    border = BorderStroke(1.dp, SleekBorder),
                    colors = CardDefaults.cardColors(containerColor = Color.White),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(10.dp),
                        verticalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(
                            text = title,
                            color = SleekPrimary,
                            fontWeight = FontWeight.Bold,
                            fontSize = 11.sp,
                            fontFamily = FontFamily.SansSerif
                        )
                        Text(
                            text = desc,
                            color = SleekSecondaryText,
                            fontSize = 9.sp,
                            lineHeight = 12.sp,
                            maxLines = 2,
                            overflow = TextOverflow.Ellipsis
                        )
                    }
                }
            }
        }

        // --- 1.5. Drag File & Drop / Upload Zone ---
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 16.dp)
                .clickable { filePickerLauncher.launch("*/*") },
            border = BorderStroke(1.dp, SleekPrimary.copy(alpha = 0.5f)),
            colors = CardDefaults.cardColors(containerColor = SleekHighlightBg.copy(alpha = 0.15f)),
            shape = RoundedCornerShape(12.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center
            ) {
                Icon(
                    imageVector = Icons.Default.UploadFile,
                    contentDescription = "Upload script file",
                    tint = SleekPrimary,
                    modifier = Modifier.size(32.dp)
                )
                Spacer(modifier = Modifier.height(6.dp))
                Text(
                    "Импорт файла скрипта (Drag / Drop / Select)",
                    color = SleekPrimary,
                    fontWeight = FontWeight.Bold,
                    fontSize = 13.sp,
                    fontFamily = FontFamily.SansSerif
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    "Нажмите сюда, чтобы загрузить файл .cs или .scm. Приложение мгновенно считает бинарный байт-код и переведет его в hex-последовательность для декомпиляции.",
                    color = SleekSecondaryText,
                    fontSize = 10.sp,
                    textAlign = TextAlign.Center,
                    lineHeight = 14.sp
                )
            }
        }

        // --- 2. Hex Byte Sequence Input ---
        Text(
            "Байт-код / Hex последовательность (Любые PC/Mobile скрипты)",
            color = SleekSecondaryText,
            fontWeight = FontWeight.Bold,
            fontSize = 12.sp,
            modifier = Modifier.padding(bottom = 6.dp)
        )

        OutlinedTextField(
            value = hexInputState,
            onValueChange = { hexInputState = it },
            placeholder = { Text("Вставьте любой hex байт-код PC или Mobile CLEO скрипта...", color = SleekSecondaryText, fontSize = 12.sp) },
            colors = OutlinedTextFieldDefaults.colors(
                focusedBorderColor = SleekPrimary,
                unfocusedBorderColor = SleekBorder,
                focusedTextColor = SleekPrimaryText,
                unfocusedTextColor = SleekPrimaryText,
                focusedContainerColor = CodeBackground,
                unfocusedContainerColor = CodeBackground
            ),
            textStyle = TextStyle(
                fontFamily = FontFamily.Monospace,
                fontSize = 12.sp,
                lineHeight = 16.sp
            ),
            modifier = Modifier
                .fillMaxWidth()
                .height(80.dp),
            maxLines = 3
        )

        Spacer(modifier = Modifier.height(12.dp))

        // --- Switch for AI Mode ---
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 16.dp)
                .border(BorderStroke(1.dp, SleekBorder), RoundedCornerShape(12.dp))
                .background(SleekHighlightBg.copy(alpha = 0.3f))
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column(modifier = Modifier.weight(1f).padding(end = 8.dp)) {
                Text(
                    "Интеллектуальный ИИ Декомпилятор (Gemini)",
                    color = SleekHighlightText,
                    fontWeight = FontWeight.Bold,
                    fontSize = 12.sp,
                    fontFamily = FontFamily.SansSerif
                )
                Text(
                    "Позволяет декомпилировать абсолютно любые ПК и мобильные скрипты, восстанавливая сложную логику, переменные и условия.",
                    color = SleekSecondaryText,
                    fontSize = 10.sp,
                    lineHeight = 13.sp
                )
            }
            Switch(
                checked = useAiMode,
                onCheckedChange = { useAiMode = it },
                colors = SwitchDefaults.colors(
                    checkedThumbColor = Color.White,
                    checkedTrackColor = SleekPrimary,
                    uncheckedThumbColor = SleekSecondaryText,
                    uncheckedTrackColor = SleekBorder
                )
            )
        }

        // --- 3. Action Button ---
        Button(
            onClick = { onDecompile(hexInputState, useAiMode) },
            modifier = Modifier.fillMaxWidth(),
            colors = ButtonDefaults.buttonColors(
                containerColor = SleekPrimary,
                contentColor = Color.White
            ),
            shape = RoundedCornerShape(10.dp),
            enabled = !isDecompiling && hexInputState.isNotBlank()
        ) {
            if (isDecompiling) {
                CircularProgressIndicator(
                    color = Color.White,
                    modifier = Modifier.size(18.dp),
                    strokeWidth = 2.dp
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text("Декомпиляция...", fontWeight = FontWeight.Bold, fontFamily = FontFamily.SansSerif)
            } else {
                Icon(Icons.Default.SettingsBackupRestore, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    if (useAiMode) "Декомпилировать с помощью ИИ" else "Декомпилировать локально",
                    fontWeight = FontWeight.Bold,
                    fontFamily = FontFamily.SansSerif
                )
            }
        }

        // --- 4. Logs Console ---
        if (decompilerLogs.isNotEmpty()) {
            Spacer(modifier = Modifier.height(16.dp))
            Text(
                "Прогресс декомпиляции",
                color = SleekSecondaryText,
                fontWeight = FontWeight.Bold,
                fontSize = 12.sp,
                modifier = Modifier.padding(bottom = 6.dp)
            )

            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(130.dp)
                    .border(BorderStroke(1.dp, SleekBorder), RoundedCornerShape(10.dp)),
                colors = CardDefaults.cardColors(containerColor = CodeBackground),
                shape = RoundedCornerShape(10.dp)
            ) {
                LazyColumn(
                    state = logsListState,
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(12.dp)
                ) {
                    items(decompilerLogs) { log ->
                        val color = when {
                            log.startsWith("[SUCCESS]") -> SleekPrimary
                            log.startsWith("[WARN]") -> SleekPurple
                            log.startsWith("[ERROR]") -> SleekDanger
                            else -> SleekSecondaryText
                        }
                        Text(
                            text = log,
                            color = color,
                            fontFamily = FontFamily.Monospace,
                            fontSize = 11.sp,
                            lineHeight = 15.sp,
                            modifier = Modifier.padding(bottom = 2.dp)
                        )
                    }
                }
            }
        }

        // --- 5. Decompiled Results Panel ---
        if (decompiledCode.isNotBlank()) {
            Spacer(modifier = Modifier.height(16.dp))
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(bottom = 6.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    "Результат: Код Sanny Builder",
                    color = SleekSecondaryText,
                    fontWeight = FontWeight.Bold,
                    fontSize = 12.sp
                )
                Button(
                    onClick = { onLoadDecompiled(decompiledCode) },
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 2.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = SleekHighlightBg, contentColor = SleekHighlightText),
                    shape = RoundedCornerShape(8.dp),
                    modifier = Modifier.height(28.dp)
                ) {
                    Icon(Icons.Default.Input, contentDescription = null, modifier = Modifier.size(12.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("В РЕДАКТОР", fontSize = 10.sp, fontWeight = FontWeight.Bold, fontFamily = FontFamily.SansSerif)
                }
            }

            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 150.dp, max = 300.dp)
                    .border(BorderStroke(1.dp, SleekBorder), RoundedCornerShape(12.dp)),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                shape = RoundedCornerShape(12.dp)
            ) {
                Box(modifier = Modifier.padding(12.dp)) {
                    Text(
                        text = decompiledCode,
                        color = CodeTextNormal,
                        fontFamily = FontFamily.Monospace,
                        fontSize = 12.sp,
                        lineHeight = 16.sp,
                        modifier = Modifier
                            .fillMaxSize()
                            .verticalScroll(rememberScrollState())
                    )
                }
            }
        }
    }
}
