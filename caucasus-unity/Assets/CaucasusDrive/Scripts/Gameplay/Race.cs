using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Кольцевая трасса по кварталам города: 8 контрольных точек на круг (углы и середины сторон), старт на южной стороне.</summary>
    public static class RaceCourse
    {
        public const int Laps = 2;
        public const float Radius = 17f, Par = 150f;       // радиус КП; «эталонное» время, с
        public static readonly List<Vector3> Points = Build();

        static List<Vector3> Build()
        {
            float x2 = CityC.Coord(2), x3 = CityC.Coord(3), x4 = CityC.Coord(4);
            return new List<Vector3> {
                new Vector3(x3, 0, x2), new Vector3(x4, 0, x2),      // юг, на восток
                new Vector3(x4, 0, x3), new Vector3(x4, 0, x4),      // восток, на север
                new Vector3(x3, 0, x4), new Vector3(x2, 0, x4),      // север, на запад
                new Vector3(x2, 0, x3), new Vector3(x2, 0, x2),      // запад, на юг; последняя — «финиш» круга
            };
        }

        /// <summary>Место на стартовой решётке: две полосы (правая сторона дороги), ряды назад.</summary>
        public static Vector3 Slot(int i)
        {
            float x0 = CityC.Coord(2) + 40f, z0 = CityC.Coord(2);
            int row = i / 2, col = i % 2;
            return new Vector3(x0 - row * 9f, 0, z0 - CityC.LANES[col]);
        }
        public const float StartHeading = Mathf.PI / 2f;     // на восток
    }

    /// <summary>Заезд одного участника: отсчёт, контрольные точки, круги, время. Используется и одиночной гонкой, и сетевой.</summary>
    public class RaceRun
    {
        readonly App app;
        public float count = 3.99f, time;
        public int idx, lap;
        public bool running, finished;
        public System.Action<float> onFinish;
        int lastBeep = 99;
        float penalty;

        public RaceRun(App a) { app = a; }
        public int Total => RaceCourse.Points.Count * RaceCourse.Laps;
        public float Time => time + penalty;

        public void Begin(int slot)
        {
            var s = RaceCourse.Slot(slot);
            app.player.Place(s.x, s.z, RaceCourse.StartHeading);
            app.player.phys.fuel = app.player.phys.spec.tank;
            app.cameraRig.Snap();
            count = 3.99f; time = 0; idx = 0; lap = 0; running = false; finished = false; penalty = 0; lastBeep = 99;
            app.inputLocked = true;
            ShowTarget();
        }

        public void Hit() { penalty += 1.5f; }

        void ShowTarget()
        {
            var p = RaceCourse.Points[idx % RaceCourse.Points.Count];
            app.beacon.Show(p, idx % RaceCourse.Points.Count == RaceCourse.Points.Count - 1 && lap == RaceCourse.Laps - 1 ? 0x35e07a : 0xffcc33);
        }

        public void Stop() { app.beacon.Hide(); app.inputLocked = false; app.hud.Mission(null, null, 0); running = false; finished = true; }

        public static string Fmt(float t) { int m = Mathf.FloorToInt(t / 60f); return m + ":" + (t - m * 60f).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture); }

        public void Update(float dt, string title)
        {
            if (finished) return;
            app.beacon.Update(dt);
            if (!running)
            {
                count -= dt;
                int c = Mathf.CeilToInt(count);
                if (c != lastBeep && c >= 0) { lastBeep = c; app.audio.Beep(c > 0 ? 700f : 1400f, c > 0 ? 0.12f : 0.4f, 0.14f, Wave.Square); app.hud.DriftTag(c > 0 ? c.ToString() : "ПОЕХАЛИ!", c > 0 ? 0.9f : 1.2f); }
                app.hud.Mission(title, "Старт через " + Mathf.Max(0, c) + " · " + RaceCourse.Laps + " круга", 0f);
                if (count <= 0f) { running = true; app.inputLocked = false; }
                return;
            }
            time += dt;
            var tgt = RaceCourse.Points[idx % RaceCourse.Points.Count];
            app.hud.Nav(tgt);
            var pp = app.player.Position;
            if (Vector2.Distance(new Vector2(pp.x, pp.z), new Vector2(tgt.x, tgt.z)) < RaceCourse.Radius)
            {
                idx++;
                if (idx % RaceCourse.Points.Count == 0) { lap++; if (lap < RaceCourse.Laps) app.hud.DriftTag("КРУГ " + (lap + 1) + "/" + RaceCourse.Laps, 1.4f); }
                app.audio.Coin();
                if (idx >= Total) { finished = true; running = false; app.beacon.Hide(); app.hud.Mission(null, null, 0); onFinish?.Invoke(Time); return; }
                ShowTarget();
            }
            app.hud.Mission(title, "Круг " + Mathf.Min(RaceCourse.Laps, lap + 1) + "/" + RaceCourse.Laps + " · КП " + (idx % RaceCourse.Points.Count + 1) + "/" + RaceCourse.Points.Count + " · " + Fmt(Time) + (penalty > 0 ? " (+" + penalty.ToString("0.0") + ")" : ""), (float)idx / Total);
        }
    }

    /// <summary>Одиночная гонка на время: рекорд сохраняется, награда зависит от времени.</summary>
    public class RaceMode : GameMode
    {
        public override string Name => "race";
        readonly RaceRun run;
        bool done;

        public RaceMode(App a) : base(a) { run = new RaceRun(a); }

        public override void Enter()
        {
            app.traffic.enabled = true; app.traffic.target = Mathf.Max(3, app.quality.traffic / 2); app.traffic.Clear();
            app.player.Place(RaceCourse.Slot(0).x, RaceCourse.Slot(0).z, RaceCourse.StartHeading);
            app.traffic.center = app.player.Position; app.traffic.Prefill(app.cam);
            app.crowd.Start();
            app.SetWeather(0f, false);
            run.onFinish = Finish;
            run.Begin(0);
            float best = app.save.d.race.best;
            app.hud.Toast("Кольцевая гонка: " + RaceCourse.Laps + " круга по кварталам" + (best > 0 ? ". Рекорд " + RaceRun.Fmt(best) : ""), HUD.Good, 4f);
        }

        public override void Exit() { run.Stop(); app.crowd.Stop(); app.hud.Drift(false, 0, 1, 0); }

        public override void OnCrash(float strength, string kind, Collider col)
        {
            if (strength < 0.1f || done) return;
            app.Crash(strength); app.save.d.stats.crashes++;
            if (run.running) { run.Hit(); app.hud.DriftTag("+1.5 с", 0.8f); }
        }

        public override void Update(float dt) { if (!done) run.Update(dt, "Кольцевая гонка"); }

        void Finish(float t)
        {
            done = true;
            var d = app.save.d.race;
            bool record = d.best <= 0f || t < d.best;
            if (record) d.best = t;
            d.runs++;
            int reward = Mathf.Clamp(Mathf.RoundToInt((RaceCourse.Par * 1.35f - t) * 45f), 300, 7000) + (record ? 1000 : 0);
            app.save.AddMoney(reward);
            app.inputLocked = true;
            app.audio.Success();
            float best = d.best;
            app.Delay(1.0f, () => app.ShowResult(new Result { race = true, ok = true, time = t, best = Mathf.RoundToInt(best * 10f), record = record, reward = reward }));
        }
    }

    /// <summary>Заезд в онлайне: любой игрок жмёт «ГОНКА» — у всех отсчёт на одной трассе, результаты в табло.</summary>
    public class OnlineRace
    {
        readonly App app; readonly Online net;
        public readonly RaceRun run;
        public bool active;
        readonly List<KeyValuePair<string, float>> results = new List<KeyValuePair<string, float>>();
        float endT;

        public OnlineRace(App a, Online n) { app = a; net = n; run = new RaceRun(a); run.onFinish = Finished; }

        public void Start(bool broadcast)
        {
            if (active) return;
            if (app.onFoot) { app.hud.Toast("Сначала сядь в машину", HUD.Bad); return; }
            active = true; results.Clear(); endT = 0;
            // место на решётке — по номеру игрока
            run.Begin(Mathf.Clamp(net.MyId, 0, 7));
            if (broadcast) net.SendEvt(NetP.EvRaceStart, 0f);
            app.hud.Toast("ГОНКА! Контрольные точки отмечены на карте", HUD.Good, 3f);
        }

        public void OnEvt(byte id, byte kind, float a)
        {
            if (kind == NetP.EvRaceStart) { if (!active) Start(false); }
            else if (kind == NetP.EvRaceFinish) Add(net.NameOf(id), a);
        }

        void Add(string name, float t)
        {
            results.Add(new KeyValuePair<string, float>(name, t));
            results.Sort((x, y) => x.Value.CompareTo(y.Value));
            app.hud.Toast(results.Count + " место: " + name + " — " + RaceRun.Fmt(t), HUD.Good, 4f);
            if (endT <= 0f) endT = 25f;       // после первого финиша остальным 25 секунд
        }

        void Finished(float t)
        {
            Add(net.myName, t);
            net.SendEvt(NetP.EvRaceFinish, t);
            var d = app.save.d.race; d.runs++;
            bool rec = d.best <= 0f || t < d.best; if (rec) d.best = t;
            app.save.AddMoney(Mathf.Clamp(Mathf.RoundToInt((RaceCourse.Par * 1.35f - t) * 25f), 200, 4000));
            app.audio.Success();
        }

        public void Update(float dt)
        {
            if (!active) return;
            if (!run.finished) run.Update(dt, "Онлайн-гонка");
            else
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Mathf.Min(5, results.Count); i++) sb.Append(i + 1).Append(". ").Append(results[i].Key).Append(" ").Append(RaceRun.Fmt(results[i].Value)).Append(i < results.Count - 1 ? "   " : "");
                app.hud.Mission("Результаты гонки", sb.ToString(), 1f);
            }
            if (endT > 0f) { endT -= dt; if (endT <= 0f) Stop(); }
            else if (run.finished && results.Count >= net.Count) endT = 6f;
        }

        public void Stop()
        {
            active = false; run.Stop(); app.hud.Mission(null, null, 0);
        }
    }
}
