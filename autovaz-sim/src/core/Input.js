import { clamp, wrapAngle } from '../utils/math.js';

const WHEEL_MAX = (150 * Math.PI) / 180; // ±150° виртуального руля = полный угол колёс

/**
 * Сенсорное управление (Pointer Events, multi-touch) + клавиатура для отладки на ПК.
 * Выход: steer [-1..1] (+ вправо), throttle/brake [0..1], handbrake, horn.
 */
export class Input {
  constructor() {
    this.steer = 0;
    this.throttle = 0;
    this.brake = 0;
    this.handbrake = false;
    this.horn = false;

    this._wheelAngle = 0;
    this._wheelPointer = null;
    this._lastAng = 0;
    this._gas = false;
    this._brk = false;
    this._keys = new Set();
    this.handlers = { camera: [], lights: [], time: [] };

    this._bindTouch();
    this._bindKeys();
  }

  on(evt, fn) { this.handlers[evt].push(fn); }
  _emit(evt) { for (const fn of this.handlers[evt]) fn(); }

  _bindTouch() {
    const wheel = document.getElementById('wheel');
    this._wheelInner = document.getElementById('wheel-inner');

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
      if (dx * dx + dy * dy < 144) return; // у центра угол нестабилен
      const ang = Math.atan2(dy, dx);
      const d = wrapAngle(ang - this._lastAng);
      this._lastAng = ang;
      // по часовой стрелке на экране = руль вправо
      this._wheelAngle = clamp(this._wheelAngle + d, -WHEEL_MAX, WHEEL_MAX);
    });
    const releaseWheel = (e) => { if (e.pointerId === this._wheelPointer) this._wheelPointer = null; };
    wheel.addEventListener('pointerup', releaseWheel);
    wheel.addEventListener('pointercancel', releaseWheel);

    const hold = (id, set) => {
      const el = document.getElementById(id);
      const down = (e) => { el.setPointerCapture(e.pointerId); set(true); el.classList.add('on'); e.preventDefault(); };
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

    const tap = (id, evt) => document.getElementById(id).addEventListener('pointerdown', (e) => {
      e.preventDefault();
      this._emit(evt);
    });
    tap('btn-cam', 'camera');
    tap('btn-lights', 'lights');
    tap('btn-time', 'time');

    // не даём браузеру WebView скроллить/зумить
    document.addEventListener('touchmove', (e) => e.preventDefault(), { passive: false });
    document.addEventListener('contextmenu', (e) => e.preventDefault());
  }

  _bindKeys() {
    window.addEventListener('keydown', (e) => {
      if (e.repeat) return;
      this._keys.add(e.code);
      if (e.code === 'KeyC') this._emit('camera');
      if (e.code === 'KeyL') this._emit('lights');
      if (e.code === 'KeyT') this._emit('time');
    });
    window.addEventListener('keyup', (e) => this._keys.delete(e.code));
    window.addEventListener('blur', () => this._keys.clear());
  }

  update(dt) {
    const k = this._keys;
    const kLeft = k.has('ArrowLeft') || k.has('KeyA');
    const kRight = k.has('ArrowRight') || k.has('KeyD');
    const kGas = k.has('ArrowUp') || k.has('KeyW');
    const kBrake = k.has('ArrowDown') || k.has('KeyS');

    if (kLeft || kRight) {
      // клавиатура: плавный доворот руля
      const target = (kRight ? 1 : 0) - (kLeft ? 1 : 0);
      this._wheelAngle += clamp(target * WHEEL_MAX - this._wheelAngle, -WHEEL_MAX * 3 * dt, WHEEL_MAX * 3 * dt);
    } else if (this._wheelPointer === null) {
      // самовозврат руля
      this._wheelAngle *= Math.exp(-dt * 7);
      if (Math.abs(this._wheelAngle) < 1e-3) this._wheelAngle = 0;
    }
    this.steer = this._wheelAngle / WHEEL_MAX;

    // педали нажимаются не мгновенно — меньше пробуксовки у заднеприводной «семёрки»
    const gasTarget = this._gas || kGas ? 1 : 0;
    const brkTarget = this._brk || kBrake ? 1 : 0;
    this.throttle += clamp(gasTarget - this.throttle, -8 * dt, 4 * dt);
    this.brake += clamp(brkTarget - this.brake, -10 * dt, 6 * dt);
    this.handbrake = !!this._hb || k.has('Space');
    this.horn = !!this._hornTouch || k.has('KeyH');

    if (this._wheelInner) {
      this._wheelInner.style.transform = `rotate(${(this._wheelAngle * 180) / Math.PI}deg)`;
    }
  }
}
