using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaucasusDrive
{
    /// <summary>
    /// Игровой интерфейс: спидометр, передача, тахометр и бензин, деньги, мини-карта, задание, стрелка
    /// навигации, панель дрифта, всплывающие сообщения и сенсорное управление
    /// (руль/стрелки/наклон, газ, тормоз, ручник, R·N·D или механика, гудок, поворотники, фары, камера).
    /// </summary>
    public partial class HUD
    {
        public const int Good = 1, Bad = 2;
        readonly App app;
        public readonly Canvas canvas;
        readonly RectTransform root, controls;
        Text speed, gear, money, missionTitle, missionText, driftPts, driftMult, driftTag, fpsText, driftBest;
        Image rpmFill, fuelFill, missionBar, navArrow, mapImg, mapDot;
        RectTransform toasts, drift, mission, nav, map;
        public HoldButton gas, brake, handbrake, horn, left, right;
        public SteeringWheel wheel;
        RectTransform wheelRoot, arrowsRoot, autoBox, manualBox;
        readonly Image[] selBtns = new Image[3];
        readonly Text[] selTxt = new Text[3];
        Image lightsBtn;
        float driftTagT;
        Texture2D mapTex;

        public HUD(App a)
        {
            app = a;
            canvas = UIKit.MakeCanvas("HUD", 10);
            root = UIKit.Fill(canvas.transform, "Root");
            BuildTop();
            BuildDash();
            BuildMap();
            BuildMission();
            controls = UIKit.Fill(root, "Controls");
            BuildControls();
            BuildExtra();
            Show(false);
        }

        public void Show(bool on) { canvas.gameObject.SetActive(on); }

        // ------------------------------------------------------------------ верх
        void BuildTop()
        {
            var tr = UIKit.Rect(root, "TopRight", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(420, 120), new Vector2(1, 1));
            var mb = UIKit.Panel(tr, "Money", new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(200, 48), UIKit.Bg);
            mb.rectTransform.pivot = new Vector2(1, 1); mb.rectTransform.anchoredPosition = Vector2.zero;
            money = UIKit.Label(mb.transform, "0 ₽", 28, UIKit.Gold);
            fpsText = UIKit.LabelAt(tr, "", 18, new Color(0.5f, 0.9f, 0.5f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-55, -205), new Vector2(110, 26), TextAnchor.MiddleRight, false); // под кнопками, не налезает на деньги
            string[] icons = { "pause", "camera", "lights" };
            System.Action[] acts = { () => app.Pause(), () => app.NextCamera(), () => { app.player.CycleLights(); } };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = UIKit.RoundButton(tr, icons[i], null, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32 - i * 66, -88), new Vector2(56, 56), acts[k]);
                if (i == 2) lightsBtn = (Image)b.targetGraphic;
            }
        }

        // ------------------------------------------------------------------ спидометр
        void BuildDash()
        {
            var d = UIKit.Panel(root, "Dash", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(250, 104), UIKit.Bg);
            d.rectTransform.pivot = new Vector2(0.5f, 0);
            UIKit.IconAt(d.transform, "speedo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(30, 30), new Color(1, 1, 1, 0.75f));
            speed = UIKit.LabelAt(d.transform, "0", 56, Color.white, new Vector2(0, 0), new Vector2(0, 1), new Vector2(80, 8), new Vector2(150, 0));
            UIKit.LabelAt(d.transform, "км/ч", 16, UIKit.Muted, new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 18), new Vector2(150, 20), TextAnchor.MiddleCenter, false);
            gear = UIKit.LabelAt(d.transform, "D1", 36, UIKit.Gold, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-50, 10), new Vector2(90, 0));
            var rpm = UIKit.Panel(d.transform, "Rpm", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(-24, 6), new Color(1, 1, 1, 0.12f));
            rpmFill = UIKit.Img(UIKit.Fill(rpm.transform, "Fill"), UIKit.Accent, false);
            rpmFill.type = Image.Type.Filled; rpmFill.fillMethod = Image.FillMethod.Horizontal; rpmFill.sprite = UIKit.Rounded;
            var fuel = UIKit.Panel(d.transform, "Fuel", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 28), new Vector2(70, 8), new Color(1, 1, 1, 0.12f));
            fuelFill = UIKit.Img(UIKit.Fill(fuel.transform, "Fill"), UIKit.Green, false);
            fuelFill.type = Image.Type.Filled; fuelFill.fillMethod = Image.FillMethod.Horizontal; fuelFill.sprite = UIKit.Rounded;
        }

        // ------------------------------------------------------------------ мини-карта
        void BuildMap()
        {
            map = UIKit.Rect(root, "Map", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(170, 170), new Vector2(0, 1));
            var bg = map.gameObject.AddComponent<Image>(); bg.sprite = UIKit.Circle; bg.color = new Color(0, 0, 0, 0.55f);
            map.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var inner = UIKit.Rect(map, "MapImg", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 1200));
            mapImg = inner.gameObject.AddComponent<Image>();
            mapTex = MakeMapTexture();
            mapImg.sprite = Sprite.Create(mapTex, new Rect(0, 0, mapTex.width, mapTex.height), new Vector2(0.5f, 0.5f));
            mapImg.color = new Color(1, 1, 1, 0.9f);
            var dot = UIKit.Rect(map, "Me", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            mapDot = dot.gameObject.AddComponent<Image>(); mapDot.sprite = UIKit.Circle; mapDot.color = UIKit.Accent;
            // индикаторы поворотов и аварийка под картой
            string[] ic = { "left", "hazard", "right" };
            System.Action[] acts = { () => app.player.ToggleIndicator('L'), () => app.player.ToggleHazard(), () => app.player.ToggleIndicator('R') };
            for (int i = 0; i < 3; i++)
                UIKit.RoundButton(root, ic[i], null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(46 + i * 64, -222), new Vector2(56, 56), acts[i], 22, null, UIKit.Ink);
        }

        /// <summary>Карта города 512×512 (1 px ≈ 1.17 м): дороги, кварталы, АЗС, автодром.</summary>
        Texture2D MakeMapTexture()
        {
            int s = 512; float world = 1400f;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float wx = (x / (float)s - 0.5f) * world - 140f, wz = (y / (float)s - 0.5f) * world;
                    var su = app.city.SurfaceAt(wx, wz);
                    Color32 c = su.type == 0 ? new Color32(70, 72, 78, 255) : su.type == 1 ? new Color32(120, 118, 112, 255) : new Color32(48, 82, 44, 255);
                    if (Mathf.Abs(wx) > CityC.Extent + 1 && su.type == 2) c = new Color32(40, 66, 38, 255);
                    if (app.city.azs != Vector3.zero && Vector2.Distance(new Vector2(wx, wz), new Vector2(app.city.azs.x, app.city.azs.z)) < 9f) c = new Color32(255, 200, 40, 255);
                    px[y * s + x] = c;
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ задание, дрифт, сообщения
        void BuildMission()
        {
            mission = UIKit.Rect(root, "Mission", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(620, 70), new Vector2(0.5f, 1));
            UIKit.Img(mission, UIKit.Bg);
            missionTitle = UIKit.LabelAt(mission, "", 20, UIKit.Gold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -16), new Vector2(-20, 26));
            missionText = UIKit.LabelAt(mission, "", 17, Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 24), new Vector2(-20, 30), TextAnchor.MiddleCenter, false);
            var bar = UIKit.Panel(mission, "Bar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 5), new Vector2(-30, 5), new Color(1, 1, 1, 0.12f));
            missionBar = UIKit.Img(UIKit.Fill(bar.transform, "Fill"), UIKit.Green, false);
            missionBar.type = Image.Type.Filled; missionBar.fillMethod = Image.FillMethod.Horizontal; missionBar.sprite = UIKit.Rounded;
            mission.gameObject.SetActive(false);

            nav = UIKit.Rect(root, "Nav", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(60, 60));
            navArrow = nav.gameObject.AddComponent<Image>();
            navArrow.sprite = ArrowSprite(); navArrow.color = UIKit.Gold;
            nav.gameObject.SetActive(false);

            drift = UIKit.Rect(root, "Drift", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(330, 84), new Vector2(0.5f, 1));
            UIKit.Img(drift, UIKit.Bg);
            driftPts = UIKit.LabelAt(drift, "0", 40, Color.white, new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(0, -28), new Vector2(0, 48));
            driftMult = UIKit.LabelAt(drift, "×1", 34, UIKit.Accent, new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -28), new Vector2(0, 48));
            driftBest = UIKit.LabelAt(drift, "", 15, UIKit.Muted, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 12), new Vector2(0, 22), TextAnchor.MiddleCenter, false);
            drift.gameObject.SetActive(false);
            driftTag = UIKit.LabelAt(root, "", 30, UIKit.Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(700, 40));
            var sh = driftTag.gameObject.AddComponent<Shadow>(); sh.effectDistance = new Vector2(2, -2);

            toasts = UIKit.Rect(root, "Toasts", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(760, 200), new Vector2(0.5f, 1));
            var vl = toasts.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperCenter; vl.spacing = 6; vl.childControlHeight = false; vl.childControlWidth = false; vl.childForceExpandHeight = false;
        }

        static Sprite arrow;
        static Sprite ArrowSprite()
        {
            if (arrow) return arrow;
            int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = (x - 31.5f) / 32f, fy = (y - 31.5f) / 32f;
                    bool head = fy > -0.1f && Mathf.Abs(fx) < (0.9f - fy) * 0.62f && fy < 0.9f;
                    bool tail = fy <= -0.1f && fy > -0.85f && Mathf.Abs(fx) < 0.22f;
                    px[y * s + x] = head || tail ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            t.SetPixels32(px); t.Apply();
            arrow = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            return arrow;
        }

        public void Toast(string text, int kind = 0, float seconds = 2.2f)
        {
            while (toasts.childCount >= 3) Object.DestroyImmediate(toasts.GetChild(0).gameObject);
            var rt = UIKit.Rect(toasts, "Toast", new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(740, 48));
            UIKit.Img(rt, kind == Good ? new Color(0.22f, 0.22f, 0.22f, 0.94f) : kind == Bad ? UIKit.Light : UIKit.Bg);
            UIKit.Label(rt, text, 20, kind == Bad ? UIKit.Ink : Color.white);
            Object.Destroy(rt.gameObject, seconds);
        }

        public void ClearToasts() { UIKit.Clear(toasts); }

        public void Mission(string title, string text, float progress)
        {
            bool on = title != null;
            if (mission.gameObject.activeSelf != on) mission.gameObject.SetActive(on);
            if (!on) return;
            missionTitle.text = title; missionText.text = text;
            missionBar.fillAmount = Mathf.Clamp01(progress);
        }

        Vector3? navTarget;
        public void Nav(Vector3 target) { navTarget = target; }

        public void Drift(bool show, int pts, float mult, float angle, int best = 0)
        {
            bool vis = show || driftTagT > 0f;
            if (drift.gameObject.activeSelf != vis) drift.gameObject.SetActive(vis);
            if (!vis) return;
            driftPts.text = show ? pts.ToString("#,0").Replace(",", " ") : "";
            driftMult.text = show ? "×" + mult.ToString("0.#") : "";
            driftBest.text = best > 0 ? "рекорд " + best.ToString("#,0").Replace(",", " ") : "";
        }

        public void DriftTag(string text, float sec) { driftTag.text = text; driftTagT = sec; }

        // ------------------------------------------------------------------ управление
        void BuildControls()
        {
            // руль
            wheelRoot = UIKit.Rect(controls, "Wheel", new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 165), new Vector2(270, 270));
            var wimg = wheelRoot.gameObject.AddComponent<Image>(); wimg.sprite = UIKit.Circle; wimg.color = new Color(0, 0, 0, 0.001f);
            var vis = UIKit.Rect(wheelRoot, "Rim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 240));
            var rim = vis.gameObject.AddComponent<Image>(); rim.sprite = UIKit.Circle; rim.color = new Color(0.96f, 0.96f, 0.95f, 0.38f); rim.raycastTarget = false;
            UIKit.IconAt(vis, "wheel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(214, 214), new Color(0.08f, 0.08f, 0.1f, 0.85f));
            var mark = UIKit.Rect(vis, "Top", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(12, 22));
            var mi = mark.gameObject.AddComponent<Image>(); mi.color = UIKit.Accent; mi.raycastTarget = false;
            wheel = wheelRoot.gameObject.AddComponent<SteeringWheel>(); wheel.visual = vis;
            // стрелки
            arrowsRoot = UIKit.Rect(controls, "Arrows", new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 110), new Vector2(300, 140));
            left = Hold(arrowsRoot, "<", new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(128, 128), UIKit.Light, "left", true);
            right = Hold(arrowsRoot, ">", new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(128, 128), UIKit.Light, "right", true);

            gas = Hold(controls, "ГАЗ", new Vector2(1, 0), new Vector2(-80, 130), new Vector2(112, 230), UIKit.Light, "pedal", false, UIKit.Ink);
            brake = Hold(controls, "ТОРМОЗ", new Vector2(1, 0), new Vector2(-206, 95), new Vector2(120, 160), UIKit.Ink, "pedal", false, Color.white);
            handbrake = Hold(controls, "РУЧНИК", new Vector2(1, 0), new Vector2(-206, 232), new Vector2(84, 84), UIKit.Light, "brake", true, UIKit.Ink);
            horn = Hold(controls, "БИП", new Vector2(1, 0), new Vector2(-80, 296), new Vector2(76, 76), UIKit.Light, null, true, UIKit.Ink);
            horn.onDown = null;

            // автомат: R N D
            autoBox = UIKit.Rect(controls, "Selector", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-322, 140), new Vector2(84, 250));
            UIKit.Img(autoBox, UIKit.Light);
            string[] sel = { "D", "N", "R" };
            for (int i = 0; i < 3; i++)
            {
                char c = sel[i][0];
                var b = UIKit.Button(autoBox, sel[i], new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -42 - i * 82), new Vector2(70, 72), UIKit.Ink, () => { app.player.phys.SetSelector(c); }, 34);
                selBtns[i] = b.GetComponent<Image>(); selTxt[i] = b.GetComponentInChildren<Text>();
            }
            // механика: + и −
            manualBox = UIKit.Rect(controls, "Manual", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-322, 140), new Vector2(84, 250));
            UIKit.RoundButton(manualBox, null, "+", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(84, 84), () => app.player.phys.ShiftUp(), 44);
            UIKit.RoundButton(manualBox, null, "−", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(84, 84), () => app.player.phys.ShiftDown(), 44);
        }

        HoldButton Hold(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Color? c = null, string icon = null, bool round = false, Color? ink = null)
        {
            var rt = UIKit.Rect(parent, text, anchor, anchor, pos, size);
            var img = UIKit.Img(rt, c ?? UIKit.Bg);
            var col = ink ?? (c.HasValue && c.Value.r > 0.8f ? UIKit.Ink : Color.white);
            if (round) { img.sprite = UIKit.Circle; }
            if (icon != null)
            {
                float k = Mathf.Min(size.x, size.y);
                // подпись снизу у больших педалей, у круглых — только значок
                bool cap = !round;
                UIKit.IconAt(rt, icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, cap ? 12 : 0), new Vector2(k * (cap ? 0.62f : 0.56f), k * (cap ? 0.62f : 0.56f)) * (icon == "pedal" ? 1.0f : 1f), col);
                if (cap) UIKit.LabelAt(rt, text, 15, col, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 18), new Vector2(0, 24));
            }
            else UIKit.Label(rt, text, text.Length > 4 ? 18 : 22, col);
            return rt.gameObject.AddComponent<HoldButton>();
        }

        public void ApplySettings(Settings s)
        {
            wheelRoot.gameObject.SetActive(s.controls == 0);
            arrowsRoot.gameObject.SetActive(s.controls == 1);
            autoBox.gameObject.SetActive(!s.manual);
            manualBox.gameObject.SetActive(s.manual);
        }

        /// <summary>Ввод с сенсорного экрана + клавиатуры (WASD/стрелки, пробел — ручник, H — гудок).</summary>
        public CarInput ReadInput(Settings s)
        {
            var i = new CarInput();
            if (app.onFoot) { i.brake = 1f; i.handbrake = true; return i; }
            float steer = s.controls == 0 ? wheel.value : s.controls == 1 ? (right.held ? 1f : 0f) - (left.held ? 1f : 0f) : Mathf.Clamp(In.Acceleration.x * 2.2f, -1f, 1f);
            float kb = (In.Held(In.K.D) || In.Held(In.K.Right) ? 1f : 0f) - (In.Held(In.K.A) || In.Held(In.K.Left) ? 1f : 0f);
            if (kb != 0f) steer = kb;
            i.steer = steer;
            i.throttle = gas.held || In.Held(In.K.W) || In.Held(In.K.Up) ? 1f : 0f;
            i.brake = brake.held || In.Held(In.K.S) || In.Held(In.K.Down) ? 1f : 0f;
            i.handbrake = handbrake.held || In.Held(In.K.Space);
            // автомат: тормоз на месте — включить задний ход, газ на R — вперёд (как в Car Parking)
            return i;
        }

        public bool Horn => !app.onFoot && (horn.held || In.Held(In.K.H));

        // ------------------------------------------------------------------ кадр
        public void Update(float dt)
        {
            var p = app.player.phys;
            speed.text = Mathf.RoundToInt(p.speed * 3.6f).ToString();
            gear.text = p.GearLabel;
            rpmFill.fillAmount = Mathf.Clamp01(p.rpm / p.spec.revLimit);
            rpmFill.color = p.rpm > p.spec.upshiftRpm + 300 ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            fuelFill.fillAmount = p.fuel / p.spec.tank;
            fuelFill.color = p.fuel < p.spec.tank * 0.15f ? new Color(0.4f, 0.4f, 0.4f) : UIKit.Green;
            money.text = M.Rub(app.save.Money);
            fpsText.text = app.save.d.settings.showFps ? Mathf.RoundToInt(app.quality.fps) + " FPS" : "";
            lightsBtn.color = app.player.lightsMode == 1 ? Color.white : app.player.lightsMode == 2 ? new Color(0.5f, 0.5f, 0.5f, 0.9f) : UIKit.Light;
            if (!p.manual)
                for (int i = 0; i < 3; i++) { bool on = "DNR"[i] == p.selector; selBtns[i].color = on ? UIKit.Ink : new Color(1, 1, 1, 0.001f); selTxt[i].color = on ? Color.white : UIKit.Ink; }

            // карта: вращается так, что курс машины — вверх
            var pos = app.onFoot ? app.walker.Pos : app.player.Position;
            float headingMap = app.onFoot ? app.cameraRig.footYaw : app.player.Heading;
            float world = 1400f, k = 1200f / world;
            var mrt = mapImg.rectTransform;
            mrt.localRotation = Quaternion.Euler(0, 0, headingMap * Mathf.Rad2Deg);
            var off = new Vector2(-(pos.x + 140f) * k, -pos.z * k);
            mrt.anchoredPosition = (Vector2)(Quaternion.Euler(0, 0, headingMap * Mathf.Rad2Deg) * off);

            if (navTarget.HasValue)
            {
                var d = navTarget.Value - pos;
                float ang = Mathf.Atan2(d.x, d.z) - headingMap;
                nav.gameObject.SetActive(d.magnitude > 4f);
                nav.localRotation = Quaternion.Euler(0, 0, -ang * Mathf.Rad2Deg);
                navTarget = null;
            }
            else if (nav.gameObject.activeSelf) nav.gameObject.SetActive(false);

            if (driftTagT > 0f) { driftTagT -= dt; if (driftTagT <= 0f) driftTag.text = ""; }
            UpdateExtra(dt);
        }
    }
}
