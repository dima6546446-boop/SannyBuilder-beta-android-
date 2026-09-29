import * as THREE from 'three';
import { smoothstep, clamp } from '../utils/math.js';

/** Ключевые кадры атмосферы по высоте солнца (sin угла над горизонтом). */
const KEYS = [
  { e: -0.25, top: 0x02040b, hor: 0x0a1228, fog: 0x0a0f1e, sun: 0x8fa3d9, sunI: 0.22, hemiSky: 0x2a3b66, hemiGround: 0x0c0c10, hemiI: 0.35 },
  { e: -0.02, top: 0x1b2248, hor: 0x9a5a58, fog: 0x3c3440, sun: 0xff7a3a, sunI: 0.3, hemiSky: 0x4a4a7a, hemiGround: 0x1a1512, hemiI: 0.45 },
  { e: 0.1, top: 0x35599a, hor: 0xf0a060, fog: 0x9c8a80, sun: 0xffb070, sunI: 1.6, hemiSky: 0x8fa6cf, hemiGround: 0x3a3226, hemiI: 0.8 },
  { e: 0.35, top: 0x3a6fc0, hor: 0xc9d9ea, fog: 0xb9c7d4, sun: 0xfff0dc, sunI: 2.6, hemiSky: 0xb4cff0, hemiGround: 0x4d4436, hemiI: 1.0 },
  { e: 1.0, top: 0x2c65d0, hor: 0xbcd6ee, fog: 0xc2d3e2, sun: 0xfff8ee, sunI: 3.0, hemiSky: 0xbcd8f7, hemiGround: 0x51493b, hemiI: 1.1 },
];

const SKY_VERT = /* glsl */`
varying vec3 vDir;
void main() {
  vDir = position;
  vec4 p = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  gl_Position = p.xyww; // всегда на дальней плоскости
}`;

const SKY_FRAG = /* glsl */`
uniform vec3 topColor;
uniform vec3 horizonColor;
uniform vec3 bottomColor;
uniform vec3 sunColor;
uniform vec3 sunDir;
uniform float sunDisk;
uniform float time;
uniform float cloudAmt;
uniform vec3 cloudColor;
varying vec3 vDir;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p), f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) { float s = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { s += a * vnoise(p); p *= 2.03; a *= 0.5; } return s; }
void main() {
  vec3 d = normalize(vDir);
  float h = d.y;
  vec3 col = h > 0.0 ? mix(horizonColor, topColor, pow(h, 0.45)) : mix(horizonColor, bottomColor, pow(-h, 0.35));
  float s = max(dot(d, sunDir), 0.0);
  col += sunColor * (pow(s, 900.0) * sunDisk + pow(s, 10.0) * 0.25);
  if (cloudAmt > 0.0 && h > 0.0) {
    // облака на «куполе»: проекция направления на плоскость
    vec2 uv = d.xz / (h + 0.12) * 1.6 + vec2(time * 0.004, time * 0.0015);
    float c = smoothstep(0.48, 0.78, fbm(uv));
    float edge = smoothstep(0.0, 0.25, h);
    vec3 cc = cloudColor + sunColor * pow(s, 4.0) * 0.35;
    col = mix(col, cc, c * edge * cloudAmt);
  }
  gl_FragColor = vec4(col, 1.0);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}`;

/**
 * Динамический цикл дня и ночи: солнце/луна (один DirectionalLight — тени и ночью),
 * небо-градиент (шейдер на сфере, 1 draw call), звёзды, туман, яркость окон/фонарей.
 * Тени: ортокамера следует за игроком (узкий фрустум = высокая плотность текселей)
 * и привязана к сетке текселей — без «дрожания» теней при движении.
 */
export class DayNight {
  constructor(scene, renderer, quality) {
    this.scene = scene;
    this.renderer = renderer;
    this.q = quality;
    this.time = 12;            // часы
    this.speed = 24 / 900;     // полные сутки за 15 минут
    this.night = 0;
    this.sunDir = new THREE.Vector3();
    this.lightDir = new THREE.Vector3();

    this.hemi = new THREE.HemisphereLight(0xffffff, 0x444444, 1);
    scene.add(this.hemi);

    this.sun = new THREE.DirectionalLight(0xffffff, 3);
    this.sun.name = 'SunMoonLight';
    if (quality.shadows) {
      const s = this.sun.shadow;
      this.sun.castShadow = true;
      s.mapSize.set(quality.shadowMapSize, quality.shadowMapSize);
      const r = quality.shadowRange;
      Object.assign(s.camera, { left: -r, right: r, top: r, bottom: -r, near: 1, far: 260 });
      s.camera.updateProjectionMatrix();
      s.bias = -0.0006;
      s.normalBias = 0.04;
    }
    scene.add(this.sun, this.sun.target);

    this.fog = new THREE.Fog(0xc2d3e2, quality.drawDistance * 0.35, quality.drawDistance);
    scene.fog = this.fog;

    this.skyUniforms = {
      topColor: { value: new THREE.Color() },
      horizonColor: { value: new THREE.Color() },
      bottomColor: { value: new THREE.Color(0x3a4030) },
      sunColor: { value: new THREE.Color() },
      sunDir: { value: new THREE.Vector3(0, 1, 0) },
      sunDisk: { value: 4 },
      time: { value: 0 },
      cloudAmt: { value: quality.clouds ? 0.85 : 0 },
      cloudColor: { value: new THREE.Color(0xffffff) },
    };
    this.sky = new THREE.Mesh(
      new THREE.SphereGeometry(quality.drawDistance * 0.9, 24, 12),
      new THREE.ShaderMaterial({
        uniforms: this.skyUniforms, vertexShader: SKY_VERT, fragmentShader: SKY_FRAG,
        side: THREE.BackSide, depthWrite: false, fog: false,
      }),
    );
    this.sky.name = 'Sky';
    this.sky.renderOrder = -10;
    this.sky.frustumCulled = false;
    scene.add(this.sky);

    // звёзды
    const n = 450, pos = new Float32Array(n * 3);
    const R = quality.drawDistance * 0.85;
    for (let i = 0; i < n; i++) {
      const u = Math.random() * Math.PI * 2, v = Math.random() * 0.95 + 0.05;
      const r = Math.sqrt(1 - v * v);
      pos[i * 3] = Math.cos(u) * r * R; pos[i * 3 + 1] = v * R; pos[i * 3 + 2] = Math.sin(u) * r * R;
    }
    const sg = new THREE.BufferGeometry();
    sg.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    this.stars = new THREE.Points(sg, new THREE.PointsMaterial({
      color: 0xffffff, size: 1.6, sizeAttenuation: false, transparent: true, opacity: 0, fog: false, depthWrite: false,
    }));
    this.stars.frustumCulled = false;
    this.stars.renderOrder = -9;
    scene.add(this.stars);

    this._a = new THREE.Color(); this._b = new THREE.Color();
    this._tmp = {};
    this.update(0, new THREE.Vector3(), new THREE.Vector3());
  }

  setTime(h) { this.time = ((h % 24) + 24) % 24; }

  _sample(e) {
    let i = 0;
    while (i < KEYS.length - 2 && e > KEYS[i + 1].e) i++;
    const A = KEYS[i], B = KEYS[i + 1];
    const t = clamp((e - A.e) / (B.e - A.e), 0, 1);
    const col = (k, out) => out.setHex(A[k]).lerp(this._b.setHex(B[k]), t);
    return { A, B, t, col };
  }

  update(dt, focus, cameraPos) {
    this.time = (this.time + dt * this.speed) % 24;
    // солнце: восход 6:00 на востоке (+X), закат 18:00
    const ang = ((this.time - 6) / 24) * Math.PI * 2;
    this.sunDir.set(Math.cos(ang), Math.sin(ang) * 0.92, 0.38).normalize();
    const e = this.sunDir.y;
    this.night = 1 - smoothstep(-0.12, 0.08, e);

    const { A, B, t, col } = this._sample(e);
    const u = this.skyUniforms;
    col('top', u.topColor.value);
    col('hor', u.horizonColor.value);
    col('fog', this.fog.color);
    col('sun', this.sun.color);
    col('hemiSky', this.hemi.color);
    col('hemiGround', this.hemi.groundColor);
    this.hemi.intensity = A.hemiI + (B.hemiI - A.hemiI) * t;
    this.sun.intensity = A.sunI + (B.sunI - A.sunI) * t;

    // ночью тот же источник работает как луна (противоположное направление)
    if (e >= 0) this.lightDir.copy(this.sunDir);
    else this.lightDir.copy(this.sunDir).negate().setY(Math.max(0.35, -e));
    this.lightDir.normalize();
    u.sunDir.value.copy(this.lightDir);
    u.sunColor.value.copy(this.sun.color).multiplyScalar(e >= 0 ? 1 : 0.6);
    u.sunDisk.value = e >= 0 ? 4 : 1.5;
    u.bottomColor.value.copy(this.fog.color).multiplyScalar(0.6);

    u.time.value += dt * 60;
    u.cloudColor.value.copy(u.horizonColor.value).lerp(this._a.setRGB(1, 1, 1), 0.55 * (1 - this.night)).multiplyScalar(1 - this.night * 0.75);
    this.stars.material.opacity = this.night * 0.9;
    this.stars.visible = this.night > 0.01;

    if (cameraPos) {
      this.sky.position.copy(cameraPos);
      this.stars.position.copy(cameraPos);
    }
    if (focus) this.placeShadowCamera(focus);
  }

  /** Двигаем ортокамеру теней за игроком со снапом к размеру текселя. */
  placeShadowCamera(focus) {
    const r = this.q.shadowRange;
    const texel = (2 * r) / (this.q.shadowMapSize || 1024);
    const fx = Math.round(focus.x / texel) * texel;
    const fz = Math.round(focus.z / texel) * texel;
    this.sun.target.position.set(fx, 0, fz);
    this.sun.position.set(fx + this.lightDir.x * 120, this.lightDir.y * 120, fz + this.lightDir.z * 120);
    this.sun.target.updateMatrixWorld();
  }

  get label() {
    const h = Math.floor(this.time), m = Math.floor((this.time - h) * 60);
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
  }
}
