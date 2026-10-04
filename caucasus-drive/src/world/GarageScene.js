import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { buildPlayerModel, createCarMaterials, buildWheels } from '../vehicles/CarFactory.js';
import { attachPlates, attachNeon, plateTexture, platePreviewTexture } from '../vehicles/Extras.js';

/**
 * 3D-гараж для меню: кирпичные стены ГСК, лампы дневного света, стеллаж с шинами,
 * поворотный круг с машиной. Вращение пальцем, мгновенный предпросмотр тюнинга.
 */
export class GarageScene {
  constructor(renderer, T, quality) {
    this.renderer = renderer;
    this.T = T;
    this.q = quality;
    const s = new THREE.Scene();
    this.scene = s;
    s.background = new THREE.Color(0x14161a);
    s.fog = new THREE.Fog(0x14161a, 14, 30);
    const pm = new THREE.PMREMGenerator(renderer);
    s.environment = pm.fromScene(new RoomEnvironment(), 0.04).texture;
    pm.dispose();
    s.environmentIntensity = 0.55;

    this.camera = new THREE.PerspectiveCamera(40, 1, 0.1, 60);
    s.add(new THREE.HemisphereLight(0xcfd8e6, 0x2a2420, 0.9));
    const key = new THREE.SpotLight(0xfff2de, 180, 20, 0.7, 0.6, 1.6);
    key.position.set(3, 6, 4);
    key.castShadow = quality.shadows;
    key.shadow.mapSize.set(1024, 1024);
    key.shadow.bias = -0.0005;
    s.add(key, key.target);
    const rim = new THREE.SpotLight(0xa8c4ff, 90, 20, 0.8, 0.7, 1.6);
    rim.position.set(-4, 4, -5);
    s.add(rim, rim.target);

    // пол (бетон с «мокрым» блеском) и стены
    const floor = new THREE.Mesh(new THREE.CircleGeometry(16, 48).rotateX(-Math.PI / 2),
      new THREE.MeshStandardMaterial({ map: T.concrete, color: 0x9a9a9a, roughness: 0.45, metalness: 0.1 }));
    T.concrete.repeat?.set(1, 1);
    floor.receiveShadow = true;
    s.add(floor);
    const brick = T.brick.clone();
    brick.repeat.set(6, 2);
    brick.wrapS = brick.wrapT = THREE.RepeatWrapping;
    brick.needsUpdate = true;
    const wallMat = new THREE.MeshStandardMaterial({ map: brick, roughness: 0.9 });
    const back = new THREE.Mesh(new THREE.PlaneGeometry(18, 6), wallMat);
    back.position.set(0, 3, -6);
    s.add(back);
    for (const sx of [-1, 1]) {
      const side = new THREE.Mesh(new THREE.PlaneGeometry(14, 6), wallMat);
      side.position.set(sx * 7, 3, 0);
      side.rotation.y = -sx * Math.PI / 2;
      s.add(side);
    }
    // поворотный круг
    const disc = new THREE.Mesh(new THREE.CylinderGeometry(3.4, 3.5, 0.12, 48),
      new THREE.MeshStandardMaterial({ color: 0x2a2d31, metalness: 0.6, roughness: 0.35 }));
    disc.position.y = 0.06;
    disc.receiveShadow = true;
    s.add(disc);
    const ring = new THREE.Mesh(new THREE.TorusGeometry(3.45, 0.03, 6, 64).rotateX(Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xff6a1a, toneMapped: false }));
    ring.position.y = 0.12;
    s.add(ring);
    // лампы дневного света
    for (const x of [-2.5, 2.5]) {
      const lamp = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.08, 3.2), new THREE.MeshBasicMaterial({ color: 0xf4f8ff, toneMapped: false }));
      lamp.position.set(x, 5.6, 0);
      s.add(lamp);
    }
    // стеллаж с шинами и канистрами
    const shelfMat = new THREE.MeshStandardMaterial({ color: 0x3d4a5a, roughness: 0.7, metalness: 0.3 });
    for (let k = 0; k < 3; k++) {
      const shelf = new THREE.Mesh(new THREE.BoxGeometry(4, 0.06, 0.8), shelfMat);
      shelf.position.set(-4.4, 0.6 + k * 1.1, -5.4);
      s.add(shelf);
    }
    const tireMat = new THREE.MeshStandardMaterial({ color: 0x151515, roughness: 0.9 });
    for (let k = 0; k < 4; k++) {
      const t = new THREE.Mesh(new THREE.TorusGeometry(0.28, 0.1, 8, 18), tireMat);
      t.position.set(-5.8 + k * 0.62, 0.95, -5.4);
      s.add(t);
    }
    const canMat = new THREE.MeshStandardMaterial({ color: 0xb71c1c, roughness: 0.5 });
    for (let k = 0; k < 3; k++) {
      const c = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.45, 0.2), canMat);
      c.position.set(-3.4 + k * 0.4, 1.95, -5.4);
      s.add(c);
    }
    // верстак и плакат
    const bench = new THREE.Mesh(new THREE.BoxGeometry(2.6, 0.9, 0.8), new THREE.MeshStandardMaterial({ color: 0x5a4030, roughness: 0.8 }));
    bench.position.set(4.3, 0.45, -5.4);
    s.add(bench);
    const poster = new THREE.Mesh(new THREE.PlaneGeometry(2.4, 1.2), new THREE.MeshBasicMaterial({ map: T.ads }));
    poster.geometry.attributes.uv.array.set([0.5, 1, 1, 1, 0.5, 0.5, 1, 0.5]);
    poster.position.set(3.6, 3.2, -5.95);
    s.add(poster);

    this.turntable = new THREE.Group();
    this.turntable.position.y = 0.12;
    s.add(this.turntable);
    this.angle = 0.6;
    this.dragVel = 0;
    this.dist = 7.2;
    this._bindDrag();
  }

  _bindDrag() {
    const el = this.renderer.domElement;
    let id = null, lx = 0;
    el.addEventListener('pointerdown', (e) => { if (!this.active) return; id = e.pointerId; lx = e.clientX; });
    el.addEventListener('pointermove', (e) => {
      if (e.pointerId !== id) return;
      this.dragVel = (e.clientX - lx) * 0.01;
      this.angle += this.dragVel;
      lx = e.clientX;
    });
    const up = (e) => { if (e.pointerId === id) id = null; };
    el.addEventListener('pointerup', up);
    el.addEventListener('pointercancel', up);
  }

  showCar(def, tv) {
    if (this.model) {
      this.turntable.remove(this.model.root);
      this.model.root.traverse((o) => { if (o.isMesh) o.geometry.dispose(); });
      Object.values(this.mats).forEach((m) => m.dispose());
    }
    this.def = def;
    this.mats = createCarMaterials({ color: tv.color, envMap: null, T: this.T, quality: { ...this.q, name: 'high', transparentGlass: true }, tint: tv.tint });
    this.model = buildPlayerModel(def, this.mats, tv.wheels);
    this.model.root.traverse((o) => { if (o.isMesh) o.castShadow = true; });
    this.model.body.position.y = tv.height;
    this.model.root.position.z = -(def.dims.front + def.dims.rear) / 2;
    this.turntable.add(this.model.root);
    this.setExtras(tv);
  }

  /** Номера и неон (меняются в тюнинге без пересборки модели). */
  setExtras(tv) {
    if (!this.model) return;
    for (const n of ['Plates', 'Neon']) { const o = this.model.root.getObjectByName(n); o?.parent.remove(o); }
    attachPlates(this.model.body, tv.plate);
    const neon = attachNeon(this.model.root, this.def, tv.neon);
    if (neon) neon.userData.glowMat.opacity = 0.9;
  }

  /** Номер без пересборки: preview — черновик из редактора (одна общая текстура), иначе — купленный. */
  setPlate(plate, preview = false) {
    const grp = this.model?.root.getObjectByName('Plates');
    if (!grp || !plate) return;
    const tex = preview ? platePreviewTexture(plate.text, plate.region) : plateTexture(plate.text, plate.region);
    for (const m of grp.children) m.material.map = tex; // map → map: шейдер тот же
  }

  setColor(hex) { this.mats?.paint.color.setHex(hex); }
  setTint(v) { if (this.mats) this.mats.glass.opacity = v; }
  setHeight(v) { if (this.model) this.model.body.position.y = v; }
  setWheels(style) {
    if (!this.model) return;
    for (const w of this.model.wheels) this.model.root.remove(w.pivot);
    this.model.wheels = buildWheels(this.def, this.mats, style);
    for (const w of this.model.wheels) { w.pivot.traverse((o) => { if (o.isMesh) o.castShadow = true; }); this.model.root.add(w.pivot); }
  }

  /** Сдвиг кадра: машина чуть правее центра, если слева панель. */
  setFraming(offsetX = 0) { this.offsetX = offsetX; }

  render(dt) {
    const w = this.renderer.domElement.clientWidth || innerWidth, h = this.renderer.domElement.clientHeight || innerHeight;
    if (this.camera.aspect !== w / h) { this.camera.aspect = w / h; this.camera.updateProjectionMatrix(); }
    this.dragVel *= Math.exp(-dt * 3);
    this.angle += dt * 0.25 + this.dragVel * 0.02;
    this.turntable.rotation.y = this.angle;
    const t = performance.now() / 1000;
    this.camera.position.set(Math.sin(t * 0.05) * 1.2 + (this.offsetX || 0), 2.1 + Math.sin(t * 0.3) * 0.08, this.dist);
    this.camera.lookAt(this.offsetX || 0, 0.75, 0);
    this.renderer.render(this.scene, this.camera);
  }
}
