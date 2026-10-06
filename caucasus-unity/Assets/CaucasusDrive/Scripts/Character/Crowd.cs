using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Прохожие на тротуарах (порт Crowd.js). Каждый ходит по «кольцу» тротуаров вокруг своего квартала (в обе
    /// стороны), часть сидит на лавках остановок. Видят машину игрока: разбегаются, если она едет на них; сбитый
    /// падает и встаёт (штраф/серия — в режиме). На свист оборачиваются и отвечают. Пул фиксированного размера,
    /// NPC «переезжают» ближе к игроку. Далёкие и те, что за камерой, не рисуются.
    /// </summary>
    public class Crowd
    {
        static readonly int[] Suits = { 0x1f3c78, 0x8a1f24, 0x2f5a3a, 0x17181c, 0x5a4632, 0x4a4f57, 0x6d2f86, 0x9a7b3a, 0x2b5d7a, 0x7a2d4a, 0xb8b2a6, 0x3a3f2a };
        static readonly int[] PantsC = { 0x23252b, 0x3a3f4a, 0x1a1a1a, 0x2b3a5a, 0x4a4036, 0x5a5a5a };
        static readonly int[] Skin = { 0xe2b48f, 0xd9a57f, 0xc99872, 0xe8c1a0, 0xb88a66 };
        static readonly int[] Hair = { 0x2a1d14, 0x5a3a22, 0x101010, 0x8a8a8a, 0x8a5a2a, 0x3a2a1a };
        static readonly int[] Shoes = { 0x1a1a1a, 0xf0f0f0, 0x3a2a1c, 0x5a5a5a };
        static readonly string[] Flee = { "Куда прёшь?!", "Тротуар для пешеходов!", "Совсем сдурел?!", "Э, осторожнее!", "Права купил, что ли?" };
        static readonly string[] WhistleT = { "Чё свистишь, братан?", "Это ты мне?", "Здорово!", "Свистеть — денег не будет!", "Ну чего тебе?", "Опа, привет!" };
        const float R = 0.26f;

        class Npc
        {
            public Character c; public bool active; public string state = "walk";
            public float x, z, yaw, phase, speed, t, stuck, downT, fleeT, lookT, lookX, lookZ, benchT, v, fx, fz, s, lane;
            public int dir, bi, bj; public BusStop bench;
            public float x0, x1, z0, z1, L;
        }

        readonly App app;
        readonly List<Npc> npcs = new List<Npc>();
        readonly Rng rnd = new Rng(777);
        public bool enabled;
        float sayT;

        public Crowd(App a)
        {
            app = a;
            int count = a.quality.level == 0 ? 10 : a.quality.level == 1 ? 18 : 28;
            for (int i = 0; i < count; i++)
            {
                int suit = rnd.Pick(Suits);
                var o = new Outfit { suit = suit, stripe = rnd.Next() < 0.5f ? 0xf2f2f2 : suit, pants = rnd.Next() < 0.3f ? suit : rnd.Pick(PantsC),
                    cap = rnd.Next() < 0.35f ? rnd.Pick(new[] { 0x2b2d33, 0x8a1f24, 0x1f3c78, 0x3a3a3a }) : -1, capBrim = 0x1a1a1a,
                    shoes = rnd.Pick(Shoes), sole = 0xdddddd, skin = rnd.Pick(Skin), hair = rnd.Pick(Hair) };
                var c = new Character(o, a.worldRoot, false);
                c.root.name = "Npc" + i;
                c.root.localScale = Vector3.one * (0.92f + rnd.Next() * 0.14f);
                c.SetVisible(false);
                npcs.Add(new Npc { c = c, phase = rnd.Next() * 6, speed = 1.2f + rnd.Next() * 0.5f, t = rnd.Next() * 10 });
            }
        }

        // ------------------------------------------------------------------ маршрут: кольцо тротуара вокруг квартала
        void Loop(Npc n)
        {
            float o = n.lane;
            n.x0 = CityC.Coord(n.bi) + o; n.x1 = CityC.Coord(n.bi + 1) - o; n.z0 = CityC.Coord(n.bj) + o; n.z1 = CityC.Coord(n.bj + 1) - o;
            n.L = 2 * (n.x1 - n.x0) + 2 * (n.z1 - n.z0);
        }

        Vector2 Point(Npc n, float s)
        {
            float w = n.x1 - n.x0, h = n.z1 - n.z0;
            s = ((s % n.L) + n.L) % n.L;
            Vector2 p;
            if (s < w) p = new Vector2(n.x0 + s, n.z0);
            else if (s < w + h) p = new Vector2(n.x1, n.z0 + (s - w));
            else if (s < 2 * w + h) p = new Vector2(n.x1 - (s - w - h), n.z1);
            else p = new Vector2(n.x0, n.z1 - (s - 2 * w - h));
            // у остановки — ближе к дороге (между павильоном и столбами)
            foreach (var st in app.city.busStops)
            {
                float dx = p.x - st.pos.x, dz = p.y - st.pos.z;
                if (dx * dx + dz * dz < 12f) { if (Mathf.Abs(st.n.x) > 0.5f) p.x = st.pos.x + st.n.x * 1.3f; else p.y = st.pos.z + st.n.z * 1.3f; break; }
            }
            return p;
        }

        bool InView(float x, float z)
        {
            var cam = app.cam.transform; var f = cam.forward;
            return (x - cam.position.x) * f.x + (z - cam.position.z) * f.z > 0;
        }

        bool Spawn(Npc n, Vector3 focus, bool initial)
        {
            for (int k = 0; k < 20; k++)
            {
                n.bi = Mathf.Clamp(Mathf.FloorToInt((focus.x - CityC.Coord(0)) / CityC.SPACING) + Mathf.RoundToInt((rnd.Next() - 0.5f) * 2.4f), 0, CityC.N - 2);
                n.bj = Mathf.Clamp(Mathf.FloorToInt((focus.z - CityC.Coord(0)) / CityC.SPACING) + Mathf.RoundToInt((rnd.Next() - 0.5f) * 2.4f), 0, CityC.N - 2);
                n.lane = 8.1f + rnd.Next() * 1.3f;
                Loop(n);
                n.s = rnd.Next() * n.L;
                n.dir = rnd.Next() < 0.5f ? 1 : -1;
                var p = Point(n, n.s);
                float d = Vector2.Distance(p, new Vector2(focus.x, focus.z));
                if (d < (initial ? 6 : 35) || d > 85) continue;
                if (!initial && InView(p.x, p.y) && d < 60) continue;
                n.x = p.x; n.z = p.y; n.state = "walk";
                if (rnd.Next() < 0.25f)
                    foreach (var b in app.city.busStops)
                    {
                        float bd = Vector2.Distance(new Vector2(b.bench.x, b.bench.z), new Vector2(focus.x, focus.z));
                        if (b.used == null && bd < 85 && bd > 8) { b.used = n; n.bench = b; n.state = "bench"; n.x = b.bench.x; n.z = b.bench.z; n.yaw = b.yaw; n.benchT = 20 + rnd.Next() * 40; break; }
                    }
                n.active = true; n.stuck = 0; n.downT = 0; n.fleeT = 0;
                n.c.SetVisible(true);
                return true;
            }
            return false;
        }

        public void Start()
        {
            enabled = true;
            foreach (var n in npcs) Spawn(n, app.Focus, true);
        }

        public void Stop()
        {
            enabled = false;
            foreach (var b in app.city.busStops) b.used = null;
            foreach (var n in npcs) { n.active = false; n.bench = null; n.c.SetVisible(false); }
        }

        public void OnWhistle(Vector3 p)
        {
            Npc best = null; float bd = 16;
            foreach (var n in npcs)
            {
                if (!n.active || n.state == "down") continue;
                float d = Vector2.Distance(new Vector2(n.x, n.z), new Vector2(p.x, p.z));
                if (d < bd) { bd = d; best = n; }
            }
            if (best == null) return;
            best.lookT = 2.5f; best.lookX = p.x; best.lookZ = p.z;
            app.daily.Progress("whistle", 1);
            string say = WhistleT[Random.Range(0, WhistleT.Length)];
            app.Delay(0.7f, () => app.hud.Toast("«" + say + "»", HUD.Good, 2.5f));
        }

        void Reproject(Npc n)
        {
            float cx = Mathf.Clamp(n.x, n.x0, n.x1), cz = Mathf.Clamp(n.z, n.z0, n.z1);
            float dl = cx - n.x0, dr = n.x1 - cx, db = cz - n.z0, dtp = n.z1 - cz;
            float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dtp)), w = n.x1 - n.x0, h = n.z1 - n.z0;
            if (m == db) n.s = cx - n.x0;
            else if (m == dr) n.s = w + (cz - n.z0);
            else if (m == dtp) n.s = w + h + (n.x1 - cx);
            else n.s = 2 * w + h + (n.z1 - cz);
        }

        void Knock(Npc n, float heading, float speed)
        {
            if (n.bench != null) { n.bench.used = null; n.bench = null; }
            n.state = "down"; n.downT = 3.2f;
            var f = M.Fwd(heading);
            n.yaw = Mathf.Atan2(-f.x, -f.z);
            n.x += f.x * 0.8f; n.z += f.z * 0.8f;
            app.audio.Crash(0.35f);
            app.cameraRig.AddShake(0.25f);
            app.mode?.OnPedHit(speed);
        }

        public void Update(float dt)
        {
            if (!enabled) return;
            var focus = app.Focus;
            var pl = app.player; var cp = pl.Position; float ph = pl.Heading, ps = pl.Speed;
            float cs = Mathf.Sin(ph), cc = Mathf.Cos(ph);
            var d = pl.def.dims; float hw = d.W / 2f;
            float vLong = Vector3.Dot(pl.Velocity, M.Fwd(ph));
            sayT -= dt;
            var camT = app.cam.transform; var fwd = camT.forward; var camP = camT.position;
            foreach (var n in npcs)
            {
                if (!n.active) { Spawn(n, focus, false); continue; }
                float dF = Vector2.Distance(new Vector2(n.x, n.z), new Vector2(focus.x, focus.z));
                if (dF > 95)
                {
                    if (n.bench != null) { n.bench.used = null; n.bench = null; }
                    n.active = false; n.c.SetVisible(false); continue;
                }
                n.t += dt;
                // машина игрока: разбежаться или упасть
                if (n.state != "down" && ps > 2f && !app.onFoot)
                {
                    float dx = n.x - cp.x, dz = n.z - cp.z;
                    float lx = dx * cc - dz * cs, lz = dx * cs + dz * cc;   // в осях машины (lx — вправо)
                    float ahead = lz * Mathf.Sign(vLong == 0 ? 1 : vLong);
                    if (Mathf.Abs(lx) < hw + R && lz < d.front + R && lz > d.rear - R) { Knock(n, ph, ps); continue; }
                    if (ahead > 0 && ahead < 4 + ps * 1.2f && Mathf.Abs(lx) < hw + 1.2f && n.state != "flee")
                    {
                        if (n.bench != null) { n.bench.used = null; n.bench = null; }
                        n.state = "flee"; n.fleeT = 1.6f;
                        float side = lx >= 0 ? 1 : -1;
                        var r = M.Right(ph); n.fx = r.x * side; n.fz = r.z * side;
                        if (sayT <= 0 && dF < 25) { sayT = 6; app.hud.Toast("«" + Flee[Random.Range(0, Flee.Length)] + "»", HUD.Bad, 2f); }
                    }
                }
                float vx = 0, vz = 0; bool moving = false;
                if (n.state == "walk")
                {
                    if (n.lookT > 0) { n.lookT -= dt; n.yaw = M.DampAngle(n.yaw, Mathf.Atan2(n.lookX - n.x, n.lookZ - n.z), 6, dt); }
                    else
                    {
                        n.s += n.dir * n.speed * dt;
                        var p = Point(n, n.s);
                        float tx = p.x - n.x, tz = p.y - n.z, dd = Mathf.Sqrt(tx * tx + tz * tz);
                        if (dd > 0.01f) { vx = tx / dd * Mathf.Min(n.speed * 1.3f, dd * 4); vz = tz / dd * Mathf.Min(n.speed * 1.3f, dd * 4); }
                        n.stuck = dd > 1.6f ? n.stuck + dt : Mathf.Max(0, n.stuck - dt);
                        if (n.stuck > 1.5f) { n.dir = -n.dir; n.stuck = 0; n.s += n.dir * 1.5f; }
                        if (dd > 6) { n.x = p.x; n.z = p.y; }
                        moving = true;
                    }
                }
                else if (n.state == "flee")
                {
                    n.fleeT -= dt; vx = n.fx * 4.5f; vz = n.fz * 4.5f; moving = true;
                    if (n.fleeT <= 0) { n.state = "walk"; Reproject(n); }
                }
                else if (n.state == "bench")
                {
                    n.benchT -= dt;
                    if (n.benchT <= 0) { n.bench.used = null; n.bench = null; n.state = "walk"; Reproject(n); }
                    else if (n.lookT > 0) n.lookT -= dt;
                }
                else if (n.state == "down") { n.downT -= dt; if (n.downT <= 0) { n.state = "walk"; Reproject(n); } }
                if (moving)
                {
                    n.x += vx * dt; n.z += vz * dt;
                    float sp = Mathf.Sqrt(vx * vx + vz * vz);
                    if (sp > 0.2f) n.yaw = M.DampAngle(n.yaw, Mathf.Atan2(vx, vz), 8, dt);
                    n.v = sp;
                }
                else n.v = 0;
                if (app.onFoot)
                {
                    var w = app.walker.Pos; float dx = n.x - w.x, dz = n.z - w.z, dd = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dd < R * 2 && dd > 1e-4f) { float k = (R * 2 - dd) / dd; n.x += dx * k * 0.5f; n.z += dz * k * 0.5f; }
                }
                float cdx = n.x - camP.x, cdz = n.z - camP.z;
                bool vis = dF < 75 && cdx * fwd.x + cdz * fwd.z > -4;
                if (n.c.root.gameObject.activeSelf != vis) n.c.SetVisible(vis);
                if (!vis) continue;
                float gy = n.state == "bench" ? n.bench.bench.y + 0.06f - 0.52f : app.city.SurfaceAt(n.x, n.z).y;
                n.c.root.SetPositionAndRotation(new Vector3(n.x, gy, n.z), M.Yaw(n.yaw));
                float bx = n.state == "down" && n.downT > 0.6f ? -83f : 0f;
                var e = n.c.body.localEulerAngles.x; if (e > 180) e -= 360;
                n.c.body.localRotation = Quaternion.Euler(Mathf.Lerp(e, bx, 1f - Mathf.Exp(-8f * dt)), 0, 0);
                if (dF < 55 || (n.t * 10) % 3 < 1) Pose(n, dt);
            }
        }

        void Pose(Npc n, float dt)
        {
            var c = n.c; float t = n.t;
            if (n.state == "bench")
            {
                c.HipsY = 0.52f;
                c.Bone("thighL", -1.5f, 0, 0.08f); c.Bone("thighR", -1.5f, 0, -0.08f);
                c.Bone("shinL", 1.45f, 0, 0); c.Bone("shinR", 1.45f, 0, 0);
                c.Bone("spine", -0.06f, 0, 0); c.Bone("chest", 0.05f, 0, 0);
                c.Bone("upperArmL", -0.35f, 0, 0.12f); c.Bone("upperArmR", -0.35f, 0, -0.12f);
                c.Bone("forearmL", -0.9f, 0, 0); c.Bone("forearmR", -0.9f, 0, 0);
                c.Bone("head", 0, Mathf.Sin(t * 0.3f) * 0.5f, 0);
                return;
            }
            c.HipsY = 0.98f;
            if (n.state == "down")
            {
                c.Bone("upperArmL", 0, 0, 1.2f); c.Bone("upperArmR", 0, 0, -1.2f);
                c.Bone("thighL", 0, 0, 0.25f); c.Bone("thighR", 0, 0, -0.25f); c.Bone("shinL", 0.4f, 0, 0); c.Bone("shinR", 0, 0, 0);
                return;
            }
            float v = n.v;
            if (v > 0.15f)
            {
                float run = Mathf.Clamp01((v - 1.8f) / 2.7f);
                n.phase += dt * (5.2f + v * 1.25f);
                float amp = 0.42f + run * 0.4f, sL = Mathf.Sin(n.phase), co = Mathf.Cos(n.phase);
                c.Bone("thighL", -amp * sL - run * 0.15f, 0, 0); c.Bone("thighR", amp * sL - run * 0.15f, 0, 0);
                c.Bone("shinL", 0.1f + Mathf.Max(0, co) * (0.6f + run * 0.9f), 0, 0); c.Bone("shinR", 0.1f + Mathf.Max(0, -co) * (0.6f + run * 0.9f), 0, 0);
                c.Bone("upperArmL", amp * sL * 0.9f, 0, 0.08f); c.Bone("upperArmR", -amp * sL * 0.9f, 0, -0.08f);
                c.Bone("forearmL", -0.25f - run * 1.2f, 0, 0); c.Bone("forearmR", -0.25f - run * 1.2f, 0, 0);
                c.Bone("spine", 0.04f + run * 0.18f, sL * 0.08f, 0); c.Bone("chest", 0, -sL * 0.12f, 0);
                c.Bone("head", -run * 0.1f, 0, 0);
                c.HipsY = 0.98f - Mathf.Abs(co) * (0.02f + run * 0.05f);
            }
            else
            {
                float br = Mathf.Sin(t * 1.7f);
                c.Bone("thighL", 0, 0, 0.04f); c.Bone("thighR", 0, 0, -0.04f); c.Bone("shinL", 0, 0, 0); c.Bone("shinR", 0, 0, 0);
                c.Bone("upperArmL", 0, 0, 0.07f + br * 0.01f); c.Bone("upperArmR", 0, 0, -0.07f);
                c.Bone("forearmL", -0.12f, 0, 0); c.Bone("forearmR", -0.12f, 0, 0);
                c.Bone("spine", 0, 0, 0); c.Bone("chest", br * 0.015f, 0, 0);
                c.Bone("head", 0, n.lookT > 0 ? 0 : Mathf.Sin(t * 0.31f) * 0.5f, 0);
            }
        }
    }
}
