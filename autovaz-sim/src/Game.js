import * as THREE from 'three';
import { CollisionWorld } from './core/CollisionWorld.js';
import { Input } from './core/Input.js';
import { createTextures } from './world/Textures.js';
import { RoadGraph } from './world/RoadGraph.js';
import { City } from './world/City.js';
import { TrafficLights } from './world/TrafficLights.js';
import { DayNight } from './world/DayNight.js';
import { Trees } from './world/Trees.js';
import { PlayerCar } from './vehicles/PlayerCar.js';
import { CarInstancer } from './vehicles/CarInstancer.js';
import { lampPoints } from './vehicles/CarFactory.js';
import { TrafficManager } from './traffic/TrafficManager.js';
import { CameraRig } from './camera/CameraRig.js';
import { HUD } from './ui/HUD.js';
import { PerfMonitor } from './perf/PerfMonitor.js';
import { GlowPoints } from './gfx/GlowPoints.js';
import { Smoke } from './gfx/Smoke.js';
import { PostFX } from './gfx/PostFX.js';
import { EnvManager } from './gfx/EnvManager.js';
import { TargetZone, Beacon, Cones, Pedestrian } from './gfx/Markers.js';
import { Rules } from './gameplay/Rules.js';
import { CAR_BY_ID } from './config/cars.js';
import { mulberry32 } from './utils/math.js';

const TIME_PRESETS = [[12, 'День'], [19.3, 'Вечер'], [23, 'Ночь'], [6.2, 'Рассвет']];

/**
 * Игровой мир и цикл. Иерархия сцены:
 *  Scene
 *  ├─ Sky (шейдер: градиент + облака + солнце/луна), Stars
 *  ├─ HemisphereLight, SunMoonLight (тени следуют за игроком)
 *  ├─ World: Asphalt, Terrain, Markings, Sidewalks, Lawns, Fence, Block_0..35 (здания),
 *  │         StreetLightPoles/LampHeads/LightPools, Props, RoadSigns, ShopSigns, Billboards,
 *  │         AutodromeAsphalt, DOSAAF
 *  ├─ TrafficLightPoles, TrafficLamps (Instanced)
 *  ├─ Trees (Instanced, 2 LOD)
 *  ├─ Car_<model>_LOD0/LOD1 (Instanced: трафик + припаркованные + препятствия), BlobShadows, Beams
 *  ├─ PlayerCar: Body (paint/chrome/glass/…), 4 колеса, SpotLight фар
 *  ├─ GlowPoints (все лампы города одним draw call), Smoke, SkidMarks
 *  └─ Маркеры: TargetZone, Beacon, Cones, Pedestrian
 */
export class Game {
  constructor(canvas, quality, save) {
    this.canvas = canvas;
    this.q = quality;
    this.save = save;
    this.frame = 0;
    this.timePreset = 0;
    this.mode = null;
    this._beepT = 0;
  }

  init(audio) {
    const q = this.q;
    this.audio = audio;
    const renderer = new THREE.WebGLRenderer({
      canvas: this.canvas, antialias: q.antialias && !q.bloom, powerPreference: 'high-performance',
      precision: q.precision, stencil: false, alpha: false, depth: true,
    });
    renderer.setSize(window.innerWidth, window.innerHeight, false);
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.shadowMap.enabled = q.shadows;
    renderer.shadowMap.type = q.name === 'high' ? THREE.PCFSoftShadowMap : THREE.PCFShadowMap;
    renderer.shadowMap.autoUpdate = false;
    this.renderer = renderer;

    const scene = new THREE.Scene();
    this.scene = scene;
    this.camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.3, q.drawDistance * 1.05);

    this.T = createTextures(Math.min(q.anisotropy, renderer.capabilities.getMaxAnisotropy()));
    this.collision = new CollisionWorld(16);
    this.rnd = mulberry32(42);
    this.glow = new GlowPoints(scene, this.T.glow, 5000);

    this.graph = new RoadGraph();
    this.city = new City(scene, this.T, q, this.collision);
    this.lights = new TrafficLights(scene, this.graph, this.collision, this.rnd, this.glow);
    this.dayNight = new DayNight(scene, renderer, q);
    this.dayNight.setTime(12);
    this.trees = new Trees(scene, this.city.treeSpots, q);
    this.glow.addStaticGroup('street', this.city.lampPositions, [1.0, 0.75, 0.45], 1.6);
    if (this.city.azs) this.glow.addStaticGroup('azs', this.city.azs.lights, [0.9, 0.95, 1.0], 2.2);
    this.glow.addStaticGroup('cams', this.city.cameras.map((c) => c.lens), [1, 0.1, 0.05], 0.25);

    this.env = q.carEnvMap ? new EnvManager(renderer, this.dayNight.sky.material, this.dayNight.fog.color) : null;

    this.instancer = new CarInstancer(scene, q, this.T, 40);
    for (const p of this.city.parked) p.model = this.instancer.modelIndex(p.key);
    this.dps = this.city.dpsPosts.map((p) => ({
      model: this.instancer.modelIndex('police'), x: p.x, z: p.z, y: 0.15, heading: p.heading,
      color: new THREE.Color(0xf2f2f2), lights: false, flashers: true,
    }));

    this.smoke = new Smoke(scene, this.T.glow, q.name === 'low' ? 80 : 160);
    this.player = new PlayerCar(scene, { T: this.T, quality: q, smoke: this.smoke });

    this.traffic = new TrafficManager({
      graph: this.graph, lights: this.lights, quality: q, instancer: this.instancer,
      onHonk: (car) => {
        const p = this.player.physics;
        const dx = car.x - p.x, dz = car.z - p.z, d = Math.hypot(dx, dz);
        this.audio.aiHorn(Math.max(0, 1 - d / 60), (dx * -Math.cos(p.heading) + dz * Math.sin(p.heading)) / (d || 1));
      },
    });

    this.zone = new TargetZone(scene);
    this.beacon = new Beacon(scene);
    this.cones = new Cones(scene);
    this.ped = new Pedestrian(scene);
    this.rules = new Rules(this.lights);

    this.cameraRig = new CameraRig(this.camera, this.collision);
    this.input = new Input();
    this.hud = new HUD(this.city);
    this.perf = new PerfMonitor(renderer, q);
    this.postfx = q.bloom ? new PostFX(renderer, scene, this.camera) : null;
    this._bindInput();

    this.refreshCar();
    this.player.onBlink = (on) => this.audio.tick(on);
    this.player.onCrash = (s, tag) => {
      this.audio.crash(s);
      this.cameraRig.addShake(s);
      if (navigator.vibrate) navigator.vibrate(Math.round(20 + s * 60));
      this.save.data.stats.crashes++;
      this.mode?.onCrash?.(s, tag);
    };
    this.player.onContact = (obj, imp) => this.mode?.onContact?.(obj, imp);

    window.addEventListener('resize', () => this._resize());
    this.canvas.addEventListener('webglcontextlost', (e) => { e.preventDefault(); this.contextLost = true; });
    this.canvas.addEventListener('webglcontextrestored', () => location.reload());

    this.player.place(0, -70, 0);
    this.dayNight.update(0, this.player.position, this.camera.position);
    this.cameraRig.update(0, this.player);
    this._updateEnv(true);
    renderer.compile(scene, this.camera);
    this.renderer.shadowMap.needsUpdate = true;
  }

  /** Применить выбранную в сохранении машину и тюнинг. */
  refreshCar() {
    const id = this.save.data.current;
    const def = CAR_BY_ID[id];
    const tv = this.save.tuningValues(id);
    const keep = this.player.physics ? { x: this.player.physics.x, z: this.player.physics.z, h: this.player.physics.heading } : null;
    this.player.setCar(def, tv);
    this.lamps = lampPoints(def);
    this.audio.setEngineProfile({ cylinders: id === 'oka' ? 2 : 4, rasp: tv.engine > 1.3 ? 1.3 : 1 });
    if (keep) this.player.place(keep.x, keep.z, keep.h);
    this.applySettings();
  }

  applySettings() {
    const s = this.save.data.settings;
    this.input?.setMode(s.controls);
    this.input?.setGearbox(s.gearbox === 'manual');
    if (this.player.physics) {
      this.player.physics.setManual(s.gearbox === 'manual');
      this.player.physics.spec.stabilityAssist = s.assist ? 0.35 : 0;
    }
    this.audio.setVolume(s.volume);
  }

  _bindInput() {
    const i = this.input, p = () => this.player.physics;
    i.on('camera', () => this.hud.toast(`Камера: ${this.cameraRig.next()}`));
    i.on('lights', () => {
      const m = this.player.cycleLights();
      this.hud.toast(`Фары: ${m === 'auto' ? 'авто' : m === 'on' ? 'вкл' : 'выкл'}`);
      document.getElementById('btn-lights').classList.toggle('on', m === 'on');
    });
    i.on('indicator', (side) => this.player.toggleIndicator(side));
    i.on('hazard', () => this.player.toggleHazard());
    i.on('selector', (sel) => { p().setSelector(sel); this.audio.click(); });
    i.on('selectorStep', (d) => {
      if (p().manual) return;
      const order = ['R', 'N', 'D'];
      const k = Math.max(0, Math.min(2, order.indexOf(p().selector) + d));
      p().setSelector(order[k]);
    });
    i.on('shiftUp', () => p().shiftUp());
    i.on('shiftDown', () => p().shiftDown());
    i.on('action', () => this.mode?.onAction?.());
    i.on('taxi', () => this.mode?.onTaxi?.());
    i.on('pause', () => this.onPause?.());
  }

  setTimePreset(k) {
    this.timePreset = ((k % TIME_PRESETS.length) + TIME_PRESETS.length) % TIME_PRESETS.length;
    this.dayNight.setTime(TIME_PRESETS[this.timePreset][0]);
    this._updateEnv(true);
    return TIME_PRESETS[this.timePreset][1];
  }

  setMode(mode, arg) {
    if (this.mode) this.mode.exit();
    this.mode = mode;
    this.input.enabled = true;
    this.player.indicator = null;
    this.hud.clearToasts();
    this.hud.combo(null);
    mode.enter(arg);
    this.cameraRig.snap();
    this.renderer.shadowMap.needsUpdate = true;
  }

  _resize() {
    const w = window.innerWidth, h = window.innerHeight;
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h, false);
    this.postfx?.setSize(w, h);
  }

  _updateEnv(force) {
    if (!this.env) return;
    const tex = this.env.update(this.dayNight.time, this.dayNight.fog.color, this.dayNight.night, force);
    if (tex && this.scene.environment !== tex) this.scene.environment = tex;
  }

  // ---------------------------------------------------------------- датчики парковки
  _distTo(x, z, maxD, cols) {
    let best = maxD;
    for (const col of cols) {
      for (const o of col.query(x - maxD, z - maxD, x + maxD, z + maxD)) {
        let d;
        if (o.type === 0) {
          const cx = Math.max(o.minX, Math.min(x, o.maxX)), cz = Math.max(o.minZ, Math.min(z, o.maxZ));
          d = Math.hypot(x - cx, z - cz);
        } else d = Math.hypot(x - o.x, z - o.z) - o.r;
        if (d < best) best = d;
      }
    }
    for (const c of this.traffic.cars) {
      const d = Math.hypot(c.x - x, c.z - z) - 1.6;
      if (d < best) best = d;
    }
    return Math.max(0, best);
  }

  _sensors(dt) {
    const pl = this.player, p = pl.physics;
    const cols = [this.collision, ...(this.mode?.extraColliders || [])];
    const s = Math.sin(p.heading), c = Math.cos(p.heading);
    const pt = (lx, lz) => [p.x + lx * c + lz * s, p.z - lx * s + lz * c];
    const h = pl.half;
    let front = 9, rear = 9;
    for (const lx of [-h.w + 0.1, 0, h.w - 0.1]) {
      front = Math.min(front, this._distTo(...pt(lx, h.front), 3, cols));
      rear = Math.min(rear, this._distTo(...pt(lx, h.rear), 3, cols));
    }
    this.hud.sensors(front, rear);
    // пищалка: чаще ближе к препятствию (сзади — только на задней передаче)
    const d = p.reversing ? rear : p.speed < 4 ? front : 9;
    this._beepT -= dt;
    if (d < 2.2 && this._beepT <= 0 && p.speed > 0.05) {
      this.audio.beep(d < 0.4 ? 2100 : 1700, d < 0.4 ? 0.12 : 0.06, 0.08);
      this._beepT = d < 0.4 ? 0.12 : d < 0.9 ? 0.2 : d < 1.5 ? 0.38 : 0.6;
    }
  }

  // ---------------------------------------------------------------- цикл
  tick(dt) {
    this.frame++;
    this.input.update(dt);
    const night = this.dayNight.night;
    const extra = this.mode?.extraColliders || [];
    this.player.update(dt, this.input, {
      surface: (x, z, out) => this.city.surface(x, z, out),
      colliders: [this.collision, ...extra],
      traffic: this.traffic.cars,
      night,
    });
    this.traffic.update(dt, this.player.physics, this.camera);
    this.rules.update(dt, this.player, this.player.surface.type);
    this.mode?.update(dt);
    this.cameraRig.update(dt, this.player, this.input.orbit);
    this.input.showGear(this.player.physics);

    this.lights.update(dt);
    this.dayNight.update(dt, this.q.shadows ? null : this.player.position, this.camera.position);
    this.city.update(dt, this.camera, night);
    this.trees.update(dt, this.camera.position);
    this.cones.update(dt);
    this.smoke.update(dt, this.camera, this.renderer, 1 - night * 0.7);
    if ((this.frame & 63) === 0) this._updateEnv(false);
    this.postfx?.setNight(night);

    const every = this.q.shadowUpdateEvery;
    if (every > 0 && this.frame % every === 0) {
      this.dayNight.placeShadowCamera(this.player.position);
      this.renderer.shadowMap.needsUpdate = true;
    }
    if (this.hud._sensorsOn) this._sensors(dt);

    // --- машины (трафик + дворы + ДПС + препятствия уровня) и свечение ламп
    const blink = (performance.now() % 700) < 350;
    this.glow.setGroup('street', night > 0.15);
    if (this.city.azs) this.glow.setGroup('azs', night > 0.15);
    this.glow.setGroup('cams', blink);
    this.glow.uniforms.intensity.value = this.postfx ? 0.75 : 1.0;
    this.glow.beginDynamic();
    this.instancer.begin(this.camera, night);
    this.traffic.render(this.glow, blink);
    const cam = this.camera.position, R2 = 160 * 160;
    for (const pc of this.city.parked) {
      const dx = pc.x - cam.x, dz = pc.z - cam.z;
      if (dx * dx + dz * dz < R2) this.instancer.add(pc, null, false);
    }
    for (const d of this.dps) this.instancer.add(d, this.glow, blink);
    this.mode?.renderExtra?.(this.instancer, this.glow, blink);
    this.instancer.end();
    this._playerGlow();
    this.glow.end(this.camera, this.renderer, this.dayNight.fog);

    const p = this.player.physics;
    this.audio.update(p.rpm, p.load, p.skid, p.speed);
    this.audio.setHorn(this.input.horn);
    this.perf.update(dt);
    this.hud.update(dt, {
      player: this.player, clock: this.dayNight.label, fps: this.perf.fps, traffic: this.traffic,
      camera: this.camera, money: this.save.money,
    });
  }

  _playerGlow() {
    const pl = this.player, p = pl.physics, L = this.lamps;
    const s = Math.sin(p.heading), c = Math.cos(p.heading), y = pl.y + (pl.tv?.height || 0);
    const put = (q, r, g, b, size) => this.glow.add(p.x + q[0] * c + q[2] * s, y + q[1], p.z - q[0] * s + q[2] * c, r, g, b, size);
    if (pl.lightsOn) for (const q of L.head) put(q, 1, 0.93, 0.78, 1.1);
    if (pl.lightsOn || p.braking) for (const q of L.tail) put(q, 1, 0.08, 0.04, p.braking ? 1.0 : 0.55);
    if (p.reversing) for (const q of L.tail) put([q[0] * 0.6, q[1], q[2]], 1, 1, 1, 0.5);
    const ind = pl.indicator;
    if (ind && pl.blinkOn) for (const sd of ind === 'H' ? ['L', 'R'] : [ind]) for (const q of L.ind[sd]) put(q, 1, 0.55, 0.05, 0.7);
  }

  render(dt) {
    if (this.postfx) this.postfx.render(dt);
    else this.renderer.render(this.scene, this.camera);
  }

  get stats() {
    const i = this.renderer.info;
    return {
      fps: this.perf.fps, drawCalls: i.render.calls, triangles: i.render.triangles,
      geometries: i.memory.geometries, textures: i.memory.textures,
      traffic: this.traffic.cars.length, chunks: this.city.visibleChunks, pixelRatio: this.renderer.getPixelRatio(),
    };
  }
}
