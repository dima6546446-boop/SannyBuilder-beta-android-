/**
 * Пресеты качества под мобильные GPU.
 * pixelRatio — абсолютный множитель рендера относительно CSS-пикселей
 * (у телефонов devicePixelRatio 2.5–3.5 — рендерить в нативном разрешении нельзя).
 */
export const QUALITY_PRESETS = {
  low: {
    name: 'low', label: 'Низкое',
    pixelRatio: 0.75, maxPixelRatio: 1.0, minPixelRatio: 0.5,
    antialias: false, precision: 'mediump',
    shadows: false, shadowMapSize: 512, shadowUpdateEvery: 0, shadowRange: 30,
    trafficCount: 10, drawDistance: 220, carLodDistance: 45,
    treeDensity: 0.45, treeLodDistance: 60,
    carEnvMap: false, transparentGlass: false, streetLightPools: false,
    anisotropy: 1, occlusion: true,
  },
  medium: {
    name: 'medium', label: 'Среднее',
    pixelRatio: 1.0, maxPixelRatio: 1.25, minPixelRatio: 0.6,
    antialias: false, precision: 'highp',
    shadows: true, shadowMapSize: 1024, shadowUpdateEvery: 2, shadowRange: 40,
    trafficCount: 18, drawDistance: 320, carLodDistance: 70,
    treeDensity: 0.7, treeLodDistance: 90,
    carEnvMap: true, transparentGlass: true, streetLightPools: true,
    anisotropy: 2, occlusion: true,
  },
  high: {
    name: 'high', label: 'Высокое',
    pixelRatio: 1.5, maxPixelRatio: 2.0, minPixelRatio: 0.8,
    antialias: true, precision: 'highp',
    shadows: true, shadowMapSize: 2048, shadowUpdateEvery: 1, shadowRange: 55,
    trafficCount: 28, drawDistance: 450, carLodDistance: 100,
    treeDensity: 1.0, treeLodDistance: 130,
    carEnvMap: true, transparentGlass: true, streetLightPools: true,
    anisotropy: 4, occlusion: true,
  },
};

const STORAGE_KEY = 'autovaz.quality';

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
  if (params.has('traffic')) q.trafficCount = Math.max(0, parseInt(params.get('traffic'), 10) || 0);
  return q;
}

export function saveQualityName(name) {
  try { localStorage.setItem(STORAGE_KEY, name); } catch { /* ignore */ }
}
