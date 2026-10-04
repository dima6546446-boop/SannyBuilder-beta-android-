using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CaucasusDrive.EditorTools
{
    /// <summary>
    /// Меню «CAUCASUS DRIVE»: настройка проекта под Android и сборка APK одной кнопкой.
    /// Метод BuildAndroidCI вызывается из GitHub Actions (game-ci/unity-builder, buildMethod).
    /// </summary>
    public static class CaucasusSetup
    {
        const string MatDir = "Assets/CaucasusDrive/Resources/Materials";
        const string ScenePath = "Assets/CaucasusDrive/Scenes/Main.unity";
        const string ApkPath = "Builds/CaucasusDrive.apk";

        [MenuItem("CAUCASUS DRIVE/1. Настроить проект", priority = 1)]
        public static void Setup()
        {
            PlayerSetup();
            EnsureUrp();
            CreateMaterials();
            CreateScene();
            PickMobileQuality();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CAUCASUS DRIVE: проект настроен. Дальше — «CAUCASUS DRIVE → 2. Собрать APK» или Play в редакторе.");
        }

        [MenuItem("CAUCASUS DRIVE/2. Собрать APK", priority = 2)]
        public static void BuildApk()
        {
            if (!File.Exists(ScenePath)) Setup();
            Build(ApkPath, false);
        }

        /// <summary>Для CI: настройка + сборка; путь — из переменной BUILD_PATH (game-ci передаёт customBuildPath).</summary>
        public static void BuildAndroidCI()
        {
            Setup();
            // game-ci передаёт путь аргументом -customBuildPath (для androidPackage — уже с .apk)
            string path = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-customBuildPath") path = args[i + 1];
            if (string.IsNullOrEmpty(path)) path = System.Environment.GetEnvironmentVariable("BUILD_PATH");
            if (string.IsNullOrEmpty(path)) path = ApkPath;
            if (!path.EndsWith(".apk")) path = Path.Combine(path, "CaucasusDrive.apk");
            Build(path, true);
        }

        static void Build(string path, bool exitOnFail)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorUserBuildSettings.buildAppBundle = false;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("APK готов: " + Path.GetFullPath(path) + " (" + (report.summary.totalSize / 1048576) + " МБ)");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogError("Сборка не удалась: " + report.summary.result + ", ошибок: " + report.summary.totalErrors);
                if (exitOnFail) EditorApplication.Exit(1);
            }
        }

        static void PlayerSetup()
        {
            PlayerSettings.companyName = "CaucasusDrive";
            PlayerSettings.productName = "CAUCASUS DRIVE";
            PlayerSettings.bundleVersion = "2.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
#if UNITY_2022_1_OR_NEWER
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.caucasusdrive.unity");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.caucasusdrive.unity");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
#endif
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.forceInternetPermission = true; // ссылка на Telegram, будущий онлайн
            PlayerSettings.SplashScreen.showUnityLogo = false;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && !Application.isBatchMode)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        // ------------------------------------------------------------------ URP
        /// <summary>
        /// Если проект открыт не из шаблона «Universal 3D» (URP не назначен), но пакет URP установлен —
        /// создаём мобильный URP-ассет сами. Через отражение: скрипт компилируется и без пакета.
        /// </summary>
        static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            var tData = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            var tAsset = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            if (tData == null || tAsset == null) { Debug.LogWarning("URP не установлен — игра пойдёт на встроенном конвейере (Standard)."); return; }
            const string dir = "Assets/CaucasusDrive/Settings";
            Directory.CreateDirectory(dir);
            var data = ScriptableObject.CreateInstance(tData);
            AssetDatabase.CreateAsset(data, dir + "/MobileRenderer.asset");
            var create = tAsset.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var asset = create.Invoke(null, new object[] { data }) as RenderPipelineAsset;
            if (asset == null) { Debug.LogError("Не удалось создать URP-ассет"); return; }
            AssetDatabase.CreateAsset(asset, dir + "/MobileURP.asset");
            GraphicsSettings.defaultRenderPipeline = asset;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
            QualitySettings.SetQualityLevel(cur, false);
            AssetDatabase.SaveAssets();
            Debug.Log("CAUCASUS DRIVE: создан URP-ассет " + dir + "/MobileURP.asset");
        }

        // ------------------------------------------------------------------ материалы
        static Shader Find(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s) return s; }
            return Shader.Find("Standard");
        }

        static bool Urp => GraphicsSettings.defaultRenderPipeline != null && Shader.Find("Universal Render Pipeline/Lit") != null;

        static Material Mat(string name, Shader sh)
        {
            Directory.CreateDirectory(MatDir);
            string p = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            else m.shader = sh;
            m.enableInstancing = true;
            return m;
        }

        static void Transparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            // встроенный конвейер (Standard): режим Fade
            m.SetFloat("_Mode", 2f);
            m.EnableKeyword("_ALPHABLEND_ON");
        }

        static void Emissive(Material m)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static void CreateMaterials()
        {
            bool urp = Urp;
            var lit = urp ? Find("Universal Render Pipeline/Lit") : Find("Standard");
            var simple = urp ? Find("Universal Render Pipeline/Simple Lit") : Find("Legacy Shaders/Diffuse", "Standard");
            var unlit = urp ? Find("Universal Render Pipeline/Unlit") : Find("Unlit/Color");
            var particles = urp ? Find("Universal Render Pipeline/Particles/Unlit") : Find("Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit");

            Mat("Lit", lit);
            Emissive(Mat("LitEmissive", lit));
            Transparent(Mat("LitTransparent", lit), false);
            Mat("SimpleLit", simple);
            Emissive(Mat("SimpleLitEmissive", simple));
            var cut = Mat("SimpleLitCutout", simple); cut.EnableKeyword("_ALPHATEST_ON"); cut.SetFloat("_AlphaClip", 1f); cut.SetFloat("_Cutoff", 0.5f);
            Mat("Unlit", unlit);
            Transparent(Mat("UnlitAdditive", unlit), true);
            var pm = Mat("Particles", particles); Transparent(pm, false);
            pm.SetFloat("_ColorMode", 0f);
            var sky = Mat("Sky", Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f); sky.SetFloat("_AtmosphereThickness", 1f); sky.SetFloat("_Exposure", 1.2f);
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)));
        }

        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (!Application.isBatchMode && SceneManager.GetActiveScene().path != ScenePath && File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Шаблон «Universal 3D» содержит уровни качества Mobile и PC: для Android берём Mobile.</summary>
        static void PickMobileQuality()
        {
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                if (names[i].ToLower().Contains("mobile") || names[i].ToLower().Contains("low"))
                {
                    QualitySettings.SetQualityLevel(i, true);
                    break;
                }
        }
    }
}
