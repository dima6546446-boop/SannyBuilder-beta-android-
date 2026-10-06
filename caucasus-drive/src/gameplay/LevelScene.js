import * as THREE from 'three';
import { CollisionWorld } from '../core/CollisionWorld.js';
import { AUTODROME } from '../world/City.js';
import { QuadBatch, prep, merge } from '../utils/geometry.js';
import { CAR_BY_ID } from '../config/cars.js';
import { wrapAngle } from '../utils/math.js';

/**
 * Объекты парковочного упражнения на автодроме: конусы, машины-препятствия,
 * стены гаражей, временная разметка, целевая зона и проверка «встал ровно».
 * Коллизии — отдельный CollisionWorld (очищается при выходе).
 */
export class LevelScene {
  constructor(game) {
    this.g = game;
    this.col = new CollisionWorld(8);
    this.cars = [];
    this.meshes = [];
    this.ox = AUTODROME.cx;
    this.oz = AUTODROME.cz;
  }

  load(L, carDef) {
    this.unload();
    const g = this.g, ox = this.ox, oz = this.oz;
    this.L = L;
    // конусы
    g.cones.set(L.cones.map(([x, z]) => [x + ox, z + oz]));
    L.cones.forEach(([x, z], i) => { const o = this.col.addCircle(x + ox, z + oz, 0.24, 'cone'); o.idx = i; });
    // машины-препятствия
    this.cars = L.cars.map(([x, z, h, key]) => {
      const def = CAR_BY_ID[key] || CAR_BY_ID.vaz2107;
      const mi = g.instancer.modelIndex(key);
      const variant = g.instancer.models[mi];
      const color = new THREE.Color(variant.fixedColor ?? def.colors[(Math.abs(Math.sin(x * 13.1 + z)) * def.colors.length) | 0]);
      const car = { model: mi, x: x + ox, z: z + oz, y: 0, heading: h, color, lights: false, flashers: key === 'police' };
      const d = variant.dims;
      const r = d.W / 2 * 0.96;
      const zF = d.front - r, zR = d.rear + r;
      for (let k = 0; k < 4; k++) {
        const off = zR + (zF - zR) * (k / 3);
        this.col.addCircle(car.x + Math.sin(h) * off, car.z + Math.cos(h) * off, r, 'obstacle');
      }
      return car;
    });
    // стены
    if (L.walls.length) {
      const parts = [];
      for (const [x0, z0, x1, z1, h] of L.walls) {
        parts.push(prep(new THREE.BoxGeometry(x1 - x0, h, z1 - z0).translate((x0 + x1) / 2 + ox, h / 2, (z0 + z1) / 2 + oz)));
        this.col.addBox(x0 + ox, z0 + oz, x1 + ox, z1 + oz, 'wall');
      }
      const m = new THREE.Mesh(merge(parts), new THREE.MeshLambertMaterial({ map: g.T.brick }));
      m.castShadow = m.receiveShadow = g.q.shadows;
      g.scene.add(m);
      this.meshes.push(m);
      // крыша гаража
      const roofParts = L.walls.filter((w) => w[4] > 2.5);
      if (roofParts.length) {
        const xs = L.walls.flatMap((w) => [w[0], w[2]]), zs = L.walls.flatMap((w) => [w[1], w[3]]);
        const minX = Math.min(...xs), maxX = Math.max(...xs), minZ = Math.min(...zs), maxZ = Math.max(...zs);
        const roof = new THREE.Mesh(new THREE.BoxGeometry(maxX - minX + 0.4, 0.15, maxZ - minZ + 0.2).translate((minX + maxX) / 2 + ox, 2.65, (minZ + maxZ) / 2 + oz),
          new THREE.MeshLambertMaterial({ color: 0x4a4d52 }));
        roof.castShadow = g.q.shadows;
        g.scene.add(roof);
        this.meshes.push(roof);
      }
    }
    // разметка
    if (L.lines.length) {
      const b = new QuadBatch();
      for (const [x0, z0, x1, z1] of L.lines) {
        const dx = x1 - x0, dz = z1 - z0, len = Math.hypot(dx, dz);
        b.flat((x0 + x1) / 2 + ox, (z0 + z1) / 2 + oz, dx / len, dz / len, len, 0.14, 0.016);
      }
      const m = new THREE.Mesh(b.build(), new THREE.MeshLambertMaterial({ color: 0xf2f2f2, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3 }));
      g.scene.add(m);
      this.meshes.push(m);
    }
    const t = L.target;
    g.zone.show(t.x + ox, t.z + oz, t.h, t.w, t.l);
    this.target = { ...t, x: t.x + ox, z: t.z + oz };
    this.hold = 0;
    void carDef;
  }

  startPose() {
    const [x, z, h] = this.L.start;
    return [x + this.ox, z + this.oz, h];
  }

  unload() {
    this.g.cones?.clear();
    this.g.zone?.hide();
    for (const m of this.meshes) { this.g.scene.remove(m); m.geometry.dispose(); m.material.dispose(); }
    this.meshes = [];
    this.cars = [];
    this.col = new CollisionWorld(8);
    this.target = null;
  }

  /** 'inside' — весь кузов в зоне и курс совпадает; 'wrong' — в зоне, но задом наперёд; иначе null. */
  check(player) {
    const t = this.target;
    if (!t) return null;
    const s = Math.sin(t.h), c = Math.cos(t.h);
    let all = true;
    for (const [x, z] of player.corners()) {
      const dx = x - t.x, dz = z - t.z;
      const lx = dx * c - dz * s, lz = dx * s + dz * c;
      if (Math.abs(lx) > t.w / 2 || Math.abs(lz) > t.l / 2) { all = false; break; }
    }
    if (!all) return null;
    const dh = Math.abs(wrapAngle(player.physics.heading - t.h));
    if (dh < 0.2) return 'inside';
    if (Math.abs(dh - Math.PI) < 0.2) return 'wrong';
    return null;
  }

  update(dt, player) {
    const st = this.check(player);
    const stopped = player.physics.speed < 0.3;
    if (st === 'inside' && stopped) this.hold += dt; else this.hold = 0;
    this.g.zone.update(dt, st, Math.min(1, this.hold / 1.2));
    return { state: st, done: this.hold >= 1.2 };
  }

  render(inst, glow, blink) {
    for (const c of this.cars) inst.add(c, glow, blink);
  }
}
