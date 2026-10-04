import { DriftScore, nearestObstacle } from './Drift.js';

/**
 * Дрифт в игре: судейство (DriftScore) + панель HUD + звуки событий.
 * Режим передаёт onBank(total, info) → сколько рублей начислено (для строки итога).
 */
export class DriftController {
  constructor(game, { onBank } = {}) {
    this.g = game;
    this.sc = new DriftScore();
    this.view = { angle: 0, pts: 0, mult: 1, active: false, tag: '', tagKind: '' };
    this.tagT = 0;
    this.near = Infinity;
    this._nearT = 0;
    this._cols = [game.collision];
    this.sc.onEvent = (text) => {
      this._tag(text, '', 1.2);
      this.g.audio.beep(text === 'БЛИЗКО!' ? 1500 : 1100 + this.sc.mult * 120, 0.07, 0.1, 'triangle');
    };
    this.sc.onBank = (total, info) => {
      const money = onBank ? onBank(total, info) : 0;
      this.view.pts = total; this.view.mult = info.mult;
      this._tag(`ИТОГ ${total.toLocaleString('ru-RU')}${money ? ` · +${money.toLocaleString('ru-RU')} ₽` : ''}`, 'bank', 2.5);
      this.g.audio.coin();
    };
    this.sc.onLost = (reason, pts) => {
      this.view.pts = 0; this.view.mult = 1;
      this._tag(`${reason.toUpperCase()}: −${pts.toLocaleString('ru-RU')}`, 'lost', 2);
      this.g.audio.beep(300, 0.18, 0.12, 'sawtooth');
    };
  }

  _tag(text, kind, sec) { this.view.tag = text; this.view.tagKind = kind; this.tagT = sec; }

  /** opts: { zone (множитель зоны), colliders (CollisionWorld[] для бонуса близости; по умолчанию — город) } */
  update(dt, opts = {}) {
    const g = this.g, p = g.player.physics, sc = this.sc;
    if (g.onFoot) { sc.bank(); g.hud.drift(null); return; }
    // близость к стенам и машинам считаем только в заносе и 10 раз в секунду
    if (sc.active) {
      this._nearT -= dt;
      if (this._nearT <= 0) {
        this._nearT = 0.1;
        this.near = nearestObstacle(g.player, opts.colliders || this._cols, g.traffic.cars);
      }
    } else this.near = Infinity;
    sc.update(dt, p, { grass: g.player.surface.type === 2, near: this.near, zone: opts.zone ?? 1 });

    const v = this.view;
    if (this.tagT > 0) {
      this.tagT -= dt;
      if (this.tagT <= 0) { v.tag = ''; v.tagKind = ''; }
    }
    if (sc.inSeries) { v.pts = sc.pts * sc.mult; v.mult = sc.mult; }
    v.angle = sc.active ? sc.angle : 0;
    v.active = sc.active;
    g.hud.drift(sc.inSeries || this.tagT > 0 ? v : null, dt);
  }

  lose(reason) { this.sc.lose(reason); }
  bank() { return this.sc.bank(); }
  hide() { this.g.hud.drift(null); }
}
