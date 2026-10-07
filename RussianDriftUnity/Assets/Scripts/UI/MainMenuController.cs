using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Audio;
using RussianDrift.Core;
using RussianDrift.Meta;
using RussianDrift.Vehicle;

namespace RussianDrift.UI
{
    /// <summary>Main menu scene: showroom background, profile bar, big navigation buttons and the mode/track picker.</summary>
    public class MainMenuController : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform safe;
        private Showroom showroom;
        private UiWidgets.ProfileBar profileBar;
        private UiWidgets.StatBars statBars;
        private Text carName;
        private ProfileService profile;
        private Image dailyDot;

        private void Start()
        {
            if (Services.Get<ProfileService>() == null) { SceneLoader.Load(SceneNames.Boot); return; }
            profile = Services.Get<ProfileService>();
            Time.timeScale = 1f;
            GameSession.Reset();
            QualityManager.Apply();

            showroom = Showroom.Create(false);
            RefreshCar();
            canvas = Ui.CreateCanvas("MenuCanvas", 10);
            var pad = Ui.Rect("OrbitPad", canvas.transform);
            Ui.Stretch(pad);
            var padImg = pad.gameObject.AddComponent<Image>(); padImg.color = new Color(0, 0, 0, 0.001f);
            pad.gameObject.AddComponent<OrbitPad>().showroom = showroom;
            safe = Ui.SafeArea(canvas.transform);
            BuildHome();

            AudioManager.Ensure().StartPlaylist("music_menu");
            GameEvents.CurrencyChanged += OnProfileChanged;
            GameEvents.LevelUp += OnLevel;
            GameEvents.LanguageChanged += OnLang;
            SceneLoader.FadeIn();

            var quests = Services.Get<QuestService>();
            if (quests != null) quests.Refresh();
            CheckDailyBadge();
            if (profile.Save.WasTampered) Toast.Show(Loc.T("msg.tamper"));
        }

        private void OnDestroy()
        {
            GameEvents.CurrencyChanged -= OnProfileChanged;
            GameEvents.LevelUp -= OnLevel;
            GameEvents.LanguageChanged -= OnLang;
        }

        private void OnProfileChanged(long v) { if (profileBar != null) profileBar.Refresh(); }
        private void OnLevel(int l) { if (profileBar != null) profileBar.Refresh(); Toast.Show(Loc.F("msg.levelup", l)); AudioManager.Ensure().Play("levelup"); }
        private void OnLang() { RefreshCarLabels(); }

        private void RefreshCar()
        {
            var def = profile.CurrentCar;
            var setup = profile.CurrentSetup;
            if (string.IsNullOrEmpty(setup.colorHex)) setup.colorHex = ColorUtility.ToHtmlStringRGB(def.defaultColor);
            showroom.SetCar(def, setup);
            showroom.SetDriver(profile.Data.driver, new Vector3(-(def.width * 0.5f + 1.1f), 0f, def.length * 0.18f));
            showroom.driver.pose = PoseKindWave();
        }

        private static RussianDrift.World.PoseKind PoseKindWave() { return RussianDrift.World.PoseKind.Wave; }

        private void RefreshCarLabels()
        {
            if (carName != null) carName.text = GameCatalog.CarName(profile.CurrentCar);
        }

        private void CheckDailyBadge()
        {
            var q = Services.Get<QuestService>();
            bool avail = q != null && (q.DailyAvailable);
            if (!avail && q != null) foreach (var d in q.today) if (q.IsComplete(d) && !q.ProgressOf(d.id).claimed) avail = true;
            if (dailyDot != null) dailyDot.gameObject.SetActive(avail);
        }

        // ------------------------------------------------------------------ home
        private void BuildHome()
        {
            // title
            var title = Ui.Label(safe, "RUSSIAN <color=#FF6120>DRIFT</color>", 78, Theme.Text, TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(40, -26), new Vector2(900, 110), new Vector2(0, 1));
            var sub = Ui.Loc(safe, "menu.tagline", 28, Theme.Dim, TextAnchor.UpperLeft);
            Ui.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(46, -118), new Vector2(900, 40), new Vector2(0, 1));

            profileBar = UiWidgets.CreateProfileBar(safe);
            var settingsBtn = Ui.IconButton(safe, UiSprites.Gear(), () => OpenSettings(), new Color(0.1f, 0.12f, 0.18f, 0.9f), 0.62f, 22);
            Ui.Place(settingsBtn.image.rectTransform, new Vector2(1, 1), new Vector2(-30, -140), new Vector2(96, 96), new Vector2(1, 1));

            // left menu column
            var col = Ui.VBox(safe, 16, TextAnchor.LowerLeft, true, false);
            Ui.Place((RectTransform)col.transform, new Vector2(0, 0), new Vector2(40, 40), new Vector2(520, 700), new Vector2(0, 0));
            var play = Ui.Button(col.transform, "menu.play", OpenModes, Theme.Accent, 56, true, 26);
            Ui.Size(play.image.transform, -1, 140);
            var garage = Ui.Button(col.transform, "menu.garage", () => SceneLoader.Load(SceneNames.Garage), Theme.PanelLight, 40);
            Ui.Size(garage.image.transform, -1, 104);
            var row = Ui.HBox(col.transform, 14, TextAnchor.MiddleLeft, true, true);
            Ui.Size(row.transform, -1, 100);
            var shop = Ui.Button(row.transform, "menu.shop", OpenShop, Theme.PanelLight, 30);
            Ui.Size(shop.image.transform, -1, 100, 1);
            var daily = Ui.Button(row.transform, "menu.daily", OpenDaily, Theme.PanelLight, 30);
            Ui.Size(daily.image.transform, -1, 100, 1);
            dailyDot = Ui.Img(daily.image.transform, "Dot", UiSprites.Circle(), Theme.Bad);
            Ui.Place(dailyDot.rectTransform, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(26, 26), new Vector2(1, 1));
            var row2 = Ui.HBox(col.transform, 14, TextAnchor.MiddleLeft, true, true);
            Ui.Size(row2.transform, -1, 90);
            var awards = Ui.Button(row2.transform, "menu.awards", OpenAwards, Theme.PanelLight, 28);
            Ui.Size(awards.image.transform, -1, 90, 1);
            var exit = Ui.Button(row2.transform, "menu.exit", Application.Quit, new Color(0.3f, 0.12f, 0.12f, 0.95f), 28);
            Ui.Size(exit.image.transform, -1, 90, 1);

            // right: car info
            var info = Ui.Panel(safe, "CarInfo", new Color(0.05f, 0.06f, 0.09f, 0.78f), 24);
            Ui.Place(info.rectTransform, new Vector2(1, 0), new Vector2(-30, 40), new Vector2(620, 470), new Vector2(1, 0));
            carName = Ui.Label(info.transform, GameCatalog.CarName(profile.CurrentCar), 46, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(carName.rectTransform, new Vector2(0, 1), new Vector2(28, -20), new Vector2(560, 64), new Vector2(0, 1));
            var drive = Ui.Label(info.transform, "", 24, Theme.Accent2, TextAnchor.MiddleLeft);
            Ui.Place(drive.rectTransform, new Vector2(0, 1), new Vector2(30, -84), new Vector2(560, 34), new Vector2(0, 1));
            var stats = VehicleStats.Compute(profile.CurrentCar, profile.CurrentSetup);
            drive.text = (stats.drive == DriveType.AWD ? "AWD" : "RWD") + "  •  " + Mathf.RoundToInt(stats.mass) + " kg  •  " + Mathf.RoundToInt(stats.peakTorque * (1f + stats.turboBoost * 0.7f)) + " Nm";
            statBars = UiWidgets.CreateStatBars(info.transform, 560);
            Ui.Place(statBars.root, new Vector2(0, 0), new Vector2(30, 24), new Vector2(560, 300), new Vector2(0, 0));
            statBars.Set(stats);
            var change = Ui.Button(info.transform, "menu.changecar", () => SceneLoader.Load(SceneNames.Garage), Theme.PanelLight, 26);
            Ui.Place(change.image.rectTransform, new Vector2(1, 1), new Vector2(-22, -22), new Vector2(230, 56), new Vector2(1, 1));
        }

        // ------------------------------------------------------------------ settings, shop, daily, awards
        private void OpenSettings()
        {
            SettingsPanel.Create(canvas.transform, false, () => { profileBar.Refresh(); }, null);
        }

        private void OpenShop()
        {
            var m = UiWidgets.CreateModal(canvas.transform, "shop.title", new Vector2(1300, 820));
            var iap = Services.Get<IIapService>();
            var ads = Services.Get<IAdService>();
            var list = Ui.VBox(m.content, 14, TextAnchor.UpperLeft, true, false);
            Ui.Stretch((RectTransform)list.transform);
            // rewarded ad
            var adRow = Ui.Panel(list.transform, "AdRow", Theme.PanelLight, 18);
            Ui.Size(adRow.transform, -1, 130);
            var adT = Ui.Loc(adRow.transform, "shop.watchad", 34, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Anchor(adT.rectTransform, Vector2.zero, new Vector2(0.65f, 1), new Vector2(28, 0), Vector2.zero);
            var adBtn = Ui.Button(adRow.transform, "shop.watchad.btn", () =>
            {
                if (ads == null) return;
                ads.ShowRewarded("shop_coins", ok =>
                {
                    if (!ok) return;
                    profile.AddCurrency(1000);
                    profile.Save.Save();
                    AudioManager.Ensure().Play("cash");
                    Toast.Show(Loc.F("shop.gotcoins", 1000));
                });
            }, Theme.Good * 0.85f, 30);
            Ui.Place(adBtn.image.rectTransform, new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(330, 90), new Vector2(1, 0.5f));
            if (iap != null)
                foreach (var p in iap.Products)
                {
                    var prod = p;
                    var row = Ui.Panel(list.transform, "Prod", Theme.PanelLight, 18);
                    Ui.Size(row.transform, -1, 120);
                    var name = Ui.Label(row.transform, (Loc.IsRu ? p.titleRu : p.titleEn) + "  <color=#FFD133>+" + Ui.Money(p.coinsGranted) + "</color>", 34, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                    Ui.Anchor(name.rectTransform, Vector2.zero, new Vector2(0.65f, 1), new Vector2(28, 0), Vector2.zero);
                    var buy = Ui.Button(row.transform, iap.GetLocalizedPrice(p.id), () =>
                    {
                        iap.Purchase(prod.id, (ok, product) =>
                        {
                            if (!ok) { Toast.Show(Loc.T("shop.unavailable")); return; }
                            profile.AddCurrency(product.coinsGranted); profile.Save.Save();
                            AudioManager.Ensure().Play("cash");
                            Toast.Show(Loc.F("shop.gotcoins", product.coinsGranted));
                        });
                    }, Theme.Accent, 32, false);
                    Ui.Place(buy.image.rectTransform, new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(270, 84), new Vector2(1, 0.5f));
                }
            var restore = Ui.Button(list.transform, "shop.restore", () => { if (iap != null) iap.RestorePurchases(ok => Toast.Show(Loc.T(ok ? "common.done" : "common.failed"))); }, Theme.PanelLight, 28);
            Ui.Size(restore.image.transform, -1, 80);
        }

        private void OpenDaily()
        {
            var quests = Services.Get<QuestService>();
            quests.Refresh();
            UiWidgets.Modal m = null;
            m = UiWidgets.CreateModal(canvas.transform, "daily.title", new Vector2(1600, 900), () => { CheckDailyBadge(); profileBar.Refresh(); });
            BuildDailyContent(m, quests);
        }

        private void BuildDailyContent(UiWidgets.Modal m, QuestService quests)
        {
            for (int i = m.content.childCount - 1; i >= 0; i--) Destroy(m.content.GetChild(i).gameObject);
            // login rewards
            var head = Ui.Loc(m.content, "daily.login", 36, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            Ui.Place(head.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(900, 50), new Vector2(0, 1));
            var grid = Ui.Grid(m.content, new Vector2(196, 170), new Vector2(12, 12), 7);
            Ui.Place((RectTransform)grid.transform, new Vector2(0, 1), new Vector2(0, -64), new Vector2(1500, 180), new Vector2(0, 1));
            int next = quests.NextDailyIndex; bool avail = quests.DailyAvailable;
            for (int i = 0; i < QuestService.DailyRewards.Length; i++)
            {
                bool current = i == next && avail;
                bool done = i < next || (i == next && !avail);
                var card = Ui.Panel(grid.transform, "Day" + i, current ? Theme.Accent : (done ? new Color(0.15f, 0.3f, 0.2f, 0.95f) : Theme.PanelLight), 16);
                var d = Ui.Label(card.transform, Loc.F("daily.day", i + 1), 26, Theme.Dim, TextAnchor.UpperCenter);
                Ui.Anchor(d.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -14));
                var v = Ui.Label(card.transform, Ui.Money(QuestService.DailyRewards[i]), 34, Theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                Ui.Stretch(v.rectTransform);
                if (done) { var ck = Ui.Img(card.transform, "Check", UiSprites.Check(), Theme.Good); Ui.Place(ck.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(54, 54), new Vector2(0.5f, 0)); }
            }
            var claim = Ui.Button(m.content, "daily.claim", () =>
            {
                int r = quests.ClaimDaily();
                if (r > 0) { AudioManager.Ensure().Play("cash"); Toast.Show(Loc.F("shop.gotcoins", r)); profileBar.Refresh(); BuildDailyContent(m, quests); }
            }, avail ? Theme.Good * 0.9f : new Color(0.3f, 0.3f, 0.3f, 0.8f), 32);
            claim.button.interactable = avail;
            Ui.Place(claim.image.rectTransform, new Vector2(1, 1), new Vector2(0, -2), new Vector2(380, 60), new Vector2(1, 1));

            var qh = Ui.Loc(m.content, "daily.quests", 36, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            Ui.Place(qh.rectTransform, new Vector2(0, 1), new Vector2(0, -270), new Vector2(900, 50), new Vector2(0, 1));
            var list = Ui.VBox(m.content, 12, TextAnchor.UpperLeft, true, false);
            Ui.Place((RectTransform)list.transform, new Vector2(0, 1), new Vector2(0, -330), new Vector2(1500, 420), new Vector2(0, 1));
            foreach (var q in quests.today)
            {
                var qq = q;
                var row = Ui.Panel(list.transform, "Quest", Theme.PanelLight, 16);
                Ui.Size(row.transform, -1, 124);
                var p = quests.ProgressOf(q.id);
                var txt = Ui.Label(row.transform, QuestService.Describe(q), 32, Theme.Text, TextAnchor.UpperLeft, FontStyle.Bold);
                Ui.Place(txt.rectTransform, new Vector2(0, 1), new Vector2(24, -12), new Vector2(900, 46), new Vector2(0, 1));
                Image fill;
                var bar = Ui.Bar(row.transform, p.claimed ? Theme.Good : Theme.Accent2, new Color(1, 1, 1, 0.14f), out fill);
                Ui.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(24, 20), new Vector2(880, 22), new Vector2(0, 0));
                Ui.SetBar(fill, p.progress / Mathf.Max(0.01f, q.target));
                var prog = Ui.Label(row.transform, Mathf.Min(p.progress, q.target).ToString(q.metric == QuestMetric.DistanceKm ? "0.0" : "0") + " / " + q.target.ToString("0"), 24, Theme.Dim, TextAnchor.MiddleLeft);
                Ui.Place(prog.rectTransform, new Vector2(0, 0), new Vector2(920, 10), new Vector2(220, 40), new Vector2(0, 0));
                var rew = Ui.Label(row.transform, "+" + Ui.Money(q.reward), 34, Theme.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
                Ui.Place(rew.rectTransform, new Vector2(1, 0.5f), new Vector2(-330, 0), new Vector2(220, 60), new Vector2(1, 0.5f));
                bool can = quests.IsComplete(q) && !p.claimed;
                var cb = Ui.Button(row.transform, p.claimed ? "daily.claimed" : "daily.claimquest", () =>
                {
                    if (quests.Claim(qq)) { AudioManager.Ensure().Play("cash"); profileBar.Refresh(); BuildDailyContent(m, quests); }
                }, can ? Theme.Good * 0.9f : new Color(0.3f, 0.3f, 0.34f, 0.9f), 26);
                cb.button.interactable = can;
                Ui.Place(cb.image.rectTransform, new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(280, 76), new Vector2(1, 0.5f));
            }
        }

        private void OpenAwards()
        {
            var ach = Services.Get<AchievementService>();
            ach.Check();
            var m = UiWidgets.CreateModal(canvas.transform, "awards.title", new Vector2(1400, 900), () => profileBar.Refresh());
            RectTransform content;
            var sr = Ui.Scroll(m.content, true, out content, 10f);
            Ui.Stretch((RectTransform)sr.transform);
            foreach (var a in AchievementService.All)
            {
                bool un = ach.IsUnlocked(a);
                var row = Ui.Panel(content, "Ach", un ? new Color(0.14f, 0.26f, 0.18f, 0.95f) : Theme.PanelLight, 16);
                Ui.Size(row.transform, -1, 118);
                var star = Ui.Img(row.transform, "Star", UiSprites.Star(), un ? Theme.Gold : new Color(1, 1, 1, 0.2f));
                Ui.Place(star.rectTransform, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(70, 70), new Vector2(0, 0.5f));
                var n = Ui.Label(row.transform, Loc.IsRu ? a.ruName : a.enName, 34, Theme.Text, TextAnchor.UpperLeft, FontStyle.Bold);
                Ui.Place(n.rectTransform, new Vector2(0, 1), new Vector2(110, -10), new Vector2(800, 44), new Vector2(0, 1));
                var d = Ui.Label(row.transform, Loc.IsRu ? a.ruDesc : a.enDesc, 24, Theme.Dim, TextAnchor.UpperLeft);
                Ui.Place(d.rectTransform, new Vector2(0, 1), new Vector2(110, -52), new Vector2(800, 34), new Vector2(0, 1));
                Image fill;
                var bar = Ui.Bar(row.transform, un ? Theme.Good : Theme.Accent2, new Color(1, 1, 1, 0.14f), out fill);
                Ui.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(110, 14), new Vector2(780, 14), new Vector2(0, 0));
                Ui.SetBar(fill, ach.Value(a) / a.target);
                var r = Ui.Label(row.transform, "+" + Ui.Money(a.reward), 32, Theme.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
                Ui.Place(r.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(240, 60), new Vector2(1, 0.5f));
            }
        }

        // ------------------------------------------------------------------ mode / track picker
        private GameMode pickMode = GameMode.DriftTrack;
        private UiWidgets.Modal modeModal;
        private RectTransform trackList;
        private Difficulty pickDiff = Difficulty.Normal;

        private void OpenModes()
        {
            modeModal = UiWidgets.CreateModal(canvas.transform, "modes.title", new Vector2(1750, 930));
            var left = Ui.VBox(modeModal.content, 14, TextAnchor.UpperLeft, true, false);
            Ui.Anchor((RectTransform)left.transform, Vector2.zero, new Vector2(0.34f, 1), Vector2.zero, Vector2.zero);
            string[] keys = { "mode.free", "mode.drift", "mode.ta", "mode.battle" };
            string[] desc = { "mode.free.d", "mode.drift.d", "mode.ta.d", "mode.battle.d" };
            GameMode[] modes = { GameMode.FreeRoam, GameMode.DriftTrack, GameMode.TimeAttack, GameMode.DriftBattle };
            var buttons = new List<Image>();
            for (int i = 0; i < 4; i++)
            {
                var gm = modes[i];
                var card = Ui.Button(left.transform, "", null, pickMode == gm ? Theme.Accent : Theme.PanelLight, 30, false, 18);
                Ui.Size(card.image.transform, -1, 190);
                buttons.Add(card.image);
                var t = Ui.Loc(card.image.transform, keys[i], 40, Theme.Text, TextAnchor.UpperLeft, FontStyle.Bold, true);
                Ui.Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(26, 0), new Vector2(-16, -16));
                var d = Ui.Loc(card.image.transform, desc[i], 24, new Color(1, 1, 1, 0.78f), TextAnchor.LowerLeft);
                Ui.Anchor(d.rectTransform, Vector2.zero, Vector2.one, new Vector2(26, 14), new Vector2(-16, -70));
                int idx = i;
                card.button.onClick.AddListener(() =>
                {
                    pickMode = gm;
                    for (int k = 0; k < buttons.Count; k++) buttons[k].color = modes[k] == pickMode ? Theme.Accent : Theme.PanelLight;
                    BuildTrackList();
                });
            }
            var right = Ui.Rect("Right", modeModal.content);
            Ui.Anchor(right, new Vector2(0.36f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            trackList = right;
            BuildTrackList();
        }

        private void BuildTrackList()
        {
            for (int i = trackList.childCount - 1; i >= 0; i--) Destroy(trackList.GetChild(i).gameObject);
            GameCatalog.EnsureLoaded();

            if (pickMode == GameMode.FreeRoam)
            {
                var t = Ui.Loc(trackList, "free.setup", 36, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, true);
                Ui.Place(t.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(900, 50), new Vector2(0, 1));
                var col = Ui.VBox(trackList, 16, TextAnchor.UpperLeft, true, false);
                Ui.Place((RectTransform)col.transform, new Vector2(0, 1), new Vector2(0, -70), new Vector2(1000, 500), new Vector2(0, 1));
                var l1 = Ui.Loc(col.transform, "free.weather", 28, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l1.transform, -1, 40);
                var rw = Ui.Rect("w", col.transform); Ui.Size(rw, -1, 80);
                SettingsPanel.Segmented(rw, new[] { "weather.clear", "weather.rain", "weather.fog" }, true, GameSession.FreeWeather, i => GameSession.FreeWeather = i, 220f);
                var l2 = Ui.Loc(col.transform, "free.time", 28, Theme.Dim, TextAnchor.MiddleLeft); Ui.Size(l2.transform, -1, 40);
                var rt = Ui.Rect("t", col.transform); Ui.Size(rt, -1, 80);
                int sel = GameSession.FreeHour < 10 ? 0 : (GameSession.FreeHour < 16 ? 1 : (GameSession.FreeHour < 20 ? 2 : 3));
                SettingsPanel.Segmented(rt, new[] { "time.morning", "time.day", "time.evening", "time.night" }, true, sel,
                    i => GameSession.FreeHour = new[] { 7.5f, 13f, 18.5f, 23f }[i], 190f);
                var go = Ui.Button(trackList, "common.start", () => StartGame(GameMode.FreeRoam, null), Theme.Accent, 44);
                Ui.Place(go.image.rectTransform, new Vector2(1, 0), new Vector2(0, 0), new Vector2(420, 110), new Vector2(1, 0));
                return;
            }

            float topOffset = 0f;
            if (pickMode == GameMode.DriftBattle)
            {
                var dl = Ui.Loc(trackList, "battle.difficulty", 30, Theme.Dim, TextAnchor.MiddleLeft);
                Ui.Place(dl.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(400, 40), new Vector2(0, 1));
                var dr = Ui.Rect("d", trackList);
                Ui.Place(dr, new Vector2(0, 1), new Vector2(0, -44), new Vector2(1100, 76), new Vector2(0, 1));
                SettingsPanel.Segmented(dr, new[] { "diff.easy", "diff.normal", "diff.hard", "diff.pro" }, true, (int)pickDiff, i => pickDiff = (Difficulty)i, 250f);
                topOffset = 140f;
            }
            RectTransform content;
            var sr = Ui.Scroll(trackList, true, out content, 12f);
            Ui.Anchor((RectTransform)sr.transform, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -topOffset));
            var tracks = GameCatalog.GetTracks(pickMode);
            foreach (var tr in tracks)
            {
                var track = tr;
                bool locked = profile.Level < tr.requiredLevel;
                var row = Ui.Panel(content, "Track", Theme.PanelLight, 18);
                Ui.Size(row.transform, -1, 170);
                var n = Ui.Label(row.transform, GameCatalog.TrackName(tr), 38, locked ? Theme.Dim : Theme.Text, TextAnchor.UpperLeft, FontStyle.Bold);
                Ui.Place(n.rectTransform, new Vector2(0, 1), new Vector2(26, -16), new Vector2(760, 50), new Vector2(0, 1));
                string best = "";
                float b = profile.Data.GetBest(tr.id);
                if (b > 0f) best = Loc.T("common.best") + ": " + (tr.mode == GameMode.TimeAttack ? Ui.FormatTime(b) : Ui.Money(Mathf.RoundToInt(b)));
                var info = Ui.Label(row.transform, locked ? Loc.F("common.reqlevel", tr.requiredLevel) : (best + "   " + TrackInfo(tr)), 26, locked ? Theme.Bad : Theme.Dim, TextAnchor.UpperLeft);
                Ui.Place(info.rectTransform, new Vector2(0, 1), new Vector2(26, -70), new Vector2(820, 80), new Vector2(0, 1));
                var go = Ui.Button(row.transform, locked ? "common.locked" : "common.start", () => { if (!locked) StartGame(track.mode, track); }, locked ? new Color(0.3f, 0.3f, 0.34f, 0.9f) : Theme.Accent, 34);
                go.button.interactable = !locked;
                Ui.Place(go.image.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(300, 100), new Vector2(1, 0.5f));
            }
        }

        private string TrackInfo(TrackDefinition t)
        {
            string w = t.weather == 1 ? Loc.T("weather.rain") : (t.weather == 2 ? Loc.T("weather.fog") : Loc.T("weather.clear"));
            string time = Mathf.FloorToInt(t.startTime).ToString("00") + ":00";
            string medals = "";
            if (t.mode == GameMode.DriftTrack) medals = Ui.Money((long)t.medalScores[0]) + " / " + Ui.Money((long)t.medalScores[1]) + " / " + Ui.Money((long)t.medalScores[2]);
            else if (t.mode == GameMode.TimeAttack) medals = Loc.F("ta.laps", t.laps);
            else medals = Loc.F("battle.duration", Mathf.RoundToInt(t.timeLimit));
            return medals + "  •  " + time + "  •  " + w;
        }

        private void StartGame(GameMode mode, TrackDefinition track)
        {
            GameSession.Mode = mode;
            GameSession.TrackId = track != null ? track.id : "";
            GameSession.Difficulty = pickDiff;
            SceneLoader.Load(SceneNames.GameWorld);
        }
    }
}
