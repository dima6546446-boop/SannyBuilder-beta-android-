using System.Collections.Generic;
using UnityEngine;
using RussianDrift.AI;
using RussianDrift.Audio;
using RussianDrift.Core;
using RussianDrift.Meta;
using RussianDrift.Vehicle;
using RussianDrift.World;

namespace RussianDrift.UI
{
    public class RunResult
    {
        public GameMode mode;
        public string trackId;
        public bool win;
        public int score;
        public int rivalScore;
        public float time;
        public float avgSpeed;
        public int medal;
        public int coins;
        public int xp;
        public bool newRecord;
        public string title = "";
        public string detail = "";
    }

    public class ModeContext
    {
        public GameWorldController gw;
        public WorldInfo world;
        public VehicleController player;
        public DriftScorer scorer;
        public TrackDefinition track;
        public Hud hud;
        public ProfileService profile;
        public CameraRig camRig;
        public Transform root;
    }

    /// <summary>Base class for the four game modes. Controllers are plain objects driven by GameWorldController.</summary>
    public abstract class ModeController
    {
        protected ModeContext c;
        public bool Finished { get; protected set; }
        public RunResult Result { get; protected set; }
        public virtual bool UsesTraffic { get { return false; } }
        public virtual bool Timed { get { return true; } }

        public virtual void Setup(ModeContext ctx) { c = ctx; }
        public virtual void Begin() { }
        public abstract void Tick(float dt);
        public virtual void Respawn() { c.player.Recover(); }
        public virtual void Dispose() { }

        protected static string Clock(float t)
        {
            if (t < 0f) t = 0f;
            int m = (int)(t / 60f); int s = (int)(t - m * 60f);
            return m.ToString("0") + ":" + s.ToString("00");
        }

        /// <summary>Puts a car on the path `dist` metres along it with a lateral offset (right positive).</summary>
        protected void Place(VehicleController v, TrackPath path, float dist, float lateral)
        {
            Vector3 t;
            Vector3 p = path.Sample(dist, out t);
            t.y = 0f; t.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, t);
            c.gw.PlaceCar(v, p + right * lateral, t);
        }
    }

    // ============================================================================================
    public class FreeRoamMode : ModeController
    {
        public override bool UsesTraffic { get { return true; } }
        public override bool Timed { get { return false; } }
        private float weatherTimer = 240f;

        public override void Setup(ModeContext ctx)
        {
            base.Setup(ctx);
            c.gw.PlaceCar(c.player, c.world.freeRoamSpawn, Vector3.forward);
            c.hud.mode.visible = false;
            GameEvents.DriftBanked += OnBanked;
        }

        public override void Dispose() { GameEvents.DriftBanked -= OnBanked; }

        private void OnBanked(int pts, float mul, float dur)
        {
            c.profile.AddCurrency(Mathf.RoundToInt(pts / 25f));
            c.profile.AddXp(Mathf.RoundToInt(pts / 70f));
            c.profile.Data.AddStat("freeroam_points", pts);
        }

        public override void Tick(float dt)
        {
            weatherTimer -= dt;
            if (weatherTimer <= 0f && c.gw.Weather != null)
            {
                weatherTimer = 200f + Random.value * 200f;
                float r = Random.value;
                c.gw.Weather.SetWeather(r < 0.5f ? WeatherState.Clear : (r < 0.8f ? WeatherState.Rain : WeatherState.Fog));
            }
        }

        public override void Respawn()
        {
            // back to the nearest road centre-line heading along it
            var g = c.world.graph;
            var n = g.Nearest(c.player.transform.position);
            Vector3 f = c.player.transform.forward; f.y = 0f;
            Vector3 dir = Mathf.Abs(f.x) > Mathf.Abs(f.z) ? new Vector3(Mathf.Sign(f.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(f.z));
            c.gw.PlaceCar(c.player, n.pos + RoadGraph.LaneShift(n.pos, n.pos + dir), dir);
        }
    }

    // ============================================================================================
    public class DriftTrackMode : ModeController
    {
        private TrackPath path;
        private PathProgress prog;
        private float timeLeft;
        private bool running;
        private float elapsed;

        public override bool UsesTraffic { get { return c.track != null && c.track.kind == TrackKind.CityRing; } }

        public override void Setup(ModeContext ctx)
        {
            base.Setup(ctx);
            path = c.world.PathFor(c.track.kind);
            Place(c.player, path, path.closed ? path.length - 14f : 6f, 0f);
            prog = new PathProgress(path);
            prog.ResetAt(c.player.transform.position, path.closed);
            DriftZone.Create(c.world.ZonesFor(c.track.kind), c.root);
            timeLeft = c.track.timeLimit;
            c.hud.mode.title = Loc.T("mode.drift");
            c.scorer.SetEnabled(false);
        }

        public override void Begin()
        {
            c.scorer.ResetAll();
            c.scorer.SetEnabled(true);
            running = true;
        }

        public override void Tick(float dt)
        {
            var m = c.hud.mode;
            if (!running || Finished) { m.timer = Clock(timeLeft); return; }
            timeLeft -= dt; elapsed += dt;
            prog.Update(c.player.transform.position);
            int score = Mathf.RoundToInt(c.scorer.TotalScore);
            m.timer = Clock(timeLeft);
            m.line1 = Loc.T("hud.score") + ": " + Ui.Money(score);
            float[] medals = c.track.medalScores;
            string next = Loc.T("hud.gold") + "!";
            float bar = 1f;
            for (int i = 0; i < medals.Length; i++)
                if (score < medals[i])
                {
                    string[] names = { Loc.T("hud.bronze"), Loc.T("hud.silver"), Loc.T("hud.gold") };
                    next = names[i] + ": " + Ui.Money((long)medals[i]);
                    bar = Mathf.Clamp01(score / medals[i]);
                    break;
                }
            m.line2 = next; m.bar = bar;
            m.barColor = bar >= 1f ? Theme.Gold : Theme.Accent2;
            bool finishedRoute = !path.closed && prog.maxTotal >= path.length - 10f;
            if (timeLeft <= 0f || finishedRoute) Finish();
        }

        private void Finish()
        {
            Finished = true; running = false;
            c.scorer.SetEnabled(false);
            int score = Mathf.RoundToInt(c.scorer.TotalScore);
            var rw = RewardCalculator.ForDrift(score, c.track, elapsed);
            float prev = c.profile.Data.GetBest(c.track.id);
            Result = new RunResult
            {
                mode = GameMode.DriftTrack, trackId = c.track.id, score = score, time = elapsed, medal = rw.medal, coins = rw.coins, xp = rw.xp,
                newRecord = score > prev, title = GameCatalog.TrackName(c.track),
                detail = Loc.F("res.bestchain", Ui.Money(Mathf.RoundToInt(c.scorer.BestChain))) + "   " + Loc.F("res.bestmult", c.scorer.BestMultiplier.ToString("0.0"))
            };
            c.profile.RecordBest(c.track.id, score);
        }
    }

    // ============================================================================================
    public class TimeAttackMode : ModeController
    {
        private TrackPath path;
        private PathProgress prog;
        private CheckpointGates gates;
        private int laps;
        private float total, lapStart, bestLap = float.MaxValue, lastLap;
        private bool running;
        private int lapsDone;
        private int nextGate;
        private float wrongTimer;
        private float lastSafeDist;

        public override bool UsesTraffic { get { return c.track != null && c.track.kind == TrackKind.CityRing; } }

        public override void Setup(ModeContext ctx)
        {
            base.Setup(ctx);
            path = c.world.PathFor(c.track.kind);
            laps = path.closed ? Mathf.Max(1, c.track.laps) : 1;
            Place(c.player, path, path.closed ? path.length - 10f : 4f, 0f);
            prog = new PathProgress(path);
            prog.ResetAt(c.player.transform.position, path.closed);
            gates = CheckpointGates.Create(path, path.closed ? 10 : 12, c.root, !path.closed);
            nextGate = 0;
            if (gates.gates.Count > 0) gates.SetActive(0);
            c.hud.mode.title = Loc.T("mode.ta");
            c.scorer.SetEnabled(false);
        }

        public override void Begin() { running = true; total = 0f; lapStart = 0f; }

        public override void Tick(float dt)
        {
            var m = c.hud.mode;
            if (!running || Finished) return;
            total += dt;
            Vector3 pos = c.player.transform.position;
            prog.Update(pos);
            float lapDist = prog.maxTotal - lapsDone * path.length;
            lastSafeDist = prog.maxTotal;

            // gates
            if (gates.gates.Count > 0)
            {
                float gd = gates.distances[nextGate % gates.distances.Count] + (nextGate / gates.distances.Count) * path.length;
                if (path.closed && gd < 0f) gd += path.length;
                if (prog.maxTotal >= gd - 4f)
                {
                    AudioManager.Ensure().Play("checkpoint", 0.8f);
                    GameEvents.RaiseCheckpoint(nextGate);
                    nextGate++;
                    gates.SetActive(nextGate % gates.gates.Count);
                }
            }

            if (path.closed)
            {
                int done = Mathf.FloorToInt(prog.maxTotal / path.length);
                if (done > lapsDone && done >= 1)
                {
                    lapsDone = done;
                    lastLap = total - lapStart; lapStart = total;
                    bestLap = Mathf.Min(bestLap, lastLap);
                    GameEvents.RaiseLap(lapsDone);
                    AudioManager.Ensure().Play("combo", 0.9f);
                    c.hud.Notify(Loc.F("hud.lapdone", lapsDone, Ui.FormatTime(lastLap)), Theme.Good);
                    if (lapsDone >= laps) { Finish(); return; }
                }
            }
            else if (prog.maxTotal >= path.length - 8f) { Finish(); return; }

            float vdot = Vector3.Dot(Vector3.ProjectOnPlane(c.player.Body.Vel(), Vector3.up).normalized, prog.Tangent);
            wrongTimer = (c.player.SpeedMs > 5f && vdot < -0.35f) ? wrongTimer + dt : 0f;
            m.wrongWay = wrongTimer > 1.2f;

            m.timer = Ui.FormatTime(total);
            m.line1 = path.closed ? Loc.F("hud.lap", Mathf.Min(laps, lapsDone + 1), laps) : Loc.T("hud.ptp");
            m.line2 = (bestLap < float.MaxValue ? Loc.T("hud.bestlap") + " " + Ui.FormatTime(bestLap) : Loc.T("hud.firstlap"));
            m.bar = Mathf.Clamp01(path.closed ? Mathf.Repeat(prog.maxTotal, path.length) / path.length : prog.maxTotal / path.length);
            m.barColor = Theme.Accent2;
        }

        public override void Respawn()
        {
            float d = Mathf.Max(0f, Mathf.Repeat(lastSafeDist, path.length) - 6f);
            if (!path.closed) d = Mathf.Max(0f, lastSafeDist - 6f);
            Place(c.player, path, d, 0f);
            prog.ResetAt(c.player.transform.position, false);
            prog.laps = lapsDone; prog.total = lapsDone * path.length + path.DistanceAt(prog.index);
        }

        private void Finish()
        {
            Finished = true; running = false;
            var rw = RewardCalculator.ForTimeAttack(total, path.length, laps, c.track);
            float avg = path.length * laps / Mathf.Max(0.1f, total) * 3.6f;
            float prev = c.profile.Data.GetBest(c.track.id);
            Result = new RunResult
            {
                mode = GameMode.TimeAttack, trackId = c.track.id, time = total, avgSpeed = avg, medal = rw.medal, coins = rw.coins, xp = rw.xp,
                newRecord = prev <= 0f || total < prev, title = GameCatalog.TrackName(c.track),
                detail = Loc.F("res.avgspeed", Mathf.RoundToInt(avg)) + (path.closed ? "   " + Loc.T("hud.bestlap") + " " + Ui.FormatTime(bestLap) : "")
            };
            c.profile.RecordBest(c.track.id, total, false);
            Services.Get<AchievementService>().Check();
        }
    }

    // ============================================================================================
    public class BattleMode : ModeController
    {
        private TrackPath path;
        private float timeLeft;
        private bool running;
        private VehicleController ai;
        private DriftScorer aiScorer;
        private AIDriver driver;
        private PathProgress playerProg;
        private string rivalName = "";
        private float elapsed;

        public override bool UsesTraffic { get { return false; } }

        public override void Setup(ModeContext ctx)
        {
            base.Setup(ctx);
            path = c.world.PathFor(c.track.kind);
            Place(c.player, path, path.closed ? path.length - 16f : 5f, 2.2f);
            playerProg = new PathProgress(path);
            playerProg.ResetAt(c.player.transform.position, path.closed);
            DriftZone.Create(c.world.ZonesFor(c.track.kind), c.root);

            // rival car scales with difficulty
            var diff = GameSession.Difficulty;
            string[] ids = { "pyaterka", "desyatka", "ronin", "raketa" };
            var def = GameCatalog.GetCar(ids[Mathf.Clamp((int)diff, 0, 3)]);
            var setup = new CarSetup { carId = def.id, colorHex = "E02020", plate = "R" + Random.Range(100, 999) + "VL", region = "99" };
            int lv = (int)diff + 1;
            setup.engine = lv; setup.tires = Mathf.Min(5, lv); setup.suspension = Mathf.Min(5, lv); setup.differential = 4; setup.turbo = lv > 2 ? lv - 1 : 0;
            setup.drive = (int)def.defaultDrive;
            rivalName = GameCatalog.CarName(def);
            Vector3 t; Vector3 sp = path.Sample(path.closed ? path.length - 4f : 14f, out t);
            var res = VehicleFactory.Create(def, setup, sp + Vector3.up, Quaternion.LookRotation(t), false, null, false);
            ai = res.vc; aiScorer = res.scorer; aiScorer.RaiseGlobalEvents = false;
            driver = res.go.AddComponent<AIDriver>();
            driver.Setup(ai, path, diff, path.closed ? path.length - 4f : 14f);
            Place(ai, path, path.closed ? path.length - 4f : 14f, -2.2f);
            ai.Frozen = true;
            EngineAudio.Attach(ai, false);
            timeLeft = c.track.timeLimit;
            c.hud.mode.title = Loc.T("mode.battle") + "  ·  " + rivalName;
            c.scorer.SetEnabled(false); aiScorer.SetEnabled(false);
        }

        public override void Begin()
        {
            c.scorer.ResetAll(); aiScorer.ResetAll();
            c.scorer.SetEnabled(true); aiScorer.SetEnabled(true);
            ai.Frozen = false; driver.racing = true;
            running = true;
        }

        public override void Dispose() { }

        public override void Tick(float dt)
        {
            var m = c.hud.mode;
            if (!running || Finished) return;
            timeLeft -= dt; elapsed += dt;
            playerProg.Update(c.player.transform.position);
            int me = Mathf.RoundToInt(c.scorer.TotalScore), rival = Mathf.RoundToInt(aiScorer.TotalScore);
            m.timer = Clock(timeLeft);
            m.line1 = Loc.T("battle.you") + ": " + Ui.Money(me);
            m.line2 = "";
            m.rightScore = Loc.T("battle.rival") + ": " + Ui.Money(rival);
            m.bar = (me + rival) > 0 ? Mathf.Clamp01((float)me / (me + rival)) : 0.5f;
            m.barColor = me >= rival ? Theme.Good : Theme.Bad;

            // rubber band the AI so the duel stays close
            if (driver.Progress != null)
            {
                float gap = driver.Progress.total - playerProg.total;
                driver.rubberBand = gap > 90f ? -0.12f : (gap < -90f ? 0.1f : 0f);
            }
            bool done = timeLeft <= 0f || (!path.closed && (playerProg.maxTotal >= path.length - 10f));
            if (done) Finish();
        }

        private void Finish()
        {
            Finished = true; running = false;
            c.scorer.SetEnabled(false); aiScorer.SetEnabled(false);
            driver.racing = false;
            int me = Mathf.RoundToInt(c.scorer.TotalScore), rival = Mathf.RoundToInt(aiScorer.TotalScore);
            bool win = me > rival;
            var rw = RewardCalculator.ForBattle(win, me, rival, c.track, GameSession.Difficulty);
            Result = new RunResult
            {
                mode = GameMode.DriftBattle, trackId = c.track.id, win = win, score = me, rivalScore = rival, medal = rw.medal, coins = rw.coins, xp = rw.xp,
                time = elapsed, title = GameCatalog.TrackName(c.track), newRecord = me > c.profile.Data.GetBest(c.track.id),
                detail = Loc.T("battle.rival") + " (" + rivalName + "): " + Ui.Money(rival)
            };
            c.profile.RecordBest(c.track.id, me);
            if (win) { var q = Services.Get<QuestService>(); if (q != null) q.ReportBattleWin(); }
            Services.Get<AchievementService>().Check();
        }

        public override void Respawn() { c.player.Recover(); }
    }
}
