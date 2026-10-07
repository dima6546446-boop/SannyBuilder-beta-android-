using UnityEngine;
using RussianDrift.Core;
using RussianDrift.Vehicle;
using RussianDrift.World;

namespace RussianDrift.AI
{
    /// <summary>
    /// Path-following drift driver. Steering command = drift angle + clamped heading error to a look-ahead point,
    /// so the same law works both gripping and sliding. Speed comes from path curvature; slides are triggered with a clutch kick + handbrake tap.
    /// </summary>
    public class AIDriver : MonoBehaviour, IVehicleInputSource
    {
        public Difficulty difficulty = Difficulty.Normal;
        public bool racing;                    // false = hold position
        public float rubberBand;               // + faster / - slower from the director

        private VehicleController vc;
        private TrackPath path;
        private PathProgress prog;
        private float skill = 0.5f;
        private float handbrakeTimer, kickCooldown, stuckTimer, noise, noiseT;
        private float steerSmooth;
        private float aggression;

        public PathProgress Progress { get { return prog; } }
        public float Skill { get { return skill; } }

        public void Setup(VehicleController v, TrackPath p, Difficulty d, float startDistance)
        {
            vc = v; path = p; difficulty = d;
            switch (d)
            {
                case Difficulty.Easy: skill = 0.28f; break;
                case Difficulty.Normal: skill = 0.55f; break;
                case Difficulty.Hard: skill = 0.8f; break;
                default: skill = 1f; break;
            }
            aggression = Mathf.Lerp(0.5f, 1f, skill);
            prog = new PathProgress(p);
            Vector3 t;
            Vector3 sp = p.Sample(startDistance, out t);
            prog.ResetAt(sp, p.closed);
            // AI uses precise manual steering; yaw damping assist stays on to avoid spins
            vc.Assists.steeringAssist = false;
            vc.Assists.driftAssist = true;
            vc.Assists.abs = true; vc.Assists.tcs = false; vc.Assists.automatic = true;
            vc.Engine.automatic = true;
            vc.InputSource = this;
        }

        public VehicleInputState Read() { return Compute(Time.fixedDeltaTime); }

        private VehicleInputState Compute(float dt)
        {
            var s = new VehicleInputState();
            if (vc == null || path == null || !racing) { s.brake = 1f; s.handbrake = true; return s; }

            Transform tr = vc.transform;
            prog.Update(tr.position);
            float speed = vc.SpeedMs;
            float dist = path.DistanceAt(prog.index);

            // ---- look-ahead & steering ----
            float look = Mathf.Clamp(7f + speed * (0.5f - 0.1f * skill), 9f, 38f);
            Vector3 tgt = path.Sample(dist + look);
            Vector3 toT = tgt - tr.position; toT.y = 0f;
            Vector3 desired = toT.sqrMagnitude > 0.01f ? toT.normalized : tr.forward;
            Vector3 vel = vc.Body.Vel(); vel.y = 0f;
            Vector3 vdir = vel.magnitude > 3f ? vel.normalized : Vector3.ProjectOnPlane(tr.forward, Vector3.up).normalized;
            float e = Vector3.SignedAngle(vdir, desired, Vector3.up);              // velocity needs to rotate by e
            float fwdErr = Vector3.SignedAngle(Vector3.ProjectOnPlane(tr.forward, Vector3.up), desired, Vector3.up);
            float drift = vc.ForwardSpeed > 2f ? vc.DriftAngle : 0f;
            float delta;
            if (speed < 4f) delta = fwdErr;
            else delta = drift + Mathf.Clamp(e * 1.15f, -28f, 28f);

            // keep inside the track edges
            float lat = prog.lateral;
            float edge = path.width * 0.5f - 2f;
            if (Mathf.Abs(lat) > edge) delta += Mathf.Sign(-lat) * Mathf.Clamp((Mathf.Abs(lat) - edge) * 3f, 0f, 14f);

            noiseT += dt;
            if (noiseT > 0.35f) { noiseT = 0f; noise = (Random.value - 0.5f) * (1f - skill) * 9f; }
            delta += noise;

            float maxA = Mathf.Max(8f, vc.MaxSteerNow);
            float steerCmd = Mathf.Clamp(delta / maxA, -1f, 1f);
            steerSmooth = Mathf.MoveTowards(steerSmooth, steerCmd, (3.5f + 4f * skill) * dt);
            s.steer = steerSmooth;

            // ---- speed plan from curvature ahead ----
            float worst = 0f;
            for (float a = 8f; a <= 8f + Mathf.Clamp(speed * 2.2f, 25f, 80f); a += 8f)
            {
                float ang = Mathf.Abs(path.Curvature(Mathf.Clamp(prog.index + Mathf.RoundToInt(a / 4f), 0, path.Count - 1), 9f));
                float radius = ang > 1f ? 18f / (ang * Mathf.Deg2Rad) : 400f;
                float aLat = Mathf.Lerp(7.5f, 12.5f, skill) * Mathf.Lerp(0.85f, 1f, WorldConditions.GripMultiplier);
                float vmax = Mathf.Sqrt(aLat * radius);
                float brakeDist = a;
                // allow arriving at vmax after braking
                float vAllowed = Mathf.Sqrt(vmax * vmax + 2f * 7f * brakeDist);
                worst = worst == 0f ? vAllowed : Mathf.Min(worst, vAllowed);
            }
            float vTarget = Mathf.Clamp(worst, 8f, Mathf.Lerp(32f, 55f, skill)) * (1f + rubberBand);
            if (vc.IsGrounded == false) vTarget = speed;

            float throttle = Mathf.Clamp01((vTarget - speed) * 0.45f + (Mathf.Abs(drift) > 12f ? 0.28f : 0f));
            float brake = speed > vTarget + 3.5f ? Mathf.Clamp01((speed - vTarget - 3f) * 0.18f) : 0f;
            if (brake > 0f) throttle = 0f;
            s.throttle = throttle; s.brake = brake;

            // ---- slide initiation: clutch kick + short handbrake at corner entry ----
            kickCooldown -= dt;
            if (handbrakeTimer > 0f) { handbrakeTimer -= dt; s.handbrake = true; s.clutchKick = true; }
            float aheadTurn = Mathf.Abs(path.Curvature(Mathf.Clamp(prog.index + 7, 0, path.Count - 1), 9f));
            if (kickCooldown <= 0f && aheadTurn > 32f && speed > 15f && Mathf.Abs(drift) < 10f && Random.value < 0.06f * aggression && difficulty != Difficulty.Easy)
            {
                handbrakeTimer = 0.28f; kickCooldown = 2.2f;
                s.clutchKick = true;
            }

            // ---- recovery ----
            if (speed < 1.2f && s.throttle > 0.3f) stuckTimer += dt; else stuckTimer = 0f;
            if (stuckTimer > 3.2f)
            {
                stuckTimer = 0f;
                Vector3 t2; Vector3 p2 = path.Sample(dist + 6f, out t2);
                vc.Teleport(p2 + Vector3.up * 0.7f, Quaternion.LookRotation(t2, Vector3.up));
                prog.ResetAt(p2, false);
            }
            return s;
        }
    }
}
