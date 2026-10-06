import { LevelScene } from './LevelScene.js';
import { examGround } from '../config/levels.js';
import { CITY, coord } from '../world/RoadGraph.js';
import { VIOLATION } from './Rules.js';
import { AUTODROME } from '../world/City.js';
import { mulberry32 } from '../utils/math.js';

const { N, HALF } = CITY;
const MAX_PTS = 5;
const INSTRUCTOR = {
  start: 'Инструктор: «Пристегнулись? Зеркала настроили? Начинаем с площадки.»',
  ground: 'Инструктор: «Площадка сдана. Теперь — в город. Выезжайте через ворота.»',
  pass: 'Инструктор: «Поздравляю, права ваши. Только не гоняйте!»',
  fail: 'Инструктор: «Экзамен не сдан. Приходите через неделю.»',
};

/**
 * Экзамен ГИБДД: этап 1 — площадка (змейка, параллельная, гараж задом),
 * этап 2 — город по маршруту инструктора с учётом ПДД. 5 штрафных баллов — не сдал.
 */
export class ExamMode {
  constructor(game) {
    this.g = game;
    this.name = 'exam';
    this.rnd = mulberry32(Date.now() & 0xffff);
  }

  enter() {
    const g = this.g;
    g.traffic.enabled = false;
    g.traffic.clear();
    this.pts = 0;
    this.log = [];
    this.stage = 'ground';
    this.ex = examGround();
    this.exIndex = 0;
    this.scene = new LevelScene(g);
    this._loadExercise(true);
    g.hud.showSensors(true);
    g.hud.showLimit(false);
    g.hud.toast(INSTRUCTOR.start, 'good', 4);
    g.rules.reset();
    g.rules.onViolation = (v) => { if (this.stage === 'city') this._penalty(v.pts, v.text); };
    g.rules.onTurn = (side, key) => this._onTurn(side, key);
  }

  exit() {
    const g = this.g;
    this.scene.unload();
    g.rules.onViolation = null;
    g.rules.onTurn = null;
    g.beacon.hide();
    g.hud.hideMission();
    g.hud.nav(null);
    g.hud.showSensors(false);
  }

  get extraColliders() { return this.stage === 'ground' ? [this.scene.col] : []; }

  _loadExercise(first) {
    const L = this.ex[this.exIndex];
    this.scene.load(L, this.g.player.def);
    if (first) { this.g.player.place(...this.scene.startPose()); this.g.cameraRig.snap(); }
    this.exTime = 0;
    this.touched = new Set();
  }

  _penalty(pts, text) {
    if (this.stage === 'done') return;
    this.pts += pts;
    this.log.push(`${text} (+${pts})`);
    this.g.audio.beep(300, 0.25, 0.15, 'sawtooth');
    this.g.hud.toast(`Инструктор: «${text}!» +${pts} ${pts === 1 ? 'балл' : pts < 5 ? 'балла' : 'баллов'}`, 'bad', 3);
    if (this.pts >= MAX_PTS) this._finish(false);
  }

  onContact(obj) {
    if (this.stage === 'done') return;
    const tag = obj.tag;
    const key = tag === 'cone' ? `c${obj.idx}` : tag === 'car' ? `car${obj.car?.id}` : tag;
    if (this.touched?.has(key)) return;
    this.touched?.add(key);
    if (tag === 'cone') { this.g.cones.knock(obj.idx, 0, 1); this._penalty(3, 'Сбит конус'); }
    else if (['obstacle', 'wall', 'parked', 'building', 'fence', 'pole', 'car', 'booth'].includes(tag)) this._penalty(5, 'Столкновение');
  }

  onCrash() {}

  _route() {
    // случайный маршрут от западного перекрёстка (0,3) по графу без разворотов
    const graph = this.g.graph;
    let a = null, b = graph.id(0, 3);
    const path = [b];
    for (let k = 0; k < 7; k++) {
      const next = graph.pickNext(b, a ?? graph.id(0, 3) - 1, this.rnd);
      if (next === undefined || path.includes(next)) break;
      path.push(next); a = b; b = next;
    }
    return path;
  }

  _startCity() {
    const g = this.g;
    this.stage = 'city';
    this.scene.unload();
    g.traffic.enabled = true;
    g.traffic.target = Math.max(6, Math.round(g.q.trafficCount * 0.6));
    g.traffic.prefill(g.camera);
    g.hud.showSensors(false);
    g.hud.showLimit(true);
    this.route = this._route();
    this.ri = 0; // индекс следующего узла маршрута
    g.hud.toast(INSTRUCTOR.ground, 'good', 4);
    this._showNext();
  }

  _node(id) { return this.g.graph.nodes[id]; }

  _showNext() {
    const g = this.g;
    if (this.ri >= this.route.length) {
      // финиш — остановка на середине последнего участка
      const a = this._node(this.route[this.route.length - 2] ?? this.route[0]);
      const b = this._node(this.route[this.route.length - 1]);
      const dx = Math.sign(b.x - a.x), dz = Math.sign(b.z - a.z);
      // точка за последним перекрёстком: продолжаем прямо 40 м по правой полосе
      const nx = dx || 0, nz = dz || 1;
      this.finish = { x: b.x + nx * 40 - nz * CITY.LANES[1], z: b.z + nz * 40 + nx * CITY.LANES[1] };
      g.beacon.show(this.finish.x, this.finish.z, 0x35e07a);
      return;
    }
    const n = this._node(this.route[this.ri]);
    g.beacon.show(n.x, n.z, 0xffcc33);
  }

  _instruction() {
    const g = this.g, p = g.player.physics;
    if (this.ri === 0) return 'Выезжайте с автодрома и езжайте к перекрёстку';
    if (this.ri >= this.route.length) return 'Остановитесь у зелёной метки';
    const prev = this._node(this.route[this.ri - 1]);
    const cur = this._node(this.route[this.ri]);
    const next = this.route[this.ri + 1] !== undefined ? this._node(this.route[this.ri + 1]) : null;
    const d = Math.round(Math.hypot(cur.x - p.x, cur.z - p.z));
    if (!next) return `Через ${d} м — прямо, затем остановка`;
    const t = g.graph.turnType(prev.id, cur.id, next.id);
    const word = t === 1 ? 'направо ↱' : t === 2 ? 'налево ↰' : 'прямо ↑';
    return `Через ${d} м — ${word}`;
  }

  _onTurn(side, key) {
    if (this.stage !== 'city' || this.ri === 0 || this.ri >= this.route.length) return;
    const cur = this._node(this.route[this.ri]);
    if (key !== `${cur.i},${cur.j}`) return;
    const prev = this._node(this.route[this.ri - 1]);
    const next = this.route[this.ri + 1];
    if (next === undefined) { this.ri++; this._showNext(); return; }
    const t = this.g.graph.turnType(prev.id, cur.id, next);
    const expect = t === 1 ? 'R' : t === 2 ? 'L' : 'S';
    if (side !== expect) {
      this._penalty(2, 'Не выполнили указание инструктора');
      // перестраиваем маршрут от текущего узла
      const p = this.g.player.physics;
      const dirX = Math.round(Math.sin(p.heading)), dirZ = Math.round(Math.cos(p.heading));
      const ni = cur.i + dirX, nj = cur.j + dirZ;
      if (ni >= 0 && nj >= 0 && ni < N && nj < N) {
        const rest = this.route.length - this.ri - 1;
        this.route = this.route.slice(0, this.ri + 1).concat([this.g.graph.id(ni, nj)]);
        let a = cur.id, b = this.g.graph.id(ni, nj);
        for (let k = 1; k < rest; k++) { const c = this.g.graph.pickNext(b, a, this.rnd); this.route.push(c); a = b; b = c; }
      }
    }
    this.ri++;
    this._showNext();
  }

  _finish(ok) {
    const g = this.g;
    this.stage = 'done';
    g.input.enabled = false;
    g.beacon.hide();
    if (ok) {
      const first = !g.save.data.license;
      g.save.data.license = true;
      g.save.commit();
      g.save.addMoney(first ? 10000 : 2000);
      g.audio.success();
      g.hud.toast(INSTRUCTOR.pass, 'good', 4);
      setTimeout(() => g.menus.showResult({ ok: true, exam: true, reward: first ? 10000 : 2000, log: this.log, pts: this.pts }), 1200);
    } else {
      g.audio.fail();
      g.hud.toast(INSTRUCTOR.fail, 'bad', 4);
      setTimeout(() => g.menus.showResult({ ok: false, exam: true, why: 'Набрано 5 штрафных баллов', log: this.log }), 1200);
    }
  }

  update(dt) {
    const g = this.g, p = g.player.physics;
    g.beacon.update(dt);
    if (this.stage === 'ground') {
      this.exTime += dt;
      const L = this.ex[this.exIndex];
      const { state, done } = this.scene.update(dt, g.player);
      g.hud.mission(`Экзамен · площадка ${this.exIndex + 1}/3 · ${L.type}`,
        `${state === 'wrong' ? 'Не той стороной!' : L.desc} · баллы: ${this.pts}/${MAX_PTS}`, this.scene.hold / 1.2);
      g.hud.nav(this.scene.target);
      if (this.exTime > 150) { this._penalty(5, 'Время на упражнение вышло'); return; }
      if (done) {
        g.audio.beep(1300, 0.1, 0.1); g.audio.beep(1700, 0.12, 0.1);
        this.exIndex++;
        if (this.exIndex >= this.ex.length) this._startCity();
        else { g.hud.toast('Упражнение выполнено! Следующее — по стрелке', 'good'); this._loadExercise(false); }
      }
      return;
    }
    if (this.stage !== 'city') return;
    // этап «город»
    if (this.ri === 0) {
      const n0 = this._node(this.route[0]);
      if (Math.hypot(n0.x - p.x, n0.z - p.z) < HALF + 2) { this.ri = 1; this._showNext(); }
    }
    if (this.ri >= this.route.length && this.finish) {
      const d = Math.hypot(this.finish.x - p.x, this.finish.z - p.z);
      if (d < 6 && p.speed < 0.5) { this._finish(true); return; }
      g.hud.nav(this.finish);
    } else if (this.route[this.ri] !== undefined) {
      g.hud.nav(this._node(this.route[this.ri]));
    }
    g.hud.mission(`Экзамен · город`, `${this._instruction()} · баллы: ${this.pts}/${MAX_PTS}`);
    void AUTODROME; void coord;
  }

  renderExtra(inst, glow, blink) { if (this.stage === 'ground') this.scene.render(inst, glow, blink); }
}
