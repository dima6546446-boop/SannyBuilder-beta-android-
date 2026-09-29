import { clamp, lerp } from '../utils/math.js';

/**
 * Характеристики ВАЗ-2107 (1.5 л, 71 л.с., 104 Н·м @ 3400, КПП 5-ст., задний привод).
 */
export const LADA_2107_SPEC = {
  name: 'ВАЗ-2107',
  mass: 1100,                 // кг (снаряжённая 1030 + водитель)
  inertia: 1850,              // кг·м² (момент инерции по рысканию)
  wheelbase: 2.424,
  cgToFront: 1.14,
  cgToRear: 1.284,
  cgHeight: 0.55,
  wheelRadius: 0.29,          // 175/70 R13
  maxSteer: 0.6,              // рад на колёсах
  steerSpeed: 3.0,            // рад/с
  torqueCurve: [[800, 70], [1500, 88], [2500, 100], [3400, 104], [4200, 101], [5000, 95], [5600, 88], [6200, 72], [6600, 50]],
  idleRpm: 850,
  revLimit: 6400,
  gears: [3.53, 3.67, 2.10, 1.36, 1.00, 0.82], // [задняя, 1..5]
  finalDrive: 3.9,
  drivetrainEff: 0.88,
  upshiftRpm: 5400,
  downshiftRpm: 2000,
  shiftTime: 0.35,
  brakeForce: 9500,           // Н (≈0.88 g)
  handbrakeForce: 4200,
  dragCoef: 0.52,             // 0.5·ρ·Cx·S = 0.5·1.2·0.45·1.9
  rollCoef: 11,
  tireGrip: 1.05,
  corneringF: 9.0,            // «жёсткость» шины, 1/рад (для tanh-модели)
  corneringR: 10.0,
  handbrakeGrip: 0.35,
  stabilityAssist: 0.35,      // помощь удержания на тач-управлении (0 — выкл.)
};

const G = 9.81;
const STEP = 1 / 120; // фиксированный шаг физики

/**
 * Физика автомобиля: плоская модель «велосипед + 2 оси» с шинной моделью
 * (сила бокового увода ~ μN·tanh(k·α)), перераспределением веса при разгоне/торможении,
 * кругом трения на ведущей задней оси (пробуксовка → занос при резком газе),
 * ручником (блокировка задних колёс → управляемый занос), двигателем
 * с кривой момента и автоматическим переключением 5 передач.
 *
 * Система координат: курс heading, вперёд = (sin h, cos h), вправо = (−cos h, sin h);
 * yawRate > 0 — поворот влево. steer > 0 — руль вправо.
 */
export class VehiclePhysics {
  constructor(spec = LADA_2107_SPEC) {
    this.spec = spec;
    this.x = 0; this.z = 0; this.heading = 0;
    this.vx = 0; this.vz = 0; this.yawRate = 0;
    this.steer = 0;
    this.gear = 1;
    this.rpm = spec.idleRpm;
    this.shiftTimer = 0;
    this.axLong = 0;
    this.ayLat = 0;
    this.vLong = 0;
    this.vLat = 0;
    this.speed = 0;
    this.slipRear = 0;
    this.slipFront = 0;
    this.wheelSpin = 0;
    this.load = 0;
    this.braking = false;
    this.reversing = false;
    this._acc = 0;
    this._revTimer = 0;
  }

  reset(x, z, heading) {
    Object.assign(this, { x, z, heading, vx: 0, vz: 0, yawRate: 0, steer: 0, gear: 1, axLong: 0 });
  }

  torqueAt(rpm) {
    const c = this.spec.torqueCurve;
    if (rpm <= c[0][0]) return c[0][1];
    for (let i = 1; i < c.length; i++) {
      if (rpm <= c[i][0]) {
        const t = (rpm - c[i - 1][0]) / (c[i][0] - c[i - 1][0]);
        return lerp(c[i - 1][1], c[i][1], t);
      }
    }
    return c[c.length - 1][1];
  }

  /** input: {steer, throttle, brake, handbrake}, surface: {mu} */
  update(dt, input, surface) {
    this._handleGearSelect(dt, input);
    this._acc += dt;
    let steps = 0;
    while (this._acc >= STEP && steps < 12) {
      this._step(STEP, input, surface.mu);
      this._acc -= STEP;
      steps++;
    }
    if (steps === 12) this._acc = 0; // защита от «спирали смерти» на слабых телефонах
  }

  _handleGearSelect(dt, input) {
    const vLong = this.vLong;
    // тормоз на месте → задняя передача; газ на задней → первая
    if (this.gear >= 1 && input.brake > 0.5 && input.throttle < 0.1 && vLong < 0.3) {
      this._revTimer += dt;
      if (this._revTimer > 0.3) { this.gear = -1; this._revTimer = 0; }
    } else if (this.gear === -1 && input.throttle > 0.5 && vLong > -0.3) {
      this._revTimer += dt;
      if (this._revTimer > 0.15) { this.gear = 1; this._revTimer = 0; }
    } else {
      this._revTimer = 0;
    }
    this.reversing = this.gear === -1;
  }

  _step(dt, input, surfaceMu) {
    const s = this.spec;
    const h = this.heading;
    const fx = Math.sin(h), fz = Math.cos(h);
    const rx = -fz, rz = fx;
    let vLong = this.vx * fx + this.vz * fz;
    let vLat = this.vx * rx + this.vz * rz;
    const speed = Math.hypot(this.vx, this.vz);

    // на задней передаче педали меняются ролями
    const rev = this.gear === -1;
    const driveIn = rev ? input.brake : input.throttle;
    const brakeIn = rev ? input.throttle : input.brake;

    // --- руль: ограничение угла с ростом скорости (≈ предел сцепления v²·δ/L ≤ μg,
    //     с небольшим запасом для заноса) + скорость вращения руля ---
    const steerLimit = clamp(26 / (vLong * vLong + 1), 0.03, s.maxSteer);
    const target = input.steer * steerLimit;
    this.steer += clamp(target - this.steer, -s.steerSpeed * dt, s.steerSpeed * dt);
    const delta = this.steer;
    const cosD = Math.cos(delta), sinD = Math.sin(delta);

    // --- перераспределение нагрузки по осям ---
    const L = s.wheelbase, W = s.mass * G;
    const transfer = (s.mass * this.axLong * s.cgHeight) / L;
    const Nf = Math.max(W * 0.15, (W * s.cgToRear) / L - transfer);
    const Nr = Math.max(W * 0.15, (W * s.cgToFront) / L + transfer);

    // --- двигатель и трансмиссия ---
    const ratio = rev ? -s.gears[0] : s.gears[this.gear];
    const total = ratio * s.finalDrive;
    const wheelRpm = (vLong / s.wheelRadius) * 60 / (2 * Math.PI);
    const shaftRpm = Math.abs(wheelRpm * total);
    let rpm = shaftRpm;
    // пробуксовка сцепления при трогании
    if (Math.abs(this.gear) === 1) rpm = Math.max(rpm, s.idleRpm + driveIn * 2000);
    rpm = Math.max(rpm, s.idleRpm);

    if (this.shiftTimer > 0) this.shiftTimer -= dt;
    let engineT = this.shiftTimer > 0 ? 0 : driveIn * this.torqueAt(rpm);
    if (rpm > s.revLimit) engineT = 0; // отсечка
    const movingWithGear = vLong * Math.sign(total) > 0.5;
    const engineBrake = movingWithGear && this.shiftTimer <= 0 ? (1 - driveIn) * (rpm / 6000) * 28 : 0;
    const Fdrive = ((engineT - engineBrake) * total * s.drivetrainEff) / s.wheelRadius;

    // автомат: пороги зависят от педали (спокойно — рано, в пол — до 5400 об/мин);
    // пороги разнесены так, чтобы после переключения не было «охоты» передач
    if (this.gear >= 1 && this.shiftTimer <= 0) {
      const up = lerp(3000, s.upshiftRpm, driveIn);
      const down = lerp(1500, s.downshiftRpm + 400, driveIn);
      if (shaftRpm > up && this.gear < s.gears.length - 1 && driveIn > 0.05) {
        this.gear++; this.shiftTimer = s.shiftTime;
      } else if (this.gear > 1) {
        const lowerRpm = shaftRpm * (s.gears[this.gear - 1] / s.gears[this.gear]);
        if (shaftRpm < down && lowerRpm < up - 300) {
          this.gear--; this.shiftTimer = s.shiftTime * 0.6;
        }
      }
    }

    // --- задняя ведущая ось: круг трения ---
    const mu = s.tireGrip * surfaceMu;
    const maxFxR = mu * Nr;
    const FxR = clamp(Fdrive, -maxFxR, maxFxR);
    this.wheelSpin = Math.max(0, Math.abs(Fdrive) / maxFxR - 0.85) * 3;
    const fxRatio = Math.min(Math.abs(Fdrive) / maxFxR, 1);
    let latAvailR = Math.sqrt(Math.max(0.08, 1 - fxRatio * fxRatio * 0.92));
    if (input.handbrake) latAvailR *= s.handbrakeGrip;

    // --- боковой увод шин ---
    const vLatF = vLat - s.cgToFront * this.yawRate;
    const vLatR = vLat + s.cgToRear * this.yawRate;
    const alphaF = Math.atan2(vLatF * cosD - vLong * sinD, Math.max(Math.abs(vLong * cosD + vLatF * sinD), 2.0));
    const alphaR = Math.atan2(vLatR, Math.max(Math.abs(vLong), 2.0));
    const FyF = -mu * Nf * Math.tanh(s.corneringF * alphaF);
    const FyR = -mu * Nr * latAvailR * Math.tanh(s.corneringR * alphaR);

    // --- продольные силы ---
    let Fx = FxR - FyF * sinD;
    Fx += -s.dragCoef * vLong * Math.abs(vLong);
    Fx += -s.rollCoef * vLong * (surfaceMu < 0.8 ? 4 : 1);
    const brakeTotal = brakeIn * s.brakeForce + (input.handbrake ? s.handbrakeForce : 0);
    if (Math.abs(vLong) > 0.01) {
      // не даём тормозу развернуть скорость в обратную сторону
      Fx -= Math.sign(vLong) * Math.min(brakeTotal, (Math.abs(vLong) * s.mass) / dt);
    }
    const Fy = FyR + FyF * cosD;

    // --- момент рыскания ---
    let torque = s.cgToRear * FyR - s.cgToFront * FyF * cosD;
    const yawKin = (-vLong * Math.tan(delta)) / L;
    if (s.stabilityAssist > 0 && speed > 3 && !input.handbrake) {
      torque += (yawKin - this.yawRate) * s.inertia * s.stabilityAssist * 3;
    }

    // --- интегрирование ---
    const ax = Fx / s.mass, ay = Fy / s.mass;
    this.vx += (fx * ax + rx * ay) * dt;
    this.vz += (fz * ax + rz * ay) * dt;
    this.yawRate += (torque / s.inertia) * dt;

    // на малой скорости переходим к кинематической модели (tanh-модель вырождается)
    if (speed < 1.5) {
      const k = 1 - speed / 1.5;
      this.yawRate = lerp(this.yawRate, yawKin, k);
      const vl = this.vx * rx + this.vz * rz;
      this.vx -= rx * vl * k * 0.5;
      this.vz -= rz * vl * k * 0.5;
      if (speed < 0.08 && driveIn < 0.02) { this.vx = 0; this.vz = 0; this.yawRate = 0; }
    }

    this.heading += this.yawRate * dt;
    this.x += this.vx * dt;
    this.z += this.vz * dt;

    // --- выходные данные для звука/камеры/HUD ---
    this.axLong = lerp(this.axLong, ax, 0.08);
    this.ayLat = lerp(this.ayLat, ay, 0.08);
    this.vLong = this.vx * Math.sin(this.heading) + this.vz * Math.cos(this.heading);
    this.vLat = vLat;
    this.speed = Math.hypot(this.vx, this.vz);
    this.slipFront = Math.abs(alphaF);
    this.slipRear = Math.abs(alphaR);
    this.rpm = lerp(this.rpm, Math.min(rpm, s.revLimit + 100), 0.2);
    this.load = driveIn;
    this.braking = brakeIn > 0.1;
  }

  /** Интенсивность визга шин 0..1 */
  get skid() {
    if (this.speed < 2) return Math.min(this.wheelSpin, 1) * 0.6;
    const lat = Math.max(0, this.slipRear - 0.12) * 4 + Math.max(0, this.slipFront - 0.18) * 3;
    return Math.min(1, lat + this.wheelSpin * 0.6);
  }
}
