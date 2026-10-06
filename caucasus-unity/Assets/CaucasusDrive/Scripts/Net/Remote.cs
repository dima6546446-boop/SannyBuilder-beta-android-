using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Другой игрок: машина (или он сам пешком) с плавным движением по присланным состояниям и ником над ней.</summary>
    public class Remote
    {
        public readonly byte id;
        public readonly NetProfile prof;
        public GameObject root;
        CarVisual vis;
        CarDef def;
        Rigidbody rb;
        HumanRig foot;
        TextMesh tag;
        float tagT; string tagText = "";
        Vector3 pos, tpos, vel; float h, th, steer, spin, lastRx, wyaw, wv; Vector3 wpos, twpos;
        byte flags;
        bool has, onFoot;
        float skinPick;

        public Vector3 Position => pos;
        public bool HasState => has;
        public bool OnFoot => onFoot;
        public float Speed => vel.magnitude;
        public string say;

        public Remote(byte id, NetProfile p, Transform parent)
        {
            this.id = id; prof = p;
            def = Cars.Get(p.car) ?? Cars.All[0];
            root = new GameObject("Remote_" + id + "_" + p.name);
            root.transform.SetParent(parent, false);
            int color = p.color >= 0 ? p.color : def.fixedColor >= 0 ? def.fixedColor : def.colors[0];
            var tune = new CarTune { id = def.id, color = color };
            var parts = (p.plate ?? "").Split(' ');
            if (parts.Length > 0 && parts[0].Length > 0) tune.plateText = parts[0];
            if (parts.Length > 1) tune.plateRegion = parts[1];
            vis = CarVisual.Build(def, tune, color, "LOD_hi", false, root.transform);
            // кузов — кинематический коллайдер: свою машину мы чувствуем, чужая от нашего удара не двигается
            rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.72f, (def.dims.front + def.dims.rear) / 2f);
            box.size = new Vector3(def.dims.W - 0.04f, 1.2f, def.dims.front - def.dims.rear - 0.04f);
            root.AddComponent<Obstacle>().kind = "car";
            root.SetActive(false);

            var tg = new GameObject("Tag"); tg.transform.SetParent(root.transform, false);
            tag = tg.AddComponent<TextMesh>();
            tag.font = UIKit.Font; tag.anchor = TextAnchor.LowerCenter; tag.alignment = TextAlignment.Center;
            tag.fontSize = 64; tag.characterSize = 0.06f; tag.color = Color.white;
            var mr = tg.GetComponent<MeshRenderer>(); mr.sharedMaterial = UIKit.Font.material; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tag.text = p.name;
            skinPick = id;
        }

        public void Apply(NetState s)
        {
            var np = new Vector3(s.x, 0, s.z);
            if (!has) { pos = tpos = np; h = th = s.h; has = true; root.SetActive(true); }
            tpos = np; th = s.h; vel = new Vector3(s.vx, 0, s.vz); steer = s.steer; flags = s.flags; lastRx = Time.time;
            bool f = (s.flags & NetP.FFoot) != 0;
            if (f)
            {
                var wp = new Vector3(s.wx, 0, s.wz);
                if (!onFoot || foot == null) wpos = wp;
                twpos = wp; wyaw = s.wyaw; wv = s.wv;
            }
            if (f != onFoot) SetFoot(f);
        }

        void SetFoot(bool f)
        {
            onFoot = f;
            if (f && foot == null)
            {
                string[] skins = { "Human_Blue", "Human_Red", "Human_Green" };
                foot = HumanRig.Create(root.transform.parent, skins[(int)skinPick % 3], 1.78f, false);
            }
            if (foot != null) foot.root.gameObject.SetActive(f);
        }

        public void Say(string text, float sec = 4f) { tagText = text; tagT = sec; }

        public void Update(float dt, Camera cam, float blink)
        {
            if (!has) return;
            // сглаживание + экстраполяция по скорости
            tpos += vel * dt;
            float d = (tpos - pos).magnitude;
            pos = d > 20f ? tpos : Vector3.Lerp(pos, tpos, 1f - Mathf.Exp(-11f * dt));
            h = M.DampAngle(h, th, 11f, dt);
            root.transform.SetPositionAndRotation(pos, M.Yaw(h));
            float sp = vel.magnitude;
            float dir = Vector3.Dot(vel, M.Fwd(h)) >= 0 ? 1f : -1f;
            float R = def.dims.wheelR;
            foreach (var w in vis.wheels)
            {
                w.spin.Rotate(sp * dir / R * dt * Mathf.Rad2Deg, 0, 0, Space.Self);
                if (w.front) w.pivot.localRotation = Quaternion.Euler(0, steer * 28f, 0);
            }
            bool on = (blink % 0.7f) < 0.35f;
            vis.SetLamps((flags & NetP.FLights) != 0, (flags & NetP.FBrake) != 0, (flags & NetP.FRev) != 0, on && (flags & NetP.FIndL) != 0, on && (flags & NetP.FIndR) != 0);

            // пешком
            if (onFoot && foot != null)
            {
                wpos = Vector3.Lerp(wpos, twpos, 1f - Mathf.Exp(-12f * dt));
                foot.root.position = wpos;
                foot.root.rotation = M.Yaw(wyaw);
                foot.hipsDrop = (flags & NetP.FSit) != 0 ? 0.45f : 0f;
                foot.Tick(dt, (flags & NetP.FSit) != 0 ? 0f : wv, false, 0f, false, 0f);
            }

            // ник
            tagT -= dt;
            var anchor = onFoot ? wpos + Vector3.up * 2.05f : pos + Vector3.up * 2.0f;
            float dist = Vector3.Distance(cam.transform.position, anchor);
            bool show = dist < 140f;
            if (tag.gameObject.activeSelf != show) tag.gameObject.SetActive(show);
            if (!show) return;
            string txt = tagT > 0f ? prof.name + "\n<color=#d0d0d0>" + tagText + "</color>" : prof.name;
            if (tag.text != txt) tag.text = txt;
            tag.richText = true;
            tag.transform.position = anchor;
            tag.transform.rotation = cam.transform.rotation;
            tag.characterSize = 0.045f * Mathf.Clamp(dist / 9f, 1f, 6f);
        }

        public void Destroy()
        {
            if (foot != null) foot.Destroy();
            vis?.Destroy();
            if (root) Object.Destroy(root);
        }
    }

    /// <summary>Сеанс онлайна: хост (сервер в телефоне) или клиент. Хранит остальных игроков и шлёт наше состояние ~12 раз в секунду.</summary>
    public class Online
    {
        readonly App app;
        public NetServer srv;
        public NetClient cli;
        public readonly Dictionary<byte, Remote> remotes = new Dictionary<byte, Remote>();
        Transform parent;
        float sendT, envT, clock, announceT;
        public bool IsHost => srv != null;
        public bool Connected => srv != null || (cli != null && cli.connected);
        public bool Failed => cli != null && cli.failed;
        public string FailReason => cli != null ? cli.failReason : "";
        public byte MyId => srv != null ? (byte)0 : cli.myId;
        public string myName;
        public System.Action<byte, byte, float, float> onEvt;
        public System.Action<string> onInfo;
        public System.Action<string> onClosed;
        public int Count => srv != null ? srv.Count : cli != null && cli.connected ? cli.players.Count + 1 : 1;

        public Online(App a) { app = a; }

        public NetProfile MakeProfile()
        {
            var d = app.player.def; var t = app.player.tune;
            var nick = app.save.d.settings.nick;
            if (string.IsNullOrEmpty(nick)) nick = "Игрок" + Random.Range(100, 999);
            myName = NetP.CleanNick(nick);
            return new NetProfile { name = myName, car = d.id, color = app.save.ColorOf(d.id), plate = t.plateText + " " + t.plateRegion };
        }

        public void Host(string room)
        {
            parent = new GameObject("Remotes").transform; parent.SetParent(app.worldRoot, false);
            srv = new NetServer { roomName = room, local = MakeProfile() };
            srv.Start(NetP.Port);
            srv.OnJoin += (id, p) => Joined(id, p);
            srv.OnLeave += id => Left(id);
            srv.OnState += (id, s) => GotState(id, s);
            srv.OnEvt += (id, k, a, b) => GotEvt(id, k, a, b);
        }

        public void Join(string host, int port)
        {
            parent = new GameObject("Remotes").transform; parent.SetParent(app.worldRoot, false);
            cli = new NetClient();
            cli.OnWelcome += () => { foreach (var kv in cli.players) Joined(kv.Key, kv.Value, true); };
            cli.OnJoin += (id, p) => Joined(id, p);
            cli.OnLeave += id => Left(id);
            cli.OnState += (id, s) => GotState(id, s);
            cli.OnEvt += (id, k, a, b) => GotEvt(id, k, a, b);
            cli.OnClosed += why => onClosed?.Invoke(why);
            cli.Connect(host, port, MakeProfile());
        }

        void Joined(byte id, NetProfile p, bool silent = false)
        {
            if (remotes.ContainsKey(id)) return;
            remotes[id] = new Remote(id, p, parent);
            if (!silent) onInfo?.Invoke(p.name + " в игре");
            if (Count >= 2 && !counted) { counted = true; app.save.d.stats.online++; app.save.Commit(); }
        }
        bool counted;

        void Left(byte id)
        {
            Remote r;
            if (!remotes.TryGetValue(id, out r)) return;
            onInfo?.Invoke(r.prof.name + " вышел");
            r.Destroy(); remotes.Remove(id);
        }

        void GotState(byte id, NetState s) { Remote r; if (remotes.TryGetValue(id, out r)) r.Apply(s); }

        void GotEvt(byte id, byte kind, float a, float b)
        {
            Remote r; remotes.TryGetValue(id, out r);
            if (kind == NetP.EvSay) { int i = Mathf.RoundToInt(a); if (r != null && i >= 0 && i < OnlineMode.Phrases.Length) { r.Say(OnlineMode.Phrases[i]); onInfo?.Invoke(r.prof.name + ": " + OnlineMode.Phrases[i]); } return; }
            if (kind == NetP.EvEnv) { app.SetTimeOfDay(a); app.SetWeather(b > 0.5f ? 1f : 0f, false); return; }
            onEvt?.Invoke(id, kind, a, b);
        }

        public void SendEvt(byte kind, float a, float b = 0f)
        {
            if (srv != null) srv.SendEvt(kind, a, b); else cli?.SendEvt(kind, a, b);
        }

        public string NameOf(byte id) { Remote r; return id == MyId ? myName : remotes.TryGetValue(id, out r) ? r.prof.name : "Игрок"; }

        NetState Local()
        {
            var p = app.player; var v = p.Velocity;
            var s = new NetState { x = p.Position.x, z = p.Position.z, h = p.Heading, vx = v.x, vz = v.z, steer = Mathf.Clamp(p.phys.steer / 0.5f, -1f, 1f) };
            byte f = 0;
            if (p.lightsOn) f |= NetP.FLights;
            if (p.phys.braking) f |= NetP.FBrake;
            if (app.hud.Horn) f |= NetP.FHorn;
            if (p.phys.reversing) f |= NetP.FRev;
            if (p.indicator == 'L' || p.indicator == 'H') f |= NetP.FIndL;
            if (p.indicator == 'R' || p.indicator == 'H') f |= NetP.FIndR;
            if (app.onFoot)
            {
                f |= NetP.FFoot;
                s.wx = app.walker.Pos.x; s.wz = app.walker.Pos.z; s.wyaw = app.walker.yaw; s.wv = app.walker.speed;
                if (app.walker.state == Walker.St.Sit) f |= NetP.FSit;
            }
            s.flags = f;
            return s;
        }

        public void Tick(float dt, Camera cam)
        {
            clock += dt;
            srv?.Update(dt); cli?.Update(dt);
            sendT -= dt;
            if (sendT <= 0f && Connected) { sendT = 1f / 12f; var s = Local(); if (srv != null) srv.SendState(s); else cli.SendState(s); }
            if (srv != null) { envT -= dt; if (envT <= 0f) { envT = 6f; srv.SendEvt(NetP.EvEnv, app.dayNight.time, app.rainLevel > 0.3f ? 1f : 0f); } }
            foreach (var r in remotes.Values) r.Update(dt, cam, clock);
        }

        public void Close()
        {
            srv?.Stop(); cli?.Close();
            foreach (var r in remotes.Values) r.Destroy();
            remotes.Clear();
            if (parent) Object.Destroy(parent.gameObject);
            srv = null; cli = null;
        }
    }
}
