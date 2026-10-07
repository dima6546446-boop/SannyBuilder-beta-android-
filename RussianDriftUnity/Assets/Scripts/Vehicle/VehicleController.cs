using System;
using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    public class WheelState
    {
        public WheelVisual visual;
        public bool front, right, driven;
        public Vector3 anchorLocal;
        public float radius;
        public bool grounded;
        public float compression, compressionPrev;
        public float normalLoad;
        public float springK, springC;
        public Vector3 contactPoint, contactNormal;
        public float slip;              // 0..1 for effects
        public float slipSpeed;         // m/s of sliding
        public float spinSpeed;         // m/s of excess wheel speed
        public float rollSpeed;         // wheel surface speed (signed)
        public bool locked;
        public float steerAngle;
        public float spinAngle;
        public bool inPuddle;
        public SurfaceType surface = SurfaceType.Asphalt;
        public float surfaceGrip = 1f;
        public float visualCompression;
        public float latForce, longForce;
    }

    [Serializable]
    public class AssistSettings
    {
        public bool steeringAssist = true;
        public bool driftAssist = true;
        public bool abs = true;
        public bool tcs = false;
        public bool automatic = true;
    }

    /// <summary>
    /// Custom arcade vehicle model: raycast suspension, friction-circle tyre forces with slip-angle falloff,
    /// weight transfer from spring loads, handbrake, clutch kick, turbo, drift assists.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        public const float Gravity = 9.81f;

        public VehicleStats Stats { get; private set; }
        public Drivetrain Engine { get; private set; }
        public AssistSettings Assists = new AssistSettings();
        public CarModel Model { get; private set; }
        public Rigidbody Body { get; private set; }
        public WheelState[] Wheels { get; private set; }
        public IVehicleInputSource InputSource;
        public bool IsPlayer;
        public bool Frozen;                 // countdown / pause: holds the car with the brakes
        public LayerMask groundMask = ~0;

        // ---- live state (read by HUD, audio, effects, scoring, AI) ----
        public float SpeedMs { get; private set; }
        public float SpeedKmh { get { return SpeedMs * 3.6f; } }
        public float ForwardSpeed { get; private set; }
        public float DriftAngle { get; private set; }          // signed deg, + = velocity right of nose
        public float AbsDriftAngle { get { return Mathf.Abs(DriftAngle); } }
        public float RearSlip { get; private set; }            // 0..1
        public float FrontSlip { get; private set; }
        public float YawRate { get; private set; }             // deg/s
        public float LateralG { get; private set; }
        public float SteerAngle { get; private set; }
        public int GroundedCount { get; private set; }
        public bool IsGrounded { get { return GroundedCount >= 2; } }
        public VehicleInputState Input { get; private set; }
        public bool BrakeLightOn { get; private set; }
        public bool ReverseOn { get { return Engine.gear < 0; } }
        public float TotalDistance { get; private set; }
        public bool Flipped { get; private set; }
        public float SteerInputSmoothed { get; private set; }

        public event Action<Collision> Collided;

        private static readonly RaycastHit[] hitBuffer = new RaycastHit[10];
        private static readonly Dictionary<int, SurfaceInfo> surfaceCache = new Dictionary<int, SurfaceInfo>();
        private float flippedTimer;
        private float distanceAccum;
        private float steerAngleCur;
        private float airTime;
        private bool prevKick;

        // ------------------------------------------------------------------ setup

        public void Initialize(VehicleStats stats, CarModel model)
        {
            Stats = stats;
            Model = model;
            Body = GetComponent<Rigidbody>();
            var d = stats.def;

            Engine = new Drivetrain();
            Engine.Configure(stats);
            Engine.automatic = Assists.automatic;

            // --- rigidbody ---
            Body.mass = stats.mass;
            Body.SetDamping(0.0f, 0.35f);
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 14f;

            float restH = d.comHeight + (stats.rideHeight - 0.17f);
            float frontShare = 0.52f;

            // --- wheels ---
            var wv = model.wheels;
            Wheels = new WheelState[wv.Length];
            for (int i = 0; i < wv.Length; i++)
            {
                var w = new WheelState();
                w.visual = wv[i];
                w.front = wv[i].front; w.right = wv[i].right;
                w.radius = d.wheelRadius;
                float massShare = stats.mass * (w.front ? frontShare : 1f - frontShare) * 0.5f;
                float omega = 2f * Mathf.PI * stats.springFreq;
                w.springK = massShare * omega * omega;
                w.springC = 2f * stats.dampingRatio * Mathf.Sqrt(w.springK * massShare);
                float cRest = Gravity / (omega * omega);
                float anchorY = d.wheelRadius + d.travel - cRest - restH;
                w.anchorLocal = new Vector3(wv[i].x, anchorY, wv[i].z);
                w.driven = stats.drive == DriveType.AWD || !w.front;
                w.compression = w.compressionPrev = cRest;
                w.visualCompression = cRest;
                Wheels[i] = w;
            }

            // inertia (a little over a solid box for a heavier, more stable feel)
            float L = d.length, W = d.width, H = d.height;
            float m = stats.mass;
            Body.centerOfMass = Vector3.zero;
            Body.inertiaTensor = new Vector3(m / 12f * (H * H + L * L), m / 12f * (W * W + L * L) * 0.95f, m / 12f * (W * W + H * H)) * 1.15f;
            Body.inertiaTensorRotation = Quaternion.identity;

            Engine.Shifted += OnShift;
        }

        public void SetAssists(GameSettings s)
        {
            Assists.steeringAssist = s.steeringAssist;
            Assists.driftAssist = s.driftAssist;
            Assists.abs = s.abs;
            Assists.tcs = s.tcs;
            Assists.automatic = s.autoGearbox;
            if (Engine != null) Engine.automatic = s.autoGearbox;
        }

        public Action ShiftedEvent;
        private void OnShift() { if (ShiftedEvent != null) ShiftedEvent(); }

        // ------------------------------------------------------------------ physics

        private VehicleInputState ReadInput()
        {
            VehicleInputState s = default(VehicleInputState);
            if (InputSource != null) s = InputSource.Read();
            if (Frozen) { s.throttle = 0f; s.brake = 1f; s.handbrake = true; s.steer = 0f; }
            s.throttle = Mathf.Clamp01(s.throttle);
            s.brake = Mathf.Clamp01(s.brake);
            s.steer = Mathf.Clamp(s.steer, -1f, 1f);
            return s;
        }

        private static SurfaceInfo SurfaceOf(Collider c)
        {
            int id = c.GetInstanceID();
            SurfaceInfo s;
            if (surfaceCache.TryGetValue(id, out s)) return s;
            c.TryGetComponent<SurfaceInfo>(out s);
            surfaceCache[id] = s;
            return s;
        }

        public static void ClearSurfaceCache() { surfaceCache.Clear(); }

        private void FixedUpdate()
        {
            if (Wheels == null) return;
            float dt = Time.fixedDeltaTime;
            var inp = ReadInput();
            Input = inp;

            Transform tr = transform;
            Vector3 vel = Body.Vel();
            Vector3 localVel = tr.InverseTransformDirection(vel);
            ForwardSpeed = localVel.z;
            SpeedMs = vel.magnitude;
            YawRate = Body.angularVelocity.y * Mathf.Rad2Deg;
            LateralG = Mathf.Clamp(localVel.x * Body.angularVelocity.y / Gravity, -4f, 4f);

            // drift angle between heading and velocity (planar)
            Vector3 fwdFlat = Vector3.ProjectOnPlane(tr.forward, Vector3.up).normalized;
            Vector3 velFlat = Vector3.ProjectOnPlane(vel, Vector3.up);
            if (velFlat.magnitude > 2.0f && ForwardSpeed > 0f && fwdFlat.sqrMagnitude > 0.1f)
                DriftAngle = Vector3.SignedAngle(fwdFlat, velFlat.normalized, Vector3.up);
            else
                DriftAngle = Mathf.MoveTowards(DriftAngle, 0f, 180f * dt);

            // ---- pedals / gearing semantics ----
            bool reverseIntent = inp.brake > 0.1f && inp.throttle < 0.05f && ForwardSpeed < 1.2f;
            if (Engine.gear == -1 && inp.brake > 0.05f && inp.throttle < 0.05f) reverseIntent = true;
            float driveInput = reverseIntent ? inp.brake : inp.throttle;
            float brakeInput = reverseIntent ? 0f : inp.brake;
            if (Engine.gear == -1 && inp.throttle > 0.05f && ForwardSpeed < -0.5f) { brakeInput = inp.throttle; driveInput = 0f; }
            BrakeLightOn = brakeInput > 0.05f || (inp.handbrake && SpeedMs > 1f);

            if (inp.clutchKick && !prevKick) Engine.KickClutch();
            prevKick = inp.clutchKick;

            // ---- TCS ----
            float tcsFactor = 1f;
            float drivenSpin = 0f; int dn = 0;
            for (int i = 0; i < Wheels.Length; i++) if (Wheels[i].driven && Wheels[i].grounded) { drivenSpin += Wheels[i].spinSpeed; dn++; }
            if (dn > 0) drivenSpin /= dn;
            if (Assists.tcs && SpeedMs > 2f) tcsFactor = 1f - Mathf.Clamp01((drivenSpin / Mathf.Max(3f, SpeedMs * 0.25f) - 0.4f) * 1.6f) * 0.85f;

            // ---- steering ----
            UpdateSteering(inp, dt);

            // ---- suspension ----
            GroundedCount = 0;
            float totalN = 0f;
            for (int i = 0; i < Wheels.Length; i++)
            {
                var w = Wheels[i];
                SuspensionStep(w, dt);
                if (w.grounded) { GroundedCount++; totalN += w.normalLoad; }
            }

            // ---- engine ----
            float drivenSpeed = 0f; int ds = 0;
            for (int i = 0; i < Wheels.Length; i++) if (Wheels[i].driven && Wheels[i].grounded) { drivenSpeed += Wheels[i].rollSpeed; ds++; }
            drivenSpeed = ds > 0 ? drivenSpeed / ds : ForwardSpeed;
            bool holdGear = Assists.driftAssist && AbsDriftAngle > 10f && inp.throttle > 0.5f;
            float driveForce = Engine.Step(dt, driveInput * tcsFactor, drivenSpeed, ForwardSpeed, reverseIntent, inp.shiftUp, inp.shiftDown, holdGear);
            // un-attenuate for rpm display purposes: Engine already used tcs-scaled throttle

            // drive split
            float frontShare = Stats.drive == DriveType.AWD ? 0.35f : 0f;
            float rearShare = 1f - frontShare;

            // ---- tyre forces ----
            float meffBase = Body.mass * 0.25f;
            float rearSlipSum = 0f, frontSlipSum = 0f; int rc = 0, fc = 0;
            float mu0 = Stats.grip * WorldConditions.GripMultiplier;
            float brakeTotal = brakeInput * Stats.brakeForce;
            bool parked = inp.throttle < 0.02f && inp.brake < 0.02f && SpeedMs < 0.6f;

            for (int i = 0; i < Wheels.Length; i++)
            {
                var w = Wheels[i];
                w.latForce = w.longForce = 0f;
                if (!w.grounded) { w.slip = Mathf.MoveTowards(w.slip, 0f, 4f * dt); w.spinSpeed = 0f; w.rollSpeed = Mathf.Lerp(w.rollSpeed, ForwardSpeed + driveInput * 8f, 3f * dt); continue; }

                Vector3 n = w.contactNormal;
                Vector3 fdir = tr.forward;
                if (w.front) fdir = Quaternion.AngleAxis(w.steerAngle, tr.up) * tr.forward;
                Vector3 f = Vector3.ProjectOnPlane(fdir, n).normalized;
                Vector3 r = Vector3.Cross(n, f);
                Vector3 v = Body.GetPointVelocity(w.contactPoint);
                float vF = Vector3.Dot(v, f), vR = Vector3.Dot(v, r);
                float N = w.normalLoad;
                float meff = meffBase;

                float mu = mu0;
                if (w.inPuddle) mu *= 0.78f;
                mu *= w.surfaceGrip;
                float latMu = mu, longMu = mu;

                // axle drive force
                float axleShare = w.front ? frontShare : rearShare;
                float baseDrive = driveForce * axleShare * 0.5f;
                float drive = baseDrive;
                if (w.driven && Mathf.Abs(baseDrive) > 1f)
                {
                    // differential: open diff is limited by the weaker wheel on the axle
                    float minGrip = AxleMinGrip(w.front, mu0);
                    float limited = Mathf.Sign(baseDrive) * Mathf.Min(Mathf.Abs(baseDrive), minGrip * 1.15f);
                    drive = Mathf.Lerp(limited, baseDrive, Stats.diffLock);
                }
                else drive = 0f;

                // brakes
                float bias = w.front ? 0.68f : 0.32f;      // front-biased: keeps the rears free to carry lateral load under braking
                float brake = brakeTotal * bias * 0.5f;
                bool hb = inp.handbrake && !w.front;
                w.locked = false;
                if (hb)
                {
                    brake += Body.mass * Gravity * 0.45f;
                    latMu *= 0.34f;
                    w.locked = true;
                }
                if (!Assists.abs && brakeInput > 0.85f && SpeedMs > 6f && !w.front) { latMu *= 0.80f; w.locked = true; }
                if (!w.front && SpeedMs > 8f)
                {
                    // power-slide: throttle (and a clutch kick) unloads the rear tyres' lateral grip once the car is already sliding
                    float gateAng = Mathf.Clamp01((AbsDriftAngle - 4f) / 12f);
                    float loss = (0.30f + 0.15f * Stats.diffLock) * (Stats.drive == DriveType.AWD ? 0.6f : 1f);
                    float cut = loss * Engine.throttleApplied * gateAng;
                    if (Engine.clutchKickTimer > 0f) cut = Mathf.Max(cut, 0.5f);
                    latMu *= 1f - Mathf.Clamp(cut, 0f, 0.6f);
                }
                if (parked) brake += Body.mass * Gravity * 0.5f;
                float brakeLimit = Mathf.Abs(vF) * meff / dt;
                float brakeMag = Mathf.Min(brake, brakeLimit);
                if (Assists.abs) brakeMag = Mathf.Min(brakeMag, longMu * N * 0.95f);
                float fxWanted = drive - Mathf.Sign(vF) * brakeMag;

                // lateral demand with slip-angle falloff
                float alpha = Mathf.Atan2(Mathf.Abs(vR), Mathf.Max(Mathf.Abs(vF), 1.0f));
                float lat = LateralCurve(alpha);
                float maxLat = latMu * N;
                float fyWanted = -Mathf.Sign(vR) * maxLat * lat;
                float fyCap = Mathf.Abs(vR) * meff / dt * 0.6f;
                if (Mathf.Abs(fyWanted) > fyCap) fyWanted = Mathf.Sign(fyWanted) * fyCap;

                // friction circle
                float maxLong = longMu * N;
                float ex = maxLong > 1f ? Mathf.Abs(fxWanted) / maxLong : 0f;
                float ey = maxLat > 1f ? Mathf.Abs(fyWanted) / maxLat : 0f;
                float mag = Mathf.Sqrt(ex * ex + ey * ey);
                float fx = fxWanted, fy = fyWanted;
                if (mag > 1f) { fx /= mag; fy /= mag; }

                // wheelspin / slip bookkeeping
                float excess = Mathf.Max(0f, Mathf.Abs(drive) - Mathf.Abs(fx));
                w.spinSpeed = Mathf.Min(35f, excess / Mathf.Max(100f, maxLong) * 22f) * Mathf.Sign(drive == 0f ? 1f : drive);
                if (Mathf.Abs(drive) < 1f) w.spinSpeed = 0f;
                w.rollSpeed = w.locked ? 0f : vF + w.spinSpeed;
                float latSlip = Mathf.Clamp01((alpha - 0.10f) / 0.22f) * Mathf.Clamp01((Mathf.Abs(vR)) / 2.5f);
                float spinSlip = Mathf.Clamp01(Mathf.Abs(w.spinSpeed) / 6f);
                float lockSlip = w.locked && SpeedMs > 3f ? 1f : 0f;
                float target = Mathf.Max(latSlip, Mathf.Max(spinSlip, lockSlip));
                w.slip = Mathf.MoveTowards(w.slip, target, (target > w.slip ? 8f : 3f) * dt);
                w.slipSpeed = Mathf.Sqrt(vR * vR + w.spinSpeed * w.spinSpeed);
                w.latForce = fy; w.longForce = fx;
                if (w.front) { frontSlipSum += w.slip; fc++; } else { rearSlipSum += w.slip; rc++; }

                Vector3 force = f * fx + r * fy;
                Body.AddForceAtPosition(force, w.contactPoint + n * (w.radius * 0.35f), ForceMode.Force);
            }
            RearSlip = rc > 0 ? rearSlipSum / rc : 0f;
            FrontSlip = fc > 0 ? frontSlipSum / fc : 0f;

            // ---- aero, downforce, assists ----
            float v2 = SpeedMs * SpeedMs;
            if (SpeedMs > 0.1f)
            {
                float cd = 0.5f * 1.2f * 0.36f * (Stats.def.width * Stats.def.height * 0.78f);
                Body.AddForce(-vel.normalized * cd * v2, ForceMode.Force);
            }
            if (Stats.def.downforce > 0f) Body.AddForce(-tr.up * Stats.def.downforce * v2, ForceMode.Force);
            else Body.AddForce(-tr.up * 0.35f * v2, ForceMode.Force);

            ApplyDriftAssist(inp, dt);

            // air control & flip recovery
            if (GroundedCount == 0) airTime += dt; else airTime = 0f;
            Flipped = Vector3.Dot(tr.up, Vector3.up) < 0.25f && SpeedMs < 3f;
            flippedTimer = Flipped ? flippedTimer + dt : 0f;
            if (flippedTimer > 2.5f) { Recover(); flippedTimer = 0f; }

            // distance tracking
            distanceAccum += SpeedMs * dt;
            TotalDistance += SpeedMs * dt;
            if (IsPlayer && distanceAccum > 20f) { GameEvents.RaiseDistance(distanceAccum); distanceAccum = 0f; }
        }

        private float AxleMinGrip(bool front, float mu)
        {
            float mn = float.MaxValue;
            for (int i = 0; i < Wheels.Length; i++)
            {
                var w = Wheels[i];
                if (w.front != front) continue;
                mn = Mathf.Min(mn, w.grounded ? w.normalLoad * mu : 0f);
            }
            return mn == float.MaxValue ? 0f : mn;
        }

        /// <summary>Normalised lateral friction vs slip angle: linear to the peak, then a soft fall-off to sliding friction.</summary>
        public static float LateralCurve(float alpha)
        {
            const float peak = 0.14f, slide = 0.50f, floor = 0.72f;
            if (alpha < peak) return alpha / peak;
            float x = Mathf.Clamp01((alpha - peak) / (slide - peak));
            return Mathf.Lerp(1f, floor, x * x * (3f - 2f * x));
        }

        /// <summary>Maximum front wheel angle (deg) at the current speed, before assists. AI uses this to convert angles to input.</summary>
        public float MaxSteerNow
        {
            get
            {
                float speedFactor = Mathf.Clamp01(SpeedMs / 48f);
                float a = Mathf.Lerp(Stats.steerAngle, Stats.steerAngle * 0.32f, Mathf.Pow(speedFactor, 0.7f));
                // in a slide the full lock is available for counter-steering
                return Mathf.Lerp(a, Stats.steerAngle, Mathf.Clamp01((AbsDriftAngle - 8f) / 20f));
            }
        }

        private void UpdateSteering(VehicleInputState inp, float dt)
        {
            float maxSteer = Stats.steerAngle;
            float speedFactor = Mathf.Clamp01(SpeedMs / 48f);
            float maxAngle = MaxSteerNow;
            float target = inp.steer * maxAngle;

            if (Assists.steeringAssist && SpeedMs > 3f && ForwardSpeed > 0f)
            {
                float gate = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((AbsDriftAngle - 3f) / 9f)) * Mathf.Clamp01((SpeedMs - 3f) / 6f);
                float counter = Mathf.Clamp(DriftAngle * 0.75f, -maxSteer, maxSteer);
                target = Mathf.Clamp(counter * gate + target * (1f - 0.30f * gate), -maxSteer, maxSteer);
            }

            float rate = Mathf.Abs(target) > Mathf.Abs(steerAngleCur) ? 260f : 340f;
            rate *= Mathf.Lerp(1f, 0.6f, speedFactor);
            steerAngleCur = Mathf.MoveTowards(steerAngleCur, target, rate * dt);
            SteerAngle = steerAngleCur;
            SteerInputSmoothed = Mathf.MoveTowards(SteerInputSmoothed, inp.steer, 6f * dt);
            for (int i = 0; i < Wheels.Length; i++)
            {
                var w = Wheels[i];
                if (!w.front) { w.steerAngle = 0f; continue; }
                // Ackermann-ish: inside wheel turns slightly more
                float side = w.right ? 1f : -1f;
                float ack = steerAngleCur * side > 0f ? 1.08f : 0.96f;     // inside wheel turns more
                w.steerAngle = steerAngleCur * ack;
            }
        }

        private void SuspensionStep(WheelState w, float dt)
        {
            Transform tr = transform;
            Vector3 origin = tr.TransformPoint(w.anchorLocal);
            Vector3 down = -tr.up;
            float travel = Stats.def.travel;
            float len = travel + w.radius;
            int n = Physics.RaycastNonAlloc(origin, down, hitBuffer, len, groundMask, QueryTriggerInteraction.Ignore);
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = hitBuffer[i];
                if (h.collider.attachedRigidbody == Body) continue;
                if (h.distance < bd) { bd = h.distance; best = i; }
            }
            if (best < 0)
            {
                w.grounded = false;
                w.normalLoad = 0f;
                w.compressionPrev = w.compression;
                w.compression = Mathf.MoveTowards(w.compression, 0f, 1.5f * dt);
                w.visualCompression = Mathf.MoveTowards(w.visualCompression, 0f, 0.8f * dt);
                w.inPuddle = false;
                return;
            }
            var hit = hitBuffer[best];
            w.grounded = true;
            w.contactPoint = hit.point;
            w.contactNormal = hit.normal;
            float cRaw = travel + w.radius - hit.distance;
            float c = Mathf.Clamp(cRaw, 0f, travel);
            w.compressionPrev = w.compression;
            w.compression = c;
            float cVel = (c - w.compressionPrev) / dt;
            float force = w.springK * c + w.springC * cVel;
            if (cRaw > travel) force += w.springK * 8f * (cRaw - travel);
            if (force < 0f) force = 0f;
            w.normalLoad = force;
            w.visualCompression = Mathf.Lerp(w.visualCompression, c, 1f - Mathf.Exp(-30f * dt));

            // suspension force along the surface normal (blend with up for stability on bumpy meshes)
            Vector3 sdir = Vector3.Slerp(tr.up, hit.normal, 0.5f).normalized;
            Body.AddForceAtPosition(sdir * force, hit.point, ForceMode.Force);

            var surf = SurfaceOf(hit.collider);
            if (surf != null) { w.surface = surf.type; w.surfaceGrip = surf.grip; }
            else { w.surface = SurfaceType.Asphalt; w.surfaceGrip = 1f; }
            w.inPuddle = WorldConditions.Puddles.Count > 0 && WorldConditions.InPuddle(hit.point);
        }

        private void ApplyDriftAssist(VehicleInputState inp, float dt)
        {
            if (!Assists.driftAssist || GroundedCount < 2 || SpeedMs < 6f) return;
            Transform tr = transform;
            float ang = AbsDriftAngle;
            // damp over-rotation so a missed counter-steer does not instantly become a spin
            if (ang > 48f && ForwardSpeed > 0f)
            {
                float k = Mathf.Clamp01((ang - 48f) / 30f);
                float yaw = Body.angularVelocity.y;
                float same = Mathf.Sign(yaw) == -Mathf.Sign(DriftAngle) ? 1f : 0f;   // rotating further away from the velocity direction
                Body.AddTorque(-Vector3.up * yaw * Body.mass * 1.4f * k * same, ForceMode.Force);
            }
        }

        // ------------------------------------------------------------------ visuals

        private void Update()
        {
            if (Wheels == null || Model == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < Wheels.Length; i++)
            {
                var w = Wheels[i];
                var v = w.visual;
                if (v.steerPivot == null) continue;
                float y = w.anchorLocal.y - (Stats.def.travel - w.visualCompression);
                v.steerPivot.localPosition = new Vector3(w.anchorLocal.x, y, w.anchorLocal.z);
                float camber = Stats.camber * 5f * (w.right ? 1f : -1f);
                v.steerPivot.localRotation = Quaternion.Euler(0f, w.steerAngle, camber);
                w.spinAngle += w.rollSpeed / w.radius * Mathf.Rad2Deg * dt;
                w.spinAngle = Mathf.Repeat(w.spinAngle, 360f);
                if (v.spinPivot != null) v.spinPivot.localRotation = Quaternion.Euler(w.spinAngle, 0f, 0f);
            }
            if (Model.steeringWheel != null)
                Model.steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -SteerAngle * 4f);
        }

        // ------------------------------------------------------------------ misc

        public void Recover()
        {
            Vector3 p = transform.position + Vector3.up * 1.0f;
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            Teleport(p, Quaternion.LookRotation(f.normalized, Vector3.up));
        }

        public void Teleport(Vector3 pos, Quaternion rot)
        {
            Body.SetVel(Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            Body.position = pos;
            Body.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
            if (Engine != null) { Engine.rpm = Engine.idleRpm; Engine.gear = 1; Engine.boost = 0f; }
            steerAngleCur = 0f;
            Physics.SyncTransforms();
        }

        private void OnCollisionEnter(Collision c)
        {
            if (Collided != null) Collided(c);
            if (IsPlayer)
            {
                float impulse = c.impulse.magnitude;
                var tag = c.collider.GetComponentInParent<CollisionTag>();
                bool traffic = tag != null && tag.isTraffic;
                bool soft = tag != null && tag.isSoft;
                if (impulse > 900f) GameEvents.RaiseCollision(soft ? impulse * 0.25f : impulse, traffic);
            }
        }
    }
}
