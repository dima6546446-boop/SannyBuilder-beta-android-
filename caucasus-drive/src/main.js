import { Game } from './Game.js';
import { resolveQuality, saveQualityName } from './config/quality.js';
import { Save } from './core/Save.js';
import { AudioManager } from './core/AudioManager.js';
import { GarageScene } from './world/GarageScene.js';
import { Menus } from './ui/Menus.js';
import { ParkingMode } from './gameplay/ParkingMode.js';
import { FreeRideMode } from './gameplay/FreeRideMode.js';
import { ExamMode } from './gameplay/ExamMode.js';
import { CAR_BY_ID, allRenderModels } from './config/cars.js';
import { ModelLibrary } from './vehicles/ModelLibrary.js';

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);

/**
 * Приложение: меню (3D-гараж на фоне) ↔ игра (режимы «Парковка», «Город», «Экзамен»).
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
  }

  async boot() {
    this.menus = new Menus(this);
    this.menus._loading('ВЫГОНЯЕМ МАШИНЫ ИЗ ГАРАЖА…');
    await ModelLibrary.load(allRenderModels().map((m) => m.key), (p) => {
      const el = document.querySelector('.loading small');
      if (el) el.textContent = `ЗАГРУЗКА МОДЕЛЕЙ ${Math.round(p * 100)}%`;
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
    if (params.has('free')) this.startFree();
    else if (params.has('level')) this.startParking(+params.get('level'));
    else if (params.has('exam')) this.startExam();
    else this.menus.show('main');
    if (params.has('t')) this.game.dayNight.setTime(parseFloat(params.get('t')));
  }

  frame(now) {
    const dt = Math.max(0, Math.min((now - this.last) / 1000, 0.05));
    this.last = now;
    if (this.game.contextLost) return;
    if (this.state === 'play' && !this.paused) {
      this.game.tick(dt);
      this.game.render(dt);
    } else if (this.state === 'menu') {
      this.garage.active = true;
      this.garage.render(dt);
    } else {
      this.game.render(0); // пауза/результат — последний кадр мира под меню
    }
  }

  onMenu(name) {
    if (['main', 'garage', 'levels', 'help'].includes(name) || (name === 'settings' && !this.paused)) {
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

  startParking(i) { this.lastStart = () => this.startParking(i); this.game.setTimePreset(i % 5 === 4 ? 1 : i % 7 === 6 ? 3 : 0); this._enterPlay(new ParkingMode(this.game, i)); }
  startFree() { this.lastStart = () => this.startFree(); this._enterPlay(new FreeRideMode(this.game)); }
  startExam() { this.lastStart = () => this.startExam(); this.game.setTimePreset(0); this._enterPlay(new ExamMode(this.game)); }
  restart() { this.lastStart?.(); }

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
