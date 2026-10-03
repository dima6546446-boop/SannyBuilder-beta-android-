import * as THREE from 'three';
import { VehiclePhysics, makeSpec } from './VehiclePhysics.js';
import { buildPlayerModel, createCarMaterials, buildWheels, roofY } from './CarFactory.js';
import { SkidMarks } from './SkidMarks.js';
import { clamp, damp } from '../utils/math.js';

/**
 * Автомобиль игрока: физика + визуал (колёса, крен, свет, поворотники, дым, следы)
 * + столкновения (окружности вдоль кузова точно повторяют габарит — важно для парковки).
 */
export class PlayerCar {
  constructor(scene, { T, quality, smoke }) {
    this.scene = scene;
    this.T = T;
    this.q = quality;
    this.smoke = smoke;
    this.root = new THREE.Group();
    this.root.name = 'PlayerCar';
    scene.add(this.root);

    this.headlight = new THREE.SpotLight(0xfff1d6, 0, 70, 0.62, 0.55, 1.4);
    this.headlight.target.position.set(0, 0, 20);
    this.root.add(this.headlight, this.headlight.target);

    this.beam = new THREE.Mesh(
      new THREE.PlaneGeometry(4.5, 14).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({
        map: T.glow, color: 0xfff0cc, transparent: true, opacity: 0, depthWrite: false,
        blending: THREE.AdditiveBlending, toneMapped: false,
      }),
    );
    this.beam.renderOrder = 2;
    this.root.add(this.beam);

    this.blob = new THREE.Mesh(
      new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ map: T.blob, transparent: true, depthWrite: false, opacity: quality.shadows ? 0.45 : 0.85 }),
    );
    this.blob.renderOrder = 1;
    this.root.add(this.blob);

    this.skids = new SkidMarks(scene, quality.name === 'low' ? 250 : 600);
    this.lightsMode = 'auto';
    this.lightsOn = false;
    this.indicator = null;      // 'L' | 'R' | 'H' (аварийка) | null
    this.blinkT = 0;
    this.blinkOn = false;
    this.lastIndicatorTime = { L: -99, R: -99 };
    this.time = 0;
    this.surface = { y: 0, mu: 1, type: 0 };
    this.y = 0;
    this._cool = 0;
    this.onCrash = null;
    this.onContact = null;
    this.physics = null;
    this.taxiSign = null;
  }

  get position() { return this.root.position; }

  /** Установить модель и тюнинг (цвет, диски, высота, тонировка, мотор, шины). */
  setCar(def, tv) {
    this.def = def;
    this.tv = tv;
    const spec = makeSpec(def, tv);
    if (!this.physics) this.physics = new VehiclePhysics(spec);
    else this.physics.setSpec(spec);

    if (this.model) {
      this.root.remove(this.model.root);
      this.model.root.traverse((o) => { if (o.isMesh) o.geometry.dispose(); });
    }
    if (this.mats) Object.values(this.mats).forEach((m) => m.dispose());
    this.mats = createCarMaterials({ color: tv.color, envMap: null, T: this.T, quality: this.q, tint: tv.tint });
    this.model = buildPlayerModel(def, this.mats, tv.wheels, { driver: true });
    this.interior = this.model.interior;
    this.interior.setDriverVisible(this.driverVisible !== false);
    this.root.add(this.model.root);
    this.body = this.model.body;
    this.wheels = this.model.wheels;
    this.body.position.y = tv.height;

    const d = def.dims;
    this.headlight.position.set(0, def.head.y, d.front - 0.1);
    this.headlight.target.position.set(0, 0, d.front + 18);
    this.beam.position.set(0, 0.04, d.front + 8);
    this.blob.scale.set(d.W + 0.6, 1, d.front - d.rear + 0.8);
    this.blob.position.set(0, 0.025, (d.front + d.rear) / 2);

    // окружности-коллайдеры: от бампера до бампера, радиус ≈ полуширина
    const r = d.W / 2 * 0.96;
    const zF = d.front - r + 0.05, zR = d.rear + r - 0.05;
    const n = 5;
    this.circles = [];
    for (let i = 0; i < n; i++) this.circles.push([zR + (zF - zR) * (i / (n - 1)), r]);
    this.half = { front: d.front + 0.05, rear: d.rear - 0.05, w: d.W / 2 + 0.02 };
    this.setTaxiSign(!!this.taxiSign);
  }

  /** Водитель в салоне (прячется, когда игрок вышел пешком). */
  setDriverVisible(v) { this.driverVisible = v; this.interior?.setDriverVisible(v); }

  setColor(hex) { this.mats.paint.color.setHex(hex); }

  setTint(opacity) { this.mats.glass.opacity = opacity; }

  setWheels(style) {
    for (const w of this.wheels) {
      this.model.root.remove(w.pivot);
      w.pivot.traverse((o) => { if (o.isMesh) o.geometry.dispose(); });
    }
    this.model.wheels = this.wheels = buildWheels(this.def, this.mats, style);
    for (const w of this.wheels) this.model.root.add(w.pivot);
  }

  setTaxiSign(on) {
    if (this.taxiSign) { this.body.remove(this.taxiSign); this.taxiSign = null; }
    if (!on || !this.body) return;
    const g = new THREE.Group();
    const sign = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.15, 0.2), new THREE.MeshStandardMaterial({ color: 0xffd000, emissive: 0xffb000, emissiveIntensity: 0.4 }));
    g.add(sign);
    for (const s of [1, -1]) for (let k = 0; k < 4; k++) {
      const c = new THREE.Mesh(new THREE.BoxGeometry(0.005, 0.06, 0.1), new THREE.MeshBasicMaterial({ color: 0x111111 }));
      c.position.set(s * 0.253, -0.03 + (k % 2) * 0.06, -0.15 + k * 0.1);
      g.add(c);
    }
    g.position.set(0, roofY(this.def) + 0.08, this.def.cabin[1][0] - 0.4);
    this.body.add(g);
    this.taxiSign = g;
  }

  place(x, z, heading) {
    this.physics.reset(x, z, heading);
    this.root.position.set(x, 0, z);
    this.root.rotation.y = heading;
    this.skids.last = [null, null];
  }

  cycleLights() {
    this.lightsMode = this.lightsMode === 'auto' ? 'on' : this.lightsMode === 'on' ? 'off' : 'auto';
    return this.lightsMode;
  }

  toggleIndicator(side) {
    this.indicator = this.indicator === side ? null : side;
    this.blinkT = 0; this.blinkOn = true;
    return this.indicator;
  }

  toggleHazard() { return this.toggleIndicator('H'); }

  /** Углы кузова в мировых координатах (для проверки парковки). */
  corners(out = []) {
    const p = this.physics, s = Math.sin(p.heading), c = Math.cos(p.heading);
    const pts = [[this.half.w, this.half.front], [-this.half.w, this.half.front], [-this.half.w, this.half.rear], [this.half.w, this.half.rear]];
    out.length = 0;
    for (const [lx, lz] of pts) out.push([p.x + lx * c + lz * s, p.z - lx * s + lz * c]);
    return out;
  }

  /**
   * world: { surface(x,z,out), colliders: CollisionWorld[], traffic: [{x,z,heading,vx,vz,model,bump}], night }
   */
  update(dt, input, world) {
    const p = this.physics;
    this.time += dt;
    world.surface(p.x, p.z, this.surface);
    p.update(dt, input, this.surface);

    this._collide(world);

    // поворотники: 1.4 Гц, щелчок реле
    if (this.indicator) {
      this.blinkT += dt;
      const on = (this.blinkT % 0.7) < 0.35;
      if (on !== this.blinkOn) { this.blinkOn = on; this.onBlink?.(on); }
      if (this.indicator !== 'H') this.lastIndicatorTime[this.indicator] = this.time;
      // самосброс после поворота (как в настоящей машине)
      if (this.indicator !== 'H') {
        const turned = Math.abs(p.steer) > 0.25;
        if (turned) this._wasTurning = true;
        else if (this._wasTurning && Math.abs(p.steer) < 0.05 && p.speed > 3) { this._wasTurning = false; this.indicator = null; }
      }
    } else { this.blinkOn = false; this._wasTurning = false; }

    // --- визуал
    this.y = damp(this.y, this.surface.y, 18, dt);
    this.root.position.set(p.x, this.y, p.z);
    this.root.rotation.y = p.heading;
    this.body.rotation.z = clamp(-p.ayLat * 0.011, -0.07, 0.07);
    this.body.rotation.x = clamp(-p.axLong * 0.009, -0.05, 0.05);
    const R = this.def.dims.wheelR;
    const spin = (p.vLong / R) * dt;
    for (const w of this.wheels) {
      w.spin.rotation.x += w.front || !input.handbrake ? spin : 0;
      const driven = p.spec.drive === 'AWD' || (p.spec.drive === 'FWD') === w.front;
      if (driven && p.wheelSpin > 0.1 && p.load > 0.5) w.spin.rotation.x += 0.6 * Math.sign(p.vLong || 1);
      if (w.front) w.pivot.rotation.y = -p.steer;
    }

    // --- салон: руль, стрелки приборов, руки водителя
    this.interior?.update(dt, {
      steer: p.steer / (p.spec.maxSteer || 0.6), kmh: Math.abs(p.speed) * 3.6, rpm: p.rpm,
      fuel: p.fuel / p.spec.tank, temp: Math.min(1, 0.25 + this.time / 240) * 0.5, lights: this.lightsOn, time: world.clock ?? 12, daylight: 1 - (world.night ?? 0),
    });

    // --- свет
    const night = world.night;
    this.lightsOn = this.lightsMode === 'on' || (this.lightsMode === 'auto' && night > 0.3);
    const L = this.lightsOn ? 1 : 0;
    this.headlight.intensity = L * 90;
    this.mats.headLamp.emissiveIntensity = L * 2.4;
    this.beam.material.opacity = L * (0.15 + night * 0.25);
    this.beam.visible = L > 0;
    this.mats.tailLamp.emissiveIntensity = p.braking ? 3.2 : L ? 1.0 : 0.05;
    this.mats.reverseLamp.emissiveIntensity = p.reversing ? 2.2 : 0;
    const ind = this.indicator, bo = this.blinkOn;
    this.mats.indL.emissiveIntensity = (ind === 'L' || ind === 'H') && bo ? 3 : 0;
    this.mats.indR.emissiveIntensity = (ind === 'R' || ind === 'H') && bo ? 3 : 0;
    const env = 1 - night * 0.75;
    this.mats.paint.envMapIntensity = env;
    this.mats.chrome.envMapIntensity = env * 1.3;
    this.mats.glass.envMapIntensity = env * 1.8;

    // --- следы и дым с задних (или ведущих передних) колёс
    const sk = p.skid;
    const skid = sk > 0.35 || (input.handbrake && p.speed > 3);
    const fx = Math.sin(p.heading), fz = Math.cos(p.heading);
    const rx = -fz, rz = fx;
    const d = this.def.dims;
    for (let i = 0; i < 2; i++) {
      const side = i === 0 ? 1 : -1;
      const wx = p.x + fx * d.axleR + rx * side * (d.track / 2);
      const wz = p.z + fz * d.axleR + rz * side * (d.track / 2);
      this.skids.add(i, wx, this.y, wz, skid && this.surface.type !== 2);
      if (this.smoke) {
        const grass = this.surface.type === 2 && p.speed > 4;
        if ((sk > 0.45 && this.surface.type !== 2 && Math.random() < sk * 0.9) || (grass && Math.random() < 0.3)) {
          this.smoke.emit(wx, this.y, wz, p.vx, p.vz, grass);
        }
      }
    }
    this._cool -= dt;
  }

  _collide(world) {
    const p = this.physics;
    let maxImpact = 0, hitTag = null;
    const s = Math.sin(p.heading), c = Math.cos(p.heading);
    for (const [off, r] of this.circles) {
      const x = p.x + s * off, z = p.z + c * off;
      for (const col of world.colliders) {
        col.collideCircle(p.x + s * off, p.z + c * off, r, (nx, nz, depth, o) => {
          const imp = this._contact(nx, nz, depth, off, 0.25);
          if (o.tag && this.onContact) this.onContact(o, imp);
          if (imp > maxImpact) { maxImpact = imp; hitTag = o.tag; }
        });
      }
      void x; void z;
    }
    for (const car of world.traffic) {
      const dx0 = car.x - p.x, dz0 = car.z - p.z;
      if (dx0 * dx0 + dz0 * dz0 > 64) continue;
      const cs = Math.sin(car.heading), cc = Math.cos(car.heading);
      for (const [off, r] of this.circles) {
        const x = p.x + s * off, z = p.z + c * off;
        for (const [coff, cr] of this.circles) {
          const ox = car.x + cs * coff, oz = car.z + cc * coff;
          const dx = x - ox, dz = z - oz;
          const rs = r + cr;
          const d2 = dx * dx + dz * dz;
          if (d2 >= rs * rs || d2 < 1e-6) continue;
          const dd = Math.sqrt(d2);
          const imp = this._contact(dx / dd, dz / dd, (rs - dd) * 0.6, off, 0.2, car.vx || 0, car.vz || 0);
          if (imp > 0) {
            car.bump?.(imp);
            if (this.onContact) this.onContact({ tag: 'car', car }, imp);
            if (imp > maxImpact) { maxImpact = imp; hitTag = 'car'; }
          }
        }
      }
    }
    if (maxImpact > 1.2 && this._cool <= 0) {
      this._cool = 0.3;
      this.onCrash?.(Math.min(1, maxImpact / 14), hitTag);
    }
  }

  _contact(nx, nz, depth, off, restitution, ovx = 0, ovz = 0) {
    const p = this.physics;
    p.x += nx * depth;
    p.z += nz * depth;
    const vn = (p.vx - ovx) * nx + (p.vz - ovz) * nz;
    if (vn >= 0) return 0;
    const j = -(1 + restitution) * vn;
    p.vx += nx * j; p.vz += nz * j;
    p.vx *= 0.97; p.vz *= 0.97;
    const rX = Math.sin(p.heading) * off, rZ = Math.cos(p.heading) * off;
    p.yawRate = clamp(p.yawRate + (rZ * nx - rX * nz) * j * 0.2, -4, 4);
    return -vn;
  }
}
