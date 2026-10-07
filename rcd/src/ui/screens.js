// Экраны интерфейса: функции возвращают HTML. Все обработчики — через data-act (делегирование в main.js).
import { CARS, getCar, buildSpec, rating, TUNING_PARTS, WHEELS, BODYKITS, SPOILERS, NEONS, PAINTS, upgradePrice } from '../game/cars.js';
import { MAP_LIST, getMap } from '../game/maps.js';
import { xpForLevel, MAX_LEVEL } from '../game/progression.js';
import { CAMERA_MODES } from '../render/camera.js';

export const fmtMoney = (n) => Math.round(n).toLocaleString('ru-RU').replace(/ /g, ' ') + ' ₽';
export const fmtNum = (n) => Math.round(n).toLocaleString('ru-RU').replace(/ /g, ' ');
export const fmtTime = (s) => { s = Math.max(0, s); const m = Math.floor(s / 60), r = Math.floor(s % 60); return `${m}:${String(r).padStart(2, '0')}`; };
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

const MAP_ART = {
  parking: 'linear-gradient(160deg,#0f1530 0%,#1d2a55 55%,#2b2f45 100%)',
  city: 'linear-gradient(160deg,#35456e 0%,#d98a54 60%,#4b3a4a 100%)',
  industrial: 'linear-gradient(160deg,#5b6a78 0%,#9aa7b2 60%,#6a655e 100%)',
  mountain: 'linear-gradient(160deg,#27385c 0%,#e7a05d 45%,#2d4a33 100%)',
};
const MODES = [
  { id: 'free', name: 'Свободная езда', desc: 'Без таймера — тренируйся и копи очки.' },
  { id: 'timed', name: 'Заезд на очки', desc: 'Набери максимум очков за отведённое время.' },
  { id: 'challenge', name: 'Испытания', desc: 'Задания с наградой: углы, серии, ворота.' },
];
const TIMES = [['auto', 'Авто'], ['day', 'День'], ['dusk', 'Закат'], ['night', 'Ночь']];
const WEATHERS = [['clear', 'Ясно'], ['rain', 'Дождь'], ['snow', 'Снег']];

export function topbar(app, title, back = true) {
  const p = app.progress, xp = p.xpInLevel;
  return `<div class="topbar">
    ${back ? '<button class="btn small" data-act="back">← Назад</button>' : ''}
    <h2>${title}</h2><div class="spacer"></div>
    <div class="lvl"><div class="row small"><b>Уровень ${p.level}</b><span class="spacer"></span><span class="muted">${xp.need ? fmtNum(xp.xp) + ' / ' + fmtNum(xp.need) : 'MAX'}</span></div><div class="bar"><i style="width:${xp.need ? Math.min(100, xp.xp / xp.need * 100) : 100}%"></i></div></div>
    <div class="chip"><span class="ic">₽</span>${fmtNum(p.money)}</div>
  </div>`;
}

export function menuScreen(app) {
  const p = app.progress, def = getCar(p.selected);
  return `<div class="screen">
    <div class="topbar"><div class="spacer"></div>
      <div class="lvl"><div class="row small"><b>Уровень ${p.level}</b><span class="spacer"></span><span class="muted">${p.xpInLevel.need ? fmtNum(p.xpInLevel.xp) + ' / ' + fmtNum(p.xpInLevel.need) : 'MAX'}</span></div><div class="bar"><i style="width:${p.xpInLevel.need ? Math.min(100, p.xpInLevel.xp / p.xpInLevel.need * 100) : 100}%"></i></div></div>
      <div class="chip"><span class="ic">₽</span>${fmtNum(p.money)}</div></div>
    <div class="menu-left">
      <h1 class="logo-big">ЗА<span>НОС</span></h1>
      <p class="tagline">Аркадный дрифт · задний привод</p>
      <button class="btn primary big" data-act="goPlay">▶ Играть</button>
      <button class="btn" data-act="goGarage">Гараж</button>
      <button class="btn" data-act="goSettings">Настройки</button>
      <button class="btn" data-act="goHints">Управление и подсказки</button>
    </div>
    <div class="carname"><b>${esc(def.brand)} ${esc(def.name)}</b><div>${esc(def.body.toUpperCase())} · RWD</div></div>
  </div>`;
}

export function playScreen(app) {
  const p = app.progress, s = app.sel;
  const map = getMap(s.map);
  const owned = Object.keys(p.data.cars);
  const maps = MAP_LIST.map((m) => {
    const d = getMap(m.id), locked = !p.mapUnlocked(d.id);
    return `<div class="card ${s.map === d.id ? 'on' : ''} ${locked ? 'locked' : ''}" data-act="pickMap" data-id="${d.id}">
      <div class="mapthumb" style="background:${MAP_ART[d.id]}"></div><h4>${esc(d.name)}</h4>
      <p>${locked ? `🔒 Нужен уровень ${d.level}` : esc(d.desc.split('.')[0] + '.')}</p></div>`;
  }).join('');
  const modes = MODES.map((m) => `<div class="tab ${s.mode === m.id ? 'on' : ''}" data-act="pickMode" data-id="${m.id}">${m.name}</div>`).join('');
  let right = '';
  if (s.mode === 'challenge') {
    right = `<div class="col scroll grow">${map.challenges.map((c) => {
      const st = p.data.challenges[c.id];
      return `<div class="chal ${s.challenge === c.id ? 'on' : ''}" data-act="pickChallenge" data-id="${c.id}">
        <div class="grow"><div class="n">${esc(c.name)}</div><div class="d">${esc(c.desc)}</div></div>
        <div style="text-align:right"><div class="${st && st.done ? 'done' : 'muted small'}">${st && st.done ? '✔ ПРОЙДЕНО' : '+' + fmtNum(c.reward.money) + ' ₽'}</div><div class="small muted">${c.time} с</div></div></div>`;
    }).join('')}</div>`;
  } else if (s.mode === 'timed') {
    right = `<div class="col"><div class="muted">Время заезда: <b class="acc">${map.timed.time} с</b></div><div class="muted">Цель: <b class="acc">${fmtNum(map.timed.goal)}</b> очков (★★ при цели, ★★★ при ×1.5)</div>
      <div class="muted">Рекорд: <b>${fmtNum(p.record(s.map, 'timed'))}</b></div><div class="small muted">Серия сгорает при ударе. Деньги = очки / 3.5 + бонус за звёзды.</div></div>`;
  } else {
    right = `<div class="col"><div class="muted">Без ограничений по времени. Выход через паузу — очки засчитываются и превращаются в деньги и опыт.</div><div class="muted">Рекорд: <b>${fmtNum(p.record(s.map, 'free'))}</b></div></div>`;
  }
  const cars = owned.map((id) => { const c = getCar(id); return `<div class="carchip ${s.car === id ? 'on' : ''}" data-act="pickCar" data-id="${id}">${esc(c.brand)} ${esc(c.name)}</div>`; }).join('');
  const times = TIMES.map(([id, n]) => `<div class="opt ${s.time === id ? 'on' : ''}" data-act="pickTime" data-id="${id}">${n}</div>`).join('');
  const weathers = WEATHERS.map(([id, n]) => `<div class="opt ${s.weather === id ? 'on' : ''}" data-act="pickWeather" data-id="${id}">${n}</div>`).join('');
  const canStart = p.mapUnlocked(s.map) && (s.mode !== 'challenge' || s.challenge);
  return `<div class="screen">
    ${topbar(app, 'Выбор <b>заезда</b>')}
    <div class="tabs">${modes}</div>
    <div class="play-grid">
      <div class="panel col scroll"><h3>Локация</h3><div class="cards">${maps}</div></div>
      <div class="panel col"><h3>${MODES.find((m) => m.id === s.mode).name}</h3><div class="muted small">${MODES.find((m) => m.id === s.mode).desc}</div>${right}</div>
    </div>
    <div class="panel col">
      <div class="row"><h3 style="margin:0;white-space:nowrap">Машина</h3><div class="carstrip grow">${cars}</div><button class="btn small" data-act="goGarage">Гараж</button></div>
      <div class="row" style="flex-wrap:wrap;gap:20px"><div class="row"><span class="muted small">ВРЕМЯ</span><div class="opts">${times}</div></div><div class="row"><span class="muted small">ПОГОДА</span><div class="opts">${weathers}</div></div><div class="spacer"></div>
        <button class="btn primary big ${canStart ? '' : 'disabled'}" data-act="start">Поехали ▶</button></div>
    </div>
  </div>`;
}

function statBars(def, tuning) {
  const base = rating(def, {}), cur = rating(def, tuning);
  const rows = [['power', 'Мощность'], ['speed', 'Скорость'], ['grip', 'Сцепление'], ['brake', 'Тормоза'], ['drift', 'Дрифт']];
  return rows.map(([k, n]) => {
    const b = base[k] * 100, c = cur[k] * 100;
    return `<div class="stat"><span>${n}</span><div class="b"><i style="width:${Math.min(b, c)}%"></i>${c > b ? `<u style="left:${b}%;width:${c - b}%"></u>` : ''}</div><b style="color:#fff">${Math.round(c)}</b></div>`;
  }).join('');
}

export function garageScreen(app) {
  const p = app.progress, g = app.garage;
  const id = g.car, def = getCar(id), owned = p.owns(id);
  const st = owned ? p.carState(id) : null;
  const tabs = [['cars', 'Машины'], ['tune', 'Тюнинг'], ['look', 'Внешний вид']].map(([k, n]) => `<div class="tab ${g.tab === k ? 'on' : ''}" data-act="garageTab" data-id="${k}">${n}</div>`).join('');
  const list = CARS.map((c) => {
    const o = p.owns(c.id), unlocked = p.carUnlocked(c.id);
    return `<div class="carrow ${id === c.id ? 'on' : ''}" data-act="garageCar" data-id="${c.id}">
      <div class="sw" style="background:${o ? p.carState(c.id).paint : c.color}"></div>
      <div><div class="nm">${esc(c.brand)} ${esc(c.name)}</div><div class="sb">${o ? (p.selected === c.id ? '✔ Выбрана' : 'В гараже') : unlocked ? 'В продаже' : '🔒 Уровень ' + c.level}</div></div>
      <div class="pr">${o ? '' : fmtMoney(c.price)}</div></div>`;
  }).join('');
  const spec = buildSpec(def, st ? st.tuning : {});
  const hp = Math.round(spec.peakTorque * (1 + spec.turbo * 0.5) * spec.peakRpm / 9549 * 1.36);
  let right = '';
  if (g.tab === 'cars' || !owned) {
    right = `<div class="col"><div><div style="font-size:24px;font-weight:900;font-style:italic">${esc(def.brand)} ${esc(def.name)}</div><div class="muted small">${esc(def.desc)}</div></div>
      <div class="row"><span class="tag acc">${hp} л.с.</span><span class="tag">${Math.round(spec.mass)} кг</span><span class="tag">RWD</span>${def.turbo ? '<span class="tag acc">Турбо</span>' : ''}<span class="tag">${def.gears.length} ст.</span></div>
      <div class="col">${statBars(def, st ? st.tuning : {})}</div>
      ${owned ? `<div class="row"><button class="btn primary ${p.selected === id ? 'disabled' : ''}" data-act="selectCar" data-id="${id}">${p.selected === id ? 'Выбрана' : 'Выбрать'}</button>
          ${Object.keys(p.data.cars).length > 1 ? `<button class="btn danger small" data-act="sellCar" data-id="${id}">Продать за ${fmtMoney(p.sellValue(id))}</button>` : '<span class="muted small">Последнюю машину продать нельзя</span>'}</div>`
        : `<div class="row"><button class="btn primary ${p.canBuy(id) ? '' : 'disabled'}" data-act="buyCar" data-id="${id}">Купить за ${fmtMoney(def.price)}</button></div>
           ${!p.carUnlocked(id) ? `<div class="bad small">Откроется на уровне ${def.level}</div>` : p.money < def.price ? `<div class="bad small">Не хватает ${fmtMoney(def.price - p.money)}</div>` : ''}`}
      </div>`;
  } else if (g.tab === 'tune') {
    right = `<div class="col scroll grow">${TUNING_PARTS.map((part) => {
      const lvl = st.tuning[part.id] || 0, nx = p.nextUpgrade(id, part.id);
      return `<div class="part"><div class="hd"><b>${part.name}</b><span class="spacer"></span><div class="pips">${Array.from({ length: part.max }, (_, i) => `<i class="${i < lvl ? 'on' : ''}"></i>`).join('')}</div></div>
        <div class="ds">${part.desc}</div>
        <div class="row"><button class="btn small primary ${nx && p.money >= nx.price ? '' : 'disabled'}" data-act="upgrade" data-id="${part.id}">${nx ? 'Улучшить · ' + fmtMoney(nx.price) : 'Максимум'}</button>
        ${lvl > 0 ? `<button class="btn small" data-act="downgrade" data-id="${part.id}">Снять (+${fmtMoney(Math.floor(upgradePrice(part.id, lvl) * 0.5))})</button>` : ''}</div></div>`;
    }).join('')}</div>
    <div class="col">${statBars(def, st.tuning)}</div>`;
  } else {
    const items = (slot, list) => `<div class="items">${list.map((it) => { const own = p.partOwned(id, slot, it.id); return `<div class="item ${st[slot] === it.id ? 'on' : ''} ${own ? 'own' : ''}" data-act="setPart" data-slot="${slot}" data-id="${it.id}">${it.name}<small>${own ? (st[slot] === it.id ? 'Установлено' : 'Есть') : fmtMoney(it.price)}</small></div>`; }).join('')}</div>`;
    right = `<div class="col scroll grow"><div class="panel"><h3>Цвет кузова</h3><div class="swatches">${PAINTS.map((c) => `<div class="swatch ${st.paint.toLowerCase() === c.toLowerCase() ? 'on' : ''}" style="background:${c}" data-act="paint" data-id="${c}"></div>`).join('')}</div>
      <div class="row" style="margin-top:8px"><span class="muted small">Свой цвет</span><input type="color" data-input="paintCustom" value="${st.paint}" style="width:60px;height:30px;border:none;background:none"></div></div>
      <div class="panel"><h3>Диски</h3>${items('wheels', WHEELS)}</div>
      <div class="panel"><h3>Обвес</h3>${items('bodykit', BODYKITS)}</div>
      <div class="panel"><h3>Спойлер</h3>${items('spoiler', SPOILERS)}</div>
      <div class="panel"><h3>Неон</h3>${items('neon', NEONS)}</div></div>`;
  }
  return `<div class="screen">
    ${topbar(app, '<b>Гараж</b>')}
    <div class="tabs">${tabs}</div>
    <div class="garage grow">
      <div class="panel col scroll">${list}</div>
      <div class="mid"></div>
      <div class="panel col ${g.tab !== 'cars' && owned ? '' : ''}" style="min-height:0">${right}</div>
    </div>
  </div>`;
}

export function settingsScreen(app) {
  const s = app.progress.settings, tab = app.setTab;
  const tabs = [['gfx', 'Графика'], ['snd', 'Звук'], ['ctl', 'Управление']].map(([k, n]) => `<div class="tab ${tab === k ? 'on' : ''}" data-act="setTab" data-id="${k}">${n}</div>`).join('');
  const sw = (k, label) => `<div class="set"><label>${label}</label><div class="switch ${s[k] ? 'on' : ''}" data-act="toggle" data-id="${k}"><i></i></div><span></span></div>`;
  const rg = (k, label, min, max, step, fmt = (v) => v) => `<div class="set"><label>${label}</label><input type="range" min="${min}" max="${max}" step="${step}" value="${s[k]}" data-input="${k}"><span class="val" id="v-${k}">${fmt(s[k])}</span></div>`;
  const choice = (k, label, list) => `<div class="set"><label>${label}</label><div class="opts">${list.map(([v, n]) => `<div class="opt ${s[k] === v ? 'on' : ''}" data-act="choice" data-key="${k}" data-id="${v}">${n}</div>`).join('')}</div><span></span></div>`;
  let body = '';
  if (tab === 'gfx') body = choice('quality', 'Качество', [['low', 'Низкое'], ['medium', 'Среднее'], ['high', 'Высокое']]) + sw('shadows', 'Тени') + sw('effects', 'Дым, следы, искры') + sw('speedFx', 'Эффект скорости') + sw('showFps', 'Показывать FPS') + rg('pixelRatio', 'Разрешение', 0.5, 1.5, 0.1, (v) => Math.round(v * 100) + '%') + rg('fov', 'Угол обзора', 55, 95, 1, (v) => v + '°');
  else if (tab === 'snd') body = rg('master', 'Общая громкость', 0, 1, 0.05, (v) => Math.round(v * 100) + '%') + rg('engine', 'Двигатель', 0, 1, 0.05, (v) => Math.round(v * 100) + '%') + rg('sfx', 'Эффекты и интерфейс', 0, 1, 0.05, (v) => Math.round(v * 100) + '%') + rg('music', 'Музыка', 0, 1, 0.05, (v) => Math.round(v * 100) + '%');
  else body = rg('assist', 'Помощник заноса', 0, 1, 0.05, (v) => v <= 0 ? 'выкл.' : Math.round(v * 100) + '%') + rg('steerSens', 'Чувствительность руля', 0.5, 1.6, 0.05, (v) => v.toFixed(2)) + rg('deadzone', 'Мёртвая зона стика', 0, 0.4, 0.01, (v) => Math.round(v * 100) + '%')
    + choice('transmission', 'Коробка передач', [['auto', 'Автомат'], ['manual', 'Механика (Q/E)']]) + choice('camera', 'Камера по умолчанию', CAMERA_MODES.map((m) => [m.id, m.name])) + choice('units', 'Единицы скорости', [['kmh', 'км/ч'], ['mph', 'миль/ч']]) + choice('touch', 'Экранные кнопки', [['auto', 'Авто'], ['on', 'Вкл.'], ['off', 'Выкл.']])
    + `<div class="set"><label>Прогресс</label><button class="btn small danger" data-act="resetSave">Сбросить сохранение</button><span></span></div>`;
  return `<div class="screen">
    ${topbar(app, '<b>Настройки</b>')}
    <div class="tabs">${tabs}</div>
    <div class="panel scroll grow"><div class="settings">${body}</div></div>
  </div>`;
}

export function hintsScreen(app) {
  return `<div class="screen center"><div class="dialog" style="max-width:1000px">
    <div class="row"><h2 style="margin:0">Управление и подсказки</h2><div class="spacer"></div><button class="btn primary" data-act="closeHints">Понятно</button></div>
    <div class="keys">
      <div class="panel"><h3>Клавиатура</h3><table>
        <tr><td><kbd>W</kbd> <kbd>↑</kbd></td><td>газ</td></tr><tr><td><kbd>S</kbd> <kbd>↓</kbd></td><td>тормоз / задний ход</td></tr>
        <tr><td><kbd>A</kbd> <kbd>D</kbd> <kbd>←</kbd> <kbd>→</kbd></td><td>руль</td></tr><tr><td><kbd>Пробел</kbd></td><td>ручной тормоз (срыв зада)</td></tr>
        <tr><td><kbd>Shift</kbd></td><td>«сброс сцепления» — рывок момента</td></tr><tr><td><kbd>Q</kbd> <kbd>E</kbd></td><td>передачи (механика)</td></tr>
        <tr><td><kbd>C</kbd></td><td>камера</td></tr><tr><td><kbd>R</kbd></td><td>вернуться на старт (серия сгорит)</td></tr>
        <tr><td><kbd>Esc</kbd> <kbd>P</kbd></td><td>пауза</td></tr><tr><td><kbd>H</kbd></td><td>эта подсказка</td></tr></table></div>
      <div class="panel"><h3>Геймпад</h3><table>
        <tr><td>RT / LT</td><td>газ / тормоз</td></tr><tr><td>Левый стик</td><td>руль</td></tr><tr><td>A</td><td>ручной тормоз</td></tr>
        <tr><td>B</td><td>сброс сцепления</td></tr><tr><td>LB / RB</td><td>передачи (механика)</td></tr><tr><td>Y</td><td>камера</td></tr><tr><td>X</td><td>вернуться на старт</td></tr><tr><td>Start</td><td>пауза</td></tr></table></div>
      <div class="panel"><h3>Экранные кнопки</h3><div class="muted small">На сенсорных экранах появляются кнопки: ◀ ▶ руль, газ, тормоз, ручник (DRIFT), рывок и пауза.</div>
        <h3 style="margin-top:12px">Очки</h3><div class="muted small">Очки = угол × скорость × время. Серия растёт во времени (×0.5 каждые 2.4 с), смена стороны заноса даёт бонус к множителю. Серия засчитывается, когда выходишь из заноса. <b class="bad">Удар или остановка сжигают серию.</b></div></div>
    </div>
    <div class="panel"><h3>Как дрифтовать</h3><ul class="tips" style="margin:0;padding-left:18px">
      <li>Разгонись до 60–80 км/ч, плавно поверни и <b>дави газ</b> — зад начнёт уходить. Или резко дёрни руль в сторону и обратно (скандинавский разворот).</li>
      <li><b>Ручник</b> — короткое нажатие в повороте мгновенно срывает зад. <b>Сброс газа</b> перед входом переносит вес на нос и тоже помогает.</li>
      <li>В заносе рули <b>в сторону скольжения</b> (помощник делает это мягко). Угол держи газом: больше газа — больше угла, меньше — машина выравнивается.</li>
      <li>Мокрый асфальт, гравий и снег срываются легче, но и скорость падает быстрее. Конусы можно сбивать — это не удар.</li>
    </ul></div>
  </div></div>`;
}

export function pauseScreen(app) {
  return `<div class="screen center"><div class="dialog" style="min-width:min(380px,92vw)">
    <h2>Пауза</h2>
    <button class="btn primary" data-act="resume">Продолжить</button>
    <button class="btn" data-act="restart">Заново</button>
    <button class="btn" data-act="cam">Камера: ${CAMERA_MODES.find((m) => m.id === app.gfx.rig.mode).name}</button>
    <button class="btn" data-act="pauseHints">Управление</button>
    <button class="btn" data-act="pauseSettings">Настройки</button>
    <button class="btn" data-act="finish">Завершить и забрать очки</button>
    <button class="btn danger" data-act="quit">Выйти без награды</button>
  </div></div>`;
}

export function resultsScreen(app) {
  const r = app.result, s = r.summary, rw = r.reward;
  const title = s.mode === 'challenge' ? (s.success ? 'Испытание пройдено!' : 'Испытание не пройдено') : s.mode === 'timed' ? 'Заезд завершён' : 'Итоги заезда';
  const stars = s.mode === 'timed' ? `<div class="stars">${[1, 2, 3].map((i) => (i <= s.stars ? '<b>★</b>' : '★')).join('')}</div>` : '';
  const ups = rw.levelUps > 0 ? `<div class="panel" style="border-color:var(--acc)"><b class="acc">🎉 Новый уровень: ${rw.level}!</b><div class="small muted">${(() => { const u = app.progress.unlocksAtLevel(rw.level); const a = [...u.cars.map((x) => 'машина «' + x + '»'), ...u.maps.map((x) => 'карта «' + x + '»')]; return a.length ? 'Открыто: ' + a.join(', ') : 'Продолжай — впереди новые машины и карты.'; })()}</div></div>` : '';
  return `<div class="screen center"><div class="dialog" style="min-width:min(560px,94vw)">
    <h2 class="${s.success ? '' : 'bad'}">${title}</h2>
    ${s.challengeName ? `<div class="muted">${esc(s.challengeName)}</div>` : ''}
    <div class="muted small">ОЧКИ</div><div class="bigscore">${fmtNum(s.score)}</div>${stars}
    ${rw.newRecord ? '<div class="ok"><b>★ Новый рекорд!</b></div>' : ''}
    <div class="stats">
      <div><span>Лучшая серия</span><b>${fmtNum(s.bestChain)}</b></div><div><span>Макс. угол</span><b>${Math.round(s.bestAngle)}°</b></div>
      <div><span>Время в дрифте</span><b>${s.driftTime.toFixed(1)} с</b></div><div><span>Макс. скорость</span><b>${Math.round(s.topSpeed)} км/ч</b></div>
      <div><span>Ударов</span><b class="${s.hits ? 'bad' : 'ok'}">${s.hits}</b></div><div><span>Сгорело очков</span><b>${fmtNum(s.lostPoints)}</b></div>
      <div><span>Дистанция</span><b>${(s.distance / 1000).toFixed(2)} км</b></div><div><span>Средний угол</span><b>${Math.round(s.avgAngle)}°</b></div>
    </div>
    <div class="rew"><div class="chip"><span class="ic">₽</span>+${fmtNum(rw.money)}</div><div class="chip"><span class="ic">XP</span>+${fmtNum(rw.xp)}</div>
      ${rw.challengeReward ? `<div class="chip">${rw.challengeReward.first ? 'Награда за испытание' : 'Повторная награда (25%)'}: +${fmtNum(rw.challengeReward.money)} ₽</div>` : ''}</div>
    ${ups}
    <div class="row" style="flex-wrap:wrap"><button class="btn primary" data-act="again">Ещё раз</button><button class="btn" data-act="toPlay">Выбор заезда</button><button class="btn" data-act="goGarage">Гараж</button><button class="btn" data-act="toMenu">Меню</button></div>
  </div></div>`;
}
