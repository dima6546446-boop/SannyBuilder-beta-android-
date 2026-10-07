import * as THREE from 'three';
import { buildMap } from './mapBuilder.js';
import { createCarModel, syncCarModel, disposeModel } from './carModel.js';
import { Smoke, Skids, Sparks, Weather, smokeColor } from './effects.js';
import { CameraRig } from './camera.js';
import { SURFACES } from '../physics/config.js';

const ENVS = {
  day:   { top: 0x3f86d4, horizon: 0xd2e6f5, sun: 0xfff0d2, sunI: 3.0, sunDir: [-0.55, 0.85, 0.4], hemiSky: 0xcfe2ff, hemiGround: 0x7a7766, hemiI: 1.15, fog: 0xcfe2f1, fogNear: 180, fogFar: 1100, exposure: 0.95, lights: false },
  dusk:  { top: 0x2a3b66, horizon: 0xf2a263, sun: 0xffb877, sunI: 2.3, sunDir: [0.8, 0.28, 0.4], hemiSky: 0x9aa6d8, hemiGround: 0x5a4a45, hemiI: 0.85, fog: 0xc49272, fogNear: 120, fogFar: 800, exposure: 0.9, lights: true },
  night: { top: 0x050914, horizon: 0x1a2744, sun: 0x8aa4ff, sunI: 0.55, sunDir: [-0.4, 0.7, 0.3], hemiSky: 0x3a4d86, hemiGround: 0x1c2029, hemiI: 1.0, fog: 0x0c1428, fogNear: 80, fogFar: 620, exposure: 1.15, lights: true },
};
const WEATHER_MOD = {
  clear: { fogMul: 1, dim: 1 },
  rain: { fogMul: 0.45, dim: 0.62 },
  snow: { fogMul: 0.5, dim: 0.9 },
};
const QUALITY = {
  low:    { shadow: 0, pr: 0.7, aa: false, smoke: 220, env: false },
  medium: { shadow: 1024, pr: 1, aa: true, smoke: 450, env: true },
  high:   { shadow: 2048, pr: 1.5, aa: true, smoke: 700, env: true },
};

export class Gfx {
  constructor(canvas) {
    this.canvas = canvas;
    this.quality = 'high';
    this.settings = { quality: 'high', shadows: true, effects: true, fov: 70, speedFx: true, pixelRatio: 1 };
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance', preserveDrawingBuffer: false });
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.camera = new THREE.PerspectiveCamera(70, 1, 0.1, 2500);
    this.rig = new CameraRig(this.camera);
    this.scene = new THREE.Scene();
    this.mapGroup = null; this.mapData = null; this.carModel = null; this.carLookKey = '';
    this.setupLights();
    this.sky = this.makeSky();
    this.scene.add(this.sky.group);
    this.smoke = new Smoke(700); this.skids = new Skids(); this.sparks = new Sparks(); this.weather = new Weather();
    this.fxGroup = new THREE.Group();
    this.fxGroup.add(this.smoke.points, this.skids.mesh, this.sparks.points, this.weather.group);
    this.scene.add(this.fxGroup);
    this.smokeAcc = [0, 0, 0, 0];
    this.lampLights = []; for (let i = 0; i < 4; i++) { const l = new THREE.PointLight(0xffd9a0, 0, 32, 2); this.scene.add(l); this.lampLights.push(l); }
    this.lampTimer = 0;
    this.gateGroup = new THREE.Group(); this.scene.add(this.gateGroup);
    this.showroom = this.makeShowroom();
    this.mode = 'none';
    this.envKey = '';
    this.t = 0;
    this.time = 'day'; this.weatherKind = 'clear';
    this.carGroupHolder = new THREE.Group(); this.scene.add(this.carGroupHolder);
    this.pmrem = new THREE.PMREMGenerator(this.renderer);
    this.resize();
  }

  setupLights() {
    this.hemi = new THREE.HemisphereLight(0xcfe2ff, 0x777766, 1);
    this.sun = new THREE.DirectionalLight(0xfff0d2, 3);
    this.sun.castShadow = true;
    this.sun.shadow.camera.left = -40; this.sun.shadow.camera.right = 40; this.sun.shadow.camera.top = 40; this.sun.shadow.camera.bottom = -40; this.sun.shadow.camera.near = 1; this.sun.shadow.camera.far = 260;
    this.sun.shadow.bias = -0.0004; this.sun.shadow.normalBias = 0.05;
    this.scene.add(this.hemi, this.sun, this.sun.target);
    this.head = [0, 1].map(() => { const l = new THREE.SpotLight(0xfff1d0, 0, 80, 0.5, 0.55, 1.6); this.scene.add(l); this.scene.add(l.target); return l; });
  }

  makeSky() {
    const group = new THREE.Group();
    const mat = new THREE.ShaderMaterial({
      side: THREE.BackSide, depthWrite: false, fog: false,
      uniforms: { top: { value: new THREE.Color(0x3f86d4) }, horizon: { value: new THREE.Color(0xd2e6f5) }, ground: { value: new THREE.Color(0x888888) } },
      vertexShader: 'varying vec3 vP; void main(){ vP=position; gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
      fragmentShader: 'uniform vec3 top; uniform vec3 horizon; uniform vec3 ground; varying vec3 vP; void main(){ float h=normalize(vP).y; vec3 c = h>0.0 ? mix(horizon, top, pow(clamp(h,0.0,1.0),0.55)) : mix(horizon, ground, clamp(-h*4.0,0.0,1.0)); gl_FragColor=vec4(c,1.0); }',
    });
    const dome = new THREE.Mesh(new THREE.SphereGeometry(1800, 24, 16), mat); dome.renderOrder = -10; dome.frustumCulled = false; group.add(dome);
    // звёзды
    const n = 600, pos = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) { const u = Math.random() * 2 - 1, a = Math.random() * 6.283, r = Math.sqrt(1 - u * u); const y = Math.abs(u) * 0.9 + 0.08; pos[i * 3] = Math.cos(a) * r * 1700; pos[i * 3 + 1] = y * 1700; pos[i * 3 + 2] = Math.sin(a) * r * 1700; }
    const sg = new THREE.BufferGeometry(); sg.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const stars = new THREE.Points(sg, new THREE.PointsMaterial({ color: 0xffffff, size: 2, sizeAttenuation: false, fog: false, transparent: true, opacity: 0.85 })); stars.visible = false; group.add(stars);
    return { group, mat, stars, dome };
  }

  makeShowroom() {
    const g = new THREE.Group();
    const floor = new THREE.Mesh(new THREE.CircleGeometry(14, 64), new THREE.MeshStandardMaterial({ color: 0x1c1f26, roughness: 0.6, metalness: 0.3 }));
    floor.rotation.x = -Math.PI / 2; floor.receiveShadow = true; g.add(floor);
    const ring = new THREE.Mesh(new THREE.RingGeometry(4.6, 4.75, 64), new THREE.MeshBasicMaterial({ color: 0xff7a00, side: THREE.DoubleSide })); ring.rotation.x = -Math.PI / 2; ring.position.y = 0.01; g.add(ring);
    const ring2 = new THREE.Mesh(new THREE.RingGeometry(6.4, 6.46, 64), new THREE.MeshBasicMaterial({ color: 0x4a5160, side: THREE.DoubleSide })); ring2.rotation.x = -Math.PI / 2; ring2.position.y = 0.01; g.add(ring2);
    const key = new THREE.SpotLight(0xffffff, 380, 40, 0.7, 0.6, 1.5); key.position.set(7, 11, 6); key.castShadow = true; key.shadow.mapSize.set(1024, 1024); key.target.position.set(0, 0.5, 0); g.add(key, key.target);
    const rim = new THREE.SpotLight(0x6aa8ff, 420, 40, 0.8, 0.6, 1.5); rim.position.set(-8, 6, -7); rim.target.position.set(0, 0.7, 0); g.add(rim, rim.target);
    const fill = new THREE.PointLight(0xffd9b0, 60, 25, 2); fill.position.set(-5, 3, 6); g.add(fill);
    g.visible = false;
    this.scene.add(g);
    return { group: g, car: null, angle: 0.6, camDist: 11.5 };
  }

  setSettings(s) {
    Object.assign(this.settings, s);
    const q = QUALITY[this.settings.quality] || QUALITY.high;
    this.quality = this.settings.quality;
    const shadowsOn = this.settings.shadows && q.shadow > 0;
    this.renderer.shadowMap.enabled = shadowsOn;
    this.sun.castShadow = shadowsOn;
    if (shadowsOn && this.sun.shadow.mapSize.x !== q.shadow) { this.sun.shadow.mapSize.set(q.shadow, q.shadow); if (this.sun.shadow.map) { this.sun.shadow.map.dispose(); this.sun.shadow.map = null; } }
    this.updatePixelRatio();
    this.rig.baseFov = this.settings.fov; this.rig.speedFx = this.settings.speedFx;
    this.effectsOn = this.settings.effects;
    this.resize();
    // материалы перекомпилируются при смене теней
    this.scene.traverse((o) => { if (o.material) { const m = Array.isArray(o.material) ? o.material : [o.material]; m.forEach((x) => { x.needsUpdate = true; }); } });
  }

  updatePixelRatio() {
    const q = QUALITY[this.settings.quality] || QUALITY.high;
    this.pixelRatioTarget = Math.min(window.devicePixelRatio || 1, q.pr * (this.settings.pixelRatio || 1) * (this.autoScale || 1));
  }

  /** Адаптивное разрешение: если кадры стабильно дольше 40 мс — уменьшаем разрешение (не ниже 50%). Возвращает true, если изменили. */
  adaptResolution(dt) {
    if (navigator.webdriver) return false;
    this._fpsAcc = (this._fpsAcc || 0) + dt; this._fpsN = (this._fpsN || 0) + 1;
    if (this._fpsAcc < 2.5) return false;
    const avg = this._fpsAcc / this._fpsN; this._fpsAcc = 0; this._fpsN = 0;
    if (avg > 0.04 && (this.autoScale || 1) > 0.55) { this.autoScale = (this.autoScale || 1) * 0.85; this.updatePixelRatio(); this.resize(); return true; }
    return false;
  }

  resize() {
    const w = this.canvas.clientWidth || window.innerWidth, h = this.canvas.clientHeight || window.innerHeight;
    this.renderer.setPixelRatio(this.pixelRatioTarget || Math.min(window.devicePixelRatio || 1, 1));
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h; this.camera.updateProjectionMatrix();
    this.viewH = h * this.renderer.getPixelRatio();
  }

  /** Освещение и небо по времени суток / погоде. */
  setEnv(time, weather) {
    const key = time + ':' + weather;
    this.time = time; this.weatherKind = weather;
    const e = ENVS[time] || ENVS.day, w = WEATHER_MOD[weather] || WEATHER_MOD.clear;
    const dim = (c) => new THREE.Color(c).multiplyScalar(w.dim);
    const rainTint = weather === 'rain' ? new THREE.Color(0x8c98a4) : weather === 'snow' ? new THREE.Color(0xc9d3dc) : null;
    const horizon = new THREE.Color(e.horizon), top = new THREE.Color(e.top);
    if (rainTint) { horizon.lerp(rainTint, 0.6).multiplyScalar(weather === 'rain' ? 0.72 : 1); top.lerp(rainTint, 0.5).multiplyScalar(weather === 'rain' ? 0.6 : 0.95); }
    this.sky.mat.uniforms.top.value.copy(top); this.sky.mat.uniforms.horizon.value.copy(horizon);
    this.sky.mat.uniforms.ground.value.copy(horizon).multiplyScalar(0.7);
    this.sky.stars.visible = time === 'night' && weather === 'clear';
    this.scene.fog = new THREE.Fog(horizon.clone(), e.fogNear * w.fogMul, e.fogFar * w.fogMul);
    this.scene.background = horizon.clone();
    this.sun.color.set(e.sun); this.sun.intensity = e.sunI * (weather === 'clear' ? 1 : weather === 'rain' ? 0.35 : 0.6);
    this.sunDir = new THREE.Vector3(...e.sunDir).normalize();
    this.hemi.color.set(e.hemiSky); this.hemi.groundColor.set(e.hemiGround); this.hemi.intensity = e.hemiI * (weather === 'rain' ? 0.8 : 1);
    this.renderer.toneMappingExposure = e.exposure;
    this.headlightsOn = e.lights || weather === 'rain';
    this.lampsOn = time !== 'day';
    this.weather.set(weather);
    // карта отражений из неба
    if (this.pmrem && (QUALITY[this.quality] || QUALITY.high).env) {
      const sc = new THREE.Scene(); sc.add(new THREE.Mesh(new THREE.SphereGeometry(50, 16, 12), this.sky.mat.clone()));
      if (this.envTex) this.envTex.dispose();
      this.envTex = this.pmrem.fromScene(sc, 0.02).texture;
      this.scene.environment = this.envTex;
      this.scene.environmentIntensity = time === 'night' ? 0.35 : weather === 'rain' ? 0.5 : 0.9;
    } else this.scene.environment = null;
  }

  loadMap(map, env) {
    if (this.mapGroup) { this.scene.remove(this.mapGroup.group); this.mapGroup.dispose(); this.mapGroup = null; }
    this.mapData = map; this.envInfo = env;
    this.setEnv(env.time, env.weather);
    this.mapGroup = buildMap(map, env);
    this.scene.add(this.mapGroup.group);
    this.skids.clear();
    this.setGates(null, 0);
    this.lampPos = this.mapGroup.lampPositions;
  }

  /** Ворота испытания: подсветка текущих. */
  setGates(gates, active) {
    while (this.gateGroup.children.length) { const c = this.gateGroup.children.pop(); c.geometry && c.geometry.dispose(); }
    this.gates = gates;
    if (!gates) return;
    gates.forEach((g, i) => {
      const w = Math.hypot(g.x2 - g.x1, g.z2 - g.z1);
      const mat = new THREE.MeshBasicMaterial({ color: i === active ? 0x42ff7b : i < active ? 0x555555 : 0x39c5ff, transparent: true, opacity: i === active ? 0.42 : i < active ? 0.06 : 0.16, side: THREE.DoubleSide, depthWrite: false });
      const plane = new THREE.Mesh(new THREE.PlaneGeometry(w, 4), mat);
      plane.position.set(g.x, 2, -g.z);
      plane.rotation.y = Math.atan2(g.z2 - g.z1, g.x2 - g.x1);
      this.gateGroup.add(plane);
      for (const s of [[g.x1, g.z1], [g.x2, g.z2]]) { const p = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 4.4, 8), new THREE.MeshBasicMaterial({ color: i === active ? 0x42ff7b : 0x39c5ff })); p.position.set(s[0], 2.2, -s[1]); this.gateGroup.add(p); }
    });
    this.gateActive = active;
  }

  setCarLook(def, look) {
    const key = def.id + JSON.stringify(look);
    if (key === this.carLookKey && this.carModel) return;
    this.carLookKey = key;
    if (this.carModel) { this.carGroupHolder.remove(this.carModel); disposeModel(this.carModel); }
    this.carModel = createCarModel(def, look);
    this.carGroupHolder.add(this.carModel);
    this.carDef = def;
    const u = this.carModel.userData;
    this.headMats = u.headMat;
  }

  /** Режим «игра». */
  enterGame() { this.mode = 'game'; this.showroom.group.visible = false; this.carModel.visible = true; this.sky.group.visible = true; this.mapGroup && (this.mapGroup.group.visible = true); this.fxGroup.visible = true; this.rig.init = false; this.skids.clear(); }
  /** Режим «автосалон»: показываем машину на платформе. */
  enterShowroom() {
    this.mode = 'showroom';
    this.showroom.group.visible = true;
    if (this.mapGroup) this.mapGroup.group.visible = false;
    this.fxGroup.visible = false; this.gateGroup.visible = false;
    this.setEnv('night', 'clear');
    this.scene.fog = new THREE.Fog(0x0a0d14, 25, 90); this.scene.background = new THREE.Color(0x0a0d14);
    this.sky.group.visible = false; this.sky.stars.visible = false;
    this.scene.environmentIntensity = 0.6;
    this.sun.intensity = 0.2; this.hemi.intensity = 0.4;
    if (this.carModel) this.carModel.visible = true;
    this.lampLights.forEach((l) => { l.intensity = 0; });
    this.head.forEach((l) => { l.intensity = 0; });
  }
  leaveShowroom() { this.showroom.group.visible = false; this.sky.group.visible = true; this.gateGroup.visible = true; }

  /** Исходная поза машины в автосалоне. */
  showroomFrame(dt, spin = 0.35) {
    this.showroom.angle += dt * spin;
    // машина должна помещаться в просвет между боковыми панелями (≈710 px)
    const vw = this.canvas.clientWidth || window.innerWidth;
    const gap = Math.max(0.3, Math.min(0.62, (vw - 720) / vw));
    const want = 5.2 / (gap * 0.85 * 2 * Math.tan(21 * Math.PI / 180) * this.camera.aspect);
    this.showroom.camDist += (Math.max(8.5, Math.min(24, want)) - this.showroom.camDist) * Math.min(1, dt * 4);
    const a = this.showroom.angle, d = this.showroom.camDist;
    this.camera.position.set(Math.sin(a) * d, 2.6, Math.cos(a) * d);
    this.camera.lookAt(0, 0.7, 0);
    this.camera.fov = 42; this.camera.updateProjectionMatrix();
    if (this.carModel) { this.carModel.position.set(0, 0, 0); this.carModel.rotation.set(0, Math.PI * 0.9, 0); const u = this.carModel.userData; u.body.rotation.set(0, 0, 0); u.wheels.forEach((w) => { w.pivot.rotation.y = 0; w.spin.rotation.x += dt * 0.0; }); u.tailMat.emissiveIntensity = 0.5; }
    this.renderer.render(this.scene, this.camera);
  }

  /** Событие сессии для эффектов. */
  onSessionEvent(e) {
    if (e.type === 'impact' && !e.light) {
      this.sparks.burst(e.x, 0.5, -e.z, e.nx, -e.nz, e.speed, Math.min(40, 8 + Math.floor(e.speed * 3)));
    }
    if (e.type === 'gate') this.setGates(this.gates, e.index);
  }

  /** Кадр игры. */
  frame(dt, session) {
    this.t += dt;
    const car = session.car;
    syncCarModel(this.carModel, car);
    if (this.mapGroup) this.mapGroup.updateProps(session.world.props);
    // солнце следует за машиной
    const P = new THREE.Vector3(car.x, 0, -car.z);
    this.sun.position.copy(P).addScaledVector(this.sunDir, 120); this.sun.target.position.copy(P);
    this.sun.target.updateMatrixWorld();
    this.sky.group.position.copy(this.camera.position);
    // фары
    const fwd = new THREE.Vector3(Math.sin(car.h), 0, -Math.cos(car.h)), rgt = new THREE.Vector3(Math.cos(car.h), 0, Math.sin(car.h));
    this.head.forEach((l, i) => {
      l.intensity = this.headlightsOn ? (this.time === 'night' ? 1500 : 700) : 0;
      l.position.copy(P).addScaledVector(fwd, car.spec.length * 0.4).addScaledVector(rgt, (i ? 1 : -1) * 0.6); l.position.y = 0.75;
      l.target.position.copy(P).addScaledVector(fwd, 30).addScaledVector(rgt, (i ? 1 : -1) * 1.2); l.target.position.y = 0;
      l.target.updateMatrixWorld();
    });
    if (this.carModel) this.carModel.userData.headMat.emissiveIntensity = this.headlightsOn ? 2.5 : 0.9;
    // световые лужи фонарей
    this.lampTimer -= dt;
    if (this.lampsOn && this.lampPos && this.lampPos.length && this.lampTimer <= 0) {
      this.lampTimer = 0.25;
      const near = this.lampPos.map((l) => ({ l, d: Math.hypot(l.x - P.x, l.z - P.z) })).sort((a, b) => a.d - b.d).slice(0, this.lampLights.length);
      near.forEach((n, i) => { const L = this.lampLights[i]; L.position.set(n.l.x, n.l.y - 0.3, n.l.z); L.intensity = this.time === 'night' ? 360 : 160; });
    } else if (!this.lampsOn) this.lampLights.forEach((l) => { l.intensity = 0; });
    // камера
    this.rig.update(dt, car, session.camShake);
    this.weather.update(dt, this.camera.position);
    if (this.effectsOn !== false) this.emitEffects(dt, session);
    this.smoke.update(dt, this.viewH); this.sparks.update(dt, this.viewH); this.skids.flush();
    this.renderer.render(this.scene, this.camera);
  }

  /** Дым не светится сам: ночью и в сумерках затемняем, чтобы не «горел» белым пятном. */
  tintSmoke(hex) {
    const k = (this.time === 'night' ? 0.32 : this.time === 'dusk' ? 0.7 : 1) * (this.weatherKind === 'rain' ? 0.8 : 1);
    const r = ((hex >> 16) & 255) * k, g = ((hex >> 8) & 255) * k, b = (hex & 255) * k;
    return (r << 16) | (g << 8) | b;
  }

  emitEffects(dt, session) {
    const car = session.car;
    const fx = Math.sin(car.h), fz = Math.cos(car.h), rx = Math.cos(car.h), rz = -Math.sin(car.h);
    for (let i = 0; i < 4; i++) {
      const [px, pz] = car.wheelPos[i];
      const wx = car.x + fx * pz + rx * px, wz = car.z + fz * pz + rz * px;
      const surf = car.contactSurface[i];
      const info = SURFACES[surf] || SURFACES.asphalt;
      const s = car.slip[i];
      const markable = info.mark && this.weatherKind === 'clear';
      this.skids.add(i, wx, wz, fx, fz, markable && s > 0.2 ? s : null);
      if (s > 0.18 && car.speed > 1.5) {
        this.smokeAcc[i] += dt * s * (surf === 'gravel' || surf === 'dirt' ? 60 : 85);
        while (this.smokeAcc[i] >= 1) {
          this.smokeAcc[i] -= 1;
          const jx = (Math.random() - 0.5) * 1.4, jz = (Math.random() - 0.5) * 1.4;
          this.smoke.emit(wx, 0.15, -wz, car.vx * 0.35 + jx, 0.6 + Math.random() * 0.8, -car.vz * 0.35 + jz, this.tintSmoke(smokeColor(surf)), 0.8 + Math.random() * 0.8 + s, 1.0 + Math.random() * 1.4, 1);
        }
      } else this.smokeAcc[i] = 0;
    }
  }

  dispose() { this.renderer.dispose(); }
}
