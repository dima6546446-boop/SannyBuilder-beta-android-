import { clamp, lerp } from '../utils/math.js';

const G = 9.81;
const STEP = 1 / 120; // фиксированный шаг физики

/**
 * Кривая момента по пиковому значению: форма типичного атмосферного мотора ВАЗ.
 * Для 2107 даёт ≈104 Н·м @ 3400 и ≈71 л.с. на 6000 об/мин.
 */
function makeTorqueCurve(peak, peakRpm, redline) {
  const mid = (peakRpm + redline) / 2;
  return [
    [800, 0.68], [1500, 0.84], [Math.min(2500, peakRpm - 400), 0.95], [peakRpm, 1.0],
    [mid, 0.93], [redline, 0.78], [redline + 400, 0.5],
  ].map(([r, k]) => [r, peak * k]);
}

/** Спецификация физики из описания модели и тюнинга. */
export function makeSpec(def, tuning = {}) {
  const s = def.spec, d = def.dims;
  const eng = tuning.engine ?? 1, grip = tuning.tires ?? 1, lower = tuning.height ?? 0;
  const L = d.axleF - d.axleR;
  return {
    name: def.name,
    mass: s.mass + 75,
    inertia: (s.mass + 75) * ((d.front - d.rear) ** 2 + d.W ** 2) / 12 * 0.95,
    wheelbase: L,
    cgToFront: d.axleF,
    cgToRear: -d.axleR,
    cgHeight: Math.max(0.35, s.cgHeight + lower * 1.5),
    wheelRadius: d.wheelR,
    maxSteer: s.maxSteer,
    steerSpeed: 3.2,
    torqueCurve: makeTorqueCurve(s.torque * eng, s.peakRpm, s.redline),
    idleRpm: 850,
    revLimit: s.redline + 300,
    gears: s.gears,
    finalDrive: s.final,
    drivetrainEff: s.drive === 'AWD' ? 0.82 : 0.88,
    drive: s.drive,
    upshiftRpm: s.redline - 700,
    downshiftRpm: 2000,
    shiftTime: 0.3,
    brakeForce: s.brake,
    handbrakeForce: s.mass * 4,
    dragCoef: s.drag,
    rollCoef: 0.013,
    tireGrip: s.grip * grip,
    corneringF: 9.0 * (lower < 0 ? 1.08 : 1),
    corneringR: 10.0 * (lower < 0 ? 1.08 : 1),
    handbrakeGrip: 0.35,
    stabilityAssist: 0.35,
    tank: s.tank,
  };
}

/**
 * Физика автомобиля: модель «2 оси» с шинами (Fy = −μN·tanh(k·α)), переносом веса,
 * кругом трения на ведущей оси (RWD → занос под газом, FWD → снос передка,
 * AWD — делит тягу 50/50), ручником, двигателем с кривой момента, КПП
 * (автомат с селектором R/N/D или механика 1–5), расходом топлива.
 *
 * Курс heading: вперёд = (sin h, cos h), вправо = (−cos h, sin h); yawRate > 0 — влево.
 */
export class VehiclePhysics {
  constructor(spec) {
    this.spec = spec;
    this.x = 0; this.z = 0; this.heading = 0;
    this.vx = 0; this.vz = 0; this.yawRate = 0;
    this.steer = 0;
    this.selector = 'D';   // автомат: 'D' | 'N' | 'R'
    this.manual = false;   // механика: gear −1..n, 0 = нейтраль
    this.gear = 1;
    this.rpm = 850;
    this.shiftTimer = 0;
    this.axLong = 0; this.ayLat = 0;
    this.vLong = 0; this.vLat = 0; this.speed = 0;
    this.slipRear = 0; this.slipFront = 0; this.wheelSpin = 0;
    this.load = 0;
    this.braking = false;
    this.reversing = false;
    this.fuel = spec.tank * 0.7;
    this.odometer = 0;
    this._acc = 0;
  }

  setSpec(spec) {
    this.spec = spec;
    this.fuel = Math.min(this.fuel, spec.tank);
    this.gear = Math.min(this.gear, spec.gears.length - 1);
  }

  reset(x, z, heading) {
    Object.assign(this, { x, z, heading, vx: 0, vz: 0, yawRate: 0, steer: 0, axLong: 0, ayLat: 0, speed: 0, vLong: 0, _acc: 0 });
    this.gear = 1;
    if (!this.manual) this.selector = 'D';
  }

  // --- управление КПП
  setSelector(sel) {
    if (this.manual) return;
    if (this.selector === sel) return;
    this.selector = sel;
    this.gear = sel === 'R' ? -1 : sel === 'N' ? 0 : 1;
    this.shiftTimer = 0.25;
  }

  setManual(on) {
    this.manual = on;
    if (!on) { const g = this.gear; this.selector = ''; this.setSelector(g < 0 ? 'R' : g === 0 ? 'N' : 'D'); }
  }

  shiftUp() {
    if (!this.manual) return;
    if (this.gear < this.spec.gears.length - 1) { this.gear++; this.shiftTimer = this.spec.shiftTime; }
  }

  shiftDown() {
    if (!this.manual) return;
    if (this.gear > -1) {
      if (this.gear === 0 && this.vLong > 1.5) return; // задняя на ходу не включается
      this.gear--; this.shiftTimer = this.spec.shiftTime;
    }
  }

  get gearLabel() {
    if (!this.manual) return this.selector === 'D' ? `D${this.gear}` : this.selector;
    return this.gear < 0 ? 'R' : this.gear === 0 ? 'N' : String(this.gear);
  }

  torqueAt(rpm) {
    const c = this.spec.torqueCurve;
    if (rpm <= c[0][0]) return c[0][1];
    for (let i = 1; i < c.length; i++) {
      if (rpm <= c[i][0]) return lerp(c[i - 1][1], c[i][1], (rpm - c[i - 1][0]) / (c[i][0] - c[i - 1][0]));
    }
    return c[c.length - 1][1];
  }

  update(dt, input, surface) {
    this._acc += dt;
    let steps = 0;
    while (this._acc >= STEP && steps < 12) {
      this._step(STEP, input, surface.mu);
      this._acc -= STEP;
      steps++;
    }
    if (steps === 12) this._acc = 0; // защита от «спирали смерти» на слабых телефонах
  }

  _step(dt, input, surfaceMu) {
    const s = this.spec;
    const h = this.heading;
    const fx = Math.sin(h), fz = Math.cos(h);
    const rx = -fz, rz = fx;
    const vLong = this.vx * fx + this.vz * fz;
    const vLat = this.vx * rx + this.vz * rz;
    const speed = Math.hypot(this.vx, this.vz);
    const hasFuel = this.fuel > 0;

    const driveIn = hasFuel ? input.throttle : 0;
    const brakeIn = input.brake;

    // --- руль: предел угла ≈ предел сцепления (v²·δ/L ≤ μg) с запасом для заноса
    const steerLimit = clamp(26 / (vLong * vLong + 1), 0.03, s.maxSteer);
    this.steer += clamp(input.steer * steerLimit - this.steer, -s.steerSpeed * dt, s.steerSpeed * dt);
    const delta = this.steer;
    const cosD = Math.cos(delta), sinD = Math.sin(delta);

    // --- нагрузка на оси
    const L = s.wheelbase, W = s.mass * G;
    const transfer = (s.mass * this.axLong * s.cgHeight) / L;
    const Nf = Math.max(W * 0.15, (W * s.cgToRear) / L - transfer);
    const Nr = Math.max(W * 0.15, (W * s.cgToFront) / L + transfer);

    // --- двигатель и КПП
    const gear = this.gear;
    const ratio = gear < 0 ? -s.gears[0] : gear === 0 ? 0 : s.gears[gear];
    const total = ratio * s.finalDrive;
    const wheelRpm = (vLong / s.wheelRadius) * 60 / (2 * Math.PI);
    const shaftRpm = Math.abs(wheelRpm * total);
    let rpm;
    if (gear === 0) rpm = s.idleRpm + driveIn * (s.revLimit - s.idleRpm); // перегазовка на нейтрали
    else {
      rpm = shaftRpm;
      if (Math.abs(gear) === 1) rpm = Math.max(rpm, s.idleRpm + driveIn * 2000); // пробуксовка сцепления
      rpm = Math.max(rpm, s.idleRpm);
    }
    if (this.shiftTimer > 0) this.shiftTimer -= dt;
    let engineT = this.shiftTimer > 0 || gear === 0 ? 0 : driveIn * this.torqueAt(rpm);
    if (rpm > s.revLimit) engineT = 0;
    const movingWithGear = gear !== 0 && vLong * Math.sign(total) > 0.5;
    const engineBrake = movingWithGear && this.shiftTimer <= 0 ? (1 - driveIn) * (rpm / 6000) * 28 : 0;
    const Fdrive = gear === 0 ? 0 : ((engineT - engineBrake) * total * s.drivetrainEff) / s.wheelRadius;

    // автомат: пороги зависят от педали, разнесены против «охоты» передач
    if (!this.manual && this.selector === 'D' && gear >= 1 && this.shiftTimer <= 0) {
      const up = lerp(3000, s.upshiftRpm, driveIn);
      const down = lerp(1500, s.downshiftRpm + 400, driveIn);
      if (shaftRpm > up && gear < s.gears.length - 1 && driveIn > 0.05) {
        this.gear++; this.shiftTimer = s.shiftTime;
      } else if (gear > 1) {
        const lowerRpm = shaftRpm * (s.gears[gear - 1] / s.gears[gear]);
        if (shaftRpm < down && lowerRpm < up - 300) { this.gear--; this.shiftTimer = s.shiftTime * 0.6; }
      }
    }

    // --- тяга по осям и круг трения
    const mu = s.tireGrip * surfaceMu;
    const maxF = mu * Nf, maxR = mu * Nr;
    let FxF = 0, FxR = 0, usedF = 0, usedR = 0;
    if (s.drive === 'RWD') { FxR = clamp(Fdrive, -maxR, maxR); usedR = Math.abs(Fdrive) / maxR; }
    else if (s.drive === 'FWD') { FxF = clamp(Fdrive, -maxF, maxF); usedF = Math.abs(Fdrive) / maxF; }
    else {
      FxF = clamp(Fdrive * 0.5, -maxF, maxF); FxR = clamp(Fdrive * 0.5, -maxR, maxR);
      usedF = Math.abs(Fdrive * 0.5) / maxF; usedR = Math.abs(Fdrive * 0.5) / maxR;
    }
    this.wheelSpin = Math.max(0, Math.max(usedF, usedR) - 0.85) * 3;
    const circle = (u) => Math.sqrt(Math.max(0.08, 1 - Math.min(u, 1) ** 2 * 0.92));
    let latAvailR = circle(usedR);
    const latAvailF = circle(usedF);
    if (input.handbrake) latAvailR *= s.handbrakeGrip;

    // --- увод шин
    const vLatF = vLat - s.cgToFront * this.yawRate;
    const vLatR = vLat + s.cgToRear * this.yawRate;
    const alphaF = Math.atan2(vLatF * cosD - vLong * sinD, Math.max(Math.abs(vLong * cosD + vLatF * sinD), 2.0));
    const alphaR = Math.atan2(vLatR, Math.max(Math.abs(vLong), 2.0));
    const FyF = -mu * Nf * latAvailF * Math.tanh(s.corneringF * alphaF);
    const FyR = -mu * Nr * latAvailR * Math.tanh(s.corneringR * alphaR);

    // --- продольные силы (тяга передних колёс повёрнута на угол руля)
    let Fx = FxR + FxF * cosD - FyF * sinD;
    Fx += -s.dragCoef * vLong * Math.abs(vLong);
    // сопротивление качению: Crr·m·g (≈0.013), на траве — в 4 раза больше
    Fx += -s.rollCoef * s.mass * G * Math.tanh(vLong * 2) * (surfaceMu < 0.8 ? 4 : 1);
    const brakeTotal = brakeIn * s.brakeForce + (input.handbrake ? s.handbrakeForce : 0);
    if (Math.abs(vLong) > 0.01) Fx -= Math.sign(vLong) * Math.min(brakeTotal, (Math.abs(vLong) * s.mass) / dt);
    const FyFront = FyF * cosD + FxF * sinD;
    const Fy = FyR + FyFront;

    // --- рыскание
    let torque = s.cgToRear * FyR - s.cgToFront * FyFront;
    const yawKin = (-vLong * Math.tan(delta)) / L;
    if (s.stabilityAssist > 0 && speed > 3 && !input.handbrake) {
      torque += (yawKin - this.yawRate) * s.inertia * s.stabilityAssist * 3;
    }

    // --- интегрирование
    const ax = Fx / s.mass, ay = Fy / s.mass;
    this.vx += (fx * ax + rx * ay) * dt;
    this.vz += (fz * ax + rz * ay) * dt;
    this.yawRate += (torque / s.inertia) * dt;

    if (speed < 1.5) {
      const k = 1 - speed / 1.5;
      this.yawRate = lerp(this.yawRate, yawKin, k);
      const vl = this.vx * rx + this.vz * rz;
      this.vx -= rx * vl * k * 0.5;
      this.vz -= rz * vl * k * 0.5;
      if (speed < 0.08 && (driveIn < 0.02 || gear === 0)) { this.vx = 0; this.vz = 0; this.yawRate = 0; }
    }

    this.heading += this.yawRate * dt;
    this.x += this.vx * dt;
    this.z += this.vz * dt;

    // --- расход топлива (л): холостой ход + пропорционально мощности
    const power = Math.max(0, engineT) * rpm / 9549; // кВт
    this.fuel = Math.max(0, this.fuel - (0.00012 * (rpm / 850) + power * 0.00008) * dt);
    this.odometer += speed * dt;

    // --- выходы
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
    this.reversing = this.gear < 0;
  }

  /** Интенсивность визга шин 0..1 */
  get skid() {
    if (this.speed < 2) return Math.min(this.wheelSpin, 1) * 0.6;
    const lat = Math.max(0, this.slipRear - 0.12) * 4 + Math.max(0, this.slipFront - 0.18) * 3;
    return Math.min(1, lat + this.wheelSpin * 0.6);
  }
}
