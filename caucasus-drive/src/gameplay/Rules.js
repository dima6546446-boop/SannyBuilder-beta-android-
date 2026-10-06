import { CITY, coord, CITY_EXTENT } from '../world/RoadGraph.js';
import { SIGNAL, AXIS } from '../world/TrafficLights.js';
import { wrapAngle } from '../utils/math.js';

const { N, SPACING, HALF, STOP_BACK } = CITY;
const STOP_DIST = HALF + STOP_BACK; // стоп-линия от центра перекрёстка

export const VIOLATION = {
  RED: { id: 'red', text: 'Проезд на красный', pts: 5, fine: 1000 },
  SPEED: { id: 'speed', text: 'Превышение скорости', pts: 3, fine: 500 },
  SOLID: { id: 'solid', text: 'Пересечение двойной сплошной', pts: 5, fine: 5000 },
  ONCOMING: { id: 'oncoming', text: 'Выезд на встречную полосу', pts: 5, fine: 5000 },
  NO_SIGNAL: { id: 'nosignal', text: 'Поворот без поворотника', pts: 2, fine: 500 },
  SIDEWALK: { id: 'sidewalk', text: 'Езда по тротуару', pts: 5, fine: 2000 },
  CRASH: { id: 'crash', text: 'ДТП', pts: 5, fine: 0 },
};

const snap = (h) => ((Math.round(wrapAngle(h) / (Math.PI / 2)) % 4) + 4) % 4; // 0:+z 1:+x 2:−z 3:−x

/**
 * Детектор нарушений ПДД для игрока. Работает на геометрии сетки (без физики):
 * стоп-линии, осевая, перекрёстки. Используется экзаменом (штрафные баллы)
 * и свободной ездой (штрафы с камер и постов ДПС).
 */
export class Rules {
  constructor(lights) {
    this.lights = lights;
    this.limit = 60;
    this.onViolation = null;
    this.reset();
  }

  reset() {
    this.prev = null;
    this.overTime = 0;
    this.lastSpeedV = -99;
    this.wrongSideTime = 0;
    this.oncomingFlag = false;
    this.sidewalkTime = 0;
    this.sidewalkFlag = false;
    this.box = null;
    this.t = 0;
    this.lastRedNode = -1;
  }

  _emit(v, extra = {}) { this.onViolation?.(v, extra); }

  /** Положение на сетке: на каком участке, в каком направлении, где перекрёстки. */
  locate(x, z) {
    if (Math.abs(x) > CITY_EXTENT || Math.abs(z) > CITY_EXTENT) return null;
    const ix = Math.round((x - coord(0)) / SPACING), iz = Math.round((z - coord(0)) / SPACING);
    const cxi = Math.min(N - 1, Math.max(0, ix)), czi = Math.min(N - 1, Math.max(0, iz));
    const dx = x - coord(cxi), dz = z - coord(czi);
    const onV = Math.abs(dx) <= HALF, onH = Math.abs(dz) <= HALF;
    return { ix: cxi, iz: czi, dx, dz, onV, onH, inBox: onV && onH };
  }

  update(dt, player, surfaceType) {
    this.t += dt;
    const p = player.physics;
    const loc = this.locate(p.x, p.z);
    const speedKmh = p.speed * 3.6;

    // --- скорость
    if (speedKmh > this.limit + 10) {
      this.overTime += dt;
      if (this.overTime > 1.5 && this.t - this.lastSpeedV > 12) {
        this.lastSpeedV = this.t;
        this._emit(VIOLATION.SPEED, { speed: Math.round(speedKmh) });
      }
    } else this.overTime = 0;

    // --- тротуар/газон в городе
    if (loc && (surfaceType === 1 || surfaceType === 2) && p.speed > 2) {
      this.sidewalkTime += dt;
      if (this.sidewalkTime > 1.2 && !this.sidewalkFlag) { this.sidewalkFlag = true; this._emit(VIOLATION.SIDEWALK); }
    } else if (surfaceType === 0) { this.sidewalkTime = 0; this.sidewalkFlag = false; }

    if (!loc || p.speed < 0.5) { this.prev = loc ? { ...loc, x: p.x, z: p.z } : null; return; }
    const dir = snap(Math.atan2(p.vx, p.vz)); // направление движения
    const alongZ = dir === 0 || dir === 2;
    const sgn = dir === 0 || dir === 1 ? 1 : -1;

    // --- стоп-линия на красный
    if (!loc.inBox && ((alongZ && loc.onV) || (!alongZ && loc.onH))) {
      // ближайший перекрёсток впереди
      const cur = alongZ ? p.z : p.x;
      const idx = Math.round((cur - coord(0)) / SPACING);
      let nodeC = coord(Math.min(N - 1, Math.max(0, idx)));
      if ((nodeC - cur) * sgn < 0) nodeC += sgn * SPACING;
      const distNow = (nodeC - cur) * sgn;
      const prevCur = this.prev ? (alongZ ? this.prev.z : this.prev.x) : cur;
      const distPrev = (nodeC - prevCur) * sgn;
      const lat = alongZ ? loc.dx : loc.dz;
      const rightSide = alongZ ? -sgn : sgn; // правая сторона по ходу
      const onOwnSide = lat * rightSide > 0;
      if (distPrev > STOP_DIST && distNow <= STOP_DIST && onOwnSide) {
        const ni = alongZ ? loc.ix : Math.round((nodeC - coord(0)) / SPACING);
        const nj = alongZ ? Math.round((nodeC - coord(0)) / SPACING) : loc.iz;
        if (ni >= 0 && ni < N && nj >= 0 && nj < N) {
          const node = nj * N + ni;
          const sig = this.lights.getSignal(node, alongZ ? AXIS.NS : AXIS.EW);
          if (sig === SIGNAL.RED && node !== this.lastRedNode) {
            this.lastRedNode = node;
            this._emit(VIOLATION.RED, { node });
          }
        }
      }
      if (distNow > STOP_DIST + 5) this.lastRedNode = -1;

      // --- осевая: двойная сплошная и встречка (вне перекрёстков)
      const offNode = distNow > HALF + 5 && (SPACING - distNow) > HALF + 5;
      if (offNode) {
        const prevLat = this.prev ? (alongZ ? this.prev.dx : this.prev.dz) : lat;
        const prevOwn = prevLat * rightSide > 0;
        if (prevOwn && !onOwnSide && Math.abs(lat) > 0.3) this._emit(VIOLATION.SOLID);
        if (!onOwnSide && Math.abs(lat) > 1.0) {
          this.wrongSideTime += dt;
          if (this.wrongSideTime > 1.5 && !this.oncomingFlag) { this.oncomingFlag = true; this._emit(VIOLATION.ONCOMING); }
        } else { this.wrongSideTime = 0; this.oncomingFlag = false; }
      }
    }

    // --- повороты на перекрёстке без поворотника
    if (loc.inBox && !this.box) this.box = { dir, t: this.t, key: `${loc.ix},${loc.iz}` };
    if (!loc.inBox && this.box) {
      const turn = (dir - this.box.dir + 4) % 4; // 1 — направо? (+z → +x при взгляде сверху)
      // при курсе вперёд +Z правая сторона −X: поворот направо = переход +z→−x (dir 0→3)
      let side = null;
      if (turn === 3) side = 'R';
      else if (turn === 1) side = 'L';
      if (side) {
        const since = this.t - player.lastIndicatorTime[side];
        const hazard = player.indicator === 'H';
        if (since > 8 && !hazard) this._emit(VIOLATION.NO_SIGNAL, { side });
        this.onTurn?.(side, this.box.key);
      } else if (turn === 0) this.onTurn?.('S', this.box.key);
      this.box = null;
    }
    this.prev = { ...loc, x: p.x, z: p.z };
  }
}
