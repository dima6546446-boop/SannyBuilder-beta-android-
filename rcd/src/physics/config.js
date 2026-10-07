// Все ключевые константы физики в одном месте — меняйте их, чтобы подкрутить ощущение дрифта.
export const PHYS = {
  stepHz: 240,                 // фиксированный шаг физики (не зависит от FPS)
  maxFrameDt: 0.1,             // защита от «спирали смерти»
  gravity: 9.81,
  airDensity: 1.2,
  rollingResistance: 0.014,

  // --- шины ---
  tire: {
    muScale: 1.22,             // общий множитель сцепления (современная спортивная резина)
    slipAnglePeak: 0.145,      // рад (~8.3°): угол увода максимального бокового сцепления
    slipRatioPeak: 0.13,       // пик продольного проскальзывания
    slideFloor: 0.76,          // сцепление в полном скольжении относительно пика (передняя ось)
    slideFloorRear: 0.82,     // задняя ось в скольжении держит чуть больше — занос не схлопывается в вертушку
    falloff: 1.25,             // скорость падения сцепления после пика
    minSpeedLat: 1.2,          // регуляризация угла увода на малых скоростях (м/с)
    minSpeedLong: 2.2,         // регуляризация продольного проскальзывания
    latForceCap: 0.55,         // защита от дрожания: макс. доля импульса на шаг
  },

  // --- динамика кузова ---
  chassis: {
    inertiaFactor: 0.54,       // Iz = m * (L * factor)^2
    loadFilterTau: 0.085,      // сглаживание ускорений для переноса веса (с)
    rollCgScale: 1.0,
    yawDamping: 0.06,          // слабое аэро/шинное демпфирование рыскания (1/с)
  },

  // --- привод ---
  drivetrain: {
    efficiency: 0.90,
    powerMul: 1.9,            // «аркадная» добавка к тяге: даже слабые машины могут сорвать зад
    engineInertia: 0.22,       // кг·м² маховик и двигатель
    wheelInertia: 1.9,         // кг·м² (колесо + приведённая трансмиссия)
    engineBrake: 0.10,         // доля пикового момента при отпущенном газе
    shiftTime: 0.14,
    upshiftRpmFrac: 0.93,
    downshiftRpmFrac: 0.40,
    launchRpm: 3600,           // «пробуксовка сцепления» при старте
    turboSpool: 1.1,           // 1/с
    turboDecay: 3.0,
    clutchKickTime: 0.30,
    clutchKickMul: 1.9,
    lsdBiasStiffness: 900,     // жёсткость блокировки дифференциала
  },

  // --- рулевое управление и помощники ---
  steering: {
    rate: 4.2,                 // рад/с макс. скорость поворота колёс (в долях хода: 1/с)
    speedRef: 38,              // м/с, при котором ход руля сокращается до minLockFrac
    minLockFrac: 0.42,
    fullLockSlideDeg: 24,      // при таком угле заноса доступен полный ход руля
    assistGain: 0.92,          // противорулевание: доля угла заноса
    assistThresholdDeg: 3,
    assistRangeDeg: 8,
    yawDampGain: 0.045,        // помощник стабилизации рыскания (Н·м·с / кг·м²)
    angleLimitBoost: 0.16,     // прибавка сцепления задней оси на больших углах
    angleLimitStartDeg: 24,
    angleLimitRangeDeg: 28,
    yawSoftMax: 0.85,          // рад/с (~60°/с): выше этого рыскание гасится
    yawSoftGain: 11,
    angleThrottleCut: 0.92,    // доля момента, срезаемая на больших углах заноса
    angleCutStartDeg: 28,
    angleCutRangeDeg: 16,
    spinGuardDeg: 62,          // выше этого угла помощник гасит вращение
  },

  // --- ручник и тормоза ---
  handbrakeTorque: 3600,       // Н·м на заднюю ось (блокировка колёс)
  brakeBias: 0.66,             // доля торможения на передней оси

  // --- столкновения ---
  collision: {
    restitution: 0.22,
    friction: 0.35,
    minImpact: 2.0,            // м/с — слабее не считаем ударом
  },
};

// Параметры поверхностей: коэффициент сцепления, сопротивление качению, цвет дыма
export const SURFACES = {
  asphalt:  { grip: 1.00, roll: 1.0, smoke: 0xe8e8e8, mark: true },
  concrete: { grip: 0.96, roll: 1.0, smoke: 0xdddddd, mark: true },
  wet:      { grip: 0.72, roll: 1.2, smoke: 0xcfd6dc, mark: false },
  gravel:   { grip: 0.62, roll: 2.6, smoke: 0xb9a98a, mark: false },
  dirt:     { grip: 0.66, roll: 2.2, smoke: 0xa88d68, mark: false },
  grass:    { grip: 0.55, roll: 3.2, smoke: 0x8f9a70, mark: false },
  snow:     { grip: 0.35, roll: 2.0, smoke: 0xf2f6fa, mark: false },
};
