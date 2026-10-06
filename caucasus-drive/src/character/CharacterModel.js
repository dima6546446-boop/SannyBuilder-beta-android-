import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

/**
 * Процедурный персонаж со скелетом: «пацан с района» — спортивный костюм с лампасами,
 * кепка, кроссовки. Все детали сливаются в ОДИН SkinnedMesh (жёсткий скиннинг: каждая
 * вершина привязана к одной кости) — 1 draw call + тень, вместо ~40 отдельных мешей.
 *
 * Оси: персонаж смотрит в +Z, вверх +Y, правая рука — на −X.
 * Повороты костей: thigh/upperArm.rotation.x < 0 — вперёд; knee.rotation.x > 0 — сгиб назад;
 * elbow.rotation.x < 0 — предплечье вперёд/вверх; spine.rotation.x > 0 — наклон вперёд.
 */

export const OUTFITS = {
  player: { suit: 0x17181c, stripe: 0xf2f2f2, pants: 0x17181c, cap: 0x2b2d33, shoes: 0xf4f4f4, sole: 0xdadada, skin: 0xe2b48f, hair: 0x2a1d14, capBrim: 0x1c1d22 },
  blue: { suit: 0x1f3c78, stripe: 0xffffff, pants: 0x23252b, cap: null, shoes: 0x2a2a2a, sole: 0xeeeeee, skin: 0xd9a57f, hair: 0x5a3a22 },
  red: { suit: 0x8a1f24, stripe: 0x1a1a1a, pants: 0x3a3f4a, cap: 0x8a1f24, shoes: 0xf0f0f0, sole: 0xdddddd, skin: 0xe8c1a0, hair: 0x1c1410, capBrim: 0x111111 },
  green: { suit: 0x2f5a3a, stripe: 0xe8e8e8, pants: 0x2b2b2b, cap: null, shoes: 0x3a2a1c, sole: 0x222222, skin: 0xc99872, hair: 0x101010 },
  coat: { suit: 0x5a4632, stripe: 0x5a4632, pants: 0x2a2c33, cap: 0x3a3a3a, shoes: 0x1a1a1a, sole: 0x111111, skin: 0xe6bb98, hair: 0x8a8a8a, capBrim: 0x2a2a2a },
};

// Кости: [имя, родитель, позиция относительно родителя в покое]
const BONES = [
  ['hips', null, [0, 0.98, 0]],
  ['spine', 'hips', [0, 0.1, 0]],
  ['chest', 'spine', [0, 0.22, 0]],
  ['neck', 'chest', [0, 0.2, 0]],
  ['head', 'neck', [0, 0.08, 0]],
  ['upperArmL', 'chest', [0.2, 0.15, 0]],
  ['forearmL', 'upperArmL', [0, -0.29, 0]],
  ['handL', 'forearmL', [0, -0.25, 0]],
  ['upperArmR', 'chest', [-0.2, 0.15, 0]],
  ['forearmR', 'upperArmR', [0, -0.29, 0]],
  ['handR', 'forearmR', [0, -0.25, 0]],
  ['thighL', 'hips', [0.1, -0.04, 0]],
  ['shinL', 'thighL', [0, -0.45, 0]],
  ['footL', 'shinL', [0, -0.44, 0]],
  ['thighR', 'hips', [-0.1, -0.04, 0]],
  ['shinR', 'thighR', [0, -0.45, 0]],
  ['footR', 'shinR', [0, -0.44, 0]],
];
export const LIMB = { upperArm: 0.29, forearm: 0.25, thigh: 0.45, shin: 0.44 };

const tmpColor = new THREE.Color();

/** Геометрия с цветом вершин. */
function paint(geo, hex) {
  geo = geo.index ? geo.toNonIndexed() : geo;
  geo.deleteAttribute('uv');
  const n = geo.attributes.position.count;
  const c = new Float32Array(n * 3);
  tmpColor.setHex(hex);
  for (let i = 0; i < n; i++) { c[i * 3] = tmpColor.r; c[i * 3 + 1] = tmpColor.g; c[i * 3 + 2] = tmpColor.b; }
  geo.setAttribute('color', new THREE.BufferAttribute(c, 3));
  return geo;
}

const capsule = (r, len, hex, rs = 7) => paint(new THREE.CapsuleGeometry(r, len, 2, rs), hex);
const box = (w, h, d, hex) => paint(new THREE.BoxGeometry(w, h, d), hex);
const sphere = (r, hex, ws = 8, hs = 6) => paint(new THREE.SphereGeometry(r, ws, hs), hex);

/** Торс-«бочка» по профилю (плечи шире талии), сплюснутый спереди-назад. */
function torso(bottomR, topR, h, depth, hex) {
  const pts = [];
  for (let i = 0; i <= 8; i++) {
    const t = i / 8;
    const r = bottomR + (topR - bottomR) * Math.pow(t, 1.4) - Math.max(0, t - 0.86) * 0.9;
    pts.push(new THREE.Vector2(Math.max(0.02, r), t * h));
  }
  const g = new THREE.LatheGeometry(pts, 10);
  g.scale(1, 1, depth);
  return paint(g, hex);
}

/**
 * Собирает персонажа. Возвращает { root, mesh, bones, rest, cigarette, cigTip, mouth }.
 * root — Group для позиции/курса; body — вложенная группа (наклон при падении).
 */
export function buildCharacter(outfit = OUTFITS.player, { shadows = true } = {}) {
  const o = outfit;
  const bones = {};
  const list = [];
  for (const [name, parent, pos] of BONES) {
    const b = new THREE.Bone();
    b.name = name;
    b.position.fromArray(pos);
    if (parent) bones[parent].add(b);
    bones[name] = b;
    list.push(b);
  }
  bones.hips.updateMatrixWorld(true);

  const parts = [];
  const add = (bone, geo, x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0) => {
    const m = new THREE.Matrix4().compose(
      new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, rz)), new THREE.Vector3(1, 1, 1));
    geo.applyMatrix4(m);
    geo.applyMatrix4(bones[bone].matrixWorld); // в позу привязки
    const idx = list.indexOf(bones[bone]);
    const n = geo.attributes.position.count;
    const si = new Uint16Array(n * 4), sw = new Float32Array(n * 4);
    for (let i = 0; i < n; i++) { si[i * 4] = idx; sw[i * 4] = 1; }
    geo.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(si, 4));
    geo.setAttribute('skinWeight', new THREE.Float32BufferAttribute(sw, 4));
    parts.push(geo);
  };

  // --- таз и торс (олимпийка) ---
  add('hips', torso(0.15, 0.165, 0.2, 0.72, o.pants), 0, -0.1, 0);
  add('spine', torso(0.15, 0.175, 0.24, 0.7, o.suit), 0, -0.02, 0);
  add('chest', torso(0.175, 0.205, 0.22, 0.66, o.suit), 0, -0.02, 0);
  add('chest', capsule(0.075, 0.3, o.suit), 0, 0.15, -0.005, 0, 0, Math.PI / 2); // плечевой пояс
  add('chest', box(0.012, 0.42, 0.01, o.stripe), 0, -0.02, 0.138);                 // молния
  add('chest', torso(0.065, 0.06, 0.07, 1, o.suit), 0, 0.18, 0);                     // воротник-стойка
  // --- шея и голова ---
  add('neck', capsule(0.048, 0.06, o.skin), 0, 0.02, 0);
  const head = sphere(0.108, o.skin, 12, 10); head.scale(1, 1.18, 1.08);
  add('head', head, 0, 0.11, 0.01);
  add('head', box(0.032, 0.055, 0.04, o.skin), 0, 0.105, 0.128, -0.15);              // нос
  for (const ex of [0.04, -0.04]) {
    add('head', sphere(0.019, 0xf4f4f0, 8, 6), ex, 0.135, 0.112);                     // белки
    add('head', sphere(0.011, 0x2a1d14, 6, 4), ex, 0.135, 0.128);                     // зрачки
    add('head', box(0.042, 0.011, 0.012, o.hair), ex * 1.05, 0.166, 0.122, 0, 0, -Math.sign(ex) * 0.12); // брови
  }
  add('head', box(0.046, 0.009, 0.012, 0x8a4a44), 0, 0.06, 0.12);                   // рот
  add('head', sphere(0.025, o.skin, 6, 5), 0.108, 0.115, 0);                        // уши
  add('head', sphere(0.025, o.skin, 6, 5), -0.108, 0.115, 0);
  const hair = sphere(0.112, o.hair, 12, 7); hair.scale(1, 1.05, 1.08);
  add('head', hair, 0, 0.13, -0.012);
  if (o.cap !== null && o.cap !== undefined) {
    const cap = paint(new THREE.SphereGeometry(0.118, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2), o.cap); cap.scale(1, 0.85, 1.08);
    add('head', cap, 0, 0.17, -0.005);
    const brim = paint(new THREE.CylinderGeometry(0.1, 0.1, 0.012, 14, 1, false, -Math.PI / 2, Math.PI), o.capBrim ?? o.cap); brim.scale(1.05, 1, 1);
    add('head', brim, 0, 0.175, 0.09, 0.12);
    add('head', sphere(0.014, o.capBrim ?? o.cap, 6, 4), 0, 0.27, 0);
  }
  // --- руки ---
  for (const s of ['L', 'R']) {
    const sx = s === 'L' ? 1 : -1;
    add('upperArm' + s, sphere(0.068, o.suit), 0, 0, 0);
    add('upperArm' + s, capsule(0.058, 0.2, o.suit), 0, -0.145, 0);
    add('upperArm' + s, box(0.012, 0.27, 0.012, o.stripe), sx * 0.057, -0.14, 0);
    add('forearm' + s, capsule(0.05, 0.17, o.suit), 0, -0.12, 0);
    add('forearm' + s, box(0.012, 0.22, 0.012, o.stripe), sx * 0.048, -0.11, 0);
    add('forearm' + s, capsule(0.044, 0.02, o.stripe), 0, -0.235, 0);                  // резинка манжеты
    add('hand' + s, box(0.06, 0.09, 0.03, o.skin), 0, -0.05, 0.006);
    add('hand' + s, box(0.022, 0.06, 0.025, o.skin), -sx * 0.025, -0.035, 0.025, 0.3, 0, sx * 0.3); // большой палец
    // --- ноги ---
    add('thigh' + s, sphere(0.085, o.pants), 0, 0, 0);
    add('thigh' + s, capsule(0.075, 0.3, o.pants), 0, -0.22, 0);
    add('thigh' + s, box(0.012, 0.42, 0.012, o.stripe), sx * 0.076, -0.22, 0);
    add('shin' + s, capsule(0.062, 0.3, o.pants), 0, -0.2, 0);
    add('shin' + s, box(0.012, 0.36, 0.012, o.stripe), sx * 0.062, -0.2, 0);
    add('foot' + s, box(0.1, 0.075, 0.26, o.shoes), 0, -0.035, 0.05);
    add('foot' + s, box(0.104, 0.028, 0.27, o.sole), 0, -0.075, 0.05);
    add('foot' + s, box(0.104, 0.012, 0.06, o.stripe === o.shoes ? o.sole : o.stripe), 0, -0.02, 0.0);
  }

  const geo = mergeGeometries(parts, false);
  geo.computeBoundingSphere();
  const mat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.82, metalness: 0 });
  const mesh = new THREE.SkinnedMesh(geo, mat);
  mesh.name = 'Character';
  mesh.castShadow = shadows;
  mesh.frustumCulled = false;
  mesh.add(bones.hips);
  const skeleton = new THREE.Skeleton(list);
  mesh.bind(skeleton);

  const body = new THREE.Group();
  body.add(mesh);
  const root = new THREE.Group();
  root.add(body);

  // сигарета в правой руке (отдельный крошечный меш, виден только при курении)
  const cig = new THREE.Group();
  const paper = new THREE.Mesh(new THREE.CylinderGeometry(0.0055, 0.0055, 0.07, 6), new THREE.MeshLambertMaterial({ color: 0xf2f2ee }));
  const filter = new THREE.Mesh(new THREE.CylinderGeometry(0.0058, 0.0058, 0.022, 6), new THREE.MeshLambertMaterial({ color: 0xd28a3c }));
  paper.position.y = 0.035; filter.position.y = -0.011;
  const ember = new THREE.Mesh(new THREE.CylinderGeometry(0.0058, 0.0058, 0.006, 6), new THREE.MeshBasicMaterial({ color: 0xff5a1a }));
  ember.position.y = 0.071;
  cig.add(paper, filter, ember);
  cig.position.set(-0.012, -0.085, 0.035);
  cig.rotation.set(Math.PI / 2 - 0.25, 0, 0.35);
  cig.visible = false;
  bones.handR.add(cig);
  const tip = new THREE.Object3D(); tip.position.y = 0.075; cig.add(tip);

  const mouth = new THREE.Object3D(); mouth.position.set(0, 0.06, 0.12); bones.head.add(mouth);

  const rest = {};
  for (const b of list) rest[b.name] = b.position.clone();
  return { root, body, mesh, bones, rest, cigarette: cig, cigTip: tip, mouth, ember };
}

const _ikV = new THREE.Vector3(), _ikV2 = new THREE.Vector3(), _ikV3 = new THREE.Vector3(), _ikE = new THREE.Vector3();
const _ikM = new THREE.Matrix4(), _ikQ = new THREE.Quaternion(), _ikQ2 = new THREE.Quaternion();
const _DOWN = new THREE.Vector3(0, -1, 0);

/**
 * Двухзвенная IK руки: плечо → локоть → запястье в точку target (мировые координаты),
 * локоть вниз и наружу. w — вес смешивания с текущей позой (0..1).
 * Перед вызовом матрицы костей должны быть актуальны (root.updateMatrixWorld()).
 */
export function solveArmIK(bones, side, target, w, pole = null) {
  const up = bones['upperArm' + side], fo = bones['forearm' + side];
  _ikM.copy(bones.chest.matrixWorld).invert();
  const d = _ikV.copy(target).applyMatrix4(_ikM).sub(up.position);
  const L1 = LIMB.upperArm, L2 = LIMB.forearm;
  const dist = Math.min(Math.max(d.length(), 0.08), L1 + L2 - 0.002);
  const dir = d.normalize();
  const a = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist);
  const h = Math.sqrt(Math.max(0, L1 * L1 - a * a));
  const sx = side === 'L' ? 1 : -1;
  const pv = pole ? _ikV2.set(pole[0] * sx, pole[1], pole[2]) : _ikV2.set(sx * 0.7, -1, -0.15);
  pv.addScaledVector(dir, -pv.dot(dir)).normalize();
  const elbow = _ikV3.copy(dir).multiplyScalar(a).addScaledVector(pv, h);
  const hand = dir.multiplyScalar(dist);
  _ikE.copy(elbow).normalize();
  _ikQ.setFromUnitVectors(_DOWN, _ikE);
  up.quaternion.slerp(_ikQ, w);
  const f = hand.sub(elbow).normalize().applyQuaternion(_ikQ2.copy(up.quaternion).invert());
  _ikQ.setFromUnitVectors(_DOWN, f);
  fo.quaternion.slerp(_ikQ, w);
}
