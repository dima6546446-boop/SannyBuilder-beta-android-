import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

const KEEP = ['position', 'normal', 'uv'];

/**
 * Приводит геометрию к единому формату для слияния (non-indexed, position/normal/uv),
 * опционально добавляет вертексный цвет. Слияние статики — главный способ
 * сократить draw calls на мобильных GPU.
 */
export function prep(geo, color) {
  let g = geo;
  if (g.index) { g = geo.toNonIndexed(); geo.dispose(); }
  for (const name of Object.keys(g.attributes)) if (!KEEP.includes(name)) g.deleteAttribute(name);
  if (!g.attributes.normal) g.computeVertexNormals();
  if (!g.attributes.uv) {
    g.setAttribute('uv', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 2), 2));
  }
  if (color !== undefined) setColor(g, color);
  g.clearGroups();
  return g;
}

export function setColor(g, color) {
  const c = color instanceof THREE.Color ? color : new THREE.Color(color);
  const n = g.attributes.position.count;
  const arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
  g.setAttribute('color', new THREE.BufferAttribute(arr, 3));
  return g;
}

export function merge(list) {
  if (list.length === 0) return new THREE.BufferGeometry();
  const m = mergeGeometries(list, false);
  if (!m) throw new Error('mergeGeometries: несовместимые атрибуты');
  for (const g of list) g.dispose();
  m.computeBoundingSphere();
  m.computeBoundingBox();
  return m;
}

export function scaleUV(geo, su, sv = su) {
  const uv = geo.attributes.uv;
  for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * su, uv.getY(i) * sv);
  uv.needsUpdate = true;
  return geo;
}

/**
 * Сборщик произвольных четырёхугольников в одну non-indexed геометрию.
 * Используется для зданий, разметки, световых пятен — всё уходит в 1 draw call.
 */
export class QuadBatch {
  constructor(withColor = false) {
    this.p = []; this.n = []; this.uv = []; this.c = withColor ? [] : null;
  }

  /** a,b,c,d — вершины [x,y,z] против часовой стрелки при взгляде с лицевой стороны. */
  quad(a, b, c, d, nx, ny, nz, uvs = [0, 0, 1, 0, 1, 1, 0, 1], col) {
    const V = [a, b, c, a, c, d];
    const U = [0, 1, 2, 0, 2, 3];
    for (let k = 0; k < 6; k++) {
      const v = V[k];
      this.p.push(v[0], v[1], v[2]);
      this.n.push(nx, ny, nz);
      this.uv.push(uvs[U[k] * 2], uvs[U[k] * 2 + 1]);
      if (this.c) this.c.push(col.r, col.g, col.b);
    }
  }

  /** Плоский прямоугольник на земле, ориентированный вдоль (dx,dz). */
  flat(cx, cz, dx, dz, len, wid, y, uvs, col) {
    const rx = -dz, rz = dx; // вектор «вправо» от направления
    const hl = len / 2, hw = wid / 2;
    const A = [cx - dx * hl - rx * hw, y, cz - dz * hl - rz * hw];
    const B = [cx - dx * hl + rx * hw, y, cz - dz * hl + rz * hw];
    const C = [cx + dx * hl + rx * hw, y, cz + dz * hl + rz * hw];
    const D = [cx + dx * hl - rx * hw, y, cz + dz * hl - rz * hw];
    this.quad(A, B, C, D, 0, 1, 0, uvs, col);
  }

  get empty() { return this.p.length === 0; }

  build() {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.p, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(this.n, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(this.uv, 2));
    if (this.c) g.setAttribute('color', new THREE.Float32BufferAttribute(this.c, 3));
    g.computeBoundingSphere();
    g.computeBoundingBox();
    return g;
  }
}
