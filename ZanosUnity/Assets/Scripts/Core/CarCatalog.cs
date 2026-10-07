// ГЕНЕРИРУЕТСЯ скриптом rcd/tools/export-unity.mjs — не редактировать вручную.
using System.Collections.Generic;

namespace Zanos.Core
{
    public static class CarCatalog
    {
        public static readonly CarSpec[] All = new CarSpec[]
        {
            new CarSpec { Id = "kopeyka", Brand = "Волжанин", Name = "Копейка", Body = "classic", Price = 0, Level = 1, Desc = "Лёгкая классика. Честный задний привод, предсказуемый занос — идеальная первая машина.",
                Mass = 1030.0, Wheelbase = 2.42, WeightFront = 0.54, TrackFront = 1.36, TrackRear = 1.34, CgHeight = 0.5,
                Length = 4.12, Width = 1.61, Height = 1.4, WheelRadius = 0.3,
                PeakTorque = 170.0, PeakRpm = 4300.0, Redline = 6600.0, IdleRpm = 900.0, Turbo = 0.0,
                Gears = new double[] { 3.75, 2.3, 1.52, 1.1, 0.86 }, ReverseRatio = 3.5, FinalDrive = 4.1,
                MaxSteerDeg = 38.0, BrakeTorque = 2300.0, TireGrip = 1.04, RearGripBias = 1.0, RollFront = 0.56, DiffLock = 0.55, CdA = 0.72,
                YawQuick = 1.0, TailHold = 1.0, Color = "#b3262b" },
            new CarSpec { Id = "pyaterka", Brand = "Волжанин", Name = "Пятёрка", Body = "sedan", Price = 14000, Level = 2, Desc = "Сбалансированный седан с отлаженной подвеской. Угол держит спокойно.",
                Mass = 1065.0, Wheelbase = 2.42, WeightFront = 0.53, TrackFront = 1.37, TrackRear = 1.35, CgHeight = 0.5,
                Length = 4.22, Width = 1.62, Height = 1.41, WheelRadius = 0.3,
                PeakTorque = 190.0, PeakRpm = 4500.0, Redline = 6900.0, IdleRpm = 900.0, Turbo = 0.0,
                Gears = new double[] { 3.65, 2.2, 1.5, 1.1, 0.85 }, ReverseRatio = 3.5, FinalDrive = 4.1,
                MaxSteerDeg = 38.0, BrakeTorque = 2400.0, TireGrip = 1.06, RearGripBias = 1.0, RollFront = 0.56, DiffLock = 0.58, CdA = 0.72,
                YawQuick = 1.0, TailHold = 1.06, Color = "#e5e5e0" },
            new CarSpec { Id = "devyatka", Brand = "Волжанин", Name = "Девятка-Д", Body = "hatch", Price = 22000, Level = 3, Desc = "Хэтчбек с задним приводом. Лёгкий нос и резкая реакция на руль.",
                Mass = 985.0, Wheelbase = 2.46, WeightFront = 0.52, TrackFront = 1.4, TrackRear = 1.38, CgHeight = 0.49,
                Length = 4.01, Width = 1.65, Height = 1.4, WheelRadius = 0.3,
                PeakTorque = 205.0, PeakRpm = 4800.0, Redline = 7200.0, IdleRpm = 900.0, Turbo = 0.0,
                Gears = new double[] { 3.6, 2.15, 1.5, 1.12, 0.88 }, ReverseRatio = 3.5, FinalDrive = 4.2,
                MaxSteerDeg = 40.0, BrakeTorque = 2300.0, TireGrip = 1.08, RearGripBias = 0.98, RollFront = 0.55, DiffLock = 0.6, CdA = 0.7,
                YawQuick = 1.12, TailHold = 0.95, Color = "#2d5fb0" },
            new CarSpec { Id = "kupe86", Brand = "Ураган", Name = "Купе-86", Body = "coupe", Price = 38000, Level = 5, Desc = "Низкое купе: точный руль, отзывчивый мотор, много угла.",
                Mass = 1130.0, Wheelbase = 2.52, WeightFront = 0.51, TrackFront = 1.46, TrackRear = 1.44, CgHeight = 0.46,
                Length = 4.28, Width = 1.69, Height = 1.3, WheelRadius = 0.31,
                PeakTorque = 240.0, PeakRpm = 5000.0, Redline = 7400.0, IdleRpm = 950.0, Turbo = 0.0,
                Gears = new double[] { 3.62, 2.19, 1.54, 1.19, 0.96, 0.78 }, ReverseRatio = 3.4, FinalDrive = 3.9,
                MaxSteerDeg = 40.0, BrakeTorque = 2700.0, TireGrip = 1.1, RearGripBias = 0.98, RollFront = 0.54, DiffLock = 0.62, CdA = 0.66,
                YawQuick = 1.1, TailHold = 1.0, Color = "#f2f2f2" },
            new CarSpec { Id = "barin", Brand = "Сибирь", Name = "Барин V8", Body = "bigsedan", Price = 52000, Level = 7, Desc = "Тяжёлый барский седан с V8. Горы момента, длинные красивые заносы.",
                Mass = 1480.0, Wheelbase = 2.8, WeightFront = 0.55, TrackFront = 1.52, TrackRear = 1.5, CgHeight = 0.55,
                Length = 4.85, Width = 1.8, Height = 1.46, WheelRadius = 0.33,
                PeakTorque = 370.0, PeakRpm = 3900.0, Redline = 6200.0, IdleRpm = 800.0, Turbo = 0.0,
                Gears = new double[] { 3.15, 1.95, 1.4, 1.0, 0.78 }, ReverseRatio = 3.2, FinalDrive = 3.7,
                MaxSteerDeg = 35.0, BrakeTorque = 3200.0, TireGrip = 1.04, RearGripBias = 1.03, RollFront = 0.58, DiffLock = 0.55, CdA = 0.78,
                YawQuick = 0.85, TailHold = 1.1, Color = "#0f3a2a" },
            new CarSpec { Id = "universal", Brand = "Бурьян", Name = "Универсал-Т", Body = "wagon", Price = 68000, Level = 9, Desc = "Дачный универсал с турбиной. Смешной снаружи, страшный на трассе.",
                Mass = 1290.0, Wheelbase = 2.62, WeightFront = 0.53, TrackFront = 1.46, TrackRear = 1.44, CgHeight = 0.52,
                Length = 4.55, Width = 1.71, Height = 1.45, WheelRadius = 0.31,
                PeakTorque = 290.0, PeakRpm = 4400.0, Redline = 6900.0, IdleRpm = 900.0, Turbo = 0.32,
                Gears = new double[] { 3.3, 2.05, 1.45, 1.09, 0.86 }, ReverseRatio = 3.3, FinalDrive = 3.9,
                MaxSteerDeg = 38.0, BrakeTorque = 2900.0, TireGrip = 1.08, RearGripBias = 1.02, RollFront = 0.56, DiffLock = 0.62, CdA = 0.76,
                YawQuick = 0.95, TailHold = 1.05, Color = "#d7b26a" },
            new CarSpec { Id = "ronin", Brand = "Ураган", Name = "Ронин", Body = "coupe", Price = 105000, Level = 12, Desc = "Турбо-купе для профи: резкая отдача, огромный угол, прощает ошибки реже.",
                Mass = 1240.0, Wheelbase = 2.55, WeightFront = 0.52, TrackFront = 1.5, TrackRear = 1.49, CgHeight = 0.45,
                Length = 4.5, Width = 1.74, Height = 1.29, WheelRadius = 0.31,
                PeakTorque = 330.0, PeakRpm = 5200.0, Redline = 7600.0, IdleRpm = 950.0, Turbo = 0.4,
                Gears = new double[] { 3.6, 2.2, 1.55, 1.18, 0.95, 0.78 }, ReverseRatio = 3.4, FinalDrive = 3.85,
                MaxSteerDeg = 42.0, BrakeTorque = 3000.0, TireGrip = 1.14, RearGripBias = 0.97, RollFront = 0.54, DiffLock = 0.68, CdA = 0.66,
                YawQuick = 1.18, TailHold = 0.93, Color = "#7a2cc0" },
            new CarSpec { Id = "raketa", Brand = "Ураган", Name = "Ракета", Body = "coupe", Price = 170000, Level = 15, Desc = "Широкое спорт-купе. Максимум мощности и сцепления — дрифт на пределе.",
                Mass = 1360.0, Wheelbase = 2.6, WeightFront = 0.51, TrackFront = 1.58, TrackRear = 1.57, CgHeight = 0.44,
                Length = 4.6, Width = 1.85, Height = 1.26, WheelRadius = 0.33,
                PeakTorque = 420.0, PeakRpm = 5200.0, Redline = 7800.0, IdleRpm = 1000.0, Turbo = 0.45,
                Gears = new double[] { 3.45, 2.2, 1.62, 1.28, 1.02, 0.83 }, ReverseRatio = 3.4, FinalDrive = 3.7,
                MaxSteerDeg = 42.0, BrakeTorque = 3600.0, TireGrip = 1.2, RearGripBias = 0.96, RollFront = 0.53, DiffLock = 0.72, CdA = 0.7,
                YawQuick = 1.25, TailHold = 0.9, Color = "#ff7a00" },
        };

        public static CarSpec ById(string id) { foreach (var c in All) if (c.Id == id) return c; return All[0]; }

        public sealed class Part { public string Id, Name, Desc; public int Max, Price; public int Rgb; }
        public static readonly Part[] Tuning = new Part[]
        {
            new Part { Id = "engine", Name = "Двигатель", Desc = "Крутящий момент и обороты", Max = 5, Price = 2200 },
            new Part { Id = "turbo", Name = "Турбо", Desc = "Давление наддува", Max = 5, Price = 2600 },
            new Part { Id = "suspension", Name = "Подвеска", Desc = "Ниже центр тяжести, быстрее реакция", Max = 5, Price = 1700 },
            new Part { Id = "tires", Name = "Шины", Desc = "Сцепление", Max = 5, Price = 1900 },
            new Part { Id = "diff", Name = "Дифференциал", Desc = "Блокировка: легче держать занос", Max = 5, Price = 1500 },
            new Part { Id = "brakes", Name = "Тормоза", Desc = "Усилие торможения", Max = 5, Price = 1400 },
            new Part { Id = "weight", Name = "Облегчение", Desc = "Меньше масса", Max = 5, Price = 2000 },
        };
        public static readonly Part[] Wheels = new Part[]
        {
            new Part { Id = "steel", Name = "Штамповки", Price = 0, Max = 0 },
            new Part { Id = "star5", Name = "Звезда-5", Price = 1200, Max = 5 },
            new Part { Id = "mesh", Name = "Сетка", Price = 2200, Max = 12 },
            new Part { Id = "deep6", Name = "Глубокие-6", Price = 3000, Max = 6 },
            new Part { Id = "split10", Name = "Раздвоенные", Price = 3800, Max = 10 },
        };
        public static readonly Part[] BodyKits = new Part[]
        {
            new Part { Id = "none", Name = "Сток", Price = 0 },
            new Part { Id = "street", Name = "Стрит", Price = 3500 },
            new Part { Id = "wide", Name = "Широкий", Price = 7500 },
            new Part { Id = "rally", Name = "Ралли", Price = 6000 },
        };
        public static readonly Part[] Spoilers = new Part[]
        {
            new Part { Id = "none", Name = "Без спойлера", Price = 0 },
            new Part { Id = "lip", Name = "Лип", Price = 900 },
            new Part { Id = "duck", Name = "Уточка", Price = 1600 },
            new Part { Id = "gt", Name = "GT-крыло", Price = 3200 },
        };
        public static readonly Part[] Neons = new Part[]
        {
            new Part { Id = "none", Name = "Без неона", Price = 0, Rgb = 0 },
            new Part { Id = "red", Name = "Красный", Price = 1500, Rgb = 16719920 },
            new Part { Id = "blue", Name = "Синий", Price = 1500, Rgb = 2777343 },
            new Part { Id = "green", Name = "Зелёный", Price = 1500, Rgb = 2162528 },
            new Part { Id = "purple", Name = "Фиолетовый", Price = 1800, Rgb = 11550975 },
        };
        public static readonly string[] Paints = new string[] { "#b3262b", "#e5e5e0", "#1c1c20", "#2d5fb0", "#0f3a2a", "#d7b26a", "#7a2cc0", "#ff7a00", "#ffd21f", "#00a3a3", "#ff4fa0", "#8a8d93", "#5a1f1f", "#0b2a6b", "#6fd1ff", "#a6ff3b" };
    }
}
