import * as THREE from 'three';
import { WHEELS, NEONS } from '../game/cars.js';

// Силуэты кузова: доли длины (0 — зад, 1 — перёд) для основания кабины, крыши; высота пояса и капота.
const BODY = {
  classic:  { cab: [0.22, 0.29, 0.58, 0.67], belt: 0.60, hood: 0.60, trunk: 0.64, cabW: 0.88 },
  sedan:    { cab: [0.20, 0.30, 0.60, 0.72], belt: 0.58, hood: 0.58, trunk: 0.62, cabW: 0.88 },
  hatch:    { cab: [0.10, 0.13, 0.58, 0.72], belt: 0.60, hood: 0.56, trunk: 0.72, cabW: 0.90 },
  coupe:    { cab: [0.26, 0.40, 0.62, 0.80], belt: 0.58, hood: 0.52, trunk: 0.58, cabW: 0.86 },
  bigsedan: { cab: [0.24, 0.34, 0.60, 0.72], belt: 0.60, hood: 0.62, trunk: 0.66, cabW: 0.88 },
  wagon:    { cab: [0.08, 0.10, 0.66, 0.77], belt: 0.60, hood: 0.58, trunk: 0.68, cabW: 0.90 },
};

const matCache = new Map();
function std(color, opts = {}) {
  const k = color + JSON.stringify(opts);
  if (!matCache.has(k)) matCache.set(k, new THREE.MeshStandardMaterial({ color, ...opts }));
  return matCache.get(k);
}

function extrudeSide(shape, width, bevel = 0.05) {
  const g = new THREE.ExtrudeGeometry(shape, { depth: Math.max(0.01, width - bevel * 2), bevelEnabled: true, bevelSize: bevel, bevelThickness: bevel, bevelSegments: 2, curveSegments: 10 });
  g.rotateY(Math.PI / 2);          // x (длина) -> -z (вперёд), глубина -> x (ширина)
  g.translate(-(width - bevel * 2) / 2, 0, 0);
  g.computeVertexNormals();
  return g;
}

export function createCarModel(def, look = {}) {
  const group = new THREE.Group();
  const L = def.length, W = def.width, H = def.height;
  const prof = BODY[def.body] || BODY.sedan;
  const paint = new THREE.MeshPhysicalMaterial({ color: look.paint || def.color, metalness: 0.55, roughness: 0.32, clearcoat: 1, clearcoatRoughness: 0.08 });
  const glass = new THREE.MeshPhysicalMaterial({ color: 0x0c1218, metalness: 0.2, roughness: 0.05, clearcoat: 1, transparent: true, opacity: 0.92 });
  const dark = std(0x111214, { roughness: 0.8 });
  const chrome = std(0xcfd3d8, { metalness: 1, roughness: 0.2 });
  const parts = { paint, glass };

  const body = new THREE.Group();
  group.add(body);
  const wheelR = def.wheelRadius;
  const lowL = 0.2, belt = H * prof.belt, hoodY = H * prof.hood, trunkY = H * prof.trunk;
  const wheelbase = def.wheelbase;
  const a = wheelbase * (1 - def.weightFront), b = wheelbase * def.weightFront;
  // позиции осей вдоль длины кузова (от заднего края): центр колёсной базы = центр кузова
  const axleFront = L / 2 + wheelbase / 2, axleRear = L / 2 - wheelbase / 2;
  const ra = wheelR + 0.075;

  // нижняя часть с вырезами под колёса
  const lower = new THREE.Shape();
  const dy = lowL - wheelR;
  const half = Math.sqrt(ra * ra - dy * dy);
  const ang = Math.atan2(dy, half);
  lower.moveTo(0.02, lowL);
  lower.lineTo(axleRear - half, lowL);
  lower.absarc(axleRear, wheelR, ra, Math.PI + ang, -ang, true);
  lower.lineTo(axleFront - half, lowL);
  lower.absarc(axleFront, wheelR, ra, Math.PI + ang, -ang, true);
  lower.lineTo(L - 0.02, lowL);
  lower.lineTo(L, lowL + 0.12);
  lower.lineTo(L, hoodY * 0.92);                       // перед
  lower.lineTo(L * 0.93, hoodY);                       // капот
  lower.lineTo(L * (prof.cab[3] + 0.02), hoodY + 0.02);
  lower.lineTo(L * prof.cab[0], trunkY);              // основание кабины сзади
  lower.lineTo(L * 0.05, trunkY - 0.02);
  lower.lineTo(0, trunkY - 0.12);
  lower.lineTo(0, lowL + 0.08);
  lower.closePath();
  const lowerMesh = new THREE.Mesh(extrudeSide(lower, W, 0.06), paint);
  lowerMesh.castShadow = true;
  body.add(lowerMesh);
  // тёмный «пол» между арками
  const floor = new THREE.Mesh(new THREE.BoxGeometry(W * 0.84, 0.22, L * 0.9), dark);
  floor.position.set(0, lowL + 0.1, 0);
  body.add(floor);

  // кабина (стекло) + крыша + стойки
  const cabW = W * prof.cabW;
  const cabBase = Math.max(hoodY, trunkY) - 0.03;
  const cab = new THREE.Shape();
  cab.moveTo(L * prof.cab[0], trunkY - 0.02);
  cab.lineTo(L * prof.cab[1], H);
  cab.lineTo(L * prof.cab[2], H);
  cab.lineTo(L * prof.cab[3], hoodY);
  cab.closePath();
  const cabMesh = new THREE.Mesh(extrudeSide(cab, cabW, 0.025), glass);
  body.add(cabMesh);
  // крыша (окрашенная плита) и стойки
  const roofLen = L * (prof.cab[2] - prof.cab[1]) + 0.12;
  const roof = new THREE.Mesh(new THREE.BoxGeometry(cabW + 0.04, 0.06, roofLen), paint);
  roof.position.set(0, H - 0.02, -(L * ((prof.cab[1] + prof.cab[2]) / 2) - L / 2));
  roof.castShadow = true;
  body.add(roof);
  const pillar = (z0, y0, z1, y1) => {
    for (const side of [-1, 1]) {
      const len = Math.hypot(z1 - z0, y1 - y0);
      const m = new THREE.Mesh(new THREE.BoxGeometry(0.06, len, 0.07), paint);
      m.position.set(side * cabW / 2, (y0 + y1) / 2, -(((z0 + z1) / 2) - L / 2));
      m.rotation.x = Math.atan2(-(z1 - z0), (y1 - y0));
      body.add(m);
    }
  };
  pillar(L * prof.cab[3], hoodY, L * prof.cab[2], H);   // A
  pillar(L * prof.cab[0], trunkY, L * prof.cab[1], H);  // C

  // фары, стоп-сигналы, решётка, бамперы
  const headMat = std(0xfff3c4, { emissive: 0xfff0b0, emissiveIntensity: 0.9 });
  const tailMat = new THREE.MeshStandardMaterial({ color: 0x7a0a0a, emissive: 0xff1010, emissiveIntensity: 0.35 });
  const lampY = hoodY - 0.12;
  for (const s of [-1, 1]) {
    const h = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.14, 0.06), headMat); h.position.set(s * W * 0.34, lampY, -L / 2 - 0.005); body.add(h);
    const t = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.14, 0.05), tailMat); t.position.set(s * W * 0.34, trunkY - 0.2, L / 2 + 0.005); body.add(t);
  }
  const grille = new THREE.Mesh(new THREE.BoxGeometry(W * 0.34, 0.12, 0.05), dark); grille.position.set(0, lampY, -L / 2 - 0.005); body.add(grille);
  const bumperF = new THREE.Mesh(new THREE.BoxGeometry(W * 0.98, 0.16, 0.14), dark); bumperF.position.set(0, lowL + 0.14, -L / 2 + 0.02); body.add(bumperF);
  const bumperR = new THREE.Mesh(new THREE.BoxGeometry(W * 0.98, 0.16, 0.14), dark); bumperR.position.set(0, lowL + 0.14, L / 2 - 0.02); body.add(bumperR);
  const plate = new THREE.Mesh(new THREE.BoxGeometry(0.46, 0.12, 0.02), std(0xe8e8e0)); plate.position.set(0, trunkY - 0.28, L / 2 + 0.01); body.add(plate);
  const exhaust = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 0.18, 10), chrome); exhaust.rotation.x = Math.PI / 2; exhaust.position.set(W * 0.3, lowL + 0.06, L / 2 + 0.04); body.add(exhaust);
  // зеркала
  for (const s of [-1, 1]) { const mm = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.09, 0.1), paint); mm.position.set(s * (cabW / 2 + 0.1), hoodY + 0.12, -(L * prof.cab[3] - L / 2) + 0.05); body.add(mm); }

  // обвес
  const kit = look.bodykit || 'none';
  if (kit !== 'none') {
    const kitMat = std(0x16161a, { roughness: 0.5 });
    const lip = new THREE.Mesh(new THREE.BoxGeometry(W * 1.02, 0.05, 0.3), kitMat); lip.position.set(0, lowL + 0.04, -L / 2 - 0.02); body.add(lip);
    const diff = new THREE.Mesh(new THREE.BoxGeometry(W * 0.9, 0.12, 0.25), kitMat); diff.position.set(0, lowL + 0.06, L / 2 - 0.1); body.add(diff);
    for (const s of [-1, 1]) {
      const skirt = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.12, wheelbase * 0.62), kitMat); skirt.position.set(s * (W / 2 + 0.01), lowL + 0.1, -(axleRear + axleFront) / 2 + L / 2); body.add(skirt);
    }
    if (kit === 'wide' || kit === 'rally') {
      for (const s of [-1, 1]) for (const ax of [axleFront, axleRear]) {
        const fl = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.12, 1.0), paint); fl.position.set(s * (W / 2 + 0.03), wheelR + ra * 0.85, -(ax - L / 2)); fl.rotation.z = s * 0.2; body.add(fl);
      }
    }
    if (kit === 'rally') {
      const scoop = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.1, 0.5), kitMat); scoop.position.set(0, H + 0.04, -(L * 0.45 - L / 2)); body.add(scoop);
      for (const s of [-0.28, -0.1, 0.1, 0.28]) { const lamp = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 0.08, 12), headMat); lamp.rotation.x = Math.PI / 2; lamp.position.set(s * W, hoodY + 0.14, -L / 2 + 0.2); body.add(lamp); }
    }
  }
  const spo = look.spoiler || 'none';
  if (spo !== 'none') {
    const sm = std(0x16161a, { roughness: 0.45 });
    const zr = L / 2 - 0.15;
    if (spo === 'lip') { const m = new THREE.Mesh(new THREE.BoxGeometry(W * 0.86, 0.04, 0.16), sm); m.position.set(0, trunkY + 0.01, zr); body.add(m); }
    if (spo === 'duck') { const m = new THREE.Mesh(new THREE.BoxGeometry(W * 0.9, 0.05, 0.26), sm); m.position.set(0, trunkY + 0.12, zr); m.rotation.x = -0.15; body.add(m); for (const s of [-1, 1]) { const f = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.12, 0.2), sm); f.position.set(s * W * 0.36, trunkY + 0.06, zr); body.add(f); } }
    if (spo === 'gt') { const m = new THREE.Mesh(new THREE.BoxGeometry(W * 1.0, 0.05, 0.34), sm); m.position.set(0, trunkY + 0.34, zr + 0.05); m.rotation.x = -0.1; body.add(m); for (const s of [-1, 1]) { const f = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.34, 0.2), sm); f.position.set(s * W * 0.34, trunkY + 0.17, zr); body.add(f); const ep = new THREE.Mesh(new THREE.BoxGeometry(0.03, 0.14, 0.38), paint); ep.position.set(s * W * 0.5, trunkY + 0.34, zr + 0.05); body.add(ep); } }
  }

  // колёса
  const wheels = [];
  const wDef = WHEELS.find((w) => w.id === (look.wheels || 'steel')) || WHEELS[0];
  const tireMat = std(0x0e0e10, { roughness: 0.9 });
  const rimMat = std(look.rim || (wDef.id === 'steel' ? 0x9aa0a6 : 0xd7dadf), { metalness: 0.9, roughness: 0.25 });
  const tw = Math.max(0.2, W * 0.14);
  const wheelXs = [-def.trackFront / 2, def.trackFront / 2, -def.trackRear / 2, def.trackRear / 2];
  const wheelZs = [a, a, -b, -b];
  for (let i = 0; i < 4; i++) {
    const pivot = new THREE.Group();   // руль
    const spin = new THREE.Group();    // вращение
    const tire = new THREE.Mesh(new THREE.CylinderGeometry(wheelR, wheelR, tw, 28), tireMat); tire.rotation.z = Math.PI / 2; tire.castShadow = true; spin.add(tire);
    const side = i % 2 === 0 ? -1 : 1;
    const rimR = wheelR * 0.68;
    const disc = new THREE.Mesh(new THREE.CylinderGeometry(rimR, rimR, tw * 0.9, 24), wDef.spokes === 0 ? rimMat : std(0x24262a, { metalness: 0.6, roughness: 0.5 })); disc.rotation.z = Math.PI / 2; spin.add(disc);
    if (wDef.spokes > 0) {
      for (let k = 0; k < wDef.spokes; k++) {
        const sp = new THREE.Mesh(new THREE.BoxGeometry(tw * 0.55, rimR * 1.9, wDef.spokes > 8 ? 0.018 : 0.045), rimMat);
        sp.position.x = side * tw * 0.32; sp.rotation.x = (k / wDef.spokes) * Math.PI;
        spin.add(sp);
      }
      const ring = new THREE.Mesh(new THREE.TorusGeometry(rimR * 0.98, 0.022, 8, 28), rimMat); ring.rotation.y = Math.PI / 2; ring.position.x = side * tw * 0.42; spin.add(ring);
    } else {
      const cap = new THREE.Mesh(new THREE.CylinderGeometry(rimR * 0.25, rimR * 0.25, tw * 1.0, 12), chrome); cap.rotation.z = Math.PI / 2; spin.add(cap);
    }
    pivot.add(spin);
    pivot.position.set(wheelXs[i], wheelR, -wheelZs[i]);
    group.add(pivot);
    wheels.push({ pivot, spin });
  }
  // смещаем кузов так, чтобы его центр совпал с центром колёсной базы
  const wbCenterZ = (a + -b) / 2;                  // вперёд (сим.) от ЦТ
  body.position.set(0, 0, -wbCenterZ);
  body.traverse((o) => { if (o.isMesh) { o.castShadow = true; } });
  // выровнять геометрию кузова по длине: ExtrudeGeometry занимает z от -L..0 → сдвигаем на +L/2
  body.children.forEach((c) => { if (c.geometry && c.geometry.type === 'ExtrudeGeometry') c.geometry.translate(0, 0, L / 2); });

  // неон
  let neon = null;
  const nDef = NEONS.find((n) => n.id === (look.neon || 'none'));
  if (nDef && nDef.color) {
    const glow = new THREE.Mesh(new THREE.PlaneGeometry(W * 1.5, L * 1.15), new THREE.MeshBasicMaterial({ color: nDef.color, transparent: true, opacity: 0.55, blending: THREE.AdditiveBlending, depthWrite: false }));
    glow.rotation.x = -Math.PI / 2; glow.position.set(0, 0.05, -wbCenterZ);
    group.add(glow);
    const pl = new THREE.PointLight(nDef.color, 3.5, 7, 2); pl.position.set(0, 0.25, -wbCenterZ); group.add(pl);
    neon = { glow, light: pl };
  }
  group.userData = { wheels, tailMat, headMat, neon, body, parts };
  return group;
}

/** Синхронизация позы модели с физикой (координаты three: x, -z). */
export function syncCarModel(group, car) {
  group.position.set(car.x, 0, -car.z);
  group.rotation.set(0, -car.h, 0);
  const u = group.userData;
  u.body.rotation.x = car.pitch;      // тангаж: торможение — клевок носом
  u.body.rotation.z = car.roll;
  const steer = car.steerAngle;
  for (let i = 0; i < 4; i++) {
    const w = u.wheels[i];
    if (i < 2) w.pivot.rotation.y = -steer;
    w.spin.rotation.x = -car.wheelSpin[i];
  }
  const braking = car.input.brake > 0.1 && !car.reversing;
  u.tailMat.emissiveIntensity = braking || car.input.handbrake ? 2.2 : 0.35;
}

/** Освободить геометрию/материалы модели (материалы из кэша не трогаем). */
export function disposeModel(group) {
  group.traverse((o) => { if (o.geometry) o.geometry.dispose(); });
}
