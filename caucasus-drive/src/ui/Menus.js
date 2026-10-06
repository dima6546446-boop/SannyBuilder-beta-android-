import { CARS, CAR_BY_ID, TUNING, PAINT_PALETTE } from '../config/cars.js';
import { LEVELS } from '../config/levels.js';
import { QUALITY_PRESETS, saveQualityName } from '../config/quality.js';
import { PLATE_PRESETS, PLATE_SLOTS, plateToSlots, slotsToPlate, plateStep, platePrice, plateEq, plateValid } from '../config/plates.js';
import { drawPlate } from '../vehicles/Extras.js';
import { MODEL_CREDITS } from '../config/credits.js';

const hex = (n) => `#${n.toString(16).padStart(6, '0')}`;
const rub = (n) => `${Math.round(n).toLocaleString('ru-RU')} ₽`;
const ICONS = {
  park: '<svg viewBox="0 0 24 24"><path d="M5 3h8a6 6 0 0 1 0 12H9v6H5zm4 4v4h4a2 2 0 0 0 0-4z"/></svg>',
  city: '<svg viewBox="0 0 24 24"><path d="M3 21V9l5-3v3l5-3v4h8v11zm4-3h2v-2H7zm0-4h2v-2H7zm5 4h2v-2h-2zm0-4h2v-2h-2zm5 4h2v-2h-2zm0-4h2v-2h-2z"/></svg>',
  exam: '<svg viewBox="0 0 24 24"><path d="M3 5h18v14H3zm2 2v10h14V7zm2 2h5v6H7zm7 0h4v2h-4zm0 3h4v2h-4z"/></svg>',
  drift: '<svg viewBox="0 0 24 24"><g transform="rotate(-20 14 12)"><path d="M8 9l1.4-3h7.2L18 9h1v6h-2v1.5h-2V15h-5v1.5H8V15H6V9zm1.8 0h6.4l-.8-1.8h-4.8z"/></g><circle cx="4" cy="17" r="2"/><circle cx="2.5" cy="13.5" r="1.4"/><circle cx="6" cy="20.5" r="1.3"/></svg>',
  online: '<svg viewBox="0 0 24 24"><path d="M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zm6.9 6h-2.9a15 15 0 0 0-1.4-3.6A8 8 0 0 1 18.9 8zM12 4c.8 1.2 1.5 2.5 1.9 4h-3.8c.4-1.5 1.1-2.8 1.9-4zM4.3 14a8 8 0 0 1 0-4h3.4a16 16 0 0 0 0 4zm.8 2h2.9a15 15 0 0 0 1.4 3.6A8 8 0 0 1 5.1 16zm2.9-8H5.1a8 8 0 0 1 4.3-3.6C8.9 5.5 8.4 6.7 8 8zm4 12c-.8-1.2-1.5-2.5-1.9-4h3.8c-.4 1.5-1.1 2.8-1.9 4zm2.3-6H9.7a14 14 0 0 1 0-4h4.6a14 14 0 0 1 0 4zm.3 5.6c.6-1.1 1.1-2.3 1.4-3.6h2.9a8 8 0 0 1-4.3 3.6zm1.8-5.6a16 16 0 0 0 0-4h3.4a8 8 0 0 1 0 4z"/></svg>',
  garage: '<svg viewBox="0 0 24 24"><path d="M12 3 2 8v13h4v-9h12v9h4V8zM8 14h8v2H8zm0 3h8v2H8z"/></svg>',
};

/** Характеристики машины для полосок (0..1). */
function carStats(def) {
  const s = def.spec;
  const P = s.hp * 735.5 * 0.88;
  const vmax = Math.cbrt(P / s.drag) * 3.6;
  return {
    vmax: Math.round(vmax),
    speed: Math.min(1, (vmax - 100) / 100),
    accel: Math.min(1, (s.hp / s.mass) * 9),
    handling: Math.min(1, (s.grip - 0.9) * 3 + (s.drive === 'FWD' ? 0.15 : s.drive === 'AWD' ? 0.2 : 0.05)),
    offroad: s.drive === 'AWD' ? 1 : def.dims.sill > 0.32 ? 0.5 : 0.25,
  };
}

/**
 * DOM-меню. Экраны: главное, уровни, гараж (покупка/тюнинг), настройки, пауза, результат.
 * Фоном служит 3D-гараж (GarageScene).
 */
export const TELEGRAM_URL = 'https://t.me/caucasusdrive';

/** Внешняя ссылка: в APK её перехватывает MainActivity и открывает Telegram/браузер. */
function openLink(url) {
  if (/; wv\)/.test(navigator.userAgent)) location.href = url;
  else window.open(url, '_blank', 'noopener');
}

export class Menus {
  constructor(app) {
    this.app = app;
    this.root = document.getElementById('ui');
    this.garageIndex = Math.max(0, CARS.findIndex((c) => c.id === app.save.data.current));
    this.tab = 'color';
    this.current = null;
  }

  get save() { return this.app.save; }

  _screen(html, cls = '') {
    this.root.innerHTML = `<div class="screen ${cls}">${html}</div>`;
    const el = this.root.firstElementChild;
    el.querySelectorAll('[data-go]').forEach((b) => b.addEventListener('click', () => { this.app.audio.click(); this.show(b.dataset.go); }));
    return el;
  }

  hide() { this.root.innerHTML = ''; this.current = null; }

  show(name, data) {
    this.current = name;
    this.app.onMenu(name);
    this[`_${name}`](data);
  }

  // ---------------------------------------------------------------- главное меню
  _main() {
    const s = this.save.data;
    const stars = this.save.totalStars;
    const el = this._screen(`
      <div class="topbar">
        <div class="logo"><b>CAUCASUS</b> DRIVE<small>ГАРАЖ · ШАШКИ · ЭКЗАМЕН ГИБДД</small></div>
        <div style="display:flex;gap:8px;align-items:center">
          <div class="pill daily ${this.app.game?.daily?.readyCount ? 'ready' : ''}" id="daily-pill">📋 Задания ${this.app.game?.daily ? `${this.app.game.daily.doneCount}/3` : ''}</div>
          <div class="pill">★ ${stars}/${LEVELS.length * 3}</div>
          <div class="pill">${s.license ? '🪪 Права есть' : '🚫 Без прав'}</div>
          <div class="pill gold">${rub(s.money)}</div>
        </div>
      </div>
      <div class="main-grid">
        <div class="tile hot" data-act="levels"><span class="badge">КАРЬЕРА</span><div class="ic">${ICONS.park}</div><div class="t">Парковка</div><div class="s">30 уровней · ★ ${stars}</div></div>
        <div class="tile" data-act="free"><span class="badge">ГОРОД</span><div class="ic">${ICONS.city}</div><div class="t">Свободная езда</div><div class="s">${s.drift?.freeBest ? `Такси · дрифт · рекорд ${s.drift.freeBest.toLocaleString('ru-RU')}` : 'Такси-бомбила · шашки · дрифт'}</div></div>
        <div class="tile" data-act="drift"><span class="badge">${s.drift?.best ? `★ ${s.drift.best.toLocaleString('ru-RU')}` : 'НОВОЕ'}</span><div class="ic">${ICONS.drift}</div><div class="t">Дрифт</div><div class="s">Автодром ДОСААФ</div></div>
        <div class="tile online" data-act="online"><span class="badge">${this.app.net?.active ? '● В СЕТИ' : 'НОВОЕ'}</span><div class="ic">${ICONS.online}</div><div class="t">Онлайн</div><div class="s">Город с друзьями · до 8</div></div>
        <div class="tile" data-act="exam"><span class="badge">${s.license ? 'СДАН' : '+10 000 ₽'}</span><div class="ic">${ICONS.exam}</div><div class="t">Экзамен ГИБДД</div><div class="s">Площадка + город</div></div>
        <div class="tile" data-act="garage"><span class="badge">${s.owned.length}/${CARS.length}</span><div class="ic">${ICONS.garage}</div><div class="t">Гараж</div><div class="s">${CAR_BY_ID[s.current].name} · тюнинг</div></div>
      </div>
      <div class="main-foot">
        <div>Автозаводский район · ${CAR_BY_ID[s.current].name} ${CAR_BY_ID[s.current].nick}</div>
        <div class="links"><div class="linkbtn tg" id="tg-link">✈ Наш Telegram</div><div class="linkbtn" data-go="help">Как играть</div><div class="linkbtn" data-go="settings">Настройки</div></div>
      </div>`);
    el.querySelector('#tg-link').addEventListener('click', () => openLink(TELEGRAM_URL));
    el.querySelector('#daily-pill').addEventListener('click', () => { this.app.audio.click(); this.show('daily'); });
    el.querySelectorAll('[data-act]').forEach((t) => t.addEventListener('click', () => {
      this.app.audio.click();
      const a = t.dataset.act;
      if (a === 'levels') this.show('levels');
      if (a === 'garage') this.show('garage');
      if (a === 'free') this.show('freeSetup');
      if (a === 'online') this.show('online');
      if (a === 'exam') this.app.startExam();
      if (a === 'drift') this.app.startDrift();
    }));
    this.app.garage.setFraming(0);
  }

  // ---------------------------------------------------------------- задания дня
  _daily() {
    const D = this.app.game.daily;
    D.refresh();
    const rows = D.tasks.map((t, i) => {
      const { text, reward } = D.describe(t);
      const pct = Math.round((t.progress / t.goal) * 100);
      const shown = t.kind === 'km' ? `${t.progress.toFixed(1)} / ${t.goal}` : `${Math.floor(t.progress)} / ${t.goal}`;
      const btn = t.claimed ? '<span class="dz-ok">Получено ✓</span>'
        : t.done ? `<div class="bigbtn green dz-claim" data-claim="${i}">Забрать ${rub(reward)}</div>`
          : `<span class="dz-rw">${rub(reward)}</span>`;
      return `<div class="dz ${t.done ? 'done' : ''}"><div class="dz-t">${text}</div>
        <div class="dz-bar"><i style="width:${pct}%"></i></div><div class="dz-row"><span class="dz-p">${shown}</span>${btn}</div></div>`;
    }).join('');
    const el = this._screen(`
      <div class="topbar"><div class="back" data-go="main">‹</div><div class="h1">Задания дня</div><div class="pill gold">${rub(this.save.money)}</div></div>
      <div class="dz-list">${rows}</div>
      <div class="help" style="text-align:center;margin:10px auto 0">Новые задания каждый день. Прогресс считается в любом режиме.</div>`, 'dim');
    el.querySelectorAll('[data-claim]').forEach((b) => b.addEventListener('click', () => {
      const r = D.claim(+b.dataset.claim);
      if (r) { this.app.audio.coin(); this.show('daily'); }
    }));
  }

  // ---------------------------------------------------------------- онлайн
  _online(err) {
    const s = this.save.data.settings;
    const el = this._screen(`
      <div class="topbar"><div class="back" data-go="main">‹</div><div class="h1">Онлайн</div><div class="pill">до 8 игроков</div></div>
      <div class="ol">
        <div class="ol-col">
          <label class="ol-l">Твой ник</label>
          <input id="ol-nick" class="ol-in" maxlength="16" autocomplete="off" spellcheck="false" value="${(s.nick || '').replace(/"/g, '')}" placeholder="Ник">
          <div class="ol-fines"><span>Штрафы в городе:</span>
            <div class="seg"><div data-f="1" class="${s.fines !== false ? 'sel' : ''}">Вкл</div><div data-f="0" class="${s.fines === false ? 'sel' : ''}">Выкл</div></div></div>
          <div class="help">Игроки соединяются напрямую. Лучше всего работает по Wi-Fi; через мобильный интернет у некоторых операторов соединиться не получится.</div>
        </div>
        <div class="ol-col">
          <div class="bigbtn green" id="ol-quick">⚡ Быстрая игра</div>
          <div class="ol-sub">случайные игроки в открытой комнате</div>
          <div class="bigbtn" id="ol-create">🔒 Создать комнату</div>
          <div class="ol-sub">получишь код — отправь его друзьям</div>
          <div class="ol-join"><input id="ol-code" class="ol-in code" maxlength="4" autocomplete="off" spellcheck="false" placeholder="КОД"><div class="bigbtn" id="ol-go">Войти</div></div>
          <div id="ol-status" class="ol-st ${err ? 'bad' : ''}">${err || ''}</div>
        </div>
      </div>`, 'dim');
    const nick = el.querySelector('#ol-nick'), code = el.querySelector('#ol-code');
    code.addEventListener('input', () => { code.value = code.value.toUpperCase().replace(/[^A-Z0-9]/g, ''); });
    el.querySelectorAll('[data-f]').forEach((b) => b.addEventListener('click', () => {
      s.fines = b.dataset.f === '1'; this.save.commit(); this.app.audio.click();
      el.querySelectorAll('[data-f]').forEach((x) => x.classList.toggle('sel', x === b));
    }));
    const go = (mode, c) => {
      this.app.audio.click();
      if (mode === 'join' && c.length !== 4) { el.querySelector('#ol-status').textContent = 'Код комнаты — 4 символа'; return; }
      s.nick = nick.value; this.save.commit();
      el.querySelectorAll('.bigbtn').forEach((b) => b.classList.add('busy'));
      el.querySelector('#ol-status').className = 'ol-st';
      el.querySelector('#ol-status').textContent = mode === 'quick' ? 'Ищем комнату…' : mode === 'create' ? 'Создаём комнату…' : 'Подключаемся…';
      this.app.startOnline(mode, c);
    };
    el.querySelector('#ol-quick').addEventListener('click', () => go('quick'));
    el.querySelector('#ol-create').addEventListener('click', () => go('create'));
    el.querySelector('#ol-go').addEventListener('click', () => go('join', code.value));
  }

  // ---------------------------------------------------------------- свободная езда: выбор штрафов
  _freeSetup() {
    const on = this.save.data.settings.fines !== false;
    const el = this._screen(`
      <div class="topbar"><div class="back" data-go="main">‹</div><div class="h1">Свободная езда</div><div></div></div>
      <div class="choose">
        <div class="choice ${on ? 'sel' : ''}" data-fines="1">
          <div class="ci">🚓</div>
          <div class="ct">С ШТРАФАМИ</div>
          <div class="cs">Камеры «Стрелка», посты ДПС и красный свет — за кривую езду снимают рубли</div>
        </div>
        <div class="choice ${!on ? 'sel' : ''}" data-fines="0">
          <div class="ci">😎</div>
          <div class="ct">БЕЗ ШТРАФОВ</div>
          <div class="cs">Катайся как хочешь: камеры и ДПС не трогают, деньги никто не снимет</div>
        </div>
      </div>
      <div class="help" style="text-align:center;margin:10px auto 0">Такси, АЗС, «шашки» и прогулки пешком работают в обоих вариантах.</div>`, 'dim');
    el.querySelectorAll('[data-fines]').forEach((b) => b.addEventListener('click', () => {
      this.app.audio.click();
      const fines = b.dataset.fines === '1';
      this.save.data.settings.fines = fines;
      this.save.commit();
      this.app.startFree(fines);
    }));
  }

  // ---------------------------------------------------------------- уровни
  _levels() {
    const st = this.save.data.stars;
    let next = LEVELS.findIndex((L, i) => !st[i]);
    if (next < 0) next = LEVELS.length;
    const cells = LEVELS.map((L, i) => {
      const locked = i > 0 && !st[i - 1] && !st[i];
      const n = st[i] || 0;
      return `<div class="lvl ${locked ? 'locked' : ''} ${i === next ? 'next' : ''}" data-l="${i}">
        <div class="n">${i + 1}</div><div class="st">${'<b>★</b>'.repeat(n)}${'★'.repeat(3 - n)}</div><div class="ty">${L.type}</div></div>`;
    }).join('');
    const el = this._screen(`
      <div class="topbar"><div class="back" data-go="main">‹</div><div class="h1">Парковка · автодром ДОСААФ</div><div class="pill">★ ${this.save.totalStars}/${LEVELS.length * 3}</div></div>
      <div class="level-grid">${cells}</div>`, 'dim');
    el.querySelectorAll('.lvl').forEach((c) => c.addEventListener('click', () => {
      if (c.classList.contains('locked')) { this.app.audio.beep(300, 0.1, 0.1); return; }
      this.app.audio.click();
      this.app.startParking(+c.dataset.l);
    }));
  }

  // ---------------------------------------------------------------- гараж
  _garage() {
    const def = CARS[this.garageIndex];
    const owned = this.save.owns(def.id);
    const isCur = this.save.data.current === def.id;
    const tv = this.save.tuningOf(def.id);
    const st = carStats(def);
    const bar = (name, v) => `<div class="stat">${name}<div class="bar"><i style="width:${Math.round(v * 100)}%"></i></div></div>`;
    const dots = CARS.map((c, i) => `<i class="${i === this.garageIndex ? 'on' : ''} ${this.save.owns(c.id) ? 'own' : ''}"></i>`).join('');
    const drive = { RWD: 'задний', FWD: 'передний', AWD: 'полный' }[def.spec.drive];
    const btn = owned
      ? (isCur ? '<div class="bigbtn gray">В гараже · выбрана</div>' : '<div class="bigbtn green" id="g-select">Выбрать</div>')
      : `<div class="bigbtn ${this.save.money < def.price ? 'disabled' : ''}" id="g-buy">Купить · ${rub(def.price)}</div>`;
    const el = this._screen(`
      <div class="topbar"><div class="back" data-go="main">‹</div><div class="h1">Гараж · ГСК «Жигули»</div><div class="pill gold">${rub(this.save.money)}</div></div>
      <div class="garage">
        <div class="gpanel">
          <div class="carname">${def.name}</div><div class="carnick">${def.nick}</div><div class="caryears">${def.years}</div>
          ${bar(`Скорость · ${st.vmax} км/ч`, st.speed)}${bar('Разгон', st.accel)}${bar('Управляемость', st.handling)}${bar('Проходимость', st.offroad)}
          <div class="specs">${def.spec.hp} л.с. · ${def.spec.torque} Н·м<br>Привод: ${drive} · КПП ${def.spec.gears.length - 1}-ст.<br>Масса ${def.spec.mass} кг · бак ${def.spec.tank} л</div>
        </div>
        <div class="garage-mid">
          <div class="dots">${dots}</div>
          <div class="carousel"><div class="arrowbtn" id="g-prev">‹</div>${btn}<div class="arrowbtn" id="g-next">›</div></div>
        </div>
        <div class="gpanel" id="tuning">${owned ? this._tuningHtml(def, tv) : '<div class="help">Купите машину, чтобы открыть тюнинг: покраска, диски, занижение, тонировка, чип-тюнинг и турбо.</div>'}</div>
      </div>`);
    el.querySelector('#g-prev').addEventListener('click', () => this._carousel(-1));
    el.querySelector('#g-next').addEventListener('click', () => this._carousel(1));
    el.querySelector('#g-buy')?.addEventListener('click', () => {
      if (this.save.buy(def.id)) { this.app.audio.coin(); this.app.refreshCar(); this.show('garage'); }
      else this.app.audio.beep(250, 0.2, 0.12);
    });
    el.querySelector('#g-select')?.addEventListener('click', () => { this.save.select(def.id); this.app.audio.click(); this.app.refreshCar(); this.show('garage'); });
    if (owned) this._bindTuning(el, def);
    this.app.garage.showCar(def, this.save.tuningValues(def.id));
    if (owned && this.tab === 'plate' && this.plateDraft) { // модель пересобрана — вернуть на неё черновик номера
      const p = slotsToPlate(this.plateDraft.slots);
      this.app.garage.setPlate(p, !plateEq(p, tv.plate));
    }
    this.app.garage.setFraming(0);
  }

  _carousel(d) {
    this.app.audio.click();
    this.garageIndex = (this.garageIndex + d + CARS.length) % CARS.length;
    this.show('garage');
  }

  _tuningHtml(def, tv) {
    const tabs = [['color', 'Цвет'], ['wheels', 'Диски'], ['height', 'Подвеска'], ['tint', 'Тонировка'], ['neon', 'Неон'], ['plate', 'Номера'], ['engine', 'Мотор'], ['exhaust', 'Выхлоп'], ['horn', 'Сигнал'], ['tires', 'Шины']];
    const head = `<div class="tabs">${tabs.map(([k, n]) => `<div class="tab ${this.tab === k ? 'sel' : ''}" data-tab="${k}">${n}</div>`).join('')}</div>`;
    if (this.tab === 'color') {
      const sw = PAINT_PALETTE.map((c) => `<div class="sw ${c === tv.color ? 'sel' : ''}" data-color="${c}" style="background:${hex(c)}"></div>`).join('');
      return `${head}<div class="swatches">${sw}</div><input type="range" class="hue" id="hue" min="0" max="360" value="0">
        <div class="opt" style="margin-top:8px">Покраска<span class="pr">${rub(TUNING.paintPrice)}</span></div>`;
    }
    if (this.tab === 'plate') return head + this._plateHtml(def, tv);
    const list = TUNING[this.tab];
    const cur = tv[this.tab];
    return head + list.map((o) => {
      const sel = o.id === cur;
      return `<div class="opt ${sel ? 'sel' : ''}" data-opt="${o.id}">${o.name}<span class="pr ${sel ? 'own' : ''}">${sel ? 'Установлено' : o.price ? rub(o.price) : 'бесплатно'}</span></div>`;
    }).join('');
  }

  _bindTuning(el, def) {
    const panel = el.querySelector('#tuning');
    const rebind = () => { panel.innerHTML = this._tuningHtml(def, this.save.tuningOf(def.id)); this._bindTuning(el, def); };
    const g = this.app.garage;
    panel.querySelectorAll('[data-tab]').forEach((t) => t.addEventListener('click', () => {
      if (this.tab === 'plate') g.setPlate(this.save.tuningOf(def.id).plate); // черновик номера не сохранён — вернуть купленный
      this.tab = t.dataset.tab;
      this.plateDraft = null;
      this.app.audio.click();
      rebind();
    }));
    if (this.tab === 'plate') this._bindPlate(panel, def);
    panel.querySelectorAll('[data-color]').forEach((s) => s.addEventListener('click', () => {
      const c = +s.dataset.color;
      if (c === this.save.tuningOf(def.id).color) return;
      if (!this.save.spend(TUNING.paintPrice)) { this.app.audio.beep(250, 0.2, 0.12); this.app.hudToastMenu('Не хватает денег'); return; }
      this.save.setTuning(def.id, { color: c });
      g.setColor(c);
      this.app.audio.coin();
      this.app.refreshCar();
      this.show('garage');
    }));
    const hue = panel.querySelector('#hue');
    if (hue) {
      hue.addEventListener('input', () => g.setColor(hslToHex(+hue.value)));
      hue.addEventListener('change', () => {
        const c = hslToHex(+hue.value);
        if (!this.save.spend(TUNING.paintPrice)) { g.setColor(this.save.tuningOf(def.id).color); this.app.hudToastMenu('Не хватает денег'); return; }
        this.save.setTuning(def.id, { color: c });
        this.app.audio.coin();
        this.app.refreshCar();
        this.show('garage');
      });
    }
    panel.querySelectorAll('[data-opt]').forEach((o) => o.addEventListener('click', () => {
      const tab = this.tab;
      const raw = o.dataset.opt;
      const id = tab === 'wheels' ? raw : +raw;
      const opt = TUNING[tab].find((x) => String(x.id) === String(id));
      if (this.save.tuningOf(def.id)[tab] === id) return;
      if (!this.save.spend(opt.price)) { this.app.audio.beep(250, 0.2, 0.12); this.app.hudToastMenu('Не хватает денег'); return; }
      this.save.setTuning(def.id, { [tab]: id });
      const tv = this.save.tuningValues(def.id);
      if (tab === 'wheels') g.setWheels(tv.wheels);
      if (tab === 'height') g.setHeight(tv.height);
      if (tab === 'tint') g.setTint(tv.tint);
      if (tab === 'neon' || tab === 'plate') g.setExtras(tv);
      this.app.audio.coin();
      if (tab === 'horn') { this.app.audio.init(); this.app.audio.hornType = tv.horn; this.app.audio.hornSample(tv.horn); }
      if (tab === 'exhaust' && tv.exhaust) { this.app.audio.init(); this.app.audio.pops(4); }
      this.app.refreshCar();
      this.show('garage');
    }));
  }

  // ---------------------------------------------------------------- редактор номера
  /**
   * Номер «Б ЦЦЦ ББ РЕГ»: каждая позиция листается стрелками ▲▼ или тапом по символу
   * (без системной клавиатуры — в WebView она закрывает пол-экрана). Черновик виден сразу
   * на canvas и на машине в 3D-гараже; платится только «Поставить».
   */
  _plateHtml(def, tv) {
    if (!this.plateDraft || this.plateDraft.car !== def.id) this.plateDraft = { car: def.id, slots: plateToSlots(tv.plate) };
    const slots = this.plateDraft.slots.map((ch, k) => `<div class="pl-slot ${k === 0 || k === 4 || k === 6 ? 'gap' : ''} ${k >= 6 ? 'reg' : ''}" data-k="${k}">
        <div class="pl-arr" data-d="1">▲</div><div class="pl-ch" data-d="1">${ch || '·'}</div><div class="pl-arr" data-d="-1">▼</div></div>`).join('');
    const quick = PLATE_PRESETS.map((p, i) => `<div class="pl-q" data-pi="${i}">${p.text} ${p.region}<small>${p.name}</small></div>`).join('');
    return `<div class="plate-ed">
      <canvas id="pl-prev" width="520" height="112"></canvas>
      <div class="pl-slots">${slots}</div>
      <div class="pl-info" id="pl-info"></div>
      <div class="bigbtn" id="pl-buy"></div>
      <div class="pl-qh">Быстрый выбор</div>
      <div class="pl-quick">${quick}</div>
    </div>`;
  }

  _bindPlate(panel, def) {
    const g = this.app.garage, d = this.plateDraft;
    const cv = panel.querySelector('#pl-prev').getContext('2d');
    const info = panel.querySelector('#pl-info'), buy = panel.querySelector('#pl-buy');
    const refresh = () => {
      const p = slotsToPlate(d.slots);
      const cur = this.save.tuningOf(def.id).plate;
      drawPlate(cv, p.text, p.region);
      panel.querySelectorAll('.pl-slot').forEach((el, k) => { el.querySelector('.pl-ch').textContent = d.slots[k] || '·'; });
      const own = plateEq(p, cur);
      const { price, tags, pretty } = platePrice(p);
      d.price = own ? 0 : price;
      info.innerHTML = own ? '<span class="ok">Этот номер уже стоит</span>'
        : `${pretty ? '<b>Красивый номер</b>' : 'Обычный номер'}${tags.length ? ` · ${tags.join(', ')}` : ''}`;
      buy.textContent = own ? 'Установлено' : `Поставить · ${rub(price)}`;
      buy.className = `bigbtn ${own ? 'gray' : this.save.money < price ? 'disabled' : 'green'}`;
      g.setPlate(p, !own);
    };
    panel.querySelectorAll('.pl-slot').forEach((el) => {
      const k = +el.dataset.k;
      el.querySelectorAll('[data-d]').forEach((b) => b.addEventListener('click', () => {
        d.slots = plateStep(d.slots, k, +b.dataset.d);
        this.app.audio.click();
        refresh();
      }));
    });
    panel.querySelectorAll('[data-pi]').forEach((el) => el.addEventListener('click', () => {
      d.slots = plateToSlots(PLATE_PRESETS[+el.dataset.pi]);
      this.app.audio.click();
      refresh();
    }));
    buy.addEventListener('click', () => {
      const p = slotsToPlate(d.slots);
      if (!plateValid(p) || plateEq(p, this.save.tuningOf(def.id).plate)) return;
      if (!this.save.spend(d.price)) { this.app.audio.beep(250, 0.2, 0.12); this.app.hudToastMenu('Не хватает денег'); return; }
      this.save.setTuning(def.id, { plate: p });
      g.setExtras(this.save.tuningValues(def.id));
      this.app.audio.coin();
      this.app.refreshCar();
      this.show('garage');
    });
    refresh();
  }

  // ---------------------------------------------------------------- настройки
  _settings() {
    const s = this.save.data.settings;
    const q = this.app.quality.name;
    const seg = (key, opts) => `<div class="seg">${opts.map(([v, n]) => `<div class="${String(s[key]) === String(v) ? 'sel' : ''}" data-k="${key}" data-v="${v}">${n}</div>`).join('')}</div>`;
    const el = this._screen(`
      <div class="topbar"><div class="back" data-go="${this.app.paused ? 'pause' : 'main'}">‹</div><div class="h1">Настройки</div><div></div></div>
      <div class="settings">
        <div class="setrow"><div class="lab">Графика</div><div class="seg">${Object.entries(QUALITY_PRESETS).map(([k, p]) => `<div class="${k === q ? 'sel' : ''}" data-q="${k}">${p.label}</div>`).join('')}</div></div>
        <div class="setrow"><div class="lab">Руление</div>${seg('controls', [['wheel', 'Руль'], ['arrows', 'Стрелки'], ['tilt', 'Наклон']])}</div>
        <div class="setrow"><div class="lab">Коробка передач</div>${seg('gearbox', [['auto', 'Автомат'], ['manual', 'Механика']])}</div>
        <div class="setrow"><div class="lab">Громкость</div>${seg('volume', [[0, 'Выкл'], [0.4, 'Тихо'], [0.8, 'Норм'], [1, 'Громко']])}</div>
        <div class="setrow"><div class="lab">Помощь при заносе</div>${seg('assist', [[true, 'Вкл'], [false, 'Выкл']])}</div>
        <div class="setrow"><div class="lab">Прогресс</div><div class="seg"><div id="reset">Сбросить всё</div></div></div>
      </div>
      <div class="help" style="margin-top:14px">Смена качества графики перезапускает игру. Прогресс сохраняется на устройстве.</div>`, 'dim');
    el.querySelectorAll('[data-q]').forEach((b) => b.addEventListener('click', () => { saveQualityName(b.dataset.q); location.reload(); }));
    el.querySelectorAll('[data-k]').forEach((b) => b.addEventListener('click', () => {
      let v = b.dataset.v;
      if (v === 'true' || v === 'false') v = v === 'true';
      else if (!isNaN(+v)) v = +v;
      s[b.dataset.k] = v;
      this.save.commit();
      this.app.applySettings();
      this.app.audio.click();
      this.show('settings');
    }));
    el.querySelector('#reset').addEventListener('click', () => {
      if (confirm('Сбросить деньги, машины и звёзды?')) { this.save.reset(); location.reload(); }
    });
  }

  _help() {
    this._screen(`
      <div class="topbar"><div class="back" data-go="main">‹</div><div class="h1">Как играть</div><div></div></div>
      <div class="help" style="margin-top:10px;max-width:760px;font-size:14px">
        <b>Управление:</b> руль слева (или стрелки/наклон), справа — газ, тормоз, рычаг <b>R · N · D</b> и ручник.
        Поворотники — кнопки под картой (Z/X на клавиатуре). Свайп по экрану — осмотреться. Камеры — кнопка с фотоаппаратом.<br><br>
        <b>Парковка:</b> поставьте машину в жёлтую зону по стрелке и остановитесь. Любое касание — провал. Быстрее норматива — три звезды.<br><br>
        <b>Город:</b> кнопка «такси» — возите пассажиров за рубли. Обгоны впритирку на скорости (<b>ШАШКИ</b>) и дрифт идут в одну серию: итог — рубли, рекорд серии в городе виден на панели дрифта.<br><br><b>Дрифт:</b> срыв — ручником или перегазовкой (отпустить газ и снова в пол с вывернутым рулём, задний привод), удержание — газом и контррулём. Очки = угол × скорость × плавность; множитель растёт за длинный занос и связки (перекладка — ×+1), рядом со стеной или машиной — бонус. Удар, конус или разворот сжигают серию. «Помощь при заносе» в настройках подруливает сама. Дрифт-зона ДОСААФ — заезд 90 с с рекордом и наградой.
        Камеры «Стрелка» и посты ДПС штрафуют за нарушения. Бензин — на АЗС (красная точка на карте).<br><br>
        <b>Онлайн:</b> «Быстрая игра» — город со случайными игроками, «Создать комнату» — получишь код из 4 символов, друзья вводят его и попадают к тебе (до 8 человек). Кнопка с облачком — быстрые фразы. Если создатель комнаты выйдет, катаешься дальше один. Лучше всего работает по Wi-Fi.<br><br>
        <b>Экзамен ГИБДД:</b> змейка, параллельная парковка и гараж задом, потом маршрут по городу. Не набирайте 5 штрафных баллов, включайте поворотники!<br><br>
        ${MODEL_CREDITS.length ? `<b>Модели машин (CC BY):</b> ${MODEL_CREDITS.map((c) => `${c.name} — ${c.author}`).join('; ')}.<br><br>` : ''}
        <b>Клавиатура:</b> WASD — езда, Пробел — ручник, Q/E — передачи, Z/X/V — поворотники/аварийка, H — гудок, C — камера, L — фары, T — такси, F — действие, Esc — пауза.
      </div>`, 'dim');
  }

  // ---------------------------------------------------------------- пауза и результат
  _pause() {
    const mode = this.app.game.mode?.name;
    const el = this._screen(`
      <div class="modal">
        <div class="h1">Пауза</div>
        ${this.app.net?.active ? `<div class="help" style="text-align:center">Онлайн: комната <b>${this.app.net.pub ? 'открытая' : this.app.net.room}</b> · игроков ${this.app.net.count}. Другие видят твою машину на месте.</div>` : ''}
        <div class="row">
          <div class="bigbtn green" id="p-cont">Продолжить</div>
          ${mode !== 'free' ? '<div class="bigbtn" id="p-restart">Заново</div>' : ''}
        </div>
        <div class="row">
          <div class="linkbtn" id="p-cam">Камера</div>
          <div class="linkbtn" data-go="settings">Настройки</div>
          <div class="linkbtn" id="p-menu">${this.app.net?.active ? 'Выйти из онлайна' : 'В меню'}</div>
        </div>
      </div>`, 'dim');
    el.querySelector('#p-cont').addEventListener('click', () => this.app.resume());
    el.querySelector('#p-restart')?.addEventListener('click', () => this.app.restart());
    el.querySelector('#p-menu').addEventListener('click', () => this.app.toMenu());
    el.querySelector('#p-cam').addEventListener('click', () => { this.app.game.cameraRig.next(); this.app.resume(); });
  }

  _result(r) {
    if (r.drift) { this._driftResult(r); return; }
    const stars = r.stars ? `<div class="stars">${'<b>★</b>'.repeat(r.stars)}${'★'.repeat(3 - r.stars)}</div>` : '';
    const title = r.exam ? (r.ok ? 'Экзамен сдан! 🪪' : 'Экзамен не сдан') : (r.ok ? 'Припарковано!' : 'Провал');
    const log = r.log?.length ? `<div class="help" style="margin-top:8px">${r.log.join('<br>')}</div>` : '';
    const hasNext = !r.exam && r.ok && r.level < LEVELS.length - 1;
    const el = this._screen(`
      <div class="modal">
        <div class="h1">${title}</div>
        ${stars}
        ${r.ok && r.reward ? `<div class="reward">+${rub(r.reward)}${r.first ? ' · первый раз ×2' : ''}</div>` : ''}
        ${r.time ? `<div class="help">Время: ${r.time.toFixed(1)} с</div>` : ''}
        ${!r.ok ? `<div class="why">${r.why}</div>` : ''}
        ${log}
        <div class="row">
          ${hasNext ? '<div class="bigbtn green" id="r-next">Далее</div>' : ''}
          <div class="bigbtn ${r.ok ? 'gray' : ''}" id="r-retry">Заново</div>
          <div class="bigbtn gray" id="r-menu">Меню</div>
        </div>
      </div>`, 'dim');
    el.querySelector('#r-next')?.addEventListener('click', () => this.app.startParking(r.level + 1));
    el.querySelector('#r-retry').addEventListener('click', () => (r.exam ? this.app.startExam() : this.app.startParking(r.level)));
    el.querySelector('#r-menu').addEventListener('click', () => this.app.toMenu(r.exam ? 'main' : 'levels'));
  }

  _driftResult(r) {
    const el = this._screen(`
      <div class="modal">
        <div class="h1">${r.record && r.score > 0 ? 'Новый рекорд! 🏁' : 'Заезд окончен'}</div>
        <div class="reward" style="font-size:30px">${r.score.toLocaleString('ru-RU')} очков</div>
        <div class="help">Серий: ${r.series} · рекорд зоны: ${r.best.toLocaleString('ru-RU')}</div>
        ${r.reward ? `<div class="reward">+${rub(r.reward)}</div>${r.record && r.score > 0 ? '<div class="help">в том числе 1 000 ₽ за рекорд</div>' : ''}` : ''}
        <div class="row">
          <div class="bigbtn green" id="r-retry">Ещё заезд</div>
          <div class="bigbtn gray" id="r-menu">Меню</div>
        </div>
      </div>`, 'dim');
    el.querySelector('#r-retry').addEventListener('click', () => this.app.startDrift());
    el.querySelector('#r-menu').addEventListener('click', () => this.app.toMenu('main'));
  }

  showResult(r) { this.app.showResult(r); }

  _loading(text) {
    this._screen(`<div class="loading"><div class="logo"><b>CAUCASUS</b> DRIVE<small>${text || 'ЗАГРУЗКА…'}</small></div><div class="bar"><i></i></div></div>`, 'dim');
  }
}

function hslToHex(h, s = 0.7, l = 0.42) {
  const k = (n) => (n + h / 30) % 12;
  const a = s * Math.min(l, 1 - l);
  const f = (n) => Math.round(255 * (l - a * Math.max(-1, Math.min(k(n) - 3, Math.min(9 - k(n), 1)))));
  return (f(0) << 16) | (f(8) << 8) | f(4);
}
