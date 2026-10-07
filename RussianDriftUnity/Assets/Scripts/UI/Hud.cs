using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Core;
using RussianDrift.Vehicle;
using RussianDrift.World;

namespace RussianDrift.UI
{
    /// <summary>Text lines a game mode wants displayed in the top-left panel.</summary>
    public class ModeHud
    {
        public string title = "";
        public string timer = "";
        public string line1 = "";
        public string line2 = "";
        public bool wrongWay;
        public bool visible = true;
        public float bar = -1f;          // 0..1, <0 hides
        public Color barColor = new Color(0.1f, 0.78f, 1f);
        public string rightScore = "";   // battle: rival score
    }

    /// <summary>In-race HUD: speedometer + tachometer, drift score, mode info, mini-map, toasts, countdown and driving controls.</summary>
    public class Hud : MonoBehaviour
    {
        public Canvas canvas;
        public RectTransform safe;
        public TouchControls controls;
        public ModeHud mode = new ModeHud();

        private VehicleController vc;
        private DriftScorer scorer;
        private GameSettings settings;

        private Image rpmRing, rpmBack, boostFill;
        private Text speedText, unitText, gearText;
        private CanvasGroup driftGroup;
        private Text scoreText, multText, angleText, totalText, bankText;
        private float bankT;
        private float shownScore;
        private RectTransform modePanel;
        private Text modeTitle, modeTimer, modeLine1, modeLine2, rivalScore;
        private Image modeBar;
        private Text wrongWay, countdown;
        private RawImage mapImage;
        private RectTransform mapRoot;
        private Image tiltBar;
        private readonly List<Text> feed = new List<Text>();
        private readonly List<float> feedT = new List<float>();
        private RectTransform feedRoot;
        private Image redZone;
        private float countT;

        public static Hud Create(VehicleController car, DriftScorer sc, RenderTexture minimap, System.Action onPause, System.Action onCamera, System.Action onRespawn, System.Action onEditDone)
        {
            var go = new GameObject("HUD");
            var h = go.AddComponent<Hud>();
            h.Build(car, sc, minimap, onPause, onCamera, onRespawn, onEditDone);
            return h;
        }

        private void Build(VehicleController car, DriftScorer sc, RenderTexture minimap, System.Action onPause, System.Action onCamera, System.Action onRespawn, System.Action onEditDone)
        {
            vc = car; scorer = sc;
            settings = Services.Get<SaveSystem>().Data.settings;
            canvas = Ui.CreateCanvas("HudCanvas", 5, transform);
            safe = Ui.SafeArea(canvas.transform);

            BuildGauge();
            BuildDriftPanel();
            BuildModePanel();
            BuildMiniMap(minimap);
            BuildButtons(onPause, onCamera, onRespawn);
            BuildFeed();

            wrongWay = Ui.Loc(safe, "hud.wrongway", 80, Theme.Bad, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            Ui.Place(wrongWay.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1200, 120));
            wrongWay.gameObject.SetActive(false);
            countdown = Ui.Label(safe, "", 260, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(countdown.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(1000, 320));
            countdown.gameObject.SetActive(false);

            controls = new TouchControls();
            controls.Build(safe, settings, onEditDone);

            GameEvents.DriftBanked += OnBanked;
            GameEvents.DriftPenalty += OnPenalty;
            GameEvents.NearMiss += OnNear;
            GameEvents.LevelUp += OnLevel;
            GameEvents.AchievementUnlocked += OnAchievement;
            GameEvents.SettingsChanged += OnSettings;
            OnSettings();
        }

        private void OnDestroy()
        {
            GameEvents.DriftBanked -= OnBanked;
            GameEvents.DriftPenalty -= OnPenalty;
            GameEvents.NearMiss -= OnNear;
            GameEvents.LevelUp -= OnLevel;
            GameEvents.AchievementUnlocked -= OnAchievement;
            GameEvents.SettingsChanged -= OnSettings;
        }

        private void OnSettings()
        {
            if (controls != null) controls.ApplySettings();
            if (mapRoot != null) mapRoot.gameObject.SetActive(settings.showMinimap);
            if (tiltBar != null) tiltBar.transform.parent.gameObject.SetActive(settings.controlScheme == 1);
        }

        // ------------------------------------------------------------------ construction
        private void BuildGauge()
        {
            var root = Ui.Rect("Gauge", safe);
            Ui.Place(root, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(380, 380), new Vector2(0.5f, 0));
            rpmBack = Ui.Img(root, "Back", UiSprites.Ring(0.84f), new Color(0, 0, 0, 0.5f));
            Ui.Stretch(rpmBack.rectTransform);
            rpmRing = Ui.Img(root, "Fill", UiSprites.Ring(0.86f), Color.white);
            Ui.Stretch(rpmRing.rectTransform, 6, 6, 6, 6);
            rpmRing.type = Image.Type.Filled; rpmRing.fillMethod = Image.FillMethod.Radial360; rpmRing.fillOrigin = (int)Image.Origin360.Bottom; rpmRing.fillClockwise = true;
            var core = Ui.Img(root, "Core", UiSprites.Circle(), new Color(0.03f, 0.04f, 0.06f, 0.55f));
            Ui.Stretch(core.rectTransform, 38, 38, 38, 38);
            speedText = Ui.Label(root, "0", 108, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(speedText.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(300, 130));
            unitText = Ui.Label(root, "km/h", 30, Theme.Dim, TextAnchor.MiddleCenter);
            Ui.Place(unitText.rectTransform, new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(200, 40));
            gearText = Ui.Label(root, "N", 64, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Place(gearText.rectTransform, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(120, 80));
            // boost bar
            var bb = Ui.Bar(root, Theme.Accent2, new Color(1, 1, 1, 0.14f), out boostFill);
            Ui.Place(bb.rectTransform, new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(170, 10));
            // tilt indicator
            var tbBg = Ui.Img(safe, "Tilt", UiSprites.Rounded(8), new Color(1, 1, 1, 0.14f), true);
            Ui.Place(tbBg.rectTransform, new Vector2(0, 0), new Vector2(60, 40), new Vector2(520, 18), new Vector2(0, 0));
            tiltBar = Ui.Img(tbBg.transform, "Mark", UiSprites.Rounded(8), Theme.Accent, true);
            Ui.Place(tiltBar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 34));
        }

        private void BuildDriftPanel()
        {
            var root = Ui.Rect("DriftPanel", safe);
            Ui.Place(root, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(760, 230), new Vector2(0.5f, 1));
            driftGroup = root.gameObject.AddComponent<CanvasGroup>();
            driftGroup.alpha = 0f;
            scoreText = Ui.Label(root, "0", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(scoreText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(760, 130), new Vector2(0.5f, 1));
            multText = Ui.Label(root, "x1.0", 54, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(multText.rectTransform, new Vector2(0.5f, 1), new Vector2(150, -132), new Vector2(300, 66), new Vector2(0.5f, 1));
            angleText = Ui.Label(root, "0°", 44, Theme.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Place(angleText.rectTransform, new Vector2(0.5f, 1), new Vector2(-150, -136), new Vector2(300, 60), new Vector2(0.5f, 1));
            totalText = Ui.Label(safe, "", 34, Theme.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Place(totalText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(760, 44), new Vector2(0.5f, 1));
            bankText = Ui.Label(safe, "", 76, Theme.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(bankText.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(900, 120));
            bankText.gameObject.SetActive(false);
        }

        private void BuildModePanel()
        {
            var p = Ui.Panel(safe, "ModePanel", new Color(0.04f, 0.05f, 0.08f, 0.62f), 20);
            modePanel = p.rectTransform;
            Ui.Place(modePanel, new Vector2(0, 1), new Vector2(24, -24), new Vector2(520, 250), new Vector2(0, 1));
            modeTitle = Ui.Label(p.transform, "", 28, Theme.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Place(modeTitle.rectTransform, new Vector2(0, 1), new Vector2(22, -12), new Vector2(480, 36), new Vector2(0, 1));
            modeTimer = Ui.Label(p.transform, "", 74, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(modeTimer.rectTransform, new Vector2(0, 1), new Vector2(22, -46), new Vector2(480, 90), new Vector2(0, 1));
            modeLine1 = Ui.Label(p.transform, "", 30, Theme.Text, TextAnchor.MiddleLeft);
            Ui.Place(modeLine1.rectTransform, new Vector2(0, 1), new Vector2(22, -140), new Vector2(480, 40), new Vector2(0, 1));
            modeLine2 = Ui.Label(p.transform, "", 26, Theme.Dim, TextAnchor.MiddleLeft);
            Ui.Place(modeLine2.rectTransform, new Vector2(0, 1), new Vector2(22, -182), new Vector2(480, 36), new Vector2(0, 1));
            Image fill;
            var bar = Ui.Bar(p.transform, Theme.Accent2, new Color(1, 1, 1, 0.14f), out fill);
            Ui.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(22, 14), new Vector2(476, 14), new Vector2(0, 0));
            modeBar = fill;
            rivalScore = Ui.Label(p.transform, "", 34, Theme.Bad, TextAnchor.MiddleRight, FontStyle.Bold);
            Ui.Place(rivalScore.rectTransform, new Vector2(1, 1), new Vector2(-18, -150), new Vector2(240, 40), new Vector2(1, 1));
        }

        private void BuildMiniMap(RenderTexture rt)
        {
            mapRoot = Ui.Rect("MiniMap", safe);
            Ui.Place(mapRoot, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(270, 270), new Vector2(1, 1));
            var ring = Ui.Img(mapRoot, "Ring", UiSprites.Circle(), new Color(0.03f, 0.04f, 0.06f, 0.85f));
            Ui.Stretch(ring.rectTransform);
            var maskGo = Ui.Rect("Mask", mapRoot);
            Ui.Stretch(maskGo, 8, 8, 8, 8);
            var mimg = maskGo.gameObject.AddComponent<Image>(); mimg.sprite = UiSprites.Circle(); mimg.color = Color.white;
            var mask = maskGo.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var rawGo = Ui.Rect("Map", maskGo);
            Ui.Stretch(rawGo);
            mapImage = rawGo.gameObject.AddComponent<RawImage>();
            mapImage.texture = rt; mapImage.color = new Color(1f, 1f, 1f, 0.95f); mapImage.raycastTarget = false;
            var arrow = Ui.Img(mapRoot, "Me", UiSprites.Arrow(0), Theme.Accent);
            Ui.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 44));
            var north = Ui.Label(mapRoot, "", 20, Theme.Dim);
        }

        private void BuildButtons(System.Action onPause, System.Action onCamera, System.Action onRespawn)
        {
            float y = -310f;
            var pause = Ui.IconButton(safe, UiSprites.Rounded(4), onPause, new Color(0.04f, 0.05f, 0.08f, 0.7f), 0.0f, 22);
            // pause glyph: two bars
            var b1 = Ui.Img(pause.image.transform, "b1", UiSprites.Rounded(4), Color.white, true); Ui.Place(b1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-12, 0), new Vector2(14, 44));
            var b2 = Ui.Img(pause.image.transform, "b2", UiSprites.Rounded(4), Color.white, true); Ui.Place(b2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(12, 0), new Vector2(14, 44));
            Ui.Place(pause.image.rectTransform, new Vector2(1, 1), new Vector2(-24, y), new Vector2(100, 100), new Vector2(1, 1));
            var cam = Ui.Button(safe, "hud.cam", onCamera, new Color(0.04f, 0.05f, 0.08f, 0.7f), 26);
            Ui.Place(cam.image.rectTransform, new Vector2(1, 1), new Vector2(-136, y), new Vector2(100, 100), new Vector2(1, 1));
            var rs = Ui.Button(safe, "hud.reset", onRespawn, new Color(0.04f, 0.05f, 0.08f, 0.7f), 24);
            Ui.Place(rs.image.rectTransform, new Vector2(1, 1), new Vector2(-248, y), new Vector2(100, 100), new Vector2(1, 1));
        }

        private void BuildFeed()
        {
            feedRoot = Ui.Rect("Feed", safe);
            Ui.Place(feedRoot, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1100, 300));
            for (int i = 0; i < 4; i++)
            {
                var t = Ui.Label(feedRoot, "", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                Ui.Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -i * 62), new Vector2(1100, 60), new Vector2(0.5f, 1));
                t.gameObject.SetActive(false);
                feed.Add(t); feedT.Add(0f);
            }
        }

        // ------------------------------------------------------------------ events
        public void Notify(string text, Color color)
        {
            // shift down, put new one on top
            for (int i = feed.Count - 1; i > 0; i--)
            {
                feed[i].text = feed[i - 1].text; feed[i].color = feed[i - 1].color; feedT[i] = feedT[i - 1];
                feed[i].gameObject.SetActive(feed[i - 1].gameObject.activeSelf);
            }
            feed[0].text = text; feed[0].color = color; feedT[0] = 2.6f; feed[0].gameObject.SetActive(true);
        }

        private void OnBanked(int pts, float mul, float dur)
        {
            bankText.text = "+" + Ui.Money(pts) + "  <size=52>x" + mul.ToString("0.0") + "</size>";
            bankText.gameObject.SetActive(true);
            bankT = 1.8f;
            if (pts > 4000) Notify(Loc.T("hud.greatdrift"), Theme.Gold);
        }

        private void OnPenalty(float lost) { Notify(Loc.F("hud.chainlost", Mathf.RoundToInt(lost)), Theme.Bad); }
        private void OnNear(float d) { Notify(Loc.T("hud.nearmiss") + " +250", Theme.Accent2); }
        private void OnLevel(int l) { Notify(Loc.F("msg.levelup", l), Theme.Gold); }
        private void OnAchievement(string id) { Notify(Loc.T("hud.achievement"), Theme.Gold); }

        public void ShowCountdown(string text)
        {
            countdown.gameObject.SetActive(!string.IsNullOrEmpty(text));
            countdown.text = text;
            countT = 0.9f;
        }

        // ------------------------------------------------------------------ per-frame
        private void Update()
        {
            if (vc == null) return;
            float dt = Time.unscaledDeltaTime;

            // gauge
            float rpm01 = Mathf.Clamp01(vc.Engine.rpm / Mathf.Max(1f, vc.Engine.redline));
            rpmRing.fillAmount = rpm01 * 0.75f;
            rpmBack.fillAmount = 1f;
            rpmRing.color = rpm01 < 0.8f ? Color.Lerp(Theme.Accent2, Color.white, rpm01 / 0.8f) : Color.Lerp(Theme.Accent, Theme.Bad, (rpm01 - 0.8f) / 0.2f);
            float spd = settings.useMph ? vc.SpeedKmh * 0.621371f : vc.SpeedKmh;
            speedText.text = Mathf.RoundToInt(spd).ToString();
            unitText.text = settings.useMph ? "mph" : "km/h";
            gearText.text = vc.Engine.gear < 0 ? "R" : (vc.Engine.gear == 0 ? "N" : vc.Engine.gear.ToString());
            Ui.SetBar(boostFill, vc.Engine.boost);
            if (tiltBar != null && tiltBar.transform.parent.gameObject.activeInHierarchy)
                tiltBar.rectTransform.anchoredPosition = new Vector2(vc.SteerInputSmoothed * 240f, 0f);

            // drift panel
            bool active = scorer != null && scorer.Active;
            float target = active ? 1f : 0f;
            driftGroup.alpha = Mathf.MoveTowards(driftGroup.alpha, target, dt * 4f);
            if (scorer != null)
            {
                shownScore = Mathf.Lerp(shownScore, scorer.ChainScore, 1f - Mathf.Exp(-12f * dt));
                scoreText.text = Ui.Money(Mathf.RoundToInt(active ? shownScore : scorer.ChainScore));
                multText.text = "x" + scorer.Multiplier.ToString("0.0");
                multText.color = Color.Lerp(Theme.Accent, Theme.Gold, Mathf.InverseLerp(1f, 8f, scorer.Multiplier));
                angleText.text = Mathf.RoundToInt(vc.AbsDriftAngle) + "°";
                scoreText.transform.localScale = Vector3.one * (1f + Mathf.Clamp01(scorer.Multiplier / 10f) * 0.18f);
                totalText.text = scorer.BankedScore > 0f ? Loc.T("hud.total") + " " + Ui.Money(Mathf.RoundToInt(scorer.BankedScore)) : "";
                if (scorer.EffectiveZoneMultiplier > 1f) totalText.text += "   <color=#FFB020>ZONE x" + scorer.EffectiveZoneMultiplier.ToString("0.#") + "</color>";
            }
            if (bankT > 0f)
            {
                bankT -= dt;
                float k = Mathf.Clamp01(bankT / 1.8f);
                bankText.color = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, Mathf.Clamp01(k * 2f));
                bankText.rectTransform.anchoredPosition = new Vector2(0f, (1f - k) * 60f);
                if (bankT <= 0f) bankText.gameObject.SetActive(false);
            }

            // mode panel
            modePanel.gameObject.SetActive(mode.visible);
            modeTitle.text = mode.title; modeTimer.text = mode.timer; modeLine1.text = mode.line1; modeLine2.text = mode.line2;
            rivalScore.text = mode.rightScore;
            modeBar.transform.parent.gameObject.SetActive(mode.bar >= 0f);
            if (mode.bar >= 0f) { Ui.SetBar(modeBar, mode.bar); modeBar.color = mode.barColor; }
            bool ww = mode.wrongWay && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
            if (wrongWay.gameObject.activeSelf != ww) wrongWay.gameObject.SetActive(ww);

            // feed
            for (int i = 0; i < feed.Count; i++)
            {
                if (!feed[i].gameObject.activeSelf) continue;
                feedT[i] -= dt;
                var c = feed[i].color; c.a = Mathf.Clamp01(feedT[i] * 2.5f); feed[i].color = c;
                if (feedT[i] <= 0f) feed[i].gameObject.SetActive(false);
            }
            if (countdown.gameObject.activeSelf && countT > 0f)
            {
                countT -= dt;
                float s = 1f + Mathf.Clamp01(countT / 0.9f) * 0.5f;
                countdown.rectTransform.localScale = Vector3.one * s;
                var c = countdown.color; c.a = Mathf.Clamp01(countT * 3f + 0.2f); countdown.color = c;
            }
        }

        public void SetVisible(bool v) { safe.gameObject.SetActive(v); }
    }
}
