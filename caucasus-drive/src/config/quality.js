/**
 * Пресеты качества под мобильные GPU.
 *
 * Чёткость: разрешение рендера задаётся в ФИЗИЧЕСКИХ пикселях по короткой стороне экрана
 * (renderHeight), а не множителем к CSS-пикселям. Раньше на телефоне с DPR 3 «Низкое»
 * рендерило ~300 строк и растягивало их в 4 раза — отсюда «мыло». Теперь рендер не опускается
 * ниже 1 CSS-пикселя (minRenderHeight) и сглаживается аппаратным MSAA, который на тайловых
 * GPU (Mali/Adreno/PowerVR) почти бесплатен.
 *
 * Производительность: адаптивный регулятор (PerfMonitor) сначала снимает дорогие эффекты
 * (bloom → тени → трафик/деревья/LOD → облака) и только в самом конце снижает разрешение.
 * Поэтому «Высокое» можно ставить и на слабом телефоне — оно само ужмётся до стабильных FPS.
 */
export const QUALITY_PRESETS = {
  low: {
    name: 'low', label: 'Низкое',
    renderHeight: 540, minRenderHeight: 300,
    antialias: true, precision: 'highp',
    shadows: false, shadowMapSize: 512, shadowUpdateEvery: 0, shadowRange: 30,
    trafficCount: 10, drawDistance: 220, carLodDistance: 45,
    treeDensity: 0.45, treeLodDistance: 60,
    carEnvMap: false, transparentGlass: false, streetLightPools: false, parkedDensity: 0.45, bloom: false, clouds: false, glow: true,
    physicalPaint: false, normalMaps: false, softShadows: false,
    anisotropy: 4, occlusion: true,
  },
  medium: {
    name: 'medium', label: 'Среднее',
    renderHeight: 680, minRenderHeight: 320,
    antialias: true, precision: 'highp',
    shadows: true, shadowMapSize: 1024, shadowUpdateEvery: 2, shadowRange: 40,
    trafficCount: 18, drawDistance: 320, carLodDistance: 70,
    treeDensity: 0.7, treeLodDistance: 90,
    carEnvMap: true, transparentGlass: true, streetLightPools: true, parkedDensity: 0.8, bloom: false, clouds: true, glow: true,
    physicalPaint: false, normalMaps: false, softShadows: false,
    anisotropy: 8, occlusion: true,
  },
  high: {
    name: 'high', label: 'Высокое',
    renderHeight: 860, minRenderHeight: 340,
    antialias: true, precision: 'highp',
    shadows: true, shadowMapSize: 2048, shadowUpdateEvery: 1, shadowRange: 55,
    trafficCount: 28, drawDistance: 450, carLodDistance: 100,
    treeDensity: 1.0, treeLodDistance: 130,
    carEnvMap: true, transparentGlass: true, streetLightPools: true, parkedDensity: 1.0, bloom: true, clouds: true, glow: true,
    physicalPaint: true, normalMaps: true, softShadows: false,
    anisotropy: 8, occlusion: true,
  },
};

/**
 * pixelRatio рендера, при котором короткая сторона холста ≈ targetHeight физических пикселей.
 * Никогда не выше devicePixelRatio (нативное разрешение) и не ниже 0.75 CSS-пикселя
 * (так низко регулятор опускается только на очень слабых GPU).
 */
export function pixelRatioFor(targetHeight) {
  const dpr = window.devicePixelRatio || 1;
  const cssShort = Math.max(1, Math.min(window.innerWidth, window.innerHeight));
  return Math.min(dpr, Math.max(0.75, targetHeight / cssShort));
}

export const STORAGE_KEY = 'caucasusdrive.quality';

function gpuInfo() {
  try {
    const c = document.createElement('canvas');
    const gl = c.getContext('webgl2') || c.getContext('webgl');
    if (!gl) return { renderer: '', webgl2: false };
    const ext = gl.getExtension('WEBGL_debug_renderer_info');
    const renderer = ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    const webgl2 = typeof WebGL2RenderingContext !== 'undefined' && gl instanceof WebGL2RenderingContext;
    gl.getExtension('WEBGL_lose_context')?.loseContext();
    return { renderer: String(renderer || ''), webgl2 };
  } catch {
    return { renderer: '', webgl2: false };
  }
}

/** Эвристика: определяем класс устройства по GPU, ядрам и памяти. */
export function detectQualityName() {
  const { renderer, webgl2 } = gpuInfo();
  const r = renderer.toLowerCase();
  const cores = navigator.hardwareConcurrency || 4;
  const mem = navigator.deviceMemory || 4;
  const mobile = /android|iphone|ipad|mobile/i.test(navigator.userAgent);

  if (!webgl2) return 'low';
  if (/mali-(4|t6|t7|g31|g51|g52)|adreno \(tm\) (3|4|5[01])|powervr|sgx|videocore/.test(r)) return 'low';
  if (!mobile) return 'high';
  if (/adreno \(tm\) (7[3-9]|8)|mali-g(7[1-9]|6[1-9]|710|715|720)|immortalis|xclipse/.test(r) && mem >= 6) return 'high';
  if (cores <= 4 || mem <= 3) return 'low';
  return 'medium';
}

export function resolveQuality() {
  const params = new URLSearchParams(location.search);
  let name = params.get('q');
  if (!name) {
    try { name = localStorage.getItem(STORAGE_KEY); } catch { /* приватный режим */ }
  }
  if (!QUALITY_PRESETS[name]) name = detectQualityName();
  const q = { ...QUALITY_PRESETS[name] };
  if (params.has('res')) q.renderHeight = Math.max(240, parseInt(params.get('res'), 10) || q.renderHeight);
  if (params.has('nogov')) q.governor = false;
  if (params.has('traffic')) q.trafficCount = Math.max(0, parseInt(params.get('traffic'), 10) || 0);
  return q;
}

export function saveQualityName(name) {
  try { localStorage.setItem(STORAGE_KEY, name); } catch { /* ignore */ }
}
