// Прогресс игрока: валюта, опыт, уровни, покупки, продажа, тюнинг, сохранение.
import { CARS, TUNING_PARTS, WHEELS, BODYKITS, SPOILERS, NEONS, PAINTS, getCar, upgradePrice } from './cars.js';
import { MAP_LIST, getMap } from './maps.js';

export const SAVE_KEY = 'rcd-drift-save-v1';
export const MAX_LEVEL = 30;
export const xpForLevel = (lvl) => Math.round(260 + 140 * Math.pow(lvl, 1.3));   // опыт для перехода lvl -> lvl+1
export const SELL_RATIO = 0.6;
export const moneyForScore = (score) => Math.floor(score / 3.5);
export const xpForScore = (score) => Math.floor(score / 7);

export const DEFAULT_SETTINGS = {
  quality: 'high', shadows: true, pixelRatio: 1, fov: 70, effects: true, speedFx: true, showFps: false,
  master: 0.8, engine: 0.7, sfx: 0.8, music: 0.4,
  assist: 0.7, steerSens: 1, deadzone: 0.1, transmission: 'auto', camera: 'chase', units: 'kmh', weather: 'clear', touch: 'auto',
  hintsSeen: false,
};

function defaultSave() {
  return {
    version: 1, money: 3000, xp: 0, level: 1,
    cars: { kopeyka: { tuning: {}, paint: getCar('kopeyka').color, wheels: 'steel', bodykit: 'none', spoiler: 'none', neon: 'none' } },
    selected: 'kopeyka',
    settings: { ...DEFAULT_SETTINGS },
    records: {},            // `${map}:${mode}` -> лучшие очки
    challenges: {},         // id -> { done, stars }
    stats: { runs: 0, totalScore: 0, totalDriftTime: 0, bestChain: 0, bestAngle: 0, earned: 0 },
  };
}

function memoryStorage() {
  const m = new Map();
  return { getItem: (k) => (m.has(k) ? m.get(k) : null), setItem: (k, v) => m.set(k, String(v)), removeItem: (k) => m.delete(k) };
}

export class Progress {
  constructor(storage) {
    this.storage = storage || (typeof localStorage !== 'undefined' ? safeLocal() : memoryStorage());
    this.data = this.load();
    this.listeners = [];
  }

  load() {
    const base = defaultSave();
    try {
      const raw = this.storage.getItem(SAVE_KEY);
      if (!raw) return base;
      const d = JSON.parse(raw);
      const out = { ...base, ...d, settings: { ...base.settings, ...(d.settings || {}) }, stats: { ...base.stats, ...(d.stats || {}) } };
      if (!out.cars || !Object.keys(out.cars).length) out.cars = base.cars;
      if (!out.cars[out.selected]) out.selected = Object.keys(out.cars)[0];
      // санация
      for (const id of Object.keys(out.cars)) { if (!CARS.some((c) => c.id === id)) delete out.cars[id]; else out.cars[id] = { ...base.cars.kopeyka, ...out.cars[id], tuning: { ...(out.cars[id].tuning || {}) } }; }
      if (!Object.keys(out.cars).length) return base;
      out.money = Math.max(0, Math.floor(+out.money || 0));
      out.xp = Math.max(0, Math.floor(+out.xp || 0));
      out.level = this.levelFromXp(out.xp);
      return out;
    } catch (e) { return base; }
  }

  save() {
    try { this.storage.setItem(SAVE_KEY, JSON.stringify(this.data)); } catch (e) { /* хранилище недоступно — играем без сохранения */ }
    for (const f of this.listeners) f(this.data);
  }
  onChange(f) { this.listeners.push(f); }
  reset() { this.data = defaultSave(); this.save(); }

  levelFromXp(xp) { let l = 1; while (l < MAX_LEVEL && xp >= xpForLevel(l)) { xp -= xpForLevel(l); l++; } return l; }
  get level() { return this.data.level; }
  get xpInLevel() { let xp = this.data.xp, l = 1; while (l < MAX_LEVEL && xp >= xpForLevel(l)) { xp -= xpForLevel(l); l++; } return { xp, need: l >= MAX_LEVEL ? 0 : xpForLevel(l) }; }
  get money() { return this.data.money; }
  get settings() { return this.data.settings; }

  addXp(n) {
    const before = this.data.level;
    this.data.xp += Math.max(0, Math.floor(n));
    this.data.level = this.levelFromXp(this.data.xp);
    return this.data.level - before;
  }
  addMoney(n) { this.data.money += Math.floor(n); }

  // ---- машины ----
  owns(id) { return !!this.data.cars[id]; }
  carState(id) { return this.data.cars[id]; }
  carUnlocked(id) { return getCar(id).level <= this.data.level; }
  canBuy(id) { const c = getCar(id); return !this.owns(id) && this.carUnlocked(id) && this.data.money >= c.price; }
  buy(id) {
    const c = getCar(id);
    if (this.owns(id)) return { ok: false, reason: 'Уже куплена' };
    if (!this.carUnlocked(id)) return { ok: false, reason: `Нужен уровень ${c.level}` };
    if (this.data.money < c.price) return { ok: false, reason: 'Не хватает денег' };
    this.data.money -= c.price;
    this.data.cars[id] = { tuning: {}, paint: c.color, wheels: 'steel', bodykit: 'none', spoiler: 'none', neon: 'none' };
    this.data.selected = id;
    this.save();
    return { ok: true };
  }
  investedIn(id) {
    const st = this.data.cars[id]; if (!st) return 0;
    let sum = 0;
    for (const [part, lvl] of Object.entries(st.tuning)) for (let l = 1; l <= lvl; l++) sum += upgradePrice(part, l);
    sum += (WHEELS.find((w) => w.id === st.wheels) || { price: 0 }).price;
    sum += (BODYKITS.find((w) => w.id === st.bodykit) || { price: 0 }).price;
    sum += (SPOILERS.find((w) => w.id === st.spoiler) || { price: 0 }).price;
    sum += (NEONS.find((w) => w.id === st.neon) || { price: 0 }).price;
    return sum;
  }
  sellValue(id) { return Math.floor((getCar(id).price + this.investedIn(id)) * SELL_RATIO); }
  sell(id) {
    if (!this.owns(id)) return { ok: false, reason: 'Нет такой машины' };
    if (Object.keys(this.data.cars).length <= 1) return { ok: false, reason: 'Нельзя продать последнюю машину' };
    const v = this.sellValue(id);
    this.data.money += v;
    delete this.data.cars[id];
    if (this.data.selected === id) this.data.selected = Object.keys(this.data.cars)[0];
    this.save();
    return { ok: true, value: v };
  }
  select(id) { if (!this.owns(id)) return false; this.data.selected = id; this.save(); return true; }
  get selected() { return this.data.selected; }

  // ---- тюнинг ----
  nextUpgrade(id, part) {
    const st = this.data.cars[id]; const p = TUNING_PARTS.find((x) => x.id === part);
    const lvl = st.tuning[part] || 0;
    if (lvl >= p.max) return null;
    return { level: lvl + 1, price: upgradePrice(part, lvl + 1) };
  }
  upgrade(id, part) {
    const n = this.nextUpgrade(id, part);
    if (!n) return { ok: false, reason: 'Максимум' };
    if (this.data.money < n.price) return { ok: false, reason: 'Не хватает денег' };
    this.data.money -= n.price;
    this.data.cars[id].tuning[part] = n.level;
    this.save();
    return { ok: true };
  }
  /** Откат детали на уровень ниже с возвратом части стоимости. */
  downgrade(id, part) {
    const st = this.data.cars[id]; const lvl = st.tuning[part] || 0;
    if (lvl <= 0) return { ok: false };
    const back = Math.floor(upgradePrice(part, lvl) * 0.5);
    this.data.money += back; st.tuning[part] = lvl - 1;
    if (st.tuning[part] === 0) delete st.tuning[part];
    this.save();
    return { ok: true, value: back };
  }

  // ---- внешний вид ----
  setPaint(id, color) { this.data.cars[id].paint = color; this.save(); }
  /** Элементы с ценой: колёса, обвес, спойлер, неон. Покупка при первой установке — упрощённо: платим при смене на более дорогой. */
  setPart(id, slot, partId) {
    const lists = { wheels: WHEELS, bodykit: BODYKITS, spoiler: SPOILERS, neon: NEONS };
    const list = lists[slot]; const it = list.find((x) => x.id === partId);
    if (!it) return { ok: false, reason: 'Нет такого элемента' };
    const st = this.data.cars[id];
    st.owned = st.owned || {};
    const have = st.owned[slot] || (st.owned[slot] = [list[0].id]);
    if (!have.includes(partId)) {
      if (this.data.money < it.price) return { ok: false, reason: 'Не хватает денег' };
      this.data.money -= it.price; have.push(partId);
    }
    st[slot] = partId;
    this.save();
    return { ok: true };
  }
  partOwned(id, slot, partId) {
    const lists = { wheels: WHEELS, bodykit: BODYKITS, spoiler: SPOILERS, neon: NEONS };
    const st = this.data.cars[id]; const o = (st.owned && st.owned[slot]) || [lists[slot][0].id];
    return o.includes(partId);
  }

  // ---- карты и испытания ----
  mapUnlocked(id) { return getMap(id).level <= this.data.level; }
  recordKey(mapId, mode) { return `${mapId}:${mode}`; }
  record(mapId, mode) { return this.data.records[this.recordKey(mapId, mode)] || 0; }

  /** Завершить заезд: начислить деньги/опыт, обновить статистику и рекорды. */
  finishRun(summary) {
    const d = this.data;
    const score = Math.round(summary.score);
    let money = moneyForScore(score), xp = xpForScore(score);
    let challengeReward = null;
    const stars = summary.stars || 0;
    if (summary.mode === 'timed') { money += stars * 400; xp += stars * 60; }
    if (summary.mode === 'challenge' && summary.success) {
      const prev = d.challenges[summary.challengeId];
      const k = prev && prev.done ? 0.25 : 1;
      const rw = summary.reward || { money: 0, xp: 0 };
      challengeReward = { money: Math.floor(rw.money * k), xp: Math.floor(rw.xp * k), first: !(prev && prev.done) };
      money += challengeReward.money; xp += challengeReward.xp;
      d.challenges[summary.challengeId] = { done: true, runs: ((prev && prev.runs) || 0) + 1 };
    }
    d.money += money;
    const levelUps = this.addXp(xp);
    const key = this.recordKey(summary.mapId, summary.mode === 'challenge' ? summary.challengeId : summary.mode);
    const newRecord = score > (d.records[key] || 0);
    if (newRecord) d.records[key] = score;
    const st = d.stats;
    st.runs++; st.totalScore += score; st.totalDriftTime += summary.driftTime || 0; st.earned += money;
    st.bestChain = Math.max(st.bestChain, Math.round(summary.bestChain || 0)); st.bestAngle = Math.max(st.bestAngle, Math.round(summary.bestAngle || 0));
    this.save();
    return { money, xp, levelUps, newRecord, challengeReward, level: d.level };
  }

  updateSettings(patch) { Object.assign(this.data.settings, patch); this.save(); }
  unlocksAtLevel(lvl) {
    return {
      cars: CARS.filter((c) => c.level === lvl).map((c) => c.brand + ' ' + c.name),
      maps: MAP_LIST.map((m) => getMap(m.id)).filter((m) => m.level === lvl).map((m) => m.name),
    };
  }
}

function safeLocal() {
  try { const k = '__t'; localStorage.setItem(k, '1'); localStorage.removeItem(k); return localStorage; } catch (e) { return memoryStorage(); }
}
export { memoryStorage };
