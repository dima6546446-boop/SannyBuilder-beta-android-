import { CollisionWorld } from '../core/CollisionWorld.js';
import { AUTODROME } from '../world/City.js';
import { DriftController } from './DriftController.js';

const RUN = 90;            // секунд на заезд
const ISLAND_R = 8;        // радиус «островов» из конусов

/**
 * Дрифт-зона на автодроме ДОСААФ: заезд 90 секунд по «восьмёрке» вокруг двух островов
 * из конусов, вдоль южной стороны — линия конусов для «клиппинга» (бонус за близость).
 * Сбитый конус или удар сжигает текущую серию. Итог заезда = сумма сохранённых серий;
 * рекорд хранится в Save (drift.best), награда — рубли (итог / 4, за рекорд +1 000 ₽).
 */
export class DriftMode {
  constructor(game) {
    this.g = game;
    this.name = 'drift';
    this.allowWalk = false;
  }

  enter() {
    const g = this.g, A = AUTODROME;
    g.traffic.enabled = false;
    g.traffic.clear();
    // разметка: два острова и линия клиппинга
    const pts = [];
    for (const ix of [-1, 1]) {
      const cx = A.cx + ix * 38, cz = A.cz - 18;
      for (let k = 0; k < 14; k++) {
        const a = (k / 14) * Math.PI * 2;
        pts.push([cx + Math.cos(a) * ISLAND_R, cz + Math.sin(a) * ISLAND_R]);
      }
    }
    for (let x = A.cx - 60; x <= A.cx + 60; x += 5) pts.push([x, A.z0 + 14]);
    g.cones.set(pts);
    this.cones = pts.map(([x, z]) => ({ x, z, hit: false }));
    this.col = new CollisionWorld(8);
    for (const c of this.cones) c.o = this.col.addCircle(c.x, c.z, 0.24, 'cone');
    this.cols = [g.collision, this.col];

    g.player.place(A.cx + 10, A.cz + 22, -Math.PI / 2);
    g.player.physics.fuel = g.player.physics.spec.tank;
    g.cameraRig.snap();
    this.drift = new DriftController(g, { onBank: (total, info) => this._bank(total, info) });
    this.total = 0;
    this.series = 0;
    this.time = RUN;
    this.started = false;
    this.state = 'play';
    this.best = g.save.data.drift.best || 0;
    g.hud.showLimit(false);
    g.hud.showSensors(false);
    g.hud.toast('Дрифт-зона ДОСААФ: 90 секунд. Ручник или перегазовка — срыв, газ и контрруль — удержание', 'good', 4);
    this._mission();
  }

  exit() {
    this.g.cones.clear();
    this.drift.hide();
    this.g.hud.hideMission();
  }

  get extraColliders() { return []; }

  _bank(total) {
    this.total += total;
    this.series++;
    this.g.daily.progress('drift', total);
    const st = this.g.save.data;
    if (total > st.drift.bestSeries) st.drift.bestSeries = total;
    if (total > st.stats.bestCombo) st.stats.bestCombo = total;
    return 0; // рубли — по итогам заезда
  }

  _mission() {
    const t = Math.max(0, Math.ceil(this.time));
    const clock = `${Math.floor(t / 60)}:${String(t % 60).padStart(2, '0')}`;
    const pts = this.total + this.drift.sc.total; // вместе с текущей серией
    const sub = `${this.started ? clock : 'Старт — по газу'} · очки ${pts.toLocaleString('ru-RU')} · рекорд ${this.best.toLocaleString('ru-RU')}`;
    this.g.hud.mission('Дрифт-зона ДОСААФ', sub, this.time / RUN);
  }

  /** Сбитые конусы: машина проходит сквозь (это не столб), конус падает, серия сгорает. */
  _cones() {
    const pl = this.g.player, p = pl.physics;
    const s = Math.sin(p.heading), c = Math.cos(p.heading);
    for (let i = 0; i < this.cones.length; i++) {
      const k = this.cones[i];
      if (k.hit) continue;
      const dx = k.x - p.x, dz = k.z - p.z;
      if (dx * dx + dz * dz > 16) continue;
      for (const [off, r] of pl.circles) {
        const ex = k.x - (p.x + s * off), ez = k.z - (p.z + c * off);
        if (ex * ex + ez * ez < (r + 0.24) ** 2) {
          k.hit = true;
          k.o.r = -100; // убрать из бонуса близости
          const d = Math.hypot(ex, ez) || 1;
          this.g.cones.knock(i, ex / d, ez / d);
          this.g.audio.beep(500, 0.06, 0.08);
          this.drift.lose('Конус');
          break;
        }
      }
    }
  }

  onCrash(strength) { if (strength > 0.08) this.drift.lose('Удар'); }

  update(dt) {
    if (this.state !== 'play') return;
    const g = this.g, p = g.player.physics;
    if (!this.started && p.speed > 3) this.started = true;
    if (this.started) this.time -= dt;
    this._cones();
    this.drift.update(dt, { colliders: this.cols });
    this._mission();
    if (this.time <= 0) this._finish();
  }

  _finish() {
    const g = this.g, d = g.save.data.drift;
    this.drift.bank();
    this.state = 'done';
    g.input.enabled = false;
    const score = this.total;
    const record = score > d.best;
    if (record) d.best = score;
    d.runs++;
    const reward = Math.round(score / 4) + (record && score > 0 ? 1000 : 0);
    if (reward > 0) g.save.addMoney(reward);
    g.save.commit();
    g.audio.success();
    setTimeout(() => g.menus.showResult({ drift: true, ok: true, score, best: d.best, record, reward, series: this.series }), 900);
  }

  renderExtra() {}
}
