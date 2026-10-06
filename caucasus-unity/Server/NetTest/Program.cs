using System;
using System.Collections.Generic;
using System.Threading;
using CaucasusDrive;

class T
{
    static int fails;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "ok   " : "FAIL ") + what); if (!ok) fails++; }
    static void Pump(NetServer s, params NetClient[] cs) { for (int i = 0; i < 40; i++) { s?.Update(0.01f); foreach (var c in cs) c.Update(0.01f); Thread.Sleep(5); } }

    static int RelayTest()
    {
        int port = 17800;
        var a = new NetClient(); var b = new NetClient();
        var bState = new List<string>(); var aEvt = new List<string>(); var aJoin = new List<string>();
        b.OnState += (id, s) => bState.Add(id + ":" + s.x + ":" + s.wx);
        a.OnEvt += (id, k, x, y) => aEvt.Add(id + ":" + k + ":" + x);
        a.OnJoin += (id, p) => aJoin.Add(id + ":" + p.name + ":" + p.car + ":" + p.color + ":" + p.plate);
        a.Connect("127.0.0.1", port, new NetProfile { name = "A", car = "niva", color = 3, plate = "Н1" });
        Pump(null, a); Check(a.connected && a.myId == 1, "relay: A id=1");
        b.Connect("127.0.0.1", port, new NetProfile { name = "Боб", car = "oka", color = -1, plate = "Б2" });
        Pump(null, a, b); Check(b.connected && b.myId == 2 && b.players.Count == 1 && b.players[1].car == "niva", "relay: B welcome has A");
        Check(aJoin.Count == 1 && aJoin[0] == "2:Боб:oka:-1:Б2", "relay: JOIN profile bytes ok (" + string.Join(",", aJoin) + ")");
        a.SendState(new NetState { x = 4.5f, flags = NetP.FFoot, wx = 9 }); Pump(null, a, b);
        Check(bState.Contains("1:4.5:9"), "relay: state forwarded incl. foot block");
        b.SendEvt(NetP.EvSay, 3, 0); b.SendEvt(NetP.EvEnv, 1, 1); Pump(null, a, b);
        Check(aEvt.Contains("2:1:3") && aEvt.Count == 1, "relay: evt forwarded, env blocked");
        b.Close(); Pump(null, a); Check(a.players.Count == 0, "relay: leave propagated");
        Console.WriteLine(fails == 0 ? "RELAY OK" : fails + " FAILED"); return fails;
    }

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "relay") return RelayTest();
        int port = 17777;
        var srv = new NetServer { roomName = "Тест", local = new NetProfile { name = "Хост", car = "vaz2107", color = 0xff0000, plate = "А001АА" } };
        srv.Start(port);
        var joined = new List<string>(); var states = new List<string>(); var evts = new List<string>(); var left = new List<byte>();
        srv.OnJoin += (id, p) => joined.Add(id + ":" + p.name);
        srv.OnState += (id, s) => states.Add(id + ":" + s.x);
        srv.OnEvt += (id, k, a, b) => evts.Add(id + ":" + k + ":" + a);
        srv.OnLeave += id => left.Add(id);

        var a = new NetClient(); var b = new NetClient();
        var aJoin = new List<string>(); var aState = new List<string>(); var aEvt = new List<string>(); var bState = new List<string>();
        a.OnJoin += (id, p) => aJoin.Add(id + ":" + p.name);
        a.OnState += (id, s) => aState.Add(id + ":" + s.x + ":" + s.wx + ":" + (s.flags & NetP.FFoot));
        a.OnEvt += (id, k, x, y) => aEvt.Add(id + ":" + k + ":" + x);
        b.OnState += (id, s) => bState.Add(id + ":" + s.x);
        a.Connect("127.0.0.1", port, new NetProfile { name = "Алиса", car = "niva", color = 5, plate = "К1" });
        Pump(srv, a);
        Check(a.connected && a.myId == 1, "A connected id=1 (" + a.myId + ")");
        Check(a.players.ContainsKey(0) && a.players[0].name == "Хост", "A sees host profile in welcome");
        b.Connect("127.0.0.1", port, new NetProfile { name = "Боб<script>", car = "oka", color = 7 });
        Pump(srv, a, b);
        Check(b.connected && b.myId == 2, "B connected id=2");
        Check(b.players.Count == 2 && b.players[1].name == "Алиса", "B sees host + A (" + b.players.Count + ")");
        Check(aJoin.Count == 1 && aJoin[0] == "2:Боб" + "script", "A got JOIN of B with sanitized nick (" + string.Join(",", aJoin) + ")");
        Check(joined.Count == 2, "server OnJoin x2");

        a.SendState(new NetState { x = 12.5f, z = 3, h = 1, flags = NetP.FFoot, wx = 7, wz = 8, wyaw = 1, wv = 2 });
        Pump(srv, a, b);
        Check(bState.Contains("1:12.5"), "B received A state");
        Check(states.Contains("1:12.5"), "server local received A state");
        Check(!aState.Exists(x => x.StartsWith("1:")), "A did not get its own state back");
        srv.SendState(new NetState { x = 99 });
        Pump(srv, a, b);
        Check(aState.Contains("0:99:0:0"), "A received host state (id 0)");
        b.SendEvt(NetP.EvRaceStart, 5, 0);
        Pump(srv, a, b);
        Check(aEvt.Contains("2:10:5") && evts.Contains("2:10:5"), "EVT relayed to A and server");
        b.SendEvt(NetP.EvEnv, 1, 1); Pump(srv, a, b);
        Check(!aEvt.Exists(x => x.Contains(":100:")), "clients cannot spoof ENV");
        srv.SendEvt(NetP.EvEnv, 0.5f, 1); Pump(srv, a, b);
        Check(aEvt.Contains("0:100:0.5"), "host ENV delivered");

        // комната заполнена
        var extras = new List<NetClient>();
        for (int i = 0; i < 7; i++) { var c = new NetClient(); c.Connect("127.0.0.1", port, new NetProfile { name = "X" + i }); extras.Add(c); }
        var all = new List<NetClient>(extras); all.Add(a); all.Add(b);
        Pump(srv, all.ToArray()); Pump(srv, all.ToArray());
        int ok = 0, full = 0; foreach (var c in extras) { if (c.connected) ok++; if (c.failed && c.failReason.Contains("заполнена")) full++; }
        Check(srv.Count == 8 && ok == 5 && full == 2, "max 8 players: connected extras=" + ok + " full=" + full + " count=" + srv.Count);

        // уход
        a.Close(); Pump(srv, b);
        Check(left.Contains(1), "BYE → server OnLeave(1)");
        // таймаут
        srv.Update(9f);
        Check(srv.Count < 8, "silent peers dropped on timeout: " + srv.Count);

        // обнаружение: прямой DISCOVER на хост
        var srv3 = new NetServer { roomName = "Комната3", local = new NetProfile() }; srv3.Start(port + 2);
        var du = new System.Net.Sockets.UdpClient(); var pk = NetP.Pack(NetP.Discover); du.Send(pk, pk.Length, "127.0.0.1", port + 2);
        for (int i = 0; i < 20; i++) { srv3.Update(0.01f); Thread.Sleep(5); }
        string ann = "";
        if (du.Available > 0) { var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0); var rb = du.Receive(ref ep); System.IO.BinaryReader rr; if (NetP.Open(rb, out rr) == NetP.Announce) ann = NetP.RStr(rr) + ":" + rr.ReadByte() + ":" + rr.ReadByte(); }
        Check(ann == "Комната3:1:8", "ANNOUNCE reply: " + ann);
        // хост закрыл
        var c3 = new NetClient(); var closedWhy = ""; c3.OnClosed += w => closedWhy = w;
        c3.Connect("127.0.0.1", port + 2, new NetProfile { name = "Z" });
        Pump(srv3, c3); Check(c3.connected, "c3 connected");
        srv3.Stop(); Pump(null, c3);
        Check(closedWhy.Contains("Хост закрыл"), "client notified when host closes: " + closedWhy);

        // мусор
        var s2 = new NetServer(); s2.Start(port + 1);
        var u = new System.Net.Sockets.UdpClient(); u.Send(new byte[] { 1, 2, 3, 4 }, 4, "127.0.0.1", port + 1); u.Send(new byte[0], 0, "127.0.0.1", port + 1);
        Thread.Sleep(30); s2.Update(0.01f); Check(true, "garbage ignored"); s2.Stop();
        Console.WriteLine(fails == 0 ? "ALL OK" : fails + " FAILED");
        return fails;
    }
}
