import { CITY, TURN } from '../world/RoadGraph.js';
import { SIGNAL, AXIS } from '../world/TrafficLights.js';
import { clamp } from '../utils/math.js';

const { HALF, LANES, STOP_BACK } = CITY;
const MODE_EDGE = 0, MODE_TURN = 1;
const CAR_LEN = 4.2;
const HALF_LEN = 2.1;
// Intelligent Driver Model (Treiber): плавное следование за лидером без «дёрганий»
const IDM = { a: 1.8, b: 3.0, s0: 2.2, T: 1.3 };
const TURN_SPEED = [0, 5.5, 7.5, 4]; // по типу манёвра (прямо — круиз)

/**
 * Машина трафика. Логика:
 *  - движение по полосе участка, затем по кривой Безье через перекрёсток
 *  - перестроение заранее в полосу для поворота (направо — правая, налево — левая)
 *  - IDM: дистанция до лидера (машина / игрок / стоп-линия на красный)
 *  - светофоры: стоп на красный, на жёлтый — если успевает остановиться
 *  - объезд препятствия: стоит за неподвижным препятствием >2 с → перестроение
 *  - гудок, если игрок перегородил дорогу
 */
export class TrafficCar {
  constructor(id) {
    this.id = id;
    this.active = false;
    this.x = 0; this.z = 0; this.heading = 0;
    this.vx = 0; this.vz = 0;
    this.speed = 0;
    this.colorIndex = 0;
    this.braking = false;
    this.waitingLight = false;
  }

  spawn(ctx, a, b, lane, s, cruise, colorIndex) {
    this.ctx = ctx;
    this.active = true;
    this.cruise = cruise;
    this.colorIndex = colorIndex;
    this.speed = cruise * 0.7;
    this.blocked = 0;
    this.stun = 0;
    this.ghost = 0;
    this.honkCooldown = 2 + ctx.rnd() * 3;
    this.laneChange = false;
    this._enterEdge(a, b, lane);
    this.s = s;
    this._pose();
  }

  bump(impact) {
    this.stun = 1.5 + Math.min(impact, 10) * 0.2;
    this.speed = 0;
  }

  _enterEdge(a, b, lane) {
    const g = this.ctx.graph;
    this.mode = MODE_EDGE;
    this.a = a; this.b = b; this.lane = lane;
    const A = g.nodes[a], B = g.nodes[b];
    const [dx, dz] = g.dir(a, b);
    this.dx = dx; this.dz = dz;
    this.rx = -dz; this.rz = dx;
    this.edgeLen = Math.hypot(B.x - A.x, B.z - A.z) - 2 * HALF;
    this.sx = A.x + dx * HALF;
    this.sz = A.z + dz * HALF;
    this.axis = Math.abs(dz) > 0.5 ? AXIS.NS : AXIS.EW;
    this.lat = LANES[lane];
    this.latTarget = this.lat;
    this.laneChange = false;
    this.c = g.pickNext(b, a, this.ctx.rnd);
    this.turn = g.turnType(a, b, this.c);
    this.wantLane = this.turn === TURN.RIGHT ? 1 : this.turn === TURN.LEFT || this.turn === TURN.UTURN ? 0 : lane;
    this.s = 0;
  }

  _enterTurn() {
    const g = this.ctx.graph;
    const B = g.nodes[this.b];
    const [d2x, d2z] = g.dir(this.b, this.c);
    const r2x = -d2z, r2z = d2x;
    const nextLane = this.turn === TURN.RIGHT ? 1 : this.turn === TURN.LEFT || this.turn === TURN.UTURN ? 0 : this.lane;
    const p0x = this.sx + this.dx * this.edgeLen + this.rx * this.lat;
    const p0z = this.sz + this.dz * this.edgeLen + this.rz * this.lat;
    const p2x = B.x + d2x * HALF + r2x * LANES[nextLane];
    const p2z = B.z + d2z * HALF + r2z * LANES[nextLane];
    let p1x, p1z;
    if (this.turn === TURN.STRAIGHT || this.turn === TURN.UTURN) {
      p1x = (p0x + p2x) / 2; p1z = (p0z + p2z) / 2;
      if (this.turn === TURN.UTURN) { p1x = B.x; p1z = B.z; }
    } else {
      // угол поворота: пересечение продолжений полос (для сетки они перпендикулярны)
      const t = (p2x - p0x) * this.dx + (p2z - p0z) * this.dz;
      p1x = p0x + this.dx * t; p1z = p0z + this.dz * t;
    }
    this.bz = [p0x, p0z, p1x, p1z, p2x, p2z];
    // длина кривой по ломаной из 8 отрезков
    let len = 0, px = p0x, pz = p0z;
    for (let i = 1; i <= 8; i++) {
      const t = i / 8, u = 1 - t;
      const x = u * u * p0x + 2 * u * t * p1x + t * t * p2x;
      const z = u * u * p0z + 2 * u * t * p1z + t * t * p2z;
      len += Math.hypot(x - px, z - pz); px = x; pz = z;
    }
    this.turnLen = len;
    this.nextLane = nextLane;
    this.mode = MODE_TURN;
  }

  /** Проверка свободна ли соседняя полоса (в локальной СК машины). */
  _laneFree(deltaLat) {
    const fx = Math.sin(this.heading), fz = Math.cos(this.heading);
    const rx = -fz, rz = fx;
    const check = (ox, oz) => {
      const dx = ox - this.x, dz = oz - this.z;
      const along = dx * fx + dz * fz;
      const side = dx * rx + dz * rz;
      return !(along > -9 && along < 14 && Math.abs(side - deltaLat) < 2.2);
    };
    for (const o of this.ctx.cars) if (o !== this && !check(o.x, o.z)) return false;
    const p = this.ctx.player;
    return check(p.x, p.z);
  }

  /** Есть ли встречная машина, которой надо уступить при левом повороте. */
  _oncoming(fx, fz, rx, rz) {
    const test = (ox, oz, oh, ov, oturn, oid) => {
      if (ov < 1.5) return false;
      // встречный тоже поворачивает налево: пропускает тот, у кого id больше
      if (oturn === TURN.LEFT && oid > this.id) return false;
      const dx = ox - this.x, dz = oz - this.z;
      const along = dx * fx + dz * fz;
      const side = dx * rx + dz * rz;
      if (along < 0 || along > 50 || side > -0.5 || side < -9) return false;
      return Math.sin(oh) * fx + Math.cos(oh) * fz < -0.7;
    };
    for (const o of this.ctx.cars) {
      if (o !== this && test(o.x, o.z, o.heading, o.speed, o.turn, o.id)) return true;
    }
    const p = this.ctx.player;
    return test(p.x, p.z, p.heading, p.speed, -1, -1);
  }

  _startLaneChange(lane) {
    this.lane = lane;
    this.latTarget = LANES[lane];
    this.laneChange = true;
  }

  update(dt) {
    const ctx = this.ctx;
    if (this.stun > 0) { this.stun -= dt; this.speed = 0; this._pose(); return; }
    if (this.ghost > 0) this.ghost -= dt;

    const fx = Math.sin(this.heading), fz = Math.cos(this.heading);
    const rx = -fz, rz = fx;

    // 1. желаемая скорость
    let v0 = this.cruise;
    if (this.mode === MODE_TURN) {
      if (this.turn !== TURN.STRAIGHT) v0 = TURN_SPEED[this.turn];
    } else if (this.turn !== TURN.STRAIGHT) {
      const toEnd = this.edgeLen - this.s;
      if (toEnd < 30) v0 = Math.min(v0, TURN_SPEED[this.turn] + (this.cruise - TURN_SPEED[this.turn]) * (toEnd / 30));
    }

    // 2. лидер впереди (машины трафика и игрок)
    let gap = 1e9, leadV = 0, leader = null, leaderIsPlayer = false;
    if (this.ghost <= 0) {
      const scan = (ox, oz, ohx, ohz, ov) => {
        const dx = ox - this.x, dz = oz - this.z;
        if (dx > 45 || dx < -45 || dz > 45 || dz < -45) return false;
        const along = dx * fx + dz * fz;
        if (along <= 0.5 || along > 40) return false;
        const side = dx * rx + dz * rz;
        const width = this.mode === MODE_TURN ? 2.4 : 1.9;
        if (Math.abs(side) > width) return false;
        // встречные на своей полосе — игнорируем, если далеко
        if (ohx * fx + ohz * fz < -0.5 && along > 10) return false;
        const g = along - CAR_LEN;
        if (g < gap) { gap = g; leadV = Math.max(0, ov * (ohx * fx + ohz * fz)); return true; }
        return false;
      };
      for (const o of ctx.cars) {
        if (o === this) continue;
        // конфликт траекторий внутри перекрёстка: уступает машина с большим id (без дедлоков)
        if (this.mode === MODE_TURN && o.mode === MODE_TURN && o.id < this.id && o.b === this.b) {
          const dx = o.x - this.x, dz = o.z - this.z;
          if (dx * dx + dz * dz < 36 && dx * fx + dz * fz > 0) { gap = 0.01; leadV = 0; leader = o; continue; }
        }
        if (scan(o.x, o.z, Math.sin(o.heading), Math.cos(o.heading), o.speed)) { leader = o; leaderIsPlayer = false; }
      }
      const p = ctx.player;
      if (scan(p.x, p.z, Math.sin(p.heading), Math.cos(p.heading), p.speed)) { leader = null; leaderIsPlayer = true; }
    }

    // 3. светофор: стоп-линия как неподвижный «лидер»
    this.waitingLight = false;
    if (this.mode === MODE_EDGE) {
      const sig = ctx.lights.getSignal(this.b, this.axis);
      if (sig !== SIGNAL.GREEN) {
        const dist = this.edgeLen - STOP_BACK - this.s - HALF_LEN;
        const canStop = dist > (this.speed * this.speed) / (2 * 3.5);
        if (dist > -1 && (sig === SIGNAL.RED || canStop)) {
          if (dist < gap) { gap = Math.max(dist, 0.01); leadV = 0; leader = null; leaderIsPlayer = false; }
          this.waitingLight = dist < 25;
        }
      }
    }

    // 3б. левый поворот: уступаем встречным, ждём у стоп-линии
    if (this.mode === MODE_EDGE && this.turn === TURN.LEFT) {
      const dist = this.edgeLen - STOP_BACK - this.s - HALF_LEN;
      if (dist > -1 && dist < 30 && this._oncoming(fx, fz, rx, rz)) {
        if (dist < gap) { gap = Math.max(dist, 0.01); leadV = 0; leader = null; leaderIsPlayer = false; }
        this.waitingLight = true;
      }
    }

    // 4. IDM
    const v = this.speed;
    const dv = v - leadV;
    const sStar = IDM.s0 + Math.max(0, v * IDM.T + (v * dv) / (2 * Math.sqrt(IDM.a * IDM.b)));
    let acc = IDM.a * (1 - Math.pow(v / Math.max(v0, 0.1), 4));
    if (gap < 1e8) acc -= IDM.a * Math.pow(sStar / Math.max(gap, 0.1), 2);
    acc = clamp(acc, -9, IDM.a);
    this.speed = Math.max(0, v + acc * dt);
    this.braking = acc < -0.8 || (this.speed < 0.3 && gap < 5);

    // 5. объезд препятствий и перестроения
    const leaderStopped = (leader && leader.speed < 0.5 && !leader.waitingLight) || (leaderIsPlayer && ctx.player.speed < 0.5);
    if ((leader || leaderIsPlayer) && this.speed < 0.5 && gap < 10 && leaderStopped) this.blocked += dt;
    else this.blocked = Math.max(0, this.blocked - dt);

    if (this.mode === MODE_EDGE && !this.laneChange) {
      const toEnd = this.edgeLen - this.s;
      if (this.blocked > 2 && toEnd > 18) {
        const other = 1 - this.lane;
        if (this._laneFree(LANES[other] - this.lat)) { this._startLaneChange(other); this.blocked = 0; }
      } else if (this.wantLane !== this.lane && toEnd < 70 && toEnd > 22) {
        if (this._laneFree(LANES[this.wantLane] - this.lat)) this._startLaneChange(this.wantLane);
      }
    }
    // пробка на перекрёстке (взаимная блокировка) — «просачиваемся»
    if (this.blocked > 8 && this.mode === MODE_TURN) { this.ghost = 2; this.blocked = 0; }

    // гудок игроку
    this.honkCooldown -= dt;
    if (leaderIsPlayer && this.blocked > 2.5 && this.honkCooldown <= 0) {
      this.honkCooldown = 4 + ctx.rnd() * 4;
      ctx.onHonk?.(this);
    }

    // 6. продвижение по пути
    this.s += this.speed * dt;
    if (this.mode === MODE_EDGE) {
      if (this.lat !== this.latTarget) {
        const step = 1.8 * dt * Math.min(1, this.speed / 3 + 0.3);
        const d = this.latTarget - this.lat;
        this.lat = Math.abs(d) <= step ? this.latTarget : this.lat + Math.sign(d) * step;
        if (this.lat === this.latTarget) this.laneChange = false;
      }
      if (this.s >= this.edgeLen) { this.s -= this.edgeLen; this._enterTurn(); }
    }
    if (this.mode === MODE_TURN && this.s >= this.turnLen) {
      const rest = this.s - this.turnLen;
      this._enterEdge(this.b, this.c, this.nextLane);
      this.s = rest;
    }
    this._pose();
  }

  _pose() {
    const ox = this.x, oz = this.z;
    if (this.mode === MODE_EDGE) {
      this.x = this.sx + this.dx * this.s + this.rx * this.lat;
      this.z = this.sz + this.dz * this.s + this.rz * this.lat;
      const latVel = this.laneChange ? Math.sign(this.latTarget - this.lat) : 0;
      this.heading = Math.atan2(this.dx, this.dz) - latVel * 0.12;
    } else {
      const [p0x, p0z, p1x, p1z, p2x, p2z] = this.bz;
      const t = Math.min(this.s / this.turnLen, 1), u = 1 - t;
      this.x = u * u * p0x + 2 * u * t * p1x + t * t * p2x;
      this.z = u * u * p0z + 2 * u * t * p1z + t * t * p2z;
      const tx = 2 * u * (p1x - p0x) + 2 * t * (p2x - p1x);
      const tz = 2 * u * (p1z - p0z) + 2 * t * (p2z - p1z);
      this.heading = Math.atan2(tx, tz);
    }
    const fx = Math.sin(this.heading), fz = Math.cos(this.heading);
    this.vx = fx * this.speed;
    this.vz = fz * this.speed;
    this._moved = Math.hypot(this.x - ox, this.z - oz);
  }
}
