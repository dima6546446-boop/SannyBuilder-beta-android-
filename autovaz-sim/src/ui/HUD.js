import { CITY, coord, CITY_EXTENT } from '../world/RoadGraph.js';

/**
 * HUD на DOM (дешевле, чем рисовать UI в WebGL): спидометр, передача,
 * тахометр, часы, FPS, миникарта. DOM обновляется с частотой 10 Гц, а не каждый кадр.
 */
export class HUD {
  constructor(city) {
    this.el = {
      spd: document.getElementById('spd'),
      gear: document.getElementById('gear'),
      rpm: document.querySelector('#rpmbar i'),
      clock: document.getElementById('clock'),
      fps: document.getElementById('fps'),
      msg: document.getElementById('msg'),
      map: document.getElementById('minimap'),
    };
    this.ctx = this.el.map.getContext('2d');
    this._t = 0;
    this._msgT = 0;
    this._mapBase = this._renderMapBase(city);
  }

  _renderMapBase(city) {
    const S = this.el.map.width;
    const c = document.createElement('canvas');
    c.width = c.height = S;
    const g = c.getContext('2d');
    const E = CITY_EXTENT;
    this.scale = S / (2 * E);
    const tx = (x) => (x + E) * this.scale;
    g.fillStyle = '#3d5233'; g.fillRect(0, 0, S, S);
    g.strokeStyle = '#8a8a8a'; g.lineWidth = CITY.ROAD_W * this.scale;
    for (let i = 0; i < CITY.N; i++) {
      g.beginPath(); g.moveTo(tx(coord(i)), tx(coord(0))); g.lineTo(tx(coord(i)), tx(coord(CITY.N - 1))); g.stroke();
      g.beginPath(); g.moveTo(tx(coord(0)), tx(coord(i))); g.lineTo(tx(coord(CITY.N - 1)), tx(coord(i))); g.stroke();
    }
    g.fillStyle = '#b8b2a4';
    for (const [x0, z0, x1, z1] of city.buildingRects) g.fillRect(tx(x0), tx(z0), (x1 - x0) * this.scale, (z1 - z0) * this.scale);
    this.tx = tx;
    return c;
  }

  message(text, seconds = 1.8) {
    this.el.msg.textContent = text;
    this.el.msg.classList.add('show');
    this._msgT = seconds;
  }

  update(dt, { physics, clock, fps, traffic }) {
    if (this._msgT > 0) {
      this._msgT -= dt;
      if (this._msgT <= 0) this.el.msg.classList.remove('show');
    }
    this._t += dt;
    if (this._t < 0.1) return;
    this._t = 0;

    this.el.spd.textContent = Math.round(physics.speed * 3.6);
    this.el.gear.textContent = physics.gear === -1 ? 'R' : String(physics.gear);
    this.el.rpm.style.width = `${Math.min(100, (physics.rpm / 6500) * 100).toFixed(1)}%`;
    this.el.clock.textContent = clock;
    if (fps !== undefined) this.el.fps.textContent = `${fps} FPS`;

    const g = this.ctx, S = this.el.map.width;
    g.drawImage(this._mapBase, 0, 0);
    g.fillStyle = '#ffd24a';
    for (const car of traffic.cars) g.fillRect(this.tx(car.x) - 1.5, this.tx(car.z) - 1.5, 3, 3);
    const px = this.tx(physics.x), pz = this.tx(physics.z);
    const fx = Math.sin(physics.heading), fz = Math.cos(physics.heading);
    g.fillStyle = '#ff3b30';
    g.beginPath();
    g.moveTo(px + fx * 7, pz + fz * 7);
    g.lineTo(px - fx * 4 - fz * 4, pz - fz * 4 + fx * 4);
    g.lineTo(px - fx * 4 + fz * 4, pz - fz * 4 - fx * 4);
    g.closePath(); g.fill();
    g.strokeStyle = 'rgba(0,0,0,.4)'; g.lineWidth = 1; g.strokeRect(0.5, 0.5, S - 1, S - 1);
  }
}
