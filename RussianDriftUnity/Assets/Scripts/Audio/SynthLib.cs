using System;
using UnityEngine;

namespace RussianDrift.Audio
{
    /// <summary>Procedural audio synthesis: every sound in the game can be generated without asset files.</summary>
    public static class SynthLib
    {
        public const int Rate = 44100;
        public const int MusicRate = 32000;

        private static AudioClip MakeClip(string name, float[] data, int channels, int rate, bool stream = false)
        {
            var clip = AudioClip.Create(name, data.Length / channels, channels, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Normalize(float[] d, float peak)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            if (m < 1e-5f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>Returns a shortened buffer whose end flows seamlessly into its start.</summary>
        private static float[] LoopCrossfade(float[] d, int fade)
        {
            fade = Mathf.Min(fade, d.Length / 4);
            int n2 = d.Length - fade;
            var o = new float[n2];
            Array.Copy(d, o, n2);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                o[i] = d[i] * t + d[n2 + i] * (1f - t);
            }
            return o;
        }

        /// <summary>Low-pass that treats the buffer as circular so noise tiles without a seam.</summary>
        private static void PeriodicLowPass(float[] d, float cutoff)
        {
            int n = d.Length;
            var big = new float[n * 3];
            for (int k = 0; k < 3; k++) Array.Copy(d, 0, big, k * n, n);
            LowPass(big, cutoff);
            Array.Copy(big, n, d, 0, n);
        }

        private static float[] Noise(int n, int seed)
        {
            var r = new System.Random(seed);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = (float)(r.NextDouble() * 2.0 - 1.0);
            return d;
        }

        private static void LowPass(float[] d, float cutoff)
        {
            float a = Mathf.Clamp01(1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate));
            float y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        }

        private static void HighPass(float[] d, float cutoff)
        {
            float a = Mathf.Clamp01(1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate));
            float y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = d[i] - y; }
        }

        // -------------------------------------------------- engine
        /// <summary>
        /// One seamless engine layer. f0 = firing frequency in Hz (must be a multiple of 1/duration), brightness 0..1 controls harmonic roll-off.
        /// </summary>
        public static AudioClip EngineLoop(string name, float f0, float brightness, float roughness, int seed)
        {
            float dur = 0.5f;
            int n = (int)(Rate * dur);
            f0 = Mathf.Round(f0 / (1f / dur)) * (1f / dur);        // integer cycles
            var d = new float[n];
            var rnd = new System.Random(seed);
            int harmonics = 14;
            var phase = new float[harmonics + 1];
            for (int k = 1; k <= harmonics; k++) phase[k] = (float)(rnd.NextDouble() * Mathf.PI * 2f);
            float[] amp = new float[harmonics + 1];
            for (int k = 1; k <= harmonics; k++)
            {
                float roll = Mathf.Pow(k, -(1.9f - brightness * 1.1f));
                float odd = (k % 2 == 1) ? 1f : 0.62f;
                amp[k] = roll * odd;
            }
            var noise = Noise(n, seed + 7);
            PeriodicLowPass(noise, 900f + brightness * 2500f);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                float s = 0f;
                for (int k = 1; k <= harmonics; k++)
                    s += amp[k] * (float)Math.Sin(2.0 * Math.PI * k * f0 * t + phase[k]);
                // combustion pulse: amplitude modulation at firing frequency
                float pulse = 0.65f + 0.35f * (float)Math.Sin(2.0 * Math.PI * f0 * 0.5 * t);
                s *= pulse;
                s += noise[i] * roughness * (0.6f + 0.4f * (float)Math.Sin(2.0 * Math.PI * f0 * t));
                d[i] = s;
            }
            Normalize(d, 0.8f);
            return MakeClip(name, d, 1, Rate);
        }

        public static AudioClip TurboWhine()
        {
            int n = Rate / 2;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                d[i] = (float)(Math.Sin(2 * Math.PI * 1200 * t) * 0.6 + Math.Sin(2 * Math.PI * 2400 * t) * 0.25 + Math.Sin(2 * Math.PI * 3600 * t) * 0.12);
            }
            Normalize(d, 0.5f);
            return MakeClip("turbo", d, 1, Rate);
        }

        public static AudioClip BlowOff()
        {
            int n = (int)(Rate * 0.7f);
            var d = Noise(n, 31);
            HighPass(d, 1800f);
            LowPass(d, 9000f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Pow(1f - t, 2.2f) * Mathf.Clamp01(t * 40f);
                d[i] *= env * (0.8f + 0.2f * Mathf.Sin(t * 120f));
            }
            Normalize(d, 0.6f);
            return MakeClip("blowoff", d, 1, Rate);
        }

        public static AudioClip Backfire(int seed)
        {
            int n = (int)(Rate * 0.32f);
            var d = Noise(n, 90 + seed);
            LowPass(d, 2600f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 16f);
                float thump = Mathf.Sin(2f * Mathf.PI * (90f - 40f * t) * t) * Mathf.Exp(-t * 20f);
                d[i] = (d[i] * env * 0.9f + thump * 0.9f) * Mathf.Clamp01(t * 600f);
            }
            Normalize(d, 0.9f);
            return MakeClip("backfire" + seed, d, 1, Rate);
        }

        public static AudioClip GearClunk()
        {
            int n = (int)(Rate * 0.12f);
            var d = new float[n];
            var nz = Noise(n, 5);
            LowPass(nz, 1500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.6f + nz[i] * 0.5f) * Mathf.Exp(-t * 38f);
            }
            Normalize(d, 0.5f);
            return MakeClip("clunk", d, 1, Rate);
        }

        // -------------------------------------------------- tyres, wind, impacts, ambience
        public static AudioClip SkidLoop()
        {
            int n = Rate;
            var d = Noise(n, 11);
            HighPass(d, 700f);
            LowPass(d, 4200f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // squeal: a few detuned tones that wobble
                float sq = Mathf.Sin(2f * Mathf.PI * (1180f + 40f * Mathf.Sin(2f * Mathf.PI * 3f * t)) * t) * 0.18f
                         + Mathf.Sin(2f * Mathf.PI * 1960f * t) * 0.07f;
                d[i] = d[i] * 0.55f + sq;
            }
            d = LoopCrossfade(d, 2000);
            Normalize(d, 0.7f);
            return MakeClip("skid", d, 1, Rate);
        }

        public static AudioClip WindLoop()
        {
            int n = Rate * 2;
            var d = Noise(n, 17);
            LowPass(d, 700f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] *= 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            }
            d = LoopCrossfade(d, 4000);
            Normalize(d, 0.6f);
            return MakeClip("wind", d, 1, Rate);
        }

        public static AudioClip RainLoop()
        {
            int n = Rate * 3;
            var d = Noise(n, 23);
            HighPass(d, 1500f);
            LowPass(d, 11000f);
            var r = new System.Random(9);
            for (int k = 0; k < 400; k++)
            {
                int p = r.Next(n - 400);
                float a = (float)r.NextDouble() * 0.7f;
                for (int i = 0; i < 300; i++) d[p + i] += a * Mathf.Exp(-i / 40f) * (r.Next(2) == 0 ? 1f : -1f);
            }
            d = LoopCrossfade(d, 6000);
            Normalize(d, 0.5f);
            return MakeClip("rain", d, 1, Rate);
        }

        public static AudioClip CityAmbience()
        {
            int n = Rate * 8;
            var d = Noise(n, 41);
            LowPass(d, 380f);
            var r = new System.Random(77);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = d[i] * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.11f * t));
            }
            // faint distant horns and sirens
            for (int h = 0; h < 3; h++)
            {
                int start = r.Next(n / 8, n - Rate);
                float f = 360f + r.Next(0, 140);
                int len = (int)(Rate * (0.25f + (float)r.NextDouble() * 0.3f));
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((len - i) / 800f);
                    d[start + i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 1.26f * t)) * 0.05f * env;
                }
            }
            d = LoopCrossfade(d, 8000);
            Normalize(d, 0.45f);
            return MakeClip("city", d, 1, Rate);
        }

        public static AudioClip Impact(float heavy, int seed)
        {
            int n = (int)(Rate * (0.25f + heavy * 0.35f));
            var d = Noise(n, 200 + seed);
            LowPass(d, 1500f + (1f - heavy) * 3500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float thud = Mathf.Sin(2f * Mathf.PI * (70f - 30f * t) * t) * Mathf.Exp(-t * 14f);
                float metal = Mathf.Sin(2f * Mathf.PI * (420f + seed * 60f) * t) * Mathf.Exp(-t * 22f) * 0.25f;
                d[i] = (d[i] * Mathf.Exp(-t * (10f + (1f - heavy) * 18f)) * 0.8f + thud + metal) * Mathf.Clamp01(t * 800f);
            }
            Normalize(d, 0.9f);
            return MakeClip("impact" + seed, d, 1, Rate);
        }

        // -------------------------------------------------- UI / jingles
        private static AudioClip Tone(string name, float[] freqs, float[] times, float dur, float decay, float vol, bool square = false)
        {
            int n = (int)(Rate * dur);
            var d = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = (int)(times[k] * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float s = Mathf.Sin(2f * Mathf.PI * freqs[k] * t);
                    if (square) s = Mathf.Sign(s) * 0.5f + s * 0.5f;
                    d[i] += s * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f);
                }
            }
            Normalize(d, vol);
            return MakeClip(name, d, 1, Rate);
        }

        public static AudioClip UiClick() { return Tone("click", new[] { 1300f }, new[] { 0f }, 0.07f, 60f, 0.35f); }
        public static AudioClip UiConfirm() { return Tone("confirm", new[] { 700f, 1050f }, new[] { 0f, 0.07f }, 0.3f, 14f, 0.45f); }
        public static AudioClip UiBack() { return Tone("back", new[] { 900f, 600f }, new[] { 0f, 0.06f }, 0.22f, 18f, 0.4f); }
        public static AudioClip Cash() { return Tone("cash", new[] { 1568f, 2093f, 2637f }, new[] { 0f, 0.06f, 0.12f }, 0.5f, 9f, 0.5f); }
        public static AudioClip LevelUp() { return Tone("levelup", new[] { 523f, 659f, 784f, 1046f }, new[] { 0f, 0.1f, 0.2f, 0.3f }, 0.9f, 4.5f, 0.6f, true); }
        public static AudioClip CountBeep() { return Tone("beep", new[] { 440f }, new[] { 0f }, 0.3f, 8f, 0.55f); }
        public static AudioClip GoBeep() { return Tone("go", new[] { 880f, 1320f }, new[] { 0f, 0f }, 0.6f, 5f, 0.6f); }
        public static AudioClip Checkpoint() { return Tone("checkpoint", new[] { 988f, 1318f }, new[] { 0f, 0.08f }, 0.45f, 8f, 0.5f); }
        public static AudioClip ComboUp() { return Tone("combo", new[] { 660f, 880f }, new[] { 0f, 0.05f }, 0.25f, 14f, 0.4f, true); }
        public static AudioClip Fail() { return Tone("fail", new[] { 330f, 262f, 196f }, new[] { 0f, 0.15f, 0.3f }, 0.9f, 5f, 0.5f, true); }

        // -------------------------------------------------- music
        public enum MusicStyle { Menu, Phonk, Drive, Night }

        private static float Midi(float note) { return 440f * Mathf.Pow(2f, (note - 69f) / 12f); }

        /// <summary>Generates a stereo music loop (bars of 4/4) with drums, 808-style bass, cowbell melody and pads.</summary>
        public static AudioClip Music(string name, MusicStyle style, int seed)
        {
            float[] data = MusicData(style, seed);
            return MakeClip(name, data, 2, MusicRate);
        }

        /// <summary>Pure-math music renderer (no Unity object access) so it can run on a worker thread.</summary>
        public static float[] MusicData(MusicStyle style, int seed)
        {
            float bpm = style == MusicStyle.Menu ? 92f : (style == MusicStyle.Phonk ? 132f : (style == MusicStyle.Drive ? 124f : 108f));
            int bars = 8;
            float beat = 60f / bpm;
            float stepLen = beat / 4f;                      // 16th
            int steps = bars * 16;
            int n = (int)(steps * stepLen * MusicRate);
            var L = new float[n];
            var R = new float[n];
            var rnd = new System.Random(seed);

            int root = style == MusicStyle.Night ? 45 : (style == MusicStyle.Menu ? 48 : (style == MusicStyle.Drive ? 43 : 41));
            int[] minor = { 0, 3, 5, 7, 10, 12 };
            int[] prog = { 0, 0, -2, -4, 0, 0, 3, -2 };      // bass root offsets per bar

            for (int s = 0; s < steps; s++)
            {
                int bar = s / 16, st = s % 16;
                int off = prog[bar % prog.Length];
                int t0 = (int)(s * stepLen * MusicRate);

                bool kick = style == MusicStyle.Menu ? (st == 0 || st == 10) : (st == 0 || st == 6 || st == 10 || (st == 14 && bar % 2 == 1));
                bool snare = style == MusicStyle.Menu ? (st == 8) : (st == 4 || st == 12);
                bool hat = style == MusicStyle.Menu ? (st % 4 == 2) : (st % 2 == 0 || (bar % 4 == 3 && st > 11));

                if (kick) AddKick(L, R, t0, style == MusicStyle.Menu ? 0.5f : 0.9f);
                if (snare) AddSnare(L, R, t0, style == MusicStyle.Menu ? 0.25f : 0.4f, rnd);
                if (hat) AddHat(L, R, t0, (st % 4 == 2 ? 0.16f : 0.1f) * (style == MusicStyle.Menu ? 0.7f : 1f), rnd);

                // bass (808 glide)
                if (style != MusicStyle.Menu && (st == 0 || st == 6 || st == 10 || st == 13))
                {
                    float f = Midi(root + off - 12);
                    AddBass(L, R, t0, f, stepLen * (st == 0 ? 5f : 3f), 0.55f);
                }
                else if (style == MusicStyle.Menu && (st == 0 || st == 8))
                    AddPad(L, R, t0, Midi(root + off), stepLen * 8f, 0.13f);

                // cowbell / lead melody
                if (style != MusicStyle.Menu && (st == 0 || st == 3 || st == 6 || st == 8 || st == 11 || st == 14))
                {
                    int deg = minor[(st * 7 + bar * 3 + seed) % minor.Length];
                    AddCowbell(L, R, t0, Midi(root + 24 + off + deg), 0.17f);
                }
                if (style == MusicStyle.Menu || style == MusicStyle.Night)
                {
                    if (st % 4 == 0)
                    {
                        int deg = minor[(st / 4 + bar * 2) % minor.Length];
                        AddPluck(L, R, t0, Midi(root + 24 + off + deg), stepLen * 4f, 0.12f);
                    }
                }
            }
            var data = new float[n * 2];
            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Max(Mathf.Abs(L[i]), Mathf.Abs(R[i])));
            float g = peak > 0.001f ? 0.85f / peak : 1f;
            for (int i = 0; i < n; i++)
            {
                // soft clip for glue
                data[i * 2] = (float)Math.Tanh(L[i] * g * 1.2f);
                data[i * 2 + 1] = (float)Math.Tanh(R[i] * g * 1.2f);
            }
            return data;
        }

        private static void Mix(float[] L, float[] R, int idx, float v, float pan = 0f)
        {
            if (idx < 0 || idx >= L.Length) return;
            L[idx] += v * (1f - Mathf.Max(0f, pan)); R[idx] += v * (1f + Mathf.Min(0f, pan));
        }

        private static void AddKick(float[] L, float[] R, int t0, float vol)
        {
            int len = (int)(MusicRate * 0.28f);
            float ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)MusicRate;
                float f = 48f + 120f * Mathf.Exp(-t * 28f);
                ph += 2f * Mathf.PI * f / MusicRate;
                Mix(L, R, t0 + i, Mathf.Sin(ph) * Mathf.Exp(-t * 9f) * vol);
            }
        }

        private static void AddSnare(float[] L, float[] R, int t0, float vol, System.Random rnd)
        {
            int len = (int)(MusicRate * 0.22f);
            float y = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)MusicRate;
                float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                y += 0.6f * (nz - y);
                float body = Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 30f);
                Mix(L, R, t0 + i, (y * Mathf.Exp(-t * 16f) * 0.9f + body * 0.4f) * vol);
            }
        }

        private static void AddHat(float[] L, float[] R, int t0, float vol, System.Random rnd)
        {
            int len = (int)(MusicRate * 0.05f);
            float prev = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)MusicRate;
                float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float hp = nz - prev; prev = nz;
                Mix(L, R, t0 + i, hp * Mathf.Exp(-t * 90f) * vol, (rnd.Next(3) - 1) * 0.2f);
            }
        }

        private static void AddBass(float[] L, float[] R, int t0, float f, float dur, float vol)
        {
            int len = (int)(MusicRate * dur);
            float ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)MusicRate;
                float fr = f * (1f + 0.5f * Mathf.Exp(-t * 20f));
                ph += 2f * Mathf.PI * fr / MusicRate;
                float env = Mathf.Clamp01(t * 200f) * Mathf.Exp(-t * 2.2f);
                float s = Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 2f);
                Mix(L, R, t0 + i, (float)Math.Tanh(s * 1.8f) * env * vol);
            }
        }

        private static void AddCowbell(float[] L, float[] R, int t0, float f, float vol)
        {
            int len = (int)(MusicRate * 0.3f);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)MusicRate;
                float a = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * 1.504f * t));
                Mix(L, R, t0 + i, a * 0.5f * Mathf.Exp(-t * 11f) * vol * Mathf.Clamp01(t * 500f), 0.25f);
            }
        }

        private static void AddPluck(float[] L, float[] R, int t0, float f, float dur, float vol)
        {
            int len = (int)(MusicRate * dur * 1.6f);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)MusicRate;
                float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 3f * t);
                Mix(L, R, t0 + i, s * Mathf.Exp(-t * 5f) * vol * Mathf.Clamp01(t * 300f), -0.3f);
            }
        }

        private static void AddPad(float[] L, float[] R, int t0, float f, float dur, float vol)
        {
            int len = (int)(MusicRate * dur);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)MusicRate;
                float env = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((dur - t) * 3f);
                float s = Mathf.Sin(2f * Mathf.PI * f * t) + Mathf.Sin(2f * Mathf.PI * f * 1.189f * t) + Mathf.Sin(2f * Mathf.PI * f * 1.498f * t);
                float w = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 0.3f * t);
                Mix(L, R, t0 + i, s * 0.33f * env * vol * w);
            }
        }
    }
}
