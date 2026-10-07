// Ввод: клавиатура, геймпад, экранные кнопки. Руль сглаживается и зависит от скорости.
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const lerp = (a, b, t) => a + (b - a) * t;

export const KEYMAP = {
  throttle: ['KeyW', 'ArrowUp'],
  brake: ['KeyS', 'ArrowDown'],
  left: ['KeyA', 'ArrowLeft'],
  right: ['KeyD', 'ArrowRight'],
  handbrake: ['Space'],
  kick: ['ShiftLeft', 'ShiftRight'],
  shiftUp: ['KeyE', 'Period'],
  shiftDown: ['KeyQ', 'Comma'],
  camera: ['KeyC'],
  reset: ['KeyR'],
  pause: ['Escape', 'KeyP'],
  hints: ['KeyH', 'F1'],
  map: ['KeyM'],
};

export class Controls {
  constructor(target = null) {
    this.keys = new Set();
    this.touch = { left: false, right: false, throttle: false, brake: false, handbrake: false, kick: false };
    this.settings = { steerSens: 1, deadzone: 0.1 };
    this.state = { throttle: 0, brake: 0, steer: 0, handbrake: false, kick: false, shiftUp: false, shiftDown: false };
    this.padActive = false;
    this.pressedEdge = new Set();
    this.actions = {};
    this._prevPad = [];
    this._steerKey = 0;
    if (target) this.attach(target);
  }

  attach(target) {
    const kd = (e) => {
      if (e.repeat) { if (this.isGameKey(e.code)) e.preventDefault(); return; }
      this.keys.add(e.code); this.pressedEdge.add(e.code);
      if (this.isGameKey(e.code)) e.preventDefault();
      this.dispatchKey(e.code);
    };
    const ku = (e) => { this.keys.delete(e.code); };
    target.addEventListener('keydown', kd);
    target.addEventListener('keyup', ku);
    target.addEventListener('blur', () => { this.keys.clear(); });
    this._detach = () => { target.removeEventListener('keydown', kd); target.removeEventListener('keyup', ku); };
  }

  isGameKey(code) { return Object.values(KEYMAP).some((a) => a.includes(code)); }
  down(action) { return KEYMAP[action].some((c) => this.keys.has(c)); }
  on(action, fn) { this.actions[action] = fn; }
  dispatchKey(code) {
    for (const a of ['camera', 'reset', 'pause', 'hints', 'map']) if (KEYMAP[a].includes(code) && this.actions[a]) this.actions[a]();
  }

  /** Эмуляция нажатия (тесты, e2e). */
  press(code) { this.keys.add(code); this.dispatchKey(code); }
  release(code) { this.keys.delete(code); }

  /** Обновление: dt (с), speed (м/с) -> состояние для машины. */
  update(dt, speed = 0) {
    const s = this.state, st = this.settings;
    const sf = clamp(speed / 35, 0, 1);

    // клавиатура / экранные кнопки
    const kThr = this.down('throttle') || this.touch.throttle ? 1 : 0;
    const kBrk = this.down('brake') || this.touch.brake ? 1 : 0;
    const kL = this.down('left') || this.touch.left, kR = this.down('right') || this.touch.right;
    let steerTarget = (kR ? 1 : 0) - (kL ? 1 : 0);

    // геймпад
    let pThr = 0, pBrk = 0, pSteer = 0, pHand = false, pKick = false, pUp = false, pDown = false;
    this.padActive = false;
    if (typeof navigator !== 'undefined' && navigator.getGamepads) {
      const pads = navigator.getGamepads();
      for (const gp of pads) {
        if (!gp || !gp.connected) continue;
        const b = (i) => (gp.buttons[i] ? gp.buttons[i].value : 0);
        const pr = (i) => !!(gp.buttons[i] && gp.buttons[i].pressed);
        let ax = gp.axes[0] || 0;
        const dz = st.deadzone;
        ax = Math.abs(ax) < dz ? 0 : Math.sign(ax) * (Math.abs(ax) - dz) / (1 - dz);
        pSteer = Math.sign(ax) * Math.pow(Math.abs(ax), 1.35);
        pThr = b(7); pBrk = b(6);
        // запасной вариант: оси триггеров
        pHand = pr(0); pKick = pr(1) || pr(5); pUp = pr(5) || pr(12); pDown = pr(4) || pr(13);
        const prev = this._prevPad;
        const edge = (i) => pr(i) && !prev[i];
        if (edge(3) && this.actions.camera) this.actions.camera();
        if (edge(2) && this.actions.reset) this.actions.reset();
        if (edge(9) && this.actions.pause) this.actions.pause();
        if (edge(8) && this.actions.hints) this.actions.hints();
        this._prevPad = gp.buttons.map((x) => !!x.pressed);
        if (pThr > 0.02 || pBrk > 0.02 || Math.abs(pSteer) > 0.02 || pHand) this.padActive = true;
        break;
      }
    }

    // газ/тормоз
    const thrTarget = Math.max(kThr, pThr), brkTarget = Math.max(kBrk, pBrk);
    s.throttle = approach(s.throttle, thrTarget, (thrTarget > s.throttle ? 5.5 : 9) * dt);
    s.brake = approach(s.brake, brkTarget, (brkTarget > s.brake ? 7 : 12) * dt);

    // руль
    if (Math.abs(pSteer) > 0.001 && !(kL || kR)) {
      // аналоговый: небольшое сглаживание
      s.steer = lerp(s.steer, pSteer, clamp(dt * 18, 0, 1));
    } else {
      // клавиатура: скорость поворота падает со скоростью, возврат быстрее
      const attack = lerp(4.6, 2.3, sf) * st.steerSens;
      const release = lerp(7.5, 5.0, sf);
      const counter = lerp(8.5, 6.5, sf) * st.steerSens;   // перекладка в другую сторону — быстрее
      if (steerTarget === 0) s.steer = approach(s.steer, 0, release * dt);
      else if (Math.sign(s.steer) !== 0 && Math.sign(steerTarget) !== Math.sign(s.steer)) s.steer = approach(s.steer, steerTarget, counter * dt);
      else s.steer = approach(s.steer, steerTarget, attack * dt);
    }
    s.steer = clamp(s.steer, -1, 1);
    s.handbrake = this.down('handbrake') || this.touch.handbrake || pHand;
    s.kick = this.down('kick') || this.touch.kick || pKick;
    s.shiftUp = (this.pressedEdge.has('KeyE') || this.pressedEdge.has('Period')) || (pUp && !this._pu);
    s.shiftDown = (this.pressedEdge.has('KeyQ') || this.pressedEdge.has('Comma')) || (pDown && !this._pd);
    this._pu = pUp; this._pd = pDown;
    this.pressedEdge.clear();
    return s;
  }

  reset() { Object.assign(this.state, { throttle: 0, brake: 0, steer: 0, handbrake: false, kick: false, shiftUp: false, shiftDown: false }); this.keys.clear(); }
}

function approach(v, t, d) { return v < t ? Math.min(t, v + d) : Math.max(t, v - d); }
