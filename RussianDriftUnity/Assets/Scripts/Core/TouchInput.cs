using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Written by on-screen controls (UI module), read by PlayerInputSource.</summary>
    public static class TouchInput
    {
        public static bool Throttle, Brake, Handbrake, Left, Right;
        public static float Wheel;                 // -1..1 from on-screen wheel
        public static bool WheelActive;
        public static bool ShiftUp, ShiftDown, CameraToggle, Pause, ClutchKick;

        public static void ResetHeld()
        {
            Throttle = Brake = Handbrake = Left = Right = false;
            Wheel = 0f; WheelActive = false;
        }
    }
}
