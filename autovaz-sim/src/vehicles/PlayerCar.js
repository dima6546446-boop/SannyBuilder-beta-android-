import * as THREE from 'three';
import { VehiclePhysics, LADA_2107_SPEC } from './VehiclePhysics.js';
import { buildPlayerCar, createPlayerMaterials, LADA_2107_DIMS } from './LadaModel.js';
import { SkidMarks } from './SkidMarks.js';
import { clamp, damp } from '../utils/math.js';

const D = LADA_2107_DIMS;
// окружности-коллайдеры вдоль кузова (смещение по длине, радиус)
const CIRCLES = [[1.35, 0.8], [0, 0.82], [-1.45, 0.8]];

/**
 * Автомобиль игрока: физика + визуал (колёса, крен кузова, фары, стоп-сигналы)
 * + столкновения со статикой и трафиком.
 */
export class PlayerCar {
  constructor(scene, { T, envMap, quality, color }) {
    this.q = quality;
    this.physics = new VehiclePhysics(LADA_2107_SPEC);
    this.mats = createPlayerMaterials(color, { envMap, transparentGlass: quality.transparentGlass, T });
    const { root, body, wheels } = buildPlayerCar(this.mats);
    this.root = root;
    this.body = body;
    this.wheels = wheels;
    scene.add(root);

    // Одна SpotLight на обе фары: каждая доп. лампа удорожает шейдер ВСЕХ освещаемых
    // материалов. Свет всегда в сцене (intensity=0 днём) — без перекомпиляции шейдеров.
    this.headlight = new THREE.SpotLight(0xfff1d6, 0, 70, 0.62, 0.55, 1.4);
    this.headlight.position.set(0, 0.7, D.front - 0.1);
    this.headlight.target.position.set(0, 0, D.front + 18);
    root.add(this.headlight, this.headlight.target);

    // светящиеся пятна фар на дороге (additive, дёшево и заметно на слабых GPU)
    const beam = new THREE.Mesh(
      new THREE.PlaneGeometry(4.5, 14).rotateX(-Math.PI / 2).translate(0, 0.04, D.front + 8),
      new THREE.MeshBasicMaterial({
        map: T.glow, color: 0xfff0cc, transparent: true, opacity: 0, depthWrite: false,
        blending: THREE.AdditiveBlending, toneMapped: false,
      }),
    );
    beam.renderOrder = 2;
    this.beam = beam;
    root.add(beam);

    if (!quality.shadows) {
      const blob = new THREE.Mesh(
        new THREE.PlaneGeometry(2.1, 4.8).rotateX(-Math.PI / 2).translate(0, 0.03, -0.1),
        new THREE.MeshBasicMaterial({ map: T.blob, transparent: true, depthWrite: false, opacity: 0.8 }),
      );
      root.add(blob);
    }

    this.skids = new SkidMarks(scene, quality.name === 'low' ? 250 : 500);
    this.lightsMode = 'auto'; // auto | on | off
    this.lightsOn = false;
    this.surface = { y: 0, mu: 1, type: 0 };
    this.y = 0;
    this.impact = 0;
    this._contactCooldown = 0;
    this.onCrash = null;
    this._v = new THREE.Vector3();
  }

  get position() { return this.root.position; }

  place(x, z, heading) {
    this.physics.reset(x, z, heading);
    this.root.position.set(x, 0, z);
    this.root.rotation.y = heading;
  }

  setColor(hex) { this.mats.paint.color.setHex(hex); }

  cycleLights() {
    this.lightsMode = this.lightsMode === 'auto' ? 'on' : this.lightsMode === 'on' ? 'off' : 'auto';
    return this.lightsMode;
  }

  update(dt, input, city, collision, traffic, night) {
    const p = this.physics;
    city.surface(p.x, p.z, this.surface);
    p.update(dt, input, this.surface);

    this._collideStatic(collision);
    this._collideTraffic(traffic);

    // --- визуал ---
    this.y = damp(this.y, this.surface.y, 18, dt);
    this.root.position.set(p.x, this.y, p.z);
    this.root.rotation.y = p.heading;
    this.body.rotation.z = clamp(-p.ayLat * 0.011, -0.07, 0.07);
    this.body.rotation.x = clamp(-p.axLong * 0.009, -0.05, 0.05);

    const spin = (p.vLong / D.wheelR) * dt;
    for (const w of this.wheels) {
      w.spin.rotation.x += w.front || !input.handbrake ? spin : 0;
      if (!w.front && p.wheelSpin > 0.1 && p.load > 0.5) w.spin.rotation.x += 0.6 * Math.sign(p.vLong || 1);
      if (w.front) w.pivot.rotation.y = -p.steer;
    }

    // --- свет ---
    this.lightsOn = this.lightsMode === 'on' || (this.lightsMode === 'auto' && night > 0.3);
    const L = this.lightsOn ? 1 : 0;
    this.headlight.intensity = L * 90;
    this.mats.headLamp.emissiveIntensity = L * 2.2;
    this.beam.material.opacity = L * (0.18 + night * 0.25);
    this.beam.visible = L > 0;
    this.mats.tailLamp.emissiveIntensity = p.braking ? 3.0 : L ? 0.9 : 0.05;
    this.mats.reverseLamp.emissiveIntensity = p.reversing ? 2.0 : 0;
    const env = 1 - night * 0.8;
    this.mats.paint.envMapIntensity = env;
    this.mats.chrome.envMapIntensity = env * 1.2;
    this.mats.glass.envMapIntensity = env * 1.4;

    // --- следы шин (задние колёса) ---
    const skid = p.skid > 0.35 || (input.handbrake && p.speed > 3);
    const fx = Math.sin(p.heading), fz = Math.cos(p.heading);
    const rx = -fz, rz = fx;
    for (let i = 0; i < 2; i++) {
      const side = i === 0 ? 1 : -1;
      const wx = p.x + fx * D.rearAxle + rx * side * (D.track / 2);
      const wz = p.z + fz * D.rearAxle + rz * side * (D.track / 2);
      this.skids.add(i, wx, this.y, wz, skid && this.surface.type !== 2);
    }
    this._contactCooldown -= dt;
  }

  _worldCircle(i) {
    const p = this.physics;
    const [off, r] = CIRCLES[i];
    return [p.x + Math.sin(p.heading) * off, p.z + Math.cos(p.heading) * off, r, off];
  }

  /**
   * Импульсный отклик: выталкиваем, отражаем нормальную составляющую относительной
   * скорости (ovx/ovz — скорость препятствия), закручиваем по рысканию.
   */
  _applyContact(nx, nz, depth, off, restitution = 0.25, ovx = 0, ovz = 0) {
    const p = this.physics;
    p.x += nx * depth;
    p.z += nz * depth;
    const vn = (p.vx - ovx) * nx + (p.vz - ovz) * nz;
    if (vn >= 0) return 0;
    const j = -(1 + restitution) * vn;
    p.vx += nx * j;
    p.vz += nz * j;
    p.vx *= 0.97; p.vz *= 0.97; // трение по касательной
    // Δω = (m/I)·(r × n)·j, r — плечо точки контакта от центра масс
    const rX = Math.sin(p.heading) * off, rZ = Math.cos(p.heading) * off;
    p.yawRate = clamp(p.yawRate + (rZ * nx - rX * nz) * j * 0.2, -4, 4);
    return -vn;
  }

  _collideStatic(collision) {
    let maxImpact = 0;
    for (let i = 0; i < CIRCLES.length; i++) {
      const [x, z, r, off] = this._worldCircle(i);
      collision.collideCircle(x, z, r, (nx, nz, depth) => {
        maxImpact = Math.max(maxImpact, this._applyContact(nx, nz, depth, off));
      });
    }
    this._reportImpact(maxImpact);
  }

  _collideTraffic(traffic) {
    if (!traffic) return;
    let maxImpact = 0;
    const p = this.physics;
    for (const car of traffic.cars) {
      const dx0 = car.x - p.x, dz0 = car.z - p.z;
      if (dx0 * dx0 + dz0 * dz0 > 49) continue;
      const cfx = Math.sin(car.heading), cfz = Math.cos(car.heading);
      for (let i = 0; i < CIRCLES.length; i++) {
        const [x, z, r, off] = this._worldCircle(i);
        for (let k = 0; k < CIRCLES.length; k++) {
          const ox = car.x + cfx * CIRCLES[k][0], oz = car.z + cfz * CIRCLES[k][0];
          const dx = x - ox, dz = z - oz;
          const rs = r + CIRCLES[k][1];
          const d2 = dx * dx + dz * dz;
          if (d2 >= rs * rs || d2 < 1e-6) continue;
          const d = Math.sqrt(d2);
          const imp = this._applyContact(dx / d, dz / d, (rs - d) * 0.6, off, 0.2, car.vx, car.vz);
          if (imp > 0) { car.bump(imp); maxImpact = Math.max(maxImpact, imp); }
        }
      }
    }
    this._reportImpact(maxImpact);
  }

  _reportImpact(v) {
    if (v > 1.5 && this._contactCooldown <= 0) {
      this._contactCooldown = 0.25;
      this.onCrash?.(Math.min(1, v / 14));
    }
  }
}
