import * as THREE from 'three';
import { damp, dampAngle, clamp } from '../utils/math.js';

const MODES = [
  { name: 'Сзади', type: 'chase', dist: 6.2, height: 2.1, look: 1.1 },
  { name: 'Сзади (далеко)', type: 'chase', dist: 9.5, height: 3.4, look: 1.2 },
  { name: 'Сверху', type: 'top', dist: 5, height: 13, look: 0 },
  { name: 'Капот', type: 'attached', pos: [0, 1.22, 0.75], look: [0, 1.0, 12] },
  { name: 'Из салона', type: 'attached', pos: [0.36, 1.2, -0.22], look: [0.36, 1.08, 12] },
];

/**
 * Камеры: сзади (с инерцией, облётом свайпом и защитой от стен), сверху (для парковки),
 * с капота и из салона. FOV растёт со скоростью, тряска при ударе.
 */
export class CameraRig {
  constructor(camera, collision) {
    this.camera = camera;
    this.col = collision;
    this.mode = 0;
    this.yaw = 0;
    this.shake = 0;
    this.orbit = 0;
    this._pos = new THREE.Vector3();
    this._look = new THREE.Vector3();
    this._init = false;
  }

  get modeName() { return MODES[this.mode].name; }
  get isInterior() { return MODES[this.mode].type === 'attached'; }

  setMode(i) { this.mode = i % MODES.length; this._init = false; }
  next() { this.setMode(this.mode + 1); return this.modeName; }
  snap() { this._init = false; }
  addShake(v) { this.shake = Math.min(1, this.shake + v); }

  // ---------------------------------------------------------------- пешком
  /** Камера от третьего лица: свайп крутит её вокруг персонажа (рыскание и наклон). */
  beginFoot(yaw) {
    this.footYaw = yaw;
    this.footPitch = 0.22;
    this.footDist = this.footDist || 3.3;
    this._footInit = false;
  }

  nextFoot() {
    this.footDist = this.footDist > 4 ? 3.3 : 5.2;
    return this.footDist > 4 ? 'Пешком: дальняя' : 'Пешком: обычная';
  }

  updateFoot(dt, w, look) {
    const cam = this.camera;
    this.footYaw -= look.dx * 0.0065;
    this.footPitch = clamp(this.footPitch + look.dy * 0.004, -0.35, 1.1);
    const head = w.headPos;
    const ty = this._footInit ? damp(this._footTY, head.y - 0.05, 8, dt) : head.y - 0.05;
    this._footTY = ty;
    const tx = w.pos.x, tz = w.pos.z;
    const cp = Math.cos(this.footPitch), sp = Math.sin(this.footPitch);
    const fx = Math.sin(this.footYaw) * cp, fz = Math.cos(this.footYaw) * cp;
    let dist = this.footDist;
    // павильон остановки для пешехода «прозрачен» (сидим внутри на лавке) — учитываем только его стенку
    const t = Math.min(this.col.segmentHit(tx, tz, tx - fx * dist, tz - fz * dist, 'busstop'),
      w.pedCol.segmentHit(tx, tz, tx - fx * dist, tz - fz * dist));
    if (t < 1) dist = Math.max(0.8, dist * t - 0.3);
    this._pos.set(tx - fx * dist, ty + sp * dist + 0.25, tz - fz * dist);
    this._pos.y = Math.max(this._pos.y, w.groundY + 0.25);
    if (!this._footInit) cam.position.copy(this._pos);
    cam.position.x = damp(cam.position.x, this._pos.x, 18, dt);
    cam.position.y = damp(cam.position.y, this._pos.y, 18, dt);
    cam.position.z = damp(cam.position.z, this._pos.z, 18, dt);
    this._footInit = true;
    if (this.shake > 0.001) {
      const s = this.shake * 0.2;
      cam.position.x += (Math.random() - 0.5) * s;
      cam.position.y += (Math.random() - 0.5) * s;
      this.shake *= Math.exp(-dt * 6);
    }
    this._look.set(tx, ty + 0.1, tz);
    cam.lookAt(this._look);
    if (Math.abs(cam.fov - 62) > 0.05) { cam.fov = damp(cam.fov, 62, 4, dt); cam.updateProjectionMatrix(); }
  }

  update(dt, car, orbit = 0) {
    const cam = this.camera;
    const m = MODES[this.mode];
    const p = car.physics;
    car.root.updateMatrixWorld();
    const base = car.root.position;

    if (m.type === 'chase' || m.type === 'top') {
      // при движении задом камера не разворачивается — держим курс кузова
      if (!this._init) this.yaw = p.heading;
      this.yaw = dampAngle(this.yaw, p.heading, m.type === 'top' ? 3 : 4.5, dt);
      const yaw = this.yaw + orbit;
      const fx = Math.sin(yaw), fz = Math.cos(yaw);
      let dist = m.dist + (m.type === 'chase' ? clamp(p.speed / 30, 0, 1) * 1.2 : 0);
      if (m.type === 'chase') {
        const t = this.col.segmentHit(base.x, base.z, base.x - fx * dist, base.z - fz * dist);
        if (t < 1) dist = Math.max(1.5, dist * t - 0.4);
      }
      this._pos.set(base.x - fx * dist, base.y + m.height, base.z - fz * dist);
      if (!this._init) cam.position.copy(this._pos);
      const k = m.type === 'top' ? 8 : 12;
      cam.position.x = damp(cam.position.x, this._pos.x, k, dt);
      cam.position.y = damp(cam.position.y, this._pos.y, 6, dt);
      cam.position.z = damp(cam.position.z, this._pos.z, k, dt);
      const hx = Math.sin(p.heading), hz = Math.cos(p.heading);
      const ahead = m.type === 'top' ? 1.5 : 2.5;
      this._look.set(base.x + hx * ahead, base.y + m.look, base.z + hz * ahead);
    } else {
      this._pos.fromArray(m.pos);
      car.root.localToWorld(this._pos);
      cam.position.copy(this._pos);
      this._look.fromArray(m.look);
      if (orbit) {
        const r = 12, a = orbit;
        this._look.set(m.look[0] + Math.sin(a) * r, m.look[1], m.pos[2] + Math.cos(a) * r);
      }
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

    const fov = (m.type === 'attached' ? 68 : m.type === 'top' ? 55 : 58) + (m.type === 'chase' ? clamp(p.speed / 40, 0, 1) * 10 : 0);
    if (Math.abs(cam.fov - fov) > 0.05) {
      cam.fov = damp(cam.fov, fov, 3, dt);
      cam.updateProjectionMatrix();
    }
  }
}
