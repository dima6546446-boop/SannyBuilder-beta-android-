// Онлайн: два игрока через локальный брокер PeerJS (tools/test/README: peer-сервер на 127.0.0.1:9000).
// Быстрая игра (оба в PUB1), видимость машины и ника, столкновение-объект, фразы, пешком, выход;
// комната по коду; несуществующий код.
//   node online.mjs /tmp/shots
import { chromium } from 'playwright';
const [,, out = '.'] = process.argv;
const browser = await chromium.launch({
  executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-features=WebRtcHideLocalIpsWithMdns'],
});
const errs = [];
const URL = 'http://localhost:4173/?nogov&q=low&peerhost=127.0.0.1&peerport=9000';
async function player(name, w = 800, h = 360) {
  const ctx = await browser.newContext({ viewport: { width: w, height: h } });
  const page = await ctx.newPage();
  page.on('pageerror', (e) => errs.push(`${name} pageerror: ${e.message} ${(e.stack || '').split('\n').slice(0, 3).join(' | ')}`));
  page.on('console', (m) => { if (m.type() === 'error' && !/WebRTC|ICE|favicon/.test(m.text())) errs.push(`${name} console: ${m.text()}`); });
  await page.goto(URL);
  await page.waitForFunction(() => window.app && window.app.menus.current === 'main', null, { timeout: 120000 });
  await page.evaluate((n) => { const a = window.app; a.save.data.settings.nick = n; a.save.commit(); }, name);
  return page;
}
const join = (page, mode, code) => page.evaluate(async ([mode, code]) => {
  const a = window.app;
  a.menus.show('online');
  await a.startOnline(mode, code);
  return { state: a.state, room: a.net.room, host: a.net.host, count: a.net.count, status: document.getElementById('ol-status')?.textContent };
}, [mode, code]);
const waitFor = (page, fn, arg, t = 20000) => page.waitForFunction(fn, arg, { timeout: t }).then(() => true, () => false);

const A = await player('Дима');
const B = await player('Саня', 640, 360);
await A.screenshot({ path: `${out}/on_menu.png` });
await A.evaluate(() => window.app.menus.show('online'));
await A.waitForTimeout(3500);
await A.screenshot({ path: `${out}/on_screen.png` });

// --- быстрая игра: A становится хостом PUB1, B заходит к нему
const ra = await join(A, 'quick');
console.log('A:', JSON.stringify(ra));
if (ra.state !== 'play' || ra.room !== 'PUB1' || !ra.host) errs.push('A не создал открытую комнату: ' + JSON.stringify(ra));
const rb = await join(B, 'quick');
console.log('B:', JSON.stringify(rb));
if (rb.state !== 'play' || rb.room !== 'PUB1' || rb.host) errs.push('B не зашёл в открытую комнату: ' + JSON.stringify(rb));
if (!await waitFor(A, () => window.app.net.count === 2)) errs.push('хост не видит второго игрока');

// A ставит машину перед B: B должен увидеть её (instancer) с ником
await B.evaluate(() => { const g = window.game; g.player.place(0, -70, 0); g.cameraRig.snap(); });
await A.evaluate(() => { const g = window.game; g.player.place(0.5, -62, Math.PI * 0.8); });
const seen = await waitFor(B, () => { const r = [...window.game.remote.map.values()][0]; return r && r.has && Math.hypot(r.car.x - 0.5, r.car.z + 62) < 0.6; });
if (!seen) errs.push('B не получил позицию машины A');
const rinfo = await B.evaluate(() => { const r = [...window.game.remote.map.values()][0]; return r && { nick: r.p.nick, car: r.p.car, tag: r.tag.visible, x: r.car.x, z: r.car.z, inTraffic: window.game._trafficWithRemote().includes(r.car) }; });
console.log('B видит:', JSON.stringify(rinfo));
if (rinfo?.nick !== 'Дима' || !rinfo.tag || !rinfo.inTraffic) errs.push('ник/столкновения удалённой машины: ' + JSON.stringify(rinfo));
// фраза
await A.evaluate(() => { window.app._chatMenu(); document.querySelector('#chat-menu [data-k="1"]').click(); });
if (!await waitFor(B, () => [...document.querySelectorAll('.toast')].some((t) => /Дима: Погнали/.test(t.textContent)), null, 5000)) errs.push('фраза не дошла');
await B.waitForTimeout(400);
await B.screenshot({ path: `${out}/on_b_sees_a.png` });
// HUD онлайна
const hud = await B.evaluate(() => ({ row: document.getElementById('online-row').textContent, chat: !document.getElementById('btn-chat').classList.contains('hidden') }));
if (!/👥 2/.test(hud.row) || !hud.chat) errs.push('HUD онлайна: ' + JSON.stringify(hud));

// A выходит из машины — B видит человечка
await A.evaluate(() => { window.game.player.physics.vx = window.game.player.physics.vz = 0; window.game.toggleFoot(); });
const foot = await waitFor(B, () => { const r = [...window.game.remote.map.values()][0]; return r?.foot?.c.root.visible; });
if (!foot) errs.push('B не видит A пешком');
await B.waitForTimeout(500);
await B.screenshot({ path: `${out}/on_b_foot.png` });

// пауза в онлайне — кнопка «Выйти из онлайна»
const pz = await A.evaluate(() => { window.app.pause(); return document.getElementById('p-menu').textContent; });
if (!/онлайн/.test(pz)) errs.push('пауза без «Выйти из онлайна»: ' + pz);
await A.screenshot({ path: `${out}/on_pause.png` });

// B выходит в меню — у A пропадает
await B.evaluate(() => window.app.toMenu());
if (!await waitFor(A, () => window.app.net.count === 1 && window.game.remote.map.size === 0)) errs.push('A не заметил выход B');

// --- комната по коду
const rc = await join(A.context().pages()[0], 'create');
console.log('A create:', JSON.stringify(rc));
if (!/^[A-Z0-9]{4}$/.test(rc.room || '')) errs.push('код комнаты: ' + rc.room);
const rj = await join(B, 'join', rc.room);
console.log('B join:', JSON.stringify(rj));
if (rj.state !== 'play' || rj.room !== rc.room) errs.push('вход по коду: ' + JSON.stringify(rj));
// хост уходит — клиент продолжает один
await A.evaluate(() => window.app.toMenu());
if (!await waitFor(B, () => !window.app.net.active && window.game.remote.map.size === 0)) errs.push('клиент не заметил ухода хоста');

// --- неверный код
await B.evaluate(() => window.app.toMenu());
const bad = await join(B, 'join', 'ZZZZ');
console.log('неверный код:', JSON.stringify(bad));
if (bad.state !== 'menu' || !/не найдена/.test(bad.status || '')) errs.push('неверный код: ' + JSON.stringify(bad));
await B.screenshot({ path: `${out}/on_bad.png` });

console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
