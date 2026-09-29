import * as THREE from 'three';
import { allRenderModels } from '../config/cars.js';
import { buildInstanceGeometries, createInstanceMaterial, lampPoints } from './CarFactory.js';

/**
 * Рендер всех «чужих» машин (трафик, припаркованные во дворах, препятствия на автодроме,
 * ДПС) через InstancedMesh: 2 уровня LOD на модель. Каждый кадр машины собираются
 * заново — begin() → add() ×N → end(). Модели без экземпляров не рисуются вовсе.
 * Draw calls ≈ число видимых моделей × LOD (обычно 10–16) независимо от количества машин.
 */
export class CarInstancer {
  constructor(scene, quality, T, capacityPerModel = 40) {
    this.q = quality;
    this.models = allRenderModels();
    this.index = Object.fromEntries(this.models.map((m, i) => [m.key, i]));
    this.material = createInstanceMaterial();
    const cap = capacityPerModel;
    const mk = (geo, name) => {
      const im = new THREE.InstancedMesh(geo, this.material, cap);
      im.name = name;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      im.setColorAt(0, new THREE.Color(1, 1, 1));
      im.instanceColor.setUsage(THREE.DynamicDrawUsage);
      im.frustumCulled = false;
      im.count = 0;
      im.visible = false;
      scene.add(im);
      return im;
    };
    const shadows = quality.shadows && quality.name === 'high';
    this.meshes = this.models.map((m) => {
      const g = buildInstanceGeometries(m);
      const lod0 = mk(g.lod0, `Car_${m.key}_LOD0`);
      lod0.castShadow = shadows;
      return { lod0, lod1: mk(g.lod1, `Car_${m.key}_LOD1`), n0: 0, n1: 0, lamps: lampPoints(m) };
    });
    this.cap = cap;

    const blobGeo = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
    this.blobs = new THREE.InstancedMesh(blobGeo, new THREE.MeshBasicMaterial({
      map: T.blob, transparent: true, depthWrite: false, opacity: 0.8,
      polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2,
    }), cap * 6);
    this.blobs.frustumCulled = false;
    this.blobs.count = 0;
    this.blobs.renderOrder = 1;
    scene.add(this.blobs);

    const beamGeo = new THREE.PlaneGeometry(4, 11).rotateX(-Math.PI / 2).translate(0, 0.035, 8);
    this.beams = new THREE.InstancedMesh(beamGeo, new THREE.MeshBasicMaterial({
      map: T.glow, color: 0xfff0c8, transparent: true, opacity: 0.3, depthWrite: false,
      blending: THREE.AdditiveBlending, toneMapped: false,
    }), cap * 4);
    this.beams.frustumCulled = false;
    this.beams.count = 0;
    this.beams.renderOrder = 2;
    scene.add(this.beams);

    this._m = new THREE.Matrix4();
    this._q = new THREE.Quaternion();
    this._p = new THREE.Vector3();
    this._s = new THREE.Vector3(1, 1, 1);
    this._bs = new THREE.Vector3();
    this._up = new THREE.Vector3(0, 1, 0);
    this._c = new THREE.Color();
    this.frustum = new THREE.Frustum();
    this._pm = new THREE.Matrix4();
    this._sphere = new THREE.Sphere(new THREE.Vector3(), 3);
  }

  modelIndex(key) { return this.index[key] ?? 0; }

  begin(camera, night) {
    for (const m of this.meshes) { m.n0 = 0; m.n1 = 0; }
    this.nb = 0;
    this.nBeams = 0;
    this.night = night;
    this.cam = camera.position;
    camera.updateMatrixWorld();
    this._pm.multiplyMatrices(camera.projectionMatrix, camera.matrixWorldInverse);
    this.frustum.setFromProjectionMatrix(this._pm);
  }

  /**
   * Добавить машину. car: {model, x, y, z, heading, color(THREE.Color), braking, lights, ind('L'|'R'|null|'H')}
   * glow: GlowPoints для ламп (может быть null).
   */
  add(car, glow, blink) {
    const mi = car.model;
    const M = this.meshes[mi];
    const dx = car.x - this.cam.x, dz = car.z - this.cam.z;
    const d2 = dx * dx + dz * dz;
    if (d2 > this.q.drawDistance * this.q.drawDistance) return;
    this._sphere.center.set(car.x, 1, car.z);
    const visible = this.frustum.intersectsSphere(this._sphere);
    const y = car.y ?? 0;
    this._q.setFromAxisAngle(this._up, car.heading);
    // тень рисуем даже вне кадра? нет — только видимые
    if (!visible) return;
    this._m.compose(this._p.set(car.x, y, car.z), this._q, this._s);
    if (d2 < this.q.carLodDistance * this.q.carLodDistance) {
      if (M.n0 < this.cap) { M.lod0.setMatrixAt(M.n0, this._m); M.lod0.setColorAt(M.n0, car.color); M.n0++; }
    } else if (M.n1 < this.cap) { M.lod1.setMatrixAt(M.n1, this._m); M.lod1.setColorAt(M.n1, car.color); M.n1++; }

    if (this.nb < this.blobs.instanceMatrix.count) {
      const def = this.models[mi].dims;
      this._bs.set(def.W + 0.5, 1, def.front - def.rear + 0.6);
      this._m.compose(this._p.set(car.x, y + 0.02, car.z), this._q, this._bs);
      this.blobs.setMatrixAt(this.nb++, this._m);
    }
    const lit = car.lights ?? this.night > 0.3;
    if (lit && this.night > 0.25 && d2 < 150 * 150 && this.nBeams < this.beams.instanceMatrix.count) {
      this._m.compose(this._p.set(car.x, y, car.z), this._q, this._s);
      this.beams.setMatrixAt(this.nBeams++, this._m);
    }
    if (glow) this._lamps(car, M.lamps, glow, lit, blink, d2);
  }

  _lamps(car, L, glow, lit, blink, d2) {
    const s = Math.sin(car.heading), c = Math.cos(car.heading);
    const y0 = car.y ?? 0;
    const put = (p, r, g, b, size) => {
      glow.add(car.x + p[0] * c + p[2] * s, y0 + p[1], car.z - p[0] * s + p[2] * c, r, g, b, size);
    };
    const far = d2 > 60 * 60 ? 1.6 : 1;
    if (lit) for (const p of L.head) put(p, 1.0, 0.92, 0.75, 0.9 * far);
    const brake = car.braking;
    if (lit || brake) for (const p of L.tail) put(p, 1.0, 0.08, 0.04, (brake ? 0.9 : 0.5) * far);
    if (car.ind && blink) {
      const sides = car.ind === 'H' ? ['L', 'R'] : [car.ind];
      for (const sd of sides) for (const p of L.ind[sd]) put(p, 1.0, 0.55, 0.05, 0.6);
    }
    for (const e of L.extra) {
      const on = blink ? e.c === 'blue' : e.c === 'red';
      if (car.flashers && on) put(e.p, e.c === 'blue' ? 0.1 : 1, 0.15, e.c === 'blue' ? 1 : 0.1, 1.6);
    }
  }

  end() {
    for (const M of this.meshes) {
      M.lod0.count = M.n0; M.lod1.count = M.n1;
      M.lod0.visible = M.n0 > 0; M.lod1.visible = M.n1 > 0;
      if (M.n0) { M.lod0.instanceMatrix.needsUpdate = true; M.lod0.instanceColor.needsUpdate = true; }
      if (M.n1) { M.lod1.instanceMatrix.needsUpdate = true; M.lod1.instanceColor.needsUpdate = true; }
    }
    this.blobs.count = this.nb;
    this.blobs.instanceMatrix.needsUpdate = true;
    this.beams.count = this.nBeams;
    this.beams.visible = this.nBeams > 0;
    this.beams.material.opacity = this.night * 0.35;
    this.beams.instanceMatrix.needsUpdate = true;
  }
}
