using System;
using System.Collections.Generic;

namespace Zanos.Core
{
    public enum GameMode { Free, Timed, Challenge }

    public sealed class SessionOptions
    {
        public string MapId = "parking", CarId = "kopeyka", Weather = "clear", ChallengeId;
        public GameMode Mode = GameMode.Free;
        public Dictionary<string, int> Tuning;
        public double Assist = 0.7; public bool AutoGear = true;
    }

    public sealed class RunSummary
    {
        public GameMode Mode; public string MapId, CarId, ChallengeId, ChallengeName; public bool Success; public long Score; public int Stars, Goal;
        public double BestChain, BestAngle, DriftTime, LongestDrift, LostPoints, TopSpeed, Distance, Duration, AvgAngle; public int Hits, Chains, Transitions;
        public int RewardMoney, RewardXp;
    }

    /// <summary>
    /// Сессия заезда: физика (фиксированный шаг) + мир + очки + правила режима. Не зависит от Unity — порт rcd/src/game/session.js.
    /// </summary>
    public sealed class Session
    {
        public readonly SessionOptions Opts; public readonly MapData Map; public readonly World World; public readonly CarSpec Spec;
        public readonly Car Car; public readonly Scoring Scoring = new Scoring();
        public ChallengeData Challenge;
        public double Time, TimeLimit, CamShake, HoldT; public bool Finished, Success; public int GateIndex;
        public Action<string, object> OnEvent;
        double accum, impactCooldown, prevX, prevZ, distance;

        public Session(MapData map, SessionOptions opts)
        {
            Opts = opts; Map = map; World = new World(map);
            Spec = CarSpec.Build(CarCatalog.ById(opts.CarId), opts.Tuning);
            Car = new Car(Spec) { Assist = opts.Assist, AutoGear = opts.AutoGear };
            if (opts.Mode == GameMode.Challenge) foreach (var c in map.challenges) if (c.id == opts.ChallengeId) Challenge = c;
            TimeLimit = opts.Mode == GameMode.Timed ? map.timedTime : (Challenge != null ? Challenge.time : 0);
            Restart();
        }

        void Emit(string type, object data = null) { if (OnEvent != null) OnEvent(type, data); }

        public Surface SurfaceAt(double x, double z) { return Surface.Weathered(World.SurfaceAt(x, z), Opts.Weather); }

        public void Restart()
        {
            var sp = Map.spawn;
            Car.Reset(sp.x, sp.z, sp.h); Car.Assist = Opts.Assist;
            World.ResetProps(); Scoring.Reset();
            Time = 0; Finished = false; Success = false; GateIndex = 0; accum = 0; distance = 0; HoldT = 0;
            prevX = Car.X; prevZ = Car.Z;
        }

        public void Respawn() { var sp = Map.spawn; Scoring.Hit(99); Car.Reset(sp.x, sp.z, sp.h); Car.Assist = Opts.Assist; Emit("respawn"); }

        void HandleImpact(ImpactEvent e)
        {
            if (e.Kind == "prop") { Emit("impact", e); return; }
            if (impactCooldown > 0) { impactCooldown = Math.Max(impactCooldown, 0.08); return; }
            impactCooldown = 0.12;
            CamShake = Math.Min(1, CamShake + e.Speed * 0.06);
            Scoring.Hit(e.Speed);
            Emit("impact", e);
        }

        /// <summary>Продвинуть симуляцию на frameDt секунд; ввод копируется в машину.</summary>
        public void Update(double frameDt, CarInput input)
        {
            if (Finished) return;
            double dt = 1.0 / Phys.StepHz;
            accum += Math.Min(frameDt, Phys.MaxFrameDt);
            if (input != null)
            {
                var i = Car.Input;
                i.Throttle = input.Throttle; i.Brake = input.Brake; i.Steer = input.Steer; i.Handbrake = input.Handbrake; i.Kick = input.Kick;
                i.ShiftUp |= input.ShiftUp; i.ShiftDown |= input.ShiftDown;
            }
            int steps = 0;
            while (accum >= dt && steps < 60) { accum -= dt; steps++; Step(dt); if (Finished) break; }
        }

        public void Step(double dt)
        {
            Car.Step(dt, SurfaceAt);
            impactCooldown = Math.Max(0, impactCooldown - dt);
            CamShake = Math.Max(0, CamShake - dt * 2.2);
            World.Collide(Car, dt, HandleImpact);
            World.StepProps(dt);
            var b = Map.bounds;
            if (b != null) { Car.X = Math.Max(b.x0 + 0.5, Math.Min(b.x1 - 0.5, Car.X)); Car.Z = Math.Max(b.z0 + 0.5, Math.Min(b.z1 - 0.5, Car.Z)); }
            Time += dt;
            Scoring.Update(dt, Car);
            foreach (var ev in Scoring.Events.ToArray()) Emit(ev.Type, ev);
            Scoring.Events.Clear();
            distance += Math.Sqrt((Car.X - prevX) * (Car.X - prevX) + (Car.Z - prevZ) * (Car.Z - prevZ));
            UpdateRules(dt);
            prevX = Car.X; prevZ = Car.Z;
        }

        void UpdateRules(double dt)
        {
            if (Opts.Mode == GameMode.Free) return;
            var sc = Scoring;
            if (Opts.Mode == GameMode.Timed) { if (Time >= TimeLimit) { sc.Bank(); Finish(true); } return; }
            var c = Challenge; if (c == null) return;
            if (Time >= c.time) { Finish(false); return; }
            switch (c.type)
            {
                case "score": if (sc.Banked + sc.Chain >= c.target) { sc.Bank(); Finish(true); } break;
                case "chain": if (sc.Chain >= c.target) { sc.Bank(); Finish(true); } break;
                case "noHit":
                    if (sc.Stats.Hits > 0) Finish(false);
                    else if (sc.Banked + sc.Chain >= c.target) { sc.Bank(); Finish(true); }
                    break;
                case "angle":
                    HoldT = sc.Active && sc.Angle >= c.target ? HoldT + dt : 0;
                    if (HoldT >= c.hold) Finish(true);
                    break;
                case "gates": UpdateGates(c); break;
            }
        }

        void UpdateGates(ChallengeData c)
        {
            if (GateIndex >= Map.gates.Length) { Finish(true); return; }
            var g = Map.gates[GateIndex];
            if (SegIntersect(prevX, prevZ, Car.X, Car.Z, g.x1, g.z1, g.x2, g.z2))
            {
                if (c.needDrift && !Scoring.Active) return;
                GateIndex++; Emit("gate", GateIndex);
                if (GateIndex >= Map.gates.Length) Finish(true);
            }
        }

        public void Finish(bool success)
        {
            if (Finished) return;
            Finished = true; Success = success; Scoring.Bank(); Emit("finish", success);
        }

        public RunSummary Summary()
        {
            var st = Scoring.Stats; long score = (long)Math.Round(Scoring.Banked);
            int stars = 0, goal = 0;
            if (Opts.Mode == GameMode.Timed) { goal = Map.timedGoal; stars = score >= goal * 1.5 ? 3 : score >= goal ? 2 : score >= goal * 0.5 ? 1 : 0; }
            return new RunSummary
            {
                Mode = Opts.Mode, MapId = Map.id, CarId = Spec.Id, ChallengeId = Challenge != null ? Challenge.id : null, ChallengeName = Challenge != null ? Challenge.name : null,
                Success = Opts.Mode == GameMode.Free ? true : Success, Score = score, Stars = stars, Goal = goal,
                BestChain = st.BestChain, BestAngle = st.BestAngle, DriftTime = st.DriftTime, LongestDrift = st.LongestDrift, LostPoints = Math.Round(st.LostPoints), TopSpeed = st.TopSpeed,
                Distance = distance, Duration = Time, Chains = st.Chains, Transitions = st.Transitions, Hits = st.Hits, AvgAngle = st.DriftTime > 0 ? st.AngleSum / st.DriftTime : 0,
                RewardMoney = Challenge != null ? Challenge.money : 0, RewardXp = Challenge != null ? Challenge.xp : 0,
            };
        }

        static bool SegIntersect(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double d1x = bx - ax, d1z = bz - az, d2x = dx - cx, d2z = dz - cz;
            double den = d1x * d2z - d1z * d2x;
            if (Math.Abs(den) < 1e-9) return false;
            double t = ((cx - ax) * d2z - (cz - az) * d2x) / den, u = ((cx - ax) * d1z - (cz - az) * d1x) / den;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }
    }
}
