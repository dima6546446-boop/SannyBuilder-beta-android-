using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RussianDrift.Core
{
    /// <summary>
    /// Player controls via the new Input System: keyboard, gamepad, on-screen buttons/wheel (TouchInput) and phone tilt.
    /// Actions are built in code, so no .inputactions asset is required.
    /// </summary>
    public class PlayerInputSource : MonoBehaviour, IVehicleInputSource
    {
        public event Action CameraTogglePressed;
        public event Action PausePressed;

        private InputActionMap map;
        private InputAction aSteer, aThrottle, aBrake, aHand, aCam, aUp, aDown, aPause, aKick;
        private float keySteer;
        private float touchSteer;
        private bool upFlag, downFlag, kickFlag;
        private float tiltSmoothed;
        public float LastSteerRaw { get; private set; }

        private void Awake()
        {
            map = new InputActionMap("Drive");
            aSteer = map.AddAction("steer", InputActionType.Value);
            aSteer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            aSteer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            aSteer.AddBinding("<Gamepad>/leftStick/x");
            aThrottle = map.AddAction("throttle", InputActionType.Value);
            aThrottle.AddBinding("<Keyboard>/w"); aThrottle.AddBinding("<Keyboard>/upArrow"); aThrottle.AddBinding("<Gamepad>/rightTrigger");
            aBrake = map.AddAction("brake", InputActionType.Value);
            aBrake.AddBinding("<Keyboard>/s"); aBrake.AddBinding("<Keyboard>/downArrow"); aBrake.AddBinding("<Gamepad>/leftTrigger");
            aHand = map.AddAction("handbrake", InputActionType.Button);
            aHand.AddBinding("<Keyboard>/space"); aHand.AddBinding("<Gamepad>/buttonSouth"); aHand.AddBinding("<Gamepad>/buttonEast");
            aKick = map.AddAction("clutchkick", InputActionType.Button);
            aKick.AddBinding("<Keyboard>/leftShift"); aKick.AddBinding("<Gamepad>/buttonWest");
            aCam = map.AddAction("camera", InputActionType.Button);
            aCam.AddBinding("<Keyboard>/c"); aCam.AddBinding("<Gamepad>/buttonNorth");
            aUp = map.AddAction("shiftup", InputActionType.Button);
            aUp.AddBinding("<Keyboard>/e"); aUp.AddBinding("<Gamepad>/rightShoulder");
            aDown = map.AddAction("shiftdown", InputActionType.Button);
            aDown.AddBinding("<Keyboard>/q"); aDown.AddBinding("<Gamepad>/leftShoulder");
            aPause = map.AddAction("pause", InputActionType.Button);
            aPause.AddBinding("<Keyboard>/escape"); aPause.AddBinding("<Keyboard>/p"); aPause.AddBinding("<Gamepad>/start");
        }

        private void OnEnable()
        {
            map.Enable();
            TouchInput.ResetHeld();
            var acc = Accelerometer.current;
            if (acc != null && !acc.enabled) InputSystem.EnableDevice(acc);
        }

        private void OnDisable()
        {
            if (map != null) map.Disable();
        }

        private void OnDestroy()
        {
            if (map != null) map.Dispose();
        }

        public void CalibrateTilt()
        {
            var s = Services.Get<SaveSystem>();
            if (s == null) return;
            s.Data.settings.tiltNeutral = ReadTiltDegrees();
        }

        private static float ReadTiltDegrees()
        {
            var acc = Accelerometer.current;
            if (acc == null) return 0f;
            Vector3 a = acc.acceleration.ReadValue();
            float mag = a.magnitude;
            if (mag < 0.01f) return 0f;
            return Mathf.Asin(Mathf.Clamp(a.x / mag, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            if (aCam.WasPressedThisFrame() || TouchInput.CameraToggle)
            {
                TouchInput.CameraToggle = false;
                if (CameraTogglePressed != null) CameraTogglePressed();
            }
            if (aPause.WasPressedThisFrame() || TouchInput.Pause)
            {
                TouchInput.Pause = false;
                if (PausePressed != null) PausePressed();
            }
            if (aUp.WasPressedThisFrame() || TouchInput.ShiftUp) { upFlag = true; TouchInput.ShiftUp = false; }
            if (aDown.WasPressedThisFrame() || TouchInput.ShiftDown) { downFlag = true; TouchInput.ShiftDown = false; }
            if (aKick.WasPressedThisFrame() || TouchInput.ClutchKick) { kickFlag = true; TouchInput.ClutchKick = false; }
        }

        public VehicleInputState Read()
        {
            var settings = Services.Get<SaveSystem>() != null ? Services.Get<SaveSystem>().Data.settings : new GameSettings();
            var s = new VehicleInputState();
            float dt = Time.unscaledDeltaTime;

            // keyboard / gamepad steering (digital keys get smoothed, sticks pass through)
            float raw = aSteer.ReadValue<float>();
            float rate = Mathf.Abs(raw) > Mathf.Abs(keySteer) ? 4.5f : 7f;
            keySteer = Mathf.MoveTowards(keySteer, raw, rate * dt);

            // on-screen buttons
            float btn = (TouchInput.Right ? 1f : 0f) - (TouchInput.Left ? 1f : 0f);
            touchSteer = Mathf.MoveTowards(touchSteer, btn, (Mathf.Abs(btn) > 0.01f ? 3.8f : 7f) * dt);

            float steer = keySteer + touchSteer;
            if (TouchInput.WheelActive) steer += TouchInput.Wheel;

            if (settings.controlScheme == 1 && Accelerometer.current != null)
            {
                float deg = ReadTiltDegrees() - settings.tiltNeutral;
                float t = Mathf.Clamp(deg / (32f / Mathf.Max(0.3f, settings.tiltSensitivity)), -1f, 1f);
                if (settings.tiltInvert) t = -t;
                tiltSmoothed = Mathf.Lerp(tiltSmoothed, t, 1f - Mathf.Exp(-14f * dt));
                steer += tiltSmoothed;
            }

            s.steer = Mathf.Clamp(steer, -1f, 1f);
            LastSteerRaw = s.steer;
            s.throttle = Mathf.Max(aThrottle.ReadValue<float>(), TouchInput.Throttle ? 1f : 0f);
            s.brake = Mathf.Max(aBrake.ReadValue<float>(), TouchInput.Brake ? 1f : 0f);
            s.handbrake = aHand.IsPressed() || TouchInput.Handbrake;
            s.shiftUp = upFlag; s.shiftDown = downFlag; s.clutchKick = kickFlag;
            upFlag = downFlag = kickFlag = false;
            return s;
        }
    }
}
