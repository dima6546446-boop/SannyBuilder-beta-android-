import { PHYS } from './config.js';

// Кривая сцепления: линейный рост до пика (s = 1), затем плавное падение до уровня скольжения.
function grip(s, floor) {
  if (s < 1) return 2 * s - s * s;
  return floor + (1 - floor) * Math.exp(-(s - 1) * PHYS.tire.falloff);
}

/**
 * Комбинированная модель шины (эллипс трения с нормированными проскальзываниями).
 * Возвращает силы в осях колеса: fx — вперёд, fy — вправо.
 * wheelSurfSpeed — окружная скорость колеса (ω·R).
 */
export function tireForce(out, N, mu, uLong, uLat, wheelSurfSpeed, rear, floorBoost = 0) {
  const T = PHYS.tire;
  const kappa = (wheelSurfSpeed - uLong) / Math.max(Math.abs(uLong), T.minSpeedLong);
  const alpha = Math.atan2(uLat, Math.max(Math.abs(uLong), T.minSpeedLat));
  const kn = kappa / T.slipRatioPeak;
  const an = alpha / T.slipAnglePeak;
  const s = Math.hypot(kn, an);
  out.kappa = kappa;
  out.alpha = alpha;
  out.s = s;
  if (s < 1e-6) { out.fx = 0; out.fy = 0; return out; }
  const g = grip(s, (rear ? T.slideFloorRear : T.slideFloor) + floorBoost);
  const f = mu * N * g;
  out.fx = f * kn / s;
  out.fy = -f * an / s;
  return out;
}
