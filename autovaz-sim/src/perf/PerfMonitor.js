/**
 * Динамическое разрешение: держим целевые ~30–60 FPS, меняя pixelRatio рендера.
 * На мобильных узкое место — fill rate, поэтому это самый эффективный рычаг.
 * Решение принимается раз в 2 с с гистерезисом, чтобы не «пилить» разрешение.
 */
export class PerfMonitor {
  constructor(renderer, quality) {
    this.renderer = renderer;
    this.q = quality;
    this.scale = quality.pixelRatio;
    this.frames = 0;
    this.acc = 0;
    this.fps = 60;
    this.enabled = true;
    this.apply();
  }

  apply() {
    const dpr = window.devicePixelRatio || 1;
    this.renderer.setPixelRatio(Math.min(this.scale, dpr));
  }

  update(dt) {
    this.frames++;
    this.acc += dt;
    if (this.acc < 2) return;
    this.fps = Math.round(this.frames / this.acc);
    this.frames = 0;
    this.acc = 0;
    if (!this.enabled) return;
    const old = this.scale;
    if (this.fps < 28) this.scale = Math.max(this.q.minPixelRatio, this.scale - 0.1);
    else if (this.fps > 56) this.scale = Math.min(this.q.maxPixelRatio, this.scale + 0.05);
    if (old !== this.scale) this.apply();
  }
}
