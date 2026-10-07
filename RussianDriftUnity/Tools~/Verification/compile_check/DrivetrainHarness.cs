using System;
using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Vehicle;
using RussianDrift.World;

class P
{
    static void Main()
    {
        // ---- Drivetrain: full-throttle pull from standstill ----
        var d = new Drivetrain();
        d.idleRpm = 900; d.redline = 6600; d.peakTorque = 185; d.peakRpm = 4300; d.finalDrive = 4.1f; d.wheelRadius = 0.3f;
        d.gears = new[] { 3.5f, 2.1f, 1.4f, 1.05f, 0.82f };
        d.automatic = true; d.turboBoost = 0f;
        float v = 0f, dt = 1f / 60f, t = 0f; int lastGear = 1; float mass = 1020f;
        Console.WriteLine("t   speed(kmh) gear rpm");
        while (t < 30f)
        {
            float f = d.Step(dt, 1f, v, v, false, false, false, false);
            float drag = 0.38f * v * v;
            float a = (f - drag - 120f) / mass;
            v = Mathf.Max(0f, v + a * dt);
            t += dt;
            if (d.gear != lastGear || ((int)(t * 60)) % 300 == 0) { Console.WriteLine("{0:0.0} {1:0} {2} {3:0}", t, v * 3.6f, d.gear, d.rpm); lastGear = d.gear; }
        }
        Console.WriteLine("final speed {0:0} km/h gear {1} rpm {2:0}", v * 3.6f, d.gear, d.rpm);

        // ---- TrackPath ----
        var ctrl = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(50, 0, 0), new Vector3(80, 0, 40), new Vector3(30, 0, 80), new Vector3(-30, 0, 40) };
        var path = TrackPath.FromControl("t", ctrl, true, 4f, 10f);
        Console.WriteLine("path pts {0} length {1:0.0}", path.Count, path.length);
        var prog = new PathProgress(path);
        prog.ResetAt(path.Sample(path.length - 10f), true);
        float dist = path.length - 10f;
        for (int i = 0; i < 200; i++) { dist += 1.5f; Vector3 tg; prog.Update(path.Sample(dist, out tg)); }
        Console.WriteLine("progress laps {0} total {1:0.0} (expected {2:0.0})", prog.laps, prog.total, dist - path.length);

    }
}
