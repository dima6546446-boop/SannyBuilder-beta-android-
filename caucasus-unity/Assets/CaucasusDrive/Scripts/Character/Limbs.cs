using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Геометрия кроссовка и кисти руки (чистые функции без привязки к сцене — проверяются отдельно).
    /// Кроссовок: плавный профиль по сечениям (задник, ворот, шнуровка, носок с подъёмом), подошва, шнурки, язычок.
    /// Кисть: ладонь, четыре пальца по три фаланги с лёгким изгибом и большой палец по две.
    /// </summary>
    /// <summary>Чистая математика (без нативных вызовов Unity) — геометрию можно проверять вне редактора.</summary>
    public static class G
    {
        public static Quaternion AngleAxis(float deg, Vector3 axis)
        {
            axis = axis.normalized; float h = deg * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }
        /// <summary>Порядок как в Unity: Z, затем X, затем Y.</summary>
        public static Quaternion Euler(float x, float y, float z) { return AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward); }
        public static Quaternion FromTo(Vector3 a, Vector3 b)
        {
            a = a.normalized; b = b.normalized;
            float d = Vector3.Dot(a, b);
            if (d > 0.99999f) return new Quaternion(0, 0, 0, 1);
            if (d < -0.99999f) { var ax = Vector3.Cross(a, Mathf.Abs(a.x) < 0.9f ? Vector3.right : Vector3.up); return AngleAxis(180f, ax); }
            var c = Vector3.Cross(a, b);
            var q = new Quaternion(c.x, c.y, c.z, 1f + d);
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
        public static Matrix4x4 TRS(Vector3 p, Quaternion q)
        {
            var m = new Matrix4x4();
            float x = q.x, y = q.y, z = q.z, w = q.w;
            m.m00 = 1 - 2 * (y * y + z * z); m.m01 = 2 * (x * y - z * w); m.m02 = 2 * (x * z + y * w); m.m03 = p.x;
            m.m10 = 2 * (x * y + z * w); m.m11 = 1 - 2 * (x * x + z * z); m.m12 = 2 * (y * z - x * w); m.m13 = p.y;
            m.m20 = 2 * (x * z - y * w); m.m21 = 2 * (y * z + x * w); m.m22 = 1 - 2 * (x * x + y * y); m.m23 = p.z;
            m.m30 = 0; m.m31 = 0; m.m32 = 0; m.m33 = 1;
            return m;
        }
        public static readonly Matrix4x4 Identity = TRS(Vector3.zero, new Quaternion(0, 0, 0, 1));
    }

    public static class Limbs
    {
        static float SP(float c, float p) { return Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / p); }

        /// <summary>Контур сечения: скруглённый прямоугольник (суперэллипс) шириной 2·hw между y = bot…top, центр по x = cx.</summary>
        static Vector3[] Ring(float z, float cx, float hw, float bot, float top, int m = 16, float p = 2.8f)
        {
            var r = new Vector3[m];
            float mid = (top + bot) * 0.5f, hh = (top - bot) * 0.5f;
            for (int k = 0; k < m; k++)
            {
                float a = k * Mathf.PI * 2f / m;
                r[k] = new Vector3(cx + hw * SP(Mathf.Cos(a), p), mid + hh * SP(Mathf.Sin(a), p), z);
            }
            return r;
        }

        static float Lerp(float[] zs, float[] vs, float z)
        {
            if (z <= zs[0]) return vs[0];
            for (int i = 1; i < zs.Length; i++) if (z <= zs[i]) return Mathf.Lerp(vs[i - 1], vs[i], (z - zs[i - 1]) / (zs[i] - zs[i - 1]));
            return vs[vs.Length - 1];
        }

        /// <summary>Кроссовок. Начало координат — центр пятки на полу, +Z — к носку, +X — вбок, +Y — вверх. len ≈ 0.29, width ≈ 0.11 (внешняя), ankle — высота ворота ≈ 0.1.</summary>
        public static PaletteMesh Shoe(float len, float width, float ankle)
        {
            var pm = new PaletteMesh();
            float k = len / 0.29f, hwK = width / 0.112f, yK = ankle / 0.1f;
            // станции верха: z, полуширина, верх, низ (всегда верх > низ)
            float[] zs = { 0.000f, 0.012f, 0.040f, 0.090f, 0.140f, 0.190f, 0.235f, 0.268f, 0.286f, 0.290f };
            float[] hw = { 0.026f, 0.040f, 0.048f, 0.051f, 0.052f, 0.055f, 0.054f, 0.046f, 0.030f, 0.016f };
            float[] top = { 0.060f, 0.084f, 0.098f, 0.094f, 0.080f, 0.066f, 0.054f, 0.044f, 0.038f, 0.034f };
            float[] bot = { 0.022f, 0.022f, 0.024f, 0.026f, 0.026f, 0.026f, 0.028f, 0.030f, 0.032f, 0.033f };
            var up = new List<Vector3[]>();
            for (int i = 0; i < zs.Length; i++) up.Add(Ring(zs[i] * k, 0, hw[i] * hwK, bot[i] * yK, top[i] * yK, 18, 2.6f));
            pm.Loft(up, 0xf6f6f6);
            // подошва: белая мидсоула + тёмный протектор снизу, чуть шире верха, носок приподнят
            float[] szs = { 0f, 0.006f, 0.05f, 0.15f, 0.22f, 0.265f, 0.285f, 0.29f };
            float[] shw = { 0.026f, 0.042f, 0.054f, 0.057f, 0.059f, 0.051f, 0.031f, 0.014f };
            float[] sbot = { 0.004f, 0f, 0f, 0f, 0.002f, 0.010f, 0.020f, 0.024f };
            float[] stop = { 0.036f, 0.034f, 0.034f, 0.034f, 0.036f, 0.040f, 0.042f, 0.042f };
            var mid = new List<Vector3[]>(); var outs = new List<Vector3[]>();
            for (int i = 0; i < szs.Length; i++)
            {
                mid.Add(Ring(szs[i] * k, 0, shw[i] * hwK, sbot[i] + 0.011f, stop[i] * yK * 0.97f, 16, 3.2f));
                outs.Add(Ring(szs[i] * k, 0, (shw[i] - 0.001f) * hwK, sbot[i], sbot[i] + 0.0135f, 16, 3.6f));
            }
            pm.Loft(mid, 0xf0f0f0);
            pm.Loft(outs, 0x4a4c53);
            // пятка: тёмная накладка на заднике
            pm.xf = G.TRS(new Vector3(0, 0.062f * yK, 0.002f), G.Euler(-10, 0, 0));
            pm.RBox(Vector3.zero, new Vector3(0.034f * hwK, 0.036f * yK, 0.01f), 0.004f, 0x23252d);
            // отверстие для ноги: мягкий белый рант и тёмная внутренность
            pm.xf = G.TRS(new Vector3(0, 0.0945f * yK, 0.058f * k), G.Euler(-4, 0, 0));
            pm.Sphere(Vector3.zero, 1f, 0xfafafa, 14, 4, new Vector3(0.043f * hwK, 0.008f, 0.062f * k), true);
            pm.xf = G.TRS(new Vector3(0, 0.0985f * yK, 0.060f * k), G.Euler(-4, 0, 0));
            pm.Sphere(Vector3.zero, 1f, 0x1d1f25, 14, 4, new Vector3(0.033f * hwK, 0.007f, 0.05f * k), true);
            // язычок
            pm.xf = G.TRS(new Vector3(0, 0.087f * yK, 0.118f * k), G.Euler(-28, 0, 0));
            pm.RBox(Vector3.zero, new Vector3(0.042f * hwK, 0.009f, 0.058f * k), 0.0035f, 0xfafafa);
            // шнурки: поперечные полоски по верху между z = 0.11 и 0.22
            for (int i = 0; i < 5; i++)
            {
                float z = (0.12f + i * 0.026f) * k;
                float y = Lerp(zs, top, z / k) * yK + 0.001f;
                pm.xf = G.TRS(new Vector3(0, y, z), G.Euler(-Mathf.Rad2Deg * Mathf.Atan2(Lerp(zs, top, z / k + 0.01f) * yK - Lerp(zs, top, z / k - 0.01f) * yK, 0.02f), 0, 0));
                pm.RBox(Vector3.zero, new Vector3(0.05f * hwK, 0.006f, 0.008f), 0.002f, 0x2a2d36);
            }
            // боковые полоски (по три на каждой стороне) и носок с резиновым мыском
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 3; i++)
                {
                    pm.xf = G.TRS(new Vector3(s * (0.0545f * hwK), (0.052f - i * 0.009f) * yK, (0.13f + i * 0.004f) * k), G.Euler(0, 0, 0));
                    pm.RBox(Vector3.zero, new Vector3(0.003f, 0.0045f, 0.09f * k), 0.0015f, 0x2a2d36);
                }
            pm.xf = G.TRS(new Vector3(0, 0.03f * yK + 0.006f, 0.268f * k), new Quaternion(0, 0, 0, 1));
            pm.Sphere(Vector3.zero, 0.04f * hwK, 0xececec, 10, 6, new Vector3(1f, 0.5f, 0.75f), true);
            pm.xf = G.Identity;
            return pm;
        }

        /// <summary>
        /// Кисть. Начало — запястье, +Z — вдоль пальцев, +Y — тыльная сторона, +X — вбок. thumbSide = ±1 — на какой стороне большой палец.
        /// scale 1 ≈ взрослая рука: ладонь 0.1, ширина 0.085, средний палец 0.08.
        /// </summary>
        public static PaletteMesh Hand(int skin, float thumbSide, float scale = 1f)
        {
            var pm = new PaletteMesh();
            float pl = 0.10f * scale, pw = 0.085f * scale, fl = 0.082f * scale;
            // ладонь: слегка сужается к запястью
            var rings = new List<Vector3[]>();
            float[] zs = { -0.02f, 0.0f, 0.04f, 0.08f, pl };
            float[] hws = { 0.026f, 0.032f, 0.040f, 0.0425f, 0.043f };
            float[] ths = { 0.026f, 0.028f, 0.032f, 0.030f, 0.026f };
            for (int i = 0; i < zs.Length; i++) rings.Add(Ring(zs[i] * scale, 0, hws[i] * scale, -ths[i] * scale * 0.55f, ths[i] * scale * 0.55f, 12, 2.4f));
            pm.Loft(rings, skin);
            // пальцы: индекс ближе к большому
            float[] len = { 0.92f, 1f, 0.93f, 0.76f };
            float[] rad = { 0.0098f, 0.0102f, 0.0096f, 0.0086f };
            float[] curl = { 16f, 12f, 16f, 22f };
            for (int f = 0; f < 4; f++)
            {
                float x = thumbSide * (pw * 0.5f - 0.0105f * scale - f * (pw - 0.021f * scale) / 3f);
                var p = new Vector3(x, 0.0015f * scale, pl - 0.004f * scale);
                float ang = curl[f];
                float[] seg = { 0.42f, 0.33f, 0.25f };
                for (int sgm = 0; sgm < 3; sgm++)
                {
                    float L = fl * len[f] * seg[sgm];
                    var d = G.AngleAxis(ang, Vector3.right) * Vector3.forward;
                    float r = rad[f] * scale * (1f - sgm * 0.1f);
                    pm.xf = G.TRS(p + d * (L * 0.5f), G.FromTo(Vector3.up, d));
                    pm.Capsule(Vector3.zero, r, L, skin, 6);
                    p += d * L;
                    ang += 20f + sgm * 8f;
                }
            }
            // большой палец: от основания ладони вперёд-вбок, две фаланги
            {
                var p = new Vector3(thumbSide * (pw * 0.5f - 0.003f * scale), -0.004f * scale, pl * 0.30f);
                var d0 = new Vector3(thumbSide * 0.55f, -0.22f, 0.8f).normalized;
                float[] tl = { 0.40f * fl, 0.34f * fl };
                for (int sgm = 0; sgm < 2; sgm++)
                {
                    var d = (d0 + new Vector3(-thumbSide * 0.18f * sgm, -0.12f * sgm, 0.15f * sgm)).normalized;
                    pm.xf = G.TRS(p + d * (tl[sgm] * 0.5f), G.FromTo(Vector3.up, d));
                    pm.Capsule(Vector3.zero, 0.0112f * scale * (1f - sgm * 0.1f), tl[sgm], skin, 6);
                    p += d * tl[sgm];
                }
            }
            pm.xf = G.Identity;
            return pm;
        }
    }
}
