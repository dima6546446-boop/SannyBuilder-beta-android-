// Заглушка API Input System (только для проверки компиляции без Unity): сигнатуры совпадают с пакетом com.unity.inputsystem 1.x.
namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool isPressed; public float ReadValue() { return 0; } }
    public class AxisControl { public float ReadValue() { return 0; } }
    public class StickControl { public AxisControl x = new AxisControl(); }
    public class DpadControl { public ButtonControl up = new ButtonControl(), down = new ButtonControl(); }
    public class Keyboard { public static Keyboard current;
        public ButtonControl wKey, upArrowKey, sKey, downArrowKey, aKey, leftArrowKey, dKey, rightArrowKey, spaceKey, leftShiftKey, rightShiftKey, eKey, periodKey, qKey, commaKey, cKey, rKey, escapeKey, pKey, hKey, f1Key; }
    public class Gamepad { public static Gamepad current; public StickControl leftStick; public ButtonControl rightTrigger, leftTrigger, buttonSouth, buttonEast, buttonNorth, buttonWest, rightShoulder, leftShoulder, startButton; public DpadControl dpad; }
    public class TouchControlStub { public ButtonControl press = new ButtonControl(); public Vec2Stub position = new Vec2Stub(); }
    public class Vec2Stub { public Vector2 ReadValue() { return Vector2.zero; } }
    public class TouchesStub : System.Collections.Generic.List<TouchControlStub> { }
    public class Touchscreen { public static Touchscreen current; public TouchesStub touches; }
}
