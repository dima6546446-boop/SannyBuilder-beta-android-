// Тесты ядра на чистом C# (без Unity): мир, очки, сессия, прогресс. Запуск: ./run-core-tests.sh
using System;
using System.Collections.Generic;
using Zanos.Core;

static class CoreTests
{
    static int fails, total;
    static void Check(string name, bool ok, string info = "") { total++; if (!ok) fails++; Console.WriteLine((ok ? "  OK   " : "  FAIL ") + name + (info != "" ? "  " + info : "")); }

    static MapData Flat(bool wall)
    {
        var m = new MapData { id = "t", name = "t", ground = "asphalt", spawn = new SpawnData { x = 0, z = 0, h = 0 }, bounds = new BoundsData { x0 = -200, z0 = -200, x1 = 200, z1 = 200 },
            segments = wall ? new[] { new SegData { a = -50, b = 60, c = 50, d = 60 } } : new SegData[0], boxes = new BoxData[0], circles = new CircleData[0], props = new PropData[0],
            surfaces = new SurfaceData[0], gates = new GateData[0], challenges = new ChallengeData[] { new ChallengeData { id = "c1", type = "score", name = "t", target = 100, time = 30, money = 1000, xp = 100 } }, timedTime = 10, timedGoal = 1000 };
        return m;
    }

    static int Main()
    {
        // разгон и устойчивость
        foreach (var def in CarCatalog.All)
        {
            var car = new Car(CarSpec.Build(def)); double t100 = -1; double dt = 1 / 240.0;
            for (int i = 0; i < 240 * 14; i++) { car.Input.Throttle = 1; car.Step(dt, null); if (t100 < 0 && car.Speed * 3.6 >= 100) t100 = i * dt; }
            Check("разгон до 100 км/ч: " + def.Id, t100 > 2.5 && t100 < 12.5, t100.ToString("0.0") + " с");
        }
        // занос после ручника удерживается
        {
            var car = new Car(CarSpec.Build(CarCatalog.ById("kopeyka"))); car.Vz = 22; for (int i = 0; i < 4; i++) car.WheelW[i] = 22 / car.Spec.WheelRadius; car.Gear = 2;
            double dt = 1 / 240.0, best = 0, cur = 0, peak = 0;
            for (int i = 0; i < 240 * 8; i++)
            {
                double t = i * dt; var inp = car.Input; inp.Steer = -0.5; inp.Throttle = t < 0.3 ? 0 : 0.85; inp.Handbrake = t > 0.35 && t < 0.6;
                car.Step(dt, null);
                double b = Math.Abs(car.DriftAngle) * 57.2958; peak = Math.Max(peak, b);
                if (b >= 20) { cur += dt; best = Math.Max(best, cur); } else cur = 0;
            }
            Check("ручник вводит в занос (угол > 20°)", peak > 20, peak.ToString("0") + "°");
            Check("занос держится ≥ 2 с подряд", best >= 2, best.ToString("0.0") + " с");
        }
        // очки
        {
            var sc = new Scoring(); var car = new Car(CarSpec.Build(CarCatalog.ById("kopeyka")));
            Action<double, double, int> run = (kmh, ang, n) => { double v = kmh / 3.6, b = ang * Math.PI / 180; car.Speed = v; car.FwdSpeed = v * Math.Cos(b); car.LatSpeed = v * Math.Sin(b); for (int i = 0; i < n; i++) sc.Update(0.01, car); };
            run(80, 40, 300); Check("серия растёт, но ещё не засчитана", sc.Chain > 0 && sc.Banked == 0);
            run(80, 0, 200); Check("серия засчитана после выхода из заноса", sc.Banked > 100 && sc.Chain == 0);
            double banked = sc.Banked; run(80, 40, 300); sc.Hit(8);
            Check("удар сжигает серию, засчитанное остаётся", sc.Chain == 0 && sc.Mult == 1 && sc.Banked == banked && sc.Stats.Hits == 1);
            var s1 = new Scoring(); var s2 = new Scoring();
            Action<Scoring, double, double> r2 = (s, kmh, ang) => { double v = kmh / 3.6, b = ang * Math.PI / 180; car.Speed = v; car.FwdSpeed = v * Math.Cos(b); car.LatSpeed = v * Math.Sin(b); for (int i = 0; i < 300; i++) s.Update(0.01, car); };
            r2(s1, 80, 40); r2(s2, 80, 20); Check("больше угол — больше очков", s1.Total > s2.Total);
        }
        // мир: стена, конус
        {
            var s = new Session(Flat(true), new SessionOptions { CarId = "kopeyka" });
            bool impact = false; s.OnEvent = (t, o) => { if (t == "impact") impact = true; };
            s.Car.Reset(0, 40, 0); s.Car.Vz = 25;
            var inp = new CarInput { Throttle = 1 };
            for (int i = 0; i < 60 * 3; i++) s.Update(1 / 60.0, inp);
            Check("машина не проходит сквозь стену", s.Car.Z < 60, "z=" + s.Car.Z.ToString("0.0"));
            Check("удар зафиксирован, серия учтена", impact && s.Scoring.Stats.Hits >= 1);
        }
        {
            var m = Flat(false); m.props = new[] { new PropData { type = "cone", x = 10, z = 0, r = 0.22, m = 3 } };
            var s = new Session(m, new SessionOptions { CarId = "kopeyka" }); s.Car.Reset(0, 0, Math.PI / 2); s.Car.Vx = 15;
            for (int i = 0; i < 60; i++) s.Update(1 / 60.0, new CarInput { Throttle = 0.5 });
            var p = s.World.Props[0];
            Check("конус сбит и отлетел", Math.Sqrt((p.X - p.X0) * (p.X - p.X0) + (p.Z - p.Z0) * (p.Z - p.Z0)) > 0.5);
        }
        // покрытия и погода
        {
            var m = Flat(false); m.surfaces = new[] { new SurfaceData { kind = "circle", type = "gravel", x = 50, z = 50, r = 10 } };
            var sr = new Session(m, new SessionOptions { Weather = "rain" }); var sc = new Session(m, new SessionOptions { Weather = "clear" });
            Check("дождь делает асфальт мокрым", sr.SurfaceAt(0, 0) == Surface.Wet && sc.SurfaceAt(0, 0) == Surface.Asphalt);
            Check("гравий по областям карты", sc.SurfaceAt(50, 50) == Surface.Gravel);
        }
        // режимы
        {
            var s = new Session(Flat(false), new SessionOptions { Mode = GameMode.Timed, CarId = "kopeyka" });
            for (int i = 0; i < 60 * 12 && !s.Finished; i++) s.Update(1 / 60.0, new CarInput());
            Check("режим на время завершается по таймеру", s.Finished && s.Summary().Duration >= 9.9);
            var c = new Session(Flat(false), new SessionOptions { Mode = GameMode.Challenge, ChallengeId = "c1", CarId = "kopeyka" });
            c.Scoring.Banked = 150; c.Step(1 / 240.0);
            Check("испытание «очки» выполняется", c.Finished && c.Success);
        }
        // прогресс
        {
            var p = new Progress();
            Check("покупка требует уровень", p.Buy("pyaterka") != null && p.Data.cars.Count == 1);
            p.Data.xp += Progress.XpForLevel(1) + 1; p.Data.level = Progress.LevelFromXp(p.Data.xp); p.Data.money = 20000;
            Check("после повышения уровня покупка проходит", p.Buy("pyaterka") == null && p.Data.money == 6000 && p.Data.selected == "pyaterka");
            for (int i = 0; i < 5; i++) p.Data.money += 100000;
            for (int i = 0; i < 5; i++) p.Upgrade("pyaterka", "engine");
            Check("тюнинг ограничен 5 уровнями", p.Upgrade("pyaterka", "engine") == "Максимум" && p.Car("pyaterka").Tune("engine") == 5);
            int val; int before = p.Data.money; Check("продажа возвращает 60% с тюнингом", p.Sell("pyaterka", out val) == null && p.Data.money == before + val && val > 8400);
            int v2; Check("последнюю машину продать нельзя", p.Sell("kopeyka", out v2) != null);
            var sum = new RunSummary { Mode = GameMode.Challenge, ChallengeId = "c1", MapId = "t", Score = 3500, Success = true, RewardMoney = 1000, RewardXp = 100 };
            var a = p.FinishRun(sum); var b = p.FinishRun(sum);
            Check("награда за испытание: полная, затем 25%", a.ChallengeMoney == 1000 && b.ChallengeMoney == 250);
        }
        Console.WriteLine(fails == 0 ? "\nВсе проверки ядра пройдены (" + total + ")" : "\nПРОВАЛЕНО: " + fails + " из " + total);
        return fails == 0 ? 0 : 1;
    }
}
