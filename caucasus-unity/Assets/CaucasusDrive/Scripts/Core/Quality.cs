using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    /// <summary>
    /// Пресеты качества и регулятор FPS. Настройки URP (renderScale, MSAA, тени) пишутся через
    /// отражение — код компилируется и без пакета URP (тогда работает встроенный конвейер).
    /// </summary>
    public class Quality
    {
        public static readonly string[] Names = { "Низкое", "Среднее", "Высокое" };
        public int level;
        public float renderScale, minScale;
        public int msaa, traffic, shadowRes;
        public float shadowDist, drawDist;
        public string carLod;
        public bool softShadows;
        float scale, fpsAcc, fpsTime, goodTime;
        public float fps = 60f;
        int frames;
        bool shadowsCut;
        Light sun;

        public static int AutoDetect()
        {
            int ram = SystemInfo.systemMemorySize, cores = SystemInfo.processorCount;
            if (ram < 3500 || cores < 6) return 0;
            if (ram >= 7000 && cores >= 8) return 2;
            return 1;
        }

        public Quality(int lvl)
        {
            level = Mathf.Clamp(lvl, 0, 2);
            switch (level)
            {
                case 0: renderScale = 0.72f; minScale = 0.55f; msaa = 1; traffic = 10; shadowDist = 0f; shadowRes = 512; drawDist = 320f; carLod = "LOD_hi"; break;
                case 1: renderScale = 0.85f; minScale = 0.6f; msaa = 2; traffic = 18; shadowDist = 45f; shadowRes = 1024; drawDist = 420f; carLod = "LOD_hi"; break;
                default: renderScale = 1f; minScale = 0.7f; msaa = 4; traffic = 26; shadowDist = 70f; shadowRes = 2048; drawDist = 520f; carLod = "LOD_ultra"; softShadows = true; break;
            }
            scale = renderScale;
        }

        static void Set(object o, string prop, object v)
        {
            if (o == null) return;
            var p = o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { try { p.SetValue(o, v); } catch { } }
        }

        public void Apply(Light sunLight, Camera cam)
        {
            sun = sunLight;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var rp = GraphicsSettings.currentRenderPipeline;
            Set(rp, "renderScale", scale);
            Set(rp, "msaaSampleCount", msaa);
            Set(rp, "shadowDistance", shadowDist);
            Set(rp, "shadowCascadeCount", 1);
            Set(rp, "supportsHDR", false);
            Set(rp, "supportsCameraDepthTexture", false);
            Set(rp, "supportsCameraOpaqueTexture", false);
            QualitySettings.shadowDistance = shadowDist;
            QualitySettings.antiAliasing = msaa > 1 ? msaa : 0;
            QualitySettings.lodBias = level == 0 ? 0.7f : level == 1 ? 1f : 1.4f;
            if (sun)
            {
                sun.shadows = shadowDist <= 0f ? LightShadows.None : softShadows ? LightShadows.Soft : LightShadows.Hard;
                sun.shadowResolution = shadowRes >= 2048 ? LightShadowResolution.High : shadowRes >= 1024 ? LightShadowResolution.Medium : LightShadowResolution.Low;
            }
            if (cam)
            {
                cam.farClipPlane = drawDist;
                cam.allowMSAA = msaa > 1;
                cam.allowHDR = false;
                // без URP — уменьшаем разрешение экрана (renderScale встроенного конвейера)
                if (rp == null) ScalableBufferManager.ResizeBuffers(scale, scale);
            }
        }

        /// <summary>Регулятор: каждые 2 с среднее FPS; ниже 48 — разрешение −8% (до minScale), потом отключаем тени;
        /// 8 с стабильно выше 57 — возвращаем по шагу.</summary>
        public void Govern(float dt)
        {
            frames++; fpsAcc += dt; fpsTime += dt;
            if (fpsTime < 2f) return;
            fps = frames / Mathf.Max(fpsAcc, 1e-3f);
            frames = 0; fpsAcc = 0; fpsTime = 0;
            var rp = GraphicsSettings.currentRenderPipeline;
            if (fps < 48f)
            {
                goodTime = 0;
                if (scale > minScale + 0.01f) { scale = Mathf.Max(minScale, scale - 0.08f); Set(rp, "renderScale", scale); }
                else if (!shadowsCut && sun && sun.shadows != LightShadows.None) { shadowsCut = true; sun.shadows = LightShadows.None; }
            }
            else if (fps > 57f)
            {
                goodTime += 2f;
                if (goodTime >= 8f)
                {
                    goodTime = 0;
                    if (shadowsCut && sun) { shadowsCut = false; sun.shadows = softShadows ? LightShadows.Soft : LightShadows.Hard; }
                    else if (scale < renderScale - 0.01f) { scale = Mathf.Min(renderScale, scale + 0.05f); Set(rp, "renderScale", scale); }
                }
            }
        }
    }

    /// <summary>Небо (процедурный skybox), солнце/луна, туман, свет фонарей ночью. Пресеты: день, вечер, ночь, рассвет.</summary>
    public class DayNight
    {
        public static readonly float[] PresetTimes = { 12f, 19.3f, 23f, 6.2f };
        public static readonly string[] PresetNames = { "День", "Вечер", "Ночь", "Рассвет" };
        public readonly Light sun;
        readonly Material sky;
        readonly ReflectionProbe probe;
        public float time = 12f, night;

        public DayNight(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadowBias = 0.06f; sun.shadowNormalBias = 0.6f;
            RenderSettings.sun = sun;
            var skyBase = Resources.Load<Material>("Materials/Sky");
            sky = skyBase ? new Material(skyBase) : new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var pg = new GameObject("SkyProbe");
            pg.transform.SetParent(parent, false);
            probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.cullingMask = 0; // только небо
            probe.resolution = 64;
            probe.size = new Vector3(5000, 2000, 5000);
            probe.importance = 0;
        }

        public void SetTime(float h)
        {
            time = ((h % 24f) + 24f) % 24f;
            // высота солнца: 6 ч — восход, 12 — зенит (60°), 18 — закат
            float a = (time - 6f) / 12f * Mathf.PI;
            float elev = Mathf.Sin(a);
            night = Mathf.Clamp01(-elev * 3f + 0.25f);
            float day = 1f - night;
            bool moon = elev < 0f;
            float e = moon ? -elev : elev;
            sun.transform.rotation = Quaternion.Euler(Mathf.Max(8f, e * 62f), moon ? 200f : 40f + (time - 6f) * 10f, 0);
            float warm = Mathf.Clamp01(1f - elev * 2.2f);
            sun.color = moon ? new Color(0.55f, 0.65f, 0.9f) : Color.Lerp(new Color(1f, 0.97f, 0.9f), new Color(1f, 0.62f, 0.35f), warm);
            sun.intensity = moon ? 0.18f : Mathf.Lerp(0.5f, 1.25f, Mathf.Clamp01(elev * 2f));
            sky.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.25f, day));
            sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.8f, 1.05f, warm));
            sky.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.6f, 0.55f, 0.6f), warm));
            sky.SetColor("_GroundColor", new Color(0.37f, 0.35f, 0.34f) * Mathf.Lerp(0.2f, 1f, day));
            var skyC = Color.Lerp(new Color(0.03f, 0.04f, 0.08f), new Color(0.55f, 0.68f, 0.85f), day);
            if (!moon && warm > 0.5f) skyC = Color.Lerp(skyC, new Color(0.85f, 0.6f, 0.45f), (warm - 0.5f));
            RenderSettings.ambientSkyColor = skyC * 0.9f;
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.05f, 0.05f, 0.07f), new Color(0.55f, 0.55f, 0.52f), day);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.02f, 0.02f, 0.03f), new Color(0.3f, 0.28f, 0.25f), day);
            RenderSettings.fogColor = Color.Lerp(new Color(0.04f, 0.05f, 0.08f), new Color(0.72f, 0.78f, 0.86f), day);
            baseSun = sun.intensity; baseFog = RenderSettings.fogColor; baseAmb = RenderSettings.ambientSkyColor; baseExp = sky.GetFloat("_Exposure");
            overcast = -1f;
            Overcast(lastOvercast);
            probe.RenderProbe();
        }

        public void SetFog(float far) { fogFar = far; RenderSettings.fogStartDistance = far * 0.35f; RenderSettings.fogEndDistance = far; overcast = -1f; Overcast(lastOvercast); }

        float baseSun = 1f, baseExp = 1f, fogFar = 400f, overcast = -1f, lastOvercast;
        Color baseFog, baseAmb;

        /// <summary>Пасмурно/дождь 0..1: солнце тусклее, небо и туман серые, туман ближе (порт Game._weather).</summary>
        public void Overcast(float r)
        {
            lastOvercast = r;
            if (Mathf.Abs(r - overcast) < 0.004f) return;
            overcast = r;
            float day = 1f - night;
            var grey = new Color(0.42f, 0.45f, 0.5f) * (0.25f + 0.75f * day);
            sun.intensity = baseSun * (1f - 0.7f * r);
            RenderSettings.fogColor = Color.Lerp(baseFog, grey, 0.75f * r);
            RenderSettings.ambientSkyColor = Color.Lerp(baseAmb, grey, 0.5f * r);
            sky.SetFloat("_Exposure", baseExp * (1f - 0.45f * r));
            RenderSettings.fogStartDistance = fogFar * 0.35f * (1f - 0.6f * r);
            RenderSettings.fogEndDistance = fogFar * (1f - 0.35f * r);
        }
    }
}
