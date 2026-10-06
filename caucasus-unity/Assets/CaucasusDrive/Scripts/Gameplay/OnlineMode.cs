using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Свободная езда с другими игроками: хост или клиент (UDP), фразы, общая погода, заезды на время.</summary>
    public class OnlineMode : FreeRideMode
    {
        public static readonly string[] Phrases = { "Привет!", "Погнали!", "Давай гонку!", "Красиво едешь", "Осторожно!", "Ха-ха", "Спасибо", "Пока!" };
        public override string Name => "online";

        readonly bool host; readonly string ip;
        Online net;
        OnlineRace race;
        float listT; string error;

        public OnlineMode(App a, bool host, string ip) : base(a, false) { this.host = host; this.ip = ip; }

        public override bool Restartable => false;

        public override void Enter()
        {
            base.Enter();
            app.traffic.target = Mathf.Max(3, app.quality.traffic / 2);      // трафик у каждого свой — в онлайне его меньше
            net = new Online(app);
            net.onInfo = t => app.hud.Toast(t, HUD.Good, 2.5f);
            net.onClosed = why => { error = why; };
            try
            {
                if (host) net.Host(string.IsNullOrEmpty(app.save.d.settings.nick) ? "Комната" : app.save.d.settings.nick);
                else
                {
                    string h = ip; int port = NetP.Port;
                    int c = ip.LastIndexOf(':');
                    if (c > 0) { int.TryParse(ip.Substring(c + 1), out port); h = ip.Substring(0, c); }
                    app.save.d.settings.lastIp = ip; app.save.Commit();
                    net.Join(h, port);
                    app.hud.Toast("Подключаюсь к " + ip + "…", HUD.Good, 3f);
                }
            }
            catch (System.Exception e) { error = e.Message; }
            race = new OnlineRace(app, net);
            net.onEvt = (id, k, a, b) => race.OnEvt(id, k, a);
            app.hud.ShowChat(true);
            if (host)
            {
                var ips = NetP.LocalIps(out var _bc);
                string s = ips.Count > 0 ? ips[0].ToString() : "—";
                app.hud.Toast("Ты хост. Друзьям: «Онлайн → Найти в Wi-Fi» или IP " + s + ":" + NetP.Port, HUD.Good, 6f);
            }
        }

        public override void Exit()
        {
            app.hud.ShowChat(false); app.hud.NetList(null);
            race?.Stop();
            net?.Close();
            base.Exit();
        }

        public override void OnSay(int i)
        {
            if (net == null || !net.Connected) return;
            net.SendEvt(NetP.EvSay, i);
            app.hud.Toast("Ты: " + Phrases[i], 0, 1.6f);
        }

        public override void IdleUpdate(float dt) { net?.Tick(dt, app.cam); }

        public override void OnRaceButton() { if (net != null && net.Connected && !race.active) race.Start(true); }

        public override void OnAction(int i)
        {
            if (atPump) { base.OnAction(i); return; }
            if (i == 0) OnRaceButton();
        }

        public override void Update(float dt)
        {
            if (error != null)
            {
                string e = error; error = null;
                app.hud.Toast(e, HUD.Bad, 4f);
                app.Delay(0.3f, () => app.ToMenu("online"));
                return;
            }
            base.Update(dt);
            net.Tick(dt, app.cam);
            race.Update(dt);
            if (!atPump && !race.active) app.hud.Actions(net.Connected ? "ГОНКА" : null, null, null);
            listT -= dt;
            if (listT <= 0f)
            {
                listT = 0.5f;
                var sb = new System.Text.StringBuilder();
                sb.Append(host ? "Хост · " : "Онлайн · ").Append(net.Count).Append(" из ").Append(NetP.Max);
                if (!net.Connected) sb.Append(" · подключение…");
                foreach (var r in net.remotes.Values)
                {
                    float dist = Vector3.Distance(app.player.Position, r.Position);
                    sb.Append('\n').Append(r.prof.name);
                    if (r.HasState) sb.Append("  ").Append(dist < 1000f ? Mathf.RoundToInt(dist) + " м" : (dist / 1000f).ToString("0.0") + " км");
                }
                app.hud.NetList(sb.ToString());
            }
        }
    }
}
