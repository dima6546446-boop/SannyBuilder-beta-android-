using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Материалы и процедурные текстуры. Базовые материалы лежат в Resources/Materials
    /// (их создаёт меню «CAUCASUS DRIVE → Настроить проект» — так нужные варианты шейдеров
    /// URP гарантированно попадают в сборку). Если их нет — создаём из шейдеров напрямую.
    /// Свойства пишутся и под URP (_BaseColor/_BaseMap), и под Built-in (_Color/_MainTex).
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> baseCache = new Dictionary<string, Material>();

        static Material Base(string name, string urpShader, string fallbackShader)
        {
            Material m;
            if (baseCache.TryGetValue(name, out m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                var sh = Shader.Find(urpShader) ?? Shader.Find(fallbackShader) ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                m = new Material(sh) { name = name };
            }
            baseCache[name] = m;
            return m;
        }

        public static Material Lit() { return new Material(Base("Lit", "Universal Render Pipeline/Lit", "Standard")); }
        public static Material LitEmissive() { return new Material(Base("LitEmissive", "Universal Render Pipeline/Lit", "Standard")); }
        public static Material LitTransparent() { return new Material(Base("LitTransparent", "Universal Render Pipeline/Lit", "Standard")); }
        public static Material Simple() { return new Material(Base("SimpleLit", "Universal Render Pipeline/Simple Lit", "Legacy Shaders/Diffuse")); }
        public static Material SimpleEmissive() { return new Material(Base("SimpleLitEmissive", "Universal Render Pipeline/Simple Lit", "Legacy Shaders/Self-Illumin/Diffuse")); }
        public static Material SimpleCutout() { return new Material(Base("SimpleLitCutout", "Universal Render Pipeline/Simple Lit", "Legacy Shaders/Transparent/Cutout/Diffuse")); }
        public static Material Unlit() { return new Material(Base("Unlit", "Universal Render Pipeline/Unlit", "Unlit/Color")); }
        public static Material Additive() { return new Material(Base("UnlitAdditive", "Universal Render Pipeline/Unlit", "Legacy Shaders/Particles/Additive")); }
        public static Material Particles() { return new Material(Base("Particles", "Universal Render Pipeline/Particles/Unlit", "Legacy Shaders/Particles/Alpha Blended")); }

        public static Material Col(this Material m, UnityEngine.Color c)
        {
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            return m;
        }
        public static Material Tex(this Material m, Texture t, float tileX = 1f, float tileY = 1f)
        {
            m.SetTexture("_BaseMap", t); m.SetTexture("_MainTex", t);
            var s = new Vector2(tileX, tileY);
            m.SetTextureScale("_BaseMap", s); m.SetTextureScale("_MainTex", s);
            return m;
        }
        public static Material Pbr(this Material m, float metallic, float smoothness)
        {
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Glossiness", smoothness);
            return m;
        }
        public static Material Emission(this Material m, Color c)
        {
            m.SetColor("_EmissionColor", c);
            return m;
        }
        public static Material EmissionMap(this Material m, Texture t)
        {
            m.SetTexture("_EmissionMap", t);
            return m;
        }

        // ------------------------------------------------------------------ текстуры
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        static Texture2D Make(string key, int w, int h, System.Func<int, int, Color32> px, bool repeat = true, bool mips = true)
        {
            Texture2D t;
            if (texCache.TryGetValue(key, out t)) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, mips) { name = key, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, anisoLevel = 4, filterMode = FilterMode.Trilinear };
            var data = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) data[y * w + x] = px(x, y);
            t.SetPixels32(data);
            t.Apply(mips, true); // true — выгрузить копию из памяти CPU
            texCache[key] = t;
            return t;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144269504;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Сглаженный шум, бесшовный с периодом p.</summary>
        static float Noise(float x, float y, int p, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int ax = ((x0 % p) + p) % p, ay = ((y0 % p) + p) % p, bx = (ax + 1) % p, by = (ay + 1) % p;
            float a = Hash(ax, ay, seed), b = Hash(bx, ay, seed), c = Hash(ax, by, seed), d = Hash(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(int x, int y, int size, int seed)
        {
            float v = 0f, amp = 0.5f;
            for (int o = 0; o < 4; o++)
            {
                int p = 4 << o;
                v += amp * Noise(x * p / (float)size, y * p / (float)size, p, seed + o);
                amp *= 0.5f;
            }
            return v;
        }

        static Color32 C(float r, float g, float b) { return new Color32((byte)(Mathf.Clamp01(r) * 255), (byte)(Mathf.Clamp01(g) * 255), (byte)(Mathf.Clamp01(b) * 255), 255); }

        public static Texture2D Asphalt => Make("asphalt", 256, 256, (x, y) =>
        {
            float n = Fbm(x, y, 256, 11) * 0.5f + Hash(x, y, 3) * 0.18f;
            float v = 0.2f + n * 0.22f;
            if (Hash(x, y, 9) > 0.985f) v += 0.12f; // щебень
            return C(v, v, v * 1.02f);
        });

        public static Texture2D Grass => Make("grass", 256, 256, (x, y) =>
        {
            float n = Fbm(x, y, 256, 21), d = Hash(x, y, 5);
            return C(0.22f + n * 0.18f + d * 0.05f, 0.38f + n * 0.22f + d * 0.08f, 0.14f + n * 0.08f);
        });

        public static Texture2D Concrete => Make("concrete", 256, 256, (x, y) =>
        {
            float n = Fbm(x, y, 256, 31) * 0.3f + Hash(x, y, 7) * 0.1f;
            float v = 0.58f + n * 0.3f;
            if (x % 128 < 2 || y % 128 < 2) v *= 0.7f; // плиты
            return C(v, v * 0.99f, v * 0.96f);
        });

        public static Texture2D Brick => Make("brick", 256, 256, (x, y) =>
        {
            int row = y / 16, off = (row % 2) * 16;
            bool mortar = y % 16 < 2 || (x + off) % 32 < 2;
            float n = Hash((x + off) / 32, row, 41) * 0.25f + Hash(x, y, 2) * 0.08f;
            return mortar ? C(0.72f, 0.7f, 0.66f) : C(0.55f + n, 0.27f + n * 0.5f, 0.2f + n * 0.3f);
        });

        /// <summary>Фасад панельки: 4×4 окна на текстуру. lit = true — карта свечения окон ночью.</summary>
        public static Texture2D Facade(bool lit) => Make(lit ? "facadeLit" : "facade", 256, 256, (x, y) =>
        {
            int cx = x / 64, cy = y / 64, lx = x % 64, ly = y % 64;
            bool win = lx >= 14 && lx < 50 && ly >= 18 && ly < 52;
            bool frame = win && (lx == 31 || lx == 32 || ly == 36);
            float r = Hash(cx, cy, 77);
            if (lit)
            {
                if (!win || frame || r < 0.55f) return C(0, 0, 0);
                float w = 0.7f + Hash(cx, cy, 78) * 0.3f;
                return C(w, w * 0.78f, w * 0.45f);
            }
            if (frame) return C(0.85f, 0.85f, 0.82f);
            if (win)
            {
                float g = 0.18f + Hash(cx, cy, 79) * 0.12f + (ly - 18) * 0.004f;
                return C(g * 0.8f, g * 0.95f, g * 1.15f);
            }
            bool seam = lx < 1 || ly < 1;          // швы панелей
            bool balcony = ly < 14 && lx > 8 && lx < 56 && r > 0.6f;
            float v = 0.86f + Fbm(x, y, 256, 51) * 0.12f;
            if (seam) v *= 0.75f;
            if (balcony) v *= 0.8f;
            return C(v, v, v);
        });

        public static Texture2D Glow => Make("glow", 64, 64, (x, y) =>
        {
            float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            a = a * a;
            return new Color32(255, 255, 255, (byte)(a * 255));
        }, false);

        public static Texture2D Smoke => Make("smoke", 64, 64, (x, y) =>
        {
            float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float n = Noise(x / 8f, y / 8f, 8, 3);
            float a = Mathf.Clamp01((1f - r) * (0.6f + n * 0.6f));
            return new Color32(235, 235, 235, (byte)(a * a * 200));
        }, false);

        public static Texture2D Skid => Make("skid", 32, 32, (x, y) =>
        {
            float a = 0.55f + Hash(x, y, 4) * 0.25f;
            if (x < 2 || x > 29) a *= 0.4f;
            return new Color32(20, 20, 20, (byte)(a * 255));
        });

        /// <summary>Маска «ромб» для сетки колёсных дисков и решёток.</summary>
        public static Texture2D White => Make("white", 4, 4, (x, y) => new Color32(255, 255, 255, 255));
    }
}
