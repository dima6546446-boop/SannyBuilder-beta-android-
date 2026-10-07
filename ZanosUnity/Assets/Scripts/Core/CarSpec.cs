using System;
using System.Collections.Generic;

namespace Zanos.Core
{
    /// <summary>Параметры машины (каталог + тюнинг). Поля повторяют rcd/src/game/cars.js.</summary>
    public sealed class CarSpec
    {
        public string Id, Brand, Name, Body, Desc, Color = "#b3262b";
        public int Price, Level;
        public double Mass, Wheelbase, WeightFront, TrackFront, TrackRear, CgHeight, Length, Width, Height, WheelRadius;
        public double PeakTorque, PeakRpm, Redline, IdleRpm, Turbo;
        public double[] Gears; public double ReverseRatio, FinalDrive;
        public double MaxSteerDeg, BrakeTorque, TireGrip = 1, RearGripBias = 1, RollFront = 0.55, DiffLock = 0.5, CdA = 0.7;
        public double YawQuick = 1, TailHold = 1;
        public double MaxSteer;                       // рад, считается при сборке

        public CarSpec Clone() { var c = (CarSpec)MemberwiseClone(); c.Gears = (double[])Gears.Clone(); return c; }

        /// <summary>Итоговые параметры = каталог + тюнинг (порт buildSpec из cars.js).</summary>
        public static CarSpec Build(CarSpec def, IDictionary<string, int> tuning = null)
        {
            Func<string, int> t = (k) => { int v; return tuning != null && tuning.TryGetValue(k, out v) ? v : 0; };
            var s = def.Clone();
            s.MaxSteer = def.MaxSteerDeg * Math.PI / 180.0;
            s.PeakTorque = def.PeakTorque * (1 + 0.085 * t("engine"));
            s.Redline = def.Redline + 110 * t("engine");
            s.Turbo = (def.Turbo > 0 || t("turbo") > 0) ? def.Turbo + (t("turbo") > 0 ? 0.08 + 0.1 * t("turbo") : 0) : 0;
            s.CgHeight = def.CgHeight * (1 - 0.025 * t("suspension"));
            s.RollFront = Math.Min(0.7, def.RollFront + 0.012 * t("suspension"));
            s.TireGrip = def.TireGrip * (1 + 0.04 * t("tires"));
            s.DiffLock = Math.Min(1, def.DiffLock + 0.07 * t("diff"));
            s.BrakeTorque = def.BrakeTorque * (1 + 0.09 * t("brakes"));
            s.Mass = def.Mass * (1 - 0.035 * t("weight"));
            return s;
        }

        /// <summary>Цена уровня улучшения (как upgradePrice в JS).</summary>
        public static int UpgradePrice(string part, int nextLevel)
        {
            foreach (var p in CarCatalog.Tuning)
                if (p.Id == part) return (int)(Math.Round(p.Price * Math.Pow(1.7, nextLevel - 1) / 50.0) * 50);
            return 0;
        }
    }
}
