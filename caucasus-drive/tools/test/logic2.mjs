import { chromium } from 'playwright';
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 640, height: 360 } });
const errs = [];
page.on('pageerror', e => errs.push('pageerror: ' + e.message + ' ' + (e.stack||'').split('\n').slice(0,4).join(' | ')));
await page.goto(`http://localhost:4173/?autostart&nogov&q=low`);
await page.waitForFunction(() => window.app && window.game && window.game.player, null, { timeout: 120000 });
const r = await page.evaluate(async () => {
  const a = window.app, g = a.game; g.renderer.setAnimationLoop(null);
  a.save.reset();
  const step = (sec, ctl = {}) => { g.input.update = function () { Object.assign(this, { steer: 0, throttle: 0, brake: 0, handbrake: false, horn: false }, ctl); }; for (let i = 0; i < sec * 30; i++) g.tick(1/30); };
  const out = {};
  let res = null; g.menus.showResult = (x) => { res = x; };
  a.startParking(0); step(0.5);
  const t = g.mode.scene.target; g.player.place(t.x, t.z, t.h); step(2);
  await new Promise(r => setTimeout(r, 1000));
  out.parkOk = res; out.money = a.save.money;
  // камера
  a.startFree(); step(0.3);
  const cam = g.city.cameras[0];
  const rx = -cam.dz, rz = cam.dx;
  const lat = 7.6 - 5.25;
  const x = cam.x - cam.dx * 40 - rx * lat, z = cam.z - cam.dz * 40 - rz * lat;
  g.traffic.clear(); g.traffic.enabled = false;
  g.player.place(x, z, Math.atan2(cam.dx, cam.dz));
  const p = g.player.physics; p.vx = cam.dx * 30; p.vz = cam.dz * 30; p.gear = 5;
  const m0 = a.save.money; const toasts = [];
  const orig = g.hud.toast.bind(g.hud); g.hud.toast = (t, k, s) => { toasts.push(t); orig(t, k, s); };
  step(2.5, { throttle: 1 });
  out.camFine = m0 - a.save.money; out.toasts = toasts; out.speed = p.speed * 3.6;
  // шашки: ставим трафик-машину впереди в соседней полосе и обгоняем
  g.traffic.enabled = true; g.traffic.prefill(g.camera);
  const car = g.traffic.cars[0];
  if (car) {
    g.player.place(car.x - Math.sin(car.heading) * 25 - Math.cos(car.heading) * 2.4 * 0 + (-Math.cos(car.heading)) * -2.5, car.z - Math.cos(car.heading) * 25 + Math.sin(car.heading) * -2.5, car.heading);
    g.player.physics.vx = Math.sin(car.heading) * 25; g.player.physics.vz = Math.cos(car.heading) * 25; g.player.physics.gear = 4;
    step(2, { throttle: 1 });
    const sc = g.mode.drift.sc; out.series = { pts: Math.round(sc.pts), mult: sc.mult, bonuses: sc.bonuses };
  }
  return out;
});
console.log(JSON.stringify(r, null, 1));
console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
