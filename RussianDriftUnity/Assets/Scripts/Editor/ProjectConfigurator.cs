using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace RussianDrift.EditorTools
{
    /// <summary>Android player settings, quality levels, graphics settings and input handling.</summary>
    public static class ProjectConfigurator
    {
        /// <summary>Returns true when the editor must be restarted (input handler switched).</summary>
        public static bool Apply()
        {
            bool restart = false;
            try { ApplyPlayerSettings(); } catch (Exception e) { Debug.LogWarning("[RussianDrift] Player settings: " + e.Message); }
            try { restart = SetActiveInputHandler(); } catch (Exception e) { Debug.LogWarning("[RussianDrift] Input handler: " + e.Message); }
            try { ApplyQualityLevels(); } catch (Exception e) { Debug.LogWarning("[RussianDrift] Quality: " + e.Message); }
            try { AddAlwaysIncludedShaders(); } catch (Exception e) { Debug.LogWarning("[RussianDrift] Shaders: " + e.Message); }
            try { ApplyPhysics(); } catch (Exception e) { Debug.LogWarning("[RussianDrift] Physics: " + e.Message); }
            AssetDatabase.SaveAssets();
            return restart;
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "RussianDriftStudio";
            PlayerSettings.productName = "Russian Drift";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.russiandrift.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.MTRendering = true;
            PlayerSettings.stripEngineCode = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
        }

        private static bool SetActiveInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p == null) return false;
            if (p.intValue == 1) return false;          // already "Input System Package (New)"
            p.intValue = 1;
            so.ApplyModifiedProperties();
            return true;
        }

        private static void ApplyPhysics()
        {
            Time.fixedDeltaTime = 1f / 60f;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.autoSyncTransforms = false;
        }

        public static void ApplyQualityLevels()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null) return;
            while (levels.arraySize < 3) levels.InsertArrayElementAtIndex(levels.arraySize - 1);
            while (levels.arraySize > 3) levels.DeleteArrayElementAtIndex(levels.arraySize - 1);
            for (int i = 0; i < 3; i++)
            {
                var lv = levels.GetArrayElementAtIndex(i);
                SetStr(lv, "name", UrpAssetBuilder.Names[i]);
                SetInt(lv, "vSyncCount", 0);
                SetInt(lv, "antiAliasing", i == 2 ? 2 : 0);
                SetFloat(lv, "shadowDistance", i == 0 ? 35f : (i == 1 ? 60f : 90f));
                SetInt(lv, "shadowCascades", i == 0 ? 1 : 2);
                SetInt(lv, "pixelLightCount", i == 0 ? 2 : 4);
                SetFloat(lv, "lodBias", i == 0 ? 0.7f : (i == 1 ? 1f : 1.4f));
                var rp = lv.FindPropertyRelative("customRenderPipeline");
                if (rp != null)
                    rp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Resources/URP/URP_" + UrpAssetBuilder.Names[i] + ".asset");
            }
            // default quality per platform = High (runtime tier detection overrides it)
            var defaults = so.FindProperty("m_PerPlatformDefaultQuality");
            if (defaults != null)
                for (int i = 0; i < defaults.arraySize; i++)
                {
                    var e = defaults.GetArrayElementAtIndex(i);
                    var v = e.FindPropertyRelative("second");
                    if (v != null) v.intValue = 2;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStr(SerializedProperty p, string n, string v) { var q = p.FindPropertyRelative(n); if (q != null) q.stringValue = v; }
        private static void SetInt(SerializedProperty p, string n, int v) { var q = p.FindPropertyRelative(n); if (q != null) q.intValue = v; }
        private static void SetFloat(SerializedProperty p, string n, float v) { var q = p.FindPropertyRelative(n); if (q != null) q.floatValue = v; }

        /// <summary>Shaders that code references with Shader.Find must survive build stripping.</summary>
        private static void AddAlwaysIncludedShaders()
        {
            string[] names =
            {
                "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Simple Lit",
                "Skybox/Procedural", "Sprites/Default", "UI/Default", "Mobile/Particles/Additive", "Legacy Shaders/Particles/Additive",
                "Hidden/Universal Render Pipeline/Blit", "Hidden/Universal Render Pipeline/CopyDepth"
            };
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            var have = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < arr.arraySize; i++) have.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null || have.Contains(sh)) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                have.Add(sh);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
