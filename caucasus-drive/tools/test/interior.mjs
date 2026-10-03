import { chromium } from 'playwright';
const [,, out, ids = 'vaz2101,vaz2106,vaz2107,oka,vaz2109,niva,priora,granta,vesta,largus'] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 960, height: 540 } });
const errs = []; page.on('pageerror', e => errs.push(e.message + ' ' + (e.stack||'').split('\n')[1]));
page.on('console', m => { if (m.type() === 'error') errs.push('console: ' + m.text()); });
await page.goto(`http://localhost:4173/?autostart&nogov&q=high&free`);
await page.waitForFunction(() => window.game && window.game.mode, null, { timeout: 180000 });
await page.evaluate(() => { const g = window.game; g.renderer.setAnimationLoop(null);
  for (const id of ['hud', 'controls', 'toasts']) document.getElementById(id).classList.add('hidden');
  window.run = (sec, ctl = {}) => { const i = g.input; const upd = i.update.bind(i); i.update = function (dt) { upd(dt); Object.assign(this, ctl); }; for (let k = 0; k < sec * 30; k++) g.tick(1/30); i.update = upd; };
});
for (const id of ids.split(',')) {
  await page.evaluate((id) => { const a = window.app, g = a.game; a.save.data.current = id; if (!a.save.data.owned.includes(id)) a.save.data.owned.push(id); g.refreshCar(); g.player.place(0, -70, 0); g.cameraRig.setMode(4); window.run(1.0, { steer: 0.2, throttle: 0.3 }); g.render(0); }, id);
  await page.screenshot({ path: `${out}/${id}_pov.png` });
  // с заднего дивана (видно водителя, руль, щиток) и с пассажирского места
  for (const [n, P, L] of [['rear', [-0.15, 0.0, -0.95], [0.3, -0.25, 1.0]], ['side', [-0.42, -0.02, -0.15], [0.36, -0.2, 0.55]]]) {
    await page.evaluate(([P, L]) => { const g = window.game, it = g.player.interior, b = g.player.body, cam = g.camera;
      const e = it.eye; const v = (d) => b.localToWorld(new (cam.position.constructor)(e.x + d[0] - 0.36 + 0.36, e.y + d[1], e.z + d[2]));
      cam.position.copy(v(P)); cam.lookAt(v(L)); cam.fov = 75; cam.updateProjectionMatrix(); it.setFirstPerson(false); g.render(0); }, [P, L]);
    await page.screenshot({ path: `${out}/${id}_${n}.png` });
  }
}
console.log(errs.slice(0, 10).join('\n') || 'NO ERRORS');
await browser.close();
