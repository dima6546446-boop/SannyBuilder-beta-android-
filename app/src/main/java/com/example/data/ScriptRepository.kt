package com.example.data

import kotlinx.coroutines.flow.Flow

class ScriptRepository(private val scriptProjectDao: ScriptProjectDao) {
    val allProjects: Flow<List<ScriptProject>> = scriptProjectDao.getAllProjects()

    suspend fun getProjectById(id: Int): ScriptProject? {
        return scriptProjectDao.getProjectById(id)
    }

    suspend fun saveProject(project: ScriptProject): Long {
        return scriptProjectDao.insertProject(project)
    }

    suspend fun deleteProject(project: ScriptProject) {
        scriptProjectDao.deleteProject(project)
    }

    suspend fun deleteProjectById(id: Int) {
        scriptProjectDao.deleteProjectById(id)
    }
}
