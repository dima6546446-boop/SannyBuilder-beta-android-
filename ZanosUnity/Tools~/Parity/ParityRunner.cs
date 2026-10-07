// Сверка C#-порта физики с браузерной версией: те же сценарии, те же входы, шаг 1/240 с.
// Сборка и запуск: ./run.sh  (нужен mono + mcs)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Zanos.Core;

static class ParityRunner
{
    delegate void Drive(double t, CarInput i);
    class Sc { public string Name, Car; public double V0, Secs; public Dictionary<string, int> Tuning; public Drive Fn; }

    static int Main(string[] args)
    {
        var sc = new List<Sc>
        {
            new Sc { Name = "accel-kopeyka", Car = "kopeyka", V0 = 0, Secs = 6, Fn = (t, i) => { i.Throttle = 1; } },
            new Sc { Name = "drift-ronin", Car = "ronin", V0 = 22, Secs = 7, Fn = (t, i) => { i.Steer = -0.5; i.Throttle = t < 0.3 ? 0 : 0.85; i.Handbrake = t > 0.35 && t < 0.6; } },
            new Sc { Name = "flick-kupe86", Car = "kupe86", V0 = 21, Secs = 6, Fn = (t, i) => { if (t < 0.3) { i.Throttle = 0.5; i.Steer = 0.9; } else if (t < 0.6) { i.Throttle = 1; i.Steer = -0.9; } else { i.Throttle = 1; i.Steer = -0.3; } i.Kick = t < 0.4; } },
            new Sc { Name = "brake-barin", Car = "barin", V0 = 30, Secs = 6, Fn = (t, i) => { i.Throttle = t < 1 ? 0.6 : 0; i.Brake = t >= 1 ? 1 : 0; i.Steer = t > 1.5 ? 0.2 : 0; } },
            new Sc { Name = "tuned-raketa", Car = "raketa", V0 = 25, Secs = 6, Tuning = new Dictionary<string, int> { { "engine", 3 }, { "tires", 2 }, { "diff", 3 }, { "weight", 2 } }, Fn = (t, i) => { i.Steer = t < 2 ? 0.4 : -0.45; i.Throttle = 0.9; i.Handbrake = t > 0.5 && t < 0.7; } },
        };
        var expected = new Dictionary<string, List<double[]>>();
        foreach (var line in File.ReadAllLines(args.Length > 0 ? args[0] : "../expected.csv"))
        {
            var p = line.Split(',');
            if (p[0] == "scenario" || p.Length < 12) continue;
            var v = new double[11];
            for (int k = 1; k < 12; k++) v[k - 1] = double.Parse(p[k], CultureInfo.InvariantCulture);
            if (!expected.ContainsKey(p[0])) expected[p[0]] = new List<double[]>();
            expected[p[0]].Add(v);
        }
        double dt = 1.0 / 240; int bad = 0;
        string[] names = { "t", "x", "z", "h", "vx", "vz", "w", "rpm", "gear", "steer", "slip2" };
        foreach (var s in sc)
        {
            var car = new Car(CarSpec.Build(CarCatalog.ById(s.Car), s.Tuning));
            if (s.V0 > 0)
            {
                car.H = 0; car.Vx = 0; car.Vz = s.V0;
                for (int i = 0; i < 4; i++) car.WheelW[i] = s.V0 / car.Spec.WheelRadius;
                car.Gear = s.V0 > 22 ? 3 : (s.V0 > 12 ? 2 : 1);
            }
            double maxErr = 0; string worst = ""; int idx = 0;
            for (int k = 0; k < (int)(s.Secs * 240); k++)
            {
                double t = k * dt; var inp = car.Input;
                inp.Throttle = 0; inp.Brake = 0; inp.Steer = 0; inp.Handbrake = false; inp.Kick = false;
                s.Fn(t, inp);
                car.Step(dt, null);
                if (k % 60 == 59)
                {
                    var e = expected[s.Name][idx++];
                    double[] got = { (k + 1) * dt, car.X, car.Z, car.H, car.Vx, car.Vz, car.W, car.Rpm, car.Gear, car.SteerAngle, car.Slip[2] };
                    for (int j = 1; j < got.Length; j++)
                    {
                        double err = Math.Abs(got[j] - e[j]) / Math.Max(1.0, Math.Abs(e[j]));
                        if (err > maxErr) { maxErr = err; worst = names[j] + "@" + got[0].ToString("0.00", CultureInfo.InvariantCulture); }
                    }
                }
            }
            bool ok = maxErr < 1e-6;
            if (!ok) bad++;
            Console.WriteLine((ok ? "  OK   " : "  FAIL ") + s.Name.PadRight(16) + " макс. отн. расхождение " + maxErr.ToString("0.0e+00", CultureInfo.InvariantCulture) + " (" + worst + ")");
        }
        Console.WriteLine(bad == 0 ? "Порт идентичен браузерной физике." : "РАСХОЖДЕНИЯ: " + bad);
        return bad == 0 ? 0 : 1;
    }
}
