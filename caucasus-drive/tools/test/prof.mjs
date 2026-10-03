import { chromium } from 'playwright';
const [,, q = 'high'] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 400, height: 225 } });
await page.goto(`http://localhost:4173/?autostart&nogov&q=${q}&free`);
await page.waitForFunction(() => window.game && window.game.mode, null, { timeout: 180000 });
console.log(q, JSON.stringify(await page.evaluate(() => { const g = window.game; g.renderer.setAnimationLoop(null);
  const T = {}; const wrap = (obj, name, label) => { const f = obj[name].bind(obj); obj[name] = (...a) => { const t = performance.now(); const r = f(...a); T[label] = (T[label] || 0) + performance.now() - t; return r; }; };
  wrap(g.player, 'update', 'player'); wrap(g.traffic, 'update', 'traffic'); wrap(g.rules, 'update', 'rules'); wrap(g.crowd, 'update', 'crowd'); wrap(g.shops, 'update', 'shops');
  wrap(g.cameraRig, 'update', 'camera'); wrap(g.lights, 'update', 'tlights'); wrap(g.dayNight, 'update', 'daynight'); wrap(g.city, 'update', 'city'); wrap(g.trees, 'update', 'trees');
  wrap(g.smoke, 'update', 'smoke'); wrap(g.instancer, 'begin', 'inst.begin'); wrap(g.instancer, 'end', 'inst.end'); wrap(g.traffic, 'render', 'traffic.render'); wrap(g.glow, 'end', 'glow'); wrap(g.hud, 'update', 'hud'); wrap(g.mode, 'update', 'mode');
  wrap(g.player.interior, 'update', 'interior'); wrap(g, '_updateEnv', 'env');
  for (let i = 0; i < 60; i++) g.tick(1/30);
  for (const k in T) T[k] = 0;
  const N = 180; let tt = 0, tr = 0;
  for (let i = 0; i < N; i++) { const a = performance.now(); g.tick(1/30); const b = performance.now(); g.render(0); tr += performance.now() - b; tt += b - a; }
  const out = { tickMs: +(tt / N).toFixed(2) }; for (const k in T) out[k] = +(T[k] / N).toFixed(3);
  out.calls = g.renderer.info.render.calls; return out; })));
await browser.close();
