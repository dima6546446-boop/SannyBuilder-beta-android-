// Дрифт в игре: режим «Дрифт-зона ДОСААФ» (заезд, конусы, итог, рекорд в Save) и дрифт в свободной
// езде (серия → рубли и задание дня). node drift3d.mjs /tmp/shots
import { chromium } from 'playwright';
const [,, out = '.'] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 800, height: 360 } });
const errs = [];
page.on('pageerror', (e) => errs.push('pageerror: ' + e.message + ' ' + (e.stack || '').split('\n').slice(0, 3).join(' | ')));
page.on('console', (m) => { if (m.type() === 'error') errs.push('console: ' + m.text()); });
await page.goto('http://localhost:4173/?autostart&nogov&q=medium');
await page.waitForFunction(() => window.app && window.game && window.game.player, null, { timeout: 120000 });
await page.evaluate(() => {
  const a = window.app, g = a.game; g.renderer.setAnimationLoop(null);
  a.save.reset();
  a.ctl = {};
  g.input.update = function () { Object.assign(this, { steer: 0, throttle: 0, brake: 0, handbrake: false, horn: false }, a.ctl); };
  a.step = (sec, ctl = {}) => { a.ctl = ctl; for (let i = 0; i < sec * 30; i++) g.tick(1 / 30); g.render(0); };
  // «пилот»: разгон → срыв ручником влево → газ и контрруль держат ~30°
  a.driftRun = (sec, dir = -1) => {
    const p = g.player.physics, log = [];
    a.step(3.2, { throttle: 1 });
    a.step(0.45, { steer: dir, handbrake: true, throttle: 0.3 });
    for (let i = 0; i < sec * 30; i++) {
      const ang = p.driftAngle * 57.3;
      a.ctl = { throttle: Math.min(1, Math.max(0.4, 0.8 + (30 - Math.abs(ang)) * 0.02)), steer: Math.sign(ang) * Math.max(-0.6, Math.min(0.6, (Math.abs(ang) - 30) * 0.03)) };
      g.tick(1 / 30);
      if (i % 15 === 0) log.push([+(i / 30).toFixed(1), Math.round(p.speed * 3.6), Math.round(ang), Math.round(g.mode.drift.view.pts), g.mode.drift.view.mult]);
    }
    g.render(0);
    return log;
  };
});
const shot = (n) => page.screenshot({ path: `${out}/${n}.png` });

// --- дрифт-зона
const zone = await page.evaluate(() => {
  const a = window.app, g = a.game;
  a.startDrift(); a.step(0.5);
  const log = a.driftRun(3);
  return { log, hud: !document.getElementById('drift').classList.contains('hidden'), tag: document.getElementById('dr-tag').textContent, stats: g.stats };
});
await shot('drift_zone');
console.log('дрифт-зона (t, км/ч, угол, очки, ×):', JSON.stringify(zone.log));
console.log('HUD дрифта виден:', zone.hud, 'stats', JSON.stringify(zone.stats));
const fin = await page.evaluate(async () => {
  const a = window.app, g = a.game;
  let res = null; g.menus.showResult = (r) => { res = r; };
  a.step(2, { brake: 1 });            // выход из заноса → серия сохраняется
  const banked = g.mode.total;
  g.mode.time = 0.05; a.step(0.2);    // конец заезда
  await new Promise((r) => setTimeout(r, 1200));
  return { banked, res, save: a.save.data.drift, money: a.save.money };
});
console.log('итог заезда:', JSON.stringify(fin));
if (!fin.res || !fin.res.drift || fin.res.score <= 0) errs.push('заезд не дал очков/результата');
if (fin.save.best !== fin.res?.score) errs.push('рекорд не записан в Save');
await page.evaluate(() => window.app.menus.show('result', window.app.game.mode && { drift: true, ok: true, score: 1234, best: 1234, record: true, reward: 1300, series: 2 }));
await shot('drift_result');

// --- конус сжигает серию
const cone = await page.evaluate(() => {
  const a = window.app, g = a.game;
  a.startDrift(); a.step(0.3);
  const m = g.mode, c = m.cones[0], p = g.player.physics;
  m.drift.sc.pts = 500; m.drift.sc.active = true;
  g.player.place(c.x, c.z + 0.5, 0); p.vz = 5; m.update(1 / 30);
  return { hit: c.hit, pts: m.drift.sc.pts };
});
console.log('конус:', JSON.stringify(cone));
if (!cone.hit || cone.pts !== 0) errs.push('сбитый конус не сжёг серию');

// --- свободная езда: серия → рубли и задание дня
const free = await page.evaluate(() => {
  const a = window.app, g = a.game;
  a.startFree(false); a.step(0.3);
  g.traffic.clear(); g.traffic.enabled = false;
  const A = { x: -471, z: 0 };
  g.player.place(A.x + 60, A.z - 40, -Math.PI / 2);
  const m0 = a.save.money;
  const d = g.daily.tasks.find((t) => t.kind === 'drift');
  if (!d) g.daily.tasks[0] = { kind: 'drift', goal: 400, progress: 0, done: false, claimed: false };
  const log = a.driftRun(3);
  a.step(2.5, { brake: 1 });
  const t = g.daily.tasks.find((x) => x.kind === 'drift');
  return { log, money: a.save.money - m0, daily: t.progress, bestSeries: a.save.data.drift.bestSeries };
});
console.log('свободная езда:', JSON.stringify(free));
if (free.money <= 0 || free.daily <= 0) errs.push('серия в городе не дала рублей/прогресса задания');
await shot('drift_free');
console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
