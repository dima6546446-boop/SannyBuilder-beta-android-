import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { SURFACES } from '../physics/config.js';
import { surfaceTexture, facadeTextures, glowTexture, tyreTexture } from './textures.js';
import { offsetLine } from '../game/geom.js';

const V = (x, z) => new THREE.Vector3(x, 0, -z);   // координаты симуляции -> three

const SURF_STYLE = {
  asphalt: { color: 0xffffff, rough: 0.92, tile: 7 },
  concrete: { color: 0xffffff, rough: 0.95, tile: 9 },
  grass: { color: 0xffffff, rough: 1, tile: 5 },
  gravel: { color: 0xffffff, rough: 1, tile: 4 },
  dirt: { color: 0xffffff, rough: 1, tile: 5 },
  wet: { color: 0xe0e4e8, rough: 0.16, tile: 7, metal: 0.2 },
  snow: { color: 0xffffff, rough: 0.9, tile: 6 },
};
const matCache = new Map();
function surfMat(type, snow = false) {
  const t = snow ? 'snow' : type;
  if (!matCache.has(t)) {
    const st = SURF_STYLE[t] || SURF_STYLE.asphalt;
    const tex = surfaceTexture(t).clone(); tex.needsUpdate = true; tex.repeat.set(1, 1);
    matCache.set(t, new THREE.MeshStandardMaterial({ map: surfaceTexture(t), color: st.color, roughness: st.rough, metalness: st.metal || 0 }));
  }
  return matCache.get(t);
}
function worldUV(geo, tile) {
  const p = geo.attributes.position, uv = new Float32Array(p.count * 2);
  for (let i = 0; i < p.count; i++) { uv[i * 2] = p.getX(i) / tile; uv[i * 2 + 1] = p.getZ(i) / tile; }
  geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  return geo;
}

function ribbonGeometry(pts, hw, closed, y, tile) {
  const left = offsetLine(pts, hw, closed), right = offsetLine(pts, -hw, closed);
  const n = pts.length, pos = [], idx = [], uv = [];
  let dist = 0;
  for (let i = 0; i < n; i++) {
    if (i > 0) dist += Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
    pos.push(left[i][0], y, -left[i][1], right[i][0], y, -right[i][1]);
    uv.push(0, dist / tile, hw * 2 / tile, dist / tile);
  }
  const segs = closed ? n : n - 1;
  for (let i = 0; i < segs; i++) { const a = i * 2, b = ((i + 1) % n) * 2; idx.push(a, b, a + 1, a + 1, b, b + 1); }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.setIndex(idx);
  g.computeVertexNormals();
  // нормали вверх
  const nrm = g.attributes.normal; for (let i = 0; i < nrm.count; i++) nrm.setXYZ(i, 0, 1, 0);
  return g;
}

function stripGeometry(pts, w, y, dash, closed = false) {
  // тонкая лента вдоль ломаной (с пунктиром)
  const pos = [], idx = [];
  const push = (ax, az, bx, bz) => {
    const dx = bx - ax, dz = bz - az, l = Math.hypot(dx, dz); if (l < 1e-4) return;
    const nx = -dz / l * w / 2, nz = dx / l * w / 2;
    const base = pos.length / 3;
    pos.push(ax + nx, y, -(az + nz), ax - nx, y, -(az - nz), bx + nx, y, -(bz + nz), bx - nx, y, -(bz - nz));
    idx.push(base, base + 1, base + 2, base + 2, base + 1, base + 3);
  };
  let carry = 0, on = true;
  const n = pts.length;
  for (let i = 0; i < (closed ? n : n - 1); i++) {
    let a = pts[i], b = pts[(i + 1) % n];
    if (!dash) { push(a[0], a[1], b[0], b[1]); continue; }
    let dx = b[0] - a[0], dz = b[1] - a[1]; const L = Math.hypot(dx, dz); dx /= L; dz /= L;
    let t = 0;
    while (t < L) {
      const rem = (on ? dash[0] : dash[1]) - carry;
      const step = Math.min(rem, L - t);
      if (on) push(a[0] + dx * t, a[1] + dz * t, a[0] + dx * (t + step), a[1] + dz * (t + step));
      t += step; carry += step;
      if (carry >= (on ? dash[0] : dash[1]) - 1e-6) { on = !on; carry = 0; }
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setIndex(idx); g.computeVertexNormals();
  const nrm = g.attributes.normal; for (let i = 0; i < nrm.count; i++) nrm.setXYZ(i, 0, 1, 0);
  return g;
}

function buildingMesh(b, night) {
  const style = b.style === 'mall' || b.style === 'warehouse' || b.style === 'factory' ? b.style : (b.style === 'office' ? 'office' : 'apart');
  const tex = facadeTextures(style);
  const geo = new THREE.BoxGeometry(b.w, b.h, b.d);
  const uv = geo.attributes.uv, groups = geo.groups;
  const rep = (w, h) => [Math.max(1, Math.round(w / (style === 'warehouse' ? 14 : 6))), Math.max(1, Math.round(h / (style === 'warehouse' ? 7 : 3.4)))];
  for (const gp of groups) {
    const side = gp.materialIndex; if (side === 2 || side === 3) continue;
    const [ru, rv] = rep(side < 2 ? b.d : b.w, b.h);
    for (let i = gp.start; i < gp.start + gp.count; i++) { const idx = geo.index.getX(i); uv.setXY(idx, uv.getX(idx) * ru, uv.getY(idx) * rv); }
  }
  const sideMat = new THREE.MeshStandardMaterial({ map: tex.map, color: b.color, emissiveMap: tex.em, emissive: 0xffffff, emissiveIntensity: night ? 1.1 : 0.0, roughness: 0.85 });
  const roofMat = new THREE.MeshStandardMaterial({ color: 0x3b3d42, roughness: 0.95 });
  const mesh = new THREE.Mesh(geo, [sideMat, sideMat, roofMat, roofMat, sideMat, sideMat]);
  mesh.position.set(b.x, b.h / 2, -b.z); mesh.rotation.y = -(b.rot || 0);
  mesh.castShadow = true; mesh.receiveShadow = true;
  return mesh;
}

function instanced(geo, mat, count) { const m = new THREE.InstancedMesh(geo, mat, count); m.castShadow = true; m.receiveShadow = true; return m; }
const _o = new THREE.Object3D(); const _c = new THREE.Color();
function setInst(mesh, i, x, y, z, rotY = 0, sx = 1, sy = 1, sz = 1, color = null) {
  _o.position.set(x, y, z); _o.rotation.set(0, rotY, 0); _o.scale.set(sx, sy, sz); _o.updateMatrix(); mesh.setMatrixAt(i, _o.matrix);
  if (color !== null) { _c.set(color); mesh.setColorAt(i, _c); }
}

export function buildMap(map, env) {
  const group = new THREE.Group();
  const night = env.time === 'night';
  const disposables = [];
  const snow = env.weather === 'snow';
  const rain = env.weather === 'rain';
  const wetify = (t) => (rain && (t === 'asphalt' || t === 'concrete') ? 'wet' : t);
  const track = (g) => { disposables.push(g); return g; };

  // земля
  const gSize = 1600;
  const gGeo = new THREE.PlaneGeometry(gSize, gSize); gGeo.rotateX(-Math.PI / 2); worldUV(gGeo, SURF_STYLE[map.ground]?.tile || 8);
  const ground = new THREE.Mesh(track(gGeo), surfMat(snow ? 'snow' : wetify(map.theme === 'mountain' ? 'grass' : map.theme === 'parking' ? 'asphalt' : map.ground)));
  ground.position.y = -0.02; ground.receiveShadow = true; group.add(ground);

  // области покрытия
  let yi = 0;
  for (const s of map.surfaces) {
    yi++;
    let g;
    const y = 0.004 * yi;
    if (s.kind === 'rect') { g = new THREE.PlaneGeometry(s.x1 - s.x0, s.z1 - s.z0); g.rotateX(-Math.PI / 2); g.translate((s.x0 + s.x1) / 2, y, -(s.z0 + s.z1) / 2); }
    else if (s.kind === 'circle') { g = new THREE.CircleGeometry(s.r, 48); g.rotateX(-Math.PI / 2); g.translate(s.x, y, -s.z); }
    else { g = ribbonGeometry(s.pts, s.hw, s.closed, y, 7); }
    if (s.kind !== 'ribbon') worldUV(g, (SURF_STYLE[s.type] || SURF_STYLE.asphalt).tile);
    const m = new THREE.Mesh(track(g), surfMat(snow && s.type !== 'wet' ? 'snow' : wetify(s.type), snow));
    m.receiveShadow = true; group.add(m);
  }
  // нарисованные дороги
  for (const r of map.roads) {
    if (!r.draw) continue;
    const g = ribbonGeometry(r.pts, r.width / 2, r.closed, 0.02, 7);
    const m = new THREE.Mesh(track(g), surfMat(snow ? 'snow' : wetify('asphalt'))); m.receiveShadow = true; group.add(m);
  }
  // разметка
  const markGroups = new Map();
  for (const mk of map.markings) {
    const g = stripGeometry(mk.pts, mk.w, 0.05, mk.dash, mk.closed);
    if (!markGroups.has(mk.color)) markGroups.set(mk.color, []);
    markGroups.get(mk.color).push(g);
  }
  for (const [color, list] of markGroups) {
    const g = mergeGeometries(list); list.forEach((x) => x.dispose());
    const m = new THREE.Mesh(track(g), new THREE.MeshStandardMaterial({ color: snow ? 0xdddddd : color, roughness: 0.7, polygonOffset: true, polygonOffsetFactor: -2 }));
    m.receiveShadow = true; group.add(m);
  }

  // здания
  for (const b of map.buildings) group.add(buildingMesh(b, night));
  for (const b of map.decoBuildings) group.add(buildingMesh(b, night));

  // контейнеры
  const conts = map.containers.filter((c) => !c.small), small = map.containers.filter((c) => c.small);
  if (conts.length) {
    const g = new THREE.BoxGeometry(2.45, 2.6, 12);
    const m = instanced(track(g), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.6, metalness: 0.2 }), conts.length);
    conts.forEach((c, i) => setInst(m, i, c.x, c.y + 1.3, -c.z, -c.rot, 1, 1, 1, c.color));
    group.add(m);
  }
  if (small.length) {
    const g = new THREE.BoxGeometry(1.8, 1.4, 3.2);
    const m = instanced(track(g), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.7 }), small.length);
    small.forEach((c, i) => setInst(m, i, c.x, 0.7, -c.z, -c.rot, 1, 1, 1, c.color));
    group.add(m);
  }

  // припаркованные машины (облегчённые)
  if (map.parked.length) {
    const n = map.parked.length;
    const body = instanced(track(new THREE.BoxGeometry(1.8, 0.62, 4.2)), new THREE.MeshStandardMaterial({ color: 0xffffff, metalness: 0.5, roughness: 0.4 }), n);
    const cabin = instanced(track(new THREE.BoxGeometry(1.6, 0.5, 2.1)), new THREE.MeshStandardMaterial({ color: 0x151a20, metalness: 0.2, roughness: 0.15 }), n);
    const wheel = instanced(track(new THREE.CylinderGeometry(0.31, 0.31, 0.22, 14)), new THREE.MeshStandardMaterial({ color: 0x101012, roughness: 0.9 }), n * 4);
    map.parked.forEach((p, i) => {
      setInst(body, i, p.x, 0.62, -p.z, -p.rot, 1, 1, 1, p.color);
      setInst(cabin, i, p.x, 1.1, -p.z, -p.rot, 1, 1, 1);
      const c = Math.cos(p.rot), s = Math.sin(p.rot);
      let k = 0;
      for (const [lx, lz] of [[-0.82, 1.35], [0.82, 1.35], [-0.82, -1.35], [0.82, -1.35]]) {
        const wx = p.x + lx * c + lz * s, wz = p.z - lx * s + lz * c;
        _o.position.set(wx, 0.31, -wz); _o.rotation.set(0, -p.rot, Math.PI / 2); _o.scale.set(1, 1, 1); _o.updateMatrix(); wheel.setMatrixAt(i * 4 + k++, _o.matrix);
      }
    });
    group.add(body, cabin, wheel);
  }

  // деревья
  if (map.trees.length) {
    const n = map.trees.length;
    const pine = map.theme === 'mountain';
    const trunk = instanced(track(new THREE.CylinderGeometry(0.15, 0.22, 2.2, 7)), new THREE.MeshStandardMaterial({ color: 0x4a3524, roughness: 1 }), n);
    const crownGeo = pine ? new THREE.ConeGeometry(1.6, 5.5, 8) : new THREE.IcosahedronGeometry(2.0, 1);
    const crown = instanced(track(crownGeo), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95, flatShading: true }), n);
    map.trees.forEach((t, i) => {
      setInst(trunk, i, t.x, 1.1 * t.s, -t.z, 0, t.s, t.s, t.s);
      const col = snow ? 0xdfe9ee : (pine ? [0x1f4a2a, 0x24552f, 0x1a3f26][i % 3] : [0x3f7a32, 0x4a8a38, 0x35692c][i % 3]);
      setInst(crown, i, t.x, (pine ? 4.2 : 3.6) * t.s, -t.z, i, t.s, t.s, t.s, col);
    });
    group.add(trunk, crown);
  }

  // фонари
  const lampPositions = [];
  if (map.lamps.length) {
    const n = map.lamps.length;
    const pole = instanced(track(new THREE.CylinderGeometry(0.09, 0.12, 1, 8)), new THREE.MeshStandardMaterial({ color: 0x44484f, metalness: 0.6, roughness: 0.5 }), n);
    const head = instanced(track(new THREE.BoxGeometry(0.9, 0.12, 0.35)), new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xffe2a0, emissiveIntensity: night || env.time === 'dusk' ? 2.2 : 0.15 }), n);
    map.lamps.forEach((l, i) => {
      setInst(pole, i, l.x, l.h / 2, -l.z, 0, 1, l.h, 1);
      setInst(head, i, l.x, l.h + 0.05, -l.z, 0);
      lampPositions.push({ x: l.x, y: l.h, z: -l.z });
    });
    group.add(pole, head);
    if (night || env.time === 'dusk') {
      const sprMat = new THREE.SpriteMaterial({ map: glowTexture(), blending: THREE.AdditiveBlending, depthWrite: false, transparent: true, opacity: night ? 0.9 : 0.6 });
      map.lamps.forEach((l) => { const sp = new THREE.Sprite(sprMat); sp.position.set(l.x, l.h, -l.z); sp.scale.set(7, 7, 1); group.add(sp); });
    }
  }

  // барьеры
  const byStyle = {};
  for (const bar of map.barriers) (byStyle[bar.style] = byStyle[bar.style] || []).push(bar);
  const boxSeg = (a, b, h, t, y0 = 0) => {
    const dx = b[0] - a[0], dz = b[1] - a[1], l = Math.hypot(dx, dz); if (l < 0.01) return null;
    const g = new THREE.BoxGeometry(l + 0.02, h, t); g.rotateY(Math.atan2(dz, dx));
    g.translate((a[0] + b[0]) / 2, y0 + h / 2, -(a[1] + b[1]) / 2);
    return g;
  };
  const mergeBars = (list, h, t, y0 = 0) => {
    const gs = [];
    for (const bar of list) { const n = bar.pts.length; for (let i = 0; i < (bar.closed ? n : n - 1); i++) { const g = boxSeg(bar.pts[i], bar.pts[(i + 1) % n], h, t, y0); if (g) gs.push(g); } }
    if (!gs.length) return null;
    const m = mergeGeometries(gs); gs.forEach((g) => g.dispose()); return track(m);
  };
  const barMesh = (g, color, rough = 0.85, metal = 0) => { if (!g) return; const m = new THREE.Mesh(g, new THREE.MeshStandardMaterial({ color, roughness: rough, metalness: metal })); m.castShadow = true; m.receiveShadow = true; group.add(m); };
  if (byStyle.jersey) barMesh(mergeBars(byStyle.jersey, 0.95, 0.55), 0xb9bbbd);
  if (byStyle.wall) barMesh(mergeBars(byStyle.wall, 3.2, 0.6), 0x8b8f93);
  if (byStyle.curb) barMesh(mergeBars(byStyle.curb, 0.28, 0.5), 0xc9c9c2);
  if (byStyle.guardrail) {
    barMesh(mergeBars(byStyle.guardrail, 0.34, 0.1, 0.55), 0xc2c6cb, 0.45, 0.7);
    // стойки
    const posts = [];
    for (const bar of byStyle.guardrail) for (let i = 0; i < bar.pts.length; i += 3) posts.push(bar.pts[i]);
    const pm = instanced(track(new THREE.BoxGeometry(0.1, 0.9, 0.1)), new THREE.MeshStandardMaterial({ color: 0x8a8f95, metalness: 0.6, roughness: 0.6 }), posts.length);
    posts.forEach((p, i) => setInst(pm, i, p[0], 0.45, -p[1], 0));
    group.add(pm);
  }
  if (byStyle.tyres) {
    const tpos = [];
    for (const bar of byStyle.tyres) for (let i = 0; i < bar.pts.length - 1; i++) {
      const a = bar.pts[i], b = bar.pts[i + 1], l = Math.hypot(b[0] - a[0], b[1] - a[1]), n = Math.ceil(l / 0.72);
      for (let k = 0; k <= n; k++) for (let lv = 0; lv < 3; lv++) tpos.push([a[0] + (b[0] - a[0]) * k / n, lv * 0.3 + 0.15, a[1] + (b[1] - a[1]) * k / n, (k + lv) % 2]);
    }
    const tm = instanced(track(new THREE.TorusGeometry(0.28, 0.14, 8, 14)), new THREE.MeshStandardMaterial({ color: 0xffffff, map: tyreTexture(), roughness: 0.95 }), tpos.length);
    tpos.forEach((t, i) => { _o.position.set(t[0], t[1], -t[2]); _o.rotation.set(Math.PI / 2, 0, 0); _o.scale.set(1, 1, 1); _o.updateMatrix(); tm.setMatrixAt(i, _o.matrix); _c.set(t[3] ? 0xf4f4f4 : 0x3a3a3c); tm.setColorAt(i, _c); });
    group.add(tm);
  }
  // шаблоны: фонтан, цистерны, столб
  for (const p of map.pads) {
    if (p.kind === 'fountain') {
      const basin = new THREE.Mesh(track(new THREE.CylinderGeometry(p.r, p.r, 0.7, 32)), new THREE.MeshStandardMaterial({ color: 0xb6b6b0, roughness: 0.8 })); basin.position.set(p.x, 0.35, -p.z); basin.castShadow = true; group.add(basin);
      const water = new THREE.Mesh(track(new THREE.CylinderGeometry(p.r * 0.88, p.r * 0.88, 0.05, 32)), new THREE.MeshStandardMaterial({ color: 0x4aa8d8, roughness: 0.05, metalness: 0.3, transparent: true, opacity: 0.85 })); water.position.set(p.x, 0.68, -p.z); group.add(water);
      const spire = new THREE.Mesh(track(new THREE.CylinderGeometry(0.3, 0.6, 2.5, 12)), new THREE.MeshStandardMaterial({ color: 0xa9a9a2 })); spire.position.set(p.x, 1.6, -p.z); group.add(spire);
    } else if (p.kind === 'tank') {
      const t = new THREE.Mesh(track(new THREE.CylinderGeometry(p.r, p.r, 10, 24)), new THREE.MeshStandardMaterial({ color: 0xa8aeb4, metalness: 0.5, roughness: 0.45 })); t.position.set(p.x, 5, -p.z); t.castShadow = true; group.add(t);
    } else if (p.kind === 'bollard') {
      const t = new THREE.Mesh(track(new THREE.CylinderGeometry(p.r, p.r, 1.1, 14)), new THREE.MeshStandardMaterial({ color: 0xf2c94c })); t.position.set(p.x, 0.55, -p.z); t.castShadow = true; group.add(t);
      const ring = new THREE.Mesh(track(new THREE.RingGeometry(p.r + 12, p.r + 12.3, 64)), new THREE.MeshBasicMaterial({ color: 0xf2c94c, side: THREE.DoubleSide })); ring.rotation.x = -Math.PI / 2; ring.position.set(p.x, 0.06, -p.z); group.add(ring);
    }
  }
  // дальние горы (для серпантина) и дальний фон
  if (map.theme === 'mountain') {
    const rr = (a) => ((Math.sin(a * 91.7) * 43758.5453) % 1 + 1) % 1;
    const gs = [];
    for (let i = 0; i < 40; i++) {
      const a = i / 40 * Math.PI * 2, d = 420 + rr(i) * 160, h = 90 + rr(i + 7) * 140, w = 120 + rr(i + 3) * 120;
      const g = new THREE.ConeGeometry(w, h, 7); g.translate(Math.cos(a) * d + 0, h / 2 - 2, Math.sin(a) * d + 100); gs.push(g);
    }
    const mg = mergeGeometries(gs); gs.forEach((g) => g.dispose());
    const mm = new THREE.Mesh(track(mg), new THREE.MeshStandardMaterial({ color: snow ? 0xe6edf2 : 0x5c6670, roughness: 1, flatShading: true })); group.add(mm);
  }

  // динамические объекты: конусы и бочки
  const cones = map.props.filter((p) => p.type === 'cone'), barrels = map.props.filter((p) => p.type === 'barrel');
  const dyn = { cones: null, barrels: null, coneIdx: [], barrelIdx: [] };
  map.props.forEach((p, i) => { if (p.type === 'cone') dyn.coneIdx.push(i); else dyn.barrelIdx.push(i); });
  if (cones.length) {
    const g = new THREE.ConeGeometry(0.2, 0.62, 12); g.translate(0, 0.31, 0);
    dyn.cones = instanced(track(g), new THREE.MeshStandardMaterial({ color: 0xff6a1a, roughness: 0.6 }), cones.length);
    group.add(dyn.cones);
  }
  if (barrels.length) {
    const g = new THREE.CylinderGeometry(0.3, 0.3, 0.9, 14); g.translate(0, 0.45, 0);
    dyn.barrels = instanced(track(g), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.5, metalness: 0.2 }), barrels.length);
    barrels.forEach((b, i) => { _c.set(b.color || 0xd9531e); dyn.barrels.setColorAt(i, _c); });
    group.add(dyn.barrels);
  }
  const updateProps = (props) => {
    if (dyn.cones) {
      dyn.coneIdx.forEach((pi, k) => {
        const p = props[pi]; const sp = Math.hypot(p.vx, p.vz);
        const tilt = p.hit ? Math.min(1.5, 0.3 + sp * 0.4) : 0;
        _o.position.set(p.x, p.hit ? 0.1 : 0, -p.z); _o.rotation.set(0, p.rot, 0); _o.rotateOnAxis(new THREE.Vector3(Math.cos(p.rot), 0, Math.sin(p.rot)), tilt); _o.scale.set(1, 1, 1); _o.updateMatrix(); dyn.cones.setMatrixAt(k, _o.matrix);
      });
      dyn.cones.instanceMatrix.needsUpdate = true;
    }
    if (dyn.barrels) {
      dyn.barrelIdx.forEach((pi, k) => {
        const p = props[pi]; const sp = Math.hypot(p.vx, p.vz);
        const tilt = p.hit ? Math.min(1.57, 0.4 + sp * 0.3) : 0;
        _o.position.set(p.x, p.hit && tilt > 1 ? 0.3 : 0, -p.z); _o.rotation.set(0, p.rot, 0); _o.rotateOnAxis(new THREE.Vector3(Math.cos(p.rot), 0, Math.sin(p.rot)), tilt); _o.scale.set(1, 1, 1); _o.updateMatrix(); dyn.barrels.setMatrixAt(k, _o.matrix);
      });
      dyn.barrels.instanceMatrix.needsUpdate = true;
    }
  };

  return {
    group, lampPositions, updateProps,
    dispose() { group.traverse((o) => { if (o.isMesh && o.material && !Array.isArray(o.material) && o.material.userData?.own) o.material.dispose(); }); disposables.forEach((d) => d.dispose && d.dispose()); },
  };
}
