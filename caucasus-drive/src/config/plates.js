import { TUNING } from './cars.js';

/**
 * Номера по ГОСТ: «Б ЦЦЦ ББ РЕГ» — буква, три цифры, две буквы, регион из 2–3 цифр.
 * Буквы — только те 12, что пишутся одинаково кириллицей и латиницей.
 * Цена: обычный номер — PLATE_BASE, «красивый» (одинаковые цифры/буквы, 00X…) — дороже.
 */
export const PLATE_LETTERS = 'АВЕКМНОРСТУХ';
export const PLATE_BASE = 2000;
export const PLATE_MAX = 50000;
const DIGITS = '0123456789';

/** Готовые варианты (быстрый выбор): заводской, кавказские регионы, «блатные». */
export const PLATE_PRESETS = TUNING.plate.map((o) => ({ name: o.name.split(' · ')[0], text: o.value[0], region: o.value[1] }));
export const PLATE_DEFAULT = { text: PLATE_PRESETS[0].text, region: PLATE_PRESETS[0].region };

export function plateValid(p) {
  if (!p || typeof p.text !== 'string' || typeof p.region !== 'string') return false;
  const t = p.text, r = p.region;
  if (t.length !== 6 || !PLATE_LETTERS.includes(t[0]) || !PLATE_LETTERS.includes(t[4]) || !PLATE_LETTERS.includes(t[5])) return false;
  if (!/^\d{3}$/.test(t.slice(1, 4)) || t.slice(1, 4) === '000') return false; // 000 не выдают
  return /^([1-9]\d{2}|\d{2})$/.test(r) && r !== '00';
}

/** Номер из сохранения: {text, region}, старый формат (id из TUNING.plate) или мусор → валидный номер. */
export function normalizePlate(v) {
  if (typeof v === 'number') {
    const o = TUNING.plate.find((x) => x.id === v);
    return o ? { text: o.value[0], region: o.value[1] } : { ...PLATE_DEFAULT };
  }
  return plateValid(v) ? { text: v.text, region: v.region } : { ...PLATE_DEFAULT };
}

export const plateEq = (a, b) => a.text === b.text && a.region === b.region;

/** Цена номера и почему он «красивый». */
export function platePrice(p) {
  const d = p.text.slice(1, 4), L = p.text[0] + p.text.slice(4, 6);
  const tags = [];
  let extra = 0;
  if (d[0] === d[1] && d[1] === d[2]) { extra += 25000; tags.push(`цифры ${d}`); }
  else if (d[0] === '0' && d[1] === '0') { extra += 20000; tags.push(`номер ${d}`); }
  else if (d[1] === '0' && d[2] === '0') { extra += 8000; tags.push(`круглый ${d}`); }
  else if (d[0] === d[2]) { extra += 3000; tags.push(`зеркальный ${d}`); }
  if (L[0] === L[1] && L[1] === L[2]) { extra += 15000; tags.push(`буквы ${L[0]}${L[0]}${L[0]}`); }
  else if (L[1] === L[2]) { extra += 2000; tags.push(`буквы ${L[1]}${L[2]}`); }
  return { price: Math.min(PLATE_MAX, PLATE_BASE + extra), tags, pretty: extra >= 8000 };
}

/**
 * Позиции редактора: 0 — буква, 1–3 — цифры, 4–5 — буквы, 6–8 — регион
 * (6 — сотни региона, '' для двузначного). Номер ↔ массив из 9 символов.
 */
export const PLATE_SLOTS = [PLATE_LETTERS, DIGITS, DIGITS, DIGITS, PLATE_LETTERS, PLATE_LETTERS, ['', ...'123456789'], DIGITS, DIGITS];

export function plateToSlots(p) {
  const r = p.region.length === 3 ? p.region : ` ${p.region}`;
  return [...p.text, r[0] === ' ' ? '' : r[0], r[1], r[2]];
}

export function slotsToPlate(s) { return { text: s.slice(0, 6).join(''), region: s.slice(6).join('') }; }

/** Шаг позиции k на d (±1) по кругу; пропускает невалидные сочетания (000, регион 00). */
export function plateStep(slots, k, d) {
  const set = PLATE_SLOTS[k], n = set.length;
  const s = slots.slice();
  let i = Math.max(0, [...set].indexOf(s[k]));
  for (let tries = 0; tries < n; tries++) {
    i = (i + d + n) % n;
    s[k] = set[i];
    if (plateValid(slotsToPlate(s))) return s;
  }
  return slots;
}
