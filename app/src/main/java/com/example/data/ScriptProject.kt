package com.example.data

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "script_projects")
data class ScriptProject(
    @PrimaryKey(autoGenerate = true) val id: Int = 0,
    val title: String,
    val code: String,
    val lastModified: Long = System.currentTimeMillis()
)
