import * as THREE from 'three';

const VERT = /* glsl */`
attribute float size;
attribute vec3 color;
uniform float projScale;
uniform float intensity;
uniform float fogNear;
uniform float fogFar;
varying vec3 vColor;
void main() {
  vec4 mv = modelViewMatrix * vec4(position, 1.0);
  float d = -mv.z;
  float fade = 1.0 - smoothstep(fogNear, fogFar, d);
  vColor = color * intensity * fade;
  gl_PointSize = clamp(size * projScale / max(d, 0.1), 0.0, 160.0);
  if (size <= 0.0 || fade <= 0.0) gl_PointSize = 0.0;
  gl_Position = projectionMatrix * mv;
}`;

const FRAG = /* glsl */`
uniform sampler2D map;
varying vec3 vColor;
void main() {
  float a = texture2D(map, gl_PointCoord).a;
  gl_FragColor = vec4(vColor * a, a);
}`;

/**
 * Свечение ламп спрайтами-точками: фонари, фары, стоп-сигналы, поворотники,
 * светофоры, мигалки ДПС — всё одним draw call. Дешёвая замена bloom на слабых GPU
 * (а на сильных дополняет его). Статическая часть задаётся один раз, динамическая — каждый кадр.
 */
export class GlowPoints {
  constructor(scene, glowTexture, capacity = 4000) {
    this.cap = capacity;
    this.pos = new Float32Array(capacity * 3);
    this.col = new Float32Array(capacity * 3);
    this.size = new Float32Array(capacity);
    const g = new THREE.BufferGeometry();
    this.aPos = new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage);
    this.aCol = new THREE.BufferAttribute(this.col, 3).setUsage(THREE.DynamicDrawUsage);
    this.aSize = new THREE.BufferAttribute(this.size, 1).setUsage(THREE.DynamicDrawUsage);
    g.setAttribute('position', this.aPos);
    g.setAttribute('color', this.aCol);
    g.setAttribute('size', this.aSize);
    this.uniforms = {
      map: { value: glowTexture },
      projScale: { value: 500 },
      intensity: { value: 1 },
      fogNear: { value: 200 },
      fogFar: { value: 400 },
    };
    this.points = new THREE.Points(g, new THREE.ShaderMaterial({
      uniforms: this.uniforms, vertexShader: VERT, fragmentShader: FRAG,
      transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    }));
    this.points.frustumCulled = false;
    this.points.renderOrder = 5;
    this.points.name = 'GlowPoints';
    scene.add(this.points);
    this.staticCount = 0;
    this.n = 0;
    this.groups = {};
  }

  /** Статическая группа (фонари и т.п.): можно отдельно включать и менять цвет. */
  addStaticGroup(name, list, color, size) {
    const start = this.staticCount;
    for (const p of list) {
      const i = this.staticCount++;
      this.pos.set(p, i * 3);
      this.col.set(color, i * 3);
      this.size[i] = size;
    }
    this.groups[name] = { start, count: list.length, size, color: [...color], on: true };
    this.n = this.staticCount;
    this._dirtyAll = true;
    return this.groups[name];
  }

  /** Включить/выключить группу (размер 0) или отдельный элемент. */
  setGroup(name, on, color) {
    const gr = this.groups[name];
    if (!gr) return;
    if (gr.on === on && !color) return;
    gr.on = on;
    for (let k = 0; k < gr.count; k++) {
      const i = gr.start + k;
      this.size[i] = on ? gr.size : 0;
      if (color) this.col.set(color, i * 3);
    }
    this._dirtyAll = true;
  }

  setItem(name, k, on, color) {
    const gr = this.groups[name];
    const i = gr.start + k;
    this.size[i] = on ? gr.size : 0;
    if (color) this.col.set(color, i * 3);
    this._dirtyAll = true;
  }

  beginDynamic() { this.n = this.staticCount; }

  add(x, y, z, r, g, b, size) {
    if (this.n >= this.cap) return;
    const i = this.n++;
    const p = this.pos, c = this.col;
    p[i * 3] = x; p[i * 3 + 1] = y; p[i * 3 + 2] = z;
    c[i * 3] = r; c[i * 3 + 1] = g; c[i * 3 + 2] = b;
    this.size[i] = size;
  }

  end(camera, renderer, fog) {
    const h = renderer.domElement.height;
    this.uniforms.projScale.value = h / (2 * Math.tan((camera.fov * Math.PI) / 360));
    if (fog) { this.uniforms.fogNear.value = fog.far * 0.55; this.uniforms.fogFar.value = fog.far * 1.05; }
    this.points.geometry.setDrawRange(0, this.n);
    // статика заливается целиком только при изменениях, динамика — только свой хвост
    if (this._dirtyAll) {
      for (const a of [this.aPos, this.aCol, this.aSize]) { a.clearUpdateRanges(); a.needsUpdate = true; }
      this._dirtyAll = false;
    } else if (this.n > this.staticCount) {
      const s = this.staticCount, cnt = this.n - s;
      this.aPos.clearUpdateRanges(); this.aPos.addUpdateRange(s * 3, cnt * 3); this.aPos.needsUpdate = true;
      this.aCol.clearUpdateRanges(); this.aCol.addUpdateRange(s * 3, cnt * 3); this.aCol.needsUpdate = true;
      this.aSize.clearUpdateRanges(); this.aSize.addUpdateRange(s, cnt); this.aSize.needsUpdate = true;
    }
  }
}
