// Редактор номера в гараже: листание позиций стрелками, живой предпросмотр (canvas и 3D),
// цена обычного/красивого номера, покупка, формат Save {text, region} и миграция старого id.
//   node plates.mjs /tmp/shots
import { chromium } from 'playwright';
const [,, out = '.'] = process.argv;
const browser = await chromium.launch({ executablePath: process.env.PW_CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const errs = [];
for (const [w, h] of [[800, 360], [640, 360]]) {
  const page = await browser.newPage({ viewport: { width: w, height: h } });
  page.on('pageerror', (e) => errs.push('pageerror: ' + e.message + ' ' + (e.stack || '').split('\n').slice(0, 3).join(' | ')));
  page.on('console', (m) => { if (m.type() === 'error') errs.push('console: ' + m.text()); });
  await page.goto('http://localhost:4173/?nogov&q=medium');
  await page.waitForFunction(() => window.app && window.game && window.app.menus.current === 'main', null, { timeout: 120000 });
  // старый формат: plate — id из TUNING.plate (7 — «А777АА 05»)
  const mig = await page.evaluate(() => {
    const a = window.app;
    a.save.reset();
    a.save.data.money = 100000;
    a.save.data.tuning.vaz2107 = { plate: 7 };
    a.save.commit();
    a.menus.tab = 'plate'; a.menus.plateDraft = null;
    a.menus.show('garage');
    for (let i = 0; i < 20 && document.querySelector('.carname')?.textContent !== 'ВАЗ-2107'; i++) { a.menus.garageIndex = i; a.menus.show('garage'); }
    return { plate: a.save.tuningOf('vaz2107').plate, values: a.save.tuningValues('vaz2107').plate };
  });
  console.log(w, 'миграция:', JSON.stringify(mig));
  if (mig.plate.text !== 'А777АА' || mig.plate.region !== '05') errs.push('старый id номера не перенесён');
  const ui = () => page.evaluate(() => ({
    chars: [...document.querySelectorAll('.pl-ch')].map((e) => e.textContent).join(''),
    info: document.getElementById('pl-info').textContent, buy: document.getElementById('pl-buy').textContent,
    buyCls: document.getElementById('pl-buy').className,
    map3d: (() => { const grp = window.app.garage.model.root.getObjectByName('Plates'); return grp && grp.children[0].material.map.image.toDataURL().length; })(),
  }));
  const u0 = await ui();
  console.log(w, 'старт:', JSON.stringify(u0));
  if (!/Установлено/.test(u0.buy)) errs.push('текущий номер не помечен «Установлено»');
  await page.waitForTimeout(3000); // заставка с логотипом гаснет
  await page.screenshot({ path: `${out}/pl_${w}_0.png` });
  // обычный номер: первая цифра 7 → 8 (877), буквы АА → АВ (последняя ▲)
  const tap = (k, dir) => page.click(`.pl-slot[data-k="${k}"] .pl-arr[data-d="${dir}"]`);
  await tap(1, 1); await tap(5, 1);
  const u1 = await ui();
  console.log(w, 'правка:', JSON.stringify(u1));
  if (!u1.chars.startsWith('А877АВ')) errs.push('стрелки не меняют позиции: ' + u1.chars);
  if (!/2\s000/.test(u1.buy) || /Красивый/.test(u1.info)) errs.push('обычный номер должен стоить 2 000 ₽');
  if (u1.map3d === u0.map3d) errs.push('3D-номер в гараже не обновился');
  // регион: сотни ▲ → «105», тап по символу тоже листает
  await tap(6, 1);
  await page.click('.pl-slot[data-k="4"] .pl-ch');
  const u2 = await ui();
  console.log(w, 'регион 3 цифры:', JSON.stringify(u2));
  if (!u2.chars.endsWith('105') || u2.chars[4] !== 'В') errs.push('регион/тап по символу: ' + u2.chars);
  // красивый номер: быстрый выбор «О001ОО 95» + проверка цены до покупки
  await page.click('.pl-q[data-pi="9"]');
  const u3 = await ui();
  console.log(w, 'блатной:', JSON.stringify(u3));
  if (!/Красивый/.test(u3.info) || !/(2\d|3\d|4\d|50)\s000/.test(u3.buy)) errs.push('красивый номер без надбавки: ' + u3.buy);
  await page.evaluate(() => { document.getElementById('pl-prev').scrollIntoView({ block: 'start' }); });
  await page.screenshot({ path: `${out}/pl_${w}_1.png` });
  // 000 не выдают: 001 ▼ на последней цифре → 009 (пропуск 000)
  await tap(3, -1);
  const u4 = await ui();
  if (u4.chars.slice(1, 4) !== '009') errs.push('позиция дала запрещённый 000: ' + u4.chars);
  // покупка
  const buy = await page.evaluate(() => {
    const a = window.app, m0 = a.save.money;
    document.getElementById('pl-buy').click();
    const t = a.save.data.tuning.vaz2107.plate;
    return { spent: m0 - a.save.money, saved: t, inGarage: document.getElementById('pl-buy')?.textContent };
  });
  console.log(w, 'покупка:', JSON.stringify(buy));
  if (buy.saved?.text !== 'О009ОО' || buy.saved?.region !== '95' || buy.spent <= 2000) errs.push('покупка номера не записана в Save как {text, region}');
  // уход с вкладки без покупки — на машине остаётся купленный номер
  await tap(1, 1);
  await page.click('.tab[data-tab="neon"]');
  const back = await page.evaluate(() => window.app.garage.model.root.getObjectByName('Plates').children[0].material.map.image.toDataURL().length);
  await page.click('.tab[data-tab="plate"]');
  const u5 = await ui();
  if (back !== u5.map3d || !/Установлено/.test(u5.buy)) errs.push('черновик остался на машине после ухода с вкладки');
  // номер на машине игрока в городе
  const car = await page.evaluate(async () => {
    const a = window.app; a.startFree(false);
    await new Promise((r) => setTimeout(r, 1500));
    const grp = a.game.player.body.getObjectByName('Plates');
    return { plates: grp?.children.length, tv: a.save.tuningValues('vaz2107').plate };
  });
  console.log(w, 'в городе:', JSON.stringify(car));
  if (!car.plates) errs.push('на машине игрока нет номера');
  await page.close();
}
console.log(errs.join('\n') || 'NO ERRORS');
await browser.close();
