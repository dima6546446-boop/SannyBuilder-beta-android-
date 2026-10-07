using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Code-defined default content. Also the source the editor uses to generate ScriptableObject assets.</summary>
    public static class DefaultCatalog
    {
        private static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString("#" + hex, out c); return c; }

        private static CarDefinition Car(string id, string ru, string en, string dru, string den, BodyStyle style, int price, int lvl,
            float len, float wid, float hei, float wb, float mass, float torque, float redline, float peakRpm,
            float[] gears, float finalDrive, int cyl, float turbo, DriveType drive, string color, float tone, float grip, float steer)
        {
            var c = ScriptableObject.CreateInstance<CarDefinition>();
            c.name = id;
            c.id = id; c.nameRu = ru; c.nameEn = en; c.descRu = dru; c.descEn = den;
            c.bodyStyle = style; c.price = price; c.requiredLevel = lvl;
            c.length = len; c.width = wid; c.height = hei; c.wheelbase = wb;
            c.trackFront = wid - 0.24f; c.trackRear = wid - 0.26f;
            c.mass = mass; c.peakTorque = torque; c.redlineRpm = redline; c.peakTorqueRpm = peak(redline, peakRpm);
            c.gearRatios = gears; c.finalDrive = finalDrive; c.cylinders = cyl; c.turboStockBoost = turbo;
            c.defaultDrive = drive; c.defaultColor = C(color); c.engineTone = tone; c.baseGrip = grip; c.maxSteerAngle = steer;
            c.wheelRadius = style == BodyStyle.BigSedan || style == BodyStyle.SportCoupe ? 0.33f : 0.30f;
            c.tireWidth = style == BodyStyle.SportCoupe ? 0.255f : 0.205f;
            c.brakeForce = mass * 8.5f;
            c.comHeight = hei * 0.30f;
            c.rideHeight = 0.17f;
            return c;
        }

        private static float peak(float redline, float peakRpm) { return peakRpm > 0 ? peakRpm : redline * 0.65f; }

        public static List<CarDefinition> CreateCars()
        {
            var l = new List<CarDefinition>();
            l.Add(Car("kopeyka", "Копейка", "Kopeyka",
                "Классическая «копейка» с коробочным кузовом. Лёгкая, честная и податливая — идеальная первая дрифт-машина.",
                "A boxy 70s-style saloon. Light, honest and playful — the perfect first drift car.",
                BodyStyle.Classic, 0, 1, 4.10f, 1.62f, 1.40f, 2.42f, 1020, 185, 6600, 4300,
                new[] { 3.50f, 2.10f, 1.40f, 1.05f, 0.82f }, 4.10f, 4, 0f, DriveType.RWD, "C8281E", 1.0f, 1.00f, 36));
            l.Add(Car("pyaterka", "Пятёрка", "Pyaterka",
                "Строгий седан с вылизанной подвеской. Предсказуемый занос и хороший баланс.",
                "A sober saloon with a tuned chassis. Predictable slides and balanced handling.",
                BodyStyle.Sedan, 18000, 2, 4.22f, 1.62f, 1.40f, 2.42f, 1060, 215, 7000, 4600,
                new[] { 3.50f, 2.15f, 1.45f, 1.08f, 0.85f }, 4.10f, 4, 0f, DriveType.RWD, "E6E6E6", 1.1f, 1.03f, 37));
            l.Add(Car("devyatka", "Девятка", "Devyatka",
                "Хэтчбек из девяностых с заднеприводным свапом. Лёгкий нос, резкая реакция на руль.",
                "A 90s hatchback with a rear-drive swap. Light nose and razor-sharp turn-in.",
                BodyStyle.Hatch, 26000, 3, 4.00f, 1.65f, 1.40f, 2.46f, 980, 205, 7200, 4800,
                new[] { 3.60f, 2.15f, 1.50f, 1.12f, 0.88f }, 4.20f, 4, 0f, DriveType.RWD, "2E5AAC", 1.15f, 1.04f, 38));
            l.Add(Car("desyatka", "Десятка", "Desyatka",
                "Низкий седан с мощным мотором. Быстро входит в занос и держит угол.",
                "A low saloon with a strong motor. Snaps into a slide and holds the angle.",
                BodyStyle.Sedan, 38000, 5, 4.35f, 1.68f, 1.38f, 2.49f, 1100, 240, 7200, 4800,
                new[] { 3.40f, 2.10f, 1.45f, 1.10f, 0.88f }, 4.10f, 4, 0f, DriveType.RWD, "1C1C20", 1.2f, 1.05f, 38));
            l.Add(Car("volzhanka", "Волжанка", "Volzhanka",
                "Тяжёлая барская «волжанка» с V8. Много момента, длинные красивые заносы.",
                "A heavy executive barge with a V8. Huge torque and long, graceful slides.",
                BodyStyle.BigSedan, 55000, 7, 4.85f, 1.80f, 1.46f, 2.80f, 1450, 360, 6200, 3800,
                new[] { 3.15f, 1.95f, 1.40f, 1.00f, 0.78f }, 3.70f, 8, 0f, DriveType.RWD, "0F3A2A", 0.8f, 1.00f, 34));
            l.Add(Car("universal", "Универсал-Турбо", "Wagon Turbo",
                "Дачный универсал с турбиной. Смешной внешне — страшный на трассе.",
                "A countryside wagon with a turbo. Looks silly, drives scary.",
                BodyStyle.Wagon, 70000, 9, 4.50f, 1.70f, 1.45f, 2.60f, 1280, 300, 6800, 4200,
                new[] { 3.30f, 2.05f, 1.42f, 1.08f, 0.85f }, 3.90f, 4, 0.30f, DriveType.RWD, "D7B26A", 1.3f, 1.04f, 37));
            l.Add(Car("ronin", "Ронин", "Ronin",
                "Низкое купе в японском духе: точный руль, отзывчивый мотор, много угла.",
                "A low coupe in the Japanese spirit: precise steering, snappy motor and plenty of angle.",
                BodyStyle.Coupe, 110000, 12, 4.50f, 1.72f, 1.30f, 2.52f, 1230, 330, 7500, 5000,
                new[] { 3.60f, 2.20f, 1.55f, 1.18f, 0.95f, 0.78f }, 3.85f, 4, 0.45f, DriveType.RWD, "7A2CC0", 1.4f, 1.08f, 40));
            l.Add(Car("raketa", "Ракета", "Raketa",
                "Широкое спорт-купе. Максимум мощности, дрифт на пределе. Можно переключить на полный привод.",
                "A wide-body sports coupe. Maximum power, drifting on the edge. Switchable to AWD.",
                BodyStyle.SportCoupe, 180000, 15, 4.60f, 1.85f, 1.25f, 2.58f, 1350, 420, 7800, 5200,
                new[] { 3.45f, 2.20f, 1.60f, 1.25f, 1.00f, 0.82f }, 3.70f, 6, 0.60f, DriveType.RWD, "FF7A00", 1.5f, 1.12f, 40));
            return l;
        }

        private static UpgradeDefinition Up(UpgradeCategory c, string ru, string en, string dru, string den, int price, float per, int max = 5)
        {
            var u = ScriptableObject.CreateInstance<UpgradeDefinition>();
            u.name = c.ToString();
            u.category = c; u.nameRu = ru; u.nameEn = en; u.descRu = dru; u.descEn = den;
            u.basePrice = price; u.valuePerLevel = per; u.maxLevel = max;
            return u;
        }

        public static List<UpgradeDefinition> CreateUpgrades()
        {
            var l = new List<UpgradeDefinition>();
            l.Add(Up(UpgradeCategory.Engine, "Двигатель", "Engine", "Больше крутящего момента и оборотов.", "More torque and revs.", 2200, 0.09f));
            l.Add(Up(UpgradeCategory.Turbo, "Турбо", "Turbo", "Давление наддува, свист и выстрелы.", "Boost pressure, whistle and flames.", 2600, 0.13f));
            l.Add(Up(UpgradeCategory.Suspension, "Подвеска", "Suspension", "Жёсткость пружин и демпфирование.", "Spring rate and damping.", 1600, 0.08f));
            l.Add(Up(UpgradeCategory.Brakes, "Тормоза", "Brakes", "Усилие торможения.", "Braking force.", 1400, 0.10f));
            l.Add(Up(UpgradeCategory.Tires, "Шины", "Tires", "Сцепление шин.", "Tyre grip.", 1800, 0.05f));
            l.Add(Up(UpgradeCategory.Differential, "Дифференциал", "Differential", "Степень блокировки: больше — легче дрифтовать.", "Lock ratio: more lock, easier drift.", 1500, 0.2f));
            l.Add(Up(UpgradeCategory.Weight, "Облегчение", "Weight reduction", "Снижение массы кузова.", "Lower body mass.", 1900, 0.035f));
            return l;
        }

        private static CosmeticDefinition Cos(CosmeticCategory c, string id, string ru, string en, int price, int variant, int lvl = 1, string colorHex = "FFFFFF")
        {
            var d = ScriptableObject.CreateInstance<CosmeticDefinition>();
            d.name = c + "_" + id;
            d.category = c; d.id = id; d.nameRu = ru; d.nameEn = en; d.price = price; d.variant = variant; d.requiredLevel = lvl;
            d.color = C(colorHex);
            return d;
        }

        public static List<CosmeticDefinition> CreateCosmetics()
        {
            var l = new List<CosmeticDefinition>();
            l.Add(Cos(CosmeticCategory.Wrap, "none", "Без плёнки", "No wrap", 0, 0));
            l.Add(Cos(CosmeticCategory.Wrap, "stripes", "Гоночные полосы", "Racing stripes", 1200, 1));
            l.Add(Cos(CosmeticCategory.Wrap, "flames", "Пламя", "Flames", 2500, 2, 2));
            l.Add(Cos(CosmeticCategory.Wrap, "checker", "Шахматка", "Checker", 2000, 3, 3));
            l.Add(Cos(CosmeticCategory.Wrap, "camo", "Камуфляж", "Camo", 2800, 4, 4));
            l.Add(Cos(CosmeticCategory.Wrap, "carbon", "Карбон", "Carbon", 3500, 5, 6));
            l.Add(Cos(CosmeticCategory.Wrap, "gradient", "Градиент", "Gradient", 3000, 6, 5));

            l.Add(Cos(CosmeticCategory.Spoiler, "none", "Без спойлера", "No spoiler", 0, 0));
            l.Add(Cos(CosmeticCategory.Spoiler, "lip", "Лип-спойлер", "Lip spoiler", 900, 1));
            l.Add(Cos(CosmeticCategory.Spoiler, "duck", "Уточка", "Ducktail", 1600, 2, 2));
            l.Add(Cos(CosmeticCategory.Spoiler, "gt", "GT-крыло", "GT wing", 3200, 3, 5));
            l.Add(Cos(CosmeticCategory.Spoiler, "huge", "Огромное крыло", "Huge wing", 4800, 4, 8));

            l.Add(Cos(CosmeticCategory.BodyKit, "none", "Сток", "Stock", 0, 0));
            l.Add(Cos(CosmeticCategory.BodyKit, "street", "Стрит-обвес", "Street kit", 3500, 1, 2));
            l.Add(Cos(CosmeticCategory.BodyKit, "wide", "Широкий обвес", "Wide kit", 7500, 2, 6));
            l.Add(Cos(CosmeticCategory.BodyKit, "rally", "Ралли-обвес", "Rally kit", 6000, 3, 4));

            l.Add(Cos(CosmeticCategory.Rims, "rim_5star", "Звезда-5", "5-star", 0, 0));
            l.Add(Cos(CosmeticCategory.Rims, "rim_6spoke", "Шестилучевые", "6-spoke", 1400, 1, 2));
            l.Add(Cos(CosmeticCategory.Rims, "rim_mesh", "Сетка", "Mesh", 2200, 2, 3));
            l.Add(Cos(CosmeticCategory.Rims, "rim_deep", "Глубокие", "Deep dish", 3000, 3, 5));
            l.Add(Cos(CosmeticCategory.Rims, "rim_split", "Раздвоенные", "Split-spoke", 3800, 4, 7));

            l.Add(Cos(CosmeticCategory.Neon, "none", "Без неона", "No neon", 0, 0));
            l.Add(Cos(CosmeticCategory.Neon, "neon_red", "Красный неон", "Red neon", 1500, 1, 2, "FF1E1E"));
            l.Add(Cos(CosmeticCategory.Neon, "neon_blue", "Синий неон", "Blue neon", 1500, 1, 2, "2060FF"));
            l.Add(Cos(CosmeticCategory.Neon, "neon_green", "Зелёный неон", "Green neon", 1500, 1, 3, "20FF50"));
            l.Add(Cos(CosmeticCategory.Neon, "neon_purple", "Фиолетовый неон", "Purple neon", 1800, 1, 4, "B030FF"));
            l.Add(Cos(CosmeticCategory.Neon, "neon_cyan", "Голубой неон", "Cyan neon", 1800, 1, 5, "20F0FF"));

            l.Add(Cos(CosmeticCategory.Decal, "none", "Без наклеек", "No decals", 0, 0));
            l.Add(Cos(CosmeticCategory.Decal, "number", "Гоночный номер", "Racing number", 600, 1));
            l.Add(Cos(CosmeticCategory.Decal, "sidestripe", "Боковая полоса", "Side stripe", 700, 2));
            l.Add(Cos(CosmeticCategory.Decal, "stars", "Звёзды", "Stars", 900, 3, 3));
            l.Add(Cos(CosmeticCategory.Decal, "flameside", "Огонь на дверях", "Door flames", 1100, 4, 4));
            l.Add(Cos(CosmeticCategory.Decal, "checkerside", "Шашки", "Checker strip", 800, 5, 2));
            return l;
        }

        private static TrackDefinition Tr(string id, string ru, string en, GameMode mode, TrackKind kind, int laps, float time,
            float[] medals, int reward, int lvl, float hour, int weather)
        {
            var t = ScriptableObject.CreateInstance<TrackDefinition>();
            t.name = id;
            t.id = id; t.nameRu = ru; t.nameEn = en; t.mode = mode; t.kind = kind; t.laps = laps; t.timeLimit = time;
            t.medalScores = medals; t.reward = reward; t.requiredLevel = lvl; t.startTime = hour; t.weather = weather;
            return t;
        }

        public static List<TrackDefinition> CreateTracks()
        {
            var l = new List<TrackDefinition>();
            l.Add(Tr("drift_industrial", "Промзона: дрифт-круг", "Industrial: drift loop", GameMode.DriftTrack, TrackKind.IndustrialLoop, 99, 120f, new[] { 5000f, 12000f, 25000f }, 2000, 1, 17f, 0));
            l.Add(Tr("drift_city", "Ночной город: дрифт", "Night city: drift run", GameMode.DriftTrack, TrackKind.CityRing, 99, 150f, new[] { 7000f, 16000f, 32000f }, 3000, 3, 22f, 1));
            l.Add(Tr("drift_serpentine", "Серпантин: дрифт", "Serpentine: drift run", GameMode.DriftTrack, TrackKind.Serpentine, 1, 150f, new[] { 8000f, 20000f, 40000f }, 4500, 6, 7f, 2));
            l.Add(Tr("ta_industrial", "Промзона: тайм-аттак", "Industrial: time attack", GameMode.TimeAttack, TrackKind.IndustrialLoop, 3, 0f, new[] { 55f, 70f, 85f }, 1800, 1, 15f, 0));
            l.Add(Tr("ta_city", "Город: тайм-аттак", "City: time attack", GameMode.TimeAttack, TrackKind.CityRing, 2, 0f, new[] { 60f, 80f, 100f }, 2600, 2, 12f, 0));
            l.Add(Tr("ta_serpentine", "Серпантин: тайм-аттак", "Serpentine: time attack", GameMode.TimeAttack, TrackKind.Serpentine, 1, 0f, new[] { 45f, 60f, 75f }, 3200, 5, 9f, 0));
            l.Add(Tr("battle_industrial", "Баттл: промзона", "Battle: industrial", GameMode.DriftBattle, TrackKind.IndustrialLoop, 99, 90f, new[] { 0f, 0f, 0f }, 3500, 2, 19f, 0));
            l.Add(Tr("battle_city", "Баттл: ночной город", "Battle: night city", GameMode.DriftBattle, TrackKind.CityRing, 99, 90f, new[] { 0f, 0f, 0f }, 5000, 5, 23f, 1));
            l.Add(Tr("battle_serpentine", "Баттл: серпантин", "Battle: serpentine", GameMode.DriftBattle, TrackKind.Serpentine, 1, 100f, new[] { 0f, 0f, 0f }, 7000, 8, 18f, 0));
            return l;
        }
    }
}
