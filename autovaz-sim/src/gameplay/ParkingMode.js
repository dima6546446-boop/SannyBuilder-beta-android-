import { LevelScene } from './LevelScene.js';
import { LEVELS } from '../config/levels.js';

const FAIL_TAGS = { cone: 'Задели конус', obstacle: 'Задели машину', wall: 'Задели стену гаража', parked: 'Задели машину', building: 'Врезались в здание', fence: 'Врезались в забор', pole: 'Задели столб' };

/**
 * Карьера «Парковка» (как в Car Parking): 30 уровней на автодроме ДОСААФ.
 * Любое касание — провал. Звёзды — за время относительно норматива.
 */
export class ParkingMode {
  constructor(game, levelIndex) {
    this.g = game;
    this.index = levelIndex;
    this.L = LEVELS[levelIndex];
    this.name = 'parking';
  }

  enter() {
    const g = this.g;
    g.traffic.enabled = false;
    g.traffic.clear();
    this.scene = new LevelScene(g);
    this.scene.load(this.L, g.player.def);
    g.player.place(...this.scene.startPose());
    g.player.physics.fuel = g.player.physics.spec.tank;
    g.cameraRig.snap();
    this.time = 0;
    this.state = 'play';
    g.hud.mission(`${this.L.name} · ${this.L.type}`, this.L.desc);
    g.hud.toast(this.L.desc, 'good', 3);
    g.hud.showSensors(true);
    g.hud.showLimit(false);
  }

  exit() {
    this.scene.unload();
    this.g.hud.hideMission();
    this.g.hud.showSensors(false);
  }

  get extraColliders() { return [this.scene.col]; }

  onContact(obj) {
    if (this.state !== 'play') return;
    const why = FAIL_TAGS[obj.tag];
    if (!why) return;
    if (obj.tag === 'cone') this.g.cones.knock(obj.idx, 0, 1);
    this.fail(why);
  }

  fail(why) {
    this.state = 'fail';
    const g = this.g;
    g.audio.fail();
    g.input.enabled = false;
    setTimeout(() => g.menus.showResult({ ok: false, why, level: this.index }), 900);
  }

  update(dt) {
    if (this.state !== 'play') return;
    this.time += dt;
    const { state, done } = this.scene.update(dt, this.g.player);
    const t = this.time, par = this.L.par;
    const mm = Math.floor(t / 60), ss = Math.floor(t % 60).toString().padStart(2, '0');
    const hint = state === 'wrong' ? 'Не той стороной! Нужно по стрелке' : state === 'inside' ? 'Стоп! Держите машину в зоне…' : this.L.desc;
    this.g.hud.mission(`${this.L.name} · ${this.L.type}`, `${hint}  ·  ${mm}:${ss} / норматив ${par} с`, this.scene.hold / 1.2);
    this.g.hud.nav(this.scene.target);
    if (done) this.success();
  }

  success() {
    this.state = 'done';
    const g = this.g;
    const stars = this.time <= this.L.par ? 3 : this.time <= this.L.par * 1.6 ? 2 : 1;
    const first = !g.save.data.stars[this.index];
    const reward = Math.round(this.L.reward * (stars / 3) * (first ? 2 : 1));
    g.save.setStars(this.index, stars);
    g.save.addMoney(reward);
    g.audio.success();
    g.input.enabled = false;
    setTimeout(() => g.menus.showResult({ ok: true, stars, reward, time: this.time, level: this.index, first }), 700);
  }

  renderExtra(inst, glow, blink) { this.scene.render(inst, glow, blink); }
}
