using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>
    /// Процедурный звук без ассетов. Двигатель, визг шин и ветер синтезируются в OnAudioFilterRead по оборотам/нагрузке/скольжению;
    /// удары, интерфейс и музыка — короткие клипы, сгенерированные кодом.
    /// </summary>
    public sealed class AudioSynth : MonoBehaviour
    {
        // параметры пишутся из основного потока, читаются в аудиопотоке (float — атомарно)
        volatile float rpmHz, throttle, slipHi, slipLo, gravel, wind, engineOn, boost;
        public float Master = 0.8f, EngineVol = 0.7f, SfxVol = 0.8f, MusicVol = 0.4f;
        double ph1, ph2, ph3, phLfo, phTurbo; float lp1, nBand1, nBand2, nLow, nGrav, lpWind;
        System.Random rnd = new System.Random(3); int sampleRate = 48000;
        AudioSource loop, oneShot, music; AudioClip impactClip, tickClip, bankClip, lostClip, gateClip, errorClip, buyClip, popClip, successClip, clickClip;

        public static AudioSynth Create()
        {
            var go = new GameObject("AudioSynth"); DontDestroyOnLoad(go); var a = go.AddComponent<AudioSynth>(); a.Init(); return a;
        }

        void Init()
        {
            sampleRate = AudioSettings.outputSampleRate;
            loop = gameObject.AddComponent<AudioSource>(); loop.loop = true; loop.spatialBlend = 0; loop.clip = AudioClip.Create("silence", 1024, 1, sampleRate, false); loop.Play();   // OnAudioFilterRead требует источника
            oneShot = gameObject.AddComponent<AudioSource>(); oneShot.spatialBlend = 0;
            music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.spatialBlend = 0;
            impactClip = Render(0.5f, (t, i) => (float)((Noise() * Mathf.Exp(-t * 9) * 0.8 + Mathf.Sin(2 * Mathf.PI * (130 * Mathf.Exp(-t * 4) + 40) * t) * Mathf.Exp(-t * 7)) * 0.9));
            tickClip = Render(0.12f, (t, i) => Mathf.Sin(2 * Mathf.PI * (700 - 4000 * t) * t) * Mathf.Exp(-t * 35) * 0.5f);
            popClip = Render(0.2f, (t, i) => (float)(Noise() * Mathf.Exp(-t * 28) * 0.6));
            clickClip = Render(0.1f, (t, i) => Mathf.Sin(2 * Mathf.PI * (520 + 1200 * t) * t) * Mathf.Exp(-t * 40) * 0.5f);
            bankClip = Render(0.35f, (t, i) => (Mathf.Sin(2 * Mathf.PI * 880 * t) * (t < 0.12f ? 1 : 0) + Mathf.Sin(2 * Mathf.PI * 1175 * t) * (t >= 0.07f ? Mathf.Exp(-(t - 0.07f) * 14) : 0)) * 0.35f);
            lostClip = Render(0.4f, (t, i) => Mathf.Sin(2 * Mathf.PI * (220 - 120 * t) * t) * Mathf.Exp(-t * 7) * 0.4f);
            gateClip = Render(0.25f, (t, i) => (Mathf.Sin(2 * Mathf.PI * 988 * t) * (t < 0.08f ? 1 : 0.0f) + Mathf.Sin(2 * Mathf.PI * 1318 * t) * (t > 0.07f ? Mathf.Exp(-(t - 0.07f) * 16) : 0)) * 0.35f);
            errorClip = Render(0.25f, (t, i) => ((Mathf.Repeat(180 * t * (1 - t), 1) * 2 - 1)) * Mathf.Exp(-t * 9) * 0.3f);
            buyClip = Render(0.45f, (t, i) => (Sq(660 * t) * (t < 0.1f ? 1 : 0) + Sq(880 * t) * (t >= 0.09f && t < 0.2f ? 1 : 0) + Sq(1320 * t) * (t >= 0.18f ? Mathf.Exp(-(t - 0.18f) * 9) : 0)) * 0.12f);
            successClip = Render(0.7f, (t, i) => { float f = t < 0.15f ? 523 : t < 0.3f ? 659 : t < 0.45f ? 784 : 1046; return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-((t % 0.15f)) * 6) * 0.35f; });
            music.clip = MakeMusic();
            if (MusicVol > 0) music.Play();
        }

        static float Sq(float p) { return Mathf.Repeat(p, 1f) < 0.5f ? 1 : -1; }
        float Noise() { return (float)(rnd.NextDouble() * 2 - 1); }

        AudioClip Render(float seconds, System.Func<float, int, float> f)
        {
            int n = (int)(seconds * sampleRate); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)sampleRate, i), -1f, 1f);
            var c = AudioClip.Create("fx", n, 1, sampleRate, false); c.SetData(d, 0); return c;
        }

        AudioClip MakeMusic()
        {
            // 8 тактов синтвейва: бас + арпеджио + бочка/хэт, 112 bpm
            float bpm = 112, step = 60f / bpm / 4f; int steps = 64; int n = (int)(steps * step * sampleRate); var d = new float[n];
            int[] bass = { 45, 45, 52, 45, 48, 48, 55, 48, 43, 43, 50, 43, 47, 47, 54, 52 }, arp = { 69, 72, 76, 72, 74, 77, 81, 77, 67, 71, 74, 71, 71, 74, 79, 74 };
            System.Func<int, float> hz = m => 440f * Mathf.Pow(2, (m - 69) / 12f);
            for (int s = 0; s < steps; s++)
            {
                int i0 = (int)(s * step * sampleRate), len = (int)(step * sampleRate); int k = s % 16;
                for (int i = 0; i < len && i0 + i < n; i++)
                {
                    float t = i / (float)sampleRate, v = 0;
                    if (k % 2 == 0) v += (Mathf.Repeat(hz(bass[k]) * t, 1f) * 2 - 1) * Mathf.Exp(-t * 6) * 0.22f;
                    v += Sq(hz(arp[k]) * t) * Mathf.Exp(-t * 12) * 0.04f;
                    if (k % 4 == 0) v += Mathf.Sin(2 * Mathf.PI * (140 * Mathf.Exp(-t * 20) + 40) * t) * Mathf.Exp(-t * 14) * 0.5f;
                    if (k % 4 == 2) v += Noise() * Mathf.Exp(-t * 60) * 0.08f;
                    d[i0 + i] += v;
                }
            }
            var c = AudioClip.Create("music", n, 1, sampleRate, false); c.SetData(d, 0); return c;
        }

        // ---------- вызовы из игры ----------
        public void Apply(float master, float engine, float sfx, float musicVol)
        {
            Master = master; EngineVol = engine; SfxVol = sfx; MusicVol = musicVol; AudioListener.volume = master;
            music.volume = musicVol * 0.5f; if (musicVol > 0 && !music.isPlaying) music.Play(); if (musicVol <= 0 && music.isPlaying) music.Pause();
        }
        void Shot(AudioClip c, float v = 1) { if (c != null) oneShot.PlayOneShot(c, v * SfxVol); }
        public void Impact(double speed) { Shot(impactClip, Mathf.Clamp01((float)speed / 14f) * 0.8f + 0.2f); }
        public void Tick() { Shot(tickClip, 0.4f); }
        public void Click() { Shot(clickClip); }
        public void Error() { Shot(errorClip); }
        public void Buy() { Shot(buyClip); }
        public void Bank() { Shot(bankClip); }
        public void Lost() { Shot(lostClip); }
        public void Gate() { Shot(gateClip); }
        public void Success() { Shot(successClip); }
        public void Pop() { Shot(popClip, 0.5f); }

        /// <summary>Обновление звука машины каждый кадр.</summary>
        public void UpdateCar(Car car, bool paused, string surface)
        {
            if (paused || car == null) { engineOn = 0; slipHi = slipLo = gravel = wind = 0; return; }
            var s = car.Spec; float rn = Mathf.Clamp01((float)(car.Rpm / s.Redline));
            rpmHz = (float)(car.Rpm / 60.0 * 2.0); throttle = (float)car.ThrottleApplied * (car.Limiter > 0 ? 0.4f : 1f); engineOn = EngineVol; boost = (float)car.Boost * (s.Turbo > 0 ? 1 : 0);
            float sl = 0; for (int i = 0; i < 4; i++) sl = Mathf.Max(sl, (float)car.Slip[i]);
            bool grav = surface == "gravel" || surface == "dirt" || surface == "grass";
            float kmh = (float)car.Speed * 3.6f, sp = Mathf.Clamp01((float)car.Speed / 10f);
            slipHi = grav ? 0 : sl * sp * 0.55f; slipLo = grav ? 0 : sl * sp * 0.38f; gravel = grav ? Mathf.Min(0.5f, (sl * 0.5f + Mathf.Clamp01((float)car.Speed / 30f) * 0.25f) * sp) : 0;
            float w = Mathf.Clamp01(kmh / 180f); wind = w * w * 0.35f;
        }

        // ---------- аудиопоток ----------
        void OnAudioFilterRead(float[] data, int channels)
        {
            float rpm = rpmHz, thr = throttle, eng = engineOn, sHi = slipHi, sLo = slipLo, gr = gravel, wn = wind, bo = boost;
            double sr = sampleRate;
            for (int i = 0; i < data.Length; i += channels)
            {
                // двигатель: пила + меандр + треугольник → мягкое ограничение → фильтр низких частот
                ph1 += rpm / sr; ph2 += rpm * 0.5 / sr; ph3 += rpm * 2.01 / sr; phLfo += Mathf.Max(8f, rpm * 0.5f) / sr; phTurbo += (1800 + bo * 3200) / sr;
                ph1 -= System.Math.Floor(ph1); ph2 -= System.Math.Floor(ph2); ph3 -= System.Math.Floor(ph3); phLfo -= System.Math.Floor(phLfo); phTurbo -= System.Math.Floor(phTurbo);
                float saw = (float)(ph1 * 2 - 1), sq = ph2 < 0.5 ? 1f : -1f, tri = (float)(System.Math.Abs(ph3 * 4 - 2) - 1);
                float raw = (float)System.Math.Tanh((saw * 0.5f + sq * 0.22f + tri * 0.35f) * 2.4f);
                float rn = Mathf.Clamp01(rpm * 30f / 7000f);
                float cutoff = (380 + rn * 1500 + thr * 1900) / (float)sr * 6.2831f; cutoff = Mathf.Clamp(cutoff, 0.01f, 0.9f);
                lp1 += (raw - lp1) * cutoff;
                float lfo = 1f + (0.1f + 0.12f * thr) * (float)System.Math.Sin(phLfo * 6.2831853);
                float engine = lp1 * (0.1f + rn * 0.22f + thr * 0.3f) * lfo * eng * 0.55f;
                engine += (float)System.Math.Sin(phTurbo * 6.2831853) * bo * 0.035f * eng;
                float n = (float)(rnd.NextDouble() * 2 - 1);
                // шум впуска
                nLow += (n - nLow) * 0.05f; engine += nLow * thr * rn * 0.3f * eng * 0.4f;
                // визг шин: два узкополосных шума + шорох гравия
                nBand1 += (n - nBand1) * 0.22f; float hi = (n - nBand1) * sHi * SfxVol * 0.6f;
                nBand2 += (n - nBand2) * 0.06f; float lo = nBand2 * sLo * SfxVol * 1.4f;
                nGrav += (n - nGrav) * 0.5f; float grv = (n - nGrav) * gr * SfxVol * 0.6f;
                lpWind += (n - lpWind) * 0.08f; float wnd = lpWind * wn * SfxVol * 1.6f;
                float mix = Mathf.Clamp(engine + hi + lo + grv + wnd, -1f, 1f);
                for (int c = 0; c < channels; c++) data[i + c] += mix;
            }
        }
    }
}
