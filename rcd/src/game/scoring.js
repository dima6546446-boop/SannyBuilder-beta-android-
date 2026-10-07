// Очки дрифта: угол × скорость × длительность, множитель растёт с длиной серии и за смены направления.
export const SCORE = {
  minAngleDeg: 10,       // меньше — не дрифт
  maxAngleDeg: 115,      // больше — вертушка
  minSpeedKmh: 28,
  graceTime: 0.9,        // перерыв в заносе, не обрывающий серию (с)
  multStepTime: 2.4,     // каждые N секунд серии +0.5 к множителю
  multStep: 0.5,
  multMax: 8,
  transitionBonus: 0.75, // смена направления заноса
  rate: 0.5,             // общий коэффициент
  hitMinSpeed: 1.4,      // м/с — удар слабее не обнуляет серию
};

export class Scoring {
  constructor() { this.reset(); }

  reset() {
    this.banked = 0;       // засчитанные очки
    this.chain = 0;        // очки текущей серии (ещё не засчитаны)
    this.mult = 1;
    this.chainTime = 0;
    this.gap = 0;
    this.active = false;
    this.angle = 0;        // текущий угол, градусы (абс.)
    this.signedAngle = 0;
    this.lastSign = 0;
    this.holdAngleTime = 0;
    this.rate = 0;         // очки/с сейчас
    this.stats = { bestChain: 0, bestAngle: 0, driftTime: 0, hits: 0, lostPoints: 0, longestDrift: 0, chains: 0, transitions: 0, topSpeed: 0, angleSum: 0 };
    this._cur = 0;
    this.events = [];
  }

  emit(type, data) { this.events.push({ type, ...data }); }

  /** Вызывается каждый шаг физики. */
  update(dt, car) {
    const kmh = car.speed * 3.6;
    this.stats.topSpeed = Math.max(this.stats.topSpeed, kmh);
    const beta = car.fwdSpeed > 1 ? Math.atan2(car.latSpeed, car.fwdSpeed) : (car.fwdSpeed < -1 ? 0 : 0);
    const a = Math.abs(beta) * 180 / Math.PI;
    this.signedAngle = beta * 180 / Math.PI * (car.w >= 0 ? 1 : 1);
    this.angle = a;
    const driftNow = kmh >= SCORE.minSpeedKmh && a >= SCORE.minAngleDeg && a <= SCORE.maxAngleDeg && car.fwdSpeed > 0.5;
    if (driftNow) {
      if (!this.active) { this.active = true; }
      this.gap = 0;
      this.chainTime += dt;
      this._cur += dt;
      this.stats.driftTime += dt;
      this.stats.angleSum += a * dt;
      this.stats.bestAngle = Math.max(this.stats.bestAngle, a);
      this.stats.longestDrift = Math.max(this.stats.longestDrift, this._cur);
      if (a >= 0) this.holdAngleTime += dt;
      const sign = Math.sign(beta);
      if (this.lastSign !== 0 && sign !== this.lastSign && a > SCORE.minAngleDeg) {
        this.mult = Math.min(SCORE.multMax, this.mult + SCORE.transitionBonus);
        this.stats.transitions++;
        this.emit('transition', { mult: this.mult });
      }
      this.lastSign = sign;
      const steps = Math.floor(this.chainTime / SCORE.multStepTime);
      const base = 1 + steps * SCORE.multStep;
      if (base > this.mult) { this.mult = Math.min(SCORE.multMax, base); this.emit('mult', { mult: this.mult }); }
      this.rate = Math.pow(a, 1.1) / 30 * kmh * SCORE.rate;
      this.chain += this.rate * this.mult * dt;
      this.stats.bestChain = Math.max(this.stats.bestChain, this.chain);
    } else {
      this.holdAngleTime = 0;
      this.rate = 0;
      this._cur = 0;
      if (this.chain > 0 || this.chainTime > 0) {
        this.gap += dt;
        // остановка машины завершает серию сразу
        if (this.gap >= SCORE.graceTime || kmh < 6) this.bank();
      }
      this.active = false;
    }
  }

  /** Засчитать серию. */
  bank() {
    if (this.chain > 0) {
      const pts = Math.round(this.chain);
      this.banked += pts;
      this.stats.chains++;
      this.emit('bank', { points: pts, mult: this.mult, time: this.chainTime });
    }
    this.chain = 0; this.mult = 1; this.chainTime = 0; this.gap = 0; this.lastSign = 0;
  }

  /** Удар: серия сгорает. */
  hit(speed) {
    if (speed < SCORE.hitMinSpeed) return;
    this.stats.hits++;
    if (this.chain > 0) {
      this.stats.lostPoints += this.chain;
      this.emit('lost', { points: Math.round(this.chain) });
    }
    this.chain = 0; this.mult = 1; this.chainTime = 0; this.gap = 0; this.lastSign = 0; this.active = false;
  }

  get total() { return Math.round(this.banked + this.chain); }
}
