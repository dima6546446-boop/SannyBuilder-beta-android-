/**
 * Лёгкий 2D-мир коллизий (вид сверху) со spatial hash.
 * Для аркадного симулятора на телефоне полноценный физический движок (Ammo/Rapier)
 * избыточен: машина — набор окружностей, статика — AABB и окружности.
 */
export class CollisionWorld {
  constructor(cellSize = 16) {
    this.cell = cellSize;
    this.map = new Map();
    this.stamp = 1;
    this.count = 0;
    this._tmp = [];
  }

  _key(ix, iz) { return (ix + 4096) * 8192 + (iz + 4096); }

  _insert(obj, minX, minZ, maxX, maxZ) {
    const c = this.cell;
    const x0 = Math.floor(minX / c), x1 = Math.floor(maxX / c);
    const z0 = Math.floor(minZ / c), z1 = Math.floor(maxZ / c);
    for (let ix = x0; ix <= x1; ix++) {
      for (let iz = z0; iz <= z1; iz++) {
        const k = this._key(ix, iz);
        let list = this.map.get(k);
        if (!list) { list = []; this.map.set(k, list); }
        list.push(obj);
      }
    }
    this.count++;
    return obj;
  }

  addBox(minX, minZ, maxX, maxZ, tag = 'wall') {
    const o = { type: 0, minX, minZ, maxX, maxZ, tag, mark: 0 };
    return this._insert(o, minX, minZ, maxX, maxZ);
  }

  addCircle(x, z, r, tag = 'pole') {
    const o = { type: 1, x, z, r, tag, mark: 0 };
    return this._insert(o, x - r, z - r, x + r, z + r);
  }

  /** Кандидаты в области (без дубликатов). Возвращает переиспользуемый массив. */
  query(minX, minZ, maxX, maxZ) {
    const out = this._tmp; out.length = 0;
    const stamp = ++this.stamp;
    const c = this.cell;
    const x0 = Math.floor(minX / c), x1 = Math.floor(maxX / c);
    const z0 = Math.floor(minZ / c), z1 = Math.floor(maxZ / c);
    for (let ix = x0; ix <= x1; ix++) {
      for (let iz = z0; iz <= z1; iz++) {
        const list = this.map.get(this._key(ix, iz));
        if (!list) continue;
        for (let i = 0; i < list.length; i++) {
          const o = list[i];
          if (o.mark === stamp) continue;
          o.mark = stamp;
          out.push(o);
        }
      }
    }
    return out;
  }

  /**
   * Контакт окружности со статикой. Вызывает cb(nx, nz, depth) для каждого пересечения.
   * Нормаль направлена от препятствия к окружности.
   */
  collideCircle(x, z, r, cb) {
    const list = this.query(x - r, z - r, x + r, z + r);
    for (let i = 0; i < list.length; i++) {
      const o = list[i];
      if (o.type === 0) {
        const cx = x < o.minX ? o.minX : x > o.maxX ? o.maxX : x;
        const cz = z < o.minZ ? o.minZ : z > o.maxZ ? o.maxZ : z;
        const dx = x - cx, dz = z - cz;
        const d2 = dx * dx + dz * dz;
        if (d2 >= r * r) continue;
        if (d2 > 1e-8) {
          const d = Math.sqrt(d2);
          cb(dx / d, dz / d, r - d, o);
        } else {
          // центр внутри прямоугольника — выталкиваем по кратчайшей оси
          const l = x - o.minX, rr = o.maxX - x, t = z - o.minZ, b = o.maxZ - z;
          const m = Math.min(l, rr, t, b);
          if (m === l) cb(-1, 0, l + r, o);
          else if (m === rr) cb(1, 0, rr + r, o);
          else if (m === t) cb(0, -1, t + r, o);
          else cb(0, 1, b + r, o);
        }
      } else {
        const dx = x - o.x, dz = z - o.z;
        const rs = r + o.r;
        const d2 = dx * dx + dz * dz;
        if (d2 >= rs * rs || d2 < 1e-8) continue;
        const d = Math.sqrt(d2);
        cb(dx / d, dz / d, rs - d, o);
      }
    }
  }

  /** Первое пересечение отрезка с прямоугольниками (для камеры). Возвращает t∈[0,1]. */
  segmentHit(x0, z0, x1, z1) {
    const list = this.query(Math.min(x0, x1), Math.min(z0, z1), Math.max(x0, x1), Math.max(z0, z1));
    let best = 1;
    const dx = x1 - x0, dz = z1 - z0;
    for (let i = 0; i < list.length; i++) {
      const o = list[i];
      if (o.type !== 0) continue;
      const t = segAabb(x0, z0, dx, dz, o.minX, o.minZ, o.maxX, o.maxZ);
      if (t < best) best = t;
    }
    return best;
  }
}

/** Slab-тест отрезка p + t*d (t∈[0,1]) с AABB. Возвращает t входа или 2 при промахе. */
export function segAabb(px, pz, dx, dz, minX, minZ, maxX, maxZ) {
  let tmin = 0, tmax = 1;
  if (Math.abs(dx) < 1e-9) {
    if (px < minX || px > maxX) return 2;
  } else {
    let t1 = (minX - px) / dx, t2 = (maxX - px) / dx;
    if (t1 > t2) { const t = t1; t1 = t2; t2 = t; }
    if (t1 > tmin) tmin = t1;
    if (t2 < tmax) tmax = t2;
    if (tmin > tmax) return 2;
  }
  if (Math.abs(dz) < 1e-9) {
    if (pz < minZ || pz > maxZ) return 2;
  } else {
    let t1 = (minZ - pz) / dz, t2 = (maxZ - pz) / dz;
    if (t1 > t2) { const t = t1; t1 = t2; t2 = t; }
    if (t1 > tmin) tmin = t1;
    if (t2 < tmax) tmax = t2;
    if (tmin > tmax) return 2;
  }
  return tmin;
}
