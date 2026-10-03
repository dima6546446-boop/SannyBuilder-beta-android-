/**
 * Каталог автомобилей АвтоВАЗ.
 * Геометрия: начало координат — центр масс на уровне земли, вперёд +Z, влево +X.
 * top   — верхний контур нижней части кузова (от переднего торца к заднему) в координатах (z, y).
 *         Низ (порог sill) и колёсные арки достраиваются генератором.
 * cabin — контур «оранжереи» (4 точки): основание лобового → крыша спереди → крыша сзади → основание заднего стекла.
 * Физика: реальные мощность/момент/передаточные числа (с небольшим округлением).
 */

const CLASSIC_COLORS = [0x7a1020, 0xecebe4, 0x1f4f6a, 0x3d86b8, 0xb31d12, 0xc8b98a, 0x155e3e, 0x4b5157, 0xb89a4a, 0x2b2b2b];
const MODERN_COLORS = [0xf2f2f2, 0x1c1c1c, 0x8a8f94, 0xb0161b, 0x1d3f7a, 0x5a6b3a, 0xc4a468, 0x6d2f86, 0xd9601c, 0x2e7ab0];

export const CARS = [
  {
    id: 'vaz2101', name: 'ВАЗ-2101', nick: '«Копейка»', years: '1970–1988', price: 25000,
    dims: { W: 1.61, cabinW: 1.39, front: 1.93, rear: -2.14, axleF: 1.14, axleR: -1.284, track: 1.35, wheelR: 0.29, wheelW: 0.165, sill: 0.30, archR: 0.37 },
    top: [[1.93, 0.50], [1.93, 0.78], [1.85, 0.83], [0.86, 0.92], [-1.40, 0.95], [-2.04, 0.90], [-2.14, 0.80], [-2.14, 0.48]],
    cabin: [[0.90, 0.88], [0.05, 1.40], [-0.90, 1.41], [-1.36, 0.92]],
    glassBottom: 0.99, pillars: [-0.26],
    head: { style: 'round4', y: 0.66, r: 0.082, xs: [0.46, 0.65] },
    tail: { style: 'vertical', y: 0.66, h: 0.24, w: 0.2, x: 0.64 },
    grille: { style: 'chrome-mesh', w: 1.46, h: 0.2, y: 0.66 },
    bumper: { style: 'chrome', y: 0.43, h: 0.09, fangs: true },
    trim: 0.71, seams: [0.93, -0.30, -1.30], wheelStyle: 'steel', antenna: true,
    colors: CLASSIC_COLORS,
    spec: { mass: 1030, hp: 64, torque: 89, peakRpm: 3400, redline: 6000, gears: [3.53, 3.75, 2.30, 1.49, 1.00], final: 4.3, drive: 'RWD', cgHeight: 0.55, brake: 8800, drag: 0.5, grip: 1.0, maxSteer: 0.6, tank: 39 },
    traffic: 1.0,
  },
  {
    id: 'vaz2106', name: 'ВАЗ-2106', nick: '«Шестёрка»', years: '1976–2006', price: 40000,
    dims: { W: 1.61, cabinW: 1.39, front: 1.99, rear: -2.18, axleF: 1.14, axleR: -1.284, track: 1.36, wheelR: 0.29, wheelW: 0.175, sill: 0.30, archR: 0.37 },
    top: [[1.99, 0.50], [1.99, 0.79], [1.90, 0.84], [0.86, 0.93], [-1.42, 0.96], [-2.10, 0.92], [-2.18, 0.84], [-2.18, 0.50]],
    cabin: [[0.90, 0.89], [0.04, 1.41], [-0.92, 1.42], [-1.38, 0.93]],
    glassBottom: 1.0, pillars: [-0.27],
    head: { style: 'round4', y: 0.67, r: 0.085, xs: [0.44, 0.64], panel: true },
    tail: { style: 'wide', y: 0.72, h: 0.2, w: 0.42, x: 0.56 },
    grille: { style: 'black-chrome', w: 1.5, h: 0.23, y: 0.67 },
    bumper: { style: 'chrome', y: 0.44, h: 0.11, strip: true },
    trim: 0.72, seams: [0.95, -0.30, -1.32], wheelStyle: 'classic', antenna: true,
    colors: CLASSIC_COLORS,
    spec: { mass: 1045, hp: 80, torque: 121, peakRpm: 3000, redline: 6000, gears: [3.24, 3.75, 2.30, 1.49, 1.00], final: 3.9, drive: 'RWD', cgHeight: 0.55, brake: 9000, drag: 0.52, grip: 1.02, maxSteer: 0.6, tank: 39 },
    traffic: 2.0,
  },
  {
    id: 'vaz2107', name: 'ВАЗ-2107', nick: '«Семёрка»', years: '1982–2012', price: 50000,
    dims: { W: 1.62, cabinW: 1.40, front: 1.98, rear: -2.18, axleF: 1.14, axleR: -1.284, track: 1.36, wheelR: 0.29, wheelW: 0.175, sill: 0.30, archR: 0.37 },
    top: [[1.98, 0.52], [1.98, 0.80], [1.88, 0.86], [0.86, 0.95], [-1.42, 0.97], [-2.10, 0.93], [-2.18, 0.84], [-2.18, 0.50]],
    cabin: [[0.92, 0.90], [0.0, 1.42], [-0.95, 1.43], [-1.40, 0.92]],
    glassBottom: 1.0, pillars: [-0.28],
    head: { style: 'rect', y: 0.68, h: 0.18, w: 0.34, x: 0.61 },
    tail: { style: 'wide', y: 0.72, h: 0.2, w: 0.44, x: 0.56 },
    grille: { style: 'chrome-bars', w: 0.8, h: 0.25, y: 0.68 },
    bumper: { style: 'chrome', y: 0.44, h: 0.12, strip: true },
    trim: 0.72, seams: [0.95, -0.30, -1.32], wheelStyle: 'classic', antenna: true,
    colors: CLASSIC_COLORS,
    spec: { mass: 1060, hp: 71, torque: 104, peakRpm: 3400, redline: 6200, gears: [3.53, 3.67, 2.10, 1.36, 1.00, 0.82], final: 3.9, drive: 'RWD', cgHeight: 0.55, brake: 9500, drag: 0.52, grip: 1.05, maxSteer: 0.6, tank: 39 },
    traffic: 3.0,
  },
  {
    id: 'oka', name: 'ВАЗ-1111', nick: '«Ока»', years: '1988–2008', price: 15000,
    dims: { W: 1.42, cabinW: 1.26, front: 1.40, rear: -1.80, axleF: 0.85, axleR: -1.33, track: 1.21, wheelR: 0.26, wheelW: 0.145, sill: 0.27, archR: 0.33 },
    top: [[1.40, 0.44], [1.40, 0.67], [1.20, 0.74], [0.55, 0.88], [-1.55, 0.96], [-1.80, 0.94], [-1.80, 0.42]],
    cabin: [[0.62, 0.84], [-0.15, 1.37], [-1.55, 1.37], [-1.76, 0.98]],
    glassBottom: 0.97, pillars: [-0.55],
    head: { style: 'rect', y: 0.59, h: 0.13, w: 0.26, x: 0.47 },
    tail: { style: 'vertical', y: 0.72, h: 0.22, w: 0.14, x: 0.57 },
    grille: { style: 'slot', w: 0.5, h: 0.07, y: 0.6 },
    bumper: { style: 'black', y: 0.38, h: 0.15 },
    trim: null, seams: [0.55, -0.55], wheelStyle: 'steel',
    colors: [0xd8c23a, 0x3d86b8, 0xecebe4, 0xb31d12, 0x7fa84a, 0x9b9b9b],
    spec: { mass: 700, hp: 33, torque: 52, peakRpm: 3400, redline: 5800, gears: [3.5, 3.70, 2.06, 1.36, 0.97], final: 4.54, drive: 'FWD', cgHeight: 0.5, brake: 6200, drag: 0.36, grip: 0.98, maxSteer: 0.62, tank: 30 },
    traffic: 0.7,
  },
  {
    id: 'vaz2109', name: 'ВАЗ-2109', nick: '«Девятка»', years: '1987–2004', price: 70000,
    dims: { W: 1.65, cabinW: 1.44, front: 1.71, rear: -2.30, axleF: 0.98, axleR: -1.48, track: 1.40, wheelR: 0.28, wheelW: 0.165, sill: 0.29, archR: 0.36 },
    top: [[1.71, 0.46], [1.71, 0.70], [1.42, 0.76], [0.72, 0.90], [-1.60, 1.00], [-2.24, 0.98], [-2.30, 0.88], [-2.30, 0.45]],
    cabin: [[0.78, 0.86], [-0.2, 1.39], [-1.15, 1.37], [-2.22, 1.01]],
    glassBottom: 0.99, pillars: [-0.35, -1.28],
    head: { style: 'wedge', y: 0.62, h: 0.12, w: 0.44, x: 0.56 },
    tail: { style: 'hatch', y: 0.78, h: 0.16, w: 0.44, x: 0.55 },
    grille: { style: 'slot', w: 0.58, h: 0.08, y: 0.62 },
    bumper: { style: 'black', y: 0.41, h: 0.2 },
    trim: null, seams: [0.78, -0.35, -1.28], wheelStyle: 'steel',
    colors: [0x1c1c1c, 0xb0161b, 0x2e5f8a, 0xecebe4, 0x4a7a3a, 0x8a8f94, 0x6d2f86],
    spec: { mass: 945, hp: 70, torque: 106, peakRpm: 3400, redline: 6200, gears: [3.53, 3.64, 1.95, 1.36, 0.94, 0.78], final: 4.13, drive: 'FWD', cgHeight: 0.52, brake: 9000, drag: 0.46, grip: 1.05, maxSteer: 0.62, tank: 43 },
    traffic: 2.0,
  },
  {
    id: 'niva', name: 'ВАЗ-2121', nick: '«Нива»', years: '1977–н.в.', price: 90000,
    dims: { W: 1.68, cabinW: 1.50, front: 1.73, rear: -2.01, axleF: 1.05, axleR: -1.15, track: 1.43, wheelR: 0.35, wheelW: 0.185, sill: 0.42, archR: 0.44 },
    top: [[1.73, 0.62], [1.73, 0.95], [1.65, 1.02], [0.70, 1.08], [-1.90, 1.12], [-2.01, 1.08], [-2.01, 0.55]],
    cabin: [[0.76, 1.05], [0.10, 1.61], [-1.85, 1.62], [-1.98, 1.10]],
    glassBottom: 1.15, pillars: [-0.52],
    head: { style: 'round2', y: 0.85, r: 0.09, xs: [0.6] },
    tail: { style: 'vertical', y: 0.92, h: 0.24, w: 0.13, x: 0.72 },
    grille: { style: 'black-bars', w: 0.84, h: 0.24, y: 0.86 },
    bumper: { style: 'black', y: 0.56, h: 0.15 },
    trim: null, seams: [0.72, -0.52], wheelStyle: 'steel', rails: false, snorkel: false,
    colors: [0xe9e3cf, 0x5a6b3a, 0xb31d12, 0x1f4f6a, 0x7a1020, 0x3d3d3d, 0xd8c23a],
    spec: { mass: 1210, hp: 80, torque: 121, peakRpm: 3000, redline: 5800, gears: [3.53, 3.67, 2.10, 1.36, 1.00, 0.82], final: 3.9, drive: 'AWD', cgHeight: 0.72, brake: 10000, drag: 0.62, grip: 1.08, maxSteer: 0.6, tank: 42 },
    traffic: 1.5,
  },
  {
    id: 'priora', name: 'Lada Priora', nick: '«Приора»', years: '2007–2018', price: 140000,
    dims: { W: 1.68, cabinW: 1.47, front: 1.83, rear: -2.52, axleF: 1.0, axleR: -1.49, track: 1.43, wheelR: 0.30, wheelW: 0.185, sill: 0.30, archR: 0.38 },
    top: [[1.83, 0.45], [1.83, 0.78], [1.60, 0.85], [0.80, 0.93], [-1.55, 1.00], [-2.32, 0.98], [-2.52, 0.92], [-2.52, 0.45]],
    cabin: [[0.86, 0.89], [-0.12, 1.40], [-1.10, 1.40], [-1.75, 0.99]],
    glassBottom: 1.0, pillars: [-0.38],
    head: { style: 'modern', y: 0.70, h: 0.15, w: 0.42, x: 0.58 },
    tail: { style: 'modern', y: 0.84, h: 0.13, w: 0.42, x: 0.57 },
    grille: { style: 'modern-chrome', w: 0.52, h: 0.12, y: 0.67 },
    bumper: { style: 'body', y: 0.42, h: 0.26 },
    trim: null, seams: [0.86, -0.38, -1.4], wheelStyle: 'star',
    colors: MODERN_COLORS,
    spec: { mass: 1088, hp: 98, torque: 145, peakRpm: 4000, redline: 6500, gears: [3.53, 3.64, 1.95, 1.36, 0.94, 0.78], final: 3.7, drive: 'FWD', cgHeight: 0.52, brake: 10500, drag: 0.44, grip: 1.1, maxSteer: 0.62, tank: 43 },
    traffic: 1.5,
  },
  {
    id: 'granta', name: 'Lada Granta', nick: '«Гранта»', years: '2011–н.в.', price: 180000,
    dims: { W: 1.70, cabinW: 1.48, front: 1.78, rear: -2.48, axleF: 0.98, axleR: -1.50, track: 1.43, wheelR: 0.305, wheelW: 0.185, sill: 0.31, archR: 0.39 },
    top: [[1.78, 0.46], [1.78, 0.83], [1.53, 0.89], [0.72, 0.97], [-1.52, 1.05], [-2.28, 1.03], [-2.48, 0.97], [-2.48, 0.46]],
    cabin: [[0.78, 0.93], [-0.15, 1.48], [-1.08, 1.48], [-1.70, 1.03]],
    glassBottom: 1.05, pillars: [-0.36],
    head: { style: 'modern', y: 0.74, h: 0.16, w: 0.46, x: 0.56 },
    tail: { style: 'modern', y: 0.88, h: 0.14, w: 0.42, x: 0.56 },
    grille: { style: 'granta', w: 0.56, h: 0.16, y: 0.7 },
    bumper: { style: 'body', y: 0.44, h: 0.28 },
    trim: null, seams: [0.8, -0.36, -1.38], wheelStyle: 'steel',
    colors: MODERN_COLORS,
    spec: { mass: 1160, hp: 87, torque: 140, peakRpm: 3800, redline: 6200, gears: [3.53, 3.64, 1.95, 1.36, 0.94, 0.78], final: 3.9, drive: 'FWD', cgHeight: 0.55, brake: 10500, drag: 0.46, grip: 1.08, maxSteer: 0.62, tank: 50 },
    traffic: 2.0,
  },
  {
    id: 'vesta', name: 'Lada Vesta', nick: '«Веста»', years: '2015–н.в.', price: 320000,
    dims: { W: 1.76, cabinW: 1.52, front: 1.89, rear: -2.52, axleF: 1.05, axleR: -1.585, track: 1.51, wheelR: 0.315, wheelW: 0.195, sill: 0.32, archR: 0.40 },
    top: [[1.89, 0.46], [1.89, 0.85], [1.60, 0.91], [0.75, 0.99], [-1.60, 1.07], [-2.34, 1.06], [-2.52, 0.99], [-2.52, 0.46]],
    cabin: [[0.82, 0.95], [-0.2, 1.48], [-1.2, 1.47], [-1.78, 1.05]],
    glassBottom: 1.07, pillars: [-0.4],
    head: { style: 'modern', y: 0.77, h: 0.14, w: 0.5, x: 0.6 },
    tail: { style: 'modern', y: 0.9, h: 0.13, w: 0.46, x: 0.6 },
    grille: { style: 'xface', w: 0.92, h: 0.3, y: 0.62 },
    bumper: { style: 'body', y: 0.44, h: 0.3 },
    trim: null, seams: [0.84, -0.40, -1.46], wheelStyle: 'sport',
    colors: MODERN_COLORS,
    spec: { mass: 1230, hp: 106, torque: 148, peakRpm: 4200, redline: 6500, gears: [3.53, 3.73, 2.05, 1.36, 1.03, 0.82], final: 3.93, drive: 'FWD', cgHeight: 0.54, brake: 11500, drag: 0.45, grip: 1.15, maxSteer: 0.6, tank: 55 },
    traffic: 1.2,
  },
  {
    id: 'largus', name: 'Lada Largus', nick: '«Ларгус»', years: '2012–н.в.', price: 260000,
    dims: { W: 1.75, cabinW: 1.56, front: 1.93, rear: -2.54, axleF: 1.15, axleR: -1.755, track: 1.47, wheelR: 0.31, wheelW: 0.185, sill: 0.33, archR: 0.40 },
    top: [[1.93, 0.48], [1.93, 0.90], [1.63, 0.96], [0.82, 1.01], [-2.36, 1.05], [-2.54, 1.03], [-2.54, 0.48]],
    cabin: [[0.88, 0.97], [-0.1, 1.64], [-2.35, 1.64], [-2.48, 1.07]],
    glassBottom: 1.08, pillars: [-0.42, -1.45],
    head: { style: 'modern', y: 0.8, h: 0.17, w: 0.45, x: 0.58 },
    tail: { style: 'vertical', y: 0.86, h: 0.28, w: 0.16, x: 0.72 },
    grille: { style: 'modern-chrome', w: 0.62, h: 0.18, y: 0.76 },
    bumper: { style: 'body', y: 0.46, h: 0.28 },
    trim: null, seams: [0.88, -0.42, -1.45], wheelStyle: 'steel', rails: true,
    colors: MODERN_COLORS,
    spec: { mass: 1260, hp: 102, torque: 145, peakRpm: 3750, redline: 6000, gears: [3.55, 3.73, 2.05, 1.32, 0.97, 0.76], final: 4.5, drive: 'FWD', cgHeight: 0.62, brake: 11000, drag: 0.55, grip: 1.05, maxSteer: 0.58, tank: 50 },
    traffic: 1.0,
  },
];

// Спецверсии (только трафик/сцены): фиксированная окраска, спецоборудование
export const VARIANTS = [
  { id: 'police', base: 'vaz2107', name: 'ДПС', fixedColor: 0xf2f2f2, police: true },
  { id: 'taxi', base: 'granta', name: 'Такси', fixedColor: 0xf2c200, taxi: true },
];

export const CAR_BY_ID = Object.fromEntries(CARS.map((c) => [c.id, c]));

/** Полный список моделей для рендера (обычные + спецверсии). */
export function allRenderModels() {
  const list = CARS.map((c) => ({ ...c, key: c.id }));
  for (const v of VARIANTS) list.push({ ...CAR_BY_ID[v.base], ...v, key: v.id, id: v.id });
  return list;
}

// ---------------------------------------------------------------- тюнинг
export const TUNING = {
  wheels: [
    { id: 'default', name: 'Заводские', price: 0 },
    { id: 'steel', name: 'Штамповка', price: 1500 },
    { id: 'classic', name: 'Колпаки «Жигули»', price: 3000 },
    { id: 'star', name: 'Литьё «Звезда»', price: 9000 },
    { id: 'sport', name: 'Спорт 10 спиц', price: 14000 },
    { id: 'mesh', name: 'Сетка «BBS»', price: 20000 },
  ],
  height: [
    { id: 0, name: 'Сток', value: 0, price: 0 },
    { id: 1, name: 'Занижение −4 см', value: -0.04, price: 4000 },
    { id: 2, name: 'Корч −8 см', value: -0.08, price: 8000 },
    { id: 3, name: 'Лифт +5 см', value: 0.05, price: 6000 },
  ],
  tint: [
    { id: 0, name: 'Заводская', value: 0.6, price: 0 },
    { id: 1, name: '50%', value: 0.82, price: 2000 },
    { id: 2, name: 'Наглухо', value: 0.97, price: 3500 },
  ],
  engine: [
    { id: 0, name: 'Сток', value: 1.0, price: 0 },
    { id: 1, name: 'Чип-тюнинг', value: 1.12, price: 12000 },
    { id: 2, name: 'Расточка + распредвал', value: 1.28, price: 35000 },
    { id: 3, name: 'Турбо «Колхоз»', value: 1.55, price: 90000 },
  ],
  tires: [
    { id: 0, name: 'Кама-205', value: 1.0, price: 0 },
    { id: 1, name: 'Спортивные', value: 1.12, price: 10000 },
  ],
  // неон под днищем (value — цвет)
  neon: [
    { id: 0, name: 'Нет', value: null, price: 0 },
    { id: 1, name: 'Синий', value: 0x2a7bff, price: 6000 },
    { id: 2, name: 'Красный', value: 0xff2a3a, price: 6000 },
    { id: 3, name: 'Зелёный', value: 0x2aff6a, price: 6000 },
    { id: 4, name: 'Фиолетовый', value: 0xb02aff, price: 7000 },
    { id: 5, name: 'Бирюзовый', value: 0x2affe6, price: 7000 },
    { id: 6, name: 'Белый', value: 0xf2f6ff, price: 8000 },
  ],
  // номера: регион и «блатные» комбинации
  plate: [
    { id: 0, name: 'Заводской · Е213КХ 26', value: ['Е213КХ', '26'], price: 0 },
    { id: 1, name: 'Дагестан · М512ОК 05', value: ['М512ОК', '05'], price: 1500 },
    { id: 2, name: 'Ингушетия · В340НА 06', value: ['В340НА', '06'], price: 1500 },
    { id: 3, name: 'КБР · Т118РС 07', value: ['Т118РС', '07'], price: 1500 },
    { id: 4, name: 'КЧР · Н622АУ 09', value: ['Н622АУ', '09'], price: 1500 },
    { id: 5, name: 'Осетия · К404ОТ 15', value: ['К404ОТ', '15'], price: 1500 },
    { id: 6, name: 'Чечня · Е555ХМ 95', value: ['Е555ХМ', '95'], price: 2500 },
    { id: 7, name: 'Блатной · А777АА 05', value: ['А777АА', '05'], price: 25000 },
    { id: 8, name: 'Блатной · Х005ХХ 06', value: ['Х005ХХ', '06'], price: 30000 },
    { id: 9, name: 'Блатной · О001ОО 95', value: ['О001ОО', '95'], price: 45000 },
  ],
  horn: [
    { id: 0, name: 'Заводской', value: 0, price: 0 },
    { id: 1, name: '«Лезгинка» (мелодия)', value: 1, price: 5000 },
    { id: 2, name: '«Итальянка» (6 нот)', value: 2, price: 7000 },
    { id: 3, name: '«Газель-дудка»', value: 3, price: 3000 },
  ],
  exhaust: [
    { id: 0, name: 'Заводской', value: 0, price: 0 },
    { id: 1, name: 'Прямоток (стреляет)', value: 1, price: 9000 },
  ],
  paintPrice: 2500,
};

export const PAINT_PALETTE = [
  0xecebe4, 0xf4f4f4, 0x1c1c1c, 0x8a8f94, 0x4b5157, 0x7a1020, 0xb31d12, 0xd9601c, 0xd8c23a,
  0xb89a4a, 0x155e3e, 0x5a6b3a, 0x7fa84a, 0x1f4f6a, 0x3d86b8, 0x1d3f7a, 0x6d2f86, 0xe07aa0,
];
