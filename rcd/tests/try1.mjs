import { makeCar, run, setSpeed } from './harness.mjs';
const set = (c, o) => Object.assign(c.input, o);
const show = (label, rows) => { console.log('==', label); for (const r of rows) console.log(JSON.stringify(r)); };
// 1. разгон
let c = makeCar('kopeyka');
show('accel', run(c, 8, (t, c) => set(c, { throttle: 1 }), 1));
// 2. стационарный поворот
c = makeCar('kopeyka'); setSpeed(c, 18);
show('steady corner', run(c, 4, (t, c) => set(c, { throttle: 0.3, steer: 0.35 }), 0.5));
// 3. ручник
c = makeCar('kopeyka'); setSpeed(c, 20);
show('handbrake entry', run(c, 6, (t, c) => set(c, t < 0.4 ? { throttle: 0, steer: -0.8 } : t < 0.8 ? { throttle: 0.3, steer: -0.8, handbrake: true } : { throttle: 0.8, steer: -0.2, handbrake: false }), 0.5));
// 4. скандинавский разворот
c = makeCar('kopeyka'); setSpeed(c, 21);
show('flick', run(c, 6, (t, c) => set(c, t < 0.3 ? { throttle: 0.5, steer: 0.9 } : t < 0.6 ? { throttle: 1, steer: -0.9 } : { throttle: 1, steer: -0.3 }), 0.5));
