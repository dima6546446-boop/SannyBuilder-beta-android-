import { chromium } from 'playwright';
const [,, out, q = 'high'] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 960, height: 540 } });
const errs = [];
page.on('pageerror', e => errs.push('pageerror: ' + e.message + ' ' + (e.stack||'').split('\n').slice(0,3).join(' | ')));
page.on('console', m => { if (m.type() === 'error') errs.push('console: ' + m.text()); });
await page.goto(`http://localhost:4173/?autostart&nogov&q=${q}&free`);
await page.waitForFunction(() => window.game && window.game.mode, null, { timeout: 180000 });
const sim = (code, arg) => page.evaluate(code, arg);
const shot = (n) => page.screenshot({ path: `${out}/${n}.png` });
await sim(() => { const a = window.app, g = a.game; g.renderer.setAnimationLoop(null);
  a.step = (sec, mv = {}) => { const i = g.input; const upd = i.update.bind(i);
    i.update = function (dt) { upd(dt); if (this.onFoot) { this.moveX = mv.x || 0; this.moveY = mv.y || 0; this.walk = !!mv.walk; } };
    for (let k = 0; k < sec * 30; k++) g.tick(1/30); g.render(0); i.update = upd; };
  a.step(1);
});
const st = () => sim(() => { const g = window.game, w = g.walker; return { onFoot: g.onFoot, state: w.state, seat: w.seat?.kind, pos: [w.pos.x.toFixed(2), w.pos.y.toFixed(2), w.pos.z.toFixed(2)], speed: w.speed.toFixed(2), car: [g.player.physics.x.toFixed(1), g.player.physics.z.toFixed(1)], smoke: w.smoke.stage, enterBtn: !document.getElementById('btn-enter').classList.contains('hidden') }; });
await sim(() => window.game.input.emit('door')); await sim(() => window.app.step(0.6)); await shot('f1_exit'); console.log('exit', JSON.stringify(await st()));
await sim(() => window.app.step(1.2, { y: 0.5 })); await shot('f2_walk'); console.log('walk', JSON.stringify(await st()));
await sim(() => window.app.step(1.5, { y: 1 })); await shot('f3_run'); console.log('run', JSON.stringify(await st()));
await sim(() => { window.game.input.emit('jump'); window.app.step(0.25, { y: 1 }); }); await shot('f4_jump'); console.log('jump', JSON.stringify(await st()));
await sim(() => window.app.step(1)); 
await sim(() => { window.game.input.emit('sit'); window.app.step(1.2); }); await shot('f5_squat'); console.log('squat', JSON.stringify(await st()));
await sim(() => { window.game.input.emit('smoke'); window.app.step(1.0); }); await shot('f6_light'); console.log('light', JSON.stringify(await st()));
await sim(() => { window.app.step(3.3); }); await shot('f7_drag'); console.log('drag', JSON.stringify(await st()));
await sim(() => { window.app.step(2.0); }); await shot('f8_exhale');
await sim(() => { window.app.step(0.5, { y: 1 }); window.game.cameraRig.footYaw += 2.6; window.app.step(0.6); }); await shot('f9_front'); console.log('stand', JSON.stringify(await st()));
await sim(() => { window.game.input.emit('whistle'); window.app.step(0.7); }); await shot('f10_whistle');
// лавка на остановке
await sim(() => { const g = window.game, b = g.walker.benches[0]; g.walker.pos.set(b.x + b.nx * 0.9, 0.15, b.z + b.nz * 0.9); g.walker.yaw = Math.atan2(-b.nx, -b.nz); g.cameraRig.footYaw = Math.atan2(-b.nx, -b.nz) + 0.5; window.app.step(0.5); g.input.emit('sit'); window.app.step(1.5); }); await shot('f11_bench'); console.log('bench', JSON.stringify(await st()));
// капот
await sim(() => { const g = window.game, p = g.player.physics, w = g.walker; w.standUp(); const s = Math.sin(p.heading), c = Math.cos(p.heading);
  w.pos.set(p.x + s * (g.player.half.front + 0.6), 0, p.z + c * (g.player.half.front + 0.6)); window.app.step(0.3); g.input.emit('sit'); g.cameraRig.footYaw = p.heading + Math.PI + 0.6; window.app.step(1.5); }); await shot('f12_hood'); console.log('hood', JSON.stringify(await st()));
// ночь + курение на капоте
await sim(() => { const g = window.game; g.setTimePreset(2); window.app.step(2.5); }); await shot('f13_night');
// назад в машину
await sim(() => { const g = window.game; g.walker.standUp(); window.app.step(0.3); g.input.emit('door'); window.app.step(0.5); }); await shot('f14_back'); console.log('back', JSON.stringify(await st()));
console.log(errs.slice(0, 15).join('\n') || 'NO ERRORS');
await browser.close();
