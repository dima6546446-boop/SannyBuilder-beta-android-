// Эталонные траектории браузерной физики для сверки с C#-портом (ZanosUnity/Tools~/Parity).
import fs from 'node:fs';
import { makeCar, setSpeed } from '../tests/harness.mjs';

const SCEN = [
  { name: 'accel-kopeyka', car: 'kopeyka', v0: 0, secs: 6, drive: (t, i) => { i.throttle = 1; } },
  { name: 'drift-ronin', car: 'ronin', v0: 22, secs: 7, drive: (t, i) => { i.steer = -0.5; i.throttle = t < 0.3 ? 0 : 0.85; i.handbrake = t > 0.35 && t < 0.6; } },
  { name: 'flick-kupe86', car: 'kupe86', v0: 21, secs: 6, drive: (t, i) => { if (t < 0.3) { i.throttle = 0.5; i.steer = 0.9; } else if (t < 0.6) { i.throttle = 1; i.steer = -0.9; } else { i.throttle = 1; i.steer = -0.3; } i.kick = t < 0.4; } },
  { name: 'brake-barin', car: 'barin', v0: 30, secs: 6, drive: (t, i) => { i.throttle = t < 1 ? 0.6 : 0; i.brake = t >= 1 ? 1 : 0; i.steer = t > 1.5 ? 0.2 : 0; } },
  { name: 'tuned-raketa', car: 'raketa', v0: 25, secs: 6, tuning: { engine: 3, tires: 2, diff: 3, weight: 2 }, drive: (t, i) => { i.steer = t < 2 ? 0.4 : -0.45; i.throttle = 0.9; i.handbrake = t > 0.5 && t < 0.7; } },
];
const rows = ['scenario,t,x,z,h,vx,vz,w,rpm,gear,steer,slip2'];
const dt = 1 / 240;
for (const sc of SCEN) {
  const c = makeCar(sc.car, sc.tuning || {});
  if (sc.v0) setSpeed(c, sc.v0);
  for (let k = 0; k < sc.secs * 240; k++) {
    const t = k * dt, i = c.input;
    i.throttle = 0; i.brake = 0; i.steer = 0; i.handbrake = false; i.kick = false;
    sc.drive(t, i);
    c.step(dt, () => 'asphalt');
    if (k % 60 === 59) rows.push([sc.name, (k + 1) * dt, c.x, c.z, c.h, c.vx, c.vz, c.w, c.rpm, c.gear, c.steerAngle, c.slip[2]].map((v) => (typeof v === 'number' ? v.toPrecision(15) : v)).join(','));
  }
}
fs.mkdirSync('../ZanosUnity/Tools~/Parity', { recursive: true });
fs.writeFileSync('../ZanosUnity/Tools~/Parity/expected.csv', rows.join('\n') + '\n');
console.log('строк:', rows.length - 1);
