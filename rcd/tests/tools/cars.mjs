import { startServer, launch } from '../lib.mjs';
const srv = await startServer(5205);
const { browser, page } = await launch(srv.url, { width: 900, height: 520 });
await page.evaluate(() => { __rcd.progress.updateSettings({ quality: 'medium', pixelRatio: 1, hintsSeen: true }); __rcd.applySettings(); __rcd.go('menu', false); });
const ids = process.argv.slice(2);
for (const id of ids) {
  await page.evaluate((id) => { __rcd.uiRoot.innerHTML = ''; __rcd.screen = ''; __rcd.gfx.showroom.angle = 0.9; __rcd.gfx.showroom.camDist = 7.5; __rcd.gfx.setCarLook(__rcd.gfx.carDef, { }); }, id).catch(() => {});
  await page.evaluate(async (id) => { const m = await import('/src/game/cars.js'); const def = m.getCar(id); __rcd.gfx.carLookKey = ''; __rcd.gfx.setCarLook(def, { paint: def.color, wheels: id === 'ronin' ? 'split10' : 'star5', bodykit: id === 'raketa' ? 'wide' : 'none', spoiler: id === 'raketa' ? 'gt' : 'none', neon: 'none' }); __rcd.gfx.showroom.angle = 0.9; }, id);
  await page.waitForTimeout(700);
  await page.screenshot({ path: `docs/screenshots/car-${id}.png` });
}
await browser.close(); srv.stop();
