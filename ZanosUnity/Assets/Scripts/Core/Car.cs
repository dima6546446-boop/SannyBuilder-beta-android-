using System;

namespace Zanos.Core
{
    public sealed class CarInput
    {
        public double Throttle, Brake, Steer;       // 0..1, 0..1, -1..1 (вправо +)
        public bool Handbrake, Kick, ShiftUp, ShiftDown;
    }

    /// <summary>
    /// Плоская (x, z) модель заднеприводной машины. Порт rcd/src/physics/car.js: 4 колеса с переносом веса,
    /// комбинированная шина, динамика колёс, двигатель/КПП/турбо/дифференциал, помощники заноса.
    /// Курс h: вперёд = (sin h, cos h), вправо = (cos h, −sin h); рыскание w &gt; 0 — вправо.
    /// </summary>
    public sealed class Car
    {
        const double TwoPi = Math.PI * 2;
        public CarSpec Spec;
        public double A, B, Iz;                                  // ЦТ → передняя/задняя ось, инерция рыскания
        public double[][] WheelPos;                              // FL FR RL RR: [x вправо, z вперёд]
        public double[] StaticLoad = new double[4];

        // состояние
        public double X, Z, H, Vx, Vz, W;
        public double[] WheelW = new double[4], WheelSpin = new double[4], Loads = new double[4];
        public double SteerAngle, Rpm, Boost, ShiftTimer, Limiter, KickTimer, GearCool;
        public int Gear = 1;
        public double AxF, AyF;
        public double[] Slip = new double[4], SlipAngle = new double[4], SlipRatio = new double[4];
        public Surface[] ContactSurface = new Surface[4];
        public double ThrottleApplied;
        public CarInput Input = new CarInput();
        public double Speed, FwdSpeed, LatSpeed, DriftAngle, YawRate, Pitch, Roll, Distance;
        public bool Reversing, AutoGear = true;
        public double Assist = 0.7;
        public Action<int> OnShift;
        bool kickPrev;
        TireOut tmp;

        public Car(CarSpec spec) { SetSpec(spec); Reset(0, 0, 0); }

        public void SetSpec(CarSpec spec)
        {
            Spec = spec;
            double L = spec.Wheelbase;
            A = L * (1 - spec.WeightFront); B = L * spec.WeightFront;
            Iz = spec.Mass * Math.Pow(L * Phys.InertiaFactor, 2);
            double tf = spec.TrackFront / 2, tr = spec.TrackRear / 2;
            WheelPos = new[] { new[] { -tf, A }, new[] { tf, A }, new[] { -tr, -B }, new[] { tr, -B } };
            double mg = spec.Mass * Phys.Gravity;
            StaticLoad = new[] { mg * spec.WeightFront * 0.5, mg * spec.WeightFront * 0.5, mg * (1 - spec.WeightFront) * 0.5, mg * (1 - spec.WeightFront) * 0.5 };
        }

        public void Reset(double x, double z, double h)
        {
            X = x; Z = z; H = h; Vx = Vz = W = 0;
            for (int i = 0; i < 4; i++) { WheelW[i] = 0; WheelSpin[i] = 0; Slip[i] = 0; SlipAngle[i] = 0; SlipRatio[i] = 0; ContactSurface[i] = Surface.Asphalt; Loads[i] = StaticLoad[i]; }
            SteerAngle = 0; Gear = 1; Rpm = Spec.IdleRpm; Boost = 0; ShiftTimer = 0; Limiter = 0; KickTimer = 0; GearCool = 0;
            AxF = AyF = 0; ThrottleApplied = 0; Input = new CarInput();
            Speed = FwdSpeed = LatSpeed = DriftAngle = YawRate = Pitch = Roll = Distance = 0;
            Reversing = false; kickPrev = false;
        }

        public double EngineTorque(double rpm, double throttle)
        {
            var s = Spec; double f;
            if (rpm < s.IdleRpm) f = 0.55;
            else if (rpm < s.PeakRpm) f = MathUtil.Lerp(0.68, 1, MathUtil.Smooth((rpm - s.IdleRpm) / (s.PeakRpm - s.IdleRpm)));
            else f = MathUtil.Lerp(1, 0.66, MathUtil.Clamp((rpm - s.PeakRpm) / Math.Max(200, s.Redline - s.PeakRpm), 0, 1));
            return s.PeakTorque * f * (1 + Boost * s.Turbo) * throttle;
        }

        /// <summary>Один шаг. surfaceAt(x, z) — покрытие под колесом (null = асфальт).</summary>
        public void Step(double dt, Func<double, double, Surface> surfaceAt)
        {
            var s = Spec; var inp = Input;
            double sinH = Math.Sin(H), cosH = Math.Cos(H);
            double fx0 = sinH, fz0 = cosH, rx0 = cosH, rz0 = -sinH;

            double vf = Vx * fx0 + Vz * fz0, vr = Vx * rx0 + Vz * rz0;
            double speed = Math.Sqrt(Vx * Vx + Vz * Vz);
            Speed = speed; FwdSpeed = vf; LatSpeed = vr; YawRate = W;
            double beta = vf > 1.5 ? Math.Atan2(vr, vf) : 0;
            DriftAngle = beta;
            double betaDeg = Math.Abs(beta) * 180 / Math.PI;

            // --- руль + противорулевание ---
            double speedFrac = MathUtil.Smooth(speed / Phys.SteerSpeedRef);
            double steerLock = MathUtil.Lerp(1, Phys.SteerMinLockFrac, speedFrac);
            steerLock = MathUtil.Lerp(steerLock, 1, MathUtil.Smooth((betaDeg - 6) / (Phys.FullLockSlideDeg - 6)));
            double maxRad = s.MaxSteer;
            double target = inp.Steer * steerLock * maxRad;
            if (Assist > 0 && vf > 2.5)
            {
                double gate = MathUtil.Smooth((betaDeg - Phys.AssistThresholdDeg) / Phys.AssistRangeDeg) * MathUtil.Smooth((speed - 3) / 5);
                double betaF = Math.Atan2(vr + W * A, vf);
                double counter = MathUtil.Clamp(betaF * Phys.AssistGain, -maxRad, maxRad);
                double blend = Math.Min(1, Assist * gate * 1.4);
                target = MathUtil.Lerp(target, MathUtil.Clamp(counter + target * 0.2, -maxRad, maxRad), blend);
            }
            double rate = Phys.SteerRate * maxRad * (Math.Abs(target) > Math.Abs(SteerAngle) ? 1 : 1.25);
            SteerAngle += MathUtil.Clamp(target - SteerAngle, -rate * dt, rate * dt);
            double cs = Math.Cos(SteerAngle), sn = Math.Sin(SteerAngle);

            // --- коробка и двигатель ---
            double rearW = (WheelW[2] + WheelW[3]) * 0.5;
            HandleGears(dt, vf, speed, betaDeg, inp);
            double ratio = (Gear > 0 ? s.Gears[Gear - 1] : -s.ReverseRatio) * s.FinalDrive;
            double engRpm = Math.Abs(rearW * ratio) * 60 / TwoPi;
            bool launching = Math.Abs(Gear) == 1 && speed < 5;
            double rpm = engRpm;
            if (launching) rpm = Math.Max(engRpm, s.IdleRpm + inp.Throttle * (Phys.LaunchRpm - s.IdleRpm));
            rpm = Math.Max(rpm, s.IdleRpm);
            Rpm = MathUtil.Lerp(Rpm, rpm, MathUtil.Clamp(dt * 22, 0, 1));
            if (Rpm >= s.Redline) Limiter = 0.06;
            if (Limiter > 0) Limiter -= dt;

            double bt = inp.Throttle * MathUtil.Smooth((Rpm - s.Redline * 0.33) / (s.Redline * 0.35));
            Boost += (bt - Boost) * MathUtil.Clamp(dt * (bt > Boost ? Phys.TurboSpool : Phys.TurboDecay), 0, 1);

            double throttle = Reversing ? inp.Brake : inp.Throttle;
            ThrottleApplied = throttle;
            double driveTorque = 0;
            if (ShiftTimer > 0) ShiftTimer -= dt;
            bool cut = ShiftTimer > 0 || Limiter > 0;
            if (!cut) driveTorque = EngineTorque(Rpm, throttle);
            if (KickTimer > 0) { KickTimer -= dt; driveTorque *= Phys.ClutchKickMul; }
            if (throttle < 0.05 && Math.Abs(Gear) >= 1 && speed > 2)
                driveTorque -= s.PeakTorque * Phys.EngineBrake * MathUtil.Clamp(Rpm / s.Redline, 0, 1) * MathUtil.Sign(rearW != 0 ? rearW : 1);
            if (Assist > 0 && driveTorque > 0)
                driveTorque *= 1 - Math.Min(0.97, Phys.AngleThrottleCut * Assist) * MathUtil.Smooth((betaDeg - Phys.AngleCutStartDeg) / Phys.AngleCutRangeDeg);
            double wheelTorque = driveTorque * ratio * Phys.Efficiency * Phys.PowerMul;

            // --- перенос веса ---
            double m = s.Mass, L = s.Wheelbase, h = s.CgHeight * Phys.RollCgScale;
            double dLong = m * AxF * h / L;
            double frontAxle = m * Phys.Gravity * s.WeightFront - dLong;
            double rearAxle = m * Phys.Gravity * (1 - s.WeightFront) + dLong;
            double avgTrack = (s.TrackFront + s.TrackRear) * 0.5;
            double dLat = m * AyF * h / avgTrack;
            double rf = s.RollFront;
            double[] axleLoad = { frontAxle * 0.5, frontAxle * 0.5, rearAxle * 0.5, rearAxle * 0.5 };
            double[] latShare = { dLat * rf, -dLat * rf, dLat * (1 - rf), -dLat * (1 - rf) };
            for (int i = 0; i < 4; i++) Loads[i] = Math.Max(StaticLoad[i] * 0.04, axleLoad[i] + latShare[i]);

            // --- силы по колёсам ---
            double Fbx = 0, Fbz = 0, tau = 0;
            double dW = WheelW[3] - WheelW[2];
            double lock2 = s.DiffLock;
            double half = wheelTorque * 0.5;
            double lim = Math.Abs(wheelTorque) * 0.5 * lock2;
            double bias = MathUtil.Clamp(dW * Phys.LsdBiasStiffness * lock2, -lim, lim);
            double[] driveTq = { 0, 0, half + bias, half - bias };

            double boost = Assist > 0 ? Phys.AngleLimitBoost * s.TailHold * Assist * MathUtil.Smooth((betaDeg - Phys.AngleLimitStartDeg) / Phys.AngleLimitRangeDeg) : 0;
            double Rw = s.WheelRadius;
            double coupled = ShiftTimer <= 0 ? (launching ? 0.35 : 1) : 0.0;
            double IwRear = Phys.WheelInertia + 0.5 * Phys.EngineInertia * ratio * ratio * coupled;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                double px = WheelPos[i][0], pz = WheelPos[i][1];
                double vbx = vr + W * pz, vbz = vf - W * px;
                double uLong, uLat;
                if (front) { uLong = vbz * cs + vbx * sn; uLat = vbx * cs - vbz * sn; } else { uLong = vbz; uLat = vbx; }

                double wx = X + fx0 * pz + rx0 * px, wz = Z + fz0 * pz + rz0 * px;
                Surface surf = surfaceAt != null ? surfaceAt(wx, wz) : Surface.Asphalt;
                ContactSurface[i] = surf;
                double mu = Phys.MuScale * s.TireGrip * (front ? 1 : s.RearGripBias) * surf.Grip;

                double brakeTq = 0;
                double brakeInput = Reversing ? 0 : inp.Brake;
                brakeTq += brakeInput * s.BrakeTorque * (front ? Phys.BrakeBias : 1 - Phys.BrakeBias) * 0.5;
                bool hb = !front && inp.Handbrake;
                if (hb) brakeTq += Phys.HandbrakeTorque * 0.5;

                double Nz = Loads[i];
                double w = WheelW[i];
                Tire.Force(ref tmp, Nz, mu, uLong, uLat, w * Rw, !front, boost);
                double fx = tmp.Fx, fy = tmp.Fy;
                double meff = m * 0.25;
                double cap = Math.Abs(uLat) * meff / dt * Phys.LatForceCap;
                if (Math.Abs(fy) > cap) fy = MathUtil.Sign(fy) * cap;

                const double eps = 0.25;
                Tire.Force(ref tmp, Nz, mu, uLong, uLat, w * Rw + eps, !front, boost);
                double dFx = (tmp.Fx - fx) / eps;
                double net = driveTq[i] - fx * Rw;
                double Iw = front ? Phys.WheelInertia : IwRear;
                double denom = Iw + dt * Rw * Rw * Math.Max(0, dFx);
                double wNew = w + dt * net / denom;
                if (brakeTq > 0)
                {
                    double sgn = MathUtil.Sign(wNew);
                    double dec = brakeTq * dt / (front ? Phys.WheelInertia : IwRear);
                    if (hb) wNew = 0;
                    else if (Math.Abs(wNew) <= dec) wNew = 0;
                    else wNew -= sgn * dec;
                }
                WheelW[i] = wNew;
                WheelSpin[i] += wNew * dt;

                Tire.Force(ref tmp, Nz, mu, uLong, uLat, wNew * Rw, !front, boost);
                fx = tmp.Fx;
                if (Math.Abs(tmp.Fy) < Math.Abs(fy)) fy = tmp.Fy; else fy = MathUtil.Sign(tmp.Fy) * Math.Min(Math.Abs(tmp.Fy), cap);
                SlipAngle[i] = tmp.Alpha; SlipRatio[i] = tmp.Kappa;
                double target2 = Math.Max(MathUtil.Clamp((Math.Abs(tmp.Alpha) - 0.10) / 0.22, 0, 1) * MathUtil.Clamp(Math.Abs(uLat) / 2.2, 0, 1),
                                          MathUtil.Clamp((Math.Abs(tmp.Kappa) - 0.25) / 0.5, 0, 1) * MathUtil.Clamp(speed / 3, 0, 1));
                Slip[i] += (target2 - Slip[i]) * MathUtil.Clamp(dt * (target2 > Slip[i] ? 14 : 5), 0, 1);

                double bx, bz;
                if (front) { bx = fy * cs + fx * sn; bz = fx * cs - fy * sn; } else { bx = fy; bz = fx; }
                Fbx += bx; Fbz += bz;
                tau += pz * bx - px * bz;
            }

            // --- помощники рыскания ---
            if (Assist > 0 && speed > 6 && vf > 0)
            {
                double excess = MathUtil.Clamp((betaDeg - Phys.SpinGuardDeg) / 25, 0, 1);
                double sameDir = MathUtil.Sign(W) == -MathUtil.Sign(beta) ? 1 : 0;
                tau += -W * Iz * Phys.YawDampGain * 22 * excess * sameDir * Assist;
            }
            if (Assist > 0 && speed > 4)
            {
                double over = Math.Abs(W) - Phys.YawSoftMax * s.YawQuick * MathUtil.Clamp(speed / 14, 0.5, 1.15);
                if (over > 0) tau -= MathUtil.Sign(W) * over * Iz * Phys.YawSoftGain * Assist;
            }
            tau -= W * Iz * Phys.YawDamping;

            // --- сопротивление ---
            double surfAvg = ContactSurface[2] != null ? ContactSurface[2].Roll : 1;
            double Fx = Fbx * rx0 + Fbz * fx0, Fz = Fbx * rz0 + Fbz * fz0;
            if (speed > 0.05)
            {
                double drag = 0.5 * Phys.AirDensity * s.CdA * speed;
                Fx -= drag * Vx; Fz -= drag * Vz;
                double roll = Phys.RollingResistance * surfAvg * m * Phys.Gravity;
                double k = Math.Min(roll, m * speed / dt * 0.5) / speed;
                Fx -= k * Vx; Fz -= k * Vz;
            }
            double ax = Fx / m, az = Fz / m;
            Vx += ax * dt; Vz += az * dt;
            W += tau / Iz * dt;
            H += W * dt;
            X += Vx * dt; Z += Vz * dt;
            Distance += speed * dt;

            double aF = ax * fx0 + az * fz0, aR = ax * rx0 + az * rz0;
            double f = MathUtil.Clamp(dt / Phys.LoadFilterTau, 0, 1);
            AxF += (aF - AxF) * f; AyF += (aR - AyF) * f;
            Pitch = MathUtil.Clamp(-AxF * 0.008, -0.07, 0.07);
            Roll = MathUtil.Clamp(AyF * 0.006, -0.08, 0.08);
        }

        void HandleGears(double dt, double vf, double speed, double betaDeg, CarInput inp)
        {
            var s = Spec;
            if (!Reversing && inp.Brake > 0.2 && inp.Throttle < 0.05 && vf < 0.8) { Reversing = true; Gear = -1; }
            if (Reversing && inp.Throttle > 0.1 && vf > -1.0) { Reversing = false; Gear = 1; }
            if (Reversing) { Gear = -1; return; }
            if (Gear < 1) Gear = 1;
            if (inp.Kick && !kickPrev) KickTimer = Phys.ClutchKickTime;
            kickPrev = inp.Kick;

            double rpm = Math.Max(s.IdleRpm, Math.Abs(vf) / s.WheelRadius * s.Gears[Gear - 1] * s.FinalDrive * 60 / TwoPi);
            bool holdGear = betaDeg > 10 && inp.Throttle > 0.5;
            if (AutoGear)
            {
                GearCool = Math.Max(0, GearCool - dt);
                if (ShiftTimer <= 0 && GearCool <= 0)
                {
                    double up = s.Redline * (holdGear ? 0.95 : (inp.Throttle > 0.85 ? Phys.UpshiftRpmFrac : 0.82));
                    double down = s.Redline * (inp.Throttle > 0.9 ? 0.5 : Phys.DownshiftRpmFrac);
                    if (rpm > up && Gear < s.Gears.Length) { Gear++; ShiftTimer = Phys.ShiftTime; GearCool = 0.5; if (OnShift != null) OnShift(1); }
                    else if (Gear > 1 && rpm < down && !holdGear) { Gear--; ShiftTimer = Phys.ShiftTime * 0.6; GearCool = 0.5; if (OnShift != null) OnShift(-1); }
                }
            }
            else
            {
                if (inp.ShiftUp && Gear < s.Gears.Length && ShiftTimer <= 0) { Gear++; ShiftTimer = Phys.ShiftTime; if (OnShift != null) OnShift(1); }
                if (inp.ShiftDown && Gear > 1 && ShiftTimer <= 0) { Gear--; ShiftTimer = Phys.ShiftTime * 0.6; if (OnShift != null) OnShift(-1); }
                inp.ShiftUp = inp.ShiftDown = false;
            }
        }
    }
}
