import * as THREE from 'three';
import { CITY } from './RoadGraph.js';
import { prep, merge } from '../utils/geometry.js';

export const SIGNAL = { GREEN: 0, YELLOW: 1, RED: 2 };
export const AXIS = { NS: 0, EW: 1 }; // NS — движение вдоль Z, EW — вдоль X

const GREEN_T = 12, YELLOW_T = 3, ALLRED_T = 2;
const CYCLE = 2 * (GREEN_T + YELLOW_T + ALLRED_T);

// Цвета ламп (линейное пространство). Выключенная лампа — тусклая, а не чёрная.
const ON = [[1.0, 0.08, 0.04], [1.0, 0.62, 0.04], [0.1, 1.0, 0.35]];
const OFF = [[0.14, 0.02, 0.02], [0.14, 0.09, 0.02], [0.02, 0.12, 0.05]];

/**
 * Светофоры на всех перекрёстках: циклы со сдвигом фаз, мигающий зелёный
 * и «красный+жёлтый» перед зелёным (как в ПДД РФ).
 * Лампы — один InstancedMesh (1 draw call на весь город), цвета через instanceColor
 * обновляются только при смене состояния.
 */
export class TrafficLights {
  constructor(scene, graph, collision, rnd) {
    this.graph = graph;
    this.time = 0;
    this.offsets = graph.nodes.map(() => rnd() * CYCLE);
    this.keys = new Int16Array(graph.nodes.length).fill(-1);
    this.heads = graph.nodes.map(() => []);

    const { HALF, SPACING } = CITY;
    const approaches = [[0, 1], [0, -1], [1, 0], [-1, 0]]; // направление движения машины
    const housing = [];
    let lampCount = 0;
    const lampPos = [];

    for (const n of graph.nodes) {
      for (const [dx, dz] of approaches) {
        // есть ли дорога, по которой машина приезжает с этой стороны
        const fromX = n.x - dx * SPACING, fromZ = n.z - dz * SPACING;
        if (!n.nb.some((m) => Math.abs(graph.nodes[m].x - fromX) < 1 && Math.abs(graph.nodes[m].z - fromZ) < 1)) continue;
        const rx = -dz, rz = dx;
        // ближний правый угол перекрёстка
        const px = n.x - dx * (HALF + 1.2) + rx * (HALF + 1.2);
        const pz = n.z - dz * (HALF + 1.2) + rz * (HALF + 1.2);
        housing.push(prep(new THREE.CylinderGeometry(0.08, 0.1, 3.2, 6, 1, true).translate(px, 1.6 + CITY.CURB, pz)));
        housing.push(prep(new THREE.BoxGeometry(0.34, 1.15, 0.34).translate(px, 3.75 + CITY.CURB, pz)));
        collision.addCircle(px, pz, 0.2, 'pole');
        const axis = Math.abs(dz) > 0.5 ? AXIS.NS : AXIS.EW;
        this.heads[n.id].push({ axis, base: lampCount });
        for (let k = 0; k < 3; k++) {
          // красный сверху, лампы смотрят навстречу машине (−d)
          lampPos.push([px - dx * 0.18, 4.12 - k * 0.37 + CITY.CURB, pz - dz * 0.18]);
          lampCount++;
        }
      }
    }

    const mat = new THREE.MeshLambertMaterial({ color: 0x2b2d2f });
    this.housing = new THREE.Mesh(merge(housing), mat);
    this.housing.name = 'TrafficLightPoles';
    this.housing.matrixAutoUpdate = false;
    scene.add(this.housing);

    const lampGeo = new THREE.SphereGeometry(0.13, 8, 6);
    this.lamps = new THREE.InstancedMesh(lampGeo, new THREE.MeshBasicMaterial({ toneMapped: false }), lampCount);
    this.lamps.name = 'TrafficLamps';
    const m = new THREE.Matrix4();
    const c = new THREE.Color();
    lampPos.forEach((p, i) => {
      m.makeTranslation(p[0], p[1], p[2]);
      this.lamps.setMatrixAt(i, m);
      this.lamps.setColorAt(i, c.setRGB(...OFF[i % 3]));
    });
    this.lamps.instanceMatrix.needsUpdate = true;
    this.lamps.computeBoundingSphere();
    scene.add(this.lamps);
    this._c = c;
    this.update(0);
  }

  /** Фаза узла: 0 NS зел, 1 NS жёлт, 2 кр (EW кр+жёлт), 3 EW зел, 4 EW жёлт, 5 кр (NS кр+жёлт). */
  _phase(id) {
    const t = (this.time + this.offsets[id]) % CYCLE;
    const a = GREEN_T, b = a + YELLOW_T, c = b + ALLRED_T, d = c + GREEN_T, e = d + YELLOW_T;
    if (t < a) return { p: 0, t };
    if (t < b) return { p: 1, t: t - a };
    if (t < c) return { p: 2, t: t - b };
    if (t < d) return { p: 3, t: t - c };
    if (t < e) return { p: 4, t: t - d };
    return { p: 5, t: t - e };
  }

  getSignal(nodeId, axis) {
    const { p } = this._phase(nodeId);
    const g = axis === AXIS.NS ? 0 : 3;
    if (p === g) return SIGNAL.GREEN;
    if (p === g + 1) return SIGNAL.YELLOW;
    return SIGNAL.RED;
  }

  update(dt) {
    this.time += dt;
    let dirty = false;
    for (let id = 0; id < this.heads.length; id++) {
      const { p, t } = this._phase(id);
      // мигающий зелёный последние 3 с
      const blink = (p === 0 || p === 3) && t > GREEN_T - 3 ? (Math.floor(t * 2) & 1) : 0;
      const key = p * 2 + blink;
      if (key === this.keys[id]) continue;
      this.keys[id] = key;
      dirty = true;
      for (const h of this.heads[id]) {
        const g = h.axis === AXIS.NS ? 0 : 3;
        const green = p === g && !blink;
        const yellow = p === g + 1;
        const preGreen = (h.axis === AXIS.NS && p === 5) || (h.axis === AXIS.EW && p === 2);
        const red = !(p === g) && !(p === g + 1);
        const state = [red, yellow || preGreen, green];
        for (let k = 0; k < 3; k++) this.lamps.setColorAt(h.base + k, this._c.setRGB(...(state[k] ? ON[k] : OFF[k])));
      }
    }
    if (dirty) this.lamps.instanceColor.needsUpdate = true;
  }
}
