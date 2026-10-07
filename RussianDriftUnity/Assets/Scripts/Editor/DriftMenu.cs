using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RussianDrift.EditorTools
{
    /// <summary>Menu entry points and the first-open auto setup.</summary>
    [InitializeOnLoad]
    public static class DriftMenu
    {
        private const string FlagKey = "RussianDrift.SetupDone.v1";

        static DriftMenu()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }
            bool scenesExist = File.Exists("Assets/Scenes/Boot.unity");
            if (SessionState.GetBool(FlagKey, false)) return;
            SessionState.SetBool(FlagKey, true);
            if (scenesExist && EditorPrefs.GetBool(FlagKey + ".project", false)) return;
            try
            {
                Debug.Log("[RussianDrift] First open detected: building scenes, data, URP assets and project settings...");
                SetupEverything(false);
            }
            catch (Exception e)
            {
                Debug.LogError("[RussianDrift] Auto setup failed: " + e);
            }
        }

        [MenuItem("Tools/Drift/1. Setup Project (everything)", priority = 1)]
        public static void SetupMenu() { SetupEverything(true); }

        public static void SetupEverything(bool interactive)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Russian Drift", "Materials...", 0.05f);
                bool materialsOk = AssetFactory.CreateBaseMaterials();
                EditorUtility.DisplayProgressBar("Russian Drift", "Data assets...", 0.2f);
                AssetFactory.CreateDataAssets();
                EditorUtility.DisplayProgressBar("Russian Drift", "URP assets...", 0.4f);
                UrpAssetBuilder.CreateAll();
                EditorUtility.DisplayProgressBar("Russian Drift", "Project settings...", 0.55f);
                bool needRestart = ProjectConfigurator.Apply();
                EditorUtility.DisplayProgressBar("Russian Drift", "Scenes...", 0.7f);
                SceneBuilder.CreateAllScenes();
                EditorUtility.DisplayProgressBar("Russian Drift", "Car prefabs...", 0.85f);
                try { AssetFactory.CreateCarPrefabs(); } catch (Exception e) { Debug.LogWarning("[RussianDrift] Car prefab generation skipped: " + e.Message); }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (materialsOk) EditorPrefs.SetBool(FlagKey + ".project", true);
                Debug.Log("[RussianDrift] Setup complete. Open Assets/Scenes/Boot.unity and press Play. Use Tools/Build/Android APK to build.");
                if (needRestart && interactive)
                    EditorUtility.DisplayDialog("Russian Drift", "Active Input Handling was switched to the new Input System. Please restart the Unity Editor once (File > Exit, then reopen).", "OK");
                else if (needRestart)
                    Debug.LogWarning("[RussianDrift] Input handling switched to the new Input System: restart the Unity Editor once.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("Tools/Drift/Create Scenes", priority = 10)] public static void CreateScenes() { SceneBuilder.CreateAllScenes(); }
        [MenuItem("Tools/Drift/Create Data Assets (cars, upgrades, parts, tracks)", priority = 11)] public static void CreateData() { AssetFactory.CreateDataAssets(); }
        [MenuItem("Tools/Drift/Create URP Assets (Low/Medium/High)", priority = 12)] public static void CreateUrp() { UrpAssetBuilder.CreateAll(); ProjectConfigurator.ApplyQualityLevels(); }
        [MenuItem("Tools/Drift/Create Car Prefabs", priority = 13)] public static void CreatePrefabs() { AssetFactory.CreateCarPrefabs(); }
        [MenuItem("Tools/Drift/Apply Android Player Settings", priority = 14)] public static void ApplySettings() { ProjectConfigurator.Apply(); }

        [MenuItem("Tools/Drift/Open Boot Scene", priority = 30)]
        public static void OpenBoot()
        {
            if (!File.Exists("Assets/Scenes/Boot.unity")) SceneBuilder.CreateAllScenes();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
        }

        [MenuItem("Tools/Drift/Delete Save Data", priority = 40)]
        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey("rd_save_mirror");
            string f = Path.Combine(Application.persistentDataPath, "profile.rdsave");
            if (File.Exists(f)) File.Delete(f);
            Debug.Log("[RussianDrift] Save data deleted.");
        }
    }
}
