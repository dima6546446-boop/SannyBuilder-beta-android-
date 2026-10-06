// CAUCASUS DRIVE — сервер комнат на Cloudflare Workers + Durable Objects (WebSocket).
// Тот же бинарный протокол, что у Server/relay.js (UDP) и у хоста внутри игры: одно WebSocket-сообщение = один пакет.
// Комната = имя в адресе:  wss://caucasus-drive.<аккаунт>.workers.dev/ABCD   (до 8 игроков).
// Используется Hibernation API: пока в комнате тишина, она не потребляет ресурсов; «p» → «p» отвечает сам Cloudflare.

const MAGIC = 0xcd, VER = 1, MAX = 8, EV_ENV = 100;
const T = { Hello: 1, Welcome: 2, Join: 3, Leave: 4, State: 5, Evt: 6, Ping: 7, Pong: 8, Full: 9, Bye: 12 };

const pack = (type, ...parts) => {
  let n = 3; for (const p of parts) n += p.length;
  const out = new Uint8Array(n); out[0] = MAGIC; out[1] = VER; out[2] = type;
  let o = 3; for (const p of parts) { out.set(p, o); o += p.length; }
  return out;
};

// профиль: имя, машина (строки с байтом длины), цвет i32, номер — длину считаем, чтобы переслать байты как есть
function profileLen(buf, o) {
  let p = o;
  p += 1 + buf[p];       // name
  p += 1 + buf[p];       // car
  p += 4;                // color
  p += 1 + buf[p];       // plate
  return p - o;
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const m = url.pathname.match(/^\/([A-Za-z0-9_-]{1,24})$/);
    if (request.headers.get('Upgrade') !== 'websocket' || !m) {
      return new Response(`CAUCASUS DRIVE: сервер работает.\nВ игре: Онлайн → Cloudflare → адрес wss://${url.host}  и код комнаты.\n`, { headers: { 'content-type': 'text/plain; charset=utf-8' } });
    }
    const stub = env.ROOMS.get(env.ROOMS.idFromName(m[1].toUpperCase()));
    return stub.fetch(request);
  },
};

export class Room {
  constructor(ctx, env) {
    this.ctx = ctx;
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair('p', 'p'));
  }

  async fetch(request) {
    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    this.ctx.acceptWebSocket(server);
    return new Response(null, { status: 101, webSocket: client });
  }

  players(except) {
    const list = [];
    for (const ws of this.ctx.getWebSockets()) {
      if (ws === except) continue;
      const a = ws.deserializeAttachment();
      if (a && a.id) list.push({ ws, id: a.id, prof: a.prof });
    }
    return list;
  }

  broadcast(data, except) { for (const p of this.players(except)) { try { p.ws.send(data); } catch (e) { /* закрыт */ } } }

  webSocketMessage(ws, message) {
    if (typeof message === 'string') return;               // «p» обрабатывает Cloudflare
    const buf = new Uint8Array(message);
    if (buf.length < 3 || buf[0] !== MAGIC || buf[1] !== VER) return;
    const type = buf[2];
    try {
      let me = ws.deserializeAttachment();
      if (type === T.Hello) {
        if (!me) {
          const others = this.players(ws);
          if (others.length >= MAX) { ws.send(pack(T.Full)); ws.close(1000, 'full'); return; }
          const used = new Set(others.map((p) => p.id));
          let id = 1; while (used.has(id)) id++;
          const prof = buf.slice(3, 3 + profileLen(buf, 3));
          me = { id, prof };
          ws.serializeAttachment(me);
          this.broadcast(pack(T.Join, Uint8Array.of(id), prof), ws);
        }
        const others = this.players(ws);
        ws.send(pack(T.Welcome, Uint8Array.of(me.id, others.length), ...others.flatMap((p) => [Uint8Array.of(p.id), new Uint8Array(p.prof)])));
        return;
      }
      if (!me) return;
      if (type === T.State) this.broadcast(pack(T.State, Uint8Array.of(me.id), buf.subarray(3)), ws);
      else if (type === T.Evt) { if (buf[3] !== EV_ENV) this.broadcast(pack(T.Evt, Uint8Array.of(me.id), buf.subarray(3)), ws); }
      else if (type === T.Ping) ws.send(pack(T.Pong));
      else if (type === T.Bye) ws.close(1000, 'bye');
    } catch (e) { /* битый пакет */ }
  }

  drop(ws) {
    const me = ws.deserializeAttachment();
    if (me && me.id) this.broadcast(pack(T.Leave, Uint8Array.of(me.id)), ws);
  }

  webSocketClose(ws, code, reason, wasClean) { this.drop(ws); try { ws.close(code); } catch (e) { /* уже закрыт */ } }
  webSocketError(ws) { this.drop(ws); }
}
