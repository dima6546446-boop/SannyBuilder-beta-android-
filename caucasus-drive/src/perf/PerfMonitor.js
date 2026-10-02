import { pixelRatioFor } from '../config/quality.js';

/**
 * Адаптивный регулятор качества. Держит плавные FPS на любом телефоне, даже на «Высоком».
 *
 * Лестница деградации (одна общая): сначала дорогие эффекты, разрешение — в последнюю очередь:
 *   ступени 1..featureLevels — game.setDegrade(k): bloom → тени реже/без деревьев и трафика →
 *     меньше трафика, LOD ближе, без облаков → карта теней меньше, ещё реже;
 *   дальше — снижение разрешения шагами ×0.88 до minRenderHeight (не ниже 1 CSS-пикселя).
 * Восстановление идёт в обратном порядке: сначала разрешение, потом эффекты.
 *
 * Решения раз в 1.5 с:
 *  - FPS < 45 → шаг вниз;
 *  - FPS ≥ 56 три окна подряд → шаг вверх; если шаг вверх снова уронил FPS — откат,
 *    и следующая попытка вверх только после паузы (10 → 20 → 40 … с);
 *  - если все ступени эффектов и первый шаг разрешения не дали прироста (FPS упёрся
 *    в vsync 30 Гц, энергосбережение или CPU), они откатываются: портить картинку без
 *    пользы нельзя. Повторная попытка — через 20 → 60 → 180 → 300 с.
 */
export class PerfMonitor {
  constructor(renderer, quality, game) {
    this.renderer = renderer;
    this.q = quality;
    this.game = game;
    this.featureLevels = game?.degradeLevels ?? 0;
    this.enabled = quality.governor !== false;
    this.frames = 0;
    this.acc = 0;
    this.fps = 60;
    this.step = 0;          // текущая ступень лестницы
    this.goodWindows = 0;
    this.cooldown = 0;      // пауза перед следующей попыткой вверх
    this.backoff = 10;
    this.justUpgraded = false;
    this.history = [];      // [{step, fps}] — последние шаги вниз
    this.blockDownUntil = 0;
    this.blockFor = 20;
    this.time = 0;
    this.warmup = 3;        // первые секунды — компиляция шейдеров и загрузка, не судим
    this.apply();
  }

  /** Новый режим/уровень: не реагировать на рывки загрузки. */
  settle() {
    this.warmup = this.time + 2.5;
    this.frames = 0;
    this.acc = 0;
  }

  get maxRatio() { return pixelRatioFor(this.q.renderHeight); }
  get minRatio() { return pixelRatioFor(this.q.minRenderHeight); }

  /** Сколько ступеней разрешения доступно на этом экране. */
  get resSteps() {
    let n = 0;
    for (let r = this.maxRatio; r * 0.88 >= this.minRatio - 1e-3; r *= 0.88) n++;
    return n;
  }

  get maxStep() { return this.featureLevels + this.resSteps; }

  /** Применить текущую ступень: эффекты + разрешение. Вызывается и при resize. */
  apply() {
    const f = Math.min(this.step, this.featureLevels);
    const r = Math.max(0, this.step - this.featureLevels);
    this.game?.setDegrade?.(f);
    const ratio = Math.max(this.minRatio, this.maxRatio * Math.pow(0.88, r));
    if (Math.abs(this.renderer.getPixelRatio() - ratio) > 1e-3) {
      this.renderer.setPixelRatio(ratio);
      this.game?.postfx?.setSize(window.innerWidth, window.innerHeight);
    }
  }

  _set(step) {
    step = Math.max(0, Math.min(this.maxStep, step));
    if (step === this.step) return false;
    this.step = step;
    this.apply();
    return true;
  }

  update(dt) {
    this.frames++;
    this.acc += dt;
    this.time += dt;
    if (this.cooldown > 0) this.cooldown -= dt;
    if (this.acc < 1.5) return;
    this.fps = Math.round(this.frames / this.acc);
    this.frames = 0;
    this.acc = 0;
    if (!this.enabled || this.time < this.warmup) return;

    if (this.fps < 45) {
      this.goodWindows = 0;
      if (this.justUpgraded) {
        // шаг вверх не потянули — назад и подождать подольше
        this.justUpgraded = false;
        this._set(this.step + 1);
        this.cooldown = this.backoff;
        this.backoff = Math.min(160, this.backoff * 2);
        return;
      }
      if (this.time < this.blockDownUntil) return;
      const h = this.history, n = Math.min(this.featureLevels + 1, this.maxStep);
      if (n > 0 && h.length >= n && h[h.length - 1].step === this.step && this.fps - h[h.length - n].fps < 2) {
        // вся лестница эффектов и первый шаг разрешения ничего не дали — FPS упёрся
        // не в GPU (vsync 30 Гц, энергосбережение, CPU): возвращаем качество
        this._set(h[h.length - n].step - 1);
        this.history = [];
        this.blockDownUntil = this.time + this.blockFor;
        this.blockFor = Math.min(300, this.blockFor * 3);
        this.cooldown = this.blockFor;
        return;
      }
      if (this._set(this.step + 1)) {
        if (h.length && h[h.length - 1].step !== this.step - 1) h.length = 0;
        h.push({ step: this.step, fps: this.fps });
        if (h.length > 8) h.shift();
      }
      return;
    }

    this.justUpgraded = false;
    this.history = [];
    if (this.fps >= 56) this.goodWindows++;
    else this.goodWindows = 0;
    if (this.goodWindows >= 3 && this.cooldown <= 0 && this.step > 0) {
      this.goodWindows = 0;
      this.history = [];
      this._set(this.step - 1);
      this.justUpgraded = true;
    }
  }
}
