package com.example.data

import androidx.room.*
import kotlinx.coroutines.flow.Flow

@Dao
interface ScriptProjectDao {
    @Query("SELECT * FROM script_projects ORDER BY lastModified DESC")
    fun getAllProjects(): Flow<List<ScriptProject>>

    @Query("SELECT * FROM script_projects WHERE id = :id LIMIT 1")
    suspend fun getProjectById(id: Int): ScriptProject?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertProject(project: ScriptProject): Long

    @Delete
    suspend fun deleteProject(project: ScriptProject)

    @Query("DELETE FROM script_projects WHERE id = :id")
    suspend fun deleteProjectById(id: Int)
}
