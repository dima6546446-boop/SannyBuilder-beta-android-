using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>Engine + gearbox + turbo model. Pure C#, driven by VehicleController each physics step.</summary>
    public class Drivetrain
    {
        // configuration
        public float idleRpm = 900f, redline = 6800f, peakTorque = 160f, peakRpm = 4200f;
        public float finalDrive = 3.9f, wheelRadius = 0.3f, reverseRatio = 3.5f, efficiency = 0.88f;
        public float turboBoost;           // extra torque fraction at full boost
        public float[] gears = { 3.5f, 2.1f, 1.4f, 1.05f, 0.82f };
        public bool automatic = true;

        // state
        public int gear = 1;               // -1 reverse, 0 neutral, 1..n
        public float rpm = 900f;
        public float boost;                // 0..1
        public float shiftTimer;
        public bool limiter;
        public float throttleApplied;
        public float clutchKickTimer;
        public float engineTorqueNm;

        public event System.Action Shifted;
        public event System.Action BlowOff;
        public event System.Action Backfired;

        private float prevThrottle;
        private float limiterTimer;
        private float shiftCooldown;
        private float backfireCooldown;

        public int GearCount { get { return gears.Length; } }

        public float Ratio
        {
            get
            {
                if (gear == 0) return 0f;
                if (gear < 0) return -reverseRatio * finalDrive;
                return gears[Mathf.Clamp(gear - 1, 0, gears.Length - 1)] * finalDrive;
            }
        }

        public float TorqueFactor(float r)
        {
            if (r < idleRpm) return 0.55f;
            if (r < peakRpm)
            {
                float x = Mathf.Clamp01((r - idleRpm) / (peakRpm - idleRpm));
                return Mathf.Lerp(0.62f, 1f, x * x * (3f - 2f * x));
            }
            float y = Mathf.Clamp01((r - peakRpm) / Mathf.Max(100f, redline - peakRpm));
            return Mathf.Lerp(1f, 0.68f, y);
        }

        public void Configure(VehicleStats s)
        {
            var d = s.def;
            idleRpm = d.idleRpm; redline = s.redline; peakTorque = s.peakTorque; peakRpm = Mathf.Min(d.peakTorqueRpm + (s.redline - d.redlineRpm) * 0.6f, redline * 0.85f);
            finalDrive = d.finalDrive; wheelRadius = d.wheelRadius; reverseRatio = d.reverseRatio; gears = d.gearRatios;
            turboBoost = s.turboBoost;
            gear = 1; rpm = idleRpm;
        }

        public void KickClutch() { clutchKickTimer = 0.28f; }

        /// <summary>Advances the model. Returns total drive force (N, at the tyre contact patches, signed along +forward).</summary>
        public float Step(float dt, float throttle, float drivenWheelSpeed, float forwardSpeed, bool reverseIntent, bool shiftUp, bool shiftDown, bool holdGear)
        {
            // --- gear selection ---
            if (shiftTimer > 0f) shiftTimer -= dt;
            if (shiftCooldown > 0f) shiftCooldown -= dt;
            if (clutchKickTimer > 0f) clutchKickTimer -= dt;

            if (reverseIntent && forwardSpeed < 1.2f && gear != -1) { gear = -1; }
            else if (!reverseIntent && gear == -1 && throttle > 0.05f && forwardSpeed > -1.2f) { gear = 1; }

            float wheelRpm = Mathf.Abs(drivenWheelSpeed) / wheelRadius * 9.5493f;   // m/s -> rpm
            float ratioAbs = Mathf.Abs(Ratio);

            if (gear > 0)
            {
                if (automatic)
                {
                    if (shiftCooldown <= 0f)
                    {
                        float upAt = redline * (holdGear ? 0.97f : (throttle > 0.85f ? 0.93f : 0.80f));
                        float downAt = redline * (throttle > 0.9f ? 0.46f : 0.34f);
                        if (rpm > upAt && gear < gears.Length) DoShift(gear + 1);
                        else if (gear > 1 && rpm < downAt && !holdGear) DoShift(gear - 1);
                    }
                }
                else
                {
                    if (shiftUp && gear < gears.Length) DoShift(gear + 1);
                    else if (shiftDown && gear > 1) DoShift(gear - 1);
                }
            }

            // --- rpm ---
            ratioAbs = Mathf.Abs(Ratio);
            float coupled = wheelRpm * ratioAbs;
            float revRange = redline * 0.6f - idleRpm;
            float freeRev = idleRpm + throttle * Mathf.Max(0f, revRange);
            float clutch = Mathf.Clamp01((Mathf.Abs(forwardSpeed) + 0.001f) / (gear == 1 || gear == -1 ? 3.5f : 0.8f));
            if (shiftTimer > 0f) clutch *= 0.2f;
            float targetRpm = Mathf.Lerp(freeRev, Mathf.Max(coupled, idleRpm), clutch);
            if (clutchKickTimer > 0f) targetRpm = Mathf.Max(targetRpm, redline * 0.88f);
            float slew = targetRpm > rpm ? (6500f + throttle * 9000f) : 7500f;
            rpm = Mathf.MoveTowards(rpm, targetRpm, slew * dt);
            rpm = Mathf.Clamp(rpm, idleRpm * 0.8f, redline * 1.04f);

            // --- limiter ---
            if (rpm >= redline) { limiter = true; limiterTimer = 0.07f; }
            if (limiter) { limiterTimer -= dt; if (limiterTimer <= 0f && rpm < redline * 0.985f) limiter = false; }

            // --- turbo ---
            float boostTarget = 0f;
            if (turboBoost > 0.01f)
                boostTarget = throttle * Mathf.Clamp01((rpm - redline * 0.33f) / (redline * 0.35f));
            float boostRate = boostTarget > boost ? 1.15f : 3.2f;
            float before = boost;
            boost = Mathf.MoveTowards(boost, boostTarget, boostRate * dt);
            if (turboBoost > 0.01f && before > 0.55f && throttle < 0.15f && prevThrottle > 0.5f && BlowOff != null) BlowOff();

            // --- backfire on lift at high revs or on gear change ---
            if (backfireCooldown > 0f) backfireCooldown -= dt;
            if (backfireCooldown <= 0f && rpm > redline * 0.6f && ((prevThrottle > 0.6f && throttle < 0.2f) || (limiter && throttle > 0.5f && turboBoost > 0.2f)))
            {
                backfireCooldown = 0.22f;
                if (Backfired != null) Backfired();
            }
            prevThrottle = throttle;

            // --- torque ---
            float t = throttle;
            if (limiter || shiftTimer > 0f) t = 0f;
            float tf = TorqueFactor(rpm);
            float torque = peakTorque * tf * (1f + boost * turboBoost) * t;
            if (clutchKickTimer > 0f) torque *= 1.7f;
            float engineBrake = -peakTorque * 0.10f * Mathf.Clamp01(rpm / redline) * (1f - t);
            engineTorqueNm = torque + engineBrake;
            throttleApplied = t;

            if (gear == 0) return 0f;
            float sign = gear < 0 ? -1f : 1f;
            // wheel-side force; engine braking only acts when rolling
            float force = engineTorqueNm * ratioAbs * efficiency / wheelRadius * sign;
            if (t <= 0.01f && Mathf.Abs(forwardSpeed) < 1.5f) force = 0f;
            return force;
        }

        private void DoShift(int g)
        {
            gear = Mathf.Clamp(g, 1, gears.Length);
            shiftTimer = 0.12f;
            shiftCooldown = 0.45f;
            if (Shifted != null) Shifted();
        }
    }
}
