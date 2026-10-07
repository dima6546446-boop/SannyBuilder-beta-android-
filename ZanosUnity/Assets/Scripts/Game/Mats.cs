using System.Collections.Generic;
using UnityEngine;

namespace Zanos.Game
{
    /// <summary>Фабрика материалов (без внешних ассетов): работает и на Built-in, и на URP. Текстуры рисуются кодом.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader lit, unlit;

        static Shader Lit()
        {
            if (lit != null) return lit;
            lit = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (lit == null) lit = Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Sprites/Default");
            return lit;
        }
        static Shader Unlit()
        {
            if (unlit != null) return unlit;
            unlit = Shader.Find("Universal Render Pipeline/Unlit"); if (unlit == null) unlit = Shader.Find("Unlit/Color"); if (unlit == null) unlit = Shader.Find("Sprites/Default");
            return unlit;
        }

        static void SetColor(Material m, Color c) { m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); }
        static void SetTex(Material m, Texture t) { m.mainTexture = t; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t); }
        static void SetSmooth(Material m, float s, float metal)
        {
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", s); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        }
        static void SetEmission(Material m, Color e)
        {
            m.EnableKeyword("_EMISSION"); if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", e);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        static void SetTransparent(Material m, float alpha)
        {
            if (m.HasProperty("_Surface")) { m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetFloat("_ZWrite", 0); }
            else if (m.HasProperty("_Mode")) { m.SetFloat("_Mode", 3); m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0); m.EnableKeyword("_ALPHABLEND_ON"); }
            m.renderQueue = 3000;
            var c = m.color; c.a = alpha; SetColor(m, c);
        }

        public static Material Solid(Color c, float smooth = 0.25f, float metal = 0f)
        {
            string key = "s" + ColorUtility.ToHtmlStringRGBA(c) + smooth + metal;
            Material m; if (cache.TryGetValue(key, out m)) return m;
            m = new Material(Lit()); SetColor(m, c); SetSmooth(m, smooth, metal); cache[key] = m; return m;
        }
        public static Material Paint(Color c) { return Solid(c, 0.78f, 0.45f); }
        public static Material Glass() { return Transparent(new Color(0.06f, 0.09f, 0.12f), 0.7f, 0.95f); }
        public static Material Transparent(Color c, float alpha, float smooth = 0.5f)
        {
            string key = "t" + ColorUtility.ToHtmlStringRGBA(c) + alpha;
            Material m; if (cache.TryGetValue(key, out m)) return m;
            m = new Material(Lit()); SetSmooth(m, smooth, 0.2f); SetTransparent(m, alpha); SetColor(m, new Color(c.r, c.g, c.b, alpha)); cache[key] = m; return m;
        }
        public static Material Emissive(Color c, float intensity)
        {
            string key = "e" + ColorUtility.ToHtmlStringRGBA(c) + intensity;
            Material m; if (cache.TryGetValue(key, out m)) return m;
            m = new Material(Lit()); SetColor(m, c * 0.4f); SetEmission(m, c * intensity); cache[key] = m; return m;
        }
        public static Material Flat(Color c)
        {
            string key = "u" + ColorUtility.ToHtmlStringRGBA(c);
            Material m; if (cache.TryGetValue(key, out m)) return m;
            m = new Material(Unlit()); SetColor(m, c); cache[key] = m; return m;
        }
        public static Material Additive(Color c)
        {
            string key = "a" + ColorUtility.ToHtmlStringRGBA(c);
            Material m; if (cache.TryGetValue(key, out m)) return m;
            var sh = Shader.Find("Legacy Shaders/Particles/Additive"); if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (sh == null) sh = Unlit();
            m = new Material(sh); SetColor(m, c); if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c); cache[key] = m; return m;
        }

        // ---- текстуры ----
        static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
        static Texture2D MakeTex(string key, int size, System.Func<int, int, Color> f, bool mip = true)
        {
            Texture2D t; if (tex.TryGetValue(key, out t)) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, mip) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = f(x, y);
            t.SetPixels(px); t.Apply(mip); tex[key] = t; return t;
        }
        static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1274126177; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }

        public static Material Surface(string type, float tileScale = 1f)
        {
            string key = "surf" + type; Material m; if (cache.TryGetValue(key, out m)) return m;
            Color baseC; float spread = 0.12f;
            switch (type)
            {
                case "concrete": baseC = new Color(0.55f, 0.56f, 0.57f); break;
                case "grass": baseC = new Color(0.25f, 0.42f, 0.2f); spread = 0.18f; break;
                case "gravel": baseC = new Color(0.54f, 0.51f, 0.45f); spread = 0.2f; break;
                case "dirt": baseC = new Color(0.42f, 0.32f, 0.22f); spread = 0.15f; break;
                case "wet": baseC = new Color(0.16f, 0.17f, 0.19f); spread = 0.06f; break;
                case "snow": baseC = new Color(0.9f, 0.93f, 0.95f); spread = 0.05f; break;
                default: baseC = new Color(0.23f, 0.24f, 0.26f); break;
            }
            var t = MakeTex("tex" + type, 128, (x, y) => { float n = (Hash(x, y, 1) - 0.5f) * spread * 2 + (Hash(x / 4, y / 4, 2) - 0.5f) * spread; return new Color(baseC.r + n, baseC.g + n, baseC.b + n, 1); });
            m = new Material(Lit()); SetTex(m, t); SetColor(m, Color.white);
            SetSmooth(m, type == "wet" ? 0.85f : type == "snow" ? 0.3f : 0.12f, type == "wet" ? 0.2f : 0f);
            cache[key] = m; return m;
        }

        /// <summary>Фасад здания: окна по сетке; ночью светятся (emission-карта).</summary>
        public static Material Facade(Color wall, bool night)
        {
            string key = "fac" + ColorUtility.ToHtmlStringRGB(wall) + night; Material m; if (cache.TryGetValue(key, out m)) return m;
            var map = MakeTex("facade", 128, (x, y) => { bool win = (x % 32) > 6 && (x % 32) < 26 && (y % 32) > 8 && (y % 32) < 26; return win ? new Color(0.35f, 0.42f, 0.52f) : new Color(0.78f, 0.79f, 0.82f); });
            m = new Material(Lit()); SetTex(m, map); SetColor(m, wall); SetSmooth(m, 0.35f, 0f);
            if (night)
            {
                var em = MakeTex("facade-em", 128, (x, y) => { bool win = (x % 32) > 6 && (x % 32) < 26 && (y % 32) > 8 && (y % 32) < 26; bool lit2 = Hash(x / 32, y / 32, 5) < 0.5f; return win && lit2 ? new Color(1f, 0.82f, 0.5f) : Color.black; });
                m.EnableKeyword("_EMISSION"); if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", em); if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white * 0.8f);
            }
            cache[key] = m; return m;
        }

        public static Texture2D SoftSprite()
        {
            return MakeTex("soft", 64, (x, y) => { float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f; float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy)); a *= a; return new Color(1, 1, 1, a); }, false);
        }

        public static Color Hex(string html) { Color c; return ColorUtility.TryParseHtmlString(html, out c) ? c : Color.red; }
        public static Color Rgb(int rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }
    }
}
