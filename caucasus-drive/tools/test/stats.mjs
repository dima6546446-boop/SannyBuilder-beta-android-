// Стоимость кадра в нескольких точках города: draw calls / треугольники (с проходом теней и без),
// число кастеров теней, время tick и render (SwiftShader — для сравнения «до/после», не абсолютное).
// node stats.mjs medium
import { chromium } from 'playwright';
const [,, q = 'medium'] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 960, height: 540 } });
const errs = [];
page.on('pageerror', (e) => errs.push('pageerror: ' + e.message));
await page.goto(`http://localhost:4173/?autostart&nogov&q=${q}&free&traffic=12`);
await page.waitForFunction(() => window.game && window.game.mode, null, { timeout: 180000 });
const r = await page.evaluate(() => {
  const g = window.game, gl = g.renderer.getContext(), px = new Uint8Array(4);
  const sync = () => gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
  const info = g.renderer.info; info.autoReset = false;
  g.renderer.setAnimationLoop(null);
  g.input.update = function () { Object.assign(this, { steer: 0, throttle: 0, brake: 1, handbrake: false, horn: false }); };
  // точки: перекрёсток в центре, улица, двор, автодром
  const spots = [[0, -70, 0], [-120, 60, Math.PI / 2], [150, 150, Math.PI], [-470, -40, 0.3]];
  const res = [];
  let casters = 0;
  g.scene.traverse((o) => { if ((o.isMesh || o.isInstancedMesh) && o.castShadow) casters++; });
  for (const [x, z, h] of spots) {
    g.player.place(x, z, h); g.cameraRig.snap();
    for (let i = 0; i < 40; i++) g.tick(1 / 30);
    // кадр с проходом теней
    g.renderer.shadowMap.needsUpdate = true;
    info.reset(); g.render(0); sync();
    const withSh = { calls: info.render.calls, tris: info.render.triangles };
    g.renderer.shadowMap.needsUpdate = false;
    info.reset(); g.render(0); sync();
    const noSh = { calls: info.render.calls, tris: info.render.triangles };
    // время: 30 кадров в обычном ритме обновления теней
    let tt = 0, tr = 0;
    for (let i = 0; i < 30; i++) {
      const a = performance.now(); g.tick(1 / 30); const b = performance.now();
      info.reset(); g.render(0); sync(); tr += performance.now() - b; tt += b - a;
    }
    res.push({ spot: `${x},${z}`, callsSh: withSh.calls, trisSh: withSh.tris, calls: noSh.calls, tris: noSh.tris, tickMs: +(tt / 30).toFixed(2), renderMs: +(tr / 30).toFixed(1) });
  }
  info.autoReset = true;
  return { casters, res };
});
console.log(q, 'shadow casters:', r.casters);
console.table(r.res);
const avg = (k) => Math.round(r.res.reduce((s, x) => s + x[k], 0) / r.res.length * 10) / 10;
console.log('avg', JSON.stringify({ callsSh: avg('callsSh'), trisSh: avg('trisSh'), calls: avg('calls'), tris: avg('tris'), tickMs: avg('tickMs'), renderMs: avg('renderMs') }));
console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
