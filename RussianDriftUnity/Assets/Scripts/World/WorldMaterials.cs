using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Shared material set for the city. Facade materials are emissive so windows glow at night.</summary>
    public class WorldMaterials
    {
        public Material asphalt, sidewalk, roof, grass, dirt, metal, brick, tire, glass, trunk, leaves;
        public Material[] facades = new Material[4];
        public Material[] containers = new Material[6];
        public Material white, yellow, red, orange, lampHead, signBlue, signRed, signGreen, signYellow, fenceMat, concrete, curbRed, curbWhite, black;
        public Material lampGlow, lightNSa, lightEWa, lightNSb, lightEWb;
        public Material roadWet;      // alias of asphalt (wetness drives its smoothness)

        private static readonly Color[] containerColors =
        {
            new Color(0.75f, 0.18f, 0.12f), new Color(0.15f, 0.35f, 0.65f), new Color(0.2f, 0.5f, 0.28f),
            new Color(0.85f, 0.62f, 0.15f), new Color(0.6f, 0.6f, 0.62f), new Color(0.45f, 0.25f, 0.55f)
        };

        public static WorldMaterials Create()
        {
            var m = new WorldMaterials();
            var asphaltTex = ProcTex.Asphalt();
            m.asphalt = MatLib.Lit(new Color(0.85f, 0.85f, 0.88f), 0.25f, 0f, asphaltTex);
            m.roadWet = m.asphalt;
            m.sidewalk = MatLib.Lit(new Color(0.8f, 0.8f, 0.78f), 0.12f, 0f, ProcTex.Concrete());
            m.concrete = MatLib.Lit(new Color(0.75f, 0.75f, 0.73f), 0.1f, 0f, ProcTex.Concrete());
            m.roof = MatLib.Lit(new Color(0.25f, 0.25f, 0.27f), 0.1f, 0f, ProcTex.Concrete());
            m.grass = MatLib.Lit(new Color(0.8f, 0.9f, 0.7f), 0.05f, 0f, ProcTex.Grass());
            m.dirt = MatLib.Lit(new Color(0.9f, 0.85f, 0.8f), 0.05f, 0f, ProcTex.Dirt());
            m.metal = MatLib.Lit(Color.white, 0.5f, 0.6f, ProcTex.Metal());
            m.brick = MatLib.Lit(Color.white, 0.1f, 0f, ProcTex.Brick());
            m.tire = MatLib.Lit(new Color(0.12f, 0.12f, 0.12f), 0.15f, 0f);
            m.glass = MatLib.Lit(new Color(0.1f, 0.15f, 0.2f), 0.9f, 0.2f);
            m.trunk = MatLib.Lit(new Color(0.28f, 0.18f, 0.1f), 0.05f, 0f);
            m.leaves = MatLib.Lit(new Color(0.2f, 0.45f, 0.16f), 0.05f, 0f);
            m.black = MatLib.Lit(new Color(0.05f, 0.05f, 0.05f), 0.2f, 0f);
            m.fenceMat = MatLib.Lit(new Color(0.55f, 0.55f, 0.55f), 0.3f, 0.4f);
            var em = ProcTex.FacadeEmission();
            for (int i = 0; i < 4; i++)
            {
                var f = MatLib.LitEmissive(Color.white, Color.black, 0.15f);
                MatLib.SetTexture(f, ProcTex.Facade(i));
                f.SetTexture("_EmissionMap", em);
                m.facades[i] = f;
            }
            for (int i = 0; i < m.containers.Length; i++) m.containers[i] = MatLib.Lit(containerColors[i], 0.35f, 0.4f, ProcTex.Metal());
            m.white = MatLib.Unlit(new Color(0.92f, 0.92f, 0.9f));
            m.yellow = MatLib.Unlit(new Color(0.95f, 0.75f, 0.1f));
            m.red = MatLib.Unlit(new Color(0.8f, 0.1f, 0.08f));
            m.orange = MatLib.Lit(new Color(1f, 0.4f, 0.05f), 0.3f, 0f);
            m.curbRed = MatLib.Lit(new Color(0.8f, 0.1f, 0.1f), 0.2f, 0f);
            m.curbWhite = MatLib.Lit(new Color(0.92f, 0.92f, 0.92f), 0.2f, 0f);
            m.lampHead = MatLib.Unlit(new Color(1.0f, 0.95f, 0.8f));
            m.signBlue = MatLib.Unlit(new Color(0.1f, 0.4f, 2.4f));
            m.signRed = MatLib.Unlit(new Color(2.6f, 0.1f, 0.2f));
            m.signGreen = MatLib.Unlit(new Color(0.2f, 2.4f, 0.4f));
            m.signYellow = MatLib.Unlit(new Color(2.6f, 1.9f, 0.2f));
            m.lampGlow = MatLib.Additive(ProcTex.Glow(), new Color(1f, 0.8f, 0.5f, 1f) * 0f);
            m.lightNSa = MatLib.Unlit(Color.red); m.lightEWa = MatLib.Unlit(Color.red);
            m.lightNSb = MatLib.Unlit(Color.red); m.lightEWb = MatLib.Unlit(Color.red);
            return m;
        }

        /// <summary>Per-frame environment response: window glow, lamp glow, wet road gloss.</summary>
        public void UpdateEnvironment(float night, float wetness)
        {
            for (int i = 0; i < facades.Length; i++)
            {
                var f = facades[i];
                if (f == null) continue;
                f.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.5f) * (night * night * 1.4f));
            }
            if (lampHead != null) MatLib.SetColor(lampHead, Color.Lerp(new Color(0.55f, 0.55f, 0.5f), new Color(3f, 2.7f, 2f), night));
            if (lampGlow != null)
            {
                Color c = new Color(1f, 0.8f, 0.5f, 1f) * (night * 0.55f);
                if (lampGlow.HasProperty("_TintColor")) lampGlow.SetColor("_TintColor", c);
                MatLib.SetColor(lampGlow, c);
            }
            if (asphalt != null)
            {
                if (asphalt.HasProperty("_Smoothness")) asphalt.SetFloat("_Smoothness", Mathf.Lerp(0.22f, 0.92f, wetness));
                MatLib.SetColor(asphalt, Color.Lerp(new Color(0.85f, 0.85f, 0.88f), new Color(0.45f, 0.46f, 0.5f), wetness));
            }
            if (sidewalk != null && sidewalk.HasProperty("_Smoothness")) sidewalk.SetFloat("_Smoothness", Mathf.Lerp(0.12f, 0.6f, wetness));
        }
    }
}
