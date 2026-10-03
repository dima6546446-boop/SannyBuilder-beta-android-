import { pixelRatioFor } from '../config/quality.js';

/**
 * Адаптивный регулятор качества: держит плавные FPS на любом телефоне, даже на «Высоком».
 *
 * На мобильных GPU главный расход — количество пикселей, поэтому снижение разрешения идёт
 * вперемешку с отключением эффектов, а не в самом конце:
 *   1 — без bloom и облаков, тени через кадр
 *   2 — разрешение ×0.85
 *   3 — тени только от зданий, карта теней ≤1024, меньше прохожих и машин во дворах
 *   4 — разрешение ×0.85
 *   5 — тени выключены совсем, меньше трафика, LOD ближе
 *   6… — разрешение ×0.85 за шаг, до минимума пресета
 * Восстановление — в обратном порядке.
 *
 * Решения раз в секунду по реальному времени кадров:
 *  - FPS < 26 → сразу две ступени вниз, FPS < 45 → одна;
 *  - ровные кадры по 33 мс бывают и у упёршегося в GPU телефона, и у экрана, ограниченного
 *    30 Гц: снижаем медленнее, и если три ступени подряд FPS не сдвинули — это ограничение
 *    экрана, возвращаем качество и больше в этой зоне его не трогаем;
 *  - FPS ≥ 56 три окна подряд → ступень вверх; если не потянули — откат и пауза 10 → 20 → 40 … с.
 */
const FX_STEPS = [0, 1, 1, 2, 2, 3];      // ступень → уровень эффектов game.setDegrade
const RES_STEPS = [0, 0, 1, 1, 2, 2];     // ступень → сколько раз ×0.85 по разрешению

export class PerfMonitor {
  constructor(renderer, quality, game) {
    this.renderer = renderer;
    this.q = quality;
    this.game = game;
    this.enabled = quality.governor !== false;
    this.fps = 60;
    this.step = 0;
    this.goodWindows = 0;
    this.cooldown = 0;
    this.backoff = 10;
    this.justUpgraded = false;
    this.time = 0;
    this.warmup = 2.5;
    this._last = performance.now();
    this._intervals = [];
    this.capWin = 0; this.capSteps = 0; this.capBlock = false;
    this.apply();
  }

  get maxRatio() { return pixelRatioFor(this.q.renderHeight); }
  get minRatio() { return pixelRatioFor(this.q.minRenderHeight); }

  _fx(step) { return FX_STEPS[Math.min(step, FX_STEPS.length - 1)]; }
  _res(step) { return step < RES_STEPS.length ? RES_STEPS[step] : RES_STEPS[RES_STEPS.length - 1] + (step - RES_STEPS.length + 1); }

  get maxStep() {
    let s = FX_STEPS.length - 1;
    while (this.maxRatio * Math.pow(0.85, this._res(s + 1)) >= this.minRatio - 1e-3) s++;
    return s;
  }

  apply() {
    this.game?.setDegrade?.(this._fx(this.step));
    const ratio = Math.max(this.minRatio, this.maxRatio * Math.pow(0.85, this._res(this.step)));
    if (Math.abs(this.renderer.getPixelRatio() - ratio) > 1e-3) {
      this.renderer.setPixelRatio(ratio);
      this.game?.postfx?.setSize(window.innerWidth, window.innerHeight);
    }
  }

  _set(step) {
    step = Math.max(0, Math.min(this.maxStep, step));
    if (step === this.step) return false;
    // выключение/включение теней перекомпилирует шейдеры — не прыгаем туда-сюда
    if ((this._fx(step) >= 3) !== (this._fx(this.step) >= 3)) this.cooldown = Math.max(this.cooldown, 25);
    this.step = step;
    this.apply();
    return true;
  }

  /** Новый режим/уровень: не реагировать на рывки загрузки. */
  settle() {
    this.warmup = this.time + 2;
    this._intervals.length = 0;
    this._last = performance.now();
  }

  update(dt) {
    const now = performance.now();
    const iv = Math.min(250, now - this._last);
    this._last = now;
    this._intervals.push(iv);
    this.time += dt;
    if (this.cooldown > 0) this.cooldown -= dt;
    let sum = 0;
    for (const x of this._intervals) sum += x;
    if (sum < 1000) return;
    const n = this._intervals.length, mean = sum / n;
    let v = 0;
    for (const x of this._intervals) v += (x - mean) * (x - mean);
    const std = Math.sqrt(v / n);
    this._intervals.length = 0;
    this.fps = Math.round(1000 / mean);
    if (!this.enabled || this.time < this.warmup) return;

    const capped30 = Math.abs(mean - 33.3) < 2.5 && std < 3;
    if (!capped30) { this.capWin = 0; this.capSteps = 0; this.capBlock = false; }
    else if (!this.justUpgraded) {
      if (this.capBlock) return;
      if (++this.capWin % 2) return;                 // в зоне 30 — шаг раз в 2 с
      if (this.capSteps >= 3) {                      // три ступени без толку → лимит экрана
        this._set(this.step - this.capSteps);
        this.capSteps = 0; this.capBlock = true;
        return;
      }
      if (this._set(this.step + 1)) this.capSteps++;
      else this.capBlock = true;
      return;
    }

    if (this.fps < 45) {
      this.goodWindows = 0;
      if (this.justUpgraded) {
        this.justUpgraded = false;
        this._set(this.step + 1);
        this.cooldown = Math.max(this.cooldown, this.backoff);
        this.backoff = Math.min(160, this.backoff * 2);
        return;
      }
      this._set(this.step + (this.fps < 26 ? 2 : 1));
      return;
    }

    this.justUpgraded = false;
    if (this.fps >= 56) this.goodWindows++;
    else this.goodWindows = 0;
    if (this.goodWindows >= 3 && this.cooldown <= 0 && this.step > 0) {
      this.goodWindows = 0;
      this._set(this.step - 1);
      this.justUpgraded = true;
    }
  }
}
