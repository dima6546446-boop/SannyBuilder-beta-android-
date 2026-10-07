using System;
using System.Collections.Generic;

namespace Zanos.Core
{
    public sealed class ScoreEvent { public string Type; public double Points, Mult, Time; }

    public sealed class ScoreStats { public double BestChain, BestAngle, DriftTime, LostPoints, LongestDrift, TopSpeed, AngleSum; public int Hits, Chains, Transitions; }

    /// <summary>Очки дрифта: угол × скорость × время, множитель растёт с серией. Порт rcd/src/game/scoring.js.</summary>
    public sealed class Scoring
    {
        public const double MinAngleDeg = 10, MaxAngleDeg = 115, MinSpeedKmh = 28, GraceTime = 0.9, MultStepTime = 2.4, MultStep = 0.5, MultMax = 8, TransitionBonus = 0.75, RateK = 0.5, HitMinSpeed = 1.4;

        public double Banked, Chain, Mult, ChainTime, Gap, Angle, SignedAngle, Rate, HoldAngleTime;
        public bool Active; public ScoreStats Stats; public readonly List<ScoreEvent> Events = new List<ScoreEvent>();
        double lastSign, cur;

        public Scoring() { Reset(); }

        public void Reset()
        {
            Banked = 0; Chain = 0; Mult = 1; ChainTime = 0; Gap = 0; Active = false; Angle = 0; SignedAngle = 0; lastSign = 0; HoldAngleTime = 0; Rate = 0; cur = 0;
            Stats = new ScoreStats(); Events.Clear();
        }

        void Emit(string type, double points = 0, double mult = 0, double time = 0) { Events.Add(new ScoreEvent { Type = type, Points = points, Mult = mult, Time = time }); }

        public void Update(double dt, Car car)
        {
            double kmh = car.Speed * 3.6;
            Stats.TopSpeed = Math.Max(Stats.TopSpeed, kmh);
            double beta = car.FwdSpeed > 1 ? Math.Atan2(car.LatSpeed, car.FwdSpeed) : 0;
            double a = Math.Abs(beta) * 180 / Math.PI;
            SignedAngle = beta * 180 / Math.PI; Angle = a;
            bool driftNow = kmh >= MinSpeedKmh && a >= MinAngleDeg && a <= MaxAngleDeg && car.FwdSpeed > 0.5;
            if (driftNow)
            {
                Active = true; Gap = 0; ChainTime += dt; cur += dt;
                Stats.DriftTime += dt; Stats.AngleSum += a * dt; Stats.BestAngle = Math.Max(Stats.BestAngle, a); Stats.LongestDrift = Math.Max(Stats.LongestDrift, cur);
                HoldAngleTime += dt;
                double sign = MathUtil.Sign(beta);
                if (lastSign != 0 && sign != lastSign && a > MinAngleDeg) { Mult = Math.Min(MultMax, Mult + TransitionBonus); Stats.Transitions++; Emit("transition", 0, Mult); }
                lastSign = sign;
                int steps = (int)Math.Floor(ChainTime / MultStepTime);
                double baseM = 1 + steps * MultStep;
                if (baseM > Mult) { Mult = Math.Min(MultMax, baseM); Emit("mult", 0, Mult); }
                Rate = Math.Pow(a, 1.1) / 30 * kmh * RateK;
                Chain += Rate * Mult * dt;
                Stats.BestChain = Math.Max(Stats.BestChain, Chain);
            }
            else
            {
                HoldAngleTime = 0; Rate = 0; cur = 0;
                if (Chain > 0 || ChainTime > 0) { Gap += dt; if (Gap >= GraceTime || kmh < 6) Bank(); }
                Active = false;
            }
        }

        public void Bank()
        {
            if (Chain > 0)
            {
                double pts = Math.Round(Chain);
                Banked += pts; Stats.Chains++; Emit("bank", pts, Mult, ChainTime);
            }
            Chain = 0; Mult = 1; ChainTime = 0; Gap = 0; lastSign = 0;
        }

        /// <summary>Удар: серия сгорает.</summary>
        public void Hit(double speed)
        {
            if (speed < HitMinSpeed) return;
            Stats.Hits++;
            if (Chain > 0) { Stats.LostPoints += Chain; Emit("lost", Math.Round(Chain)); }
            Chain = 0; Mult = 1; ChainTime = 0; Gap = 0; lastSign = 0; Active = false;
        }

        public long Total { get { return (long)Math.Round(Banked + Chain); } }
    }
}
