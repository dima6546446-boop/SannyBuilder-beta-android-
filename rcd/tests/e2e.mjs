// Сквозной тест в настоящем браузере (Chromium + WebGL): меню → заезд → управление → очки → удар → режимы → гараж → сохранение.
import { startServer, launch } from './lib.mjs';
import fs from 'node:fs';

const srv = await startServer(5201);
const { browser, page, errors } = await launch(srv.url, { width: 960, height: 540 });
fs.mkdirSync('docs/screenshots', { recursive: true });
const sleep = (ms) => page.waitForTimeout(ms);
const shot = (n) => page.screenshot({ path: `docs/screenshots/${n}.png` });
let failed = 0;
const check = (name, ok, info = '') => { console.log((ok ? '  ✔ ' : '  ✘ ') + name + (info ? '  ' + info : '')); if (!ok) failed++; };
const ev = (fn, arg) => page.evaluate(fn, arg);

// быстрый рендер для тестов в программном WebGL
await ev(() => { __rcd.progress.updateSettings({ quality: 'low', pixelRatio: 0.6, hintsSeen: true, showFps: false }); __rcd.applySettings(); __rcd.go('menu', false); });
await sleep(500);
console.log('Меню');
check('главное меню отображается', (await ev(() => document.querySelector('#ui .logo-big')?.textContent)) === 'ЗАНОС');
await shot('01-menu');

console.log('Выбор заезда и старт');
await ev(() => __rcd.act('goPlay')); await sleep(300);
check('экран выбора заезда', !!(await ev(() => document.querySelector('.play-grid'))));
await shot('02-play');
await ev(() => { __rcd.sel.map = 'parking'; __rcd.sel.mode = 'free'; __rcd.act('start'); });
await ev(() => { __rcd.countdown = 0; });
await sleep(800);
check('игра запущена', (await ev(() => __rcd.state)) === 'play');
check('HUD виден', !(await ev(() => document.querySelector('#hud').classList.contains('hidden'))));

console.log('Реальный ввод с клавиатуры');
const z0 = await ev(() => __rcd.session.car.z);
await page.keyboard.down('KeyW'); await page.keyboard.down('KeyD'); await sleep(2500);
const kb = await ev(() => ({ thr: __rcd.controls.state.throttle, steer: __rcd.controls.state.steer, z: __rcd.session.car.z, kmh: __rcd.session.car.speed * 3.6 }));
check('клавиша W даёт газ и машина едет', kb.thr > 0.9 && kb.z > z0 + 0.5, `газ ${kb.thr.toFixed(2)}, ${Math.round(kb.kmh)} км/ч`);
check('клавиша D поворачивает руль плавно', kb.steer > 0.5, 'руль ' + kb.steer.toFixed(2));
await page.keyboard.down('Space'); await sleep(250);
check('пробел включает ручник', await ev(() => __rcd.session.car.input.handbrake === true));
await page.keyboard.up('Space'); await page.keyboard.up('KeyW'); await page.keyboard.up('KeyD'); await sleep(200);
await page.keyboard.press('KeyR'); await sleep(300);
check('R возвращает на старт', Math.abs((await ev(() => __rcd.session.car.z)) - (await ev(() => __rcd.session.map.spawn.z))) < 3);
await ev(() => { __rcd.session.restart(); });

// помощник: шагаем симуляцию напрямую, чтобы не зависеть от FPS программного рендера
const drive = (script, seconds) => ev(([script, seconds]) => {
  const s = __rcd.session; const dt = 1 / 60; const fn = new Function('t', 'c', script);
  for (let i = 0; i < seconds * 60; i++) { const inp = { throttle: 0, brake: 0, steer: 0, handbrake: false, kick: false }; fn(i * dt, inp); s.update(dt, inp); }
  const c = s.car; return { kmh: c.speed * 3.6, beta: c.driftAngle * 57.3, score: s.scoring.total, banked: s.scoring.banked, chain: s.scoring.chain, hits: s.scoring.stats.hits, mult: s.scoring.mult, x: c.x, z: c.z, gear: c.gear, rpm: c.rpm };
}, [script, seconds]);

console.log('Разгон');
let r = await drive('c.throttle = 1; c.steer = 0;', 4);
check('машина разгоняется', r.kmh > 50, Math.round(r.kmh) + ' км/ч');
console.log('Вход в занос');
r = await drive('c.throttle = t < 0.3 ? 0.4 : 0.9; c.steer = t < 0.9 ? -0.5 : -0.3; c.handbrake = t > 0.2 && t < 0.55;', 1.6);
check('появился угол заноса > 20°', Math.abs(r.beta) > 20, Math.round(r.beta) + '°');
r = await drive('c.throttle = 0.85; c.steer = -0.35;', 2.5);
check('набираются очки серии', r.score > 50, 'очки ' + r.score + ', множитель ×' + r.mult.toFixed(2));
await ev(() => { __rcd.hud.hintT = 0; });
await sleep(300);
await shot('10-game-drift');
r = await drive('c.throttle = 0.3; c.steer = 0;', 2.5);
check('серия засчитана после выхода из заноса', r.banked > 50, 'засчитано ' + Math.round(r.banked));
console.log('Удар сжигает серию');
const chainBefore = r.chain + r.banked;
r = await drive('c.throttle = 1; c.steer = 0;', 0.01);
await ev(() => { const c = __rcd.session.car; c.reset(0, 70, 0); c.vz = 25; });
r = await drive('c.throttle = 1;', 2.5);
check('удар зафиксирован', r.hits > 0, 'ударов ' + r.hits);
check('серия обнулена', r.chain < 30, 'серия ' + Math.round(r.chain));
await shot('11-after-hit');

console.log('Пауза, настройки, камера');
await page.keyboard.press('Escape'); await sleep(400);
check('пауза открывается по Esc', (await ev(() => __rcd.state)) === 'pause');
await shot('12-pause');
await ev(() => __rcd.act('pauseSettings')); await sleep(300);
check('настройки доступны из паузы', !!(await ev(() => document.querySelector('.settings'))));
await shot('13-settings');
await ev(() => __rcd.act('back')); await sleep(200);
await page.keyboard.press('Escape'); await sleep(300);
check('возврат в игру', (await ev(() => __rcd.state)) === 'play');
const cams = [];
for (let i = 0; i < 6; i++) { cams.push(await ev(() => { __rcd.cycleCamera(); return __rcd.gfx.rig.mode; })); await sleep(120); }
check('6 режимов камеры переключаются', new Set(cams).size === 6, cams.join(','));
await ev(() => { __rcd.gfx.rig.setMode('chase'); });

console.log('Результаты и награда');
const moneyBefore = await ev(() => __rcd.progress.money);
await ev(() => __rcd.act('finish')); await sleep(400);
check('экран результатов', (await ev(() => __rcd.state)) === 'results');
const res = await ev(() => ({ money: __rcd.progress.money, score: __rcd.result.summary.score, rew: __rcd.result.reward.money }));
check('деньги начислены', res.money > moneyBefore, `+${res.money - moneyBefore} ₽ за ${res.score} очков`);
await shot('14-results');

console.log('Режим на время');
await ev(() => { __rcd.act('toPlay'); __rcd.sel.mode = 'timed'; __rcd.sel.map = 'city'; __rcd.act('start'); __rcd.countdown = 0; });
await sleep(500);
check('режим на время запущен', (await ev(() => __rcd.session.mode)) === 'timed');
r = await drive('c.throttle = 0.6;', 1);
await ev(() => { const s = __rcd.session; for (let i = 0; i < 60 * 130 && !s.finished; i++) s.update(1 / 60, { throttle: 0 }); });
await sleep(500);
check('заезд завершился по таймеру', (await ev(() => __rcd.state)) === 'results');
await shot('15-timed-results');

console.log('Испытания');
await ev(() => { __rcd.act('toPlay'); __rcd.sel.mode = 'challenge'; __rcd.sel.map = 'parking'; __rcd.sel.challenge = 'park-1'; __rcd.render(); });
await sleep(300);
await shot('16-challenges');
await ev(() => { __rcd.act('start'); __rcd.countdown = 0; });
await sleep(300);
check('испытание запущено', (await ev(() => __rcd.session.challenge?.id)) === 'park-1');
await ev(() => __rcd.act('quit'));

console.log('Гараж, покупка, тюнинг, сохранение');
await ev(() => { __rcd.progress.data.money = 200000; __rcd.progress.addXp(5000); __rcd.progress.save(); __rcd.go('menu', false); __rcd.act('goGarage'); });
await sleep(300);
await ev(() => __rcd.act('garageCar', { id: 'pyaterka' })); await sleep(300);
await shot('17-garage-buy');
await ev(() => __rcd.act('buyCar', { id: 'pyaterka' }));
check('машина куплена', await ev(() => __rcd.progress.owns('pyaterka')));
await ev(() => __rcd.act('garageTab', { id: 'tune' })); await ev(() => { __rcd.act('upgrade', { id: 'engine' }); __rcd.act('upgrade', { id: 'tires' }); });
check('тюнинг применён', (await ev(() => __rcd.progress.carState('pyaterka').tuning.engine)) === 1);
await sleep(300); await shot('18-tune');
await ev(() => __rcd.act('garageTab', { id: 'look' })); await ev(() => { __rcd.act('paint', { id: '#2d5fb0' }); __rcd.act('setPart', { slot: 'wheels', id: 'star5' }); __rcd.act('setPart', { slot: 'spoiler', id: 'gt' }); __rcd.act('setPart', { slot: 'bodykit', id: 'wide' }); __rcd.act('setPart', { slot: 'neon', id: 'blue' }); });
await sleep(500); await shot('19-look');
const snap = await ev(() => ({ money: __rcd.progress.money, paint: __rcd.progress.carState('pyaterka').paint, level: __rcd.progress.level }));
console.log('Перезагрузка страницы (проверка сохранения)');
await page.reload(); await page.waitForFunction(() => window.__rcd); await sleep(600);
const after = await ev(() => ({ money: __rcd.progress.money, paint: __rcd.progress.carState('pyaterka')?.paint, level: __rcd.progress.level, owns: __rcd.progress.owns('pyaterka'), eng: __rcd.progress.carState('pyaterka')?.tuning.engine }));
check('прогресс сохранён после перезагрузки', after.owns && after.money === snap.money && after.paint === snap.paint && after.level === snap.level && after.eng === 1, JSON.stringify(after));

console.log('Другие карты и погода');
for (const [map, weather, time] of [['city', 'rain', 'dusk'], ['industrial', 'clear', 'day'], ['mountain', 'snow', 'day']]) {
  await ev(([m, w, t]) => { __rcd.go('menu', false); __rcd.sel.car = 'pyaterka'; __rcd.sel.map = m; __rcd.sel.mode = 'free'; __rcd.sel.weather = w; __rcd.sel.time = t; __rcd.act('start'); __rcd.countdown = 0; __rcd.progress.updateSettings({ quality: 'medium' }); __rcd.applySettings(); }, [map, weather, time]);
  await sleep(600);
  await drive('c.throttle = 1;', 2.5);
  await drive('c.throttle = 0.9; c.steer = -0.45; c.handbrake = t > 0.2 && t < 0.5;', 2);
  await sleep(500);
  await shot(`20-map-${map}-${weather}`);
  check(`карта ${map} (${weather}) без ошибок отрисовки`, (await ev(() => !__rcd.fatal)));
  await ev(() => __rcd.act('quit'));
}

await browser.close(); srv.stop();
const real = errors.filter((e) => !e.includes('favicon'));
check('нет ошибок в консоли браузера', real.length === 0, real.slice(0, 3).join(' | '));
console.log(failed ? `\nПровалено проверок: ${failed}` : '\nВсе проверки пройдены');
process.exit(failed ? 1 : 0);
