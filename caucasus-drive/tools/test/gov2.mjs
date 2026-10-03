import { chromium } from 'playwright';
const [,, q = 'high'] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 915, height: 412 }, deviceScaleFactor: 2.625 });
const errs = []; page.on('pageerror', e => errs.push(e.message));
await page.goto(`http://localhost:4173/?autostart&q=${q}&free`);
await page.waitForFunction(() => window.game && window.game.mode, null, { timeout: 180000 });
const r = await page.evaluate(() => {
  const g = window.game, p = g.perf; g.renderer.setAnimationLoop(null);
  const log = [];
  const snap = (tag) => log.push(`${tag}: step=${p.step}/${p.maxStep} fx=${g.degrade} ratio=${g.renderer.getPixelRatio().toFixed(2)} buf=${g.renderer.domElement.width}x${g.renderer.domElement.height} shadow=${g.dayNight.sun.castShadow} fps=${p.fps}`);
  p.time = 100; p.warmup = 0;
  // имитация: время кадра ∝ числу пикселей (упор в GPU), эффекты почти не влияют
  let now = performance.now();
  const realNow = performance.now.bind(performance);
  performance.now = () => now;
  const sim = (sec, ms) => { const end = now + sec * 1000; while (now < end) { now += ms(); p.update(0.016); } };
  const pix = () => g.renderer.domElement.width * g.renderer.domElement.height;
  const base = pix();
  snap('start');
  for (let k = 0; k < 14; k++) { sim(1.05, () => 62 * (pix() / base) * (g.degrade >= 3 ? 0.85 : 1)); snap('gpu' + k); }
  // vsync 30 стабильно — не должно трогать
  for (let k = 0; k < 120; k++) sim(1.05, () => 16.6);
  snap('fast');
  for (let k = 0; k < 12; k++) { sim(1.05, () => 33.3 + (Math.random() - 0.5)); }
  snap('vsync30 (must come back to 0)');
  // мощный телефон: 60 FPS → восстановление
  for (let k = 0; k < 120; k++) sim(1.05, () => 16.6);
  snap('recovered');
  performance.now = realNow;
  return log;
});
console.log(r.join('\n')); console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
