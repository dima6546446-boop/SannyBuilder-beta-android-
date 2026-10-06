using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Текстура щитка приборов (аналог canvas-рисования веб-версии): ободки, шкалы с рисками, красная зона,
    /// цифры и подписи (встроенный пиксельный шрифт 5×7), одометр, контрольные лампы. Сглаживание — по расстоянию.
    /// </summary>
    public static class ClusterTexture
    {
        const float PX = 1100f;   // пикселей на метр
        static Color32[] px; static int W, H;

        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]> {
            { '0', new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" } },
            { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
            { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
            { '3', new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" } },
            { '4', new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
            { '5', new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" } },
            { '6', new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" } },
            { '7', new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" } },
            { '8', new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" } },
            { '9', new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" } },
            { 'x', new[] { "00000", "00000", "10001", "01010", "00100", "01010", "10001" } },
            { '/', new[] { "00001", "00010", "00010", "00100", "01000", "01000", "10000" } },
            { 'к', new[] { "00000", "00000", "10010", "10100", "11000", "10100", "10010" } },
            { 'м', new[] { "00000", "00000", "10001", "11011", "10101", "10001", "10001" } },
            { 'ч', new[] { "00000", "00000", "10001", "10001", "01111", "00001", "00001" } },
            { 'о', new[] { "00000", "00000", "01110", "10001", "10001", "10001", "01110" } },
            { 'б', new[] { "00111", "01000", "11110", "10001", "10001", "10001", "01110" } },
            { 'и', new[] { "00000", "00000", "10001", "10011", "10101", "11001", "10001" } },
            { 'н', new[] { "00000", "00000", "10001", "10001", "11111", "10001", "10001" } },
            { 'C', new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" } },
            { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
            { 'F', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'D', new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" } },
            { ':', new[] { "00000", "01100", "01100", "00000", "01100", "01100", "00000" } },
            { '°', new[] { "01100", "10010", "10010", "01100", "00000", "00000", "00000" } },
        };

        static Color32 C(string hex) { int v = System.Convert.ToInt32(hex.Substring(1), 16); return new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255); }
        static Color32 C(int v) { return new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255); }

        static void Blend(int x, int y, Color32 c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
            int i = y * W + x; var d = px[i];
            a = Mathf.Clamp01(a);
            px[i] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), 255);
        }

        static void Disc(float cx, float cy, float r, Color32 c)
        {
            for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                    Blend(x, y, c, r - Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) + 0.5f);
        }

        static void Ring(float cx, float cy, float r, float w, Color32 c, float a0 = 0, float a1 = Mathf.PI * 2)
        {
            for (int y = (int)(cy - r - w); y <= cy + r + w; y++)
                for (int x = (int)(cx - r - w); x <= cx + r + w; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);
                    if (a1 - a0 < 6.28f)
                    {
                        float ang = Mathf.Atan2(dy, dx); if (ang < 0) ang += Mathf.PI * 2;
                        float lo = (a0 % (Mathf.PI * 2) + Mathf.PI * 2) % (Mathf.PI * 2), hi = lo + (a1 - a0);
                        if (!(ang >= lo && ang <= hi) && !(ang + Mathf.PI * 2 >= lo && ang + Mathf.PI * 2 <= hi)) continue;
                    }
                    Blend(x, y, c, w / 2 - d + 0.5f);
                }
        }

        static void Line(float x0, float y0, float x1, float y1, float w, Color32 c)
        {
            float minx = Mathf.Min(x0, x1) - w, maxx = Mathf.Max(x0, x1) + w, miny = Mathf.Min(y0, y1) - w, maxy = Mathf.Max(y0, y1) + w;
            var ab = new Vector2(x1 - x0, y1 - y0); float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = (int)miny; y <= maxy; y++)
                for (int x = (int)minx; x <= maxx; x++)
                {
                    var p = new Vector2(x + 0.5f - x0, y + 0.5f - y0);
                    float t = Mathf.Clamp01(Vector2.Dot(p, ab) / len2);
                    float d = (p - ab * t).magnitude;
                    Blend(x, y, c, w / 2 - d + 0.5f);
                }
        }

        static void Rect(float x0, float y0, float x1, float y1, Color32 c)
        {
            for (int y = (int)y0; y < y1; y++) for (int x = (int)x0; x < x1; x++) Blend(x, y, c, 1f);
        }

        /// <summary>Текст по центру (cx, cy), высота символа size px.</summary>
        static void Text(string s, float cx, float cy, float size, Color32 c)
        {
            float k = size / 7f, cw = 6f * k;
            float x0 = cx - s.Length * cw / 2f + k * 0.5f, y0 = cy + size / 2f;
            for (int i = 0; i < s.Length; i++)
            {
                string[] g;
                if (!Font.TryGetValue(s[i], out g)) continue;
                for (int r = 0; r < 7; r++)
                    for (int q = 0; q < 5; q++)
                        if (g[r][q] == '1') Rect(x0 + i * cw + q * k, y0 - (r + 1) * k, x0 + i * cw + (q + 1) * k, y0 - r * k, c);
            }
        }

        static float Ang(float f) { return (225f - 270f * f) * Mathf.Deg2Rad; }

        public static Texture2D Draw(InteriorStyle st)
        {
            W = Mathf.CeilToInt(st.cw * PX); H = Mathf.CeilToInt(st.ch * PX);
            px = new Color32[W * H];
            var face = C(st.face); for (int i = 0; i < px.Length; i++) px[i] = face;
            var ink = C(st.ink); var red = C(st.red); var ring = C(st.ring);
            foreach (var g in st.gauges)
            {
                float cx = W / 2f + g.x * PX, cy = H / 2f + g.y * PX, R = g.r * PX;
                if (g.type == "display")
                {
                    Rect(cx - R, cy - R * 1.2f, cx + R, cy + R * 1.2f, C(0x0a1420));
                    Text("ECO", cx, cy + R * 0.4f, R * 0.4f, C(0x9fd0ff));
                    Text("D 12:00", cx, cy - R * 0.3f, R * 0.3f, C(0x9fd0ff));
                    continue;
                }
                Disc(cx, cy, R, C(0x050505));
                Ring(cx, cy, R, Mathf.Max(2f, R * 0.07f), ring);
                System.Action<float, float, float, Color32> tick = (f, len, wdt, col) =>
                {
                    float a = Ang(f);
                    Line(cx + Mathf.Cos(a) * R * 0.86f, cy + Mathf.Sin(a) * R * 0.86f, cx + Mathf.Cos(a) * R * (0.86f - len), cy + Mathf.Sin(a) * R * (0.86f - len), wdt, col);
                };
                if (g.type == "speed" || g.type == "rpm")
                {
                    float major = g.type == "speed" ? 20 : 1; int n = Mathf.RoundToInt(g.max / major);
                    for (int i = 0; i <= n * 2; i++)
                    {
                        float f = i / (n * 2f); bool redz = g.type == "rpm" && f >= 0.75f;
                        tick(f, i % 2 == 1 ? 0.08f : 0.16f, i % 2 == 1 ? 1.5f : 3f, redz ? red : ink);
                    }
                    for (int i = 0; i <= n; i++)
                    {
                        if (g.type == "speed" && n > 9 && i % 2 == 1) continue;
                        float f = i / (float)n, a = Ang(f); int v = Mathf.RoundToInt(i * major);
                        Text(v.ToString(), cx + Mathf.Cos(a) * R * 0.56f, cy + Mathf.Sin(a) * R * 0.56f, Mathf.Max(7f, R * 0.17f), g.type == "rpm" && v >= g.max * 0.75f ? red : ink);
                    }
                    Text(g.label, cx, cy - R * 0.42f, Mathf.Max(7f, R * 0.13f), ink);
                    if (g.type == "speed")
                    {
                        Rect(cx - R * 0.32f, cy - R * 0.69f, cx + R * 0.32f, cy - R * 0.52f, C(0x1a1a1a));
                        Text("084217", cx, cy - R * 0.605f, Mathf.Max(7f, R * 0.12f), C(0xdddddd));
                    }
                }
                else
                {
                    bool both = g.type == "combo" || g.type == "fuelTemp";
                    if (g.type == "fuel" || both) for (int i = 0; i <= 4; i++) tick(i / 4f * (both ? 0.45f : 1f), 0.18f, 2f, i == 0 ? red : ink);
                    if (g.type == "temp" || both) for (int i = 0; i <= 4; i++) tick((both ? 0.55f : 0f) + i / 4f * (both ? 0.45f : 1f), 0.18f, 2f, i == 4 ? red : ink);
                    if (both) { Ring(cx, cy, R * 0.75f, R * 0.08f, red, Ang(0.08f), Ang(0f)); Ring(cx, cy, R * 0.75f, R * 0.08f, red, Ang(1f), Ang(0.92f)); }
                    float ts = Mathf.Max(7f, R * 0.26f);
                    if (g.type == "fuel") Text("F", cx, cy - R * 0.4f, ts, ink);
                    else if (g.type == "temp") Text("°C", cx, cy - R * 0.4f, ts, ink);
                    else { Text("F", cx - R * 0.38f, cy - R * 0.45f, ts, ink); Text("°C", cx + R * 0.38f, cy - R * 0.45f, ts, ink); }
                }
            }
            // контрольные лампы (погашены)
            int[] lamps = { 0x22aa88, 0xee3333, 0xffaa33 };
            for (int i = 0; i < 3; i++) Disc(W / 2f + (i - 1) * H * 0.12f, H * 0.1f, H * 0.025f, Color32.Lerp(face, C(lamps[i]), 0.25f));
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Cluster", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4, filterMode = FilterMode.Trilinear };
            t.SetPixels32(px); t.Apply(true, true);
            px = null;
            return t;
        }
    }
}
