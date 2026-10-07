using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Audio;
using RussianDrift.Core;

namespace RussianDrift.UI
{
    /// <summary>
    /// On-screen driving controls: arrows / wheel / tilt for steering, pedals, handbrake, clutch kick and manual shifts.
    /// Each control can be moved and scaled in the layout editor; placements persist in GameSettings.layout.
    /// </summary>
    public class TouchControls
    {
        private class Item
        {
            public string id;
            public RectTransform rt;
            public Image img;
            public Vector2 defaultPos;
            public DragMove drag;
            public System.Func<GameSettings, bool> visible;
            public Image outline;
        }

        private readonly List<Item> items = new List<Item>();
        private RectTransform root;
        private GameSettings settings;
        private bool editing;
        private Item selected;
        private Slider scaleSlider;
        private RectTransform editBar;
        private System.Action onEditDone;
        private bool ignoreSlider;

        public void Build(Transform parent, GameSettings s, System.Action onEditDone)
        {
            settings = s;
            this.onEditDone = onEditDone;
            root = Ui.Rect("TouchControls", parent);
            Ui.Stretch(root);
            // steering
            Add("left", new Vector2(0, 0), new Vector2(60, 60), new Vector2(230, 230), UiSprites.Arrow(3), null, st => st.controlScheme == 0, v => TouchInput.Left = v);
            Add("right", new Vector2(0, 0), new Vector2(320, 60), new Vector2(230, 230), UiSprites.Arrow(1), null, st => st.controlScheme == 0, v => TouchInput.Right = v);
            AddWheel("wheel", new Vector2(0, 0), new Vector2(60, 40), new Vector2(400, 400), st => st.controlScheme == 2);
            // pedals + helpers
            Add("throttle", new Vector2(1, 0), new Vector2(-60, 60), new Vector2(240, 340), null, "hud.gas", st => true, v => TouchInput.Throttle = v);
            Add("brake", new Vector2(1, 0), new Vector2(-330, 60), new Vector2(210, 210), null, "hud.brake", st => true, v => TouchInput.Brake = v);
            Add("handbrake", new Vector2(1, 0), new Vector2(-330, 290), new Vector2(210, 130), null, "hud.handbrake", st => true, v => TouchInput.Handbrake = v);
            Add("kick", new Vector2(1, 0), new Vector2(-570, 60), new Vector2(180, 180), null, "hud.kick", st => true, v => { if (v) TouchInput.ClutchKick = true; });
            Add("shiftup", new Vector2(1, 0), new Vector2(-570, 260), new Vector2(180, 110), null, "hud.shiftup", st => !st.autoGearbox, v => { if (v) TouchInput.ShiftUp = true; });
            Add("shiftdown", new Vector2(1, 0), new Vector2(-570, 380), new Vector2(180, 110), null, "hud.shiftdown", st => !st.autoGearbox, v => { if (v) TouchInput.ShiftDown = true; });
            BuildEditBar(parent);
            ApplySettings();
        }

        private Item Add(string id, Vector2 anchor, Vector2 pos, Vector2 size, Sprite icon, string label, System.Func<GameSettings, bool> vis, System.Action<bool> onHold)
        {
            var img = Ui.Panel(root, id, Color.white, 30);
            var it = new Item { id = id, rt = img.rectTransform, img = img, defaultPos = pos, visible = vis };
            Vector2 pivot = new Vector2(anchor.x, anchor.y);
            Ui.Place(img.rectTransform, anchor, pos, size, pivot);
            if (anchor.x > 0.5f) img.rectTransform.anchoredPosition = new Vector2(pos.x, pos.y);
            var hb = img.gameObject.AddComponent<HoldButton>();
            hb.tint = img; hb.onChange = v =>
            {
                if (editing) { if (v) Select(it); return; }
                if (onHold != null) onHold(v);
                if (v && settings.vibration) { }
            };
            var dm = img.gameObject.AddComponent<DragMove>();
            dm.onMoved = rt => SavePlacement(it);
            it.drag = dm;
            if (icon != null)
            {
                var ic = Ui.Img(img.transform, "Icon", icon, Color.white);
                Ui.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.55f);
            }
            if (label != null)
            {
                var t = Ui.Loc(img.transform, label, 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
                Ui.Stretch(t.rectTransform, 6, 6, 6, 6);
            }
            var ol = Ui.Img(img.transform, "Outline", UiSprites.Rounded(30), new Color(1f, 0.82f, 0.2f, 0.0f), true);
            Ui.Stretch(ol.rectTransform, -6, -6, -6, -6);
            it.outline = ol;
            items.Add(it);
            return it;
        }

        private void AddWheel(string id, Vector2 anchor, Vector2 pos, Vector2 size, System.Func<GameSettings, bool> vis)
        {
            var rt = Ui.Rect(id, root);
            Ui.Place(rt, anchor, pos, size, anchor);
            var holder = rt.gameObject.AddComponent<Image>();
            holder.color = new Color(0, 0, 0, 0.001f);
            var tw = rt.gameObject.AddComponent<TouchWheel>();
            var w = Ui.Img(rt, "Wheel", UiSprites.Wheel(), new Color(1, 1, 1, 0.6f));
            Ui.Stretch(w.rectTransform);
            tw.wheel = w.rectTransform;
            var dm = rt.gameObject.AddComponent<DragMove>();
            var it = new Item { id = id, rt = rt, img = w, defaultPos = pos, visible = vis, drag = dm };
            dm.onMoved = r => SavePlacement(it);
            var ol = Ui.Img(rt, "Outline", UiSprites.Rounded(30), new Color(1f, 0.82f, 0.2f, 0f), true);
            Ui.Stretch(ol.rectTransform, -6, -6, -6, -6);
            it.outline = ol;
            var hb = rt.gameObject.AddComponent<HoldButton>();
            hb.onChange = v => { if (editing && v) Select(it); };
            items.Add(it);
        }

        private void SavePlacement(Item it)
        {
            var p = settings.GetPlacement(it.id);
            Vector2 d = it.rt.anchoredPosition - it.defaultPos;
            p.x = d.x; p.y = d.y;
        }

        private void Select(Item it)
        {
            selected = it;
            foreach (var o in items) if (o.outline != null) o.outline.color = new Color(1f, 0.82f, 0.2f, o == it ? 0.9f : 0f);
            if (scaleSlider != null) { ignoreSlider = true; scaleSlider.value = Mathf.InverseLerp(0.6f, 1.6f, settings.GetPlacement(it.id).scale); ignoreSlider = false; }
        }

        public void ApplySettings()
        {
            float alpha = settings.controlOpacity;
            foreach (var it in items)
            {
                var p = settings.GetPlacement(it.id);
                bool vis = it.visible(settings);
                it.rt.gameObject.SetActive(vis || editing && it.visible(settings));
                it.rt.anchoredPosition = it.defaultPos + new Vector2(p.x, p.y);
                it.rt.localScale = Vector3.one * p.scale;
                var c = it.img.color; c.a = alpha;
                if (it.img.GetComponent<HoldButton>() != null)
                {
                    var hb = it.img.GetComponent<HoldButton>();
                    hb.normal = new Color(1, 1, 1, alpha); hb.pressed = new Color(1, 1, 1, Mathf.Min(1f, alpha + 0.35f));
                    it.img.color = hb.normal;
                }
                else it.img.color = new Color(1, 1, 1, alpha);
                if (it.drag != null) it.drag.editing = editing;
            }
        }

        private void BuildEditBar(Transform parent)
        {
            var bar = Ui.Panel(parent, "EditBar", new Color(0.04f, 0.05f, 0.08f, 0.93f), 22);
            editBar = bar.rectTransform;
            Ui.Place(editBar, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(1300, 130), new Vector2(0.5f, 1));
            var t = Ui.Loc(bar.transform, "hud.edit.hint", 28, Theme.Text, TextAnchor.MiddleLeft);
            Ui.Anchor(t.rectTransform, new Vector2(0, 0.5f), new Vector2(0.45f, 1), new Vector2(24, 0), new Vector2(0, -8));
            scaleSlider = Ui.Slider(bar.transform, 0f, 1f, 0.4f, v =>
            {
                if (selected == null || ignoreSlider) return;
                var p = settings.GetPlacement(selected.id);
                p.scale = Mathf.Lerp(0.6f, 1.6f, v);
                selected.rt.localScale = Vector3.one * p.scale;
            });
            Ui.Anchor((RectTransform)scaleSlider.transform, new Vector2(0, 0), new Vector2(0.45f, 0.5f), new Vector2(30, 6), new Vector2(0, -2));
            var reset = Ui.Button(bar.transform, "hud.edit.reset", () => { settings.layout.Clear(); ApplySettings(); }, Theme.PanelLight, 28);
            Ui.Place(reset.image.rectTransform, new Vector2(1, 0.5f), new Vector2(-300, 0), new Vector2(240, 80), new Vector2(1, 0.5f));
            var done = Ui.Button(bar.transform, "common.done", () => SetEditMode(false), Theme.Good * 0.9f, 30);
            Ui.Place(done.image.rectTransform, new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(240, 80), new Vector2(1, 0.5f));
            editBar.gameObject.SetActive(false);
        }

        public void SetEditMode(bool on)
        {
            editing = on;
            editBar.gameObject.SetActive(on);
            if (!on)
            {
                foreach (var o in items) if (o.outline != null) o.outline.color = new Color(1f, 0.82f, 0.2f, 0f);
                selected = null;
                var save = Services.Get<SaveSystem>();
                if (save != null) save.Save();
                if (onEditDone != null) onEditDone();
            }
            else { TouchInput.ResetHeld(); }
            ApplySettings();
        }

        public bool Editing { get { return editing; } }
    }
}
