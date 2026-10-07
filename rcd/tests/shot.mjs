import { startServer, launch } from './lib.mjs';
const only = process.argv[2];
const srv = await startServer();
const { browser, page, errors } = await launch(srv.url);
const sleep = (ms) => page.waitForTimeout(ms);
const shot = (n) => page.screenshot({ path: `docs/screenshots/${n}.png` });
await page.evaluate(() => { __rcd.progress.updateSettings({ hintsSeen: true }); __rcd.progress.data.money = 500000; __rcd.progress.addXp(200000); __rcd.progress.save(); __rcd.go('menu', false); });
await sleep(600);
await shot('01-menu');
await page.evaluate(() => __rcd.act('goPlay')); await sleep(500); await shot('02-play');
await page.evaluate(() => __rcd.act('goGarage')); await sleep(500); await shot('03-garage');
await page.evaluate(() => __rcd.act('garageTab', { id: 'tune' })); await sleep(300); await shot('04-tune');
await page.evaluate(() => __rcd.act('garageTab', { id: 'look' })); await sleep(300); await shot('05-look');
await page.evaluate(() => { __rcd.go('settings', false); }); await sleep(300); await shot('06-settings');
await page.evaluate(() => { __rcd.go('hints', false); }); await sleep(300); await shot('07-hints');
for (const map of ['parking', 'city', 'industrial', 'mountain']) {
  if (only && only !== map) continue;
  await page.evaluate((m) => { __rcd.go('menu', false); __rcd.sel.map = m; __rcd.sel.mode = 'free'; __rcd.sel.time = 'auto'; __rcd.act('start'); }, map);
  await sleep(4500);
  // газ + занос
  await page.evaluate(() => { const c = __rcd.session.car; c.input.throttle = 1; });
  await page.evaluate(() => { __rcd.controls.touch.throttle = true; });
  await sleep(3000);
  await page.evaluate(() => { __rcd.controls.touch.left = true; __rcd.controls.touch.handbrake = true; });
  await sleep(400);
  await page.evaluate(() => { __rcd.controls.touch.handbrake = false; });
  await sleep(1500);
  await shot('10-game-' + map);
  console.log(map, await page.evaluate(() => { const c = __rcd.session.car; return { kmh: Math.round(c.speed * 3.6), beta: Math.round(c.driftAngle * 57.3), score: __rcd.session.scoring.total, x: Math.round(c.x), z: Math.round(c.z) }; }));
  await page.evaluate(() => { __rcd.controls.touch.throttle = false; __rcd.controls.touch.left = false; __rcd.act('quit'); });
  await sleep(300);
}
await browser.close(); srv.stop();
console.log(errors);
