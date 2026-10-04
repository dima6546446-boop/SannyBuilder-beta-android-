// Паспорт физики без браузера: 0–100 км/ч, максималка, тормозной путь со 100 км/ч и обычная
// управляемость (поворот на 60 км/ч, разворот на 15 км/ч) для всех машин.
// node physics.mjs                 — таблица; сравнивать с README («Машины», допуск ±10 %).
// PHYS=/путь/VehiclePhysics.js node physics.mjs — то же для другой версии физики (сравнение до/после).
import { CARS } from '../../src/config/cars.js';

const { VehiclePhysics, makeSpec } = await import(process.env.PHYS || '../../src/vehicles/VehiclePhysics.js');
const DT = 1 / 60;
const SURF = { mu: 1 };
const rows = [];
const drive = (p, inp, sec) => { for (let t = 0; t < sec; t += DT) p.update(DT, inp, SURF); };
for (const def of CARS.filter((c) => c.spec && c.price)) {
  const p = new VehiclePhysics(makeSpec(def));
  p.reset(0, 0, 0);
  let t = 0, t100 = null;
  while (t < 120) {
    p.update(DT, { steer: 0, throttle: 1, brake: 0, handbrake: false }, SURF); t += DT;
    if (t100 === null && p.speed * 3.6 >= 100) t100 = t;
  }
  const vmax = p.speed * 3.6;
  // торможение со 100 км/ч
  p.reset(0, 0, 0); p.vz = 100 / 3.6; p.gear = 4; p.speed = 100 / 3.6;
  let n = 0;
  while (p.speed > 0.05 && n++ < 1200) p.update(DT, { steer: 0, throttle: 0, brake: 1, handbrake: false }, SURF);
  const brake = p.z;
  // поворот: 60 км/ч, руль на половину, газ поддерживает скорость — макс. угол задней оси и скорость
  p.reset(0, 0, 0); p.vz = 60 / 3.6; p.gear = 3;
  let maxSlip = 0;
  for (let k = 0; k < 180; k++) {
    p.update(DT, { steer: 0.5, throttle: p.speed * 3.6 < 60 ? 0.6 : 0.2, brake: 0, handbrake: false }, SURF);
    maxSlip = Math.max(maxSlip, Math.abs(p.slipRear));
  }
  const turnV = p.speed * 3.6;
  // разворот на 15 км/ч полным рулём: радиус по скорости рыскания
  p.reset(0, 0, 0); p.vz = 15 / 3.6; p.gear = 1;
  drive(p, { steer: 1, throttle: 0.15, brake: 0, handbrake: false }, 3);
  const radius = p.speed / Math.max(1e-3, Math.abs(p.yawRate));
  rows.push({ car: def.id, '0-100': t100 ? +t100.toFixed(1) : '—', vmax: Math.round(vmax), brake100: +brake.toFixed(1),
    'turn60 slipR°': +(maxSlip * 57.3).toFixed(1), 'turn60 v': Math.round(turnV), 'R@15': +radius.toFixed(1) });
}
console.table(rows);
