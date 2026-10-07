using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>
    /// Material factory for URP. Prefers the pre-built base materials in Resources/Materials (so shader variants survive build stripping),
    /// and falls back to Shader.Find so the project also works straight after import.
    /// </summary>
    public static class MatLib
    {
        private static Shader litShader, unlitShader, addShader, spriteShader;
        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        private static Shader Find(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var s = Shader.Find(names[i]);
                if (s != null) return s;
            }
            return null;
        }

        private static void Init()
        {
            if (litShader != null) return;
            litShader = Find("Universal Render Pipeline/Lit", "Standard", "Sprites/Default");
            unlitShader = Find("Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default");
            addShader = Find("Mobile/Particles/Additive", "Legacy Shaders/Particles/Additive", "Sprites/Default");
            spriteShader = Find("Sprites/Default", "Universal Render Pipeline/Unlit");
        }

        private static Material FromBase(string resName, Shader fallback)
        {
            var b = Resources.Load<Material>("Materials/" + resName);
            if (b != null) return new Material(b);
            return new Material(fallback);
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }

        public static void SetTiling(Material m, Vector2 tiling)
        {
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
            if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", tiling);
        }

        public static Material Lit(Color c, float smoothness = 0.4f, float metallic = 0f, Texture tex = null)
        {
            Init();
            var m = FromBase("BaseLit", litShader);
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (tex != null) SetTexture(m, tex);
            return m;
        }

        /// <summary>Lit material with emission enabled (keyword baked in the Resources base material).</summary>
        public static Material LitEmissive(Color c, Color emission, float smoothness = 0.4f)
        {
            Init();
            var m = FromBase("BaseLitEmissive", litShader);
            SetColor(m, c);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        public static Material Unlit(Color c, Texture tex = null)
        {
            Init();
            var m = FromBase("BaseUnlit", unlitShader);
            SetColor(m, c);
            if (tex != null) SetTexture(m, tex);
            return m;
        }

        /// <summary>Additive glow (beams, neon halos, flames).</summary>
        public static Material Additive(Texture tex, Color tint)
        {
            Init();
            var m = new Material(addShader);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
            SetColor(m, tint);
            if (tex != null) SetTexture(m, tex);
            m.renderQueue = 3100;
            return m;
        }

        /// <summary>Alpha-blended unlit sprite-style material (smoke, decals, skidmarks).</summary>
        public static Material Alpha(Texture tex, Color tint)
        {
            Init();
            var m = new Material(spriteShader);
            SetColor(m, tint);
            if (tex != null) SetTexture(m, tex);
            m.renderQueue = 3000;
            return m;
        }

        public static Material Cached(string key, System.Func<Material> make)
        {
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = make();
            cache[key] = m;
            return m;
        }

        public static void ClearCache() { cache.Clear(); }
    }
}
