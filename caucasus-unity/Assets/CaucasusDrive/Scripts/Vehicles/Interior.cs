using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    public class Gauge { public string type, label; public float x, y, r, max; public Gauge(string t, float x, float y, float r, float max, string l) { type = t; this.x = x; this.y = y; this.r = r; this.max = max; label = l; } }

    public class WheelStyle { public float r, t, pad, tilt; public int spokes, color; public bool ring, buttons; }

    public class InteriorStyle
    {
        public string dash, hood, pattern, radio, console, lever;
        public float depth, cw, ch;
        public int dashColor, strip, trim, seat, seat2, door, backlight;
        public bool headrest, crank, clock, hatch;
        public string face, ring, ink, red;
        public WheelStyle wheel;
        public Gauge[] gauges;
    }

    /// <summary>
    /// Салон каждой модели (порт Interior.js): торпедо по прототипу, щиток с живыми стрелками (спидометр,
    /// тахометр, топливо, температура), руль (крутится с рулением), консоль, магнитола с названием станции,
    /// рычаги, педали, кресла с обивкой, обшивка дверей, потолок, зеркало, козырьки, дворники (машут в дождь)
    /// и водитель, держащий руль двумя руками (IK). Габариты — по форме кузова (InteriorAnchors).
    /// Размеры заданы в осях веб-версии (+X — влево) и зеркалятся в Unity (+X — вправо): водитель слева.
    /// </summary>
    public class Interior
    {
        static InteriorStyle S(string dash, float depth, int dashColor, int strip, int trim, int seat, int seat2, string pattern, bool headrest,
            WheelStyle w, float cw, float ch, string hood, string face, string ring, string ink, string red, Gauge[] g, int backlight, string radio, string console, string lever, int door, bool crank, bool clock = false, bool hatch = false)
        {
            return new InteriorStyle { dash = dash, depth = depth, dashColor = dashColor, strip = strip, trim = trim, seat = seat, seat2 = seat2, pattern = pattern, headrest = headrest, wheel = w, cw = cw, ch = ch, hood = hood, face = face, ring = ring, ink = ink, red = red, gauges = g, backlight = backlight, radio = radio, console = console, lever = lever, door = door, crank = crank, clock = clock, hatch = hatch };
        }
        static WheelStyle W(float r, float t, int spokes, int color, bool ring, float pad, float tilt, bool buttons = false) { return new WheelStyle { r = r, t = t, spokes = spokes, color = color, ring = ring, pad = pad, tilt = tilt, buttons = buttons }; }
        static Gauge G(string t, float x, float y, float r, float max, string l) { return new Gauge(t, x, y, r, max, l); }

        public static InteriorStyle Style(string id)
        {
            switch (id)
            {
                case "vaz2101": return S("flat", 0.4f, 0x1d1d1d, 0xb8b8b0, 0x2a2624, 0x7a2a22, 0x5a1c16, "stripes", false, W(0.205f, 0.014f, 2, 0x161616, true, 0.05f, 0.62f), 0.34f, 0.15f, "oval", "#101010", "#c8c8c8", "#f2f2f2", "#e04030",
                    new[] { G("speed", -0.075f, 0, 0.068f, 180, "км/ч"), G("combo", 0.08f, 0, 0.055f, 0, "") }, 0xffa860, "none", "tunnel", "long", 0x2e2a26, true);
                case "vaz2106": return S("flat", 0.42f, 0x1b1b1b, 0x6b4226, 0x2a2420, 0x6a4a32, 0x4a3222, "stripes", true, W(0.2f, 0.016f, 2, 0x141414, false, 0.075f, 0.6f), 0.42f, 0.15f, "wells", "#0d0d0d", "#b0b0b0", "#f2f2f2", "#e04030",
                    new[] { G("speed", -0.11f, 0.005f, 0.062f, 180, "км/ч"), G("rpm", 0.035f, 0.005f, 0.055f, 8, "об/мин"), G("fuel", 0.15f, 0.03f, 0.03f, 0, ""), G("temp", 0.15f, -0.035f, 0.03f, 0, "") }, 0xffb060, "ural", "tunnel", "long", 0x34302c, true, true);
                case "oka": return S("mini", 0.36f, 0x262626, 0x3a3a3a, 0x303030, 0x5a5d62, 0x3c3e42, "plain", true, W(0.18f, 0.017f, 2, 0x1a1a1a, false, 0.07f, 0.55f), 0.26f, 0.13f, "rect", "#0f0f0f", "#606060", "#ffffff", "#ff5040",
                    new[] { G("speed", -0.035f, 0, 0.058f, 150, "км/ч"), G("fuel", 0.085f, 0.02f, 0.028f, 0, ""), G("temp", 0.085f, -0.035f, 0.024f, 0, "") }, 0xffa060, "none", "tunnel", "long", 0x3a3a3c, true, false, true);
                case "vaz2109": return S("angular", 0.5f, 0x1e1e20, 0x2c2c2e, 0x2a2a2c, 0x50555c, 0x2e3238, "checks", true, W(0.19f, 0.02f, 4, 0x151515, false, 0.07f, 0.52f), 0.4f, 0.15f, "angular", "#0b0b0b", "#4a4a4a", "#eaffea", "#ff4a3a",
                    new[] { G("speed", -0.09f, 0, 0.064f, 200, "км/ч"), G("rpm", 0.09f, 0, 0.06f, 8, "x1000"), G("fuel", 0, 0.035f, 0.024f, 0, ""), G("temp", 0, -0.035f, 0.024f, 0, "") }, 0x6fe08a, "ural", "console", "short", 0x323236, true, false, true);
                case "niva": return S("flat", 0.42f, 0x1a1a1a, 0x2a2a2a, 0x262422, 0x5c4634, 0x3e2e22, "stripes", true, W(0.205f, 0.016f, 2, 0x141414, false, 0.08f, 0.66f), 0.36f, 0.15f, "rect", "#0c0c0c", "#9a9a9a", "#f6f6f0", "#e04030",
                    new[] { G("speed", -0.085f, 0, 0.064f, 160, "км/ч"), G("rpm", 0.09f, 0, 0.058f, 8, "x1000") }, 0xffa860, "ural", "niva", "long", 0x302c28, true, true, true);
                case "priora": return S("modern", 0.54f, 0x232325, 0x55585c, 0x2b2b2d, 0x2e3034, 0x45484e, "insert", true, W(0.185f, 0.022f, 3, 0x161616, false, 0.075f, 0.42f), 0.4f, 0.15f, "tubes", "#060606", "#8a8d92", "#ffffff", "#ff3a2a",
                    new[] { G("rpm", -0.12f, 0, 0.058f, 8, "x1000"), G("speed", 0, 0.005f, 0.066f, 220, "км/ч"), G("fuelTemp", 0.12f, 0, 0.05f, 0, "") }, 0xff8a3a, "head", "console", "short", 0x2a2a2c, false);
                case "granta": case "taxi": return S("modern", 0.56f, 0x1f2022, 0x3a3c40, 0x29292b, 0x2a2c30, 0x4a4e56, "insert", true, W(0.185f, 0.022f, 3, 0x141414, false, 0.075f, 0.42f), 0.38f, 0.15f, "tubes", "#050505", "#6a6d72", "#ffffff", "#ff3a2a",
                    new[] { G("speed", -0.09f, 0, 0.066f, 200, "км/ч"), G("rpm", 0.09f, 0, 0.066f, 8, "x1000") }, 0xffb070, "screen", "console", "short", 0x2a2a2c, false);
                case "vesta": return S("modern", 0.58f, 0x18191b, 0xa8adb4, 0x232325, 0x1c1d20, 0x6a2a2a, "insert", true, W(0.185f, 0.024f, 3, 0x111111, false, 0.08f, 0.38f, true), 0.4f, 0.16f, "tubes", "#030303", "#c0c4ca", "#ffffff", "#ff2a2a",
                    new[] { G("speed", -0.11f, 0, 0.064f, 220, "км/ч"), G("rpm", 0.11f, 0, 0.064f, 8, "x1000"), G("display", 0, 0, 0.04f, 0, "") }, 0xf2f6ff, "tablet", "console", "short", 0x252527, false);
                case "largus": return S("modern", 0.52f, 0x2a2a2c, 0x4a4c50, 0x2e2e30, 0x3a3c40, 0x26282c, "checks", true, W(0.19f, 0.024f, 3, 0x181818, false, 0.08f, 0.44f), 0.36f, 0.15f, "tubes", "#070707", "#7a7d82", "#ffffff", "#ff3a2a",
                    new[] { G("rpm", -0.11f, -0.005f, 0.048f, 7, "x1000"), G("speed", 0.005f, 0.005f, 0.066f, 200, "км/ч"), G("fuelTemp", 0.115f, -0.005f, 0.046f, 0, "") }, 0x9fe0ff, "head", "console", "short", 0x303032, false, false, true);
                default: // vaz2107, police
                    return S("block", 0.44f, 0x181818, 0x5e3a20, 0x262626, 0x3d4a5c, 0x2a3442, "velour", true, W(0.2f, 0.017f, 2, 0x121212, false, 0.09f, 0.58f), 0.44f, 0.16f, "rect", "#0a0a0a", "#7a7a7a", "#e8ffe8", "#ff4a3a",
                        new[] { G("speed", -0.1f, 0, 0.066f, 200, "км/ч"), G("rpm", 0.08f, 0, 0.062f, 8, "x1000"), G("fuel", -0.19f, 0, 0.026f, 0, ""), G("temp", 0.18f, 0, 0.026f, 0, "") }, 0x7cff9a, "ural", "console", "long", 0x2c2c2e, true, true);
            }
        }

        // ------------------------------------------------------------------ состояние
        public GameObject root;
        public Character driver;
        public HumanRig hr;                   // готовая модель водителя (если есть), иначе driver
        public Vector3 eye;                   // точка глаз водителя (в осях кузова Unity)
        InteriorStyle st;
        Transform spin, wheelTilt, clockH, clockM;
        readonly List<Transform> wipers = new List<Transform>();
        readonly List<KeyValuePair<string, Transform>> needles = new List<KeyValuePair<string, Transform>>();
        readonly List<Vector3> needleSpan = new List<Vector3>();  // startF, spanF, max
        Material faceMat;
        TextMesh screenText;
        Color backlight;
        float steerA, wipePhase;
        bool firstPerson;

        static float Interp(float[] pts, float z)
        {
            if (z >= pts[0]) return pts[1];
            for (int i = 2; i < pts.Length; i += 2)
            {
                float z1 = pts[i], y1 = pts[i + 1], z0 = pts[i - 2], y0 = pts[i - 1];
                if (z >= z1) return y1 + (y0 - y1) * (z - z1) / ((z0 - z1) == 0 ? 1 : (z0 - z1));
            }
            return pts[pts.Length - 1];
        }

        static int Hex(string s) { return System.Convert.ToInt32(s.Substring(1), 16); }

        // детали в осях веб-версии
        PaletteMesh P;
        void At(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) { P.xf = Matrix4x4.TRS(new Vector3(-x, y, z), PaletteMesh.Q3(rx, ry, rz), Vector3.one); }
        void Box(float w, float h, float d, int hex, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) { At(x, y, z, rx, ry, rz); P.Box(Vector3.zero, new Vector3(w, h, d), hex); }
        void RBox(float w, float h, float d, float r, int hex, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) { At(x, y, z, rx, ry, rz); P.RBox(Vector3.zero, new Vector3(w, h, d), r, hex); }
        void Cyl(float r, float h, int hex, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, int seg = 10) { At(x, y, z, rx, ry, rz); P.Cyl(Vector3.zero, r, r, h, hex, seg); }
        void Sph(float r, int hex, float x, float y, float z) { At(x, y, z); P.Sphere(Vector3.zero, r, hex, 10, 8); }

        /// <summary>Обшивка двери: вертикальная панель на x от пола до линии окон.</summary>
        void Side(float x, float z0, float z1, float yb, System.Func<float, float> top, int hex, float inward)
        {
            P.xf = Matrix4x4.identity;
            for (int i = 0; i < 8; i++)
            {
                float za = z0 + (z1 - z0) * i / 8f, zb = z0 + (z1 - z0) * (i + 1) / 8f;
                P.Quad(new Vector3(-x, yb, za), new Vector3(-x, yb, zb), new Vector3(-x, top(zb), zb), new Vector3(-x, top(za), za), new Vector3(-inward, 0, 0), hex);
            }
        }

        public static Interior Build(CarDef def, Transform body)
        {
            var it = new Interior();
            it.Make(def, body);
            return it;
        }

        void Make(CarDef def, Transform body)
        {
            string key = def.id;
            st = Style(key == "vaz2108" || key == "vaz2110" ? "vaz2109" : key == "kalina" ? "granta" : key);   // новые модели — салон по аналогии
            var A = InteriorAnchors.Get(key == "police" ? "vaz2107" : key);
            root = new GameObject("Interior");
            root.transform.SetParent(body, false);
            P = new PaletteMesh();

            float W2 = A.W / 2;
            System.Func<float, float> belt = (z) => Interp(A.belt, z);
            System.Func<float, float> roofAt = (z) => Interp(A.top, z);
            float floorY = A.floor + 0.12f;
            float hwB = W2 - 0.085f, hwR = W2 - A.tumble - 0.07f;
            float zDashF = A.zW0 + 0.04f, zDash = A.zW0 - st.depth;
            float yDashTop = belt(A.zW0) + (st.dash == "block" || st.dash == "angular" ? 0.03f : 0f);
            float zr0 = A.zW1 - 0.03f, zr1 = A.zB1 + 0.03f;
            float roofMin = 9; for (float z = zr1; z <= zr0; z += 0.1f) roofMin = Mathf.Min(roofMin, roofAt(z));
            float ceiling = roofMin - 0.055f;
            float Hz = zDash - 0.68f;
            float Hy = Mathf.Min(Mathf.Max(ceiling - 0.95f, floorY + 0.12f), floorY + (key == "niva" ? 0.26f : 0.22f));
            const float DX = 0.36f;
            int dark = 0x121212, chrome = 0xb8bcc0;

            // пол и тоннель
            float zRear = Mathf.Max(A.zB0 + 0.05f, Hz - 1.35f);
            Box(hwB * 2, 0.03f, zDashF - zRear, 0x1a1a1a, 0, floorY - 0.015f, (zDashF + zRear) / 2);
            Box(0.24f, 0.1f, zDashF - zRear - 0.2f, 0x1c1c1c, 0, floorY + 0.05f, (zDashF + zRear) / 2 + 0.1f);
            // торпедо
            float dashH = 0.27f, dashD = zDashF - zDash;
            if (st.dash == "modern")
            {
                RBox(hwB * 2, dashH, dashD, 0.08f, st.dashColor, 0, yDashTop - dashH / 2, zDash + dashD / 2);
                RBox(hwB * 2 - 0.06f, 0.035f, 0.05f, 0.015f, st.strip, 0, yDashTop - 0.17f, zDash + 0.01f);
                RBox(0.26f, 0.42f, 0.22f, 0.04f, st.dashColor, 0, yDashTop - 0.36f, zDash + 0.08f);
                if (key == "vesta") { Box(0.03f, 0.4f, 0.02f, st.strip, 0.15f, yDashTop - 0.33f, zDash - 0.02f, 0, 0, 0.35f); Box(0.03f, 0.4f, 0.02f, st.strip, -0.15f, yDashTop - 0.33f, zDash - 0.02f, 0, 0, -0.35f); }
            }
            else if (st.dash == "angular")
            {
                Box(hwB * 2, dashH, dashD, st.dashColor, 0, yDashTop - dashH / 2, zDash + dashD / 2);
                Box(hwB * 2 - 0.04f, 0.06f, 0.12f, st.dashColor, 0, yDashTop - dashH + 0.02f, zDash - 0.05f, -0.4f);
                Box(0.24f, 0.36f, 0.16f, st.dashColor, 0, yDashTop - 0.38f, zDash + 0.04f);
            }
            else if (st.dash == "mini")
            {
                RBox(hwB * 2, 0.2f, dashD, 0.04f, st.dashColor, 0, yDashTop - 0.1f, zDash + dashD / 2);
                Box(hwB * 2 - 0.1f, 0.04f, 0.16f, 0x1a1a1a, 0, yDashTop - 0.22f, zDash + 0.06f);
            }
            else
            {
                Box(hwB * 2, dashH, dashD, st.dashColor, 0, yDashTop - dashH / 2, zDash + dashD / 2);
                Box(hwB * 2 - 0.02f, 0.05f, 0.012f, st.strip, 0, yDashTop - 0.06f, zDash - 0.004f);
                if (st.dash == "block") { Box(0.3f, 0.3f, 0.14f, st.dashColor, 0, yDashTop - 0.38f, zDash + 0.05f); Box(0.28f, 0.05f, 0.01f, st.strip, 0, yDashTop - 0.3f, zDash - 0.021f); }
            }
            Box(hwB * 2, 0.025f, A.zW0 - zDash + 0.05f, st.dashColor, 0, yDashTop + 0.005f, (A.zW0 + zDash) / 2 + 0.02f, 0.06f);
            Box(hwB * 2, yDashTop - dashH - floorY, 0.04f, 0x161616, 0, (yDashTop - dashH + floorY) / 2, zDashF - 0.06f);
            foreach (float vx in new[] { -hwB + 0.14f, -0.07f, 0.07f, hwB - 0.14f }) Box(0.1f, 0.045f, 0.01f, 0x0c0c0c, vx, yDashTop - 0.1f, zDash - 0.006f);
            Box(0.34f, 0.12f, 0.01f, st.dashColor + 0x060606, -0.38f, yDashTop - 0.18f, zDash - 0.006f);
            // козырёк щитка
            float cy = yDashTop - st.ch / 2 + 0.03f, cz = zDash - 0.025f;
            int bezel = st.hood == "angular" || st.hood == "rect" ? 0x0e0e0e : 0x151515;
            if (st.hood == "oval" || st.hood == "tubes") RBox(st.cw + 0.05f, st.ch + 0.05f, 0.04f, 0.02f, bezel, DX, cy, zDash);
            else Box(st.cw + 0.04f, st.ch + 0.04f, 0.04f, bezel, DX, cy, zDash);
            float visorD = st.hood == "angular" ? 0.13f : st.dash == "modern" ? 0.1f : 0.07f;
            RBox(st.cw + 0.1f, 0.04f, visorD + 0.06f, 0.018f, st.dashColor, DX, cy + st.ch / 2 + 0.035f, zDash - visorD / 2 + 0.03f, st.hood == "angular" ? -0.12f : 0);
            Box(st.cw + 0.08f, 0.07f, 0.06f, st.dashColor, DX, cy + st.ch / 2 - 0.005f, zDash + 0.01f);
            // магнитола
            if (st.radio != "none")
            {
                string kind = st.radio;
                float sw = kind == "tablet" ? 0.2f : kind == "screen" ? 0.15f : 0.18f, sh = kind == "tablet" ? 0.125f : kind == "screen" ? 0.06f : 0.05f;
                float sy = kind == "tablet" ? yDashTop + 0.04f : yDashTop - 0.24f, sz = kind == "tablet" ? zDash + 0.06f : zDash - 0.012f;
                Box(sw + 0.03f, sh + 0.04f, 0.03f, 0x0d0d0d, 0, sy, sz + 0.012f, kind == "tablet" ? 0.25f : 0);
                if (kind == "ural") foreach (float kx in new[] { -0.075f, 0.075f }) Cyl(0.012f, 0.02f, 0x8a8a8a, kx, sy, sz - 0.01f, Mathf.PI / 2);
                int scr = kind == "ural" ? 0x0f1a0f : kind == "head" ? 0x081018 : 0x10243c;
                Box(kind == "ural" ? sw * 0.5f : sw, sh, 0.004f, scr, 0, sy, sz - 0.004f, kind == "tablet" ? 0.25f : 0);
                screenText = City.Text3D(root.transform, "", new Vector3(0, sy, sz - 0.008f), 0.0016f, kind == "ural" ? new Color(0.49f, 1f, 0.54f) : kind == "head" ? new Color(1f, 0.68f, 0.35f) : Color.white);
                screenText.transform.localRotation = Quaternion.Euler(kind == "tablet" ? -0.25f * Mathf.Rad2Deg : 0, 0, 0);
                screenText.gameObject.layer = 2;
            }
            // часы на консоли
            if (st.clock)
            {
                float kx = 0, ky = st.radio != "none" ? yDashTop - 0.14f : yDashTop - 0.2f, kz = zDash - 0.01f;
                Cyl(0.04f, 0.012f, 0x8a8a8a, kx, ky, kz, Mathf.PI / 2, 0, 0, 16);
                Cyl(0.035f, 0.014f, 0xf0f0e8, kx, ky, kz - 0.002f, Mathf.PI / 2, 0, 0, 16);
                var hp = new GameObject("ClockHands").transform; hp.SetParent(root.transform, false);
                hp.localPosition = new Vector3(-kx, ky, kz - 0.011f); hp.localRotation = PaletteMesh.Q3(0, Mathf.PI, 0);
                clockH = Hand(hp, 0.004f, 0.022f); clockM = Hand(hp, 0.003f, 0.03f);
            }
            // консоль, рычаги, педали
            if (st.console == "console" || st.console == "niva") RBox(0.2f, 0.16f, 0.5f, 0.03f, st.dashColor, 0, floorY + 0.12f, Hz + 0.25f);
            if (st.console == "niva")
            {
                for (int i = 0; i < 3; i++) Cyl(0.022f, 0.012f, 0x9a9a9a, -0.05f + i * 0.05f, yDashTop - 0.2f, zDash - 0.008f, Mathf.PI / 2, 0, 0, 12);
                Cyl(0.006f, 0.22f, chrome, 0.05f, floorY + 0.2f, Hz + 0.42f, -0.4f); Sph(0.018f, dark, 0.05f, floorY + 0.3f, Hz + 0.37f);
            }
            if (st.lever == "long")
            {
                Cyl(0.007f, 0.42f, chrome, 0, floorY + 0.25f, Hz + 0.42f, -0.3f);
                Sph(0.025f, dark, 0, floorY + 0.45f, Hz + 0.36f);
                Cyl(0.05f, 0.05f, 0x111111, 0, floorY + 0.1f, Hz + 0.47f);
            }
            else
            {
                Cyl(0.04f, 0.08f, 0x111111, 0, floorY + 0.22f, Hz + 0.3f, -0.2f);
                Cyl(0.008f, 0.14f, 0x222222, 0, floorY + 0.3f, Hz + 0.28f, -0.2f);
                RBox(0.04f, 0.06f, 0.05f, 0.015f, st.radio == "tablet" ? 0x2a2a2a : dark, 0, floorY + 0.38f, Hz + 0.26f);
            }
            Box(0.03f, 0.03f, 0.22f, 0x161616, 0, floorY + (st.console == "tunnel" ? 0.13f : 0.22f), Hz - 0.02f, 0.22f);
            for (int i = 0; i < 3; i++) Box(0.055f, 0.075f, 0.015f, 0x202020, DX + 0.12f - i * 0.1f - (i == 2 ? 0.02f : 0), floorY + 0.14f, Hz + 0.95f, 0.5f);
            // кресла
            float seatY = Hy - 0.05f;
            Seat(DX, Hz + 0.12f, seatY, 0.5f, 1);
            Seat(-DX, Hz + 0.12f, seatY, 0.5f, 1);
            float rearZ = Hz - 0.8f;
            if (rearZ - 0.3f > A.zB0 - 0.35f) Seat(0, rearZ, seatY + 0.02f, hwB * 2 - 0.12f, 2);
            if (!st.hatch && A.zB0 < rearZ - 0.3f) Box(hwB * 2, 0.02f, rearZ - 0.32f - A.zB0, 0x1e1e1e, 0, belt(A.zB0) - 0.02f, (rearZ - 0.32f + A.zB0) / 2);
            // обшивка дверей
            float zF = zDash + 0.12f, zB = zRear - 0.05f;
            foreach (float sx in new[] { 1f, -1f })
            {
                Side(sx * (W2 - 0.07f), zB, zF, floorY - 0.02f, (z) => belt(z) - 0.008f, st.door, -sx);
                Box(0.06f, 0.04f, 0.42f, st.trim, sx * (W2 - 0.1f), Hy + 0.15f, Hz + 0.2f);
                Box(0.012f, 0.025f, 0.08f, chrome, sx * (W2 - 0.078f), belt(Hz + 0.45f) - 0.12f, Hz + 0.45f);
                if (st.crank) { Cyl(0.03f, 0.01f, chrome, sx * (W2 - 0.08f), Hy + 0.05f, Hz + 0.35f, 0, 0, Mathf.PI / 2); Cyl(0.008f, 0.05f, 0x111111, sx * (W2 - 0.095f), Hy + 0.07f, Hz + 0.38f, 0, 0, Mathf.PI / 2); }
                Box(0.02f, 0.12f, 0.5f, 0x161616, sx * (W2 - 0.08f), floorY + 0.12f, Hz + 0.3f);
            }
            // потолок, плафон, козырьки, зеркало
            Box(hwR * 2, 0.02f, zr0 - zr1, 0xc9c4b8, 0, roofMin - 0.045f, (zr0 + zr1) / 2);
            Box(0.12f, 0.02f, 0.06f, 0xeeeeee, 0, roofMin - 0.058f, (zr0 + zr1) / 2);
            foreach (float sx in new[] { 1f, -1f }) Box(0.32f, 0.016f, 0.16f, 0xc0bab0, sx * 0.36f, roofMin - 0.07f, A.zW1 - 0.1f, 0.12f);
            RBox(0.22f, 0.06f, 0.025f, 0.012f, 0x1a1a1a, 0, roofMin - 0.11f, A.zW1 - 0.08f, -0.15f);
            Box(0.2f, 0.045f, 0.004f, 0x9fb2c0, 0, roofMin - 0.11f, A.zW1 - 0.094f, -0.15f);
            var shell = P.ToObject("InteriorShell", root.transform, PaletteMesh.FillMaterial);

            // дворники в плоскости лобового стекла
            {
                float yW0 = roofAt(A.zW0) + 0.015f, yW1 = roofAt(A.zW1);
                var up = new Vector3(0, yW1 - yW0, A.zW1 - A.zW0).normalized;
                var nrm = new Vector3(0, -up.z, up.y);
                float L = Mathf.Min(0.5f, hwB * 0.62f);
                foreach (float px in new[] { 0.5f, -0.12f })
                {
                    var b = new GameObject("Wiper").transform; b.SetParent(root.transform, false);
                    b.localPosition = new Vector3(-px, yW0, A.zW0 + 0.02f) + nrm * 0.02f;
                    b.localRotation = Quaternion.LookRotation(nrm, up);
                    var sp = new GameObject("Spin").transform; sp.SetParent(b, false);
                    var wm = new PaletteMesh();
                    wm.Box(new Vector3(L / 2, 0, 0.006f), new Vector3(L, 0.016f, 0.012f), 0x111111);
                    wm.Box(new Vector3(L, 0, 0.012f), new Vector3(0.02f, L * 0.95f, 0.02f), 0x111111);
                    wm.ToObject("Arm", sp);
                    wipers.Add(sp);
                }
            }

            // щиток: текстура + стрелки
            var cr = new GameObject("Cluster").transform; cr.SetParent(root.transform, false);
            cr.localPosition = new Vector3(-DX, cy, cz); cr.localRotation = PaletteMesh.Q3(0, Mathf.PI, 0);
            var tilt = new GameObject("Tilt").transform; tilt.SetParent(cr, false); tilt.localRotation = PaletteMesh.Q3(-0.12f, 0, 0);
            var tex = ClusterTexture.Draw(st);
            backlight = M.Hex(st.backlight);
            faceMat = Mats.SimpleEmissive().Tex(tex).EmissionMap(tex).Emission(backlight * 0.25f);
            var face = new GameObject("Face"); face.transform.SetParent(tilt, false);
            var fm = new Mesh { name = "ClusterFace" };
            float hw = st.cw / 2, hh = st.ch / 2;
            fm.vertices = new[] { new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0), new Vector3(hw, hh, 0), new Vector3(-hw, hh, 0) };
            fm.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
            fm.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            fm.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            FixWinding(fm);
            face.AddComponent<MeshFilter>().sharedMesh = fm;
            face.AddComponent<MeshRenderer>().sharedMaterial = faceMat;
            var needleMat = Mats.Unlit().Col(M.Hex(key == "vesta" ? 0xff3030 : 0xff6a1a));
            foreach (var g in st.gauges)
            {
                if (g.type == "display") continue;
                if (g.type == "combo" || g.type == "fuelTemp") { Needle(tilt, g, "fuel", 0, 0.45f, needleMat); Needle(tilt, g, "temp", 0.55f, 0.45f, needleMat); }
                else Needle(tilt, g, g.type, 0, 1, needleMat);
            }

            // руль
            var ws = st.wheel;
            var wr = new GameObject("SteeringWheel").transform; wr.SetParent(root.transform, false);
            wr.localPosition = new Vector3(-DX, Hy + 0.33f, Hz + 0.47f); wr.localRotation = PaletteMesh.Q3(0, Mathf.PI, 0);
            wheelTilt = new GameObject("Tilt").transform; wheelTilt.SetParent(wr, false); wheelTilt.localRotation = PaletteMesh.Q3(-ws.tilt, 0, 0);
            spin = new GameObject("Spin").transform; spin.SetParent(wheelTilt, false);
            P = new PaletteMesh();
            At(0, 0, 0); P.Torus(Vector3.zero, ws.r, ws.t, ws.color);
            Cyl(ws.pad * 0.9f, 0.05f, ws.color == 0x111111 ? 0x1a1a1a : ws.color, 0, 0, 0.02f, Mathf.PI / 2, 0, 0, 16);
            Cyl(ws.pad * 0.35f, 0.052f, key.StartsWith("vaz2") || key == "niva" || key == "oka" || key == "police" ? 0xb0b4b8 : 0x9aa6b8, 0, 0, 0.022f, Mathf.PI / 2, 0, 0, 12);
            float[] spokeAng = ws.spokes == 2 ? new[] { 200f, 340f } : ws.spokes == 3 ? new[] { 180f, 0f, 270f } : new[] { 215f, 325f, 145f, 35f };
            foreach (float a in spokeAng)
            {
                float r = a * Mathf.Deg2Rad, len = ws.r - ws.pad * 0.6f;
                Box(len, ws.spokes == 2 ? 0.022f : 0.04f, 0.014f, ws.color, Mathf.Cos(r) * (ws.pad * 0.6f + len / 2), Mathf.Sin(r) * (ws.pad * 0.6f + len / 2), 0.008f, 0, 0, r);
            }
            if (ws.ring) { At(0, 0, 0.02f); P.Torus(Vector3.zero, ws.r * 0.62f, 0.004f, chrome, 4, 28); }
            if (ws.buttons) foreach (float bx in new[] { -0.08f, 0.08f }) Box(0.04f, 0.03f, 0.006f, 0x2a2a2a, bx, -0.02f, 0.016f);
            P.ToObject("Rim", spin, PaletteMesh.FillMaterial);
            P = new PaletteMesh();
            At(0, 0, -0.17f, Mathf.PI / 2); P.Cyl(Vector3.zero, 0.035f, 0.045f, 0.32f, 0x151515, 10);
            P.ToObject("Column", wheelTilt);

            // водитель
            driver = new Character(Outfit.Player, root.transform, false, 1);
            driver.root.localPosition = new Vector3(-DX, Hy - 0.98f + 0.04f, Hz - 0.04f);
            driver.Bone("spine", -0.24f, 0, 0); driver.Bone("chest", 0.02f, 0, 0); driver.Bone("neck", 0.1f, 0, 0); driver.Bone("head", 0.08f, 0, 0);
            foreach (var s in new[] { "L", "R" })
            {
                float sx = s == "L" ? 1 : -1;
                driver.Bone("thigh" + s, -1.42f, 0, sx * 0.1f);
                driver.Bone("shin" + s, 1.05f, 0, 0);
                driver.Bone("foot" + s, -0.25f, 0, 0);
            }
            hr = HumanRig.Create(root.transform, "Human_Player", 1.78f, true);
            Vector3 headLocal;
            if (hr != null)
            {
                driver.smr.enabled = false;
                hr.root.localPosition = new Vector3(-DX, Hy + 0.04f - hr.restHips, Hz - 0.04f);
                SeatDriver(0f, 0f);
                headLocal = root.transform.InverseTransformPoint(hr.Head.position);
            }
            else headLocal = root.transform.InverseTransformPoint(driver.B["head"].position);
            eye = new Vector3(-(DX - 0.035f), headLocal.y + 0.1f, headLocal.z + 0.09f);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        }

        static void FixWinding(Mesh m)
        {
            var v = m.vertices; var t = m.triangles; var n = m.normals;
            for (int i = 0; i < t.Length; i += 3)
                if (Vector3.Dot(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]), n[t[i]]) < 0) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            m.triangles = t;
            m.RecalculateBounds();
        }

        Transform Hand(Transform parent, float w, float len)
        {
            var p = new GameObject("Hand").transform; p.SetParent(parent, false);
            var pm = new PaletteMesh();
            pm.Box(new Vector3(0, len / 2, 0.001f), new Vector3(w, len, 0.002f), 0x111111);
            pm.ToObject("H", p);
            return p;
        }

        void Needle(Transform tilt, Gauge g, string type, float startF, float spanF, Material mat)
        {
            var pv = new GameObject("Needle_" + type).transform; pv.SetParent(tilt, false);
            pv.localPosition = new Vector3(-g.x, g.y, 0.006f);
            var pm = new PaletteMesh();
            pm.Box(new Vector3(-g.r * 0.36f, 0, 0), new Vector3(g.r * 0.82f, Mathf.Max(0.0025f, g.r * 0.05f), 0.002f), 0xffffff);
            pm.ToObject("N", pv, mat);
            var cap = new PaletteMesh();
            cap.xf = Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0));
            cap.Cyl(Vector3.zero, g.r * 0.1f, g.r * 0.1f, 0.004f, 0x222222, 10);
            cap.ToObject("Cap", pv);
            needles.Add(new KeyValuePair<string, Transform>(type, pv));
            needleSpan.Add(new Vector3(startF, spanF, g.max));
        }

        void Seat(float x, float sz, float sy, float seatW, int back)
        {
            int c1 = st.seat, c2 = st.seat2;
            RBox(seatW, 0.13f, 0.5f, 0.05f, c1, x, sy, sz, -0.08f);
            if (st.pattern != "plain") RBox(seatW * 0.55f, 0.012f, 0.4f, 0.005f, c2, x, sy + 0.066f, sz, -0.08f);
            float a = -0.24f, bh = 0.6f, by = sy + 0.06f + (bh / 2) * Mathf.Cos(a), bz = sz - 0.24f + (bh / 2) * Mathf.Sin(a);
            RBox(seatW, bh, 0.13f, 0.05f, c1, x, by, bz, a);
            if (st.pattern != "plain")
            {
                float ux = Mathf.Cos(a), uz = Mathf.Sin(a), ny = -Mathf.Sin(a) * 0.066f, nz = Mathf.Cos(a) * 0.066f;
                int ins = st.pattern == "checks" ? 6 : st.pattern == "stripes" ? 7 : 1;
                if (ins == 1) RBox(seatW * 0.55f, bh * 0.75f, 0.012f, 0.005f, c2, x, by + ny, bz + nz, a);
                else for (int i = 0; i < ins; i++)
                    {
                        float t = (-0.38f + i * (0.76f / (ins - 1))) * bh;
                        Box(seatW * 0.58f, st.pattern == "checks" ? 0.05f : 0.022f, 0.012f, (i % 2 == 1 && st.pattern == "checks") ? c1 : c2, x, by + t * ux + ny, bz + t * uz + nz, a);
                    }
            }
            if (st.headrest && back == 1)
            {
                float hy = sy + 0.06f + (bh + 0.1f) * Mathf.Cos(a), hz = sz - 0.24f + (bh + 0.1f) * Mathf.Sin(a);
                RBox(0.26f, 0.17f, 0.09f, 0.04f, c1, x, hy, hz, a);
                Cyl(0.006f, 0.06f, 0xb8bcc0, x + 0.06f, hy - 0.1f, hz + 0.01f); Cyl(0.006f, 0.06f, 0xb8bcc0, x - 0.06f, hy - 0.1f, hz + 0.01f);
            }
        }

        // ------------------------------------------------------------------ кадр
        static float ValueToAngle(float f) { return (225f - 270f * Mathf.Clamp01(f)) * Mathf.Deg2Rad; }

        public void SetRadio(string name, string freq)
        {
            if (screenText == null) return;
            bool off = string.IsNullOrEmpty(freq);
            screenText.text = st.radio == "ural" ? (off ? "--.- FM" : freq + " FM") : off ? (st.radio == "head" ? "OFF" : "Радио выкл") : name + "\n" + freq + " FM";
        }

        /// <summary>В виде из салона камера в голове водителя — голову прячем.</summary>
        public bool FirstPerson => firstPerson;
        public void SetFirstPerson(bool v) { firstPerson = v; if (hr != null) hr.HideHead(v); else driver.B["neck"].localScale = Vector3.one * (v ? 0.001f : 1f); }

        public void SetDriverVisible(bool v) { if (hr != null) hr.root.gameObject.SetActive(v); else driver.SetVisible(v); }

        /// <summary>Поза водителя: стойка из клипа Idle, ноги сидя, голова поворачивается за рулём.</summary>
        void SeatDriver(float dt, float steer)
        {
            hr.Tick(dt, 0f, false, 0f, false, 0f);
            hr.SeatedLegs(hr.root.forward, 1f, 0.14f, 0.4f);
            hr.LeanSpine(-0.12f, 1f);
            hr.B["head"].rotation = Quaternion.AngleAxis(steer * 20f, hr.root.up) * hr.B["head"].rotation;
        }

        public void UpdateState(float dt, VehiclePhysics p, bool lights, float night, float clock, bool wipersOn)
        {
            float steer = p.steer / Mathf.Max(0.1f, p.spec.maxSteer);
            float target = -steer * 2.62f;
            steerA += (target - steerA) * Mathf.Min(1f, dt * 18f);
            spin.localRotation = PaletteMesh.Q3(0, 0, steerA);
            for (int i = 0; i < needles.Count; i++)
            {
                var sp = needleSpan[i]; string type = needles[i].Key; float f;
                if (type == "speed") f = Mathf.Abs(p.speed) * 3.6f / sp.z;
                else if (type == "rpm") f = p.rpm / 1000f / sp.z;
                else if (type == "fuel") f = sp.x + p.fuel / p.spec.tank * sp.y;
                else f = sp.x + Mathf.Min(1f, 0.25f + Time.time / 240f) * 0.5f * sp.y;
                needles[i].Value.localRotation = PaletteMesh.Q3(0, 0, ValueToAngle(f));
            }
            if (wipersOn || wipePhase % (Mathf.PI * 2) > 0.05f)
            {
                wipePhase += dt * 3.2f;
                if (!wipersOn && wipePhase % (Mathf.PI * 2) < 0.2f) wipePhase = 0;
            }
            float sweep = (1 - Mathf.Cos(wipePhase)) / 2 * 1.9f;
            foreach (var w in wipers) w.localRotation = PaletteMesh.Q3(0, 0, -sweep);
            faceMat.SetColor("_EmissionColor", backlight * (lights ? 1.1f : 0.35f));
            float fill = 0.08f + (1f - night) * 0.32f + (lights ? 0.04f : 0f);
            PaletteMesh.FillMaterial.SetColor("_EmissionColor", Color.white * fill);
            if (clockH)
            {
                float h = clock % 12f, m = (clock % 1f) * 60f;
                clockH.localRotation = PaletteMesh.Q3(0, 0, -h / 12f * Mathf.PI * 2);
                clockM.localRotation = PaletteMesh.Q3(0, 0, -m / 60f * Mathf.PI * 2);
            }
            // руки на руле: «10 и 2», следуют за ободом до ±90°, дальше перехватывают
            if (hr != null)
            {
                if (hr.root.gameObject.activeInHierarchy)
                {
                    SeatDriver(dt, steer);
                    foreach (var side in new[] { 'L', 'R' })
                    {
                        float a = (side == 'L' ? 150f : 30f) * Mathf.Deg2Rad + Mathf.Clamp(steerA, -1.55f, 1.55f);
                        var tgt = wheelTilt.TransformPoint(new Vector3(-Mathf.Cos(a) * st.wheel.r, Mathf.Sin(a) * st.wheel.r, 0.035f));
                        hr.ArmIK(side, tgt, 1f, hr.root.up * -0.6f + hr.root.right * (side == 'L' ? -0.5f : 0.5f) - hr.root.forward * 0.3f);
                    }
                    hr.UpdateAttach();
                }
            }
            else if (driver.root.gameObject.activeInHierarchy)
            {
                driver.Bone("head", 0.08f, -steer * 0.35f, 0);
                float lim = Mathf.Clamp(steerA, -1.55f, 1.55f);
                foreach (var side in new[] { 'L', 'R' })
                {
                    float a = (side == 'L' ? 150f : 30f) * Mathf.Deg2Rad + lim;
                    var tgt = wheelTilt.TransformPoint(PaletteMesh.Q3(0, 0, 0) * new Vector3(-Mathf.Cos(a) * st.wheel.r, Mathf.Sin(a) * st.wheel.r, 0.035f));
                    driver.ArmIK(side, tgt, 1f, new Vector3(0.6f, -1f, -0.4f));
                    driver.Bone(side == 'L' ? "handL" : "handR", 0.5f, 0, (side == 'L' ? 1 : -1) * 0.3f);
                }
            }
        }

        public void Destroy()
        {
            if (root) Object.Destroy(root);
            if (faceMat) { Object.Destroy(faceMat.mainTexture); Object.Destroy(faceMat); }
        }
    }
}
