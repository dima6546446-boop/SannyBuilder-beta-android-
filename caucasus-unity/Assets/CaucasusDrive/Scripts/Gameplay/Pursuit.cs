using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Ведущий ДПС: едет по дорожному графу города к игроку, на подходе — напрямую. Чистая математика без Unity-объектов
    /// (проверяется в тесте): поворот ограничен боковым ускорением, скорость падает на поворотах и растёт на прямых.
    /// </summary>
    public class Chaser
    {
        public float x, z, h, v;
        public const float VMax = 31f, Accel = 7f, Brake = 14f, LatAcc = 13f;
        readonly RoadGraph g;
        readonly List<int> path = new List<int>();
        float replanT;
        public int Waypoints => path.Count;

        public Chaser(RoadGraph g) { this.g = g; }

        public int NearestNode(float px, float pz, float? ahead = null)
        {
            int best = 0; float bd = 1e9f;
            foreach (var n in g.nodes)
            {
                float dx = n.x - px, dz = n.z - pz, d = dx * dx + dz * dz;
                if (ahead.HasValue && d > 1f)
                {
                    float dot = (dx * Mathf.Sin(ahead.Value) + dz * Mathf.Cos(ahead.Value)) / Mathf.Sqrt(d);
                    if (dot < 0.2f) d += 90f * 90f;      // узлы позади штрафуем
                }
                if (d < bd) { bd = d; best = n.id; }
            }
            return best;
        }

        /// <summary>Кратчайший путь по графу (Дейкстра на 49 узлах).</summary>
        public static List<int> Route(RoadGraph g, int from, int to)
        {
            int n = g.nodes.Count;
            var dist = new float[n]; var prev = new int[n]; var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = 1e9f; prev[i] = -1; }
            dist[from] = 0;
            for (int it = 0; it < n; it++)
            {
                int u = -1; float bd = 1e9f;
                for (int i = 0; i < n; i++) if (!done[i] && dist[i] < bd) { bd = dist[i]; u = i; }
                if (u < 0) break;
                done[u] = true;
                if (u == to) break;
                foreach (int w in g.nodes[u].nb)
                {
                    float d = dist[u] + Mathf.Sqrt(Mathf.Pow(g.nodes[u].x - g.nodes[w].x, 2) + Mathf.Pow(g.nodes[u].z - g.nodes[w].z, 2));
                    if (d < dist[w]) { dist[w] = d; prev[w] = u; }
                }
            }
            var r = new List<int>();
            for (int c = to; c >= 0; c = prev[c]) { r.Add(c); if (c == from) break; }
            r.Reverse();
            return r;
        }

        public void Place(float px, float pz, float toX, float toZ)
        {
            x = px; z = pz; v = 8f; h = Mathf.Atan2(toX - px, toZ - pz); path.Clear(); replanT = 0;
        }

        public float DistTo(float px, float pz) { return Mathf.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z)); }

        public void Step(float dt, float px, float pz, float pSpeed)
        {
            replanT -= dt;
            float dp = DistTo(px, pz);
            if (replanT <= 0f)
            {
                replanT = 0.7f;
                path.Clear();
                if (dp > 55f)
                {
                    int a = NearestNode(x, z, h), b = NearestNode(px, pz);
                    path.AddRange(Route(g, a, b));
                }
            }
            // цель: ближайшая впереди точка пути, иначе игрок
            float tx = px, tz = pz;
            while (path.Count > 0)
            {
                var n = g.nodes[path[0]];
                float d = Mathf.Sqrt((n.x - x) * (n.x - x) + (n.z - z) * (n.z - z));
                if (d < 11f && path.Count > 1) { path.RemoveAt(0); continue; }
                if (path.Count == 1 && dp < 55f) { path.RemoveAt(0); break; }
                tx = n.x; tz = n.z; break;
            }
            Drive(dt, tx, tz, path.Count > 1 ? (float?)g.nodes[path[1]].x : null, path.Count > 1 ? (float?)g.nodes[path[1]].z : null, VMax, dp, pSpeed);
        }

        /// <summary>Один шаг движения к точке (tx,tz): поворот ограничен боковым ускорением, перед поворотом на следующую точку — торможение.</summary>
        public void Drive(float dt, float tx, float tz, float? nx, float? nz, float vmax, float dp, float pSpeed)
        {
            float want = Mathf.Atan2(tx - x, tz - z);
            float e = M.WrapAngle(want - h);
            float rate = Mathf.Min(2.6f, LatAcc / Mathf.Max(v, 4f));
            h += Mathf.Clamp(e, -rate * dt, rate * dt);
            float vt = vmax * Mathf.Clamp(1f - Mathf.Abs(e) / 1.1f, 0.3f, 1f);
            // торможение перед поворотом: на узле нужна скорость vc, тормозной путь (v²−vc²)/2B
            if (nx.HasValue)
            {
                float dn = Mathf.Sqrt((tx - x) * (tx - x) + (tz - z) * (tz - z));
                float turn = Mathf.Abs(M.WrapAngle(Mathf.Atan2(nx.Value - tx, nz.Value - tz) - Mathf.Atan2(tx - x, tz - z)));
                float vc = Mathf.Lerp(vmax, 11f, Mathf.Clamp01(turn / 1.1f));
                vt = Mathf.Min(vt, Mathf.Sqrt(vc * vc + 2f * Brake * 0.8f * Mathf.Max(dn - 4f, 0f)));
            }
            if (dp < 14f) vt = Mathf.Min(vt, Mathf.Max(pSpeed * 0.9f, 0f));            // рядом — подстраивается под игрока и прижимает
            v = v < vt ? Mathf.Min(vt, v + Accel * dt) : Mathf.Max(vt, v - Brake * dt);
            x += Mathf.Sin(h) * v * dt; z += Mathf.Cos(h) * v * dt;
        }
    }

    /// <summary>Погоня ДПС в свободной езде: «Остановитесь!» — оторвись (≥ 260 м на 7 с) или остановись рядом (задержание, штраф).</summary>
    public class Pursuit
    {
        readonly App app;
        Chaser ch; GameObject car;
        public bool active;
        float busted, far, siren, cool = 25f, lockT, lifeT;
        bool engaged;
        const float EscapeDist = 260f, EscapeTime = 7f, BustTime = 2.4f, Fine = 4000f;

        public Pursuit(App a) { app = a; }

        public bool Ready => !active && cool <= 0f && !app.onFoot;

        public void Start(string reason)
        {
            if (!Ready) return;
            ch = new Chaser(app.graph);
            var pp = app.player.Position; float hd = app.player.Heading;
            // узел 160–330 м от игрока, лучше позади
            int best = -1; float bs = -1e9f;
            foreach (var n in app.graph.nodes)
            {
                float dx = n.x - pp.x, dz = n.z - pp.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < 150f || d > 240f) continue;
                float dot = (dx * Mathf.Sin(hd) + dz * Mathf.Cos(hd)) / d;
                float sc = -dot * 100f - Mathf.Abs(d - 200f) * 0.2f;
                if (sc > bs) { bs = sc; best = n.id; }
            }
            if (best < 0) return;
            var nn = app.graph.nodes[best];
            ch.Place(nn.x, nn.z, pp.x, pp.z);
            ch.v = 14f;
            car = TrafficManager.MakeCarObject(Cars.Get("police"), 0xf2f2f2, app.worldRoot, true, app.quality.level >= 2);
            car.GetComponent<Obstacle>().kind = "car";
            Apply();
            active = true; busted = 0; far = 0; siren = 0; lifeT = 0; engaged = false;
            app.audio.Siren();
            app.hud.Toast("ДПС: «Водитель, прижмитесь к обочине и остановитесь!»  " + reason, HUD.Bad, 4.5f);
        }

        void Apply()
        {
            car.transform.SetPositionAndRotation(new Vector3(ch.x, 0, ch.z), M.Yaw(ch.h));
        }

        public void Exit() { End(false); app.inputLocked = false; app.hud.Mission(null, null, 0); }

        void End(bool setCool)
        {
            active = false;
            if (car) Object.Destroy(car); car = null;
            if (setCool) cool = 90f;
        }

        public void Update(float dt)
        {
            if (cool > 0f) cool -= dt;
            if (lockT > 0f) { lockT -= dt; if (lockT <= 0f) app.inputLocked = false; }
            if (!active) return;
            lifeT += dt;
            var pp = app.player.Position; float ps = app.player.Speed;
            ch.Step(dt, pp.x, pp.z, ps);
            Apply();
            float dist = ch.DistTo(pp.x, pp.z);
            siren -= dt; if (siren <= 0f) { siren = 3.2f; if (dist < 220f) app.audio.Siren(); }
            app.hud.Nav(new Vector3(ch.x, 0, ch.z));
            // задержание: остановился рядом с патрульной
            if (dist < 11f && ps < 3.5f) busted += dt; else busted = Mathf.Max(0f, busted - dt * 0.7f);
            if (dist < 170f) engaged = true;                    // «оторвался» засчитывается только после того, как погоня сблизилась
            far = engaged && dist > EscapeDist ? far + dt : Mathf.Max(0f, far - dt * 2f);
            string s = dist < 40f ? "Патрульная рядом! Остановишься — задержат" : "Оторвись на " + Mathf.CeilToInt(EscapeDist) + " м от погони";
            app.hud.Mission("ПОГОНЯ ДПС", s + " · " + Mathf.RoundToInt(dist) + " м", Mathf.Clamp01(Mathf.Max(far / EscapeTime, busted / BustTime)));
            if (busted >= BustTime) Busted();
            else if (far >= EscapeTime) Escaped();
            else if (app.onFoot || lifeT > 240f) { End(true); app.hud.Mission(null, null, 0); }
        }

        void Busted()
        {
            int fine = (int)Fine;
            app.save.d.money = Mathf.Max(0, app.save.d.money - fine);
            app.save.d.stats.fines += fine;
            app.save.Commit();
            app.hud.Toast("ЗАДЕРЖАН! Протокол и штраф " + M.Rub(fine), HUD.Bad, 4f);
            app.audio.Siren();
            app.inputLocked = true; lockT = 2.5f;
            End(true); app.hud.Mission(null, null, 0);
        }

        void Escaped()
        {
            app.save.AddMoney(1500);
            app.save.d.stats.escapes++;
            app.save.Commit();
            app.hud.Toast("Оторвались! Погоня окончена · +" + M.Rub(1500), HUD.Good, 3.5f);
            app.audio.Success();
            End(true); app.hud.Mission(null, null, 0);
        }

        public void RenderGlow(Glow glow, bool blink)
        {
            if (!active || car == null) return;
            var t = car.transform;
            glow.Add(t.position + Vector3.up * 1.65f + t.right * (blink ? -0.3f : 0.3f), blink ? new Color(0.1f, 0.2f, 1f) : new Color(1f, 0.1f, 0.1f), 1.1f);
            glow.Add(t.position + Vector3.up * 0.7f + t.forward * 2.0f + t.right * 0.6f, new Color(1f, 0.95f, 0.8f), 0.6f);
            glow.Add(t.position + Vector3.up * 0.7f + t.forward * 2.0f - t.right * 0.6f, new Color(1f, 0.95f, 0.8f), 0.6f);
        }
    }
}
