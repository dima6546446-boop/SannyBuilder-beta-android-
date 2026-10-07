using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Generates the whole open world procedurally (deterministic for a given seed) and returns its WorldInfo.</summary>
    public static class WorldBuilder
    {
        public static WorldInfo Build(Transform parent, int seed)
        {
            WorldInfo result = null;
            var it = BuildAsync(parent, seed, null, w => result = w);
            while (it.MoveNext()) { }
            return result;
        }

        /// <summary>Same as Build but yields between the heavy stages so a loading screen can animate.</summary>
        public static System.Collections.IEnumerator BuildAsync(Transform parent, int seed, System.Action<float> progress, System.Action<WorldInfo> done)
        {
            var rootGo = new GameObject("World");
            rootGo.transform.SetParent(parent, false);
            var info = new WorldInfo();
            info.root = rootGo.transform;
            info.mats = WorldMaterials.Create();
            info.graph = new RoadGraph();
            var rng = new System.Random(seed);
            if (progress != null) progress(0.05f);
            yield return null;

            CityBuilder.Build(info, rootGo.transform, rng);
            if (progress != null) progress(0.4f);
            yield return null;
            IndustrialBuilder.Build(info, rootGo.transform, rng);
            if (progress != null) progress(0.6f);
            yield return null;
            SerpentineBuilder.Build(info, rootGo.transform, rng);
            if (progress != null) progress(0.85f);
            yield return null;
            BuildCityRing(info);
            BuildPuddleSpots(info, rng);

            info.freeRoamSpawn = new Vector3(-43.5f, 0.55f, -110f);
            info.freeRoamRot = Quaternion.identity;
            info.bounds = new Bounds(new Vector3(200f, 0f, 200f), new Vector3(1000f, 300f, 1000f));

            var tl = rootGo.AddComponent<TrafficLightController>();
            tl.mats = info.mats;
            WorldInfo.Current = info;
            if (progress != null) progress(1f);
            if (done != null) done(info);
        }

        private static void BuildCityRing(WorldInfo info)
        {
            float R = Mathf.Abs(RoadGraph.CoordOf(1)) + RoadGraph.LaneOffset;   // 144.5, outer lane of the inner ring
            float r = 32f;
            float k = 0.293f * r;
            var c = new List<Vector3>();
            Vector3 V(float x, float z) { return new Vector3(x, 0.05f, z); }
            // counter-clockwise loop, start on the south side heading east
            c.Add(V(-R + r, -R)); c.Add(V(0, -R)); c.Add(V(R - r, -R));
            c.Add(V(R - k, -R + k)); c.Add(V(R, -R + r));
            c.Add(V(R, 0)); c.Add(V(R, R - r));
            c.Add(V(R - k, R - k)); c.Add(V(R - r, R));
            c.Add(V(0, R)); c.Add(V(-R + r, R));
            c.Add(V(-R + k, R - k)); c.Add(V(-R, R - r));
            c.Add(V(-R, 0)); c.Add(V(-R, -R + r));
            c.Add(V(-R + k, -R + k));
            info.cityRing = TrackPath.FromControl("cityring", c, true, 4f, 11f);

            float zr = 56f;
            info.cityZones.Add(new DriftZoneDef { center = new Vector3(R - 18f, 1f, -R + 18f), size = new Vector3(zr, 6f, zr), rot = Quaternion.identity, multiplier = 1.6f });
            info.cityZones.Add(new DriftZoneDef { center = new Vector3(R - 18f, 1f, R - 18f), size = new Vector3(zr, 6f, zr), rot = Quaternion.identity, multiplier = 1.6f });
            info.cityZones.Add(new DriftZoneDef { center = new Vector3(-R + 18f, 1f, R - 18f), size = new Vector3(zr, 6f, zr), rot = Quaternion.identity, multiplier = 1.6f });
            info.cityZones.Add(new DriftZoneDef { center = new Vector3(-R + 18f, 1f, -R + 18f), size = new Vector3(zr, 6f, zr), rot = Quaternion.identity, multiplier = 1.6f });
            float so = R + 12f;
            info.citySpectators.Add(new Vector3(so, 0.15f, -so)); info.citySpectators.Add(new Vector3(so, 0.15f, so));
            info.citySpectators.Add(new Vector3(-so, 0.15f, so)); info.citySpectators.Add(new Vector3(-so, 0.15f, -so));
            info.citySpectators.Add(new Vector3(so + 3f, 0.15f, -so + 3f)); info.citySpectators.Add(new Vector3(-so - 3f, 0.15f, so - 3f));
        }

        private static void BuildPuddleSpots(WorldInfo info, System.Random rng)
        {
            for (int i = 0; i < 40; i++)
            {
                float line = RoadGraph.CoordOf(rng.Next(RoadGraph.N));
                float along = ((float)rng.NextDouble() * 2f - 1f) * 220f;
                bool vertical = rng.Next(2) == 0;
                float off = ((float)rng.NextDouble() * 2f - 1f) * 4f;
                // avoid junction centres
                Vector3 p = vertical ? new Vector3(line + off, 0.04f, along) : new Vector3(along, 0.04f, line + off);
                info.puddleSpots.Add(p);
            }
            // industrial apron
            var path = info.industrialLoop;
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = path.Point(rng.Next(path.Count));
                Vector3 t = path.Tangent(path.Nearest(p));
                p += Vector3.Cross(Vector3.up, t) * (((float)rng.NextDouble() * 2f - 1f) * 5f);
                p.y = 0.04f;
                info.puddleSpots.Add(p);
            }
        }
    }
}
