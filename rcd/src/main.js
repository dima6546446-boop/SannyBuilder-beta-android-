import { Gfx } from './render/scene.js';
import { AudioSystem } from './audio/audio.js';
import { Controls } from './input/controls.js';
import { Progress } from './game/progression.js';
import { Session } from './game/session.js';
import { getMap } from './game/maps.js';
import { getCar } from './game/cars.js';
import { Hud, buildTouch } from './ui/hud.js';
import { menuScreen, playScreen, garageScreen, settingsScreen, hintsScreen, pauseScreen, resultsScreen, fmtNum } from './ui/screens.js';
import { CAMERA_MODES } from './render/camera.js';

const $ = (s) => document.querySelector(s);

class App {
  constructor() {
    this.progress = new Progress();
    this.audio = new AudioSystem();
    this.controls = new Controls(window);
    this.gfx = new Gfx($('#gl'));
    this.hud = new Hud($('#hud'), $('#speedfx'));
    this.uiRoot = $('#ui');
    this.state = 'menu';            // menu | play | pause | results
    this.screen = 'menu';
    this.nav = [];
    this.session = null;
    this.countdown = 0;
    const p = this.progress;
    this.sel = { mode: 'free', map: 'parking', challenge: null, car: p.selected, time: 'auto', weather: 'clear' };
    this.garage = { tab: 'cars', car: p.selected };
    this.setTab = 'gfx';
    this.result = null;
    this.last = performance.now();
    this.applySettings();
    this.bind();
    this.showCarInRoom(p.selected);
    this.gfx.enterShowroom();
    this.go('menu', false);
    window.__rcd = this;
    requestAnimationFrame((t) => this.loop(t));
    setTimeout(() => $('#loading').classList.add('gone'), 400);
    if (!p.settings.hintsSeen) setTimeout(() => { if (this.screen === 'menu') this.go('hints'); p.updateSettings({ hintsSeen: true }); }, 700);
  }

  // ---------- настройки ----------
  applySettings() {
    const s = this.progress.settings;
    this.gfx.setSettings({ quality: s.quality, shadows: s.shadows, effects: s.effects, fov: s.fov, speedFx: s.speedFx, pixelRatio: s.pixelRatio });
    this.audio.setVolumes({ master: s.master, engine: s.engine, sfx: s.sfx, music: s.music });
    this.controls.settings.steerSens = s.steerSens; this.controls.settings.deadzone = s.deadzone;
    if (this.session) { this.session.car.assist = s.assist; this.session.opts.assist = s.assist; this.session.car.autoGear = s.transmission === 'auto'; }
    const touchOn = s.touch === 'on' || (s.touch === 'auto' && ('ontouchstart' in window || navigator.maxTouchPoints > 0));
    this.touchOn = touchOn;
    document.body.classList.toggle('touch', touchOn);
    if (touchOn && !this.touchBuilt) { buildTouch($('#touch'), this.controls, () => this.pauseToggle()); this.touchBuilt = true; }
    $('#touch').classList.toggle('hidden', !(touchOn && this.state === 'play'));
  }

  // ---------- события ввода ----------
  bind() {
    const unlock = () => { this.audio.unlock(); this.audio.startMusic(); };
    window.addEventListener('pointerdown', unlock, { once: false });
    window.addEventListener('keydown', unlock);
    window.addEventListener('resize', () => this.gfx.resize());
    window.addEventListener('blur', () => { if (this.state === 'play') this.pauseToggle(); });
    document.addEventListener('visibilitychange', () => { if (document.hidden && this.state === 'play') this.pauseToggle(); });
    this.controls.on('pause', () => this.pauseToggle());
    this.controls.on('camera', () => this.cycleCamera());
    this.controls.on('reset', () => { if (this.state === 'play' && !this.countdown) { this.session.respawn(); this.toast('Машина возвращена на старт', 'bad'); } });
    this.controls.on('hints', () => { if (this.state === 'play') { this.pauseToggle(); this.go('hints'); } else if (this.screen === 'hints') this.act('closeHints'); else if (this.state === 'menu') this.go('hints'); });
    this.uiRoot.addEventListener('click', (e) => { const el = e.target.closest('[data-act]'); if (el) { this.audio.unlock(); this.act(el.dataset.act, el.dataset); } });
    this.uiRoot.addEventListener('input', (e) => { const t = e.target; if (t.dataset.input) this.onInput(t, false); });
    this.uiRoot.addEventListener('change', (e) => { const t = e.target; if (t.dataset.input) this.onInput(t, true); });
    this.uiRoot.addEventListener('pointerover', (e) => { const el = e.target.closest('.btn,.card,.tab,.carrow,.item,.opt'); if (el && el !== this._hov) { this._hov = el; this.audio.hover(); } });
  }

  onInput(t, commit) {
    const k = t.dataset.input, p = this.progress;
    if (k === 'paintCustom') { if (commit) { p.setPaint(this.garage.car, t.value); this.refresh(); } return; }
    const v = parseFloat(t.value);
    p.updateSettings({ [k]: v });
    const lab = document.getElementById('v-' + k);
    if (lab) lab.textContent = this.fmtSetting(k, v);
    this.applySettings();
  }
  fmtSetting(k, v) {
    if (['master', 'engine', 'sfx', 'music'].includes(k)) return Math.round(v * 100) + '%';
    if (k === 'pixelRatio') return Math.round(v * 100) + '%';
    if (k === 'fov') return v + '°';
    if (k === 'assist') return v <= 0 ? 'выкл.' : Math.round(v * 100) + '%';
    if (k === 'deadzone') return Math.round(v * 100) + '%';
    return v.toFixed(2);
  }

  toast(msg, cls = '') {
    const d = document.createElement('div'); d.className = 'toast ' + cls; d.textContent = msg; $('#toast').appendChild(d); setTimeout(() => d.remove(), 2900);
  }

  // ---------- навигация ----------
  go(name, push = true) {
    if (push && this.screen && this.screen !== name) this.nav.push(this.screen);
    this.screen = name;
    if (name === 'garage') { this.gfx.enterShowroom(); this.showCarInRoom(this.garage.car); }
    if (name === 'menu') { this.nav = []; this.gfx.enterShowroom(); this.showCarInRoom(this.progress.selected); }
    if (name === 'play') { this.gfx.enterShowroom(); this.showCarInRoom(this.sel.car); }
    this.render();
  }
  back() { const prev = this.nav.pop() || 'menu'; this.screen = prev; if (prev === 'garage') this.showCarInRoom(this.garage.car); if (prev === 'menu') { this.showCarInRoom(this.progress.selected); } this.render(); }

  render() {
    const root = this.uiRoot;
    const scrolls = [...root.querySelectorAll('.scroll')].map((e) => e.scrollTop);
    const fn = { menu: menuScreen, play: playScreen, garage: garageScreen, settings: settingsScreen, hints: hintsScreen, pause: pauseScreen, results: resultsScreen }[this.screen];
    root.innerHTML = fn ? fn(this) : '';
    [...root.querySelectorAll('.scroll')].forEach((e, i) => { if (scrolls[i]) e.scrollTop = scrolls[i]; });
  }
  refresh() { this.render(); if (this.screen === 'garage') this.showCarInRoom(this.garage.car); else if (this.screen === 'play') this.showCarInRoom(this.sel.car); }

  look(id) {
    const st = this.progress.owns(id) ? this.progress.carState(id) : null;
    const def = getCar(id);
    return st ? { paint: st.paint, wheels: st.wheels, bodykit: st.bodykit, spoiler: st.spoiler, neon: st.neon } : { paint: def.color, wheels: 'steel', bodykit: 'none', spoiler: 'none', neon: 'none' };
  }
  showCarInRoom(id) { if (this.state !== 'play' && this.state !== 'pause') this.gfx.setCarLook(getCar(id), this.look(id)); }

  // ---------- действия интерфейса ----------
  act(a, d = {}) {
    const p = this.progress, A = this.audio;
    const click = () => A.click();
    switch (a) {
      case 'back': A.back(); this.back(); break;
      case 'goPlay': click(); this.sel.car = p.owns(this.sel.car) ? this.sel.car : p.selected; this.go('play'); break;
      case 'goGarage': click(); this.garage.car = p.selected; this.garage.tab = 'cars'; this.go('garage'); break;
      case 'goSettings': click(); this.go('settings'); break;
      case 'goHints': click(); this.go('hints'); break;
      case 'closeHints': A.back(); if (this.state === 'pause') { this.screen = 'pause'; this.render(); } else this.back(); break;
      case 'pickMap': { const m = getMap(d.id); if (!p.mapUnlocked(d.id)) { A.error(); this.toast(`Карта откроется на уровне ${m.level}`, 'bad'); break; } click(); this.sel.map = d.id; this.sel.challenge = null; this.render(); break; }
      case 'pickMode': click(); this.sel.mode = d.id; this.sel.challenge = null; this.render(); break;
      case 'pickChallenge': click(); this.sel.challenge = d.id; this.render(); break;
      case 'pickCar': click(); this.sel.car = d.id; this.refresh(); break;
      case 'pickTime': click(); this.sel.time = d.id; this.render(); break;
      case 'pickWeather': click(); this.sel.weather = d.id; this.render(); break;
      case 'start': if (!p.mapUnlocked(this.sel.map)) { A.error(); break; } if (this.sel.mode === 'challenge' && !this.sel.challenge) { A.error(); this.toast('Выбери испытание', 'bad'); break; } click(); this.startGame(); break;
      // гараж
      case 'garageTab': click(); this.garage.tab = d.id; this.render(); break;
      case 'garageCar': click(); this.garage.car = d.id; if (!p.owns(d.id)) this.garage.tab = 'cars'; this.refresh(); break;
      case 'selectCar': click(); p.select(d.id); this.sel.car = d.id; this.refresh(); break;
      case 'buyCar': { const r = p.buy(d.id); if (r.ok) { A.buy(); this.sel.car = d.id; this.toast('Машина куплена!', 'ok'); } else { A.error(); this.toast(r.reason, 'bad'); } this.refresh(); break; }
      case 'sellCar': { const r = p.sell(d.id); if (r.ok) { A.buy(); this.toast('Продано за ' + fmtNum(r.value) + ' ₽', 'ok'); this.garage.car = p.selected; this.sel.car = p.owns(this.sel.car) ? this.sel.car : p.selected; } else { A.error(); this.toast(r.reason, 'bad'); } this.refresh(); break; }
      case 'upgrade': { const r = p.upgrade(this.garage.car, d.id); if (r.ok) A.buy(); else { A.error(); this.toast(r.reason, 'bad'); } this.refresh(); break; }
      case 'downgrade': { const r = p.downgrade(this.garage.car, d.id); if (r.ok) { A.click(); this.toast('Возвращено ' + fmtNum(r.value) + ' ₽', 'ok'); } this.refresh(); break; }
      case 'paint': click(); p.setPaint(this.garage.car, d.id); this.refresh(); break;
      case 'setPart': { const r = p.setPart(this.garage.car, d.slot, d.id); if (r.ok) A.click(); else { A.error(); this.toast(r.reason, 'bad'); } this.refresh(); break; }
      // настройки
      case 'setTab': click(); this.setTab = d.id; this.render(); break;
      case 'toggle': click(); p.updateSettings({ [d.id]: !p.settings[d.id] }); this.applySettings(); this.render(); break;
      case 'choice': click(); p.updateSettings({ [d.key]: d.id }); this.applySettings(); this.render(); break;
      case 'resetSave': if (confirm('Сбросить весь прогресс (деньги, машины, рекорды)?')) { p.reset(); this.sel.car = p.selected; this.garage.car = p.selected; this.applySettings(); this.toast('Прогресс сброшен'); this.go('menu', false); } break;
      // пауза / результаты
      case 'resume': click(); this.pauseToggle(); break;
      case 'restart': click(); this.restartGame(); break;
      case 'cam': click(); this.cycleCamera(); this.render(); break;
      case 'pauseHints': click(); this.go('hints'); break;
      case 'pauseSettings': click(); this.go('settings'); break;
      case 'finish': click(); this.endRun(false); break;
      case 'quit': click(); this.quitToMenu(); break;
      case 'again': click(); this.startGame(); break;
      case 'toPlay': click(); this.leaveGame(); this.go('play', false); break;
      case 'toMenu': click(); this.leaveGame(); this.go('menu', false); break;
    }
  }

  // ---------- игра ----------
  startGame() {
    const s = this.sel, p = this.progress, set = p.settings;
    const map = getMap(s.map), st = p.carState(s.car), def = getCar(s.car);
    const time = s.time === 'auto' ? map.time : s.time;
    this.session = new Session({ mapId: s.map, carId: s.car, tuning: st.tuning, mode: s.mode, challengeId: s.mode === 'challenge' ? s.challenge : null, weather: s.weather, assist: set.assist, transmission: set.transmission });
    const sess = this.session;
    sess.on((e) => this.onEvent(e));
    sess.car.onShift = () => this.audio.shift(def.turbo > 0 || (st.tuning.turbo || 0) > 0);
    this.gfx.leaveShowroom();
    this.gfx.loadMap(map, { time, weather: s.weather });
    this.gfx.carLookKey = '';
    this.gfx.setCarLook(def, this.look(s.car));
    this.gfx.setGates(sess.challenge && sess.challenge.type === 'gates' ? map.gates : null, 0);
    this.gfx.enterGame();
    this.gfx.rig.setMode(set.camera);
    this.hud.mount(sess); this.hud.show(true);
    this.controls.reset();
    this.state = 'play'; this.screen = ''; this.render();
    this.countdown = 3.2; this.lastCount = 4;
    this.applySettings();
    // стартовая камера сразу за машиной
    this.gfx.rig.init = false;
  }

  restartGame() {
    this.session.restart(); this.gfx.skids.clear(); this.gfx.setGates(this.session.challenge && this.session.challenge.type === 'gates' ? this.session.map.gates : null, 0);
    this.controls.reset();
    this.state = 'play'; this.screen = ''; this.render();
    this.countdown = 3.2; this.lastCount = 4; this.hud.mount(this.session);
    this.applySettings();
  }

  pauseToggle() {
    if (this.state === 'play') { this.state = 'pause'; this.screen = 'pause'; this.nav = []; this.controls.reset(); this.render(); this.applySettings(); }
    else if (this.state === 'pause') { this.state = 'play'; this.screen = ''; this.render(); this.applySettings(); }
  }

  cycleCamera() { if (this.state !== 'play' && this.state !== 'pause') return; const m = this.gfx.rig.nextMode(); this.hud.cameraName(m); }

  onEvent(e) {
    const A = this.audio, H = this.hud;
    switch (e.type) {
      case 'impact': this.gfx.onSessionEvent(e); if (e.light) A.tick(0.15); else { A.impact(e.speed); if (navigator.vibrate && e.speed > 4) navigator.vibrate(40); } break;
      case 'lost': H.popup('−' + fmtNum(e.points) + '  СЕРИЯ СГОРЕЛА', 'bad'); A.lost(); break;
      case 'bank': H.popup('+' + fmtNum(e.points) + (e.mult > 1 ? '  ×' + e.mult.toFixed(1).replace('.0', '') : ''), e.points > 2000 ? 'good' : 'acc'); A.bank(e.points); break;
      case 'transition': H.popup('СМЕНА СТОРОНЫ! множитель ×' + e.mult.toFixed(2).replace(/\.?0+$/, ''), 'cy'); A.mult(); break;
      case 'mult': A.mult(); break;
      case 'gate': H.popup(`Ворота ${e.index}/${e.total}`, 'good'); A.gate(); this.gfx.onSessionEvent(e); break;
      case 'respawn': this.gfx.skids.clear(); break;
      case 'finish': this.endRun(true); break;
    }
  }

  endRun(auto) {
    if (!this.session) return;
    const sess = this.session;
    if (!sess.finished) sess.finish(sess.mode === 'free' ? true : false);
    const summary = sess.summary();
    const reward = this.progress.finishRun(summary);
    this.result = { summary, reward };
    this.state = 'results'; this.screen = 'results';
    this.controls.reset();
    this.render();
    if (summary.success) this.audio.success(); else this.audio.error();
    if (reward.levelUps > 0) setTimeout(() => this.audio.levelUp(), 700);
    this.applySettings();
  }

  leaveGame() { this.hud.show(false); this.state = 'menu'; this.session = null; this.countdown = 0; this.gfx.enterShowroom(); this.applySettings(); }
  quitToMenu() { this.leaveGame(); this.go('menu', false); }

  // ---------- главный цикл ----------
  loop(now) {
    const dt = Math.min(0.05, (now - this.last) / 1000); this.last = now;
    try { this.tick(dt); } catch (e) { console.error(e); this.fatal = e; }
    requestAnimationFrame((t) => this.loop(t));
  }

  tick(dt) {
    const g = this.gfx;
    if (this.session && (this.state === 'play' || this.state === 'pause' || this.state === 'results')) {
      const sess = this.session, car = sess.car;
      let frameDt = 0;
      if (this.state === 'play') {
        if (this.countdown > 0) {
          const prev = Math.ceil(this.countdown);
          this.countdown -= dt;
          const n = Math.ceil(this.countdown);
          if (n !== this.lastCount) { this.lastCount = n; if (n > 0 && n <= 3) { this.hud.countdown(String(n)); this.audio.countdown(false); } }
          if (this.countdown <= 0) { this.hud.countdown('ГО!'); this.audio.countdown(true); setTimeout(() => this.hud.countdown(''), 700); }
          this.controls.update(dt, 0);
          car.input.throttle = 0; car.input.brake = 0; car.input.handbrake = true; car.input.steer = 0;
          sess.update(dt, null);
        } else {
          const inp = this.controls.update(dt, car.speed);
          sess.update(dt, inp);
          frameDt = dt;
        }
        if (this.countdown > 0) { car.vx = car.vz = 0; car.w = 0; }
        this.audio.update(car, car.input.throttle, car.contactSurface[2]);
      } else this.audio.update(car, 0, 'asphalt', true);
      g.frame(this.state === 'play' ? dt : 0, sess);
      this.hud.update(dt, sess, this.progress.settings);
    } else {
      this.audio.update({ rpm: 0, spec: { redline: 7000, turbo: 0 }, slip: [0, 0, 0, 0], speed: 0, boost: 0, throttleApplied: 0, limiter: 0 }, 0, 'asphalt', true);
      g.showroomFrame(dt, 0.4);
    }
  }
}

window.addEventListener('DOMContentLoaded', () => { new App(); });
