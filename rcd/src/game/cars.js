// Каталог машин (вымышленные марки и модели; названия не принадлежат реальным производителям).
const D2R = Math.PI / 180;

export const CARS = [
  {
    id: 'kopeyka', brand: 'Волжанин', name: 'Копейка', body: 'classic', price: 0, level: 1,
    desc: 'Лёгкая классика. Честный задний привод, предсказуемый занос — идеальная первая машина.',
    mass: 1030, wheelbase: 2.42, weightFront: 0.54, trackFront: 1.36, trackRear: 1.34, cgHeight: 0.50,
    length: 4.12, width: 1.61, height: 1.40, wheelRadius: 0.30,
    peakTorque: 170, peakRpm: 4300, redline: 6600, idleRpm: 900, turbo: 0,
    gears: [3.75, 2.30, 1.52, 1.10, 0.86], reverseRatio: 3.5, finalDrive: 4.1,
    maxSteerDeg: 38, brakeTorque: 2300, tireGrip: 1.04, rearGripBias: 1.0, rollFront: 0.56, diffLock: 0.55, cdA: 0.72,
    color: '#b3262b',
  },
  {
    id: 'pyaterka', brand: 'Волжанин', name: 'Пятёрка', body: 'sedan', price: 14000, level: 2,
    desc: 'Сбалансированный седан с отлаженной подвеской. Угол держит спокойно.',
    mass: 1065, wheelbase: 2.42, weightFront: 0.53, trackFront: 1.37, trackRear: 1.35, cgHeight: 0.50,
    length: 4.22, width: 1.62, height: 1.41, wheelRadius: 0.30,
    peakTorque: 190, peakRpm: 4500, redline: 6900, idleRpm: 900, turbo: 0,
    gears: [3.65, 2.20, 1.50, 1.10, 0.85], reverseRatio: 3.5, finalDrive: 4.1,
    maxSteerDeg: 38, brakeTorque: 2400, tireGrip: 1.06, rearGripBias: 1.0, rollFront: 0.56, diffLock: 0.58, cdA: 0.72,
    color: '#e5e5e0',
  },
  {
    id: 'devyatka', brand: 'Волжанин', name: 'Девятка-Д', body: 'hatch', price: 22000, level: 3,
    desc: 'Хэтчбек с задним приводом. Лёгкий нос и резкая реакция на руль.',
    mass: 985, wheelbase: 2.46, weightFront: 0.52, trackFront: 1.40, trackRear: 1.38, cgHeight: 0.49,
    length: 4.01, width: 1.65, height: 1.40, wheelRadius: 0.30,
    peakTorque: 205, peakRpm: 4800, redline: 7200, idleRpm: 900, turbo: 0,
    gears: [3.60, 2.15, 1.50, 1.12, 0.88], reverseRatio: 3.5, finalDrive: 4.2,
    maxSteerDeg: 40, brakeTorque: 2300, tireGrip: 1.08, rearGripBias: 1.0, rollFront: 0.55, diffLock: 0.60, cdA: 0.70,
    color: '#2d5fb0',
  },
  {
    id: 'kupe86', brand: 'Ураган', name: 'Купе-86', body: 'coupe', price: 38000, level: 5,
    desc: 'Низкое купе: точный руль, отзывчивый мотор, много угла.',
    mass: 1130, wheelbase: 2.52, weightFront: 0.51, trackFront: 1.46, trackRear: 1.44, cgHeight: 0.46,
    length: 4.28, width: 1.69, height: 1.30, wheelRadius: 0.31,
    peakTorque: 240, peakRpm: 5000, redline: 7400, idleRpm: 950, turbo: 0,
    gears: [3.62, 2.19, 1.54, 1.19, 0.96, 0.78], reverseRatio: 3.4, finalDrive: 3.9,
    maxSteerDeg: 40, brakeTorque: 2700, tireGrip: 1.10, rearGripBias: 1.0, rollFront: 0.54, diffLock: 0.62, cdA: 0.66,
    color: '#f2f2f2',
  },
  {
    id: 'barin', brand: 'Сибирь', name: 'Барин V8', body: 'bigsedan', price: 52000, level: 7,
    desc: 'Тяжёлый барский седан с V8. Горы момента, длинные красивые заносы.',
    mass: 1480, wheelbase: 2.80, weightFront: 0.55, trackFront: 1.52, trackRear: 1.50, cgHeight: 0.55,
    length: 4.85, width: 1.80, height: 1.46, wheelRadius: 0.33,
    peakTorque: 370, peakRpm: 3900, redline: 6200, idleRpm: 800, turbo: 0,
    gears: [3.15, 1.95, 1.40, 1.00, 0.78], reverseRatio: 3.2, finalDrive: 3.7,
    maxSteerDeg: 35, brakeTorque: 3200, tireGrip: 1.04, rearGripBias: 1.0, rollFront: 0.58, diffLock: 0.55, cdA: 0.78,
    color: '#0f3a2a',
  },
  {
    id: 'universal', brand: 'Бурьян', name: 'Универсал-Т', body: 'wagon', price: 68000, level: 9,
    desc: 'Дачный универсал с турбиной. Смешной снаружи, страшный на трассе.',
    mass: 1290, wheelbase: 2.62, weightFront: 0.53, trackFront: 1.46, trackRear: 1.44, cgHeight: 0.52,
    length: 4.55, width: 1.71, height: 1.45, wheelRadius: 0.31,
    peakTorque: 290, peakRpm: 4400, redline: 6900, idleRpm: 900, turbo: 0.32,
    gears: [3.30, 2.05, 1.45, 1.09, 0.86], reverseRatio: 3.3, finalDrive: 3.9,
    maxSteerDeg: 38, brakeTorque: 2900, tireGrip: 1.08, rearGripBias: 1.0, rollFront: 0.56, diffLock: 0.62, cdA: 0.76,
    color: '#d7b26a',
  },
  {
    id: 'ronin', brand: 'Ураган', name: 'Ронин', body: 'coupe', price: 105000, level: 12,
    desc: 'Турбо-купе для профи: резкая отдача, огромный угол, прощает ошибки реже.',
    mass: 1240, wheelbase: 2.55, weightFront: 0.52, trackFront: 1.50, trackRear: 1.49, cgHeight: 0.45,
    length: 4.50, width: 1.74, height: 1.29, wheelRadius: 0.31,
    peakTorque: 330, peakRpm: 5200, redline: 7600, idleRpm: 950, turbo: 0.40,
    gears: [3.60, 2.20, 1.55, 1.18, 0.95, 0.78], reverseRatio: 3.4, finalDrive: 3.85,
    maxSteerDeg: 42, brakeTorque: 3000, tireGrip: 1.14, rearGripBias: 1.0, rollFront: 0.54, diffLock: 0.68, cdA: 0.66,
    color: '#7a2cc0',
  },
  {
    id: 'raketa', brand: 'Ураган', name: 'Ракета', body: 'coupe', price: 170000, level: 15,
    desc: 'Широкое спорт-купе. Максимум мощности и сцепления — дрифт на пределе.',
    mass: 1360, wheelbase: 2.60, weightFront: 0.51, trackFront: 1.58, trackRear: 1.57, cgHeight: 0.44,
    length: 4.60, width: 1.85, height: 1.26, wheelRadius: 0.33,
    peakTorque: 420, peakRpm: 5200, redline: 7800, idleRpm: 1000, turbo: 0.45,
    gears: [3.45, 2.20, 1.62, 1.28, 1.02, 0.83], reverseRatio: 3.4, finalDrive: 3.7,
    maxSteerDeg: 42, brakeTorque: 3600, tireGrip: 1.20, rearGripBias: 1.0, rollFront: 0.53, diffLock: 0.72, cdA: 0.70,
    color: '#ff7a00',
  },
];

export const TUNING_PARTS = [
  { id: 'engine', name: 'Двигатель', desc: 'Крутящий момент и обороты', max: 5, basePrice: 2200 },
  { id: 'turbo', name: 'Турбо', desc: 'Давление наддува', max: 5, basePrice: 2600 },
  { id: 'suspension', name: 'Подвеска', desc: 'Ниже центр тяжести, быстрее реакция', max: 5, basePrice: 1700 },
  { id: 'tires', name: 'Шины', desc: 'Сцепление', max: 5, basePrice: 1900 },
  { id: 'diff', name: 'Дифференциал', desc: 'Блокировка: легче держать занос', max: 5, basePrice: 1500 },
  { id: 'brakes', name: 'Тормоза', desc: 'Усилие торможения', max: 5, basePrice: 1400 },
  { id: 'weight', name: 'Облегчение', desc: 'Меньше масса', max: 5, basePrice: 2000 },
];

export const WHEELS = [
  { id: 'steel', name: 'Штамповки', price: 0, spokes: 0 },
  { id: 'star5', name: 'Звезда-5', price: 1200, spokes: 5 },
  { id: 'mesh', name: 'Сетка', price: 2200, spokes: 12 },
  { id: 'deep6', name: 'Глубокие-6', price: 3000, spokes: 6 },
  { id: 'split10', name: 'Раздвоенные', price: 3800, spokes: 10 },
];
export const BODYKITS = [
  { id: 'none', name: 'Сток', price: 0 },
  { id: 'street', name: 'Стрит', price: 3500 },
  { id: 'wide', name: 'Широкий', price: 7500 },
  { id: 'rally', name: 'Ралли', price: 6000 },
];
export const SPOILERS = [
  { id: 'none', name: 'Без спойлера', price: 0 },
  { id: 'lip', name: 'Лип', price: 900 },
  { id: 'duck', name: 'Уточка', price: 1600 },
  { id: 'gt', name: 'GT-крыло', price: 3200 },
];
export const NEONS = [
  { id: 'none', name: 'Без неона', price: 0, color: 0 },
  { id: 'red', name: 'Красный', price: 1500, color: 0xff2030 },
  { id: 'blue', name: 'Синий', price: 1500, color: 0x2a60ff },
  { id: 'green', name: 'Зелёный', price: 1500, color: 0x20ff60 },
  { id: 'purple', name: 'Фиолетовый', price: 1800, color: 0xb040ff },
];
export const PAINTS = ['#b3262b', '#e5e5e0', '#1c1c20', '#2d5fb0', '#0f3a2a', '#d7b26a', '#7a2cc0', '#ff7a00', '#ffd21f', '#00a3a3', '#ff4fa0', '#8a8d93', '#5a1f1f', '#0b2a6b', '#6fd1ff', '#a6ff3b'];

export function getCar(id) { return CARS.find((c) => c.id === id) || CARS[0]; }

export function upgradePrice(part, nextLevel) {
  const p = TUNING_PARTS.find((x) => x.id === part);
  return Math.round(p.basePrice * Math.pow(1.7, nextLevel - 1) / 50) * 50;
}

/** Итоговые параметры физики = базовые параметры машины + тюнинг. */
export function buildSpec(def, tuning = {}) {
  const t = (k) => tuning[k] || 0;
  const spec = { ...def };
  spec.maxSteer = def.maxSteerDeg * D2R;
  spec.peakTorque = def.peakTorque * (1 + 0.085 * t('engine'));
  spec.redline = def.redline + 110 * t('engine');
  spec.turbo = def.turbo > 0 || t('turbo') > 0 ? def.turbo + (t('turbo') > 0 ? 0.08 + 0.1 * t('turbo') : 0) : 0;
  spec.cgHeight = def.cgHeight * (1 - 0.025 * t('suspension'));
  spec.rollFront = Math.min(0.7, def.rollFront + 0.012 * t('suspension'));
  spec.tireGrip = def.tireGrip * (1 + 0.04 * t('tires'));
  spec.diffLock = Math.min(1, def.diffLock + 0.07 * t('diff'));
  spec.brakeTorque = def.brakeTorque * (1 + 0.09 * t('brakes'));
  spec.mass = def.mass * (1 - 0.035 * t('weight'));
  return spec;
}

/** Условные «очки производительности» для баров в гараже. */
export function rating(def, tuning = {}) {
  const s = buildSpec(def, tuning);
  const power = Math.min(1, (s.peakTorque * (1 + s.turbo * 0.7)) / s.mass / 0.40);
  const speed = Math.min(1, (s.peakTorque * (1 + s.turbo * 0.7) * s.redline / 9549) / s.mass / 0.20);
  const grip = Math.min(1, (s.tireGrip - 0.9) / 0.5);
  const brake = Math.min(1, s.brakeTorque / s.mass / 3.4);
  const drift = Math.min(1, 0.3 + s.diffLock * 0.35 + power * 0.25 + (1 - Math.abs(s.tireGrip - 1.1)) * 0.1);
  return { power, speed, grip, brake, drift };
}
