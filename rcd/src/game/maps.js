// Карты: четыре локации. Одни и те же данные используются физикой, рендером, миникартой и испытаниями.
import { rng, chaikin, offsetLine, polyToSegments, rectSurface, circleSurface, ribbonSurface, sampleLine } from './geom.js';

function newMap(o) {
  return {
    segments: [], boxes: [], circles: [], props: [], surfaces: [], buildings: [], barriers: [], trees: [], lamps: [],
    roads: [], markings: [], parked: [], containers: [], gates: [], decoBuildings: [], pads: [],
    ...o,
  };
}
const addBarrier = (m, pts, style, closed = false, skip = null) => {
  m.barriers.push({ pts, style, closed });
  for (const s of polyToSegments(pts, closed, skip)) m.segments.push(s);
};
const addBuilding = (m, b, solid = true) => {
  (solid ? m.buildings : m.decoBuildings).push(b);
  if (solid) m.boxes.push({ x: b.x, z: b.z, w: b.w, d: b.d, rot: b.rot || 0 });
};
const addLamp = (m, x, z, h = 7) => { m.lamps.push({ x, z, h }); m.circles.push({ x, z, r: 0.22 }); };
const addTree = (m, x, z, s = 1) => { m.trees.push({ x, z, s }); m.circles.push({ x, z, r: 0.45 * s }); };
const cone = (m, x, z) => m.props.push({ type: 'cone', x, z, r: 0.22, m: 3 });
const barrel = (m, x, z, color) => m.props.push({ type: 'barrel', x, z, r: 0.3, m: 18, color });
const coneLine = (m, x0, z0, x1, z1, n) => { for (let i = 0; i < n; i++) { const t = n === 1 ? 0 : i / (n - 1); cone(m, x0 + (x1 - x0) * t, z0 + (z1 - z0) * t); } };
const gate = (m, x, z, dx, dz, w = 7) => m.gates.push({ x1: x - dx * w / 2, z1: z - dz * w / 2, x2: x + dx * w / 2, z2: z + dz * w / 2, x, z });

// ============ 1. Центральная улица ============
function cityStreet() {
  const r = rng(11);
  const m = newMap({
    id: 'city', name: 'Центральная улица', level: 1, ground: 'concrete', theme: 'city', time: 'dusk',
    desc: 'Сетка городских кварталов: перекрёстки, широкие проспекты, парк и фонари. Хорош для связок и снайперских заносов у бордюра.',
    spawn: { x: -60, z: 0, h: Math.PI / 2 },
    bounds: { x0: -175, z0: -125, x1: 175, z1: 125 },
  });
  const W = 14, X = [-130, 0, 130], Z = [-85, 0, 85];
  // дороги
  for (const x of X) m.surfaces.push(rectSurface('asphalt', x - W / 2, -85 - W / 2, x + W / 2, 85 + W / 2));
  for (const z of Z) m.surfaces.push(rectSurface('asphalt', -130 - W / 2, z - W / 2, 130 + W / 2, z + W / 2));
  for (const x of X) m.roads.push({ pts: [[x, -85 - W / 2], [x, 85 + W / 2]], width: W });
  for (const z of Z) m.roads.push({ pts: [[-130 - W / 2, z], [130 + W / 2, z]], width: W });
  // разметка
  for (const x of X) m.markings.push({ pts: [[x, -85 + 8], [x, 85 - 8]], w: 0.2, color: 0xf2c94c, dash: [6, 6] });
  for (const z of Z) m.markings.push({ pts: [[-130 + 8, z], [130 - 8, z]], w: 0.2, color: 0xf2c94c, dash: [6, 6] });
  // здания по кварталам
  const blocks = [[-130, -85, 0, 0], [0, -85, 130, 0], [-130, 0, 0, 85], [0, 0, 130, 85]];
  const palette = [0x8c8f96, 0xa3866a, 0x7c8ea3, 0xb59b8a, 0x6f7a73, 0x9a9aa6];
  blocks.forEach(([x0, z0, x1, z1], bi) => {
    const ix0 = x0 + W / 2 + 4.5, iz0 = z0 + W / 2 + 4.5, ix1 = x1 - W / 2 - 4.5, iz1 = z1 - W / 2 - 4.5;
    if (bi === 3) { // парк с фонтаном
      m.surfaces.push(rectSurface('grass', ix0, iz0, ix1, iz1));
      m.parkRect = { x0: ix0, z0: iz0, x1: ix1, z1: iz1 };
      m.circles.push({ x: (ix0 + ix1) / 2, z: (iz0 + iz1) / 2, r: 5 });
      m.pads.push({ x: (ix0 + ix1) / 2, z: (iz0 + iz1) / 2, r: 5, kind: 'fountain' });
      for (let i = 0; i < 26; i++) addTree(m, ix0 + 3 + r() * (ix1 - ix0 - 6), iz0 + 3 + r() * (iz1 - iz0 - 6), 0.9 + r() * 0.8);
      return;
    }
    // ряд зданий внутри квартала
    const cols = bi === 0 ? 3 : 2, rows = 2;
    const cw = (ix1 - ix0) / cols, rh = (iz1 - iz0) / rows;
    for (let i = 0; i < cols; i++) for (let j = 0; j < rows; j++) {
      const w = cw - 5 - r() * 4, d = rh - 5 - r() * 4;
      addBuilding(m, { x: ix0 + cw * (i + 0.5), z: iz0 + rh * (j + 0.5), w, d, h: 14 + r() * 30, rot: 0, style: r() > 0.5 ? 'office' : 'apart', color: palette[Math.floor(r() * palette.length)] });
    }
  });
  // внешняя граница: бетонные блоки и здания за ними
  const bd = m.bounds;
  addBarrier(m, [[bd.x0, bd.z0], [bd.x1, bd.z0], [bd.x1, bd.z1], [bd.x0, bd.z1]], 'jersey', true);
  for (let i = 0; i < 18; i++) {
    const t = i / 17;
    m.decoBuildings.push({ x: bd.x0 + 14 + t * (bd.x1 - bd.x0 - 28), z: bd.z0 - 18, w: 22, d: 22, h: 20 + r() * 40, style: 'office', color: palette[i % palette.length] });
    m.decoBuildings.push({ x: bd.x0 + 14 + t * (bd.x1 - bd.x0 - 28), z: bd.z1 + 18, w: 22, d: 22, h: 20 + r() * 40, style: 'apart', color: palette[(i + 2) % palette.length] });
  }
  for (let i = 0; i < 12; i++) {
    const t = i / 11;
    m.decoBuildings.push({ x: bd.x0 - 18, z: bd.z0 + 10 + t * (bd.z1 - bd.z0 - 20), w: 22, d: 22, h: 20 + r() * 40, style: 'apart', color: palette[(i + 1) % palette.length] });
    m.decoBuildings.push({ x: bd.x1 + 18, z: bd.z0 + 10 + t * (bd.z1 - bd.z0 - 20), w: 22, d: 22, h: 20 + r() * 40, style: 'office', color: palette[(i + 3) % palette.length] });
  }
  // фонари на тротуарах вдоль улиц
  for (const z of Z) for (let x = -120; x <= 120; x += 30) { if (Math.abs(x) % 130 < 12) continue; addLamp(m, x, z + (z > 0 ? -W / 2 - 1.2 : W / 2 + 1.2)); }
  for (const x of X) for (let z = -75; z <= 75; z += 30) { if (Math.abs(z % 85) < 12) continue; addLamp(m, x + W / 2 + 1.2, z); }
  // припаркованные машины вдоль улицы (на бордюре у нижней улицы)
  const pcol = [0x6a1b1b, 0x1b3a6a, 0x2c2c2c, 0xd8d8d0, 0x3b6a45];
  for (let i = 0; i < 6; i++) { const x = -112 + i * 8.6; m.parked.push({ x, z: -85 + W / 2 - 1.5, rot: Math.PI / 2, color: pcol[i % pcol.length], w: 1.8, d: 4.2 }); m.boxes.push({ x, z: -85 + W / 2 - 1.5, w: 4.2, d: 1.8, rot: Math.PI / 2 }); }
  for (let i = 0; i < 5; i++) { const z = 14 + i * 9; m.parked.push({ x: 130 - W / 2 + 1.5, z, rot: 0, color: pcol[(i + 2) % pcol.length], w: 1.8, d: 4.2 }); m.boxes.push({ x: 130 - W / 2 + 1.5, z, w: 1.8, d: 4.2, rot: 0 }); }
  // слалом из конусов на проспекте и шины на перекрёстках
  coneLine(m, -110, 3, -30, -3, 9);
  coneLine(m, 20, -85 + 3, 100, -85 - 3, 9);
  for (const [x, z] of [[0, 0], [130, 0], [-130, 0], [0, 85], [0, -85]]) { barrel(m, x + 4, z + 4, 0xd9531e); barrel(m, x - 4, z - 4, 0x2d6ad9); }
  // ворота для испытаний
  for (let i = 0; i < 6; i++) gate(m, -100 + i * 22, i % 2 ? 4 : -4, 0, 1, 8);
  m.challenges = [
    { id: 'city-1', type: 'score', name: 'Разминка', desc: 'Набери 4 000 очков за 90 секунд.', target: 4000, time: 90, reward: { money: 1200, xp: 200 } },
    { id: 'city-2', type: 'chain', name: 'Длинная серия', desc: 'Одна серия на 3 000 очков без ударов.', target: 3000, time: 150, reward: { money: 1800, xp: 320 } },
    { id: 'city-3', type: 'angle', name: 'Под углом', desc: 'Держи угол 40°+ в течение 3 секунд подряд.', target: 40, hold: 3, time: 120, reward: { money: 1600, xp: 280 } },
    { id: 'city-4', type: 'gates', name: 'Змейка по проспекту', desc: 'Пройди 6 ворот по порядку в заносе за 70 секунд.', time: 70, needDrift: true, reward: { money: 2400, xp: 400 } },
    { id: 'city-5', type: 'noHit', name: 'Чистый дрифт', desc: '6 000 очков без единого удара за 150 секунд.', target: 6000, time: 150, reward: { money: 3200, xp: 520 } },
  ];
  m.timed = { time: 120, goal: 8000 };
  return m;
}

// ============ 2. Промзона ============
function industrial() {
  const r = rng(23);
  const m = newMap({
    id: 'industrial', name: 'Промзона', level: 2, ground: 'concrete', theme: 'industrial', time: 'day',
    desc: 'Огромный бетонный двор между складами: контейнерные лабиринты, шинные стены, гравийная яма и скидпад с бетонным столбом для бубликов.',
    spawn: { x: -80, z: 60, h: 0 },
    bounds: { x0: -170, z0: -120, x1: 170, z1: 120 },
  });
  const bd = m.bounds;
  addBarrier(m, [[bd.x0, bd.z0], [bd.x1, bd.z0], [bd.x1, bd.z1], [bd.x0, bd.z1]], 'wall', true);
  // склады вдоль северной и восточной границ
  for (let i = 0; i < 4; i++) addBuilding(m, { x: -120 + i * 70, z: bd.z1 - 14, w: 56, d: 24, h: 10, style: 'warehouse', color: [0x6d7f8f, 0x8f7d6d, 0x7a8f6d, 0x8f6d6d][i] });
  for (let i = 0; i < 2; i++) addBuilding(m, { x: bd.x1 - 14, z: -60 + i * 60, w: 24, d: 50, h: 9, style: 'warehouse', color: [0x7f7f8f, 0x8f8f7f][i] });
  addBuilding(m, { x: -140, z: -60, w: 20, d: 60, h: 14, style: 'factory', color: 0x8a6a55 });
  // трубы и цистерны (круглые препятствия)
  for (const [x, z] of [[-150, 20], [-150, 40], [-150, 0]]) { m.circles.push({ x, z, r: 6 }); m.pads.push({ x, z, r: 6, kind: 'tank' }); }
  // асфальтовое кольцо — основная трасса
  const ring = [];
  for (let i = 0; i < 40; i++) { const a = i / 40 * Math.PI * 2; ring.push([Math.cos(a) * 95 + 0, Math.sin(a) * 58 + 0]); }
  m.surfaces.push(rectSurface('asphalt', -110, -70, 110, 70));
  m.roads.push({ pts: ring, width: 16, closed: true, draw: true });
  m.markings.push({ pts: ring, w: 0.2, color: 0xf2f2f2, dash: [5, 5], closed: true });
  // зона бубликов (скидпад) — мокрый бетон, столб в центре
  m.surfaces.push(circleSurface('asphalt', 100, -55, 38));
  m.pads.push({ x: 100, z: -55, r: 38, kind: 'skidpad' });
  m.circles.push({ x: 100, z: -55, r: 0.7 });
  m.pads.push({ x: 100, z: -55, r: 0.7, kind: 'bollard' });
  // гравийная яма
  m.surfaces.push(rectSurface('gravel', -130, -112, -50, -76));
  m.pads.push({ x: -90, z: -94, w: 80, d: 36, kind: 'gravel' });
  m.surfaces.push(circleSurface('dirt', -20, -92, 20));
  m.pads.push({ x: -20, z: -92, r: 20, kind: 'dirt' });
  // контейнерные стопки, образующие лабиринт
  const ccol = [0xa83232, 0x2f5fa8, 0x3a8a4f, 0xd9a21e, 0x8a8a8a, 0x7a4a2a];
  const cont = (x, z, rot, stack = 1) => {
    for (let k = 0; k < stack; k++) m.containers.push({ x, z, rot, w: 2.45, d: 12, y: k * 2.6, color: ccol[Math.floor(r() * ccol.length)] });
    m.boxes.push({ x, z, w: 2.45, d: 12, rot });
  };
  for (let i = 0; i < 4; i++) cont(-30 + i * 2.6, 95 - 7, 0, 1 + (i % 2));
  for (let i = 0; i < 5; i++) cont(40 + i * 2.6, 85, Math.PI / 2, 1);
  cont(-40, 20, Math.PI / 4, 2); cont(-33, 27, Math.PI / 4, 1); cont(60, 30, -0.4, 2); cont(70, 36, -0.4, 1);
  cont(-95, 75, Math.PI / 2, 2); cont(-95, 62, Math.PI / 2, 1); cont(5, -28, 0.3, 2); cont(14, -21, 0.3, 1);
  cont(110, 70, 0, 2); cont(125, 70, 0, 1); cont(-70, 20, 0.2, 1); cont(-62, 30, 0.2, 2);
  // шинные стены на шпильках
  addBarrier(m, [[-110, 25], [-110, 55]], 'tyres');
  addBarrier(m, [[110, 30], [138, 30]], 'tyres');
  addBarrier(m, [[40, -40], [72, -40]], 'tyres');
  addBarrier(m, [[-60, -50], [-30, -50]], 'tyres');
  // конусы, бочки
  coneLine(m, -60, 10, 30, 14, 10);
  coneLine(m, 20, -10, 90, -14, 8);
  for (let i = 0; i < 10; i++) barrel(m, -10 + r() * 80, 50 + r() * 15, [0xd9531e, 0x2d6ad9, 0xdcdcdc][i % 3]);
  // фонари
  for (let x = -150; x <= 150; x += 50) { addLamp(m, x, bd.z0 + 6, 9); addLamp(m, x, 6, 9); }
  for (let i = 0; i < 7; i++) gate(m, -60 + i * 22, i % 2 ? 22 : 38, 0, 1, 9);
  m.challenges = [
    { id: 'ind-1', type: 'score', name: 'Тёплый двор', desc: '6 000 очков за 100 секунд.', target: 6000, time: 100, reward: { money: 1800, xp: 300 } },
    { id: 'ind-2', type: 'angle', name: 'Бублик', desc: 'Держи угол 55°+ в течение 4 секунд.', target: 55, hold: 4, time: 100, reward: { money: 2200, xp: 380 } },
    { id: 'ind-3', type: 'chain', name: 'Контейнерный слалом', desc: 'Серия на 5 000 очков без ударов.', target: 5000, time: 180, reward: { money: 3000, xp: 480 } },
    { id: 'ind-4', type: 'gates', name: 'Ворота склада', desc: 'Пройди 7 ворот по порядку за 90 секунд.', time: 90, needDrift: false, reward: { money: 2800, xp: 440 } },
    { id: 'ind-5', type: 'noHit', name: 'Без царапин', desc: '10 000 очков без ударов за 180 секунд.', target: 10000, time: 180, reward: { money: 4500, xp: 700 } },
  ];
  m.timed = { time: 120, goal: 12000 };
  return m;
}

// ============ 3. Горный серпантин ============
function mountain() {
  const r = rng(37);
  const m = newMap({
    id: 'mountain', name: 'Горный серпантин', level: 4, ground: 'grass', theme: 'mountain', time: 'dusk',
    desc: 'Горная дорога со шпильками и отбойниками. Узко, быстро и опасно — один промах, и серия сгорела.',
    spawn: { x: -85, z: 0, h: Math.PI / 2 },
  });
  const sp = 46, R = sp / 2, X0 = -88, X1 = 88;
  let pts = [];
  const line = (xa, xb, z, ph) => { const n = 14; for (let i = 0; i <= n; i++) { const x = xa + (xb - xa) * i / n; pts.push([x, z + 5 * Math.sin(x / 19 + ph)]); } };
  const arc = (cx, cz, dir) => { const n = 14; for (let i = 1; i < n; i++) { const a = Math.PI * i / n; pts.push([cx + dir * Math.sin(a) * R, cz - Math.cos(a) * R]); } };
  pts.push([X0 - 30, 0]);
  line(X0, X1, 0, 0); arc(X1, R, 1);
  line(X1, X0, sp, 1.7); arc(X0, sp + R, -1);
  line(X0, X1, 2 * sp, 3.1); arc(X1, 2 * sp + R, 1);
  line(X1, X0, 3 * sp, 4.4);
  pts.push([X0 - 30, 3 * sp]);
  pts = chaikin(pts, 3);
  const hw = 4.6;
  { // точка старта — на оси дороги
    let bi = 0, bd2 = 1e9;
    pts.forEach((p, i) => { const d = Math.abs(p[0] + 80); if (d < bd2) { bd2 = d; bi = i; } });
    const a0 = pts[bi], b0 = pts[bi + 1];
    m.spawn = { x: a0[0], z: a0[1], h: Math.atan2(b0[0] - a0[0], b0[1] - a0[1]) };
  }
  m.roads.push({ pts, width: hw * 2 });
  m.surfaces.push(ribbonSurface('asphalt', pts, hw));
  m.surfaces.unshift(ribbonSurface('gravel', pts, hw + 2.6));
  // отбойники с двух сторон
  const left = offsetLine(pts, hw + 1.0), right = offsetLine(pts, -(hw + 1.0));
  addBarrier(m, left, 'guardrail');
  addBarrier(m, right, 'guardrail');
  // закрытие концов дороги
  const a = pts[0], b = pts[pts.length - 1];
  m.segments.push([left[0][0], left[0][1], right[0][0], right[0][1]]);
  m.segments.push([left[left.length - 1][0], left[left.length - 1][1], right[right.length - 1][0], right[right.length - 1][1]]);
  m.barriers.push({ pts: [[left[0][0], left[0][1]], [right[0][0], right[0][1]]], style: 'jersey', closed: false });
  m.barriers.push({ pts: [[left[left.length - 1][0], left[left.length - 1][1]], [right[right.length - 1][0], right[right.length - 1][1]]], style: 'jersey', closed: false });
  // разметка
  m.markings.push({ pts: pts.slice(1, -1), w: 0.15, color: 0xf0f0f0, dash: [5, 5] });
  m.markings.push({ pts: offsetLine(pts, hw - 0.5).slice(1, -1), w: 0.15, color: 0xffffff });
  m.markings.push({ pts: offsetLine(pts, -(hw - 0.5)).slice(1, -1), w: 0.15, color: 0xffffff });
  // деревья вдоль дороги (за отбойником) и столбики
  const samples = sampleLine(pts, 9);
  samples.forEach((s, i) => {
    for (const side of [1, -1]) {
      const off = (hw + 3.5 + r() * 14) * side;
      const x = s.x - s.dz * off, z = s.z + s.dx * off;
      if (r() > 0.35) m.trees.push({ x, z, s: 1 + r() * 1.4, noCollide: true });
    }
    if (i % 4 === 0) { const x = s.x - s.dz * -(hw + 1.6), z = s.z + s.dx * -(hw + 1.6); m.lamps.push({ x, z, h: 6, noCollide: true }); }
  });
  // камни и конусы на шпильках
  coneLine(m, pts[10][0] - 2, pts[10][1] + 2, pts[14][0] + 2, pts[14][1] - 2, 5);
  for (let i = 0; i < 8; i++) { const s = samples[Math.floor(r() * samples.length)]; barrel(m, s.x - s.dz * (r() * 6 - 3), s.z + s.dx * (r() * 6 - 3), 0xd9531e); }
  // ворота: на шпильках
  const gs = [Math.floor(samples.length * 0.18), Math.floor(samples.length * 0.33), Math.floor(samples.length * 0.5), Math.floor(samples.length * 0.66), Math.floor(samples.length * 0.8)];
  for (const i of gs) gate(m, samples[i].x, samples[i].z, -samples[i].dz, samples[i].dx, hw * 1.6);
  m.bounds = { x0: -190, z0: -60, x1: 190, z1: 3 * sp + 60 };
  m.challenges = [
    { id: 'mtn-1', type: 'score', name: 'Первый подъём', desc: '5 000 очков за 120 секунд.', target: 5000, time: 120, reward: { money: 2600, xp: 420 } },
    { id: 'mtn-2', type: 'chain', name: 'Шпильки подряд', desc: 'Серия на 4 000 очков без ударов.', target: 4000, time: 160, reward: { money: 3200, xp: 500 } },
    { id: 'mtn-3', type: 'gates', name: 'Контрольные точки', desc: 'Проезжай 5 ворот по порядку за 80 секунд.', time: 80, needDrift: false, reward: { money: 3500, xp: 560 } },
    { id: 'mtn-4', type: 'angle', name: 'Угол над пропастью', desc: 'Держи 45°+ в течение 3 секунд.', target: 45, hold: 3, time: 120, reward: { money: 3300, xp: 520 } },
    { id: 'mtn-5', type: 'noHit', name: 'Идеальный спуск', desc: '9 000 очков без ударов.', target: 9000, time: 200, reward: { money: 6000, xp: 900 } },
  ];
  m.timed = { time: 150, goal: 12000 };
  return m;
}

// ============ 4. Парковка ============
function parking() {
  const r = rng(53);
  const m = newMap({
    id: 'parking', name: 'Ночная парковка', level: 1, ground: 'asphalt', theme: 'parking', time: 'night',
    desc: 'Огромная ночная парковка торгового центра: много места, лужи и стоянки со светлячками фонарей. Лучше всего для тренировки.',
    spawn: { x: -15, z: -40, h: 0 },
    bounds: { x0: -125, z0: -85, x1: 125, z1: 85 },
  });
  const bd = m.bounds;
  addBarrier(m, [[bd.x0, bd.z0], [bd.x1, bd.z0], [bd.x1, bd.z1], [bd.x0, bd.z1]], 'curb', true);
  m.decoBuildings.push({ x: 0, z: bd.z1 + 25, w: 200, d: 40, h: 14, style: 'mall', color: 0x9a9aa8 });
  for (let i = 0; i < 12; i++) m.decoBuildings.push({ x: bd.x0 - 14, z: -75 + i * 14, w: 18, d: 12, h: 10 + r() * 14, style: 'apart', color: 0x8d8f99 });
  for (let i = 0; i < 12; i++) m.decoBuildings.push({ x: bd.x1 + 14, z: -75 + i * 14, w: 18, d: 12, h: 10 + r() * 14, style: 'apart', color: 0x8d8f99 });
  m.roads.push({ pts: [[bd.x0, 0], [bd.x1, 0]], width: 170, type: 'lot' });
  // стоянки: ряды машин у северной и боковых границ
  const pcol = [0x6a1b1b, 0x1b3a6a, 0x2c2c2c, 0xd8d8d0, 0x3b6a45, 0x8a6a1b, 0x555a66];
  const park = (x, z, rot) => { if (r() < 0.22) return; m.parked.push({ x, z, rot, color: pcol[Math.floor(r() * pcol.length)], w: 1.8, d: 4.3 }); m.boxes.push({ x, z, w: 1.8, d: 4.3, rot }); };
  for (let i = 0; i < 20; i++) park(-95 + i * 5.2, 78, 0);
  for (let i = 0; i < 20; i++) { park(-95 + i * 5.2, 62, Math.PI); park(-95 + i * 5.2, 48, 0); }
  for (let i = 0; i < 8; i++) { park(-117, -70 + i * 5.2, Math.PI / 2); park(117, -70 + i * 5.2, -Math.PI / 2); }
  // разметка парковочных мест
  for (let i = 0; i <= 20; i++) { const x = -97.6 + i * 5.2; m.markings.push({ pts: [[x, 56], [x, 66]], w: 0.12, color: 0xffffff }); m.markings.push({ pts: [[x, 71], [x, 80]], w: 0.12, color: 0xffffff }); m.markings.push({ pts: [[x, 42], [x, 52]], w: 0.12, color: 0xffffff }); }
  m.markings.push({ pts: [[-60, -40], [60, -40]], w: 0.25, color: 0xf2c94c, dash: [3, 3] });
  // острова с фонарями, тележки и мусорные баки как препятствия
  for (let x = -90; x <= 90; x += 30) for (const z of [-60, 0, 30]) addLamp(m, x, z, 8);
  const dump = (x, z, rot) => { m.containers.push({ x, z, rot, w: 1.8, d: 3.2, y: 0, color: 0x2f6f3a, small: true }); m.boxes.push({ x, z, w: 1.8, d: 3.2, rot }); };
  dump(-100, -40, 0); dump(100, -20, 0.3); dump(0, 20, 0);
  // лужи
  m.surfaces.push(circleSurface('wet', 40, -20, 14), circleSurface('wet', -50, 10, 11), circleSurface('wet', 70, 20, 9));
  m.pads.push({ x: 40, z: -20, r: 14, kind: 'puddle' }, { x: -50, z: 10, r: 11, kind: 'puddle' }, { x: 70, z: 20, r: 9, kind: 'puddle' });
  // слалом и зона бубликов
  coneLine(m, -80, -10, -10, -18, 12);
  coneLine(m, 20, -60, 100, -50, 10);
  for (let i = 0; i < 12; i++) { const a = i / 12 * Math.PI * 2; cone(m, -70 + Math.cos(a) * 14, -45 + Math.sin(a) * 14); }
  for (let i = 0; i < 6; i++) barrel(m, 60 + r() * 25, 5 + r() * 12, 0xd9531e);
  for (let i = 0; i < 8; i++) gate(m, -80 + i * 22, i % 2 ? -12 : -24, 0, 1, 9);
  m.challenges = [
    { id: 'park-1', type: 'score', name: 'Школа дрифта', desc: '3 000 очков за 90 секунд.', target: 3000, time: 90, reward: { money: 900, xp: 160 } },
    { id: 'park-2', type: 'angle', name: 'Угол 35°', desc: 'Держи угол 35°+ 3 секунды.', target: 35, hold: 3, time: 90, reward: { money: 1100, xp: 200 } },
    { id: 'park-3', type: 'gates', name: 'Змейка', desc: 'Пройди 8 ворот в заносе за 80 секунд.', time: 80, needDrift: true, reward: { money: 2000, xp: 340 } },
    { id: 'park-4', type: 'chain', name: 'Серия мастера', desc: 'Одна серия на 4 000 очков.', target: 4000, time: 150, reward: { money: 1900, xp: 330 } },
    { id: 'park-5', type: 'noHit', name: 'Между машин', desc: '5 000 очков без ударов за 150 секунд.', target: 5000, time: 150, reward: { money: 2500, xp: 420 } },
  ];
  m.timed = { time: 90, goal: 6000 };
  return m;
}

export const MAP_LIST = [
  { id: 'parking', build: parking },
  { id: 'city', build: cityStreet },
  { id: 'industrial', build: industrial },
  { id: 'mountain', build: mountain },
];
const cache = {};
export function getMap(id) {
  if (!cache[id]) { const e = MAP_LIST.find((m) => m.id === id); if (!e) throw new Error('Нет карты ' + id); cache[id] = e.build(); }
  return cache[id];
}
export function mapInfos() { return MAP_LIST.map((m) => { const d = getMap(m.id); return { id: d.id, name: d.name, level: d.level, desc: d.desc }; }); }
