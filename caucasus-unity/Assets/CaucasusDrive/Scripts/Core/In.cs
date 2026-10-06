using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CaucasusDrive
{
    /// <summary>
    /// Ввод, работающий и со старым Input Manager, и с новым Input System (в проектах Unity 6
    /// по умолчанию включён только новый — старый Input.GetKey там выбрасывает исключение).
    /// </summary>
    public static class In
    {
        public enum K { W, A, S, D, Up, Down, Left, Right, Space, H, C, Escape, F, E, T, R, X, G, Z, B, Q, LeftShift, Comma, Period, Slash }

#if ENABLE_INPUT_SYSTEM
        static Key Map(K k)
        {
            switch (k)
            {
                case K.W: return Key.W; case K.A: return Key.A; case K.S: return Key.S; case K.D: return Key.D;
                case K.Up: return Key.UpArrow; case K.Down: return Key.DownArrow; case K.Left: return Key.LeftArrow; case K.Right: return Key.RightArrow;
                case K.Space: return Key.Space; case K.H: return Key.H; case K.C: return Key.C;
                case K.F: return Key.F; case K.E: return Key.E; case K.T: return Key.T; case K.R: return Key.R; case K.X: return Key.X;
                case K.G: return Key.G; case K.Z: return Key.Z; case K.B: return Key.B; case K.Q: return Key.Q; case K.LeftShift: return Key.LeftShift;
                case K.Comma: return Key.Comma; case K.Period: return Key.Period; case K.Slash: return Key.Slash;
                default: return Key.Escape;
            }
        }
        public static bool Held(K k) { var kb = Keyboard.current; return kb != null && kb[Map(k)].isPressed; }
        public static bool Pressed(K k) { var kb = Keyboard.current; return kb != null && kb[Map(k)].wasPressedThisFrame; }

        /// <summary>Нажат ли палец/кнопка мыши и где (экранные координаты).</summary>
        public static bool Pointer(out Vector2 pos, out bool down)
        {
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                pos = ts.primaryTouch.position.ReadValue();
                down = ts.primaryTouch.press.wasPressedThisFrame;
                return true;
            }
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed)
            {
                pos = m.position.ReadValue();
                down = m.leftButton.wasPressedThisFrame;
                return true;
            }
            pos = Vector2.zero; down = false;
            return false;
        }

        /// <summary>Расстояние между двумя пальцами (щипок), если нажаты ровно два.</summary>
        public static bool Pinch(out float dist)
        {
            dist = 0f;
            var ts = Touchscreen.current;
            if (ts == null) return false;
            Vector2 a = Vector2.zero, b = Vector2.zero; int n = 0;
            foreach (var t in ts.touches) if (t.press.isPressed) { if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue(); n++; }
            if (n != 2) return false;
            dist = Vector2.Distance(a, b); return true;
        }

        public static float Scroll { get { var m = Mouse.current; return m != null ? m.scroll.ReadValue().y : 0f; } }

        public static int TouchCount
        {
            get
            {
                var ts = Touchscreen.current;
                if (ts == null) return 0;
                int n = 0;
                foreach (var t in ts.touches) if (t.press.isPressed) n++;
                return n;
            }
        }

        public static Vector3 Acceleration
        {
            get
            {
                var a = Accelerometer.current;
                if (a == null) return Vector3.zero;
                if (!a.enabled) InputSystem.EnableDevice(a);
                return a.acceleration.ReadValue();
            }
        }

        public static void AddUIModule(GameObject go) { go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(); }
#else
        static KeyCode Map(K k)
        {
            switch (k)
            {
                case K.W: return KeyCode.W; case K.A: return KeyCode.A; case K.S: return KeyCode.S; case K.D: return KeyCode.D;
                case K.Up: return KeyCode.UpArrow; case K.Down: return KeyCode.DownArrow; case K.Left: return KeyCode.LeftArrow; case K.Right: return KeyCode.RightArrow;
                case K.Space: return KeyCode.Space; case K.H: return KeyCode.H; case K.C: return KeyCode.C;
                case K.F: return KeyCode.F; case K.E: return KeyCode.E; case K.T: return KeyCode.T; case K.R: return KeyCode.R; case K.X: return KeyCode.X;
                case K.G: return KeyCode.G; case K.Z: return KeyCode.Z; case K.B: return KeyCode.B; case K.Q: return KeyCode.Q; case K.LeftShift: return KeyCode.LeftShift;
                case K.Comma: return KeyCode.Comma; case K.Period: return KeyCode.Period; case K.Slash: return KeyCode.Slash;
                default: return KeyCode.Escape;
            }
        }
        public static bool Held(K k) { return Input.GetKey(Map(k)); }
        public static bool Pressed(K k) { return Input.GetKeyDown(Map(k)); }

        public static bool Pointer(out Vector2 pos, out bool down)
        {
            pos = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            return Input.GetMouseButton(0);
        }

        public static bool Pinch(out float dist)
        {
            dist = 0f;
            if (Input.touchCount != 2) return false;
            dist = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position); return true;
        }
        public static float Scroll => Input.mouseScrollDelta.y;
        public static int TouchCount => Input.touchCount;
        public static Vector3 Acceleration => Input.acceleration;
        public static void AddUIModule(GameObject go) { go.AddComponent<StandaloneInputModule>(); }
#endif
    }
}
