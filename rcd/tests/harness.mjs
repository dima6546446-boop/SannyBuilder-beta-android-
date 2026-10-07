import { Car } from '../src/physics/car.js';
import { CARS, buildSpec } from '../src/game/cars.js';
export function makeCar(id = 'kopeyka', tuning = {}) {
  const def = CARS.find((c) => c.id === id);
  const c = new Car(buildSpec(def, tuning));
  return c;
}
export function run(car, seconds, driver, log = 0.5, surf = () => 'asphalt') {
  const dt = 1 / 240; let t = 0, next = 0; const rows = [];
  while (t < seconds) {
    driver(t, car);
    car.step(dt, surf);
    if (t >= next) { rows.push({ t: +t.toFixed(2), kmh: Math.round(car.speed * 3.6), ang: Math.round(car.driftAngle * 57.3), rpm: Math.round(car.rpm), g: car.gear, w: Math.round(car.w * 57.3), slipR: +((car.slip[2] + car.slip[3]) / 2).toFixed(2), st: +(car.steerAngle * 57.3).toFixed(1), ay: +(car.ayF / 9.81).toFixed(2) }); next += log; }
    t += dt;
  }
  return rows;
}
export function setSpeed(car, ms, heading = 0) { car.h = heading; car.vx = Math.sin(heading) * ms; car.vz = Math.cos(heading) * ms; for (let i = 0; i < 4; i++) car.wheelW[i] = ms / car.spec.wheelRadius; car.gear = ms > 22 ? 3 : (ms > 12 ? 2 : 1); }
