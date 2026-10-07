import { describe, it, expect } from 'vitest';
import { Scoring, SCORE } from '../src/game/scoring.js';
import { Progress, memoryStorage, xpForLevel } from '../src/game/progression.js';
import { Session } from '../src/game/session.js';
import { World } from '../src/physics/world.js';
import { getMap, MAP_LIST } from '../src/game/maps.js';
import { Controls } from '../src/input/controls.js';

const fakeCar = (kmh, betaDeg) => {
  const v = kmh / 3.6, b = betaDeg * Math.PI / 180;
  return { speed: v, fwdSpeed: v * Math.cos(b), latSpeed: v * Math.sin(b), w: 0 };
};

describe('очки', () => {
  it('угол × скорость × время: больше угол и скорость — больше очков', () => {
    const run = (kmh, ang, t) => { const s = new Scoring(); for (let i = 0; i < t * 100; i++) s.update(0.01, fakeCar(kmh, ang)); return s.total; };
    expect(run(80, 40, 3)).toBeGreaterThan(run(80, 20, 3));
    expect(run(100, 30, 3)).toBeGreaterThan(run(60, 30, 3));
    expect(run(80, 30, 6)).toBeGreaterThan(run(80, 30, 3) * 2); // множитель растёт со временем
  });
  it('малый угол или низкая скорость не дают очков', () => {
    const s = new Scoring();
    for (let i = 0; i < 300; i++) s.update(0.01, fakeCar(80, 4));
    for (let i = 0; i < 300; i++) s.update(0.01, fakeCar(15, 40));
    expect(s.total).toBe(0);
  });
  it('серия засчитывается после выхода из заноса', () => {
    const s = new Scoring();
    for (let i = 0; i < 300; i++) s.update(0.01, fakeCar(80, 40));
    expect(s.banked).toBe(0);
    for (let i = 0; i < 200; i++) s.update(0.01, fakeCar(80, 0));
    expect(s.banked).toBeGreaterThan(100);
    expect(s.chain).toBe(0);
  });
  it('удар сжигает несзасчитанную серию, засчитанные очки остаются', () => {
    const s = new Scoring();
    for (let i = 0; i < 300; i++) s.update(0.01, fakeCar(80, 40));
    for (let i = 0; i < 200; i++) s.update(0.01, fakeCar(80, 0));
    const banked = s.banked;
    for (let i = 0; i < 300; i++) s.update(0.01, fakeCar(80, 40));
    expect(s.chain).toBeGreaterThan(0);
    s.hit(8);
    expect(s.chain).toBe(0); expect(s.mult).toBe(1); expect(s.banked).toBe(banked); expect(s.stats.hits).toBe(1);
  });
  it('смена направления заноса повышает множитель', () => {
    const s = new Scoring();
    for (let i = 0; i < 100; i++) s.update(0.01, fakeCar(80, 35));
    const m = s.mult;
    for (let i = 0; i < 100; i++) s.update(0.01, fakeCar(80, -35));
    expect(s.mult).toBeGreaterThan(m);
  });
  it('множитель ограничен', () => {
    const s = new Scoring();
    for (let i = 0; i < 6000; i++) s.update(0.01, fakeCar(90, 35));
    expect(s.mult).toBeLessThanOrEqual(SCORE.multMax);
  });
});

describe('прогресс и сохранение', () => {
  it('уровни растут от опыта, покупка требует уровень и деньги', () => {
    const p = new Progress(memoryStorage());
    expect(p.level).toBe(1);
    expect(p.buy('pyaterka').ok).toBe(false);
    p.addXp(xpForLevel(1) + 1);
    expect(p.level).toBe(2);
    p.data.money = 20000;
    expect(p.buy('pyaterka').ok).toBe(true);
    expect(p.money).toBe(6000);
    expect(p.selected).toBe('pyaterka');
  });
  it('продажа возвращает 60% стоимости с тюнингом; последнюю машину продать нельзя', () => {
    const p = new Progress(memoryStorage());
    p.data.money = 100000; p.addXp(100000);
    p.buy('pyaterka');
    p.upgrade('pyaterka', 'engine');
    const v = p.sellValue('pyaterka');
    const before = p.money;
    expect(p.sell('pyaterka').ok).toBe(true);
    expect(p.money).toBe(before + v);
    expect(p.sell('kopeyka').ok).toBe(false);
  });
  it('тюнинг: покупка уровней, лимит 5, нехватка денег', () => {
    const p = new Progress(memoryStorage());
    p.data.money = 1e6;
    for (let i = 0; i < 5; i++) expect(p.upgrade('kopeyka', 'engine').ok).toBe(true);
    expect(p.upgrade('kopeyka', 'engine').ok).toBe(false);
    p.data.money = 0;
    expect(p.upgrade('kopeyka', 'tires').ok).toBe(false);
  });
  it('сохранение и загрузка из хранилища', () => {
    const st = memoryStorage();
    const p = new Progress(st);
    p.data.money = 50000; p.addXp(3000);
    p.upgrade('kopeyka', 'diff'); p.setPaint('kopeyka', '#123456');
    p.setPart('kopeyka', 'wheels', 'star5');
    p.finishRun({ mode: 'free', mapId: 'parking', score: 5000, driftTime: 20, bestChain: 1500, bestAngle: 55 });
    p.save();
    const q = new Progress(st);
    expect(q.money).toBe(p.money);
    expect(q.data.xp).toBe(p.data.xp);
    expect(q.level).toBe(p.level);
    expect(q.carState('kopeyka').tuning.diff).toBe(1);
    expect(q.carState('kopeyka').paint).toBe('#123456');
    expect(q.carState('kopeyka').wheels).toBe('star5');
    expect(q.record('parking', 'free')).toBe(5000);
  });
  it('повреждённое сохранение не ломает игру', () => {
    const st = memoryStorage(); st.setItem('rcd-drift-save-v1', '{bad json');
    const p = new Progress(st);
    expect(p.owns('kopeyka')).toBe(true);
  });
  it('награда за испытание выдаётся полностью один раз, потом 25%', () => {
    const p = new Progress(memoryStorage());
    const s = { mode: 'challenge', mapId: 'parking', challengeId: 'park-1', score: 3000, success: true, reward: { money: 1000, xp: 100 } };
    const a = p.finishRun(s), b = p.finishRun(s);
    expect(a.challengeReward.money).toBe(1000);
    expect(b.challengeReward.money).toBe(250);
  });
});

describe('мир и столкновения', () => {
  it('все карты строятся, у машины есть точка старта на проходимом месте', () => {
    for (const e of MAP_LIST) {
      const m = getMap(e.id); const w = new World(m);
      expect(m.segments.length + m.boxes.length).toBeGreaterThan(0);
      const sp = m.spawn;
      const s = new Session({ mapId: e.id, carId: 'kopeyka' });
      s.update(0.5, { throttle: 0 });
      expect(Math.hypot(s.car.x - sp.x, s.car.z - sp.z)).toBeLessThan(0.5);
      expect(m.challenges.length).toBeGreaterThanOrEqual(4);
    }
  });
  it('машина не проходит сквозь стену и теряет серию при ударе', () => {
    const s = new Session({ mapId: 'parking', carId: 'kopeyka' });
    const ev = [];
    s.on((e) => ev.push(e.type));
    s.car.reset(0, 70, 0);   // едем на северную стену (z = 85)
    s.car.vz = 25; s.car.vx = 0;
    for (let i = 0; i < 60 * 3; i++) s.update(1 / 60, { throttle: 1 });
    expect(s.car.z).toBeLessThan(85);
    expect(ev).toContain('impact');
  });
  it('конусы сбиваются и отлетают', () => {
    const s = new Session({ mapId: 'city', carId: 'kopeyka' });
    const c = s.world.props[0];
    s.car.reset(c.x - 8, c.z, Math.PI / 2); s.car.vx = 15; s.car.vz = 0;
    const x0 = c.x;
    for (let i = 0; i < 60; i++) s.update(1 / 60, { throttle: 0.5 });
    expect(Math.hypot(c.x - c.x0, c.z - c.z0)).toBeGreaterThan(0.5);
  });
  it('покрытие зависит от места и погоды', () => {
    const s = new Session({ mapId: 'industrial', carId: 'kopeyka', weather: 'rain' });
    expect(s.surfaceAt(0, 0)).toBe('wet');
    expect(s.surfaceAt(-90, -94)).toBe('gravel');
    const s2 = new Session({ mapId: 'industrial', carId: 'kopeyka', weather: 'clear' });
    expect(s2.surfaceAt(0, 0)).toBe('asphalt');
  });
});

describe('режимы', () => {
  it('режим на время завершается по таймеру и выдаёт сводку', () => {
    const s = new Session({ mapId: 'parking', carId: 'kopeyka', mode: 'timed' });
    let fin = false; s.on((e) => { if (e.type === 'finish') fin = true; });
    for (let i = 0; i < 60 * 100 && !s.finished; i++) s.update(1 / 60, { throttle: 0 });
    expect(fin).toBe(true);
    const sum = s.summary();
    expect(sum.mode).toBe('timed');
    expect(sum.duration).toBeGreaterThan(80);
  });
  it('испытание «ворота» проходится по порядку', () => {
    const s = new Session({ mapId: 'industrial', carId: 'kopeyka', mode: 'challenge', challengeId: 'ind-4' });
    const g = s.map.gates;
    // телепортируем машину через каждые ворота
    for (let i = 0; i < g.length; i++) {
      s.car.reset(g[i].x - 3, g[i].z, Math.PI / 2); s.prevX = s.car.x; s.prevZ = s.car.z;
      s.car.x = g[i].x + 1;
      s.step(1 / 240);
    }
    expect(s.gateIndex).toBe(g.length);
    expect(s.finished && s.success).toBe(true);
  });
});

describe('ввод', () => {
  it('руль клавиатуры сглаженный и возвращается в центр', () => {
    const c = new Controls();
    c.press('KeyD');
    let prev = 0;
    for (let i = 0; i < 10; i++) { const s = c.update(1 / 60, 20); expect(s.steer).toBeGreaterThanOrEqual(prev); prev = s.steer; }
    expect(prev).toBeLessThan(1);
    for (let i = 0; i < 120; i++) c.update(1 / 60, 20);
    expect(c.state.steer).toBe(1);
    c.release('KeyD');
    for (let i = 0; i < 120; i++) c.update(1 / 60, 20);
    expect(c.state.steer).toBe(0);
  });
  it('на скорости руль поворачивается медленнее', () => {
    const slow = new Controls(), fast = new Controls();
    slow.press('KeyD'); fast.press('KeyD');
    for (let i = 0; i < 10; i++) { slow.update(1 / 60, 2); fast.update(1 / 60, 40); }
    expect(slow.state.steer).toBeGreaterThan(fast.state.steer);
  });
});

describe('старт на картах', () => {
  it('с точки старта можно ехать прямо 4 секунды без удара ни на одной карте', () => {
    for (const e of MAP_LIST) {
      const s = new Session({ mapId: e.id, carId: 'kopeyka' });
      for (let i = 0; i < 60 * 4; i++) s.update(1 / 60, { throttle: 1, steer: 0 });
      expect(s.scoring.stats.hits, e.id).toBe(0);
      expect(s.car.speed * 3.6, e.id).toBeGreaterThan(40);
    }
  });
});
