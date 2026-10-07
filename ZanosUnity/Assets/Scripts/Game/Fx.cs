using System.Collections.Generic;
using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>Эффекты: дым от шин (ParticleSystem), следы шин (TrailRenderer на каждом колесе), искры при ударах.</summary>
    public sealed class Fx
    {
        readonly ParticleSystem smoke, sparks; readonly TrailRenderer[] trails = new TrailRenderer[4]; readonly float[] acc = new float[4];
        readonly GameObject root;
        public bool Enabled = true;

        static ParticleSystem MakePS(Transform parent, string name, Material mat, float size, float life, int max)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = life; main.startSize = size; main.startSpeed = 0; main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        public Fx(Transform parent)
        {
            root = new GameObject("Fx"); root.transform.SetParent(parent, false);
            var smat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default"));
            smat.mainTexture = Mats.SoftSprite(); if (smat.HasProperty("_BaseMap")) smat.SetTexture("_BaseMap", Mats.SoftSprite());
            if (smat.HasProperty("_Surface")) { smat.SetFloat("_Surface", 1); smat.SetFloat("_Blend", 0); smat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); smat.SetFloat("_ZWrite", 0); smat.renderQueue = 3000; }
            smoke = MakePS(root.transform, "Smoke", smat, 1.2f, 1.8f, 700);
            var smcol = smoke.colorOverLifetime; smcol.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(0.5f, 0.08f), new GradientAlphaKey(0f, 1) }); smcol.color = g;
            var sol = smoke.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(1, 1.6f)));
            var amat = Mats.Additive(new Color(1f, 0.75f, 0.3f)); sparks = MakePS(root.transform, "Sparks", amat, 0.12f, 0.6f, 300);
            var tmat = Mats.Flat(new Color(0.03f, 0.03f, 0.035f));
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Skid" + i); go.transform.SetParent(root.transform, false);
                var tr = go.AddComponent<TrailRenderer>(); tr.time = 20f; tr.startWidth = 0.24f; tr.endWidth = 0.24f; tr.minVertexDistance = 0.3f; tr.sharedMaterial = tmat; tr.alignment = LineAlignment.TransformZ; tr.emitting = false;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; go.transform.rotation = Quaternion.Euler(90, 0, 0); trails[i] = tr;
            }
        }

        public void Clear() { smoke.Clear(); sparks.Clear(); foreach (var t in trails) t.Clear(); }

        public void Impact(ImpactEvent e)
        {
            if (!Enabled || e.Kind == "prop") return;
            var ep = new ParticleSystem.EmitParams(); int n = Mathf.Min(40, 8 + (int)(e.Speed * 3));
            for (int i = 0; i < n; i++)
            {
                float sp = 2 + Random.value * Mathf.Min(10f, (float)e.Speed * 0.8f);
                ep.position = new Vector3((float)e.X, 0.5f, (float)e.Z); ep.velocity = new Vector3((float)e.Nx * sp + (Random.value - 0.5f) * 5, 1.5f + Random.value * 4, (float)e.Nz * sp + (Random.value - 0.5f) * 5);
                ep.startLifetime = 0.35f + Random.value * 0.5f; ep.startSize = 0.1f; sparks.Emit(ep, 1);
            }
        }

        public void Update(float dt, Car car, string time, string weather)
        {
            if (!Enabled) { foreach (var t in trails) t.emitting = false; return; }
            float fx = Mathf.Sin((float)car.H), fz = Mathf.Cos((float)car.H), rx = Mathf.Cos((float)car.H), rz = -Mathf.Sin((float)car.H);
            float tint = (time == "night" ? 0.32f : time == "dusk" ? 0.7f : 1f) * (weather == "rain" ? 0.8f : 1f);
            for (int i = 0; i < 4; i++)
            {
                double px = car.WheelPos[i][0], pz = car.WheelPos[i][1];
                var wp = new Vector3((float)(car.X + fx * pz + rx * px), 0.07f, (float)(car.Z + fz * pz + rz * px));
                var surf = car.ContactSurface[i] ?? Surface.Asphalt; float s = (float)car.Slip[i];
                bool mark = surf.Mark && weather == "clear" && s > 0.2f;
                trails[i].transform.position = wp; trails[i].emitting = mark;
                if (s > 0.18f && car.Speed > 1.5)
                {
                    acc[i] += dt * s * ((surf == Surface.Gravel || surf == Surface.Dirt) ? 60 : 85);
                    while (acc[i] >= 1)
                    {
                        acc[i] -= 1; var ep = new ParticleSystem.EmitParams(); var c = Mats.Rgb(surf.SmokeRgb) * tint; c.a = 1;
                        ep.position = new Vector3(wp.x, 0.15f, wp.z); ep.velocity = new Vector3((float)car.Vx * 0.35f + (Random.value - 0.5f) * 1.4f, 0.6f + Random.value * 0.8f, (float)car.Vz * 0.35f + (Random.value - 0.5f) * 1.4f);
                        ep.startColor = c; ep.startSize = 0.8f + Random.value * 0.8f + s; ep.startLifetime = 1.0f + Random.value * 1.4f; smoke.Emit(ep, 1);
                    }
                }
                else acc[i] = 0;
            }
        }

        public void Destroy() { if (root != null) Object.Destroy(root); }
    }
}
