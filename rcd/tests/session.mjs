import { Session } from '../src/game/session.js';
const s = new Session({ mapId: 'parking', carId: 'kopeyka', mode: 'free' });
const ev = []; s.on((e) => ev.push(e.type));
const dt = 1 / 60;
for (let i = 0; i < 60 * 12; i++) {
  const t = i * dt;
  const inp = t < 3 ? { throttle: 1, steer: 0 } : t < 3.6 ? { throttle: 0.4, steer: 0.8, handbrake: t > 3.2 } : { throttle: 0.85, steer: -0.5, handbrake: false };
  s.update(dt, inp);
  if (i % 60 == 0) console.log(t.toFixed(0), 'kmh', (s.car.speed * 3.6).toFixed(0), 'beta', (s.scoring.angle).toFixed(0), 'score', s.scoring.total, 'x', s.car.x.toFixed(0), 'z', s.car.z.toFixed(0));
}
console.log(ev.join(','));
