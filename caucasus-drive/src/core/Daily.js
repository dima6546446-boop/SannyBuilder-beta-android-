import { mulberry32 } from '../utils/math.js';

/**
 * Задания дня: каждый календарный день — 3 случайных задания из пула (одинаковые весь день),
 * прогресс копится в сохранении, награду забирают в меню «Задания».
 * kind — тип события, которое игра сообщает через progress(kind, value).
 * max: true — засчитывается лучший результат (скорость, очки комбо), иначе — сумма.
 */
const POOL = [
  { kind: 'km', goals: [3, 5, 8], text: (n) => `Проехать ${n} км`, reward: (n) => n * 450 },
  { kind: 'taxi', goals: [1, 2, 3], text: (n) => `Выполнить ${n} ${n === 1 ? 'заказ' : 'заказа'} такси`, reward: (n) => n * 1300 },
  { kind: 'drift', goals: [400, 900, 1600], max: true, text: (n) => `Набрать ${n} очков дрифта или «шашек» за одно комбо`, reward: (n) => 800 + n * 2 },
  { kind: 'food', goals: [1, 2], text: (n) => (n === 1 ? 'Перекусить в ларьке или магазине' : `Купить еду ${n} раза`), reward: (n) => 600 * n },
  { kind: 'park', goals: [1, 2, 3], text: (n) => `Пройти ${n} ${n === 1 ? 'уровень' : 'уровня'} парковки`, reward: (n) => n * 1100 },
  { kind: 'whistle', goals: [2, 3], text: (n) => `Свистнуть прохожим ${n} раза`, reward: () => 900 },
  { kind: 'fuel', goals: [1], text: () => 'Заправиться на АЗС', reward: () => 700 },
  { kind: 'speed', goals: [110, 130, 150], max: true, text: (n) => `Разогнаться до ${n} км/ч`, reward: (n) => n * 12 },
  { kind: 'walk', goals: [200, 400], text: (n) => `Пройти пешком ${n} м`, reward: (n) => n * 3 },
];

const today = () => { const d = new Date(); return `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`; };

export class Daily {
  constructor(save, onDone) {
    this.save = save;
    this.onDone = onDone;
    this.refresh();
  }

  /** Новый день — новые задания. */
  refresh() {
    const d = this.save.data;
    const date = today();
    if (d.daily?.date === date && d.daily.tasks?.length) return;
    let seed = 0;
    for (const ch of date) seed = (seed * 31 + ch.charCodeAt(0)) | 0;
    const r = mulberry32(seed);
    const pool = POOL.slice();
    const tasks = [];
    while (tasks.length < 3 && pool.length) {
      const t = pool.splice((r() * pool.length) | 0, 1)[0];
      const goal = t.goals[(r() * t.goals.length) | 0];
      tasks.push({ kind: t.kind, goal, progress: 0, done: false, claimed: false });
    }
    d.daily = { date, tasks };
    this.save.commit();
  }

  get tasks() { return this.save.data.daily.tasks; }

  describe(t) {
    const P = POOL.find((p) => p.kind === t.kind);
    return { text: P.text(t.goal), reward: Math.round(P.reward(t.goal) / 50) * 50 };
  }

  progress(kind, value = 1) {
    this.refresh();
    let changed = false, finished = false;
    for (const t of this.tasks) {
      if (t.kind !== kind || t.done) continue;
      const P = POOL.find((p) => p.kind === kind);
      const v = P.max ? Math.max(t.progress, value) : t.progress + value;
      if (v === t.progress) continue;
      t.progress = Math.min(t.goal, v);
      changed = true;
      if (t.progress >= t.goal) { t.done = true; finished = true; this.onDone?.(t, this.describe(t)); }
    }
    // километры/метры копятся каждый кадр — сохраняем не чаще раза в 3 с
    if (changed) {
      const now = performance.now();
      if (finished || (kind !== 'km' && kind !== 'walk') || now - (this._saved || 0) > 3000) {
        this._saved = now;
        this.save.commit();
      }
    }
  }

  claim(i) {
    const t = this.tasks[i];
    if (!t || !t.done || t.claimed) return 0;
    t.claimed = true;
    const { reward } = this.describe(t);
    this.save.addMoney(reward);
    return reward;
  }

  get readyCount() { return this.tasks.filter((t) => t.done && !t.claimed).length; }
  get doneCount() { return this.tasks.filter((t) => t.done).length; }
}
