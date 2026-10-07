using UnityEngine;
using UnityEngine.Rendering;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>Per-car visual effects: tyre smoke, skid marks, sparks, exhaust smoke/flames, puddle spray, turbo heat glow.</summary>
    public class VehicleEffects : MonoBehaviour
    {
        private VehicleController vc;
        private bool full;
        private ParticleSystem[] smoke;
        private ParticleSystem[] spray;
        private ParticleSystem sparks, exhaustSmoke, flames, heatGlow;
        private SkidMarks.Writer[] skids;
        private float heat;
        private float partScale = 1f;
        private float sparkCooldown;

        public static ParticleSystem MakePS(string name, Transform parent, Material mat, float life0, float life1, float size0, float size1,
            Color color, float gravity, bool worldSpace, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
            main.startSpeed = 0f;
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = maxParticles;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.08f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(1, 1.3f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 0;
            ps.Play();
            return ps;
        }

        public void Init(VehicleController v, bool fullEffects)
        {
            vc = v;
            full = fullEffects;
            partScale = QualityManager.Current.particleScale;
            var white = MatLib.Alpha(ProcTex.SoftCircle(), Color.white);
            var add = MatLib.Additive(ProcTex.Glow(), Color.white);
            int n = vc.Wheels.Length;
            smoke = new ParticleSystem[n];
            skids = new SkidMarks.Writer[n];
            spray = new ParticleSystem[n];
            for (int i = 0; i < n; i++)
            {
                var w = vc.Wheels[i];
                bool rear = !w.front;
                if (rear || full)
                    smoke[i] = MakePS("Smoke" + i, transform, white, 0.9f, 1.8f, 0.7f, 1.6f, new Color(0.92f, 0.92f, 0.92f, 0.5f), -0.06f, true, 120);
                skids[i] = new SkidMarks.Writer();
                if (full)
                    spray[i] = MakePS("Spray" + i, transform, white, 0.35f, 0.7f, 0.18f, 0.4f, new Color(0.8f, 0.85f, 0.95f, 0.45f), 0.6f, true, 80);
            }
            if (full)
            {
                sparks = MakePS("Sparks", transform, add, 0.25f, 0.55f, 0.05f, 0.12f, new Color(1f, 0.75f, 0.35f, 1f), 1.2f, true, 80);
                var m = sparks.main; m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
                var sh = sparks.shape; sh.angle = 35f;
                if (vc.Model.exhaust != null)
                {
                    exhaustSmoke = MakePS("ExhaustSmoke", vc.Model.exhaust, white, 0.8f, 1.5f, 0.15f, 0.45f, new Color(0.5f, 0.5f, 0.5f, 0.3f), -0.04f, true, 60);
                    exhaustSmoke.transform.localRotation = Quaternion.Euler(180f, 0, 0);
                    exhaustSmoke.transform.localPosition = new Vector3(0, 0, -0.1f);
                    var ms = exhaustSmoke.main; ms.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
                    flames = MakePS("Flames", vc.Model.exhaust, add, 0.10f, 0.22f, 0.25f, 0.5f, new Color(1f, 0.55f, 0.15f, 1f), 0f, true, 40);
                    flames.transform.localPosition = new Vector3(0, 0, -0.1f);
                    flames.transform.localRotation = Quaternion.Euler(180f, 0, 0);
                    var mf = flames.main; mf.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                    var shf = flames.shape; shf.angle = 6f;
                    heatGlow = MakePS("HeatGlow", vc.Model.exhaust, add, 0.5f, 0.9f, 0.3f, 0.6f, new Color(1f, 0.3f, 0.08f, 0.35f), -0.1f, true, 40);
                    heatGlow.transform.localRotation = Quaternion.Euler(180f, 0, 0);
                    var mh = heatGlow.main; mh.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
                }
            }
            vc.Engine.Backfired += OnBackfire;
        }

        private void OnDestroy()
        {
            if (vc != null && vc.Engine != null) vc.Engine.Backfired -= OnBackfire;
        }

        private void OnBackfire()
        {
            if (flames != null) flames.Emit(Mathf.CeilToInt(10 * partScale));
            if (exhaustSmoke != null) exhaustSmoke.Emit(6);
            if (vc.IsPlayer) GameEvents.RaiseBackfire();
        }

        /// <summary>One-shot dust/smoke puff for impacts, taken from the shared PoolManager and returned automatically.</summary>
        public static void SpawnImpactPuff(Vector3 pos, float strength)
        {
            var go = PoolManager.Spawn("ImpactPuff", () =>
            {
                var holder = new GameObject("ImpactPuff");
                var ps = MakePS("Puff", holder.transform, MatLib.Alpha(ProcTex.SoftCircle(), Color.white), 0.6f, 1.2f, 0.8f, 1.8f, new Color(0.75f, 0.72f, 0.68f, 0.55f), -0.05f, true, 40);
                var m = ps.main; m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f); m.loop = false;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.25f;
                holder.AddComponent<PooledLifetime>().key = "ImpactPuff";
                return holder;
            }, pos, Quaternion.identity);
            var life = go.GetComponent<PooledLifetime>();
            life.life = 2.5f;
            var puff = go.GetComponentInChildren<ParticleSystem>();
            puff.Clear();
            puff.Play();
            puff.Emit(Mathf.CeilToInt(Mathf.Lerp(5f, 22f, Mathf.Clamp01(strength)) * QualityManager.Current.particleScale));
        }

        public void EmitSparks(Vector3 pos, Vector3 dir, int count)
        {
            if (sparks == null || sparkCooldown > 0f) return;
            sparkCooldown = 0.05f;
            sparks.transform.position = pos;
            sparks.transform.rotation = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            sparks.Emit(Mathf.CeilToInt(count * partScale));
        }

        private void LateUpdate()
        {
            if (vc == null || vc.Wheels == null) return;
            float dt = Time.deltaTime;
            sparkCooldown -= dt;
            float speed = vc.SpeedMs;
            for (int i = 0; i < vc.Wheels.Length; i++)
            {
                var w = vc.Wheels[i];
                bool marking = w.grounded && w.slip > 0.3f && (speed > 2.5f || Mathf.Abs(w.spinSpeed) > 3f) && w.surface != SurfaceType.Grass && !w.inPuddle;
                if (marking)
                {
                    Vector3 f = Vector3.ProjectOnPlane(vc.transform.forward, w.contactNormal);
                    SkidMarks.Instance.Add(skids[i], w.contactPoint, w.contactNormal, f.sqrMagnitude > 0.01f ? f.normalized : vc.transform.forward,
                        vc.Stats.def.tireWidth * 0.95f, Mathf.Clamp01((w.slip - 0.25f) * 1.6f) * (1f - WorldConditions.Wetness * 0.6f));
                }
                else skids[i].End();

                if (smoke[i] != null)
                {
                    var ps = smoke[i];
                    bool emit = w.grounded && w.slip > 0.28f && (speed > 3f || Mathf.Abs(w.spinSpeed) > 4f);
                    ps.transform.position = w.contactPoint + Vector3.up * 0.1f;
                    var em = ps.emission;
                    float rate = emit ? (20f + 70f * Mathf.Clamp01(w.slip)) * partScale : 0f;
                    if (w.surface == SurfaceType.Dirt || w.surface == SurfaceType.Grass) rate *= 0.6f;
                    em.rateOverTime = rate;
                    var main = ps.main;
                    Color c = w.surface == SurfaceType.Dirt ? new Color(0.55f, 0.45f, 0.32f, 0.45f) : new Color(0.93f, 0.93f, 0.93f, Mathf.Lerp(0.3f, 0.6f, w.slip));
                    main.startColor = c;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f + speed * 0.04f);
                    ps.transform.rotation = Quaternion.LookRotation(Vector3.up, vc.transform.forward);
                }
                if (spray[i] != null)
                {
                    var ps = spray[i];
                    bool wet = (w.inPuddle && w.grounded && speed > 5f) || (WorldConditions.Wetness > 0.5f && w.grounded && speed > 14f);
                    var em = ps.emission;
                    em.rateOverTime = wet ? Mathf.Clamp(speed * 3f, 0f, 90f) * partScale * (w.inPuddle ? 1f : 0.35f) : 0f;
                    ps.transform.position = w.contactPoint + Vector3.up * 0.05f;
                    ps.transform.rotation = Quaternion.LookRotation(Vector3.up + vc.transform.right * (w.right ? 0.5f : -0.5f));
                    var m = ps.main; m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f + speed * 0.1f);
                }
                // sparks when the chassis bottoms out
                if (full && w.grounded && w.compression >= vc.Stats.def.travel * 0.985f && speed > 9f)
                    EmitSparks(w.contactPoint + Vector3.up * 0.08f, -vc.transform.forward + Vector3.up * 0.3f, 4);
            }

            if (full && exhaustSmoke != null)
            {
                var em = exhaustSmoke.emission;
                em.rateOverTime = (vc.Input.throttle > 0.2f ? 14f : 4f) * partScale;
                var em2 = heatGlow.emission;
                float tgt = Mathf.Clamp01(vc.Engine.boost * 1.2f + vc.Input.throttle * 0.3f);
                heat = Mathf.MoveTowards(heat, tgt * (vc.Stats.turboBoost > 0.05f ? 1f : 0.35f), (tgt > heat ? 0.5f : 0.15f) * dt);
                em2.rateOverTime = heat > 0.3f ? heat * 12f * partScale : 0f;
            }
        }
    }
}
