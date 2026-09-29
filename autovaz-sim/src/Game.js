import * as THREE from 'three';
import { CollisionWorld } from './core/CollisionWorld.js';
import { Input } from './core/Input.js';
import { AudioManager } from './core/AudioManager.js';
import { createTextures } from './world/Textures.js';
import { RoadGraph, CITY, coord } from './world/RoadGraph.js';
import { City } from './world/City.js';
import { TrafficLights } from './world/TrafficLights.js';
import { DayNight } from './world/DayNight.js';
import { Trees } from './world/Trees.js';
import { PlayerCar } from './vehicles/PlayerCar.js';
import { TrafficManager } from './traffic/TrafficManager.js';
import { CameraRig } from './camera/CameraRig.js';
import { HUD } from './ui/HUD.js';
import { PerfMonitor } from './perf/PerfMonitor.js';
import { mulberry32 } from './utils/math.js';

const TIME_PRESETS = [[12, 'День'], [19.3, 'Вечер'], [23, 'Ночь'], [6.2, 'Рассвет']];

/**
 * Корень игры. Иерархия сцены:
 *  Scene
 *  ├─ Sky, Stars                       (следуют за камерой)
 *  ├─ HemisphereLight, SunMoonLight    (+target; тени следуют за игроком)
 *  ├─ World: Asphalt, Terrain, Markings, Sidewalks, Lawns, Fence,
 *  │         Block_0..N (здания-чанки), StreetLightPoles/LampHeads/LightPools
 *  ├─ TrafficLightPoles, TrafficLamps (Instanced)
 *  ├─ TreeTrunksLOD0, TreeCrownsLOD0, TreeCrownsLOD1 (Instanced)
 *  ├─ Traffic*: BodyLOD0, DetailLOD0, LOD1, HeadLamps, TailLamps, Beams, BlobShadows (Instanced)
 *  ├─ PlayerCar: Body(paint, chrome, glass…), 4 × wheel pivot, SpotLight фар
 *  └─ SkidMarks
 */
export class Game {
  constructor(canvas, quality) {
    this.canvas = canvas;
    this.q = quality;
    this.paused = false;
    this.running = false;
    this.timePreset = 0;
    this.frame = 0;
  }

  init({ carColor, startTime }) {
    const q = this.q;
    const renderer = new THREE.WebGLRenderer({
      canvas: this.canvas,
      antialias: q.antialias,
      powerPreference: 'high-performance',
      precision: q.precision,
      stencil: false,
      alpha: false,
      depth: true,
    });
    renderer.setSize(window.innerWidth, window.innerHeight, false);
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.shadowMap.enabled = q.shadows;
    renderer.shadowMap.type = q.name === 'high' ? THREE.PCFSoftShadowMap : THREE.PCFShadowMap;
    // тени перерисовываем не каждый кадр (medium — через кадр)
    renderer.shadowMap.autoUpdate = false;
    this.renderer = renderer;

    const scene = new THREE.Scene();
    this.scene = scene;
    const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.3, q.drawDistance * 1.05);
    this.camera = camera;

    const maxAniso = renderer.capabilities.getMaxAnisotropy();
    this.T = createTextures(Math.min(q.anisotropy, maxAniso));
    this.collision = new CollisionWorld(16);
    this.rnd = mulberry32(42);

    this.graph = new RoadGraph();
    this.city = new City(scene, this.T, q, this.collision);
    this.lights = new TrafficLights(scene, this.graph, this.collision, this.rnd);
    this.dayNight = new DayNight(scene, renderer, q);
    this.dayNight.setTime(startTime ?? 12);
    this.trees = new Trees(scene, this.city.treeSpots, q);

    const envMap = q.carEnvMap ? this._createEnvMap() : null;
    this.player = new PlayerCar(scene, { T: this.T, envMap, quality: q, color: carColor });
    // старт: правая полоса, едем на север к центральному перекрёстку
    const mid = (CITY.N - 1) / 2;
    this.player.place(coord(mid) - CITY.LANES[1], coord(mid - 1) + CITY.HALF + 20, 0);

    this.audio = new AudioManager();
    this.traffic = new TrafficManager(scene, {
      graph: this.graph, lights: this.lights, quality: q, T: this.T,
      onHonk: (car) => {
        const p = this.player.physics;
        const dx = car.x - p.x, dz = car.z - p.z;
        const d = Math.hypot(dx, dz);
        const rightX = -Math.cos(p.heading), rightZ = Math.sin(p.heading);
        this.audio.aiHorn(Math.max(0, 1 - d / 60), ((dx * rightX + dz * rightZ) / (d || 1)));
      },
    });
    this.player.onCrash = (s) => {
      this.audio.crash(s);
      this.cameraRig.addShake(s);
      if (navigator.vibrate) navigator.vibrate(Math.round(20 + s * 60));
    };

    this.cameraRig = new CameraRig(camera, this.collision);
    this.input = new Input();
    this.hud = new HUD(this.city);
    this.perf = new PerfMonitor(renderer, q);

    this.input.on('camera', () => this.hud.message(`Камера: ${this.cameraRig.next()}`));
    this.input.on('lights', () => {
      const m = this.player.cycleLights();
      this.hud.message(`Фары: ${m === 'auto' ? 'авто' : m === 'on' ? 'вкл' : 'выкл'}`);
      document.getElementById('btn-lights').classList.toggle('on', m === 'on');
    });
    this.input.on('time', () => {
      this.timePreset = (this.timePreset + 1) % TIME_PRESETS.length;
      const [t, name] = TIME_PRESETS[this.timePreset];
      this.dayNight.setTime(t);
      this.hud.message(name);
    });

    window.addEventListener('resize', () => this._resize());
    document.addEventListener('visibilitychange', () => this.setPaused(document.hidden));
    this.canvas.addEventListener('webglcontextlost', (e) => {
      e.preventDefault();
      this.setPaused(true);
      this.hud.message('Графический контекст потерян — перезапуск…', 5);
    });
    this.canvas.addEventListener('webglcontextrestored', () => location.reload());
    // хуки для нативной Android-обёртки
    window.__onNativePause = () => this.setPaused(true);
    window.__onNativeResume = () => this.setPaused(false);

    // первичная расстановка
    this.dayNight.update(0, this.player.position, camera.position);
    this._updateWorld(0);
    this.cameraRig.update(0, this.player);
    this.traffic.prefill(camera);
    this.renderer.shadowMap.needsUpdate = true;
    // прогрев: компилируем все шейдеры заранее, а не в первом кадре езды
    renderer.compile(scene, camera);
  }

  /** Маленький PMREM-envmap из градиентного «неба» для отражений на краске/хроме. */
  _createEnvMap() {
    const envScene = new THREE.Scene();
    const geo = new THREE.SphereGeometry(10, 16, 8);
    const colors = [];
    const top = new THREE.Color(0x6f9fd8), hor = new THREE.Color(0xdfe8f0), bot = new THREE.Color(0x4a4a44);
    const c = new THREE.Color();
    const pos = geo.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const y = pos.getY(i) / 10;
      if (y > 0) c.copy(hor).lerp(top, Math.pow(y, 0.6)); else c.copy(hor).lerp(bot, Math.pow(-y, 0.4));
      colors.push(c.r, c.g, c.b);
    }
    geo.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
    envScene.add(new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.BackSide })));
    const pmrem = new THREE.PMREMGenerator(this.renderer);
    const rt = pmrem.fromScene(envScene, 0.02);
    pmrem.dispose();
    geo.dispose();
    return rt.texture;
  }

  start() {
    this.audio.init();
    this.running = true;
    this.last = performance.now();
    this.renderer.setAnimationLoop((t) => this._frame(t));
  }

  setPaused(p) {
    this.paused = p;
    if (p) this.audio.suspend(); else { this.audio.resume(); this.last = performance.now(); }
  }

  _resize() {
    const w = window.innerWidth, h = window.innerHeight;
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h, false);
  }

  _updateWorld(dt) {
    const night = this.dayNight.night;
    this.lights.update(dt);
    // без теней направление света обновляем каждый кадр; с тенями — вместе с shadow map
    this.dayNight.update(dt, this.q.shadows ? null : this.player.position, this.camera.position);
    this.city.update(dt, this.camera, night);
    this.trees.update(dt, this.camera.position);
  }

  _frame(now) {
    if (this.paused) return;
    // метка rAF может быть раньше performance.now() из start() → отрицательный dt
    const dt = Math.max(0, Math.min((now - this.last) / 1000, 0.05));
    this.last = now;
    this._tick(dt);
    this.renderer.render(this.scene, this.camera);
  }

  /** Вся логика кадра без рендера (удобно для тестов: game._tick(1/60)). */
  _tick(dt) {
    this.frame++;
    this.input.update(dt);
    const night = this.dayNight.night;
    this.player.update(dt, this.input, this.city, this.collision, this.traffic, night);
    this.traffic.update(dt, this.player.physics, this.camera, night);
    this.cameraRig.update(dt, this.player);
    this._updateWorld(dt);

    // тени: перерисовка по расписанию пресета; камера теней двигается только вместе с ней
    const every = this.q.shadowUpdateEvery;
    if (every > 0 && this.frame % every === 0) {
      this.dayNight.placeShadowCamera(this.player.position);
      this.renderer.shadowMap.needsUpdate = true;
    }

    const p = this.player.physics;
    this.audio.update(p.rpm, p.load, p.skid, p.speed);
    this.audio.setHorn(this.input.horn);
    this.perf.update(dt);
    this.hud.update(dt, {
      physics: p, clock: this.dayNight.label, fps: this.perf.fps, traffic: this.traffic,
    });
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
