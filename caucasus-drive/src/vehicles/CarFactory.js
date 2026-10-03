import * as THREE from 'three';
import { buildInterior } from './Interior.js';
import { prep, merge } from '../utils/geometry.js';
import { ModelLibrary, meshesFromLod } from './ModelLibrary.js';

/**
 * Параметрический генератор автомобилей по описанию из config/cars.js.
 *  - кузов: боковой профиль с вырезанными арками, выдавленный по ширине (с фаской)
 *  - кабина: контур «оранжереи», стёкла строятся автоматически (inset + clip по стойкам)
 *  - оптика/решётки/бамперы — по стилю модели (круглые ×4, прямоугольные, клин, современные, X-face)
 *  - детализация: high (игрок, ~3k треуг.), mid (трафик LOD0, ~1k), low (LOD1, ~200)
 */

// ------------------------------------------------------------------ 2D-утилиты
const lerp2 = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];

function signedArea(poly) {
  let s = 0;
  for (let i = 0; i < poly.length; i++) {
    const p = poly[i], q = poly[(i + 1) % poly.length];
    s += p[0] * q[1] - q[0] * p[1];
  }
  return s / 2;
}

/** Смещение выпуклого CCW-многоугольника внутрь на d. */
function insetPoly(poly, d) {
  const n = poly.length;
  const lines = [];
  for (let i = 0; i < n; i++) {
    const p = poly[i], q = poly[(i + 1) % n];
    const dx = q[0] - p[0], dy = q[1] - p[1];
    const len = Math.hypot(dx, dy);
    const nx = -dy / len, ny = dx / len; // левая нормаль = внутрь для CCW
    lines.push([p[0] + nx * d, p[1] + ny * d, dx, dy]);
  }
  const out = [];
  for (let i = 0; i < n; i++) {
    const a = lines[(i - 1 + n) % n], b = lines[i];
    const den = a[2] * b[3] - a[3] * b[2];
    if (Math.abs(den) < 1e-9) { out.push([b[0], b[1]]); continue; }
    const t = ((b[0] - a[0]) * b[3] - (b[1] - a[1]) * b[2]) / den;
    out.push([a[0] + a[2] * t, a[1] + a[3] * t]);
  }
  return out;
}

/** Отсечение полуплоскостью a·x + b·y + c ≥ 0 (Сазерленд–Ходжмен). */
function clipPoly(poly, a, b, c) {
  const out = [];
  for (let i = 0; i < poly.length; i++) {
    const P = poly[i], Q = poly[(i + 1) % poly.length];
    const fp = a * P[0] + b * P[1] + c, fq = a * Q[0] + b * Q[1] + c;
    if (fp >= 0) out.push(P);
    if ((fp >= 0) !== (fq >= 0)) out.push(lerp2(P, Q, fp / (fp - fq)));
  }
  return out;
}

// ------------------------------------------------------------------ примитивы
const box = (w, h, d, x, y, z) => new THREE.BoxGeometry(w, h, d).translate(x, y, z);
const cylZ = (r, len, seg, x, y, z) => new THREE.CylinderGeometry(r, r, len, seg).rotateX(Math.PI / 2).translate(x, y, z);
const cylX = (r, len, seg, x, y, z) => new THREE.CylinderGeometry(r, r, len, seg).rotateZ(Math.PI / 2).translate(x, y, z);

function extrudeSide(shape, width, bevel, curveSegments) {
  const depth = width - 2 * bevel;
  const g = new THREE.ExtrudeGeometry(shape, {
    depth, steps: 1, curveSegments,
    bevelEnabled: bevel > 0, bevelThickness: bevel, bevelSize: bevel, bevelSegments: 1,
  });
  g.rotateY(-Math.PI / 2);
  g.translate(depth / 2, 0, 0);
  return g;
}

/** Плоский выпуклый многоугольник (z,y) на плоскости x, нормаль (sign,0,0). */
function sidePanel(pts, x, sign) {
  const pos = [], nor = [], uv = [];
  for (let i = 1; i < pts.length - 1; i++) {
    let A = pts[0], B = pts[i], C = pts[i + 1];
    const nx = (B[1] - A[1]) * (C[0] - A[0]) - (B[0] - A[0]) * (C[1] - A[1]);
    if (Math.sign(nx) !== sign) { const t = B; B = C; C = t; }
    for (const P of [A, B, C]) { pos.push(x, P[1], P[0]); nor.push(sign, 0, 0); uv.push(0, 0); }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  return g;
}

/** Наклонное стекло между точками профиля (z,y), приподнятое над фаской кабины. */
function slantGlass(p0, p1, width, outward, off = 0.042) {
  const dz = p1[0] - p0[0], dy = p1[1] - p0[1];
  const len = Math.hypot(dz, dy);
  let nz = dy / len, ny = -dz / len;
  if (nz * outward[0] + ny * outward[1] < 0) { nz = -nz; ny = -ny; }
  const g = new THREE.PlaneGeometry(width, len);
  const o = new THREE.Object3D();
  o.position.set(0, (p0[1] + p1[1]) / 2 + ny * off, (p0[0] + p1[0]) / 2 + nz * off);
  o.lookAt(o.position.x, o.position.y + ny, o.position.z + nz);
  o.updateMatrix();
  g.applyMatrix4(o.matrix);
  return g;
}

// ------------------------------------------------------------------ профиль
function lowerShape(def, curved) {
  const d = def.dims;
  const s = new THREE.Shape();
  s.moveTo(d.rear + 0.03, d.sill);
  const arch = (zc) => {
    s.lineTo(zc - d.archR, d.sill);
    if (curved) s.absarc(zc, d.sill, d.archR, Math.PI, 0, true);
    else {
      s.lineTo(zc - d.archR * 0.75, d.sill + d.archR * 0.75);
      s.lineTo(zc + d.archR * 0.75, d.sill + d.archR * 0.75);
      s.lineTo(zc + d.archR, d.sill);
    }
  };
  arch(d.axleR);
  arch(d.axleF);
  s.lineTo(d.front - 0.03, d.sill);
  for (const [z, y] of def.top) s.lineTo(z, y);
  s.closePath();
  return s;
}

function cabinPoly(def) {
  const c = def.cabin.map((p) => [p[0], p[1]]);
  return signedArea(c) < 0 ? c.reverse() : c;
}

function cabinShape(def) {
  const s = new THREE.Shape();
  def.cabin.forEach(([z, y], i) => (i === 0 ? s.moveTo(z, y) : s.lineTo(z, y)));
  s.closePath();
  return s;
}

/** Высота верхнего контура кузова в точке z (для капота/багажника). */
export function bodyTopAt(def, z) {
  const t = def.top;
  for (let i = 0; i < t.length - 1; i++) {
    const a = t[i], b = t[i + 1];
    if ((z <= a[0] && z >= b[0]) || (z >= a[0] && z <= b[0])) {
      const k = (z - a[0]) / ((b[0] - a[0]) || 1);
      return a[1] + (b[1] - a[1]) * k;
    }
  }
  return t[1][1];
}

export const roofY = (def) => Math.max(...def.cabin.map((p) => p[1]));

// ------------------------------------------------------------------ детали
/**
 * Список деталей [{geo, part}]. part — ключ материала:
 * paint, glass, chrome, black, rubber, grille, headLamp, tailLamp, reverseLamp,
 * indL, indR, plate, interior, rimDark, policeBlue, policeRed, taxiSign
 */
export function buildParts(def, detail) {
  const P = [];
  const add = (geo, part) => P.push({ geo, part });
  const d = def.dims;
  const hi = detail === 'high', low = detail === 'low';
  const F = d.front, R = d.rear, W = d.W, hw = W / 2;
  const cab = cabinPoly(def);
  const rY = roofY(def);

  // --- кузов и кабина
  add(extrudeSide(lowerShape(def, !low), W, low ? 0 : 0.035, low ? 2 : hi ? 8 : 5), 'paint');
  add(extrudeSide(cabinShape(def), d.cabinW, low ? 0 : 0.03, 1), 'paint');

  // --- стёкла: боковые по зонам между стойками
  const inset = insetPoly(cab, 0.05);
  const zs = [Infinity, ...[...def.pillars].sort((a, b) => b - a), -Infinity];
  const sx = d.cabinW / 2 + 0.004;
  for (let i = 0; i < zs.length - 1; i++) {
    let w = inset;
    if (zs[i] !== Infinity) w = clipPoly(w, -1, 0, zs[i] - 0.045);
    if (zs[i + 1] !== -Infinity) w = clipPoly(w, 1, 0, -(zs[i + 1] + 0.045));
    w = clipPoly(w, 0, 1, -def.glassBottom);
    if (w.length < 3 || Math.abs(signedArea(w)) < 0.01) continue;
    add(sidePanel(w, sx, 1), 'glass');
    add(sidePanel(w, -sx, -1), 'glass');
  }
  const n = cab.length;
  // в CCW-порядке: найдём лобовое (ребро с наибольшим z) и заднее (с наименьшим z)
  const edges = [];
  for (let i = 0; i < n; i++) {
    const p = cab[i], q = cab[(i + 1) % n];
    const midZ = (p[0] + q[0]) / 2, midY = (p[1] + q[1]) / 2;
    edges.push({ p, q, midZ, midY });
  }
  const upper = edges.filter((e) => Math.max(e.p[1], e.q[1]) > def.glassBottom + 0.1 && Math.abs(e.p[1] - e.q[1]) > 0.15);
  const ws = upper.reduce((a, b) => (b.midZ > a.midZ ? b : a));
  const rg = upper.reduce((a, b) => (b.midZ < a.midZ ? b : a));
  const gw = d.cabinW - 0.14;
  add(slantGlass(lerp2(ws.p, ws.q, 0.07), lerp2(ws.p, ws.q, 0.93), gw, [1, 1]), 'glass');
  add(slantGlass(lerp2(rg.p, rg.q, 0.08), lerp2(rg.p, rg.q, 0.92), gw, [-1, 1]), 'glass');

  // --- колёса (у игрока — отдельные вращающиеся группы)
  if (!hi) {
    const seg = low ? 6 : 10;
    for (const z of [d.axleF, d.axleR]) {
      for (const s of [1, -1]) {
        const x = s * (d.track / 2);
        add(cylX(d.wheelR, d.wheelW, seg, x, d.wheelR, z), 'rubber');
        if (!low) {
          const cap = def.wheelStyle === 'classic' ? 'chrome' : 'rimDark';
          add(cylX(d.wheelR * 0.62, 0.02, seg, x + s * (d.wheelW / 2), d.wheelR, z), cap);
        }
      }
    }
  }
  if (low) {
    // LOD1: только светлые пятна фар/фонарей
    add(box(W * 0.8, 0.12, 0.02, 0, def.head.y, F + 0.01), 'headLamp');
    add(box(W * 0.8, 0.1, 0.02, 0, def.tail.y + def.tail.h * 0.2, R - 0.01), 'tailLamp');
    return P;
  }

  // --- передняя оптика
  const h = def.head;
  if (h.style === 'round4' || h.style === 'round2') {
    for (const s of [1, -1]) {
      for (const x of h.xs) {
        add(cylZ(h.r, 0.04, hi ? 16 : 10, s * x, h.y, F + 0.012), 'headLamp');
        add(cylZ(h.r + 0.018, 0.028, hi ? 16 : 10, s * x, h.y, F + 0.004), 'chrome');
      }
    }
  } else {
    for (const s of [1, -1]) {
      add(box(h.w, h.h, 0.04, s * h.x, h.y, F + 0.008), 'headLamp');
      add(box(h.w + 0.03, h.h + 0.03, 0.02, s * h.x, h.y, F + 0.001), h.style === 'rect' ? 'chrome' : 'black');
      if (h.style === 'modern') add(box(h.w * 0.7, 0.018, 0.012, s * (h.x + 0.02), h.y - h.h * 0.36, F + 0.03), 'reverseLamp'); // ДХО
    }
  }
  // поворотники спереди
  const b = def.bumper;
  for (const s of [1, -1]) {
    const part = s > 0 ? 'indL' : 'indR';
    if (h.style === 'rect' || h.style === 'wedge' || h.style === 'modern') {
      add(box(0.08, h.h ? h.h * 0.8 : 0.1, 0.035, s * (h.x + (h.w || 0.2) / 2 + 0.05), h.y, F + 0.004), part);
    } else {
      add(box(0.14, 0.05, 0.04, s * (hw - 0.22), b.y + b.h / 2 + 0.05, F + 0.01), part);
    }
  }

  // --- решётка
  const g = def.grille;
  const gz = F + 0.004;
  switch (g.style) {
    case 'chrome-bars':
      add(box(g.w, g.h, 0.03, 0, g.y, gz), 'grille');
      break;
    case 'chrome-mesh':
      add(box(g.w, g.h, 0.025, 0, g.y, gz - 0.004), 'chrome');
      add(box(g.w - 0.04, g.h - 0.04, 0.02, 0, g.y, gz + 0.004), 'black');
      for (let k = -1; k <= 1; k++) add(box(g.w - 0.06, 0.012, 0.012, 0, g.y + k * 0.05, gz + 0.016), 'chrome');
      break;
    case 'black-chrome':
      add(box(g.w, g.h, 0.025, 0, g.y, gz - 0.004), 'black');
      add(box(g.w, 0.018, 0.03, 0, g.y + g.h / 2, gz), 'chrome');
      add(box(g.w, 0.018, 0.03, 0, g.y - g.h / 2, gz), 'chrome');
      add(box(0.5, g.h - 0.03, 0.02, 0, g.y, gz + 0.008), 'grille');
      break;
    case 'slot':
      add(box(g.w, g.h, 0.02, 0, g.y, gz), 'black');
      break;
    case 'black-bars':
      add(box(g.w, g.h, 0.025, 0, g.y, gz - 0.002), 'black');
      for (let k = -2; k <= 2; k++) add(box(g.w - 0.04, 0.014, 0.015, 0, g.y + k * 0.045, gz + 0.014), 'rimDark');
      break;
    case 'modern-chrome':
      add(box(g.w, g.h, 0.02, 0, g.y, gz), 'black');
      add(box(g.w + 0.04, 0.03, 0.025, 0, g.y + g.h * 0.2, gz + 0.008), 'chrome');
      add(box(W * 0.5, 0.08, 0.02, 0, b.y - b.h * 0.2, F + 0.02 + 0.055), 'black');
      break;
    case 'granta':
      add(box(g.w, g.h, 0.02, 0, g.y, gz), 'black');
      add(box(g.w + 0.06, 0.028, 0.025, 0, g.y + g.h * 0.25, gz + 0.008), 'chrome');
      add(box(0.13, 0.08, 0.02, 0, g.y, gz + 0.014), 'chrome');
      add(box(W * 0.55, 0.1, 0.02, 0, b.y - b.h * 0.15, F + 0.02 + 0.055), 'black');
      break;
    case 'xface': {
      // фирменный X-стиль Vesta: хромированные «скобки» от фар к центру бампера и к противотуманкам
      const zf = F + 0.075;
      const bar = (x0, y0, x1, y1) => {
        const len = Math.hypot(x1 - x0, y1 - y0);
        add(new THREE.BoxGeometry(len, 0.032, 0.025).rotateZ(Math.atan2(y1 - y0, x1 - x0)).translate((x0 + x1) / 2, (y0 + y1) / 2, zf), 'chrome');
      };
      add(box(0.46, g.h * 0.42, 0.02, 0, g.y - 0.02, F + 0.06), 'black');       // нижняя решётка
      add(box(0.34, 0.09, 0.02, 0, g.y + g.h * 0.42, gz + 0.004), 'black');     // верхняя решётка
      add(box(0.12, 0.07, 0.02, 0, g.y + g.h * 0.42, gz + 0.016), 'chrome');    // ладья
      for (const s of [1, -1]) {
        bar(s * 0.55, g.y + 0.12, s * 0.26, g.y - 0.02);
        bar(s * 0.26, g.y - 0.02, s * 0.55, g.y - 0.17);
        add(box(0.12, 0.06, 0.02, s * 0.62, g.y - 0.19, zf), 'headLamp');       // ПТФ
      }
      break;
    }
    default:
      add(box(g.w, g.h, 0.02, 0, g.y, gz), 'black');
  }

  // --- бамперы
  const bumper = (zc, sign) => {
    if (b.style === 'chrome') {
      add(box(W + 0.04, b.h, 0.14, 0, b.y, zc + sign * 0.05), 'chrome');
      if (b.strip) add(box(W + 0.05, 0.035, 0.15, 0, b.y, zc + sign * 0.05), 'black');
      if (b.fangs) for (const s of [1, -1]) add(box(0.05, 0.17, 0.06, s * 0.32, b.y + 0.02, zc + sign * 0.12), 'chrome');
      return zc + sign * 0.12;
    }
    if (b.style === 'black') {
      add(box(W + 0.02, b.h, 0.12, 0, b.y, zc + sign * 0.03), 'black');
      return zc + sign * 0.09;
    }
    add(box(W + 0.01, b.h, 0.1, 0, b.y, zc + sign * 0.02), 'paint');
    add(box(W * 0.9, 0.05, 0.1, 0, b.y - b.h / 2 + 0.02, zc + sign * 0.03), 'black');
    return zc + sign * 0.07;
  };
  const fFace = bumper(F, 1);
  const rFace = bumper(R, -1);

  // --- номера
  add(box(0.52, 0.115, 0.01, 0, g.style === 'xface' ? b.y - 0.07 : b.y, fFace + 0.006), 'plate');
  const plateR = new THREE.PlaneGeometry(0.52, 0.115).rotateY(Math.PI);
  if (b.style === 'chrome') add(plateR.translate(0, b.y + b.h / 2 + 0.11, R - 0.012), 'plate');
  else add(plateR.translate(0, b.y + 0.02, rFace - 0.006), 'plate');

  // --- задние фонари
  const t = def.tail, tz = R - 0.006;
  for (const s of [1, -1]) {
    const ind = s > 0 ? 'indL' : 'indR';
    const outer = s * (t.x + t.w / 2), dir = -s; // от внешнего края внутрь
    if (t.style === 'wide') {
      add(box(t.w * 0.55, t.h, 0.03, outer + dir * t.w * 0.275, t.y, tz), 'tailLamp');
      add(box(t.w * 0.22, t.h, 0.03, outer + dir * t.w * 0.66, t.y, tz), ind);
      add(box(t.w * 0.2, t.h, 0.03, outer + dir * t.w * 0.88, t.y, tz), 'reverseLamp');
    } else if (t.style === 'vertical') {
      add(box(t.w, t.h * 0.6, 0.03, s * t.x, t.y + t.h * 0.2, tz), 'tailLamp');
      add(box(t.w, t.h * 0.38, 0.03, s * t.x, t.y - t.h * 0.3, tz), ind);
      if (s < 0) add(box(t.w * 0.8, 0.06, 0.03, s * t.x, t.y - t.h * 0.62, tz), 'reverseLamp');
    } else if (t.style === 'hatch') {
      add(box(t.w * 0.62, t.h, 0.03, outer + dir * t.w * 0.31, t.y, tz), 'tailLamp');
      add(box(t.w * 0.2, t.h, 0.03, outer + dir * t.w * 0.72, t.y, tz), ind);
      add(box(t.w * 0.16, t.h, 0.03, outer + dir * t.w * 0.91, t.y, tz), 'reverseLamp');
    } else {
      add(box(t.w, t.h, 0.03, s * t.x, t.y, tz), 'tailLamp');
      add(box(t.w * 0.25, t.h * 0.45, 0.02, s * (t.x - t.w * 0.3), t.y, tz - 0.016), 'reverseLamp');
      add(box(t.w * 0.3, t.h * 0.3, 0.02, s * (t.x + t.w * 0.2), t.y - t.h * 0.1, tz - 0.016), ind);
    }
  }

  // --- боковины: швы дверей, ручки, молдинги, зеркала
  const beltY = def.glassBottom - 0.05;
  for (const s of [1, -1]) {
    const x = s * (hw + 0.002);
    for (const z of def.seams) {
      const top = Math.min(beltY, bodyTopAt(def, z) - 0.03);
      add(box(0.006, top - d.sill - 0.06, 0.012, x, (top + d.sill + 0.06) / 2, z), 'black');
    }
    for (const z of def.seams.slice(1)) add(box(0.02, 0.025, 0.12, s * (hw + 0.008), beltY - 0.1, z + 0.2), 'chrome');
    if (def.trim) add(box(0.02, 0.03, (F - R) - 0.5, s * (hw + 0.006), def.trim, (F + R) / 2), 'chrome');
    if (def.trim) add(box(0.015, 0.02, def.cabin[0][0] - def.cabin[3][0] - 0.1, s * (d.cabinW / 2 + 0.012), def.glassBottom - 0.015, (def.cabin[0][0] + def.cabin[3][0]) / 2), 'chrome');
    const mz = def.cabin[0][0] - 0.14;
    add(box(0.13, 0.085, 0.06, s * (d.cabinW / 2 + 0.1), def.glassBottom + 0.03, mz), def.bumper.style === 'body' ? 'paint' : 'black');
    add(box(0.07, 0.03, 0.03, s * (d.cabinW / 2 + 0.04), def.glassBottom + 0.02, mz), 'black');
    // брызговики у классики
    if (def.bumper.style === 'chrome') add(box(0.2, 0.2, 0.012, s * d.track / 2, d.sill + 0.02, d.axleR - d.archR - 0.02), 'black');
  }
  // дворники
  const c0 = def.cabin[0];
  for (const s of [1, -1]) add(box(0.46, 0.014, 0.02, s * 0.22, c0[1] + 0.035, c0[0] + 0.04), 'black');
  // антенна
  if (def.antenna) add(new THREE.CylinderGeometry(0.004, 0.006, 0.8, 4).translate(-hw + 0.12, bodyTopAt(def, 1.2) + 0.4, 1.2), 'black');
  // рейлинги
  if (def.rails) for (const s of [1, -1]) add(box(0.04, 0.05, def.cabin[1][0] - def.cabin[2][0] - 0.1, s * (d.cabinW / 2 - 0.08), rY + 0.04, (def.cabin[1][0] + def.cabin[2][0]) / 2), 'black');
  // глушитель
  add(cylZ(0.03, 0.2, 6, -0.45, 0.24, R - 0.02), 'black');

  // --- спецверсии
  if (def.police) {
    add(box(0.55, 0.11, 0.24, 0.28, rY + 0.07, -0.45), 'policeBlue');
    add(box(0.55, 0.11, 0.24, -0.28, rY + 0.07, -0.45), 'policeRed');
    add(box(1.16, 0.03, 0.28, 0, rY + 0.015, -0.45), 'black');
    for (const s of [1, -1]) add(box(0.01, 0.12, (F - R) - 0.9, s * (hw + 0.008), 0.62, (F + R) / 2 - 0.1), 'policeStripe');
  }
  if (def.taxi) {
    add(box(0.5, 0.15, 0.2, 0, rY + 0.08, -0.5), 'taxiSign');
    for (const s of [1, -1]) {
      for (let k = 0; k < 4; k++) add(box(0.004, 0.06, 0.1, s * 0.253, rY + 0.05 + (k % 2) * 0.06, -0.65 + k * 0.1), 'black');
      for (let k = 0; k < 10; k++) add(box(0.006, 0.05, 0.12, s * (hw + 0.005), 0.64 + (k % 2) * 0.05, 1.0 - k * 0.24), 'black');
    }
  }

  // --- салон (только игрок)
  if (hi) {
    const dashZ = c0[0] - 0.3;
    add(box(d.cabinW - 0.12, 0.2, 0.36, 0, def.glassBottom - 0.06, dashZ), 'interior');
    const seatZ = dashZ - 0.95;
    for (const s of [1, -1]) {
      add(box(0.5, 0.12, 0.5, s * 0.36, def.glassBottom - 0.36, seatZ), 'interior');
      add(box(0.5, 0.62, 0.12, s * 0.36, def.glassBottom - 0.05, seatZ - 0.26), 'interior');
      add(box(0.3, 0.16, 0.1, s * 0.36, def.glassBottom + 0.34, seatZ - 0.28), 'interior');
    }
    if (def.pillars.length || rY > 1.45) add(box(d.cabinW - 0.2, 0.12, 0.5, 0, def.glassBottom - 0.36, seatZ - 0.85), 'interior');
    const wheel = new THREE.TorusGeometry(0.19, 0.022, 5, 18).rotateX(-0.35).translate(0.36, def.glassBottom + 0.05, dashZ - 0.25);
    add(wheel, 'black');
    add(new THREE.CylinderGeometry(0.025, 0.025, 0.3, 6).rotateX(Math.PI / 2 - 0.35).translate(0.36, def.glassBottom, dashZ - 0.12), 'black');
    add(new THREE.CylinderGeometry(0.012, 0.012, 0.25, 5).rotateX(-0.3).translate(0, def.glassBottom - 0.3, dashZ - 0.45), 'chrome'); // рычаг КПП
  }
  return P;
}

/** Точки ламп в локальных координатах (для glow-спрайтов трафика). */
export function lampPoints(def) {
  const d = def.dims, h = def.head, t = def.tail;
  const head = [];
  if (h.xs) for (const s of [1, -1]) head.push([s * h.xs[h.xs.length - 1], h.y, d.front + 0.05]);
  else for (const s of [1, -1]) head.push([s * h.x, h.y, d.front + 0.05]);
  const tail = [1, -1].map((s) => [s * t.x, t.y + (t.style === 'vertical' ? t.h * 0.2 : 0), d.rear - 0.05]);
  const hw = d.W / 2;
  const ind = {
    L: [[hw - 0.15, h.y, d.front + 0.04], [t.x + t.w * 0.2, t.y, d.rear - 0.04]],
    R: [[-(hw - 0.15), h.y, d.front + 0.04], [-(t.x + t.w * 0.2), t.y, d.rear - 0.04]],
  };
  const extra = [];
  if (def.police) extra.push({ p: [0.28, roofY(def) + 0.08, -0.45], c: 'blue' }, { p: [-0.28, roofY(def) + 0.08, -0.45], c: 'red' });
  return { head, tail, ind, extra };
}

// ------------------------------------------------------------------ колёса игрока
function tireGeometry(R, w, rimR, seg) {
  const pts = [
    [rimR, -w / 2], [R * 0.9, -w / 2], [R * 0.98, -w * 0.38], [R, -w * 0.15],
    [R, w * 0.15], [R * 0.98, w * 0.38], [R * 0.9, w / 2], [rimR, w / 2],
  ].map(([r, y]) => new THREE.Vector2(r, y));
  return new THREE.LatheGeometry(pts, seg).rotateZ(Math.PI / 2);
}

/** Колесо с выбранным дизайном дисков. Возвращает {pivot, spin}. */
export function buildWheel(def, style, mats, side) {
  const d = def.dims;
  const R = d.wheelR, w = d.wheelW;
  const rimR = R * 0.64;
  const out = side * (w / 2 - 0.01);
  const spin = new THREE.Group();
  const byMat = {};
  const add = (geo, m) => (byMat[m] ||= []).push(prep(geo));

  add(tireGeometry(R, w, rimR, 22), 'rubber');
  add(cylX(rimR * 0.98, w * 0.9, 20, 0, 0, 0), 'rimDark'); // обод внутри
  add(cylX(rimR * 0.7, 0.02, 14, -side * 0.02, 0, 0), 'brake'); // тормозной диск
  const st = style === 'default' ? def.wheelStyle : style;
  const spokes = (count, width, m, depth = 0.025) => {
    for (let k = 0; k < count; k++) {
      const a = (k / count) * Math.PI * 2;
      add(new THREE.BoxGeometry(depth, rimR * 0.82, width).translate(out, rimR * 0.45, 0).rotateX(a), m);
    }
  };
  const x0 = new THREE.Vector3();
  if (st === 'steel') {
    add(cylX(rimR, 0.02, 20, out - side * 0.005, 0, 0), 'rimDark');
    for (let k = 0; k < 6; k++) {
      const a = (k / 6) * Math.PI * 2;
      add(cylX(0.022, 0.024, 8, out, Math.cos(a) * rimR * 0.62, Math.sin(a) * rimR * 0.62), 'black');
    }
    add(cylX(0.06, 0.03, 12, out + side * 0.01, 0, 0), 'chrome');
  } else if (st === 'classic') {
    add(cylX(rimR, 0.02, 20, out - side * 0.005, 0, 0), 'rimDark');
    const cap = new THREE.LatheGeometry([[0, 0.035], [0.08, 0.03], [0.15, 0.015], [0.17, 0]].map(([r, y]) => new THREE.Vector2(r, y)), 20);
    cap.rotateZ(-side * Math.PI / 2).translate(out, 0, 0);
    add(cap, 'chrome');
  } else if (st === 'star') {
    add(cylX(rimR, 0.012, 24, out - side * 0.012, 0, 0), 'alloy');
    spokes(5, 0.06, 'alloy');
    add(cylX(0.05, 0.035, 12, out + side * 0.005, 0, 0), 'alloy');
  } else if (st === 'sport') {
    add(cylX(rimR, 0.012, 24, out - side * 0.012, 0, 0), 'graphite');
    spokes(10, 0.025, 'graphite');
    add(cylX(0.045, 0.035, 12, out + side * 0.005, 0, 0), 'chrome');
  } else if (st === 'mesh') {
    add(new THREE.TorusGeometry(rimR * 0.97, 0.014, 5, 28).rotateY(Math.PI / 2).translate(out + side * 0.008, 0, 0), 'chrome');
    add(cylX(rimR, 0.01, 24, out - side * 0.014, 0, 0), 'gold');
    for (let k = 0; k < 16; k++) {
      const a = (k / 16) * Math.PI * 2;
      add(new THREE.BoxGeometry(0.015, rimR * 0.95, 0.014).translate(out, rimR * 0.45, 0).rotateX(0.35).rotateX(a), 'gold');
      add(new THREE.BoxGeometry(0.015, rimR * 0.95, 0.014).translate(out, rimR * 0.45, 0).rotateX(-0.35).rotateX(a), 'gold');
    }
    add(cylX(0.05, 0.035, 12, out + side * 0.006, 0, 0), 'chrome');
  }
  void x0;
  for (const [m, list] of Object.entries(byMat)) {
    const mesh = new THREE.Mesh(merge(list), mats[m] || mats.black);
    mesh.castShadow = m === 'rubber';
    spin.add(mesh);
  }
  const pivot = new THREE.Group();
  pivot.add(spin);
  return { pivot, spin };
}

// ------------------------------------------------------------------ материалы игрока
export function createCarMaterials({ color, envMap, T, quality, tint = 0.6 }) {
  const physical = !!quality.physicalPaint;
  const Std = (o) => new THREE.MeshStandardMaterial({ envMap, ...o });
  const paint = physical
    ? new THREE.MeshPhysicalMaterial({ color, metalness: 0.35, roughness: 0.38, clearcoat: 1, clearcoatRoughness: 0.08, envMap, envMapIntensity: 1 })
    : Std({ color, metalness: 0.4, roughness: 0.3, envMapIntensity: 1 });
  const tg = quality.transparentGlass;
  const mk = (hex, e = 0x000000) => Std({ color: hex, emissive: e, emissiveIntensity: 0, roughness: 0.25, metalness: 0.1 });
  const mats = {
    paint,
    chrome: Std({ color: 0xe6e6e6, metalness: 1, roughness: 0.14, envMapIntensity: 1.3 }),
    glass: Std({
      color: 0x0c1318, metalness: 0.3, roughness: 0.04, envMapIntensity: 1.8,
      transparent: tg, opacity: tg ? tint : 1, depthWrite: !tg,
    }),
    rubber: new THREE.MeshStandardMaterial({ color: 0x151515, roughness: 0.92 }),
    under: new THREE.MeshLambertMaterial({ color: 0x0b0b0b }),
    grilleDark: new THREE.MeshStandardMaterial({ color: 0x141414, roughness: 0.5, metalness: 0.3 }),
    black: new THREE.MeshStandardMaterial({ color: 0x1b1b1b, roughness: 0.7 }),
    interior: new THREE.MeshLambertMaterial({ color: 0x3a2e28 }),
    grille: Std({ map: T.grille, metalness: 0.6, roughness: 0.35 }),
    headLamp: Std({ color: 0xdddddd, emissive: 0xfff1cf, emissiveIntensity: 0, metalness: 0.4, roughness: 0.08 }),
    tailLamp: mk(0x5a0808, 0xff1608),
    reverseLamp: mk(0xbbbbbb, 0xffffff),
    indL: mk(0xb86a00, 0xff9a10),
    indR: mk(0xb86a00, 0xff9a10),
    plate: new THREE.MeshLambertMaterial({ map: T.plate }),
    rimDark: Std({ color: 0x3c3f42, metalness: 0.5, roughness: 0.5 }),
    alloy: Std({ color: 0xc9ccd0, metalness: 0.9, roughness: 0.25 }),
    graphite: Std({ color: 0x3a3c40, metalness: 0.8, roughness: 0.3 }),
    gold: Std({ color: 0xc8a24a, metalness: 1, roughness: 0.25 }),
    brake: Std({ color: 0x777777, metalness: 0.8, roughness: 0.4 }),
    policeBlue: mk(0x1030ff, 0x2050ff),
    policeRed: mk(0xff1010, 0xff2020),
    policeStripe: new THREE.MeshLambertMaterial({ color: 0x1b3c9e }),
    taxiSign: mk(0xffd000, 0xffc000),
  };
  // кузов виден и изнутри (вид из салона): крыша, стойки, двери не «просвечивают»
  for (const k of ['paint', 'black', 'chrome', 'trim', 'under', 'grille', 'grilleDark', 'rubber', 'plate']) if (mats[k]) mats[k].side = THREE.DoubleSide;
  return mats;

}

/** Модель игрока: кузов слит по материалам, колёса — отдельные группы. */
export function buildPlayerModel(def, mats, wheelStyle = 'default', { driver = false } = {}) {
  const root = new THREE.Group();
  root.name = `Player_${def.id}`;
  const body = new THREE.Group();
  body.name = 'Body';
  root.add(body);
  const lib = ModelLibrary.get(def.key || def.id);
  if (lib) {
    // модель из Blender: подмеши по материалам → игровые материалы
    const map = new Proxy(mats, { get: (m, k) => (k === 'grille' ? m.grilleDark : m[k]) });
    body.add(meshesFromLod(lib.hi, map, mats.black));
  } else {
    const byPart = {};
    for (const { geo, part } of buildParts(def, 'high')) (byPart[part] ||= []).push(prep(geo));
    for (const [part, list] of Object.entries(byPart)) {
      const mesh = new THREE.Mesh(merge(list), mats[part] || mats.black);
      mesh.name = part;
      mesh.castShadow = part !== 'glass';
      mesh.receiveShadow = part === 'paint';
      body.add(mesh);
    }
  }
  const wheels = buildWheels(def, mats, wheelStyle);
  for (const w of wheels) root.add(w.pivot);
  // салон своей модели (торпедо, приборы, руль, кресла) + водитель для машины игрока
  const interior = buildInterior(def, { driver });
  body.add(interior.group);
  return { root, body, wheels, interior };
}

export function buildWheels(def, mats, style) {
  const d = def.dims;
  const wheels = [];
  for (const [z, front] of [[d.axleF, true], [d.axleR, false]]) {
    for (const side of [1, -1]) {
      const w = buildWheel(def, style, mats, side);
      w.pivot.position.set(side * (d.track / 2), d.wheelR, z);
      w.front = front;
      w.side = side;
      wheels.push(w);
    }
  }
  return wheels;
}

// ------------------------------------------------------------------ инстансинг
const DETAIL_COLORS = {
  glass: 0x161d23, chrome: 0xc8c8c8, rubber: 0x121212, black: 0x1c1c1c, grille: 0x2c2c2c,
  headLamp: 0xe0e0d8, tailLamp: 0x7a1010, reverseLamp: 0xb8b8b8, indL: 0xd07000, indR: 0xd07000,
  plate: 0xe6e6e6, rimDark: 0x3a3a3a, policeBlue: 0x1a3cff, policeRed: 0xff1a1a,
  policeStripe: 0x1b3c9e, taxiSign: 0xffd000, interior: 0x3a2e28, under: 0x0b0b0b,
};

function withMask(geo, mask) {
  const n = geo.attributes.position.count;
  geo.setAttribute('paintMask', new THREE.BufferAttribute(new Float32Array(n).fill(mask), 1));
  return geo;
}

/**
 * Геометрии для InstancedMesh: всё в одной геометрии (кузов + детали) с вертексными
 * цветами и атрибутом paintMask (1 — окрашивается instanceColor, 0 — свой цвет).
 * Итого 1 draw call на модель и уровень LOD.
 */
export function buildInstanceGeometries(def) {
  const lib = ModelLibrary.get(def.key || def.id);
  const fixed = def.fixedColor !== undefined;
  if (lib) {
    const fromLod = (parts) => merge(parts.map((p) => {
      const paint = p.material === 'paint';
      const g = prep(p.geometry.clone(), paint ? (fixed ? def.fixedColor : 0xffffff) : DETAIL_COLORS[p.material] ?? 0x222222);
      return withMask(g, paint && !fixed ? 1 : 0);
    }));
    return { lod0: fromLod(lib.lod0), lod1: fromLod(lib.lod1) };
  }
  const build = (detail) => {
    const list = buildParts(def, detail).map(({ geo, part }) => {
      const paint = part === 'paint';
      const fixed = def.fixedColor !== undefined;
      const g = prep(geo, paint ? (fixed ? def.fixedColor : 0xffffff) : DETAIL_COLORS[part] ?? 0x222222);
      return withMask(g, paint && !fixed ? 1 : 0);
    });
    return merge(list);
  };
  return { lod0: build('mid'), lod1: build('low') };
}

/** Материал с paintMask: instanceColor красит только кузов. */
export function createInstanceMaterial() {
  const m = new THREE.MeshLambertMaterial({ vertexColors: true });
  m.onBeforeCompile = (shader) => {
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nattribute float paintMask;')
      .replace('#include <color_vertex>', `
        vColor = vec3(1.0);
        #ifdef USE_COLOR
          vColor *= color;
        #endif
        #ifdef USE_INSTANCING_COLOR
          vColor *= mix(vec3(1.0), instanceColor.xyz, paintMask);
        #endif
      `);
  };
  m.customProgramCacheKey = () => 'paintMask';
  return m;
}
