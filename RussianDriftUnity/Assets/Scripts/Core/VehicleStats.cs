using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Final tuned numbers = CarDefinition + CarSetup upgrades. Single source for physics and garage bars.</summary>
    public struct VehicleStats
    {
        public CarDefinition def;
        public float mass;
        public float peakTorque;
        public float redline;
        public float turboBoost;       // 0 = NA, 1 = +100 % torque at full boost
        public float springFreq;
        public float dampingRatio;
        public float rideHeight;
        public float brakeForce;
        public float grip;
        public float diffLock;         // 0..1
        public DriveType drive;
        public float camber;           // 0..1
        public float steerAngle;

        public float Power01 { get { return Mathf.Clamp01((peakTorque * (1f + turboBoost)) / mass / 0.55f); } }
        public float Speed01 { get { return Mathf.Clamp01((peakTorque * (1f + turboBoost * 0.7f) * redline / 9549f * 0.9f) / mass / 0.32f); } }
        public float Handling01 { get { return Mathf.Clamp01((grip - 0.8f) / 0.5f * 0.7f + springFreq / 4f * 0.3f); } }
        public float Brake01 { get { return Mathf.Clamp01(brakeForce / mass / 20f); } }
        public float Drift01 { get { return Mathf.Clamp01(0.35f + diffLock * 0.35f + Power01 * 0.3f - (grip - 1f)); } }

        public static VehicleStats Compute(CarDefinition d, CarSetup s)
        {
            var v = new VehicleStats();
            v.def = d;
            int eng = s.engine, tur = s.turbo, sus = s.suspension, brk = s.brakes, tir = s.tires, dif = s.differential, wgt = s.weight;
            v.mass = d.mass * (1f - 0.035f * wgt);
            v.peakTorque = d.peakTorque * (1f + 0.09f * eng);
            v.redline = d.redlineRpm + 120f * eng;
            v.turboBoost = d.turboStockBoost + (tur > 0 ? 0.10f + 0.13f * tur : 0f);
            v.springFreq = d.springFrequency * (1f + 0.08f * sus);
            v.dampingRatio = d.dampingRatio + 0.02f * sus;
            v.rideHeight = Mathf.Clamp(d.rideHeight + s.clearance * 0.06f, 0.08f, 0.30f);
            v.brakeForce = d.brakeForce * (1f + 0.10f * brk);
            v.grip = d.baseGrip * (1f + 0.05f * tir) * (1f + 0.03f * s.camber);
            v.diffLock = Mathf.Clamp01(0.30f + 0.2f * dif);
            v.drive = (DriveType)Mathf.Clamp(s.drive, 0, 1);
            v.camber = s.camber;
            v.steerAngle = d.maxSteerAngle;
            return v;
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            Color c;
            if (ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out c)) return c;
            return fallback;
        }
    }
}
