using UnityEngine;

namespace CaucasusDrive
{
    public struct CarInput
    {
        public float steer, throttle, brake;  // steer: −1 влево … +1 вправо
        public bool handbrake;
    }

    public class PhysSpec
    {
        public float mass, inertia, wheelbase, cgToFront, cgToRear, cgHeight, wheelRadius, maxSteer, steerSpeed;
        public float[][] torqueCurve;
        public float idleRpm, revLimit, finalDrive, drivetrainEff, upshiftRpm, downshiftRpm, shiftTime;
        public float[] gears;
        public Drive drive;
        public float brakeForce, handbrakeForce, dragCoef, rollCoef, tireGrip, corneringF, corneringR, handbrakeGrip;
        public float stabilityAssist, driftAssist, csInto, driftDrag, power, tank;

        static float[][] TorqueCurve(float peak, float peakRpm, float redline)
        {
            float mid = (peakRpm + redline) / 2f;
            float[][] k = {
                new[] { 800f, 0.68f }, new[] { 1500f, 0.84f }, new[] { Mathf.Min(2500f, peakRpm - 400f), 0.95f }, new[] { peakRpm, 1f },
                new[] { mid, 0.93f }, new[] { redline, 0.78f }, new[] { redline + 400f, 0.5f },
            };
            foreach (var p in k) p[1] *= peak;
            return k;
        }

        /// <summary>Спецификация физики из описания модели и тюнинга (как makeSpec в веб-версии).</summary>
        public static PhysSpec Make(CarDef def, float engine = 1f, float tires = 1f, float lower = 0f)
        {
            var s = def.spec; var d = def.dims;
            float L = d.axleF - d.axleR;
            float m = s.mass + 75f;
            float len = d.front - d.rear;
            return new PhysSpec
            {
                mass = m,
                inertia = m * (len * len + d.W * d.W) / 12f * 0.95f,
                wheelbase = L, cgToFront = d.axleF, cgToRear = -d.axleR,
                cgHeight = Mathf.Max(0.35f, s.cgHeight + lower * 1.5f),
                wheelRadius = d.wheelR, maxSteer = s.maxSteer, steerSpeed = 3.2f,
                torqueCurve = TorqueCurve(s.torque * engine, s.peakRpm, s.redline),
                idleRpm = 850f, revLimit = s.redline + 300f, gears = s.gears, finalDrive = s.final,
                drivetrainEff = s.drive == Drive.AWD ? 0.82f : 0.88f, drive = s.drive,
                upshiftRpm = s.redline - 700f, downshiftRpm = 2000f, shiftTime = 0.3f,
                brakeForce = s.brake, handbrakeForce = s.mass * 4f, dragCoef = s.drag, rollCoef = 0.013f,
                tireGrip = s.grip * tires,
                corneringF = 9f * (lower < 0 ? 1.08f : 1f), corneringR = 10f * (lower < 0 ? 1.08f : 1f),
                handbrakeGrip = 0.35f, stabilityAssist = 0.35f, driftAssist = 1f, csInto = 0.12f, driftDrag = 0.1f,
                power = s.hp * 745.7f * engine, tank = s.tank,
            };
        }
    }

    /// <summary>
    /// Физика автомобиля — дословный порт VehiclePhysics.js: «2 оси» с шинами (Fy = −μN·tanh(k·α)),
    /// перенос веса, круг трения, ручник, мотор с кривой момента, автомат R/N/D и механика,
    /// дрифт (гистерезис заноса, перегазовка, помощник контрруления, ограничитель угла).
    /// Внутренние координаты — как в веб-версии (+X влево): вперёд = (sin h, cos h), yawRate > 0 — влево.
    /// Перевод в Unity (+X вправо) делает PlayerCar: x → −x, курс → −курс.
    /// </summary>
    public class VehiclePhysics
    {
        const float G = 9.81f;
        public const float Step = 1f / 120f;

        public PhysSpec spec;
        public float x, z, heading, vx, vz, yawRate, steer;
        public char selector = 'D';
        public bool manual;
        public int gear = 1;
        public float rpm = 850f, shiftTimer, axLong, ayLat, vLong, vLat, speed;
        public float slipRear, slipFront, wheelSpin, load;
        public bool braking, reversing;
        public float fuel, odometer;
        public float driftAngle, sliding, kickT;
        float liftT = 9f, prevThr, acc;

        public VehiclePhysics(PhysSpec s) { spec = s; fuel = s.tank * 0.7f; }

        public void SetSpec(PhysSpec s) { spec = s; fuel = Mathf.Min(fuel, s.tank); gear = Mathf.Min(gear, s.gears.Length - 1); }

        public void Reset(float px, float pz, float h)
        {
            x = px; z = pz; heading = h; vx = vz = yawRate = steer = axLong = ayLat = speed = vLong = 0f;
            acc = 0f; driftAngle = sliding = kickT = 0f; liftT = 9f; prevThr = 0f;
            gear = 1; if (!manual) selector = 'D';
        }

        public void SetSelector(char sel)
        {
            if (manual || selector == sel) return;
            selector = sel;
            gear = sel == 'R' ? -1 : sel == 'N' ? 0 : 1;
            shiftTimer = 0.25f;
        }

        public void SetManual(bool on)
        {
            manual = on;
            if (!on) { int g = gear; selector = ' '; SetSelector(g < 0 ? 'R' : g == 0 ? 'N' : 'D'); }
        }

        public void ShiftUp() { if (manual && gear < spec.gears.Length - 1) { gear++; shiftTimer = spec.shiftTime; } }
        public void ShiftDown()
        {
            if (!manual || gear <= -1) return;
            if (gear == 0 && vLong > 1.5f) return;
            gear--; shiftTimer = spec.shiftTime;
        }

        public string GearLabel
        {
            get
            {
                if (!manual) return selector == 'D' ? "D" + gear : selector.ToString();
                return gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString();
            }
        }

        float TorqueAt(float r)
        {
            var c = spec.torqueCurve;
            if (r <= c[0][0]) return c[0][1];
            for (int i = 1; i < c.Length; i++)
                if (r <= c[i][0]) return M.Lerp(c[i - 1][1], c[i][1], (r - c[i - 1][0]) / (c[i][0] - c[i - 1][0]));
            return c[c.Length - 1][1];
        }

        /// <summary>Шаги по 1/120 с (накопитель), не больше 12 за вызов.</summary>
        public void Update(float dt, CarInput input, float surfaceMu)
        {
            acc += dt;
            int steps = 0;
            while (acc >= Step && steps < 12) { StepOnce(Step, input, surfaceMu); acc -= Step; steps++; }
            if (steps == 12) acc = 0f;
        }

        void StepOnce(float dt, CarInput input, float surfaceMu)
        {
            var s = spec;
            float h = heading;
            float fx = Mathf.Sin(h), fz = Mathf.Cos(h);
            float rx = -fz, rz = fx;
            float vLo = vx * fx + vz * fz;
            float vLa = vx * rx + vz * rz;
            float spd = Mathf.Sqrt(vx * vx + vz * vz);
            bool hasFuel = fuel > 0f;
            float driveIn = hasFuel ? input.throttle : 0f;
            float brakeIn = input.brake;

            bool fwd = vLo > 1f;
            float vLatR = vLa + s.cgToRear * yawRate;
            float slipR = fwd ? Mathf.Atan2(vLatR, vLo) : 0f;
            bool inSlide = sliding > 0.3f;
            float slideK = fwd && spd > 4f ? M.Smoothstep(inSlide ? 0.06f : 0.14f, inSlide ? 0.22f : 0.32f, Mathf.Abs(slipR)) : 0f;
            float assist = s.driftAssist;

            float steerLimit = M.Lerp(M.Clamp(26f / (vLo * vLo + 1f), 0.03f, s.maxSteer), s.maxSteer, slideK);
            float steerTarget = input.steer * steerLimit;
            if (assist > 0f && slideK > 0f)
            {
                float vLatF0 = vLa - s.cgToFront * yawRate;
                float align = Mathf.Atan2(vLatF0, vLo);
                float cs = M.Clamp(align - M.Sign(slipR) * s.csInto + input.steer * 0.32f, -s.maxSteer, s.maxSteer);
                steerTarget = M.Lerp(steerTarget, cs, slideK * assist);
            }
            float steerRate = s.steerSpeed * (1f + slideK * 1.5f);
            steer += M.Clamp(steerTarget - steer, -steerRate * dt, steerRate * dt);
            float delta = steer;
            float cosD = Mathf.Cos(delta), sinD = Mathf.Sin(delta);

            float L = s.wheelbase, W = s.mass * G;
            float transfer = s.mass * axLong * s.cgHeight / L;
            float Nf = Mathf.Max(W * 0.15f, W * s.cgToRear / L - transfer);
            float Nr = Mathf.Max(W * 0.15f, W * s.cgToFront / L + transfer);

            int g = gear;
            float ratio = g < 0 ? -s.gears[0] : g == 0 ? 0f : s.gears[g];
            float total = ratio * s.finalDrive;
            float wheelRpm = vLo / s.wheelRadius * 60f / (2f * Mathf.PI);
            float shaftRpm = Mathf.Abs(wheelRpm * total);
            float r;
            if (g == 0) r = s.idleRpm + driveIn * (s.revLimit - s.idleRpm);
            else
            {
                r = shaftRpm;
                if (Mathf.Abs(g) == 1) r = Mathf.Max(r, s.idleRpm + driveIn * 2000f);
                r = Mathf.Max(r, s.idleRpm);
            }
            if (shiftTimer > 0f) shiftTimer -= dt;
            float engineT = shiftTimer > 0f || g == 0 ? 0f : driveIn * TorqueAt(r);
            if (r > s.revLimit) engineT = 0f;
            bool movingWithGear = g != 0 && vLo * M.Sign(total) > 0.5f;
            float engineBrake = movingWithGear && shiftTimer <= 0f ? (1f - driveIn) * (r / 6000f) * 28f : 0f;
            float Fdrive = g == 0 ? 0f : (engineT - engineBrake) * total * s.drivetrainEff / s.wheelRadius;

            if (!manual && selector == 'D' && g >= 1 && shiftTimer <= 0f)
            {
                float up = M.Lerp(3000f, s.upshiftRpm, driveIn);
                float down = M.Lerp(1500f, s.downshiftRpm + 400f, driveIn);
                if (shaftRpm > up && g < s.gears.Length - 1 && driveIn > 0.05f) { gear++; shiftTimer = s.shiftTime; }
                else if (g > 1)
                {
                    float lowerRpm = shaftRpm * (s.gears[g - 1] / s.gears[g]);
                    if (shaftRpm < down && lowerRpm < up - 300f) { gear--; shiftTimer = s.shiftTime * 0.6f; }
                }
            }

            float mu = s.tireGrip * surfaceMu;
            float maxF = mu * Nf, maxR = mu * Nr;
            float FxF = 0f, FxR = 0f, usedF = 0f, usedR = 0f;
            if (slideK > 0f && s.drive != Drive.FWD && g >= 1 && shiftTimer <= 0f)
                Fdrive = Mathf.Max(Fdrive, driveIn * slideK * Mathf.Min(s.power * s.drivetrainEff / Mathf.Max(spd, 8f), 0.6f * mu * Nr));
            if (s.drive == Drive.RWD) { FxR = M.Clamp(Fdrive, -maxR, maxR); usedR = Mathf.Abs(Fdrive) / maxR; }
            else if (s.drive == Drive.FWD) { FxF = M.Clamp(Fdrive, -maxF, maxF); usedF = Mathf.Abs(Fdrive) / maxF; }
            else
            {
                FxF = M.Clamp(Fdrive * 0.5f, -maxF, maxF); FxR = M.Clamp(Fdrive * 0.5f, -maxR, maxR);
                usedF = Mathf.Abs(Fdrive * 0.5f) / maxF; usedR = Mathf.Abs(Fdrive * 0.5f) / maxR;
            }
            wheelSpin = Mathf.Max(0f, Mathf.Max(usedF, usedR) - 0.85f) * 3f;
            float latAvailR = Circle(usedR);
            float latAvailF = Circle(usedF);
            if (input.handbrake) latAvailR *= s.handbrakeGrip;
            if (s.drive != Drive.FWD) latAvailR *= 1f - 0.5f * slideK * driveIn * (s.drive == Drive.AWD ? 0.5f : 1f);
            if (driveIn < 0.3f && prevThr >= 0.3f) liftT = 0f;
            liftT += dt;
            if (s.drive == Drive.RWD && driveIn > 0.8f && prevThr <= 0.8f && liftT < 0.45f
                && Mathf.Abs(input.steer) > 0.5f && spd > 7f && spd < 32f && g >= 1) kickT = 0.45f;
            prevThr = driveIn;
            if (kickT > 0f) { kickT -= dt; latAvailR *= 0.5f; }

            float vLatF = vLa - s.cgToFront * yawRate;
            float alphaF = Mathf.Atan2(vLatF * cosD - vLo * sinD, Mathf.Max(Mathf.Abs(vLo * cosD + vLatF * sinD), 2f));
            float alphaR = Mathf.Atan2(vLatR, Mathf.Max(Mathf.Abs(vLo), 2f));
            float FyF = -mu * Nf * latAvailF * M.Tanh(s.corneringF * alphaF);
            float FyR = -mu * Nr * latAvailR * M.Tanh(s.corneringR * alphaR);

            float Fx = FxR + FxF * cosD - FyF * sinD;
            Fx += -s.dragCoef * vLo * Mathf.Abs(vLo);
            Fx += -s.rollCoef * s.mass * G * M.Tanh(vLo * 2f) * (surfaceMu < 0.8f ? 4f : 1f);
            float brakeTotal = brakeIn * s.brakeForce + (input.handbrake ? s.handbrakeForce : 0f);
            if (Mathf.Abs(vLo) > 0.01f) Fx -= M.Sign(vLo) * Mathf.Min(brakeTotal, Mathf.Abs(vLo) * s.mass / dt);
            if (slideK > 0f) Fx -= slideK * s.driftDrag * mu * Nr * Mathf.Min(1f, Mathf.Abs(Mathf.Sin(slipR)) * 2.5f);
            float FyFront = FyF * cosD + FxF * sinD;
            float Fy = FyR + FyFront;

            float torque = s.cgToRear * FyR - s.cgToFront * FyFront;
            float yawKin = -vLo * Mathf.Tan(delta) / L;
            float stab = s.stabilityAssist * (1f - slideK * (assist > 0f ? 1f : 0f));
            if (stab > 0f && spd > 3f && !input.handbrake) torque += (yawKin - yawRate) * s.inertia * stab * 3f;
            if (assist > 0f && slideK > 0f)
            {
                float over = Mathf.Abs(slipR) - 0.85f;
                if (over > -0.2f)
                {
                    float sgn = M.Sign(slipR);
                    torque -= sgn * Mathf.Max(0f, over) * s.inertia * 30f * assist;
                    if (yawRate * sgn > 0f) torque -= yawRate * s.inertia * 2.5f * M.Smoothstep(-0.2f, 0.1f, over) * assist;
                }
            }

            float ax = Fx / s.mass, ay = Fy / s.mass;
            vx += (fx * ax + rx * ay) * dt;
            vz += (fz * ax + rz * ay) * dt;
            yawRate += torque / s.inertia * dt;

            if (spd < 1.5f)
            {
                float k = 1f - spd / 1.5f;
                yawRate = M.Lerp(yawRate, yawKin, k);
                float vl = vx * rx + vz * rz;
                vx -= rx * vl * k * 0.5f;
                vz -= rz * vl * k * 0.5f;
                if (spd < 0.08f && (driveIn < 0.02f || g == 0)) { vx = 0f; vz = 0f; yawRate = 0f; }
            }

            heading += yawRate * dt;
            x += vx * dt;
            z += vz * dt;

            float powerKw = Mathf.Max(0f, engineT) * r / 9549f;
            fuel = Mathf.Max(0f, fuel - (0.00012f * (r / 850f) + powerKw * 0.00008f) * dt);
            odometer += spd * dt;

            axLong = M.Lerp(axLong, ax, 0.08f);
            ayLat = M.Lerp(ayLat, ay, 0.08f);
            vLong = vx * Mathf.Sin(heading) + vz * Mathf.Cos(heading);
            vLat = vLa;
            speed = Mathf.Sqrt(vx * vx + vz * vz);
            slipFront = Mathf.Abs(alphaF);
            slipRear = Mathf.Abs(alphaR);
            float vLatNew = -vx * Mathf.Cos(heading) + vz * Mathf.Sin(heading);
            driftAngle = speed > 2f && !reversing ? Mathf.Atan2(vLatNew + s.cgToRear * yawRate, vLong) : 0f;
            sliding = slideK;
            float spinRpm = s.drive != Drive.FWD ? slideK * driveIn * 0.55f : 0f;
            rpm = M.Lerp(rpm, Mathf.Min(M.Lerp(r, s.revLimit - 300f, spinRpm), s.revLimit + 100f), 0.2f);
            load = driveIn;
            braking = brakeIn > 0.1f;
            reversing = gear < 0;
        }

        static float Circle(float u) { u = Mathf.Min(u, 1f); return Mathf.Sqrt(Mathf.Max(0.08f, 1f - u * u * 0.92f)); }

        /// <summary>Интенсивность визга шин 0..1.</summary>
        public float Skid
        {
            get
            {
                if (speed < 2f) return Mathf.Min(wheelSpin, 1f) * 0.6f;
                float v = Mathf.Min(1f, speed / 12f);
                float lat = Mathf.Max(0f, slipRear - 0.12f) * 4f + Mathf.Max(0f, slipFront - 0.18f) * 3f;
                float drift = sliding * (0.55f + Mathf.Min(Mathf.Abs(driftAngle), 0.9f) * 0.5f);
                return Mathf.Min(1f, Mathf.Max(lat, drift) * v + wheelSpin * 0.6f);
            }
        }
    }
}
