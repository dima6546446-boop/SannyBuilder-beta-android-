import * as THREE from 'three';
import { Pool } from '../core/Pool.js';
import { TrafficCar } from './TrafficCar.js';
import { CITY } from '../world/RoadGraph.js';
import { buildTrafficGeometries, LADA_COLORS } from '../vehicles/LadaModel.js';
import { mulberry32 } from '../utils/math.js';

const SPAWN_MIN = 70;
const SPAWN_MAX = 190;
const DESPAWN = 240;

/**
 * Менеджер трафика.
 *  - Object pooling: MAX машин создаётся один раз, spawn/despawn = acquire/release.
 *  - Спавн вокруг игрока в кольце 70–190 м, вне поля зрения (или за туманом).
 *  - Рендер: 7 InstancedMesh на ВЕСЬ трафик (≈7 draw calls независимо от числа машин):
 *    кузов LOD0 (цвет — instanceColor), детали LOD0, LOD1 целиком, фары, фонари,
 *    световые пятна фар (ночью), blob-тени.
 */
export class TrafficManager {
  constructor(scene, { graph, lights, quality, T, onHonk }) {
    this.q = quality;
    this.graph = graph;
    this.max = Math.max(1, quality.trafficCount);
    this.rnd = mulberry32(777);
    this.pool = new Pool((i) => new TrafficCar(i), this.max);
    this.cars = this.pool.active;
    this.player = { x: 0, z: 0, heading: 0, speed: 0 };
    this.ctx = { graph, lights, cars: this.cars, player: this.player, rnd: this.rnd, onHonk };
    this._spawnTimer = 0;
    this._frustum = new THREE.Frustum();
    this._pm = new THREE.Matrix4();
    this._v = new THREE.Vector3();
    this._buildRender(scene, T);
  }

  _buildRender(scene, T) {
    const g = buildTrafficGeometries();
    const n = this.max;
    const mk = (geo, mat, name, colored = true) => {
      const im = new THREE.InstancedMesh(geo, mat, n);
      im.name = name;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      if (colored) {
        im.setColorAt(0, new THREE.Color(1, 1, 1)); // создаём instanceColor до первой компиляции шейдера
        im.instanceColor.setUsage(THREE.DynamicDrawUsage);
      }
      im.frustumCulled = false; // bounding sphere инстансов меняется каждый кадр
      im.count = 0;
      scene.add(im);
      return im;
    };
    const shadows = this.q.shadows && this.q.name === 'high';
    this.body0 = mk(g.body, new THREE.MeshLambertMaterial({ color: 0xffffff }), 'TrafficBodyLOD0');
    this.detail0 = mk(g.detail, new THREE.MeshLambertMaterial({ vertexColors: true }), 'TrafficDetailLOD0', false);
    this.lod1 = mk(g.low, new THREE.MeshLambertMaterial({ vertexColors: true }), 'TrafficLOD1');
    this.body0.castShadow = this.detail0.castShadow = shadows;
    this.heads = mk(g.heads, new THREE.MeshBasicMaterial({ toneMapped: false }), 'TrafficHeadLamps');
    this.tails = mk(g.tails, new THREE.MeshBasicMaterial({ toneMapped: false }), 'TrafficTailLamps');

    const beamGeo = new THREE.PlaneGeometry(4, 11).rotateX(-Math.PI / 2).translate(0, 0.035, 8);
    this.beams = mk(beamGeo, new THREE.MeshBasicMaterial({
      map: T.glow, color: 0xfff0c8, transparent: true, opacity: 0.3, depthWrite: false,
      blending: THREE.AdditiveBlending, toneMapped: false,
    }), 'TrafficBeams', false);
    this.beams.renderOrder = 2;
    const blobGeo = new THREE.PlaneGeometry(2.0, 4.7).rotateX(-Math.PI / 2).translate(0, 0.025, -0.1);
    this.blobs = mk(blobGeo, new THREE.MeshBasicMaterial({
      map: T.blob, transparent: true, depthWrite: false, opacity: 0.75,
    }), 'TrafficBlobShadows', false);

    this.palette = LADA_COLORS.map((c) => new THREE.Color(c.hex));
    this._m = new THREE.Matrix4();
    this._q = new THREE.Quaternion();
    this._p = new THREE.Vector3();
    this._s = new THREE.Vector3(1, 1, 1);
    this._up = new THREE.Vector3(0, 1, 0);
    this._c = new THREE.Color();
  }

  _trySpawn(camera) {
    const car = this.pool.acquire();
    if (!car) return;
    const p = this.player;
    camera.updateMatrixWorld();
    this._pm.multiplyMatrices(camera.projectionMatrix, camera.matrixWorldInverse);
    this._frustum.setFromProjectionMatrix(this._pm);
    for (let attempt = 0; attempt < 12; attempt++) {
      const [a, b] = this.graph.edges[(this.rnd() * this.graph.edges.length) | 0];
      const A = this.graph.nodes[a], B = this.graph.nodes[b];
      const [dx, dz] = this.graph.dir(a, b);
      const len = Math.hypot(B.x - A.x, B.z - A.z) - 2 * CITY.HALF;
      const s = 6 + this.rnd() * (len - 26);
      const lane = this.rnd() < 0.5 ? 0 : 1;
      const x = A.x + dx * (CITY.HALF + s) - dz * CITY.LANES[lane];
      const z = A.z + dz * (CITY.HALF + s) + dx * CITY.LANES[lane];
      const d = Math.hypot(x - p.x, z - p.z);
      if (d < SPAWN_MIN || d > SPAWN_MAX) continue;
      // не «выпрыгиваем» из воздуха на глазах у игрока
      if (d < this.q.drawDistance * 0.6 && this._frustum.containsPoint(this._v.set(x, 1, z))) continue;
      if (this.cars.some((o) => o !== car && Math.abs(o.x - x) < 12 && Math.abs(o.z - z) < 12)) continue;
      const cruise = 10 + this.rnd() * 5.5; // 36–56 км/ч
      car.spawn(this.ctx, a, b, lane, s, cruise, (this.rnd() * this.palette.length) | 0);
      return;
    }
    car.active = false;
    this.pool.release(car);
  }

  update(dt, playerPhysics, camera, night) {
    const p = this.player;
    p.x = playerPhysics.x; p.z = playerPhysics.z;
    p.heading = playerPhysics.heading; p.speed = playerPhysics.speed;

    // спавн: не больше одной машины за 0.25 с — без пиков нагрузки
    this._spawnTimer -= dt;
    if (this._spawnTimer <= 0 && this.cars.length < this.q.trafficCount) {
      this._spawnTimer = 0.25;
      this._trySpawn(camera);
    }

    for (let i = this.cars.length - 1; i >= 0; i--) {
      const c = this.cars[i];
      c.update(dt);
      if (Math.hypot(c.x - p.x, c.z - p.z) > DESPAWN) { c.active = false; this.pool.release(c); }
    }
    this._render(camera.position, night);
  }

  _render(camPos, night) {
    const lodD2 = this.q.carLodDistance ** 2;
    let n0 = 0, n1 = 0, nl = 0;
    const m = this._m, q = this._q, pos = this._p, c = this._c;
    const headDay = 0.28, beamsOn = night > 0.25;
    for (const car of this.cars) {
      q.setFromAxisAngle(this._up, car.heading);
      m.compose(pos.set(car.x, 0, car.z), q, this._s);
      const dx = car.x - camPos.x, dz = car.z - camPos.z;
      const col = this.palette[car.colorIndex];
      if (dx * dx + dz * dz < lodD2) {
        this.body0.setMatrixAt(n0, m); this.body0.setColorAt(n0, col);
        this.detail0.setMatrixAt(n0, m);
        n0++;
      } else {
        this.lod1.setMatrixAt(n1, m); this.lod1.setColorAt(n1, col);
        n1++;
      }
      this.heads.setMatrixAt(nl, m);
      this.tails.setMatrixAt(nl, m);
      this.blobs.setMatrixAt(nl, m);
      if (beamsOn) this.beams.setMatrixAt(nl, m);
      const h = night > 0.3 ? 1.6 : headDay;
      this.heads.setColorAt(nl, c.setRGB(h, h * 0.95, h * 0.8));
      const t = car.braking ? 2.2 : night > 0.3 ? 0.9 : 0.25;
      this.tails.setColorAt(nl, c.setRGB(t, t * 0.05, t * 0.03));
      nl++;
    }
    this.body0.count = this.detail0.count = n0;
    this.lod1.count = n1;
    this.heads.count = this.tails.count = this.blobs.count = nl;
    this.beams.count = beamsOn ? nl : 0;
    this.beams.material.opacity = night * 0.35;
    for (const im of [this.body0, this.detail0, this.lod1, this.heads, this.tails, this.blobs, this.beams]) {
      im.instanceMatrix.needsUpdate = true;
      if (im.instanceColor) im.instanceColor.needsUpdate = true;
    }
  }

  /** Стартовое заполнение города, чтобы улицы не были пустыми в первые секунды. */
  prefill(camera) {
    for (let i = 0; i < this.q.trafficCount * 3 && this.cars.length < this.q.trafficCount; i++) this._trySpawn(camera);
  }
}

