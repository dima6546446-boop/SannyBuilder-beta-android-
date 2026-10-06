using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Дым из-под колёс, пыль с грунта и клубы сигареты — одна система частиц на всю игру (1 draw call).
    /// Клубы небольшие (0.35–0.6 м при рождении, ~1.2–1.8 м к концу жизни), мягкие по краям, медленно вращаются,
    /// плавно проявляются и тают; ночью дым темнее (его подсвечивают только фары и фонари).
    /// </summary>
    public class SmokeFx
    {
        public static SmokeFx I;
        readonly ParticleSystem ps;
        readonly Material mat;

        public SmokeFx(Transform parent, int max)
        {
            I = this;
            var g = new GameObject("Smoke");
            g.transform.SetParent(parent, false);
            ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = max;
            main.startLifetime = 2f; main.startSpeed = 0f; main.startSize = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = false;
            // рост: быстро в первые доли секунды, затем медленно расплывается
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f, 6f, 6f), new Keyframe(0.25f, 2.1f), new Keyframe(1f, 3.4f, 0.8f, 0.8f));
            sz.size = new ParticleSystem.MinMaxCurve(1f, curve);
            // прозрачность: проявляется за 10% жизни, держится и тает
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            // торможение воздухом: клуб отстаёт от машины и зависает
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.4f; lim.dampen = 0.08f;
            var r = g.GetComponent<ParticleSystemRenderer>();
            mat = Mats.Particles().Tex(Mats.Smoke);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 0.35f; // не больше трети экрана, даже если камера внутри клуба
            ps.Play();
        }

        /// <summary>k — сила заноса 0..1. dust — пыль с травы (короче, коричневая).</summary>
        public void Emit(Vector3 pos, Vector3 carVel, bool dust, float k, float light)
        {
            float g = Mathf.Lerp(0.3f, 0.92f, light);
            var ep = new ParticleSystem.EmitParams
            {
                position = pos + new Vector3(Random.Range(-0.15f, 0.15f), 0.12f, Random.Range(-0.15f, 0.15f)),
                velocity = carVel * 0.18f + new Vector3(Random.Range(-0.35f, 0.35f), 0.35f + Random.value * 0.35f, Random.Range(-0.35f, 0.35f)),
                startSize = dust ? Random.Range(0.3f, 0.45f) : Random.Range(0.35f, 0.42f) + k * 0.2f,
                startLifetime = dust ? 0.9f + Random.value * 0.3f : 1.2f + k * 1.1f + Random.value * 0.5f,
                startColor = dust ? new Color(0.55f * g, 0.46f * g, 0.33f * g, 0.32f) : new Color(g, g, g, 0.2f + k * 0.22f),
            };
            ps.Emit(ep, 1);
        }

        /// <summary>Клуб дыма (сигарета, выдох).</summary>
        public void Puff(Vector3 pos, Vector3 vel, float size, float alpha, float life)
        {
            ps.Emit(new ParticleSystem.EmitParams { position = pos, velocity = vel, startSize = size, startLifetime = life, startColor = new Color(0.85f, 0.85f, 0.85f, alpha) }, 1);
        }
    }
}
