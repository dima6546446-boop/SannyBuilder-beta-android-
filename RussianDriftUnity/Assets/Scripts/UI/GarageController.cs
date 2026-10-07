using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Audio;
using RussianDrift.Core;
using RussianDrift.Meta;
using RussianDrift.Vehicle;
using RussianDrift.World;

namespace RussianDrift.UI
{
    /// <summary>Garage scene: 3D showroom + tabs for cars, performance tuning, visual customisation and the driver.</summary>
    public class GarageController : MonoBehaviour
    {
        private enum Tab { Cars, Tuning, Visual, Driver }
        private enum VisualTab { Paint, Wrap, Spoiler, Kit, Rims, Neon, Decal, Plate }

        private Canvas canvas;
        private RectTransform safe;
        private Showroom showroom;
        private ProfileService profile;
        private UiWidgets.ProfileBar profileBar;
        private UiWidgets.StatBars statBars;
        private Text carNameText, carStatusText;
        private RectTransform tabBar, body;
        private Tab tab = Tab.Cars;
        private VisualTab vtab = VisualTab.Paint;
        private string previewCarId;
        private CosmeticDefinition pendingCosmetic;
        private bool dirty;
        private float dirtyTimer;
        private CarSetup previewSetup;      // non-null while previewing an unowned car / unbought part

        private CarSetup LiveSetup { get { return profile.Data.GetSetup(previewCarId); } }
        private CarDefinition PreviewDef { get { return GameCatalog.GetCar(previewCarId); } }

        private static readonly string[] paintHex =
        {
            "C8281E","E6E6E6","1C1C20","2E5AAC","0F3A2A","D7B26A","7A2CC0","FF7A00","FFD21F","00A3A3","FF4FA0","8A8D93","5A1F1F","0B2A6B","6FD1FF","A6FF3B","B87333","F5F0DC","2B2B2B","FF2020"
        };
        private static readonly string[] skinHex = { "F2CDAA", "E0B48C", "C68E64", "9B6B45", "6E4A30" };
        private static readonly string[] outfitHexes = { "2B3A55", "A32020", "1F1F23", "E8E8E8", "2F6B3A", "C98A12", "6A2B8F", "00798C" };

        private void Start()
        {
            profile = Services.Get<ProfileService>();
            if (profile == null) { SceneLoader.Load(SceneNames.Boot); return; }
            GameCatalog.EnsureLoaded();
            Time.timeScale = 1f;
            previewCarId = profile.Data.selectedCar;
            showroom = Showroom.Create(true);
            canvas = Ui.CreateCanvas("GarageCanvas", 10);
            var pad = Ui.Rect("OrbitPad", canvas.transform);
            Ui.Stretch(pad);
            var pi = pad.gameObject.AddComponent<Image>(); pi.color = new Color(0, 0, 0, 0.001f);
            pad.gameObject.AddComponent<OrbitPad>().showroom = showroom;
            safe = Ui.SafeArea(canvas.transform);
            BuildFrame();
            RebuildCar();
            SwitchTab(Tab.Cars);
            AudioManager.Ensure().StartPlaylist("music_menu");
            GameEvents.CurrencyChanged += OnMoney;
            GameEvents.LanguageChanged += OnLang;
            SceneLoader.FadeIn();
        }

        private void OnDestroy() { GameEvents.CurrencyChanged -= OnMoney; GameEvents.LanguageChanged -= OnLang; }
        private void OnMoney(long v) { if (profileBar != null) profileBar.Refresh(); }
        private void OnLang() { RefreshInfo(); if (body != null) SwitchTab(tab); }

        private void Update()
        {
            if (dirty)
            {
                dirtyTimer -= Time.unscaledDeltaTime;
                if (dirtyTimer <= 0f) { dirty = false; RebuildCar(); }
            }
        }

        private void RequestRebuild() { dirty = true; dirtyTimer = 0.12f; }

        // ------------------------------------------------------------------ frame
        private void BuildFrame()
        {
            var back = Ui.Button(safe, "common.back", () => { profile.Save.Save(); SceneLoader.Load(SceneNames.MainMenu); }, Theme.PanelLight, 34);
            Ui.Place(back.image.rectTransform, new Vector2(0, 1), new Vector2(30, -26), new Vector2(260, 84), new Vector2(0, 1));
            profileBar = UiWidgets.CreateProfileBar(safe);

            var info = Ui.Panel(safe, "Info", new Color(0.05f, 0.06f, 0.09f, 0.78f), 22);
            Ui.Place(info.rectTransform, new Vector2(0, 1), new Vector2(30, -128), new Vector2(560, 430), new Vector2(0, 1));
            carNameText = Ui.Label(info.transform, "", 44, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(carNameText.rectTransform, new Vector2(0, 1), new Vector2(24, -14), new Vector2(520, 60), new Vector2(0, 1));
            carStatusText = Ui.Label(info.transform, "", 26, Theme.Accent2, TextAnchor.MiddleLeft);
            Ui.Place(carStatusText.rectTransform, new Vector2(0, 1), new Vector2(26, -74), new Vector2(520, 36), new Vector2(0, 1));
            statBars = UiWidgets.CreateStatBars(info.transform, 500);
            Ui.Place(statBars.root, new Vector2(0, 0), new Vector2(26, 22), new Vector2(500, 290), new Vector2(0, 0));

            var bottom = Ui.Panel(safe, "Bottom", new Color(0.04f, 0.05f, 0.08f, 0.92f), 26);
            Ui.Place(bottom.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(1840, 470), new Vector2(0.5f, 0));
            tabBar = Ui.Rect("Tabs", bottom.transform);
            Ui.Anchor(tabBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -92), new Vector2(-20, -10));
            var h = Ui.HBox(tabBar, 12, TextAnchor.MiddleLeft, true, true);
            Ui.Stretch((RectTransform)h.transform);
            string[] keys = { "garage.cars", "garage.tuning", "garage.visual", "garage.driver" };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var b = Ui.Button(h.transform, keys[i], () => SwitchTab((Tab)idx), Theme.PanelLight, 32);
                Ui.Size(b.image.transform, -1, 80, 1);
                b.image.name = "Tab" + i;
            }
            body = Ui.Rect("Body", bottom.transform);
            Ui.Anchor(body, Vector2.zero, Vector2.one, new Vector2(20, 14), new Vector2(-20, -104));
        }

        private void HighlightTabs()
        {
            for (int i = 0; i < 4; i++)
            {
                var t = tabBar.GetComponentsInChildren<Image>(true);
                foreach (var im in t) if (im.name == "Tab" + i) im.color = (int)tab == i ? Theme.Accent : Theme.PanelLight;
            }
        }

        private void Clear(RectTransform rt) { for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject); }

        private void SwitchTab(Tab t)
        {
            tab = t;
            pendingCosmetic = null; previewSetup = null;
            HighlightTabs();
            Clear(body);
            showroom.FocusDriver(t == Tab.Driver);
            if (t == Tab.Driver) showroom.ShowDriver(); else showroom.HideDriver();
            switch (t)
            {
                case Tab.Cars: BuildCars(); break;
                case Tab.Tuning: BuildTuning(); break;
                case Tab.Visual: BuildVisual(); break;
                default: BuildDriver(); break;
            }
            RefreshInfo();
        }

        // ------------------------------------------------------------------ car rebuild / info
        private void RebuildCar()
        {
            var def = PreviewDef;
            var setup = previewSetup ?? LiveSetup;
            if (string.IsNullOrEmpty(setup.colorHex)) setup.colorHex = ColorUtility.ToHtmlStringRGB(def.defaultColor);
            showroom.SetCar(def, setup);
            showroom.SetDriver(profile.Data.driver, new Vector3(-(def.width * 0.5f + 1.1f), 0f, def.length * 0.2f));
            showroom.driver.pose = PoseKind.Wave;
            if (tab == Tab.Driver) showroom.FocusDriver(true); else showroom.HideDriver();
            RefreshInfo();
        }

        private void RefreshInfo()
        {
            if (carNameText == null) return;
            var def = PreviewDef;
            var setup = previewSetup ?? LiveSetup;
            carNameText.text = GameCatalog.CarName(def);
            bool owned = profile.OwnsCar(def.id);
            carStatusText.text = owned ? (profile.Data.selectedCar == def.id ? Loc.T("garage.selected") : Loc.T("garage.owned")) : Loc.F("garage.price", Ui.Money(def.price));
            statBars.Set(VehicleStats.Compute(def, setup));
            profileBar.Refresh();
        }

        // ------------------------------------------------------------------ cars tab
        private void BuildCars()
        {
            RectTransform content;
            var sr = Ui.Scroll(body, false, out content, 16f);
            Ui.Anchor((RectTransform)sr.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-440, 0));
            foreach (var car in GameCatalog.Cars)
            {
                var c = car;
                bool owned = profile.OwnsCar(c.id);
                bool locked = !owned && profile.Level < c.requiredLevel;
                bool sel = previewCarId == c.id;
                var card = Ui.Panel(content, "Car", sel ? Theme.Accent : Theme.PanelLight, 18);
                Ui.Size(card.transform, 300, -1);
                var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card;
                b.onClick.AddListener(() => { AudioManager.Click(); previewCarId = c.id; previewSetup = null; RebuildCar(); BuildCarsRefresh(); });
                var swatch = Ui.Img(card.transform, "Swatch", UiSprites.Rounded(14), c.defaultColor, true);
                Ui.Anchor(swatch.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(18, 0), new Vector2(-18, -18));
                var shape = Ui.Label(swatch.transform, GameCatalog.CarName(c).ToUpperInvariant(), 34, new Color(0, 0, 0, 0.55f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
                Ui.Stretch(shape.rectTransform, 6, 6, 6, 6);
                string status = owned ? Loc.T("garage.owned") : (locked ? Loc.F("common.reqlevel", c.requiredLevel) : Ui.Money(c.price));
                var st = Ui.Label(card.transform, status, 30, owned ? Theme.Good : (locked ? Theme.Bad : Theme.Gold), TextAnchor.LowerCenter, FontStyle.Bold);
                Ui.Anchor(st.rectTransform, Vector2.zero, new Vector2(1, 0.5f), new Vector2(0, 14), new Vector2(0, -10));
            }
            // action panel
            var act = Ui.Panel(body, "Action", new Color(1, 1, 1, 0.05f), 18);
            Ui.Anchor(act.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-420, 0), Vector2.zero);
            var def = PreviewDef;
            var desc = Ui.Label(act.transform, GameCatalog.CarDesc(def), 24, Theme.Dim, TextAnchor.UpperLeft);
            Ui.Anchor(desc.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 130), new Vector2(-18, -14));
            bool own = profile.OwnsCar(def.id);
            bool lockd = !own && profile.Level < def.requiredLevel;
            var act1 = Ui.Button(act.transform, own ? (profile.Data.selectedCar == def.id ? "garage.selected" : "garage.select") : (lockd ? "common.locked" : "garage.buy"), () =>
            {
                if (own) { profile.SelectCar(def.id); AudioManager.Ensure().Play("confirm"); SwitchTab(Tab.Cars); }
                else
                {
                    var r = profile.BuyCar(def);
                    if (r == PurchaseResult.Ok) { AudioManager.Ensure().Play("cash"); previewCarId = def.id; RebuildCar(); SwitchTab(Tab.Cars); Services.Get<AchievementService>().Check(); }
                    else Toast.Show(Loc.T(r == PurchaseResult.NotEnoughMoney ? "msg.nomoney" : "msg.lowlevel"));
                }
            }, own ? Theme.Good * 0.9f : (lockd ? new Color(0.3f, 0.3f, 0.34f, 0.9f) : Theme.Accent), 32);
            act1.button.interactable = !(own && profile.Data.selectedCar == def.id) && !lockd;
            Ui.Anchor(act1.image.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 104));
        }

        private void BuildCarsRefresh() { Clear(body); BuildCars(); RefreshInfo(); }

        // ------------------------------------------------------------------ tuning tab
        private void BuildTuning()
        {
            if (!profile.OwnsCar(previewCarId)) { var n = Ui.Loc(body, "garage.ownfirst", 36, Theme.Dim); Ui.Stretch(n.rectTransform); return; }
            var setup = LiveSetup;
            var left = Ui.Rect("Upg", body);
            Ui.Anchor(left, Vector2.zero, new Vector2(0.64f, 1), Vector2.zero, Vector2.zero);
            RectTransform content;
            var sr = Ui.Scroll(left, true, out content, 8f);
            Ui.Stretch((RectTransform)sr.transform);
            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                var c = cat;
                var u = GameCatalog.GetUpgrade(c);
                var row = Ui.Panel(content, "Upg", Theme.PanelLight, 14);
                Ui.Size(row.transform, -1, 92);
                var nm = Ui.Label(row.transform, GameCatalog.UpgName(u), 30, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                Ui.Anchor(nm.rectTransform, Vector2.zero, new Vector2(0.3f, 1), new Vector2(20, 0), Vector2.zero);
                int lv = setup.GetLevel(c);
                var pips = Ui.HBox(row.transform, 6, TextAnchor.MiddleLeft, false, false);
                Ui.Anchor((RectTransform)pips.transform, new Vector2(0.3f, 0), new Vector2(0.62f, 1), Vector2.zero, Vector2.zero);
                for (int i = 0; i < u.maxLevel; i++)
                {
                    var pip = Ui.Img(pips.transform, "Pip", UiSprites.Rounded(5), i < lv ? Theme.Accent : new Color(1, 1, 1, 0.16f), true);
                    Ui.Size(pip.transform, 34, 22);
                }
                bool maxed = lv >= u.maxLevel;
                int price = u.PriceForLevel(lv + 1);
                var buy = Ui.Button(row.transform, maxed ? Loc.T("garage.max") : Ui.Money(price), () =>
                {
                    var r = profile.BuyUpgrade(c, setup);
                    if (r == PurchaseResult.Ok) { AudioManager.Ensure().Play("cash"); RebuildCar(); Clear(body); BuildTuning(); Services.Get<AchievementService>().Check(); }
                    else if (r == PurchaseResult.NotEnoughMoney) Toast.Show(Loc.T("msg.nomoney"));
                }, maxed ? new Color(0.25f, 0.3f, 0.28f, 0.9f) : Theme.Accent, 28, false);
                buy.button.interactable = !maxed;
                Ui.Anchor(buy.image.rectTransform, new Vector2(0.64f, 0), Vector2.one, new Vector2(8, 10), new Vector2(-14, -10));
            }
            // right: chassis
            var right = Ui.VBox(body, 10, TextAnchor.UpperLeft, true, false);
            Ui.Anchor((RectTransform)right.transform, new Vector2(0.66f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            var lbl = Ui.Loc(right.transform, "garage.drive", 28, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(lbl.transform, -1, 36);
            var seg = Ui.Rect("seg", right.transform); Ui.Size(seg, -1, 70);
            SettingsPanel.Segmented(seg, new[] { "RWD", "AWD" }, false, setup.drive, i => { setup.drive = i; profile.Save.Save(); RefreshInfo(); }, 200f);
            AddSlider(right.transform, "garage.clearance", Mathf.InverseLerp(-1f, 1f, setup.clearance), v => { setup.clearance = Mathf.Lerp(-1f, 1f, v); profile.Save.Save(); RefreshInfo(); });
            AddSlider(right.transform, "garage.camber", setup.camber, v => { setup.camber = v; profile.Save.Save(); RefreshInfo(); });
            var tl = Ui.Loc(right.transform, "garage.tint", 28, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(tl.transform, -1, 36);
            var seg2 = Ui.Rect("tint", right.transform); Ui.Size(seg2, -1, 70);
            SettingsPanel.Segmented(seg2, new[] { "0", "1", "2", "3" }, false, setup.tint, i => { setup.tint = i; profile.Save.Save(); RequestRebuild(); }, 110f);
        }

        private void AddSlider(Transform parent, string key, float value, Action<float> on)
        {
            var l = Ui.Loc(parent, key, 28, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l.transform, -1, 36);
            var holder = Ui.Rect("s", parent); Ui.Size(holder, -1, 50);
            var s = Ui.Slider(holder, 0f, 1f, value, on);
            Ui.Stretch((RectTransform)s.transform);
            s.onValueChanged.AddListener(v => RequestRebuild());
        }

        // ------------------------------------------------------------------ visual tab
        private void BuildVisual()
        {
            if (!profile.OwnsCar(previewCarId)) { var n = Ui.Loc(body, "garage.ownfirst", 36, Theme.Dim); Ui.Stretch(n.rectTransform); return; }
            var bar = Ui.Rect("Sub", body);
            Ui.Anchor(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), Vector2.zero);
            var h = Ui.HBox(bar, 8, TextAnchor.MiddleLeft, true, true);
            Ui.Stretch((RectTransform)h.transform);
            string[] keys = { "vis.paint", "vis.wrap", "vis.spoiler", "vis.kit", "vis.rims", "vis.neon", "vis.decal", "vis.plate" };
            for (int i = 0; i < keys.Length; i++)
            {
                int idx = i;
                var b = Ui.Button(h.transform, keys[i], () => { vtab = (VisualTab)idx; pendingCosmetic = null; previewSetup = null; RequestRebuild(); Clear(body); BuildVisual(); }, vtab == (VisualTab)i ? Theme.Accent : Theme.PanelLight, 24, true, 12);
                Ui.Size(b.image.transform, -1, 64, 1);
            }
            var area = Ui.Rect("Area", body);
            Ui.Anchor(area, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -78));
            switch (vtab)
            {
                case VisualTab.Paint: BuildPaint(area); break;
                case VisualTab.Wrap: BuildItems(area, CosmeticCategory.Wrap); break;
                case VisualTab.Spoiler: BuildItems(area, CosmeticCategory.Spoiler); break;
                case VisualTab.Kit: BuildItems(area, CosmeticCategory.BodyKit); break;
                case VisualTab.Rims: BuildItems(area, CosmeticCategory.Rims); break;
                case VisualTab.Neon: BuildItems(area, CosmeticCategory.Neon); break;
                case VisualTab.Decal: BuildItems(area, CosmeticCategory.Decal); break;
                default: BuildPlate(area); break;
            }
        }

        private void BuildPaint(RectTransform area)
        {
            var setup = LiveSetup;
            var grid = Ui.Grid(area, new Vector2(88, 88), new Vector2(10, 10), 10);
            Ui.Anchor((RectTransform)grid.transform, new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(0, 0), Vector2.zero);
            foreach (var hex in paintHex)
            {
                string hx = hex;
                Color col = VehicleStats.ParseColor(hx, Color.white);
                var sw = Ui.Img(grid.transform, "Sw", UiSprites.Rounded(16), col, true);
                sw.raycastTarget = true;
                var b = sw.gameObject.AddComponent<Button>(); b.targetGraphic = sw;
                b.onClick.AddListener(() => { AudioManager.Click(); setup.colorHex = hx; profile.Save.Save(); RequestRebuild(); });
            }
            // custom HSV
            var col2 = Ui.VBox(area, 6, TextAnchor.UpperLeft, true, false);
            Ui.Anchor((RectTransform)col2.transform, new Vector2(0.62f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            Color cur = VehicleStats.ParseColor(setup.colorHex, Color.white);
            float hh, ss, vv; Color.RGBToHSV(cur, out hh, out ss, out vv);
            float[] hsv = { hh, ss, vv };
            string[] keys = { "paint.hue", "paint.sat", "paint.val" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var l = Ui.Loc(col2.transform, keys[i], 26, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l.transform, -1, 32);
                var holder = Ui.Rect("s", col2.transform); Ui.Size(holder, -1, 56);
                var s = Ui.Slider(holder, 0f, 1f, hsv[i], v =>
                {
                    hsv[idx] = v;
                    setup.colorHex = ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(hsv[0], hsv[1], hsv[2]));
                    profile.Save.Save(); RequestRebuild();
                });
                Ui.Stretch((RectTransform)s.transform);
            }
        }

        private void BuildItems(RectTransform area, CosmeticCategory cat)
        {
            var setup = previewSetup ?? LiveSetup;
            var live = LiveSetup;
            RectTransform content;
            var sr = Ui.Scroll(area, false, out content, 14f);
            bool rims = cat == CosmeticCategory.Rims;
            Ui.Anchor((RectTransform)sr.transform, Vector2.zero, Vector2.one, new Vector2(0, rims ? 104 : 0), new Vector2(-(pendingCosmetic != null ? 360 : 0), 0));
            foreach (var d in GameCatalog.GetCosmetics(cat))
            {
                var def = d;
                bool owned = profile.OwnsCosmetic(live, d);
                bool equipped = ProfileService.EquippedId(live, cat) == d.id && previewSetup == null;
                bool previewed = pendingCosmetic == d;
                bool locked = !owned && profile.Level < d.requiredLevel;
                var card = Ui.Panel(content, "Item", equipped ? Theme.Accent : (previewed ? Theme.Accent2 * 0.8f : Theme.PanelLight), 16);
                Ui.Size(card.transform, 250, -1);
                var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card;
                b.onClick.AddListener(() => OnItemPicked(def, cat));
                var nm = Ui.Label(card.transform, GameCatalog.CosName(d), 28, Theme.Text, TextAnchor.UpperCenter, FontStyle.Bold);
                Ui.Anchor(nm.rectTransform, new Vector2(0, 0.35f), Vector2.one, new Vector2(8, 0), new Vector2(-8, -14));
                if (cat == CosmeticCategory.Neon && d.id != "none")
                {
                    var dot = Ui.Img(card.transform, "Dot", UiSprites.Circle(), d.color);
                    Ui.Place(dot.rectTransform, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(50, 50), new Vector2(0.5f, 0.5f));
                }
                string status = equipped ? Loc.T("garage.equipped") : (owned ? Loc.T("garage.owned") : (locked ? Loc.F("common.reqlevel", d.requiredLevel) : Ui.Money(d.price)));
                var st = Ui.Label(card.transform, status, 26, equipped ? Color.white : (owned ? Theme.Good : (locked ? Theme.Bad : Theme.Gold)), TextAnchor.LowerCenter, FontStyle.Bold);
                Ui.Anchor(st.rectTransform, Vector2.zero, new Vector2(1, 0.35f), new Vector2(0, 10), Vector2.zero);
            }
            if (rims)
            {
                var row = Ui.HBox(area, 8, TextAnchor.MiddleLeft, false, true);
                Ui.Anchor((RectTransform)row.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 96));
                var l = Ui.Loc(row.transform, "paint.rimcolor", 26, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l.transform, 240, 80);
                string[] cols = { "C8C8C8", "1B1B1B", "FFD21F", "FF2020", "2060FF", "B87333", "FFFFFF", "00C080" };
                foreach (var hx in cols)
                {
                    string h2 = hx;
                    var sw = Ui.Img(row.transform, "Sw", UiSprites.Circle(), VehicleStats.ParseColor(hx, Color.white));
                    sw.raycastTarget = true; Ui.Size(sw.transform, 70, 70);
                    var bt = sw.gameObject.AddComponent<Button>(); bt.targetGraphic = sw;
                    bt.onClick.AddListener(() => { AudioManager.Click(); live.rimColorHex = h2; if (previewSetup != null) previewSetup.rimColorHex = h2; profile.Save.Save(); RequestRebuild(); });
                }
            }
            if (pendingCosmetic != null)
            {
                var d = pendingCosmetic;
                var panel = Ui.Panel(area, "Buy", new Color(1, 1, 1, 0.06f), 16);
                Ui.Anchor(panel.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-340, 0), Vector2.zero);
                var t = Ui.Label(panel.transform, GameCatalog.CosName(d) + "\n<color=#FFD133>" + Ui.Money(d.price) + "</color>", 32, Theme.Text, TextAnchor.UpperCenter, FontStyle.Bold);
                Ui.Anchor(t.rectTransform, new Vector2(0, 0.4f), Vector2.one, new Vector2(10, 0), new Vector2(-10, -14));
                bool locked = profile.Level < d.requiredLevel;
                var buy = Ui.Button(panel.transform, locked ? "common.locked" : "garage.buy", () =>
                {
                    var r = profile.BuyCosmetic(live, d);
                    if (r == PurchaseResult.Ok)
                    {
                        ProfileService.Equip(live, d); profile.Save.Save();
                        AudioManager.Ensure().Play("cash");
                        pendingCosmetic = null; previewSetup = null; RebuildCar(); Clear(body); BuildVisual();
                    }
                    else Toast.Show(Loc.T(r == PurchaseResult.NotEnoughMoney ? "msg.nomoney" : "msg.lowlevel"));
                }, locked ? new Color(0.3f, 0.3f, 0.34f, 0.9f) : Theme.Accent, 32);
                buy.button.interactable = !locked;
                Ui.Anchor(buy.image.rectTransform, Vector2.zero, new Vector2(1, 0.4f), new Vector2(14, 14), new Vector2(-14, -6));
            }
        }

        private void OnItemPicked(CosmeticDefinition d, CosmeticCategory cat)
        {
            AudioManager.Click();
            var live = LiveSetup;
            if (profile.OwnsCosmetic(live, d))
            {
                ProfileService.Equip(live, d);
                profile.Save.Save();
                pendingCosmetic = null; previewSetup = null;
            }
            else
            {
                pendingCosmetic = d;
                previewSetup = live.Clone();
                ProfileService.Equip(previewSetup, d);
            }
            RebuildCar();
            Clear(body); BuildVisual();
        }

        private void BuildPlate(RectTransform area)
        {
            var setup = LiveSetup;
            var l = Ui.Loc(area, "plate.text", 30, Theme.Dim, TextAnchor.MiddleLeft);
            Ui.Place(l.rectTransform, new Vector2(0, 1), new Vector2(0, -10), new Vector2(700, 40), new Vector2(0, 1));
            var field = MakeInput(area, setup.plate, 6, "plate.hint", new Vector2(0, -60), new Vector2(380, 90), InputField.CharacterValidation.None, s =>
            {
                string clean = ProcTex.SanitizePlate(s);
                setup.plate = clean; profile.Save.Save(); RequestRebuild();
            });
            var l2 = Ui.Loc(area, "plate.region", 30, Theme.Dim, TextAnchor.MiddleLeft);
            Ui.Place(l2.rectTransform, new Vector2(0, 1), new Vector2(430, -10), new Vector2(500, 40), new Vector2(0, 1));
            MakeInput(area, setup.region, 3, "77", new Vector2(430, -60), new Vector2(200, 90), InputField.CharacterValidation.Integer, s =>
            {
                setup.region = string.IsNullOrEmpty(s) ? "77" : s; profile.Save.Save(); RequestRebuild();
            });
            var hint = Ui.Loc(area, "plate.allowed", 24, Theme.Dim, TextAnchor.UpperLeft);
            Ui.Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, -170), new Vector2(1200, 60), new Vector2(0, 1));
        }

        private InputField MakeInput(RectTransform parent, string value, int maxLen, string placeholderKey, Vector2 pos, Vector2 size, InputField.CharacterValidation validation, Action<string> onEnd)
        {
            var bg = Ui.Panel(parent, "Input", new Color(1, 1, 1, 0.1f), 14);
            Ui.Place(bg.rectTransform, new Vector2(0, 1), pos, size, new Vector2(0, 1));
            var text = Ui.Label(bg.transform, "", 44, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(text.rectTransform, 10, 6, 10, 6);
            var ph = Ui.Label(bg.transform, placeholderKey.Length > 3 ? Loc.T(placeholderKey) : placeholderKey, 34, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter);
            Ui.Stretch(ph.rectTransform, 10, 6, 10, 6);
            var f = bg.gameObject.AddComponent<InputField>();
            f.textComponent = text; f.placeholder = ph; f.characterLimit = maxLen; f.characterValidation = validation;
            f.text = value;
            f.onEndEdit.AddListener(s => { if (onEnd != null) onEnd(s); });
            return f;
        }

        // ------------------------------------------------------------------ driver tab
        private void BuildDriver()
        {
            var look = profile.Data.driver;
            var col = Ui.VBox(body, 8, TextAnchor.UpperLeft, true, false);
            Ui.Anchor((RectTransform)col.transform, Vector2.zero, new Vector2(0.55f, 1), Vector2.zero, Vector2.zero);
            Action refresh = () => { showroom.driver.Rebuild(look); profile.Save.Save(); };
            var l1 = Ui.Loc(col.transform, "driver.outfit", 26, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l1.transform, -1, 32);
            var r1 = Ui.Rect("r", col.transform); Ui.Size(r1, -1, 64);
            SettingsPanel.Segmented(r1, new[] { "driver.o0", "driver.o1", "driver.o2", "driver.o3", "driver.o4" }, true, look.outfit, i => { look.outfit = i; refresh(); }, 190f);
            var l2 = Ui.Loc(col.transform, "driver.cap", 26, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l2.transform, -1, 32);
            var r2 = Ui.Rect("r", col.transform); Ui.Size(r2, -1, 64);
            SettingsPanel.Segmented(r2, new[] { "driver.none", "driver.cap1", "driver.cap2", "driver.cap3" }, true, look.cap, i => { look.cap = i; refresh(); }, 190f);
            var l3 = Ui.Loc(col.transform, "driver.glasses", 26, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l3.transform, -1, 32);
            var r3 = Ui.Rect("r", col.transform); Ui.Size(r3, -1, 64);
            SettingsPanel.Segmented(r3, new[] { "driver.none", "driver.gl1", "driver.gl2" }, true, look.glasses, i => { look.glasses = i; refresh(); }, 190f);

            var col2 = Ui.VBox(body, 8, TextAnchor.UpperLeft, true, false);
            Ui.Anchor((RectTransform)col2.transform, new Vector2(0.58f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            var l4 = Ui.Loc(col2.transform, "driver.skin", 26, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l4.transform, -1, 32);
            SwatchRow(col2.transform, skinHex, hx => { look.skinHex = hx; refresh(); });
            var l5 = Ui.Loc(col2.transform, "driver.color", 26, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l5.transform, -1, 32);
            SwatchRow(col2.transform, outfitHexes, hx => { look.outfitHex = hx; refresh(); });
        }

        private void SwatchRow(Transform parent, string[] hexes, Action<string> pick)
        {
            var row = Ui.HBox(parent, 10, TextAnchor.MiddleLeft, false, true);
            Ui.Size(row.transform, -1, 80);
            foreach (var hx in hexes)
            {
                string h = hx;
                var sw = Ui.Img(row.transform, "Sw", UiSprites.Circle(), VehicleStats.ParseColor(hx, Color.white));
                sw.raycastTarget = true; Ui.Size(sw.transform, 74, 74);
                var b = sw.gameObject.AddComponent<Button>(); b.targetGraphic = sw;
                b.onClick.AddListener(() => { AudioManager.Click(); pick(h); });
            }
        }
    }
}
