import { CAR_BY_ID, TUNING } from '../config/cars.js';

const KEY = 'ladaparking.save.v1';

const DEFAULT = () => ({
  money: 15000,
  owned: ['vaz2107'],
  current: 'vaz2107',
  tuning: {},          // id → {color, wheels, height, tint, engine, tires}
  stars: {},           // levelIndex → 1..3
  license: false,
  settings: { controls: 'wheel', gearbox: 'auto', quality: null, volume: 0.8, cameraMode: 0, assist: true },
  stats: { km: 0, fines: 0, taxi: 0, bestCombo: 0, crashes: 0 },
});

/**
 * Прогресс игрока в localStorage (деньги, гараж, тюнинг, звёзды уровней, права).
 * Все обращения обёрнуты в try/catch — в приватном режиме WebView хранилище может быть недоступно.
 */
export class Save {
  constructor() {
    this.data = DEFAULT();
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) this.data = { ...DEFAULT(), ...JSON.parse(raw) };
      this.data.settings = { ...DEFAULT().settings, ...this.data.settings };
      this.data.stats = { ...DEFAULT().stats, ...this.data.stats };
    } catch { /* пустой прогресс */ }
    if (!CAR_BY_ID[this.data.current]) this.data.current = 'vaz2107';
    this.listeners = [];
  }

  onChange(fn) { this.listeners.push(fn); }

  commit() {
    try { localStorage.setItem(KEY, JSON.stringify(this.data)); } catch { /* ignore */ }
    for (const fn of this.listeners) fn(this.data);
  }

  get money() { return this.data.money; }

  addMoney(v) { this.data.money = Math.max(0, Math.round(this.data.money + v)); this.commit(); }

  spend(v) {
    if (this.data.money < v) return false;
    this.data.money -= v;
    this.commit();
    return true;
  }

  tuningOf(id) {
    const def = CAR_BY_ID[id];
    const t = this.data.tuning[id] || {};
    return {
      color: t.color ?? def.colors[0],
      wheels: t.wheels ?? 'default',
      height: t.height ?? 0,
      tint: t.tint ?? 0,
      engine: t.engine ?? 0,
      tires: t.tires ?? 0,
    };
  }

  setTuning(id, patch) {
    this.data.tuning[id] = { ...this.tuningOf(id), ...patch };
    this.commit();
  }

  /** Числовые параметры тюнинга для физики/визуала. */
  tuningValues(id) {
    const t = this.tuningOf(id);
    return {
      color: t.color,
      wheels: t.wheels,
      height: TUNING.height.find((x) => x.id === t.height)?.value ?? 0,
      tint: TUNING.tint.find((x) => x.id === t.tint)?.value ?? 0.6,
      engine: TUNING.engine.find((x) => x.id === t.engine)?.value ?? 1,
      tires: TUNING.tires.find((x) => x.id === t.tires)?.value ?? 1,
    };
  }

  owns(id) { return this.data.owned.includes(id); }

  buy(id) {
    const def = CAR_BY_ID[id];
    if (this.owns(id) || !this.spend(def.price)) return false;
    this.data.owned.push(id);
    this.data.current = id;
    this.commit();
    return true;
  }

  select(id) { if (this.owns(id)) { this.data.current = id; this.commit(); } }

  setStars(level, stars) {
    if ((this.data.stars[level] || 0) < stars) { this.data.stars[level] = stars; this.commit(); }
  }

  get totalStars() { return Object.values(this.data.stars).reduce((a, b) => a + b, 0); }

  reset() { this.data = DEFAULT(); this.commit(); }
}
