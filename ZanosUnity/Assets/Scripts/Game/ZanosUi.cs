using System.Collections.Generic;
using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>Интерфейс на IMGUI (ни одного префаба/Canvas — собирается из кода). Экраны: меню, выбор заезда, гараж, настройки, подсказки, пауза, результаты, HUD.</summary>
    public sealed partial class ZanosApp
    {
        static readonly Color Acc = new Color(1f, 0.48f, 0f), Acc2 = new Color(1f, 0.7f, 0.28f), Dim = new Color(0.6f, 0.64f, 0.7f), Ok = new Color(0.24f, 0.86f, 0.52f), Bad = new Color(1f, 0.3f, 0.37f);
        GUIStyle stLabel, stSmall, stTitle, stBig, stBtn, stBtnP, stPanel, stTab, stTabOn, stHud, stHudBig, stCenter;
        Texture2D tPanel, tPanel2, tBtn, tBtnHover, tAcc, tTabOn, tWhite;
        bool stylesReady; float W, H, ui;
        readonly Dictionary<int, Vector2> scrolls = new Dictionary<int, Vector2>();
        static readonly string[] Times = { "auto", "day", "dusk", "night" }, TimeNames = { "Авто", "День", "Закат", "Ночь" };
        static readonly string[] Weathers = { "clear", "rain", "snow" }, WeatherNames = { "Ясно", "Дождь", "Снег" };
        static readonly string[] ModeNames = { "Свободная езда", "Заезд на очки", "Испытания" };
        static readonly string[] QualityNames = { "Низкое", "Среднее", "Высокое" };

        static string Money(long n) { return n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")).Replace(' ', ' ') + " ₽"; }
        static string Num(long n) { return n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")).Replace(' ', ' '); }

        Texture2D Solid(Color c) { var t = new Texture2D(1, 1, TextureFormat.RGBA32, false); t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t; }

        void MakeStyles()
        {
            stylesReady = true; tPanel = Solid(new Color(0.055f, 0.067f, 0.094f, 0.86f)); tPanel2 = Solid(new Color(0.1f, 0.12f, 0.16f, 0.95f)); tBtn = Solid(new Color(0.1f, 0.12f, 0.16f, 0.95f));
            tBtnHover = Solid(new Color(0.17f, 0.19f, 0.25f, 1f)); tAcc = Solid(Acc); tTabOn = Solid(Acc); tWhite = Solid(Color.white);
            stLabel = new GUIStyle { fontSize = 16, normal = { textColor = Color.white }, wordWrap = true, richText = true };
            stSmall = new GUIStyle(stLabel) { fontSize = 12, normal = { textColor = Dim } };
            stTitle = new GUIStyle(stLabel) { fontSize = 30, fontStyle = FontStyle.BoldAndItalic };
            stBig = new GUIStyle(stLabel) { fontSize = 64, fontStyle = FontStyle.BoldAndItalic, normal = { textColor = Acc } };
            stCenter = new GUIStyle(stLabel) { alignment = TextAnchor.MiddleCenter };
            stBtn = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { background = tBtn, textColor = Color.white }, hover = { background = tBtnHover, textColor = Color.white }, active = { background = tAcc, textColor = Color.black }, border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(8, 8, 4, 4) };
            stBtnP = new GUIStyle(stBtn) { normal = { background = tAcc, textColor = new Color(0.08f, 0.04f, 0f) }, hover = { background = Solid(Acc2), textColor = Color.black } };
            stPanel = new GUIStyle { normal = { background = tPanel }, padding = new RectOffset(14, 14, 12, 12) };
            stTab = new GUIStyle(stBtn) { fontSize = 13 }; stTabOn = new GUIStyle(stBtnP) { fontSize = 13 };
            stHud = new GUIStyle(stLabel) { fontSize = 22, fontStyle = FontStyle.BoldAndItalic, wordWrap = false, normal = { textColor = Color.white } };
            stHudBig = new GUIStyle(stHud) { fontSize = 54, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (!stylesReady) MakeStyles();
            ui = Screen.height / 720f; W = Screen.width / ui; H = 720f;
            var m = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1));
            if (state == AppState.Play || state == AppState.Pause || state == AppState.Results) DrawHud();
            switch (screen)
            {
                case Scr.Menu: DrawMenu(); break; case Scr.Play: DrawPlay(); break; case Scr.Garage: DrawGarage(); break;
                case Scr.Settings: DrawSettings(); break; case Scr.Hints: DrawHints(); break; case Scr.Pause: DrawPause(); break; case Scr.Results: DrawResults(); break;
            }
            if (toastT > 0) { var r = new Rect(W / 2 - 220, 70, 440, 40); GUI.Box(r, "", stPanel); GUI.Label(r, toast, stCenter); }
            GUI.matrix = m;
        }

        bool Btn(Rect r, string text, bool primary = false, bool enabled = true)
        {
            bool old = GUI.enabled; GUI.enabled = enabled; bool c = GUI.Button(r, text, primary ? stBtnP : stBtn); GUI.enabled = old;
            if (c && enabled) audioSynth.Click(); return c && enabled;
        }
        bool Tab(Rect r, string text, bool on) { bool c = GUI.Button(r, text, on ? stTabOn : stTab); if (c) audioSynth.Click(); return c; }
        void Lbl(Rect r, string t, GUIStyle s = null) { GUI.Label(r, t, s ?? stLabel); }
        void Panel(Rect r) { GUI.Box(r, "", stPanel); }
        float Slider(Rect r, float v, float a, float b, string text)
        {
            Lbl(new Rect(r.x, r.y, r.width * 0.42f, r.height), text);
            float nv = GUI.HorizontalSlider(new Rect(r.x + r.width * 0.44f, r.y + 8, r.width * 0.42f, 20), v, a, b); return nv;
        }

        void TopBar(string title)
        {
            if (Btn(new Rect(24, 18, 110, 36), "← Назад")) { audioSynth.Click(); Back(); }
            Lbl(new Rect(150, 14, 500, 44), title, stTitle);
            int xp, need; progress.XpInLevel(out xp, out need);
            Lbl(new Rect(W - 380, 16, 200, 24), "Уровень " + progress.Data.level, stSmall);
            GUI.DrawTexture(new Rect(W - 380, 42, 170, 6), tPanel2); GUI.color = Acc; GUI.DrawTexture(new Rect(W - 380, 42, need > 0 ? 170 * Mathf.Clamp01(xp / (float)need) : 170, 6), tWhite); GUI.color = Color.white;
            GUI.Box(new Rect(W - 190, 14, 166, 38), "", stPanel); Lbl(new Rect(W - 180, 20, 150, 28), Money(progress.Data.money), stSmall);
        }

        // ---------- главное меню ----------
        void DrawMenu()
        {
            int xp, need; progress.XpInLevel(out xp, out need);
            Lbl(new Rect(W - 380, 16, 200, 24), "Уровень " + progress.Data.level, stSmall); GUI.Box(new Rect(W - 190, 14, 166, 38), "", stPanel); Lbl(new Rect(W - 180, 20, 150, 28), Money(progress.Data.money), stSmall);
            var big = new GUIStyle(stBig) { fontSize = 110, normal = { textColor = Color.white } }; Lbl(new Rect(40, 70, 700, 130), "ЗА<color=#ff7a00>НОС</color>", big);
            Lbl(new Rect(46, 200, 500, 24), "АРКАДНЫЙ ДРИФТ · ЗАДНИЙ ПРИВОД", stSmall);
            if (Btn(new Rect(40, 250, 380, 56), "▶  ИГРАТЬ", true)) { selCar = progress.Car(selCar) != null ? selCar : progress.Data.selected; Go(Scr.Play); }
            if (Btn(new Rect(40, 318, 380, 48), "ГАРАЖ")) { garCar = progress.Data.selected; garTab = 0; Go(Scr.Garage); }
            if (Btn(new Rect(40, 378, 380, 48), "НАСТРОЙКИ")) Go(Scr.Settings);
            if (Btn(new Rect(40, 438, 380, 48), "УПРАВЛЕНИЕ И ПОДСКАЗКИ")) Go(Scr.Hints);
            var def = CarCatalog.ById(progress.Data.selected); Lbl(new Rect(W - 520, H - 90, 480, 44), def.Brand + " " + def.Name, new GUIStyle(stTitle) { alignment = TextAnchor.MiddleRight });
        }

        // ---------- выбор заезда ----------
        void DrawPlay()
        {
            TopBar("Выбор заезда");
            for (int i = 0; i < 3; i++) if (Tab(new Rect(24 + i * 190, 66, 184, 34), ModeNames[i], (int)selMode == i)) { selMode = (GameMode)i; selChallenge = null; }
            // карты
            Panel(new Rect(24, 108, W * 0.54f, 380)); Lbl(new Rect(40, 116, 300, 22), "ЛОКАЦИЯ", stSmall);
            for (int i = 0; i < MapLoader.Ids.Length; i++)
            {
                var md = MapLoader.Load(MapLoader.Ids[i]); bool locked = !progress.MapUnlocked(md); var r = new Rect(40 + (i % 2) * ((W * 0.54f - 40) / 2), 144 + (i / 2) * 170, (W * 0.54f - 56) / 2, 158);
                GUI.Box(r, "", selMap == md.id ? stPanel : stPanel); if (selMap == md.id) { GUI.color = Acc; GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), tWhite); GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), tWhite); GUI.color = Color.white; }
                Lbl(new Rect(r.x + 10, r.y + 8, r.width - 20, 26), md.name, new GUIStyle(stLabel) { fontStyle = FontStyle.Bold, fontSize = 18 });
                Lbl(new Rect(r.x + 10, r.y + 38, r.width - 20, 80), locked ? "🔒 Нужен уровень " + md.level : md.desc, stSmall);
                if (GUI.Button(r, "", GUIStyle.none)) { if (locked) { audioSynth.Error(); Toast("Карта откроется на уровне " + md.level); } else { audioSynth.Click(); selMap = md.id; selChallenge = null; } }
            }
            // правая колонка
            float rx = 24 + W * 0.54f + 16, rw = W - rx - 24; Panel(new Rect(rx, 108, rw, 380)); var map = MapLoader.Load(selMap);
            Lbl(new Rect(rx + 14, 116, rw - 28, 22), ModeNames[(int)selMode].ToUpperInvariant(), stSmall);
            if (selMode == GameMode.Challenge)
            {
                var sv = BeginScroll(1, new Rect(rx + 8, 142, rw - 16, 338), map.challenges.Length * 70);
                for (int i = 0; i < map.challenges.Length; i++)
                {
                    var c = map.challenges[i]; var r = new Rect(0, i * 70, rw - 32, 64); GUI.Box(r, "", stPanel);
                    if (selChallenge == c.id) { GUI.color = Acc; GUI.DrawTexture(new Rect(0, r.y, 3, r.height), tWhite); GUI.color = Color.white; }
                    Lbl(new Rect(10, r.y + 4, r.width - 140, 24), c.name, new GUIStyle(stLabel) { fontStyle = FontStyle.Bold }); Lbl(new Rect(10, r.y + 28, r.width - 140, 34), c.desc, stSmall);
                    bool done = progress.ChallengeDone(c.id); GUI.color = done ? Ok : Dim; Lbl(new Rect(r.width - 130, r.y + 8, 120, 40), done ? "✔ ПРОЙДЕНО" : "+" + Num(c.money) + " ₽\n" + c.time + " с", stSmall); GUI.color = Color.white;
                    if (GUI.Button(r, "", GUIStyle.none)) { audioSynth.Click(); selChallenge = c.id; }
                }
                GUI.EndScrollView();
            }
            else if (selMode == GameMode.Timed) Lbl(new Rect(rx + 14, 146, rw - 28, 200), "Время: <b>" + map.timedTime + " с</b>\nЦель: <b>" + Num(map.timedGoal) + "</b> очков (★★ при цели, ★★★ при ×1.5)\nРекорд: <b>" + Num(progress.Record(selMap, "timed")) + "</b>\n\nСерия сгорает при ударе. Деньги = очки / 3.5 + бонус за звёзды.", stLabel);
            else Lbl(new Rect(rx + 14, 146, rw - 28, 200), "Без ограничений по времени. Выход через паузу — очки засчитываются и превращаются в деньги и опыт.\n\nРекорд: <b>" + Num(progress.Record(selMap, "free")) + "</b>", stLabel);
            // нижняя панель
            Panel(new Rect(24, 500, W - 48, 200)); Lbl(new Rect(40, 508, 100, 22), "МАШИНА", stSmall);
            float x = 40; foreach (var cs in progress.Data.cars) { var d = CarCatalog.ById(cs.id); string t = d.Brand + " " + d.Name; float w = 24 + t.Length * 9.5f; if (Tab(new Rect(x, 532, w, 34), t, selCar == cs.id)) { selCar = cs.id; RefreshShowCar(selCar); } x += w + 6; }
            Lbl(new Rect(40, 586, 80, 22), "ВРЕМЯ", stSmall); for (int i = 0; i < 4; i++) if (Tab(new Rect(110 + i * 92, 580, 88, 32), TimeNames[i], selTime == Times[i])) selTime = Times[i];
            Lbl(new Rect(500, 586, 80, 22), "ПОГОДА", stSmall); for (int i = 0; i < 3; i++) if (Tab(new Rect(580 + i * 92, 580, 88, 32), WeatherNames[i], selWeather == Weathers[i])) selWeather = Weathers[i];
            if (Btn(new Rect(W - 200, 640, 160, 44), "Гараж")) { garCar = selCar; garTab = 0; Go(Scr.Garage); }
            bool can = progress.MapUnlocked(map) && (selMode != GameMode.Challenge || selChallenge != null);
            if (Btn(new Rect(40, 636, 280, 52), "ПОЕХАЛИ  ▶", true, can)) StartGame();
        }

        Vector2 BeginScroll(int id, Rect view, float contentH)
        {
            Vector2 v; scrolls.TryGetValue(id, out v); v = GUI.BeginScrollView(view, v, new Rect(0, 0, view.width - 18, contentH)); scrolls[id] = v; return v;
        }

        // ---------- гараж ----------
        void StatBars(CarSpec def, CarState st, float x, float y, float w)
        {
            var b = Rating(CarSpec.Build(def, null)); var c = Rating(CarSpec.Build(def, st != null ? st.TuningMap() : null)); string[] n = { "Мощность", "Скорость", "Сцепление", "Тормоза", "Дрифт" };
            for (int i = 0; i < 5; i++)
            {
                Lbl(new Rect(x, y + i * 24, 90, 20), n[i], stSmall); GUI.DrawTexture(new Rect(x + 96, y + i * 24 + 6, w - 140, 8), tPanel2);
                GUI.color = Acc; GUI.DrawTexture(new Rect(x + 96, y + i * 24 + 6, (w - 140) * Mathf.Min(b[i], c[i]), 8), tWhite);
                if (c[i] > b[i]) { GUI.color = Ok; GUI.DrawTexture(new Rect(x + 96 + (w - 140) * b[i], y + i * 24 + 6, (w - 140) * (c[i] - b[i]), 8), tWhite); }
                GUI.color = Color.white; Lbl(new Rect(x + w - 36, y + i * 24, 36, 20), Mathf.Round(c[i] * 100).ToString(), stSmall);
            }
        }
        static float[] Rating(CarSpec s)
        {
            double power = System.Math.Min(1, (s.PeakTorque * (1 + s.Turbo * 0.7)) / s.Mass / 0.40), speed = System.Math.Min(1, (s.PeakTorque * (1 + s.Turbo * 0.7) * s.Redline / 9549) / s.Mass / 0.20);
            double grip = System.Math.Min(1, (s.TireGrip - 0.9) / 0.5), brake = System.Math.Min(1, s.BrakeTorque / s.Mass / 3.4), drift = System.Math.Min(1, 0.3 + s.DiffLock * 0.35 + power * 0.25 + (1 - System.Math.Abs(s.TireGrip - 1.1)) * 0.1);
            return new[] { (float)power, (float)speed, (float)Mathf.Clamp01((float)grip), (float)Mathf.Clamp01((float)brake), (float)drift };
        }

        void DrawGarage()
        {
            TopBar("Гараж");
            string[] tabs = { "Машины", "Тюнинг", "Внешний вид" }; bool owned = progress.Owns(garCar);
            for (int i = 0; i < 3; i++) if (Tab(new Rect(24 + i * 150, 66, 144, 34), tabs[i], garTab == i)) garTab = i;
            Panel(new Rect(24, 108, 320, 590)); var sv = BeginScroll(2, new Rect(30, 116, 308, 574), CarCatalog.All.Length * 70);
            for (int i = 0; i < CarCatalog.All.Length; i++)
            {
                var c = CarCatalog.All[i]; bool o = progress.Owns(c.Id), un = progress.CarUnlocked(c.Id); var r = new Rect(0, i * 70, 288, 64); GUI.Box(r, "", stPanel);
                if (garCar == c.Id) { GUI.color = Acc; GUI.DrawTexture(new Rect(0, r.y, 3, 64), tWhite); }
                GUI.color = Mats.Hex(o ? progress.Car(c.Id).paint : c.Color); GUI.DrawTexture(new Rect(10, r.y + 14, 12, 36), tWhite); GUI.color = Color.white;
                Lbl(new Rect(32, r.y + 6, 250, 24), c.Brand + " " + c.Name, new GUIStyle(stLabel) { fontStyle = FontStyle.Bold });
                Lbl(new Rect(32, r.y + 32, 250, 20), o ? (progress.Data.selected == c.Id ? "✔ Выбрана" : "В гараже") : un ? Money(c.Price) : "🔒 Уровень " + c.Level, stSmall);
                if (GUI.Button(r, "", GUIStyle.none)) { audioSynth.Click(); garCar = c.Id; if (!progress.Owns(c.Id)) garTab = 0; RefreshShowCar(garCar); }
            }
            GUI.EndScrollView();
            float rx = W - 400; Panel(new Rect(rx, 108, 376, 590)); var def = CarCatalog.ById(garCar); var st = progress.Car(garCar);
            if (garTab == 0 || !owned)
            {
                Lbl(new Rect(rx + 14, 118, 350, 34), def.Brand + " " + def.Name, new GUIStyle(stTitle) { fontSize = 24 }); Lbl(new Rect(rx + 14, 154, 350, 60), def.Desc, stSmall);
                var sp = CarSpec.Build(def, st != null ? st.TuningMap() : null); int hp = (int)(sp.PeakTorque * (1 + sp.Turbo * 0.5) * sp.PeakRpm / 9549 * 1.36);
                Lbl(new Rect(rx + 14, 218, 350, 24), hp + " л.с. · " + (int)sp.Mass + " кг · RWD · " + def.Gears.Length + " ст." + (def.Turbo > 0 ? " · турбо" : ""), stSmall);
                StatBars(def, st, rx + 14, 252, 348);
                if (owned)
                {
                    if (Btn(new Rect(rx + 14, 390, 170, 44), progress.Data.selected == garCar ? "Выбрана" : "Выбрать", true, progress.Data.selected != garCar)) { progress.Select(garCar); selCar = garCar; }
                    if (progress.Data.cars.Count > 1) { if (Btn(new Rect(rx + 192, 390, 170, 44), "Продать " + Money(progress.SellValue(garCar)))) { int v; string err = progress.Sell(garCar, out v); if (err == null) { Toast("Продано за " + Money(v)); garCar = progress.Data.selected; selCar = progress.Data.cars.Exists(c2 => c2.id == selCar) ? selCar : garCar; RefreshShowCar(garCar); } else { audioSynth.Error(); Toast(err); } } }
                    else Lbl(new Rect(rx + 192, 396, 170, 40), "Последнюю машину продать нельзя", stSmall);
                }
                else
                {
                    if (Btn(new Rect(rx + 14, 390, 348, 48), "Купить за " + Money(def.Price), true, progress.CanBuy(garCar))) { string err = progress.Buy(garCar); if (err == null) { audioSynth.Buy(); selCar = garCar; Toast("Машина куплена!"); RefreshShowCar(garCar); } else { audioSynth.Error(); Toast(err); } }
                    if (!progress.CarUnlocked(garCar)) Lbl(new Rect(rx + 14, 446, 348, 24), "Откроется на уровне " + def.Level, new GUIStyle(stSmall) { normal = { textColor = Bad } });
                    else if (progress.Data.money < def.Price) Lbl(new Rect(rx + 14, 446, 348, 24), "Не хватает " + Money(def.Price - progress.Data.money), new GUIStyle(stSmall) { normal = { textColor = Bad } });
                }
            }
            else if (garTab == 1)
            {
                var sv2 = BeginScroll(3, new Rect(rx + 8, 114, 360, 340), CarCatalog.Tuning.Length * 84);
                for (int i = 0; i < CarCatalog.Tuning.Length; i++)
                {
                    var p = CarCatalog.Tuning[i]; int lvl = st.Tune(p.Id); int nl, pr; bool can = progress.NextUpgrade(garCar, p.Id, out nl, out pr); var r = new Rect(0, i * 84, 340, 78); GUI.Box(r, "", stPanel);
                    Lbl(new Rect(10, r.y + 4, 180, 24), p.Name, new GUIStyle(stLabel) { fontStyle = FontStyle.Bold });
                    for (int k = 0; k < p.Max; k++) { GUI.color = k < lvl ? Acc : new Color(0.18f, 0.2f, 0.25f); GUI.DrawTexture(new Rect(206 + k * 24, r.y + 12, 20, 8), tWhite); } GUI.color = Color.white;
                    Lbl(new Rect(10, r.y + 26, 320, 20), p.Desc, stSmall);
                    if (Btn(new Rect(10, r.y + 46, 190, 26), can ? "Улучшить · " + Money(pr) : "Максимум", true, can && progress.Data.money >= pr)) { string err = progress.Upgrade(garCar, p.Id); if (err == null) { audioSynth.Buy(); RefreshShowCar(garCar); } }
                    if (lvl > 0 && Btn(new Rect(206, r.y + 46, 124, 26), "Снять +" + Money((long)(CarSpec.UpgradePrice(p.Id, lvl) * 0.5)))) { progress.Downgrade(garCar, p.Id); RefreshShowCar(garCar); }
                }
                GUI.EndScrollView(); StatBars(def, st, rx + 14, 470, 348);
            }
            else DrawLook(rx, st);
        }

        void DrawLook(float rx, CarState st)
        {
            var sv = BeginScroll(4, new Rect(rx + 8, 114, 360, 574), 760); float y = 0;
            Lbl(new Rect(4, y, 300, 22), "ЦВЕТ КУЗОВА", stSmall); y += 24;
            for (int i = 0; i < CarCatalog.Paints.Length; i++)
            {
                var r = new Rect(4 + (i % 8) * 43, y + (i / 8) * 43, 38, 38); GUI.color = Mats.Hex(CarCatalog.Paints[i]); GUI.DrawTexture(r, tWhite); GUI.color = Color.white;
                if (st.paint.ToLowerInvariant() == CarCatalog.Paints[i].ToLowerInvariant()) { GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3), tWhite); }
                if (GUI.Button(r, "", GUIStyle.none)) { audioSynth.Click(); progress.SetPaint(garCar, CarCatalog.Paints[i]); RefreshShowCar(garCar); }
            }
            y += 92;
            y = PartGrid("ДИСКИ", "wheels", CarCatalog.Wheels, st.wheels, y); y = PartGrid("ОБВЕС", "bodykit", CarCatalog.BodyKits, st.bodykit, y);
            y = PartGrid("СПОЙЛЕР", "spoiler", CarCatalog.Spoilers, st.spoiler, y); y = PartGrid("НЕОН", "neon", CarCatalog.Neons, st.neon, y);
            GUI.EndScrollView();
        }

        float PartGrid(string title, string slot, CarCatalog.Part[] list, string cur, float y)
        {
            Lbl(new Rect(4, y, 300, 22), title, stSmall); y += 24;
            for (int i = 0; i < list.Length; i++)
            {
                var r = new Rect(4 + (i % 2) * 172, y + (i / 2) * 50, 166, 44); bool own = progress.PartOwned(garCar, slot, list[i].Id);
                GUI.Box(r, "", stPanel); if (cur == list[i].Id) { GUI.color = Acc; GUI.DrawTexture(new Rect(r.x, r.y, 3, r.height), tWhite); GUI.color = Color.white; }
                Lbl(new Rect(r.x + 8, r.y + 2, 150, 20), list[i].Name, new GUIStyle(stSmall) { normal = { textColor = Color.white }, fontStyle = FontStyle.Bold });
                GUI.color = own ? Ok : Acc2; Lbl(new Rect(r.x + 8, r.y + 22, 150, 18), own ? (cur == list[i].Id ? "Установлено" : "Есть") : Money(list[i].Price), stSmall); GUI.color = Color.white;
                if (GUI.Button(r, "", GUIStyle.none)) { string err = progress.SetPart(garCar, slot, list[i].Id); if (err == null) { audioSynth.Click(); RefreshShowCar(garCar); } else { audioSynth.Error(); Toast(err); } }
            }
            return y + ((list.Length + 1) / 2) * 50 + 14;
        }

        // ---------- настройки ----------
        void DrawSettings()
        {
            TopBar("Настройки"); string[] tabs = { "Графика", "Звук", "Управление" };
            for (int i = 0; i < 3; i++) if (Tab(new Rect(24 + i * 150, 66, 144, 34), tabs[i], setTab == i)) setTab = i;
            Panel(new Rect(24, 108, Mathf.Min(W - 48, 860), 590)); float x = 44, w = Mathf.Min(W - 88, 820), y = 124; var s = S; bool ch = false;
            if (setTab == 0)
            {
                Lbl(new Rect(x, y, 200, 28), "Качество"); for (int i = 0; i < 3; i++) if (Tab(new Rect(x + 260 + i * 110, y - 2, 104, 32), QualityNames[i], s.quality == i)) { s.quality = i; ch = true; } y += 46;
                ch |= Toggle(ref s.shadows, "Тени", x, y); y += 40; ch |= Toggle(ref s.effects, "Дым, следы, искры", x, y); y += 40; ch |= Toggle(ref s.speedFx, "Эффект скорости (FOV, тряска)", x, y); y += 40; ch |= Toggle(ref s.showFps, "Показывать FPS", x, y); y += 46;
                float f = Slider(new Rect(x, y, w, 30), s.fov, 55, 95, "Угол обзора  " + Mathf.Round(s.fov) + "°"); if (!Mathf.Approximately(f, s.fov)) { s.fov = Mathf.Round(f); ch = true; }
            }
            else if (setTab == 1)
            {
                ch |= Sl(ref s.master, "Общая громкость", x, y, w); y += 44; ch |= Sl(ref s.engine, "Двигатель", x, y, w); y += 44; ch |= Sl(ref s.sfx, "Эффекты и интерфейс", x, y, w); y += 44; ch |= Sl(ref s.music, "Музыка", x, y, w);
            }
            else
            {
                ch |= Sl(ref s.assist, "Помощник заноса " + (s.assist <= 0.01f ? "(выкл.)" : Mathf.Round(s.assist * 100) + "%"), x, y, w); y += 44;
                float ss = Slider(new Rect(x, y, w, 30), s.steerSens, 0.5f, 1.6f, "Чувствительность руля " + s.steerSens.ToString("0.00")); if (!Mathf.Approximately(ss, s.steerSens)) { s.steerSens = ss; ch = true; } y += 44;
                float dz = Slider(new Rect(x, y, w, 30), s.deadzone, 0f, 0.4f, "Мёртвая зона стика " + Mathf.Round(s.deadzone * 100) + "%"); if (!Mathf.Approximately(dz, s.deadzone)) { s.deadzone = dz; ch = true; } y += 50;
                Lbl(new Rect(x, y, 240, 28), "Коробка передач"); if (Tab(new Rect(x + 260, y - 2, 130, 32), "Автомат", !s.manual)) { s.manual = false; ch = true; } if (Tab(new Rect(x + 396, y - 2, 170, 32), "Механика (Q/E)", s.manual)) { s.manual = true; ch = true; } y += 46;
                Lbl(new Rect(x, y, 240, 28), "Камера по умолчанию"); for (int i = 0; i < CamRig.Names.Length; i++) if (Tab(new Rect(x + 260 + i * 92, y - 2, 88, 32), CamRig.Names[i], s.camera == i)) { s.camera = i; ch = true; } y += 46;
                Lbl(new Rect(x, y, 240, 28), "Единицы скорости"); if (Tab(new Rect(x + 260, y - 2, 90, 32), "км/ч", !s.mph)) { s.mph = false; ch = true; } if (Tab(new Rect(x + 356, y - 2, 90, 32), "миль/ч", s.mph)) { s.mph = true; ch = true; } y += 56;
                if (Btn(new Rect(x, y, 260, 38), "Сбросить сохранение")) { SaveStore.Delete(); progress = new Progress(new SaveData()); progress.OnChanged = Persist; selCar = garCar = progress.Data.selected; Persist(); ApplySettings(); Toast("Прогресс сброшен"); Go(Scr.Menu, false); }
            }
            if (ch) { Persist(); ApplySettings(); }
        }
        bool Toggle(ref bool v, string text, float x, float y) { Lbl(new Rect(x, y, 400, 28), text); bool n = GUI.Toggle(new Rect(x + 520, y - 2, 40, 30), v, ""); if (n != v) { v = n; audioSynth.Click(); return true; } return false; }
        bool Sl(ref float v, string text, float x, float y, float w) { float n = Slider(new Rect(x, y, w, 30), v, 0, 1, text); if (!Mathf.Approximately(n, v)) { v = n; return true; } return false; }

        // ---------- подсказки ----------
        void DrawHints()
        {
            var r = new Rect(W / 2 - 480, 40, 960, 640); GUI.Box(new Rect(0, 0, W, H), "", stPanel); Panel(r);
            Lbl(new Rect(r.x + 24, r.y + 14, 600, 40), "Управление и подсказки", stTitle);
            if (Btn(new Rect(r.xMax - 164, r.y + 16, 140, 38), "Понятно", true)) { audioSynth.Click(); if (state == AppState.Pause) screen = Scr.Pause; else Back(); }
            Lbl(new Rect(r.x + 24, r.y + 70, 290, 300), "<b>Клавиатура</b>\nW / ↑ — газ\nS / ↓ — тормоз, задний ход\nA D / ← → — руль\nПробел — ручник (срыв зада)\nShift — рывок момента\nQ / E — передачи (механика)\nC — камера   R — на старт\nEsc / P — пауза   H — подсказки");
            Lbl(new Rect(r.x + 330, r.y + 70, 290, 300), "<b>Геймпад</b>\nRT / LT — газ / тормоз\nЛевый стик — руль\nA — ручник   B — рывок\nLB / RB — передачи\nY — камера   X — на старт\nStart — пауза");
            Lbl(new Rect(r.x + 640, r.y + 70, 300, 300), "<b>Очки</b>\nОчки = угол × скорость × время. Серия растёт во времени (+0.5 каждые 2.4 с), смена стороны заноса даёт бонус. Серия засчитывается при выходе из заноса. <color=#ff4d5e>Удар или остановка сжигают серию.</color>");
            Lbl(new Rect(r.x + 24, r.y + 380, 910, 240), "<b>Как дрифтовать</b>\n• Разгонись до 60–80 км/ч, плавно поверни и дави газ — зад начнёт уходить. Или резко дёрни руль в сторону и обратно.\n• Ручник — короткое нажатие в повороте мгновенно срывает зад. Сброс газа перед входом переносит вес на нос.\n• В заносе рули в сторону скольжения (помощник делает это мягко). Угол держи газом: больше газа — больше угла.\n• Мокрый асфальт, гравий и снег срываются легче. Конусы можно сбивать — это не удар.");
        }

        // ---------- пауза / результаты ----------
        void DrawPause()
        {
            GUI.Box(new Rect(0, 0, W, H), "", stPanel); var r = new Rect(W / 2 - 200, 110, 400, 500); Panel(r); Lbl(new Rect(r.x + 24, r.y + 14, 340, 44), "Пауза", stTitle);
            float y = r.y + 70;
            if (Btn(new Rect(r.x + 24, y, 352, 44), "Продолжить", true)) PauseToggle(); y += 54;
            if (Btn(new Rect(r.x + 24, y, 352, 44), "Заново")) RestartGame(); y += 54;
            if (Btn(new Rect(r.x + 24, y, 352, 44), "Камера: " + CamRig.Names[rig.Mode])) rig.Next(); y += 54;
            if (Btn(new Rect(r.x + 24, y, 352, 44), "Управление")) Go(Scr.Hints); y += 54;
            if (Btn(new Rect(r.x + 24, y, 352, 44), "Настройки")) Go(Scr.Settings); y += 54;
            if (Btn(new Rect(r.x + 24, y, 352, 44), "Завершить и забрать очки")) EndRun(); y += 54;
            if (Btn(new Rect(r.x + 24, y, 352, 44), "Выйти без награды")) { LeaveGame(); Go(Scr.Menu, false); }
        }

        void DrawResults()
        {
            var s = resultSum; var rw = resultRew; GUI.Box(new Rect(0, 0, W, H), "", stPanel); var r = new Rect(W / 2 - 330, 50, 660, 620); Panel(r);
            string title = s.Mode == GameMode.Challenge ? (s.Success ? "Испытание пройдено!" : "Испытание не пройдено") : s.Mode == GameMode.Timed ? "Заезд завершён" : "Итоги заезда";
            Lbl(new Rect(r.x + 24, r.y + 14, 600, 40), title, new GUIStyle(stTitle) { normal = { textColor = s.Success ? Color.white : Bad } });
            if (s.ChallengeName != null) Lbl(new Rect(r.x + 24, r.y + 54, 600, 22), s.ChallengeName, stSmall);
            Lbl(new Rect(r.x + 24, r.y + 78, 200, 18), "ОЧКИ", stSmall); Lbl(new Rect(r.x + 24, r.y + 92, 600, 80), Num(s.Score), stBig);
            if (s.Mode == GameMode.Timed) { string stars = ""; for (int i = 1; i <= 3; i++) stars += i <= s.Stars ? "<color=#ffd21f>★</color>" : "<color=#444444>★</color>"; Lbl(new Rect(r.x + 400, r.y + 100, 220, 50), stars, new GUIStyle(stBig) { fontSize = 40 }); }
            if (rw.NewRecord) Lbl(new Rect(r.x + 24, r.y + 170, 300, 24), "★ Новый рекорд!", new GUIStyle(stLabel) { normal = { textColor = Ok }, fontStyle = FontStyle.Bold });
            string[,] rows = { { "Лучшая серия", Num((long)s.BestChain) }, { "Макс. угол", Mathf.Round((float)s.BestAngle) + "°" }, { "Время в дрифте", s.DriftTime.ToString("0.0") + " с" }, { "Макс. скорость", Mathf.Round((float)s.TopSpeed) + " км/ч" }, { "Ударов", s.Hits.ToString() }, { "Сгорело очков", Num((long)s.LostPoints) }, { "Дистанция", (s.Distance / 1000).ToString("0.00") + " км" }, { "Средний угол", Mathf.Round((float)s.AvgAngle) + "°" } };
            for (int i = 0; i < 8; i++) { float cx = r.x + 24 + (i % 2) * 320, cy = r.y + 204 + (i / 2) * 30; Lbl(new Rect(cx, cy, 170, 24), rows[i, 0], stSmall); Lbl(new Rect(cx + 170, cy, 130, 24), rows[i, 1], new GUIStyle(stLabel) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold }); }
            Lbl(new Rect(r.x + 24, r.y + 340, 620, 30), "<color=#ffb347>₽ +" + Num(rw.Money) + "</color>      <color=#ffb347>XP +" + Num(rw.Xp) + "</color>" + (rw.ChallengeMoney > 0 ? "      " + (rw.FirstChallenge ? "Награда за испытание" : "Повторная награда (25%)") + ": +" + Num(rw.ChallengeMoney) + " ₽" : ""));
            if (rw.LevelUps > 0) Lbl(new Rect(r.x + 24, r.y + 380, 620, 60), "<color=#ff7a00><b>🎉 Новый уровень: " + rw.Level + "!</b></color>\nОткрыты новые машины и карты — загляни в гараж.", stLabel);
            if (Btn(new Rect(r.x + 24, r.yMax - 64, 140, 44), "Ещё раз", true)) StartGame();
            if (Btn(new Rect(r.x + 174, r.yMax - 64, 170, 44), "Выбор заезда")) { LeaveGame(); Go(Scr.Play, false); }
            if (Btn(new Rect(r.x + 354, r.yMax - 64, 130, 44), "Гараж")) { LeaveGame(); garCar = progress.Data.selected; Go(Scr.Garage, false); }
            if (Btn(new Rect(r.x + 494, r.yMax - 64, 140, 44), "Меню")) { LeaveGame(); Go(Scr.Menu, false); }
        }
    }
}
