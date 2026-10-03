import * as THREE from 'three';
import { buildCharacter } from './CharacterModel.js';
import { CITY, coord } from '../world/RoadGraph.js';
import { clamp, damp, dampAngle, mulberry32 } from '../utils/math.js';

const { N, CURB } = CITY;
const R = 0.26;
const SUITS = [0x1f3c78, 0x8a1f24, 0x2f5a3a, 0x17181c, 0x5a4632, 0x4a4f57, 0x6d2f86, 0x9a7b3a, 0x2b5d7a, 0x7a2d4a, 0xb8b2a6, 0x3a3f2a];
const PANTS = [0x23252b, 0x3a3f4a, 0x1a1a1a, 0x2b3a5a, 0x4a4036, 0x5a5a5a];
const SKIN = [0xe2b48f, 0xd9a57f, 0xc99872, 0xe8c1a0, 0xb88a66];
const HAIR = [0x2a1d14, 0x5a3a22, 0x101010, 0x8a8a8a, 0x8a5a2a, 0x3a2a1a];
const SHOES = [0x1a1a1a, 0xf0f0f0, 0x3a2a1c, 0x5a5a5a];
const FLEE = ['Куда прёшь?!', 'Тротуар для пешеходов!', 'Совсем сдурел?!', 'Э, осторожнее!', 'Права купил, что ли?'];
const WHISTLE = ['Чё свистишь, братан?', 'Это ты мне?', 'Здорово!', 'Свистеть — денег не будет!', 'Ну чего тебе?', 'Опа, привет!'];

const _v = new THREE.Vector3();

/**
 * Пешеходы-NPC на тротуарах. Каждый ходит по «кольцу» тротуаров вокруг своего квартала
 * (в обе стороны), обходит павильоны остановок, упирается — разворачивается. Часть сидит
 * на лавках остановок. Видят машину игрока: разбегаются, если она едет по тротуару;
 * сбитый падает и встаёт. На свист оборачиваются и отвечают.
 * Пул фиксированного размера (по пресету качества), NPC переезжают ближе к игроку.
 */
export class Crowd {
  constructor(game) {
    this.g = game;
    const q = game.q;
    this.count = q.name === 'low' ? 5 : q.name === 'medium' ? 10 : 14;
    this.density = 1;   // регулятор качества может прореживать толпу
    this.rnd = mulberry32(777);
    this.enabled = false;
    this.npcs = [];
    this.benches = (game.city.busStops || []).map((s) => ({ x: s.x - s.nx * 0.35, z: s.z - s.nz * 0.35, yaw: Math.atan2(s.nx, s.nz), used: null }));
    this.stops = game.city.busStops || [];
    this._sayT = 0;
    for (let i = 0; i < this.count; i++) {
      const r = this.rnd;
      const pick = (a) => a[(r() * a.length) | 0];
      const suit = pick(SUITS);
      const outfit = {
        suit, stripe: r() < 0.5 ? 0xf2f2f2 : suit, pants: r() < 0.3 ? suit : pick(PANTS),
        cap: r() < 0.35 ? pick([0x2b2d33, 0x8a1f24, 0x1f3c78, 0x3a3a3a]) : null, capBrim: 0x1a1a1a,
        shoes: pick(SHOES), sole: 0xdddddd, skin: pick(SKIN), hair: pick(HAIR),
      };
      const c = buildCharacter(outfit, { shadows: q.shadows && q.name === 'high' });
      c.root.visible = false;
      c.root.scale.setScalar(0.92 + r() * 0.14);
      game.scene.add(c.root);
      this.npcs.push({ c, active: false, state: 'walk', x: 0, z: 0, yaw: 0, phase: r() * 6, speed: 1.2 + r() * 0.5, t: r() * 10, stuck: 0, downT: 0, fleeT: 0, lookT: 0 });
    }
  }

  // ---------------------------------------------------------------- маршрут
  _loop(n) {
    const o = n.lane;
    const x0 = coord(n.bi) + o, x1 = coord(n.bi + 1) - o, z0 = coord(n.bj) + o, z1 = coord(n.bj + 1) - o;
    return { x0, x1, z0, z1, L: 2 * (x1 - x0) + 2 * (z1 - z0) };
  }

  /** Точка кольца по длине дуги s (против часовой: юг → восток → север → запад). */
  _point(n, s, out) {
    const p = n.loop, w = p.x1 - p.x0, h = p.z1 - p.z0;
    s = ((s % p.L) + p.L) % p.L;
    if (s < w) out.set(p.x0 + s, 0, p.z0);
    else if (s < w + h) out.set(p.x1, 0, p.z0 + (s - w));
    else if (s < 2 * w + h) out.set(p.x1 - (s - w - h), 0, p.z1);
    else out.set(p.x0, 0, p.z1 - (s - 2 * w - h));
    // у остановки — ближе к дороге (между павильоном и столбами)
    for (const st of this.stops) {
      const dx = out.x - st.x, dz = out.z - st.z;
      if (dx * dx + dz * dz < 12) { if (st.nx) out.x = st.x + st.nx * 1.3; else out.z = st.z + st.nz * 1.3; break; }
    }
    return out;
  }

  _spawn(n, fx, fz, initial) {
    const r = this.rnd;
    for (let k = 0; k < 20; k++) {
      // квартал рядом с игроком
      const bi = clamp(Math.floor((fx - coord(0)) / CITY.SPACING) + Math.round((r() - 0.5) * 2.4), 0, N - 2);
      const bj = clamp(Math.floor((fz - coord(0)) / CITY.SPACING) + Math.round((r() - 0.5) * 2.4), 0, N - 2);
      n.bi = bi; n.bj = bj;
      n.lane = 8.1 + r() * 1.3;
      n.loop = this._loop(n);
      n.s = r() * n.loop.L;
      n.dir = r() < 0.5 ? 1 : -1;
      this._point(n, n.s, _v);
      const d = Math.hypot(_v.x - fx, _v.z - fz);
      if (d < (initial ? 6 : 35) || d > 85) continue;
      if (!initial && this._inView(_v.x, _v.z) && d < 60) continue;
      n.x = _v.x; n.z = _v.z;
      n.state = 'walk';
      // иногда — посидеть на лавке
      if (r() < 0.25) {
        const b = this.benches.find((bb) => !bb.used && Math.hypot(bb.x - fx, bb.z - fz) < 85 && Math.hypot(bb.x - fx, bb.z - fz) > 8);
        if (b) { b.used = n; n.bench = b; n.state = 'bench'; n.x = b.x; n.z = b.z; n.yaw = b.yaw; n.benchT = 20 + r() * 40; }
      }
      n.active = true;
      n.c.root.visible = true;
      n.c.body.rotation.x = 0;
      n.stuck = 0; n.downT = 0; n.fleeT = 0;
      return true;
    }
    return false;
  }

  _inView(x, z) {
    const cam = this.g.camera;
    const dx = x - cam.position.x, dz = z - cam.position.z;
    const f = cam.getWorldDirection(_v.clone());
    return dx * f.x + dz * f.z > 0;
  }

  start() {
    this.enabled = true;
    const f = this.g.focus;
    for (const n of this.npcs) this._spawn(n, f.x, f.z, true);
  }

  stop() {
    this.enabled = false;
    for (const b of this.benches) b.used = null;
    for (const n of this.npcs) { n.active = false; n.c.root.visible = false; n.bench = null; }
  }

  // ---------------------------------------------------------------- события
  /** Игрок свистнул: ближайший прохожий оборачивается и отвечает. */
  onWhistle(x, z) {
    let best = null, bd = 16;
    for (const n of this.npcs) {
      if (!n.active || n.state === 'down') continue;
      const d = Math.hypot(n.x - x, n.z - z);
      if (d < bd) { bd = d; best = n; }
    }
    if (!best) return;
    best.lookT = 2.5; best.lookX = x; best.lookZ = z;
    setTimeout(() => this.g.hud.toast(`«${WHISTLE[(Math.random() * WHISTLE.length) | 0]}»`, 'good', 2.5), 700);
  }

  // ---------------------------------------------------------------- цикл
  update(dt) {
    if (!this.enabled) return;
    const g = this.g, f = g.focus, col = g.collision;
    const pl = g.player, p = pl.physics, h = pl.half;
    const cs = Math.sin(p.heading), cc = Math.cos(p.heading);
    this._sayT -= dt;
    for (const n of this.npcs) {
      if (!n.active) { this._spawn(n, f.x, f.z, false); continue; }
      const dF = Math.hypot(n.x - f.x, n.z - f.z);
      if (dF > 95) {
        if (n.bench) { n.bench.used = null; n.bench = null; }
        n.active = false; n.c.root.visible = false;
        continue;
      }
      n.t += dt;
      const far = dF > 55;

      // --- машина игрока: разбежаться с пути или упасть, если сбили
      if (n.state !== 'down' && p.speed > 2) {
        const dx = n.x - p.x, dz = n.z - p.z;
        const lx = dx * cc - dz * cs, lz = dx * cs + dz * cc;   // в системе машины
        const ahead = lz * Math.sign(p.vLong || 1);
        if (Math.abs(lx) < h.w + R && lz < h.front + R && lz > h.rear - R) { this._knock(n, p); continue; }
        if (ahead > 0 && ahead < 4 + p.speed * 1.2 && Math.abs(lx) < h.w + 1.2 && n.state !== 'flee') {
          if (n.bench) { n.bench.used = null; n.bench = null; }
          n.state = 'flee'; n.fleeT = 1.6;
          const side = lx >= 0 ? 1 : -1;
          n.fx = cc * side; n.fz = -cs * side;      // перпендикулярно курсу машины
          if (this._sayT <= 0 && dF < 25) { this._sayT = 6; g.hud.toast(`«${FLEE[(Math.random() * FLEE.length) | 0]}»`, 'bad', 2); }
        }
      }

      let vx = 0, vz = 0, moving = false;
      if (n.state === 'walk') {
        if (n.lookT > 0) { n.lookT -= dt; n.yaw = dampAngle(n.yaw, Math.atan2(n.lookX - n.x, n.lookZ - n.z), 6, dt); }
        else {
          n.s += n.dir * n.speed * dt;
          this._point(n, n.s, _v);
          const tx = _v.x - n.x, tz = _v.z - n.z, d = Math.hypot(tx, tz);
          if (d > 0.01) { vx = (tx / d) * Math.min(n.speed * 1.3, d * 4); vz = (tz / d) * Math.min(n.speed * 1.3, d * 4); }
          n.stuck = d > 1.6 ? n.stuck + dt : Math.max(0, n.stuck - dt);
          if (n.stuck > 1.5) { n.dir = -n.dir; n.stuck = 0; n.s += n.dir * 1.5; }
          if (d > 6) { n.x = _v.x; n.z = _v.z; } // далеко оттолкнули — вернулся на маршрут
          moving = true;
        }
      } else if (n.state === 'flee') {
        n.fleeT -= dt;
        vx = n.fx * 4.5; vz = n.fz * 4.5; moving = true;
        if (n.fleeT <= 0) { n.state = 'walk'; this._reproject(n); }
      } else if (n.state === 'bench') {
        n.benchT -= dt;
        if (n.benchT <= 0 || n.lookT > 0) {
          if (n.benchT <= 0) { n.bench.used = null; n.bench = null; n.state = 'walk'; this._reproject(n); }
        }
      } else if (n.state === 'down') {
        n.downT -= dt;
        if (n.downT <= 0) { n.state = 'walk'; this._reproject(n); }
      }
      if (moving) {
        n.x += vx * dt; n.z += vz * dt;
        // коллизии со статикой и игроком пешком
        if (!far) col.collideCircle(n.x, n.z, R, (nx, nz, depth) => { n.x += nx * depth; n.z += nz * depth; });
        const sp = Math.hypot(vx, vz);
        if (sp > 0.2) n.yaw = dampAngle(n.yaw, Math.atan2(vx, vz), 8, dt);
        n.v = sp;
      } else n.v = 0;
      if (g.onFoot) {
        const w = g.walker.pos, dx = n.x - w.x, dz = n.z - w.z, d = Math.hypot(dx, dz);
        if (d < R * 2 && d > 1e-4) { const k = (R * 2 - d) / d; n.x += dx * k * 0.5; n.z += dz * k * 0.5; }
      }

      const root = n.c.root;
      // рисуем только тех, кто близко и перед камерой (SkinnedMesh не умеет сам отсекаться)
      const cam = g.camera.position, cdx = n.x - cam.x, cdz = n.z - cam.z;
      const fwd = this._fwd || (this._fwd = new THREE.Vector3());
      if ((n.t * 30 | 0) % 4 === 0) g.camera.getWorldDirection(fwd);
      const idx = this.npcs.indexOf(n);
      root.visible = dF < 60 && cdx * fwd.x + cdz * fwd.z > -4 && (this.density >= 1 || idx % 2 === 0);
      const gy = n.state === 'bench' ? CURB + 0.5 + 0.06 - 0.52 : this._ground(n.x, n.z);
      root.position.set(n.x, gy, n.z);
      root.rotation.y = n.yaw;
      n.c.body.rotation.x = damp(n.c.body.rotation.x, n.state === 'down' && n.downT > 0.6 ? -1.45 : 0, 8, dt);
      if (root.visible && (!far || (n.t * 10) % 3 < 1)) this._pose(n, dt);
    }
  }

  _ground(x, z) { return this.g.city.surface(x, z, this._s || (this._s = {})).y; }

  _reproject(n) {
    // ближайшая точка своего кольца
    const p = n.loop;
    const cx = clamp(n.x, p.x0, p.x1), cz = clamp(n.z, p.z0, p.z1);
    const dl = cx - p.x0, dr = p.x1 - cx, db = cz - p.z0, dt = p.z1 - cz;
    const m = Math.min(dl, dr, db, dt), w = p.x1 - p.x0, h = p.z1 - p.z0;
    if (m === db) n.s = cx - p.x0;
    else if (m === dr) n.s = w + (cz - p.z0);
    else if (m === dt) n.s = w + h + (p.x1 - cx);
    else n.s = 2 * w + h + (p.z1 - cz);
  }

  _knock(n, p) {
    if (n.bench) { n.bench.used = null; n.bench = null; }
    n.state = 'down'; n.downT = 3.2;
    n.yaw = Math.atan2(-Math.sin(p.heading), -Math.cos(p.heading));
    n.x += Math.sin(p.heading) * 0.8; n.z += Math.cos(p.heading) * 0.8;
    const g = this.g;
    g.audio.crash(0.35);
    g.cameraRig.addShake(0.25);
    g.mode?.onPedHit?.(Math.abs(p.speed));
  }

  /** Процедурная поза: ходьба/бег, скамейка, лежит, стоит. */
  _pose(n, dt) {
    const B = n.c.bones, t = n.t;
    const set = (b, x, y, z) => { B[b].rotation.set(x, y, z); };
    if (n.state === 'bench') {
      B.hips.position.y = 0.52;
      set('thighL', -1.5, 0, 0.08); set('thighR', -1.5, 0, -0.08);
      set('shinL', 1.45, 0, 0); set('shinR', 1.45, 0, 0);
      set('spine', -0.06, 0, 0); set('chest', 0.05, 0, 0);
      set('upperArmL', -0.35, 0, 0.12); set('upperArmR', -0.35, 0, -0.12);
      set('forearmL', -0.9, 0, 0); set('forearmR', -0.9, 0, 0);
      set('head', 0, Math.sin(t * 0.3) * 0.5, 0);
      return;
    }
    B.hips.position.y = 0.98;
    if (n.state === 'down') {
      set('upperArmL', 0, 0, 1.2); set('upperArmR', 0, 0, -1.2);
      set('thighL', 0, 0, 0.25); set('thighR', 0, 0, -0.25); set('shinL', 0.4, 0, 0); set('shinR', 0, 0, 0);
      return;
    }
    const v = n.v || 0;
    if (v > 0.15) {
      const run = clamp((v - 1.8) / 2.7, 0, 1);
      n.phase += dt * (5.2 + v * 1.25);
      const amp = 0.42 + run * 0.4, sL = Math.sin(n.phase), c = Math.cos(n.phase);
      set('thighL', -amp * sL - run * 0.15, 0, 0); set('thighR', amp * sL - run * 0.15, 0, 0);
      set('shinL', 0.1 + Math.max(0, c) * (0.6 + run * 0.9), 0, 0); set('shinR', 0.1 + Math.max(0, -c) * (0.6 + run * 0.9), 0, 0);
      set('upperArmL', amp * sL * 0.9, 0, 0.08); set('upperArmR', -amp * sL * 0.9, 0, -0.08);
      set('forearmL', -0.25 - run * 1.2, 0, 0); set('forearmR', -0.25 - run * 1.2, 0, 0);
      set('spine', 0.04 + run * 0.18, sL * 0.08, 0); set('chest', 0, -sL * 0.12, 0);
      set('head', -run * 0.1, 0, 0);
      B.hips.position.y = 0.98 - Math.abs(c) * (0.02 + run * 0.05);
    } else {
      const br = Math.sin(t * 1.7);
      set('thighL', 0, 0, 0.04); set('thighR', 0, 0, -0.04); set('shinL', 0, 0, 0); set('shinR', 0, 0, 0);
      set('upperArmL', 0, 0, 0.07 + br * 0.01); set('upperArmR', 0, 0, -0.07);
      set('forearmL', -0.12, 0, 0); set('forearmR', -0.12, 0, 0);
      set('spine', 0, 0, 0); set('chest', br * 0.015, 0, 0);
      set('head', 0, n.lookT > 0 ? 0 : Math.sin(t * 0.31) * 0.5, 0);
    }
  }
}
