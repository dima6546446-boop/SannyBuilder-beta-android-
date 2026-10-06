using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace CaucasusDrive
{
    /// <summary>
    /// Сетевая часть без Unity (проверяется отдельным консольным тестом). Транспорт — UDP, топология «звезда»:
    /// сервер комнаты (в телефоне хоста или отдельный Server/relay.js) принимает до Max игроков и пересылает
    /// состояние каждого остальным. Пакет: [0xCD][ver][тип][тело]; числа little-endian, строки — байт длины + UTF-8.
    /// </summary>
    public static class NetP
    {
        public const byte Magic = 0xCD, Ver = 1;
        public const int Port = 7777, Max = 8;
        public const byte Hello = 1, Welcome = 2, Join = 3, Leave = 4, State = 5, Evt = 6, Ping = 7, Pong = 8, Full = 9, Discover = 10, Announce = 11, Bye = 12;
        public const byte FLights = 1, FBrake = 2, FHorn = 4, FIndL = 8, FIndR = 16, FFoot = 32, FRev = 64, FSit = 128;
        public const byte EvSay = 1, EvRaceStart = 10, EvRaceFinish = 11, EvRaceAbort = 12, EvEnv = 100;

        public static string CleanNick(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") if (!char.IsControl(c) && c != '<' && c != '>' && c != '&' && c != '"' && c != '\'') sb.Append(c);
            var t = sb.ToString().Trim();
            if (t.Length > 16) t = t.Substring(0, 16);
            return t.Length == 0 ? "Игрок" : t;
        }

        // ------------------------------------------------------------------ запись/чтение
        public static void WStr(BinaryWriter w, string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? "");
            int n = Math.Min(b.Length, 60);
            w.Write((byte)n); w.Write(b, 0, n);
        }
        public static string RStr(BinaryReader r) { int n = r.ReadByte(); return Encoding.UTF8.GetString(r.ReadBytes(n)); }

        public static byte[] Pack(byte type, Action<BinaryWriter> body = null)
        {
            var ms = new MemoryStream(64);
            var w = new BinaryWriter(ms);
            w.Write(Magic); w.Write(Ver); w.Write(type);
            body?.Invoke(w);
            return ms.ToArray();
        }

        /// <summary>Проверка заголовка; возвращает тип или 0 (чужой/битый пакет).</summary>
        public static byte Open(byte[] data, out BinaryReader r)
        {
            r = null;
            if (data == null || data.Length < 3 || data[0] != Magic || data[1] != Ver) return 0;
            r = new BinaryReader(new MemoryStream(data, 3, data.Length - 3));
            return data[2];
        }

        /// <summary>Локальные IPv4 адреса (для показа хосту и вычисления широковещательных адресов).</summary>
        public static List<IPAddress> LocalIps(out List<IPAddress> broadcasts)
        {
            var ips = new List<IPAddress>(); broadcasts = new List<IPAddress>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        ips.Add(ua.Address);
                        var mask = ua.IPv4Mask;
                        if (mask == null) continue;
                        var a = ua.Address.GetAddressBytes(); var m = mask.GetAddressBytes(); var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
                        broadcasts.Add(new IPAddress(b));
                    }
                }
            }
            catch (Exception) { }
            return ips;
        }
    }

    public class NetProfile
    {
        public string name = "Игрок", car = "vaz2107", plate = "";
        public int color = -1;
        public void Write(BinaryWriter w) { NetP.WStr(w, name); NetP.WStr(w, car); w.Write(color); NetP.WStr(w, plate); }
        public static NetProfile Read(BinaryReader r) { var p = new NetProfile(); p.name = NetP.CleanNick(NetP.RStr(r)); p.car = NetP.RStr(r); p.color = r.ReadInt32(); p.plate = NetP.RStr(r); return p; }
    }

    public struct NetState
    {
        public float x, z, h, vx, vz, steer;   // машина: позиция, курс (Unity-yaw), скорость, руль −1..1
        public byte flags;
        public float wx, wz, wyaw, wv;         // пешеход (если FFoot)

        public void Write(BinaryWriter w)
        {
            w.Write(x); w.Write(z); w.Write(h); w.Write(vx); w.Write(vz);
            w.Write((sbyte)Math.Max(-127, Math.Min(127, (int)(steer * 127f)))); w.Write(flags);
            if ((flags & NetP.FFoot) != 0) { w.Write(wx); w.Write(wz); w.Write(wyaw); w.Write(wv); }
        }
        public static NetState Read(BinaryReader r)
        {
            var s = new NetState { x = r.ReadSingle(), z = r.ReadSingle(), h = r.ReadSingle(), vx = r.ReadSingle(), vz = r.ReadSingle() };
            s.steer = r.ReadSByte() / 127f; s.flags = r.ReadByte();
            if ((s.flags & NetP.FFoot) != 0) { s.wx = r.ReadSingle(); s.wz = r.ReadSingle(); s.wyaw = r.ReadSingle(); s.wv = r.ReadSingle(); }
            return s;
        }
        public bool Valid => !(float.IsNaN(x) || float.IsNaN(z) || float.IsNaN(h) || Math.Abs(x) > 5000 || Math.Abs(z) > 5000);
    }

    // ===================================================================== сервер комнаты
    public class NetServer
    {
        class Peer { public byte id; public IPEndPoint ep; public NetProfile p; public float seen; }

        UdpClient udp;
        readonly Dictionary<string, Peer> byEp = new Dictionary<string, Peer>();
        readonly Dictionary<byte, Peer> byId = new Dictionary<byte, Peer>();
        readonly List<byte[]> outbox = new List<byte[]>();
        float clock;
        public string roomName = "Комната";
        public NetProfile local;             // профиль игрока на сервере (id 0); null — выделенный сервер
        public bool Running => udp != null;
        public int Count => byId.Count + (local != null ? 1 : 0);

        public event Action<byte, NetProfile> OnJoin;
        public event Action<byte> OnLeave;
        public event Action<byte, NetState> OnState;
        public event Action<byte, byte, float, float> OnEvt;

        public void Start(int port)
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            udp.EnableBroadcast = true;
        }

        public void Stop()
        {
            if (udp == null) return;
            foreach (var p in byId.Values) Send(p.ep, NetP.Pack(NetP.Leave, w => w.Write((byte)0)));
            try { udp.Close(); } catch (Exception) { }
            udp = null; byEp.Clear(); byId.Clear();
        }

        void Send(IPEndPoint ep, byte[] data) { try { udp.Send(data, data.Length, ep); } catch (Exception) { } }

        void Broadcast(byte[] data, byte except)
        {
            foreach (var p in byId.Values) if (p.id != except) Send(p.ep, data);
        }

        byte FreeId()
        {
            for (byte i = 1; i < 255; i++) if (!byId.ContainsKey(i)) return i;
            return 0;
        }

        // ------------------------------------------------------------------ локальный игрок сервера
        public void SendState(NetState s) { if (udp == null) return; Broadcast(NetP.Pack(NetP.State, w => { w.Write((byte)0); s.Write(w); }), 255); }
        public void SendEvt(byte kind, float a, float b) { if (udp == null) return; Broadcast(NetP.Pack(NetP.Evt, w => { w.Write((byte)0); w.Write(kind); w.Write(a); w.Write(b); }), 255); }

        // ------------------------------------------------------------------ цикл
        public void Update(float dt)
        {
            if (udp == null) return;
            clock += dt;
            for (int n = 0; n < 128 && udp != null; n++)
            {
                byte[] data; IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                try { if (udp.Available <= 0) break; data = udp.Receive(ref ep); }
                catch (SocketException) { continue; }
                catch (Exception) { break; }
                Handle(ep, data);
            }
            // «замолчавшие» игроки
            List<byte> dead = null;
            foreach (var p in byId.Values) if (clock - p.seen > 8f) (dead ?? (dead = new List<byte>())).Add(p.id);
            if (dead != null) foreach (var id in dead) Drop(id, false);
        }

        void Drop(byte id, bool polite)
        {
            Peer p; if (!byId.TryGetValue(id, out p)) return;
            byId.Remove(id); byEp.Remove(p.ep.ToString());
            Broadcast(NetP.Pack(NetP.Leave, w => w.Write(id)), id);
            OnLeave?.Invoke(id);
        }

        void Handle(IPEndPoint ep, byte[] data)
        {
            BinaryReader r;
            byte type = NetP.Open(data, out r);
            if (type == 0) return;
            try
            {
                if (type == NetP.Discover)
                {
                    int cnt = Count; string nm = roomName;
                    Send(ep, NetP.Pack(NetP.Announce, w => { NetP.WStr(w, nm); w.Write((byte)cnt); w.Write((byte)NetP.Max); }));
                    return;
                }
                string key = ep.ToString();
                Peer p;
                byEp.TryGetValue(key, out p);
                if (type == NetP.Hello)
                {
                    var prof = NetProfile.Read(r);
                    if (p == null)
                    {
                        if (Count >= NetP.Max) { Send(ep, NetP.Pack(NetP.Full)); return; }
                        byte id = FreeId();
                        if (id == 0) { Send(ep, NetP.Pack(NetP.Full)); return; }
                        p = new Peer { id = id, ep = ep, p = prof, seen = clock };
                        byEp[key] = p; byId[id] = p;
                        Broadcast(NetP.Pack(NetP.Join, w => { w.Write(id); prof.Write(w); }), id);
                        OnJoin?.Invoke(id, prof);
                    }
                    p.seen = clock;
                    // WELCOME (повторяем на каждый HELLO — пакет мог потеряться)
                    var me = p;
                    Send(ep, NetP.Pack(NetP.Welcome, w =>
                    {
                        w.Write(me.id);
                        int n = byId.Count - 1 + (local != null ? 1 : 0);
                        w.Write((byte)n);
                        if (local != null) { w.Write((byte)0); local.Write(w); }
                        foreach (var o in byId.Values) if (o.id != me.id) { w.Write(o.id); o.p.Write(w); }
                    }));
                    return;
                }
                if (p == null) return;
                p.seen = clock;
                switch (type)
                {
                    case NetP.State:
                        {
                            var s = NetState.Read(r);
                            if (!s.Valid) return;
                            var id = p.id;
                            Broadcast(NetP.Pack(NetP.State, w => { w.Write(id); s.Write(w); }), id);
                            OnState?.Invoke(id, s);
                            break;
                        }
                    case NetP.Evt:
                        {
                            byte kind = r.ReadByte(); float a = r.ReadSingle(), b = r.ReadSingle();
                            if (kind == NetP.EvEnv) return;          // окружение задаёт только хост
                            var id = p.id;
                            Broadcast(NetP.Pack(NetP.Evt, w => { w.Write(id); w.Write(kind); w.Write(a); w.Write(b); }), id);
                            OnEvt?.Invoke(id, kind, a, b);
                            break;
                        }
                    case NetP.Ping: Send(ep, NetP.Pack(NetP.Pong)); break;
                    case NetP.Bye: Drop(p.id, true); break;
                }
            }
            catch (Exception) { /* битый пакет */ }
        }
    }

    // ===================================================================== клиент
    public class NetClient
    {
        ITransport tr;
        bool ws;
        float clock, helloT, pingT, lastRx;
        public bool connected, failed;
        public string failReason;
        public byte myId;
        public NetProfile profile;
        public readonly Dictionary<byte, NetProfile> players = new Dictionary<byte, NetProfile>();

        public event Action OnWelcome;
        public event Action<byte, NetProfile> OnJoin;
        public event Action<byte> OnLeave;
        public event Action<byte, NetState> OnState;
        public event Action<byte, byte, float, float> OnEvt;
        public event Action<string> OnClosed;

        public bool Running => tr != null;

        /// <summary>UDP: хост в телефоне или свой сервер (host — IP или имя, например play.site.ru).</summary>
        public void Connect(string host, int port, NetProfile p)
        {
            profile = p;
            IPAddress ip;
            if (!IPAddress.TryParse(host, out ip))
            {
                var he = Dns.GetHostAddresses(host);
                ip = null; foreach (var a in he) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null) throw new Exception("адрес не найден");
            }
            tr = new UdpTransport(new IPEndPoint(ip, port));
            ws = false; clock = 0; helloT = 0; lastRx = 0;
        }

        /// <summary>WebSocket: wss://имя.workers.dev/КОД (Cloudflare Worker).</summary>
        public void ConnectWs(string url, NetProfile p)
        {
            profile = p;
            tr = new WsTransport(url);
            ws = true; clock = 0; helloT = 0; lastRx = 0;
        }

        void Send(byte[] d) { tr?.Send(d); }

        public void SendState(NetState s) { if (!connected) return; Send(NetP.Pack(NetP.State, w => s.Write(w))); }
        public void SendEvt(byte kind, float a, float b) { if (!connected) return; Send(NetP.Pack(NetP.Evt, w => { w.Write(kind); w.Write(a); w.Write(b); })); }

        public void Close()
        {
            if (tr == null) return;
            if (connected) Send(NetP.Pack(NetP.Bye));
            tr.Close(); tr = null; connected = false; players.Clear();
        }

        void Fail(string why) { failed = true; failReason = why; try { tr?.Close(); } catch (Exception) { } tr = null; connected = false; OnClosed?.Invoke(why); }

        public void Update(float dt)
        {
            if (tr == null) return;
            clock += dt;
            // сначала разбираем уже пришедшие пакеты (например, «комната заполнена» перед закрытием сокета), потом смотрим на ошибку
            byte[] data;
            for (int n = 0; n < 128 && tr != null && tr.Poll(out data); n++)
            {
                if (data.Length == 0) { lastRx = clock; continue; }
                Handle(data);
            }
            if (tr == null) return;
            if (tr.Error != null) { Fail(tr.Error); return; }
            if (!connected)
            {
                if (tr.Open)
                {
                    helloT -= dt;
                    if (helloT <= 0) { helloT = 0.5f; Send(NetP.Pack(NetP.Hello, w => profile.Write(w))); }
                }
                if (clock > (ws ? 12f : 5f)) { Fail(ws ? "Сервер не отвечает" : "Хост не отвечает"); return; }
            }
            else
            {
                pingT -= dt;
                if (pingT <= 0) { pingT = 1f; tr.Keepalive(); }
                if (clock - lastRx > 6f) { Fail("Связь потеряна"); return; }
            }
        }

        void Handle(byte[] data)
        {
            BinaryReader r;
            byte type = NetP.Open(data, out r);
            if (type == 0) return;
            lastRx = clock;
            try
            {
                switch (type)
                {
                    case NetP.Welcome:
                        {
                            myId = r.ReadByte();
                            int n = r.ReadByte();
                            players.Clear();
                            for (int i = 0; i < n; i++) { byte id = r.ReadByte(); players[id] = NetProfile.Read(r); }
                            if (!connected) { connected = true; OnWelcome?.Invoke(); }
                            break;
                        }
                    case NetP.Full: Fail("Комната заполнена"); break;
                    case NetP.Join: { byte id = r.ReadByte(); var p = NetProfile.Read(r); players[id] = p; OnJoin?.Invoke(id, p); break; }
                    case NetP.Leave:
                        {
                            byte id = r.ReadByte();
                            if (id == 0 && !ws) { Fail("Хост закрыл комнату"); break; }       // UDP: id 0 — сам сервер
                            players.Remove(id); OnLeave?.Invoke(id);
                            break;
                        }
                    case NetP.State: { byte id = r.ReadByte(); var s = NetState.Read(r); if (s.Valid) OnState?.Invoke(id, s); break; }
                    case NetP.Evt: { byte id = r.ReadByte(); byte k = r.ReadByte(); float a = r.ReadSingle(), b = r.ReadSingle(); OnEvt?.Invoke(id, k, a, b); break; }
                }
            }
            catch (Exception) { }
        }
    }

    // ===================================================================== поиск комнат в локальной сети
    public class NetDiscovery
    {
        public class Room { public IPEndPoint ep; public string name; public int players, max; public float seen; }
        UdpClient udp;
        float clock, sendT;
        public readonly List<Room> rooms = new List<Room>();

        public void Start()
        {
            udp = new UdpClient(0); udp.EnableBroadcast = true;
            sendT = 0; clock = 0;
        }

        public void Stop() { try { udp?.Close(); } catch (Exception) { } udp = null; rooms.Clear(); }

        public void Update(float dt)
        {
            if (udp == null) return;
            clock += dt; sendT -= dt;
            if (sendT <= 0)
            {
                sendT = 1.5f;
                var pk = NetP.Pack(NetP.Discover);
                List<IPAddress> bc; NetP.LocalIps(out bc);
                bc.Add(IPAddress.Broadcast);
                foreach (var b in bc) { try { udp.Send(pk, pk.Length, new IPEndPoint(b, NetP.Port)); } catch (Exception) { } }
            }
            for (int n = 0; n < 32; n++)
            {
                byte[] data; IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                try { if (udp.Available <= 0) break; data = udp.Receive(ref ep); }
                catch (SocketException) { continue; }
                catch (Exception) { break; }
                BinaryReader r;
                if (NetP.Open(data, out r) != NetP.Announce) continue;
                try
                {
                    string name = NetP.RStr(r); int pl = r.ReadByte(), mx = r.ReadByte();
                    Room found = null;
                    foreach (var q in rooms) if (q.ep.Equals(ep)) found = q;
                    if (found == null) { found = new Room { ep = ep }; rooms.Add(found); }
                    found.name = name; found.players = pl; found.max = mx; found.seen = clock;
                }
                catch (Exception) { }
            }
            rooms.RemoveAll(q => clock - q.seen > 5f);
        }
    }
}
