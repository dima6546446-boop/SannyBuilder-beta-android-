import * as THREE from 'three';
import { CITY, coord, CITY_EXTENT } from './RoadGraph.js';
import { QuadBatch, prep, merge, scaleUV } from '../utils/geometry.js';
import { mulberry32, makeNoise2D, smoothstep, clamp, lerp } from '../utils/math.js';
import { segAabb } from '../core/CollisionWorld.js';

const { N, SPACING, HALF, SIDEWALK, CURB } = CITY;
const SEG_LEN = SPACING - 2 * HALF; // длина участка дороги между перекрёстками (82 м)
const FACADE_TILE = 24;             // фасадная текстура покрывает 24×24 м
const FLAT_UV = [6 / 512, 1 - 8 / 512]; // точка «глухой панели» на атласе фасада (без окон)

const BUILDING_COLORS = [0xd9d3c5, 0xcfcfcf, 0xc4b59a, 0xb9785a, 0xe3dbc0, 0xaebfcc, 0xd7c6a8];
const GARAGE_COLORS = [0x7d7f80, 0x6f6152, 0x8a7a66, 0x5d6a72, 0x94918a];

/**
 * Город: асфальт, разметка, кварталы (панельки, башни, гаражи, парки),
 * фонари, забор ПО-2, рельеф за городом.
 * Каждый квартал = один «чанк» (одна слитая геометрия зданий = 1 draw call),
 * к чанкам применяется distance culling и программный occlusion culling.
 */
export class City {
  constructor(scene, T, quality, collision) {
    this.scene = scene;
    this.q = quality;
    this.col = collision;
    this.rnd = mulberry32(2107);
    this.noise = makeNoise2D(7);
    this.chunks = [];
    this.occluders = [];
    this.treeSpots = [];
    this.buildingRects = [];
    this.group = new THREE.Group();
    this.group.name = 'World';
    scene.add(this.group);
    this._frame = 0;
    this.visibleChunks = 0;

    this._createMaterials(T);
    this._buildGround();
    this._buildMarkings();
    this._buildBlocks();
    this._buildFence();
    this._buildStreetLights();
    this._buildOuterTrees();
  }

  _createMaterials(T) {
    const shadowRecv = this.q.shadows;
    this.mat = {
      asphalt: new THREE.MeshLambertMaterial({ map: T.asphalt }),
      terrain: new THREE.MeshLambertMaterial({ map: T.grass, vertexColors: true }),
      lawn: new THREE.MeshLambertMaterial({ map: T.grass }),
      concrete: new THREE.MeshLambertMaterial({ map: T.concrete }),
      fence: new THREE.MeshLambertMaterial({ map: T.fence }),
      marking: new THREE.MeshLambertMaterial({
        color: 0xe6e6e6, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2,
      }),
      building: new THREE.MeshLambertMaterial({
        map: T.facade, emissiveMap: T.facadeEmissive, emissive: 0xffffff, emissiveIntensity: 0, vertexColors: true,
      }),
      props: new THREE.MeshLambertMaterial({ color: 0x5b5f63 }),
      lampHead: new THREE.MeshBasicMaterial({ color: 0x3a3a3a }),
      pool: new THREE.MeshBasicMaterial({
        map: T.glow, color: 0xffc27a, transparent: true, opacity: 0, depthWrite: false,
        blending: THREE.AdditiveBlending, toneMapped: false,
        polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4,
      }),
    };
    this.shadowRecv = shadowRecv;
  }

  _mesh(geo, mat, name, { cast = false, receive = false } = {}) {
    const m = new THREE.Mesh(geo, mat);
    m.name = name;
    m.castShadow = cast && this.q.shadows;
    m.receiveShadow = receive && this.q.shadows;
    m.matrixAutoUpdate = false; // статика: не пересчитываем матрицы каждый кадр
    m.updateMatrix();
    this.group.add(m);
    return m;
  }

  // ---------------------------------------------------------------- земля
  heightAt(x, z) {
    const d = Math.max(Math.abs(x), Math.abs(z)) - (CITY_EXTENT + 6);
    if (d <= 0) return -0.05;
    const ramp = smoothstep(0, 170, d);
    const n = this.noise.fbm(x / 260 + 10, z / 260 + 10, 4);
    return Math.max(-3, -0.05 + ramp * ((n - 0.4) * 75 + 6));
  }

  _buildGround() {
    const E = CITY_EXTENT;
    const asphalt = new THREE.PlaneGeometry(E * 2, E * 2).rotateX(-Math.PI / 2);
    scaleUV(asphalt, (E * 2) / 8);
    this._mesh(asphalt, this.mat.asphalt, 'Asphalt', { receive: true });

    const size = 2400, seg = 120;
    const g = new THREE.PlaneGeometry(size, size, seg, seg).rotateX(-Math.PI / 2);
    const pos = g.attributes.position;
    const colors = new Float32Array(pos.count * 3);
    const green = new THREE.Color(0x9fc27a), dry = new THREE.Color(0xc8bb7a), c = new THREE.Color();
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i), z = pos.getZ(i);
      pos.setY(i, this.heightAt(x, z));
      c.copy(green).lerp(dry, clamp(this.noise(x / 90, z / 90) * 1.4 - 0.3, 0, 1));
      colors[i * 3] = c.r; colors[i * 3 + 1] = c.g; colors[i * 3 + 2] = c.b;
    }
    g.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    g.computeVertexNormals();
    scaleUV(g, size / 8);
    this._mesh(g, this.mat.terrain, 'Terrain', { receive: true });
  }

  // ---------------------------------------------------------------- разметка
  _buildMarkings() {
    const b = new QuadBatch();
    const y = 0.012;
    const seen = new Set();
    for (let j = 0; j < N; j++) {
      for (let i = 0; i < N; i++) {
        for (const [ni, nj] of [[i + 1, j], [i, j + 1]]) {
          if (ni >= N || nj >= N) continue;
          const key = `${i},${j},${ni},${nj}`;
          if (seen.has(key)) continue;
          seen.add(key);
          const ax = coord(i), az = coord(j);
          const dx = ni > i ? 1 : 0, dz = nj > j ? 1 : 0;
          const rx = -dz, rz = dx;
          // точка на участке: u — вдоль (0 … SEG_LEN), lat — поперёк (+ вправо)
          const P = (u, lat) => [ax + dx * (HALF + u) + rx * lat, az + dz * (HALF + u) + rz * lat];
          const line = (u0, u1, lat, w) => {
            const [cx, cz] = P((u0 + u1) / 2, lat);
            b.flat(cx, cz, dx, dz, u1 - u0, w, y);
          };
          const across = (u, lat0, lat1, w) => {
            const [cx, cz] = P(u, (lat0 + lat1) / 2);
            b.flat(cx, cz, rx, rz, Math.abs(lat1 - lat0), w, y);
          };
          const u0 = 4.5, u1 = SEG_LEN - 4.5;
          // двойная сплошная (1.3)
          line(u0, u1, 0.15, 0.12); line(u0, u1, -0.15, 0.12);
          // разделители полос: прерывистая, у стоп-линий — сплошная (1.1)
          for (const side of [1, -1]) {
            const lat = side * CITY.LANE_W;
            const solidFromEnd = side > 0; // для правой стороны перекрёсток впереди — в конце участка
            const sA = solidFromEnd ? u1 - 20 : u0;
            const sB = solidFromEnd ? u1 : u0 + 20;
            line(sA, sB, lat, 0.12);
            const dashStart = solidFromEnd ? u0 : u0 + 20;
            const dashEnd = solidFromEnd ? u1 - 20 : u1;
            for (let u = dashStart + 1; u + 3 <= dashEnd; u += 9) line(u, u + 3, lat, 0.12);
          }
          // стоп-линии (1.12)
          across(u1, 0, HALF - 0.3, 0.4);
          across(u0, -(HALF - 0.3), 0, 0.4);
          // «зебры» (1.14.1)
          for (const uc of [2, SEG_LEN - 2]) {
            for (let lat = -HALF + 0.75; lat < HALF - 0.5; lat += 1.0) {
              const [cx, cz] = P(uc, lat);
              b.flat(cx, cz, dx, dz, 3, 0.5, y);
            }
          }
        }
      }
    }
    this._mesh(b.build(), this.mat.marking, 'Markings', { receive: true });
  }

  // ---------------------------------------------------------------- кварталы
  _buildBlocks() {
    const sidewalks = [];
    const lawns = [];
    const types = ['panel', 'panel', 'panel', 'tower', 'tower', 'garages', 'park'];
    for (let bj = 0; bj < N - 1; bj++) {
      for (let bi = 0; bi < N - 1; bi++) {
        const x0 = coord(bi) + HALF, x1 = coord(bi + 1) - HALF;
        const z0 = coord(bj) + HALF, z1 = coord(bj + 1) - HALF;
        const w = x1 - x0, d = z1 - z0;
        const sw = new THREE.BoxGeometry(w, CURB, d).translate((x0 + x1) / 2, CURB / 2, (z0 + z1) / 2);
        sidewalks.push(prep(scaleUV(sw, w / 2, d / 2)));
        const lw = w - SIDEWALK * 2, ld = d - SIDEWALK * 2;
        const lawn = new THREE.PlaneGeometry(lw, ld).rotateX(-Math.PI / 2).translate((x0 + x1) / 2, CURB + 0.03, (z0 + z1) / 2);
        lawns.push(prep(scaleUV(lawn, lw / 8, ld / 8)));

        const type = types[(this.rnd() * types.length) | 0];
        this._buildBlockContent(type, x0 + SIDEWALK, z0 + SIDEWALK, x1 - SIDEWALK, z1 - SIDEWALK);
      }
    }
    // внешнее кольцо тротуара вдоль забора
    const E = CITY_EXTENT, inner = coord(N - 1) + HALF, ring = E - inner;
    for (const s of [1, -1]) {
      const a = new THREE.BoxGeometry(E * 2, CURB, ring).translate(0, CURB / 2, s * (inner + ring / 2));
      const b = new THREE.BoxGeometry(ring, CURB, E * 2).translate(s * (inner + ring / 2), CURB / 2, 0);
      sidewalks.push(prep(scaleUV(a, E, ring / 2)), prep(scaleUV(b, ring / 2, E)));
    }
    this._mesh(merge(sidewalks), this.mat.concrete, 'Sidewalks', { receive: true });
    this._mesh(merge(lawns), this.mat.lawn, 'Lawns', { receive: true });
  }

  _buildBlockContent(type, lx0, lz0, lx1, lz1) {
    const rnd = this.rnd;
    const cx = (lx0 + lx1) / 2, cz = (lz0 + lz1) / 2;
    const batch = new QuadBatch(true);
    const chunk = {
      idx: this.chunks.length, cx, cz, mesh: null, maxH: 0,
      minX: Infinity, minZ: Infinity, maxX: -Infinity, maxZ: -Infinity,
    };
    const placed = [];
    const add = (bx, bz, w, d, floors, opts = {}) => {
      const h = opts.height ?? floors * 3;
      const minX = bx - w / 2, maxX = bx + w / 2, minZ = bz - d / 2, maxZ = bz + d / 2;
      const color = new THREE.Color(opts.color ?? BUILDING_COLORS[(rnd() * BUILDING_COLORS.length) | 0]);
      addBuilding(batch, minX, minZ, maxX, maxZ, CURB, h, color, opts.flat, rnd);
      this.col.addBox(minX, minZ, maxX, maxZ, 'building');
      if (h >= 12) this.occluders.push({ minX, minZ, maxX, maxZ, h, chunk: chunk.idx });
      this.buildingRects.push([minX, minZ, maxX, maxZ]);
      placed.push([minX - 2, minZ - 2, maxX + 2, maxZ + 2]);
      chunk.maxH = Math.max(chunk.maxH, h);
      chunk.minX = Math.min(chunk.minX, minX); chunk.maxX = Math.max(chunk.maxX, maxX);
      chunk.minZ = Math.min(chunk.minZ, minZ); chunk.maxZ = Math.max(chunk.maxZ, maxZ);
    };
    const free = (x, z) => placed.every(([a, b, c, d]) => x < a || x > c || z < b || z > d);
    const tree = (x, z, kind = rnd() < 0.5 ? 0 : 1) => {
      if (!free(x, z)) return;
      if (rnd() > this.q.treeDensity) return;
      const s = 0.8 + rnd() * 0.5;
      this.treeSpots.push({ x, z, y: CURB, s, kind });
      this.col.addCircle(x, z, 0.3, 'tree');
    };
    const alongX = rnd() < 0.5;
    const P = (u, v) => (alongX ? [cx + u, cz + v] : [cx + v, cz + u]);

    if (type === 'panel') {
      // «хрущёвки»/девятиэтажки: 3 длинных дома параллельно
      const floors = rnd() < 0.6 ? 5 : 9;
      for (const v of [-24, 0, 24]) {
        if (v === 0 && rnd() < 0.25) continue;
        const [x, z] = P((rnd() - 0.5) * 6, v);
        const len = 48 + ((rnd() * 3) | 0) * 6;
        add(x, z, alongX ? len : 12, alongX ? 12 : len, floors);
      }
      for (const v of [-12, 12]) for (let u = -30; u <= 30; u += 9) tree(...P(u + (rnd() - 0.5) * 3, v + (rnd() - 0.5) * 3));
    } else if (type === 'tower') {
      // 12–16-этажные точечные дома
      add(...P(-16, -16), 18, 18, 12 + ((rnd() * 5) | 0));
      add(...P(16, 16), 18, 18, 12 + ((rnd() * 5) | 0));
      if (rnd() < 0.6) {
        const [x, z] = P(16, -18);
        add(x, z, alongX ? 30 : 12, alongX ? 12 : 30, 9);
      }
      for (let k = 0; k < 22; k++) tree(lx0 + 4 + rnd() * (lx1 - lx0 - 8), lz0 + 4 + rnd() * (lz1 - lz0 - 8));
    } else if (type === 'garages') {
      // гаражный кооператив + пятиэтажка
      for (const v of [-28, -16, -4]) {
        const [x, z] = P(0, v);
        add(x, z, alongX ? 60 : 6, alongX ? 6 : 60, 1, {
          height: 2.7, flat: true, color: GARAGE_COLORS[(rnd() * GARAGE_COLORS.length) | 0],
        });
      }
      const [x, z] = P(0, 22);
      add(x, z, alongX ? 60 : 12, alongX ? 12 : 60, 5);
      for (let u = -30; u <= 30; u += 7) tree(...P(u, 10 + rnd() * 2));
    } else {
      // парк: деревья + киоск «Союзпечать»
      add(cx + 6, cz + 4, 4, 3, 1, { height: 3, flat: true, color: 0x4f7fb5 });
      for (let k = 0; k < 45; k++) tree(lx0 + 3 + rnd() * (lx1 - lx0 - 6), lz0 + 3 + rnd() * (lz1 - lz0 - 6));
    }

    if (!batch.empty) {
      chunk.mesh = this._mesh(batch.build(), this.mat.building, `Block_${chunk.idx}`, { cast: true, receive: false });
      chunk.radius = Math.hypot(chunk.maxX - chunk.minX, chunk.maxZ - chunk.minZ) / 2;
      chunk.cx = (chunk.minX + chunk.maxX) / 2;
      chunk.cz = (chunk.minZ + chunk.maxZ) / 2;
      this.chunks.push(chunk);
    }
  }

  // ---------------------------------------------------------------- забор ПО-2
  _buildFence() {
    const E = CITY_EXTENT + 0.3, H = 2.4, T = 0.2;
    const parts = [];
    for (const s of [1, -1]) {
      parts.push(prep(scaleUV(new THREE.BoxGeometry(E * 2 + T, H, T).translate(0, H / 2, s * E), (E * 2) / 4, 1)));
      parts.push(prep(scaleUV(new THREE.BoxGeometry(T, H, E * 2 + T).translate(s * E, H / 2, 0), (E * 2) / 4, 1)));
      this.col.addBox(-E - 1, s * E - 0.5, E + 1, s * E + 0.5, 'fence');
      this.col.addBox(s * E - 0.5, -E - 1, s * E + 0.5, E + 1, 'fence');
    }
    this._mesh(merge(parts), this.mat.fence, 'Fence', { cast: true, receive: true });
  }

  // ---------------------------------------------------------------- фонари
  _buildStreetLights() {
    const poles = [], heads = [];
    const pools = new QuadBatch();
    const poleBase = new THREE.CylinderGeometry(0.08, 0.13, 8, 6, 1, true);
    const withPools = this.q.streetLightPools;
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
              poles.push(prep(poleBase.clone().translate(px, 4 + CURB, pz)));
              // кронштейн к проезжей части
              const armLen = 1.9;
              const amx = px - rx * side * armLen / 2, amz = pz - rz * side * armLen / 2;
              const arm = new THREE.BoxGeometry(Math.abs(rx) * armLen + 0.08, 0.08, Math.abs(rz) * armLen + 0.08);
              poles.push(prep(arm.translate(amx, 7.95 + CURB, amz)));
              const hx = px - rx * side * armLen, hz = pz - rz * side * armLen;
              const head = new THREE.BoxGeometry(Math.abs(rx) * 0.7 + 0.3, 0.14, Math.abs(rz) * 0.7 + 0.3);
              heads.push(prep(head.translate(hx, 7.85 + CURB, hz)));
              if (withPools) pools.flat(hx - rx * side * 0.8, hz - rz * side * 0.8, dx, dz, 13, 13, 0.02);
              this.col.addCircle(px, pz, 0.18, 'pole');
            }
          }
        }
      }
    }
    poleBase.dispose();
    this._mesh(merge(poles), this.mat.props, 'StreetLightPoles', { cast: false });
    this.lampHeads = this._mesh(merge(heads), this.mat.lampHead, 'StreetLampHeads');
    if (withPools) {
      this.lightPools = this._mesh(pools.build(), this.mat.pool, 'StreetLightPools');
      this.lightPools.renderOrder = 2;
      this.lightPools.visible = false;
    }
  }

  // ---------------------------------------------------------------- деревья за городом
  _buildOuterTrees() {
    const rnd = this.rnd;
    const count = Math.floor(900 * this.q.treeDensity);
    for (let k = 0; k < count; k++) {
      const x = (rnd() - 0.5) * 1300, z = (rnd() - 0.5) * 1300;
      const d = Math.max(Math.abs(x), Math.abs(z)) - CITY_EXTENT;
      if (d < 10) continue;
      // лесополосы: плотнее там, где шум выше
      if (this.noise(x / 120, z / 120) < 0.45) continue;
      this.treeSpots.push({ x, z, y: this.heightAt(x, z), s: 0.9 + rnd() * 0.7, kind: rnd() < 0.55 ? 2 : 0 });
    }
  }

  // ---------------------------------------------------------------- запросы
  /** Тип покрытия под точкой: высота и коэффициент сцепления. */
  surface(x, z, out) {
    const n = N - 1;
    const ix = clamp(Math.round((x - coord(0)) / SPACING), 0, n);
    const iz = clamp(Math.round((z - coord(0)) / SPACING), 0, n);
    const rx = Math.abs(x - coord(ix)), rz = Math.abs(z - coord(iz));
    if (rx <= HALF || rz <= HALF) { out.y = 0; out.mu = 1.0; out.type = 0; }
    else if (rx <= HALF + SIDEWALK || rz <= HALF + SIDEWALK) { out.y = CURB; out.mu = 0.95; out.type = 1; }
    else { out.y = CURB; out.mu = 0.62; out.type = 2; }
    return out;
  }

  // ---------------------------------------------------------------- кадр
  update(dt, camera, night) {
    this.mat.building.emissiveIntensity = night * 1.15;
    this.mat.lampHead.color.setRGB(lerp(0.23, 1.0, night), lerp(0.23, 0.82, night), lerp(0.23, 0.55, night));
    if (this.lightPools) {
      this.mat.pool.opacity = night * 0.55;
      this.lightPools.visible = night > 0.02;
    }
    // culling раз в 8 кадров — чанки статичны, камера за 8 кадров смещается мало
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

  /**
   * Программный occlusion culling: квартал скрыт, если все линии взгляда
   * от камеры к 9 точкам его контура перекрыты высокими зданиями других кварталов
   * (высота окклюдера ≥ высоты самого высокого здания цели). Дёшево: только 2D slab-тесты.
   */
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

/** Здание-коробка: 4 стены с UV фасада (окна кратны 3 м) + крыша. 10 треугольников. */
function addBuilding(b, x0, z0, x1, z1, y0, h, color, flat, rnd) {
  const y1 = y0 + h;
  const roof = color.clone().multiplyScalar(0.45);
  const off = ((rnd() * 8) | 0) / 8; // сдвиг окон, чтобы дома не выглядели одинаково
  const wall = (ax, az, ux, uz, len, nx, nz) => {
    const bx = ax + ux * len, bz = az + uz * len;
    const u1 = len / FACADE_TILE + off, v1 = h / FACADE_TILE;
    const uvs = flat
      ? [FLAT_UV[0], FLAT_UV[1], FLAT_UV[0], FLAT_UV[1], FLAT_UV[0], FLAT_UV[1], FLAT_UV[0], FLAT_UV[1]]
      : [off, 0, u1, 0, u1, v1, off, v1];
    b.quad([ax, y0, az], [bx, y0, bz], [bx, y1, bz], [ax, y1, az], nx, 0, nz, uvs, color);
  };
  const w = x1 - x0, d = z1 - z0;
  wall(x0, z1, 1, 0, w, 0, 1);    // +Z
  wall(x1, z0, -1, 0, w, 0, -1);  // -Z
  wall(x1, z1, 0, -1, d, 1, 0);   // +X
  wall(x0, z0, 0, 1, d, -1, 0);   // -X
  const f = FLAT_UV;
  b.quad([x0, y1, z1], [x1, y1, z1], [x1, y1, z0], [x0, y1, z0], 0, 1, 0,
    [f[0], f[1], f[0], f[1], f[0], f[1], f[0], f[1]], roof);
}
