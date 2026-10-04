// Дрифт без браузера: ВАЗ-2107 (RWD) разгоняется, срывается ручником и держит занос газом
// и контррулением. Печатает угол/скорость/очки по времени и итог серии.
//   node drift.mjs           — с «Помощью при заносе» (помощник контрруления) и без неё
//   node drift.mjs kick      — срыв перегазовкой (отпустить газ и снова в пол с рулём) вместо ручника
// Критерий: занос держится ≥ 4 с при угле 15–60° без разворота, серия сохраняется с очками > 0;
// FWD (ВАЗ-2109) после ручника выпрямляется сам.
import { CAR_BY_ID } from '../../src/config/cars.js';
import { VehiclePhysics, makeSpec } from '../../src/vehicles/VehiclePhysics.js';
import { DriftScore } from '../../src/gameplay/Drift.js';

const DT = 1 / 30, SURF = { mu: 1 };
const mode = process.argv[2] || 'handbrake';
const errs = [];

function run(carId, assist, label, { countersteer = true, print = true } = {}) {
  const spec = makeSpec(CAR_BY_ID[carId]);
  spec.driftAssist = assist ? 1 : 0;
  spec.stabilityAssist = assist ? 0.35 : 0;
  const p = new VehiclePhysics(spec);
  p.reset(0, 0, 0);
  const sc = new DriftScore();
  let banked = null, lost = null;
  const events = [];
  sc.onBank = (t, info) => { banked = { total: t, ...info }; };
  sc.onLost = (r, pts) => { lost = { r, pts }; };
  sc.onEvent = (e) => events.push(e);
  const rows = [];
  let t = 0, held = 0, maxA = 0, spun = false;
  const target = 30; // градусов
  const lowSpeed = { v: 0 };
  while (t < 12) {
    const kmh = p.speed * 3.6, a = p.driftAngle * 57.3;
    const inp = { steer: 0, throttle: 0, brake: 0, handbrake: false };
    if (t < 4.0) { inp.throttle = kmh < 58 ? 1 : 0.4; }                         // разгон до ~60 км/ч
    else if (t < 4.6) {                                                            // срыв: руль влево + ручник
      inp.steer = -1;
      if (mode === 'kick') inp.throttle = t < 4.25 ? 0 : 1;
      else { inp.handbrake = t < 4.45; inp.throttle = 0.3; }
    } else if (t < 9.5) {                                                          // удержание
      // газ держит угол: меньше угла — больше газа; руль — контрруление (против заноса)
      inp.throttle = Math.min(1, Math.max(0.4, 0.8 + (target - Math.abs(a)) * 0.02));
      // знак: занос влево (руль −1) даёт угол > 0, контрруление — руль в сторону знака угла
      if (countersteer) {
        if (assist) inp.steer = Math.sign(a) * Math.max(-0.6, Math.min(0.6, (Math.abs(a) - target) * 0.03));
        else inp.steer = Math.max(-1, Math.min(1, Math.sign(a) * (0.3 + (Math.abs(a) - target) * 0.03) + p.yawRate * 0.12 * 0));
      } else inp.steer = -0.3;
    } else { inp.throttle = 0; inp.brake = 0.3; }                                  // выход из заноса
    p.update(DT, inp, SURF);
    sc.update(DT, p, {});
    t += DT;
    if (t > 4.6 && t < 9.5) {
      if (Math.abs(a) > 12) held += DT;
      maxA = Math.max(maxA, Math.abs(a));
      if (Math.abs(a) > 100) spun = true;
      lowSpeed.v = kmh;
    }
    if (Math.abs(t * 4 - Math.round(t * 4)) < 1e-6 || Math.abs((t * 4) % 1) < DT * 4 - 1e-9) {
      if (t >= 3.75 && t <= 11) rows.push({ t: +t.toFixed(2), kmh: Math.round(kmh), 'угол°': Math.round(a), руль: +p.steer.toFixed(2), газ: +inp.throttle.toFixed(2), очки: Math.round(sc.pts), '×': sc.mult, серия: sc.total });
    }
  }
  sc.bank();
  if (print) {
    console.log(`\n=== ${label} ===`);
    console.table(rows.filter((r, i) => i % 1 === 0));
    console.log('в заносе >12°:', held.toFixed(2), 'с; макс. угол', Math.round(maxA), '°; события', events.join(', ') || '—');
    console.log('итог серии:', banked ? JSON.stringify(banked) : '—', lost ? `сгорела: ${lost.r}` : '');
  }
  return { held, maxA, spun, banked, lost, endKmh: lowSpeed.v };
}

const a = run('vaz2107', true, `ВАЗ-2107, срыв ${mode === 'kick' ? 'перегазовкой' : 'ручником'}, помощь при заносе ВКЛ`);
if (a.held < 4) errs.push(`с помощником занос держится ${a.held.toFixed(1)} с (< 4)`);
if (a.spun) errs.push('с помощником машина развернулась');
if (!a.banked || a.banked.total <= 0) errs.push('серия с помощником не сохранилась');
const b = run('vaz2107', false, `ВАЗ-2107, срыв ${mode === 'kick' ? 'перегазовкой' : 'ручником'}, помощь ВЫКЛ, ручное контрруление`);
if (b.held < 4) errs.push(`без помощника занос держится ${b.held.toFixed(1)} с (< 4)`);
if (b.spun) errs.push('без помощника с контррулением машина развернулась');
const c = run('vaz2107', false, 'ВАЗ-2107, помощь ВЫКЛ, без контрруления (ожидается разворот)', { countersteer: false });
console.log('без контрруления развернулась:', c.spun ? 'да' : 'нет', '; макс. угол', Math.round(c.maxA));
const f = run('vaz2109', true, 'ВАЗ-2109 (FWD), ручник', { print: false });
console.log('ВАЗ-2109 FWD: в заносе', f.held.toFixed(2), 'с, макс. угол', Math.round(f.maxA), '° (передний привод выпрямляется газом)');
// судейство на синтетических данных: перекладка, связка, близость, удар
{
  const sc = new DriftScore(), ev = [];
  let bank = null, lost = null;
  sc.onEvent = (e) => ev.push(e); sc.onBank = (t, i) => { bank = { t, ...i }; }; sc.onLost = (r) => { lost = r; };
  const P = { driftAngle: 0, speed: 15, vLong: 12 };
  const feed = (deg, sec, near) => { P.driftAngle = deg / 57.3; for (let t = 0; t < sec; t += DT) sc.update(DT, P, { near }); };
  feed(30, 3); feed(-30, 2); feed(-3, 0.5); feed(-30, 1, 0.5); feed(2, 2);
  console.log('\nсудейство: события', ev.join(', '), '· итог', JSON.stringify(bank));
  if (!ev.includes('ПЕРЕКЛАДКА') || !ev.includes('СВЯЗКА') || !ev.includes('БЛИЗКО!')) errs.push('нет события перекладки/связки/близости');
  if (!bank || bank.mult < 2.5) errs.push('множитель серии не вырос');
  feed(30, 2); sc.lose('Удар');
  if (lost !== 'Удар' || sc.pts !== 0) errs.push('удар не сбросил серию');
}
console.log(errs.join('\n') || 'NO ERRORS');
