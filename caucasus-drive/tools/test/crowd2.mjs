import { chromium } from 'playwright';
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 640, height: 360 } });
const errs = []; page.on('pageerror', e => errs.push(e.message));
for (const nf of ['', '&nofines']) {
  await page.goto(`http://localhost:4173/?autostart&nogov&q=low&free${nf}`);
  await page.waitForFunction(() => window.game && window.game.mode, null, { timeout: 180000 });
  const r = await page.evaluate(() => { const g = window.game; g.renderer.setAnimationLoop(null);
    const run = (sec, ctl = {}) => { const i = g.input; const upd = i.update.bind(i); i.update = function (dt) { upd(dt); Object.assign(this, ctl); }; for (let k = 0; k < sec * 30; k++) g.tick(1/30); i.update = upd; };
    run(2.5, { throttle: 1 });
    const p = g.player.physics, n = g.crowd.npcs.find((n) => n.active);
    n.state = 'walk'; n.x = p.x + Math.sin(p.heading) * 2.3; n.z = p.z + Math.cos(p.heading) * 2.3;
    const m0 = g.save.money; const v = p.speed;
    run(0.1, { throttle: 1 });
    const s1 = n.state; run(4);
    return { v: v.toFixed(1), afterHit: s1, later: n.state, moneyDelta: g.save.money - m0, fines: g.mode.fines }; });
  console.log(nf || 'fines', JSON.stringify(r));
}
console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
