import { CITY, coord, CITY_EXTENT } from '../world/RoadGraph.js';
import { AUTODROME } from '../world/City.js';

/**
 * HUD на DOM: деньги, спидометр/передача/топливо, поворотники, миссия, тосты,
 * навигационная стрелка, комбо «шашек», парктроник, миникарта (поворачивается с машиной).
 * Тяжёлые обновления DOM — 10 Гц.
 */
export class HUD {
  constructor(city) {
    const $ = (id) => document.getElementById(id);
    this.el = {
      root: $('hud'), spd: $('spd'), gear: $('gear'), rpm: document.querySelector('#rpmbar i'),
      fuel: $('fuel'), fuelI: document.querySelector('#fuel i'), clock: $('clock'), fps: $('fps'),
      money: $('money'), mission: $('mission'), mTitle: $('mission-title'), mSub: $('mission-sub'),
      mBar: document.querySelector('#mission-bar i'), combo: $('combo'), toasts: $('toasts'),
      nav: $('nav'), navArrow: $('nav-arrow'), navDist: $('nav-dist'), limit: $('speed-limit'),
      sensors: $('sensors'), sf: [...document.querySelectorAll('#sensor-f i')], sr: [...document.querySelectorAll('#sensor-r i')],
      dl: $('dash-ind-l'), dr: $('dash-ind-r'), il: $('ind-l'), ir: $('ind-r'), hz: $('hazard'),
      action: $('btn-action'), flash: $('flash'), map: $('minimap'),
    };
    this.ctx = this.el.map.getContext('2d');
    this._t = 0;
    this._navTarget = null;
    this.city = city;
    this._mapBase = this._renderMapBase(city);
  }

  _renderMapBase(city) {
    // карта мира в масштабе 1 px = 1.6 м, рисуется один раз
    // охват: x −620…+340, z −330…+330 (город + коридор + автодром)
    this.mapScale = 1 / 1.6;
    this.mapOx = -620; this.mapOz = -330;
    const c = document.createElement('canvas');
    c.width = Math.ceil(960 * this.mapScale); c.height = Math.ceil(660 * this.mapScale);
    const g = c.getContext('2d');
    const tx = (x) => (x - this.mapOx) * this.mapScale, tz = (z) => (z - this.mapOz) * this.mapScale;
    g.fillStyle = '#34482c'; g.fillRect(0, 0, c.width, c.height);
    g.fillStyle = '#4b5a44';
    g.fillRect(tx(-CITY_EXTENT), tz(-CITY_EXTENT), CITY_EXTENT * 2 * this.mapScale, CITY_EXTENT * 2 * this.mapScale);
    g.fillStyle = '#6b6f73';
    g.fillRect(tx(AUTODROME.x0), tz(AUTODROME.z0), (AUTODROME.x1 - AUTODROME.x0) * this.mapScale, (AUTODROME.z1 - AUTODROME.z0) * this.mapScale);
    g.fillRect(tx(AUTODROME.x1), tz(-8), (-CITY_EXTENT - AUTODROME.x1) * this.mapScale, 16 * this.mapScale);
    g.strokeStyle = '#8d9196'; g.lineWidth = CITY.ROAD_W * this.mapScale;
    for (let i = 0; i < CITY.N; i++) {
      g.beginPath(); g.moveTo(tx(coord(i)), tz(coord(0))); g.lineTo(tx(coord(i)), tz(coord(CITY.N - 1))); g.stroke();
      g.beginPath(); g.moveTo(tx(coord(0)), tz(coord(i))); g.lineTo(tx(coord(CITY.N - 1)), tz(coord(i))); g.stroke();
    }
    g.fillStyle = '#b8b2a4';
    for (const [x0, z0, x1, z1] of city.buildingRects) g.fillRect(tx(x0), tz(z0), (x1 - x0) * this.mapScale, (z1 - z0) * this.mapScale);
    if (city.azs) { g.fillStyle = '#e53935'; g.beginPath(); g.arc(tx(city.azs.x), tz(city.azs.z), 5, 0, 7); g.fill(); }
    g.fillStyle = '#1e5bd8';
    for (const p of city.dpsPosts) { g.beginPath(); g.arc(tx(p.x), tz(p.z), 4, 0, 7); g.fill(); }
    this.tx = tx; this.tz = tz;
    return c;
  }

  setMoney(v) { this.el.money.textContent = Math.round(v).toLocaleString('ru-RU'); }

  mission(title, sub, progress = 0) {
    this.el.mission.classList.remove('hidden');
    if (this._mt !== title) { this.el.mTitle.textContent = title; this._mt = title; }
    if (this._ms !== sub) { this.el.mSub.textContent = sub; this._ms = sub; }
    this.el.mBar.style.width = `${Math.round(progress * 100)}%`;
  }

  hideMission() { this.el.mission.classList.add('hidden'); this._mt = this._ms = null; }

  toast(text, kind = '', seconds = 2.2) {
    const d = document.createElement('div');
    d.className = `toast ${kind}`;
    d.textContent = text;
    this.el.toasts.appendChild(d);
    while (this.el.toasts.children.length > 3) this.el.toasts.firstChild.remove();
    setTimeout(() => { d.classList.add('out'); setTimeout(() => d.remove(), 450); }, seconds * 1000);
  }

  clearToasts() { this.el.toasts.innerHTML = ''; }

  combo(text) {
    if (!text) { this.el.combo.classList.add('hidden'); return; }
    this.el.combo.classList.remove('hidden');
    this.el.combo.textContent = text;
  }

  nav(target) { this._navTarget = target; if (!target) this.el.nav.classList.remove('show'); }

  showLimit(on) { this.el.limit.classList.toggle('hidden', !on); }
  showSensors(on) { this.el.sensors.classList.toggle('hidden', !on); this._sensorsOn = on; }

  setAction(label) {
    if (!label) { this.el.action.classList.add('hidden'); return; }
    this.el.action.classList.remove('hidden');
    if (this.el.action.textContent !== label) this.el.action.textContent = label;
  }

  flash() {
    this.el.flash.classList.add('on');
    requestAnimationFrame(() => requestAnimationFrame(() => this.el.flash.classList.remove('on')));
  }

  sensors(front, rear) {
    const paint = (bars, d) => {
      const lvl = d > 2.2 ? 0 : d > 1.5 ? 1 : d > 0.9 ? 2 : d > 0.45 ? 3 : 4;
      bars.forEach((b, i) => { b.className = i < lvl ? (lvl >= 4 ? 'r' : lvl >= 3 ? 'y' : 'g') : ''; });
    };
    paint(this.el.sf, front);
    paint(this.el.sr, rear);
  }

  update(dt, { player, clock, fps, traffic, camera, money }) {
    const p = player.physics;
    // поворотники — каждый кадр (мигание)
    const ind = player.indicator, on = player.blinkOn;
    const L = (ind === 'L' || ind === 'H') && on, R = (ind === 'R' || ind === 'H') && on;
    this.el.dl.classList.toggle('on', L); this.el.dr.classList.toggle('on', R);
    this.el.il.classList.toggle('blink', L); this.el.ir.classList.toggle('blink', R);
    this.el.il.classList.toggle('on', ind === 'L'); this.el.ir.classList.toggle('on', ind === 'R');
    this.el.hz.classList.toggle('on', ind === 'H');

    if (this._navTarget && camera) {
      const t = this._navTarget;
      const dx = t.x - p.x, dz = t.z - p.z;
      const d = Math.hypot(dx, dz);
      // угол цели относительно направления камеры
      const cy = Math.atan2(camera.position.x - p.x, camera.position.z - p.z) + Math.PI;
      const a = Math.atan2(dx, dz) - cy;
      this.el.nav.classList.add('show');
      this.el.navArrow.style.transform = `rotate(${-a}rad)`;
      this.el.navDist.textContent = d > 1000 ? `${(d / 1000).toFixed(1)} км` : `${Math.round(d)} м`;
    }

    this._t += dt;
    if (this._t < 0.1) return;
    this._t = 0;
    const kmh = Math.round(p.speed * 3.6);
    this.el.spd.textContent = kmh;
    this.el.gear.textContent = p.gearLabel;
    this.el.rpm.style.width = `${Math.min(100, (p.rpm / (p.spec.revLimit + 200)) * 100).toFixed(1)}%`;
    const fk = p.fuel / p.spec.tank;
    this.el.fuelI.style.width = `${Math.round(fk * 100)}%`;
    this.el.fuel.classList.toggle('low', fk < 0.15);
    this.el.clock.textContent = clock;
    if (fps !== undefined) this.el.fps.textContent = `${fps} FPS`;
    if (money !== undefined) this.setMoney(money);
    this.el.limit.classList.toggle('over', kmh > 70);

    // миникарта: поворачивается по курсу, игрок в центре
    const g = this.ctx, S = this.el.map.width;
    g.save();
    g.fillStyle = '#34482c'; g.fillRect(0, 0, S, S);
    g.translate(S / 2, S / 2);
    const zoom = 1.9;
    g.rotate(Math.PI + p.heading);
    g.scale(zoom, zoom);
    g.translate(-this.tx(p.x), -this.tz(p.z));
    g.drawImage(this._mapBase, 0, 0);
    g.fillStyle = '#ffd24a';
    for (const car of traffic.cars) g.fillRect(this.tx(car.x) - 1.2, this.tz(car.z) - 1.2, 2.4, 2.4);
    if (this._navTarget) {
      g.fillStyle = '#35e07a';
      g.beginPath(); g.arc(this.tx(this._navTarget.x), this.tz(this._navTarget.z), 4, 0, 7); g.fill();
    }
    g.restore();
    g.fillStyle = '#ff3b30';
    g.beginPath(); g.moveTo(S / 2, S / 2 - 9); g.lineTo(S / 2 - 6, S / 2 + 7); g.lineTo(S / 2 + 6, S / 2 + 7); g.closePath(); g.fill();
    g.strokeStyle = '#fff'; g.lineWidth = 1.5; g.stroke();
  }
}
