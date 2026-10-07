import * as THREE from 'three';

export const CAMERA_MODES = [
  { id: 'chase', name: 'Сзади' },
  { id: 'far', name: 'Далеко' },
  { id: 'hood', name: 'Капот' },
  { id: 'bumper', name: 'Бампер' },
  { id: 'top', name: 'Сверху' },
  { id: 'tv', name: 'ТВ' },
];

const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const angDiff = (a, b) => { let d = b - a; while (d > Math.PI) d -= Math.PI * 2; while (d < -Math.PI) d += Math.PI * 2; return d; };

/** Камера: плавное преследование с наклоном «в сторону движения», чтобы был виден угол заноса. */
export class CameraRig {
  constructor(camera) {
    this.camera = camera; this.mode = 'chase';
    this.angle = 0; this.pos = new THREE.Vector3(); this.look = new THREE.Vector3(); this.fov = 70;
    this.tvPos = new THREE.Vector3(); this.tvInit = false;
    this.init = false; this.shakeT = 0;
    this.baseFov = 70; this.speedFx = true;
    this._tmp = new THREE.Vector3();
  }
  setMode(id) { this.mode = id; this.init = false; this.tvInit = false; }
  nextMode() { const i = CAMERA_MODES.findIndex((m) => m.id === this.mode); this.setMode(CAMERA_MODES[(i + 1) % CAMERA_MODES.length].id); return this.mode; }

  update(dt, car, shake = 0) {
    const cam = this.camera;
    const P = this._tmp.set(car.x, 0, -car.z).clone();
    const fwdAng = Math.atan2(Math.sin(car.h), -Math.cos(car.h));       // направление носа в плоскости three (x, z)
    const speed = car.speed, sf = clamp(speed / 40, 0, 1);
    let desiredFov = this.baseFov + (this.speedFx ? sf * 13 : 0);
    // направление скорости
    let velAng = fwdAng;
    if (speed > 4 && car.fwdSpeed > 0) velAng = Math.atan2(car.vx, -car.vz);
    if (!this.init) { this.angle = fwdAng; this.init = true; this.pos.copy(P); this.look.copy(P); }
    const k = (1 - Math.exp(-dt * 8));
    this.shakeT += dt;
    const sh = shake * 0.25 + sf * 0.012;
    const shx = (Math.sin(this.shakeT * 53) + Math.sin(this.shakeT * 31)) * sh, shy = Math.sin(this.shakeT * 47) * sh;

    if (this.mode === 'chase' || this.mode === 'far') {
      const far = this.mode === 'far';
      // камера следует за смесью направления носа и скорости
      const blend = clamp((speed - 5) / 14, 0, 1) * 0.6;
      const target = fwdAng + angDiff(fwdAng, velAng) * blend;
      this.angle += angDiff(this.angle, target) * (1 - Math.exp(-dt * (far ? 2.6 : 3.4)));
      const dist = (far ? 10.5 : 6.0) + sf * (far ? 3 : 1.6), height = far ? 4.2 : 2.0 + sf * 0.3;
      const dx = Math.sin(this.angle), dz = Math.cos(this.angle);
      const want = new THREE.Vector3(P.x - dx * dist, height, P.z - dz * dist);
      this.pos.lerp(want, k);
      const lookWant = new THREE.Vector3(P.x + dx * (far ? 3 : 4.5), 0.9, P.z + dz * (far ? 3 : 4.5));
      this.look.lerp(lookWant, 1 - Math.exp(-dt * 12));
      cam.position.set(this.pos.x + shx, this.pos.y + shy, this.pos.z);
      cam.lookAt(this.look);
    } else if (this.mode === 'hood' || this.mode === 'bumper') {
      const hood = this.mode === 'hood';
      const f = [Math.sin(car.h), -Math.cos(car.h)];
      const off = hood ? 0.4 : 2.0, h = hood ? 1.12 : 0.55;
      cam.position.set(P.x + f[0] * off + shx, h + shy, P.z + f[1] * off);
      cam.lookAt(P.x + f[0] * 30, h - 0.1, P.z + f[1] * 30);
      cam.rotateZ(-car.roll * 0.8);
      desiredFov = this.baseFov + 6 + (this.speedFx ? sf * 14 : 0);
    } else if (this.mode === 'top') {
      const hgt = 34 + sf * 22;
      const want = new THREE.Vector3(P.x + car.vx * 0.35, hgt, P.z - car.vz * 0.35 + 0.01);
      if (!this.init) this.pos.copy(want);
      this.pos.lerp(want, k * 0.7);
      cam.position.copy(this.pos); cam.up.set(0, 0, -1); cam.lookAt(this.pos.x, 0, this.pos.z - 0.001); cam.up.set(0, 1, 0);
      desiredFov = 55;
    } else if (this.mode === 'tv') {
      // телекамера: стоит у трассы и поворачивается вслед, перелетает при уходе далеко
      const d = Math.hypot(this.tvPos.x - P.x, this.tvPos.z - P.z);
      if (!this.tvInit || d > 55 || d < 4) {
        const dir = new THREE.Vector3(Math.sin(velAng), 0, Math.cos(velAng));
        const side = new THREE.Vector3(-dir.z, 0, dir.x);
        this.tvPos.set(P.x + dir.x * 30 + side.x * 13, 3.2, P.z + dir.z * 30 + side.z * 13);
        this.tvInit = true;
      }
      cam.position.copy(this.tvPos);
      this.look.lerp(new THREE.Vector3(P.x, 0.8, P.z), 1 - Math.exp(-dt * 7));
      cam.lookAt(this.look);
      desiredFov = clamp(30 + Math.hypot(this.tvPos.x - P.x, this.tvPos.z - P.z) * -0.2 + 22, 24, 55);
    }
    this.fov += (desiredFov - this.fov) * (1 - Math.exp(-dt * 5));
    if (Math.abs(cam.fov - this.fov) > 0.01) { cam.fov = this.fov; cam.updateProjectionMatrix(); }
  }
}
