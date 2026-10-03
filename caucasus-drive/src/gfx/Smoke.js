import * as THREE from 'three';

const VERT = /* glsl */`
attribute float size;
attribute float alpha;
uniform float projScale;
varying float vAlpha;
void main() {
  vec4 mv = modelViewMatrix * vec4(position, 1.0);
  vAlpha = alpha;
  gl_PointSize = clamp(size * projScale / max(-mv.z, 0.1), 0.0, 256.0);
  gl_Position = projectionMatrix * mv;
}`;
const FRAG = /* glsl */`
uniform sampler2D map;
uniform vec3 tint;
varying float vAlpha;
void main() {
  float a = texture2D(map, gl_PointCoord).a * vAlpha;
  if (a < 0.01) discard;
  gl_FragColor = vec4(tint, a);
}`;

/** Дым из-под колёс при заносе/пробуксовке и пыль на грунте. Пул частиц, 1 draw call. */
export class Smoke {
  constructor(scene, texture, max = 160) {
    this.max = max;
    this.p = new Float32Array(max * 3);
    this.v = new Float32Array(max * 3);
    this.size = new Float32Array(max);
    this.alpha = new Float32Array(max);
    this.life = new Float32Array(max);
    this.maxLife = new Float32Array(max);
    this.grow = new Float32Array(max);
    this.a0 = new Float32Array(max);
    const g = new THREE.BufferGeometry();
    this.aP = new THREE.BufferAttribute(this.p, 3).setUsage(THREE.DynamicDrawUsage);
    this.aS = new THREE.BufferAttribute(this.size, 1).setUsage(THREE.DynamicDrawUsage);
    this.aA = new THREE.BufferAttribute(this.alpha, 1).setUsage(THREE.DynamicDrawUsage);
    g.setAttribute('position', this.aP);
    g.setAttribute('size', this.aS);
    g.setAttribute('alpha', this.aA);
    this.uniforms = { map: { value: texture }, projScale: { value: 500 }, tint: { value: new THREE.Color(0.8, 0.8, 0.8) } };
    this.points = new THREE.Points(g, new THREE.ShaderMaterial({
      uniforms: this.uniforms, vertexShader: VERT, fragmentShader: FRAG, transparent: true, depthWrite: false,
    }));
    this.points.frustumCulled = false;
    this.points.renderOrder = 4;
    scene.add(this.points);
    this.head = 0;
    this.active = 0;
  }

  emit(x, y, z, vx, vz, dust = false) {
    const i = this.head;
    this.head = (this.head + 1) % this.max;
    this.p[i * 3] = x + (Math.random() - 0.5) * 0.3;
    this.p[i * 3 + 1] = y + 0.2;
    this.p[i * 3 + 2] = z + (Math.random() - 0.5) * 0.3;
    this.v[i * 3] = vx * 0.2 + (Math.random() - 0.5) * 0.8;
    this.v[i * 3 + 1] = 0.5 + Math.random() * 0.6;
    this.v[i * 3 + 2] = vz * 0.2 + (Math.random() - 0.5) * 0.8;
    this.maxLife[i] = this.life[i] = dust ? 1.0 : 1.8 + Math.random();
    this.size[i] = 0.6;
    this.alpha[i] = this.a0[i] = dust ? 0.35 : 0.5;
    this.grow[i] = 2.2;
  }

  /** Маленький клуб дыма (сигарета, выдох): без случайного разброса, медленный рост. */
  emitPuff(x, y, z, vx, vy, vz, size, alpha, life) {
    const i = this.head;
    this.head = (this.head + 1) % this.max;
    this.p[i * 3] = x; this.p[i * 3 + 1] = y; this.p[i * 3 + 2] = z;
    this.v[i * 3] = vx; this.v[i * 3 + 1] = vy; this.v[i * 3 + 2] = vz;
    this.maxLife[i] = this.life[i] = life;
    this.size[i] = size;
    this.alpha[i] = this.a0[i] = alpha;
    this.grow[i] = size * 3;
  }

  update(dt, camera, renderer, light) {
    for (let i = 0; i < this.max; i++) {
      if (this.life[i] <= 0) { this.alpha[i] = 0; this.size[i] = 0; continue; }
      this.life[i] -= dt;
      const k = Math.max(0, this.life[i] / this.maxLife[i]);
      this.p[i * 3] += this.v[i * 3] * dt;
      this.p[i * 3 + 1] += this.v[i * 3 + 1] * dt;
      this.p[i * 3 + 2] += this.v[i * 3 + 2] * dt;
      this.v[i * 3] *= 0.97; this.v[i * 3 + 2] *= 0.97;
      this.size[i] += dt * this.grow[i];
      this.alpha[i] = this.a0[i] * Math.min(1, k * 1.6);
    }
    this.uniforms.projScale.value = renderer.domElement.height / (2 * Math.tan((camera.fov * Math.PI) / 360));
    this.uniforms.tint.value.setScalar(0.25 + 0.6 * light);
    this.aP.needsUpdate = this.aS.needsUpdate = this.aA.needsUpdate = true;
  }
}
