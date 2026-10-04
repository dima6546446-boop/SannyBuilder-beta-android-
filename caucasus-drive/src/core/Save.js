import { CAR_BY_ID, TUNING } from '../config/cars.js';

const KEY = 'caucasusdrive.save.v1';
const LEGACY_KEYS = ['ladaparking.save.v1']; // прогресс версий «Лада Паркинг» подхватывается

const DEFAULT = () => ({
  money: 15000,
  owned: ['vaz2107'],
  current: 'vaz2107',
  tuning: {},          // id → {color, wheels, height, tint, engine, tires}
  stars: {},           // levelIndex → 1..3
  license: false,
  settings: { controls: 'wheel', gearbox: 'auto', quality: null, volume: 0.8, cameraMode: 0, assist: true, fines: true },
  stats: { km: 0, fines: 0, taxi: 0, bestCombo: 0, crashes: 0 },
  drift: { best: 0, bestSeries: 0, runs: 0 }, // дрифт-зона: рекорд заезда, лучшая серия (везде), заездов
});

/**
 * Прогресс игрока в localStorage (деньги, гараж, тюнинг, звёзды уровней, права).
 * Все обращения обёрнуты в try/catch — в приватном режиме WebView хранилище может быть недоступно.
 */
export class Save {
  constructor() {
    this.data = DEFAULT();
    try {
      const raw = localStorage.getItem(KEY) ?? LEGACY_KEYS.map((k) => localStorage.getItem(k)).find(Boolean);
      if (raw) this.data = { ...DEFAULT(), ...JSON.parse(raw) };
      this.data.settings = { ...DEFAULT().settings, ...this.data.settings };
      this.data.stats = { ...DEFAULT().stats, ...this.data.stats };
      this.data.drift = { ...DEFAULT().drift, ...this.data.drift };
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
      neon: t.neon ?? 0,
      plate: t.plate ?? 0,
      horn: t.horn ?? 0,
      exhaust: t.exhaust ?? 0,
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
      neon: TUNING.neon.find((x) => x.id === t.neon)?.value ?? null,
      plate: (([text, region]) => ({ text, region }))(TUNING.plate.find((x) => x.id === t.plate)?.value ?? TUNING.plate[0].value),
      horn: TUNING.horn.find((x) => x.id === t.horn)?.value ?? 0,
      exhaust: TUNING.exhaust.find((x) => x.id === t.exhaust)?.value ?? 0,
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
