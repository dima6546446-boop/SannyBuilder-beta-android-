using System;
namespace UnityEngine.InputSystem
{
    public enum InputActionType { Value, Button, PassThrough }
    public struct CompositeSyntax { public CompositeSyntax With(string n, string p) { return this; } }
    public class InputAction
    {
        public CompositeSyntax AddCompositeBinding(string c) { return default(CompositeSyntax); }
        public InputAction AddBinding(string p) { return this; }
        public T ReadValue<T>() where T : struct { return default(T); }
        public bool WasPressedThisFrame() { return false; }
        public bool IsPressed() { return false; }
    }
    public class InputActionMap : IDisposable
    {
        public InputActionMap(string n) { }
        public InputAction AddAction(string n, InputActionType t = InputActionType.Value) { return new InputAction(); }
        public void Enable() { } public void Disable() { } public void Dispose() { }
    }
    public class InputDevice { public bool enabled { get { return true; } } }
    public class AxisV3 { public Vector3 ReadValue() { return Vector3.zero; } }
    public class Accelerometer : InputDevice { public static Accelerometer current; public AxisV3 acceleration; }
    public static class InputSystem { public static void EnableDevice(InputDevice d) { } }
    public class ButtonControl { public bool wasPressedThisFrame; public bool isPressed; }
    public class Keyboard : InputDevice { public static Keyboard current; public ButtonControl escapeKey; }
}
namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule
    {
        public void AssignDefaultActions() { }
        public override void Process() { }
    }
}
namespace UnityEngine { public static class Handheld { public static void Vibrate() { } } }
