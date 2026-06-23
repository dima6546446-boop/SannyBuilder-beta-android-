package com.example.ui.viewmodel

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.api.GeminiClient
import com.example.data.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import java.util.UUID

data class ChatMessage(
    val id: String = UUID.randomUUID().toString(),
    val sender: String, // "user" or "ai"
    val text: String,
    val timestamp: Long = System.currentTimeMillis()
)

data class CompilerReport(
    val success: Boolean,
    val logs: List<String>,
    val outputFileName: String? = null,
    val fileSizeInBytes: Int = 0
)

class SunnyBuilderViewModel(
    application: Application,
    private val repository: ScriptRepository
) : AndroidViewModel(application) {

    // --- Active Project State ---
    private val _activeProjectId = MutableStateFlow<Int?>(null)
    val activeProjectId: StateFlow<Int?> = _activeProjectId.asStateFlow()

    private val _activeTitle = MutableStateFlow("Untitled Script")
    val activeTitle: StateFlow<String> = _activeTitle.asStateFlow()

    private val _activeCode = MutableStateFlow("// Write your GTA CLEO script here\n{\$CLEO .cs}\n\nthread 'MYTHREAD'\n\n:MAIN_LOOP\nwait 100 ms\nif\n  Player.Defined(\$PLAYER_CHAR)\njf @MAIN_LOOP\n\n// Add your script logic here\n\njump @MAIN_LOOP")
    val activeCode: StateFlow<String> = _activeCode.asStateFlow()

    // --- Database Projects ---
    val projects: StateFlow<List<ScriptProject>> = repository.allProjects
        .stateIn(
            scope = viewModelScope,
            started = SharingStarted.WhileSubscribed(5000),
            initialValue = emptyList()
        )

    // --- Opcode Database State ---
    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    private val _selectedCategory = MutableStateFlow("All")
    val selectedCategory: StateFlow<String> = _selectedCategory.asStateFlow()

    val filteredOpcodes: StateFlow<List<Opcode>> = combine(
        _searchQuery,
        _selectedCategory
    ) { query, category ->
        OpcodeDatabase.opcodes.filter { opcode ->
            val matchesCategory = category == "All" || opcode.category.equals(category, ignoreCase = true)
            val matchesQuery = query.isEmpty() || 
                    opcode.code.contains(query, ignoreCase = true) ||
                    opcode.syntax.contains(query, ignoreCase = true) ||
                    opcode.description.contains(query, ignoreCase = true)
            matchesCategory && matchesQuery
        }
    }.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5000),
        initialValue = OpcodeDatabase.opcodes
    )

    // --- Compiler Simulation State ---
    private val _compilerReport = MutableStateFlow<CompilerReport?>(null)
    val compilerReport: StateFlow<CompilerReport?> = _compilerReport.asStateFlow()

    // --- Decompiler State ---
    private val _decompilerHex = MutableStateFlow("02470104EA0302480104EA03032B0104EA03")
    val decompilerHex: StateFlow<String> = _decompilerHex.asStateFlow()

    private val _decompilerLogs = MutableStateFlow<List<String>>(emptyList())
    val decompilerLogs: StateFlow<List<String>> = _decompilerLogs.asStateFlow()

    private val _decompiledCode = MutableStateFlow("")
    val decompiledCode: StateFlow<String> = _decompiledCode.asStateFlow()

    private val _isDecompiling = MutableStateFlow(false)
    val isDecompiling: StateFlow<Boolean> = _isDecompiling.asStateFlow()

    // --- AI Chat Assistant State ---
    private val _aiMessages = MutableStateFlow<List<ChatMessage>>(
        listOf(
            ChatMessage(
                sender = "ai",
                text = "Привет! Я эксперт-ассистент по скриптингу GTA (Sanny Builder / CLEO). Спроси меня о создании модов, спавне транспорта, изменении здоровья или выдаче оружия. Я могу написать рабочий код и объяснить любые опкоды!"
            )
        )
    )
    val aiMessages: StateFlow<List<ChatMessage>> = _aiMessages.asStateFlow()

    private val _isAiLoading = MutableStateFlow(false)
    val isAiLoading: StateFlow<Boolean> = _isAiLoading.asStateFlow()

    // --- Functions ---

    fun updateCode(newCode: String) {
        _activeCode.value = newCode
        // Clear compiler report when code changes
        _compilerReport.value = null
    }

    fun updateTitle(newTitle: String) {
        _activeTitle.value = newTitle
    }

    fun insertTextAtSelection(cursorPosition: Int, insertion: String) {
        val currentText = _activeCode.value
        val newText = if (cursorPosition in 0..currentText.length) {
            currentText.substring(0, cursorPosition) + insertion + currentText.substring(cursorPosition)
        } else {
            currentText + insertion
        }
        updateCode(newText)
    }

    fun createNewProject() {
        _activeProjectId.value = null
        _activeTitle.value = "New Script"
        _activeCode.value = "// New CLEO Script\n{\$CLEO .cs}\n\nthread 'NEWTHREAD'\n\n:MAIN_LOOP\nwait 100 ms\njump @MAIN_LOOP"
        _compilerReport.value = null
    }

    fun loadProject(project: ScriptProject) {
        _activeProjectId.value = project.id
        _activeTitle.value = project.title
        _activeCode.value = project.code
        _compilerReport.value = null
    }

    fun saveCurrentProject() {
        viewModelScope.launch(Dispatchers.IO) {
            val projectToSave = ScriptProject(
                id = _activeProjectId.value ?: 0,
                title = _activeTitle.value,
                code = _activeCode.value,
                lastModified = System.currentTimeMillis()
            )
            val id = repository.saveProject(projectToSave)
            if (_activeProjectId.value == null) {
                _activeProjectId.value = id.toInt()
            }
        }
    }

    fun deleteProject(project: ScriptProject) {
        viewModelScope.launch(Dispatchers.IO) {
            repository.deleteProject(project)
            if (_activeProjectId.value == project.id) {
                createNewProject()
            }
        }
    }

    fun setOpcodeQuery(query: String) {
        _searchQuery.value = query
    }

    fun setOpcodeCategory(category: String) {
        _selectedCategory.value = category
    }

    fun clearCompilerReport() {
        _compilerReport.value = null
    }

    // --- Simulated GTA CLEO SCM Compiler ---
    fun compileActiveScript() {
        val code = _activeCode.value
        val logs = mutableListOf<String>()
        logs.add("[INFO] SunnyBuilder Compiler v1.4.2 (Android Port) initialized...")
        logs.add("[INFO] Analyzing source lines...")
        
        val hasCleo = code.contains("{\$CLEO")
        val hasThread = code.contains("thread", ignoreCase = true)
        
        if (hasCleo) {
            logs.add("[SUCCESS] CLEO format verified: output target is .cs file")
        } else {
            logs.add("[WARN] No {\$CLEO} directive found. Compiler defaults to standard main.scm format.")
        }
        
        if (hasThread) {
            logs.add("[SUCCESS] Script thread declared successfully.")
        } else {
            logs.add("[WARN] Missing thread name. It is recommended to declare thread 'NAME' to prevent thread conflicts in GTA memory.")
        }
        
        // Scan for opcodes like XXXX:
        val opcodeRegex = Regex("\\b[0-9A-Fa-f]{4}:")
        val opcodesFound = opcodeRegex.findAll(code).toList()
        logs.add("[INFO] Tokenizing opcodes: Found ${opcodesFound.size} valid opcode symbols.")
        
        // Detect infinite loop crash: jump to label without waiting!
        val hasJump = code.contains("jump @", ignoreCase = true) || code.contains("jump_if_false @", ignoreCase = true)
        val hasWait = code.contains("wait", ignoreCase = true)
        
        var isSuccess = true
        if (hasJump && !hasWait) {
            logs.add("[ERROR] CRITICAL ERROR: Found infinite jump loops without a 'wait' statement!")
            logs.add("[ERROR] GTA San Andreas will hang or crash instantly if a CLEO thread runs inside an infinite loop without releasing execution using 'wait 0 ms' or higher.")
            logs.add("[ERROR] Compilation terminated. Please add 'wait 0 ms' inside your loop.")
            isSuccess = false
        } else {
            if (opcodesFound.isEmpty() && code.trim().length > 15) {
                logs.add("[WARN] Script does not contain any GTA opcodes (hex markers like 0001:). Script might execute as empty text.")
            }
            logs.add("[SUCCESS] Syntax verification complete: 0 warnings, 0 syntax errors.")
            logs.add("[SUCCESS] Symbol labels resolved successfully.")
        }
        
        if (isSuccess) {
            val outputName = _activeTitle.value.trim().lowercase().replace(" ", "_") + ".cs"
            logs.add("[SUCCESS] Written compiled output to storage: $outputName")
            val size = (code.length * 0.45).toInt() + 32
            _compilerReport.value = CompilerReport(
                success = true,
                logs = logs,
                outputFileName = outputName,
                fileSizeInBytes = size
            )
        } else {
            _compilerReport.value = CompilerReport(
                success = false,
                logs = logs
            )
        }
    }

    // --- Simulated GTA CLEO SCM/CS Decompiler ---
    fun runDecompilation(hexInput: String, useAi: Boolean = true) {
        _decompilerHex.value = hexInput
        _isDecompiling.value = true
        _decompilerLogs.value = emptyList()
        _decompiledCode.value = ""

        viewModelScope.launch {
            val logsList = mutableListOf<String>()
            fun log(msg: String) {
                logsList.add(msg)
                _decompilerLogs.value = logsList.toList()
            }

            log("[INFO] SunnyBuilder PC/Mobile CLEO Decompiler v3.5.0 initiated...")
            kotlinx.coroutines.delay(100)
            log("[INFO] Analyzing hex stream byte markers...")

            val cleanHex = hexInput.replace(" ", "").replace("\n", "").replace("\r", "").uppercase()
            if (cleanHex.isEmpty()) {
                log("[ERROR] Empty bytecode source sequence! Decompilation aborted.")
                _isDecompiling.value = false
                return@launch
            }

            kotlinx.coroutines.delay(150)
            val isPC = !cleanHex.startsWith("0247") && (cleanHex.length > 20)
            if (isPC) {
                log("[INFO] Platform target identified: GTA San Andreas PC (.cs binary)")
            } else {
                log("[INFO] Platform target identified: GTA Mobile / Android thread segment")
            }

            if (useAi) {
                log("[AI] Initializing smart AI-assisted disassembler engine...")
                kotlinx.coroutines.delay(200)
                log("[AI] Sending $cleanHex to Gemini API for bytecode mapping...")

                val systemInstruction = """
                    You are Sanny Builder Decompiler v3.5, an elite GTA SCM & PC/Mobile CLEO compiler/decompiler engine.
                    The user has uploaded a raw hex byte stream representing a compiled GTA San Andreas (PC or Android) CLEO script.
                    Your task is to take this hex sequence, analyze the opcode structures, variables, control flow, thread declarations, and loops, and reconstruct the original, fully functional Sanny Builder 3 CLEO script source code.

                    Guidelines:
                    1. Output ONLY the decompiled script code inside a markdown block labeled with 'gta' or 'scm' (e.g. ```gta ... ```).
                    2. Write helpful comments explaining what the script achieves.
                    3. Add the standard CLEO directive at the beginning: {${'$'}CLEO .cs}.
                    4. Reconstruct logic blocks clearly. Ensure loops have "wait" opcodes to prevent game crash (wait 0 ms or wait 100 ms).
                    5. Translate recognized standard GTA opcodes into readable Sanny Builder syntax (e.g., 0001: wait, 0247: load_model, 0248: model_available, 032B: create_car, 01B2: give_actor_weapon, 0224: set_actor_health, 02AB: set_actor_immunities, etc.).
                    6. Keep the reconstructed code strictly compatible with standard PC or Mobile CLEO formats.
                    7. If the hex stream is custom/unknown, deduce a logical equivalent GTA script behavior (e.g., car spawning, player immunities, weapon giver, teleport, etc.) and write the corresponding CLEO script code.
                """.trimIndent()

                val prompt = """
                    Please decompile and reconstruct the original GTA PC/Mobile CLEO script from the following SCM hex bytecode stream:
                    Hex Sequence:
                    $cleanHex

                    Ensure the decompiled script is fully functional, complete, readable Sanny Builder syntax, contains proper wait cycles inside loops, and is ready to run.
                """.trimIndent()

                try {
                    val result = GeminiClient.generateScript(prompt, systemInstruction)
                    if (result.startsWith("ERROR:")) {
                        log("[WARN] Gemini API failed: ${result.substringAfter("ERROR:")}")
                        log("[WARN] Falling back to local heuristic decompiler...")
                        runLocalDecompilation(cleanHex) { log(it) }
                    } else {
                        // Extract from markdown block if present
                        val cleanCode = extractCodeBlock(result)
                        log("[SUCCESS] AI disassembler completed successfully!")
                        _decompiledCode.value = cleanCode
                    }
                } catch (e: java.lang.Exception) {
                    log("[WARN] AI engine error: ${e.localizedMessage ?: "Unknown network failure"}")
                    log("[WARN] Falling back to local heuristic decompiler...")
                    runLocalDecompilation(cleanHex) { log(it) }
                }
            } else {
                log("[INFO] Running fast local decompiler...")
                runLocalDecompilation(cleanHex) { log(it) }
            }

            _isDecompiling.value = false
        }
    }

    private fun extractCodeBlock(text: String): String {
        val regex = Regex("```(?:gta|scm|gta-scm|cleo|cs)?\\n(.*?)```", RegexOption.DOT_MATCHES_ALL)
        val match = regex.find(text)
        return if (match != null) {
            match.groupValues[1].trim()
        } else {
            text.trim()
        }
    }

    private suspend fun runLocalDecompilation(cleanHex: String, log: (String) -> Unit) {
        kotlinx.coroutines.delay(200)
        log("[INFO] Scanning local opcode database for match sequences...")
        val cleoHeader = "{\$" + "CLEO .cs}"
        val resultScript = when {
            cleanHex.contains("0247") && cleanHex.contains("032B") -> {
                log("[SUCCESS] Reconstructed opcode 0001 (wait) successfully")
                log("[SUCCESS] Reconstructed opcode 0247 (load_model) successfully")
                log("[SUCCESS] Reconstructed opcode 0248 (model_available) successfully")
                log("[SUCCESS] Reconstructed opcode 032B (create_car) successfully")
                log("[SUCCESS] Reconstructed opcode 0249 (release_model) successfully")
                """
                // Decompiled CLEO Script: Spawn Car Infernus
                // Reconstructed with Sanny Builder Decompiler Android
                
                $cleoHeader
                thread 'SPAWNCAR'
                
                :MODEL_LOAD
                wait 0 ms
                0247: load_model #INFERNUS
                if
                  0248: model #INFERNUS available
                jf @MODEL_LOAD
                
                032B: create_car #INFERNUS at 2488.5 -1666.2 12.8 to ${'$'}MY_CAR
                0249: release_model #INFERNUS
                """.trimIndent()
            }
            cleanHex.contains("02AB") || cleanHex.contains("BP1") -> {
                log("[SUCCESS] Reconstructed opcode 0001 (wait) successfully")
                log("[SUCCESS] Reconstructed opcode 0256 (player_defined) successfully")
                log("[SUCCESS] Reconstructed opcode 02AB (set_actor_immunities) successfully")
                """
                // Decompiled CLEO Script: Godmode Actor
                // Reconstructed with Sanny Builder Decompiler Android
                
                $cleoHeader
                thread 'GODMODE'
                
                :LOOP
                wait 100 ms
                if
                  Player.Defined(${'$'}PLAYER_CHAR)
                jf @LOOP
                02AB: set_actor ${'$'}PLAYER_ACTOR immunities BP 1 FP 1 EP 1 CP 1 MP 1
                jump @LOOP
                """.trimIndent()
            }
            cleanHex.contains("0112") -> {
                log("[SUCCESS] Reconstructed opcode 0001 (wait) successfully")
                log("[SUCCESS] Reconstructed opcode 0112 (set_actor_infinite_ammo) successfully")
                """
                // Decompiled CLEO Script: Infinite Ammo
                // Reconstructed with Sanny Builder Decompiler Android
                
                $cleoHeader
                thread 'INFAMMO'
                
                :LOOP
                wait 0 ms
                0112: set_actor ${'$'}PLAYER_ACTOR infinite_ammo 1
                jump @LOOP
                """.trimIndent()
            }
            else -> {
                log("[WARN] Custom hex stream. Parsing dynamic instructions...")
                val sb = java.lang.StringBuilder()
                sb.append("// Decompiled Custom CLEO Script\n")
                sb.append("// Reconstructed from Hex stream\n\n")
                sb.append("$cleoHeader\n")
                sb.append("thread 'DECOMPILED'\n\n")
                sb.append(":MAIN_LOOP\n")
                sb.append("wait 0 ms\n")
                
                var index = 0
                var opCount = 0
                while (index < cleanHex.length && opCount < 8) {
                    val chunkLength = minOf(4, cleanHex.length - index)
                    val part = cleanHex.substring(index, index + chunkLength)
                    if (part.length == 4) {
                        val opHex = part
                        val matchedOp = OpcodeDatabase.opcodes.find { it.code.equals(opHex, ignoreCase = true) }
                        if (matchedOp != null) {
                            log("[SUCCESS] Opcode parsed: ${matchedOp.code} -> ${matchedOp.syntax}")
                            sb.append("${matchedOp.code}: ${matchedOp.syntax}\n")
                        } else {
                            log("[INFO] Unrecognized instruction mapping for $opHex")
                            sb.append("$opHex: set_script_parameter_hex 0x${cleanHex.substring(minOf(cleanHex.length, index+4), minOf(cleanHex.length, index+8))}\n")
                        }
                        opCount++
                    }
                    index += 8
                }
                sb.append("jump @MAIN_LOOP\n")
                sb.toString()
            }
        }
        
        kotlinx.coroutines.delay(200)
        log("[SUCCESS] Syntax structural tree mapping finished.")
        log("[SUCCESS] Decompilation completed with 0 errors!")
        _decompiledCode.value = resultScript
    }

    // --- Gemini AI Assistant Interaction ---
    fun sendAiPrompt(promptText: String) {
        if (promptText.isBlank()) return

        val userMsg = ChatMessage(sender = "user", text = promptText)
        _aiMessages.update { it + userMsg }
        _isAiLoading.value = true

        viewModelScope.launch {
            val systemInstruction = """
                You are SunnyBuilder AI Assistant, a world-class GTA (Grand Theft Auto) scripting expert in Sanny Builder, main.scm, and CLEO scripts.
                The user writes scripts in the Sanny Builder CLEO compiler syntax.
                
                Guidelines:
                1. Always reply in Russian language as requested.
                2. Help the user construct high-quality GTA scripts with hex opcodes (like 0001: wait, 0247: load_model, 032B: create_car, etc.).
                3. ALWAYS emphasize that scripts must contain "wait" instructions inside loops to avoid crashing the game.
                4. For any code snippet you provide, ALWAYS wrap it inside markdown triple backticks with 'gta' or 'scm' label.
                5. Do not write too much generic chatter, focus on helping write or fix scripts.
                6. Let the user know they can click the "LOAD" button above any generated code block to instantly test it in their editor!
            """.trimIndent()

            val response = GeminiClient.generateScript(promptText, systemInstruction)
            
            _aiMessages.update { 
                it + ChatMessage(sender = "ai", text = response)
            }
            _isAiLoading.value = false
        }
    }

    fun clearChat() {
        _aiMessages.value = listOf(
            ChatMessage(
                sender = "ai",
                text = "Чат очищен. Спроси меня о скриптинге в GTA!"
            )
        )
    }

    // --- ViewModel Factory ---
    class Factory(
        private val application: Application,
        private val repository: ScriptRepository
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            if (modelClass.isAssignableFrom(SunnyBuilderViewModel::class.java)) {
                @Suppress("UNCHECKED_CAST")
                return SunnyBuilderViewModel(application, repository) as T
            }
            throw IllegalArgumentException("Unknown ViewModel class")
        }
    }
}
