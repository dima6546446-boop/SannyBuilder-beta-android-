using System.Collections.Generic;

namespace CaucasusDrive
{
    public enum Drive { RWD, FWD, AWD }

    public class CarDims
    {
        public float W, front, rear, axleF, axleR, track, wheelR, wheelW, sill;
    }

    public class CarSpec
    {
        public float mass, hp, torque, peakRpm, redline, final, cgHeight, brake, drag, grip, maxSteer, tank;
        public float[] gears;
        public Drive drive;
    }

    public class CarDef
    {
        public string id, name, nick, years, wheelStyle;
        public int price;
        public CarDims dims;
        public CarSpec spec;
        public int[] colors;
        public float traffic;
        public float headY, headX, tailY, tailX; // фары и фонари (для света и «светящихся точек»)
        public int fixedColor = -1;               // спецверсии: ДПС, такси
        public bool police, taxi;
        public string baseId;                     // модель кузова для спецверсий
    }

    /// <summary>Каталог машин АвтоВАЗ (размеры и физика — как в веб-версии, config/cars.js).</summary>
    public static class Cars
    {
        static readonly int[] Classic = { 0x7a1020, 0xecebe4, 0x1f4f6a, 0x3d86b8, 0xb31d12, 0xc8b98a, 0x155e3e, 0x4b5157, 0xb89a4a, 0x2b2b2b };
        static readonly int[] Modern = { 0xf2f2f2, 0x1c1c1c, 0x8a8f94, 0xb0161b, 0x1d3f7a, 0x5a6b3a, 0xc4a468, 0x6d2f86, 0xd9601c, 0x2e7ab0 };

        public static readonly int[] Palette = {
            0xecebe4, 0xf4f4f4, 0x1c1c1c, 0x8a8f94, 0x4b5157, 0x7a1020, 0xb31d12, 0xd9601c, 0xd8c23a,
            0xb89a4a, 0x155e3e, 0x5a6b3a, 0x7fa84a, 0x1f4f6a, 0x3d86b8, 0x1d3f7a, 0x6d2f86, 0xe07aa0,
        };

        static CarDims D(float W, float front, float rear, float axleF, float axleR, float track, float wheelR, float wheelW, float sill)
        {
            return new CarDims { W = W, front = front, rear = rear, axleF = axleF, axleR = axleR, track = track, wheelR = wheelR, wheelW = wheelW, sill = sill };
        }

        static CarSpec S(float mass, float hp, float torque, float peakRpm, float redline, float[] gears, float final, Drive drive, float cg, float brake, float drag, float grip, float maxSteer, float tank)
        {
            return new CarSpec { mass = mass, hp = hp, torque = torque, peakRpm = peakRpm, redline = redline, gears = gears, final = final, drive = drive, cgHeight = cg, brake = brake, drag = drag, grip = grip, maxSteer = maxSteer, tank = tank };
        }

        public static readonly CarDef[] All = {
            new CarDef { id = "vaz2101", name = "ВАЗ-2101", nick = "«Копейка»", years = "1970–1988", price = 25000, wheelStyle = "steel",
                dims = D(1.61f, 1.93f, -2.14f, 1.14f, -1.284f, 1.35f, 0.29f, 0.165f, 0.30f), colors = Classic, traffic = 1f,
                spec = S(1030, 64, 89, 3400, 6000, new[] { 3.53f, 3.75f, 2.30f, 1.49f, 1.00f }, 4.3f, Drive.RWD, 0.55f, 8800, 0.5f, 1.0f, 0.6f, 39),
                headY = 0.66f, headX = 0.55f, tailY = 0.66f, tailX = 0.64f },
            new CarDef { id = "vaz2106", name = "ВАЗ-2106", nick = "«Шестёрка»", years = "1976–2006", price = 40000, wheelStyle = "classic",
                dims = D(1.61f, 1.99f, -2.18f, 1.14f, -1.284f, 1.36f, 0.29f, 0.175f, 0.30f), colors = Classic, traffic = 2f,
                spec = S(1045, 80, 121, 3000, 6000, new[] { 3.24f, 3.75f, 2.30f, 1.49f, 1.00f }, 3.9f, Drive.RWD, 0.55f, 9000, 0.52f, 1.02f, 0.6f, 39),
                headY = 0.67f, headX = 0.54f, tailY = 0.72f, tailX = 0.56f },
            new CarDef { id = "vaz2107", name = "ВАЗ-2107", nick = "«Семёрка»", years = "1982–2012", price = 50000, wheelStyle = "classic",
                dims = D(1.62f, 1.98f, -2.18f, 1.14f, -1.284f, 1.36f, 0.29f, 0.175f, 0.30f), colors = Classic, traffic = 3f,
                spec = S(1060, 71, 104, 3400, 6200, new[] { 3.53f, 3.67f, 2.10f, 1.36f, 1.00f, 0.82f }, 3.9f, Drive.RWD, 0.55f, 9500, 0.52f, 1.05f, 0.6f, 39),
                headY = 0.68f, headX = 0.61f, tailY = 0.72f, tailX = 0.56f },
            new CarDef { id = "oka", name = "ВАЗ-1111", nick = "«Ока»", years = "1988–2008", price = 15000, wheelStyle = "steel",
                dims = D(1.42f, 1.40f, -1.80f, 0.85f, -1.33f, 1.21f, 0.26f, 0.145f, 0.27f), colors = new[] { 0xd8c23a, 0x3d86b8, 0xecebe4, 0xb31d12, 0x7fa84a, 0x9b9b9b }, traffic = 0.7f,
                spec = S(700, 33, 52, 3400, 5800, new[] { 3.5f, 3.70f, 2.06f, 1.36f, 0.97f }, 4.54f, Drive.FWD, 0.5f, 6200, 0.36f, 0.98f, 0.62f, 30),
                headY = 0.59f, headX = 0.47f, tailY = 0.72f, tailX = 0.57f },
            new CarDef { id = "vaz2109", name = "ВАЗ-2109", nick = "«Девятка»", years = "1987–2004", price = 70000, wheelStyle = "steel",
                dims = D(1.65f, 1.71f, -2.30f, 0.98f, -1.48f, 1.40f, 0.28f, 0.165f, 0.29f), colors = new[] { 0x1c1c1c, 0xb0161b, 0x2e5f8a, 0xecebe4, 0x4a7a3a, 0x8a8f94, 0x6d2f86 }, traffic = 2f,
                spec = S(945, 70, 106, 3400, 6200, new[] { 3.53f, 3.64f, 1.95f, 1.36f, 0.94f, 0.78f }, 4.13f, Drive.FWD, 0.52f, 9000, 0.46f, 1.05f, 0.62f, 43),
                headY = 0.62f, headX = 0.56f, tailY = 0.78f, tailX = 0.55f },
            new CarDef { id = "niva", name = "ВАЗ-2121", nick = "«Нива»", years = "1977–н.в.", price = 90000, wheelStyle = "steel",
                dims = D(1.68f, 1.73f, -2.01f, 1.05f, -1.15f, 1.43f, 0.35f, 0.185f, 0.42f), colors = new[] { 0xe9e3cf, 0x5a6b3a, 0xb31d12, 0x1f4f6a, 0x7a1020, 0x3d3d3d, 0xd8c23a }, traffic = 1.5f,
                spec = S(1210, 80, 121, 3000, 5800, new[] { 3.53f, 3.67f, 2.10f, 1.36f, 1.00f, 0.82f }, 3.9f, Drive.AWD, 0.72f, 10000, 0.62f, 1.08f, 0.6f, 42),
                headY = 0.85f, headX = 0.6f, tailY = 0.92f, tailX = 0.72f },
            new CarDef { id = "priora", name = "Lada Priora", nick = "«Приора»", years = "2007–2018", price = 140000, wheelStyle = "star",
                dims = D(1.68f, 1.83f, -2.52f, 1.0f, -1.49f, 1.43f, 0.30f, 0.185f, 0.30f), colors = Modern, traffic = 1.5f,
                spec = S(1088, 98, 145, 4000, 6500, new[] { 3.53f, 3.64f, 1.95f, 1.36f, 0.94f, 0.78f }, 3.7f, Drive.FWD, 0.52f, 10500, 0.44f, 1.1f, 0.62f, 43),
                headY = 0.70f, headX = 0.58f, tailY = 0.84f, tailX = 0.57f },
            new CarDef { id = "granta", name = "Lada Granta", nick = "«Гранта»", years = "2011–н.в.", price = 180000, wheelStyle = "steel",
                dims = D(1.70f, 1.78f, -2.48f, 0.98f, -1.50f, 1.43f, 0.305f, 0.185f, 0.31f), colors = Modern, traffic = 2f,
                spec = S(1160, 87, 140, 3800, 6200, new[] { 3.53f, 3.64f, 1.95f, 1.36f, 0.94f, 0.78f }, 3.9f, Drive.FWD, 0.55f, 10500, 0.46f, 1.08f, 0.62f, 50),
                headY = 0.74f, headX = 0.56f, tailY = 0.88f, tailX = 0.56f },
            new CarDef { id = "vesta", name = "Lada Vesta", nick = "«Веста»", years = "2015–н.в.", price = 320000, wheelStyle = "sport",
                dims = D(1.76f, 1.89f, -2.52f, 1.05f, -1.585f, 1.51f, 0.315f, 0.195f, 0.32f), colors = Modern, traffic = 1.2f,
                spec = S(1230, 106, 148, 4200, 6500, new[] { 3.53f, 3.73f, 2.05f, 1.36f, 1.03f, 0.82f }, 3.93f, Drive.FWD, 0.54f, 11500, 0.45f, 1.15f, 0.6f, 55),
                headY = 0.77f, headX = 0.6f, tailY = 0.9f, tailX = 0.6f },
            new CarDef { id = "largus", name = "Lada Largus", nick = "«Ларгус»", years = "2012–н.в.", price = 260000, wheelStyle = "steel",
                dims = D(1.75f, 1.93f, -2.54f, 1.15f, -1.755f, 1.47f, 0.31f, 0.185f, 0.33f), colors = Modern, traffic = 1f,
                spec = S(1260, 102, 145, 3750, 6000, new[] { 3.55f, 3.73f, 2.05f, 1.32f, 0.97f, 0.76f }, 4.5f, Drive.FWD, 0.62f, 11000, 0.55f, 1.05f, 0.58f, 50),
                headY = 0.8f, headX = 0.58f, tailY = 0.86f, tailX = 0.72f },
            new CarDef { id = "vaz2108", name = "ВАЗ-2108", nick = "«Спутник»", years = "1984–2003", price = 60000, wheelStyle = "steel",
                dims = D(1.65f, 1.71f, -2.30f, 0.98f, -1.48f, 1.40f, 0.28f, 0.165f, 0.29f), colors = new[] { 0xecebe4, 0xb0161b, 0x2e5f8a, 0x1c1c1c, 0x4a7a3a, 0xd8c23a, 0x8a8f94 }, traffic = 1.5f,
                spec = S(915, 64, 94, 3400, 6000, new[] { 3.53f, 3.64f, 1.95f, 1.36f, 0.94f, 0.78f }, 4.13f, Drive.FWD, 0.52f, 8800, 0.46f, 1.04f, 0.62f, 43),
                headY = 0.62f, headX = 0.56f, tailY = 0.78f, tailX = 0.55f },
            new CarDef { id = "vaz2110", name = "ВАЗ-2110", nick = "«Десятка»", years = "1996–2014", price = 120000, wheelStyle = "star",
                dims = D(1.68f, 1.83f, -2.46f, 1.0f, -1.49f, 1.42f, 0.295f, 0.175f, 0.30f), colors = Modern, traffic = 1.5f,
                spec = S(1070, 81, 120, 3800, 6200, new[] { 3.53f, 3.64f, 1.95f, 1.36f, 0.94f, 0.78f }, 3.9f, Drive.FWD, 0.53f, 10000, 0.43f, 1.08f, 0.62f, 43),
                headY = 0.66f, headX = 0.57f, tailY = 0.88f, tailX = 0.56f },
            new CarDef { id = "kalina", name = "Lada Kalina", nick = "«Калина»", years = "2004–2018", price = 160000, wheelStyle = "steel",
                dims = D(1.70f, 1.78f, -2.07f, 1.0f, -1.47f, 1.43f, 0.285f, 0.175f, 0.31f), colors = Modern, traffic = 1.5f,
                spec = S(1010, 87, 128, 3800, 6200, new[] { 3.5f, 3.7f, 2.0f, 1.36f, 0.97f, 0.8f }, 3.9f, Drive.FWD, 0.53f, 10000, 0.42f, 1.1f, 0.62f, 50),
                headY = 0.70f, headX = 0.57f, tailY = 0.88f, tailX = 0.58f },
        };

        static Dictionary<string, CarDef> _byId;
        public static CarDef Get(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, CarDef>();
                foreach (var c in All) _byId[c.id] = c;
                foreach (var v in Variants) _byId[v.id] = v;
            }
            CarDef d;
            return _byId.TryGetValue(id ?? "", out d) ? d : All[2];
        }

        static CarDef Variant(string id, string baseId, string name, int color, bool police, bool taxi)
        {
            CarDef b = null;
            foreach (var c in All) if (c.id == baseId) b = c;
            return new CarDef
            {
                id = id, baseId = baseId, name = name, nick = b.nick, years = b.years, price = 0, wheelStyle = b.wheelStyle,
                dims = b.dims, spec = b.spec, colors = new[] { color }, traffic = 0, headY = b.headY, headX = b.headX,
                tailY = b.tailY, tailX = b.tailX, fixedColor = color, police = police, taxi = taxi,
            };
        }

        public static readonly CarDef[] Variants = {
            Variant("police", "vaz2107", "ДПС", 0xf2f2f2, true, false),
            Variant("taxi", "granta", "Такси", 0xf2c200, false, true),
        };

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
            return 2;
        }
    }

    public class TuneItem
    {
        public string name; public float value; public int price;
        public TuneItem(string n, float v, int p) { name = n; value = v; price = p; }
    }

    /// <summary>Тюнинг (как в веб-версии): диски, подвеска, тонировка, мотор, шины, неон.</summary>
    public static class Tuning
    {
        public static readonly string[] WheelIds = { "default", "steel", "classic", "star", "sport", "mesh" };
        public static readonly TuneItem[] Wheels = {
            new TuneItem("Заводские", 0, 0), new TuneItem("Штамповка", 1, 1500), new TuneItem("Колпаки «Жигули»", 2, 3000),
            new TuneItem("Литьё «Звезда»", 3, 9000), new TuneItem("Спорт 10 спиц", 4, 14000), new TuneItem("Сетка «BBS»", 5, 20000),
        };
        public static readonly TuneItem[] Height = {
            new TuneItem("Сток", 0, 0), new TuneItem("Занижение −4 см", -0.04f, 4000), new TuneItem("Корч −8 см", -0.08f, 8000), new TuneItem("Лифт +5 см", 0.05f, 6000),
        };
        public static readonly TuneItem[] Tint = { new TuneItem("Заводская", 0.6f, 0), new TuneItem("50%", 0.82f, 2000), new TuneItem("Наглухо", 0.97f, 3500) };
        public static readonly TuneItem[] Engine = {
            new TuneItem("Сток", 1f, 0), new TuneItem("Чип-тюнинг", 1.12f, 12000), new TuneItem("Расточка + распредвал", 1.28f, 35000), new TuneItem("Турбо «Колхоз»", 1.55f, 90000),
        };
        public static readonly TuneItem[] Tires = { new TuneItem("Кама-205", 1f, 0), new TuneItem("Спортивные", 1.12f, 10000) };
        public static readonly int[] NeonColors = { -1, 0x2a7bff, 0xff2a3a, 0x2aff6a, 0xb02aff, 0x2affe6, 0xf2f6ff };
        public static readonly TuneItem[] Neon = {
            new TuneItem("Нет", 0, 0), new TuneItem("Синий", 1, 6000), new TuneItem("Красный", 2, 6000), new TuneItem("Зелёный", 3, 6000),
            new TuneItem("Фиолетовый", 4, 7000), new TuneItem("Бирюзовый", 5, 7000), new TuneItem("Белый", 6, 8000),
        };
        public const int PaintPrice = 2500;
    }
}
