import { chromium } from 'playwright';
const [,, out] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
for (const [w, h] of [[640, 360], [740, 360], [800, 360], [915, 412]]) {
  const page = await browser.newPage({ viewport: { width: w, height: h } });
  await page.goto(`http://localhost:4173/?autostart&nogov&q=low&free`);
  await page.waitForFunction(() => window.game && window.game.mode, null, { timeout: 180000 });
  await page.evaluate(() => { const a = window.app; a.game.renderer.setAnimationLoop(null); for (let i = 0; i < 30; i++) a.game.tick(1/30); a.game.render(0); });
  await page.evaluate(() => { const g = window.game; g.mode.onTaxi(); document.getElementById('btn-action').classList.remove('hidden'); g.hud.mission('Такси · посадка', 'Пассажир ждёт: 300 м'); g.mode.update = () => {}; g.hud.drift({ angle: 35, pts: 12345, mult: 2.5, active: true, tag: 'ПЕРЕКЛАДКА', tagKind: '' }, 1); });
  for (const foot of [false, true]) {
  if (foot) await page.evaluate(() => { const g = window.game; g.input.emit('door'); for (let i = 0; i < 10; i++) g.tick(1/30); g.render(0); });
  const boxes = await page.evaluate(() => {
    const ids = ['drift', 'btn-cam','btn-lights','btn-radio','btn-door','btn-pause','speed-limit','money-box','btn-horn','btn-taxi','btn-action','hb','lever','gas','brake','minimap','indicators','dash','wheel','mission','stick','btn-jump','btn-sit','btn-smoke','btn-whistle','btn-enter'];
    const r = {}; for (const id of ids) { const e = document.getElementById(id); if (!e) continue; const b = e.getBoundingClientRect(); if (b.width) r[id] = [b.left|0, b.top|0, b.right|0, b.bottom|0]; }
    return r;
  });
  const k = Object.keys(boxes), over = [];
  for (let i = 0; i < k.length; i++) for (let j = i + 1; j < k.length; j++) { const a = boxes[k[i]], b = boxes[k[j]]; if (a[0] < b[2] && b[0] < a[2] && a[1] < b[3] && b[1] < a[3]) over.push(k[i] + '×' + k[j]); }
  console.log(w + 'x' + h + (foot ? ' пешком' : ' в машине'), over.join(', ') || 'no overlaps');
  await page.screenshot({ path: `${out}/hud_${w}x${h}${foot ? '_foot' : ''}.png` });
  }
  await page.close();
}
await browser.close();
