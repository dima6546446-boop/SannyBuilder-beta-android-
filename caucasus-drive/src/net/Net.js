import { Peer } from 'peerjs';

/**
 * Онлайн без своего сервера: игроки соединяются напрямую (WebRTC DataChannel), а бесплатный
 * брокер PeerJS только знакомит их. Топология «звезда»: хост комнаты принимает до MAX-1
 * игроков и пересылает каждому состояния остальных.
 *
 * Комната = peer-id хоста `${PREFIX}${код}`. Быстрая игра — публичные комнаты pub1..pubN:
 * подключаемся к первой с местом, если комнаты нет — сами становимся её хостом.
 *
 * Сообщения (JSON):
 *  hello {p}            клиент → хост: профиль (ник, машина, цвет)
 *  welcome {id, players, env} хост → новому клиенту; full — мест нет
 *  join {id, p} / leave {id}
 *  s {id, d}            состояние игрока, d = [x, z, heading, vx, vz, y, flags, wx, wz, wyaw, wv]
 *  say {id, k}          быстрая фраза
 *  env {time, rain}     хост раз в несколько секунд: время суток и погода
 */
export const MAX_PLAYERS = 8;
const PREFIX = 'caucasusdrive-v1-';
const PUBLIC_ROOMS = 6;
const SEND_HZ = 12;
const ICE = [{ urls: 'stun:stun.l.google.com:19302' }, { urls: 'stun:stun1.l.google.com:19302' }];

export const FLAG = { lights: 1, brake: 2, horn: 4, indL: 8, indR: 16, foot: 32, rev: 64, sit: 128 };

/** Ник: без разметки и управляющих символов, до 16 знаков. */
export function cleanNick(s) {
  const t = String(s || '').replace(/[<>&"'`\u0000-\u001f]/g, '').replace(/\s+/g, ' ').trim().slice(0, 16);
  return t || `Игрок${(Math.random() * 9000 + 1000) | 0}`;
}

export function randomCode() {
  const A = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let s = '';
  for (let i = 0; i < 4; i++) s += A[(Math.random() * A.length) | 0];
  return s;
}

function peerOptions() {
  const q = new URLSearchParams(location.search);
  const o = { debug: 0, config: { iceServers: ICE } };
  // тесты: локальный брокер (npx peerjs --port 9000)
  if (q.get('peerhost')) Object.assign(o, { host: q.get('peerhost'), port: +(q.get('peerport') || 9000), path: '/', secure: false });
  return o;
}

export class Net {
  constructor() {
    this.peer = null;
    this.host = false;
    this.conns = new Map();   // хост: id → DataConnection; клиент: один (к хосту)
    this.players = new Map(); // id → { p: профиль }
    this.handlers = {};
    this.room = null;
    this.active = false;
    this._sendT = 0;
    this.seen = new Map();    // id → время последнего пакета (хост выкидывает «замолчавших»)
    this.hostSeen = 0;
  }

  on(ev, fn) { this.handlers[ev] = fn; return this; }
  _emit(ev, ...a) { this.handlers[ev]?.(...a); }

  get count() { return this.players.size + (this.active ? 1 : 0); }

  // ------------------------------------------------------------------ подключение
  /** mode: 'quick' | 'create' | 'join'. Возвращает код комнаты. */
  async connect(mode, profile, code) {
    this.profile = profile;
    if (mode === 'create') return this._host(code || randomCode(), false);
    if (mode === 'join') return this._join(String(code).toUpperCase().trim(), false);
    for (let i = 1; i <= PUBLIC_ROOMS; i++) {
      const room = `PUB${i}`;
      try { return await this._join(room, true); } catch (e) {
        if (e.code === 'full') continue;
        if (e.code !== 'absent') throw e;
      }
      try { return await this._host(room, true); } catch (e) {
        if (e.code !== 'taken') throw e;
        // кто-то занял комнату одновременно с нами — пробуем зайти к нему
        try { return await this._join(room, true); } catch (e2) { if (e2.code !== 'full' && e2.code !== 'absent') throw e2; }
      }
    }
    throw Object.assign(new Error('Все публичные комнаты заполнены'), { code: 'busy' });
  }

  _newPeer(id) {
    return new Promise((resolve, reject) => {
      const peer = id ? new Peer(PREFIX + id, peerOptions()) : new Peer(peerOptions());
      const t = setTimeout(() => { peer.destroy(); reject(Object.assign(new Error('Сервер не отвечает'), { code: 'timeout' })); }, 12000);
      const onErr = (e) => {
        clearTimeout(t);
        peer.destroy();
        reject(Object.assign(new Error(e.message), { code: e.type === 'unavailable-id' ? 'taken' : e.type === 'network' || e.type === 'server-error' || e.type === 'socket-error' ? 'offline' : e.type }));
      };
      peer.once('error', onErr);
      peer.once('open', () => { clearTimeout(t); peer.off('error', onErr); resolve(peer); });
    });
  }

  async _host(room, pub) {
    const peer = await this._newPeer(room);
    this.peer = peer;
    this.host = true;
    this.room = room;
    this.pub = pub;
    this.myId = peer.id;
    this.active = true;
    peer.on('connection', (c) => this._accept(c));
    peer.on('disconnected', () => { if (this.active) peer.reconnect(); });
    peer.on('error', (e) => { if (e.type !== 'peer-unavailable') console.warn('net', e.type); });
    return room;
  }

  _accept(c) {
    c.on('open', () => {
      if (this.players.size >= MAX_PLAYERS - 1) { c.send({ t: 'full' }); setTimeout(() => c.close(), 300); return; }
      this.conns.set(c.peer, c);
    });
    c.on('data', (m) => this._hostMsg(c, m));
    c.on('close', () => this._drop(c.peer));
    c.on('error', () => this._drop(c.peer));
  }

  _drop(id) {
    this.seen.delete(id);
    if (!this.conns.has(id)) return;
    this.conns.delete(id);
    if (this.players.delete(id)) {
      this._emit('leave', id);
      this._bcast({ t: 'leave', id });
    }
  }

  _bcast(m, except) {
    for (const [id, c] of this.conns) if (id !== except && c.open) c.send(m);
  }

  _hostMsg(c, m) {
    if (!m || typeof m !== 'object') return;
    const id = c.peer;
    this.seen.set(id, performance.now());
    if (m.t === 'hello') {
      if (!this.conns.has(id)) return;
      const p = sanitizeProfile(m.p);
      this.players.set(id, { p });
      const list = [[this.myId, this.profile]];
      for (const [pid, pl] of this.players) if (pid !== id) list.push([pid, pl.p]);
      c.send({ t: 'welcome', id, players: list, env: this.envState?.() });
      this._bcast({ t: 'join', id, p }, id);
      this._emit('join', id, p);
      return;
    }
    if (!this.players.has(id)) return;
    if (m.t === 's' && Array.isArray(m.d)) { this._bcast({ t: 's', id, d: m.d }, id); this._emit('state', id, m.d); }
    else if (m.t === 'say') { const k = m.k | 0; this._bcast({ t: 'say', id, k }, id); this._emit('say', id, k); }
  }

  _join(room, pub) {
    return new Promise((resolve, reject) => {
      this._newPeer(null).then((peer) => {
        const fail = (code, msg) => { clearTimeout(t); peer.destroy(); reject(Object.assign(new Error(msg), { code })); };
        // комнаты нет → брокер присылает peer-unavailable
        peer.on('error', (e) => { if (e.type === 'peer-unavailable') fail('absent', 'Комната не найдена'); });
        const c = peer.connect(PREFIX + room, { reliable: true, serialization: 'json' });
        const t = setTimeout(() => fail('p2p', 'Не удалось соединиться с игроками'), 15000);
        c.on('open', () => c.send({ t: 'hello', p: this.profile }));
        c.on('data', (m) => {
          if (m?.t === 'full') { fail('full', 'Комната заполнена'); return; }
          if (m?.t === 'welcome') {
            clearTimeout(t);
            this.peer = peer;
            this.host = false;
            this.room = room;
            this.pub = pub;
            this.myId = m.id;
            this.active = true;
            this.conns.set(c.peer, c);
            this.hostId = c.peer;
            this.hostSeen = performance.now();
            for (const [id, p] of m.players) { this.players.set(id, { p: sanitizeProfile(p) }); this._emit('join', id, this.players.get(id).p); }
            if (m.env) this._emit('env', m.env);
            c.on('close', () => this._lost());
            peer.on('disconnected', () => { if (this.active) peer.reconnect(); });
            resolve(room);
            return;
          }
          if (this.active) this._clientMsg(m);
        });
      }, reject);
    });
  }

  _clientMsg(m) {
    if (!m || typeof m !== 'object') return;
    this.hostSeen = performance.now();
    if (m.t === 's') this._emit('state', m.id, m.d);
    else if (m.t === 'join') { const p = sanitizeProfile(m.p); this.players.set(m.id, { p }); this._emit('join', m.id, p); }
    else if (m.t === 'leave') { this.players.delete(m.id); this._emit('leave', m.id); }
    else if (m.t === 'say') this._emit('say', m.id, m.k | 0);
    else if (m.t === 'env') this._emit('env', m);
  }

  _lost() {
    if (!this.active) return;
    this.close();
    this._emit('lost');
  }

  // ------------------------------------------------------------------ отправка
  /** Каждый кадр: getState() → массив состояния, отправка SEND_HZ раз в секунду. */
  update(dt, getState) {
    if (!this.active) return;
    this._sendT -= dt;
    if (this._sendT > 0) return;
    this._sendT += 1 / SEND_HZ;
    if (this._sendT < 0) this._sendT = 0;
    // обрыв без «close» (телефон ушёл из сети): 15 с тишины — игрок вышел / связь потеряна
    const now = performance.now();
    if (this.host) { for (const [id, t] of this.seen) if (now - t > 15000) { this.seen.delete(id); this.conns.get(id)?.close(); this._drop(id); } }
    else if (now - this.hostSeen > 15000) { this._lost(); return; }
    const d = getState();
    if (this.host) this._bcast({ t: 's', id: this.myId, d });
    else this._toHost({ t: 's', d });
    if (this.host && this.envState && (this._envT = (this._envT || 0) - 1 / SEND_HZ) <= 0) {
      this._envT = 5;
      this._bcast({ t: 'env', ...this.envState() });
    }
  }

  _toHost(m) { const c = this.conns.get(this.hostId); if (c?.open) c.send(m); }

  say(k) {
    if (!this.active) return;
    if (this.host) this._bcast({ t: 'say', id: this.myId, k });
    else this._toHost({ t: 'say', k });
  }

  close() {
    this.active = false;
    for (const c of this.conns.values()) { try { c.close(); } catch { /* уже закрыт */ } }
    this.conns.clear();
    this.seen.clear();
    for (const id of [...this.players.keys()]) { this.players.delete(id); this._emit('leave', id); }
    this.peer?.destroy();
    this.peer = null;
    this.room = null;
  }
}

function sanitizeProfile(p) {
  p = p && typeof p === 'object' ? p : {};
  return {
    nick: cleanNick(p.nick),
    car: typeof p.car === 'string' ? p.car.slice(0, 24) : 'vaz2107',
    color: Number.isFinite(p.color) ? p.color & 0xffffff : 0xb02020,
    plate: typeof p.plate === 'string' ? p.plate.slice(0, 12) : '',
  };
}
