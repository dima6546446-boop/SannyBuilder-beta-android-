import { PHYS, SURFACES } from './config.js';
import { tireForce } from './tire.js';

const TWO_PI = Math.PI * 2;
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const lerp = (a, b, t) => a + (b - a) * t;
const smooth = (t) => { t = clamp(t, 0, 1); return t * t * (3 - 2 * t); };

/**
 * Плоская (x, z) модель заднеприводного автомобиля.
 * Курс h: вперёд = (sin h, cos h), вправо = (cos h, -sin h). Рыскание w > 0 — вправо (по часовой).
 * 4 колеса со своими нагрузками (перенос веса), динамикой пробуксовки, комбинированным скольжением шин.
 */
export class Car {
  constructor(spec) {
    this.tmp = { fx: 0, fy: 0, kappa: 0, alpha: 0, s: 0 };
    this.setSpec(spec);
    this.reset(0, 0, 0);
  }

  setSpec(spec) {
    this.spec = spec;
    const L = spec.wheelbase;
    this.a = L * (1 - spec.weightFront);       // от ЦТ до передней оси
    this.b = L * spec.weightFront;             // от ЦТ до задней оси
    this.Iz = spec.mass * Math.pow(L * PHYS.chassis.inertiaFactor, 2);
    const tf = spec.trackFront / 2, tr = spec.trackRear / 2;
    this.wheelPos = [[-tf, this.a], [tf, this.a], [-tr, -this.b], [tr, -this.b]]; // FL FR RL RR (x вправо, z вперёд)
    this.staticLoad = [
      spec.mass * PHYS.gravity * spec.weightFront * 0.5,
      spec.mass * PHYS.gravity * spec.weightFront * 0.5,
      spec.mass * PHYS.gravity * (1 - spec.weightFront) * 0.5,
      spec.mass * PHYS.gravity * (1 - spec.weightFront) * 0.5,
    ];
  }

  reset(x, z, h) {
    this.x = x; this.z = z; this.h = h;
    this.vx = 0; this.vz = 0; this.w = 0;
    this.wheelW = [0, 0, 0, 0];        // угловые скорости колёс
    this.wheelSpin = [0, 0, 0, 0];     // накопленный угол вращения (визуализация)
    this.steerAngle = 0;
    this.gear = 1; this.rpm = this.spec.idleRpm; this.boost = 0;
    this.shiftTimer = 0; this.limiter = 0; this.kickTimer = 0;
    this.axF = 0; this.ayF = 0;        // сглаженные ускорения в осях кузова
    this.loads = this.staticLoad.slice();
    this.slip = [0, 0, 0, 0];          // интенсивность скольжения 0..1 (дым/следы/звук)
    this.slipAngle = [0, 0, 0, 0];
    this.slipRatio = [0, 0, 0, 0];
    this.contactSurface = ['asphalt', 'asphalt', 'asphalt', 'asphalt'];
    this.throttleApplied = 0;
    this.input = { throttle: 0, brake: 0, steer: 0, handbrake: false, kick: false, shiftUp: false, shiftDown: false };
    this.speed = 0; this.fwdSpeed = 0; this.latSpeed = 0; this.driftAngle = 0; this.yawRate = 0;
    this.pitch = 0; this.roll = 0;
    this.reversing = false;
    this.autoGear = true;
    this.assist = 0.7;   // сила помощника противорулевания 0..1
    this.distance = 0;
    this._kickPrev = false;
    this.mu = 1;
  }

  get forward() { return [Math.sin(this.h), Math.cos(this.h)]; }
  get right() { return [Math.cos(this.h), -Math.sin(this.h)]; }

  engineTorque(rpm, throttle) {
    const s = this.spec;
    let f;
    if (rpm < s.idleRpm) f = 0.55;
    else if (rpm < s.peakRpm) f = lerp(0.68, 1, smooth((rpm - s.idleRpm) / (s.peakRpm - s.idleRpm)));
    else f = lerp(1, 0.66, clamp((rpm - s.peakRpm) / Math.max(200, s.redline - s.peakRpm), 0, 1));
    const turbo = 1 + this.boost * s.turbo;
    return s.peakTorque * f * turbo * throttle;
  }

  /** Один шаг физики. surfaceAt(x, z) -> имя поверхности. */
  step(dt, surfaceAt) {
    const s = this.spec, P = PHYS, T = P.tire, D = P.drivetrain, S = P.steering;
    const inp = this.input;
    const sinH = Math.sin(this.h), cosH = Math.cos(this.h);
    const fx0 = sinH, fz0 = cosH, rx0 = cosH, rz0 = -sinH;

    // --- скорости в осях кузова ---
    const vf = this.vx * fx0 + this.vz * fz0;
    const vr = this.vx * rx0 + this.vz * rz0;
    const speed = Math.hypot(this.vx, this.vz);
    this.speed = speed; this.fwdSpeed = vf; this.latSpeed = vr; this.yawRate = this.w;
    const beta = vf > 1.5 ? Math.atan2(vr, vf) : 0;      // угол заноса: + = движемся правее носа
    this.driftAngle = beta;
    const betaDeg = Math.abs(beta) * 180 / Math.PI;

    // --- руль: ход зависит от скорости, в заносе — полный; помощник противорулевания ---
    const speedFrac = smooth(speed / S.speedRef);
    let lock = lerp(1, S.minLockFrac, speedFrac);
    lock = lerp(lock, 1, smooth((betaDeg - 6) / (S.fullLockSlideDeg - 6)));
    const maxRad = s.maxSteer;
    let target = inp.steer * lock * maxRad;
    if (this.assist > 0 && vf > 2.5) {
      const gate = smooth((betaDeg - S.assistThresholdDeg) / S.assistRangeDeg) * smooth((speed - 3) / 5);
      const betaF = Math.atan2(vr + this.w * this.a, vf);      // направление скорости передней оси относительно носа
      const counter = clamp(betaF * S.assistGain, -maxRad, maxRad);
      const blend = Math.min(1, this.assist * gate * 1.4);
      target = lerp(target, clamp(counter + target * 0.2, -maxRad, maxRad), blend);
    }
    const rate = S.rate * maxRad * (Math.abs(target) > Math.abs(this.steerAngle) ? 1 : 1.25);
    const dSteer = target - this.steerAngle;
    this.steerAngle += clamp(dSteer, -rate * dt, rate * dt);
    const cs = Math.cos(this.steerAngle), sn = Math.sin(this.steerAngle);

    // --- коробка и двигатель ---
    const rearW = (this.wheelW[2] + this.wheelW[3]) * 0.5;
    this.handleGears(dt, vf, speed, betaDeg, inp);
    const ratio = (this.gear > 0 ? s.gears[this.gear - 1] : -s.reverseRatio) * s.finalDrive;
    let engRpm = Math.abs(rearW * ratio) * 60 / TWO_PI;
    const launching = Math.abs(this.gear) === 1 && speed < 5;
    let rpm = engRpm;
    if (launching) rpm = Math.max(engRpm, s.idleRpm + inp.throttle * (D.launchRpm - s.idleRpm));
    rpm = Math.max(rpm, s.idleRpm);
    this.rpm = lerp(this.rpm, rpm, clamp(dt * 22, 0, 1));
    if (this.rpm >= s.redline) this.limiter = 0.06;
    if (this.limiter > 0) this.limiter -= dt;

    // турбина
    const bt = inp.throttle * smooth((this.rpm - s.redline * 0.33) / (s.redline * 0.35));
    this.boost += (bt - this.boost) * clamp(dt * (bt > this.boost ? D.turboSpool : D.turboDecay), 0, 1);

    // момент на колёсах
    let throttle = this.reversing ? inp.brake : inp.throttle;
    this.throttleApplied = throttle;
    let driveTorque = 0;
    if (this.shiftTimer > 0) { this.shiftTimer -= dt; }
    const cut = this.shiftTimer > 0 || this.limiter > 0;
    if (!cut) driveTorque = this.engineTorque(this.rpm, throttle);
    if (this.kickTimer > 0) { this.kickTimer -= dt; driveTorque *= D.clutchKickMul; }
    if (throttle < 0.05 && Math.abs(this.gear) >= 1 && speed > 2) {
      driveTorque -= s.peakTorque * D.engineBrake * clamp(this.rpm / s.redline, 0, 1) * Math.sign(rearW || 1);
    }
    // ограничитель заноса: на очень больших углах мягко убираем момент, чтобы газ в пол не закручивал в вертушку
    if (this.assist > 0 && driveTorque > 0) driveTorque *= 1 - S.angleThrottleCut * this.assist * smooth((betaDeg - S.angleCutStartDeg) / S.angleCutRangeDeg);
    let wheelTorque = driveTorque * ratio * D.efficiency * D.powerMul;
    // скорость-ограничитель: тяга плавно затухает у предела передач
    // (естественное ограничение через обороты отсечки)

    // --- перенос веса ---
    const m = s.mass, L = s.wheelbase, h = s.cgHeight * P.chassis.rollCgScale;
    const dLong = m * this.axF * h / L;                          // ax>0 — разгон: нагрузка на зад
    const frontAxle = m * P.gravity * s.weightFront - dLong;
    const rearAxle = m * P.gravity * (1 - s.weightFront) + dLong;
    const avgTrack = (s.trackFront + s.trackRear) * 0.5;
    const dLat = m * this.ayF * h / avgTrack;                    // ay>0 — к правому борту: нагрузка на левые колёса
    const rf = s.rollFront;
    const axleLoad = [frontAxle * 0.5, frontAxle * 0.5, rearAxle * 0.5, rearAxle * 0.5];
    const latShare = [dLat * rf, -dLat * rf, dLat * (1 - rf), -dLat * (1 - rf)];
    const loads = this.loads;
    for (let i = 0; i < 4; i++) loads[i] = Math.max(this.staticLoad[i] * 0.04, axleLoad[i] + latShare[i]);

    // --- силы по колёсам ---
    let Fbx = 0, Fbz = 0, tau = 0;
    const tmp = this.tmp;
    const diffBiasTotal = [0, 0];
    // дифференциал с блокировкой: больше момента на колесо, которое отстаёт
    const dW = this.wheelW[3] - this.wheelW[2];
    const lock2 = s.diffLock;
    const half = wheelTorque * 0.5;
    const bias = clamp(dW * D.lsdBiasStiffness * lock2, -Math.abs(wheelTorque) * 0.5 * lock2, Math.abs(wheelTorque) * 0.5 * lock2);
    const driveTq = [0, 0, half + bias, half - bias];            // RL получает больше, если RR крутится быстрее

    // самоограничение угла: на больших углах задняя ось «цепляется» сильнее — занос держится, а не уходит в вертушку
    const boost = this.assist > 0 ? S.angleLimitBoost * this.assist * smooth((betaDeg - S.angleLimitStartDeg) / S.angleLimitRangeDeg) : 0;
    const Rw = s.wheelRadius;
    // приведённая инерция двигателя: ведущие колёса «тянут» за собой маховик (иначе обороты взлетают мгновенно)
    const coupled = this.shiftTimer <= 0 ? (launching ? 0.35 : 1) : 0.0;
    const IwRear = D.wheelInertia + 0.5 * D.engineInertia * ratio * ratio * coupled;
    for (let i = 0; i < 4; i++) {
      const front = i < 2;
      const [px, pz] = this.wheelPos[i];
      // скорость точки колеса в осях кузова
      const vbx = vr + this.w * pz;
      const vbz = vf - this.w * px;
      let uLong, uLat;
      if (front) { uLong = vbz * cs + vbx * sn; uLat = vbx * cs - vbz * sn; }
      else { uLong = vbz; uLat = vbx; }

      // поверхность под колесом
      const wx = this.x + fx0 * pz + rx0 * px, wz = this.z + fz0 * pz + rz0 * px;
      const surfName = surfaceAt ? surfaceAt(wx, wz) : 'asphalt';
      this.contactSurface[i] = surfName;
      const surf = SURFACES[surfName] || SURFACES.asphalt;
      const mu = T.muScale * s.tireGrip * (front ? 1 : s.rearGripBias) * surf.grip;
      this.mu = mu;

      // тормозной момент
      let brakeTq = 0;
      const brakeInput = this.reversing ? 0 : inp.brake;
      brakeTq += brakeInput * s.brakeTorque * (front ? P.brakeBias : 1 - P.brakeBias) * 0.5;
      const hb = !front && inp.handbrake;
      if (hb) brakeTq += P.handbrakeTorque * 0.5;

      const Nz = loads[i];
      // силы при текущей скорости колеса
      let w = this.wheelW[i];
      tireForce(tmp, Nz, mu, uLong, uLat, w * Rw, !front, boost);
      let fx = tmp.fx, fy = tmp.fy;
      const kappa = tmp.kappa;
      // защита боковой силы от «перерегулирования»
      const meff = m * 0.25;
      const cap = Math.abs(uLat) * meff / dt * T.latForceCap;
      if (Math.abs(fy) > cap) fy = Math.sign(fy) * cap;

      // динамика колеса (полунеявная): dω = (T_drive - T_brake*sgn - Fx*R) dt / I
      const eps = 0.25;
      tireForce(tmp, Nz, mu, uLong, uLat, w * Rw + eps, !front, boost);
      const dFx = (tmp.fx - fx) / eps;                       // dFx / d(скорость колеса)
      const free = (!front || true);
      let drive = driveTq[i];
      let net = drive - fx * Rw;
      const Iw = front ? D.wheelInertia : IwRear;
      const denom = Iw + dt * Rw * Rw * Math.max(0, dFx);
      let wNew = w + dt * net / denom;
      if (brakeTq > 0) {
        // тормоз не может развернуть колесо: гасит вращение до нуля
        const sgn = Math.sign(wNew);
        const dec = brakeTq * dt / (front ? D.wheelInertia : IwRear);
        if (hb) wNew = 0;
        else if (Math.abs(wNew) <= dec) wNew = 0;
        else wNew -= sgn * dec;
      }
      // свободное качение передних колёс при отсутствии тормоза тоже идёт по этой же динамике
      this.wheelW[i] = wNew;
      this.wheelSpin[i] += wNew * dt;

      // пересчёт силы по новой скорости колеса для согласованности импульса
      tireForce(tmp, Nz, mu, uLong, uLat, wNew * Rw, !front, boost);
      fx = tmp.fx; if (Math.abs(tmp.fy) < Math.abs(fy)) fy = tmp.fy; else fy = Math.sign(tmp.fy) * Math.min(Math.abs(tmp.fy), cap);
      this.slipAngle[i] = tmp.alpha;
      this.slipRatio[i] = tmp.kappa;
      const target2 = Math.max(clamp((Math.abs(tmp.alpha) - 0.10) / 0.22, 0, 1) * clamp(Math.abs(uLat) / 2.2, 0, 1), clamp((Math.abs(tmp.kappa) - 0.25) / 0.5, 0, 1) * clamp(speed / 3, 0, 1));
      this.slip[i] += (target2 - this.slip[i]) * clamp(dt * (target2 > this.slip[i] ? 14 : 5), 0, 1);

      // в оси кузова
      let bx, bz;
      if (front) { bx = fy * cs + fx * sn; bz = fx * cs - fy * sn; } else { bx = fy; bz = fx; }
      Fbx += bx; Fbz += bz;
      tau += pz * bx - px * bz;
    }

    // --- помощник стабилизации рыскания (мягко ловит вращение, не мешает контролируемому заносу) ---
    if (this.assist > 0 && speed > 6 && vf > 0) {
      const excess = clamp((betaDeg - S.spinGuardDeg) / 25, 0, 1);
      const sameDir = Math.sign(this.w) === -Math.sign(beta) ? 1 : 0;
      tau += -this.w * this.Iz * S.yawDampGain * 22 * excess * sameDir * this.assist;
    }
    if (this.assist > 0 && speed > 4) {
      // мягкий потолок скорости рыскания: не даёт машине закручиваться в вертушку, но не мешает держать угол
      const over = Math.abs(this.w) - S.yawSoftMax * clamp(speed / 14, 0.5, 1.15);
      if (over > 0) tau -= Math.sign(this.w) * over * this.Iz * S.yawSoftGain * this.assist;
    }
    tau -= this.w * this.Iz * P.chassis.yawDamping;

    // --- сопротивление ---
    const surfAvg = (SURFACES[this.contactSurface[2]]?.roll ?? 1);
    let Fx = Fbx * rx0 + Fbz * fx0, Fz = Fbx * rz0 + Fbz * fz0;
    if (speed > 0.05) {
      const drag = 0.5 * P.airDensity * s.cdA * speed;
      Fx -= drag * this.vx; Fz -= drag * this.vz;
      const roll = P.rollingResistance * surfAvg * m * P.gravity;
      const k = Math.min(roll, m * speed / dt * 0.5) / speed;
      Fx -= k * this.vx; Fz -= k * this.vz;
    }
    // прижимная сила / «липкость» на высокой скорости
    // интеграция
    const ax = Fx / m, az = Fz / m;
    this.vx += ax * dt; this.vz += az * dt;
    this.w += tau / this.Iz * dt;
    this.h += this.w * dt;
    this.x += this.vx * dt; this.z += this.vz * dt;
    this.distance += speed * dt;

    // сглаженные ускорения в осях кузова (для переноса веса и крена)
    const aF = ax * fx0 + az * fz0, aR = ax * rx0 + az * rz0;
    const f = clamp(dt / P.chassis.loadFilterTau, 0, 1);
    this.axF += (aF - this.axF) * f;
    this.ayF += (aR - this.ayF) * f;
    // визуальный крен/тангаж (рад)
    this.pitch = clamp(-this.axF * 0.008, -0.07, 0.07);
    this.roll = clamp(this.ayF * 0.006, -0.08, 0.08);
  }

  handleGears(dt, vf, speed, betaDeg, inp) {
    const s = this.spec, D = PHYS.drivetrain;
    // задний ход: тормоз с места
    if (!this.reversing && inp.brake > 0.2 && inp.throttle < 0.05 && vf < 0.8) { this.reversing = true; this.gear = -1; }
    if (this.reversing && inp.throttle > 0.1 && vf > -1.0) { this.reversing = false; this.gear = 1; }
    if (this.reversing) { this.gear = -1; return; }
    if (this.gear < 1) this.gear = 1;
    // сцепление-«пинок»
    if (inp.kick && !this._kickPrev) this.kickTimer = D.clutchKickTime;
    this._kickPrev = inp.kick;

    // решение о переключении принимаем по скорости автомобиля, а не по оборотам при пробуксовке
    const rpm = Math.max(s.idleRpm, Math.abs(vf) / s.wheelRadius * s.gears[this.gear - 1] * s.finalDrive * 60 / TWO_PI);
    const holdGear = betaDeg > 10 && inp.throttle > 0.5;
    if (this.autoGear) {
      this.gearCool = Math.max(0, (this.gearCool || 0) - dt);
      if (this.shiftTimer <= 0 && this.gearCool <= 0) {
        const up = s.redline * (holdGear ? 0.95 : (inp.throttle > 0.85 ? D.upshiftRpmFrac : 0.82));
        const down = s.redline * (inp.throttle > 0.9 ? 0.5 : D.downshiftRpmFrac);
        if (rpm > up && this.gear < s.gears.length) { this.gear++; this.shiftTimer = D.shiftTime; this.gearCool = 0.5; this.onShift && this.onShift(1); }
        else if (this.gear > 1 && rpm < down && !holdGear) {
          this.gear--; this.shiftTimer = D.shiftTime * 0.6; this.gearCool = 0.5; this.onShift && this.onShift(-1);
        }
      }
    } else {
      if (inp.shiftUp && this.gear < s.gears.length && this.shiftTimer <= 0) { this.gear++; this.shiftTimer = D.shiftTime; this.onShift && this.onShift(1); }
      if (inp.shiftDown && this.gear > 1 && this.shiftTimer <= 0) { this.gear--; this.shiftTimer = D.shiftTime * 0.6; this.onShift && this.onShift(-1); }
      inp.shiftUp = inp.shiftDown = false;
    }
  }
}
