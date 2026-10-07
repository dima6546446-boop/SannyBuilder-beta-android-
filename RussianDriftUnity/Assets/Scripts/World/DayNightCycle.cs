using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Time of day: sun/moon light, procedural skybox, ambient gradient, fog colour and star dome.</summary>
    public class DayNightCycle : MonoBehaviour
    {
        public Light sun;
        public float hour = 17f;
        public float hoursPerRealSecond = 1f / 75f;     // one game hour = 75 s
        public bool frozen;

        private Material sky;
        private Transform stars;
        private ParticleSystem starPs;
        private Camera followCam;
        private readonly Gradient ambientSky = new Gradient();
        private readonly Gradient ambientEquator = new Gradient();
        private readonly Gradient sunColor = new Gradient();

        public static DayNightCycle Create(Transform parent, float startHour)
        {
            var go = new GameObject("DayNight");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DayNightCycle>();
            var lg = new GameObject("Sun");
            lg.transform.SetParent(go.transform, false);
            d.sun = lg.AddComponent<Light>();
            d.sun.type = LightType.Directional;
            d.sun.shadows = LightShadows.Soft;
            d.sun.shadowStrength = 0.9f;
            d.sun.shadowBias = 0.05f; d.sun.shadowNormalBias = 0.4f;
            d.hour = startHour;
            d.Init();
            return d;
        }

        private void Init()
        {
            RenderSettings.sun = sun;
            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null)
            {
                sky = new Material(shader);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = (UnityEngine.Rendering.AmbientMode)1;   // 1 = Trilinear (sky/equator/ground gradient)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 64;
            RenderSettings.reflectionIntensity = 1f;

            // gradients over 0..1 day
            ambientSky.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.05f, 0.07f, 0.14f), 0f), new GradientColorKey(new Color(0.07f, 0.09f, 0.18f), 0.2f),
                new GradientColorKey(new Color(0.55f, 0.66f, 0.85f), 0.32f), new GradientColorKey(new Color(0.55f, 0.72f, 0.95f), 0.5f),
                new GradientColorKey(new Color(0.65f, 0.60f, 0.62f), 0.72f), new GradientColorKey(new Color(0.07f, 0.09f, 0.18f), 0.85f),
                new GradientColorKey(new Color(0.05f, 0.07f, 0.14f), 1f)
            }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            ambientEquator.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.04f, 0.05f, 0.09f), 0f), new GradientColorKey(new Color(0.06f, 0.07f, 0.12f), 0.2f),
                new GradientColorKey(new Color(0.75f, 0.55f, 0.45f), 0.27f), new GradientColorKey(new Color(0.55f, 0.60f, 0.65f), 0.4f),
                new GradientColorKey(new Color(0.55f, 0.60f, 0.65f), 0.6f), new GradientColorKey(new Color(0.85f, 0.45f, 0.30f), 0.76f),
                new GradientColorKey(new Color(0.06f, 0.07f, 0.12f), 0.85f), new GradientColorKey(new Color(0.04f, 0.05f, 0.09f), 1f)
            }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            sunColor.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.45f, 0.55f, 1f), 0f), new GradientColorKey(new Color(0.45f, 0.55f, 1f), 0.2f),
                new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0.27f), new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.38f),
                new GradientColorKey(new Color(1f, 0.96f, 0.88f), 0.62f), new GradientColorKey(new Color(1f, 0.5f, 0.25f), 0.74f),
                new GradientColorKey(new Color(0.45f, 0.55f, 1f), 0.82f), new GradientColorKey(new Color(0.45f, 0.55f, 1f), 1f)
            }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });

            // star dome
            var sg = new GameObject("Stars");
            sg.transform.SetParent(transform, false);
            stars = sg.transform;
            starPs = sg.AddComponent<ParticleSystem>();
            starPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = starPs.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = 1000000f; main.startSpeed = 0f; main.maxParticles = 500;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = starPs.emission; em.enabled = false;
            var sh = starPs.shape; sh.enabled = false;
            var r = sg.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MatLib.Additive(ProcTex.Glow(), Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            starPs.Play();
            var rnd = new System.Random(3);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 400; i++)
            {
                Vector3 dir = new Vector3((float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 0.95f + 0.05f, (float)rnd.NextDouble() * 2f - 1f).normalized;
                ep.position = dir * 900f;
                ep.startSize = 2.5f + (float)rnd.NextDouble() * 4f;
                ep.startLifetime = 1000000f;
                ep.startColor = new Color(1f, 1f, 1f, 0.5f + (float)rnd.NextDouble() * 0.5f);
                ep.velocity = Vector3.zero;
                starPs.Emit(ep, 1);
            }
            Apply();
        }

        public void SetHour(float h) { hour = Mathf.Repeat(h, 24f); Apply(); }

        private void Update()
        {
            if (!frozen) hour = Mathf.Repeat(hour + Time.deltaTime * hoursPerRealSecond, 24f);
            Apply();
            if (followCam == null) followCam = Camera.main;
            if (followCam != null && stars != null) stars.position = followCam.transform.position;
        }

        private void Apply()
        {
            float t01 = hour / 24f;
            // sun elevation: noon (12h) = +90 deg
            float ang = (hour - 6f) / 24f * 360f;                     // 0 at 6h sunrise, 90 at noon
            float elev = Mathf.Sin(ang * Mathf.Deg2Rad);
            bool day = elev > -0.05f;
            Quaternion rot = day ? Quaternion.Euler(ang, 35f, 0f) : Quaternion.Euler(55f + 15f * Mathf.Sin(t01 * Mathf.PI * 2f), 215f, 0f);   // night: moon light from the opposite side
            sun.transform.rotation = rot;

            float night = 1f - Mathf.SmoothStep(-0.12f, 0.22f, elev);
            WorldConditions.NightFactor = night;
            WorldConditions.TimeOfDay = hour;

            Color sc = sunColor.Evaluate(t01);
            sun.color = day ? sc : new Color(0.45f, 0.55f, 0.95f);
            float dayInt = Mathf.Clamp01(elev * 2.2f + 0.15f);
            sun.intensity = day ? Mathf.Lerp(0.12f, 1.35f, dayInt) * (1f - WorldConditions.Rain * 0.45f) : 0.22f;
            sun.shadowStrength = day ? Mathf.Lerp(0.5f, 0.92f, dayInt) : 0.35f;

            RenderSettings.ambientSkyColor = ambientSky.Evaluate(t01) * (1f - WorldConditions.Rain * 0.25f);
            RenderSettings.ambientEquatorColor = ambientEquator.Evaluate(t01);
            RenderSettings.ambientGroundColor = ambientEquator.Evaluate(t01) * 0.5f;
            RenderSettings.reflectionIntensity = Mathf.Lerp(1f, 0.55f, night);

            Color fog = Color.Lerp(new Color(0.62f, 0.72f, 0.85f), new Color(0.03f, 0.04f, 0.08f), night);
            fog = Color.Lerp(fog, new Color(0.55f, 0.58f, 0.62f) * Mathf.Lerp(1f, 0.15f, night), Mathf.Clamp01(WorldConditions.Fog + WorldConditions.Rain * 0.5f));
            if (Mathf.Abs(elev) < 0.25f && night < 0.9f) fog = Color.Lerp(fog, new Color(0.95f, 0.55f, 0.35f), 0.35f * (1f - Mathf.Abs(elev) / 0.25f));
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = 0.0007f + WorldConditions.Fog * 0.010f + WorldConditions.Rain * 0.0025f + night * 0.0004f;

            if (sky != null)
            {
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1f, 1.6f, 1f - Mathf.Clamp01(elev * 3f + 0.5f)) * (1f - WorldConditions.Fog * 0.2f));
                sky.SetFloat("_SunSize", day ? 0.05f : 0.0f);
                sky.SetFloat("_SunSizeConvergence", 5f);
                sky.SetFloat("_Exposure", Mathf.Lerp(1.25f, 0.18f, night) * (1f - WorldConditions.Rain * 0.35f));
                sky.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.35f, 0.4f, 0.55f), WorldConditions.Rain));
                sky.SetColor("_GroundColor", new Color(0.3f, 0.3f, 0.32f));
            }
            if (starPs != null)
            {
                var r = starPs.GetComponent<ParticleSystemRenderer>();
                r.enabled = night > 0.35f && WorldConditions.Rain < 0.6f;
                var mat = r.sharedMaterial;
                Color c = new Color(1f, 1f, 1f, 1f) * Mathf.Clamp01((night - 0.35f) * 1.5f) * (1f - WorldConditions.Fog);
                if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
                MatLib.SetColor(mat, c);
            }
        }
    }
}
