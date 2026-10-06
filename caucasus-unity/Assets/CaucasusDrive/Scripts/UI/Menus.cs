using UnityEngine;
using UnityEngine.UI;

namespace CaucasusDrive
{
    /// <summary>3D-гараж ГСК «Жигули»: поворотный круг, кирпичные стены, лампы. Камера облетает машину.</summary>
    public class GarageStage
    {
        public readonly Transform root;
        readonly Transform turntable;
        public CarVisual car;
        float angle, drag;
        static readonly Vector3 Origin = new Vector3(2000, 0, 2000);

        public GarageStage(Transform parent)
        {
            root = new GameObject("Garage").transform;
            root.SetParent(parent, false);
            root.position = Origin;
            var b = new MeshBuilder();
            b.Flat(0, 0, 0, 30, 30, 0, 4f);
            b.ToObject("Floor", Mats.Lit().Tex(Mats.Concrete, 6, 6).Col(M.Hex(0x8a8a8a)).Pbr(0.1f, 0.55f), root, false).transform.localPosition = Vector3.zero;
            var w = new MeshBuilder();
            w.Wall(-9, 7, 9, 7, 0, 6, 3, 3);   // задняя стена (смотрит к камере, в −Z)
            w.Wall(9, 7, 9, -9, 0, 6, 3, 3);
            w.Wall(-9, -9, -9, 7, 0, 6, 3, 3);
            w.ToObject("Walls", Mats.Simple().Tex(Mats.Brick), root, false);
            var disc = new MeshBuilder();
            disc.CylinderY(new Vector3(0, 0, 0), 3.5f, 3.4f, 0.12f, 48, true);
            disc.ToObject("Turntable", Mats.Lit().Col(M.Hex(0x2a2d31)).Pbr(0.6f, 0.65f), root, false);
            // стеллаж с шинами у стены
            var shelf = new MeshBuilder();
            for (int i = 0; i < 4; i++) shelf.CylinderX(new Vector3(-6.5f + i * 0.5f, 0.38f, 6.2f), 0.36f, 0.22f, 16);
            shelf.ToObject("Tires", Mats.Simple().Col(M.Hex(0x151515)), root, false);
            turntable = new GameObject("Spin").transform;
            turntable.SetParent(root, false);
            turntable.localPosition = new Vector3(0, 0.12f, 0);
            foreach (var p in new[] { new Vector3(3, 5, -3), new Vector3(-4, 4.5f, 4) })
            {
                var l = new GameObject("Lamp").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = p;
                l.type = LightType.Point; l.range = 14; l.intensity = 2.2f; l.color = new Color(1f, 0.95f, 0.88f);
                l.shadows = LightShadows.None;
            }
            root.gameObject.SetActive(false);
        }

        public void Show(bool on) { root.gameObject.SetActive(on); }

        public void SetCar(CarDef def, CarTune t, int color, string lod)
        {
            if (car != null) car.Destroy();
            car = CarVisual.Build(def, t, color, lod, false, turntable);
            CarCare.ApplyLook(car.mats, t, color);
            car.root.transform.localPosition = new Vector3(0, 0, -(def.dims.front + def.dims.rear) / 2f);
        }

        public void Drag(float dx) { drag = dx * 0.01f; angle += drag; }

        public void Update(float dt, Camera cam, float offsetX)
        {
            drag *= Mathf.Exp(-dt * 3f);
            angle += dt * 0.25f + drag * 0.02f;
            turntable.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
            var target = Origin + new Vector3(0, 0.75f, 0);
            cam.transform.position = Origin + new Vector3(-offsetX, 2.1f, -7.6f);
            cam.transform.rotation = Quaternion.LookRotation(target + new Vector3(-offsetX, 0, 0) - cam.transform.position);
            cam.fieldOfView = 38f;
        }
    }

    /// <summary>Меню: главное, уровни, гараж (покупка и тюнинг), настройки, пауза, итог уровня.</summary>
    public class Menus
    {
        readonly App app;
        public readonly Canvas canvas;
        readonly RectTransform root;
        public string current;
        int garageIndex;
        int tab;
        static readonly string[] Tabs = { "Цвет", "Диски", "Подвеска", "Тонировка", "Мотор", "Шины", "Неон", "Сигнал", "Выхлоп", "Номер" };
        static readonly string[] ExhaustNames = { "Заводской", "Прямоток (стреляет)" };
        static readonly int[] ExhaustPrices = { 0, 9000 };
        string[] plateSlots;
        string plateFor;
        bool dontAsk;
        static readonly string[][] PlatePresets = {
            new[] { "Е213КХ", "26" }, new[] { "М512ОК", "05" }, new[] { "В340НА", "06" }, new[] { "Т118РС", "07" },
            new[] { "Н622АУ", "09" }, new[] { "К404ОТ", "15" }, new[] { "Е555ХМ", "95" }, new[] { "А777АА", "05" }, new[] { "Х005ХХ", "06" }, new[] { "О001ОО", "95" },
        };
        static readonly int[] PlatePrices = { 0, 1500, 1500, 1500, 1500, 1500, 2500, 25000, 30000, 45000 };

        public Menus(App a)
        {
            app = a;
            canvas = UIKit.MakeCanvas("Menus", 20);
            root = UIKit.Fill(canvas.transform, "Root");
            garageIndex = Cars.IndexOf(app.save.d.current);
        }

        RectTransform Screen(bool dim)
        {
            UIKit.Clear(root);
            var s = UIKit.Fill(root, "Screen");
            if (dim) UIKit.Img(s, new Color(0, 0, 0, 0.55f), false);
            return s;
        }

        public void Hide() { UIKit.Clear(root); current = null; }

        /// <summary>Сообщение поверх меню (HUD в меню скрыт).</summary>
        public void Toast(string text, int kind = 0, float sec = 2f)
        {
            var rt = UIKit.Rect(canvas.transform, "Toast", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(620, 54));
            UIKit.Img(rt, kind == HUD.Bad ? new Color(0.45f, 0.1f, 0.08f, 0.95f) : new Color(0.1f, 0.35f, 0.18f, 0.95f));
            UIKit.Label(rt, text, 22, Color.white);
            Object.Destroy(rt.gameObject, sec);
        }

        public void Show(string name)
        {
            current = name;
            app.OnMenu(name);
            switch (name)
            {
                case "main": Main(); break;
                case "levels": LevelsScreen(); break;
                case "garage": Garage(); break;
                case "settings": SettingsScreen(); break;
                case "pause": Pause(); break;
                case "help": Help(); break;
                case "freeask": FreeAsk(); break;
                case "daily": DailyScreen(); break;
                case "ach": AchScreen(); break;
            }
        }

        void TopBar(RectTransform s, string title, string back)
        {
            if (back != null) UIKit.Button(s, "‹", new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -46), new Vector2(64, 60), UIKit.Bg, () => Show(back), 40);
            UIKit.LabelAt(s, title, 34, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(700, 60));
            var m = UIKit.Panel(s, "Money", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-130, -46), new Vector2(220, 52), UIKit.Bg);
            UIKit.Label(m.transform, M.Rub(app.save.Money), 26, UIKit.Gold);
        }

        // ------------------------------------------------------------------ главное
        void Main()
        {
            var s = Screen(false);
            app.garageOffset = 0f;
            var cur = Cars.Get(app.save.d.current);
            app.garage.SetCar(cur, app.save.Tune(cur.id), app.save.ColorOf(cur.id), app.quality.carLod);
            var logo = UIKit.LabelAt(s, "<color=#ff6a1a>CAUCASUS</color> DRIVE", 64, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(330, -70), new Vector2(620, 90), TextAnchor.MiddleLeft);
            logo.supportRichText = true;
            logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);
            UIKit.LabelAt(s, "ГАРАЖ · ШАШКИ · ПАРКОВКА · ДРИФТ", 18, UIKit.Muted, new Vector2(0, 1), new Vector2(0, 1), new Vector2(330, -122), new Vector2(620, 30), TextAnchor.MiddleLeft);
            var m = UIKit.Panel(s, "Money", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-130, -46), new Vector2(220, 52), UIKit.Bg);
            UIKit.Label(m.transform, M.Rub(app.save.Money), 26, UIKit.Gold);
            var stars = UIKit.Panel(s, "Stars", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-350, -46), new Vector2(190, 52), UIKit.Bg);
            UIKit.Label(stars.transform, "★ " + app.save.StarsTotal + "/" + Levels.Count * 3, 24, Color.white);

            int ready = app.daily.ReadyCount;
            string[] titles = { "ПАРКОВКА", "СВОБОДНАЯ ЕЗДА", "ЭКЗАМЕН ГИБДД", "ДРИФТ-ЗОНА", "ГАРАЖ", "НАСТРОЙКИ" };
            string[] subs = { "30 уровней · ★ " + app.save.StarsTotal, "Город · такси · штрафы · пешком", app.save.d.license ? "Права получены · пересдать" : "Площадка + город · права", "ДОСААФ · 90 с · рекорд " + app.save.d.drift.best, Cars.Get(app.save.d.current).name + " · тюнинг", "Графика · управление · звук" };
            Color[] cols = { UIKit.Accent, M.Hex(0x1f6a8a), M.Hex(0x2b5d3a), M.Hex(0x6d2f86), UIKit.Bg2, UIKit.Bg2 };
            System.Action[] acts = { () => Show("levels"), () => { if (app.save.d.settings.askFines) Show("freeask"); else app.StartFree(app.save.d.settings.fines); }, () => app.StartExam(), () => app.StartDrift(), () => Show("garage"), () => Show("settings") };
            for (int i = 0; i < 6; i++)
            {
                int k = i;
                var b = UIKit.Button(s, "", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-320 + (i % 3) * 320, 255 - (i / 3) * 135), new Vector2(300, 122), cols[i], acts[k]);
                UIKit.LabelAt(b.transform, titles[i], 26, Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 76), new Vector2(-24, 36), TextAnchor.MiddleLeft);
                UIKit.LabelAt(b.transform, subs[i], 15, new Color(1, 1, 1, 0.75f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 38), new Vector2(-24, 30), TextAnchor.MiddleLeft, false);
            }
            UIKit.Button(s, ready > 0 ? "ЗАДАНИЯ · забрать " + ready : "ЗАДАНИЯ ДНЯ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-110, 40), new Vector2(260, 50), ready > 0 ? UIKit.Green : UIKit.Bg, () => Show("daily"), 18);
            UIKit.Button(s, "ДОСТИЖЕНИЯ " + Achievements.Count(app.save.d) + "/" + Achievements.All.Length, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 40), new Vector2(260, 50), UIKit.Bg, () => Show("ach"), 18);
            Achievements.Check(app);
            var tg = UIKit.Button(s, "Telegram: t.me/caucasusdrive", new Vector2(0, 0), new Vector2(0, 0), new Vector2(190, 40), new Vector2(340, 50), M.Hex(0x229ed9), () => Application.OpenURL("https://t.me/caucasusdrive"), 20);
            UIKit.Button(s, "Как играть", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-120, 40), new Vector2(200, 50), UIKit.Bg, () => Show("help"), 20);
            tg.name = "Telegram";
        }

        // ------------------------------------------------------------------ штрафы: спросить при входе в город
        void FreeAsk()
        {
            var s = Screen(true);
            var m = UIKit.Panel(s, "Modal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 400), UIKit.Bg);
            UIKit.LabelAt(m.transform, "Свободная езда", 36, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(560, 50));
            UIKit.LabelAt(m.transform, "Камеры «Стрелка», посты ДПС и нарушения ПДД снимают деньги.\nИграть со штрафами?", 19, UIKit.Muted, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(560, 70), TextAnchor.MiddleCenter, false);
            System.Action<bool> go = (f) =>
            {
                var st = app.save.d.settings;
                st.fines = f; if (dontAsk) st.askFines = false;
                app.save.Commit();
                app.StartFree(f);
            };
            UIKit.Button(m.transform, "СО ШТРАФАМИ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-145, 150), new Vector2(270, 70), M.Hex(0x8a2a1f), () => go(true), 22);
            UIKit.Button(m.transform, "БЕЗ ШТРАФОВ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(145, 150), new Vector2(270, 70), UIKit.Green, () => go(false), 22);
            UIKit.Button(m.transform, (dontAsk ? "[x]" : "[  ]") + " больше не спрашивать", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-60, 60), new Vector2(320, 46), UIKit.Bg2, () => { dontAsk = !dontAsk; Show("freeask"); }, 17);
            UIKit.Button(m.transform, "Назад", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(200, 60), new Vector2(140, 46), UIKit.Bg2, () => Show("main"), 17);
        }

        // ------------------------------------------------------------------ задания дня
        void DailyScreen()
        {
            var s = Screen(true);
            TopBar(s, "Задания дня", "main");
            app.daily.Refresh();
            var tasks = app.daily.Tasks;
            for (int i = 0; i < tasks.Count; i++)
            {
                int k = i; var t = tasks[i];
                var row = UIKit.Panel(s, "Task", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150 - i * 130), new Vector2(860, 112), UIKit.Bg);
                UIKit.LabelAt(row.transform, app.daily.Text(t), 22, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-150, -32), new Vector2(-330, 36), TextAnchor.MiddleLeft);
                string prog = t.kind == "km" ? t.progress.ToString("0.0") : Mathf.FloorToInt(t.progress).ToString();
                UIKit.LabelAt(row.transform, prog + " / " + t.goal + "   ·   награда " + M.Rub(app.daily.Reward(t)), 17, UIKit.Muted, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-150, 30), new Vector2(-330, 30), TextAnchor.MiddleLeft, false);
                var bar = UIKit.Panel(row.transform, "Bar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(-150, 10), new Vector2(-330, 6), new Color(1, 1, 1, 0.12f));
                var fill = UIKit.Img(UIKit.Fill(bar.transform, "Fill"), UIKit.Green, false);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.sprite = UIKit.Rounded; fill.fillAmount = Mathf.Clamp01(t.progress / t.goal);
                if (t.claimed) UIKit.Button(row.transform, "ПОЛУЧЕНО", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(190, 64), UIKit.Bg2, null, 18);
                else if (t.done) UIKit.Button(row.transform, "ЗАБРАТЬ", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(190, 64), UIKit.Green, () => { int r = app.daily.Claim(k); if (r > 0) { app.audio.Coin(); Toast("+" + M.Rub(r)); } Show("daily"); }, 22);
                else UIKit.Button(row.transform, "В ПРОЦЕССЕ", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(190, 64), UIKit.Bg2, null, 16);
            }
            UIKit.LabelAt(s, "Новые задания — каждый день. Статистика: " + app.save.d.stats.km.ToString("0") + " км, такси " + app.save.d.stats.taxi + ", штрафов " + M.Rub(app.save.d.stats.fines), 17, UIKit.Muted, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1000, 30), TextAnchor.MiddleCenter, false);
        }

        // ------------------------------------------------------------------ достижения
        void AchScreen()
        {
            var s = Screen(true);
            TopBar(s, "Достижения · " + Achievements.Count(app.save.d) + " из " + Achievements.All.Length, "main");
            var d = app.save.d;
            for (int i = 0; i < Achievements.All.Length; i++)
            {
                var a = Achievements.All[i];
                bool done = Achievements.Has(d, a.id);
                float v = Mathf.Min(a.value(d), a.goal);
                int col = i % 3, row = i / 3;
                var p = UIKit.Panel(s, "Ach", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400 + col * 400, 190 - row * 92), new Vector2(388, 84), done ? new Color(0.1f, 0.3f, 0.16f, 0.92f) : UIKit.Bg);
                UIKit.LabelAt(p.transform, (done ? "★ " : "") + a.title, 19, done ? UIKit.Gold : Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20), new Vector2(-20, 26), TextAnchor.MiddleLeft);
                UIKit.LabelAt(p.transform, a.desc + " · +" + M.Rub(a.reward), 14, UIKit.Muted, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(-20, 22), TextAnchor.MiddleLeft, false);
                var bar = UIKit.Panel(p.transform, "Bar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 12), new Vector2(-24, 6), new Color(1, 1, 1, 0.12f));
                var fill = UIKit.Img(UIKit.Fill(bar.transform, "Fill"), done ? UIKit.Gold : UIKit.Green, false);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.sprite = UIKit.Rounded; fill.fillAmount = v / a.goal;
            }
        }

        // ------------------------------------------------------------------ уровни
        void LevelsScreen()
        {
            var s = Screen(true);
            TopBar(s, "Парковка · автодром ДОСААФ", "main");
            for (int i = 0; i < Levels.Count; i++)
            {
                int k = i;
                int st = app.save.d.stars[i];
                bool open = i == 0 || app.save.d.stars[i - 1] > 0 || st > 0;
                int col = i % 10, row = i / 10;
                var b = UIKit.Button(s, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-540 + col * 120, 150 - row * 135), new Vector2(108, 120),
                    open ? (st > 0 ? M.Hex(0x234a2c) : UIKit.Bg2) : new Color(0.1f, 0.1f, 0.12f, 0.9f), () => { if (open) app.StartParking(k); else Toast("Сначала пройдите предыдущий уровень", HUD.Bad); }, 20);
                UIKit.LabelAt(b.transform, open ? (i + 1).ToString() : "×", 34, open ? Color.white : UIKit.Muted, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -42), new Vector2(0, 50));
                UIKit.LabelAt(b.transform, new string('★', st) + new string('☆', 3 - st), 20, UIKit.Gold, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 22), new Vector2(0, 30));
            }
        }

        // ------------------------------------------------------------------ гараж
        void Garage()
        {
            var s = Screen(false);
            TopBar(s, "Гараж · ГСК «Жигули»", "main");
            app.garageOffset = -1.6f;
            var def = Cars.All[garageIndex];
            bool owned = app.save.Owns(def.id);
            var t = app.save.Tune(def.id);
            app.garage.SetCar(def, t, app.save.ColorOf(def.id), app.quality.carLod);

            var info = UIKit.Panel(s, "Info", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(170, 0), new Vector2(300, 400), UIKit.Bg);
            UIKit.LabelAt(info.transform, def.name, 30, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), new Vector2(-24, 40), TextAnchor.MiddleLeft);
            UIKit.LabelAt(info.transform, def.nick + "  " + def.years, 18, UIKit.Accent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-24, 30), TextAnchor.MiddleLeft);
            var sp = def.spec;
            string stats = sp.hp + " л.с. · " + sp.torque + " Н·м\nПривод: " + (sp.drive == Drive.RWD ? "задний" : sp.drive == Drive.FWD ? "передний" : "полный") +
                "\nКПП: " + (sp.gears.Length - 1) + "-ст.\nМасса: " + sp.mass + " кг\nБак: " + sp.tank + " л";
            UIKit.LabelAt(info.transform, stats, 18, new Color(1, 1, 1, 0.85f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(-24, 200), TextAnchor.UpperLeft, false);
            if (owned)
            {
                UIKit.LabelAt(info.transform, "Кузов: " + Mathf.RoundToInt((1f - t.damage) * 100f) + "% · грязь: " + Mathf.RoundToInt(t.dirt * 100f) + "%", 17, t.damage > 0.4f ? M.Hex(0xff7a6a) : UIKit.Gold, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 120), new Vector2(-24, 26), TextAnchor.MiddleLeft, false);
                int rep = CarCare.RepairPrice(t);
                UIKit.Button(info.transform, rep > 0 ? "РЕМОНТ · " + M.Rub(rep) : "ЦЕЛАЯ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 76), new Vector2(260, 44), rep > 0 ? new Color(0.85f, 0.45f, 0.2f) : UIKit.Bg2, () => { if (CarCare.Repair(app, t)) { app.RefreshCar(); Show("garage"); } }, 17);
                UIKit.Button(info.transform, t.dirt > 0.02f ? "МОЙКА · " + M.Rub(CarCare.WashPrice) : "ЧИСТАЯ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(260, 44), t.dirt > 0.02f ? new Color(0.25f, 0.55f, 0.75f) : UIKit.Bg2, () => { if (CarCare.Wash(app, t)) { app.RefreshCar(); Show("garage"); } }, 17);
            }

            UIKit.Button(s, "<", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-330, 60), new Vector2(70, 70), UIKit.Bg, () => { garageIndex = (garageIndex + Cars.All.Length - 1) % Cars.All.Length; Show("garage"); }, 34);
            UIKit.Button(s, ">", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 60), new Vector2(70, 70), UIKit.Bg, () => { garageIndex = (garageIndex + 1) % Cars.All.Length; Show("garage"); }, 34);
            if (!owned)
                UIKit.Button(s, "КУПИТЬ · " + M.Rub(def.price), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 60), new Vector2(310, 70), app.save.Money >= def.price ? UIKit.Accent : UIKit.Bg2,
                    () => { if (app.save.Buy(def.id)) { app.audio.Coin(); Show("garage"); } else Toast("Не хватает денег", HUD.Bad); }, 24);
            else if (app.save.d.current != def.id)
                UIKit.Button(s, "ВЫБРАТЬ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 60), new Vector2(310, 70), UIKit.Green, () => { app.save.Select(def.id); Show("garage"); }, 26);
            else UIKit.Button(s, "В ГАРАЖЕ · ВЫБРАНА", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 60), new Vector2(310, 70), UIKit.Bg2, null, 22);

            // тюнинг (только у купленной машины)
            var panel = UIKit.Panel(s, "Tuning", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-235, -30), new Vector2(440, 560), UIKit.Bg);
            if (!owned) { UIKit.Label(panel.transform, "Купите машину, чтобы открыть тюнинг", 22, UIKit.Muted); return; }
            for (int i = 0; i < Tabs.Length; i++)
            {
                int k = i;
                UIKit.Button(panel.transform, Tabs[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(58 + (i % 4) * 106, -30 - (i / 4) * 50), new Vector2(100, 42), i == tab ? UIKit.Accent : UIKit.Bg2, () => { tab = k; Show("garage"); }, 16);
            }
            var list = UIKit.Rect(panel.transform, "List", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -80), new Vector2(-20, -170));
            if (tab == 0)
            {
                for (int i = 0; i < Cars.Palette.Length; i++)
                {
                    int c = Cars.Palette[i];
                    var b = UIKit.Button(list, "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36 + (i % 6) * 68, -36 - (i / 6) * 68), new Vector2(60, 60), M.Hex(c), () =>
                    {
                        if (app.save.ColorOf(def.id) == c) return;
                        if (!app.save.Spend(Tuning.PaintPrice)) { Toast("Не хватает денег", HUD.Bad); return; }
                        t.color = c; app.save.Commit(); app.audio.Coin(); Show("garage");
                    });
                    if (app.save.ColorOf(def.id) == c) b.GetComponent<Image>().color = Color.Lerp(M.Hex(c), Color.white, 0.25f);
                }
                UIKit.LabelAt(list, "Покраска — " + M.Rub(Tuning.PaintPrice), 18, UIKit.Muted, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 20), new Vector2(0, 30));
                return;
            }
            TuneItem[] items; string field;
            switch (tab)
            {
                case 1: items = Tuning.Wheels; field = "wheels"; break;
                case 2: items = Tuning.Height; field = "height"; break;
                case 3: items = Tuning.Tint; field = "tint"; break;
                case 4: items = Tuning.Engine; field = "engine"; break;
                case 5: items = Tuning.Tires; field = "tires"; break;
                case 6: items = Tuning.Neon; field = "neon"; break;
                case 7: items = Make(GameAudio.HornNames, GameAudio.HornPrices); field = "horn"; break;
                case 8: items = Make(ExhaustNames, ExhaustPrices); field = "exhaust"; break;
                default: items = null; field = "plate"; break;
            }
            if (items == null) { PlateEditor(list, def, t); return; }
            if (field == "horn") UIKit.Button(list, "Послушать выбранный", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(300, 44), UIKit.Bg2, () => app.audio.HornSample(t.horn), 17);
            if (field == "exhaust") UIKit.LabelAt(list, "Прямоток «стреляет» при резком сбросе газа на высоких оборотах", 15, UIKit.Muted, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(-10, 50), TextAnchor.MiddleCenter, false);
            int curIdx = Get(t, field);
            for (int i = 0; i < items.Length; i++)
            {
                int k = i;
                bool has = (Owned(t, field) & (1 << i)) != 0;
                bool isCur = curIdx == i;
                string label = items[i].name + (isCur ? "  ✓" : has ? "  · куплено" : items[i].price > 0 ? "  · " + M.Rub(items[i].price) : "");
                UIKit.Button(list, label, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26 - i * 52), new Vector2(400, 46), isCur ? UIKit.Green : UIKit.Bg2, () =>
                {
                    if (isCur) return;
                    if (!has) { if (!app.save.Spend(items[k].price)) { Toast("Не хватает денег", HUD.Bad); return; } SetOwned(t, field, Owned(t, field) | (1 << k)); app.audio.Coin(); }
                    Set(t, field, k); app.save.Commit();
                    if (field == "horn") app.audio.HornSample(k);
                    if (field == "exhaust" && k > 0) app.audio.Pops(3);
                    app.RefreshCar();
                    Show("garage");
                }, 18);
            }
        }

        static int Get(CarTune t, string f) { switch (f) { case "wheels": return t.wheels; case "height": return t.height; case "tint": return t.tint; case "engine": return t.engine; case "tires": return t.tires; case "horn": return t.horn; case "exhaust": return t.exhaust; default: return t.neon; } }
        static void Set(CarTune t, string f, int v) { switch (f) { case "wheels": t.wheels = v; break; case "height": t.height = v; break; case "tint": t.tint = v; break; case "engine": t.engine = v; break; case "tires": t.tires = v; break; case "horn": t.horn = v; break; case "exhaust": t.exhaust = v; break; default: t.neon = v; break; } }
        static int Owned(CarTune t, string f) { switch (f) { case "wheels": return t.ownWheels; case "height": return t.ownHeight; case "tint": return t.ownTint; case "engine": return t.ownEngine; case "tires": return t.ownTires; case "horn": return t.ownHorn; case "exhaust": return t.ownExhaust; default: return t.ownNeon; } }
        static void SetOwned(CarTune t, string f, int v) { switch (f) { case "wheels": t.ownWheels = v; break; case "height": t.ownHeight = v; break; case "tint": t.ownTint = v; break; case "engine": t.ownEngine = v; break; case "tires": t.ownTires = v; break; case "horn": t.ownHorn = v; break; case "exhaust": t.ownExhaust = v; break; default: t.ownNeon = v; break; } }
        static TuneItem[] Make(string[] names, int[] prices) { var r = new TuneItem[names.Length]; for (int i = 0; i < r.Length; i++) r[i] = new TuneItem(names[i], i, prices[i]); return r; }

        // ------------------------------------------------------------------ номер: редактор по символам
        const string Letters = "АВЕКМНОРСТУХ", Digits = "0123456789";
        static string SlotSet(int k) { return k == 0 || k == 4 || k == 5 ? Letters : k == 6 ? " 123456789" : Digits; }

        static bool PlateValid(string text, string region)
        {
            if (text.Length != 6 || Letters.IndexOf(text[0]) < 0 || Letters.IndexOf(text[4]) < 0 || Letters.IndexOf(text[5]) < 0) return false;
            for (int i = 1; i <= 3; i++) if (!char.IsDigit(text[i])) return false;
            if (text.Substring(1, 3) == "000") return false;
            if (region.Length == 2) return region != "00" && char.IsDigit(region[0]) && char.IsDigit(region[1]);
            return region.Length == 3 && region[0] != '0' && char.IsDigit(region[0]) && char.IsDigit(region[1]) && char.IsDigit(region[2]);
        }

        static void SlotsToPlate(string[] sl, out string text, out string region)
        {
            text = sl[0] + sl[1] + sl[2] + sl[3] + sl[4] + sl[5];
            region = (sl[6] == " " ? "" : sl[6]) + sl[7] + sl[8];
        }

        /// <summary>Цена номера: обычный 2 000 ₽, «красивый» (одинаковые цифры/буквы, 00X, круглый, зеркальный) — дороже.</summary>
        public static int PlatePrice(string text, out string tags)
        {
            string d = text.Substring(1, 3), L = "" + text[0] + text[4] + text[5];
            int extra = 0; tags = "";
            if (d[0] == d[1] && d[1] == d[2]) { extra += 25000; tags += "цифры " + d + " "; }
            else if (d[0] == '0' && d[1] == '0') { extra += 20000; tags += "номер " + d + " "; }
            else if (d[1] == '0' && d[2] == '0') { extra += 8000; tags += "круглый " + d + " "; }
            else if (d[0] == d[2]) { extra += 3000; tags += "зеркальный " + d + " "; }
            if (L[0] == L[1] && L[1] == L[2]) { extra += 15000; tags += "буквы " + L + " "; }
            else if (L[1] == L[2]) { extra += 2000; tags += "буквы " + L[1] + L[2] + " "; }
            return Mathf.Min(50000, 2000 + extra);
        }

        void Step(int k, int d)
        {
            string set = SlotSet(k);
            var sl = (string[])plateSlots.Clone();
            int i = Mathf.Max(0, set.IndexOf(sl[k][0]));
            for (int tries = 0; tries < set.Length; tries++)
            {
                i = (i + d + set.Length) % set.Length;
                sl[k] = set[i].ToString();
                string tx, rg; SlotsToPlate(sl, out tx, out rg);
                if (PlateValid(tx, rg)) { plateSlots = sl; return; }
            }
        }

        void PlateEditor(RectTransform list, CarDef def, CarTune t)
        {
            if (plateSlots == null || plateFor != def.id)
            {
                plateFor = def.id;
                string r = t.plateRegion.Length == 3 ? t.plateRegion : " " + t.plateRegion;
                plateSlots = new[] { t.plateText.Substring(0, 1), t.plateText.Substring(1, 1), t.plateText.Substring(2, 1), t.plateText.Substring(3, 1), t.plateText.Substring(4, 1), t.plateText.Substring(5, 1), r.Substring(0, 1), r.Substring(1, 1), r.Substring(2, 1) };
            }
            // табличка: 6 символов номера + регион (3 позиции)
            var plate = UIKit.Panel(list, "Plate", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -95), new Vector2(408, 64), Color.white);
            for (int k = 0; k < 9; k++)
            {
                int kk = k;
                float x = -180 + k * 40 + (k >= 6 ? 14 : 0);
                UIKit.Button(list, "▲", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -38), new Vector2(36, 40), UIKit.Bg2, () => { Step(kk, 1); Show("garage"); }, 18);
                UIKit.LabelAt(plate.transform, plateSlots[k] == " " ? "" : plateSlots[k], k >= 6 ? 24 : 34, Color.black, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, k >= 6 ? 8 : 0), new Vector2(38, 50));
                UIKit.Button(list, "▼", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -152), new Vector2(36, 40), UIKit.Bg2, () => { Step(kk, -1); Show("garage"); }, 18);
            }
            UIKit.LabelAt(plate.transform, "RUS", 11, M.Hex(0x1d3f7a), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(334 - 180 + 40 * 0, -20), new Vector2(60, 16));
            string text, region; SlotsToPlate(plateSlots, out text, out region);
            string tags; int price = PlatePrice(text, out tags);
            bool cur = text == t.plateText && region == t.plateRegion;
            UIKit.LabelAt(list, cur ? "Этот номер уже стоит" : (tags.Length > 0 ? "«Красивый»: " + tags : "Обычный номер"), 16, cur ? UIKit.Green : UIKit.Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(400, 26));
            UIKit.Button(list, cur ? "УСТАНОВЛЕН" : "ПОЛУЧИТЬ НОМЕР · " + M.Rub(price), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -232), new Vector2(380, 50), cur ? UIKit.Bg2 : UIKit.Accent, () =>
            {
                if (cur) return;
                if (!app.save.Spend(price)) { Toast("Не хватает денег", HUD.Bad); return; }
                t.plateText = text; t.plateRegion = region; app.save.Commit(); app.audio.Coin(); Show("garage");
            }, 19);
            // быстрый выбор: кавказские регионы и «блатные»
            UIKit.LabelAt(list, "Готовые варианты:", 15, UIKit.Muted, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -276), new Vector2(400, 22));
            for (int i = 0; i < PlatePresets.Length; i++)
            {
                int k = i; var pp = PlatePresets[i];
                UIKit.Button(list, pp[0] + " " + pp[1], new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-150 + (i % 4) * 100, -300 - (i / 4) * 36), new Vector2(96, 32), UIKit.Bg2, () =>
                {
                    string r = pp[1].Length == 3 ? pp[1] : " " + pp[1];
                    plateSlots = new[] { pp[0].Substring(0, 1), pp[0].Substring(1, 1), pp[0].Substring(2, 1), pp[0].Substring(3, 1), pp[0].Substring(4, 1), pp[0].Substring(5, 1), r.Substring(0, 1), r.Substring(1, 1), r.Substring(2, 1) };
                    Show("garage");
                }, 13);
            }
        }

        // ------------------------------------------------------------------ настройки
        void SettingsScreen()
        {
            var s = Screen(true);
            TopBar(s, "Настройки", app.paused ? "pause" : "main");
            var st = app.save.d.settings;
            int y = 0;
            System.Action<string, string[], int, System.Action<int>> row = (title, opts, cur, set) =>
            {
                float py = 215 - y * 64;
                UIKit.LabelAt(s, title, 22, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, py), new Vector2(300, 40), TextAnchor.MiddleLeft);
                for (int i = 0; i < opts.Length; i++)
                {
                    int k = i;
                    UIKit.Button(s, opts[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-140 + i * 165, py), new Vector2(155, 52), i == cur ? UIKit.Accent : UIKit.Bg2, () => { set(k); app.save.Commit(); app.ApplySettings(); Show("settings"); }, 18);
                }
                y++;
            };
            row("Графика", Quality.Names, app.quality.level, (k) => { st.quality = k; app.SetQuality(k); });
            row("Управление", new[] { "Руль", "Стрелки", "Наклон" }, st.controls, (k) => st.controls = k);
            row("Коробка", new[] { "Автомат", "Механика" }, st.manual ? 1 : 0, (k) => st.manual = k == 1);
            row("Помощь в заносе", new[] { "Вкл", "Выкл" }, st.assist ? 0 : 1, (k) => st.assist = k == 0);
            row("Громкость", new[] { "0%", "40%", "80%", "100%" }, st.volume < 0.2f ? 0 : st.volume < 0.6f ? 1 : st.volume < 0.9f ? 2 : 3, (k) => st.volume = new[] { 0f, 0.4f, 0.8f, 1f }[k]);
            row("Показывать FPS", new[] { "Да", "Нет" }, st.showFps ? 0 : 1, (k) => st.showFps = k == 0);
            row("Парктроник", new[] { "Вкл", "Выкл" }, st.sensors ? 0 : 1, (k) => st.sensors = k == 0);
            row("Штрафы в городе", new[] { "Спрашивать", "Всегда", "Никогда" }, st.askFines ? 0 : st.fines ? 1 : 2, (k) => { st.askFines = k == 0; if (k > 0) st.fines = k == 1; });
            UIKit.Button(s, "Сбросить прогресс", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(300, 52), new Color(0.45f, 0.1f, 0.08f, 0.9f), () => { app.save.Reset(); Show("settings"); }, 18);
        }

        void Help()
        {
            var s = Screen(true);
            TopBar(s, "Как играть", "main");
            UIKit.LabelAt(s,
                "Управление: руль слева (или стрелки/наклон — в настройках), справа — газ, тормоз, ручник и R · N · D.\n" +
                "Поворотники и аварийка — под картой. КАМ — камера, ФАРЫ — авто/вкл/выкл, БИП — гудок.\n\n" +
                "Парковка: поставьте машину в жёлтую зону по стрелке и остановитесь. Любое касание — провал. Быстрее норматива — три звезды.\n\n" +
                "Свободная езда: дрифт (угол × скорость × плавность, связки и перекладки поднимают множитель) и «шашки» — обгоны впритирку — приносят рубли. " +
                "Бензин — на АЗС (жёлтая точка на карте): встаньте под навес.\n\n" +
                "Дрифт: срыв ручником или перегазовкой (задний привод), держите газом и контррулём. «Помощь в заносе» подруливает сама.\n\n" +
                "Экзамен ГИБДД: площадка (змейка, параллельная, гараж задом), затем город по указаниям инструктора. 5 штрафных баллов — не сдал.\n" +
                "Дрифт-зона ДОСААФ: 90 секунд вокруг конусов, сбитый конус сжигает серию. Пешком: «ВЫЙТИ» — гулять, курить, свистеть, есть шаурму.\n\n" +
                "Клавиатура: WASD — езда, пробел — ручник, H — гудок, C — камера, F — выйти/сесть, R — радио, T — такси, E — заправка, Esc — пауза." +
                "\n\nПерсонаж игрока: «Animated Human» — Quaternius (CC0), quaternius.com" +
                (Credits.Models.Length > 0 ? "\n\nМодели машин (CC BY): " + string.Join("; ", Credits.Models) : ""),
                20, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1000, 520), TextAnchor.UpperLeft, false);
        }

        // ------------------------------------------------------------------ пауза и итог
        void Pause()
        {
            var s = Screen(true);
            var m = UIKit.Panel(s, "Modal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 380), UIKit.Bg);
            UIKit.LabelAt(m.transform, "ПАУЗА", 40, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(400, 60));
            UIKit.Button(m.transform, "Продолжить", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(app.mode != null && app.mode.Restartable ? -125 : 0, -140), new Vector2(230, 70), UIKit.Green, () => app.Resume(), 24);
            if (app.mode != null && app.mode.Restartable) UIKit.Button(m.transform, "Заново", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(125, -140), new Vector2(230, 70), UIKit.Bg2, () => app.Restart(), 24);
            UIKit.Button(m.transform, "Время: " + DayNight.PresetNames[app.timePreset], new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 70), new Vector2(160, 54), UIKit.Bg2, () => { app.NextTime(); Show("pause"); }, 17);
            UIKit.Button(m.transform, "Настройки", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(160, 54), UIKit.Bg2, () => Show("settings"), 18);
            UIKit.Button(m.transform, "В меню", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 70), new Vector2(160, 54), UIKit.Bg2, () => app.ToMenu("main"), 18);
        }

        public void ShowResult(Result r)
        {
            current = "result";
            var s = Screen(true);
            if (r.exam || r.drift) { SpecialResult(s, r); return; }
            var m = UIKit.Panel(s, "Modal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 420), UIKit.Bg);
            UIKit.LabelAt(m.transform, r.ok ? "Припарковано!" : "Провал", 42, r.ok ? Color.white : M.Hex(0xff7a6a), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(500, 60));
            if (r.ok)
            {
                UIKit.LabelAt(m.transform, new string('★', r.stars) + new string('☆', 3 - r.stars), 56, UIKit.Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(400, 70));
                UIKit.LabelAt(m.transform, "+" + M.Rub(r.reward) + (r.first ? " · первый раз ×2" : "") + "\nВремя: " + r.time.ToString("0.0") + " с", 24, UIKit.Green, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(500, 70));
            }
            else UIKit.LabelAt(m.transform, r.why, 26, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(500, 60));
            bool next = r.ok && r.level < Levels.Count - 1;
            if (next) UIKit.Button(m.transform, "Далее", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 60), new Vector2(160, 64), UIKit.Green, () => app.StartParking(r.level + 1), 24);
            UIKit.Button(m.transform, "Заново", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(next ? 0 : -90, 60), new Vector2(160, 64), r.ok ? UIKit.Bg2 : UIKit.Accent, () => app.StartParking(r.level), 24);
            UIKit.Button(m.transform, "Меню", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(next ? 180 : 90, 60), new Vector2(160, 64), UIKit.Bg2, () => app.ToMenu("levels"), 24);
        }

        void SpecialResult(RectTransform s, Result r)
        {
            var m = UIKit.Panel(s, "Modal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 470), UIKit.Bg);
            string title = r.exam ? (r.ok ? "Права получены!" : "Экзамен не сдан") : r.record ? "Новый рекорд!" : "Заезд окончен";
            UIKit.LabelAt(m.transform, title, 40, r.ok ? Color.white : M.Hex(0xff7a6a), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(560, 60));
            string body;
            if (r.exam)
            {
                body = (r.ok ? "+" + M.Rub(r.reward) + "\n" : r.why + "\n") + "Штрафные баллы: " + r.pts + " из 5";
                if (r.log != null && r.log.Count > 0) body += "\n\n" + string.Join("\n", r.log.ToArray());
            }
            else body = "Очки: " + r.score + "\nСерий: " + r.series + "\nРекорд: " + r.best + (r.reward > 0 ? "\n+" + M.Rub(r.reward) : "");
            UIKit.LabelAt(m.transform, body, 21, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(560, 230), TextAnchor.UpperCenter, false);
            UIKit.Button(m.transform, "Заново", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 60), new Vector2(180, 64), UIKit.Accent, () => { if (r.exam) app.StartExam(); else app.StartDrift(); }, 24);
            UIKit.Button(m.transform, "Меню", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(100, 60), new Vector2(180, 64), UIKit.Bg2, () => app.ToMenu("main"), 24);
        }

        /// <summary>Заставка: чёрный экран, название плавно проявляется.</summary>
        public void Intro()
        {
            var s = Screen(false);
            UIKit.Img(s, Color.black, false);
            var t = UIKit.LabelAt(s, "<color=#ff6a1a>CAUCASUS</color> DRIVE", 72, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(900, 100));
            t.supportRichText = true;
            var sub = UIKit.LabelAt(s, "АВТОВАЗ · КАВКАЗ · ДРИФТ", 20, UIKit.Muted, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(900, 40));
            var cg = t.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0;
            var cg2 = sub.gameObject.AddComponent<CanvasGroup>(); cg2.alpha = 0;
            app.StartCoroutine(FadeIn(cg, cg2));
        }

        static System.Collections.IEnumerator FadeIn(CanvasGroup a, CanvasGroup b)
        {
            for (float t = 0; t < 1.6f; t += Time.unscaledDeltaTime)
            {
                if (!a) yield break;
                a.alpha = Mathf.SmoothStep(0, 1, t / 1.2f);
                if (b) b.alpha = Mathf.SmoothStep(0, 1, (t - 0.5f) / 1.1f);
                yield return null;
            }
            if (a) a.alpha = 1; if (b) b.alpha = 1;
        }

        public void Loading(string text)
        {
            var s = Screen(false);
            UIKit.Img(s, Color.black, false);
            var t = UIKit.LabelAt(s, "<color=#ff6a1a>CAUCASUS</color> DRIVE", 72, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(900, 100));
            t.supportRichText = true;
            UIKit.LabelAt(s, text, 22, UIKit.Muted, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(900, 40));
        }
    }
}
