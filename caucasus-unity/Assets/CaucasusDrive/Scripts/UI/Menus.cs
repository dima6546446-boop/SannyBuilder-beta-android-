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
        static readonly string[] Tabs = { "Цвет", "Диски", "Подвеска", "Тонировка", "Мотор", "Шины", "Неон", "Номер" };
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

            string[] titles = { "ПАРКОВКА", "СВОБОДНАЯ ЕЗДА", "ГАРАЖ", "НАСТРОЙКИ" };
            string[] subs = { "30 уровней · ★ " + app.save.StarsTotal, "Город · дрифт · шашки · АЗС", Cars.Get(app.save.d.current).name + " · тюнинг", "Графика · управление · звук" };
            Color[] cols = { UIKit.Accent, M.Hex(0x1f6a8a), UIKit.Bg2, UIKit.Bg2 };
            System.Action[] acts = { () => Show("levels"), () => app.StartFree(), () => Show("garage"), () => Show("settings") };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = UIKit.Button(s, "", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-465 + i * 310, 150), new Vector2(290, 170), cols[i], acts[k]);
                UIKit.LabelAt(b.transform, titles[i], 28, Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 74), new Vector2(-24, 40), TextAnchor.MiddleLeft);
                UIKit.LabelAt(b.transform, subs[i], 16, new Color(1, 1, 1, 0.75f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 38), new Vector2(-24, 30), TextAnchor.MiddleLeft, false);
            }
            var tg = UIKit.Button(s, "Наш Telegram: t.me/caucasusdrive", new Vector2(0, 0), new Vector2(0, 0), new Vector2(230, 40), new Vector2(420, 50), M.Hex(0x229ed9), () => Application.OpenURL("https://t.me/caucasusdrive"), 20);
            UIKit.Button(s, "Как играть", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-120, 40), new Vector2(200, 50), UIKit.Bg, () => Show("help"), 20);
            tg.name = "Telegram";
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

            UIKit.Button(s, "<", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-330, 60), new Vector2(70, 70), UIKit.Bg, () => { garageIndex = (garageIndex + Cars.All.Length - 1) % Cars.All.Length; Show("garage"); }, 34);
            UIKit.Button(s, ">", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 60), new Vector2(70, 70), UIKit.Bg, () => { garageIndex = (garageIndex + 1) % Cars.All.Length; Show("garage"); }, 34);
            if (!owned)
                UIKit.Button(s, "КУПИТЬ · " + M.Rub(def.price), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 60), new Vector2(310, 70), app.save.Money >= def.price ? UIKit.Accent : UIKit.Bg2,
                    () => { if (app.save.Buy(def.id)) { app.audio.Coin(); Show("garage"); } else Toast("Не хватает денег", HUD.Bad); }, 24);
            else if (app.save.d.current != def.id)
                UIKit.Button(s, "ВЫБРАТЬ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 60), new Vector2(310, 70), UIKit.Green, () => { app.save.Select(def.id); Show("garage"); }, 26);
            else UIKit.Button(s, "В ГАРАЖЕ · ВЫБРАНА", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 60), new Vector2(310, 70), UIKit.Bg2, null, 22);

            // тюнинг (только у купленной машины)
            var panel = UIKit.Panel(s, "Tuning", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-235, -20), new Vector2(440, 520), UIKit.Bg);
            if (!owned) { UIKit.Label(panel.transform, "Купите машину, чтобы открыть тюнинг", 22, UIKit.Muted); return; }
            for (int i = 0; i < Tabs.Length; i++)
            {
                int k = i;
                UIKit.Button(panel.transform, Tabs[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(58 + (i % 4) * 106, -30 - (i / 4) * 50), new Vector2(100, 42), i == tab ? UIKit.Accent : UIKit.Bg2, () => { tab = k; Show("garage"); }, 16);
            }
            var list = UIKit.Rect(panel.transform, "List", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -60), new Vector2(-20, -140));
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
                default: items = null; field = "plate"; break;
            }
            if (items == null)
            {
                for (int i = 0; i < PlatePresets.Length; i++)
                {
                    int k = i;
                    var p = PlatePresets[i];
                    bool cur = t.plateText == p[0] && t.plateRegion == p[1];
                    UIKit.Button(list, p[0] + " " + p[1] + (cur ? "  ✓" : PlatePrices[i] > 0 ? "  · " + M.Rub(PlatePrices[i]) : ""), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24 - i * 44), new Vector2(400, 40), cur ? UIKit.Green : UIKit.Bg2, () =>
                    {
                        if (cur) return;
                        if (!app.save.Spend(PlatePrices[k])) { Toast("Не хватает денег", HUD.Bad); return; }
                        t.plateText = PlatePresets[k][0]; t.plateRegion = PlatePresets[k][1]; app.save.Commit(); app.audio.Coin(); Show("garage");
                    }, 18);
                }
                return;
            }
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
                    Set(t, field, k); app.save.Commit(); Show("garage");
                }, 18);
            }
        }

        static int Get(CarTune t, string f) { switch (f) { case "wheels": return t.wheels; case "height": return t.height; case "tint": return t.tint; case "engine": return t.engine; case "tires": return t.tires; default: return t.neon; } }
        static void Set(CarTune t, string f, int v) { switch (f) { case "wheels": t.wheels = v; break; case "height": t.height = v; break; case "tint": t.tint = v; break; case "engine": t.engine = v; break; case "tires": t.tires = v; break; default: t.neon = v; break; } }
        static int Owned(CarTune t, string f) { switch (f) { case "wheels": return t.ownWheels; case "height": return t.ownHeight; case "tint": return t.ownTint; case "engine": return t.ownEngine; case "tires": return t.ownTires; default: return t.ownNeon; } }
        static void SetOwned(CarTune t, string f, int v) { switch (f) { case "wheels": t.ownWheels = v; break; case "height": t.ownHeight = v; break; case "tint": t.ownTint = v; break; case "engine": t.ownEngine = v; break; case "tires": t.ownTires = v; break; default: t.ownNeon = v; break; } }

        // ------------------------------------------------------------------ настройки
        void SettingsScreen()
        {
            var s = Screen(true);
            TopBar(s, "Настройки", app.paused ? "pause" : "main");
            var st = app.save.d.settings;
            int y = 0;
            System.Action<string, string[], int, System.Action<int>> row = (title, opts, cur, set) =>
            {
                float py = 190 - y * 78;
                UIKit.LabelAt(s, title, 22, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, py), new Vector2(300, 40), TextAnchor.MiddleLeft);
                for (int i = 0; i < opts.Length; i++)
                {
                    int k = i;
                    UIKit.Button(s, opts[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-140 + i * 165, py), new Vector2(155, 56), i == cur ? UIKit.Accent : UIKit.Bg2, () => { set(k); app.save.Commit(); app.ApplySettings(); Show("settings"); }, 18);
                }
                y++;
            };
            row("Графика", Quality.Names, app.quality.level, (k) => { st.quality = k; app.SetQuality(k); });
            row("Управление", new[] { "Руль", "Стрелки", "Наклон" }, st.controls, (k) => st.controls = k);
            row("Коробка", new[] { "Автомат", "Механика" }, st.manual ? 1 : 0, (k) => st.manual = k == 1);
            row("Помощь в заносе", new[] { "Вкл", "Выкл" }, st.assist ? 0 : 1, (k) => st.assist = k == 0);
            row("Громкость", new[] { "0%", "40%", "80%", "100%" }, st.volume < 0.2f ? 0 : st.volume < 0.6f ? 1 : st.volume < 0.9f ? 2 : 3, (k) => st.volume = new[] { 0f, 0.4f, 0.8f, 1f }[k]);
            row("Показывать FPS", new[] { "Да", "Нет" }, st.showFps ? 0 : 1, (k) => st.showFps = k == 0);
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
                "Клавиатура: WASD — езда, пробел — ручник, H — гудок, C — камера, Esc — пауза.",
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
