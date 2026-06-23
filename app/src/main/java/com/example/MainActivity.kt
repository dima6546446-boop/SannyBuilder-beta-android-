package com.example

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import androidx.lifecycle.lifecycleScope
import com.example.data.AppDatabase
import com.example.data.ScriptRepository
import com.example.ui.editor.SunnyBuilderApp
import com.example.ui.theme.MyApplicationTheme
import com.example.ui.viewmodel.SunnyBuilderViewModel

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        // Initialize Room Database & Script Repository safely
        val database = AppDatabase.getDatabase(applicationContext, lifecycleScope)
        val repository = ScriptRepository(database.scriptProjectDao())
        
        // Instantiate ViewModel with the factory
        val viewModel: SunnyBuilderViewModel by viewModels {
            SunnyBuilderViewModel.Factory(application, repository)
        }

        enableEdgeToEdge()
        setContent {
            MyApplicationTheme {
                Surface(
                    modifier = Modifier.fillMaxSize()
                ) {
                    SunnyBuilderApp(viewModel = viewModel)
                }
            }
        }
    }
}
