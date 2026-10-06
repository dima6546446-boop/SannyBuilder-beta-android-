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
            t = new Texture2D(w, h, TextureFormat.RGBA32, mips) { name = key, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, anisoLevel = 16, filterMode = FilterMode.Trilinear, mipMapBias = -0.25f };
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

        /// <summary>Множитель разрешения текстур города: 2 — 512 px, 4 — 1024 px (ставится до первого обращения).</summary>
        public static int TexScale = 2;
        static int S => 256 * TexScale;
        static string K(string n) { return n + TexScale; }

        static float Soft(float e0, float e1, float x) { return M.Smoothstep(e0, e1, x); }

        public static Texture2D Asphalt => Make(K("asphalt"), S, S, (x, y) =>
        {
            float k = TexScale;
            float n = Fbm(x, y, S, 11) * 0.5f + Fbm(x * 3, y * 3, S, 12) * 0.12f + Hash(x, y, 3) * 0.14f;
            float v = 0.2f + n * 0.22f;
            if (Hash(x / 2, y / 2, 9) > 0.975f) v += 0.1f + Hash(x, y, 4) * 0.08f;     // щебень
            if (Hash(x, y, 14) > 0.998f) v *= 0.55f;                                  // тёмные вкрапления
            float crack = Mathf.Abs(Fbm(x, y, S, 19) - 0.5f);                          // тонкие трещины
            if (crack < 0.006f * k * 0.5f) v *= 0.7f;
            return C(v, v, v * 1.02f);
        });

        public static Texture2D Grass => Make(K("grass"), S, S, (x, y) =>
        {
            float n = Fbm(x, y, S, 21), d = Hash(x, y, 5), blade = Hash(x / 2, y, 6);
            float shade = 0.85f + blade * 0.3f;
            return C((0.22f + n * 0.18f + d * 0.05f) * shade, (0.38f + n * 0.22f + d * 0.08f) * shade, (0.14f + n * 0.08f) * shade);
        });

        public static Texture2D Concrete => Make(K("concrete"), S, S, (x, y) =>
        {
            float k = TexScale;
            float n = Fbm(x, y, S, 31) * 0.3f + Hash(x, y, 7) * 0.1f + Fbm(x * 4, y * 4, S, 32) * 0.05f;
            float v = 0.58f + n * 0.3f;
            float gx = x % (128 * k), gy = y % (128 * k);
            if (gx < 1.5f * k || gy < 1.5f * k) v *= 0.7f;                              // швы плит
            else if (gx < 3f * k || gy < 3f * k) v *= 1.06f;                             // фаска у шва
            if (Hash(x / 3, y / 3, 33) > 0.992f) v *= 0.75f;                            // пятна
            return C(v, v * 0.99f, v * 0.96f);
        });

        public static Texture2D Brick => Make(K("brick"), S, S, (x, y) =>
        {
            float k = TexScale;
            float bh = 16 * k, bw = 32 * k, mt = 2 * k;
            int row = Mathf.FloorToInt(y / bh); float off = (row % 2) * bw / 2;
            float by = y % bh, bx = (x + off) % bw;
            int col = Mathf.FloorToInt((x + off) / bw);
            bool mortar = by < mt || bx < mt;
            float edge = Mathf.Min(Mathf.Min(by - mt, bh - by), Mathf.Min(bx - mt, bw - bx));
            float n = Hash(col, row, 41) * 0.25f + Hash(x, y, 2) * 0.1f + Fbm(x, y, S, 43) * 0.12f;
            if (mortar) { float m = 0.68f + Hash(x, y, 8) * 0.08f; return C(m, m * 0.97f, m * 0.92f); }
            float bevel = 0.85f + 0.15f * Soft(0, 2.5f * k, edge);                     // грани кирпича
            return C((0.55f + n) * bevel, (0.27f + n * 0.5f) * bevel, (0.2f + n * 0.3f) * bevel);
        });

        /// <summary>Фасад панельки: 4×4 окна на текстуру. lit = true — карта свечения окон ночью.</summary>
        public static Texture2D Facade(bool lit) => Make(K(lit ? "facadeLit" : "facade"), S, S, (x, y) =>
        {
            float k = TexScale, cell = 64 * k;
            int cx = Mathf.FloorToInt(x / cell), cy = Mathf.FloorToInt(y / cell);
            float lx = (x % cell) / k, ly = (y % cell) / k;
            bool win = lx >= 14 && lx < 50 && ly >= 18 && ly < 52;
            bool frame = win && (Mathf.Abs(lx - 32f) < 0.8f || Mathf.Abs(ly - 36f) < 0.8f);
            float r = Hash(cx, cy, 77);
            if (lit)
            {
                if (!win || frame || r < 0.55f) return C(0, 0, 0);
                float w = 0.7f + Hash(cx, cy, 78) * 0.3f;
                return C(w, w * 0.78f, w * 0.45f);
            }
            bool rim = (lx >= 12.5f && lx < 51.5f && ly >= 16.5f && ly < 53.5f) && !win;        // рама окна
            if (rim) return C(0.88f, 0.88f, 0.85f);
            if (frame) return C(0.85f, 0.85f, 0.82f);
            if (win)
            {
                float g = 0.18f + Hash(cx, cy, 79) * 0.12f + (ly - 18) * 0.004f;
                float refl = Soft(0.2f, 0.9f, (lx - 14f) / 36f - (ly - 18f) / 34f + 0.5f) * 0.1f;   // блик на стекле
                return C((g + refl) * 0.8f, (g + refl) * 0.95f, (g + refl) * 1.15f);
            }
            bool seam = lx < 0.8f || ly < 0.8f;                                                  // швы панелей
            bool sill = ly >= 14 && ly < 16.5f && lx >= 12 && lx < 52;                           // подоконник
            bool balcony = ly < 14 && lx > 8 && lx < 56 && r > 0.6f;
            float v = 0.86f + Fbm(x, y, S, 51) * 0.12f + (Hash(x, y, 52) - 0.5f) * 0.03f;
            if (seam) v *= 0.75f;
            if (sill) v *= 0.82f;
            if (balcony) v *= 0.8f;
            if (ly > 56) v *= 0.95f - (ly - 56) * 0.01f;                                         // потёки под карнизом
            return C(v, v, v);
        });

        public static Texture2D Glow => Make("glow", 64, 64, (x, y) =>
        {
            float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            a = a * a;
            return new Color32(255, 255, 255, (byte)(a * 255));
        }, false);

        /// <summary>Мягкий клуб дыма: гауссов спад к краю и лёгкая «вата» из шума, без резкой границы.</summary>
        public static Texture2D Smoke => Make("smoke2", 128, 128, (x, y) =>
        {
            float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f;
            float r2 = dx * dx + dy * dy;
            float n = Fbm(x, y, 128, 3);
            float a = Mathf.Exp(-r2 * 4.2f) * (0.55f + 0.75f * n) - 0.06f;
            a = Mathf.Clamp01(a) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 3f);
            byte v = (byte)(215 + n * 40);
            return new Color32(v, v, v, (byte)(Mathf.Clamp01(a) * 230));
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
