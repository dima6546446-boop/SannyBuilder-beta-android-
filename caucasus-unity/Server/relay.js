// Выделенный сервер комнаты CAUCASUS DRIVE (UDP). Тот же протокол, что у хоста внутри игры:
//   node relay.js [порт] ["Название комнаты"]
// Игроки вводят IP:порт сервера в «Онлайн → По IP». Нужен открытый UDP-порт (по умолчанию 7777).
// Зависимостей нет — только Node.js 14+.
const dgram = require('dgram');

const PORT = +(process.argv[2] || 7777);
const ROOM = process.argv[3] || 'Кавказ-сервер';
const MAGIC = 0xcd, VER = 1, MAX = 8, TIMEOUT = 8000;
const T = { Hello: 1, Welcome: 2, Join: 3, Leave: 4, State: 5, Evt: 6, Ping: 7, Pong: 8, Full: 9, Discover: 10, Announce: 11, Bye: 12 };
const EV_ENV = 100;

const sock = dgram.createSocket('udp4');
const byKey = new Map();   // "ip:port" → peer
const byId = new Map();    // id → peer

const pack = (type, ...parts) => Buffer.concat([Buffer.from([MAGIC, VER, type]), ...parts]);
const str = (s) => { const b = Buffer.from(s, 'utf8').subarray(0, 60); return Buffer.concat([Buffer.from([b.length]), b]); };

// профиль читаем «как есть», чтобы переслать байты без пересборки: имя, машина, цвет(i32), номер
function profileLen(buf, o) {
  let p = o;
  let n = buf[p]; p += 1 + n;       // name
  n = buf[p]; p += 1 + n;           // car
  p += 4;                           // color
  n = buf[p]; p += 1 + n;           // plate
  return p - o;
}

function send(peer, data) { sock.send(data, peer.port, peer.address); }
function broadcast(data, exceptId) { for (const p of byId.values()) if (p.id !== exceptId) send(p, data); }

function drop(peer) {
  byId.delete(peer.id); byKey.delete(peer.key);
  broadcast(pack(T.Leave, Buffer.from([peer.id])), peer.id);
  console.log(`- ${peer.id} вышел (осталось ${byId.size})`);
}

sock.on('message', (buf, rinfo) => {
  if (buf.length < 3 || buf[0] !== MAGIC || buf[1] !== VER) return;
  const type = buf[2];
  const key = rinfo.address + ':' + rinfo.port;
  try {
    if (type === T.Discover) {
      return sock.send(pack(T.Announce, str(ROOM), Buffer.from([byId.size, MAX])), rinfo.port, rinfo.address);
    }
    let peer = byKey.get(key);
    if (type === T.Hello) {
      const prof = buf.subarray(3, 3 + profileLen(buf, 3));
      if (!peer) {
        if (byId.size >= MAX) return sock.send(pack(T.Full), rinfo.port, rinfo.address);
        let id = 1; while (byId.has(id)) id++;
        peer = { id, key, address: rinfo.address, port: rinfo.port, prof, seen: Date.now() };
        byKey.set(key, peer); byId.set(id, peer);
        broadcast(pack(T.Join, Buffer.from([id]), prof), id);
        console.log(`+ ${id} вошёл с ${key} (всего ${byId.size})`);
      }
      peer.seen = Date.now();
      const others = [...byId.values()].filter((p) => p.id !== peer.id);
      return send(peer, pack(T.Welcome, Buffer.from([peer.id, others.length]), ...others.flatMap((p) => [Buffer.from([p.id]), p.prof])));
    }
    if (!peer) return;
    peer.seen = Date.now();
    if (type === T.State) return broadcast(pack(T.State, Buffer.from([peer.id]), buf.subarray(3)), peer.id);
    if (type === T.Evt) {
      if (buf[3] === EV_ENV) return;
      return broadcast(pack(T.Evt, Buffer.from([peer.id]), buf.subarray(3)), peer.id);
    }
    if (type === T.Ping) return send(peer, pack(T.Pong));
    if (type === T.Bye) return drop(peer);
  } catch (e) { /* битый пакет */ }
});

setInterval(() => {
  const now = Date.now();
  for (const p of [...byId.values()]) if (now - p.seen > TIMEOUT) drop(p);
}, 1000);

sock.bind(PORT, () => console.log(`CAUCASUS DRIVE: сервер «${ROOM}» слушает UDP ${PORT}`));
