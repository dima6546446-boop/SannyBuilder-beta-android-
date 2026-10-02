import * as THREE from 'three';

/**
 * Следы шин: кольцевой буфер квадов в одной геометрии (1 draw call, 0 аллокаций).
 */
export class SkidMarks {
  constructor(scene, maxSegments = 500) {
    this.max = maxSegments;
    this.pos = new Float32Array(maxSegments * 6 * 3);
    const g = new THREE.BufferGeometry();
    this.attr = new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage);
    g.setAttribute('position', this.attr);
    this.mesh = new THREE.Mesh(g, new THREE.MeshBasicMaterial({
      color: 0x000000, transparent: true, opacity: 0.45, depthWrite: false,
      polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3,
    }));
    this.mesh.name = 'SkidMarks';
    this.mesh.frustumCulled = false;
    this.mesh.renderOrder = 1;
    scene.add(this.mesh);
    this.head = 0;
    this.last = [null, null];
  }

  /** Добавить точку для колеса w (0/1). active=false обрывает след. */
  add(w, x, y, z, active) {
    if (!active) { this.last[w] = null; return; }
    const l = this.last[w];
    if (!l) { this.last[w] = [x, y, z]; return; }
    const dx = x - l[0], dz = z - l[2];
    const d = Math.hypot(dx, dz);
    if (d < 0.35) return;
    const hw = 0.09;
    const nx = (-dz / d) * hw, nz = (dx / d) * hw;
    const yy = y + 0.015;
    const i = this.head * 18;
    const p = this.pos;
    const v = (k, X, Z) => { p[i + k * 3] = X; p[i + k * 3 + 1] = yy; p[i + k * 3 + 2] = Z; };
    // два треугольника, обход против часовой сверху
    v(0, l[0] + nx, l[2] + nz); v(1, l[0] - nx, l[2] - nz); v(2, x - nx, z - nz);
    v(3, l[0] + nx, l[2] + nz); v(4, x - nx, z - nz); v(5, x + nx, z + nz);
    this.head = (this.head + 1) % this.max;
    this.attr.needsUpdate = true;
    this.last[w] = [x, y, z];
  }
}
