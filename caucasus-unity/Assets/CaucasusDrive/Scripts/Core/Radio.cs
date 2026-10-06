using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Магнитола (порт веб-версии): три процедурные станции, музыка синтезируется на лету (0 КБ в APK).
    ///  - «Кавказ FM»: 6/8 в духе лезгинки — «дум»/«тек», «гармошка» по гармоническому минору, бас по долям;
    ///  - «Ретро ВАЗ»: синти-поп 80-х — бочка/малый, бас восьмыми, арпеджио;
    ///  - «Дорожное»: спокойные аккорды электропиано и мягкий ритм.
    /// Мелодии сочиняются из гаммы (фраза AABB). Пешком — глуше и тише с расстоянием.
    /// </summary>
    public class Radio
    {
        public static readonly string[] Names = { "Радио выкл", "Кавказ FM 101.7", "Ретро ВАЗ 88.3", "Дорожное 104.5" };
        static readonly float[] Bpm = { 0, 138, 118, 84 };
        static readonly int[] Steps = { 0, 6, 16, 8 };
        static readonly int[] Sub = { 1, 2, 4, 2 };
        readonly GameAudio a;
        readonly Rng rnd = new Rng(2024);
        public int index;
        int step, bar, blk = -1;
        double next;
        int[] phrase;

        public Radio(GameAudio audio) { a = audio; }

        public string Name => Names[index];

        public void Set(int i)
        {
            index = ((i % Names.Length) + Names.Length) % Names.Length;
            step = 0; bar = 0; phrase = null; blk = -1;
            next = a.Now + 0.1;
        }

        public string Cycle() { Set(index + 1); return Name; }

        static float Hz(float m) { return 440f * Mathf.Pow(2f, (m - 69f) / 12f); }

        /// <summary>Каждый кадр: ноты на 0,25 с вперёд. mix — громкость (0 — выкл/далеко).</summary>
        public void Update(float mix, bool muffled)
        {
            bool on = index > 0;
            a.SetRadio(on ? 0.65f * mix : 0f, muffled);
            double t = a.Now;
            if (!on || mix <= 0.01f) { next = t + 0.05; return; }
            float sd = 60f / Bpm[index] / Sub[index];
            if (next < t) next = t + 0.02;
            while (next < t + 0.25)
            {
                double d = next;
                if (index == 1) Caucasus(d, sd); else if (index == 2) Retro(d, sd); else Road(d, sd);
                next += sd;
                step = (step + 1) % Steps[index];
                if (step == 0) bar++;
            }
        }

        // ------------------------------------------------------------------ инструменты
        void Tone(double d, float f, float dur, Wave w, float vol, float cutoff, float vib = 0f)
        {
            a.Play(new Voice { wave = w, f0 = f, dur = dur + 0.01f, vol = vol, attack = 0.01f, filter = Flt.Low, cutoff = Mathf.Min(cutoff, a.RadioCut), vib = vib, bus = 1, at = d });
        }

        void Drum(double d, float f0, float f1, float dur, float vol)
        {
            a.Play(new Voice { wave = Wave.Sine, f0 = f0, f1 = f1, dur = dur, vol = vol, attack = 0.003f, bus = 1, at = d });
        }

        void Noise(double d, float dur, float freq, float q, float vol, Flt type = Flt.Band)
        {
            if (type == Flt.High && a.RadioCut < 1000f) return; // через стекло верхов не слышно
            a.Play(new Voice { wave = Wave.Noise, filter = type, cutoff = type == Flt.Band ? Mathf.Min(freq, a.RadioCut) : freq, q = q, dur = dur, vol = vol, attack = 0.002f, bus = 1, at = d });
        }

        /// <summary>Фраза из гаммы: случайное блуждание с притяжением к тонике.</summary>
        int[] MakePhrase(int[] scale, int len, float density)
        {
            var notes = new int[len];
            int i = 7;
            for (int k = 0; k < len; k++)
            {
                if (rnd.Next() > density) { notes[k] = 0; continue; }
                i += Mathf.RoundToInt((rnd.Next() - 0.5f) * 4f);
                if (k % 6 == 0 && rnd.Next() < 0.4f) i = 7 + (rnd.Next() < 0.5f ? 0 : 4);
                i = Mathf.Clamp(i, 0, scale.Length - 1);
                notes[k] = scale[i];
            }
            return notes;
        }

        static readonly int[] CaucScale = { 57, 59, 60, 62, 64, 65, 68, 69, 71, 72, 74, 76, 77, 80, 81 };
        static readonly int[] RetroScale = { 57, 60, 62, 64, 67, 69, 72, 74, 76, 79, 81 };

        void Caucasus(double t, float sd)
        {
            if (step == 0 || step == 3) Drum(t, 120, 55, 0.18f, 0.55f);
            if (step == 2 || step == 4 || step == 5) Noise(t, 0.05f, 3200, 1.2f, 0.18f);
            if (step == 1 && bar % 2 == 1) Noise(t, 0.04f, 5000, 2, 0.08f);
            int[] roots = { 45, 45, 50, 52 };
            if (step == 0 || step == 3) Tone(t, Hz(roots[(bar >> 1) % 4] - 12 + (step == 3 ? 7 : 0)), sd * 2.6f, Wave.Tri, 0.32f, 600);
            int b = bar / 8;
            if (phrase == null || blk != b) { blk = b; phrase = MakePhrase(CaucScale, 24, 0.85f); }
            int n = phrase[(bar % 4) * 6 + step];
            if (n > 0) { Tone(t, Hz(n), sd * 0.95f, Wave.Saw, 0.11f, 2600, 0.006f); Tone(t, Hz(n + 12), sd * 0.9f, Wave.Square, 0.035f, 3000); }
        }

        void Retro(double t, float sd)
        {
            if (step == 0 || step == 8 || (step == 10 && bar % 2 == 1)) Drum(t, 150, 45, 0.22f, 0.6f);
            if (step == 4 || step == 12) Noise(t, 0.14f, 1800, 0.8f, 0.22f);
            if (step % 2 == 0) Noise(t, 0.03f, 8000, 1, 0.06f, Flt.High);
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } };
            var ch = chords[bar % 4];
            if (step % 2 == 0) Tone(t, Hz(ch[0] - 24 + (step % 4 == 2 ? 12 : 0)), sd * 1.8f, Wave.Saw, 0.16f, 700);
            if (step == 0) foreach (var m in ch) Tone(t, Hz(m), sd * 15f, Wave.Tri, 0.035f, 1500);
            int[] arp = { 0, 1, 2, 1 };
            if (bar % 8 >= 4) Tone(t, Hz(ch[arp[step % 4]] + 12), sd * 0.8f, Wave.Square, 0.05f, 2400);
            else if (step % 4 == 0)
            {
                if (phrase == null || blk != bar >> 3) { blk = bar >> 3; phrase = MakePhrase(RetroScale, 16, 0.7f); }
                int n = phrase[(bar % 4) * 4 + step / 4];
                if (n > 0) Tone(t, Hz(n + 12), sd * 3.5f, Wave.Square, 0.06f, 2800, 0.004f);
            }
        }

        void Road(double t, float sd)
        {
            if (step == 0 || step == 5) Drum(t, 110, 50, 0.25f, 0.4f);
            if (step == 4) Noise(t, 0.12f, 2200, 0.7f, 0.12f);
            if (step % 2 == 1) Noise(t, 0.025f, 7000, 1, 0.035f, Flt.High);
            int[][] chords = { new[] { 50, 53, 57, 60 }, new[] { 55, 59, 62, 65 }, new[] { 48, 52, 55, 59 }, new[] { 45, 48, 52, 55 } };
            var ch = chords[bar % 4];
            if (step == 0) foreach (var m in ch) Tone(t, Hz(m + 12), sd * 7f, Wave.Sine, 0.07f, 1800);
            if (step == 0 || step == 6) Tone(t, Hz(ch[0] - 12), sd * 3f, Wave.Tri, 0.25f, 500);
            if (step == 3 && bar % 2 == 1) Tone(t, Hz(ch[2 + ((bar >> 1) % 2)] + 24), sd * 2.5f, Wave.Sine, 0.06f, 3000);
        }
    }
}
