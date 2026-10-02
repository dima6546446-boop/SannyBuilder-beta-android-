import { mulberry32 } from '../utils/math.js';

/**
 * Парковочные уровни на автодроме ДОСААФ. Координаты — локальные относительно центра автодрома.
 * Курс h: 0 — «вверх» (+Z), π/2 — вправо (+X).
 * Уровень: { name, type, desc, start:[x,z,h], target:{x,z,h,w,l}, cones:[[x,z]], cars:[[x,z,h,key]],
 *            walls:[[x0,z0,x1,z1,h]], lines:[[x0,z0,x1,z1]], par, reward }
 * Сложность k ∈ [0..1] сужает места и проезды.
 */

const PI = Math.PI;
const CLASSIC = ['vaz2101', 'vaz2106', 'vaz2107', 'vaz2109', 'niva', 'oka', 'priora', 'granta', 'vesta', 'largus'];

class Builder {
  constructor(seed) {
    this.rnd = mulberry32(seed);
    this.cones = []; this.cars = []; this.walls = []; this.lines = [];
  }
  cone(x, z) { this.cones.push([x, z]); }
  coneLine(x0, z0, x1, z1, step = 1.6) {
    const len = Math.hypot(x1 - x0, z1 - z0), n = Math.max(1, Math.round(len / step));
    for (let i = 0; i <= n; i++) this.cone(x0 + (x1 - x0) * (i / n), z0 + (z1 - z0) * (i / n));
  }
  car(x, z, h, key) { this.cars.push([x, z, h, key || CLASSIC[(this.rnd() * CLASSIC.length) | 0]]); }
  wall(x0, z0, x1, z1, h = 2.4) { this.walls.push([Math.min(x0, x1), Math.min(z0, z1), Math.max(x0, x1), Math.max(z0, z1), h]); }
  line(x0, z0, x1, z1) { this.lines.push([x0, z0, x1, z1]); }
  /** Разметка места (П-образная) вокруг центра с курсом h. */
  stall(cx, cz, h, w, l, open = true) {
    const s = Math.sin(h), c = Math.cos(h);
    const P = (lx, lz) => [cx + lx * c + lz * s, cz - lx * s + lz * c];
    const a = P(w / 2, -l / 2), b = P(w / 2, l / 2), d = P(-w / 2, l / 2), e = P(-w / 2, -l / 2);
    this.line(...a, ...b); this.line(...e, ...d); this.line(...b, ...d);
    if (!open) this.line(...a, ...e);
  }
  out(extra) { return { cones: this.cones, cars: this.cars, walls: this.walls, lines: this.lines, ...extra }; }
}

// ---------------------------------------------------------------- типы упражнений
function perpendicular(k, reverse, seed) {
  const b = new Builder(seed);
  const W = 2.95 - 0.4 * k, L = 5.4, row = 9.5;
  const aisle = 11 - 4 * k;
  const n = 7, ti = 2 + ((b.rnd() * 3) | 0);
  const x0 = -((n - 1) / 2) * W;
  for (let i = 0; i < n; i++) {
    const x = x0 + i * W;
    b.stall(x, row, 0, W, L);
    if (i !== ti) b.car(x, row + (b.rnd() - 0.5) * 0.3, b.rnd() < 0.5 ? 0 : PI);
  }
  b.coneLine(x0 - W, row + L / 2 + 0.8, x0 + n * W, row + L / 2 + 0.8, 2.2);
  const zA = row - L / 2 - aisle;
  for (let i = 0; i < n; i++) if (b.rnd() < 0.85) b.car(x0 + i * W + (b.rnd() - 0.5) * 0.4, zA - 2.8, b.rnd() < 0.5 ? 0 : PI);
  const tx = x0 + ti * W;
  return b.out({
    type: reverse ? 'Задом в бокс' : 'Передом в бокс',
    desc: reverse ? 'Заедьте задним ходом на свободное место' : 'Поставьте машину на свободное место',
    start: [x0 - W * 2.5, row - L / 2 - aisle / 2, PI / 2],
    target: { x: tx, z: row, h: reverse ? PI : 0, w: W, l: L },
  });
}

function parallel(k, seed, police = false) {
  const b = new Builder(seed);
  const gap = 7.6 - 1.5 * k, lane = 3.6;
  b.coneLine(-34, lane + 1.6, 34, lane + 1.6, 1.8);
  b.coneLine(-34, -5.5 + k, 34, -5.5 + k, 2.4);
  const key = police ? 'police' : null;
  b.car(-gap / 2 - 2.35, lane, PI / 2, key);
  b.car(gap / 2 + 2.35, lane, PI / 2, key);
  b.car(-gap / 2 - 7.3, lane, PI / 2);
  b.car(gap / 2 + 7.3, lane, PI / 2);
  b.line(-gap / 2, lane - 1.2, gap / 2, lane - 1.2);
  return b.out({
    type: police ? 'Между двух ДПС' : 'Параллельная',
    desc: police ? 'Встаньте между патрульными машинами. Не задень — лишат прав!' : 'Параллельная парковка у бордюра',
    start: [-gap / 2 - 12, 0, PI / 2],
    target: { x: 0, z: lane, h: PI / 2, w: 2.2, l: gap - 0.5 },
  });
}

function garage(k, reverse, seed) {
  const b = new Builder(seed);
  const iw = 3.25 - 0.45 * k, depth = 6.4, z0 = 5;
  const t = 0.2;
  b.wall(-iw / 2 - t, z0, -iw / 2, z0 + depth);
  b.wall(iw / 2, z0, iw / 2 + t, z0 + depth);
  b.wall(-iw / 2 - t, z0 + depth, iw / 2 + t, z0 + depth + t, 2.6);
  b.wall(-iw / 2 - t, z0 + depth - 0.1, iw / 2 + t, z0 + depth + t, 2.6);
  // соседние боксы
  for (const s of [-1, 1]) {
    b.wall(s * (iw / 2 + t + iw), z0, s * (iw / 2 + t + iw) + s * t, z0 + depth);
    b.car(s * (iw + t), z0 + depth / 2, b.rnd() < 0.5 ? 0 : PI);
  }
  b.coneLine(-14, z0 - 9 + 2 * k, 14, z0 - 9 + 2 * k, 2);
  return b.out({
    type: reverse ? 'Гараж задом' : 'Гараж',
    desc: reverse ? 'Загоните машину в гараж задним ходом' : 'Заедьте в гараж, не задев стены',
    start: [-12, z0 - 5 + k, PI / 2],
    target: { x: 0, z: z0 + depth / 2 - 0.1, h: reverse ? PI : 0, w: iw - 0.1, l: depth - 0.3 },
  });
}

function slalom(k, seed) {
  const b = new Builder(seed);
  const step = 8 - 2 * k;
  const half = 5 - k;
  for (let x = -30; x <= 22; x += step) b.cone(x, 0);
  b.coneLine(-44, half + 1, 40, half + 1, 2.2);
  b.coneLine(-44, -half - 1, 40, -half - 1, 2.2);
  // бокс из конусов в конце
  const W = 3.0 - 0.3 * k, L = 5.6;
  const tx = 34;
  b.coneLine(tx - L / 2, W / 2 + 0.3, tx + L / 2, W / 2 + 0.3, 1.2);
  b.coneLine(tx - L / 2, -W / 2 - 0.3, tx + L / 2, -W / 2 - 0.3, 1.2);
  b.coneLine(tx + L / 2 + 0.4, -W / 2, tx + L / 2 + 0.4, W / 2, 1.1);
  return b.out({
    type: 'Змейка', desc: 'Проедьте змейкой между конусами и встаньте в бокс',
    start: [-42, 0, PI / 2],
    target: { x: tx, z: 0, h: PI / 2, w: W, l: L },
    slalomCones: true,
  });
}

function angled(k, reverse, seed) {
  const b = new Builder(seed);
  const hs = PI / 4, W = 2.9 - 0.35 * k, L = 5.4, row = 9;
  const step = W / Math.sin(hs);
  const n = 7, ti = 2 + ((b.rnd() * 3) | 0);
  const x0 = -((n - 1) / 2) * step;
  for (let i = 0; i < n; i++) {
    const x = x0 + i * step;
    b.stall(x, row, hs, W, L);
    if (i !== ti) b.car(x, row, b.rnd() < 0.8 ? hs : hs + PI);
  }
  b.coneLine(x0 - step * 1.5, row + 5, x0 + n * step, row + 5, 2.2);
  b.coneLine(x0 - step * 2, row - 12 + 3 * k, x0 + n * step, row - 12 + 3 * k, 2.2);
  return b.out({
    type: 'Ёлочка 45°', desc: 'Парковка под углом — по стрелке',
    start: [x0 - step * 2.5, row - 7 + 1.5 * k, PI / 2],
    target: { x: x0 + ti * step, z: row, h: reverse ? hs + PI : hs, w: W, l: L },
  });
}

function maze(k, seed) {
  const b = new Builder(seed);
  const w = 5.2 - 1.2 * k;
  const pts = [[-44, -20], [-12, -20], [-12, 18], [22, 18], [22, -12]];
  // стенки из конусов вдоль ломаной с обеих сторон
  for (let i = 0; i < pts.length - 1; i++) {
    const [ax, az] = pts[i], [bx, bz] = pts[i + 1];
    const dx = Math.sign(bx - ax), dz = Math.sign(bz - az);
    const nx = -dz, nz = dx;
    for (const s of [1, -1]) {
      const ox = nx * s * (w / 2 + 0.3), oz = nz * s * (w / 2 + 0.3);
      // на внутренних углах укорачиваем, на внешних удлиняем
      const e0 = i === 0 ? 0 : s * turnSign(pts, i) * (w / 2 + 0.3);
      const e1 = i === pts.length - 2 ? 0 : s * turnSign(pts, i + 1) * (w / 2 + 0.3);
      b.coneLine(ax + ox - dx * e0, az + oz - dz * e0, bx + ox + dx * e1, bz + oz + dz * e1, 1.7);
    }
  }
  const [ex, ez] = pts[pts.length - 1];
  b.coneLine(ex - w / 2, ez - 6.4, ex + w / 2, ez - 6.4, 1.2);
  return b.out({
    type: 'Лабиринт', desc: 'Проедьте коридор из конусов и остановитесь в зоне',
    start: [-40, -20, PI / 2],
    target: { x: ex, z: ez - 2.6, h: PI, w: w - 0.3, l: 5.2 },
  });
}
function turnSign(pts, i) {
  const [ax, az] = pts[i - 1], [bx, bz] = pts[i], [cx, cz] = pts[i + 1];
  const d1x = Math.sign(bx - ax), d1z = Math.sign(bz - az), d2x = Math.sign(cx - bx), d2z = Math.sign(cz - bz);
  return d1x * d2z - d1z * d2x > 0 ? 1 : -1;
}

function uturn(k, seed) {
  const b = new Builder(seed);
  const w = 9.5 - 2 * k;
  b.coneLine(-36, w / 2, 20, w / 2, 1.7);
  b.coneLine(-36, -w / 2, 20, -w / 2, 1.7);
  b.coneLine(20, -w / 2, 20, w / 2, 1.2);
  b.car(-6, w / 2 - 1.2, PI / 2);
  return b.out({
    type: 'Разворот', desc: 'Развернитесь в тупике (в 3 приёма) и встаньте в зону',
    start: [-30, -w / 4, PI / 2],
    target: { x: -26, z: w / 4, h: -PI / 2, w: 2.8, l: 5.4 },
  });
}

// ---------------------------------------------------------------- список уровней
const PLAN = [
  ['perp', 0, false], ['garage', 0, false], ['parallel', 0], ['perp', 0.2, true], ['slalom', 0],
  ['angled', 0, false], ['garage', 0.2, true], ['parallel', 0.25], ['maze', 0], ['uturn', 0],
  ['perp', 0.4, false], ['police', 0.3], ['angled', 0.4, false], ['garage', 0.4, true], ['slalom', 0.4],
  ['perp', 0.5, true], ['maze', 0.4], ['parallel', 0.5], ['uturn', 0.4], ['angled', 0.6, true],
  ['perp', 0.7, true], ['garage', 0.65, false], ['police', 0.6], ['maze', 0.7], ['slalom', 0.7],
  ['parallel', 0.75], ['uturn', 0.7], ['garage', 0.85, true], ['perp', 0.9, true], ['police', 0.9],
];

export const LEVELS = PLAN.map(([t, k, rev], i) => {
  const seed = 1000 + i * 17;
  let L;
  switch (t) {
    case 'perp': L = perpendicular(k, rev, seed); break;
    case 'garage': L = garage(k, rev, seed); break;
    case 'parallel': L = parallel(k, seed); break;
    case 'police': L = parallel(k, seed, true); break;
    case 'slalom': L = slalom(k, seed); break;
    case 'angled': L = angled(k, rev, seed); break;
    case 'maze': L = maze(k, seed); break;
    default: L = uturn(k, seed);
  }
  L.index = i;
  L.name = `Уровень ${i + 1}`;
  L.k = k;
  L.par = Math.round(28 + k * 22 + (t === 'maze' || t === 'slalom' ? 15 : 0) + (t === 'uturn' ? 10 : 0));
  L.reward = 300 + i * 90;
  return L;
});

/** Упражнения экзамена на площадке (последовательно). */
export function examGround() {
  return [slalom(0.3, 501), parallel(0.35, 502), garage(0.3, true, 503)];
}
