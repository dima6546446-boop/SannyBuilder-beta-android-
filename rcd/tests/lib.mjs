import { chromium } from 'playwright-core';
import { spawn } from 'node:child_process';
import fs from 'node:fs';

function chromePath() {
  const c = ['/opt/pw-browsers/chromium', ...fs.existsSync('/opt/pw-browsers') ? fs.readdirSync('/opt/pw-browsers').filter((d) => d.startsWith('chromium')).map((d) => `/opt/pw-browsers/${d}/chrome-linux/chrome`) : []];
  return c.find((p) => { try { return fs.statSync(p).isFile(); } catch (e) { return false; } });
}

export async function startServer(port = 5199) {
  const p = spawn('npx', ['vite', '--port', String(port), '--strictPort', '--host', '127.0.0.1'], { stdio: ['ignore', 'pipe', 'pipe'] });
  await new Promise((res, rej) => {
    const t = setTimeout(() => rej(new Error('vite не запустился')), 30000);
    const onData = (d) => { if (String(d).includes('Local') || String(d).includes('ready')) { clearTimeout(t); res(); } };
    p.stdout.on('data', onData); p.stderr.on('data', onData);
  });
  return { url: `http://127.0.0.1:${port}/`, stop: () => p.kill() };
}

export async function launch(url, { width = 1280, height = 720, touch = false } = {}) {
  const browser = await chromium.launch({ executablePath: chromePath(), args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--no-sandbox', '--autoplay-policy=no-user-gesture-required'] });
  const ctx = await browser.newContext({ viewport: { width, height }, hasTouch: touch, isMobile: touch });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push('console: ' + m.text()); });
  await page.goto(url);
  await page.waitForFunction(() => window.__rcd, null, { timeout: 20000 });
  return { browser, page, errors };
}
