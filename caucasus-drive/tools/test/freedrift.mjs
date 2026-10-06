// Дрифт в свободной езде: та же система, что в дрифт-зоне (HUD угла/очков/×, события), «шашки»
// бонусом в ту же серию, бонус близости к припаркованным машинам, удар сжигает серию,
// итог → рубли, задание дня, рекорд серии в городе (Save.drift.freeBest) и показ рекорда.
//   node freedrift.mjs /tmp/shots
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
  a.startFree(false); a.step(0.3);
  g.daily.refresh();
  g.daily.tasks[0] = { kind: 'drift', goal: 400, progress: 0, done: false, claimed: false };
});
const shot = (n) => page.screenshot({ path: `${out}/${n}.png` });
const ui = () => page.evaluate(() => ({
  shown: !document.getElementById('drift').classList.contains('hidden'),
  pts: document.getElementById('dr-pts').textContent, mult: document.getElementById('dr-mult').textContent,
  tag: document.getElementById('dr-tag').textContent, tagKind: document.getElementById('dr-tag').className,
  combo: !document.getElementById('combo').classList.contains('hidden'),
}));

// --- «шашки»: обгон стоящей машины трафика в соседней полосе → бонус в серию дрифта
const pass = await page.evaluate(() => {
  const a = window.app, g = a.game, m = g.mode, p = g.player.physics;
  g.traffic.enabled = true; g.traffic.prefill(g.camera);
  const car = g.traffic.cars[0];
  if (!car) return { skip: true };
  g.traffic.update = () => {};   // машина стоит на месте
  const h = p.heading, s = Math.sin(h), c = Math.cos(h);
  car.x = p.x + s * 30 + c * 2.6; car.z = p.z + c * 30 - s * 2.6; car.heading = h;
  for (const o of g.traffic.cars) if (o !== car) { o.x += 5000; }
  p.vx = s * 26; p.vz = c * 26; p.gear = 4;
  const log = [];
  for (let i = 0; i < 60; i++) { a.ctl = { throttle: 0.6 }; g.tick(1 / 30); if (m.drift.sc.bonuses) break; }
  g.render(0);
  log.push(Math.round(p.speed * 3.6));
  return { bonuses: m.drift.sc.bonuses, pts: Math.round(m.drift.sc.pts), keep: m.drift.sc.keepT, kmh: log[0] };
});
console.log('шашки:', JSON.stringify(pass));
if (!pass.skip && pass.bonuses !== 1) errs.push('обгон впритирку не попал в серию');
const u1 = await ui();
console.log('HUD после шашек:', JSON.stringify(u1));
if (!pass.skip && (!u1.shown || !/ШАШКИ|ВПРИТИРКУ/.test(u1.tag))) errs.push('HUD дрифта не показал «шашки»');
if (u1.combo) errs.push('старое табло комбо всё ещё показывается');
await shot('fd_shashki');

// --- удар сжигает серию (вместе с «шашками»)
const crash = await page.evaluate(() => {
  const a = window.app, g = a.game, m = g.mode;
  const m0 = a.save.money;
  if (!m.drift.sc.inSeries) m.drift.sc.bonus(200, 'ШАШКИ');
  m.onCrash(0.4); a.step(0.1);
  return { pts: m.drift.sc.pts, money: a.save.money - m0, tag: document.getElementById('dr-tag').textContent };
});
console.log('удар:', JSON.stringify(crash));
if (crash.pts !== 0 || crash.money !== 0 || !/УДАР/.test(crash.tag)) errs.push('удар не сжёг серию');

// --- «шашки» без удара: серия держится 4 с, затем сохраняется → рубли, задание, рекорд
const bank = await page.evaluate(() => {
  const a = window.app, g = a.game, m = g.mode, sc = m.drift.sc;
  a.step(3);
  const m0 = a.save.money, d0 = g.daily.tasks[0].progress;
  sc.bonus(300, 'ВПРИТИРКУ!'); sc.bonus(300, 'ШАШКИ');  // две подряд: ×1.5
  const total = sc.total;
  a.step(2);
  const alive = sc.inSeries;
  let tag = '';
  for (let i = 0; i < 90 && !tag.startsWith('РЕКОРД'); i++) { g.tick(1 / 30); tag = m.drift.view.tag; }
  g.render(0);
  return { total, alive, tag, money: a.save.money - m0, daily: g.daily.tasks[0].progress - d0, freeBest: a.save.data.drift.freeBest };
});
console.log('итог серии шашек:', JSON.stringify(bank));
if (!bank.alive) errs.push('серия «шашек» сохранилась раньше 4 с');
if (bank.money !== Math.round(bank.total / 5) || bank.daily !== Math.min(bank.total, 400)) errs.push('итог серии: неверные рубли/задание');
if (bank.freeBest !== bank.total || !bank.tag.startsWith('РЕКОРД')) errs.push('рекорд серии в городе не записан/не показан');
await page.evaluate(() => window.app.step(0.4));
await shot('fd_record');

// --- бонус близости: припаркованная машина в 1 м от борта
const near = await page.evaluate(() => {
  const a = window.app, g = a.game, m = g.mode;
  const pc = g.city.parked[0];
  if (!pc) return { skip: true };
  const s = Math.sin(pc.heading), c = Math.cos(pc.heading);
  // ставим машину игрока параллельно, вбок на ширину кузова + 1 м
  g.player.place(pc.x + c * 2.8, pc.z - s * 2.8, pc.heading);
  m.drift.sc.active = true; m.drift._nearT = 0;
  m.drift.update(1 / 30);
  const d = m.drift.near;
  m.drift.sc.reset();
  return { near: +d.toFixed(2) };
});
console.log('близость к припаркованной машине:', JSON.stringify(near));
if (!near.skip && !(near.near < 1.5)) errs.push('припаркованная машина не даёт бонус близости');

// --- настоящий занос на автодроме (×1,5) → рекорд показывается на панели
const run = await page.evaluate(() => {
  const a = window.app, g = a.game, m = g.mode, p = g.player.physics;
  g.traffic.clear(); g.traffic.enabled = false;
  g.player.place(-471 + 60, -40, -Math.PI / 2);
  a.step(0.2);
  a.step(3.2, { throttle: 1 });
  a.step(0.45, { steer: -1, handbrake: true, throttle: 0.3 });
  const log = []; let events = [];
  const oe = m.drift.sc.onEvent; m.drift.sc.onEvent = (t) => { events.push(t); oe(t); };
  for (let i = 0; i < 90; i++) {
    const ang = p.driftAngle * 57.3;
    a.ctl = { throttle: Math.min(1, Math.max(0.4, 0.8 + (30 - Math.abs(ang)) * 0.02)), steer: Math.sign(ang) * Math.max(-0.6, Math.min(0.6, (Math.abs(ang) - 30) * 0.03)) };
    g.tick(1 / 30);
    if (i % 15 === 0) log.push([Math.round(p.speed * 3.6), Math.round(ang), Math.round(m.drift.view.pts), m.drift.view.mult]);
  }
  g.render(0);
  const hud = { ang: document.getElementById('dr-ang').textContent, pts: document.getElementById('dr-pts').textContent };
  const m0 = a.save.money;
  a.step(2.5, { brake: 1 });
  return { log, hud, events, money: a.save.money - m0, freeBest: a.save.data.drift.freeBest, stats: g.stats };
});
console.log('занос в городе (км/ч, угол, очки, ×):', JSON.stringify(run));
if (run.money <= 0) errs.push('занос в свободной езде не дал рублей');
await shot('fd_drift');
const menu = await page.evaluate(() => { window.app.toMenu('main'); return document.querySelector('[data-act="free"] .s')?.textContent; });
console.log('плитка свободной езды:', menu);
if (!/рекорд/.test(menu || '')) errs.push('рекорд не показан в меню');
console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
