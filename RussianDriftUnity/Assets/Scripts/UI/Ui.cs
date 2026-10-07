using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using RussianDrift.Audio;
using RussianDrift.Core;

namespace RussianDrift.UI
{
    public static class Theme
    {
        public static readonly Color Bg = new Color(0.05f, 0.06f, 0.09f, 0.94f);
        public static readonly Color Panel = new Color(0.11f, 0.12f, 0.17f, 0.95f);
        public static readonly Color PanelLight = new Color(0.17f, 0.19f, 0.26f, 0.98f);
        public static readonly Color Accent = new Color(1f, 0.38f, 0.1f, 1f);
        public static readonly Color Accent2 = new Color(0.1f, 0.78f, 1f, 1f);
        public static readonly Color Good = new Color(0.25f, 0.85f, 0.4f, 1f);
        public static readonly Color Bad = new Color(1f, 0.25f, 0.25f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.2f, 1f);
        public static readonly Color Text = new Color(0.96f, 0.97f, 1f, 1f);
        public static readonly Color Dim = new Color(0.62f, 0.66f, 0.76f, 1f);
        public static readonly Color Clear = new Color(0, 0, 0, 0);
    }

    /// <summary>Text that re-renders when the language changes.</summary>
    public class LocText : MonoBehaviour
    {
        public string key;
        public object[] args;
        public bool upper;
        private Text text;

        private void Awake() { text = GetComponent<Text>(); }
        private void OnEnable() { GameEvents.LanguageChanged += Refresh; Refresh(); }
        private void OnDisable() { GameEvents.LanguageChanged -= Refresh; }

        public void Set(string k, params object[] a) { key = k; args = a; Refresh(); }

        public void Refresh()
        {
            if (text == null) text = GetComponent<Text>();
            if (text == null || string.IsNullOrEmpty(key)) return;
            string s = args != null && args.Length > 0 ? Loc.F(key, args) : Loc.T(key);
            text.text = upper ? s.ToUpperInvariant() : s;
        }
    }

    /// <summary>Keeps a panel inside Screen.safeArea (notches, gesture bars).</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rt;
        private Rect last;
        private void Awake() { rt = (RectTransform)transform; Apply(); }
        private void Update() { if (Screen.safeArea != last) Apply(); }
        private void Apply()
        {
            last = Screen.safeArea;
            Vector2 min = last.position, max = last.position + last.size;
            min.x /= Screen.width; min.y /= Screen.height; max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Press/hold button supporting multi-touch (used for pedals, steering arrows, handbrake...).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> onChange;
        public Image tint;
        public Color normal = new Color(1, 1, 1, 0.55f), pressed = new Color(1, 1, 1, 0.95f);
        private bool down;
        private int pointerId = int.MinValue;

        public bool IsDown { get { return down; } }

        public void OnPointerDown(PointerEventData e) { pointerId = e.pointerId; Set(true); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointerId || pointerId == int.MinValue) Set(false); }
        public void OnPointerExit(PointerEventData e) { }
        private void OnDisable() { if (down) Set(false); }

        private void Set(bool v)
        {
            if (down == v) return;
            down = v;
            if (tint != null) tint.color = v ? pressed : normal;
            if (onChange != null) onChange(v);
            if (!v) pointerId = int.MinValue;
        }
    }

    /// <summary>On-screen steering wheel: drag around the centre; springs back on release.</summary>
    public class TouchWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform wheel;
        public float lockAngle = 120f;
        private float value;
        private bool held;
        private int pid = int.MinValue;
        private float startAngle, startValue;

        public void OnPointerDown(PointerEventData e)
        {
            pid = e.pointerId; held = true;
            startAngle = AngleOf(e);
            startValue = value;
            TouchInput.WheelActive = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pid) return;
            float a = AngleOf(e);
            float delta = Mathf.DeltaAngle(startAngle, a);
            value = Mathf.Clamp(startValue - delta / lockAngle, -1f, 1f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pid) return;
            held = false; pid = int.MinValue;
        }

        private float AngleOf(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out local);
            return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            if (!held) value = Mathf.MoveTowards(value, 0f, Time.unscaledDeltaTime * 3.5f);
            TouchInput.Wheel = value;
            TouchInput.WheelActive = true;
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0, 0, -value * lockAngle);
        }

        private void OnDisable() { TouchInput.WheelActive = false; TouchInput.Wheel = 0f; }
    }

    /// <summary>Draggable control used by the HUD layout editor.</summary>
    public class DragMove : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public bool editing;
        public Action<RectTransform> onMoved;
        private RectTransform rt;
        private void Awake() { rt = (RectTransform)transform; }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e)
        {
            if (!editing) return;
            var parent = (RectTransform)rt.parent;
            Vector2 d = e.delta / parent.lossyScale.x;
            rt.anchoredPosition += d;
        }
        public void OnEndDrag(PointerEventData e) { if (editing && onMoved != null) onMoved(rt); }
    }

    /// <summary>Small helper for building uGUI hierarchies from code.</summary>
    public static class Ui
    {
        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return font;
            }
        }

        public static void EnsureEventSystem()
        {
            if (Compat.FindFirst<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var m = go.AddComponent<InputSystemUIInputModule>();
            m.AssignDefaultActions();
        }

        public static Canvas CreateCanvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = Rect("SafeArea", canvas);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Anchor at a point and size in canvas units. anchor/pivot in 0..1.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color;
            if (sprite != null && sliced) img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, int radius = 18)
        {
            var img = Img(parent, name, UiSprites.Rounded(radius), color, true);
            img.raycastTarget = true;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Text Loc(Transform parent, string key, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal, bool upper = false, params object[] args)
        {
            var t = Label(parent, "", size, color, align, style);
            var lt = t.gameObject.AddComponent<LocText>();
            lt.key = key; lt.args = args; lt.upper = upper;
            lt.Refresh();
            return t;
        }

        public class ButtonRefs { public Button button; public Image image; public Text label; public LocText loc; }

        public static ButtonRefs Button(Transform parent, string locKeyOrText, Action onClick, Color color, int fontSize = 34, bool isKey = true, int radius = 18)
        {
            var img = Panel(parent, "Button", color, radius);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f); cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); cb.fadeDuration = 0.06f;
            b.colors = cb;
            var refs = new ButtonRefs { button = b, image = img };
            if (!string.IsNullOrEmpty(locKeyOrText))
            {
                var t = isKey ? Loc(img.transform, locKeyOrText, fontSize, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold) : Label(img.transform, locKeyOrText, fontSize, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                Stretch(t.rectTransform, 8, 4, 8, 4);
                refs.label = t; refs.loc = t.GetComponent<LocText>();
            }
            b.onClick.AddListener(() => { AudioManager.Click(); if (onClick != null) onClick(); });
            return refs;
        }

        public static ButtonRefs IconButton(Transform parent, Sprite icon, Action onClick, Color color, float iconScale = 0.6f, int radius = 22)
        {
            var refs = Button(parent, "", onClick, color, 30, false, radius);
            var ic = Img(refs.image.transform, "Icon", icon, Color.white);
            ic.rectTransform.anchorMin = new Vector2(0.5f - iconScale * 0.5f, 0.5f - iconScale * 0.5f);
            ic.rectTransform.anchorMax = new Vector2(0.5f + iconScale * 0.5f, 0.5f + iconScale * 0.5f);
            ic.rectTransform.offsetMin = ic.rectTransform.offsetMax = Vector2.zero;
            return refs;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange, bool whole = false)
        {
            var root = Rect("Slider", parent);
            var s = root.gameObject.AddComponent<Slider>();
            var bg = Img(root, "BG", UiSprites.Rounded(8), new Color(1, 1, 1, 0.14f), true);
            Anchor(bg.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -8), new Vector2(0, 8));
            var fillArea = Rect("FillArea", root);
            Anchor(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -8), new Vector2(0, 8));
            var fill = Img(fillArea, "Fill", UiSprites.Rounded(8), Theme.Accent, true);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1); fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect("HandleArea", root);
            Stretch(handleArea, 14, 0, 14, 0);
            var handle = Img(handleArea, "Handle", UiSprites.Circle(), Color.white);
            handle.rectTransform.sizeDelta = new Vector2(40, 40);
            handle.raycastTarget = true;
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.wholeNumbers = whole; s.value = value;
            if (onChange != null) s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        public class ToggleRefs { public Toggle toggle; public Image box, check; public LocText label; }

        public static ToggleRefs Toggle(Transform parent, string locKey, bool value, Action<bool> onChange)
        {
            var root = Rect("Toggle", parent);
            var tg = root.gameObject.AddComponent<Toggle>();
            var bg = Img(root, "Box", UiSprites.Rounded(10), new Color(1, 1, 1, 0.16f), true);
            Place(bg.rectTransform, new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(76, 42), new Vector2(1, 0.5f));
            bg.raycastTarget = true;
            var knob = Img(bg.transform, "Knob", UiSprites.Circle(), Color.white);
            knob.rectTransform.sizeDelta = new Vector2(34, 34);
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0, 0.5f);
            var lab = Loc(root, locKey, 30, Theme.Text, TextAnchor.MiddleLeft);
            Stretch(lab.rectTransform, 10, 0, 90, 0);
            tg.targetGraphic = bg;
            tg.isOn = value;
            Action<bool> apply = on =>
            {
                bg.color = on ? Theme.Accent : new Color(1, 1, 1, 0.16f);
                knob.rectTransform.pivot = new Vector2(on ? 1f : 0f, 0.5f);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? 1f : 0f, 0.5f);
                knob.rectTransform.anchoredPosition = new Vector2(on ? -4f : 4f, 0f);
            };
            apply(value);
            tg.onValueChanged.AddListener(v => { AudioManager.Click(); apply(v); if (onChange != null) onChange(v); });
            return new ToggleRefs { toggle = tg, box = bg, check = knob, label = lab.GetComponent<LocText>() };
        }

        public static ScrollRect Scroll(Transform parent, bool vertical, out RectTransform content, float spacing = 12f, RectOffset padding = null)
        {
            var root = Rect("Scroll", parent);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            var maskImg = root.gameObject.AddComponent<Image>();
            maskImg.color = new Color(0, 0, 0, 0.001f);
            root.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", root);
            if (vertical)
            {
                content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
                var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vl.spacing = spacing; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
                vl.padding = padding ?? new RectOffset(8, 8, 8, 8);
            }
            else
            {
                content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
                var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = spacing; hl.childForceExpandHeight = true; hl.childForceExpandWidth = false; hl.childControlHeight = true; hl.childControlWidth = false;
                hl.padding = padding ?? new RectOffset(8, 8, 8, 8);
            }
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fit.horizontalFit = vertical ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.vertical = vertical; sr.horizontal = !vertical;
            sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30f; sr.inertia = true;
            sr.viewport = root;
            return sr;
        }

        public static LayoutElement Size(Transform t, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) le.preferredHeight = h;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static HorizontalLayoutGroup HBox(Transform parent, float spacing, TextAnchor align = TextAnchor.MiddleLeft, bool expandW = false, bool expandH = true)
        {
            var rt = Rect("HBox", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = expandW; h.childForceExpandHeight = expandH;
            return h;
        }

        public static VerticalLayoutGroup VBox(Transform parent, float spacing, TextAnchor align = TextAnchor.UpperLeft, bool expandW = true, bool expandH = false)
        {
            var rt = Rect("VBox", parent);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = expandW; v.childForceExpandHeight = expandH;
            return v;
        }

        public static GridLayoutGroup Grid(Transform parent, Vector2 cell, Vector2 spacing, int columns)
        {
            var rt = Rect("Grid", parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = spacing; g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns;
            return g;
        }

        public static Image Bar(Transform parent, Color fill, Color back, out Image fillImg)
        {
            var bg = Img(parent, "Bar", UiSprites.Rounded(8), back, true);
            fillImg = Img(bg.transform, "Fill", UiSprites.Rounded(8), fill, true);
            fillImg.type = Image.Type.Sliced;
            fillImg.rectTransform.anchorMin = Vector2.zero; fillImg.rectTransform.anchorMax = new Vector2(0.5f, 1);
            fillImg.rectTransform.offsetMin = fillImg.rectTransform.offsetMax = Vector2.zero;
            return bg;
        }

        public static void SetBar(Image fill, float v)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
        }

        public static string Money(long v) { return v.ToString("N0").Replace(',', ' '); }

        public static string FormatTime(float t)
        {
            if (t < 0f) t = 0f;
            int m = (int)(t / 60f); float s = t - m * 60f;
            return m.ToString("0") + ":" + s.ToString("00.00");
        }
    }
}
