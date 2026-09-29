/**
 * Параметры городской сетки и граф дорог для ИИ.
 * Узлы — перекрёстки, рёбра — участки дороги (по 2 полосы в каждую сторону).
 * Движение правостороннее.
 */
export const CITY = {
  N: 7,             // перекрёстков по стороне → 6×6 кварталов
  SPACING: 96,      // шаг сетки, м
  ROAD_W: 14,       // ширина проезжей части (4 × 3.5 м)
  HALF: 7,
  SIDEWALK: 3,
  LANE_W: 3.5,
  LANES: [1.75, 5.25], // смещение центра полосы от осевой (0 — левая/внутренняя, 1 — правая)
  STOP_BACK: 4.5,   // стоп-линия: за 4.5 м до конца участка (перед зеброй)
  CURB: 0.15,
};

export const coord = (i) => (i - (CITY.N - 1) / 2) * CITY.SPACING;
export const CITY_EXTENT = coord(CITY.N - 1) + CITY.HALF + CITY.SIDEWALK; // до забора

export const TURN = { STRAIGHT: 0, RIGHT: 1, LEFT: 2, UTURN: 3 };

export class RoadGraph {
  constructor() {
    const { N } = CITY;
    this.nodes = [];
    for (let j = 0; j < N; j++) {
      for (let i = 0; i < N; i++) {
        this.nodes.push({ id: j * N + i, i, j, x: coord(i), z: coord(j), nb: [] });
      }
    }
    this.edges = []; // направленные рёбра [a, b]
    for (const n of this.nodes) {
      const add = (i, j) => {
        if (i < 0 || j < 0 || i >= N || j >= N) return;
        const m = this.id(i, j);
        n.nb.push(m);
        this.edges.push([n.id, m]);
      };
      add(n.i + 1, n.j); add(n.i - 1, n.j); add(n.i, n.j + 1); add(n.i, n.j - 1);
    }
  }

  id(i, j) { return j * CITY.N + i; }

  dir(a, b) {
    const A = this.nodes[a], B = this.nodes[b];
    const dx = B.x - A.x, dz = B.z - A.z;
    const l = Math.hypot(dx, dz);
    return [dx / l, dz / l];
  }

  /** Тип манёвра на узле b при движении a→b→c. */
  turnType(a, b, c) {
    if (c === a) return TURN.UTURN;
    const [d1x, d1z] = this.dir(a, b);
    const [d2x, d2z] = this.dir(b, c);
    const cross = d1z * d2x - d1x * d2z; // y-компонента d1 × d2
    if (Math.abs(cross) < 0.01) return TURN.STRAIGHT;
    return cross < 0 ? TURN.RIGHT : TURN.LEFT;
  }

  /** Случайный следующий узел (без разворота, прямо — чаще). */
  pickNext(b, a, rnd) {
    const opts = this.nodes[b].nb.filter((n) => n !== a);
    if (opts.length === 0) return a;
    let total = 0;
    const w = opts.map((c) => {
      const t = this.turnType(a, b, c);
      const v = t === TURN.STRAIGHT ? 2 : 1;
      total += v;
      return v;
    });
    let r = rnd() * total;
    for (let k = 0; k < opts.length; k++) { r -= w[k]; if (r <= 0) return opts[k]; }
    return opts[opts.length - 1];
  }
}
