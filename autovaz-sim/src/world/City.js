import * as THREE from 'three';
import { CITY, coord, CITY_EXTENT } from './RoadGraph.js';
import { QuadBatch, prep, merge, scaleUV } from '../utils/geometry.js';
import { mulberry32, makeNoise2D, smoothstep, clamp, lerp } from '../utils/math.js';
import { segAabb } from '../core/CollisionWorld.js';
import { CARS } from '../config/cars.js';

const { N, SPACING, HALF, SIDEWALK, CURB } = CITY;
const SEG_LEN = SPACING - 2 * HALF;
const FACADE_TILE = 24;
const FLAT_UV = [6 / 512, 1 - 8 / 512];
const E = CITY_EXTENT;

/** Автодром ДОСААФ (площадка для парковочных уровней и экзамена) — за западными воротами. */
export const AUTODROME = { x0: -570, x1: -372, z0: -100, z1: 100, cx: -471, cz: 0, gateHalf: 8 };
const CORRIDOR = { x0: AUTODROME.x1, x1: -E, half: 8 };

const BUILDING_COLORS = [0xd9d3c5, 0xcfcfcf, 0xc4b59a, 0xb9785a, 0xe3dbc0, 0xaebfcc, 0xd7c6a8, 0xe6d3a0];
const GARAGE_COLORS = [0x7d7f80, 0x6f6152, 0x8a7a66, 0x5d6a72, 0x94918a, 0x6e7a5a];

// UV-прямоугольники атласов
const signUV = (i) => { const u0 = (i % 4) / 4, v1 = 1 - Math.floor(i / 4) * 0.5; return [u0, v1 - 0.5, u0 + 0.25, v1]; };
const shopUV = (i) => { const v1 = 1 - i / 8; return [0, v1 - 1 / 8, 1, v1]; };
const adUV = (i) => { const u0 = (i % 2) * 0.5, v1 = 1 - Math.floor(i / 2) * 0.5; return [u0, v1 - 0.5, u0 + 0.5, v1]; };
const SIGN = { PEDESTRIAN: 0, SPEED60: 1, BUS: 2, DPS: 3, AZS: 4, PARKING: 5, CAMERA: 6, DOSAAF: 7 };

/** Вертикальный щит: центр, нормаль (nx,nz), размеры, UV-прямоугольник. */
function panel(b, cx, cy, cz, nx, nz, w, h, uv) {
  const rx = nz, rz = -nx; // «вправо», если смотреть на лицевую сторону
  const hw = w / 2, hh = h / 2;
  const A = [cx - rx * hw, cy - hh, cz - rz * hw];
  const B = [cx + rx * hw, cy - hh, cz + rz * hw];
  const C = [cx + rx * hw, cy + hh, cz + rz * hw];
  const D = [cx - rx * hw, cy + hh, cz - rz * hw];
  b.quad(A, B, C, D, nx, 0, nz, [uv[0], uv[1], uv[2], uv[1], uv[2], uv[3], uv[0], uv[3]]);
}

/**
 * Город «Автозаводский район»: дороги и разметка, кварталы (панельки, башни, гаражи,
 * парк, АЗС), припаркованные машины во дворах, вывески, остановки, знаки, камеры «Стрелка»,
 * посты ДПС, автодром ДОСААФ за городом. Здания квартала = 1 чанк (1 draw call)
 * с distance + occlusion culling.
 */
export class City {
  constructor(scene, T, quality, collision) {
    this.scene = scene;
    this.T = T;
    this.q = quality;
    this.col = collision;
    this.rnd = mulberry32(2107);
    this.noise = makeNoise2D(7);
    this.chunks = [];
    this.occluders = [];
    this.treeSpots = [];
    this.buildingRects = [];
    this.parked = [];
    this.lampPositions = [];
    this.cameras = [];
    this.dpsPosts = [];
    this.busStops = [];
    this.azs = null;
    this.group = new THREE.Group();
    this.group.name = 'World';
    scene.add(this.group);
    this._frame = 0;
    this.visibleChunks = 0;

    this.propParts = [];
    this.signs = new QuadBatch();
    this.shopSigns = new QuadBatch();
    this.ads = new QuadBatch();
    this.colored = new QuadBatch(true);
    this.markings = new QuadBatch();

    this._createMaterials(T);
    this._buildGround();
    this._buildMarkings();
    this._buildBlocks();
    this._buildFence();
    this._buildStreetLights();
    this._buildStreetFurniture();
    this._buildAutodrome();
    this._buildOuterTrees();
    this._finalize();
  }

  _createMaterials(T) {
    const high = this.q.name === 'high';
    this.mat = {
      asphalt: high
        ? new THREE.MeshStandardMaterial({ map: T.asphalt, normalMap: T.asphaltNormal, normalScale: new THREE.Vector2(0.45, 0.45), roughness: 0.93, metalness: 0, envMapIntensity: 0.12 })
        : new THREE.MeshLambertMaterial({ map: T.asphalt }),
      terrain: new THREE.MeshLambertMaterial({ map: T.grass, vertexColors: true }),
      lawn: new THREE.MeshLambertMaterial({ map: T.grass }),
      concrete: new THREE.MeshLambertMaterial({ map: T.concrete }),
      fence: new THREE.MeshLambertMaterial({ map: T.fence }),
      marking: new THREE.MeshLambertMaterial({ color: 0xe6e6e6, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }),
      building: new THREE.MeshLambertMaterial({
        map: T.facade, emissiveMap: T.facadeEmissive, emissive: 0xffffff, emissiveIntensity: 0, vertexColors: true,
      }),
      colored: new THREE.MeshLambertMaterial({ vertexColors: true }),
      props: new THREE.MeshLambertMaterial({ color: 0x5b5f63 }),
      lampHead: new THREE.MeshBasicMaterial({ color: 0x3a3a3a }),
      signs: new THREE.MeshLambertMaterial({ map: T.signs, alphaTest: 0.5, side: THREE.DoubleSide }),
      shops: new THREE.MeshLambertMaterial({ map: T.shops, emissiveMap: T.shops, emissive: 0xffffff, emissiveIntensity: 0 }),
      ads: new THREE.MeshLambertMaterial({ map: T.ads, emissiveMap: T.ads, emissive: 0xffffff, emissiveIntensity: 0 }),
      pool: new THREE.MeshBasicMaterial({
        map: T.glow, color: 0xffc27a, transparent: true, opacity: 0, depthWrite: false,
        blending: THREE.AdditiveBlending, toneMapped: false,
        polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4,
      }),
    };
  }

  _mesh(geo, mat, name, { cast = false, receive = false } = {}) {
    const m = new THREE.Mesh(geo, mat);
    m.name = name;
    m.castShadow = cast && this.q.shadows;
    m.receiveShadow = receive && this.q.shadows;
    m.matrixAutoUpdate = false;
    m.updateMatrix();
    this.group.add(m);
    return m;
  }

  // ================================================================ земля
  _flatDist(x, z) {
    // расстояние до ровных зон (город, коридор, автодром)
    const dCity = Math.max(Math.abs(x), Math.abs(z)) - (E + 6);
    const dx = Math.max(AUTODROME.x0 - 10 - x, 0, x - (E + 6));
    const dz = Math.max(AUTODROME.z0 - 10 - z, 0, z - (AUTODROME.z1 + 10));
    const dA = Math.hypot(dx, dz);
    const dC = Math.max(Math.abs(z) - (CORRIDOR.half + 8), 0) + Math.max(AUTODROME.x1 - x, 0, x - E);
    return Math.min(Math.max(dCity, 0), dA, dC);
  }

  heightAt(x, z) {
    const d = this._flatDist(x, z);
    if (d <= 0) return -0.05;
    const ramp = smoothstep(0, 170, d);
    const n = this.noise.fbm(x / 260 + 10, z / 260 + 10, 4);
    return Math.max(-3, -0.05 + ramp * ((n - 0.4) * 75 + 6));
  }

  _buildGround() {
    const asphalt = new THREE.PlaneGeometry(E * 2, E * 2).rotateX(-Math.PI / 2);
    scaleUV(asphalt, (E * 2) / 8);
    const corridor = new THREE.PlaneGeometry(CORRIDOR.x1 - CORRIDOR.x0 + 2, CORRIDOR.half * 2).rotateX(-Math.PI / 2)
      .translate((CORRIDOR.x0 + CORRIDOR.x1) / 2, 0.001, 0);
    scaleUV(corridor, (CORRIDOR.x1 - CORRIDOR.x0) / 8, (CORRIDOR.half * 2) / 8);
    this._mesh(merge([prep(asphalt), prep(corridor)]), this.mat.asphalt, 'Asphalt', { receive: true });

    const size = 2600, seg = 130;
    const g = new THREE.PlaneGeometry(size, size, seg, seg).rotateX(-Math.PI / 2);
    const pos = g.attributes.position;
    const colors = new Float32Array(pos.count * 3);
    const green = new THREE.Color(0x9fc27a), dry = new THREE.Color(0xc8bb7a), c = new THREE.Color();
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i) - 150, z = pos.getZ(i);
      pos.setX(i, x);
      pos.setY(i, this.heightAt(x, z));
      c.copy(green).lerp(dry, clamp(this.noise(x / 90, z / 90) * 1.4 - 0.3, 0, 1));
      colors[i * 3] = c.r; colors[i * 3 + 1] = c.g; colors[i * 3 + 2] = c.b;
    }
    g.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    g.computeVertexNormals();
    scaleUV(g, size / 8);
    this._mesh(g, this.mat.terrain, 'Terrain', { receive: true });
  }

  // ================================================================ разметка
  _buildMarkings() {
    const b = this.markings;
    const y = 0.012;
    for (let j = 0; j < N; j++) {
      for (let i = 0; i < N; i++) {
        for (const [ni, nj] of [[i + 1, j], [i, j + 1]]) {
          if (ni >= N || nj >= N) continue;
          const ax = coord(i), az = coord(j);
          const dx = ni > i ? 1 : 0, dz = nj > j ? 1 : 0;
          const rx = -dz, rz = dx;
          const P = (u, lat) => [ax + dx * (HALF + u) + rx * lat, az + dz * (HALF + u) + rz * lat];
          const line = (u0, u1, lat, w) => { const [cx, cz] = P((u0 + u1) / 2, lat); b.flat(cx, cz, dx, dz, u1 - u0, w, y); };
          const across = (u, lat0, lat1, w) => { const [cx, cz] = P(u, (lat0 + lat1) / 2); b.flat(cx, cz, rx, rz, Math.abs(lat1 - lat0), w, y); };
          const u0 = 4.5, u1 = SEG_LEN - 4.5;
          line(u0, u1, 0.15, 0.12); line(u0, u1, -0.15, 0.12);
          for (const side of [1, -1]) {
            const lat = side * CITY.LANE_W;
            const endSolid = side > 0;
            line(endSolid ? u1 - 20 : u0, endSolid ? u1 : u0 + 20, lat, 0.12);
            const ds = endSolid ? u0 : u0 + 20, de = endSolid ? u1 - 20 : u1;
            for (let u = ds + 1; u + 3 <= de; u += 9) line(u, u + 3, lat, 0.12);
          }
          across(u1, 0, HALF - 0.3, 0.4);
          across(u0, -(HALF - 0.3), 0, 0.4);
          for (const uc of [2, SEG_LEN - 2]) {
            for (let lat = -HALF + 0.75; lat < HALF - 0.5; lat += 1.0) { const [cx, cz] = P(uc, lat); b.flat(cx, cz, dx, dz, 3, 0.5, y); }
          }
        }
      }
    }
    // коридор к автодрому
    for (let x = CORRIDOR.x0 + 2; x < CORRIDOR.x1 - 4; x += 9) b.flat(x + 1.5, 0, 1, 0, 3, 0.12, y);
  }

  // ================================================================ кварталы
  _buildBlocks() {
    const sidewalks = [], lawns = [];
    const types = ['panel', 'panel', 'panel', 'tower', 'tower', 'garages', 'park'];
    for (let bj = 0; bj < N - 1; bj++) {
      for (let bi = 0; bi < N - 1; bi++) {
        const x0 = coord(bi) + HALF, x1 = coord(bi + 1) - HALF;
        const z0 = coord(bj) + HALF, z1 = coord(bj + 1) - HALF;
        const w = x1 - x0, d = z1 - z0;
        sidewalks.push(prep(scaleUV(new THREE.BoxGeometry(w, CURB, d).translate((x0 + x1) / 2, CURB / 2, (z0 + z1) / 2), w / 2, d / 2)));
        const lw = w - SIDEWALK * 2, ld = d - SIDEWALK * 2;
        lawns.push(prep(scaleUV(new THREE.PlaneGeometry(lw, ld).rotateX(-Math.PI / 2).translate((x0 + x1) / 2, CURB + 0.03, (z0 + z1) / 2), lw / 8, ld / 8)));
        let type = types[(this.rnd() * types.length) | 0];
        if (bi === 2 && bj === 2) type = 'azs';
        if (bi === 3 && bj === 1) type = 'garages';
        this._buildBlockContent(type, x0 + SIDEWALK, z0 + SIDEWALK, x1 - SIDEWALK, z1 - SIDEWALK);
      }
    }
    const inner = coord(N - 1) + HALF, ring = E - inner;
    for (const s of [1, -1]) {
      sidewalks.push(prep(scaleUV(new THREE.BoxGeometry(E * 2, CURB, ring).translate(0, CURB / 2, s * (inner + ring / 2)), E, ring / 2)));
      sidewalks.push(prep(scaleUV(new THREE.BoxGeometry(ring, CURB, E * 2).translate(s * (inner + ring / 2), CURB / 2, 0), ring / 2, E)));
    }
    this._mesh(merge(sidewalks), this.mat.concrete, 'Sidewalks', { receive: true });
    this._mesh(merge(lawns), this.mat.lawn, 'Lawns', { receive: true });
  }

  _addParked(x, z, heading, keys) {
    const rnd = this.rnd;
    const pick = keys ? keys[(rnd() * keys.length) | 0] : null;
    let def;
    if (pick) def = CARS.find((c) => c.id === pick);
    else {
      const tot = CARS.reduce((a, c) => a + c.traffic, 0);
      let r = rnd() * tot;
      def = CARS.find((c) => (r -= c.traffic) <= 0) || CARS[0];
    }
    const color = def.colors[(rnd() * def.colors.length) | 0];
    const d = def.dims;
    const along = Math.abs(Math.cos(heading)) > 0.5;
    const cz = (d.front + d.rear) / 2;
    const cx = x + Math.sin(heading) * cz, czz = z + Math.cos(heading) * cz;
    const hl = (d.front - d.rear) / 2, hw = d.W / 2;
    const [ex, ez] = along ? [hw, hl] : [hl, hw];
    this.col.addBox(cx - ex, czz - ez, cx + ex, czz + ez, 'parked');
    this.parked.push({ key: def.id, x, z, y: CURB, heading, color: new THREE.Color(color) });
    return [cx - ex - 0.5, czz - ez - 0.5, cx + ex + 0.5, czz + ez + 0.5];
  }

  _buildBlockContent(type, lx0, lz0, lx1, lz1) {
    const rnd = this.rnd;
    const cx = (lx0 + lx1) / 2, cz = (lz0 + lz1) / 2;
    const batch = new QuadBatch(true);
    const chunk = { idx: this.chunks.length, cx, cz, mesh: null, maxH: 0, minX: Infinity, minZ: Infinity, maxX: -Infinity, maxZ: -Infinity };
    const placed = [];
    const alongX = rnd() < 0.5;
    const P = (u, v) => (alongX ? [cx + u, cz + v] : [cx + v, cz + u]);
    // нормаль «в сторону v»
    const Nv = (s) => (alongX ? [0, s] : [s, 0]);

    const add = (bx, bz, w, d, floors, opts = {}) => {
      const h = opts.height ?? floors * 3;
      const minX = bx - w / 2, maxX = bx + w / 2, minZ = bz - d / 2, maxZ = bz + d / 2;
      const color = new THREE.Color(opts.color ?? BUILDING_COLORS[(rnd() * BUILDING_COLORS.length) | 0]);
      addBuilding(batch, minX, minZ, maxX, maxZ, CURB, h, color, opts.flat, rnd, opts);
      this.col.addBox(minX, minZ, maxX, maxZ, 'building');
      if (h >= 12) this.occluders.push({ minX, minZ, maxX, maxZ, h, chunk: chunk.idx });
      this.buildingRects.push([minX, minZ, maxX, maxZ]);
      placed.push([minX - 2, minZ - 2, maxX + 2, maxZ + 2]);
      chunk.maxH = Math.max(chunk.maxH, h);
      chunk.minX = Math.min(chunk.minX, minX); chunk.maxX = Math.max(chunk.maxX, maxX);
      chunk.minZ = Math.min(chunk.minZ, minZ); chunk.maxZ = Math.max(chunk.maxZ, maxZ);
      return { minX, maxX, minZ, maxZ, h };
    };
    const free = (x, z) => placed.every(([a, b, c, d]) => x < a || x > c || z < b || z > d);
    const tree = (x, z, kind = rnd() < 0.5 ? 0 : 1) => {
      if (!free(x, z) || rnd() > this.q.treeDensity) return;
      this.treeSpots.push({ x, z, y: CURB, s: 0.8 + rnd() * 0.5, kind });
      this.col.addCircle(x, z, 0.3, 'tree');
    };
    const parkRow = (u0, u1, v, facing, keys) => {
      // ряд машин перпендикулярно оси u (носом в сторону facing по v)
      if (rnd() > this.q.parkedDensity) return;
      for (let u = u0; u <= u1; u += 2.8) {
        if (rnd() < 0.25) continue;
        const [x, z] = P(u + (rnd() - 0.5) * 0.3, v);
        const heading = alongX ? (facing > 0 ? 0 : Math.PI) : (facing > 0 ? Math.PI / 2 : -Math.PI / 2);
        placed.push(this._addParked(x, z, heading + (rnd() - 0.5) * 0.06, keys));
      }
    };
    const shopSign = (b) => {
      // вывеска на первом этаже со стороны улицы
      const i = (rnd() * 8) | 0;
      const [nx, nz] = Nv(Math.sign((alongX ? b.minZ + b.maxZ - 2 * cz : b.minX + b.maxX - 2 * cx)) || 1);
      const fx = nx ? (nx > 0 ? b.maxX : b.minX) + nx * 0.06 : (b.minX + b.maxX) / 2 + (rnd() - 0.5) * 12;
      const fz = nz ? (nz > 0 ? b.maxZ : b.minZ) + nz * 0.06 : (b.minZ + b.maxZ) / 2 + (rnd() - 0.5) * 12;
      panel(this.shopSigns, fx, CURB + 3.4, fz, nx, nz, 7, 0.9, shopUV(i));
    };

    if (type === 'panel') {
      const floors = rnd() < 0.6 ? 5 : 9;
      for (const v of [-24, 0, 24]) {
        if (v === 0 && rnd() < 0.3) continue;
        const [x, z] = P((rnd() - 0.5) * 6, v);
        const len = 48 + ((rnd() * 3) | 0) * 6;
        const front = v === 0 ? (rnd() < 0.5 ? 1 : -1) : -Math.sign(v);
        const b = add(x, z, alongX ? len : 12, alongX ? 12 : len, floors, { entrances: Nv(front) });
        if (v !== 0 && rnd() < 0.7) shopSign(b);
      }
      parkRow(-26, -4, -14.5 + 2.6, 1);
      parkRow(4, 26, 14.5 - 2.6, -1);
      for (const v of [-12, 12]) for (let u = -30; u <= 30; u += 8) tree(...P(u + (rnd() - 0.5) * 3, v + (rnd() - 0.5) * 3));
    } else if (type === 'tower') {
      add(...P(-16, -16), 18, 18, 12 + ((rnd() * 5) | 0), { entrances: Nv(1) });
      add(...P(16, 16), 18, 18, 12 + ((rnd() * 5) | 0), { entrances: Nv(-1) });
      parkRow(-30, -6, 10, -1);
      parkRow(-30, -6, 5, 1);
      for (let k = 0; k < 24; k++) tree(lx0 + 4 + rnd() * (lx1 - lx0 - 8), lz0 + 4 + rnd() * (lz1 - lz0 - 8));
    } else if (type === 'garages') {
      for (const v of [-28, -14]) {
        const [x, z] = P(0, v);
        add(x, z, alongX ? 60 : 6, alongX ? 6 : 60, 1, { height: 2.7, flat: true, garage: Nv(1), color: GARAGE_COLORS[(rnd() * GARAGE_COLORS.length) | 0] });
      }
      parkRow(-24, 24, -7.5, -1, ['vaz2101', 'vaz2106', 'vaz2107', 'vaz2109', 'niva', 'oka']);
      const [x, z] = P(0, 22);
      add(x, z, alongX ? 60 : 12, alongX ? 12 : 60, 5, { entrances: Nv(-1) });
      for (let u = -30; u <= 30; u += 7) tree(...P(u, 10 + rnd() * 2));
    } else if (type === 'azs') {
      this._buildAZS(lx0, lz0, lx1, lz1, batch, add, placed);
    } else {
      add(cx + 6, cz + 4, 4, 3, 1, { height: 3, flat: true, color: 0x4f7fb5 });
      for (let k = 0; k < 45; k++) tree(lx0 + 3 + rnd() * (lx1 - lx0 - 6), lz0 + 3 + rnd() * (lz1 - lz0 - 6));
    }

    if (!batch.empty) {
      chunk.mesh = this._mesh(batch.build(), this.mat.building, `Block_${chunk.idx}`, { cast: true, receive: this.q.name === 'high' });
      chunk.radius = Math.hypot(chunk.maxX - chunk.minX, chunk.maxZ - chunk.minZ) / 2;
      chunk.cx = (chunk.minX + chunk.maxX) / 2;
      chunk.cz = (chunk.minZ + chunk.maxZ) / 2;
      this.chunks.push(chunk);
    }
  }

  // ================================================================ АЗС
  _buildAZS(lx0, lz0, lx1, lz1, batch, add, placed) {
    const cx = (lx0 + lx1) / 2, cz = (lz0 + lz1) / 2;
    const pad = new THREE.PlaneGeometry(lx1 - lx0, lz1 - lz0).rotateX(-Math.PI / 2).translate(cx, CURB + 0.05, cz);
    this._azsPad = prep(scaleUV(pad, (lx1 - lx0) / 8, (lz1 - lz0) / 8));
    // навес над колонками (ближе к дороге по +x: восточная граница квартала — улица x=0)
    const kx = lx1 - 18, kz = cz;
    const white = new THREE.Color(0xf2f2f2), red = new THREE.Color(0xd32020), dark = new THREE.Color(0x3a3d40);
    const f = FLAT_UV;
    this.colored.box(kx - 8, CURB + 4.6, kz - 12, kx + 8, CURB + 5.4, kz + 12, white);
    this.colored.box(kx - 8.05, CURB + 4.5, kz - 12.05, kx + 8.05, CURB + 4.8, kz + 12.05, red);
    for (const sx of [-6, 6]) for (const sz of [-10, 10]) {
      this.colored.box(kx + sx - 0.3, CURB, kz + sz - 0.3, kx + sx + 0.3, CURB + 4.6, kz + sz + 0.3, white);
      this.col.addBox(kx + sx - 0.3, kz + sz - 0.3, kx + sx + 0.3, kz + sz + 0.3, 'pole');
    }
    this.azs = { x: kx, z: kz, zones: [], lights: [] };
    for (const pz of [-6, 0, 6]) {
      // островок с колонкой
      this.colored.box(kx - 0.8, CURB, kz + pz - 1.6, kx + 0.8, CURB + 0.2, kz + pz + 1.6, new THREE.Color(0x9a9a9a));
      this.colored.box(kx - 0.4, CURB + 0.2, kz + pz - 0.6, kx + 0.4, CURB + 2.0, kz + pz + 0.6, red);
      this.colored.box(kx - 0.42, CURB + 1.2, kz + pz - 0.5, kx + 0.42, CURB + 1.7, kz + pz + 0.5, dark);
      this.col.addBox(kx - 0.8, kz + pz - 1.6, kx + 0.8, kz + pz + 1.6, 'pump');
      for (const side of [-1, 1]) this.azs.zones.push({ x: kx + side * 3.2, z: kz + pz, w: 3.2, d: 5.5 });
    }
    for (let k = -1; k <= 1; k++) this.azs.lights.push([kx, CURB + 4.45, kz + k * 8]);
    // магазин АЗС
    add(lx0 + 16, cz, 14, 9, 1, { height: 4, flat: true, color: 0xf0f0f0 });
    panel(this.shopSigns, lx0 + 23.06, CURB + 3.4, cz, 1, 0, 8, 0.9, shopUV(6));
    // стела
    this.propParts.push(prep(new THREE.BoxGeometry(0.4, 6, 0.4).translate(lx1 - 3, CURB + 3, lz0 + 6)));
    this.propParts.push(prep(new THREE.BoxGeometry(0.2, 2.2, 2.2).translate(lx1 - 3, CURB + 6.6, lz0 + 6)));
    panel(this.signs, lx1 - 2.88, CURB + 6.6, lz0 + 6, 1, 0, 2.1, 2.1, signUV(SIGN.AZS));
    this.col.addCircle(lx1 - 3, lz0 + 6, 0.3, 'pole');
    this.azs.rect = [lx0, lz0, lx1, lz1];
    placed.push([kx - 9, kz - 13, kx + 9, kz + 13]);
    void f; void batch;
  }

  // ================================================================ забор ПО-2
  _buildFence() {
    const F = E + 0.3, H = 2.4, T = 0.2, g = AUTODROME.gateHalf;
    const parts = [];
    const wall = (x0, z0, x1, z1) => {
      const len = Math.max(x1 - x0, z1 - z0);
      parts.push(prep(scaleUV(new THREE.BoxGeometry(Math.max(x1 - x0, T), H, Math.max(z1 - z0, T)).translate((x0 + x1) / 2, H / 2, (z0 + z1) / 2), len / 4, 1)));
      this.col.addBox(Math.min(x0, x1) - 0.3, Math.min(z0, z1) - 0.3, Math.max(x0, x1) + 0.3, Math.max(z0, z1) + 0.3, 'fence');
    };
    wall(-F, F, F, F); wall(-F, -F, F, -F); wall(F, -F, F, F);
    wall(-F, -F, -F, -g - 1); wall(-F, g + 1, -F, F);             // западные ворота
    wall(CORRIDOR.x0, g + 1, CORRIDOR.x1, g + 1);                // стенки коридора
    wall(CORRIDOR.x0, -g - 1, CORRIDOR.x1, -g - 1);
    const A = AUTODROME;
    wall(A.x0, A.z0, A.x1, A.z0); wall(A.x0, A.z1, A.x1, A.z1); wall(A.x0, A.z0, A.x0, A.z1);
    wall(A.x1, A.z0, A.x1, -g - 1); wall(A.x1, g + 1, A.x1, A.z1);
    this._mesh(merge(parts), this.mat.fence, 'Fence', { cast: true, receive: true });
  }

  // ================================================================ фонари
  _buildStreetLights() {
    const poles = [], heads = [];
    const pools = new QuadBatch();
    const poleBase = new THREE.CylinderGeometry(0.08, 0.13, 8, 6, 1, true);
    const withPools = this.q.streetLightPools;
    const addLamp = (px, pz, hx, hz, armDx, armDz, py = CURB) => {
      poles.push(prep(poleBase.clone().translate(px, 4 + py, pz)));
      const armLen = Math.hypot(hx - px, hz - pz);
      const arm = new THREE.BoxGeometry(Math.abs(armDx) * armLen + 0.08, 0.08, Math.abs(armDz) * armLen + 0.08);
      poles.push(prep(arm.translate((px + hx) / 2, 7.95 + py, (pz + hz) / 2)));
      heads.push(prep(new THREE.BoxGeometry(Math.abs(armDx) * 0.7 + 0.3, 0.14, Math.abs(armDz) * 0.7 + 0.3).translate(hx, 7.85 + py, hz)));
      this.lampPositions.push([hx, 7.7 + py, hz]);
      if (withPools) pools.flat(hx + armDx * 0.8, hz + armDz * 0.8, 1, 0, 13, 13, 0.02);
      this.col.addCircle(px, pz, 0.18, 'pole');
    };
    for (let j = 0; j < N; j++) {
      for (let i = 0; i < N; i++) {
        for (const [ni, nj] of [[i + 1, j], [i, j + 1]]) {
          if (ni >= N || nj >= N) continue;
          const ax = coord(i), az = coord(j);
          const dx = ni > i ? 1 : 0, dz = nj > j ? 1 : 0;
          const rx = -dz, rz = dx;
          for (const u of [9, 41, 73]) {
            for (const side of [1, -1]) {
              const lat = side * (HALF + 0.9);
              const px = ax + dx * (HALF + u) + rx * lat, pz = az + dz * (HALF + u) + rz * lat;
              addLamp(px, pz, px - rx * side * 1.9, pz - rz * side * 1.9, -rx * side, -rz * side);
            }
          }
        }
      }
    }
    // коридор и автодром
    for (let x = CORRIDOR.x0 + 10; x < CORRIDOR.x1; x += 30) addLamp(x, CORRIDOR.half + 0.5, x, CORRIDOR.half - 1.4, 0, -1, 0);
    const A = AUTODROME;
    for (let x = A.x0 + 20; x < A.x1; x += 40) {
      addLamp(x, A.z1 - 1, x, A.z1 - 2.9, 0, -1, 0);
      addLamp(x, A.z0 + 1, x, A.z0 + 2.9, 0, 1, 0);
    }
    poleBase.dispose();
    this._mesh(merge(poles), this.mat.props, 'StreetLightPoles');
    this.lampHeads = this._mesh(merge(heads), this.mat.lampHead, 'StreetLampHeads');
    if (withPools) {
      this.lightPools = this._mesh(pools.build(), this.mat.pool, 'StreetLightPools');
      this.lightPools.renderOrder = 2;
      this.lightPools.visible = false;
    }
  }

  // ================================================================ остановки, знаки, камеры, ДПС, щиты
  _buildStreetFurniture() {
    const rnd = this.rnd;
    const blue = new THREE.Color(0x2f5fa8), glass = new THREE.Color(0x9fb8c8), dark = new THREE.Color(0x303438), wood = new THREE.Color(0x8a5a36);
    const signPole = (x, z, h, nx, nz, idx, size = 0.7) => {
      this.propParts.push(prep(new THREE.CylinderGeometry(0.04, 0.04, h, 5).translate(x, CURB + h / 2, z)));
      this.propParts.push(prep(new THREE.BoxGeometry(Math.abs(nz) * size + 0.03, size, Math.abs(nx) * size + 0.03).translate(x - nx * 0.03, CURB + h - size / 2, z - nz * 0.03)));
      panel(this.signs, x + nx * 0.005, CURB + h - size / 2, z + nz * 0.005, nx, nz, size, size, signUV(idx));
      this.col.addCircle(x, z, 0.1, 'pole');
    };
    let edgeIdx = 0;
    const dpsNodes = [[1, 3], [5, 3]];
    const dpsEdges = new Set(dpsNodes.map(([i, j]) => `${i},${j},${i},${j + 1}`));
    for (let j = 0; j < N; j++) {
      for (let i = 0; i < N; i++) {
        for (const [ni, nj] of [[i + 1, j], [i, j + 1]]) {
          if (ni >= N || nj >= N) continue;
          edgeIdx++;
          const ax = coord(i), az = coord(j);
          const dx = ni > i ? 1 : 0, dz = nj > j ? 1 : 0;
          const rx = -dz, rz = dx;
          const P = (u, lat) => [ax + dx * (HALF + u) + rx * lat, az + dz * (HALF + u) + rz * lat];
          // знаки «пешеходный переход» перед каждой зеброй (для обоих направлений)
          {
            const [x1, z1] = P(SEG_LEN - 4.2, HALF + 0.55);
            signPole(x1, z1, 2.6, -dx, -dz, SIGN.PEDESTRIAN);
            const [x2, z2] = P(4.2, -(HALF + 0.55));
            signPole(x2, z2, 2.6, dx, dz, SIGN.PEDESTRIAN);
          }
          if (edgeIdx % 3 === 0) {
            const [x, z] = P(14, HALF + 0.55);
            signPole(x, z, 2.6, -dx, -dz, SIGN.SPEED60);
          }
          const isDps = dpsEdges.has(`${i},${j},${ni},${nj}`);
          // остановки
          if (!isDps && edgeIdx % 4 === 1) {
            const side = rnd() < 0.5 ? 1 : -1;
            const [x, z] = P(55, side * (HALF + 2.0));
            const nx = -rx * side, nz = -rz * side; // лицом к дороге
            const L = 4.2, D = 1.5;
            const ux = dx, uz = dz;
            const x0 = x - ux * L / 2 - nx * D / 2, x1 = x + ux * L / 2 + nx * D / 2;
            const z0 = z - uz * L / 2 - nz * D / 2, z1 = z + uz * L / 2 + nz * D / 2;
            const bx0 = Math.min(x0, x1), bx1 = Math.max(x0, x1), bz0 = Math.min(z0, z1), bz1 = Math.max(z0, z1);
            this.colored.box(bx0, CURB + 2.5, bz0, bx1, CURB + 2.65, bz1, blue);
            // задняя стенка
            const bxb = x - nx * D / 2, bzb = z - nz * D / 2;
            this.colored.box(bxb - Math.abs(ux) * L / 2 - Math.abs(nx) * 0.04, CURB + 0.3, bzb - Math.abs(uz) * L / 2 - Math.abs(nz) * 0.04,
              bxb + Math.abs(ux) * L / 2 + Math.abs(nx) * 0.04, CURB + 2.5, bzb + Math.abs(uz) * L / 2 + Math.abs(nz) * 0.04, glass);
            for (const s of [-1, 1]) {
              const px = x + ux * s * (L / 2 - 0.1), pz = z + uz * s * (L / 2 - 0.1);
              this.colored.box(px - 0.06, CURB, pz - 0.06, px + 0.06, CURB + 2.5, pz + 0.06, dark);
            }
            const bx = x - nx * 0.35, bz = z - nz * 0.35;
            this.colored.box(bx - Math.abs(ux) * 1.6 - Math.abs(nx) * 0.2, CURB + 0.42, bz - Math.abs(uz) * 1.6 - Math.abs(nz) * 0.2,
              bx + Math.abs(ux) * 1.6 + Math.abs(nx) * 0.2, CURB + 0.5, bz + Math.abs(uz) * 1.6 + Math.abs(nz) * 0.2, wood);
            this.col.addBox(bx0, bz0, bx1, bz1, 'busstop');
            const [sx, sz] = P(55 + 2.8, side * (HALF + 0.6));
            signPole(sx, sz, 2.8, -dx * side, -dz * side, SIGN.BUS, 0.6);
            this.busStops.push({ x, z, nx, nz });
          }
          // камеры «Стрелка»
          if (!isDps && edgeIdx % 7 === 3) {
            const [x, z] = P(28, HALF + 0.6);
            this.propParts.push(prep(new THREE.CylinderGeometry(0.1, 0.12, 5.5, 6).translate(x, CURB + 2.75, z)));
            this.colored.box(x - 0.25, CURB + 5.2, z - 0.25, x + 0.25, CURB + 5.8, z + 0.25, new THREE.Color(0xdedede));
            this.colored.box(x - 0.2 - dx * 0.3, CURB + 5.3, z - 0.2 - dz * 0.3, x + 0.2 - dx * 0.3, CURB + 5.7, z + 0.2 - dz * 0.3, dark);
            this.col.addCircle(x, z, 0.15, 'pole');
            const [sx, sz] = P(8, HALF + 0.55);
            signPole(sx, sz, 2.6, -dx, -dz, SIGN.CAMERA, 0.6);
            this.cameras.push({ x, z, dx, dz, lens: [x - dx * 0.5, CURB + 5.5, z - dz * 0.5] });
          }
        }
      }
    }
    // посты ДПС: будка + машина на тротуаре (с мигалками)
    for (const [i, j] of dpsNodes) {
      const nx = coord(i), nz = coord(j);
      const bx = nx + HALF + 1.5, bz = nz + 12.5;
      this.colored.box(bx - 1.2, CURB, bz - 1.2, bx + 1.2, CURB + 2.7, bz + 1.2, new THREE.Color(0xe8e8e8));
      this.colored.box(bx - 1.25, CURB + 1.3, bz - 1.25, bx + 1.25, CURB + 1.9, bz + 1.25, new THREE.Color(0x4a6a8a));
      this.colored.box(bx - 1.4, CURB + 2.7, bz - 1.4, bx + 1.4, CURB + 2.9, bz + 1.4, new THREE.Color(0x1b3c9e));
      panel(this.signs, bx - 1.21, CURB + 2.2, bz, -1, 0, 1.6, 1.6, signUV(SIGN.DPS));
      this.col.addBox(bx - 1.2, bz - 1.2, bx + 1.2, bz + 1.2, 'booth');
      const px = nx + HALF + 1.6, pz = nz + 24;
      this.col.addBox(px - 0.85, pz - 2.2, px + 0.85, pz + 2.2, 'parked');
      this.dpsPosts.push({ x: px, z: pz, heading: 0, node: j * N + i });
    }
    // рекламные щиты у периметра (смотрят в город)
    const adSpots = [[-150, E - 4, 0, -1], [120, E - 4, 0, -1], [-120, -E + 4, 0, 1], [160, -E + 4, 0, 1], [E - 4, 60, -1, 0], [E - 4, -170, -1, 0]];
    adSpots.forEach(([x, z, nx, nz], k) => {
      for (const s of [-2.5, 2.5]) this.propParts.push(prep(new THREE.BoxGeometry(0.25, 6, 0.25).translate(x + nz * s, CURB + 3, z + nx * s)));
      this.propParts.push(prep(new THREE.BoxGeometry(Math.abs(nz) * 8 + 0.2, 4, Math.abs(nx) * 8 + 0.2).translate(x - nx * 0.15, CURB + 7, z - nz * 0.15)));
      panel(this.ads, x - nx * 0.04, CURB + 7, z - nz * 0.04, nx, nz, 7.8, 3.9, adUV(k % 4));
    });
  }

  // ================================================================ автодром ДОСААФ
  _buildAutodrome() {
    const A = AUTODROME;
    const w = A.x1 - A.x0, d = A.z1 - A.z0;
    const pad = new THREE.PlaneGeometry(w, d).rotateX(-Math.PI / 2).translate(A.cx, 0.002, A.cz);
    scaleUV(pad, w / 8, d / 8);
    this._mesh(merge([prep(pad), ...(this._azsPad ? [this._azsPad] : [])]), this.mat.asphalt, 'AutodromeAsphalt', { receive: true });
    const b = this.markings, y = 0.014;
    // периметр площадки
    b.flat(A.cx, A.z0 + 3, 1, 0, w - 6, 0.2, y); b.flat(A.cx, A.z1 - 3, 1, 0, w - 6, 0.2, y);
    b.flat(A.x0 + 3, A.cz, 0, 1, d - 6, 0.2, y); b.flat(A.x1 - 3, A.cz, 0, 1, d - 6, 0.2, y);
    // декоративные ряды парковочных мест вдоль северной стороны
    for (let x = A.x0 + 70; x < A.x1 - 20; x += 2.8) b.flat(x, A.z1 - 12, 0, 1, 5.5, 0.12, y);
    b.flat((A.x0 + 70 + A.x1 - 20) / 2, A.z1 - 14.75, 1, 0, A.x1 - 20 - A.x0 - 70, 0.12, y);
    // здание ДОСААФ
    const batch = new QuadBatch(true);
    addBuilding(batch, A.x0 + 8, A.z1 - 22, A.x0 + 50, A.z1 - 8, 0, 7.5, new THREE.Color(0xd8c9a8), false, this.rnd, { entrances: [0, -1] });
    this.col.addBox(A.x0 + 8, A.z1 - 22, A.x0 + 50, A.z1 - 8, 'building');
    this.buildingRects.push([A.x0 + 8, A.z1 - 22, A.x0 + 50, A.z1 - 8]);
    this._mesh(batch.build(), this.mat.building, 'DOSAAF', { cast: true });
    panel(this.signs, A.x0 + 29, 8.6, A.z1 - 22.05, 0, -1, 8, 4, signUV(SIGN.DOSAAF));
    this.propParts.push(prep(new THREE.BoxGeometry(8.2, 4.2, 0.1).translate(A.x0 + 29, 8.6, A.z1 - 21.98)));
    // трибуна-навес для инструкторов
    this.colored.box(A.x0 + 60, 0, A.z1 - 10, A.x0 + 76, 0.6, A.z1 - 6, new THREE.Color(0x7a7a7a));
    this.colored.box(A.x0 + 60, 3.2, A.z1 - 11, A.x0 + 76, 3.4, A.z1 - 5, new THREE.Color(0xb71c1c));
    for (const x of [A.x0 + 60.3, A.x0 + 75.7]) this.colored.box(x - 0.1, 0, A.z1 - 10.9, x + 0.1, 3.2, A.z1 - 10.7, new THREE.Color(0x444444));
    this.col.addBox(A.x0 + 60, A.z1 - 10, A.x0 + 76, A.z1 - 6, 'building');
    this.buildingRects.push([A.x0 + 60, A.z1 - 10, A.x0 + 76, A.z1 - 6]);
  }

  _buildOuterTrees() {
    const rnd = this.rnd;
    const count = Math.floor(1000 * this.q.treeDensity);
    for (let k = 0; k < count; k++) {
      const x = (rnd() - 0.5) * 1500 - 150, z = (rnd() - 0.5) * 1400;
      if (this._flatDist(x, z) < 12) continue;
      if (this.noise(x / 120, z / 120) < 0.45) continue;
      this.treeSpots.push({ x, z, y: this.heightAt(x, z), s: 0.9 + rnd() * 0.7, kind: rnd() < 0.55 ? 2 : 0 });
    }
  }

  _finalize() {
    this._mesh(this.markings.build(), this.mat.marking, 'Markings', { receive: true });
    if (!this.colored.empty) this._mesh(this.colored.build(), this.mat.colored, 'Props', { cast: true, receive: true });
    if (this.propParts.length) this._mesh(merge(this.propParts), this.mat.props, 'PropsMetal', { cast: false });
    if (!this.signs.empty) this._mesh(this.signs.build(), this.mat.signs, 'RoadSigns');
    if (!this.shopSigns.empty) this._mesh(this.shopSigns.build(), this.mat.shops, 'ShopSigns');
    if (!this.ads.empty) this._mesh(this.ads.build(), this.mat.ads, 'Billboards');
  }

  // ================================================================ запросы
  inAutodrome(x, z, margin = 0) {
    const A = AUTODROME;
    return x > A.x0 - margin && x < A.x1 + margin && z > A.z0 - margin && z < A.z1 + margin;
  }

  surface(x, z, out) {
    if (Math.abs(x) > E || Math.abs(z) > E) {
      if (this.inAutodrome(x, z) || (x >= CORRIDOR.x0 - 1 && x <= CORRIDOR.x1 + 2 && Math.abs(z) <= CORRIDOR.half + 1)) {
        out.y = 0; out.mu = 1.0; out.type = 3;
      } else { out.y = 0; out.mu = 0.62; out.type = 2; }
      return out;
    }
    const n = N - 1;
    const ix = clamp(Math.round((x - coord(0)) / SPACING), 0, n);
    const iz = clamp(Math.round((z - coord(0)) / SPACING), 0, n);
    const rx = Math.abs(x - coord(ix)), rz = Math.abs(z - coord(iz));
    if (rx <= HALF || rz <= HALF) { out.y = 0; out.mu = 1.0; out.type = 0; }
    else if (rx <= HALF + SIDEWALK || rz <= HALF + SIDEWALK) { out.y = CURB; out.mu = 0.95; out.type = 1; }
    else {
      out.y = CURB;
      const r = this.azs?.rect;
      if (r && x > r[0] && x < r[2] && z > r[1] && z < r[3]) { out.mu = 1.0; out.type = 1; }
      else { out.mu = 0.62; out.type = 2; }
    }
    return out;
  }

  update(dt, camera, night) {
    this.mat.building.emissiveIntensity = night * 1.15;
    this.mat.shops.emissiveIntensity = 0.1 + night * 0.9;
    this.mat.ads.emissiveIntensity = 0.05 + night * 0.8;
    this.mat.lampHead.color.setRGB(lerp(0.23, 1.0, night), lerp(0.23, 0.82, night), lerp(0.23, 0.55, night));
    if (this.lightPools) {
      this.mat.pool.opacity = night * 0.55;
      this.lightPools.visible = night > 0.02;
    }
    if ((this._frame++ & 7) !== 0) return;
    const cx = camera.position.x, cz = camera.position.z, cy = camera.position.y;
    const maxD = this.q.drawDistance;
    let vis = 0;
    for (const ch of this.chunks) {
      const d = Math.hypot(ch.cx - cx, ch.cz - cz) - ch.radius;
      let visible = d < maxD;
      if (visible && this.q.occlusion && cy < 25 && d > 5) visible = !this._occluded(ch, cx, cz);
      ch.mesh.visible = visible;
      if (visible) vis++;
    }
    this.visibleChunks = vis;
  }

  _occluded(ch, cx, cz) {
    const xs = [ch.minX, (ch.minX + ch.maxX) / 2, ch.maxX];
    const zs = [ch.minZ, (ch.minZ + ch.maxZ) / 2, ch.maxZ];
    for (const px of xs) {
      for (const pz of zs) {
        const dx = px - cx, dz = pz - cz;
        let blocked = false;
        for (const o of this.occluders) {
          if (o.chunk === ch.idx || o.h < ch.maxH) continue;
          if (segAabb(cx, cz, dx, dz, o.minX, o.minZ, o.maxX, o.maxZ) <= 1) { blocked = true; break; }
        }
        if (!blocked) return false;
      }
    }
    return true;
  }
}

/**
 * Здание: стены с «AO-юбкой» (затемнение у земли вертексным цветом), фасадные UV,
 * парапет, машинное помещение лифта, подъезды с козырьками, ворота гаражей.
 */
function addBuilding(b, x0, z0, x1, z1, y0, h, color, flat, rnd, opts = {}) {
  const y1 = y0 + h;
  const roof = color.clone().multiplyScalar(0.45);
  const ao = color.clone().multiplyScalar(0.5);
  const off = ((rnd() * 8) | 0) / 8;
  const F = FLAT_UV;
  const flatUV = [F[0], F[1], F[0], F[1], F[0], F[1], F[0], F[1]];
  const skirt = Math.min(2.4, h * 0.5);
  const wall = (ax, az, ux, uz, len, nx, nz) => {
    const bx = ax + ux * len, bz = az + uz * len;
    const u1 = len / FACADE_TILE + off;
    const vS = skirt / FACADE_TILE, vT = h / FACADE_TILE;
    const ys = y0 + skirt;
    b.quad([ax, y0, az], [bx, y0, bz], [bx, ys, bz], [ax, ys, az], nx, 0, nz,
      flat ? flatUV : [off, 0, u1, 0, u1, vS, off, vS], [ao, ao, color, color]);
    b.quad([ax, ys, az], [bx, ys, bz], [bx, y1, bz], [ax, y1, az], nx, 0, nz,
      flat ? flatUV : [off, vS, u1, vS, u1, vT, off, vT], color);
  };
  const w = x1 - x0, d = z1 - z0;
  wall(x0, z1, 1, 0, w, 0, 1);
  wall(x1, z0, -1, 0, w, 0, -1);
  wall(x1, z1, 0, -1, d, 1, 0);
  wall(x0, z0, 0, 1, d, -1, 0);
  b.quad([x0, y1, z1], [x1, y1, z1], [x1, y1, z0], [x0, y1, z0], 0, 1, 0, flatUV, roof);
  if (h >= 6) {
    const p = 0.25, ph = 0.6, pc = color.clone().multiplyScalar(0.8);
    b.box(x0, y1, z0, x1, y1 + ph, z0 + p, pc, F);
    b.box(x0, y1, z1 - p, x1, y1 + ph, z1, pc, F);
    b.box(x0, y1, z0, x0 + p, y1 + ph, z1, pc, F);
    b.box(x1 - p, y1, z0, x1, y1 + ph, z1, pc, F);
  }
  if (h >= 14) {
    const mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
    b.box(mx - 2, y1, mz - 1.5, mx + 2, y1 + 2.8, mz + 1.5, color.clone().multiplyScalar(0.7), F);
    b.box(mx + 3, y1, mz - 0.1, mx + 3.1, y1 + 4, mz + 0.1, new THREE.Color(0x333333), F); // антенна
  }
  // подъезды: дверь + козырёк каждые 12–14 м по фасаду
  if (opts.entrances) {
    const [nx, nz] = opts.entrances;
    const len = nx ? d : w;
    const count = Math.max(1, Math.floor(len / 13));
    const doorC = new THREE.Color(0x3b2a1e), canopy = new THREE.Color(0x8a8a8a);
    for (let k = 0; k < count; k++) {
      const t = (k + 0.5) / count;
      const px = nx ? (nx > 0 ? x1 : x0) : x0 + w * t;
      const pz = nz ? (nz > 0 ? z1 : z0) : z0 + d * t;
      const ux = nx ? 0 : 1, uz = nx ? 1 : 0;
      doorQuad(b, px + nx * 0.03, pz + nz * 0.03, nx, nz, 1.5, y0, 2.2, flatUV, doorC);
      const cx0 = Math.min(px - ux * 1.1, px + nx * 1.4), cx1 = Math.max(px + ux * 1.1, px + nx * 1.4);
      const cz0 = Math.min(pz - uz * 1.1, pz + nz * 1.4), cz1 = Math.max(pz + uz * 1.1, pz + nz * 1.4);
      b.box(nx ? cx0 : px - 1.1, y0 + 2.5, nz ? cz0 : pz - 1.1, nx ? cx1 : px + 1.1, y0 + 2.65, nz ? cz1 : pz + 1.1, canopy, F);
    }
  }
  // ворота гаражей
  if (opts.garage) {
    const [nx, nz] = opts.garage;
    const len = nx ? d : w;
    const gc = [new THREE.Color(0x5a6a5a), new THREE.Color(0x6a4a3a), new THREE.Color(0x4a5a6a), new THREE.Color(0x707070)];
    for (let k = 0; k < Math.floor(len / 3.2); k++) {
      const t = (k + 0.5) * 3.2;
      const px = nx ? (nx > 0 ? x1 : x0) + nx * 0.03 : x0 + t, pz = nz ? (nz > 0 ? z1 : z0) + nz * 0.03 : z0 + t;
      doorQuad(b, px, pz, nx, nz, 2.6, y0, 2.2, flatUV, gc[(rnd() * gc.length) | 0]);
    }
  }
}

/** Прямоугольник на фасаде с нормалью (nx,nz): «вправо» при взгляде снаружи = (nz, −nx). */
function doorQuad(b, cx, cz, nx, nz, w, y0, h, uv, col) {
  const rx = nz, rz = -nx, hw = w / 2;
  const A = [cx - rx * hw, y0, cz - rz * hw], B = [cx + rx * hw, y0, cz + rz * hw];
  b.quad(A, B, [B[0], y0 + h, B[2]], [A[0], y0 + h, A[2]], nx, 0, nz, uv, col);
}
