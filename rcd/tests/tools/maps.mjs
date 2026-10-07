// Скриншоты карт в заносе с высоким качеством (для оценки графики). Использование: node tests/tools/maps.mjs [city parking ...]
import { startServer, launch } from '../lib.mjs';
const maps = process.argv.slice(2);
const list = maps.length ? maps : ['city', 'parking', 'industrial', 'mountain'];
const srv = await startServer(5209);
const { browser, page, errors } = await launch(srv.url, { width: 1280, height: 720 });
const sleep = (ms) => page.waitForTimeout(ms);
await page.evaluate(() => { __rcd.progress.updateSettings({ quality: 'high', pixelRatio: 1, hintsSeen: true }); __rcd.progress.data.money = 9e5; __rcd.progress.addXp(1e6); __rcd.applySettings(); });
const spots = { city: [-60, 0, Math.PI / 2, 'dusk', 'clear'], parking: [-40, -40, Math.PI / 2, 'night', 'clear'], industrial: [-80, 60, 0, 'day', 'clear'], mountain: [-79, 4, 1.7, 'dusk', 'clear'] };
for (const m of list) {
  const [x, z, h, time, weather] = spots[m];
  await page.evaluate(([m, time, weather]) => { __rcd.go('menu', false); __rcd.sel.map = m; __rcd.sel.mode = 'free'; __rcd.sel.time = time; __rcd.sel.weather = weather; __rcd.act('start'); __rcd.countdown = 0; }, [m, time, weather]);
  await sleep(1500);
  await page.evaluate(([x, z, h]) => { const s = __rcd.session, c = s.car; c.reset(x, z, h); c.vx = Math.sin(h) * 20; c.vz = Math.cos(h) * 20; for (let i = 0; i < 4; i++) c.wheelW[i] = 20 / c.spec.wheelRadius; c.gear = 3; const dt = 1 / 60;
    for (let i = 0; i < 150; i++) { const t = i * dt; s.update(dt, { throttle: t < 0.3 ? 0.4 : 0.9, brake: 0, steer: t < 0.9 ? -0.5 : -0.3, handbrake: t > 0.2 && t < 0.55, kick: false }); }
    __rcd.gfx.rig.init = false; __rcd.hud.hintT = 0; }, [x, z, h]);
  await sleep(2500);
  await page.screenshot({ path: `docs/screenshots/40-hq-${m}.png` });
  await page.evaluate(() => __rcd.act('quit'));
}
await browser.close(); srv.stop();
console.log('errors:', errors.filter((e) => !e.includes('favicon')));
