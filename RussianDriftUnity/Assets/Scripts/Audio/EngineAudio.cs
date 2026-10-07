using UnityEngine;
using RussianDrift.Core;
using RussianDrift.Vehicle;

namespace RussianDrift.Audio
{
    /// <summary>Dynamic car sound: three crossfaded engine layers by rpm, load, turbo whine/blow-off, backfire, skid, wind, rain, impacts.</summary>
    public class EngineAudio : MonoBehaviour
    {
        private VehicleController vc;
        private bool player;
        private AudioSource[] layers = new AudioSource[3];
        private float[] baseHz = new float[3];
        private AudioSource turbo, skid, wind, rain, oneShot;
        private AudioLowPassFilter lowpass;
        private bool interior;
        private float loadSmooth;
        private int cylinders;
        private float tone;

        public static EngineAudio Attach(VehicleController v, bool isPlayer)
        {
            var e = v.gameObject.AddComponent<EngineAudio>();
            e.Init(v, isPlayer);
            return e;
        }

        private AudioSource MakeSource(GameObject go, AudioClip clip, bool loop, float vol)
        {
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.volume = vol; s.playOnAwake = false;
            s.spatialBlend = player ? 0f : 1f;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 5f; s.maxDistance = 75f; s.dopplerLevel = player ? 0f : 0.7f;
            return s;
        }

        private void Init(VehicleController v, bool isPlayer)
        {
            vc = v; player = isPlayer;
            cylinders = v.Stats.def.cylinders; tone = v.Stats.def.engineTone;
            var am = AudioManager.Ensure();
            var go = new GameObject("CarAudio");
            go.transform.SetParent(transform, false);
            lowpass = go.AddComponent<AudioLowPassFilter>();
            lowpass.cutoffFrequency = 22000f;
            var clipsL = am.GetEngineLayers(cylinders, tone);
            float[] rpmRef = { 1500f, 3500f, 6000f };
            for (int i = 0; i < 3; i++)
            {
                layers[i] = MakeSource(go, clipsL[i], true, 0f);
                baseHz[i] = rpmRef[i] / 60f * cylinders * 0.5f;
                layers[i].Play();
            }
            turbo = MakeSource(go, am.Get("turbo"), true, 0f); turbo.Play();
            skid = MakeSource(go, am.Get("skid"), true, 0f); skid.Play();
            if (player)
            {
                wind = MakeSource(go, am.Get("wind"), true, 0f); wind.Play();
                rain = MakeSource(go, am.Get("rain"), true, 0f); rain.Play();
            }
            oneShot = MakeSource(go, null, false, 1f);
            vc.Engine.BlowOff += OnBlowOff;
            vc.Engine.Backfired += OnBackfire;
            vc.Engine.Shifted += OnShift;
            vc.Collided += OnCollided;
        }

        private void OnDestroy()
        {
            if (vc == null || vc.Engine == null) return;
            vc.Engine.BlowOff -= OnBlowOff; vc.Engine.Backfired -= OnBackfire; vc.Engine.Shifted -= OnShift; vc.Collided -= OnCollided;
        }

        public void SetInterior(bool on)
        {
            interior = on;
            lowpass.cutoffFrequency = on ? 3200f : 22000f;
        }

        private void Shot(string key, float vol, float pitch)
        {
            var c = AudioManager.Ensure().Get(key);
            if (c == null) return;
            oneShot.pitch = pitch;
            oneShot.PlayOneShot(c, vol * AudioManager.Ensure().SfxVolume);
        }

        private void OnBlowOff() { if (vc.Stats.turboBoost > 0.05f) Shot("blowoff", 0.7f, Random.Range(0.95f, 1.1f)); }
        private void OnBackfire() { Shot("backfire" + Random.Range(0, 3), 0.9f, Random.Range(0.9f, 1.15f)); }
        private void OnShift() { Shot("clunk", 0.35f, Random.Range(0.9f, 1.1f)); loadSmooth *= 0.5f; }

        private void OnCollided(Collision c)
        {
            float imp = c.impulse.magnitude;
            if (imp < 800f) return;
            string key = imp > 9000f ? "impact_h" + Random.Range(0, 2) : "impact_l" + Random.Range(0, 2);
            Shot(key, Mathf.Clamp01(imp / 12000f) * 0.9f + 0.15f, Random.Range(0.9f, 1.1f));
        }

        private void Update()
        {
            if (vc == null || vc.Engine == null) return;
            float sfx = AudioManager.Ensure().SfxVolume;
            float rpm = vc.Engine.rpm;
            float hz = rpm / 60f * cylinders * 0.5f;
            float thr = vc.Engine.throttleApplied;
            loadSmooth = Mathf.MoveTowards(loadSmooth, Mathf.Max(thr, 0.0f), Time.deltaTime * (thr > loadSmooth ? 4f : 2.5f));
            float load = 0.5f + 0.5f * loadSmooth;
            float master = (player ? 0.85f : 0.9f) * sfx * (interior ? 0.7f : 1f);

            float wA = Mathf.Clamp01((3600f - rpm) / 2200f);
            float wC = Mathf.Clamp01((rpm - 3400f) / 2200f);
            float wB = Mathf.Clamp01(1f - Mathf.Abs(rpm - 3500f) / 2400f);
            float[] w = { wA, wB, wC };
            for (int i = 0; i < 3; i++)
            {
                layers[i].pitch = Mathf.Clamp(hz / baseHz[i], 0.3f, 3f);
                layers[i].volume = w[i] * load * master;
            }
            float boost = vc.Engine.boost;
            turbo.pitch = 0.6f + boost * 2.2f;
            turbo.volume = (vc.Stats.turboBoost > 0.05f ? Mathf.Pow(boost, 1.5f) * 0.22f : 0f) * master;

            float slip = Mathf.Max(vc.RearSlip, vc.FrontSlip * 0.8f);
            float sv = slip > 0.25f && vc.SpeedMs > 3f ? Mathf.Clamp01((slip - 0.2f) * 1.4f) : 0f;
            if (vc.Wheels != null) foreach (var wh in vc.Wheels) if (wh.locked && vc.SpeedMs > 4f) sv = Mathf.Max(sv, 0.7f);
            skid.volume = Mathf.MoveTowards(skid.volume, sv * 0.8f * sfx, Time.deltaTime * 6f);
            skid.pitch = 0.85f + Mathf.Clamp01(vc.SpeedMs / 40f) * 0.35f + vc.AbsDriftAngle * 0.004f;

            if (player)
            {
                wind.volume = Mathf.Clamp01(vc.SpeedMs / 60f) * 0.45f * sfx * (interior ? 1.2f : 1f);
                wind.pitch = 0.7f + vc.SpeedMs / 70f;
                rain.volume = WorldConditions.Rain * 0.35f * sfx;
            }
        }
    }
}
