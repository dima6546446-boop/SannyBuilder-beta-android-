using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.UI
{
    /// <summary>Procedurally generated UI sprites (rounded panels, circles, rings, arrows, steering wheel, gauge ticks).</summary>
    public static class UiSprites
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        private static Sprite Make(string key, int w, int h, System.Func<int, int, Color32> px, Vector4 border, bool mips = false)
        {
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, false);
            var data = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) data[y * w + x] = px(x, y);
            t.SetPixels32(data);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            t.Apply(mips, false);
            s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            cache[key] = s;
            return s;
        }

        private static Color32 C(float a) { return new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255)); }

        public static Sprite Rounded(int radius = 18)
        {
            int size = radius * 2 + 4;
            return Make("rr" + radius, size, size, (x, y) =>
            {
                float dx = Mathf.Max(radius + 1 - x, 0, x - (size - 2 - radius));
                float dy = Mathf.Max(radius + 1 - y, 0, y - (size - 2 - radius));
                return C(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
            }, new Vector4(radius, radius, radius, radius));
        }

        public static Sprite Circle()
        {
            return Make("circle", 128, 128, (x, y) =>
            {
                float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f));
                return C(64f - d);
            }, Vector4.zero);
        }

        public static Sprite Ring(float inner = 0.82f)
        {
            return Make("ring" + inner, 256, 256, (x, y) =>
            {
                float d = Mathf.Sqrt((x - 127.5f) * (x - 127.5f) + (y - 127.5f) * (y - 127.5f)) / 128f;
                float a = Mathf.Clamp01((1f - d) * 128f) * Mathf.Clamp01((d - inner) * 128f);
                return C(a);
            }, Vector4.zero);
        }

        public static Sprite Arrow(int dir)   // 0 up, 1 right, 2 down, 3 left
        {
            return Make("arrow" + dir, 64, 64, (x, y) =>
            {
                float u = x / 63f - 0.5f, v = y / 63f - 0.5f;
                float a, b;
                switch (dir) { case 1: a = v; b = -u; break; case 2: a = -u; b = -v; break; case 3: a = -v; b = u; break; default: a = u; b = v; break; }
                // triangle pointing +b
                float half = (0.45f - b) * 0.62f;
                float inside = (b > -0.4f && b < 0.45f && Mathf.Abs(a) < half) ? 1f : 0f;
                return C(inside * 0.98f + 0f);
            }, Vector4.zero);
        }

        public static Sprite Wheel()
        {
            return Make("wheel", 256, 256, (x, y) =>
            {
                float dx = x - 127.5f, dy = y - 127.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 128f;
                float a = 0f;
                if (d < 0.97f && d > 0.84f) a = 1f;                                  // rim
                float ang = Mathf.Atan2(dy, dx);
                // three spokes: left, right, bottom
                for (int k = 0; k < 3; k++)
                {
                    float sa = k == 0 ? Mathf.PI : (k == 1 ? 0f : -Mathf.PI * 0.5f);
                    float diff = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, sa * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    float lateral = Mathf.Sin(diff) * d;
                    if (d < 0.86f && d > 0.15f && diff < 1.2f && Mathf.Abs(lateral) < 0.085f) a = 1f;
                }
                if (d < 0.2f) a = 1f;                                                 // hub
                // top marker
                if (d > 0.84f && d < 0.97f && Mathf.Abs(dx) < 10f && dy > 0f) a = 0.45f;
                return C(a);
            }, Vector4.zero);
        }

        public static Sprite Gradient()
        {
            return Make("grad", 4, 64, (x, y) => C(y / 63f), Vector4.zero);
        }

        public static Sprite Glow()
        {
            return Make("uiglow", 64, 64, (x, y) =>
            {
                float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
                return C(Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
            }, Vector4.zero);
        }

        public static Sprite Gear()
        {
            return Make("gear", 128, 128, (x, y) =>
            {
                float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float teeth = 0.80f + 0.14f * Mathf.Clamp01(Mathf.Sin(ang * 8f) * 3f + 0.5f);
                float outer = Mathf.Clamp01((teeth - r) * 40f);
                float hole = Mathf.Clamp01((r - 0.30f) * 40f);
                return C(outer * hole);
            }, Vector4.zero);
        }

        public static Sprite Check()
        {
            return Make("check", 64, 64, (x, y) =>
            {
                float u = x / 63f, v = y / 63f;
                // two strokes: short down-right, long up-right
                float d1 = DistSeg(u, v, 0.18f, 0.5f, 0.4f, 0.26f);
                float d2 = DistSeg(u, v, 0.4f, 0.26f, 0.84f, 0.76f);
                float d = Mathf.Min(d1, d2);
                return C((0.085f - d) * 30f);
            }, Vector4.zero);
        }

        private static float DistSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float cx = ax + abx * t, cy = ay + aby * t;
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }

        public static Sprite Star()
        {
            return Make("star", 64, 64, (x, y) =>
            {
                float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
                float ang = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float lim = 0.46f + 0.54f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2.5f)), 1.6f);
                return C((lim - r) * 24f);
            }, Vector4.zero);
        }
    }
}
