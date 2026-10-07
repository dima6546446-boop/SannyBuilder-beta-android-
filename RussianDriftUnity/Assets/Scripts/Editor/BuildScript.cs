using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RussianDrift.EditorTools
{
    /// <summary>One-click Android builds. Keystore comes from env vars RD_KEYSTORE_PATH / RD_KEYSTORE_PASS / RD_KEY_ALIAS / RD_KEY_PASS; otherwise Unity's debug keystore is used.</summary>
    public static class BuildScript
    {
        [MenuItem("Tools/Build/Android APK", priority = 100)]
        public static void BuildApkMenu() { Build(false, false); }

        [MenuItem("Tools/Build/Android AAB (Play Store)", priority = 101)]
        public static void BuildAabMenu() { Build(true, false); }

        [MenuItem("Tools/Build/Android APK (development, profiler)", priority = 102)]
        public static void BuildDevApk() { Build(false, true); }

        /// <summary>Command line: Unity -batchmode -quit -projectPath . -executeMethod RussianDrift.EditorTools.BuildScript.BuildAndroidCli</summary>
        public static void BuildAndroidCli()
        {
            bool aab = Environment.GetEnvironmentVariable("RD_BUILD_AAB") == "1";
            Build(aab, false);
        }

        private static void Build(bool bundle, bool development)
        {
            if (!File.Exists("Assets/Scenes/Boot.unity")) DriftMenu.SetupEverything(false);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            ProjectConfigurator.Apply();

            string dir = "Builds/Android";
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "RussianDrift" + (bundle ? ".aab" : ".apk"));
            EditorUserBuildSettings.buildAppBundle = bundle;
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, PlayerSettings.Android.bundleVersionCode);

            string ks = Environment.GetEnvironmentVariable("RD_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(ks) && File.Exists(ks))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("RD_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("RD_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("RD_KEY_PASS");
            }
            else PlayerSettings.Android.useCustomKeystore = false;

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boot.unity", "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Garage.unity", "Assets/Scenes/GameWorld.unity" },
                locationPathName = path,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log("[RussianDrift] Build succeeded: " + path + " (" + (s.totalSize / (1024 * 1024)) + " MB, " + s.totalTime.TotalSeconds.ToString("0") + " s)");
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogError("[RussianDrift] Build failed: " + s.result + " (" + s.totalErrors + " errors)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
