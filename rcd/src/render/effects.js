import * as THREE from 'three';
import { SURFACES } from '../physics/config.js';
import { spriteTexture } from './textures.js';

/** Пул частиц дыма (Points с шейдером: размер растёт, прозрачность падает). */
export class Smoke {
  constructor(max = 700) {
    this.max = max; this.i = 0;
    this.pos = new Float32Array(max * 3); this.vel = new Float32Array(max * 3);
    this.age = new Float32Array(max).fill(99); this.life = new Float32Array(max).fill(1);
    this.base = new Float32Array(max); this.col = new Float32Array(max * 3); this.alpha = new Float32Array(max);
    this.size = new Float32Array(max);
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(this.pos, 3));
    g.setAttribute('aSize', new THREE.BufferAttribute(this.size, 1));
    g.setAttribute('aAlpha', new THREE.BufferAttribute(this.alpha, 1));
    g.setAttribute('aColor', new THREE.BufferAttribute(this.col, 3));
    this.geo = g;
    this.mat = new THREE.ShaderMaterial({
      transparent: true, depthWrite: false,
      uniforms: { map: { value: spriteTexture('smoke') }, scale: { value: 600 } },
      vertexShader: 'attribute float aSize; attribute float aAlpha; attribute vec3 aColor; varying float vA; varying vec3 vC; uniform float scale; void main(){ vA=aAlpha; vC=aColor; vec4 mv=modelViewMatrix*vec4(position,1.0); gl_PointSize=aSize*scale/max(0.1,-mv.z); gl_Position=projectionMatrix*mv; }',
      fragmentShader: 'uniform sampler2D map; varying float vA; varying vec3 vC; void main(){ vec4 t=texture2D(map,gl_PointCoord); gl_FragColor=vec4(vC, t.a*vA); if(gl_FragColor.a<0.004) discard; }',
    });
    this.points = new THREE.Points(g, this.mat);
    this.points.frustumCulled = false;
    this.points.renderOrder = 5;
    this._c = new THREE.Color();
  }
  emit(x, y, z, vx, vy, vz, color, size, life, alpha) {
    const i = this.i; this.i = (this.i + 1) % this.max;
    this.pos[i * 3] = x; this.pos[i * 3 + 1] = y; this.pos[i * 3 + 2] = z;
    this.vel[i * 3] = vx; this.vel[i * 3 + 1] = vy; this.vel[i * 3 + 2] = vz;
    this.age[i] = 0; this.life[i] = life; this.base[i] = size;
    this._c.set(color); this.col[i * 3] = this._c.r; this.col[i * 3 + 1] = this._c.g; this.col[i * 3 + 2] = this._c.b;
    this.alpha[i] = alpha;
    this.size[i] = size;
  }
  update(dt, viewHeight) {
    for (let i = 0; i < this.max; i++) {
      if (this.age[i] >= this.life[i]) { this.alpha[i] = 0; continue; }
      this.age[i] += dt;
      const t = this.age[i] / this.life[i];
      const drag = Math.max(0, 1 - 1.8 * dt);
      this.vel[i * 3] *= drag; this.vel[i * 3 + 1] = this.vel[i * 3 + 1] * drag + 0.5 * dt; this.vel[i * 3 + 2] *= drag;
      this.pos[i * 3] += this.vel[i * 3] * dt; this.pos[i * 3 + 1] += this.vel[i * 3 + 1] * dt; this.pos[i * 3 + 2] += this.vel[i * 3 + 2] * dt;
      this.size[i] = this.base[i] * (1 + t * 3.2);
      this.alpha[i] = Math.max(0, (1 - t) * (1 - t)) * (t < 0.08 ? t / 0.08 : 1) * 0.55;
    }
    this.geo.attributes.position.needsUpdate = true; this.geo.attributes.aSize.needsUpdate = true; this.geo.attributes.aAlpha.needsUpdate = true; this.geo.attributes.aColor.needsUpdate = true;
    this.mat.uniforms.scale.value = viewHeight * 0.55;
  }
}

/** Следы шин: кольцевой буфер квадов. */
export class Skids {
  constructor(max = 9000) {
    this.max = max; this.n = 0; this.head = 0;
    this.pos = new Float32Array(max * 12); this.col = new Float32Array(max * 16);
    const idx = new Uint32Array(max * 6);
    for (let q = 0; q < max; q++) { const b = q * 4; idx.set([b, b + 1, b + 2, b + 2, b + 1, b + 3], q * 6); }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(this.pos, 3));
    g.setAttribute('color', new THREE.BufferAttribute(this.col, 4));
    g.setIndex(new THREE.BufferAttribute(idx, 1));
    g.setDrawRange(0, 0);
    this.geo = g;
    this.mesh = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -4 }));
    this.mesh.frustumCulled = false; this.mesh.renderOrder = 2;
    this.last = [null, null, null, null];
    this.dirty = false;
  }
  clear() { this.n = 0; this.head = 0; this.last = [null, null, null, null]; this.geo.setDrawRange(0, 0); }
  /** Добавить отрезок следа для колеса i. intensity 0..1, null — прервать. */
  add(i, x, z, hx, hz, intensity, width = 0.24) {
    if (intensity === null || intensity < 0.12) { this.last[i] = null; return; }
    const nx = -hz * width / 2, nz = hx * width / 2;
    const cur = [x + nx, z + nz, x - nx, z - nz];
    const prev = this.last[i];
    this.last[i] = cur;
    if (!prev) return;
    if (Math.hypot(cur[0] - prev[0], cur[1] - prev[1]) > 3 || Math.hypot(cur[0] - prev[0], cur[1] - prev[1]) < 0.02) return;
    const q = this.head; this.head = (this.head + 1) % this.max; this.n = Math.min(this.max, this.n + 1);
    const y = 0.07;
    const p = this.pos, o = q * 12;
    // three: z = -z_sim
    p[o] = prev[0]; p[o + 1] = y; p[o + 2] = -prev[1];
    p[o + 3] = prev[2]; p[o + 4] = y; p[o + 5] = -prev[3];
    p[o + 6] = cur[0]; p[o + 7] = y; p[o + 8] = -cur[1];
    p[o + 9] = cur[2]; p[o + 10] = y; p[o + 11] = -cur[3];
    const a = Math.min(0.85, 0.25 + intensity * 0.7);
    for (let k = 0; k < 4; k++) { const c = q * 16 + k * 4; this.col[c] = 0.02; this.col[c + 1] = 0.02; this.col[c + 2] = 0.025; this.col[c + 3] = a; }
    this.dirty = true;
  }
  flush() {
    if (!this.dirty) return;
    this.geo.attributes.position.needsUpdate = true; this.geo.attributes.color.needsUpdate = true;
    this.geo.setDrawRange(0, this.n * 6);
    this.dirty = false;
  }
}

/** Искры (аддитивные точки с гравитацией). */
export class Sparks {
  constructor(max = 300) {
    this.max = max; this.i = 0;
    this.pos = new Float32Array(max * 3); this.vel = new Float32Array(max * 3); this.life = new Float32Array(max); this.age = new Float32Array(max).fill(99);
    this.alpha = new Float32Array(max);
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(this.pos, 3)); g.setAttribute('aAlpha', new THREE.BufferAttribute(this.alpha, 1));
    this.geo = g;
    this.mat = new THREE.ShaderMaterial({
      transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
      uniforms: { map: { value: spriteTexture('spark') }, scale: { value: 600 } },
      vertexShader: 'attribute float aAlpha; varying float vA; uniform float scale; void main(){ vA=aAlpha; vec4 mv=modelViewMatrix*vec4(position,1.0); gl_PointSize=0.22*scale/max(0.1,-mv.z); gl_Position=projectionMatrix*mv; }',
      fragmentShader: 'uniform sampler2D map; varying float vA; void main(){ vec4 t=texture2D(map,gl_PointCoord); gl_FragColor=vec4(t.rgb*1.4, t.a*vA); }',
    });
    this.points = new THREE.Points(g, this.mat); this.points.frustumCulled = false; this.points.renderOrder = 6;
  }
  burst(x, y, z, nx, nz, speed, count) {
    for (let k = 0; k < count; k++) {
      const i = this.i; this.i = (this.i + 1) % this.max;
      this.pos[i * 3] = x; this.pos[i * 3 + 1] = y; this.pos[i * 3 + 2] = z;
      const sp = 2 + Math.random() * Math.min(10, speed * 0.8);
      this.vel[i * 3] = nx * sp + (Math.random() - 0.5) * 5; this.vel[i * 3 + 1] = 1.5 + Math.random() * 4; this.vel[i * 3 + 2] = nz * sp + (Math.random() - 0.5) * 5;
      this.life[i] = 0.35 + Math.random() * 0.5; this.age[i] = 0;
    }
  }
  update(dt, viewHeight) {
    for (let i = 0; i < this.max; i++) {
      if (this.age[i] >= this.life[i]) { this.alpha[i] = 0; continue; }
      this.age[i] += dt;
      this.vel[i * 3 + 1] -= 14 * dt;
      this.pos[i * 3] += this.vel[i * 3] * dt; this.pos[i * 3 + 1] = Math.max(0.03, this.pos[i * 3 + 1] + this.vel[i * 3 + 1] * dt); this.pos[i * 3 + 2] += this.vel[i * 3 + 2] * dt;
      this.alpha[i] = 1 - this.age[i] / this.life[i];
    }
    this.geo.attributes.position.needsUpdate = true; this.geo.attributes.aAlpha.needsUpdate = true;
    this.mat.uniforms.scale.value = viewHeight * 0.55;
  }
}

/** Дождь (линии) и снег (точки) вокруг камеры. */
export class Weather {
  constructor() {
    this.group = new THREE.Group();
    this.kind = 'clear';
    const nR = 2200;
    this.rainPos = new Float32Array(nR * 6);
    this.rainSeed = new Float32Array(nR * 3);
    for (let i = 0; i < nR; i++) { this.rainSeed[i * 3] = Math.random() * 80 - 40; this.rainSeed[i * 3 + 1] = Math.random() * 30; this.rainSeed[i * 3 + 2] = Math.random() * 80 - 40; }
    const rg = new THREE.BufferGeometry(); rg.setAttribute('position', new THREE.BufferAttribute(this.rainPos, 3));
    this.rain = new THREE.LineSegments(rg, new THREE.LineBasicMaterial({ color: 0xaec6d8, transparent: true, opacity: 0.45 }));
    this.rain.frustumCulled = false; this.rain.visible = false;
    const nS = 1800;
    this.snowPos = new Float32Array(nS * 3); this.snowSeed = new Float32Array(nS * 3);
    for (let i = 0; i < nS; i++) { this.snowSeed[i * 3] = Math.random() * 70 - 35; this.snowSeed[i * 3 + 1] = Math.random() * 28; this.snowSeed[i * 3 + 2] = Math.random() * 70 - 35; }
    const sg = new THREE.BufferGeometry(); sg.setAttribute('position', new THREE.BufferAttribute(this.snowPos, 3));
    this.snow = new THREE.Points(sg, new THREE.PointsMaterial({ color: 0xffffff, size: 0.18, transparent: true, opacity: 0.85, depthWrite: false }));
    this.snow.frustumCulled = false; this.snow.visible = false;
    this.group.add(this.rain, this.snow);
    this.t = 0;
  }
  set(kind) { this.kind = kind; this.rain.visible = kind === 'rain'; this.snow.visible = kind === 'snow'; }
  update(dt, cam) {
    this.t += dt;
    if (this.kind === 'rain') {
      const n = this.rainSeed.length / 3, h = 30;
      for (let i = 0; i < n; i++) {
        const y = ((this.rainSeed[i * 3 + 1] - this.t * 32) % h + h) % h;
        const x = cam.x + ((this.rainSeed[i * 3] - cam.x) % 80 + 120) % 80 - 40, z = cam.z + ((this.rainSeed[i * 3 + 2] - cam.z) % 80 + 120) % 80 - 40;
        this.rainPos[i * 6] = x; this.rainPos[i * 6 + 1] = y; this.rainPos[i * 6 + 2] = z;
        this.rainPos[i * 6 + 3] = x - 0.04; this.rainPos[i * 6 + 4] = y + 0.9; this.rainPos[i * 6 + 5] = z;
      }
      this.rain.geometry.attributes.position.needsUpdate = true;
    } else if (this.kind === 'snow') {
      const n = this.snowSeed.length / 3, h = 28;
      for (let i = 0; i < n; i++) {
        const y = ((this.snowSeed[i * 3 + 1] - this.t * 2.4) % h + h) % h;
        const wob = Math.sin(this.t * 0.9 + i) * 0.6;
        this.snowPos[i * 3] = cam.x + ((this.snowSeed[i * 3] - cam.x) % 70 + 105) % 70 - 35 + wob;
        this.snowPos[i * 3 + 1] = y;
        this.snowPos[i * 3 + 2] = cam.z + ((this.snowSeed[i * 3 + 2] - cam.z) % 70 + 105) % 70 - 35;
      }
      this.snow.geometry.attributes.position.needsUpdate = true;
    }
  }
}

export const smokeColor = (surf) => (SURFACES[surf] || SURFACES.asphalt).smoke;
