// Геометрические помощники для генерации карт.
export function rng(seed) {
  let a = seed >>> 0;
  return () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}

/** Срезание углов (Chaikin) — сглаживает ломаную. */
export function chaikin(pts, iters = 2, closed = false) {
  let p = pts;
  for (let k = 0; k < iters; k++) {
    const out = [];
    const n = p.length;
    if (!closed) out.push(p[0]);
    for (let i = 0; i < (closed ? n : n - 1); i++) {
      const a = p[i], b = p[(i + 1) % n];
      out.push([a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25]);
      out.push([a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75]);
    }
    if (!closed) out.push(p[n - 1]);
    p = out;
  }
  return p;
}

/** Ломаная, смещённая на d влево от направления движения (в осях x, z). */
export function offsetLine(pts, d, closed = false) {
  const n = pts.length, out = [];
  for (let i = 0; i < n; i++) {
    const a = pts[closed ? (i - 1 + n) % n : Math.max(0, i - 1)];
    const b = pts[closed ? (i + 1) % n : Math.min(n - 1, i + 1)];
    let dx = b[0] - a[0], dz = b[1] - a[1];
    const l = Math.hypot(dx, dz) || 1; dx /= l; dz /= l;
    out.push([pts[i][0] - dz * d, pts[i][1] + dx * d]);
  }
  return out;
}

export function polyToSegments(pts, closed = false, skip = null) {
  const segs = [];
  const n = pts.length;
  for (let i = 0; i < (closed ? n : n - 1); i++) {
    if (skip && skip(i)) continue;
    const a = pts[i], b = pts[(i + 1) % n];
    segs.push([a[0], a[1], b[0], b[1]]);
  }
  return segs;
}

export function rectSurface(type, x0, z0, x1, z1) { return { kind: 'rect', type, x0: Math.min(x0, x1), z0: Math.min(z0, z1), x1: Math.max(x0, x1), z1: Math.max(z0, z1) }; }
export function circleSurface(type, x, z, r) { return { kind: 'circle', type, x, z, r }; }
export function ribbonSurface(type, pts, hw, closed = false) {
  let minx = Infinity, maxx = -Infinity, minz = Infinity, maxz = -Infinity;
  for (const p of pts) { minx = Math.min(minx, p[0]); maxx = Math.max(maxx, p[0]); minz = Math.min(minz, p[1]); maxz = Math.max(maxz, p[1]); }
  return { kind: 'ribbon', type, pts, hw, closed, minx: minx - hw, maxx: maxx + hw, minz: minz - hw, maxz: maxz + hw };
}

export function lineLength(pts) { let l = 0; for (let i = 1; i < pts.length; i++) l += Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]); return l; }

/** Точки вдоль ломаной с шагом step. */
export function sampleLine(pts, step, closed = false) {
  const out = [];
  let carry = 0;
  const n = pts.length;
  for (let i = 0; i < (closed ? n : n - 1); i++) {
    const a = pts[i], b = pts[(i + 1) % n];
    const dx = b[0] - a[0], dz = b[1] - a[1], l = Math.hypot(dx, dz);
    let t = carry;
    while (t < l) { out.push({ x: a[0] + dx * t / l, z: a[1] + dz * t / l, dx: dx / l, dz: dz / l }); t += step; }
    carry = t - l;
  }
  return out;
}
