import { Game } from './Game.js';
import { resolveQuality, saveQualityName, QUALITY_PRESETS } from './config/quality.js';
import { LADA_COLORS } from './vehicles/LadaModel.js';

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);

let quality = resolveQuality();
let colorIndex = 1; // «Вишня»

function buildMenu() {
  const colors = $('colors');
  LADA_COLORS.forEach((c, i) => {
    const el = document.createElement('div');
    el.className = 'swatch' + (i === colorIndex ? ' sel' : '');
    el.style.background = `#${c.hex.toString(16).padStart(6, '0')}`;
    el.title = c.name;
    el.addEventListener('pointerdown', () => {
      colorIndex = i;
      colors.querySelectorAll('.swatch').forEach((s, k) => s.classList.toggle('sel', k === i));
    });
    colors.appendChild(el);
  });
  const qEl = $('quality');
  for (const [name, preset] of Object.entries(QUALITY_PRESETS)) {
    const b = document.createElement('div');
    b.className = 'qbtn' + (name === quality.name ? ' sel' : '');
    b.textContent = preset.label;
    b.addEventListener('pointerdown', () => {
      saveQualityName(name);
      // качество влияет на создание рендерера/геометрии — перезапускаем страницу
      if (name !== quality.name) location.reload();
    });
    qEl.appendChild(b);
  }
}

async function boot() {
  buildMenu();
  $('loading').textContent = `Строим Автозаводский район… (качество: ${quality.label})`;
  await new Promise((r) => requestAnimationFrame(() => setTimeout(r, 0))); // даём отрисоваться меню

  const game = new Game($('game'), quality);
  window.game = game; // для отладки: game.stats в консоли / chrome://inspect
  try {
    game.init({
      carColor: LADA_COLORS[colorIndex].hex,
      startTime: params.has('t') ? parseFloat(params.get('t')) : 12,
    });
  } catch (err) {
    console.error(err);
    $('loading').textContent = `Ошибка инициализации WebGL: ${err.message}`;
    return;
  }
  // первый кадр под меню, чтобы фон был уже виден
  game.renderer.render(game.scene, game.camera);
  $('loading').textContent = 'Готово';
  $('play').disabled = false;

  const startGame = () => {
    game.player.setColor(LADA_COLORS[colorIndex].hex);
    $('start').classList.add('hidden');
    $('hud').classList.remove('hidden');
    $('controls').classList.remove('hidden');
    // полноэкранный режим и альбомная ориентация (в браузере; в APK это делает Activity)
    const el = document.documentElement;
    if (el.requestFullscreen && !document.fullscreenElement) {
      el.requestFullscreen({ navigationUI: 'hide' })
        .then(() => screen.orientation?.lock?.('landscape'))
        .catch(() => {});
    }
    game.start();
  };
  $('play').addEventListener('click', startGame);
  if (params.has('autostart')) startGame();
}

boot();
