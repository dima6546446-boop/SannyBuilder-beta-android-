using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Global post-processing volume built in code: ACES tonemap, bloom, colour grading, vignette and speed-driven motion blur.</summary>
    public class PostFxController : MonoBehaviour
    {
        private Volume volume;
        private Bloom bloom;
        private Tonemapping tone;
        private ColorAdjustments color;
        private Vignette vignette;
        private MotionBlur blur;
        private float speedKmh;

        public static PostFxController Create(Transform parent)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PostFxController>();
            p.Build();
            return p;
        }

        private void Build()
        {
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.65f);
            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.1f);
            color.contrast.Override(14f);
            color.saturation.Override(12f);
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            blur = profile.Add<MotionBlur>(true);
            blur.mode.Override(MotionBlurMode.CameraOnly);
            blur.quality.Override(MotionBlurQuality.Low);
            blur.intensity.Override(0f);
            blur.clamp.Override(0.1f);
            volume.profile = profile;
            GameEvents.SettingsChanged += Refresh;
            Refresh();
        }

        private void OnDestroy() { GameEvents.SettingsChanged -= Refresh; }

        public void Refresh()
        {
            var q = QualityManager.Current;
            var save = Services.Get<SaveSystem>();
            bool mb = q.motionBlur && (save == null || save.Data.settings.motionBlur);
            if (blur != null) blur.active = mb;
            if (bloom != null) bloom.active = q.bloom;
        }

        public void SetSpeed(float kmh) { speedKmh = kmh; }

        private void Update()
        {
            float night = WorldConditions.NightFactor;
            if (bloom != null)
            {
                bloom.intensity.Override(Mathf.Lerp(0.45f, 1.1f, night) + WorldConditions.Wetness * 0.2f);
                bloom.threshold.Override(Mathf.Lerp(1.0f, 0.8f, night));
            }
            if (color != null)
            {
                color.postExposure.Override(Mathf.Lerp(0.15f, 0.55f, night));
                color.saturation.Override(Mathf.Lerp(14f, 24f, night) - WorldConditions.Fog * 25f - WorldConditions.Rain * 10f);
                color.colorFilter.Override(Color.Lerp(Color.white, new Color(0.82f, 0.9f, 1f), night * 0.6f + WorldConditions.Rain * 0.2f));
            }
            if (vignette != null)
                vignette.intensity.Override(0.2f + Mathf.Clamp01((speedKmh - 80f) / 140f) * 0.22f + night * 0.08f);
            if (blur != null && blur.active)
                blur.intensity.Override(Mathf.Clamp01((speedKmh - 70f) / 160f) * 0.45f);
        }
    }
}
