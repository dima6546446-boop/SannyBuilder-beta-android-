import { CITY, coord } from '../world/RoadGraph.js';
import { VIOLATION } from './Rules.js';
import { mulberry32 } from '../utils/math.js';
import { DriftController } from './DriftController.js';

const { N, HALF } = CITY;
const SEG = CITY.SPACING - 2 * HALF;
const FUEL_PRICE = 56;
let comboStamp = 0; // номер кадра для «шашек» (общий для всех заездов)

const HELLO = [
  'Шеф, до ДК подбросишь?', 'Только не гони, у меня рассада!', 'На «Жигулях»? Ну давай, с ветерком!',
  'Командир, свободен? Опаздываю!', 'А музыку можно погромче?', 'Мне к тёще, но можно не торопиться…',
  'Поехали! Как Гагарин говорил.', 'Главное — довези целым.',
];
const OUCH = ['Эй, аккуратнее!', 'Я на такое не подписывался!', 'У меня яйца в сумке!', 'Шеф, ты права где купил?'];
const BYE = ['Спасибо, шеф! Сдачи не надо.', 'Довёз как короля!', 'Держи на бензин.', 'Отлично доехали!'];

/**
 * Свободная езда по городу: такси-«бомбила», АЗС, камеры «Стрелка» и посты ДПС
 * со штрафами, «шашки» (обгоны впритирку) с комбо-множителем и дрифт с судейством
 * (DriftController: угол × скорость × плавность, связки, близость к стенам; на автодроме ДОСААФ ×1,5).
 */
export class FreeRideMode {
  constructor(game, { fines = true } = {}) {
    this.g = game;
    this.fines = fines; // false — камеры, ДПС и нарушения ничего не снимают
    this.name = 'free';
    this.allowWalk = true; // можно выйти из машины и гулять
    this.rnd = mulberry32(Date.now() & 0xffff);
  }

  enter(spawn) {
    const g = this.g;
    g.traffic.enabled = true;
    g.traffic.target = g.q.trafficCount;
    const [x, z, h] = spawn || [coord(3) - CITY.LANES[1], coord(2) + HALF + 20, 0];
    g.player.place(x, z, h);
    g.cameraRig.snap();
    g.traffic.clear();
    Object.assign(g.traffic.center, { x, z });
    g.traffic.prefill(g.camera);
    this.taxi = { on: false, stage: 'off', timer: 0 };
    this.combo = { pts: 0, mult: 1, events: 0, timer: 0, kind: '' };
    this.drift = new DriftController(g, { onBank: (total, info) => this._bankDrift(total, info) });
    this.inZone = false;
    this.camCooldown = new Map();
    this.lowFuelWarned = false;
    g.hud.showLimit(true);
    g.hud.showSensors(false);
    g.hud.hideMission();
    document.getElementById('btn-taxi').classList.remove('hidden');
    g.rules.reset();
    g.rules.onViolation = (v, extra) => this._violation(v, extra);
    g.crowd.start();
    const rainParam = new URLSearchParams(location.search).has('rain');
    g.setWeather(rainParam || Math.random() < 0.3 ? 1 : 0, !rainParam);
    if (rainParam) g.weather.level = 1;
    g.hud.toast(`Свободная езда${this.fines ? '' : ' без штрафов'}. Нажми «ТАКСИ», чтобы брать заказы`, 'good', 3.5);
  }

  exit() {
    const g = this.g;
    g.crowd.stop();
    g.rules.onViolation = null;
    this._endTaxi(true);
    this._payCombo();
    this.drift.bank();
    this.drift.hide();
    document.getElementById('btn-taxi').classList.add('hidden');
    g.hud.setAction(null);
    g.hud.nav(null);
    g.hud.combo(null);
  }

  get extraColliders() { return []; }

  // ---------------------------------------------------------------- штрафы
  _nearDps(x, z, r = 90) { return this.g.city.dpsPosts.some((p) => Math.hypot(p.x - x, p.z - z) < r); }

  _fine(amount, text, source) {
    const g = this.g;
    if (!this.fines) return;
    g.save.addMoney(-amount);
    g.save.data.stats.fines += amount;
    g.save.commit();
    if (source === 'camera') { g.hud.flash(); g.audio.shutter(); } else g.audio.siren();
    g.hud.toast(`${source === 'camera' ? '📸 ' : '🚓 '}${text} — штраф ${amount.toLocaleString('ru-RU')} ₽`, 'bad', 3.5);
  }

  _violation(v) {
    const p = this.g.player.physics;
    if (v === VIOLATION.RED) { this._fine(v.fine, 'Камера «Безопасный город»: проезд на красный', 'camera'); return; }
    if (v === VIOLATION.SPEED) return; // скорость ловят камеры (см. _cameras) и посты
    if (this._nearDps(p.x, p.z)) this._fine(v.fine, `Инспектор ДПС: ${v.text.toLowerCase()}`, 'dps');
  }

  _cameras(dt) {
    const g = this.g, p = g.player.physics;
    const kmh = p.speed * 3.6;
    for (const [k, t] of this.camCooldown) { if (t - dt <= 0) this.camCooldown.delete(k); else this.camCooldown.set(k, t - dt); }
    g.city.cameras.forEach((c, i) => {
      if (this.camCooldown.has(i)) return;
      if (Math.hypot(c.x - p.x, c.z - p.z) > 22) return;
      if (kmh > 80) {
        const over = kmh - 60;
        const fine = over > 60 ? 2500 : over > 40 ? 1500 : 500;
        this._fine(fine, `Камера «Стрелка»: ${Math.round(kmh)} км/ч при 60`, 'camera');
        this.camCooldown.set(i, 10);
      }
    });
    if (kmh > 90 && this._nearDps(p.x, p.z, 60) && !this.camCooldown.has('dps')) {
      this._fine(1500, `Инспектор ДПС: превышение ${Math.round(kmh)} км/ч`, 'dps');
      this.camCooldown.set('dps', 12);
    }
  }

  // ---------------------------------------------------------------- топливо и АЗС
  _fuel() {
    const g = this.g, p = g.player.physics;
    const k = p.fuel / p.spec.tank;
    if (k < 0.15 && !this.lowFuelWarned) { this.lowFuelWarned = true; g.hud.toast('Мало бензина! АЗС отмечена на карте', 'bad', 3); }
    if (k > 0.3) this.lowFuelWarned = false;
    if (p.fuel <= 0 && p.speed < 0.2 && !this._evac) {
      this._evac = true;
      setTimeout(() => {
        g.save.addMoney(-1500);
        p.fuel = 6;
        g.hud.toast('Бензин кончился. Знакомый привёз канистру: −1 500 ₽', 'bad', 3.5);
        this._evac = false;
      }, 1500);
    }
    const azs = g.city.azs;
    this.atPump = false;
    if (azs && p.speed < 0.5) {
      for (const z of azs.zones) {
        if (Math.abs(p.x - z.x) < z.w / 2 + 0.5 && Math.abs(p.z - z.z) < z.d / 2 + 0.5) { this.atPump = true; break; }
      }
    }
    const need = p.spec.tank - p.fuel;
    if (this.atPump && need > 0.5) g.hud.setAction(`ЗАПРАВИТЬ · ${Math.ceil(need * FUEL_PRICE).toLocaleString('ru-RU')} ₽`);
    else if (this.taxi.stage !== 'off') g.hud.setAction(null);
    else g.hud.setAction(null);
  }

  onAction() {
    const g = this.g, p = g.player.physics;
    if (!this.atPump) return;
    const need = p.spec.tank - p.fuel;
    const afford = Math.min(need, g.save.money / FUEL_PRICE);
    if (afford < 0.5) { g.hud.toast('Не хватает денег на бензин', 'bad'); return; }
    g.save.addMoney(-Math.ceil(afford * FUEL_PRICE));
    p.fuel += afford;
    g.daily.progress('fuel', 1);
    g.audio.coin();
    g.hud.toast(`Заправлено ${afford.toFixed(1)} л АИ-92`, 'money');
  }

  // ---------------------------------------------------------------- такси
  onTaxi() {
    if (this.taxi.on) { this._endTaxi(); return; }
    this.taxi = { on: true, stage: 'wait', timer: 2 + this.rnd() * 3 };
    this.g.player.setTaxiSign(true);
    document.getElementById('btn-taxi').classList.add('on');
    this.g.hud.toast('Шашечки на крыше. Ждём заказ…', 'good');
  }

  _endTaxi(silent) {
    const g = this.g;
    if (!this.taxi.on) return;
    this.taxi = { on: false, stage: 'off', timer: 0 };
    g.player.setTaxiSign(false);
    g.beacon.hide();
    g.ped.hide();
    g.hud.nav(null);
    g.hud.hideMission();
    document.getElementById('btn-taxi').classList.remove('on');
    if (!silent) g.hud.toast('Смена окончена', 'good');
  }

  /** Случайная точка на тротуаре у дороги (правая сторона по ходу участка). */
  _sidewalkSpot(nearX, nearZ, minD, maxD) {
    for (let k = 0; k < 40; k++) {
      const i = (this.rnd() * N) | 0, j = (this.rnd() * N) | 0;
      const horiz = this.rnd() < 0.5;
      if ((horiz && i >= N - 1) || (!horiz && j >= N - 1)) continue;
      const u = 20 + this.rnd() * (SEG - 40);
      const side = this.rnd() < 0.5 ? 1 : -1;
      const x = horiz ? coord(i) + HALF + u : coord(i) + side * (HALF + 1.6);
      const z = horiz ? coord(j) + side * (HALF + 1.6) : coord(j) + HALF + u;
      const d = Math.hypot(x - nearX, z - nearZ);
      if (d >= minD && d <= maxD) return { x, z, roadX: horiz ? x : coord(i) + side * 5.2, roadZ: horiz ? coord(j) + side * 5.2 : z };
    }
    return null;
  }

  _taxi(dt) {
    const g = this.g, p = g.player.physics, T = this.taxi;
    if (!T.on) return;
    T.timer -= dt;
    if (T.stage === 'wait' && T.timer <= 0) {
      const s = this._sidewalkSpot(p.x, p.z, 120, 380);
      if (!s) { T.timer = 1; return; }
      T.stage = 'pickup';
      T.spot = s;
      g.beacon.show(s.roadX, s.roadZ, 0xffcc33);
      g.ped.show(s.x, CITY.CURB, s.z, [0x2d4a7a, 0x7a2d2d, 0x2d7a4a, 0x6a5a2a][(this.rnd() * 4) | 0]);
      g.audio.beep(1200, 0.08, 0.1); g.audio.beep(1600, 0.08, 0.1);
      g.hud.toast('Новый заказ! Забери пассажира', 'money');
    }
    if (T.stage === 'pickup') {
      const d = Math.hypot(T.spot.roadX - p.x, T.spot.roadZ - p.z);
      g.hud.mission('Такси · посадка', `Пассажир ждёт: ${Math.round(d)} м. Остановитесь у метки`);
      g.hud.nav({ x: T.spot.roadX, z: T.spot.roadZ });
      g.ped.update(dt, p.x, p.z);
      if (d < 6 && p.speed < 1) {
        const dest = this._sidewalkSpot(p.x, p.z, 300, 850);
        if (!dest) return;
        T.stage = 'ride';
        T.dest = dest;
        T.dist = Math.hypot(dest.x - p.x, dest.z - p.z);
        T.limit = T.dist / 7.5 + 25;
        T.time = 0;
        T.rating = 5;
        g.ped.hide();
        g.beacon.show(dest.roadX, dest.roadZ, 0x35e07a);
        g.hud.toast(`«${HELLO[(this.rnd() * HELLO.length) | 0]}»`, 'good', 3.5);
      }
    }
    if (T.stage === 'ride') {
      T.time += dt;
      const d = Math.hypot(T.dest.roadX - p.x, T.dest.roadZ - p.z);
      const left = Math.max(0, T.limit - T.time);
      g.hud.mission('Такси · везём пассажира', `${Math.round(d)} м · осталось ${Math.ceil(left)} с · ${'★'.repeat(T.rating)}${'☆'.repeat(5 - T.rating)}`, left / T.limit);
      g.hud.nav({ x: T.dest.roadX, z: T.dest.roadZ });
      if (d < 6 && p.speed < 1) {
        let fare = 150 + T.dist * 0.9;
        if (T.time < T.limit * 0.7) fare *= 1.3;
        if (T.time > T.limit) fare *= 0.5;
        fare = Math.round(fare * (T.rating / 5));
        g.save.addMoney(fare);
        g.save.data.stats.taxi++;
        g.daily.progress('taxi', 1);
        g.save.commit();
        g.audio.coin();
        g.hud.toast(`«${BYE[(this.rnd() * BYE.length) | 0]}» +${fare.toLocaleString('ru-RU')} ₽`, 'money', 3.5);
        g.beacon.hide();
        g.hud.nav(null);
        T.stage = 'wait';
        T.timer = 3 + this.rnd() * 4;
        g.hud.mission('Такси', 'Ждём следующий заказ…');
      }
    }
    if (T.stage === 'wait' && T.timer > 0) g.hud.mission('Такси', 'Ждём заказ…');
  }

  // ---------------------------------------------------------------- «шашки»
  _combo(dt) {
    const g = this.g, p = g.player.physics, c = this.combo;
    const s = Math.sin(p.heading), co = Math.cos(p.heading);
    const kmh = p.speed * 3.6;
    // положение «впереди/позади» с прошлого кадра хранится на самой машине (без Map/Set в кадре)
    const stamp = ++comboStamp;
    for (const car of g.traffic.cars) {
      const dx = car.x - p.x, dz = car.z - p.z;
      if (Math.abs(dx) > 30 || Math.abs(dz) > 30) continue;
      const along = dx * s + dz * co;
      const side = -dx * co + dz * s;
      const prev = car._comboSeen === stamp - 1 ? car._comboAlong : undefined;
      car._comboSeen = stamp;
      car._comboAlong = along;
      if (prev !== undefined && prev > 0 && along <= 0 && Math.abs(side) < 3.1 && kmh > 60) {
        const pts = Math.round((3.1 - Math.abs(side)) * 160 + kmh * 2);
        this._addCombo(pts, Math.abs(side) < 2.2 ? 'ВПРИТИРКУ!' : 'ШАШКИ');
      }
    }
    if (c.pts > 0) {
      c.timer -= dt;
      g.hud.combo(`${c.kind} ×${c.mult} · ${Math.round(c.pts)}`);
      if (c.timer <= 0) this._payCombo();
    }
  }

  _addCombo(pts, kind) {
    const c = this.combo;
    c.events++;
    c.mult = 1 + Math.floor(c.events / 3);
    c.pts += pts * c.mult;
    c.timer = 4;
    c.kind = kind;
    this.g.audio.beep(900 + c.mult * 150, 0.07, 0.1, 'triangle');
  }

  _payCombo() {
    const c = this.combo;
    if (!c || c.pts <= 0) return;
    const money = Math.round(c.pts / 4);
    const g = this.g;
    g.save.addMoney(money);
    g.daily.progress('drift', Math.round(c.pts));
    if (c.pts > g.save.data.stats.bestCombo) { g.save.data.stats.bestCombo = Math.round(c.pts); g.save.commit(); }
    g.hud.toast(`${c.kind} ×${c.mult}: ${Math.round(c.pts)} очков → +${money.toLocaleString('ru-RU')} ₽`, 'money');
    g.audio.coin();
    this.combo = { pts: 0, mult: 1, events: 0, timer: 0, kind: '' };
    g.hud.combo(null);
  }

  // ---------------------------------------------------------------- дрифт
  _drift(dt) {
    const g = this.g, p = g.player.physics;
    const zone = g.city.inAutodrome(p.x, p.z);
    if (zone !== this.inZone) {
      this.inZone = zone;
      if (zone) g.hud.toast('Дрифт-зона ДОСААФ: очки дрифта ×1,5', 'good', 2.5);
    }
    this.drift.update(dt, { zone: zone ? 1.5 : 1 });
  }

  /** Серия дрифта сохранена: рубли, задание дня, рекорды. Возвращает начисленные рубли. */
  _bankDrift(total) {
    const g = this.g, st = g.save.data;
    const money = Math.round(total / 5);
    if (money > 0) g.save.addMoney(money);
    g.daily.progress('drift', total);
    if (total > st.stats.bestCombo) st.stats.bestCombo = total;
    if (total > st.drift.bestSeries) st.drift.bestSeries = total;
    g.save.commit();
    return money;
  }

  onCrash(strength) {
    const g = this.g;
    if (strength > 0.08) this.drift.lose('Удар');
    if (this.combo.pts > 0) {
      g.hud.toast('Шашки не удались! Комбо сгорело', 'bad');
      this.combo = { pts: 0, mult: 1, events: 0, timer: 0, kind: '' };
      g.hud.combo(null);
    }
    if (this.taxi.stage === 'ride' && strength > 0.15) {
      this.taxi.rating = Math.max(1, this.taxi.rating - 1);
      g.hud.toast(`«${OUCH[(this.rnd() * OUCH.length) | 0]}»`, 'bad');
    }
  }

  onContact() {}

  /** Сбит прохожий (сам встанет): штраф, если штрафы включены; комбо сгорает. */
  onPedHit() {
    const g = this.g;
    if (this.combo.pts > 0) { this.combo = { pts: 0, mult: 1, events: 0, timer: 0, kind: '' }; g.hud.combo(null); }
    this.drift.lose('Пешеход');
    if (this.fines) this._fine(5000, 'ДПС: наезд на пешехода', 'dps');
    else g.hud.toast('Пешеход! Аккуратнее, он еле увернулся', 'bad', 2.5);
  }

  update(dt) {
    this._cameras(dt);
    this._fuel();
    this._taxi(dt);
    this._combo(dt);
    this._drift(dt);
    this.g.beacon.update(dt);
  }

  renderExtra() {}
}
