import * as THREE from 'three';
import { damp, dampAngle, clamp } from '../utils/math.js';

const MODES = [
  { name: 'Сзади', type: 'chase', dist: 6.0, height: 2.0, look: 1.1 },
  { name: 'Сзади (далеко)', type: 'chase', dist: 9.0, height: 3.2, look: 1.2 },
  { name: 'Капот', type: 'attached', pos: [0, 1.22, 0.75], look: [0, 1.0, 12] },
  { name: 'Из салона', type: 'attached', pos: [0.36, 1.2, -0.22], look: [0.36, 1.08, 12] },
];

/**
 * Камера: от третьего лица (с инерцией и защитой от «проваливания» в стены),
 * с капота и из салона (водитель слева). FOV растёт со скоростью, тряска при ударе.
 */
export class CameraRig {
  constructor(camera, collision) {
    this.camera = camera;
    this.col = collision;
    this.mode = 0;
    this.yaw = 0;
    this.shake = 0;
    this._pos = new THREE.Vector3();
    this._look = new THREE.Vector3();
    this._tmp = new THREE.Vector3();
    this._init = false;
  }

  get modeName() { return MODES[this.mode].name; }

  next() {
    this.mode = (this.mode + 1) % MODES.length;
    this._init = false;
    return this.modeName;
  }

  addShake(v) { this.shake = Math.min(1, this.shake + v); }

  update(dt, car) {
    const cam = this.camera;
    const m = MODES[this.mode];
    const p = car.physics;
    car.root.updateMatrixWorld();

    if (m.type === 'chase') {
      // за движением назад камера не разворачивается — смотрим по курсу кузова
      if (!this._init) { this.yaw = p.heading; }
      this.yaw = dampAngle(this.yaw, p.heading, 4.5, dt);
      const fx = Math.sin(this.yaw), fz = Math.cos(this.yaw);
      const base = car.root.position;
      let dist = m.dist + clamp(p.speed / 30, 0, 1) * 1.2;
      // не даём камере уйти за стену здания
      const tx = base.x - fx * dist, tz = base.z - fz * dist;
      const t = this.col.segmentHit(base.x, base.z, tx, tz);
      if (t < 1) dist = Math.max(1.5, dist * t - 0.4);
      this._pos.set(base.x - fx * dist, base.y + m.height, base.z - fz * dist);
      if (!this._init) cam.position.copy(this._pos);
      cam.position.x = damp(cam.position.x, this._pos.x, 12, dt);
      cam.position.y = damp(cam.position.y, this._pos.y, 6, dt);
      cam.position.z = damp(cam.position.z, this._pos.z, 12, dt);
      const hx = Math.sin(p.heading), hz = Math.cos(p.heading);
      this._look.set(base.x + hx * 2.5, base.y + m.look, base.z + hz * 2.5);
    } else {
      this._pos.fromArray(m.pos);
      car.root.localToWorld(this._pos);
      cam.position.copy(this._pos);
      this._look.fromArray(m.look);
      car.root.localToWorld(this._look);
    }
    this._init = true;

    if (this.shake > 0.001) {
      const s = this.shake * 0.25;
      cam.position.x += (Math.random() - 0.5) * s;
      cam.position.y += (Math.random() - 0.5) * s;
      this.shake *= Math.exp(-dt * 6);
    }
    cam.lookAt(this._look);

    const fov = (m.type === 'chase' ? 58 : 66) + clamp(p.speed / 40, 0, 1) * 10;
    if (Math.abs(cam.fov - fov) > 0.05) {
      cam.fov = damp(cam.fov, fov, 3, dt);
      cam.updateProjectionMatrix();
    }
  }
}
