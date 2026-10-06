using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CaucasusDrive
{
    /// <summary>Конструктор интерфейса из кода (uGUI): закруглённые панели, текст, кнопки. Без префабов.</summary>
    public static class UIKit
    {
        public static readonly Color Bg = new Color(0.05f, 0.05f, 0.05f, 0.84f);
        public static readonly Color Bg2 = new Color(0.14f, 0.14f, 0.14f, 0.94f);
        public static readonly Color Accent = new Color(0.96f, 0.96f, 0.96f, 1f);
        public static readonly Color Gold = new Color(0.94f, 0.94f, 0.94f, 1f);
        public static readonly Color Green = new Color(0.84f, 0.84f, 0.84f, 1f);
        public static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);

        static Font font;
        public static Font Font
        {
            get
            {
                if (font) return font;
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (!font) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return font;
            }
        }

        static Sprite rounded, circle;
        /// <summary>Закруглённый прямоугольник 64×64 с 9-slice-рамкой 24 px.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded) return rounded;
                int s = 64; float r = 22f;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (s - r))), dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (s - r)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255);
                        px[y * s + x] = new Color32(255, 255, 255, a);
                    }
                t.SetPixels32(px); t.Apply();
                rounded = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle) return circle;
                int s = 128;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f));
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(63.5f - d) * 255));
                    }
                t.SetPixels32(px); t.Apply();
                circle = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return circle;
            }
        }

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            #if UNITY_2023_1_OR_NEWER
            if (Object.FindFirstObjectByType<EventSystem>() == null)
#else
            if (Object.FindObjectOfType<EventSystem>() == null)
#endif
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                In.AddUIModule(es);
            }
            return c;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Растянуть на весь родитель с отступами.</summary>
        public static RectTransform Fill(Transform parent, string name, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Чёрно-белая тема интерфейса: любой цвет приводится к серому (по яркости), прозрачность сохраняется.</summary>
        public static Color Mono(Color c) { float l = Mathf.Clamp01(0.299f * c.r + 0.587f * c.g + 0.114f * c.b); return new Color(l, l, l, c.a); }
        public static float Lum(Color c) { return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b; }

        public static Image Img(RectTransform rt, Color c, bool round = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Mono(c);
            if (round)
            {
                img.sprite = Rounded; img.type = Image.Type.Sliced;
#if !CD_STUB
                img.pixelsPerUnitMultiplier = 2f; // радиус скругления 12 px
#endif
            }
            return img;
        }

        public static Image Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c)
        {
            return Img(Rect(parent, name, aMin, aMax, pos, size), c);
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
        {
            var rt = Fill(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = Mono(c); t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Text LabelAt(Transform parent, string text, int size, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
        {
            var rt = Rect(parent, "Label", aMin, aMax, pos, sz);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = Mono(c); t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string text, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color bg, System.Action onClick, int fontSize = 26, bool keepColor = false)
        {
            var rt = Rect(parent, "Btn_" + text, aMin, aMax, pos, size);
            var img = Img(rt, bg);
            if (keepColor) img.color = bg;                 // цветные плитки (выбор краски кузова) остаются цветными
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.pressedColor = new Color(0.75f, 0.75f, 0.75f); cb.highlightedColor = Color.white; cb.fadeDuration = 0.05f; b.colors = cb;
            b.onClick.AddListener(() => { App.I?.audio?.Click(); onClick?.Invoke(); });
            Label(rt, text, fontSize, Lum(img.color) > 0.62f ? Ink : Color.white);
            return b;
        }

        // ------------------------------------------------------------------ иконки (светлые круглые кнопки с чёрным значком)
        public static readonly Color Light = new Color(0.96f, 0.96f, 0.95f, 0.9f), Ink = new Color(0.1f, 0.1f, 0.11f, 1f), RingC = new Color(0.62f, 0.62f, 0.64f, 0.95f);
        static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

        /// <summary>Белый значок с прозрачностью из Resources/Icons (красится через Image.color).</summary>
        public static Sprite Icon(string name)
        {
            Sprite sp;
            if (iconCache.TryGetValue(name, out sp)) return sp;
            var t = Resources.Load<Texture2D>("Icons/ic_" + name);
            if (t != null) { t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; sp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
            iconCache[name] = sp;
            return sp;
        }

        public static Image IconAt(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c)
        {
            var rt = Rect(parent, "Icon_" + name, aMin, aMax, pos, size);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = Icon(name); im.color = c; im.raycastTarget = false; im.preserveAspect = true;
            if (im.sprite == null) im.enabled = false;
            return im;
        }

        /// <summary>Круглая кнопка в стиле иконок: светлый круг с тонким серым ободком и тёмным значком (или коротким текстом).</summary>
        public static Button RoundButton(Transform parent, string icon, string text, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, System.Action onClick, int fontSize = 15, Color? face = null, Color? ink = null)
        {
            var rt = Rect(parent, "Round_" + (icon ?? text), aMin, aMax, pos, size);
            var ring = rt.gameObject.AddComponent<Image>(); ring.sprite = Circle; ring.color = RingC;
            var f = Fill(rt, "Face", 3, 3, 3, 3);
            var img = f.gameObject.AddComponent<Image>(); img.sprite = Circle; img.color = face ?? Light;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.pressedColor = new Color(0.72f, 0.72f, 0.72f); cb.highlightedColor = Color.white; cb.fadeDuration = 0.05f; b.colors = cb;
            b.onClick.AddListener(() => { App.I?.audio?.Click(); onClick?.Invoke(); });
            var col = ink ?? Ink;
            if (icon != null) IconAt(f, icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.56f, col);
            else Label(f, text, fontSize, col);
            return b;
        }

        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject); }
    }

    /// <summary>Кнопка с удержанием (педали, ручник, гудок): каждый палец отслеживается отдельно — мультитач.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool held;
        public System.Action onDown;
        int pointers;
        Image img; Color baseCol; bool haveBase;
        void Update()
        {
            if (!haveBase) { img = GetComponent<Image>(); if (img == null) return; baseCol = img.color; haveBase = true; }
            img.color = Color.Lerp(img.color, held ? new Color(baseCol.r * 0.72f, baseCol.g * 0.72f, baseCol.b * 0.72f, baseCol.a) : baseCol, 0.5f);
        }
        public void OnPointerDown(PointerEventData e) { pointers++; held = true; onDown?.Invoke(); }
        public void OnPointerUp(PointerEventData e) { pointers = Mathf.Max(0, pointers - 1); held = pointers > 0; }
        void OnDisable() { pointers = 0; held = false; }
    }

    /// <summary>Руль: поворот пальцем вокруг центра (±135° → −1…+1), отпустил — возвращается в центр.</summary>
    public class SteeringWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float value;
        public RectTransform visual;
        float angle, startAngle, startValue;
        bool held;
        int pointer = -100;

        float AngleOf(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out local);
            return Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
        }

        public void OnPointerDown(PointerEventData e) { if (held) return; held = true; pointer = e.pointerId; startAngle = AngleOf(e); startValue = angle; }
        public void OnDrag(PointerEventData e)
        {
            if (!held || e.pointerId != pointer) return;
            float d = Mathf.DeltaAngle(startAngle, AngleOf(e));
            angle = Mathf.Clamp(startValue + d, -135f, 135f);
            startAngle = AngleOf(e); startValue = angle;
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) { held = false; pointer = -100; } }

        void Update()
        {
            if (!held) angle = Mathf.MoveTowards(angle, 0f, 540f * Time.unscaledDeltaTime);
            value = angle / 135f;
            if (visual) visual.localRotation = Quaternion.Euler(0, 0, -angle);
        }

        void OnDisable() { held = false; angle = 0; value = 0; }
    }
}
