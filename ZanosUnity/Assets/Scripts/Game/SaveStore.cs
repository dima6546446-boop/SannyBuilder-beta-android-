using System;
using System.IO;
using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>Сохранение прогресса в JSON (persistentDataPath). Повреждённый файл не ломает игру.</summary>
    public static class SaveStore
    {
        static string PathFile { get { return Path.Combine(Application.persistentDataPath, "zanos-save.json"); } }

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(PathFile)) { var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFile)); if (d != null) return d; }
            }
            catch (Exception e) { Debug.LogWarning("Сохранение не прочитано, начинаем заново: " + e.Message); }
            return new SaveData();
        }

        public static void Save(SaveData d)
        {
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(d)); }
            catch (Exception e) { Debug.LogWarning("Не удалось сохранить: " + e.Message); }
        }

        public static void Delete() { try { if (File.Exists(PathFile)) File.Delete(PathFile); } catch (Exception) { } }
    }
}
