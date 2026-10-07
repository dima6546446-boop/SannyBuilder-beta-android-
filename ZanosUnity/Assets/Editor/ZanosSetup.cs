#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zanos.Editor
{
    /// <summary>
    /// Автонастройка проекта при первом открытии: параметры Android, пустая сцена в сборке, шейдеры в билд.
    /// Меню «Zanos»: сборка APK, запуск в редакторе, повторная настройка.
    /// </summary>
    [InitializeOnLoad]
    public static class ZanosSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string Flag = "Zanos.SetupDone.v1";

        static ZanosSetup() { EditorApplication.delayCall += () => { if (!SessionState.GetBool(Flag, false)) { SessionState.SetBool(Flag, true); Configure(); } }; }

        [MenuItem("Zanos/Настроить проект")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Zanos"; PlayerSettings.productName = "ЗАНОС";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.zanos.drift");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // шейдеры, которые игра ищет по имени, не должны вырезаться из сборки
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gs == null || gs.Length == 0) { Debug.LogWarning("ЗАНОС: GraphicsSettings.asset не найден — добавьте шейдеры в Always Included вручную."); EnsureScene(); return; }
            var so = new SerializedObject(gs[0]); var arr = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "Standard", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit", "Legacy Shaders/Particles/Additive", "Legacy Shaders/Particles/Alpha Blended", "Unlit/Color", "Sprites/Default" })
            {
                var sh = Shader.Find(name); if (sh == null) continue; bool has = false;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
                if (!has) { arr.InsertArrayElementAtIndex(arr.arraySize); arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh; }
            }
            so.ApplyModifiedProperties();
            EnsureScene();
            Debug.Log("ЗАНОС: проект настроен. Откройте сцену Assets/Scenes/Main.unity и нажмите Play (игра создаётся сама).");
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorSceneManager.SaveScene(sc, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("Zanos/Собрать Android APK")]
        public static void BuildApk()
        {
            Configure(); Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/Zanos.apk", target = BuildTarget.Android, options = BuildOptions.None });
            if (report.summary.result == BuildResult.Succeeded) { Debug.Log("APK готов: Builds/Zanos.apk (" + (report.summary.totalSize / 1048576) + " МБ)"); EditorUtility.RevealInFinder("Builds/Zanos.apk"); }
            else Debug.LogError("Сборка не удалась: " + report.summary.result);
        }

        [MenuItem("Zanos/Открыть сцену и запустить")]
        public static void Play() { EnsureScene(); EditorSceneManager.OpenScene(ScenePath); EditorApplication.isPlaying = true; }
    }
}
#endif
