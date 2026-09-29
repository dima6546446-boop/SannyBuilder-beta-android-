import * as THREE from 'three';
import { prep, merge } from '../utils/geometry.js';

/**
 * Процедурная low-poly модель ВАЗ-2107 «Семёрка».
 * Габариты реальные: 4145 × 1620 × 1435 мм, база 2424 мм, колёса 175/70 R13.
 * Узнаваемые черты: трёхобъёмный кузов, прямоугольные фары, хромированная
 * решётка с вертикальными прутьями, хромированные бамперы и молдинги.
 *
 * Начало координат — центр масс на уровне земли; вперёд +Z, влево +X.
 * Бюджет: LOD0 ≈ 1.2k треугольников, LOD1 (трафик вдали) ≈ 150.
 */
export const LADA_2107_DIMS = {
  frontAxle: 1.14, rearAxle: -1.284, // от центра масс
  front: 1.98, rear: -2.18,
  track: 1.36, wheelR: 0.29, wheelW: 0.175,
  bodyW: 1.62, cabinW: 1.40,
};
const D = LADA_2107_DIMS;

export const LADA_COLORS = [
  { name: 'Белая ночь', hex: 0xecebe4 },
  { name: 'Вишня', hex: 0x7a1020 },
  { name: 'Мурена', hex: 0x1f4f6a },
  { name: 'Балтика', hex: 0x3d86b8 },
  { name: 'Snow White', hex: 0xf4f4f4 },
  { name: 'Коррида', hex: 0xb31d12 },
  { name: 'Сафари', hex: 0xc8b98a },
  { name: 'Изумруд', hex: 0x155e3e },
  { name: 'Мокрый асфальт', hex: 0x4b5157 },
  { name: 'Золотистая', hex: 0xb89a4a },
];

// ------------------------------------------------------------- профили кузова
function lowerBodyShape(curveSeg = true) {
  const s = new THREE.Shape();
  const arch = (zc) => {
    s.lineTo(zc - 0.37, 0.30);
    if (curveSeg) s.absarc(zc, 0.30, 0.37, Math.PI, 0, true);
    else { s.lineTo(zc - 0.3, 0.6); s.lineTo(zc + 0.3, 0.6); s.lineTo(zc + 0.37, 0.30); }
  };
  s.moveTo(D.rear + 0.03, 0.30);
  arch(D.rearAxle);
  arch(D.frontAxle);
  s.lineTo(D.front - 0.03, 0.30);
  s.lineTo(D.front, 0.52);
  s.lineTo(D.front, 0.80);
  s.lineTo(D.front - 0.10, 0.86);
  s.lineTo(0.86, 0.95);
  s.lineTo(-1.42, 0.97);
  s.lineTo(D.rear + 0.08, 0.93);
  s.lineTo(D.rear, 0.84);
  s.lineTo(D.rear, 0.50);
  s.closePath();
  return s;
}

function cabinShape() {
  const s = new THREE.Shape();
  s.moveTo(0.92, 0.90);
  s.lineTo(0.0, 1.42);
  s.lineTo(-0.95, 1.43);
  s.lineTo(-1.40, 0.92);
  s.closePath();
  return s;
}

/** Выдавливание бокового профиля по ширине: X формы → Z мира, глубина → X мира. */
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

/** Плоский многоугольник на боковине (стекло) с правильной ориентацией граней. */
function sidePanel(pts, x, sign) {
  const v2 = pts.map(([z, y]) => new THREE.Vector2(z, y));
  const tris = THREE.ShapeUtils.triangulateShape(v2, []);
  const pos = [], nor = [], uv = [];
  for (const t of tris) {
    let [a, b, c] = t;
    const A = pts[a], B = pts[b], C = pts[c];
    // нормаль треугольника в мире: (B-A)×(C-A), x-компонента = dy1*dz2 - dz1*dy2
    const nx = (B[1] - A[1]) * (C[0] - A[0]) - (B[0] - A[0]) * (C[1] - A[1]);
    if (Math.sign(nx) !== sign) { const tmp = b; b = c; c = tmp; }
    for (const k of [a, b, c]) {
      pos.push(x, pts[k][1], pts[k][0]);
      nor.push(sign, 0, 0);
      uv.push(0, 0);
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  return g;
}

/** Наклонное стекло (лобовое/заднее) между точками профиля p0→p1 (z,y). */
function slantGlass(p0, p1, width, outward) {
  const dz = p1[0] - p0[0], dy = p1[1] - p0[1];
  const len = Math.hypot(dz, dy);
  let nz = dy / len, ny = -dz / len;
  if (nz * outward[0] + ny * outward[1] < 0) { nz = -nz; ny = -ny; }
  const g = new THREE.PlaneGeometry(width, len);
  const o = new THREE.Object3D();
  const off = 0.042; // фаска кабины (0.03) + зазор
  o.position.set(0, (p0[1] + p1[1]) / 2 + ny * off, (p0[0] + p1[0]) / 2 + nz * off);
  o.lookAt(o.position.x, o.position.y + ny, o.position.z + nz);
  o.updateMatrix();
  g.applyMatrix4(o.matrix);
  return g;
}

const box = (w, h, d, x, y, z) => new THREE.BoxGeometry(w, h, d).translate(x, y, z);

/** Плоский «стикер» лицом вперёд (+Z) или назад. */
const facePlane = (w, h, x, y, z, back = false) => {
  const g = new THREE.PlaneGeometry(w, h);
  if (back) g.rotateY(Math.PI);
  return g.translate(x, y, z);
};

/**
 * Общий список деталей: [{ geo, part }]. part — ключ материала.
 * detail: 'high' (игрок), 'mid' (трафик LOD0), 'low' (трафик LOD1)
 */
function buildParts(detail) {
  const P = [];
  const add = (geo, part) => P.push({ geo, part });
  const hi = detail === 'high';
  const low = detail === 'low';

  // кузов
  add(extrudeSide(lowerBodyShape(!low), D.bodyW, low ? 0 : 0.035, low ? 2 : hi ? 8 : 5), 'paint');
  add(extrudeSide(cabinShape(), D.cabinW, low ? 0 : 0.03, 1), 'paint');

  // стёкла
  const sx = D.cabinW / 2 + 0.004;
  const front = [[0.70, 1.0], [0.07, 1.37], [-0.24, 1.37], [-0.24, 1.0]];
  const rear = [[-0.32, 1.0], [-0.32, 1.37], [-0.97, 1.37], [-1.28, 1.0]];
  for (const s of [1, -1]) {
    add(sidePanel(front, s * sx, s), 'glass');
    add(sidePanel(rear, s * sx, s), 'glass');
  }
  // точки лежат на линиях профиля кабины (0.92,0.90)→(0,1.42) и (−1.40,0.92)→(−0.95,1.43)
  add(slantGlass([0.846, 0.942], [0.064, 1.384], 1.28, [1, 1]), 'glass');
  add(slantGlass([-1.355, 0.971], [-0.995, 1.379], 1.28, [-1, 1]), 'glass');

  // колёса (для игрока — отдельно, вращаются)
  if (!hi) {
    const seg = low ? 6 : 10;
    for (const z of [D.frontAxle, D.rearAxle]) {
      for (const s of [1, -1]) {
        const x = s * (D.track / 2);
        add(new THREE.CylinderGeometry(D.wheelR, D.wheelR, D.wheelW, seg).rotateZ(Math.PI / 2).translate(x, D.wheelR, z), 'rubber');
        if (!low) add(new THREE.CylinderGeometry(0.16, 0.16, 0.02, seg).rotateZ(Math.PI / 2).translate(x + s * 0.09, D.wheelR, z), 'chrome');
      }
    }
  }
  if (low) return P;

  // хром: бамперы, молдинги, ручки
  add(box(1.66, 0.12, 0.16, 0, 0.44, D.front + 0.05), 'chrome');
  add(box(1.66, 0.12, 0.16, 0, 0.44, D.rear - 0.05), 'chrome');
  add(box(1.67, 0.035, 0.17, 0, 0.44, D.front + 0.05), 'black');
  add(box(1.67, 0.035, 0.17, 0, 0.44, D.rear - 0.05), 'black');
  for (const s of [1, -1]) {
    add(box(0.02, 0.03, 3.5, s * 0.822, 0.72, -0.12), 'chrome');
    add(box(0.02, 0.025, 0.13, s * 0.822, 0.85, 0.05), 'chrome');
    add(box(0.02, 0.025, 0.13, s * 0.822, 0.85, -0.85), 'chrome');
    // зеркала
    add(box(0.12, 0.08, 0.05, s * 0.8, 1.0, 0.72), 'black');
  }
  // решётка и оптика
  add(box(0.80, 0.25, 0.03, 0, 0.68, D.front + 0.006), 'grille');
  for (const s of [1, -1]) {
    add(box(0.34, 0.18, 0.03, s * 0.61, 0.68, D.front + 0.006), 'headLamp');
    add(box(0.16, 0.06, 0.03, s * 0.58, 0.33, D.front + 0.02), 'amber');
    add(box(0.30, 0.20, 0.03, s * 0.62, 0.72, D.rear - 0.006), 'tailLamp');
    add(box(0.10, 0.20, 0.03, s * 0.42, 0.72, D.rear - 0.006), 'reverseLamp');
  }
  add(box(0.52, 0.115, 0.01, 0, 0.44, D.front + 0.135), 'plate');
  add(facePlane(0.52, 0.115, 0, 0.62, D.rear - 0.012, true), 'plate');
  add(box(0.06, 0.06, 0.2, -0.45, 0.25, D.rear - 0.02), 'black'); // глушитель

  if (hi) {
    // салон: торпедо, сиденья, руль (водитель слева → +X)
    add(box(1.34, 0.2, 0.34, 0, 0.92, 0.62), 'interior');
    for (const s of [1, -1]) {
      add(box(0.5, 0.12, 0.5, s * 0.36, 0.62, -0.2), 'interior');
      add(box(0.5, 0.6, 0.12, s * 0.36, 0.95, -0.45), 'interior');
    }
    add(box(1.25, 0.12, 0.5, 0, 0.62, -1.0), 'interior');
    add(box(1.25, 0.5, 0.12, 0, 0.9, -1.26), 'interior');
    const wheel = new THREE.TorusGeometry(0.19, 0.022, 4, 14).rotateX(-0.35).translate(0.36, 1.05, 0.38);
    add(wheel, 'black');
  }
  return P;
}

/** Колесо игрока: шина + колпак, ось вдоль X. */
function buildWheel(side, mats) {
  const spin = new THREE.Group();
  const tire = new THREE.Mesh(new THREE.CylinderGeometry(D.wheelR, D.wheelR, D.wheelW, 16).rotateZ(Math.PI / 2), mats.rubber);
  const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.165, 0.165, 0.02, 16).rotateZ(Math.PI / 2).translate(side * 0.09, 0, 0), mats.chrome);
  // метка на колпаке, чтобы было видно вращение
  const mark = new THREE.Mesh(new THREE.BoxGeometry(0.01, 0.2, 0.04).translate(side * 0.1, 0, 0), mats.black);
  tire.castShadow = true;
  spin.add(tire, hub, mark);
  const pivot = new THREE.Group();
  pivot.add(spin);
  return { pivot, spin };
}

export function createPlayerMaterials(color, { envMap, transparentGlass, T }) {
  const std = (o) => new THREE.MeshStandardMaterial({ envMap, ...o });
  return {
    paint: std({ color, metalness: 0.45, roughness: 0.32, envMapIntensity: 1.0 }),
    chrome: std({ color: 0xe0e0e0, metalness: 1.0, roughness: 0.18, envMapIntensity: 1.2 }),
    glass: std({
      color: 0x16222c, metalness: 0.2, roughness: 0.06, envMapIntensity: 1.4,
      transparent: transparentGlass, opacity: transparentGlass ? 0.55 : 1, depthWrite: !transparentGlass,
    }),
    rubber: new THREE.MeshLambertMaterial({ color: 0x151515 }),
    black: new THREE.MeshLambertMaterial({ color: 0x1d1d1d }),
    interior: new THREE.MeshLambertMaterial({ color: 0x3a2e28 }),
    grille: std({ map: T.grille, metalness: 0.6, roughness: 0.35 }),
    headLamp: std({ color: 0xdddddd, emissive: 0xfff1cf, emissiveIntensity: 0, metalness: 0.3, roughness: 0.1 }),
    tailLamp: std({ color: 0x5a0808, emissive: 0xff1608, emissiveIntensity: 0.05, roughness: 0.2 }),
    reverseLamp: std({ color: 0xbbbbbb, emissive: 0xffffff, emissiveIntensity: 0, roughness: 0.2 }),
    amber: new THREE.MeshLambertMaterial({ color: 0xff8a00, emissive: 0x552200 }),
    plate: new THREE.MeshLambertMaterial({ map: T.plate }),
  };
}

/**
 * Модель игрока: детали слиты по материалам (≈10 draw calls на всю машину),
 * колёса — отдельные группы (руление + вращение).
 */
export function buildPlayerCar(mats) {
  const root = new THREE.Group();
  root.name = 'PlayerCar';
  const body = new THREE.Group(); // крен/тангаж визуальной «подвески»
  body.name = 'Body';
  root.add(body);

  const byPart = {};
  for (const { geo, part } of buildParts('high')) (byPart[part] ||= []).push(prep(geo));
  for (const [part, list] of Object.entries(byPart)) {
    const mesh = new THREE.Mesh(merge(list), mats[part]);
    mesh.name = part;
    mesh.castShadow = part !== 'glass';
    body.add(mesh);
  }

  const wheels = [];
  for (const [z, front] of [[D.frontAxle, true], [D.rearAxle, false]]) {
    for (const side of [1, -1]) {
      const w = buildWheel(side, mats);
      w.pivot.position.set(side * (D.track / 2), D.wheelR, z);
      w.front = front;
      w.side = side;
      root.add(w.pivot);
      wheels.push(w);
    }
  }
  return { root, body, wheels };
}

// Цвета деталей трафика (вертексные) — линейное пространство задаёт THREE.Color
const DETAIL_COLORS = {
  glass: 0x1a232b, chrome: 0xb8b8b8, rubber: 0x121212, black: 0x1c1c1c, grille: 0x303030,
  headLamp: 0xd8d8d0, tailLamp: 0x5a0e0e, reverseLamp: 0xa0a0a0, amber: 0xd07000, plate: 0xe6e6e6,
};

/**
 * Геометрии для InstancedMesh трафика:
 *  body  — окрашиваемые детали (цвет через instanceColor)
 *  detail — всё остальное с вертексными цветами (не окрашивается)
 *  low   — LOD1 одной геометрией (кузов белый → окрашивается, стёкла/колёса тёмные)
 *  heads/tails — излучающие квадраты фар/фонарей (цвет по состоянию: день/ночь/торможение)
 */
export function buildTrafficGeometries() {
  const body = [], detail = [];
  for (const { geo, part } of buildParts('mid')) {
    if (part === 'paint') body.push(prep(geo));
    else detail.push(prep(geo, DETAIL_COLORS[part]));
  }
  const low = buildParts('low').map(({ geo, part }) => prep(geo, part === 'paint' ? 0xffffff : DETAIL_COLORS[part]));

  const heads = [], tails = [];
  for (const s of [1, -1]) {
    heads.push(prep(facePlane(0.32, 0.16, s * 0.61, 0.68, D.front + 0.03)));
    tails.push(prep(facePlane(0.36, 0.18, s * 0.58, 0.72, D.rear - 0.03, true)));
  }
  return {
    body: merge(body), detail: merge(detail), low: merge(low),
    heads: merge(heads), tails: merge(tails),
  };
}
