using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    public class LevelCar { public float x, z, h; public string key; }
    public class LevelTarget { public float x, z, h, w, l; }

    public class Level
    {
        public int index, par, reward;
        public string name, type, desc;
        public float k;
        public Vector3 start; // x, z, курс
        public LevelTarget target;
        public readonly List<Vector2> cones = new List<Vector2>();
        public readonly List<LevelCar> cars = new List<LevelCar>();
        public readonly List<Vector4> walls = new List<Vector4>(); // x0, z0, x1, z1
        public readonly List<float> wallH = new List<float>();
        public readonly List<Vector4> lines = new List<Vector4>();
    }

    /// <summary>30 парковочных уровней на автодроме ДОСААФ (порт levels.js, те же сиды и раскладки).</summary>
    public static class Levels
    {
        const float PI = Mathf.PI;
        static readonly string[] Classic = { "vaz2101", "vaz2106", "vaz2107", "vaz2109", "niva", "oka", "priora", "granta", "vesta", "largus" };

        class B
        {
            public readonly Rng rnd;
            public readonly Level L = new Level();
            public B(int seed) { rnd = new Rng(seed); }
            public void Cone(float x, float z) { L.cones.Add(new Vector2(x, z)); }
            public void ConeLine(float x0, float z0, float x1, float z1, float step = 1.6f)
            {
                float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
                int n = Mathf.Max(1, Mathf.RoundToInt(len / step));
                for (int i = 0; i <= n; i++) Cone(x0 + (x1 - x0) * i / n, z0 + (z1 - z0) * i / n);
            }
            public void Car(float x, float z, float h, string key = null) { L.cars.Add(new LevelCar { x = x, z = z, h = h, key = key ?? Classic[(int)(rnd.Next() * Classic.Length)] }); }
            public void Wall(float x0, float z0, float x1, float z1, float h = 2.4f)
            {
                L.walls.Add(new Vector4(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1))); L.wallH.Add(h);
            }
            public void Line(float x0, float z0, float x1, float z1) { L.lines.Add(new Vector4(x0, z0, x1, z1)); }
            public void Stall(float cx, float cz, float h, float w, float l, bool open = true)
            {
                float s = Mathf.Sin(h), c = Mathf.Cos(h);
                System.Func<float, float, Vector2> P = (lx, lz) => new Vector2(cx + lx * c + lz * s, cz - lx * s + lz * c);
                var a = P(w / 2, -l / 2); var b = P(w / 2, l / 2); var d = P(-w / 2, l / 2); var e = P(-w / 2, -l / 2);
                Line(a.x, a.y, b.x, b.y); Line(e.x, e.y, d.x, d.y); Line(b.x, b.y, d.x, d.y);
                if (!open) Line(a.x, a.y, e.x, e.y);
            }
        }

        static Level Perpendicular(float k, bool reverse, int seed)
        {
            var b = new B(seed);
            float W = 2.95f - 0.4f * k, L = 5.4f, row = 9.5f, aisle = 11f - 4f * k;
            int n = 7, ti = 2 + (int)(b.rnd.Next() * 3);
            float x0 = -((n - 1) / 2f) * W;
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * W;
                b.Stall(x, row, 0, W, L);
                if (i != ti) b.Car(x, row + (b.rnd.Next() - 0.5f) * 0.3f, b.rnd.Next() < 0.5f ? 0 : PI);
            }
            b.ConeLine(x0 - W, row + L / 2 + 0.8f, x0 + n * W, row + L / 2 + 0.8f, 2.2f);
            float zA = row - L / 2 - aisle;
            for (int i = 0; i < n; i++) if (b.rnd.Next() < 0.85f) b.Car(x0 + i * W + (b.rnd.Next() - 0.5f) * 0.4f, zA - 2.8f, b.rnd.Next() < 0.5f ? 0 : PI);
            float tx = x0 + ti * W;
            var l = b.L;
            l.type = reverse ? "Задом в бокс" : "Передом в бокс";
            l.desc = reverse ? "Заедьте задним ходом на свободное место" : "Поставьте машину на свободное место";
            l.start = new Vector3(x0 - W * 2.5f, row - L / 2 - aisle / 2, PI / 2);
            l.target = new LevelTarget { x = tx, z = row, h = reverse ? PI : 0, w = W, l = L };
            return l;
        }

        static Level Parallel(float k, int seed, bool police = false)
        {
            var b = new B(seed);
            float gap = 7.6f - 1.5f * k, lane = 3.6f;
            b.ConeLine(-34, lane + 1.6f, 34, lane + 1.6f, 1.8f);
            b.ConeLine(-34, -5.5f + k, 34, -5.5f + k, 2.4f);
            string key = police ? "police" : null;
            b.Car(-gap / 2 - 2.35f, lane, PI / 2, key);
            b.Car(gap / 2 + 2.35f, lane, PI / 2, key);
            b.Car(-gap / 2 - 7.3f, lane, PI / 2);
            b.Car(gap / 2 + 7.3f, lane, PI / 2);
            b.Line(-gap / 2, lane - 1.2f, gap / 2, lane - 1.2f);
            var l = b.L;
            l.type = police ? "Между двух ДПС" : "Параллельная";
            l.desc = police ? "Встаньте между патрульными машинами. Не задень — лишат прав!" : "Параллельная парковка у бордюра";
            l.start = new Vector3(-gap / 2 - 12, 0, PI / 2);
            l.target = new LevelTarget { x = 0, z = lane, h = PI / 2, w = 2.2f, l = gap - 0.5f };
            return l;
        }

        static Level Garage(float k, bool reverse, int seed)
        {
            var b = new B(seed);
            float iw = 3.25f - 0.45f * k, depth = 6.4f, z0 = 5f, t = 0.2f;
            b.Wall(-iw / 2 - t, z0, -iw / 2, z0 + depth);
            b.Wall(iw / 2, z0, iw / 2 + t, z0 + depth);
            b.Wall(-iw / 2 - t, z0 + depth, iw / 2 + t, z0 + depth + t, 2.6f);
            b.Wall(-iw / 2 - t, z0 + depth - 0.1f, iw / 2 + t, z0 + depth + t, 2.6f);
            foreach (int s in new[] { -1, 1 })
            {
                b.Wall(s * (iw / 2 + t + iw), z0, s * (iw / 2 + t + iw) + s * t, z0 + depth);
                b.Car(s * (iw + t), z0 + depth / 2, b.rnd.Next() < 0.5f ? 0 : PI);
            }
            b.ConeLine(-14, z0 - 9 + 2 * k, 14, z0 - 9 + 2 * k, 2);
            var l = b.L;
            l.type = reverse ? "Гараж задом" : "Гараж";
            l.desc = reverse ? "Загоните машину в гараж задним ходом" : "Заедьте в гараж, не задев стены";
            l.start = new Vector3(-12, z0 - 5 + k, PI / 2);
            l.target = new LevelTarget { x = 0, z = z0 + depth / 2 - 0.1f, h = reverse ? PI : 0, w = iw - 0.1f, l = depth - 0.3f };
            return l;
        }

        static Level Slalom(float k, int seed)
        {
            var b = new B(seed);
            float step = 8 - 2 * k, half = 5 - k;
            for (float x = -30; x <= 22; x += step) b.Cone(x, 0);
            b.ConeLine(-44, half + 1, 40, half + 1, 2.2f);
            b.ConeLine(-44, -half - 1, 40, -half - 1, 2.2f);
            float W = 3.0f - 0.3f * k, L = 5.6f, tx = 34;
            b.ConeLine(tx - L / 2, W / 2 + 0.3f, tx + L / 2, W / 2 + 0.3f, 1.2f);
            b.ConeLine(tx - L / 2, -W / 2 - 0.3f, tx + L / 2, -W / 2 - 0.3f, 1.2f);
            b.ConeLine(tx + L / 2 + 0.4f, -W / 2, tx + L / 2 + 0.4f, W / 2, 1.1f);
            var l = b.L;
            l.type = "Змейка"; l.desc = "Проедьте змейкой между конусами и встаньте в бокс";
            l.start = new Vector3(-42, 0, PI / 2);
            l.target = new LevelTarget { x = tx, z = 0, h = PI / 2, w = W, l = L };
            return l;
        }

        static Level Angled(float k, bool reverse, int seed)
        {
            var b = new B(seed);
            float hs = PI / 4, W = 2.9f - 0.35f * k, L = 5.4f, row = 9;
            float step = W / Mathf.Sin(hs);
            int n = 7, ti = 2 + (int)(b.rnd.Next() * 3);
            float x0 = -((n - 1) / 2f) * step;
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * step;
                b.Stall(x, row, hs, W, L);
                if (i != ti) b.Car(x, row, b.rnd.Next() < 0.8f ? hs : hs + PI);
            }
            b.ConeLine(x0 - step * 1.5f, row + 5, x0 + n * step, row + 5, 2.2f);
            b.ConeLine(x0 - step * 2, row - 12 + 3 * k, x0 + n * step, row - 12 + 3 * k, 2.2f);
            var l = b.L;
            l.type = "Ёлочка 45°"; l.desc = "Парковка под углом — по стрелке";
            l.start = new Vector3(x0 - step * 2.5f, row - 7 + 1.5f * k, PI / 2);
            l.target = new LevelTarget { x = x0 + ti * step, z = row, h = reverse ? hs + PI : hs, w = W, l = L };
            return l;
        }

        static int TurnSign(Vector2[] pts, int i)
        {
            var a = pts[i - 1]; var b = pts[i]; var c = pts[i + 1];
            float d1x = Mathf.Sign(b.x - a.x) * (b.x != a.x ? 1 : 0), d1z = Mathf.Sign(b.y - a.y) * (b.y != a.y ? 1 : 0);
            float d2x = Mathf.Sign(c.x - b.x) * (c.x != b.x ? 1 : 0), d2z = Mathf.Sign(c.y - b.y) * (c.y != b.y ? 1 : 0);
            return d1x * d2z - d1z * d2x > 0 ? 1 : -1;
        }

        static Level Maze(float k, int seed)
        {
            var b = new B(seed);
            float w = 5.2f - 1.2f * k;
            Vector2[] pts = { new Vector2(-44, -20), new Vector2(-12, -20), new Vector2(-12, 18), new Vector2(22, 18), new Vector2(22, -12) };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var A = pts[i]; var Bp = pts[i + 1];
                float dx = Bp.x != A.x ? Mathf.Sign(Bp.x - A.x) : 0, dz = Bp.y != A.y ? Mathf.Sign(Bp.y - A.y) : 0;
                float nx = -dz, nz = dx;
                foreach (int s in new[] { 1, -1 })
                {
                    float ox = nx * s * (w / 2 + 0.3f), oz = nz * s * (w / 2 + 0.3f);
                    float e0 = i == 0 ? 0 : s * TurnSign(pts, i) * (w / 2 + 0.3f);
                    float e1 = i == pts.Length - 2 ? 0 : s * TurnSign(pts, i + 1) * (w / 2 + 0.3f);
                    b.ConeLine(A.x + ox - dx * e0, A.y + oz - dz * e0, Bp.x + ox + dx * e1, Bp.y + oz + dz * e1, 1.7f);
                }
            }
            var E = pts[pts.Length - 1];
            b.ConeLine(E.x - w / 2, E.y - 6.4f, E.x + w / 2, E.y - 6.4f, 1.2f);
            var l = b.L;
            l.type = "Лабиринт"; l.desc = "Проедьте коридор из конусов и остановитесь в зоне";
            l.start = new Vector3(-40, -20, PI / 2);
            l.target = new LevelTarget { x = E.x, z = E.y - 2.6f, h = PI, w = w - 0.3f, l = 5.2f };
            return l;
        }

        static Level UTurnLevel(float k, int seed)
        {
            var b = new B(seed);
            float w = 9.5f - 2 * k;
            b.ConeLine(-36, w / 2, 20, w / 2, 1.7f);
            b.ConeLine(-36, -w / 2, 20, -w / 2, 1.7f);
            b.ConeLine(20, -w / 2, 20, w / 2, 1.2f);
            b.Car(-6, w / 2 - 1.2f, PI / 2);
            var l = b.L;
            l.type = "Разворот"; l.desc = "Развернитесь в тупике (в 3 приёма) и встаньте в зону";
            l.start = new Vector3(-30, -w / 4, PI / 2);
            l.target = new LevelTarget { x = -26, z = w / 4, h = -PI / 2, w = 2.8f, l = 5.4f };
            return l;
        }

        static readonly object[][] Plan = {
            new object[] { "perp", 0f, false }, new object[] { "garage", 0f, false }, new object[] { "parallel", 0f, false }, new object[] { "perp", 0.2f, true }, new object[] { "slalom", 0f, false },
            new object[] { "angled", 0f, false }, new object[] { "garage", 0.2f, true }, new object[] { "parallel", 0.25f, false }, new object[] { "maze", 0f, false }, new object[] { "uturn", 0f, false },
            new object[] { "perp", 0.4f, false }, new object[] { "police", 0.3f, false }, new object[] { "angled", 0.4f, false }, new object[] { "garage", 0.4f, true }, new object[] { "slalom", 0.4f, false },
            new object[] { "perp", 0.5f, true }, new object[] { "maze", 0.4f, false }, new object[] { "parallel", 0.5f, false }, new object[] { "uturn", 0.4f, false }, new object[] { "angled", 0.6f, true },
            new object[] { "perp", 0.7f, true }, new object[] { "garage", 0.65f, false }, new object[] { "police", 0.6f, false }, new object[] { "maze", 0.7f, false }, new object[] { "slalom", 0.7f, false },
            new object[] { "parallel", 0.75f, false }, new object[] { "uturn", 0.7f, false }, new object[] { "garage", 0.85f, true }, new object[] { "perp", 0.9f, true }, new object[] { "police", 0.9f, false },
        };

        public static int Count => Plan.Length;

        public static Level Get(int i)
        {
            var p = Plan[i];
            string t = (string)p[0]; float k = (float)p[1]; bool rev = (bool)p[2];
            int seed = 1000 + i * 17;
            Level L;
            switch (t)
            {
                case "perp": L = Perpendicular(k, rev, seed); break;
                case "garage": L = Garage(k, rev, seed); break;
                case "parallel": L = Parallel(k, seed); break;
                case "police": L = Parallel(k, seed, true); break;
                case "slalom": L = Slalom(k, seed); break;
                case "angled": L = Angled(k, rev, seed); break;
                case "maze": L = Maze(k, seed); break;
                default: L = UTurnLevel(k, seed); break;
            }
            L.index = i;
            L.name = "Уровень " + (i + 1);
            L.k = k;
            L.par = Mathf.RoundToInt(28 + k * 22 + (t == "maze" || t == "slalom" ? 15 : 0) + (t == "uturn" ? 10 : 0));
            L.reward = 300 + i * 90;
            return L;
        }
    }
}
