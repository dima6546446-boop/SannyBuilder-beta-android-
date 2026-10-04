import * as THREE from 'three';

/**
 * Следы шин: кольцевой буфер квадов в одной геометрии (1 draw call, 0 аллокаций).
 * Яркость и ширина следа зависят от силы скольжения k (угол заноса × скорость):
 * лёгкий визг — бледная узкая полоса, глубокий занос — широкий чёрный след.
 */
export class SkidMarks {
  constructor(scene, maxSegments = 500) {
    this.max = maxSegments;
    this.pos = new Float32Array(maxSegments * 6 * 3);
    this.col = new Float32Array(maxSegments * 6 * 4); // rgba: rgb = 0, a — плотность следа
    const g = new THREE.BufferGeometry();
    this.attr = new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage);
    this.cAttr = new THREE.BufferAttribute(this.col, 4).setUsage(THREE.DynamicDrawUsage);
    g.setAttribute('position', this.attr);
    g.setAttribute('color', this.cAttr);
    this.mesh = new THREE.Mesh(g, new THREE.MeshBasicMaterial({
      color: 0x000000, vertexColors: true, transparent: true, depthWrite: false,
      polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3,
    }));
    this.mesh.name = 'SkidMarks';
    this.mesh.frustumCulled = false;
    this.mesh.renderOrder = 1;
    scene.add(this.mesh);
    this.head = 0;
    this.used = 0;
    // последняя точка колеса: x, y, z, плотность; on — след продолжается
    this.lastPt = [new Float32Array(4), new Float32Array(4)];
    this.on = [false, false];
    this.mesh.geometry.setDrawRange(0, 0);
  }

  /** Совместимость: place() обрывает следы. */
  set last(v) { this.on[0] = this.on[1] = false; void v; }

  /** Добавить точку для колеса w (0/1). active=false обрывает след; k — сила скольжения 0..1. */
  add(w, x, y, z, active, k = 0.6) {
    if (!active) { this.on[w] = false; return; }
    const l = this.lastPt[w];
    if (!this.on[w]) { l[0] = x; l[1] = y; l[2] = z; l[3] = k; this.on[w] = true; return; }
    const dx = x - l[0], dz = z - l[2];
    const d = Math.hypot(dx, dz);
    if (d < 0.35) return;
    const hw = 0.07 + 0.06 * k;
    const nx = (-dz / d) * hw, nz = (dx / d) * hw;
    const yy = y + 0.015;
    const i = this.head * 18;
    const p = this.pos;
    const v = (n, X, Z) => { p[i + n * 3] = X; p[i + n * 3 + 1] = yy; p[i + n * 3 + 2] = Z; };
    // два треугольника, обход против часовой сверху
    v(0, l[0] + nx, l[2] + nz); v(1, l[0] - nx, l[2] - nz); v(2, x - nx, z - nz);
    v(3, l[0] + nx, l[2] + nz); v(4, x - nx, z - nz); v(5, x + nx, z + nz);
    const a0 = 0.12 + 0.48 * l[3], a1 = 0.12 + 0.48 * k;
    const c = this.col, j = this.head * 24;
    for (let n = 0; n < 6; n++) c[j + n * 4 + 3] = n === 2 || n === 4 || n === 5 ? a1 : a0;
    this.head = (this.head + 1) % this.max;
    if (this.used < this.max) { this.used++; this.mesh.geometry.setDrawRange(0, this.used * 6); }
    this.attr.needsUpdate = true;
    this.cAttr.needsUpdate = true;
    l[0] = x; l[1] = y; l[2] = z; l[3] = k;
  }
}
