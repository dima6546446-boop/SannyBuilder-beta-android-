/**
 * Судейство дрифта (без DOM и Three.js — работает и в headless-тесте на Node).
 *
 * Очки в секунду = угол (до 60°) × скорость × плавность × близость к препятствию × зона.
 * Серия — несколько заносов подряд: множитель растёт за длительность заноса (+0,5 каждые 2,5 с)
 * и за связки (новый занос в течение 1,2 с после предыдущего: +0,5, перекладка в другую
 * сторону: +1), до ×5. Итог серии = очки × множитель; он «сохраняется» (bank), когда машина
 * выходит из заноса и не входит в новый за окно связки. Удар, разворот или съезд на газон —
 * серия сгорает. В городе в ту же серию идут «шашки» (bonus): одна серия — одна выплата.
 */
export const DRIFT = {
  enterDeg: 12, stayDeg: 7, enterKmh: 20, stayKmh: 15, spinDeg: 105,
  linkTime: 1.2, holdStep: 2.5, maxMult: 5, nearDist: 1.5,
};

const DEG = 180 / Math.PI;

export class DriftScore {
  constructor() {
    this.onBank = null;   // (total, info) — серия сохранена
    this.onLost = null;   // (reason, pts) — серия сгорела
    this.onEvent = null;  // (text) — «СВЯЗКА», «ПЕРЕКЛАДКА», «БЛИЗКО!»
    this.reset();
  }

  reset() {
    this.active = false;  // сейчас в заносе
    this.angle = 0;       // текущий угол, градусы (со знаком)
    this.pts = 0;         // очки серии без множителя
    this.mult = 1;
    this.dir = 0;         // сторона текущего/последнего заноса
    this.holdT = 0;       // длительность текущего заноса
    this.gapT = 0;        // время после выхода из заноса
    this.rate = 0;        // сглаженная скорость изменения угла, °/с
    this.smooth = 1;
    this.near = Infinity;
    this.nearHit = false; // «БЛИЗКО!» уже засчитано в этом заносе
    this.drifts = 0;      // заносов в серии
    this.maxAngle = 0;
    this.time = 0;        // суммарное время в заносе в серии
    this.keepT = 0;       // после бонуса серия живёт дольше окна связки
    this.bonuses = 0;     // «шашек» в серии
  }

  get inSeries() { return this.pts > 0; }
  get total() { return Math.round(this.pts * this.mult); }

  /**
   * p — VehiclePhysics; opts: { grass, near (м до стены/машины), zone (множитель зоны) }.
   */
  update(dt, p, opts = {}) {
    const a = p.driftAngle * DEG, abs = Math.abs(a), kmh = p.speed * 3.6;
    this.rate += (Math.abs(a - this.angle) / Math.max(dt, 1e-3) - this.rate) * (1 - Math.exp(-6 * dt));
    this.angle = a;
    this.near = opts.near ?? Infinity;

    if (this.inSeries && (opts.grass || (abs > DRIFT.spinDeg && kmh > 5))) {
      this.lose(opts.grass ? 'Съезд с асфальта' : 'Разворот');
      return;
    }
    const can = !opts.grass && p.vLong > 0;
    const drifting = can && (this.active ? abs > DRIFT.stayDeg && kmh > DRIFT.stayKmh : abs > DRIFT.enterDeg && kmh > DRIFT.enterKmh);

    if (drifting && !this.active) {
      const dir = Math.sign(a);
      if (this.inSeries) {
        // связка: новый занос без паузы; перекладка — в другую сторону
        const flip = this.dir !== 0 && dir !== this.dir; // после одних «шашек» стороны ещё нет
        this.mult = Math.min(DRIFT.maxMult, this.mult + (flip ? 1 : 0.5));
        this.onEvent?.(flip ? 'ПЕРЕКЛАДКА' : 'СВЯЗКА');
      }
      this.active = true;
      this.dir = dir;
      this.holdT = 0;
      this.nearHit = false;
      this.drifts++;
    } else if (drifting && Math.sign(a) !== this.dir) {
      // перекладка без выхода из заноса (угол проскочил через ноль за один кадр)
      this.dir = Math.sign(a);
      this.holdT = 0;
      this.nearHit = false;
      this.drifts++;
      this.mult = Math.min(DRIFT.maxMult, this.mult + 1);
      this.onEvent?.('ПЕРЕКЛАДКА');
    } else if (!drifting && this.active) {
      this.active = false;
      this.gapT = 0;
    }

    if (this.active) {
      this.holdT += dt;
      this.time += dt;
      this.maxAngle = Math.max(this.maxAngle, abs);
      if (this.holdT >= DRIFT.holdStep) { this.holdT -= DRIFT.holdStep; this.mult = Math.min(DRIFT.maxMult, this.mult + 0.5); }
      // плавность: дёрганый угол (рывки рулём, «пила») — до −50 % очков
      this.smooth = Math.min(1, Math.max(0.5, 1.2 - this.rate / 120));
      let prox = 1;
      if (this.near < DRIFT.nearDist) {
        prox = 1 + (DRIFT.nearDist - this.near) / DRIFT.nearDist;
        if (!this.nearHit) { this.nearHit = true; this.onEvent?.('БЛИЗКО!'); }
      }
      const speedK = Math.min(2.5, Math.max(0.5, kmh / 40));
      this.pts += dt * 2 * Math.min(abs, 60) * speedK * this.smooth * prox * (opts.zone ?? 1);
    } else if (this.inSeries) {
      this.gapT += dt;
      if (this.keepT > 0) this.keepT -= dt;
      else if (this.gapT > DRIFT.linkTime || kmh < 8) this.bank();
    }
  }

  /**
   * Очки вне заноса (обгон впритирку): идут в текущую серию (или открывают новую), +0,5 к множителю,
   * серия не сохраняется ещё keep секунд — успеть связать следующим обгоном или заносом.
   */
  bonus(pts, text, keep = 4) {
    if (this.inSeries) this.mult = Math.min(DRIFT.maxMult, this.mult + 0.5);
    this.pts += pts;
    this.gapT = 0;
    this.keepT = Math.max(this.keepT, keep);
    this.bonuses++;
    this.onEvent?.(text);
  }

  /** Сохранить серию (выход из заноса или конец заезда). */
  bank() {
    if (!this.inSeries) return 0;
    const total = this.total;
    const info = { mult: this.mult, drifts: this.drifts, bonuses: this.bonuses, maxAngle: Math.round(this.maxAngle), time: this.time };
    this.reset();
    this.onBank?.(total, info);
    return total;
  }

  /** Серия сгорает (удар, разворот, газон). */
  lose(reason) {
    if (!this.inSeries) { this.active = false; return; }
    const pts = this.total;
    this.reset();
    this.onLost?.(reason, pts);
  }
}

/**
 * Расстояние от кузова машины игрока до ближайшей стены, столба, конуса или машины трафика (м).
 * Проверяются 4 угла кузова и середина заднего бампера (в заносе к стене идёт именно зад).
 */
export function nearestObstacle(player, colliders, traffic, maxD = 3) {
  const p = player.physics, h = player.half;
  const s = Math.sin(p.heading), c = Math.cos(p.heading);
  let best = maxD;
  for (let k = 0; k < 5; k++) {
    const lx = k === 4 ? 0 : (k & 1 ? h.w : -h.w);
    const lz = k === 4 ? h.rear : (k < 2 ? h.front : h.rear);
    const x = p.x + lx * c + lz * s, z = p.z - lx * s + lz * c;
    for (let i = 0; i < colliders.length; i++) {
      const list = colliders[i].query(x - maxD, z - maxD, x + maxD, z + maxD);
      for (let j = 0; j < list.length; j++) {
        const o = list[j];
        let d;
        if (o.type === 0) {
          const cx = x < o.minX ? o.minX : x > o.maxX ? o.maxX : x;
          const cz = z < o.minZ ? o.minZ : z > o.maxZ ? o.maxZ : z;
          d = Math.hypot(x - cx, z - cz);
        } else d = Math.hypot(x - o.x, z - o.z) - o.r;
        if (d < best) best = d;
      }
    }
    for (let i = 0; i < traffic.length; i++) {
      const t = traffic[i];
      const d = Math.hypot(t.x - x, t.z - z) - 1.9;
      if (d < best) best = d;
    }
  }
  return Math.max(0, best);
}
