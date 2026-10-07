using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Audio;
using RussianDrift.Core;

namespace RussianDrift.UI
{
    /// <summary>Settings screen shared by the main menu and the pause menu.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        public Action onClose;
        public Action onEditLayout;
        private GameSettings s;
        private SaveSystem save;

        public static SettingsPanel Create(Transform canvasParent, bool inGame, Action onClose, Action onEditLayout)
        {
            var root = Ui.Rect("SettingsPanel", canvasParent);
            Ui.Stretch(root);
            var sp = root.gameObject.AddComponent<SettingsPanel>();
            sp.onClose = onClose; sp.onEditLayout = onEditLayout;
            sp.Build(inGame);
            return sp;
        }

        public static List<Button> Segmented(Transform parent, string[] labels, bool keys, int selected, Action<int> onPick, float w = 190f)
        {
            var row = Ui.HBox(parent, 10, TextAnchor.MiddleRight);
            var list = new List<Button>();
            var images = new List<Image>();
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = Ui.Button(row.transform, labels[i], null, i == selected ? Theme.Accent : Theme.PanelLight, 28, keys, 14);
                Ui.Size(b.image.transform, w, 70);
                list.Add(b.button); images.Add(b.image);
                b.button.onClick.AddListener(() =>
                {
                    for (int k = 0; k < images.Count; k++) images[k].color = k == idx ? Theme.Accent : Theme.PanelLight;
                    if (onPick != null) onPick(idx);
                });
            }
            return list;
        }

        private RectTransform Row(RectTransform content, string key, float h = 84f)
        {
            var bg = Ui.Panel(content, "Row", new Color(1, 1, 1, 0.04f), 14);
            Ui.Size(bg.transform, -1, h);
            var lab = Ui.Loc(bg.transform, key, 30, Theme.Text, TextAnchor.MiddleLeft);
            Ui.Anchor(lab.rectTransform, new Vector2(0, 0), new Vector2(0.42f, 1), new Vector2(24, 0), new Vector2(0, 0));
            var holder = Ui.Rect("Ctrl", bg.transform);
            Ui.Anchor(holder, new Vector2(0.42f, 0), new Vector2(1, 1), new Vector2(0, 6), new Vector2(-18, -6));
            return holder;
        }

        private void Header(RectTransform content, string key)
        {
            var t = Ui.Loc(content, key, 34, Theme.Accent, TextAnchor.LowerLeft, FontStyle.Bold, true);
            Ui.Size(t.transform, -1, 70);
        }

        private void Build(bool inGame)
        {
            save = Services.Get<SaveSystem>();
            s = save.Data.settings;
            var dim = Ui.Img(transform, "Dim", null, new Color(0, 0, 0, 0.78f));
            dim.raycastTarget = true; Ui.Stretch(dim.rectTransform);
            var panel = Ui.Panel(transform, "Panel", Theme.Bg, 28);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 940));
            var title = Ui.Loc(panel.transform, "settings.title", 52, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(40, -20), new Vector2(900, 90), new Vector2(0, 1));
            var close = Ui.Button(panel.transform, "common.close", Close, Theme.Accent, 32);
            Ui.Place(close.image.rectTransform, new Vector2(1, 1), new Vector2(-30, -26), new Vector2(240, 76), new Vector2(1, 1));

            RectTransform content;
            var sr = Ui.Scroll(panel.transform, true, out content, 10f);
            Ui.Anchor((RectTransform)sr.transform, Vector2.zero, Vector2.one, new Vector2(30, 24), new Vector2(-30, -120));

            // ---- language ----
            Header(content, "settings.language");
            var r = Row(content, "settings.language");
            Segmented(r, new[] { "Русский", "English" }, false, s.language, i => { s.language = i; Loc.SetLanguage(i); Apply(); }, 240f);

            // ---- graphics ----
            Header(content, "settings.graphics");
            r = Row(content, "settings.quality");
            Segmented(r, new[] { "settings.q.low", "settings.q.med", "settings.q.high", "settings.q.auto" }, true, s.graphicsQuality == 3 ? 3 : s.graphicsQuality,
                i => { s.graphicsQuality = i; QualityManager.Apply(); Apply(); }, 175f);
            r = Row(content, "settings.fps");
            Segmented(r, new[] { "30", "60" }, false, s.targetFps >= 60 ? 1 : 0, i => { s.targetFps = i == 0 ? 30 : 60; QualityManager.Apply(); Apply(); }, 150f);
            ToggleRow(content, "settings.motionblur", s.motionBlur, v => { s.motionBlur = v; Apply(); });
            ToggleRow(content, "settings.shake", s.cameraShake, v => { s.cameraShake = v; Apply(); });
            ToggleRow(content, "settings.minimap", s.showMinimap, v => { s.showMinimap = v; Apply(); });

            // ---- audio ----
            Header(content, "settings.audio");
            SliderRow(content, "settings.master", s.masterVolume, v => { s.masterVolume = v; Apply(); });
            SliderRow(content, "settings.music", s.musicVolume, v => { s.musicVolume = v; Apply(); });
            SliderRow(content, "settings.sfx", s.sfxVolume, v => { s.sfxVolume = v; Apply(); });

            // ---- controls ----
            Header(content, "settings.controls");
            r = Row(content, "settings.scheme");
            Segmented(r, new[] { "settings.scheme.buttons", "settings.scheme.tilt", "settings.scheme.wheel" }, true, s.controlScheme, i => { s.controlScheme = i; Apply(); }, 220f);
            SliderRow(content, "settings.tiltsens", Mathf.InverseLerp(0.4f, 2f, s.tiltSensitivity), v => { s.tiltSensitivity = Mathf.Lerp(0.4f, 2f, v); Apply(); });
            ToggleRow(content, "settings.tiltinvert", s.tiltInvert, v => { s.tiltInvert = v; Apply(); });
            r = Row(content, "settings.calibrate");
            var cal = Ui.Button(r, "settings.calibrate.btn", () =>
            {
                var acc = UnityEngine.InputSystem.Accelerometer.current;
                if (acc != null)
                {
                    Vector3 a = acc.acceleration.ReadValue();
                    float mag = a.magnitude;
                    s.tiltNeutral = mag > 0.01f ? Mathf.Asin(Mathf.Clamp(a.x / mag, -1f, 1f)) * Mathf.Rad2Deg : 0f;
                    Apply();
                }
            }, Theme.PanelLight, 28);
            Ui.Place(cal.image.rectTransform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(300, 70));
            SliderRow(content, "settings.opacity", Mathf.InverseLerp(0.25f, 1f, s.controlOpacity), v => { s.controlOpacity = Mathf.Lerp(0.25f, 1f, v); Apply(); });
            ToggleRow(content, "settings.vibration", s.vibration, v => { s.vibration = v; Apply(); });
            if (inGame && onEditLayout != null)
            {
                r = Row(content, "settings.layout");
                var el = Ui.Button(r, "settings.layout.btn", () => { Close(); if (onEditLayout != null) onEditLayout(); }, Theme.Accent2 * 0.7f, 28);
                Ui.Place(el.image.rectTransform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(300, 70));
            }

            // ---- assists ----
            Header(content, "settings.assists");
            ToggleRow(content, "settings.steerassist", s.steeringAssist, v => { s.steeringAssist = v; Apply(); });
            ToggleRow(content, "settings.driftassist", s.driftAssist, v => { s.driftAssist = v; Apply(); });
            ToggleRow(content, "settings.abs", s.abs, v => { s.abs = v; Apply(); });
            ToggleRow(content, "settings.tcs", s.tcs, v => { s.tcs = v; Apply(); });
            ToggleRow(content, "settings.auto", s.autoGearbox, v => { s.autoGearbox = v; Apply(); });
            ToggleRow(content, "settings.mph", s.useMph, v => { s.useMph = v; Apply(); });

            // ---- cloud ----
            Header(content, "settings.cloud");
            r = Row(content, "settings.cloud.sync");
            var h = Ui.HBox(r, 10, TextAnchor.MiddleRight);
            var up = Ui.Button(h.transform, "settings.cloud.up", () =>
            {
                var c = Services.Get<ICloudSaveService>();
                if (c != null) c.Upload(SaveSystem.Encode(save.Data), ok => Toast.Show(Loc.T(ok ? "common.done" : "common.failed")));
            }, Theme.PanelLight, 26);
            Ui.Size(up.image.transform, 250, 70);
            var down = Ui.Button(h.transform, "settings.cloud.down", () =>
            {
                var c = Services.Get<ICloudSaveService>();
                if (c != null) c.Download((ok, json) =>
                {
                    var d = ok ? SaveSystem.Decode(json) : null;
                    if (d != null) { save.ReplaceWith(d); Loc.SetLanguage(d.settings.language); Toast.Show(Loc.T("common.done")); }
                    else Toast.Show(Loc.T("common.failed"));
                });
            }, Theme.PanelLight, 26);
            Ui.Size(down.image.transform, 250, 70);
            var pad = Ui.Rect("Pad", content); Ui.Size(pad, -1, 40);
        }

        private void ToggleRow(RectTransform content, string key, bool value, Action<bool> on)
        {
            var bg = Ui.Panel(content, "Row", new Color(1, 1, 1, 0.04f), 14);
            Ui.Size(bg.transform, -1, 84);
            var t = Ui.Toggle(bg.transform, key, value, on);
            Ui.Stretch((RectTransform)t.toggle.transform, 24, 8, 24, 8);
        }

        private void SliderRow(RectTransform content, string key, float value, Action<float> on)
        {
            var r = Row(content, key);
            var sl = Ui.Slider(r, 0f, 1f, value, on);
            Ui.Anchor((RectTransform)sl.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 0), new Vector2(-10, 0));
        }

        private void Apply()
        {
            GameEvents.RaiseSettings();
            AudioManager.Ensure().ApplySettings();
        }

        public void Close()
        {
            save.Save();
            GameEvents.RaiseSettings();
            if (onClose != null) onClose();
            Destroy(gameObject);
        }
    }

    /// <summary>Tiny toast notification.</summary>
    public class Toast : MonoBehaviour
    {
        private static Toast instance;
        private Text text;
        private Image bg;
        private float t;

        public static void Show(string message)
        {
            if (instance == null)
            {
                var c = Ui.CreateCanvas("ToastCanvas", 500);
                var go = Ui.Rect("Toast", c.transform);
                instance = go.gameObject.AddComponent<Toast>();
                instance.bg = go.gameObject.AddComponent<Image>();
                instance.bg.sprite = UiSprites.Rounded(22); instance.bg.type = Image.Type.Sliced;
                instance.bg.color = new Color(0.08f, 0.09f, 0.13f, 0.95f); instance.bg.raycastTarget = false;
                Ui.Place(go, new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(760, 90));
                instance.text = Ui.Label(go, "", 34, Theme.Text);
                Ui.Stretch(instance.text.rectTransform, 12, 6, 12, 6);
                DontDestroyOnLoad(c.gameObject);
            }
            instance.text.text = message;
            instance.t = 2.2f;
            instance.gameObject.SetActive(true);
        }

        private void Update()
        {
            t -= Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t * 3f);
            bg.color = new Color(0.08f, 0.09f, 0.13f, 0.95f * a);
            text.color = new Color(1, 1, 1, a);
            if (t <= 0f) gameObject.SetActive(false);
        }
    }
}
