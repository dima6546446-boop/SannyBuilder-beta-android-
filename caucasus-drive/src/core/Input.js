import { clamp, wrapAngle } from '../utils/math.js';

const WHEEL_MAX = (150 * Math.PI) / 180; // ±150° виртуального руля = полный угол колёс

/**
 * Сенсорное управление в стиле Car Parking:
 *  - руль (drag по кругу) / стрелки / наклон телефона
 *  - педали газа и тормоза, ручник, рычаг R-N-D (или механика +/−)
 *  - поворотники, аварийка, гудок, свайп по экрану — облёт камеры
 * Плюс клавиатура для ПК: WASD/стрелки, Пробел, Q/E — передачи, Z/X — поворотники, H, C, L, Esc.
 */
export class Input {
  constructor() {
    this.steer = 0; this.throttle = 0; this.brake = 0;
    this.handbrake = false; this.horn = false;
    this.orbit = 0;        // смещение камеры по рысканию от свайпа
    this.orbitActive = false;
    this.mode = 'wheel';
    this.enabled = true;
    // пешком
    this.onFoot = false;
    this.moveX = 0; this.moveY = 0; this.walk = false;
    this.look = { dx: 0, dy: 0 };   // накопленный свайп для камеры (px), обнуляет потребитель
    this._stick = { id: null, x: 0, y: 0 };

    this._wheelAngle = 0;
    this._wheelPointer = null;
    this._keys = new Set();
    this._arrow = { L: false, R: false };
    this._tilt = 0;
    this._tiltZero = null;
    this.handlers = {};
    this._bindTouch();
    this._bindKeys();
  }

  on(evt, fn) { (this.handlers[evt] ||= []).push(fn); }
  emit(evt, arg) { for (const fn of this.handlers[evt] || []) fn(arg); }

  setMode(mode) {
    this.mode = mode;
    document.getElementById('wheel').classList.toggle('hidden', mode !== 'wheel');
    document.getElementById('arrows').classList.toggle('hidden', mode !== 'arrows');
    if (mode === 'tilt') this._enableTilt();
  }

  /** Переключить раскладку: машина ↔ пешком (CSS скрывает лишнее по классу body.onfoot). */
  setOnFoot(v) {
    this.onFoot = v;
    document.body.classList.toggle('onfoot', v);
    this._stick.id = null; this._stick.x = this._stick.y = 0;
    this._knob && (this._knob.style.transform = '');
    this._gas = this._brk = this._hb = this._hornTouch = false;
    this.look.dx = this.look.dy = 0;
  }

  setGearbox(manual) {
    document.getElementById('lever').classList.toggle('hidden', manual);
    document.getElementById('shifter').classList.toggle('hidden', !manual);
  }

  /** Подсветка рычага/передачи из состояния физики. */
  showGear(physics) {
    const sel = physics.manual ? null : physics.selector;
    if (sel !== this._shownSel) {
      this._shownSel = sel;
      document.querySelectorAll('.lslot').forEach((el) => el.classList.toggle('sel', el.dataset.sel === sel));
    }
    if (physics.manual) document.getElementById('shift-gear').textContent = physics.gearLabel;
  }

  _enableTilt() {
    if (this._tiltBound) return;
    this._tiltBound = true;
    const handler = (e) => {
      const a = screen.orientation?.angle ?? window.orientation ?? 0;
      let v = a === 90 ? e.beta : a === 270 || a === -90 ? -e.beta : e.gamma;
      if (v == null) return;
      if (this._tiltZero === null) this._tiltZero = 0;
      this._tilt = clamp((v - this._tiltZero) / 28, -1, 1);
    };
    const req = window.DeviceOrientationEvent?.requestPermission;
    if (req) req().then((r) => r === 'granted' && window.addEventListener('deviceorientation', handler)).catch(() => {});
    else window.addEventListener('deviceorientation', handler);
  }

  _bindTouch() {
    const $ = (id) => document.getElementById(id);
    const wheel = $('wheel');
    this._wheelInner = $('wheel-inner');
    wheel.addEventListener('pointerdown', (e) => {
      if (this._wheelPointer !== null) return;
      this._wheelPointer = e.pointerId;
      wheel.setPointerCapture(e.pointerId);
      const r = wheel.getBoundingClientRect();
      this._wcx = r.left + r.width / 2;
      this._wcy = r.top + r.height / 2;
      this._lastAng = Math.atan2(e.clientY - this._wcy, e.clientX - this._wcx);
      e.preventDefault();
    });
    wheel.addEventListener('pointermove', (e) => {
      if (e.pointerId !== this._wheelPointer) return;
      const dx = e.clientX - this._wcx, dy = e.clientY - this._wcy;
      if (dx * dx + dy * dy < 144) return;
      const ang = Math.atan2(dy, dx);
      this._wheelAngle = clamp(this._wheelAngle + wrapAngle(ang - this._lastAng), -WHEEL_MAX, WHEEL_MAX);
      this._lastAng = ang;
    });
    const rel = (e) => { if (e.pointerId === this._wheelPointer) this._wheelPointer = null; };
    wheel.addEventListener('pointerup', rel);
    wheel.addEventListener('pointercancel', rel);

    const hold = (id, set) => {
      const el = $(id);
      const down = (e) => { el.setPointerCapture(e.pointerId); set(true); el.classList.add('on'); e.preventDefault(); e.stopPropagation(); };
      const up = () => { set(false); el.classList.remove('on'); };
      el.addEventListener('pointerdown', down);
      el.addEventListener('pointerup', up);
      el.addEventListener('pointercancel', up);
      el.addEventListener('lostpointercapture', up);
    };
    hold('gas', (v) => { this._gas = v; });
    hold('brake', (v) => { this._brk = v; });
    hold('hb', (v) => { this._hb = v; });
    hold('btn-horn', (v) => { this._hornTouch = v; });
    hold('arrow-l', (v) => { this._arrow.L = v; });
    hold('arrow-r', (v) => { this._arrow.R = v; });
    hold('shift-up', (v) => { if (v) this.emit('shiftUp'); });
    hold('shift-down', (v) => { if (v) this.emit('shiftDown'); });

    const tap = (id, evt, arg) => $(id).addEventListener('pointerdown', (e) => { e.preventDefault(); e.stopPropagation(); this.emit(evt, arg); });
    tap('btn-cam', 'camera');
    tap('btn-lights', 'lights');
    tap('btn-pause', 'pause');
    tap('ind-l', 'indicator', 'L');
    tap('ind-r', 'indicator', 'R');
    tap('hazard', 'hazard');
    tap('btn-action', 'action');
    tap('btn-taxi', 'taxi');
    tap('btn-door', 'door');
    tap('btn-enter', 'door');
    tap('btn-jump', 'jump');
    tap('btn-sit', 'sit');
    tap('btn-smoke', 'smoke');
    tap('btn-whistle', 'whistle');

    // джойстик пешехода
    const stick = $('stick'), st = this._stick;
    this._knob = $('stick-knob');
    const moveStick = (e) => {
      const r = stick.getBoundingClientRect(), rad = r.width / 2;
      let dx = (e.clientX - (r.left + rad)) / rad, dy = (e.clientY - (r.top + rad)) / rad;
      const m = Math.hypot(dx, dy);
      if (m > 1) { dx /= m; dy /= m; }
      st.x = dx; st.y = -dy;
      this._knob.style.transform = `translate(${dx * rad * 0.62}px, ${dy * rad * 0.62}px)`;
    };
    stick.addEventListener('pointerdown', (e) => {
      if (st.id !== null) return;
      st.id = e.pointerId; stick.setPointerCapture(e.pointerId); moveStick(e); e.preventDefault(); e.stopPropagation();
    });
    stick.addEventListener('pointermove', (e) => { if (e.pointerId === st.id) moveStick(e); });
    const endStick = (e) => { if (e.pointerId !== st.id) return; st.id = null; st.x = st.y = 0; this._knob.style.transform = ''; };
    stick.addEventListener('pointerup', endStick);
    stick.addEventListener('pointercancel', endStick);
    document.querySelectorAll('.lslot').forEach((el) => el.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      this.emit('selector', el.dataset.sel);
    }));

    // облёт камеры свайпом по свободной части экрана
    const canvas = $('game');
    let orbitId = null, lastX = 0, lastY = 0;
    canvas.addEventListener('pointerdown', (e) => { if (orbitId === null) { orbitId = e.pointerId; lastX = e.clientX; lastY = e.clientY; this.orbitActive = true; } });
    canvas.addEventListener('pointermove', (e) => {
      if (e.pointerId !== orbitId) return;
      this.orbit = wrapAngle(this.orbit - (e.clientX - lastX) * 0.008);
      this.look.dx += e.clientX - lastX;
      this.look.dy += e.clientY - lastY;
      lastX = e.clientX; lastY = e.clientY;
    });
    const endOrbit = (e) => { if (e.pointerId === orbitId) { orbitId = null; this.orbitActive = false; } };
    canvas.addEventListener('pointerup', endOrbit);
    canvas.addEventListener('pointercancel', endOrbit);

    // запрет скролла/зума WebView, кроме прокручиваемых панелей меню
    document.addEventListener('touchmove', (e) => {
      if (e.target.closest?.('.gpanel, .level-grid, .settings, .help')) return;
      e.preventDefault();
    }, { passive: false });
    document.addEventListener('contextmenu', (e) => e.preventDefault());
  }

  _bindKeys() {
    const map = { KeyC: 'camera', KeyL: 'lights', Escape: 'pause', KeyP: 'pause', KeyF: 'action', Enter: 'action', KeyT: 'taxi' };
    window.addEventListener('keydown', (e) => {
      if (e.repeat) return;
      this._keys.add(e.code);
      if (e.code === 'KeyG') this.emit('door');
      if (this.onFoot) {
        const foot = { Space: 'jump', KeyB: 'sit', KeyK: 'smoke', KeyR: 'whistle', KeyC: 'camera', Escape: 'pause', KeyP: 'pause' };
        if (foot[e.code]) this.emit(foot[e.code]);
        return;
      }
      if (map[e.code]) this.emit(map[e.code]);
      if (e.code === 'KeyZ') this.emit('indicator', 'L');
      if (e.code === 'KeyX') this.emit('indicator', 'R');
      if (e.code === 'KeyV') this.emit('hazard');
      if (e.code === 'KeyE') { this.emit('shiftUp'); this.emit('selectorStep', 1); }
      if (e.code === 'KeyQ') { this.emit('shiftDown'); this.emit('selectorStep', -1); }
    });
    window.addEventListener('keyup', (e) => this._keys.delete(e.code));
    window.addEventListener('blur', () => this._keys.clear());
  }

  update(dt) {
    const k = this._keys;
    if (this.onFoot) {
      let x = (k.has('KeyD') || k.has('ArrowRight') ? 1 : 0) - (k.has('KeyA') || k.has('ArrowLeft') ? 1 : 0);
      let y = (k.has('KeyW') || k.has('ArrowUp') ? 1 : 0) - (k.has('KeyS') || k.has('ArrowDown') ? 1 : 0);
      if (x || y) { const m = Math.hypot(x, y); x /= m; y /= m; this.walk = k.has('ShiftLeft') || k.has('ShiftRight'); }
      else { x = this._stick.x; y = this._stick.y; this.walk = false; }
      this.moveX = this.enabled ? x : 0;
      this.moveY = this.enabled ? y : 0;
      this.steer = 0; this.throttle = 0; this.brake = 0; this.handbrake = false; this.horn = false;
      if (!this.orbitActive) this.orbit *= Math.exp(-dt * 1.5);
      return;
    }
    const kLeft = k.has('ArrowLeft') || k.has('KeyA') || this._arrow.L;
    const kRight = k.has('ArrowRight') || k.has('KeyD') || this._arrow.R;
    const kGas = k.has('ArrowUp') || k.has('KeyW');
    const kBrake = k.has('ArrowDown') || k.has('KeyS');

    if (this.mode === 'tilt' && !kLeft && !kRight) {
      this._wheelAngle = this._tilt * WHEEL_MAX;
    } else if (kLeft || kRight) {
      const target = (kRight ? 1 : 0) - (kLeft ? 1 : 0);
      const rate = WHEEL_MAX * 2.6 * dt;
      this._wheelAngle += clamp(target * WHEEL_MAX - this._wheelAngle, -rate, rate);
    } else if (this._wheelPointer === null) {
      this._wheelAngle *= Math.exp(-dt * 7);
      if (Math.abs(this._wheelAngle) < 1e-3) this._wheelAngle = 0;
    }
    this.steer = this.enabled ? this._wheelAngle / WHEEL_MAX : 0;

    const gasT = this.enabled && (this._gas || kGas) ? 1 : 0;
    const brkT = this.enabled && (this._brk || kBrake) ? 1 : 0;
    this.throttle += clamp(gasT - this.throttle, -8 * dt, 4 * dt);
    this.brake += clamp(brkT - this.brake, -10 * dt, 6 * dt);
    this.handbrake = this.enabled && (!!this._hb || k.has('Space'));
    this.horn = this.enabled && (!!this._hornTouch || k.has('KeyH'));

    if (!this.orbitActive) this.orbit *= Math.exp(-dt * 1.5);
    if (this._wheelInner) this._wheelInner.style.transform = `rotate(${(this._wheelAngle * 180) / Math.PI}deg)`;
  }
}
