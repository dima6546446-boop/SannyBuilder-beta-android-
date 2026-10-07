import { fmtNum, fmtTime } from './screens.js';
import { CAMERA_MODES } from '../render/camera.js';

const clamp = (v, a, b) => Math.max(a, Math.min(b, v));

/** HUD: очки, множитель, угол, спидометр/тахометр, миникарта, таймер, подсказки, эффект скорости. */
export class Hud {
  constructor(root, fxCanvas) {
    this.root = root; this.fx = fxCanvas; this.fxc = fxCanvas.getContext('2d');
    root.innerHTML = `
      <div class="hud-angle"><div class="lbl">УГОЛ</div><div class="num"><span id="h-angle">0</span><sup>°</sup></div></div>
      <div class="hud-top"><div class="hud-total"><small>ОЧКИ</small><span id="h-total">0</span></div>
        <div class="hud-chain" id="h-chain"><span class="pts" id="h-pts">0</span><span class="mul" id="h-mul">×1</span></div><div class="hud-bar"><i id="h-bar"></i></div></div>
      <div class="hud-mode"><div class="t" id="h-time"></div><div class="g" id="h-goal"></div><div class="c" id="h-chal"></div></div>
      <canvas id="minimap" width="380" height="380"></canvas>
      <canvas id="gauge" width="500" height="500"></canvas>
      <div class="hud-pop" id="h-pop"></div>
      <div class="hud-count" id="h-count"></div>
      <div class="hud-cam" id="h-cam"></div>
      <div class="hud-hint" id="h-hint">Газ ↑ + руль, <kbd>Пробел</kbd> — ручник, <kbd>Shift</kbd> — рывок, <kbd>C</kbd> — камера, <kbd>H</kbd> — подсказки</div>
      <div class="hud-fps" id="h-fps"></div>`;
    this.q = (id) => root.querySelector('#' + id);
    this.el = { angle: this.q('h-angle'), total: this.q('h-total'), chain: this.q('h-chain'), pts: this.q('h-pts'), mul: this.q('h-mul'), bar: this.q('h-bar'), time: this.q('h-time'), goal: this.q('h-goal'), chal: this.q('h-chal'), pop: this.q('h-pop'), count: this.q('h-count'), cam: this.q('h-cam'), hint: this.q('h-hint'), fps: this.q('h-fps') };
    this.gauge = this.q('gauge').getContext('2d'); this.mm = this.q('minimap').getContext('2d');
    this.mmBg = null; this.fpsAcc = 0; this.fpsN = 0;
    this.shownTotal = 0; this.speedSmooth = 0; this.hintT = 0; this.camT = 0;
  }
  show(v) { this.root.classList.toggle('hidden', !v); this.fx.classList.toggle('hidden', !v); }

  mount(session) {
    this.session = session;
    this.buildMinimap(session.map);
    this.shownTotal = 0; this.hintT = 9; this.el.hint.style.opacity = 1;
    this.el.pop.innerHTML = '';
    const s = session;
    this.el.chal.textContent = s.challenge ? s.challenge.name + ' — ' + s.challenge.desc : '';
  }

  buildMinimap(map) {
    const b = map.bounds || { x0: -150, z0: -100, x1: 150, z1: 100 };
    const S = 2.4, W = Math.ceil((b.x1 - b.x0) * S) + 40, H = Math.ceil((b.z1 - b.z0) * S) + 40;
    const c = document.createElement('canvas'); c.width = W; c.height = H;
    const g = c.getContext('2d');
    const X = (x) => (x - b.x0) * S + 20, Y = (z) => (b.z1 - z) * S + 20;
    g.fillStyle = '#10141b'; g.fillRect(0, 0, W, H);
    g.fillStyle = '#1b2230'; g.fillRect(X(b.x0), Y(b.z1), (b.x1 - b.x0) * S, (b.z1 - b.z0) * S);
    const col = { asphalt: '#4a5160', concrete: '#3a4150', grass: '#27402c', gravel: '#6a6254', dirt: '#5a4630', wet: '#3c5668', snow: '#8a96a0' };
    for (const s of map.surfaces) {
      g.fillStyle = g.strokeStyle = col[s.type] || '#444';
      if (s.kind === 'rect') g.fillRect(X(s.x0), Y(s.z1), (s.x1 - s.x0) * S, (s.z1 - s.z0) * S);
      else if (s.kind === 'circle') { g.beginPath(); g.arc(X(s.x), Y(s.z), s.r * S, 0, 7); g.fill(); }
      else { g.lineWidth = s.hw * 2 * S; g.lineJoin = g.lineCap = 'round'; g.beginPath(); s.pts.forEach((p, i) => (i ? g.lineTo(X(p[0]), Y(p[1])) : g.moveTo(X(p[0]), Y(p[1])))); if (s.closed) g.closePath(); g.stroke(); }
    }
    g.fillStyle = '#0c0f14';
    for (const bx of map.boxes) { g.save(); g.translate(X(bx.x), Y(bx.z)); g.rotate(bx.rot || 0); g.fillRect(-bx.w * S / 2, -bx.d * S / 2, bx.w * S, bx.d * S); g.restore(); }
    g.strokeStyle = '#ff7a00'; g.lineWidth = 2;
    for (const sg of map.segments) { if (map.segments.length > 800) break; g.beginPath(); g.moveTo(X(sg[0]), Y(sg[1])); g.lineTo(X(sg[2]), Y(sg[3])); g.stroke(); }
    this.mmBg = { c, S, X, Y, b };
  }

  popup(text, cls = 'acc') {
    const d = document.createElement('div'); d.className = 'pop ' + cls; d.textContent = text;
    this.el.pop.appendChild(d); setTimeout(() => d.remove(), 1900);
    while (this.el.pop.children.length > 4) this.el.pop.firstChild.remove();
  }
  countdown(text) { this.el.count.innerHTML = text ? `<span>${text}</span>` : ''; }
  cameraName(id) { const m = CAMERA_MODES.find((x) => x.id === id); this.el.cam.textContent = 'Камера: ' + m.name; this.el.cam.style.opacity = 1; this.camT = 1.6; }

  update(dt, session, settings, frozen) {
    const car = session.car, sc = session.scoring, e = this.el;
    // очки: плавный счётчик
    const target = sc.total;
    this.shownTotal += (target - this.shownTotal) * clamp(dt * 10, 0, 1);
    if (Math.abs(target - this.shownTotal) < 1) this.shownTotal = target;
    e.total.textContent = fmtNum(this.shownTotal);
    const inChain = sc.chain > 0;
    e.chain.classList.toggle('on', inChain);
    if (inChain) { e.pts.textContent = '+' + fmtNum(sc.chain); e.mul.textContent = '×' + sc.mult.toFixed(sc.mult % 1 ? 2 : 1).replace(/\.?0+$/, ''); }
    const stepT = 2.4; e.bar.style.width = (sc.gap > 0 ? Math.max(0, 1 - sc.gap / 0.9) * 100 : ((sc.chainTime % stepT) / stepT) * 100) + '%';
    e.bar.style.opacity = inChain ? 1 : 0;
    e.angle.textContent = Math.round(sc.active ? sc.angle : Math.abs(car.driftAngle) * 57.3);
    // режим
    if (session.mode === 'timed' || session.challenge) {
      const left = session.timeLimit - session.time;
      e.time.textContent = fmtTime(left); e.time.classList.toggle('warn', left < 10);
      if (session.mode === 'timed') e.goal.textContent = 'Цель ' + fmtNum(session.map.timed.goal);
      else if (session.challenge.type === 'gates') e.goal.textContent = `Ворота ${session.gateIndex} / ${session.map.gates.length}`;
      else if (session.challenge.type === 'angle') e.goal.textContent = `Угол ${session.challenge.target}°: ${(session.holdT || 0).toFixed(1)} / ${session.challenge.hold} с`;
      else e.goal.textContent = 'Цель ' + fmtNum(session.challenge.target);
    } else { e.time.textContent = ''; e.goal.textContent = session.map.name; }
    // подсказки
    this.hintT -= dt; if (this.hintT < 0) e.hint.style.opacity = 0;
    this.camT -= dt; if (this.camT < 0) e.cam.style.opacity = 0;
    // fps
    if (settings.showFps) { this.fpsAcc += dt; this.fpsN++; if (this.fpsAcc > 0.5) { e.fps.textContent = Math.round(this.fpsN / this.fpsAcc) + ' FPS'; this.fpsAcc = 0; this.fpsN = 0; } } else e.fps.textContent = '';
    this.drawGauge(car, settings);
    this.drawMinimap(session);
    this.drawSpeedFx(dt, car, settings);
  }

  drawGauge(car, settings) {
    const g = this.gauge, W = 500, c = W / 2, R = 215;
    g.clearRect(0, 0, W, W);
    const s = car.spec, maxRpm = Math.ceil(s.redline / 1000) * 1000 + 500;
    const a0 = Math.PI * 0.75, sweep = Math.PI * 1.5;
    g.lineCap = 'butt';
    g.fillStyle = 'rgba(8,10,14,0.55)'; g.beginPath(); g.arc(c, c, R + 24, 0, 7); g.fill();
    g.lineWidth = 16; g.strokeStyle = 'rgba(255,255,255,0.14)'; g.beginPath(); g.arc(c, c, R, a0, a0 + sweep); g.stroke();
    // красная зона
    g.strokeStyle = '#ff3b4a'; g.beginPath(); g.arc(c, c, R, a0 + sweep * (s.redline - 500) / maxRpm, a0 + sweep * s.redline / maxRpm + 0.0); g.stroke();
    // заполнение
    const frac = clamp(car.rpm / maxRpm, 0, 1);
    const grd = g.createLinearGradient(0, W, W, 0); grd.addColorStop(0, '#ff7a00'); grd.addColorStop(1, '#ffd36a');
    g.strokeStyle = car.rpm > s.redline - 500 ? '#ff3b4a' : grd; g.beginPath(); g.arc(c, c, R, a0, a0 + sweep * frac); g.stroke();
    // отметки
    g.fillStyle = '#cfd5e0'; g.font = '700 22px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
    for (let k = 0; k * 1000 <= maxRpm; k++) {
      const a = a0 + sweep * k * 1000 / maxRpm, x1 = c + Math.cos(a) * (R - 14), y1 = c + Math.sin(a) * (R - 14), x2 = c + Math.cos(a) * (R - 30), y2 = c + Math.sin(a) * (R - 30);
      g.strokeStyle = '#cfd5e0'; g.lineWidth = 3; g.beginPath(); g.moveTo(x1, y1); g.lineTo(x2, y2); g.stroke();
      g.fillText(String(k), c + Math.cos(a) * (R - 52), c + Math.sin(a) * (R - 52));
    }
    // скорость
    const mph = settings.units === 'mph';
    this.speedSmooth += (car.speed * 3.6 - this.speedSmooth) * 0.3;
    const sp = Math.round(this.speedSmooth * (mph ? 0.6214 : 1));
    g.fillStyle = '#fff'; g.font = 'italic 900 120px sans-serif'; g.fillText(String(sp), c, c - 10);
    g.fillStyle = '#9aa3b2'; g.font = '700 24px sans-serif'; g.fillText(mph ? 'МИЛЬ/Ч' : 'КМ/Ч', c, c + 60);
    // передача
    const gear = car.reversing ? 'R' : String(car.gear);
    g.fillStyle = '#ff7a00'; g.font = 'italic 900 64px sans-serif'; g.fillText(gear, c, c + 125);
    // индикаторы
    if (car.input.handbrake) { g.fillStyle = '#ff3b4a'; g.font = '800 22px sans-serif'; g.fillText('РУЧНИК', c, c + 168); }
    if (s.turbo > 0) { g.strokeStyle = '#39c5ff'; g.lineWidth = 8; g.beginPath(); g.arc(c, c, R - 80, a0, a0 + sweep * 0.33 * car.boost); g.stroke(); }
  }

  drawMinimap(session) {
    const g = this.mm, bg = this.mmBg; if (!bg) return;
    const Wc = 380, car = session.car;
    g.clearRect(0, 0, Wc, Wc);
    g.save(); g.beginPath(); g.arc(Wc / 2, Wc / 2, Wc / 2 - 2, 0, 7); g.clip();
    const view = 2.4 * 1.5 / 2.4 * 2.2; // пикселей миникарты на метр
    const k = 2.4;                      // масштаб фоновой карты
    const viewScale = 2.3;              // px/м на миникарте
    g.translate(Wc / 2, Wc / 2); g.rotate(-car.h);
    g.scale(viewScale / k, viewScale / k);
    g.drawImage(bg.c, -bg.X(car.x), -bg.Y(car.z));
    // ворота
    if (session.challenge && session.challenge.type === 'gates') {
      const gt = session.map.gates;
      gt.forEach((gate, i) => {
        if (i < session.gateIndex) return;
        g.strokeStyle = i === session.gateIndex ? '#42ff7b' : '#39c5ff'; g.lineWidth = i === session.gateIndex ? 8 : 4;
        g.beginPath(); g.moveTo(bg.X(gate.x1) - bg.X(car.x), bg.Y(gate.z1) - bg.Y(car.z)); g.lineTo(bg.X(gate.x2) - bg.X(car.x), bg.Y(gate.z2) - bg.Y(car.z)); g.stroke();
      });
    }
    g.restore();
    // машина
    g.save(); g.translate(Wc / 2, Wc / 2); g.fillStyle = '#ff7a00'; g.strokeStyle = '#fff'; g.lineWidth = 3;
    g.beginPath(); g.moveTo(0, -16); g.lineTo(11, 14); g.lineTo(0, 8); g.lineTo(-11, 14); g.closePath(); g.fill(); g.stroke(); g.restore();
  }

  drawSpeedFx(dt, car, settings) {
    const c = this.fx, g = this.fxc;
    const w = c.clientWidth >> 1, h = c.clientHeight >> 1;
    if (c.width !== w || c.height !== h) { c.width = w; c.height = h; }
    g.clearRect(0, 0, w, h);
    if (!settings.speedFx) return;
    const f = clamp((car.speed * 3.6 - 70) / 110, 0, 1);
    if (f <= 0.01) return;
    const cx = w / 2, cy = h * 0.52, n = 46, t = performance.now() / 1000;
    g.lineWidth = 1.4;
    for (let i = 0; i < n; i++) {
      const a = (i / n) * 6.2832 + Math.sin(i * 12.9) * 0.2, r0 = Math.min(w, h) * (0.42 + ((i * 0.37 + t * (1 + f)) % 1) * 0.15), r1 = r0 + Math.min(w, h) * (0.06 + f * 0.18);
      g.strokeStyle = `rgba(255,255,255,${0.05 + f * 0.14})`;
      g.beginPath(); g.moveTo(cx + Math.cos(a) * r0 * 1.3, cy + Math.sin(a) * r0); g.lineTo(cx + Math.cos(a) * r1 * 1.3, cy + Math.sin(a) * r1); g.stroke();
    }
    const grd = g.createRadialGradient(cx, cy, Math.min(w, h) * 0.45, cx, cy, Math.max(w, h) * 0.75);
    grd.addColorStop(0, 'rgba(0,0,0,0)'); grd.addColorStop(1, `rgba(0,0,0,${f * 0.5})`); g.fillStyle = grd; g.fillRect(0, 0, w, h);
  }
}

/** Экранные кнопки для сенсорных устройств. */
export function buildTouch(root, controls, onPause) {
  root.innerHTML = '';
  const mk = (label, style, key, extra = '') => {
    const b = document.createElement('div'); b.className = 'tb'; b.textContent = label; b.setAttribute('style', style + extra);
    const set = (v) => { controls.touch[key] = v; b.classList.toggle('on', v); };
    b.addEventListener('pointerdown', (e) => { e.preventDefault(); b.setPointerCapture(e.pointerId); set(true); });
    ['pointerup', 'pointercancel', 'lostpointercapture'].forEach((ev) => b.addEventListener(ev, () => set(false)));
    root.appendChild(b); return b;
  };
  mk('◀', 'left:3vw;bottom:8vh;width:90px;height:90px;font-size:34px', 'left');
  mk('▶', 'left:calc(3vw + 104px);bottom:8vh;width:90px;height:90px;font-size:34px', 'right');
  mk('ГАЗ', 'right:3vw;bottom:8vh;width:100px;height:100px', 'throttle');
  mk('ТОРМ', 'right:calc(3vw + 112px);bottom:8vh;width:80px;height:80px;font-size:12px', 'brake');
  mk('DRIFT', 'right:calc(3vw + 20px);bottom:calc(8vh + 116px);width:80px;height:80px;font-size:13px', 'handbrake');
  mk('РЫВОК', 'right:calc(3vw + 112px);bottom:calc(8vh + 92px);width:64px;height:64px;font-size:11px', 'kick');
  const p = document.createElement('div'); p.className = 'tb'; p.textContent = 'II'; p.setAttribute('style', 'right:2vw;top:2vh;width:48px;height:48px;font-size:18px;border-radius:8px'); p.addEventListener('pointerdown', (e) => { e.preventDefault(); onPause(); }); root.appendChild(p);
}
