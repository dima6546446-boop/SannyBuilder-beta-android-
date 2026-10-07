import { PHYS, SURFACES } from './config.js';

const CELL = 16;
const key = (cx, cz) => cx * 73856093 ^ cz * 19349663;

/** Расстояние от точки до отрезка (возвращает ближайшую точку через out). */
function closestOnSegment(px, pz, ax, az, bx, bz, out) {
  const dx = bx - ax, dz = bz - az;
  const l2 = dx * dx + dz * dz;
  let t = l2 > 1e-9 ? ((px - ax) * dx + (pz - az) * dz) / l2 : 0;
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  out.x = ax + dx * t; out.z = az + dz * t;
  return Math.hypot(px - out.x, pz - out.z);
}

/** Прямоугольник с поворотом -> 4 отрезка. */
export function boxSegments(x, z, w, d, rot) {
  const c = Math.cos(rot), s = Math.sin(rot);
  const pts = [[-w / 2, -d / 2], [w / 2, -d / 2], [w / 2, d / 2], [-w / 2, d / 2]].map(([px, pz]) => [x + px * c + pz * s, z - px * s + pz * c]);
  const segs = [];
  for (let i = 0; i < 4; i++) { const a = pts[i], b = pts[(i + 1) % 4]; segs.push([a[0], a[1], b[0], b[1]]); }
  return segs;
}

/**
 * Мир: статические препятствия (отрезки/круги), динамические объекты (конусы, бочки),
 * покрытие под колёсами и обработка ударов (импульсы с трением и упругостью).
 */
export class World {
  constructor(map) {
    this.map = map;
    this.segs = [];
    this.grid = new Map();
    this.circles = map.circles ? map.circles.slice() : [];
    for (const s of map.segments) this.addSegment(s);
    for (const b of map.boxes || []) for (const s of boxSegments(b.x, b.z, b.w, b.d, b.rot || 0)) this.addSegment(s);
    this.props = (map.props || []).map((p, i) => ({ id: i, type: p.type, x: p.x, z: p.z, x0: p.x, z0: p.z, vx: 0, vz: 0, r: p.r || 0.22, m: p.m || 4, rot: 0, spin: 0, hit: false, z0h: 0 }));
    this.tmp = { x: 0, z: 0 };
    this.regions = (map.surfaces || []).slice().reverse();
    this.ground = map.ground || 'asphalt';
    this.cache = null;
  }

  addSegment(s) {
    const idx = this.segs.length;
    this.segs.push(s);
    const minx = Math.floor(Math.min(s[0], s[2]) / CELL), maxx = Math.floor(Math.max(s[0], s[2]) / CELL);
    const minz = Math.floor(Math.min(s[1], s[3]) / CELL), maxz = Math.floor(Math.max(s[1], s[3]) / CELL);
    for (let cx = minx; cx <= maxx; cx++) for (let cz = minz; cz <= maxz; cz++) {
      const k = key(cx, cz);
      let a = this.grid.get(k);
      if (!a) { a = []; this.grid.set(k, a); }
      a.push(idx);
    }
  }

  /** Покрытие в точке. */
  surfaceAt(x, z) {
    for (const r of this.regions) {
      if (r.kind === 'rect') {
        if (x >= r.x0 && x <= r.x1 && z >= r.z0 && z <= r.z1) return r.type;
      } else if (r.kind === 'circle') {
        if (Math.hypot(x - r.x, z - r.z) <= r.r) return r.type;
      } else if (r.kind === 'ribbon') {
        if (x < r.minx || x > r.maxx || z < r.minz || z > r.maxz) continue;
        const p = r.pts, hw = r.hw, tmp = this.tmp;
        const n = r.closed ? p.length : p.length - 1;
        for (let i = 0; i < n; i++) {
          const a = p[i], b = p[(i + 1) % p.length];
          if (closestOnSegment(x, z, a[0], a[1], b[0], b[1], tmp) <= hw) return r.type;
        }
      }
    }
    return this.ground;
  }

  /** Сегменты рядом с точкой. */
  nearSegments(x, z, r, out) {
    out.length = 0;
    const cx0 = Math.floor((x - r) / CELL), cx1 = Math.floor((x + r) / CELL);
    const cz0 = Math.floor((z - r) / CELL), cz1 = Math.floor((z + r) / CELL);
    for (let cx = cx0; cx <= cx1; cx++) for (let cz = cz0; cz <= cz1; cz++) {
      const a = this.grid.get(key(cx, cz));
      if (!a) continue;
      for (const i of a) if (!out.includes(i)) out.push(i);
    }
    return out;
  }

  /** Круги столкновения машины (мировые координаты). */
  carCircles(car, out) {
    const s = car.spec, r = s.width * 0.5 * 0.92;
    const half = Math.max(0, s.length * 0.5 - r);
    const fx = Math.sin(car.h), fz = Math.cos(car.h);
    const cx = car.x, cz = car.z;
    out.length = 0;
    const n = 3;
    for (let i = 0; i < n; i++) {
      const t = (i / (n - 1) * 2 - 1) * half;
      out.push({ x: cx + fx * t, z: cz + fz * t, r });
    }
    return out;
  }

  /**
   * Столкновения машины с миром. Возвращает наибольшую скорость удара (м/с) за шаг; события в onImpact.
   */
  collide(car, dt, onImpact) {
    const C = PHYS.collision;
    const circles = this._cc || (this._cc = []);
    this.carCircles(car, circles);
    const near = this._near || (this._near = []);
    const cp = this._cp || (this._cp = { x: 0, z: 0 });
    let maxImpact = 0;
    const m = car.spec.mass, Iz = car.Iz;
    for (let iter = 0; iter < 2; iter++) {
      for (const c of circles) {
        // статические отрезки
        this.nearSegments(c.x, c.z, c.r + 0.5, near);
        for (const idx of near) {
          const s = this.segs[idx];
          const d = closestOnSegment(c.x, c.z, s[0], s[1], s[2], s[3], cp);
          if (d < c.r) {
            let nx, nz;
            if (d > 1e-6) { nx = (c.x - cp.x) / d; nz = (c.z - cp.z) / d; }
            else { const lx = s[2] - s[0], lz = s[3] - s[1], ll = Math.hypot(lx, lz) || 1; nx = -lz / ll; nz = lx / ll; }
            maxImpact = Math.max(maxImpact, this.resolve(car, c, cp.x, cp.z, nx, nz, c.r - d, Infinity, m, Iz, C, onImpact, 'wall'));
            this.carCircles(car, circles); // позиция изменилась
          }
        }
        for (const o of this.circles) {
          const d = Math.hypot(c.x - o.x, c.z - o.z);
          if (d < c.r + o.r) {
            const nx = d > 1e-6 ? (c.x - o.x) / d : 1, nz = d > 1e-6 ? (c.z - o.z) / d : 0;
            maxImpact = Math.max(maxImpact, this.resolve(car, c, o.x + nx * o.r, o.z + nz * o.r, nx, nz, c.r + o.r - d, Infinity, m, Iz, C, onImpact, 'pole'));
            this.carCircles(car, circles);
          }
        }
        // динамические объекты
        for (const p of this.props) {
          const dx = c.x - p.x, dz = c.z - p.z;
          if (dx > 6 || dx < -6 || dz > 6 || dz < -6) continue;
          const d = Math.hypot(dx, dz);
          if (d < c.r + p.r) {
            const nx = d > 1e-6 ? dx / d : 1, nz = d > 1e-6 ? dz / d : 0;
            // лёгкие предметы не считаются «ударом» для серии — возвращаемое значение не учитываем
            this.resolve(car, c, p.x + nx * p.r, p.z + nz * p.r, nx, nz, 0, p.m, m, Iz, C, onImpact, 'prop', p);
            p.hit = true;
            this.carCircles(car, circles);
          }
        }
      }
    }
    return maxImpact;
  }

  /** Импульс в точке контакта. other — масса второго тела (Infinity для стен). */
  resolve(car, c, px, pz, nx, nz, pen, otherMass, m, Iz, C, onImpact, kind, prop) {
    // раздвигаем
    if (pen > 0) { car.x += nx * pen; car.z += nz * pen; }
    const rx = px - car.x, rz = pz - car.z;
    // скорость точки контакта машины
    const vpx = car.vx + car.w * rz, vpz = car.vz - car.w * rx;
    let ovx = 0, ovz = 0;
    if (prop) { ovx = prop.vx; ovz = prop.vz; }
    const rvx = vpx - ovx, rvz = vpz - ovz;
    const vn = rvx * nx + rvz * nz;
    if (vn >= 0) return 0;
    const rn = rz * nx - rx * nz;
    const invM = 1 / m + (isFinite(otherMass) ? 1 / otherMass : 0);
    const e = prop ? 0.5 : C.restitution;
    const denom = invM + (rn * rn) / Iz;
    const j = -(1 + e) * vn / denom;
    car.vx += nx * j / m; car.vz += nz * j / m;
    car.w += (rz * nx * j - rx * nz * j) / Iz;                // момент: τ = pz·Fx − px·Fz
    // трение вдоль поверхности
    const tx = -nz, tz = nx;
    const vt = rvx * tx + rvz * tz;
    const jt = Math.max(-C.friction * j, Math.min(C.friction * j, -vt / (invM + 1e-6)));
    car.vx += tx * jt / m; car.vz += tz * jt / m;
    car.w += (rz * (tx * jt) - rx * (tz * jt)) / Iz;
    if (prop) {
      prop.vx -= nx * j / prop.m; prop.vz -= nz * j / prop.m;
      prop.vx -= tx * jt / prop.m; prop.vz -= tz * jt / prop.m;
      prop.spin += (Math.random() - 0.5) * 8;
    }
    const impact = -vn;
    if (onImpact && impact > 0.5) onImpact({ speed: impact, x: px, z: pz, nx, nz, kind, prop });
    return impact;
  }

  /** Движение динамических объектов. */
  stepProps(dt) {
    const near = this._near2 || (this._near2 = []);
    const cp = this._cp2 || (this._cp2 = { x: 0, z: 0 });
    for (const p of this.props) {
      if (!p.hit && p.vx === 0 && p.vz === 0) continue;
      p.x += p.vx * dt; p.z += p.vz * dt;
      const sp = Math.hypot(p.vx, p.vz);
      if (sp > 0.01) {
        const f = Math.max(0, 1 - (6.5 / Math.max(sp, 0.5)) * dt);
        p.vx *= f; p.vz *= f;
      } else { p.vx = p.vz = 0; }
      p.rot += p.spin * dt; p.spin *= Math.max(0, 1 - 2.5 * dt);
      this.nearSegments(p.x, p.z, p.r + 0.3, near);
      for (const idx of near) {
        const s = this.segs[idx];
        const d = closestOnSegment(p.x, p.z, s[0], s[1], s[2], s[3], cp);
        if (d < p.r) {
          const nx = d > 1e-6 ? (p.x - cp.x) / d : 1, nz = d > 1e-6 ? (p.z - cp.z) / d : 0;
          p.x += nx * (p.r - d); p.z += nz * (p.r - d);
          const vn = p.vx * nx + p.vz * nz;
          if (vn < 0) { p.vx -= 1.5 * vn * nx; p.vz -= 1.5 * vn * nz; }
        }
      }
    }
  }

  resetProps() { for (const p of this.props) { p.x = p.x0; p.z = p.z0; p.vx = p.vz = 0; p.rot = 0; p.spin = 0; p.hit = false; } }
}

export { closestOnSegment };
