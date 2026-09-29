import * as THREE from 'three';
import { makeNoise2D, mulberry32, clamp } from '../utils/math.js';

/**
 * Процедурные текстуры (генерируются на canvas при загрузке).
 * Плюсы для мобильной сборки: 0 байт в APK, степень двойки (mipmaps),
 * малые разрешения (256–512). Бюджет видеопамяти всех текстур ≈ 6 МБ.
 *
 * Для реальных художественных ассетов используйте KTX2 (Basis Universal) —
 * см. loadCompressedTexture() ниже: на GPU они остаются сжатыми (ETC2/ASTC),
 * экономя 4–8× памяти и полосы пропускания.
 */

function makeCanvas(size, h = size) {
  const c = document.createElement('canvas');
  c.width = size; c.height = h;
  return [c, c.getContext('2d')];
}

function toTexture(canvas, { repeat = true, srgb = true, aniso = 1, mips = true } = {}) {
  const t = new THREE.CanvasTexture(canvas);
  if (repeat) t.wrapS = t.wrapT = THREE.RepeatWrapping;
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = aniso;
  t.generateMipmaps = mips;
  t.minFilter = mips ? THREE.LinearMipmapLinearFilter : THREE.LinearFilter;
  t.magFilter = THREE.LinearFilter;
  return t;
}

function pixelFill(ctx, size, fn) {
  const img = ctx.getImageData(0, 0, size, size);
  const d = img.data;
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const i = (y * size + x) * 4;
      const [r, g, b] = fn(x, y, d[i], d[i + 1], d[i + 2]);
      d[i] = clamp(r, 0, 255); d[i + 1] = clamp(g, 0, 255); d[i + 2] = clamp(b, 0, 255); d[i + 3] = 255;
    }
  }
  ctx.putImageData(img, 0, 0);
}

/** Тайлящийся шум: сэмплируем по тору, чтобы не было швов. */
function tileNoise(noise, x, y, size, scale, seedOff = 0) {
  const u = x / size, v = y / size;
  const a = noise.fbm(u * scale + seedOff, v * scale, 3);
  const b = noise.fbm((u - 1) * scale + seedOff, v * scale, 3);
  const c = noise.fbm(u * scale + seedOff, (v - 1) * scale, 3);
  const d = noise.fbm((u - 1) * scale + seedOff, (v - 1) * scale, 3);
  return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v;
}

export function createTextures(aniso = 1) {
  const noise = makeNoise2D(1337);
  const rnd = mulberry32(99);
  const T = {};

  // --- Асфальт 256² (тайл 8 м) ---
  {
    const S = 256; const [c, ctx] = makeCanvas(S);
    pixelFill(ctx, S, (x, y) => {
      const big = tileNoise(noise, x, y, S, 4) - 0.5;
      const grain = (rnd() - 0.5) * 34;
      const v = 72 + big * 26 + grain;
      return [v, v, v + 3];
    });
    ctx.strokeStyle = 'rgba(25,25,25,0.55)'; ctx.lineWidth = 1.2;
    for (let k = 0; k < 5; k++) { // трещины
      let x = rnd() * S, y = rnd() * S;
      ctx.beginPath(); ctx.moveTo(x, y);
      for (let s = 0; s < 14; s++) { x += (rnd() - 0.5) * 18; y += (rnd() - 0.5) * 18; ctx.lineTo(x, y); }
      ctx.stroke();
    }
    T.asphalt = toTexture(c, { aniso });
  }

  // --- Трава 256² ---
  {
    const S = 256; const [c, ctx] = makeCanvas(S);
    pixelFill(ctx, S, (x, y) => {
      const n = tileNoise(noise, x, y, S, 6, 50);
      const g = (rnd() - 0.5) * 40;
      return [60 + n * 40 + g * 0.4, 105 + n * 45 + g, 40 + n * 15 + g * 0.3];
    });
    T.grass = toTexture(c, { aniso });
  }

  // --- Тротуарная плитка / бетон 256² (тайл 2 м, 4 плиты) ---
  {
    const S = 256; const [c, ctx] = makeCanvas(S);
    pixelFill(ctx, S, (x, y) => {
      const n = tileNoise(noise, x, y, S, 8, 100);
      const v = 150 + n * 30 + (rnd() - 0.5) * 22;
      return [v, v - 2, v - 6];
    });
    ctx.strokeStyle = 'rgba(70,70,70,0.8)'; ctx.lineWidth = 3;
    for (let i = 0; i <= 2; i++) {
      ctx.beginPath(); ctx.moveTo(0, i * 128); ctx.lineTo(S, i * 128); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(i * 128, 0); ctx.lineTo(i * 128, S); ctx.stroke();
    }
    T.concrete = toTexture(c, { aniso });
  }

  // --- Бетонный забор ПО-2 (ромбический рельеф) 256² ---
  {
    const S = 256; const [c, ctx] = makeCanvas(S);
    pixelFill(ctx, S, (x, y) => {
      const n = tileNoise(noise, x, y, S, 5, 200);
      const dx = Math.abs(((x % 32) - 16)), dy = Math.abs(((y % 32) - 16));
      const diamond = (dx + dy) < 12 ? 16 : 0;
      const v = 140 + n * 30 + diamond + (rnd() - 0.5) * 16;
      return [v, v, v - 4];
    });
    ctx.fillStyle = 'rgba(40,40,40,0.9)'; ctx.fillRect(0, 0, 3, S);
    T.fence = toTexture(c, { aniso });
  }

  // --- Фасад панельного дома 512² = 8×8 окон (24×24 м), + карта свечения окон ---
  {
    const S = 512, cell = 64;
    const [c, ctx] = makeCanvas(S);
    const [e, ectx] = makeCanvas(S);
    pixelFill(ctx, S, (x, y) => {
      const n = tileNoise(noise, x, y, S, 10, 300);
      const v = 205 + n * 28 + (rnd() - 0.5) * 10;
      return [v, v - 3, v - 8];
    });
    ectx.fillStyle = '#000'; ectx.fillRect(0, 0, S, S);
    for (let gy = 0; gy < 8; gy++) {
      for (let gx = 0; gx < 8; gx++) {
        const x0 = gx * cell, y0 = gy * cell;
        // швы панелей
        ctx.fillStyle = 'rgba(90,90,90,0.55)';
        ctx.fillRect(x0, y0, cell, 2); ctx.fillRect(x0, y0, 2, cell);
        // окно с белой рамой
        const wx = x0 + 14, wy = y0 + 16, ww = 36, wh = 30;
        ctx.fillStyle = '#e9e9e4'; ctx.fillRect(wx - 3, wy - 3, ww + 6, wh + 6);
        const glass = 40 + rnd() * 30;
        ctx.fillStyle = `rgb(${glass},${glass + 12},${glass + 26})`; ctx.fillRect(wx, wy, ww, wh);
        ctx.fillStyle = '#e9e9e4'; ctx.fillRect(wx + ww / 2 - 1, wy, 3, wh); ctx.fillRect(wx, wy + 9, ww / 2, 2);
        if (rnd() < 0.3) { // занавески
          ctx.fillStyle = `rgba(${150 + rnd() * 100},${120 + rnd() * 80},${90 + rnd() * 60},0.6)`;
          ctx.fillRect(wx, wy, ww * 0.3, wh);
        }
        if (gx % 2 === 1 && rnd() < 0.5) { // балкон
          ctx.fillStyle = 'rgba(160,160,155,0.95)'; ctx.fillRect(x0 + 8, y0 + 44, 48, 16);
          ctx.fillStyle = 'rgba(90,90,90,0.8)'; ctx.fillRect(x0 + 8, y0 + 44, 48, 2);
        }
        // свечение окон ночью (~40% окон)
        if (rnd() < 0.4) {
          const warm = rnd();
          const col = warm < 0.7 ? `rgb(255,${190 + rnd() * 40},${110 + rnd() * 40})`
            : warm < 0.9 ? 'rgb(255,240,210)' : 'rgb(140,170,255)'; // телевизор
          ectx.fillStyle = col; ectx.fillRect(wx, wy, ww, wh);
          ectx.fillStyle = '#000'; ectx.fillRect(wx + ww / 2 - 1, wy, 3, wh);
        }
      }
    }
    T.facade = toTexture(c, { aniso });
    T.facadeEmissive = toTexture(e, { aniso });
  }

  // --- Радиальное свечение (фары, фонари) 64² ---
  {
    const S = 64; const [c, ctx] = makeCanvas(S);
    const g = ctx.createRadialGradient(S / 2, S / 2, 0, S / 2, S / 2, S / 2);
    g.addColorStop(0, 'rgba(255,255,255,1)');
    g.addColorStop(0.35, 'rgba(255,255,255,0.45)');
    g.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = g; ctx.fillRect(0, 0, S, S);
    T.glow = toTexture(c, { repeat: false });
  }

  // --- Blob-тень под машинами 64² (дешёвая альтернатива shadow map) ---
  {
    const S = 64; const [c, ctx] = makeCanvas(S);
    const g = ctx.createRadialGradient(S / 2, S / 2, 4, S / 2, S / 2, S / 2);
    g.addColorStop(0, 'rgba(0,0,0,0.85)');
    g.addColorStop(0.6, 'rgba(0,0,0,0.5)');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = g; ctx.fillRect(0, 0, S, S);
    T.blob = toTexture(c, { repeat: false, srgb: false });
  }

  // --- Решётка радиатора ВАЗ-2107 128×32 ---
  {
    const [c, ctx] = makeCanvas(128, 32);
    ctx.fillStyle = '#111'; ctx.fillRect(0, 0, 128, 32);
    ctx.fillStyle = '#cfcfcf';
    for (let x = 2; x < 128; x += 6) ctx.fillRect(x, 0, 2, 32);
    ctx.fillRect(0, 0, 128, 3); ctx.fillRect(0, 29, 128, 3);
    ctx.fillRect(0, 0, 3, 32); ctx.fillRect(125, 0, 3, 32);
    ctx.fillStyle = '#b22'; ctx.fillRect(56, 11, 16, 10); // шильдик
    T.grille = toTexture(c, { repeat: false });
  }

  // --- Номерной знак 256×64 ---
  {
    const [c, ctx] = makeCanvas(256, 64);
    ctx.fillStyle = '#f4f4f4'; ctx.fillRect(0, 0, 256, 64);
    ctx.strokeStyle = '#111'; ctx.lineWidth = 4; ctx.strokeRect(3, 3, 250, 58);
    ctx.beginPath(); ctx.moveTo(196, 3); ctx.lineTo(196, 61); ctx.stroke();
    ctx.fillStyle = '#111'; ctx.font = 'bold 40px sans-serif'; ctx.textBaseline = 'middle';
    ctx.fillText('В 107 АЗ', 12, 34);
    ctx.font = 'bold 24px sans-serif'; ctx.fillText('63', 208, 26);
    ctx.fillStyle = '#fff'; ctx.fillRect(210, 44, 30, 4);
    ctx.fillStyle = '#1a47c2'; ctx.fillRect(210, 48, 30, 4);
    ctx.fillStyle = '#d22'; ctx.fillRect(210, 52, 30, 4);
    T.plate = toTexture(c, { repeat: false });
  }

  // --- Дорожные знаки: атлас 4×2 по 128 px ---
  {
    const [c, ctx] = makeCanvas(512, 256);
    ctx.clearRect(0, 0, 512, 256);
    const cell = (i) => [(i % 4) * 128, Math.floor(i / 4) * 128];
    const blueSq = (x, y) => { ctx.fillStyle = '#fff'; ctx.fillRect(x + 6, y + 6, 116, 116); ctx.fillStyle = '#1f4fb0'; ctx.fillRect(x + 12, y + 12, 104, 104); };
    // 0 — пешеходный переход (5.19)
    let [x, y] = cell(0); blueSq(x, y);
    ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.moveTo(x + 64, y + 22); ctx.lineTo(x + 108, y + 104); ctx.lineTo(x + 20, y + 104); ctx.closePath(); ctx.fill();
    ctx.fillStyle = '#111'; ctx.beginPath(); ctx.arc(x + 66, y + 50, 7, 0, 7); ctx.fill();
    ctx.lineWidth = 6; ctx.strokeStyle = '#111'; ctx.beginPath(); ctx.moveTo(x + 64, y + 58); ctx.lineTo(x + 60, y + 78); ctx.lineTo(x + 48, y + 96); ctx.moveTo(x + 60, y + 78); ctx.lineTo(x + 72, y + 96); ctx.moveTo(x + 50, y + 70); ctx.lineTo(x + 78, y + 64); ctx.stroke();
    // 1 — ограничение 60
    [x, y] = cell(1);
    ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(x + 64, y + 64, 58, 0, 7); ctx.fill();
    ctx.lineWidth = 14; ctx.strokeStyle = '#d42020'; ctx.beginPath(); ctx.arc(x + 64, y + 64, 50, 0, 7); ctx.stroke();
    ctx.fillStyle = '#111'; ctx.font = 'bold 52px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText('60', x + 64, y + 67);
    // 2 — остановка «А»
    [x, y] = cell(2); blueSq(x, y);
    ctx.fillStyle = '#fff'; ctx.fillRect(x + 28, y + 28, 72, 72); ctx.fillStyle = '#111'; ctx.font = 'bold 64px sans-serif'; ctx.fillText('А', x + 64, y + 68);
    // 3 — ДПС
    [x, y] = cell(3); ctx.fillStyle = '#fff'; ctx.fillRect(x + 4, y + 30, 120, 68); ctx.fillStyle = '#1b3c9e'; ctx.fillRect(x + 8, y + 34, 112, 60);
    ctx.fillStyle = '#fff'; ctx.font = 'bold 44px sans-serif'; ctx.fillText('ДПС', x + 64, y + 66);
    // 4 — АЗС
    [x, y] = cell(4); blueSq(x, y); ctx.fillStyle = '#fff'; ctx.fillRect(x + 30, y + 26, 40, 76); ctx.fillStyle = '#1f4fb0'; ctx.fillRect(x + 36, y + 34, 28, 22);
    ctx.fillStyle = '#fff'; ctx.fillRect(x + 76, y + 40, 8, 50); ctx.fillRect(x + 70, y + 36, 14, 8);
    // 5 — парковка P
    [x, y] = cell(5); blueSq(x, y); ctx.fillStyle = '#fff'; ctx.font = 'bold 86px sans-serif'; ctx.fillText('P', x + 64, y + 70);
    // 6 — камера
    [x, y] = cell(6); blueSq(x, y); ctx.fillStyle = '#fff'; ctx.fillRect(x + 26, y + 46, 56, 40); ctx.beginPath(); ctx.moveTo(x + 82, y + 56); ctx.lineTo(x + 104, y + 44); ctx.lineTo(x + 104, y + 88); ctx.lineTo(x + 82, y + 76); ctx.fill();
    // 7 — ДОСААФ
    [x, y] = cell(7); ctx.fillStyle = '#b71c1c'; ctx.fillRect(x + 2, y + 34, 124, 60); ctx.fillStyle = '#ffd54a'; ctx.font = 'bold 26px sans-serif'; ctx.fillText('ДОСААФ', x + 64, y + 66);
    T.signs = toTexture(c, { repeat: false, aniso });
  }

  // --- Вывески магазинов: 8 строк 512×64 ---
  {
    const [c, ctx] = makeCanvas(512, 512);
    const shops = [
      ['ПРОДУКТЫ', '#1b5e20', '#fff'], ['АПТЕКА', '#fff', '#2e7d32'], ['ХЛЕБ', '#6d4c41', '#ffe082'],
      ['ШИНОМОНТАЖ 24', '#212121', '#ffca28'], ['АВТОЗАПЧАСТИ ВАЗ', '#0d47a1', '#fff'], ['ПОЧТА', '#1565c0', '#fff'],
      ['КАФЕ «ЛАДА»', '#b71c1c', '#fff59d'], ['ГАСТРОНОМ', '#4a148c', '#fff'],
    ];
    shops.forEach(([t, bg, fg], i) => {
      ctx.fillStyle = bg; ctx.fillRect(0, i * 64, 512, 64);
      ctx.fillStyle = 'rgba(0,0,0,.25)'; ctx.fillRect(0, i * 64 + 58, 512, 6);
      ctx.fillStyle = fg; ctx.font = 'bold 42px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillText(t, 256, i * 64 + 32);
    });
    T.shops = toTexture(c, { repeat: false, aniso });
  }

  // --- Рекламные щиты 1024×512: 4 плаката 512×256 ---
  {
    const [c, ctx] = makeCanvas(1024, 512);
    const ads = [
      ['РЕМОНТ ЖИГУЛЕЙ', 'недорого · 8-900-107-21-07', '#ff6f00', '#1a1a1a'],
      ['ЛАДА ПАРКИНГ', 'сдай на права с первого раза!', '#0d47a1', '#ffffff'],
      ['АВТОШКОЛА «КЛАКСОН»', 'категория B · ДОСААФ', '#c62828', '#ffffff'],
      ['КУПЛЮ ВАШЕ ВЕДРО', 'в любом состоянии', '#2e7d32', '#ffffff'],
    ];
    ads.forEach(([t, sub, bg, fg], i) => {
      const x = (i % 2) * 512, y = Math.floor(i / 2) * 256;
      const g = ctx.createLinearGradient(x, y, x + 512, y + 256); g.addColorStop(0, bg); g.addColorStop(1, '#111');
      ctx.fillStyle = g; ctx.fillRect(x, y, 512, 256);
      ctx.fillStyle = fg; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.font = 'bold 52px sans-serif'; ctx.fillText(t, x + 256, y + 100, 480);
      ctx.font = 'bold 28px sans-serif'; ctx.fillText(sub, x + 256, y + 170, 480);
      ctx.strokeStyle = 'rgba(255,255,255,.6)'; ctx.lineWidth = 6; ctx.strokeRect(x + 8, y + 8, 496, 240);
    });
    T.ads = toTexture(c, { repeat: false, aniso });
  }

  // --- Нормали асфальта (для «Высокого»): из высоты = шум зерна ---
  {
    const S = 256; const [c, ctx] = makeCanvas(S);
    const hgt = new Float32Array(S * S);
    for (let i = 0; i < S * S; i++) hgt[i] = rnd();
    pixelFill(ctx, S, (x, y) => {
      const hL = hgt[y * S + ((x + S - 1) % S)], hR = hgt[y * S + ((x + 1) % S)];
      const hU = hgt[((y + S - 1) % S) * S + x], hD = hgt[((y + 1) % S) * S + x];
      const nx = (hL - hR) * 0.9, ny = (hU - hD) * 0.9;
      return [128 + nx * 127, 128 + ny * 127, 255];
    });
    T.asphaltNormal = toTexture(c, { srgb: false, aniso });
  }

  // --- Кирпич гаражной стены (для 3D-гаража в меню) ---
  {
    const S = 256; const [c, ctx] = makeCanvas(S);
    ctx.fillStyle = '#6b4a3a'; ctx.fillRect(0, 0, S, S);
    for (let row = 0; row < 16; row++) {
      for (let col = 0; col < 5; col++) {
        const off = row % 2 ? 25 : 0;
        const v = 120 + rnd() * 50;
        ctx.fillStyle = `rgb(${v},${v * 0.55},${v * 0.42})`;
        ctx.fillRect(col * 51 + off - 25, row * 16 + 1, 49, 14);
        ctx.fillRect(col * 51 + off + 230, row * 16 + 1, 49, 14);
      }
    }
    T.brick = toTexture(c, { aniso });
  }

  return T;
}

/**
 * Загрузка сжатой текстуры KTX2 (Basis Universal → ETC2/ASTC на GPU).
 * Транскодер копируется командой `npm run copy-basis` в public/basis/.
 * Конвертация: toktx --t2 --encode etc1s --genmipmap asphalt.ktx2 asphalt.png
 */
let ktx2Loader = null;
export async function loadCompressedTexture(renderer, url, { srgb = true, repeat = true } = {}) {
  if (!ktx2Loader) {
    const { KTX2Loader } = await import('three/addons/loaders/KTX2Loader.js');
    ktx2Loader = new KTX2Loader().setTranscoderPath('./basis/').detectSupport(renderer);
  }
  const tex = await ktx2Loader.loadAsync(url);
  if (srgb) tex.colorSpace = THREE.SRGBColorSpace;
  if (repeat) tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
  return tex;
}
