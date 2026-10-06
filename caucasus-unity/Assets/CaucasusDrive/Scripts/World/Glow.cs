using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    /// <summary>
    /// Светящиеся точки (фонари, фары и стопы трафика, светофоры, мигалки) — все одним draw call:
    /// система частиц без симуляции, точки задаются каждый кадр через SetParticles.
    /// Аналог GlowPoints из веб-версии.
    /// </summary>
    public class Glow
    {
        readonly ParticleSystem ps;
        readonly ParticleSystem.Particle[] buf;
        int count;
        public int Capacity => buf.Length;

        public Glow(Transform parent, int capacity)
        {
            var go = new GameObject("GlowPoints");
            go.transform.SetParent(parent, false);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1e6f; main.startSpeed = 0f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particles().Tex(Mats.Glow);
            r.sharedMaterial.name = "GlowAdditive";
            SetAdditive(r.sharedMaterial);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -10;
            r.maxParticleSize = 0.5f;
            buf = new ParticleSystem.Particle[capacity];
            ps.Play();
        }

        /// <summary>Перевести материал частиц URP в аддитивный режим (если базовый не настроен).</summary>
        static void SetAdditive(Material m)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)RenderQueue.Transparent + 10;
        }

        public void Begin() { count = 0; }

        public void Add(Vector3 p, Color c, float size)
        {
            if (count >= buf.Length) return;
            var q = new ParticleSystem.Particle
            {
                position = p, startColor = c, startSize = size, remainingLifetime = 1e6f, startLifetime = 1e6f,
                velocity = Vector3.zero, rotation = 0f,
            };
            buf[count++] = q;
        }

        public void End() { ps.SetParticles(buf, count); }
    }
}
