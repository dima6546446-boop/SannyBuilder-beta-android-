using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Процедурный звук без аудиофайлов: мотор (гармоники частоты вспышек по оборотам и нагрузке),
    /// визг шин (фильтрованный шум), гудок; короткие эффекты (удар, монета, успех, провал, писк)
    /// генерируются в AudioClip один раз при старте.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        AudioSource loop, fx;
        volatile float rpm = 850f, load, skid, horn, master = 0.8f, cylinders = 4f, engineOn;
        double phase, hornPhase1, hornPhase2;
        float lpEngine, lpSkid, noiseLp;
        int sampleRate = 48000;
        uint seed = 12345;
        AudioClip crash, coin, success, fail, click, beepHi, beepLo;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            loop = gameObject.AddComponent<AudioSource>();
            loop.playOnAwake = false; loop.loop = true; loop.spatialBlend = 0f;
            loop.clip = AudioClip.Create("silence", 1024, 1, sampleRate, false);
            loop.Play();
            // эффекты — на отдельном объекте, чтобы фильтр синтеза мотора их не затирал
            var fxGo = new GameObject("Fx");
            fxGo.transform.SetParent(transform, false);
            fx = fxGo.AddComponent<AudioSource>();
            fx.playOnAwake = false; fx.spatialBlend = 0f;
            crash = Make("crash", 0.5f, (t, n) => n * Mathf.Exp(-t * 9f) * 0.9f + Mathf.Sin(t * 2f * Mathf.PI * 70f) * Mathf.Exp(-t * 12f) * 0.6f);
            coin = Make("coin", 0.35f, (t, n) => Sq(t * (t < 0.08f ? 988f : 1319f)) * Mathf.Exp(-t * 7f) * 0.35f);
            success = Make("success", 0.9f, (t, n) => Sq(t * (t < 0.15f ? 523f : t < 0.3f ? 659f : t < 0.45f ? 784f : 1047f)) * Mathf.Exp(-(t % 0.15f) * 6f) * 0.3f);
            fail = Make("fail", 0.8f, (t, n) => Saw(t * (220f - t * 120f)) * Mathf.Exp(-t * 2.5f) * 0.35f);
            click = Make("click", 0.05f, (t, n) => n * Mathf.Exp(-t * 120f) * 0.4f);
            beepHi = Make("beepHi", 0.09f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * 1500f) * 0.3f);
            beepLo = Make("beepLo", 0.18f, (t, n) => Saw(t * 300f) * 0.3f * (1f - t / 0.18f));
        }

        static float Sq(float ph) { return (ph % 1f) < 0.5f ? 1f : -1f; }
        static float Saw(float ph) { return (ph % 1f) * 2f - 1f; }

        AudioClip Make(string name, float sec, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(sec * sampleRate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)sampleRate, (float)(rng.NextDouble() * 2 - 1)), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        public void SetEngine(float r, float l, float s, bool on) { rpm = r; load = l; skid = s; engineOn = on ? 1f : 0f; }
        public void SetHorn(bool on) { horn = on ? 1f : 0f; }
        public void SetVolume(float v) { master = v; AudioListener.volume = v; }
        public void SetCylinders(int c) { cylinders = c; }

        public void Crash(float s) { fx.PlayOneShot(crash, Mathf.Clamp01(0.3f + s)); }
        public void Coin() { fx.PlayOneShot(coin, 0.8f); }
        public void Success() { fx.PlayOneShot(success, 0.8f); }
        public void Fail() { fx.PlayOneShot(fail, 0.8f); }
        public void Click() { fx.PlayOneShot(click, 0.6f); }
        public void Beep(float freq, float dur) { fx.PlayOneShot(freq > 800 ? beepHi : beepLo, 0.6f); }

        float Noise()
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xffffff) / 8388608f - 1f;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float sr = sampleRate;
            float r = rpm, l = load, s = skid, h = horn, on = engineOn;
            // частота вспышек: обороты/60 × цилиндры/2
            double f0 = r / 60.0 * cylinders / 2.0;
            float amp = (0.10f + 0.16f * l) * on;
            float bright = 0.25f + 0.5f * l + r / 14000f;
            float kLp = Mathf.Clamp01(bright * 0.35f);
            for (int i = 0; i < data.Length; i += channels)
            {
                phase += f0 / sr;
                if (phase > 1e6) phase -= 1e6;
                float p = (float)(phase % 1.0);
                // «пульсы» выхлопа: сумма гармоник + немного шума для «грязи»
                float e = Mathf.Sin(p * 6.2832f) * 0.6f + Mathf.Sin(p * 12.566f) * 0.35f * bright + (p < 0.18f ? 0.5f : -0.1f) * bright
                    + Mathf.Sin(p * 3.1416f) * 0.4f + Noise() * 0.12f * l;
                lpEngine += (e - lpEngine) * kLp;
                float v = lpEngine * amp;
                if (s > 0.02f)
                {
                    float nz = Noise();
                    noiseLp += (nz - noiseLp) * 0.35f;
                    lpSkid += ((nz - noiseLp) - lpSkid) * 0.5f;
                    v += lpSkid * s * 0.22f;
                }
                if (h > 0f)
                {
                    hornPhase1 += 392.0 / sr; hornPhase2 += 494.0 / sr;
                    v += (Sq((float)(hornPhase1 % 1.0)) + Sq((float)(hornPhase2 % 1.0))) * 0.06f;
                }
                for (int c = 0; c < channels; c++) data[i + c] = data[i + c] * 0f + v;
            }
        }
    }
}
