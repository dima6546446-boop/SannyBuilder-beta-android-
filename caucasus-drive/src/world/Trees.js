import * as THREE from 'three';

// kind: 0 — берёза, 1 — липа, 2 — сосна/ель (за городом)
const CROWN_COLORS = [[0.42, 0.62, 0.22], [0.2, 0.42, 0.14], [0.12, 0.28, 0.12]];
const TRUNK_COLORS = [[0.85, 0.83, 0.78], [0.35, 0.27, 0.2], [0.4, 0.28, 0.18]];

/**
 * Деревья: InstancedMesh с двумя уровнями LOD.
 *  LOD0 (ближние): ствол + крона-икосаэдр detail 1 (80 треуг.)
 *  LOD1 (дальние): только крона detail 0 (20 треуг.)
 *  Дальше drawDistance — не рисуются вовсе.
 * Перераспределение по LOD — раз в 0.4 с, копированием заранее посчитанных матриц.
 */
export class Trees {
  constructor(scene, spots, quality) {
    this.q = quality;
    this.n = spots.length;
    const n = this.n;
    this.x = new Float32Array(n);
    this.z = new Float32Array(n);
    this.trunkM = new Float32Array(n * 16);
    this.crownM = new Float32Array(n * 16);
    this.trunkC = new Float32Array(n * 3);
    this.crownC = new Float32Array(n * 3);

    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), p = new THREE.Vector3(), s = new THREE.Vector3();
    const up = new THREE.Vector3(0, 1, 0);
    spots.forEach((t, i) => {
      this.x[i] = t.x; this.z[i] = t.z;
      const trunkH = t.kind === 2 ? 5 : 4;
      q.setFromAxisAngle(up, Math.random() * Math.PI * 2);
      m.compose(p.set(t.x, t.y, t.z), q, s.set(t.s, trunkH * t.s, t.s));
      m.toArray(this.trunkM, i * 16);
      const cs = t.kind === 2 ? [1.6, 4.2, 1.6] : t.kind === 0 ? [1.9, 3.0, 1.9] : [2.5, 2.4, 2.5];
      const cy = t.y + trunkH * t.s + cs[1] * t.s * 0.55;
      m.compose(p.set(t.x, cy, t.z), q, s.set(cs[0] * t.s, cs[1] * t.s, cs[2] * t.s));
      m.toArray(this.crownM, i * 16);
      const v = 0.85 + Math.random() * 0.3;
      const cc = CROWN_COLORS[t.kind], tc = TRUNK_COLORS[t.kind];
      this.crownC.set([cc[0] * v, cc[1] * v, cc[2] * v], i * 3);
      this.trunkC.set(tc, i * 3);
    });

    const trunkGeo = new THREE.CylinderGeometry(0.09, 0.15, 1, 5, 1, true).translate(0, 0.5, 0);
    const mat = new THREE.MeshLambertMaterial({ color: 0xffffff });
    const mk = (geo, name, cast) => {
      const im = new THREE.InstancedMesh(geo, mat, Math.max(n, 1));
      im.name = name;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      im.setColorAt(0, new THREE.Color(1, 1, 1));
      im.instanceColor.setUsage(THREE.DynamicDrawUsage);
      im.frustumCulled = false; // собственный distance culling; bounding sphere инстансов меняется
      im.castShadow = cast && quality.shadows && quality.name === 'high';
      im.userData.cast = im.castShadow;
      im.count = 0;
      scene.add(im);
      return im;
    };
    this.trunks = mk(trunkGeo, 'TreeTrunksLOD0', true);
    this.crowns = mk(new THREE.IcosahedronGeometry(1, 1), 'TreeCrownsLOD0', true);
    this.far = mk(new THREE.IcosahedronGeometry(1, 0), 'TreeCrownsLOD1', false);
    this._timer = 1;
  }

  update(dt, camPos) {
    this._timer += dt;
    if (this._timer < 0.4) return;
    this._timer = 0;
    const near2 = this.q.treeLodDistance ** 2, far2 = this.q.drawDistance ** 2;
    const cx = camPos.x, cz = camPos.z;
    const tA = this.trunks.instanceMatrix.array, cA = this.crowns.instanceMatrix.array, fA = this.far.instanceMatrix.array;
    const tC = this.trunks.instanceColor.array, cC = this.crowns.instanceColor.array, fC = this.far.instanceColor.array;
    let a = 0, b = 0;
    for (let i = 0; i < this.n; i++) {
      const dx = this.x[i] - cx, dz = this.z[i] - cz;
      const d2 = dx * dx + dz * dz;
      if (d2 > far2) continue;
      if (d2 < near2) {
        tA.set(this.trunkM.subarray(i * 16, i * 16 + 16), a * 16);
        cA.set(this.crownM.subarray(i * 16, i * 16 + 16), a * 16);
        tC.set(this.trunkC.subarray(i * 3, i * 3 + 3), a * 3);
        cC.set(this.crownC.subarray(i * 3, i * 3 + 3), a * 3);
        a++;
      } else {
        fA.set(this.crownM.subarray(i * 16, i * 16 + 16), b * 16);
        fC.set(this.crownC.subarray(i * 3, i * 3 + 3), b * 3);
        b++;
      }
    }
    this.trunks.count = this.crowns.count = a;
    this.far.count = b;
    for (const im of [this.trunks, this.crowns, this.far]) {
      im.instanceMatrix.needsUpdate = true;
      im.instanceColor.needsUpdate = true;
    }
  }
}
