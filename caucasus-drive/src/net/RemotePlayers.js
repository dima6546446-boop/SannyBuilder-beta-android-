import * as THREE from 'three';
import { buildCharacter, OUTFITS } from '../character/CharacterModel.js';
import { Crowd } from '../character/Crowd.js';
import { FLAG } from './Net.js';

/** Быстрые фразы (без свободного чата: нечего модерировать). */
export const PHRASES = ['Привет! 👋', 'Погнали! 🏁', 'Го за мной', 'Красиво! 🔥', 'Давай дрифт', 'Сорян 🙏', 'Ха-ха 😂', 'Пока! ✌'];

const DELAY = 0.18;   // рисуем чужих с задержкой — между двумя пакетами, без рывков
const TAG_R = 90;     // дальше ники не видны
const TAG_W = 0.6;    // ширина ника в долях высоты экрана
const SUITS = [0x1f3c78, 0x8a1f24, 0x2f5a3a, 0x6d2f86, 0x9a7b3a, 0x2b5d7a, 0x7a2d4a, 0x17181c];

const lerpAngle = (a, b, k) => a + (((b - a + Math.PI * 3) % (Math.PI * 2)) - Math.PI) * k;

/**
 * Другие игроки онлайн: машины рисуются тем же CarInstancer, что и трафик (почти бесплатно),
 * сталкиваются с машиной игрока как трафик; пешком — персонаж с анимацией прохожего.
 * Над каждым — ник (и фраза из быстрого чата).
 */
export class RemotePlayers {
  constructor(game) {
    this.g = game;
    this.map = new Map();
    this.cars = []; // для столкновений: [{x, z, heading, vx, vz}]
    this.time = 0;
  }

  add(id, p) {
    const old = this.map.get(id);
    if (old) { old.p = p; this._tag(old); return; }
    const g = this.g;
    const r = {
      id, p, buf: [], has: false,
      car: { model: g.instancer.modelIndex(p.car), x: 0, z: 0, y: 0, heading: 0, vx: 0, vz: 0, color: new THREE.Color(p.color), lights: false, braking: false, ind: null, remote: true },
      flags: 0, foot: null, sayT: 0, say: '',
    };
    const c = document.createElement('canvas'); c.width = 256; c.height = 80;
    r.tagCanvas = c;
    r.tagTex = new THREE.CanvasTexture(c);
    r.tagTex.colorSpace = THREE.SRGBColorSpace;
    r.tag = new THREE.Sprite(new THREE.SpriteMaterial({ map: r.tagTex, depthTest: false, transparent: true, fog: false, toneMapped: false, sizeAttenuation: false }));
    r.tag.scale.set(TAG_W, TAG_W * 80 / 256, 1); // постоянный размер на экране
    r.tag.renderOrder = 5;
    r.tag.visible = false;
    g.scene.add(r.tag);
    this._tag(r);
    this.map.set(id, r);
  }

  remove(id) {
    const r = this.map.get(id);
    if (!r) return;
    this.g.scene.remove(r.tag);
    r.tag.material.dispose(); r.tagTex.dispose();
    if (r.foot) this.g.scene.remove(r.foot.c.root);
    this.map.delete(id);
  }

  clear() { for (const id of [...this.map.keys()]) this.remove(id); }

  state(id, d) {
    const r = this.map.get(id);
    if (!r || !Array.isArray(d) || d.length < 7 || !d.slice(0, 7).every(Number.isFinite)) return;
    r.buf.push({ t: performance.now() / 1000, d }); // реальное время прихода: пакеты приходят и между кадрами
    if (r.buf.length > 12) r.buf.shift();
  }

  say(id, k) {
    const r = this.map.get(id);
    if (!r || !PHRASES[k]) return;
    r.say = PHRASES[k]; r.sayT = 4;
    this._tag(r);
    this.g.hud.toast(`${r.p.nick}: ${PHRASES[k]}`, 'good', 2.5);
  }

  _tag(r) {
    const g = r.tagCanvas.getContext('2d');
    g.clearRect(0, 0, 256, 80);
    g.textAlign = 'center';
    g.font = 'bold 26px Arial, sans-serif';
    const w = Math.min(250, g.measureText(r.p.nick).width + 24);
    g.fillStyle = 'rgba(10,12,16,.62)';
    g.fillRect(128 - w / 2, 46, w, 32);
    g.fillStyle = '#' + r.p.color.toString(16).padStart(6, '0');
    g.fillRect(128 - w / 2, 74, w, 4);
    g.fillStyle = '#fff';
    g.fillText(r.p.nick, 128, 71, 240);
    if (r.sayT > 0) {
      g.font = 'bold 24px Arial, sans-serif';
      const sw = Math.min(250, g.measureText(r.say).width + 20);
      g.fillStyle = 'rgba(255,255,255,.92)';
      g.fillRect(128 - sw / 2, 4, sw, 36);
      g.fillStyle = '#111';
      g.fillText(r.say, 128, 31, 240);
    }
    r.tagTex.needsUpdate = true;
  }

  _footModel(r) {
    if (r.foot) return r.foot;
    const o = { ...OUTFITS.player, suit: SUITS[[...r.id].reduce((a, ch) => a + ch.charCodeAt(0), 0) % SUITS.length] };
    const c = buildCharacter(o, { shadows: false });
    this.g.scene.add(c.root);
    r.foot = { c, state: 'walk', v: 0, t: 0, phase: 0, lookT: 0 };
    return r.foot;
  }

  /** Интерполяция между пакетами; если пакеты запаздывают — недолго экстраполируем. */
  update(dt, camera) {
    this.time += dt;
    this.cars.length = 0;
    const T = performance.now() / 1000 - DELAY;
    for (const r of this.map.values()) {
      const b = r.buf;
      if (!b.length) continue;
      while (b.length > 2 && b[1].t <= T) b.shift();
      let d, a = b[0];
      const car = r.car;
      if (b.length > 1 && b[1].t > T && a.t <= T) {
        const n = b[1], k = (T - a.t) / Math.max(1e-3, n.t - a.t);
        d = n.d;
        car.x = a.d[0] + (n.d[0] - a.d[0]) * k;
        car.z = a.d[1] + (n.d[1] - a.d[1]) * k;
        car.heading = lerpAngle(a.d[2], n.d[2], k);
        car.y = a.d[5] + (n.d[5] - a.d[5]) * k;
        if (d.length >= 12 && a.d.length >= 12) {
          r.fx = a.d[7] + (n.d[7] - a.d[7]) * k; r.fz = a.d[8] + (n.d[8] - a.d[8]) * k;
          r.fy = a.d[11] + (n.d[11] - a.d[11]) * k; r.fyaw = lerpAngle(a.d[9], n.d[9], k);
        }
      } else {
        a = b[b.length - 1]; d = a.d;
        const ex = Math.min(0.35, Math.max(0, T - a.t));
        car.x = d[0] + d[3] * ex; car.z = d[1] + d[4] * ex; car.heading = d[2]; car.y = d[5];
        if (d.length >= 12) { r.fx = d[7]; r.fz = d[8]; r.fy = d[11]; r.fyaw = d[9]; }
      }
      car.vx = d[3]; car.vz = d[4];
      const f = d[6] | 0;
      if ((f & FLAG.horn) && !(r.flags & FLAG.horn)) this._horn(car);
      r.flags = f;
      car.lights = !!(f & FLAG.lights);
      car.braking = !!(f & FLAG.brake);
      car.ind = f & FLAG.indL && f & FLAG.indR ? 'H' : f & FLAG.indL ? 'L' : f & FLAG.indR ? 'R' : null;
      r.has = true;
      this.cars.push(car);

      // пешком
      const onFoot = !!(f & FLAG.foot) && d.length >= 12;
      if (onFoot) {
        const F = this._footModel(r);
        F.c.root.visible = true;
        F.c.root.position.set(r.fx, r.fy, r.fz);
        F.c.root.rotation.y = r.fyaw;
        F.state = f & FLAG.sit ? 'bench' : 'walk';
        F.v = d[10];
        F.t += dt;
        if (camera.position.distanceToSquared(F.c.root.position) < 3600) Crowd.prototype._pose.call(null, F, dt);
      } else if (r.foot) r.foot.c.root.visible = false;

      // ник над машиной или над головой
      if (r.sayT > 0 && (r.sayT -= dt) <= 0) this._tag(r);
      const tx = onFoot ? r.fx : car.x, tz = onFoot ? r.fz : car.z, ty = onFoot ? r.fy + 2.3 : car.y + 2.2;
      r.tag.position.set(tx, ty, tz);
      const dist = Math.hypot(tx - camera.position.x, tz - camera.position.z);
      r.tag.visible = dist < TAG_R;
      r.tag.material.opacity = Math.min(1, (TAG_R - dist) / 25);
    }
  }

  _horn(car) {
    const g = this.g, p = g.player.physics;
    const dx = car.x - p.x, dz = car.z - p.z, dd = Math.hypot(dx, dz);
    g.audio.aiHorn(Math.max(0, 1 - dd / 80), (dx * -Math.cos(p.heading) + dz * Math.sin(p.heading)) / (dd || 1));
  }

  render(instancer, glow, blink) {
    for (const r of this.map.values()) if (r.has) instancer.add(r.car, glow, blink);
  }
}
