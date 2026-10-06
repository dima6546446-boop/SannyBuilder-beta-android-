import * as THREE from 'three';

/**
 * Дождь: штрихи капель в коробке вокруг камеры (LineSegments, 1 draw call).
 * Капли переиспользуются (вылетели снизу — появились сверху), аллокаций в цикле нет.
 * В виде из салона капли рядом с камерой прячутся — не «идёт дождь внутри машины».
 */
export class Rain {
  constructor(scene, count) {
    this.n = count;
    this.box = { x: 34, y: 16, z: 34 };
    this.p = new Float32Array(count * 3);
    for (let i = 0; i < count; i++) {
      this.p[i * 3] = (Math.random() - 0.5) * this.box.x;
      this.p[i * 3 + 1] = Math.random() * this.box.y;
      this.p[i * 3 + 2] = (Math.random() - 0.5) * this.box.z;
    }
    this.pos = new Float32Array(count * 6);
    const g = new THREE.BufferGeometry();
    this.attr = new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage);
    g.setAttribute('position', this.attr);
    this.mat = new THREE.LineBasicMaterial({ color: 0xb8c4d0, transparent: true, opacity: 0, depthWrite: false, fog: true });
    this.mesh = new THREE.LineSegments(g, this.mat);
    this.mesh.frustumCulled = false;
    this.mesh.name = 'Rain';
    this.mesh.visible = false;
    this.mesh.renderOrder = 5;
    scene.add(this.mesh);
  }

  update(dt, camera, level, light, interior) {
    this.mesh.visible = level > 0.02;
    if (!this.mesh.visible) return;
    this.mat.opacity = 0.42 * level;
    this.mat.color.setRGB(0.45 + 0.4 * light, 0.5 + 0.4 * light, 0.55 + 0.4 * light);
    const c = camera.position, B = this.box;
    const fall = 16 * dt, wind = 1.2 * dt, len = 0.55;
    const active = Math.floor(this.n * Math.min(1, 0.3 + level * 0.7));
    const p = this.p, o = this.pos;
    for (let i = 0; i < this.n; i++) {
      const k = i * 3, j = i * 6;
      if (i >= active) { o[j + 1] = o[j + 4] = -100; continue; }
      p[k + 1] -= fall; p[k] += wind;
      if (p[k + 1] < 0) { p[k + 1] += B.y; p[k] = (Math.random() - 0.5) * B.x; p[k + 2] = (Math.random() - 0.5) * B.z; }
      if (p[k] > B.x / 2) p[k] -= B.x;
      const x = c.x + p[k], y = c.y - 4 + p[k + 1], z = c.z + p[k + 2];
      if (interior && Math.abs(p[k]) < 1.4 && Math.abs(p[k + 2]) < 1.8) { o[j + 1] = o[j + 4] = -100; continue; }
      o[j] = x; o[j + 1] = y; o[j + 2] = z;
      o[j + 3] = x - 0.04; o[j + 4] = y + len; o[j + 5] = z;
    }
    this.attr.needsUpdate = true;
  }
}
