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
        Button radioBtn, taxiBtn, doorBtn, actionBtn, shopBtn, carBtn;
        Text radioLabel, doorLabel, actionLabel, sensorText, shopLabel;
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
            radioBtn = UIKit.Button(tr, "РАДИО", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -146), new Vector2(58, 52), UIKit.Bg, () => app.CycleRadio(), 14);
            radioLabel = radioBtn.GetComponentInChildren<Text>();
            doorBtn = UIKit.Button(tr, "ВЫЙТИ", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-96, -146), new Vector2(58, 52), UIKit.Bg, () => app.ToggleFoot(), 13);
            doorLabel = doorBtn.GetComponentInChildren<Text>();
            taxiBtn = UIKit.Button(tr, "ТАКСИ", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-162, -146), new Vector2(58, 52), UIKit.Bg, () => app.mode?.OnTaxi(), 14);
            taxiImg = taxiBtn.GetComponent<Image>();
            taxiBtn.gameObject.SetActive(false);

            // кнопка действия (заправка)
            actionBtn = UIKit.Button(root, "", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(360, 60), UIKit.Gold * 0.85f, () => app.mode?.OnAction(), 22);
            actionLabel = actionBtn.GetComponentInChildren<Text>(); actionLabel.color = Color.black;
            actionBtn.gameObject.SetActive(false);

            // знак «60»
            limit = UIKit.Rect(root, "Limit", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 60), new Vector2(64, 64));
            var ring = limit.gameObject.AddComponent<Image>(); ring.sprite = UIKit.Circle; ring.color = new Color(0.85f, 0.1f, 0.1f);
            var inner = UIKit.Rect(limit, "In", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
            var ii = inner.gameObject.AddComponent<Image>(); ii.sprite = UIKit.Circle; ii.color = Color.white; ii.raycastTarget = false;
            UIKit.Label(inner, "60", 24, Color.black);
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
            UIKit.Button(foot, "ПРЫЖОК", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-90, 110), new Vector2(130, 110), UIKit.Bg2, () => app.walker.Jump(), 18);
            UIKit.Button(foot, "СЕСТЬ", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-230, 80), new Vector2(120, 80), UIKit.Bg2, () => app.walker.ToggleSit(), 18);
            var sb = UIKit.Button(foot, "КУРИТЬ", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-230, 175), new Vector2(120, 80), UIKit.Bg2, () => app.walker.ToggleSmoking(), 18);
            smokeImg = sb.GetComponent<Image>();
            UIKit.Button(foot, "СВИСТ", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-90, 230), new Vector2(130, 80), UIKit.Bg2, () => app.walker.WhistleNow(), 18);
            shopBtn = UIKit.Button(foot, "МАГАЗИН", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(360, 60), UIKit.Green, () => OpenShop(), 20);
            shopLabel = shopBtn.GetComponentInChildren<Text>();
            carBtn = UIKit.Button(foot, "СЕСТЬ В МАШИНУ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(300, 56), UIKit.Accent, () => app.ToggleFoot(), 20);
            foot.SetAsFirstSibling();   // свайп-пад под всеми кнопками HUD
            foot.gameObject.SetActive(false);

            // меню ларька
            shopPanel = UIKit.Rect(root, "Shop", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(520, 470));
            UIKit.Img(shopPanel, UIKit.Bg2);
            shopPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API для режимов
        public void ShowTaxi(bool on) { taxiVisible = on; taxiBtn.gameObject.SetActive(on); TaxiOn(false); }
        public void TaxiOn(bool on) { taxiImg.color = on ? new Color(0.75f, 0.6f, 0.05f, 0.95f) : UIKit.Bg; }
        public void ShowLimit(bool on) { limitOn = on; }
        public void Flash() { flashT = 0.35f; }

        public void Action(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            if (actionBtn.gameObject.activeSelf != on) actionBtn.gameObject.SetActive(on);
            if (on && actionLabel.text != text) actionLabel.text = text;
        }

        public void Sensors(bool on, float front, float rear)
        {
            if (sensors.gameObject.activeSelf != on) sensors.gameObject.SetActive(on);
            if (!on) return;
            System.Func<float, string> f = d => d >= 2.95f ? "—" : d.ToString("0.0") + " м";
            sensorText.text = "П: " + f(front) + "\nЗ: " + f(rear);
            float m = Mathf.Min(front, rear);
            sensorText.color = m < 0.4f ? new Color(1f, 0.3f, 0.25f) : m < 1f ? UIKit.Gold : Color.white;
        }

        public void SetRadioLabel(string name) { radioLabel.text = string.IsNullOrEmpty(name) ? "РАДИО" : name; radioBtn.GetComponent<Image>().color = string.IsNullOrEmpty(name) ? UIKit.Bg : new Color(0.12f, 0.35f, 0.5f, 0.9f); }

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
                UIKit.Button(shopPanel, it.name + "   " + it.price + " ₽", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -115 - i * 56), new Vector2(470, 50), ok ? UIKit.Bg : new Color(0.1f, 0.1f, 0.12f, 0.9f), () => Buy(k), 19);
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
            doorLabel.text = onFoot ? "В МАШ." : "ВЫЙТИ";
            if (taxiBtn.gameObject.activeSelf != (taxiVisible && !onFoot)) taxiBtn.gameObject.SetActive(taxiVisible && !onFoot);
            if (onFoot)
            {
                var w = app.walker;
                var near = w.state == Walker.St.Walk ? Shops.Near(app.city, w.Pos) : null;
                if (near != shopNear) { shopNear = near; if (near != null) shopLabel.text = near.title.ToUpper(); }
                if (shopBtn.gameObject.activeSelf != (near != null)) shopBtn.gameObject.SetActive(near != null);
                bool nearCar = Vector2.Distance(new Vector2(w.Pos.x, w.Pos.z), new Vector2(app.player.Position.x, app.player.Position.z)) < 3.4f && !w.Sitting;
                if (carBtn.gameObject.activeSelf != nearCar) carBtn.gameObject.SetActive(nearCar);
                smokeImg.color = w.smoking ? new Color(0.6f, 0.35f, 0.1f, 0.95f) : UIKit.Bg2;
                if (shopPanel.gameObject.activeSelf && near == null) shopPanel.gameObject.SetActive(false);
            }
            else if (shopPanel.gameObject.activeSelf) shopPanel.gameObject.SetActive(false);
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
