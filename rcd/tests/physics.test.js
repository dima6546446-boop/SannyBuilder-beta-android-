import { describe, it, expect } from 'vitest';
import { makeCar, setSpeed } from './harness.mjs';
import { Car } from '../src/physics/car.js';
import { Session } from '../src/game/session.js';
import { CARS, buildSpec } from '../src/game/cars.js';

const DT = 1 / 240;
function sim(car, seconds, drive, surf = () => 'asphalt') {
  const log = [];
  for (let i = 0; i < seconds * 240; i++) {
    const t = i * DT; drive(t, car); car.step(DT, surf);
    if (i % 24 === 0) log.push({ t, kmh: car.speed * 3.6, beta: car.driftAngle * 57.2958, w: car.w * 57.2958, rpm: car.rpm, gear: car.gear });
  }
  return log;
}

describe('физика машины', () => {
  it('разгоняется с места до 100 км/ч за разумное время (все машины)', () => {
    for (const def of CARS) {
      const c = makeCar(def.id);
      let t100 = null;
      sim(c, 14, (t, c) => { c.input.throttle = 1; if (t100 === null && c.speed * 3.6 >= 100) t100 = t; });
      expect(t100, def.id).not.toBeNull();
      expect(t100, def.id).toBeGreaterThan(2.5);
      expect(t100, def.id).toBeLessThan(12.5);
    }
  });

  it('едет по прямой без ухода (нет паразитного рыскания)', () => {
    const c = makeCar('kopeyka');
    const log = sim(c, 8, (t, c) => { c.input.throttle = 1; });
    for (const r of log) { expect(Math.abs(r.w)).toBeLessThan(2); expect(Math.abs(r.beta)).toBeLessThan(2); }
  });

  it('тормозит до остановки без отката и без вращения', () => {
    const c = makeCar('kopeyka'); setSpeed(c, 25);
    const log = sim(c, 2.6, (t, c) => { c.input.brake = 1; c.input.throttle = 0; });
    const last = log[log.length - 1];
    expect(last.kmh).toBeLessThan(25);
    for (const r of log) expect(Math.abs(r.w)).toBeLessThan(15);
  });

  it('поворот на умеренной скорости устойчив (нет вертушки)', () => {
    const c = makeCar('kopeyka'); setSpeed(c, 15);
    const log = sim(c, 5, (t, c) => { c.input.throttle = 0.25; c.input.steer = 0.2; });
    for (const r of log) expect(Math.abs(r.beta)).toBeLessThan(25);
  });

  it('газ + руль в повороте срывает зад: возникает угол заноса', () => {
    const c = makeCar('kupe86'); setSpeed(c, 22);
    let maxBeta = 0;
    sim(c, 3, (t, c) => { c.input.throttle = 1; c.input.steer = 0.5; c.input.kick = t < 0.4; });
    const log = sim(c, 3, (t, c) => { c.input.throttle = 1; c.input.steer = 0.4; });
    for (const r of log) maxBeta = Math.max(maxBeta, Math.abs(r.beta));
    expect(maxBeta).toBeGreaterThan(8);
  });

  it('ручник вводит в занос, помощник удерживает угол 20–70° без вертушки', () => {
    for (const id of ['kopeyka', 'kupe86', 'barin', 'ronin']) {
      const c = makeCar(id); setSpeed(c, 22);
      const log = sim(c, 6, (t, c) => {
        c.input.steer = -0.5; c.input.throttle = t < 0.3 ? 0 : 0.85; c.input.handbrake = t > 0.35 && t < 0.6;
      });
      const peak = Math.max(...log.map((r) => Math.abs(r.beta)));
      expect(peak, id).toBeGreaterThan(20);
      const after = log.filter((r) => r.t > 2.0);
      // не остановились и не крутимся на месте
      expect(Math.min(...after.map((r) => r.kmh)), id).toBeGreaterThan(12);
      expect(Math.max(...after.map((r) => Math.abs(r.w))), id).toBeLessThan(140);
    }
  });

  it('занос можно удерживать несколько секунд (угол ≥ 20° не менее 2 с подряд)', () => {
    const c = makeCar('kopeyka'); setSpeed(c, 22);
    const log = sim(c, 8, (t, c) => { c.input.steer = -0.5; c.input.throttle = t < 0.3 ? 0 : 0.85; c.input.handbrake = t > 0.35 && t < 0.6; });
    let best = 0, cur = 0;
    for (const r of log) { if (Math.abs(r.beta) >= 20) { cur += 0.1; best = Math.max(best, cur); } else cur = 0; }
    expect(best).toBeGreaterThanOrEqual(2);
  });

  it('«клавиатурный» игрок: после входа в занос отпустил руль / держит руль — машина не уходит в вертушку и не встаёт', () => {
    const entry = (t, c) => { if (t < 0.35) { c.input.steer = -0.5; c.input.throttle = 0; c.input.handbrake = false; } else if (t < 0.6) { c.input.steer = -0.5; c.input.throttle = 0.6; c.input.handbrake = true; } else c.input.handbrake = false; };
    for (const def of CARS) {
      for (const [name, after, minV] of [['руль в центр, газ 1', (c) => { c.input.steer = 0; c.input.throttle = 1; }, 40], ['держит руль, газ 0.5', (c) => { c.input.steer = -0.4; c.input.throttle = 0.5; }, 25]]) {
        const c = makeCar(def.id); setSpeed(c, 22);
        const log = sim(c, 8, (t, c) => { entry(t, c); if (t >= 0.6) after(c); });
        const late = log.filter((r) => r.t > 2.5);
        expect(Math.min(...late.map((r) => r.kmh)), def.id + ' ' + name).toBeGreaterThan(minV);
        expect(Math.max(...log.map((r) => Math.abs(r.beta))), def.id + ' ' + name).toBeLessThan(75);
      }
    }
  });

  it('после сброса газа и руля в центр занос плавно выходит (угол < 10° за 3 с)', () => {
    for (const id of ['kopeyka', 'kupe86', 'barin']) {
      const c = makeCar(id); setSpeed(c, 22);
      const log = sim(c, 5, (t, c) => { if (t < 0.35) { c.input.steer = -0.5; c.input.throttle = 0; } else if (t < 0.6) { c.input.steer = -0.5; c.input.throttle = 0.6; c.input.handbrake = true; } else { c.input.handbrake = false; c.input.steer = 0; c.input.throttle = 0; } });
      const tail = log.filter((r) => r.t > 3.4);
      expect(Math.max(...tail.map((r) => Math.abs(r.beta))), id).toBeLessThan(10);
    }
  });

  it('покрытие меняет сцепление: на снегу тормозной путь длиннее', () => {
    const dist = (surf) => { const c = makeCar('kopeyka'); setSpeed(c, 20); const z0 = c.z; let stopped = false; for (let i = 0; i < 240 * 8 && !stopped; i++) { c.input.brake = 1; c.step(DT, () => surf); if (c.speed < 0.5) stopped = true; } return c.z - z0; };
    expect(dist('snow')).toBeGreaterThan(dist('asphalt') * 1.5);
  });

  it('фиксированный шаг: результат не зависит от частоты кадров', () => {
    const run = (fps) => {
      const s = new Session({ mapId: 'parking', carId: 'kopeyka', mode: 'free' });
      const dt = 1 / fps;
      for (let i = 0; i < fps * 3; i++) s.update(dt, { throttle: 1, steer: 0 });
      return s.car.z;
    };
    const a = run(30), b = run(60), c = run(144);
    expect(Math.abs(a - b)).toBeLessThan(0.6);
    expect(Math.abs(b - c)).toBeLessThan(0.6);
  });

  it('разные машины ведут себя по-разному: динамика и характер заноса', () => {
    const tv = (id, v) => { const c = makeCar(id); let t = 99; sim(c, 20, (tt, c) => { c.input.throttle = 1; if (t === 99 && c.speed * 3.6 >= v) t = tt; }); return t; };
    expect(tv('raketa', 150)).toBeLessThan(tv('kopeyka', 150) - 3);
    // тяжёлый «Барин» вращается спокойнее, чем лёгкая «Девятка» и злой «Ронин»
    const peakYaw = (id) => { const c = makeCar(id); setSpeed(c, 22); const log = sim(c, 3, (t, c) => { c.input.steer = -0.5; c.input.throttle = t < 0.3 ? 0 : 0.85; c.input.handbrake = t > 0.35 && t < 0.6; }); return Math.max(...log.map((r) => Math.abs(r.w))); };
    expect(peakYaw('barin')).toBeLessThan(peakYaw('devyatka'));
    expect(peakYaw('devyatka')).toBeLessThan(peakYaw('ronin'));
  });

  it('тюнинг двигателя ускоряет машину', () => {
    const t100 = (tun) => { const c = makeCar('kopeyka', tun); let t = 99; sim(c, 14, (tt, c) => { c.input.throttle = 1; if (t === 99 && c.speed * 3.6 >= 100) t = tt; }); return t; };
    expect(t100({ engine: 5, tires: 5 })).toBeLessThan(t100({}));
  });
});
