using System;

namespace Zanos.Core
{
    /// <summary>Комбинированная модель шины (эллипс трения). Порт rcd/src/physics/tire.js.</summary>
    public struct TireOut { public double Fx, Fy, Kappa, Alpha, S; }

    public static class Tire
    {
        static double Grip(double s, double floor)
        {
            if (s < 1) return 2 * s - s * s;
            return floor + (1 - floor) * Math.Exp(-(s - 1) * Phys.Falloff);
        }

        /// <summary>fx — вперёд, fy — вправо (в осях колеса). wheelSurfSpeed = ω·R.</summary>
        public static void Force(ref TireOut o, double N, double mu, double uLong, double uLat, double wheelSurfSpeed, bool rear, double floorBoost)
        {
            double kappa = (wheelSurfSpeed - uLong) / Math.Max(Math.Abs(uLong), Phys.MinSpeedLong);
            double alpha = Math.Atan2(uLat, Math.Max(Math.Abs(uLong), Phys.MinSpeedLat));
            double kn = kappa / Phys.SlipRatioPeak, an = alpha / Phys.SlipAnglePeak;
            double s = Math.Sqrt(kn * kn + an * an);
            o.Kappa = kappa; o.Alpha = alpha; o.S = s;
            if (s < 1e-6) { o.Fx = 0; o.Fy = 0; return; }
            double g = Grip(s, (rear ? Phys.SlideFloorRear : Phys.SlideFloor) + floorBoost);
            double f = mu * N * g;
            o.Fx = f * kn / s;
            o.Fy = -f * an / s;
        }
    }
}
