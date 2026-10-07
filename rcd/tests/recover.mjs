import { makeCar, setSpeed } from './harness.mjs';
const id = process.argv[2] || 'kopeyka';
function scenario(label, after) {
  const c = makeCar(id); setSpeed(c, 22); const dt = 1 / 240; const out = [];
  for (let i = 0; i < 240 * 9; i++) {
    const t = i * dt, inp = c.input;
    if (t < 0.35) { inp.steer = -0.5; inp.throttle = 0; inp.handbrake = false; }
    else if (t < 0.6) { inp.steer = -0.5; inp.throttle = 0.6; inp.handbrake = true; }
    else { inp.handbrake = false; after(t, inp); }
    c.step(dt);
    out.push({ t, kmh: c.speed * 3.6, b: c.driftAngle * 57.3, w: c.w * 57.3 });
  }
  const mx = Math.max(...out.map((o) => Math.abs(o.b))), mxw = Math.max(...out.map((o) => Math.abs(o.w))), last = out[out.length - 1], mn = Math.min(...out.filter((o) => o.t > 1.5).map((o) => o.kmh));
  console.log(`${id.padEnd(9)} ${label.padEnd(36)} maxβ ${mx.toFixed(0).padStart(3)}°  maxω ${mxw.toFixed(0).padStart(3)}°/с  minV(>1.5с) ${mn.toFixed(0).padStart(3)}  конец: ${last.kmh.toFixed(0)} км/ч β${last.b.toFixed(0)}°`);
}
scenario('руль в центр + газ 0.7', (t, i) => { i.steer = 0; i.throttle = 0.7; });
scenario('руль в центр + газ 1', (t, i) => { i.steer = 0; i.throttle = 1; });
scenario('сброс газа и руль в центр', (t, i) => { i.steer = 0; i.throttle = 0; });
scenario('тормоз в заносе', (t, i) => { i.steer = 0; i.throttle = 0; i.brake = 1; });
scenario('удерживаем руль влево + газ 0.5', (t, i) => { i.steer = -0.4; i.throttle = 0.5; });
scenario('перекладка (смена сторон) на 2.2 с', (t, i) => { i.throttle = 0.9; i.steer = t < 2.2 ? -0.3 : 0.6; });
