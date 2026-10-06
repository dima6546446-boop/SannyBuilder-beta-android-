import { chromium } from 'playwright';
const [,, out, q = 'medium', only = ''] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 960, height: 540 } });
const errs = [];
page.on('pageerror', e => errs.push('pageerror: ' + e.message + ' ' + (e.stack||'').split('\n').slice(0,3).join(' | ')));
page.on('console', m => { if (m.type() === 'error') errs.push('console: ' + m.text()); });
await page.goto(`http://localhost:4173/?autostart&nogov&q=${q}`);
await page.waitForFunction(() => window.app && window.game && window.game.player, null, { timeout: 120000 }).catch(e => errs.push('timeout init'));
await page.waitForTimeout(1500);
const shot = async (name) => { await page.screenshot({ path: `${out}/${name}.png` }); };
const sim = (code, arg) => page.evaluate(code, arg);
await sim(() => { const a = window.app; a.game.renderer.setAnimationLoop(null);
  a.step = (sec, ctl = {}) => { const g = a.game; g.input.update = function () { Object.assign(this, { steer: 0, throttle: 0, brake: 0, handbrake: false, horn: false }, ctl); }; for (let i = 0; i < sec * 30; i++) g.tick(1/30); g.render(0); };
  a.menuShot = () => { a.garage.render(0.016); };
});
if (!only || only.includes('menu')) {
  await sim(() => window.app.menuShot()); await shot('m_main');
  await sim(() => { window.app.menus.show('garage'); window.app.menuShot(); }); await shot('m_garage');
  await sim(() => { window.app.menus.show('levels'); }); await shot('m_levels');
}
if (!only || only.includes('free')) {
  await sim(() => { window.app.startFree(); window.app.step(1); }); await shot('f_start');
  console.log('free', JSON.stringify(await sim(() => { const a = window.app; a.step(6, { throttle: 1 }); return { ...a.game.stats, v: a.game.player.physics.speed*3.6, pos: [a.game.player.physics.x|0, a.game.player.physics.z|0], money: a.save.money }; })));
  await shot('f_drive');
  await sim(() => { const a = window.app; a.game.cameraRig.setMode(1); a.game.setTimePreset(2); a.step(2, { throttle: 0.5 }); }); await shot('f_night');
  await sim(() => { const a = window.app; a.game.mode.onTaxi(); a.step(8, { brake: 1 }); a.game.setTimePreset(1); a.step(1); }); await shot('f_taxi');
}
if (!only || only.includes('park')) {
  for (const lv of [0, 2, 4, 6, 8, 11]) {
    await sim((lv) => { const a = window.app; a.startParking(lv); a.game.cameraRig.setMode(2); a.step(1.5); }, lv);
    await shot('p_' + lv);
  }
}
if (!only || only.includes('exam')) {
  await sim(() => { const a = window.app; a.startExam(); a.step(1.5); }); await shot('e_start');
}
console.log(errs.slice(0, 15).join('\n') || 'NO ERRORS');
await browser.close();
