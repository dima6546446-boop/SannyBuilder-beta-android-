import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import ANCHORS from '../config/interiorAnchors.json';
import { buildCharacter, solveArmIK, OUTFITS } from '../character/CharacterModel.js';

/**
 * Салон каждой модели по реальному прототипу: торпедо, щиток приборов с живыми стрелками
 * (спидометр, тахометр, топливо, температура, часы), руль (крутится вместе с рулением),
 * центральная консоль, магнитола, рычаги, педали, кресла с обивкой, обшивка дверей,
 * потолок, зеркало, козырьки — и водитель, который держит руль двумя руками (IK).
 *
 * Габариты берутся из формы кузова (interiorAnchors.json ← tools/blender/export_interior.py),
 * поэтому салон стоит точно внутри своей машины. Координаты машины: +X — влево, +Y — вверх,
 * +Z — вперёд; водитель слева (x = +0.36).
 *
 * Draw calls: статичная часть одним мешем с цветами вершин, щиток, стрелки, руль, водитель.
 */

// ------------------------------------------------------------------ стили по моделям
const G = (type, x, y, r, max, label) => ({ type, x, y, r, max, label });

export const INTERIOR_STYLES = {
  // «Копейка»: плоская панель, металлическая накладка, два круглых прибора в овале,
  // тонкий двухспицевый руль с хромированным кольцом сигнала, кресла без подголовников
  vaz2101: {
    dash: 'flat', depth: 0.4, dashColor: 0x1d1d1d, strip: 0xb8b8b0, trim: 0x2a2624,
    seat: 0x7a2a22, seat2: 0x5a1c16, pattern: 'stripes', headrest: false,
    wheel: { r: 0.205, t: 0.014, spokes: 2, color: 0x161616, ring: true, pad: 0.05, tilt: 0.62 },
    cluster: { w: 0.34, h: 0.15, hood: 'oval', face: '#101010', ring: '#c8c8c8', ink: '#f2f2f2', red: '#e04030',
      gauges: [G('speed', -0.075, 0, 0.068, 180, 'км/ч'), G('combo', 0.08, 0, 0.055, 0, '')] },
    backlight: 0xffa860, radio: 'none', console: 'tunnel', lever: 'long', door: 0x2e2a26, crank: true,
  },
  // «Шестёрка»: панель под дерево, четыре колодца приборов (спидометр, тахометр, 2 малых)
  vaz2106: {
    dash: 'flat', depth: 0.42, dashColor: 0x1b1b1b, strip: 0x6b4226, trim: 0x2a2420,
    seat: 0x6a4a32, seat2: 0x4a3222, pattern: 'stripes', headrest: true,
    wheel: { r: 0.2, t: 0.016, spokes: 2, color: 0x141414, ring: false, pad: 0.075, tilt: 0.6 },
    cluster: { w: 0.42, h: 0.15, hood: 'wells', face: '#0d0d0d', ring: '#b0b0b0', ink: '#f2f2f2', red: '#e04030',
      gauges: [G('speed', -0.11, 0.005, 0.062, 180, 'км/ч'), G('rpm', 0.035, 0.005, 0.055, 8, 'об/мин'),
        G('fuel', 0.15, 0.03, 0.03, 0, ''), G('temp', 0.15, -0.035, 0.03, 0, '')] },
    backlight: 0xffb060, radio: 'ural', console: 'tunnel', lever: 'long', door: 0x34302c, crank: true, clock: true,
  },
  // «Семёрка»: высокая панель с деревянной вставкой, прямоугольный щиток, часы на консоли
  vaz2107: {
    dash: 'block', depth: 0.44, dashColor: 0x181818, strip: 0x5e3a20, trim: 0x262626,
    seat: 0x3d4a5c, seat2: 0x2a3442, pattern: 'velour', headrest: true,
    wheel: { r: 0.2, t: 0.017, spokes: 2, color: 0x121212, ring: false, pad: 0.09, tilt: 0.58 },
    cluster: { w: 0.44, h: 0.16, hood: 'rect', face: '#0a0a0a', ring: '#7a7a7a', ink: '#e8ffe8', red: '#ff4a3a',
      gauges: [G('speed', -0.1, 0, 0.066, 200, 'км/ч'), G('rpm', 0.08, 0, 0.062, 8, 'x1000'),
        G('fuel', -0.19, 0.0, 0.026, 0, ''), G('temp', 0.18, 0.0, 0.026, 0, '')] },
    backlight: 0x7cff9a, radio: 'ural', console: 'console', lever: 'long', door: 0x2c2c2e, crank: true, clock: true,
  },
  // «Ока»: крошечная панель, один спидометр с указателями, маленький руль
  oka: {
    dash: 'mini', depth: 0.36, dashColor: 0x262626, strip: 0x3a3a3a, trim: 0x303030,
    seat: 0x5a5d62, seat2: 0x3c3e42, pattern: 'plain', headrest: true,
    wheel: { r: 0.18, t: 0.017, spokes: 2, color: 0x1a1a1a, ring: false, pad: 0.07, tilt: 0.55 },
    cluster: { w: 0.26, h: 0.13, hood: 'rect', face: '#0f0f0f', ring: '#606060', ink: '#ffffff', red: '#ff5040',
      gauges: [G('speed', -0.035, 0, 0.058, 150, 'км/ч'), G('fuel', 0.085, 0.02, 0.028, 0, ''), G('temp', 0.085, -0.035, 0.024, 0, '')] },
    backlight: 0xffa060, radio: 'none', console: 'tunnel', lever: 'long', door: 0x3a3a3c, crank: true, hatch: true,
  },
  // «Девятка» (высокая панель): угловатый козырёк щитка, четырёхспицевый руль
  vaz2109: {
    dash: 'angular', depth: 0.5, dashColor: 0x1e1e20, strip: 0x2c2c2e, trim: 0x2a2a2c,
    seat: 0x50555c, seat2: 0x2e3238, pattern: 'checks', headrest: true,
    wheel: { r: 0.19, t: 0.02, spokes: 4, color: 0x151515, ring: false, pad: 0.07, tilt: 0.52 },
    cluster: { w: 0.4, h: 0.15, hood: 'angular', face: '#0b0b0b', ring: '#4a4a4a', ink: '#eaffea', red: '#ff4a3a',
      gauges: [G('speed', -0.09, 0, 0.064, 200, 'км/ч'), G('rpm', 0.09, 0, 0.06, 8, 'x1000'),
        G('fuel', 0.0, 0.035, 0.024, 0, ''), G('temp', 0.0, -0.035, 0.024, 0, '')] },
    backlight: 0x6fe08a, radio: 'ural', console: 'console', lever: 'short', door: 0x323236, crank: true, hatch: true,
  },
  // «Нива»: плоская панель, два прибора, отдельный блок указателей на консоли, раздатка
  niva: {
    dash: 'flat', depth: 0.42, dashColor: 0x1a1a1a, strip: 0x2a2a2a, trim: 0x262422,
    seat: 0x5c4634, seat2: 0x3e2e22, pattern: 'stripes', headrest: true,
    wheel: { r: 0.205, t: 0.016, spokes: 2, color: 0x141414, ring: false, pad: 0.08, tilt: 0.66 },
    cluster: { w: 0.36, h: 0.15, hood: 'rect', face: '#0c0c0c', ring: '#9a9a9a', ink: '#f6f6f0', red: '#e04030',
      gauges: [G('speed', -0.085, 0, 0.064, 160, 'км/ч'), G('rpm', 0.09, 0, 0.058, 8, 'x1000')] },
    backlight: 0xffa860, radio: 'ural', console: 'niva', lever: 'long', door: 0x302c28, crank: true, hatch: true, clock: true,
  },
  // Priora: округлая панель, три «колодца» щитка, трёхспицевый руль, оранжевая подсветка
  priora: {
    dash: 'modern', depth: 0.54, dashColor: 0x232325, strip: 0x55585c, trim: 0x2b2b2d,
    seat: 0x2e3034, seat2: 0x45484e, pattern: 'insert', headrest: true,
    wheel: { r: 0.185, t: 0.022, spokes: 3, color: 0x161616, ring: false, pad: 0.075, tilt: 0.42 },
    cluster: { w: 0.4, h: 0.15, hood: 'tubes', face: '#060606', ring: '#8a8d92', ink: '#ffffff', red: '#ff3a2a',
      gauges: [G('rpm', -0.12, 0, 0.058, 8, 'x1000'), G('speed', 0.0, 0.005, 0.066, 220, 'км/ч'), G('fuelTemp', 0.12, 0, 0.05, 0, '')] },
    backlight: 0xff8a3a, radio: 'head', console: 'console', lever: 'short', door: 0x2a2a2c, crank: false,
  },
  // Granta: два больших колодца, маленький экран, трёхспицевый руль
  granta: {
    dash: 'modern', depth: 0.56, dashColor: 0x1f2022, strip: 0x3a3c40, trim: 0x29292b,
    seat: 0x2a2c30, seat2: 0x4a4e56, pattern: 'insert', headrest: true,
    wheel: { r: 0.185, t: 0.022, spokes: 3, color: 0x141414, ring: false, pad: 0.075, tilt: 0.42 },
    cluster: { w: 0.38, h: 0.15, hood: 'tubes', face: '#050505', ring: '#6a6d72', ink: '#ffffff', red: '#ff3a2a',
      gauges: [G('speed', -0.09, 0, 0.066, 200, 'км/ч'), G('rpm', 0.09, 0, 0.066, 8, 'x1000')] },
    backlight: 0xffb070, radio: 'screen', console: 'console', lever: 'short', door: 0x2a2a2c, crank: false,
  },
  // Vesta: «X-дизайн», серебристые вставки, планшет мультимедиа, руль с кнопками, белая подсветка
  vesta: {
    dash: 'modern', depth: 0.58, dashColor: 0x18191b, strip: 0xa8adb4, trim: 0x232325,
    seat: 0x1c1d20, seat2: 0x6a2a2a, pattern: 'insert', headrest: true,
    wheel: { r: 0.185, t: 0.024, spokes: 3, color: 0x111111, ring: false, pad: 0.08, tilt: 0.38, buttons: true },
    cluster: { w: 0.4, h: 0.16, hood: 'tubes', face: '#030303', ring: '#c0c4ca', ink: '#ffffff', red: '#ff2a2a',
      gauges: [G('speed', -0.11, 0, 0.064, 220, 'км/ч'), G('rpm', 0.11, 0, 0.064, 8, 'x1000'), G('display', 0, 0, 0.04, 0, '')] },
    backlight: 0xf2f6ff, radio: 'tablet', console: 'console', lever: 'short', door: 0x252527, crank: false,
  },
  // Largus: панель Logan, крупный спидометр в центре, трёхспицевый руль
  largus: {
    dash: 'modern', depth: 0.52, dashColor: 0x2a2a2c, strip: 0x4a4c50, trim: 0x2e2e30,
    seat: 0x3a3c40, seat2: 0x26282c, pattern: 'checks', headrest: true,
    wheel: { r: 0.19, t: 0.024, spokes: 3, color: 0x181818, ring: false, pad: 0.08, tilt: 0.44 },
    cluster: { w: 0.36, h: 0.15, hood: 'tubes', face: '#070707', ring: '#7a7d82', ink: '#ffffff', red: '#ff3a2a',
      gauges: [G('rpm', -0.11, -0.005, 0.048, 7, 'x1000'), G('speed', 0.005, 0.005, 0.066, 200, 'км/ч'), G('fuelTemp', 0.115, -0.005, 0.046, 0, '')] },
    backlight: 0x9fe0ff, radio: 'head', console: 'console', lever: 'short', door: 0x303032, crank: false, hatch: true,
  },
};
INTERIOR_STYLES.police = INTERIOR_STYLES.vaz2107;
INTERIOR_STYLES.taxi = INTERIOR_STYLES.granta;

// ------------------------------------------------------------------ геометрия
const _c = new THREE.Color();
function colored(geo, hex) {
  geo = geo.index ? geo.toNonIndexed() : geo;
  for (const k of Object.keys(geo.attributes)) if (k !== 'position' && k !== 'normal') geo.deleteAttribute(k);
  const n = geo.attributes.position.count, c = new Float32Array(n * 3);
  _c.setHex(hex);
  for (let i = 0; i < n; i++) { c[i * 3] = _c.r; c[i * 3 + 1] = _c.g; c[i * 3 + 2] = _c.b; }
  geo.setAttribute('color', new THREE.BufferAttribute(c, 3));
  return geo;
}

function interp(pts, z) {
  if (z >= pts[0][0]) return pts[0][1];
  for (let i = 1; i < pts.length; i++) {
    const [z1, y1] = pts[i], [z0, y0] = pts[i - 1];
    if (z >= z1) return y1 + ((y0 - y1) * (z - z1)) / (z0 - z1 || 1);
  }
  return pts[pts.length - 1][1];
}

class Parts {
  constructor() { this.list = []; this._e = new THREE.Euler(); this._m = new THREE.Matrix4(); this._q = new THREE.Quaternion(); }
  add(geo, hex, x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0) {
    this._m.compose(new THREE.Vector3(x, y, z), this._q.setFromEuler(this._e.set(rx, ry, rz)), new THREE.Vector3(1, 1, 1));
    this.list.push(colored(geo, hex).applyMatrix4(this._m));
  }
  box(w, h, d, hex, x, y, z, rx, ry, rz) { this.add(new THREE.BoxGeometry(w, h, d), hex, x, y, z, rx, ry, rz); }
  rbox(w, h, d, r, hex, x, y, z, rx, ry, rz) { this.add(new RoundedBoxGeometry(w, h, d, 2, Math.min(r, w / 2, h / 2, d / 2) * 0.99), hex, x, y, z, rx, ry, rz); }
  cyl(r, h, hex, x, y, z, rx = 0, ry = 0, rz = 0, seg = 10) { this.add(new THREE.CylinderGeometry(r, r, h, seg), hex, x, y, z, rx, ry, rz); }
  /** Вертикальная панель (обшивка двери) от пола до линии окон, повторяет линию окон. */
  side(x, z0, z1, yBottom, topFn, hex, inward) {
    const N = 8, pos = [], nrm = [];
    for (let i = 0; i < N; i++) {
      const za = z0 + ((z1 - z0) * i) / N, zb = z0 + ((z1 - z0) * (i + 1)) / N;
      const ta = topFn(za), tb = topFn(zb);
      const q = [[za, yBottom], [zb, yBottom], [zb, tb], [za, ta]];
      const tri = inward > 0 ? [0, 2, 1, 0, 3, 2] : [0, 1, 2, 0, 2, 3];
      for (const k of tri) { pos.push(x, q[k][1], q[k][0]); nrm.push(inward, 0, 0); }
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(nrm, 3));
    this.list.push(colored(g, hex));
  }
  mesh(mat) {
    const g = mergeGeometries(this.list, false);
    g.computeBoundingSphere();
    return new THREE.Mesh(g, mat);
  }
}

// ------------------------------------------------------------------ щиток (текстура)
function drawCluster(cl) {
  const PX = 1100; // пикселей на метр
  const W = Math.ceil(cl.w * PX), H = Math.ceil(cl.h * PX);
  const c = document.createElement('canvas'); c.width = W; c.height = H;
  const g = c.getContext('2d');
  g.fillStyle = '#000'; g.fillRect(0, 0, W, H);
  // фон щитка
  g.fillStyle = cl.face;
  g.fillRect(0, 0, W, H);
  const P = (x, y) => [W / 2 + x * PX, H / 2 - y * PX];
  for (const gg of cl.gauges) {
    const [cx, cy] = P(gg.x, gg.y), R = gg.r * PX;
    if (gg.type === 'display') {
      g.fillStyle = '#0a1420'; g.fillRect(cx - R, cy - R * 1.2, R * 2, R * 2.4);
      g.fillStyle = '#9fd0ff'; g.font = `bold ${R * 0.45}px Arial`; g.textAlign = 'center';
      g.fillText('ECO', cx, cy - R * 0.4); g.font = `${R * 0.35}px Arial`; g.fillText('D  12:00', cx, cy + R * 0.3);
      continue;
    }
    // ободок
    g.beginPath(); g.arc(cx, cy, R, 0, Math.PI * 2);
    g.fillStyle = '#050505'; g.fill();
    g.lineWidth = Math.max(2, R * 0.07); g.strokeStyle = cl.ring; g.stroke();
    const ang = (f) => (225 - 270 * f) * Math.PI / 180;
    const tick = (f, len, wdt, col) => {
      const a = ang(f);
      g.beginPath();
      g.moveTo(cx + Math.cos(a) * R * 0.86, cy - Math.sin(a) * R * 0.86);
      g.lineTo(cx + Math.cos(a) * R * (0.86 - len), cy - Math.sin(a) * R * (0.86 - len));
      g.lineWidth = wdt; g.strokeStyle = col; g.stroke();
    };
    g.textAlign = 'center'; g.textBaseline = 'middle';
    if (gg.type === 'speed' || gg.type === 'rpm') {
      const major = gg.type === 'speed' ? 20 : 1, n = gg.max / major;
      for (let i = 0; i <= n * 2; i++) {
        const f = i / (n * 2), redz = gg.type === 'rpm' && f * gg.max >= gg.max * 0.75;
        tick(f, i % 2 ? 0.08 : 0.16, i % 2 ? 1.5 : 3, redz ? cl.red : cl.ink);
      }
      g.font = `bold ${Math.round(R * 0.2)}px Arial`;
      for (let i = 0; i <= n; i++) {
        const f = i / n, a = ang(f), v = i * major;
        if (gg.type === 'speed' && n > 9 && i % 2) continue;
        g.fillStyle = gg.type === 'rpm' && v >= gg.max * 0.75 ? cl.red : cl.ink;
        g.fillText(String(v), cx + Math.cos(a) * R * 0.56, cy - Math.sin(a) * R * 0.56);
      }
      g.font = `${Math.round(R * 0.14)}px Arial`; g.fillStyle = cl.ink;
      g.fillText(gg.label, cx, cy + R * 0.42);
      if (gg.type === 'speed') { // одометр
        g.fillStyle = '#1a1a1a'; g.fillRect(cx - R * 0.32, cy + R * 0.52, R * 0.64, R * 0.17);
        g.fillStyle = '#ddd'; g.font = `${Math.round(R * 0.14)}px monospace`; g.fillText('084217', cx, cy + R * 0.61);
      }
    } else {
      // малые: топливо / температура / комбинированный
      const both = gg.type === 'combo' || gg.type === 'fuelTemp';
      const arc = (f0, f1, col) => { g.beginPath(); g.arc(cx, cy, R * 0.75, -ang(f0), -ang(f1)); g.lineWidth = R * 0.08; g.strokeStyle = col; g.stroke(); };
      if (gg.type === 'fuel' || both) { for (let i = 0; i <= 4; i++) tick(i / 4 * (both ? 0.45 : 1), 0.18, 2, i === 0 ? cl.red : cl.ink); }
      if (gg.type === 'temp' || both) { for (let i = 0; i <= 4; i++) tick((both ? 0.55 : 0) + i / 4 * (both ? 0.45 : 1), 0.18, 2, i === 4 ? cl.red : cl.ink); }
      if (both) { arc(0, 0.08, cl.red); arc(0.92, 1, cl.red); }
      g.fillStyle = cl.ink; g.font = `bold ${Math.round(R * 0.26)}px Arial`;
      if (gg.type === 'fuel') g.fillText('⛽', cx, cy + R * 0.4);
      else if (gg.type === 'temp') g.fillText('°C', cx, cy + R * 0.4);
      else { g.fillText('⛽', cx - R * 0.38, cy + R * 0.45); g.fillText('°C', cx + R * 0.38, cy + R * 0.45); }
    }
  }
  // контрольные лампы
  const lamps = [['#2a8', 'ⓟ'], ['#e33', '!'], ['#fa3', '⚠']];
  g.font = `bold ${Math.round(H * 0.08)}px Arial`; g.textAlign = 'center';
  lamps.forEach(([col], i) => { g.fillStyle = col; g.globalAlpha = 0.25; g.beginPath(); g.arc(W / 2 + (i - 1) * H * 0.12, H * 0.9, H * 0.025, 0, 7); g.fill(); });
  g.globalAlpha = 1;
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}

/** Экран магнитолы/мультимедиа. */
function drawScreen(kind) {
  const c = document.createElement('canvas'); c.width = 256; c.height = kind === 'tablet' ? 160 : 64;
  const g = c.getContext('2d');
  if (kind === 'ural') {
    g.fillStyle = '#0f1a0f'; g.fillRect(0, 0, 256, 64);
    g.fillStyle = '#7dff8a'; g.font = 'bold 30px monospace'; g.textAlign = 'center'; g.fillText('101.7 FM', 128, 44);
  } else if (kind === 'head') {
    g.fillStyle = '#081018'; g.fillRect(0, 0, 256, 64);
    g.fillStyle = '#ffae5a'; g.font = 'bold 26px Arial'; g.textAlign = 'center'; g.fillText('CAUCASUS FM', 128, 42);
  } else {
    const grd = g.createLinearGradient(0, 0, 256, c.height); grd.addColorStop(0, '#10243c'); grd.addColorStop(1, '#05080e');
    g.fillStyle = grd; g.fillRect(0, 0, 256, c.height);
    g.fillStyle = '#fff'; g.font = 'bold 22px Arial'; g.textAlign = 'left'; g.fillText('CAUCASUS FM', 16, 34);
    g.fillStyle = '#9fd0ff'; g.font = '16px Arial'; g.fillText('Лезгинка — ремикс', 16, 58);
    if (kind === 'tablet') {
      for (let i = 0; i < 4; i++) { g.fillStyle = ['#2a6', '#36c', '#c63', '#888'][i]; g.fillRect(16 + i * 60, 90, 50, 50); }
    }
  }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

// ------------------------------------------------------------------ сборка
/**
 * @returns {{ group, update(state), setDriverVisible(v), setFirstPerson(v), eye: Vector3, look: Vector3 }}
 */
export function buildInterior(def, { driver = true } = {}) {
  const key = def.key || def.id;
  const st = INTERIOR_STYLES[key] || INTERIOR_STYLES[def.id] || INTERIOR_STYLES.vaz2107;
  const A = ANCHORS[key] || ANCHORS[def.id] || ANCHORS.vaz2107;
  const group = new THREE.Group();
  group.name = 'Interior';
  const P = new Parts();

  const W2 = A.W / 2;
  const belt = (z) => interp(A.belt, z);
  const roofAt = (z) => interp(A.top, z);
  const floorY = A.floor + 0.12;
  const hwB = W2 - 0.085;                 // внутренняя полуширина на уровне окон
  const hwR = W2 - A.tumble - 0.07;       // под крышей
  const zDashF = A.zW0 + 0.04;
  const zDash = A.zW0 - st.depth;         // задняя (к водителю) плоскость торпедо
  const yDashTop = belt(A.zW0) + (st.dash === 'block' || st.dash === 'angular' ? 0.03 : 0.0);
  // потолок (по самой низкой точке крыши над салоном)
  const zr0 = A.zW1 - 0.03, zr1 = A.zB1 + 0.03;
  let roofMin = 9; for (let z = zr1; z <= zr0; z += 0.1) roofMin = Math.min(roofMin, roofAt(z));
  const ceiling = roofMin - 0.055;
  // H-точка водителя: посадка такая, чтобы над глазами оставалось ~20 см до потолка
  const Hz = zDash - 0.68;
  const Hy = Math.min(Math.max(ceiling - 0.95, floorY + 0.12), floorY + (key === 'niva' ? 0.26 : 0.22));
  const DX = 0.36;
  const trim = st.trim, dark = 0x121212, chrome = 0xb8bcc0;

  // --- пол и тоннель
  const zRear = Math.max(A.zB0 + 0.05, Hz - 1.35);
  P.box(hwB * 2, 0.03, zDashF - zRear, 0x1a1a1a, 0, floorY - 0.015, (zDashF + zRear) / 2);
  P.box(0.24, 0.1, zDashF - zRear - 0.2, 0x1c1c1c, 0, floorY + 0.05, (zDashF + zRear) / 2 + 0.1);

  // --- торпедо
  const dashH = 0.27, dashD = zDashF - zDash;
  if (st.dash === 'modern') {
    P.rbox(hwB * 2, dashH, dashD, 0.08, st.dashColor, 0, yDashTop - dashH / 2, zDash + dashD / 2);
    P.rbox(hwB * 2 - 0.06, 0.035, 0.05, 0.015, st.strip, 0, yDashTop - 0.17, zDash + 0.01);            // декоративная полоса
    P.rbox(0.26, 0.42, 0.22, 0.04, st.dashColor, 0, yDashTop - 0.36, zDash + 0.08);                   // центральная консоль
    if (key === 'vesta') { // X-образные вставки
      P.box(0.03, 0.4, 0.02, st.strip, 0.15, yDashTop - 0.33, zDash - 0.02, 0, 0, 0.35);
      P.box(0.03, 0.4, 0.02, st.strip, -0.15, yDashTop - 0.33, zDash - 0.02, 0, 0, -0.35);
    }
  } else if (st.dash === 'angular') {
    P.box(hwB * 2, dashH, dashD, st.dashColor, 0, yDashTop - dashH / 2, zDash + dashD / 2);
    P.box(hwB * 2 - 0.04, 0.06, 0.12, st.dashColor, 0, yDashTop - dashH + 0.02, zDash - 0.05, -0.4);   // нижняя полка
    P.box(0.24, 0.36, 0.16, st.dashColor, 0, yDashTop - 0.38, zDash + 0.04);
  } else if (st.dash === 'mini') {
    P.rbox(hwB * 2, 0.2, dashD, 0.04, st.dashColor, 0, yDashTop - 0.1, zDash + dashD / 2);
    P.box(hwB * 2 - 0.1, 0.04, 0.16, 0x1a1a1a, 0, yDashTop - 0.22, zDash + 0.06);                      // полочка
  } else {
    P.box(hwB * 2, dashH, dashD, st.dashColor, 0, yDashTop - dashH / 2, zDash + dashD / 2);
    P.box(hwB * 2 - 0.02, 0.05, 0.012, st.strip, 0, yDashTop - 0.06, zDash - 0.004);                  // накладка (металл/дерево)
    if (st.dash === 'block') {
      P.box(0.3, 0.3, 0.14, st.dashColor, 0, yDashTop - 0.38, zDash + 0.05);                          // консоль «семёрки»
      P.box(0.28, 0.05, 0.01, st.strip, 0, yDashTop - 0.3, zDash - 0.021);
    }
  }
  // верх торпедо до лобового, перегородка ног
  P.box(hwB * 2, 0.025, A.zW0 - zDash + 0.05, st.dashColor, 0, yDashTop + 0.005, (A.zW0 + zDash) / 2 + 0.02, 0.06);
  P.box(hwB * 2, yDashTop - dashH - floorY, 0.04, 0x161616, 0, (yDashTop - dashH + floorY) / 2, zDashF - 0.06);
  // дефлекторы и бардачок
  for (const vx of [-hwB + 0.14, -0.07, 0.07, hwB - 0.14]) P.box(0.1, 0.045, 0.01, 0x0c0c0c, vx, yDashTop - 0.1, zDash - 0.006);
  P.box(0.34, 0.12, 0.01, st.dashColor === 0x181818 ? 0x202020 : st.dashColor + 0x060606, -0.38, yDashTop - 0.18, zDash - 0.006);

  // --- козырёк щитка
  const cl = st.cluster;
  // щиток утоплен в торпедо (виден над ступицей, сквозь верхний сектор руля), над ним — козырёк
  const cy = yDashTop - cl.h / 2 + 0.03;
  const cz = zDash - 0.025;   // лицо щитка чуть впереди торпедо (к водителю)
  const hood = cl.hood;
  const bezel = hood === 'angular' || hood === 'rect' ? 0x0e0e0e : 0x151515;
  if (hood === 'oval' || hood === 'tubes') P.rbox(cl.w + 0.05, cl.h + 0.05, 0.04, 0.02, bezel, DX, cy, zDash);
  else P.box(cl.w + 0.04, cl.h + 0.04, 0.04, bezel, DX, cy, zDash);
  const visorD = hood === 'angular' ? 0.13 : st.dash === 'modern' ? 0.1 : 0.07;
  P.rbox(cl.w + 0.1, 0.04, visorD + 0.06, 0.018, st.dashColor, DX, cy + cl.h / 2 + 0.035, zDash - visorD / 2 + 0.03, hood === 'angular' ? -0.12 : 0);
  P.box(cl.w + 0.08, 0.07, 0.06, st.dashColor, DX, cy + cl.h / 2 - 0.005, zDash + 0.01);                // боковины козырька

  // --- магнитола / экран
  let screenMesh = null;
  if (st.radio !== 'none') {
    const kind = st.radio;
    const sw = kind === 'tablet' ? 0.2 : kind === 'screen' ? 0.15 : 0.18, sh = kind === 'tablet' ? 0.125 : kind === 'screen' ? 0.06 : 0.05;
    const sy = kind === 'tablet' ? yDashTop + 0.04 : yDashTop - 0.24;
    const sz = kind === 'tablet' ? zDash + 0.06 : zDash - 0.012;
    P.box(sw + 0.03, sh + 0.04, 0.03, 0x0d0d0d, 0, sy, sz + 0.012);
    if (kind === 'ural') for (const kx of [-0.075, 0.075]) P.cyl(0.012, 0.02, 0x8a8a8a, kx, sy, sz - 0.01, Math.PI / 2);
    const scr = new THREE.Mesh(new THREE.PlaneGeometry(kind === 'ural' ? sw * 0.5 : sw, sh), new THREE.MeshBasicMaterial({ map: drawScreen(kind), toneMapped: false }));
    scr.position.set(0, sy, sz - 0.004);
    scr.rotation.y = Math.PI;
    scr.rotation.x = kind === 'tablet' ? 0.25 : 0;
    group.add(scr);
    screenMesh = scr;
  }
  // часы на консоли (классика)
  let clockHands = null;
  if (st.clock) {
    const kx = 0, ky = st.radio !== 'none' ? yDashTop - 0.14 : yDashTop - 0.2, kz = zDash - 0.01;
    P.cyl(0.04, 0.012, 0x8a8a8a, kx, ky, kz, Math.PI / 2, 0, 0, 16);
    P.cyl(0.035, 0.014, 0xf0f0e8, kx, ky, kz - 0.002, Math.PI / 2, 0, 0, 16);
    clockHands = new THREE.Group();
    clockHands.position.set(kx, ky, kz - 0.011);
    clockHands.rotation.y = Math.PI;
    const hm = new THREE.MeshBasicMaterial({ color: 0x111111 });
    const hh = new THREE.Mesh(new THREE.BoxGeometry(0.004, 0.022, 0.002).translate(0, 0.011, 0), hm);
    const mm = new THREE.Mesh(new THREE.BoxGeometry(0.003, 0.03, 0.002).translate(0, 0.015, 0.001), hm);
    clockHands.add(hh, mm);
    clockHands.userData = { hh, mm };
    group.add(clockHands);
  }

  // --- консоль, рычаги, педали
  if (st.console === 'console' || st.console === 'niva') P.rbox(0.2, 0.16, 0.5, 0.03, st.dashColor, 0, floorY + 0.12, Hz + 0.25);
  if (st.console === 'niva') { // блок дополнительных приборов и рычаги раздатки
    for (let i = 0; i < 3; i++) P.cyl(0.022, 0.012, 0x9a9a9a, -0.05 + i * 0.05, yDashTop - 0.2, zDash - 0.008, Math.PI / 2, 0, 0, 12);
    P.cyl(0.006, 0.22, chrome, 0.05, floorY + 0.2, Hz + 0.42, -0.4); P.add(new THREE.SphereGeometry(0.018, 8, 6), dark, 0.05, floorY + 0.3, Hz + 0.37);
  }
  if (st.lever === 'long') {
    P.cyl(0.007, 0.42, chrome, 0, floorY + 0.25, Hz + 0.42, -0.3);
    P.add(new THREE.SphereGeometry(0.025, 10, 8), dark, 0, floorY + 0.45, Hz + 0.36);
    P.cyl(0.05, 0.05, 0x111111, 0, floorY + 0.1, Hz + 0.47, 0, 0, 0, 10);
  } else {
    P.cyl(0.04, 0.08, 0x111111, 0, floorY + 0.22, Hz + 0.3, -0.2, 0, 0, 10);
    P.cyl(0.008, 0.14, 0x222222, 0, floorY + 0.3, Hz + 0.28, -0.2);
    P.rbox(0.04, 0.06, 0.05, 0.015, st.radio === 'tablet' ? 0x2a2a2a : dark, 0, floorY + 0.38, Hz + 0.26);
  }
  P.box(0.03, 0.03, 0.22, 0x161616, 0, floorY + (st.console === 'tunnel' ? 0.13 : 0.22), Hz - 0.02, 0.22);  // ручник
  for (let i = 0; i < 3; i++) P.box(0.055, 0.075, 0.015, 0x202020, DX + 0.12 - i * 0.1 - (i === 2 ? 0.02 : 0), floorY + 0.14, Hz + 0.95, 0.5);

  // --- кресла
  const seatTex = (x, sz, sy, seatW, back) => {
    const c1 = st.seat, c2 = st.seat2;
    // подушка
    P.rbox(seatW, 0.13, 0.5, 0.05, c1, x, sy, sz, -0.08);
    if (st.pattern !== 'plain') P.rbox(seatW * 0.55, 0.012, 0.4, 0.005, c2, x, sy + 0.066, sz, -0.08);
    if (!back) return;
    // спинка, наклон назад
    const a = -0.24, bh = 0.6, by = sy + 0.06 + (bh / 2) * Math.cos(a), bz = sz - 0.24 + (bh / 2) * Math.sin(a);
    P.rbox(seatW, bh, 0.13, 0.05, c1, x, by, bz, a);
    if (st.pattern !== 'plain') {
      // вставка обивки на лицевой стороне спинки: вверх (0, cos a, sin a), наружу (0, −sin a, cos a)
      const ux = Math.cos(a), uz = Math.sin(a), ny = -Math.sin(a) * 0.066, nz = Math.cos(a) * 0.066;
      const ins = st.pattern === 'checks' ? 6 : st.pattern === 'stripes' ? 7 : 1;
      if (ins === 1) P.rbox(seatW * 0.55, bh * 0.75, 0.012, 0.005, c2, x, by + ny, bz + nz, a);
      else for (let i = 0; i < ins; i++) {
        const t = (-0.38 + i * (0.76 / (ins - 1))) * bh;
        P.box(seatW * 0.58, st.pattern === 'checks' ? 0.05 : 0.022, 0.012, (i % 2 && st.pattern === 'checks') ? c1 : c2, x, by + t * ux + ny, bz + t * uz + nz, a);
      }
    }
    if (st.headrest && back === 'front') {
      const hy = sy + 0.06 + (bh + 0.1) * Math.cos(a), hz = sz - 0.24 + (bh + 0.1) * Math.sin(a);
      P.rbox(0.26, 0.17, 0.09, 0.04, c1, x, hy, hz, a);
      P.cyl(0.006, 0.06, chrome, x + 0.06, hy - 0.1, hz + 0.01); P.cyl(0.006, 0.06, chrome, x - 0.06, hy - 0.1, hz + 0.01);
    }
  };
  const seatY = Hy - 0.05;
  seatTex(DX, Hz + 0.12, seatY, 0.5, 'front');
  seatTex(-DX, Hz + 0.12, seatY, 0.5, 'front');
  const rearZ = Hz - 0.8;
  if (rearZ - 0.3 > A.zB0 - 0.35) seatTex(0, rearZ, seatY + 0.02, hwB * 2 - 0.12, 'rear');
  // полка / багажник
  if (!st.hatch && A.zB0 < rearZ - 0.3) P.box(hwB * 2, 0.02, rearZ - 0.32 - A.zB0, 0x1e1e1e, 0, belt(A.zB0) - 0.02, (rearZ - 0.32 + A.zB0) / 2);

  // --- обшивка дверей и боковин (закрывает изнанку кузова)
  const zF = zDash + 0.12, zB = zRear - 0.05;
  for (const sx of [1, -1]) {
    P.side(sx * (W2 - 0.07), zB, zF, floorY - 0.02, (z) => belt(z) - 0.008, st.door, -sx);
    P.box(0.06, 0.04, 0.42, st.trim, sx * (W2 - 0.1), Hy + 0.15, Hz + 0.2);                          // подлокотник
    P.box(0.012, 0.025, 0.08, chrome, sx * (W2 - 0.078), belt(Hz + 0.45) - 0.12, Hz + 0.45);           // ручка
    if (st.crank) { P.cyl(0.03, 0.01, chrome, sx * (W2 - 0.08), Hy + 0.05, Hz + 0.35, 0, 0, Math.PI / 2, 10); P.cyl(0.008, 0.05, 0x111111, sx * (W2 - 0.095), Hy + 0.07, Hz + 0.38, 0, 0, Math.PI / 2); }
    // нижняя часть двери и карман
    P.box(0.02, 0.12, 0.5, 0x161616, sx * (W2 - 0.08), floorY + 0.12, Hz + 0.3);
  }
  // потолок
  P.box(hwR * 2, 0.02, zr0 - zr1, 0xc9c4b8, 0, roofMin - 0.045, (zr0 + zr1) / 2);
  P.box(0.12, 0.02, 0.06, 0xeeeeee, 0, roofMin - 0.058, (zr0 + zr1) / 2);                                  // плафон
  // козырьки и зеркало
  for (const sx of [1, -1]) P.box(0.32, 0.016, 0.16, 0xc0bab0, sx * 0.36, roofMin - 0.07, A.zW1 - 0.1, 0.12);
  P.rbox(0.22, 0.06, 0.025, 0.012, 0x1a1a1a, 0, roofMin - 0.11, A.zW1 - 0.08, -0.15);
  P.box(0.2, 0.045, 0.004, 0x9fb2c0, 0, roofMin - 0.11, A.zW1 - 0.094, -0.15);

  // в салоне нет прямого солнца (крыша), поэтому добавляем мягкую «заполняющую» подсветку
  // по цвету вершин; её яркость следует за дневным светом (uniform fill)
  const fill = { value: 0.35 };
  const fillMat = (o = {}) => {
    const m = new THREE.MeshLambertMaterial({ vertexColors: true, ...o });
    m.onBeforeCompile = (sh) => {
      sh.uniforms.uFill = fill;
      sh.fragmentShader = 'uniform float uFill;\n' + sh.fragmentShader.replace('#include <emissivemap_fragment>',
        '#include <emissivemap_fragment>\n  totalEmissiveRadiance += vColor.rgb * uFill;');
    };
    return m;
  };
  const staticMat = fillMat();
  const shell = P.mesh(staticMat);
  shell.name = 'InteriorShell';
  group.add(shell);

  // --- щиток приборов: текстура + стрелки
  const clusterRoot = new THREE.Group();
  clusterRoot.position.set(DX, cy, cz);
  clusterRoot.rotation.y = Math.PI;              // лицом к водителю; local +x — вправо для водителя
  const tilt = new THREE.Group();
  tilt.rotation.x = -0.12;
  clusterRoot.add(tilt);
  const faceTex = drawCluster(cl);
  const faceMat = new THREE.MeshLambertMaterial({ map: faceTex, emissiveMap: faceTex, emissive: new THREE.Color(st.backlight), emissiveIntensity: 0.25 });
  tilt.add(new THREE.Mesh(new THREE.PlaneGeometry(cl.w, cl.h), faceMat));
  const needleMat = new THREE.MeshBasicMaterial({ color: key === 'vesta' ? 0xff3030 : 0xff6a1a, toneMapped: false });
  const needles = {};
  for (const gg of cl.gauges) {
    if (gg.type === 'display') continue;
    const mk = (type, startF = 0, spanF = 1) => {
      const pivot = new THREE.Group();
      pivot.position.set(gg.x, gg.y, 0.006);
      const n = new THREE.Mesh(new THREE.BoxGeometry(gg.r * 0.82, Math.max(0.0025, gg.r * 0.05), 0.002).translate(gg.r * 0.36, 0, 0), needleMat);
      pivot.add(n);
      const cap = new THREE.Mesh(new THREE.CylinderGeometry(gg.r * 0.1, gg.r * 0.1, 0.004, 10).rotateX(Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x222222 }));
      pivot.add(cap);
      tilt.add(pivot);
      (needles[type] ||= []).push({ pivot, startF, spanF, max: gg.max });
    };
    if (gg.type === 'combo' || gg.type === 'fuelTemp') { mk('fuel', 0, 0.45); mk('temp', 0.55, 0.45); }
    else mk(gg.type);
  }
  group.add(clusterRoot);

  // --- руль
  const wd = st.wheel;
  const wheelPos = new THREE.Vector3(DX, Hy + 0.33, Hz + 0.47);
  const wheelRoot = new THREE.Group();
  wheelRoot.position.copy(wheelPos);
  wheelRoot.rotation.y = Math.PI;
  const wheelTilt = new THREE.Group();
  wheelTilt.rotation.x = -wd.tilt;               // верх обода — к приборам
  wheelRoot.add(wheelTilt);
  const spin = new THREE.Group();
  wheelTilt.add(spin);
  const WP = new Parts();
  WP.add(new THREE.TorusGeometry(wd.r, wd.t, 8, 36), wd.color);
  WP.cyl(wd.pad * 0.9, 0.05, wd.color === 0x111111 ? 0x1a1a1a : wd.color, 0, 0, 0.02, Math.PI / 2, 0, 0, 16);
  WP.cyl(wd.pad * 0.35, 0.052, key.startsWith('vaz2') || key === 'niva' || key === 'oka' ? 0xb0b4b8 : 0x9aa6b8, 0, 0, 0.022, Math.PI / 2, 0, 0, 12); // эмблема-ладья
  const spokeAng = wd.spokes === 2 ? [200, 340] : wd.spokes === 3 ? [180, 0, 270] : [215, 325, 145, 35];
  for (const a of spokeAng) {
    const r = a * Math.PI / 180, len = wd.r - wd.pad * 0.6;
    const cx2 = Math.cos(r) * (wd.pad * 0.6 + len / 2), cy2 = Math.sin(r) * (wd.pad * 0.6 + len / 2);
    WP.box(len, wd.spokes === 2 ? 0.022 : 0.04, 0.014, wd.color, cx2, cy2, 0.008, 0, 0, r);
  }
  if (wd.ring) WP.add(new THREE.TorusGeometry(wd.r * 0.62, 0.004, 4, 28), chrome, 0, 0, 0.02);
  if (wd.buttons) for (const bx of [-0.08, 0.08]) WP.box(0.04, 0.03, 0.006, 0x2a2a2a, bx, -0.02, 0.016);
  spin.add(WP.mesh(fillMat()));
  // рулевая колонка
  const col = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.045, 0.32, 10).rotateX(Math.PI / 2).translate(0, 0, -0.17), new THREE.MeshLambertMaterial({ color: 0x151515 }));
  wheelTilt.add(col);
  group.add(wheelRoot);

  // --- водитель
  let drv = null;
  if (driver) {
    drv = buildCharacter(OUTFITS.player, { shadows: false });
    drv.root.position.set(DX, Hy - 0.98 + 0.04, Hz - 0.04);
    const B = drv.bones;
    B.spine.rotation.x = -0.24; B.chest.rotation.x = 0.02; B.neck.rotation.x = 0.1; B.head.rotation.x = 0.08;
    for (const s of ['L', 'R']) {
      const sx = s === 'L' ? 1 : -1;
      B['thigh' + s].rotation.set(-1.42, 0, sx * 0.1);
      B['shin' + s].rotation.set(1.05, 0, 0);
      B['foot' + s].rotation.set(-0.25, 0, 0);
    }
    group.add(drv.root);
  }

  // глаза водителя: по фактической позе головы (или по антропометрии, если водителя нет)
  const eye = new THREE.Vector3(DX, Hy + 0.74, Hz - 0.08);
  if (drv) {
    group.updateMatrixWorld(true);
    const inv = new THREE.Matrix4().copy(group.matrixWorld).invert();
    const hp = drv.bones.head.getWorldPosition(new THREE.Vector3()).applyMatrix4(inv);
    eye.set(DX - 0.035, hp.y + 0.11, hp.z + 0.09);
  }
  const _t = new THREE.Vector3();
  const state = { steerA: 0 };

  function valueToAngle(f) { return (225 - 270 * Math.max(0, Math.min(1, f))) * Math.PI / 180; }

  return {
    group, eye, wheelPos, style: st, driver: drv,
    look: new THREE.Vector3(DX, eye.y - 0.95, eye.z + 10),
    setDriverVisible(v) { if (drv) drv.root.visible = v; },
    /** В виде «из салона» камера стоит в голове водителя — голову прячем. */
    setFirstPerson(v) { if (drv) { drv.bones.neck.scale.setScalar(v ? 0.001 : 1); } },
    /**
     * @param s { steer (−1..1, + вправо), kmh, rpm, fuel (0..1), temp (0..1), lights, time (часы) }
     */
    update(dt, s) {
      // руль: ±150° как у виртуального руля на экране
      const target = -s.steer * 2.62;
      state.steerA += (target - state.steerA) * Math.min(1, dt * 18);
      spin.rotation.z = state.steerA;
      // стрелки
      const set = (type, f) => { for (const n of needles[type] || []) n.pivot.rotation.z = valueToAngle(n.startF + f * n.spanF); };
      if (needles.speed) for (const n of needles.speed) n.pivot.rotation.z = valueToAngle(s.kmh / n.max);
      if (needles.rpm) for (const n of needles.rpm) n.pivot.rotation.z = valueToAngle(s.rpm / 1000 / n.max);
      set('fuel', s.fuel);
      set('temp', s.temp);
      faceMat.emissiveIntensity = s.lights ? 1.1 : 0.35;
      fill.value = 0.08 + (s.daylight ?? 1) * 0.32 + (s.lights ? 0.04 : 0);
      if (clockHands) {
        const h = s.time % 12, m = (s.time % 1) * 60;
        clockHands.userData.hh.rotation.z = -h / 12 * Math.PI * 2;
        clockHands.userData.mm.rotation.z = -m / 60 * Math.PI * 2;
      }
      // руки на руле: «10 и 2», следуют за ободом до ±90°, дальше перехватывают
      if (drv && drv.root.visible) {
        const B = drv.bones;
        B.head.rotation.y = -s.steer * 0.35;
        drv.root.updateMatrixWorld(true);
        wheelTilt.updateMatrixWorld(true);
        const lim = Math.max(-1.55, Math.min(1.55, state.steerA));
        for (const [side, base] of [['L', 150], ['R', 30]]) {
          const a = base * Math.PI / 180 + lim;
          _t.set(Math.cos(a) * wd.r, Math.sin(a) * wd.r, 0.035);
          wheelTilt.localToWorld(_t);
          solveArmIK(B, side, _t, 1, [0.6, -1, -0.4]);
          B['hand' + side].rotation.set(0.5, 0, (side === 'L' ? 1 : -1) * 0.3);
        }
      }
    },
  };
}
