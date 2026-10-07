// Все ключевые константы физики — один в один с rcd/src/physics/config.js (браузерная версия).
// Меняйте здесь, чтобы подкрутить ощущение дрифта.
using System;

namespace Zanos.Core
{
    public static class Phys
    {
        public const double StepHz = 240;          // фиксированный шаг физики (не зависит от FPS)
        public const double MaxFrameDt = 0.1;
        public const double Gravity = 9.81;
        public const double AirDensity = 1.2;
        public const double RollingResistance = 0.014;

        // --- шины ---
        public const double MuScale = 1.22;
        public const double SlipAnglePeak = 0.145;
        public const double SlipRatioPeak = 0.13;
        public const double SlideFloor = 0.76;
        public const double SlideFloorRear = 0.82;
        public const double Falloff = 1.25;
        public const double MinSpeedLat = 1.2;
        public const double MinSpeedLong = 2.2;
        public const double LatForceCap = 0.55;

        // --- кузов ---
        public const double InertiaFactor = 0.54;
        public const double LoadFilterTau = 0.085;
        public const double RollCgScale = 1.0;
        public const double YawDamping = 0.06;

        // --- привод ---
        public const double Efficiency = 0.90;
        public const double PowerMul = 1.9;
        public const double EngineInertia = 0.22;
        public const double WheelInertia = 1.9;
        public const double EngineBrake = 0.10;
        public const double ShiftTime = 0.14;
        public const double UpshiftRpmFrac = 0.93;
        public const double DownshiftRpmFrac = 0.40;
        public const double LaunchRpm = 3600;
        public const double TurboSpool = 1.1;
        public const double TurboDecay = 3.0;
        public const double ClutchKickTime = 0.30;
        public const double ClutchKickMul = 1.9;
        public const double LsdBiasStiffness = 900;

        // --- руль и помощники ---
        public const double SteerRate = 4.2;
        public const double SteerSpeedRef = 38;
        public const double SteerMinLockFrac = 0.42;
        public const double FullLockSlideDeg = 24;
        public const double AssistGain = 0.92;
        public const double AssistThresholdDeg = 3;
        public const double AssistRangeDeg = 8;
        public const double YawDampGain = 0.045;
        public const double AngleLimitBoost = 0.16;
        public const double AngleLimitStartDeg = 24;
        public const double AngleLimitRangeDeg = 28;
        public const double YawSoftMax = 0.85;
        public const double YawSoftGain = 11;
        public const double AngleThrottleCut = 1.45;
        public const double AngleCutStartDeg = 28;
        public const double AngleCutRangeDeg = 16;
        public const double SpinGuardDeg = 62;

        public const double HandbrakeTorque = 3600;
        public const double BrakeBias = 0.66;

        // --- столкновения ---
        public const double Restitution = 0.22;
        public const double Friction = 0.35;
    }

    /// <summary>Покрытия: сцепление, сопротивление качению, цвет дыма (RGB), оставляет ли следы.</summary>
    public sealed class Surface
    {
        public readonly string Name; public readonly double Grip, Roll; public readonly int SmokeRgb; public readonly bool Mark;
        Surface(string n, double g, double r, int s, bool m) { Name = n; Grip = g; Roll = r; SmokeRgb = s; Mark = m; }

        public static readonly Surface Asphalt = new Surface("asphalt", 1.00, 1.0, 0xe8e8e8, true);
        public static readonly Surface Concrete = new Surface("concrete", 0.96, 1.0, 0xdddddd, true);
        public static readonly Surface Wet = new Surface("wet", 0.72, 1.2, 0xcfd6dc, false);
        public static readonly Surface Gravel = new Surface("gravel", 0.62, 2.6, 0xb9a98a, false);
        public static readonly Surface Dirt = new Surface("dirt", 0.66, 2.2, 0xa88d68, false);
        public static readonly Surface Grass = new Surface("grass", 0.55, 3.2, 0x8f9a70, false);
        public static readonly Surface Snow = new Surface("snow", 0.35, 2.0, 0xf2f6fa, false);

        public static Surface ByName(string n)
        {
            switch (n)
            {
                case "concrete": return Concrete; case "wet": return Wet; case "gravel": return Gravel;
                case "dirt": return Dirt; case "grass": return Grass; case "snow": return Snow; default: return Asphalt;
            }
        }
        /// <summary>Погода превращает покрытия (как в Session.js).</summary>
        public static Surface Weathered(Surface s, string weather)
        {
            if (weather == "rain") return (s == Asphalt || s == Concrete) ? Wet : s;
            if (weather == "snow") return (s == Asphalt || s == Concrete || s == Grass || s == Wet) ? Snow : s;
            return s;
        }
    }

    public static class MathUtil
    {
        public static double Clamp(double v, double a, double b) { return v < a ? a : v > b ? b : v; }
        public static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
        public static double Smooth(double t) { t = Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
        public static double Sign(double v) { return v > 0 ? 1 : v < 0 ? -1 : 0; }
    }
}
