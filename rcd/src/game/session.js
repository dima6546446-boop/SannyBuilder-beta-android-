// Сессия заезда: физика (фиксированный шаг) + мир + очки + правила режима. Не зависит от рендера — тестируется в Node.
import { PHYS } from '../physics/config.js';
import { Car } from '../physics/car.js';
import { World } from '../physics/world.js';
import { Scoring } from './scoring.js';
import { getMap } from './maps.js';
import { getCar, buildSpec } from './cars.js';

const WEATHER = {
  clear: (t) => t,
  rain: (t) => (t === 'asphalt' || t === 'concrete' ? 'wet' : t),
  snow: (t) => (t === 'asphalt' || t === 'concrete' || t === 'grass' || t === 'wet' ? 'snow' : t),
};

export class Session {
  /**
   * opts: { mapId, carId, tuning, mode: 'free'|'timed'|'challenge', challengeId, weather, assist, transmission }
   */
  constructor(opts) {
    this.opts = opts;
    this.map = getMap(opts.mapId);
    this.world = new World(this.map);
    this.def = getCar(opts.carId);
    this.spec = buildSpec(this.def, opts.tuning || {});
    this.car = new Car(this.spec);
    this.car.assist = opts.assist ?? 0.7;
    this.car.autoGear = (opts.transmission || 'auto') === 'auto';
    this.scoring = new Scoring();
    this.mode = opts.mode || 'free';
    this.challenge = this.mode === 'challenge' ? this.map.challenges.find((c) => c.id === opts.challengeId) : null;
    this.weatherFn = WEATHER[opts.weather || 'clear'] || WEATHER.clear;
    this.surfaceAt = (x, z) => this.weatherFn(this.world.surfaceAt(x, z));
    this.listeners = [];
    this.accum = 0;
    this.time = 0;
    this.finished = false;
    this.success = false;
    this.gateIndex = 0;
    this.timeLimit = this.mode === 'timed' ? this.map.timed.time : (this.challenge ? this.challenge.time : 0);
    this.impactCooldown = 0;
    this.contactTime = 0;
    this.camShake = 0;
    this.lastImpact = null;
    this.restart();
    this._onImpact = (e) => this.handleImpact(e);
  }

  on(f) { this.listeners.push(f); }
  emit(type, data = {}) { for (const f of this.listeners) f({ type, ...data }); }

  restart() {
    const sp = this.map.spawn;
    this.car.reset(sp.x, sp.z, sp.h);
    this.car.assist = this.opts.assist ?? 0.7;
    this.world.resetProps();
    this.scoring.reset();
    this.time = 0; this.finished = false; this.success = false; this.gateIndex = 0; this.accum = 0;
    this.stopTime = 0;
    this.prevX = this.car.x; this.prevZ = this.car.z;
    this.stats = { distance: 0 };
  }

  /** Вернуть машину на дорогу в точке старта, не сбрасывая очки (штраф: серия сгорает). */
  respawn() { const sp = this.map.spawn; this.scoring.hit(99); this.car.reset(sp.x, sp.z, sp.h); this.car.assist = this.opts.assist ?? 0.7; this.emit('respawn'); }

  handleImpact(e) {
    if (e.kind === 'prop') { this.emit('impact', { ...e, light: true }); return; }
    if (this.impactCooldown > 0) { this.impactCooldown = Math.max(this.impactCooldown, 0.08); return; }
    this.impactCooldown = 0.12;
    this.camShake = Math.min(1, this.camShake + e.speed * 0.06);
    this.scoring.hit(e.speed);
    this.lastImpact = e;
    this.emit('impact', e);
  }

  /** Продвинуть симуляцию на frameDt секунд. input — {throttle, brake, steer, handbrake, kick, shiftUp, shiftDown}. */
  update(frameDt, input) {
    if (this.finished) return;
    const dt = 1 / PHYS.stepHz;
    this.accum += Math.min(frameDt, PHYS.maxFrameDt);
    const car = this.car;
    if (input) Object.assign(car.input, input);
    let steps = 0;
    while (this.accum >= dt && steps < 60) {
      this.accum -= dt; steps++;
      this.step(dt);
      if (this.finished) break;
    }
  }

  step(dt) {
    const car = this.car;
    car.step(dt, this.surfaceAt);
    this.impactCooldown = Math.max(0, this.impactCooldown - dt);
    this.camShake = Math.max(0, this.camShake - dt * 2.2);
    this.world.collide(car, dt, this._onImpact);
    this.world.stepProps(dt);
    // мягкий предел карты (на случай просачивания)
    const b = this.map.bounds;
    if (b) { car.x = Math.max(b.x0 + 0.5, Math.min(b.x1 - 0.5, car.x)); car.z = Math.max(b.z0 + 0.5, Math.min(b.z1 - 0.5, car.z)); }
    this.time += dt;
    this.scoring.update(dt, car);
    for (const ev of this.scoring.events.splice(0)) this.emit(ev.type, ev);
    this.stats.distance += Math.hypot(car.x - this.prevX, car.z - this.prevZ);
    this.updateRules(dt);
    this.prevX = car.x; this.prevZ = car.z;
  }

  updateRules(dt) {
    if (this.mode === 'free') return;
    const sc = this.scoring;
    if (this.mode === 'timed') {
      if (this.time >= this.timeLimit) { sc.bank(); this.finish(true); }
      return;
    }
    const c = this.challenge;
    if (!c) return;
    if (this.time >= c.time && c.type !== 'angle') { this.finish(false); return; }
    if (this.time >= c.time) { this.finish(false); return; }
    switch (c.type) {
      case 'score': if (sc.banked + sc.chain >= c.target) { sc.bank(); this.finish(true); } break;
      case 'chain': if (sc.chain >= c.target) { sc.bank(); this.finish(true); } break;
      case 'noHit': if (sc.stats.hits > 0 && sc.banked + sc.chain < c.target) { /* продолжаем, но попытка провалена */ this.finish(false); } else if (sc.banked + sc.chain >= c.target) { sc.bank(); this.finish(true); } break;
      case 'angle': if (sc.angle >= c.target && sc.active) { this.holdT = (this.holdT || 0) + dt; } else this.holdT = 0; if (this.holdT >= c.hold) this.finish(true); break;
      case 'gates': this.updateGates(c); break;
    }
  }

  updateGates(c) {
    const g = this.map.gates[this.gateIndex];
    if (!g) { this.finish(true); return; }
    const car = this.car;
    if (segIntersect(this.prevX, this.prevZ, car.x, car.z, g.x1, g.z1, g.x2, g.z2)) {
      if (c.needDrift && !this.scoring.active) return;
      this.gateIndex++;
      this.emit('gate', { index: this.gateIndex, total: this.map.gates.length });
      if (this.gateIndex >= this.map.gates.length) this.finish(true);
    }
  }

  finish(success) {
    if (this.finished) return;
    this.finished = true; this.success = success;
    this.scoring.bank();
    this.emit('finish', { success });
  }

  /** Данные для экрана результатов. */
  summary() {
    const sc = this.scoring, st = sc.stats;
    const score = Math.round(sc.banked);
    let stars = 0, goal = 0;
    if (this.mode === 'timed') { goal = this.map.timed.goal; stars = score >= goal * 1.5 ? 3 : score >= goal ? 2 : score >= goal * 0.5 ? 1 : 0; }
    return {
      mode: this.mode, mapId: this.map.id, carId: this.def.id, challengeId: this.challenge ? this.challenge.id : null,
      challengeName: this.challenge ? this.challenge.name : null, reward: this.challenge ? this.challenge.reward : null,
      success: this.mode === 'free' ? true : this.success, score, stars, goal,
      bestChain: st.bestChain, bestAngle: st.bestAngle, driftTime: st.driftTime, longestDrift: st.longestDrift, hits: st.hits, lostPoints: Math.round(st.lostPoints),
      topSpeed: st.topSpeed, distance: this.stats.distance, duration: this.time, chains: st.chains, transitions: st.transitions,
      avgAngle: st.driftTime > 0 ? st.angleSum / st.driftTime : 0,
    };
  }
}

function segIntersect(ax, az, bx, bz, cx, cz, dx, dz) {
  const d1x = bx - ax, d1z = bz - az, d2x = dx - cx, d2z = dz - cz;
  const den = d1x * d2z - d1z * d2x;
  if (Math.abs(den) < 1e-9) return false;
  const t = ((cx - ax) * d2z - (cz - az) * d2x) / den;
  const u = ((cx - ax) * d1z - (cz - az) * d1x) / den;
  return t >= 0 && t <= 1 && u >= 0 && u <= 1;
}
