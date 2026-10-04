import { Game } from './Game.js';
import { resolveQuality, saveQualityName } from './config/quality.js';
import { Save } from './core/Save.js';
import { AudioManager } from './core/AudioManager.js';
import { GarageScene } from './world/GarageScene.js';
import { Menus } from './ui/Menus.js';
import { ParkingMode } from './gameplay/ParkingMode.js';
import { FreeRideMode } from './gameplay/FreeRideMode.js';
import { ExamMode } from './gameplay/ExamMode.js';
import { DriftMode } from './gameplay/DriftMode.js';
import { CAR_BY_ID, allRenderModels } from './config/cars.js';
import { ModelLibrary } from './vehicles/ModelLibrary.js';
import { Net, cleanNick } from './net/Net.js';
import { PHRASES } from './net/RemotePlayers.js';

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);

/**
 * Приложение: меню (3D-гараж на фоне) ↔ игра (режимы «Парковка», «Город», «Экзамен», «Дрифт»).
 */
class App {
  constructor() {
    this.save = new Save();
    const qName = this.save.data.settings.quality;
    if (qName && !params.get('q')) saveQualityName(qName);
    this.quality = resolveQuality();
    this.audio = new AudioManager();
    this.state = 'menu';
    this.paused = false;
    this.last = performance.now();
    this.net = new Net();
  }

  async boot() {
    const intro = document.getElementById('intro');
    if (params.has('autostart')) intro.remove();
    const introBar = intro?.querySelector('.intro-bar i');
    this.menus = new Menus(this);
    this.menus._loading('ВЫГОНЯЕМ МАШИНЫ ИЗ ГАРАЖА…');
    await ModelLibrary.load(allRenderModels().map((m) => m.key), (p) => {
      const el = document.querySelector('.loading small');
      if (el) el.textContent = `ЗАГРУЗКА МОДЕЛЕЙ ${Math.round(p * 100)}%`;
      if (introBar) introBar.style.width = `${Math.round(p * 70)}%`;
    });
    this.menus._loading('СТРОИМ АВТОЗАВОДСКИЙ РАЙОН…');
    await new Promise((r) => requestAnimationFrame(() => setTimeout(r, 30)));

    this.game = new Game($('game'), this.quality, this.save);
    window.game = this.game; // отладка: game.stats
    window.app = this;
    this.game.init(this.audio);
    this.game.onPause = () => this.pause();
    this.game.menus = this.menus;
    this.garage = new GarageScene(this.game.renderer, this.game.T, this.quality);
    const cur = this.save.data.current;
    this.garage.showCar(CAR_BY_ID[cur], this.save.tuningValues(cur));
    this.save.onChange(() => this.game.hud.setMoney(this.save.money));

    // звук можно запустить только после жеста пользователя
    const unlock = () => { this.audio.init(); this.audio.setVolume(this.save.data.settings.volume); this.audio.resume(); };
    document.addEventListener('pointerdown', unlock, { once: true });
    document.addEventListener('keydown', unlock, { once: true });
    document.addEventListener('visibilitychange', () => { if (document.hidden) this.pause(); });
    window.__onNativePause = () => this.pause();
    window.__onNativeResume = () => { if (this.state === 'play' && !this.paused) this.audio.resume(); };

    this.game.renderer.setAnimationLoop((t) => this.frame(t));
    if (params.has('free')) this.startFree(params.has('nofines') ? false : undefined);
    else if (params.has('level')) this.startParking(+params.get('level'));
    else if (params.has('exam')) this.startExam();
    else if (params.has('drift')) this.startDrift();
    else this.menus.show('main');
    if (params.has('t')) this.game.dayNight.setTime(parseFloat(params.get('t')));
    // заставка держится, пока название не проявится полностью, затем плавно растворяется
    if (intro?.isConnected) {
      if (introBar) introBar.style.width = '100%';
      const wait = Math.max(0, 3900 - performance.now()); // от открытия страницы: название + подзаголовок успевают проявиться
      setTimeout(() => { intro.classList.add('out'); setTimeout(() => intro.remove(), 1100); }, wait);
    }
  }

  frame(now) {
    const dt = Math.max(0, Math.min((now - this.last) / 1000, 0.05));
    this.last = now;
    if (this.game.contextLost) return;
    if (this.net.active && this.state !== 'menu') this.net.update(dt, () => this.game.netState());
    if (this.state === 'play' && !this.paused) {
      this.game.tick(dt);
      this.game.render(dt);
    } else if (this.state === 'menu') {
      this.game.radio.update(0);
      this.garage.active = true;
      this.garage.render(dt);
    } else {
      this.game.render(0); // пауза/результат — последний кадр мира под меню
      this.game.radio.update(0.35, true);
    }
  }

  onMenu(name) {
    if (['main', 'garage', 'levels', 'help', 'freeSetup', 'daily', 'online'].includes(name) || (name === 'settings' && !this.paused)) {
      this.state = 'menu';
      $('hud').classList.add('hidden');
      $('controls').classList.add('hidden');
      this.audio.update(850, 0, 0, 0);
      this.audio.setHorn(false);
    }
  }

  _enterPlay(mode, arg) {
    this.menus.hide();
    this.garage.active = false;
    this.state = 'play';
    this.paused = false;
    $('hud').classList.remove('hidden');
    $('controls').classList.remove('hidden');
    this.game.refreshCar();
    this.game.setMode(mode, arg);
    this.game.hud.setMoney(this.save.money);
    this.audio.resume();
    const el = document.documentElement;
    if (el.requestFullscreen && !document.fullscreenElement && !params.has('autostart')) {
      el.requestFullscreen({ navigationUI: 'hide' }).then(() => screen.orientation?.lock?.('landscape')).catch(() => {});
    }
  }

  startParking(i) { this._leaveNet(); this.lastStart = () => this.startParking(i); this.game.setTimePreset(i % 5 === 4 ? 1 : i % 7 === 6 ? 3 : 0); this._enterPlay(new ParkingMode(this.game, i)); }
  startFree(fines = this.save.data.settings.fines !== false) { this._leaveNet(); this.lastStart = () => this.startFree(fines); this._enterPlay(new FreeRideMode(this.game, { fines })); }
  startExam() { this._leaveNet(); this.lastStart = () => this.startExam(); this.game.setTimePreset(0); this._enterPlay(new ExamMode(this.game)); }
  startDrift() { this._leaveNet(); this.lastStart = () => this.startDrift(); this.game.setTimePreset(0); this._enterPlay(new DriftMode(this.game)); }
  restart() { this.lastStart?.(); }

  _leaveNet() { if (this.net.active) { this.net.close(); this.game.remote.clear(); this._onlineHud(); } }

  // ---------------------------------------------------------------- онлайн
  /** mode: quick | create | join. Подключаемся, потом — свободная езда с другими игроками. */
  async startOnline(mode, code) {
    const net = this.net, g = this.game, s = this.save.data;
    if (net.active) net.close();
    const tv = this.save.tuningValues(s.current);
    s.settings.nick = cleanNick(s.settings.nick);
    this.save.commit();
    this._wireNet();
    try {
      await net.connect(mode, { nick: s.settings.nick, car: s.current, color: tv.color, plate: tv.plate ? tv.plate.text + tv.plate.region : '' }, code);
    } catch (e) {
      const msg = { absent: `Комната ${code || ''} не найдена`, full: 'Комната заполнена (8 из 8)', offline: 'Нет связи с сервером. Проверь интернет', timeout: 'Сервер не отвечает. Проверь интернет',
        p2p: 'Не удалось соединиться с игроками. Попробуй Wi-Fi', busy: 'Все открытые комнаты заполнены, создай свою' }[e.code] || `Ошибка сети: ${e.message}`;
      if (this.state === 'menu' && this.menus.current === 'online') this.menus.show('online', msg);
      return;
    }
    if (this.menus.current !== 'online') { net.close(); return; } // ушёл из меню, пока подключались
    const fines = s.settings.fines !== false;
    this.lastStart = null;
    this._enterPlay(new FreeRideMode(g, { fines, online: true }));
    if (this._pendingEnv) { this._applyEnv(this._pendingEnv); this._pendingEnv = null; }
    this._onlineHud();
    g.hud.toast(net.pub ? `Онлайн: открытая комната, игроков ${net.count}` : `Комната ${net.room} — отправь код друзьям`, 'good', 4);
  }

  _wireNet() {
    if (this._netWired) return;
    this._netWired = true;
    const net = this.net, g = this.game;
    net.envState = () => ({ time: g.dayNight.time, rain: g.weather.target });
    net.on('join', (id, p) => {
      g.remote.add(id, p);
      if (this.state === 'play') { g.hud.toast(`👋 ${p.nick} в игре`, 'good', 2.2); this._onlineHud(); }
    })
      .on('leave', (id) => {
        const r = g.remote.map.get(id);
        g.remote.remove(id);
        if (r && this.state === 'play' && net.active) g.hud.toast(`${r.p.nick} вышел`, '', 2);
        this._onlineHud();
      })
      .on('state', (id, d) => g.remote.state(id, d))
      .on('say', (id, k) => g.remote.say(id, k))
      .on('env', (e) => { if (this.state === 'play') this._applyEnv(e); else this._pendingEnv = e; })
      .on('lost', () => {
        g.remote.clear();
        this._onlineHud();
        if (this.state === 'play') g.hud.toast('Связь с комнатой потеряна — катаешься один', 'bad', 4);
      });
    $('btn-chat').addEventListener('click', () => this._chatMenu());
  }

  _applyEnv(e) {
    const g = this.game;
    if (Number.isFinite(e.time)) {
      const diff = Math.abs(((e.time - g.dayNight.time + 36) % 24) - 12);
      if (12 - diff > 0.15) g.dayNight.setTime(e.time);
    }
    if (Number.isFinite(e.rain)) g.setWeather(e.rain > 0.5 ? 1 : 0, false);
  }

  _onlineHud() {
    const net = this.net, on = net.active && this.state !== 'menu';
    $('online-row').classList.toggle('hidden', !on);
    $('btn-chat').classList.toggle('hidden', !on);
    if (!on) { $('chat-menu')?.remove(); return; }
    $('online-row').textContent = `👥 ${net.count}${net.pub ? '' : ` · код ${net.room}`}`;
  }

  _chatMenu() {
    const old = $('chat-menu');
    if (old) { old.remove(); return; }
    const m = document.createElement('div');
    m.id = 'chat-menu';
    m.innerHTML = PHRASES.map((t, i) => `<div data-k="${i}">${t}</div>`).join('');
    m.addEventListener('click', (e) => {
      const k = e.target.dataset?.k;
      if (k === undefined) return;
      this.net.say(+k);
      this.game.hud.toast(`Ты: ${PHRASES[k]}`, '', 1.6);
      m.remove();
    });
    $('hud').appendChild(m);
  }

  pause() {
    if (this.state !== 'play' || this.paused) return;
    this.paused = true;
    this.audio.update(850, 0, 0, 0);
    this.audio.setHorn(false);
    $('controls').classList.add('hidden');
    this.menus.show('pause');
  }

  resume() {
    this.menus.hide();
    this.paused = false;
    this.state = 'play';
    $('controls').classList.remove('hidden');
    this.last = performance.now();
  }

  showResult(r) {
    this.paused = true;
    $('controls').classList.add('hidden');
    this.menus.show('result', r);
  }

  toMenu(screen = 'main') {
    if (this.net.active) { this.net.close(); this.game.remote.clear(); }
    this._onlineHud();
    this.game.enterCar(true);
    this.game.mode?.exit();
    this.game.mode = null;
    this.game.traffic.clear();
    this.paused = false;
    this.menus.show(screen);
    const cur = this.save.data.current;
    this.garage.showCar(CAR_BY_ID[cur], this.save.tuningValues(cur));
  }

  refreshCar() {
    this.game.refreshCar();
  }

  applySettings() {
    this.game.applySettings();
    this.save.data.settings.quality = this.quality.name;
  }

  hudToastMenu(text) { this.game.hud.toast(text, 'bad'); }
}

new App().boot();
