using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RussianDrift.Core
{
    public struct QualityTier
    {
        public int index;               // 0 low, 1 medium, 2 high
        public string name;
        public float renderScale;
        public float shadowDistance;
        public int msaa;
        public int shadowCascades;
        public int additionalLights;
        public int shadowResolution;
        public bool motionBlur;
        public bool bloom;
        public bool reflectionProbe;
        public int trafficCount;
        public int spectatorCount;
        public float viewDistance;
        public float particleScale;
        public float lodBias;
        public int targetFps;
    }

    /// <summary>
    /// Detects device class and applies a graphics tier. URP assets Resources/URP/URP_Low|Medium|High (made by the editor menu)
    /// are swapped in when present; numeric settings are always applied on top so the project also works with a single asset.
    /// </summary>
    public static class QualityManager
    {
        public static QualityTier Current { get; private set; }
        public static int DetectedTier { get; private set; }
        public static event Action TierChanged;

        public static QualityTier GetTier(int i)
        {
            switch (i)
            {
                case 0:
                    return new QualityTier { index = 0, name = "Low", renderScale = 0.75f, shadowDistance = 35f, msaa = 1, shadowCascades = 1, additionalLights = 2, shadowResolution = 1024,
                        motionBlur = false, bloom = false, reflectionProbe = false, trafficCount = 6, spectatorCount = 8, viewDistance = 260f, particleScale = 0.5f, lodBias = 0.7f, targetFps = 30 };
                case 1:
                    return new QualityTier { index = 1, name = "Medium", renderScale = 0.9f, shadowDistance = 60f, msaa = 1, shadowCascades = 2, additionalLights = 4, shadowResolution = 2048,
                        motionBlur = false, bloom = true, reflectionProbe = true, trafficCount = 12, spectatorCount = 16, viewDistance = 420f, particleScale = 0.8f, lodBias = 1f, targetFps = 60 };
                default:
                    return new QualityTier { index = 2, name = "High", renderScale = 1f, shadowDistance = 90f, msaa = 2, shadowCascades = 2, additionalLights = 6, shadowResolution = 2048,
                        motionBlur = true, bloom = true, reflectionProbe = true, trafficCount = 20, spectatorCount = 28, viewDistance = 650f, particleScale = 1f, lodBias = 1.4f, targetFps = 60 };
            }
        }

        /// <summary>Heuristic device profiler: RAM, cores, GPU memory and graphics API.</summary>
        public static int DetectTier()
        {
            int ram = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            int vram = SystemInfo.graphicsMemorySize;
            int score = 0;
            if (ram >= 7000) score += 3; else if (ram >= 5000) score += 2; else if (ram >= 3000) score += 1;
            if (cores >= 8) score += 2; else if (cores >= 6) score += 1;
            if (vram >= 2000) score += 1;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) score += 1;
            if (SystemInfo.maxTextureSize >= 16384) score += 1;
            if (Application.isEditor) score = 7;
            int tier = score >= 6 ? 2 : (score >= 3 ? 1 : 0);
            DetectedTier = tier;
            return tier;
        }

        public static void Apply()
        {
            var save = Services.Get<SaveSystem>();
            int q = save != null ? save.Data.settings.graphicsQuality : 3;
            int tier = q >= 3 ? DetectTier() : Mathf.Clamp(q, 0, 2);
            ApplyTier(tier);
        }

        public static void ApplyTier(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            var t = GetTier(tier);
            var save = Services.Get<SaveSystem>();
            if (save != null && save.Data.settings.targetFps > 0) t.targetFps = Mathf.Min(t.targetFps, save.Data.settings.targetFps);
            Current = t;

            string[] names = { "Low", "Medium", "High" };
            var asset = Resources.Load<RenderPipelineAsset>("URP/URP_" + names[tier]);
            if (asset != null && QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = t.targetFps;
            QualitySettings.lodBias = t.lodBias;
            QualitySettings.shadowDistance = t.shadowDistance;
            QualitySettings.antiAliasing = t.msaa;
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.particleRaycastBudget = tier == 0 ? 16 : 64;
            QualitySettings.anisotropicFiltering = tier == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;

            SetRenderScale(t.renderScale);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.shadowDistance = t.shadowDistance;
                urp.msaaSampleCount = t.msaa;
                TrySet(urp, "shadowCascadeCount", t.shadowCascades);
                TrySet(urp, "maxAdditionalLightsCount", t.additionalLights);
                TrySet(urp, "mainLightShadowmapResolution", t.shadowResolution);
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (TierChanged != null) TierChanged();
        }

        public static void SetRenderScale(float s)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) urp.renderScale = Mathf.Clamp(s, 0.5f, 1.0f);
        }

        public static float GetRenderScale()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return urp != null ? urp.renderScale : 1f;
        }

        private static void TrySet(object target, string prop, object value)
        {
            try
            {
                var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null && p.CanWrite) { p.SetValue(target, Convert.ChangeType(value, p.PropertyType), null); return; }
            }
            catch (Exception) { }
        }
    }

    /// <summary>Runtime frame-time monitor: lowers render scale (then tier) when FPS is below target and restores it when stable.</summary>
    public class AdaptiveQuality : MonoBehaviour
    {
        private static AdaptiveQuality instance;
        private float avg = 0.016f;
        private float lowTimer, highTimer;
        private float scale = 1f;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("[AdaptiveQuality]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AdaptiveQuality>();
        }

        private void Start() { scale = QualityManager.Current.renderScale; QualityManager.TierChanged += OnTier; }
        private void OnDestroy() { QualityManager.TierChanged -= OnTier; }
        private void OnTier() { scale = QualityManager.Current.renderScale; lowTimer = highTimer = 0f; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || dt > 0.5f) return;          // ignore hitches / scene loads
            avg = Mathf.Lerp(avg, dt, 0.05f);
            float target = 1f / Mathf.Max(20, QualityManager.Current.targetFps);
            var save = Services.Get<SaveSystem>();
            bool auto = save == null || save.Data.settings.graphicsQuality >= 3;

            if (avg > target * 1.22f) { lowTimer += dt; highTimer = 0f; }
            else if (avg < target * 1.05f) { highTimer += dt; lowTimer = 0f; }
            else { lowTimer = highTimer = 0f; }

            if (lowTimer > 2.5f)
            {
                lowTimer = 0f;
                if (scale > 0.62f) { scale = Mathf.Max(0.6f, scale - 0.1f); QualityManager.SetRenderScale(scale); }
                else if (auto && QualityManager.Current.index > 0) QualityManager.ApplyTier(QualityManager.Current.index - 1);
            }
            else if (highTimer > 12f)
            {
                highTimer = 0f;
                float max = QualityManager.Current.renderScale;
                if (scale < max) { scale = Mathf.Min(max, scale + 0.05f); QualityManager.SetRenderScale(scale); }
            }
        }
    }
}
