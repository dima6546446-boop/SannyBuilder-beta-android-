using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    public enum Wave { Sine, Square, Saw, Tri, Noise }
    public enum Flt { None, Low, Band, High }

    /// <summary>Один «голос» синтезатора: осциллятор или шум → фильтр → огибающая → панорама.</summary>
    public class Voice
    {
        public Wave wave = Wave.Sine;
        public float f0 = 440f, f1 = -1f, f2 = -1f, sweep = 1f; // частота: f0 → f1 за sweep·dur, затем → f2 (экспоненциально)
        public float dur = 0.2f, attack = 0.01f, vol = 0.1f, pan;
        public bool perc = true;           // true — затухание по экспоненте до конца; false — «вентиль» (держит и обрывается)
        public float release = 0.01f;
        public Flt filter = Flt.None;
        public float cutoff = 2000f, q = 0.707f;
        public bool track;                 // частота среза следует за частотой тона (свист)
        public float vib, vibRate = 6f;    // вибрато: доля частоты
        public int bus;                    // 0 — эффекты, 1 — радио
        public float delay;                // задержка старта, с
        public double at = -1;             // или точное время старта по часам аудиопотока (GameAudio.Now), с
        // состояние (заполняет аудиопоток)
        internal long start;
        internal double ph, vph;
        internal float ic1, ic2, a1, a2, a3, k;
        internal uint rng = 22222;
        internal int coefT;
    }

    /// <summary>
    /// Процедурный звук без аудиофайлов (порт WebAudio-движка веб-версии). Непрерывно: мотор (гармоники частоты
    /// вспышек + «рокот»), визг шин, ветер, дождь, заводской гудок. Разовые эффекты — голоса синтезатора с точным
    /// расписанием по сэмплам: удар, монета, писк, сирена ДПС, затвор камеры, выстрелы прямотока, дверь, свист,
    /// зажигалка, шаги, мелодичные сигналы; на шине 1 — музыка радио (её глушит «динамик в салоне»).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        AudioSource loop;
        volatile float rpm = 850f, load, skid, squeal = 0.5f, speed, horn, master = 0.8f, cylinders = 4f, rasp = 1f, engineOn, mix = 1f;
        volatile float rain, rainCut = 2600f, radioGain;
        public int hornType;
        double phase;
        float lpEngine, noiseLp, rumbleLp, rumbleLp2, windLp, hornLp;
        double hornPh1, hornPh2;
        float skidI1, skidI2, rainI1, rainI2;
        float sr = 48000f;
        uint seed = 12345;
        long clock;                         // позиция аудиопотока в сэмплах
        readonly List<Voice> pending = new List<Voice>();
        readonly List<Voice> voices = new List<Voice>(128);
        readonly object gate = new object();
        double tuneEnd;

        void Awake()
        {
            sr = AudioSettings.outputSampleRate;
            loop = gameObject.AddComponent<AudioSource>();
            loop.playOnAwake = false; loop.loop = true; loop.spatialBlend = 0f;
            loop.clip = AudioClip.Create("silence", 1024, 1, (int)sr, false);
            loop.Play();
        }

        /// <summary>Текущее время аудиопотока, с (для расписания нот).</summary>
        public double Now => System.Threading.Interlocked.Read(ref clock) / (double)sr + 0.04;

        public void Play(Voice v)
        {
            if (master <= 0.001f) return;
            lock (gate) { if (pending.Count < 96) pending.Add(v); }
        }

        /// <summary>Нота с огибающей «удар-затухание».</summary>
        public void Tone(float f, float dur, Wave w, float vol, float delay = 0f, float cutoff = 0f, float vib = 0f, int bus = 0)
        {
            Play(new Voice { wave = w, f0 = f, dur = dur, vol = vol, delay = delay, filter = cutoff > 0 ? Flt.Low : Flt.None, cutoff = cutoff, vib = vib, bus = bus });
        }

        /// <summary>Шумовой всплеск через фильтр (как _burst в веб-версии).</summary>
        public void Burst(float freq, float q, float vol, float dur, Flt type = Flt.Band, float delay = 0f, int bus = 0)
        {
            Play(new Voice { wave = Wave.Noise, filter = type, cutoff = freq, q = q, vol = vol, dur = dur, attack = Mathf.Min(0.01f, dur * 0.2f), delay = delay, bus = bus });
        }

        public void Sweep(Wave w, float f0, float f1, float dur, float vol, float delay = 0f)
        {
            Play(new Voice { wave = w, f0 = f0, f1 = f1, dur = dur, vol = vol, attack = 0.002f, delay = delay });
        }

        // ------------------------------------------------------------------ непрерывные
        public void SetEngine(float r, float l, float s, bool on) { rpm = r; load = l; skid = s; engineOn = on ? 1f : 0f; }
        /// <summary>mix — громкость мотора (пешком тише с расстоянием); sq — тон визга 0..1.</summary>
        public void SetEngineMix(float m, float sq, float spd) { mix = m; squeal = sq; speed = spd; }
        public void SetHorn(bool on)
        {
            if (hornType == 1 || hornType == 2)
            {
                horn = 0f;
                if (on && Now > tuneEnd) PlayHornTune(hornType);
                return;
            }
            horn = on ? 1f : 0f;
        }
        public void SetVolume(float v) { master = v; AudioListener.volume = v; }
        public void SetCylinders(int c) { cylinders = c; }
        public void SetRasp(float r) { rasp = r; }
        public void SetRain(float level, bool inside) { rain = level * (inside ? 0.16f : 0.12f); rainCut = inside ? 900f : 2600f; }
        /// <summary>Громкость радио (плавно); muffled — «динамик в салоне» слышен снаружи: ноты глуше.</summary>
        public void SetRadio(float gain, bool muffled) { radioGain += (gain - radioGain) * 0.1f; RadioCut = muffled ? 700f : 6000f; }
        public float RadioCut = 6000f;

        // ------------------------------------------------------------------ эффекты
        public void Beep(float freq, float dur, float vol = 0.12f, Wave w = Wave.Square, float delay = 0f)
        {
            Play(new Voice { wave = w, f0 = freq, dur = dur, vol = vol, attack = 0.005f, perc = false, release = 0.01f, delay = delay });
        }

        public void Melody(float[] notes, Wave w, float vol, float delay = 0f)
        {
            float t = delay + 0.02f;
            for (int i = 0; i + 1 < notes.Length; i += 2)
            {
                if (notes[i] > 0) Play(new Voice { wave = w, f0 = notes[i], dur = notes[i + 1] * 0.95f, vol = vol, attack = 0.015f, delay = t });
                t += notes[i + 1];
            }
        }

        public void Coin() { Melody(new[] { 988f, 0.08f, 1319f, 0.22f }, Wave.Square, 0.08f); }
        public void Success() { Melody(new[] { 523f, 0.12f, 659f, 0.12f, 784f, 0.12f, 1047f, 0.35f }, Wave.Tri, 0.18f); }
        public void Fail() { Melody(new[] { 392f, 0.18f, 330f, 0.18f, 262f, 0.4f }, Wave.Saw, 0.09f); }
        public void Click() { Beep(900, 0.03f, 0.06f, Wave.Tri); }

        public void Crash(float s)
        {
            Burst(700 + s * 1500, 0.707f, 0.9f * s, 0.45f, Flt.Low);
            Sweep(Wave.Sine, 90, 35, 0.3f, 0.8f * s);
        }

        /// <summary>Щелчок реле поворотника.</summary>
        public void Tick(bool on) { Play(new Voice { wave = Wave.Noise, filter = Flt.Band, cutoff = on ? 2600 : 1800, q = 3, vol = 0.35f, dur = 0.03f, attack = 0.0005f }); }

        /// <summary>Гудок машины трафика: два тона, панорама по положению.</summary>
        public void AiHorn(float volume, float pan)
        {
            if (volume < 0.02f) return;
            foreach (var f in new[] { 330f, 392f })
                Play(new Voice { wave = Wave.Square, f0 = f, dur = 0.36f, vol = 0.18f * volume * 0.5f, attack = 0.02f, perc = false, release = 0.06f, pan = Mathf.Clamp(pan, -1, 1) });
        }

        /// <summary>«Крякалка» ДПС.</summary>
        public void Siren()
        {
            Play(new Voice { wave = Wave.Saw, f0 = 600, f1 = 1500, f2 = 700, sweep = 0.45f, dur = 0.55f, vol = 0.14f, attack = 0.03f, perc = false, release = 0.1f, filter = Flt.Low, cutoff = 2500 });
        }

        /// <summary>Затвор камеры «Стрелка».</summary>
        public void Shutter()
        {
            Burst(3000, 0.707f, 0.3f, 0.05f, Flt.High);
            Burst(3000, 0.707f, 0.3f, 0.05f, Flt.High, 0.07f);
        }

        /// <summary>Выстрел прямотока: хлопок + низкий «бах».</summary>
        public void Pop(float v = 1f)
        {
            Burst(900, 0.7f, 0.55f * v, 0.09f, Flt.Low);
            Burst(2400, 1.5f, 0.18f * v, 0.05f, Flt.Band);
            Sweep(Wave.Sine, 95, 40, 0.1f, 0.5f * v);
        }

        public void Pops(int n) { for (int i = 0; i < n; i++) { float d = i * 0.12f + Random.value * 0.05f; Burst(900, 0.7f, 0.44f, 0.09f, Flt.Low, d); Sweep(Wave.Sine, 95, 40, 0.1f, 0.4f, d); } }

        public void Footstep(float s = 0.6f)
        {
            Burst(500 + Random.value * 300, 1.2f, 0.09f * s, 0.07f);
            Burst(2600, 2, 0.025f * s, 0.04f, Flt.Band, 0.008f);
        }

        /// <summary>Хлопок двери «Жигулей».</summary>
        public void Door()
        {
            Burst(180, 0.8f, 0.5f, 0.18f, Flt.Low);
            Burst(1400, 4, 0.08f, 0.12f, Flt.Band, 0.02f);
            Sweep(Wave.Sine, 110, 50, 0.18f, 0.35f);
        }

        public void Lighter()
        {
            Burst(4200, 3, 0.12f, 0.05f);
            Burst(3600, 2, 0.08f, 0.04f, Flt.Band, 0.06f);
            Burst(1800, 0.5f, 0.03f, 0.6f, Flt.High, 0.1f);
        }

        public void Inhale()
        {
            for (int i = 0; i < 6; i++) Burst(5000 + Random.value * 2000, 6, 0.02f, 0.02f, Flt.Band, 0.05f + i * 0.12f + Random.value * 0.05f);
            Burst(900, 0.6f, 0.03f, 0.7f, Flt.Band, 0.05f);
        }

        public void Exhale() { Burst(700, 0.5f, 0.05f, 0.6f); }

        public void Bite(bool crunchy = false)
        {
            for (int i = 0; i < (crunchy ? 4 : 2); i++) Burst(crunchy ? 3200 : 1200, crunchy ? 3 : 1.2f, crunchy ? 0.07f : 0.09f, 0.05f, Flt.Band, i * 0.07f + Random.value * 0.03f);
        }

        public void Gulp() { for (int i = 0; i < 2; i++) Sweep(Wave.Sine, 220, 90, 0.14f, 0.18f, i * 0.28f); }

        /// <summary>Свист «в два пальца»: два подъёма с завитком, тон + «воздух» вокруг него.</summary>
        public void Whistle()
        {
            void Seg(float d, float a, float b, float c, float len)
            {
                Play(new Voice { wave = Wave.Sine, f0 = a, f1 = b, f2 = c, sweep = 0.55f, dur = len, vol = 0.16f, attack = 0.04f, perc = false, release = 0.06f, vib = 18f / b, vibRate = 7f, delay = d });
                Play(new Voice { wave = Wave.Noise, f0 = a, f1 = b, f2 = c, sweep = 0.55f, dur = len, vol = 0.16f * 0.35f, attack = 0.04f, perc = false, release = 0.06f, filter = Flt.Band, q = 8, track = true, delay = d });
            }
            Seg(0.02f, 1300, 2700, 2500, 0.32f);
            Seg(0.44f, 1500, 2900, 1700, 0.55f);
        }

        // ------------------------------------------------------------------ сигналы (тюнинг)
        static readonly float[] Lezginka = { 659, .1f, 659, .1f, 698, .1f, 659, .1f, 587, .1f, 523, .1f, 494, .1f, 523, .2f, 659, .1f, 831, .1f, 880, .1f, 831, .1f, 698, .1f, 659, .1f, 587, .1f, 659, .1f, 440, .3f };
        static readonly float[] Italian = { 392, .12f, 523, .12f, 659, .12f, 784, .24f, 659, .12f, 784, .4f };

        public static readonly string[] HornNames = { "Заводской", "«Лезгинка» (мелодия)", "«Итальянка» (6 нот)", "«Газель-дудка»" };
        public static readonly int[] HornPrices = { 0, 5000, 7000, 3000 };

        /// <summary>Сыграть сигнал целиком (предпрослушивание в гараже).</summary>
        public void HornSample(int type)
        {
            if (type == 1 || type == 2) { PlayHornTune(type); return; }
            HornTone(type, 0.45f);
        }

        void HornTone(int type, float dur)
        {
            bool low = type == 3;
            foreach (var f in low ? new[] { 233f, 294f } : new[] { 405f, 510f })
                Play(new Voice { wave = low ? Wave.Saw : Wave.Square, f0 = f, dur = dur, vol = 0.11f, attack = 0.01f, perc = false, release = 0.03f, filter = Flt.Low, cutoff = 2200 });
        }

        void PlayHornTune(int type)
        {
            var notes = type == 1 ? Lezginka : Italian;
            float t = 0.01f;
            for (int i = 0; i < notes.Length; i += 2)
            {
                float f = notes[i], d = notes[i + 1];
                Play(new Voice { wave = Wave.Square, f0 = f, dur = d * 0.98f, vol = 0.085f, attack = 0.012f, perc = false, release = d * 0.13f, filter = Flt.Low, cutoff = 2600, delay = t });
                if (type == 2) Play(new Voice { wave = Wave.Square, f0 = f * 1.26f, dur = d * 0.98f, vol = 0.085f, attack = 0.012f, perc = false, release = d * 0.13f, filter = Flt.Low, cutoff = 2600, delay = t });
                t += d;
            }
            tuneEnd = Now + t;
        }

        // ------------------------------------------------------------------ аудиопоток
        float Noise()
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xffffff) / 8388608f - 1f;
        }

        static float Osc(Wave w, float p)
        {
            switch (w)
            {
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Saw: return p * 2f - 1f;
                case Wave.Tri: return p < 0.5f ? p * 4f - 1f : 3f - p * 4f;
                default: return Mathf.Sin(p * 6.2831853f);
            }
        }

        void Coefs(Voice v, float fc)
        {
            float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(fc, 20f, sr * 0.45f) / sr);
            v.k = 1f / Mathf.Max(0.05f, v.q);
            v.a1 = 1f / (1f + g * (g + v.k)); v.a2 = g * v.a1; v.a3 = g * v.a2;
        }

        float Freq(Voice v, float t)
        {
            if (v.f1 <= 0f) return v.f0;
            float t1 = v.dur * v.sweep;
            if (t < t1) return v.f0 * Mathf.Pow(v.f1 / v.f0, t / t1);
            if (v.f2 <= 0f) return v.f1;
            return v.f1 * Mathf.Pow(v.f2 / v.f1, Mathf.Min(1f, (t - t1) / Mathf.Max(1e-4f, v.dur - t1)));
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (gate)
            {
                foreach (var v in pending)
                {
                    v.start = v.at >= 0 ? (long)(v.at * sr) : clock + (long)(v.delay * sr);
                    if (v.filter != Flt.None) Coefs(v, v.track ? v.f0 : v.cutoff);
                    if (voices.Count < 120) voices.Add(v);
                }
                pending.Clear();
            }
            int frames = data.Length / channels;
            System.Array.Clear(data, 0, data.Length);

            // --- мотор, рокот, визг, ветер, гудок, дождь
            float r = rpm, l = load, s = Mathf.Min(1f, skid), on = engineOn * mix;
            double f0 = r / 60.0 * cylinders / 2.0;
            float amp = (0.1f + 0.16f * l + r / 6500f * 0.08f) * rasp * on;
            float bright = 0.25f + 0.5f * l + r / 14000f;
            float kLp = Mathf.Clamp01((220f + r * 0.22f + l * 900f) / sr * 6.2832f);
            float rumbleAmp = (0.15f + l * 0.25f) * on * 0.5f;
            float windAmp = Mathf.Min(speed / 45f, 1f) * 0.12f;
            // визг: полосовой фильтр (TPT SVF) по тону squeal
            float sg = Mathf.Tan(Mathf.PI * (1350f + squeal * 1100f) / sr), sk = 1f / 7f;
            float sa1 = 1f / (1f + sg * (sg + sk)), sa2 = sg * sa1, sa3 = sg * sa2;
            float rg = Mathf.Tan(Mathf.PI * rainCut / sr), rk = 1f / 0.4f;
            float ra1 = 1f / (1f + rg * (rg + rk)), ra2 = rg * ra1, ra3 = rg * ra2;
            float h = horn;
            bool low = hornType == 3;
            float hf1 = low ? 233f : 405f, hf2 = low ? 294f : 510f;
            float rGain = radioGain, rainL = rain;
            for (int i = 0; i < frames; i++)
            {
                float v = 0f;
                if (on > 0.001f)
                {
                    phase += f0 / sr;
                    if (phase > 1e6) phase -= 1e6;
                    float p = (float)(phase % 1.0), p2 = (float)((phase * 0.5) % 1.0), p3 = (float)((phase * 1.5) % 1.0);
                    float e = (p * 2f - 1f) * 0.45f + (p2 < 0.5f ? 0.3f : -0.3f) + Osc(Wave.Tri, p3) * 0.35f * bright;
                    float nz = Noise();
                    rumbleLp += (nz - rumbleLp) * 0.02f; rumbleLp2 += (rumbleLp - rumbleLp2) * 0.05f;
                    lpEngine += (e - lpEngine) * kLp;
                    v += lpEngine * amp + (rumbleLp - rumbleLp2) * 6f * rumbleAmp;
                }
                if (s > 0.02f)
                {
                    float x = Noise();
                    float v3 = x - skidI2, v1 = sa1 * skidI1 + sa2 * v3, v2 = skidI2 + sa2 * skidI1 + sa3 * v3;
                    skidI1 = 2f * v1 - skidI1; skidI2 = 2f * v2 - skidI2;
                    v += sk * v1 * s * 0.28f * 2.5f;
                }
                if (windAmp > 0.002f) { windLp += (Noise() - windLp) * 0.065f; v += windLp * windAmp * 1.5f; }
                if (h > 0f)
                {
                    hornPh1 += hf1 / sr; hornPh2 += hf2 / sr;
                    float hv = low ? Osc(Wave.Saw, (float)(hornPh1 % 1.0)) + Osc(Wave.Saw, (float)(hornPh2 % 1.0)) : Osc(Wave.Square, (float)(hornPh1 % 1.0)) + Osc(Wave.Square, (float)(hornPh2 % 1.0));
                    hornLp += (hv - hornLp) * 0.25f;
                    v += hornLp * 0.11f;
                }
                if (rainL > 0.002f)
                {
                    float x = Noise();
                    float v3 = x - rainI2, v1 = ra1 * rainI1 + ra2 * v3, v2 = rainI2 + ra2 * rainI1 + ra3 * v3;
                    rainI1 = 2f * v1 - rainI1; rainI2 = 2f * v2 - rainI2;
                    v += rk * v1 * rainL * 0.9f;
                }
                for (int c = 0; c < channels; c++) data[i * channels + c] = v;
            }

            // --- голоса
            float invSr = 1f / sr;
            for (int vi = voices.Count - 1; vi >= 0; vi--)
            {
                var vo = voices[vi];
                long end = vo.start + (long)(vo.dur * sr);
                if (clock + frames <= vo.start) continue;
                float panL = Mathf.Sqrt(0.5f * (1f - vo.pan)) * 1.414f, panR = Mathf.Sqrt(0.5f * (1f + vo.pan)) * 1.414f;
                for (int i = 0; i < frames; i++)
                {
                    long n = clock + i;
                    if (n < vo.start) continue;
                    if (n >= end) break;
                    float t = (n - vo.start) * invSr;
                    float f = Freq(vo, t);
                    float x;
                    if (vo.wave == Wave.Noise)
                    {
                        vo.rng ^= vo.rng << 13; vo.rng ^= vo.rng >> 17; vo.rng ^= vo.rng << 5;
                        x = (vo.rng & 0xffffff) / 8388608f - 1f;
                    }
                    else
                    {
                        float ff = f;
                        if (vo.vib > 0f) { vo.vph += vo.vibRate * invSr; ff *= 1f + vo.vib * Mathf.Sin((float)(vo.vph % 1.0) * 6.2831853f); }
                        vo.ph += ff * invSr;
                        if (vo.ph > 1e4) vo.ph -= 1e4;
                        x = Osc(vo.wave, (float)(vo.ph % 1.0));
                    }
                    if (vo.filter != Flt.None)
                    {
                        if (vo.track && (vo.coefT++ & 31) == 0) Coefs(vo, f);
                        float v3 = x - vo.ic2, v1 = vo.a1 * vo.ic1 + vo.a2 * v3, v2 = vo.ic2 + vo.a2 * vo.ic1 + vo.a3 * v3;
                        vo.ic1 = 2f * v1 - vo.ic1; vo.ic2 = 2f * v2 - vo.ic2;
                        x = vo.filter == Flt.Low ? v2 : vo.filter == Flt.Band ? vo.k * v1 : x - vo.k * v1 - v2;
                    }
                    float env;
                    if (t < vo.attack) env = t / vo.attack;
                    else if (vo.perc) env = Mathf.Exp(-7f * (t - vo.attack) / Mathf.Max(1e-3f, vo.dur - vo.attack));
                    else env = Mathf.Clamp01((vo.dur - t) / Mathf.Max(1e-4f, vo.release));
                    float y = x * env * vo.vol * (vo.bus == 1 ? rGain : 1f);
                    if (channels >= 2) { data[i * channels] += y * panL; data[i * channels + 1] += y * panR; }
                    else data[i * channels] += y;
                }
                if (clock + frames >= end) voices.RemoveAt(vi);
            }

            for (int i = 0; i < frames; i++)
                for (int c = 0; c < channels; c++)
                {
                    float y = data[i * channels + c];
                    data[i * channels + c] = y / (1f + Mathf.Abs(y) * 0.6f); // мягкий лимитер (как компрессор WebAudio)
                }
            System.Threading.Interlocked.Add(ref clock, frames);
        }
    }
}
