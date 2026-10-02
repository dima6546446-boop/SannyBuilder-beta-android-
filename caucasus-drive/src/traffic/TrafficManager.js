import * as THREE from 'three';
import { Pool } from '../core/Pool.js';
import { TrafficCar } from './TrafficCar.js';
import { CITY } from '../world/RoadGraph.js';
import { CARS, CAR_BY_ID } from '../config/cars.js';
import { mulberry32 } from '../utils/math.js';

const SPAWN_MIN = 70;
const SPAWN_MAX = 190;
const DESPAWN = 240;

/**
 * Менеджер трафика.
 *  - Object pooling: машины создаются один раз, spawn/despawn = acquire/release.
 *  - Спавн вокруг игрока в кольце 70–190 м вне поля зрения; модель — по весам
 *    «встречаемости» (классика чаще), иногда ДПС и такси.
 *  - Рендер делегирован CarInstancer (общий с припаркованными машинами).
 */
export class TrafficManager {
  constructor({ graph, lights, quality, instancer, onHonk }) {
    this.q = quality;
    this.graph = graph;
    this.inst = instancer;
    this.max = Math.max(1, quality.trafficCount);
    this.target = quality.trafficCount;
    this.density = 1;   // множитель от регулятора качества (PerfMonitor)
    this.rnd = mulberry32(777);
    this.pool = new Pool((i) => new TrafficCar(i), this.max);
    this.cars = this.pool.active;
    this.player = { x: 0, z: 0, heading: 0, speed: 0 };
    this.ctx = { graph, lights, cars: this.cars, player: this.player, rnd: this.rnd, onHonk };
    this._spawnTimer = 0;
    this._frustum = new THREE.Frustum();
    this._pm = new THREE.Matrix4();
    this._v = new THREE.Vector3();
    this.enabled = true;

    // таблица весов моделей
    this.weights = [];
    let tot = 0;
    for (const c of CARS) { tot += c.traffic; this.weights.push([instancer.modelIndex(c.id), tot, c]); }
    this.weights.push([instancer.modelIndex('taxi'), tot += 0.8, CAR_BY_ID.granta]);
    this.weights.push([instancer.modelIndex('police'), tot += 0.4, CAR_BY_ID.vaz2107]);
    this.totalWeight = tot;
  }

  _pickModel() {
    const r = this.rnd() * this.totalWeight;
    for (const [mi, acc, def] of this.weights) {
      if (r <= acc) {
        const variant = this.inst.models[mi];
        const hex = variant.fixedColor ?? def.colors[(this.rnd() * def.colors.length) | 0];
        return [mi, new THREE.Color(hex)];
      }
    }
    return [0, new THREE.Color(0xffffff)];
  }

  clear() {
    for (let i = this.cars.length - 1; i >= 0; i--) { this.cars[i].active = false; this.pool.release(this.cars[i]); }
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
      if (d < this.q.drawDistance * 0.6 && this._frustum.containsPoint(this._v.set(x, 1, z))) continue;
      if (this.cars.some((o) => o !== car && Math.abs(o.x - x) < 12 && Math.abs(o.z - z) < 12)) continue;
      const cruise = 10 + this.rnd() * 5.5; // 36–56 км/ч
      const [mi, color] = this._pickModel();
      car.spawn(this.ctx, a, b, lane, s, cruise, mi, color);
      return;
    }
    car.active = false;
    this.pool.release(car);
  }

  update(dt, playerPhysics, camera) {
    const p = this.player;
    p.x = playerPhysics.x; p.z = playerPhysics.z;
    p.heading = playerPhysics.heading; p.speed = playerPhysics.speed;
    if (!this.enabled) return;

    this._spawnTimer -= dt;
    if (this._spawnTimer <= 0 && this.cars.length < Math.round(this.target * this.density)) {
      this._spawnTimer = 0.25;
      this._trySpawn(camera);
    }
    for (let i = this.cars.length - 1; i >= 0; i--) {
      const c = this.cars[i];
      c.update(dt);
      if (Math.hypot(c.x - p.x, c.z - p.z) > DESPAWN) { c.active = false; this.pool.release(c); }
    }
  }

  render(glow, blink) {
    const inst = this.inst;
    for (const car of this.cars) {
      car.flashers = false;
      inst.add(car, glow, blink);
    }
  }

  prefill(camera) {
    for (let i = 0; i < this.target * 3 && this.cars.length < this.target; i++) this._trySpawn(camera);
  }
}
