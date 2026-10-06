using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CaucasusDrive
{
    /// <summary>Джойстик ходьбы: отклонение пальца от центра → −1…+1 по осям.</summary>
    public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Vector2 value;
        public RectTransform knob;
        int pointer = -100;
        void Set(PointerEventData e)
        {
            Vector2 local;
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out local);
            float r = rt.rect.width * 0.4f;
            value = Vector2.ClampMagnitude(local / r, 1f);
            if (knob) knob.anchoredPosition = value * r;
        }
        public void OnPointerDown(PointerEventData e) { if (pointer != -100) return; pointer = e.pointerId; Set(e); }
        public void OnDrag(PointerEventData e) { if (e.pointerId == pointer) Set(e); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId != pointer) return; pointer = -100; value = Vector2.zero; if (knob) knob.anchoredPosition = Vector2.zero; }
        void OnDisable() { pointer = -100; value = Vector2.zero; if (knob) knob.anchoredPosition = Vector2.zero; }
    }

    /// <summary>Свайп по свободной части экрана — поворот камеры (накапливает сдвиг за кадр).</summary>
    public class LookPad : MonoBehaviour, IDragHandler
    {
        public Vector2 delta;
        public void OnDrag(PointerEventData e) { delta += e.delta * (720f / Mathf.Max(1, Screen.height)); }
        public Vector2 Take() { var d = delta; delta = Vector2.zero; return d; }
    }

    /// <summary>Дополнения HUD из веб-версии: радио, такси, дверь, кнопка действия (АЗС), знак 60, вспышка камеры,
    /// парктроник, управление пешком (джойстик, прыжок, сесть, курить, свист, магазин), меню ларька.</summary>
    public partial class HUD
    {
        Button radioBtn, taxiBtn, doorBtn, shopBtn, carBtn;
        Text radioLabel, sensorText, shopLabel, netText;
        Image doorIcon;
        Button chatBtn;
        RectTransform chatPanel;
        Image taxiImg, flashImg, smokeImg;
        RectTransform limit, sensors, foot, shopPanel;
        public Joystick joy;
        public LookPad look;
        float flashT;
        bool limitOn, taxiVisible;
        ShopPoint shopNear;

        void BuildExtra()
        {
            // нижний ряд кнопок справа сверху
            var tr = (RectTransform)root.Find("TopRight");
            radioBtn = UIKit.RoundButton(tr, "radio", null, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -152), new Vector2(56, 56), () => app.CycleRadio());
            radioLabel = UIKit.LabelAt(tr, "", 13, Color.white, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -194), new Vector2(190, 20), TextAnchor.MiddleRight);
            radioLabel.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            doorBtn = UIKit.RoundButton(tr, "door", null, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-98, -152), new Vector2(56, 56), () => app.ToggleFoot());
            doorIcon = doorBtn.GetComponentsInChildren<Image>(true)[2];
            taxiBtn = UIKit.RoundButton(tr, "taxi", null, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-164, -152), new Vector2(56, 56), () => app.mode?.OnTaxi());
            taxiImg = (Image)taxiBtn.targetGraphic;
            taxiBtn.gameObject.SetActive(false);
            // онлайн: фразы и список игроков
            chatBtn = UIKit.RoundButton(tr, "chat", null, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-230, -152), new Vector2(56, 56), () => chatPanel.gameObject.SetActive(!chatPanel.gameObject.activeSelf));
            chatBtn.gameObject.SetActive(false);
            chatPanel = UIKit.Rect(root, "Chat", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-160, 10), new Vector2(270, 4 * 58 + 12));
            UIKit.Img(chatPanel, UIKit.Bg2);
            for (int i = 0; i < OnlineMode.Phrases.Length; i++)
            {
                int k = i;
                UIKit.Button(chatPanel, OnlineMode.Phrases[i], new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i % 2 == 0 ? -64 : 64), -35 - (i / 2) * 58), new Vector2(124, 52), UIKit.Bg, () => { app.mode?.OnSay(k); chatPanel.gameObject.SetActive(false); }, 15);
            }
            chatPanel.gameObject.SetActive(false);
            netText = UIKit.LabelAt(root, "", 16, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(160, -300), new Vector2(290, 220), TextAnchor.UpperLeft, false);
            netText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            netText.raycastTarget = false;

            // кнопка действия (заправка)
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                actBtns[i] = UIKit.Button(root, "", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(250, 58), UIKit.Light, () => app.mode?.OnAction(k), 19);
                actLabels[i] = actBtns[i].GetComponentInChildren<Text>(); actLabels[i].color = UIKit.Ink;
                actBtns[i].gameObject.SetActive(false);
            }
            damageText = UIKit.LabelAt(root, "", 16, UIKit.Gold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 112), new Vector2(150, 24), TextAnchor.MiddleCenter, true);

            // знак «60»
            limit = UIKit.Rect(root, "Limit", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 60), new Vector2(64, 64));
            var ring = limit.gameObject.AddComponent<Image>(); ring.sprite = UIKit.Circle; ring.color = UIKit.Light;
            var inner = UIKit.Rect(limit, "In", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
            var ii = inner.gameObject.AddComponent<Image>(); ii.sprite = UIKit.Circle; ii.color = UIKit.Ink; ii.raycastTarget = false;
            UIKit.Label(inner, "60", 24, Color.white);
            ring.raycastTarget = false;
            limit.gameObject.SetActive(false);

            // парктроник
            sensors = UIKit.Rect(root, "Sensors", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(200, 60), new Vector2(150, 64));
            UIKit.Img(sensors, UIKit.Bg);
            sensorText = UIKit.Label(sensors, "", 17, Color.white);
            sensors.gameObject.SetActive(false);

            // вспышка камеры «Стрелка»
            var fl = UIKit.Fill(root, "Flash");
            flashImg = fl.gameObject.AddComponent<Image>(); flashImg.color = new Color(1, 1, 1, 0); flashImg.raycastTarget = false;

            // --- пешком
            foot = UIKit.Fill(root, "Foot");
            look = UIKit.Img(UIKit.Fill(foot, "LookPad"), new Color(0, 0, 0, 0.001f), false).gameObject.AddComponent<LookPad>();
            var jr = UIKit.Rect(foot, "Joystick", new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 165), new Vector2(240, 240));
            var ji = jr.gameObject.AddComponent<Image>(); ji.sprite = UIKit.Circle; ji.color = new Color(0, 0, 0, 0.35f);
            var kn = UIKit.Rect(jr, "Knob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
            var ki = kn.gameObject.AddComponent<Image>(); ki.sprite = UIKit.Circle; ki.color = new Color(1, 1, 1, 0.55f); ki.raycastTarget = false;
            joy = jr.gameObject.AddComponent<Joystick>(); joy.knob = kn;
            System.Func<string, string, Vector2, System.Action, Button> fb = (icon, cap, pos, act) =>
            {
                var bt = UIKit.RoundButton(foot, icon, null, new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(92, 92), act);
                UIKit.LabelAt(foot, cap, 13, Color.white, new Vector2(1, 0), new Vector2(1, 0), pos + new Vector2(0, -56), new Vector2(110, 20)).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
                return bt;
            };
            fb("up", "ПРЫЖОК", new Vector2(-84, 120), () => app.walker.Jump());
            fb("sit", "СЕСТЬ", new Vector2(-196, 90), () => app.walker.ToggleSit());
            var sb = fb("smoke", "КУРИТЬ", new Vector2(-196, 215), () => app.walker.ToggleSmoking());
            smokeImg = (Image)sb.targetGraphic;
            fb("note", "СВИСТ", new Vector2(-84, 245), () => app.walker.WhistleNow());
            shopBtn = UIKit.Button(foot, "МАГАЗИН", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(360, 60), UIKit.Light, () => OpenShop(), 20);
            shopLabel = shopBtn.GetComponentInChildren<Text>();
            UIKit.IconAt(shopBtn.transform, "bag", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(34, 34), UIKit.Ink);
            carBtn = UIKit.Button(foot, "СЕСТЬ В МАШИНУ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(300, 56), UIKit.Light, () => app.ToggleFoot(), 20);
            UIKit.IconAt(carBtn.transform, "car", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(34, 34), UIKit.Ink);
            foot.SetAsFirstSibling();   // свайп-пад под всеми кнопками HUD
            foot.gameObject.SetActive(false);

            // меню ларька
            shopPanel = UIKit.Rect(root, "Shop", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(520, 470));
            UIKit.Img(shopPanel, UIKit.Bg2);
            shopPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API для режимов
        public void ShowTaxi(bool on) { taxiVisible = on; taxiBtn.gameObject.SetActive(on); TaxiOn(false); }
        public void TaxiOn(bool on) { taxiImg.color = on ? UIKit.Gold : UIKit.Light; }
        public void ShowLimit(bool on) { limitOn = on; }
        public void ShowChat(bool on) { chatBtn.gameObject.SetActive(on); if (!on) chatPanel.gameObject.SetActive(false); }
        public void NetList(string text) { if (netText == null) return; bool on = !string.IsNullOrEmpty(text); if (netText.gameObject.activeSelf != on) netText.gameObject.SetActive(on); if (on) netText.text = text; }
        public void Flash() { flashT = 0.35f; }

        readonly Button[] actBtns = new Button[3];
        readonly Text[] actLabels = new Text[3];
        Text damageText;

        /// <summary>До трёх кнопок действий (АЗС: заправка, ремонт, мойка); null — скрыть.</summary>
        public void Actions(string a0, string a1, string a2)
        {
            string[] t = { a0, a1, a2 };
            int n = 0; foreach (var x in t) if (!string.IsNullOrEmpty(x)) n++;
            int k = 0;
            for (int i = 0; i < 3; i++)
            {
                bool on = !string.IsNullOrEmpty(t[i]);
                if (actBtns[i].gameObject.activeSelf != on) actBtns[i].gameObject.SetActive(on);
                if (!on) continue;
                if (actLabels[i].text != t[i]) actLabels[i].text = t[i];
                ((RectTransform)actBtns[i].transform).anchoredPosition = new Vector2((k - (n - 1) / 2f) * 262f, 150);
                k++;
            }
        }

        public void Sensors(bool on, float front, float rear)
        {
            if (sensors.gameObject.activeSelf != on) sensors.gameObject.SetActive(on);
            if (!on) return;
            System.Func<float, string> f = d => d >= 2.95f ? "—" : d.ToString("0.0") + " м";
            sensorText.text = "П: " + f(front) + "\nЗ: " + f(rear);
            float m = Mathf.Min(front, rear);
            sensorText.color = m < 0.4f ? Color.white : m < 1f ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.55f, 0.55f, 0.55f);
        }

        public void SetRadioLabel(string name) { radioLabel.text = string.IsNullOrEmpty(name) ? "" : name; ((Image)radioBtn.targetGraphic).color = string.IsNullOrEmpty(name) ? UIKit.Light : Color.white; }

        public void SetDoor(bool allowWalk) { doorBtn.gameObject.SetActive(allowWalk); }

        // ------------------------------------------------------------------ ларёк
        void OpenShop()
        {
            if (shopNear == null) return;
            UIKit.Clear(shopPanel);
            shopPanel.gameObject.SetActive(true);
            UIKit.LabelAt(shopPanel, shopNear.title, 26, UIKit.Gold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30), new Vector2(-30, 40), TextAnchor.MiddleLeft);
            UIKit.LabelAt(shopPanel, "Баланс: " + M.Rub(app.save.Money), 17, UIKit.Muted, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -62), new Vector2(-30, 26), TextAnchor.MiddleLeft, false);
            UIKit.Button(shopPanel, "×", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -34), new Vector2(50, 50), UIKit.Bg, () => shopPanel.gameObject.SetActive(false), 30);
            var items = Shops.Menus.ContainsKey(shopNear.kind) ? Shops.Menus[shopNear.kind] : Shops.Menus["grocery"];
            for (int i = 0; i < items.Length; i++)
            {
                string k = items[i]; var it = Shops.Items[k];
                bool ok = app.save.Money >= it.price;
                UIKit.Button(shopPanel, it.name + "   " + it.price + " ₽", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -115 - i * 56), new Vector2(470, 50), ok ? UIKit.Bg : new Color(0.1f, 0.1f, 0.1f, 0.9f), () => Buy(k), 19);
            }
        }

        void Buy(string k)
        {
            var it = Shops.Items[k];
            if (!app.save.Spend(it.price)) { Toast("Не хватает денег", Bad); return; }
            app.audio.Coin();
            shopPanel.gameObject.SetActive(false);
            app.walker.Eat(k);
            app.daily.Progress("food", 1);
            app.save.d.stats.food++;
            Toast(it.name + " · −" + it.price + " ₽ · «" + Shops.Thank() + "»", Good, 2.5f);
        }

        // ------------------------------------------------------------------ кадр
        void UpdateExtra(float dt)
        {
            bool onFoot = app.onFoot;
            if (controls.gameObject.activeSelf == onFoot) controls.gameObject.SetActive(!onFoot);
            if (foot.gameObject.activeSelf != onFoot) foot.gameObject.SetActive(onFoot);
            bool lim = limitOn && !onFoot;
            if (limit.gameObject.activeSelf != lim) limit.gameObject.SetActive(lim);
            { var want = UIKit.Icon(onFoot ? "car" : "door"); if (doorIcon != null && doorIcon.sprite != want) doorIcon.sprite = want; }
            if (taxiBtn.gameObject.activeSelf != (taxiVisible && !onFoot)) taxiBtn.gameObject.SetActive(taxiVisible && !onFoot);
            if (onFoot)
            {
                var w = app.walker;
                var near = w.state == Walker.St.Walk ? Shops.Near(app.city, w.Pos) : null;
                if (near != shopNear) { shopNear = near; if (near != null) shopLabel.text = near.title.ToUpper(); }
                if (shopBtn.gameObject.activeSelf != (near != null)) shopBtn.gameObject.SetActive(near != null);
                bool nearCar = Vector2.Distance(new Vector2(w.Pos.x, w.Pos.z), new Vector2(app.player.Position.x, app.player.Position.z)) < 3.4f && !w.Sitting;
                if (carBtn.gameObject.activeSelf != nearCar) carBtn.gameObject.SetActive(nearCar);
                smokeImg.color = w.smoking ? Color.white : UIKit.Light;
                if (shopPanel.gameObject.activeSelf && near == null) shopPanel.gameObject.SetActive(false);
            }
            else if (shopPanel.gameObject.activeSelf) shopPanel.gameObject.SetActive(false);
            var tn = app.player.tune;
            string dmg = !onFoot && tn != null && tn.damage > 0.05f ? "КУЗОВ " + Mathf.RoundToInt((1f - tn.damage) * 100f) + "%" : "";
            if (damageText.text != dmg) { damageText.text = dmg; damageText.color = tn != null && tn.damage > 0.4f ? Color.white : new Color(0.7f, 0.7f, 0.7f); }
            if (flashT > 0f) { flashT -= dt; flashImg.color = new Color(1, 1, 1, Mathf.Clamp01(flashT / 0.35f) * 0.85f); }
        }

        /// <summary>Ввод пешехода: джойстик + WASD, Shift — шагом.</summary>
        public Vector2 Move()
        {
            var v = joy.value;
            float kx = (In.Held(In.K.D) || In.Held(In.K.Right) ? 1f : 0f) - (In.Held(In.K.A) || In.Held(In.K.Left) ? 1f : 0f);
            float ky = (In.Held(In.K.W) || In.Held(In.K.Up) ? 1f : 0f) - (In.Held(In.K.S) || In.Held(In.K.Down) ? 1f : 0f);
            if (kx != 0 || ky != 0) v = new Vector2(kx, ky).normalized;
            return v;
        }
    }
}
