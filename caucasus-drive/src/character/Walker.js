import * as THREE from 'three';
import { buildCharacter, LIMB } from './CharacterModel.js';
import { CollisionWorld } from '../core/CollisionWorld.js';
import { damp, dampAngle, clamp, wrapAngle } from '../utils/math.js';

const R = 0.28;            // радиус коллизии персонажа
const WALK = 1.7, RUN = 5.2;
const GRAVITY = 13, JUMP_V = 4.4;
const SIT_HIPS = 0.52;     // высота таза над ступнями, когда сидим на лавке/капоте
const SQUAT_HIPS = 0.4;    // «на корточках»
const DOWN = new THREE.Vector3(0, -1, 0);

const _v = new THREE.Vector3(), _v2 = new THREE.Vector3(), _v3 = new THREE.Vector3();
const _m = new THREE.Matrix4(), _q = new THREE.Quaternion(), _q2 = new THREE.Quaternion();

/**
 * Пешеход-игрок: выходит из машины и гуляет по городу.
 *  - ходьба/бег (джойстик, скорость по отклонению), прыжок;
 *  - «Сесть»: на лавку остановки, на капот своей машины или на корточки, если рядом ничего нет;
 *  - «Курить»: прикурить, затяжки с огоньком, дым с кончика и выдох изо рта (частицы Smoke);
 *  - «Свист»: два пальца в рот + процедурный звук, машины рядом сигналят в ответ;
 *  - сбит машиной — падение и подъём.
 * Анимация процедурная: целевая поза по состоянию сглаживается по каждой кости (плавные
 * переходы без клипов), руки к лицу/коленям ставятся двухзвенной IK.
 */
export class Walker {
  constructor(game) {
    this.g = game;
    const c = buildCharacter(undefined, { shadows: game.q.shadows });
    Object.assign(this, c);
    this.root.visible = false;
    this.root.name = 'Walker';
    game.scene.add(this.root);

    this.pos = this.root.position;
    this.yaw = 0;
    this.vx = 0; this.vz = 0; this.vy = 0;
    this.speed = 0;
    this.groundY = 0;
    this.state = 'walk';           // walk | air | sit | down
    this.seat = null;              // { kind: 'bench'|'hood'|'squat', ... }
    this.phase = 0;
    this.stepIdx = 0;
    this.t = 0;
    this.smoke = { on: false, t: 0, stage: 'off', drags: 0, next: 0, tipT: 0, ik: 0 };
    this.whistle = { t: 0, hand: 'R', ik: 0 };
    this.ikR = 0; this.ikL = 0;
    this.downT = 0;
    this.immuneUntil = 0;
    this.landT = 0;
    this._surf = { y: 0, mu: 1, type: 0 };
    this._hits = 0;

    // стены остановок (сам павильон для пешехода проходим, чтобы сесть на лавку)
    this.pedCol = new CollisionWorld(16);
    this.benches = [];
    for (const s of game.city.busStops || []) {
      const ux = Math.abs(s.nz), uz = Math.abs(s.nx); // вдоль дороги
      const bx = s.x - s.nx * 0.75, bz = s.z - s.nz * 0.75;
      this.pedCol.addBox(bx - ux * 2.1 - Math.abs(s.nx) * 0.06, bz - uz * 2.1 - Math.abs(s.nz) * 0.06,
        bx + ux * 2.1 + Math.abs(s.nx) * 0.06, bz + uz * 2.1 + Math.abs(s.nz) * 0.06, 'glass');
      this.benches.push({ x: s.x - s.nx * 0.35, z: s.z - s.nz * 0.35, ux, uz, nx: s.nx, nz: s.nz, top: 0.15 + 0.5 });
    }
    this._ray = new THREE.Raycaster();
  }

  get active() { return this.root.visible; }
  get headPos() { return this.bones.head.getWorldPosition(_v3); }

  // ---------------------------------------------------------------- появление
  spawn(x, z, yaw) {
    this.pos.set(x, this._ground(x, z), z);
    this.groundY = this.pos.y;
    this.yaw = yaw;
    this.root.rotation.y = yaw;
    this.vx = this.vz = this.vy = 0;
    this.speed = 0;
    this.state = 'walk';
    this.seat = null;
    this.body.rotation.set(0, 0, 0);
    this.bones.hips.position.y = this.rest.hips.y;
    for (const b of Object.values(this.bones)) b.rotation.set(0, 0, 0);
    this.root.visible = true;
  }

  hide() {
    this.stopSmoking(true);
    this.root.visible = false;
  }

  _ground(x, z) { return this.g.city.surface(x, z, this._surf).y; }

  // ---------------------------------------------------------------- действия
  jump() {
    if (this.state === 'sit') { this.standUp(); return; }
    if (this.state !== 'walk') return;
    this.state = 'air';
    this.vy = JUMP_V;
    this.g.audio.footstep?.(1.4);
  }

  /** Сесть: лавка остановки или капот своей машины поблизости, иначе — на корточки. */
  toggleSit() {
    if (this.state === 'sit') { this.standUp(); return; }
    if (this.state !== 'walk') return;
    let best = null, bestD = 1.8;
    for (const b of this.benches) {
      const along = clamp((this.pos.x - b.x) * b.ux + (this.pos.z - b.z) * b.uz, -1.3, 1.3);
      const sx = b.x + b.ux * along, sz = b.z + b.uz * along;
      const d = Math.hypot(this.pos.x - sx, this.pos.z - sz);
      if (d < bestD) { bestD = d; best = { kind: 'bench', x: sx, z: sz, yaw: Math.atan2(b.nx, b.nz), top: b.top, exit: [sx + b.nx * 0.7, sz + b.nz * 0.7] }; }
    }
    const car = this.g.player, p = car.physics;
    const s = Math.sin(p.heading), c = Math.cos(p.heading);
    const hz = car.half.front - 0.32;
    const hx = p.x + hz * s, hzW = p.z + hz * c;
    const dHood = Math.hypot(this.pos.x - (p.x + (car.half.front + 0.4) * s), this.pos.z - (p.z + (car.half.front + 0.4) * c));
    if (dHood < Math.min(bestD, 1.6) && p.speed < 0.3) {
      const top = this._hoodHeight(hx, hzW);
      if (top > 0.4) best = { kind: 'hood', x: hx, z: hzW, yaw: p.heading, top, exit: [p.x + (car.half.front + 0.6) * s, p.z + (car.half.front + 0.6) * c] };
    }
    this.seat = best || { kind: 'squat' };
    if (best) { this.seat.from = [this.pos.x, this.pos.y, this.pos.z, this.yaw]; this.seat.k = 0; }
    this.state = 'sit';
    this.vx = this.vz = 0;
  }

  _hoodHeight(x, z) {
    this._ray.set(_v.set(x, 4, z), DOWN);
    this._ray.far = 5;
    const hit = this._ray.intersectObject(this.g.player.root, true).find((h) => h.object.visible && h.object.isMesh);
    return hit ? hit.point.y : 0;
  }

  standUp() {
    const s = this.seat;
    this.state = 'walk';
    if (s && s.exit) {
      this.pos.x = s.exit[0]; this.pos.z = s.exit[1];
      this.pos.y = this._ground(this.pos.x, this.pos.z);
    }
    this.seat = null;
  }

  toggleSmoking() {
    const S = this.smoke;
    if (S.on) { this.stopSmoking(); return; }
    Object.assign(S, { on: true, stage: 'light', t: 0, drags: 0, next: 0, tipT: 0 });
    this.cigarette.visible = true;
    if (!this.g._warnedSmoke) {
      this.g._warnedSmoke = true;
      this.g.hud.toast('Минздрав предупреждает: курение вредит вашему здоровью', 'bad', 3);
    }
  }

  stopSmoking(silent) {
    const S = this.smoke;
    if (!S.on) return;
    S.on = false;
    S.stage = 'off';
    this.cigarette.visible = false;
    if (!silent) {
      // бычок улетает щелчком
      const tip = this.cigTip.getWorldPosition(_v);
      for (let i = 0; i < 3; i++) this.g.smoke.emitPuff?.(tip.x, tip.y, tip.z, Math.sin(this.yaw) * 1.5, 0.4, Math.cos(this.yaw) * 1.5, 0.06, 0.4, 0.5);
    }
  }

  whistleNow() {
    if (this.whistle.t > 0 || this.state === 'down') return;
    this.whistle.t = 1.5;
    this.whistle.hand = this.smoke.on ? 'L' : 'R';
    this.whistle.sounded = false;
  }

  // ---------------------------------------------------------------- цикл
  /**
   * @param input  { moveX, moveY (−1..1, вперёд +), run, camYaw }
   */
  update(dt, input) {
    if (!this.active) return;
    this.t += dt;
    const g = this.g;

    // --- движение
    let mx = input.moveX, my = input.moveY;
    const mag = Math.min(1, Math.hypot(mx, my));
    if (this.state === 'sit' && mag > 0.35) this.standUp();
    let tvx = 0, tvz = 0;
    if ((this.state === 'walk' || this.state === 'air') && mag > 0.08) {
      const cy = input.camYaw;
      const fx = Math.sin(cy), fz = Math.cos(cy), rx = -fz, rz = fx;
      const dx = (fx * my + rx * mx) / (mag || 1), dz = (fz * my + rz * mx) / (mag || 1);
      const sp = input.walk ? WALK * Math.min(1, mag / 0.6) : mag < 0.8 ? WALK * (mag / 0.8) : WALK + ((mag - 0.8) / 0.2) * (RUN - WALK);
      tvx = dx * sp; tvz = dz * sp;
      if (this.state === 'walk') this.yaw = dampAngle(this.yaw, Math.atan2(dx, dz), 10, dt);
    }
    const acc = this.state === 'air' ? 2.5 : 12;
    if (this.state !== 'sit' && this.state !== 'down') {
      this.vx = damp(this.vx, tvx, acc, dt);
      this.vz = damp(this.vz, tvz, acc, dt);
    } else { this.vx = damp(this.vx, 0, 8, dt); this.vz = damp(this.vz, 0, 8, dt); }
    this.speed = Math.hypot(this.vx, this.vz);

    if (this.state !== 'sit' || this.seat?.kind === 'squat') {
      this.pos.x += this.vx * dt;
      this.pos.z += this.vz * dt;
      this._collide(dt);
    }

    // --- вертикаль
    const gy = this._ground(this.pos.x, this.pos.z);
    this.groundY = gy;
    if (this.state === 'air') {
      this.vy -= GRAVITY * dt;
      this.pos.y += this.vy * dt;
      if (this.pos.y <= gy && this.vy < 0) {
        this.pos.y = gy;
        this.state = 'walk';
        this.landT = 0.25;
        g.audio.footstep?.(1.6);
      }
    } else if (this.state === 'sit' && this.seat.kind !== 'squat') {
      const s = this.seat;
      s.k = Math.min(1, s.k + dt * 2.5);
      const k = s.k * s.k * (3 - 2 * s.k);
      this.pos.x = s.from[0] + (s.x - s.from[0]) * k;
      this.pos.z = s.from[2] + (s.z - s.from[2]) * k;
      const seatRoot = s.top + 0.06 - SIT_HIPS;
      this.pos.y = s.from[1] + (seatRoot - s.from[1]) * k;
      this.yaw = s.from[3] + wrapAngle(s.yaw - s.from[3]) * k;
      if (s.kind === 'hood' && g.player.physics.speed > 0.6) this.standUp(); // машину толкнули
    } else {
      // ступеньки бордюров — плавно
      this.pos.y = gy > this.pos.y ? damp(this.pos.y, gy, 25, dt) : damp(this.pos.y, gy, 18, dt);
    }

    if (this.state === 'down') {
      this.downT -= dt;
      if (this.downT <= 0) { this.state = 'walk'; this.landT = 0.3; }
    }
    this.root.rotation.y = this.yaw;
    this.body.rotation.x = damp(this.body.rotation.x, this.state === 'down' && this.downT > 0.5 ? -1.45 : 0, this.state === 'down' ? 9 : 5, dt);

    // трафик знает, где пешеход (тормозит перед ним)
    const ped = g.traffic.ctx.ped;
    ped.active = this.state !== 'sit' || this.seat?.kind === 'squat';
    ped.x = this.pos.x; ped.z = this.pos.z;

    this._animate(dt);
    this._smoke(dt);
    this._whistle(dt);
  }

  // ---------------------------------------------------------------- коллизии
  _collide() {
    const g = this.g;
    const push = (nx, nz, depth, o) => {
      if (o && o.tag === 'busstop') return;
      this.pos.x += nx * depth; this.pos.z += nz * depth;
      const vn = this.vx * nx + this.vz * nz;
      if (vn < 0) { this.vx -= vn * nx; this.vz -= vn * nz; }
    };
    for (let it = 0; it < 2; it++) {
      g.collision.collideCircle(this.pos.x, this.pos.z, R, push);
      this.pedCol.collideCircle(this.pos.x, this.pos.z, R, push);
    }
    // своя машина
    const p = g.player.physics, h = g.player.half;
    this._obb(p.x, p.z, p.heading, h.w, h.rear, h.front, push);
    // трафик: медленно — толкаемся, быстро — сбивает с ног
    for (const c of g.traffic.cars) {
      const dx = c.x - this.pos.x, dz = c.z - this.pos.z;
      if (dx * dx + dz * dz > 16) continue;
      const hit = this._obb(c.x, c.z, c.heading, 0.85, -2.15, 2.15, push);
      if (hit && c.speed > 2.5 && this.state !== 'down' && this.t > this.immuneUntil) this._knockDown(c);
    }
    for (const d of g.dps) this._obb(d.x, d.z, d.heading, 0.85, -2.15, 2.15, push);
  }

  /** Пересечение с повёрнутым прямоугольником машины; выталкивание через push. */
  _obb(cx, cz, heading, hw, rear, front, push) {
    const s = Math.sin(heading), c = Math.cos(heading);
    const dx = this.pos.x - cx, dz = this.pos.z - cz;
    const lx = dx * c - dz * s, lz = dx * s + dz * c;
    const qx = clamp(lx, -hw, hw), qz = clamp(lz, rear, front);
    let ex = lx - qx, ez = lz - qz;
    let d = Math.hypot(ex, ez), depth;
    if (d > R) return false;
    if (d < 1e-5) {
      // внутри — выталкиваем к ближайшей стороне
      const m = Math.min(hw - lx, lx + hw, front - lz, lz - rear);
      if (m === hw - lx) { ex = 1; ez = 0; } else if (m === lx + hw) { ex = -1; ez = 0; } else if (m === front - lz) { ex = 0; ez = 1; } else { ex = 0; ez = -1; }
      depth = m + R;
    } else { ex /= d; ez /= d; depth = R - d; }
    // локальная нормаль → мировая
    push(ex * c + ez * s, -ex * s + ez * c, depth, null);
    return true;
  }

  _knockDown(car) {
    this.state = 'down';
    this.downT = 2.4;
    this.immuneUntil = this.t + 5;
    if (this.seat) this.seat = null;
    const fx = Math.sin(car.heading), fz = Math.cos(car.heading);
    this.vx = fx * car.speed * 0.6; this.vz = fz * car.speed * 0.6;
    this.yaw = Math.atan2(-fx, -fz); // лицом к машине → падаем на спину по ходу удара
    this.stopSmoking(true);
    this.g.audio.crash(0.5);
    this.g.cameraRig.addShake(0.6);
    this.g.hud.toast('Смотри по сторонам, пешеход!', 'bad');
    car.stun = 1.5;
    if (navigator.vibrate) navigator.vibrate(60);
  }

  // ---------------------------------------------------------------- анимация
  _animate(dt) {
    const B = this.bones, T = TARGET;
    for (const k in T) { T[k][0] = 0; T[k][1] = 0; T[k][2] = 0; }
    let hipsY = this.rest.hips.y, rate = 10;
    const t = this.t;

    if (this.state === 'walk' || (this.state === 'down' && this.downT <= 0.5)) {
      const v = this.speed;
      const run = clamp((v - WALK) / (RUN - WALK), 0, 1);
      if (v > 0.15) {
        this.phase += dt * (5.2 + v * 1.25);
        const amp = clamp(v / WALK, 0, 1) * (0.42 + run * 0.4);
        const sL = Math.sin(this.phase), sR = -sL;
        T.thighL[0] = -amp * sL - run * 0.15; T.thighR[0] = -amp * sR - run * 0.15;
        T.shinL[0] = 0.1 + Math.max(0, Math.cos(this.phase)) * (0.6 + run * 0.9) * clamp(v / WALK, 0, 1);
        T.shinR[0] = 0.1 + Math.max(0, -Math.cos(this.phase)) * (0.6 + run * 0.9) * clamp(v / WALK, 0, 1);
        T.footL[0] = -T.thighL[0] * 0.3; T.footR[0] = -T.thighR[0] * 0.3;
        T.upperArmL[0] = amp * sL * 0.9; T.upperArmR[0] = amp * sR * 0.9;
        T.upperArmL[2] = 0.08; T.upperArmR[2] = -0.08;
        T.forearmL[0] = -0.25 - run * 1.25; T.forearmR[0] = -0.25 - run * 1.25;
        T.spine[0] = 0.04 + run * 0.18; T.spine[1] = sL * 0.08;
        T.chest[1] = -sL * 0.12;
        T.head[0] = -run * 0.12;
        hipsY -= Math.abs(Math.cos(this.phase)) * (0.02 + run * 0.05) + run * 0.04;
        rate = 16 + run * 10;
        const step = Math.floor(this.phase / Math.PI);
        if (step !== this.stepIdx) { this.stepIdx = step; this.g.audio.footstep?.(0.5 + run * 0.7); }
      } else {
        // стойка: дыхание, лёгкое покачивание, иногда оглядывается
        const br = Math.sin(t * 1.7);
        T.chest[0] = br * 0.015; T.spine[2] = Math.sin(t * 0.4) * 0.02;
        T.upperArmL[2] = 0.07 + br * 0.01; T.upperArmR[2] = -0.07 - br * 0.01;
        T.forearmL[0] = -0.12; T.forearmR[0] = -0.12;
        T.thighL[2] = 0.04; T.thighR[2] = -0.04;
        T.head[1] = Math.sin(t * 0.31) * Math.sin(t * 0.13) * 0.6;
        rate = 6;
      }
      if (this.landT > 0) { this.landT -= dt; hipsY -= this.landT * 0.4; T.shinL[0] += this.landT * 2; T.shinR[0] += this.landT * 2; T.thighL[0] -= this.landT; T.thighR[0] -= this.landT; }
    } else if (this.state === 'air') {
      const up = this.vy > 0 ? 1 : 0.5;
      T.thighL[0] = -0.9 * up; T.shinL[0] = 1.3 * up;
      T.thighR[0] = -0.3; T.shinR[0] = 0.6;
      T.upperArmL[2] = 0.7; T.upperArmR[2] = -0.7; T.upperArmL[0] = -0.4; T.upperArmR[0] = -0.4;
      T.forearmL[0] = -0.6; T.forearmR[0] = -0.6;
      T.spine[0] = 0.1;
      rate = 14;
    } else if (this.state === 'sit') {
      if (this.seat.kind === 'squat') {
        // «на корточках»: колени в стороны, локти на коленях, спина чуть вперёд
        hipsY = SQUAT_HIPS;
        T.thighL[0] = -1.75; T.thighR[0] = -1.75; T.thighL[2] = 0.38; T.thighR[2] = -0.38;
        T.thighL[1] = -0.2; T.thighR[1] = 0.2;
        T.shinL[0] = 2.45; T.shinR[0] = 2.45;
        T.footL[0] = -0.75; T.footR[0] = -0.75;
        T.spine[0] = 0.32; T.chest[0] = 0.1; T.head[0] = -0.35;
        T.upperArmL[0] = -0.75; T.upperArmR[0] = -0.75; T.upperArmL[2] = 0.15; T.upperArmR[2] = -0.15;
        T.forearmL[0] = -0.5; T.forearmR[0] = -0.5;
      } else {
        hipsY = SIT_HIPS;
        const swing = this.seat.kind === 'hood' ? Math.sin(t * 2.1) * 0.12 : 0;
        T.thighL[0] = -1.5; T.thighR[0] = -1.5; T.thighL[2] = 0.08; T.thighR[2] = -0.08;
        T.shinL[0] = 1.45 + swing; T.shinR[0] = 1.45 - swing;
        T.spine[0] = -0.06; T.chest[0] = 0.05;
        T.upperArmL[0] = -0.35; T.upperArmR[0] = -0.35; T.upperArmL[2] = 0.12; T.upperArmR[2] = -0.12;
        T.forearmL[0] = -0.9; T.forearmR[0] = -0.9;
        T.head[1] = Math.sin(t * 0.27) * 0.5;
      }
      rate = 7;
    } else if (this.state === 'down') {
      T.upperArmL[2] = 1.2; T.upperArmR[2] = -1.2; T.thighL[2] = 0.25; T.thighR[2] = -0.25;
      T.shinL[0] = 0.4; T.head[1] = 0.5;
      rate = 8;
    }

    // курим стоя/в ходьбе: правая рука согнута с сигаретой у пояса
    if (this.smoke.on && this.state !== 'down') {
      T.forearmR[0] = Math.min(T.forearmR[0], -1.25);
      if (this.state === 'walk') T.upperArmR[0] *= 0.3;
    }

    for (const name in T) {
      const b = B[name], tr = T[name];
      b.rotation.x = damp(b.rotation.x, tr[0], rate, dt);
      b.rotation.y = damp(b.rotation.y, tr[1], rate, dt);
      b.rotation.z = damp(b.rotation.z, tr[2], rate, dt);
    }
    B.hips.position.y = damp(B.hips.position.y, hipsY, rate * 0.8, dt);

    // --- IK рук: затяжка/свист — к губам; на корточках и сидя — на колени
    this.root.updateMatrixWorld(true);
    const S = this.smoke, W = this.whistle;
    const toMouthR = S.on && (S.stage === 'light' || S.stage === 'drag');
    const whistleHand = W.t > 0.15 && W.t < 1.35 ? W.hand : null;
    const sitHands = this.state === 'sit';
    const wR = toMouthR || whistleHand === 'R' ? 1 : sitHands ? 0.85 : 0;
    const wL = whistleHand === 'L' ? 1 : sitHands ? 0.85 : 0;
    this.ikR = damp(this.ikR, wR, 9, dt);
    this.ikL = damp(this.ikL, wL, 9, dt);
    if (this.ikR > 0.01) {
      if (toMouthR) this._mouthTarget(_v2, 0.07, 0.085);
      else if (whistleHand === 'R') this._mouthTarget(_v2, 0.05, 0.075);
      else this._kneeTarget(_v2, 'L', 'R');
      this._armIK('R', _v2, this.ikR);
    }
    if (this.ikL > 0.01) {
      if (whistleHand === 'L') this._mouthTarget(_v2, 0.05, 0.075);
      else this._kneeTarget(_v2, 'R', 'L');
      this._armIK('L', _v2, this.ikL);
    }
  }

  _mouthTarget(out, fwd, down) {
    const head = this.bones.head;
    out.set(0, 0.06 - down, 0.11 + fwd);
    return head.localToWorld(out);
  }

  /** Кисть — на колено (свою сторону); на корточках чуть впереди колена. */
  _kneeTarget(out, _other, side) {
    const knee = this.bones['shin' + side];
    knee.getWorldPosition(out);
    _v.set(Math.sin(this.yaw), 0, Math.cos(this.yaw));
    const squat = this.seat?.kind === 'squat';
    out.addScaledVector(_v, squat ? 0.16 : 0.02);
    out.y += squat ? -0.12 : 0.05;
    return out;
  }

  /** Двухзвенная IK: плечо → локоть → запястье в точку target (мир), локоть вниз и наружу. */
  _armIK(side, target, w) {
    const B = this.bones, up = B['upperArm' + side], fo = B['forearm' + side];
    const chest = B.chest;
    _m.copy(chest.matrixWorld).invert();
    const T = _v.copy(target).applyMatrix4(_m);         // цель в системе груди
    const S = up.position;
    const d = T.sub(S);
    const L1 = LIMB.upperArm, L2 = LIMB.forearm;
    const dist = clamp(d.length(), 0.08, L1 + L2 - 0.002);
    const dir = d.normalize();
    const a = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist);
    const h = Math.sqrt(Math.max(0, L1 * L1 - a * a));
    const sx = side === 'L' ? 1 : -1;
    const pole = _v2.set(sx * 0.7, -1, -0.15);
    pole.addScaledVector(dir, -pole.dot(dir)).normalize();
    const elbow = _v3.copy(dir).multiplyScalar(a).addScaledVector(pole, h);   // относительно плеча
    const hand = dir.multiplyScalar(dist);                                   // относительно плеча
    const u = elbow.clone().normalize();
    _q.setFromUnitVectors(DOWN, u);
    up.quaternion.slerp(_q, w);
    // предплечье в системе плеча
    const f = hand.sub(elbow).normalize().applyQuaternion(_q2.copy(up.quaternion).invert());
    _q.setFromUnitVectors(DOWN, f);
    fo.quaternion.slerp(_q, w);
  }

  // ---------------------------------------------------------------- курение
  _smoke(dt) {
    const S = this.smoke, g = this.g;
    if (!S.on) return;
    S.t += dt;
    const tip = this.cigTip.getWorldPosition(_v);
    let glow = 0.45 + Math.sin(this.t * 3) * 0.05;
    if (S.stage === 'light') {
      if (S.t > 0.55 && !S.lit) { S.lit = true; g.audio.lighter?.(); }
      if (S.t > 0.55 && S.t < 1.3) glow = 1.6;
      if (S.t > 1.6) { S.stage = 'idle'; S.t = 0; S.next = 2.5; S.lit = false; this._exhale = 0.9; }
    } else if (S.stage === 'idle') {
      S.next -= dt;
      if (S.next <= 0 && this.whistle.t <= 0) { S.stage = 'drag'; S.t = 0; }
    } else if (S.stage === 'drag') {
      if (S.t > 0.45 && S.t < 1.45) { glow = 1.0 + Math.sin(S.t * 9) * 0.15; if (!S.puffed) { S.puffed = true; g.audio.inhale?.(); } }
      if (S.t > 1.7) {
        S.stage = 'idle'; S.t = 0; S.puffed = false;
        S.drags++;
        S.next = 4 + Math.random() * 3;
        this._exhale = 0.9;
        if (S.drags >= 7) { this.stopSmoking(); g.hud.toast('Сигарета докурена', 'good'); return; }
      }
    }
    this.ember.material.color.setRGB(1, 0.25 + glow * 0.2, 0.05 * glow);
    this._tipGlow = glow;
    // тонкая струйка с кончика
    S.tipT -= dt;
    if (S.tipT <= 0) {
      S.tipT = 0.18;
      g.smoke.emitPuff?.(tip.x, tip.y + 0.02, tip.z, (Math.random() - 0.5) * 0.06, 0.35, (Math.random() - 0.5) * 0.06, 0.05, 0.22, 1.6);
    }
    // выдох изо рта через полсекунды после затяжки
    if (this._exhale > 0) {
      this._exhale -= dt;
      if (this._exhale < 0.6 && this._exhale > 0) {
        const m = this.mouth.getWorldPosition(_v2);
        const fx = Math.sin(this.yaw), fz = Math.cos(this.yaw);
        g.smoke.emitPuff?.(m.x, m.y, m.z, fx * 0.6 + (Math.random() - 0.5) * 0.2, 0.1, fz * 0.6 + (Math.random() - 0.5) * 0.2, 0.06, 0.24, 1.5);
        if (Math.random() < 0.2) g.audio.exhale?.();
      }
    }
  }

  // ---------------------------------------------------------------- свист
  _whistle(dt) {
    const W = this.whistle;
    if (W.t <= 0) return;
    W.t -= dt;
    if (!W.sounded && W.t < 1.2) {
      W.sounded = true;
      this.g.audio.whistle?.();
      // машины рядом отвечают гудком
      const g = this.g;
      for (const c of g.traffic.cars) {
        const d = Math.hypot(c.x - this.pos.x, c.z - this.pos.z);
        if (d < 30 && Math.random() < 0.5) setTimeout(() => g.audio.aiHorn(Math.max(0.2, 1 - d / 40), 0), 500 + Math.random() * 600);
      }
    }
    if (W.t > 0.15 && W.t < 1.35) this.bones.head.rotation.x = damp(this.bones.head.rotation.x, -0.12, 8, dt);
  }

  /** Огонёк сигареты и вспышка зажигалки — в общий батч GlowPoints. */
  renderGlow(glow) {
    if (!this.active || !this.smoke.on) return;
    const tip = this.cigTip.getWorldPosition(_v);
    const k = this._tipGlow || 0.4;
    glow.add(tip.x, tip.y, tip.z, 1, 0.35, 0.08, 0.05 + k * 0.08);
    const S = this.smoke;
    if (S.stage === 'light' && S.t > 0.55 && S.t < 1.3) {
      const m = this.mouth.getWorldPosition(_v2);
      glow.add(m.x, m.y - 0.02, m.z + 0.02, 1, 0.75, 0.3, 0.35);
    }
  }
}

const TARGET = {};
for (const n of ['spine', 'chest', 'neck', 'head', 'upperArmL', 'forearmL', 'handL', 'upperArmR', 'forearmR', 'handR', 'thighL', 'shinL', 'footL', 'thighR', 'shinR', 'footR']) TARGET[n] = [0, 0, 0];
