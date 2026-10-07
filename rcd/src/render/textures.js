import * as THREE from 'three';

// Процедурные текстуры (canvas). Никаких внешних файлов, нет защищённых ассетов.
const cache = new Map();
function make(key, w, h, draw, opts = {}) {
  if (cache.has(key)) return cache.get(key);
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  const g = c.getContext('2d'); draw(g, w, h);
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = opts.clamp ? THREE.ClampToEdgeWrapping : THREE.RepeatWrapping;
  t.colorSpace = opts.linear ? THREE.NoColorSpace : THREE.SRGBColorSpace;
  t.anisotropy = 8;
  cache.set(key, t);
  return t;
}
function speckle(g, w, h, base, n, spread, alpha = 0.5, size = 1.4) {
  g.fillStyle = base; g.fillRect(0, 0, w, h);
  for (let i = 0; i < n; i++) {
    const v = Math.floor(128 + (Math.random() - 0.5) * spread);
    g.fillStyle = `rgba(${v},${v},${v},${alpha})`;
    g.fillRect(Math.random() * w, Math.random() * h, size, size);
  }
}

export function surfaceTexture(type) {
  switch (type) {
    case 'asphalt': return make('asphalt', 256, 256, (g, w, h) => { speckle(g, w, h, '#3a3c40', 5000, 90, 0.45); g.strokeStyle = 'rgba(20,20,22,0.35)'; for (let i = 0; i < 6; i++) { g.beginPath(); g.moveTo(Math.random() * w, Math.random() * h); g.lineTo(Math.random() * w, Math.random() * h); g.stroke(); } });
    case 'concrete': return make('concrete', 256, 256, (g, w, h) => { speckle(g, w, h, '#8d8f90', 4000, 50, 0.4); g.strokeStyle = 'rgba(60,60,60,0.5)'; g.lineWidth = 2; g.strokeRect(0, 0, w, h); });
    case 'grass': return make('grass', 256, 256, (g, w, h) => { speckle(g, w, h, '#3e6a34', 6000, 80, 0.5); for (let i = 0; i < 1200; i++) { g.fillStyle = `rgba(${40 + Math.random() * 40},${100 + Math.random() * 60},${30 + Math.random() * 30},0.6)`; g.fillRect(Math.random() * w, Math.random() * h, 1.5, 3); } });
    case 'gravel': return make('gravel', 256, 256, (g, w, h) => { speckle(g, w, h, '#8a8174', 1500, 60, 0.5, 2); for (let i = 0; i < 2500; i++) { const v = 90 + Math.random() * 100; g.fillStyle = `rgb(${v},${v - 6},${v - 14})`; g.beginPath(); g.arc(Math.random() * w, Math.random() * h, 1 + Math.random() * 1.6, 0, 6.3); g.fill(); } });
    case 'dirt': return make('dirt', 256, 256, (g, w, h) => { speckle(g, w, h, '#6b5238', 4000, 60, 0.5, 2); });
    case 'wet': return make('wet', 256, 256, (g, w, h) => { speckle(g, w, h, '#26292d', 3500, 40, 0.4); });
    case 'snow': return make('snow', 256, 256, (g, w, h) => { speckle(g, w, h, '#e8eef2', 2500, 30, 0.35); });
    default: return surfaceTexture('asphalt');
  }
}

/** Фасад здания с окнами; emissive-версия для ночи. */
export function facadeTextures(style) {
  const key = 'facade-' + style;
  const map = make(key, 128, 256, (g, w, h) => {
    g.fillStyle = '#b8bcc4'; g.fillRect(0, 0, w, h);
    const cols = style === 'warehouse' ? 2 : 4, rows = style === 'warehouse' ? 2 : 8;
    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
      g.fillStyle = style === 'mall' ? '#2b3a4a' : '#44566a';
      g.fillRect((c + 0.15) * w / cols, (r + 0.2) * h / rows, w / cols * 0.7, h / rows * 0.55);
    }
  });
  const em = make(key + '-em', 128, 256, (g, w, h) => {
    g.fillStyle = '#000'; g.fillRect(0, 0, w, h);
    const cols = style === 'warehouse' ? 2 : 4, rows = style === 'warehouse' ? 2 : 8;
    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
      if (Math.random() < 0.5) { g.fillStyle = Math.random() < 0.7 ? '#ffd98a' : '#bfe0ff'; g.fillRect((c + 0.15) * w / cols, (r + 0.2) * h / rows, w / cols * 0.7, h / rows * 0.55); }
    }
  });
  return { map, em };
}

export function spriteTexture(kind) {
  return make('sprite-' + kind, 64, 64, (g, w, h) => {
    const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
    if (kind === 'smoke') { grd.addColorStop(0, 'rgba(255,255,255,0.9)'); grd.addColorStop(0.5, 'rgba(255,255,255,0.35)'); grd.addColorStop(1, 'rgba(255,255,255,0)'); }
    else { grd.addColorStop(0, 'rgba(255,255,255,1)'); grd.addColorStop(0.3, 'rgba(255,220,150,0.8)'); grd.addColorStop(1, 'rgba(255,160,40,0)'); }
    g.fillStyle = grd; g.fillRect(0, 0, w, h);
  }, { clamp: true });
}

export function glowTexture() {
  return make('glow', 128, 128, (g, w, h) => { const grd = g.createRadialGradient(64, 64, 0, 64, 64, 64); grd.addColorStop(0, 'rgba(255,230,170,0.9)'); grd.addColorStop(0.35, 'rgba(255,200,120,0.25)'); grd.addColorStop(1, 'rgba(255,200,120,0)'); g.fillStyle = grd; g.fillRect(0, 0, w, h); }, { clamp: true });
}

export function tyreTexture() {
  return make('tyre', 64, 64, (g, w, h) => { g.fillStyle = '#17181a'; g.fillRect(0, 0, w, h); for (let i = 0; i < 8; i++) { g.fillStyle = '#26282b'; g.fillRect(0, i * 8, w, 2); } });
}
