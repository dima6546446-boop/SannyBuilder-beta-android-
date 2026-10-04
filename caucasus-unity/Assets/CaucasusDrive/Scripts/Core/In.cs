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
        public enum K { W, A, S, D, Up, Down, Left, Right, Space, H, C, Escape }

#if ENABLE_INPUT_SYSTEM
        static Key Map(K k)
        {
            switch (k)
            {
                case K.W: return Key.W; case K.A: return Key.A; case K.S: return Key.S; case K.D: return Key.D;
                case K.Up: return Key.UpArrow; case K.Down: return Key.DownArrow; case K.Left: return Key.LeftArrow; case K.Right: return Key.RightArrow;
                case K.Space: return Key.Space; case K.H: return Key.H; case K.C: return Key.C; default: return Key.Escape;
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
                case K.Space: return KeyCode.Space; case K.H: return KeyCode.H; case K.C: return KeyCode.C; default: return KeyCode.Escape;
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

        public static int TouchCount => Input.touchCount;
        public static Vector3 Acceleration => Input.acceleration;
        public static void AddUIModule(GameObject go) { go.AddComponent<StandaloneInputModule>(); }
#endif
    }
}
