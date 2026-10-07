using System;
using UnityEngine;
using Zanos.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Zanos.Game
{
    /// <summary>Ввод: клавиатура, геймпад, экранные кнопки. Руль сглаживается и зависит от скорости (порт rcd/src/input/controls.js).</summary>
    public sealed class Controls
    {
        public readonly CarInput State = new CarInput();
        public float SteerSens = 1f, Deadzone = 0.1f;
        public bool TouchLeft, TouchRight, TouchGas, TouchBrake, TouchHand, TouchKick;
        /// <summary>Экранные кнопки (координаты экрана, y снизу): left, right, gas, brake, hand, kick. Заполняет UI; null — кнопок нет.</summary>
        public Rect[] TouchRects;

        /// <summary>Опрос мультитача по прямоугольникам кнопок (IMGUI умеет только одно касание).</summary>
        public void PollTouch()
        {
            TouchLeft = TouchRight = TouchGas = TouchBrake = TouchHand = TouchKick = false;
            if (TouchRects == null) return;
            var pts = new System.Collections.Generic.List<Vector2>();
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current; if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) pts.Add(t.position.ReadValue());
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++) { var t = Input.GetTouch(i); if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) pts.Add(t.position); }
#endif
            foreach (var p in pts)
            {
                if (TouchRects[0].Contains(p)) TouchLeft = true; if (TouchRects[1].Contains(p)) TouchRight = true; if (TouchRects[2].Contains(p)) TouchGas = true;
                if (TouchRects[3].Contains(p)) TouchBrake = true; if (TouchRects[4].Contains(p)) TouchHand = true; if (TouchRects[5].Contains(p)) TouchKick = true;
            }
        }
        public System.Action OnCamera, OnReset, OnPause, OnHints;
        bool prevCam, prevReset, prevPause, prevHints, prevUp, prevDown;
#if ENABLE_INPUT_SYSTEM
        bool prevPadCam, prevPadReset, prevPadPause;
#endif

        static double Approach(double v, double t, double d) { return v < t ? Math.Min(t, v + d) : Math.Max(t, v - d); }

        bool Key(string name)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; if (k == null) return false;
            switch (name)
            {
                case "gas": return k.wKey.isPressed || k.upArrowKey.isPressed; case "brake": return k.sKey.isPressed || k.downArrowKey.isPressed;
                case "left": return k.aKey.isPressed || k.leftArrowKey.isPressed; case "right": return k.dKey.isPressed || k.rightArrowKey.isPressed;
                case "hand": return k.spaceKey.isPressed; case "kick": return k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                case "up": return k.eKey.isPressed || k.periodKey.isPressed; case "down": return k.qKey.isPressed || k.commaKey.isPressed;
                case "cam": return k.cKey.isPressed; case "reset": return k.rKey.isPressed; case "pause": return k.escapeKey.isPressed || k.pKey.isPressed; case "hints": return k.hKey.isPressed || k.f1Key.isPressed;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (name)
            {
                case "gas": return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow); case "brake": return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                case "left": return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow); case "right": return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
                case "hand": return Input.GetKey(KeyCode.Space); case "kick": return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case "up": return Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Period); case "down": return Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Comma);
                case "cam": return Input.GetKey(KeyCode.C); case "reset": return Input.GetKey(KeyCode.R); case "pause": return Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.P); case "hints": return Input.GetKey(KeyCode.H) || Input.GetKey(KeyCode.F1);
            }
            return false;
#else
            return false;
#endif
        }

        void Edge(string name, ref bool prev, System.Action act) { bool now = Key(name); if (now && !prev && act != null) act(); prev = now; }

        public void ResetState() { State.Throttle = State.Brake = State.Steer = 0; State.Handbrake = State.Kick = State.ShiftUp = State.ShiftDown = false; }

        /// <summary>Обновление кадра: dt (с), speed (м/с). Возвращает состояние для машины.</summary>
        public CarInput Update(double dt, double speed)
        {
            double sf = MathUtil.Clamp(speed / 35, 0, 1);
            double kThr = Key("gas") || TouchGas ? 1 : 0, kBrk = Key("brake") || TouchBrake ? 1 : 0;
            bool kL = Key("left") || TouchLeft, kR = Key("right") || TouchRight;
            double steerTarget = (kR ? 1 : 0) - (kL ? 1 : 0);
            double pThr = 0, pBrk = 0, pSteer = 0; bool pHand = false, pKick = false, pUp = false, pDown = false;
#if ENABLE_INPUT_SYSTEM
            var gp = Gamepad.current;
            if (gp != null)
            {
                float ax = gp.leftStick.x.ReadValue(); float dz = Deadzone;
                ax = Mathf.Abs(ax) < dz ? 0 : Mathf.Sign(ax) * (Mathf.Abs(ax) - dz) / (1 - dz);
                pSteer = Mathf.Sign(ax) * Mathf.Pow(Mathf.Abs(ax), 1.35f);
                pThr = gp.rightTrigger.ReadValue(); pBrk = gp.leftTrigger.ReadValue();
                pHand = gp.buttonSouth.isPressed; pKick = gp.buttonEast.isPressed || gp.rightShoulder.isPressed; pUp = gp.rightShoulder.isPressed || gp.dpad.up.isPressed; pDown = gp.leftShoulder.isPressed || gp.dpad.down.isPressed;
                bool cam = gp.buttonNorth.isPressed, reset = gp.buttonWest.isPressed, pause = gp.startButton.isPressed;
                if (cam && !prevPadCam && OnCamera != null) OnCamera(); if (reset && !prevPadReset && OnReset != null) OnReset(); if (pause && !prevPadPause && OnPause != null) OnPause();
                prevPadCam = cam; prevPadReset = reset; prevPadPause = pause;
            }
#endif
            Edge("cam", ref prevCam, OnCamera); Edge("reset", ref prevReset, OnReset); Edge("pause", ref prevPause, OnPause); Edge("hints", ref prevHints, OnHints);

            double thrT = Math.Max(kThr, pThr), brkT = Math.Max(kBrk, pBrk);
            State.Throttle = Approach(State.Throttle, thrT, (thrT > State.Throttle ? 5.5 : 9) * dt);
            State.Brake = Approach(State.Brake, brkT, (brkT > State.Brake ? 7 : 12) * dt);
            if (Math.Abs(pSteer) > 0.001 && !(kL || kR)) State.Steer = MathUtil.Lerp(State.Steer, pSteer, MathUtil.Clamp(dt * 18, 0, 1));
            else
            {
                double attack = MathUtil.Lerp(4.6, 2.3, sf) * SteerSens, release = MathUtil.Lerp(7.5, 5.0, sf), counter = MathUtil.Lerp(8.5, 6.5, sf) * SteerSens;
                if (steerTarget == 0) State.Steer = Approach(State.Steer, 0, release * dt);
                else if (MathUtil.Sign(State.Steer) != 0 && MathUtil.Sign(steerTarget) != MathUtil.Sign(State.Steer)) State.Steer = Approach(State.Steer, steerTarget, counter * dt);
                else State.Steer = Approach(State.Steer, steerTarget, attack * dt);
            }
            State.Steer = MathUtil.Clamp(State.Steer, -1, 1);
            State.Handbrake = Key("hand") || TouchHand || pHand; State.Kick = Key("kick") || TouchKick || pKick;
            bool up = Key("up") || pUp, down = Key("down") || pDown;
            State.ShiftUp = up && !prevUp; State.ShiftDown = down && !prevDown; prevUp = up; prevDown = down;
            return State;
        }
    }
}
