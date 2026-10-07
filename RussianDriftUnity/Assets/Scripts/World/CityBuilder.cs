using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>City grid: ground, roads + markings, 25 themed blocks, street lamps, traffic lights.</summary>
    public static class CityBuilder
    {
        private static readonly string[] layout =
        {
            "RGRMG",   // j = 0 (south)
            "SRGPR",
            "RMXMR",
            "GRMRP",
            "RPRGS",   // j = 4 (north)
        };

        public static void Build(WorldInfo info, Transform parent, System.Random rng)
        {
            var m = info.mats;
            const float P = RoadGraph.Pitch, RW = RoadGraph.RoadWidth;
            float ext = RoadGraph.CoordOf(RoadGraph.N - 1) + RW * 0.5f;   // 242

            // ---------- ground ----------
            {
                var cb = new ChunkBuilder();
                cb.For(m.grass).AddFace(new Vector3(0, -0.01f, 0), Vector3.up, Vector3.right, 2600f, 2600f, new Vector2(8f, 8f));
                cb.Collider(new Vector3(0, -2f, 0), new Vector3(2600f, 4f, 2600f), Quaternion.identity, SurfaceType.Grass, 0.62f);
                var go = cb.Build("Ground", parent, false);
                info.chunks.Add(go.transform);
            }

            // ---------- roads ----------
            {
                var cb = new ChunkBuilder();
                var asphalt = cb.For(m.asphalt);
                float len = ext * 2f;
                for (int k = 0; k < RoadGraph.N; k++)
                {
                    float c = RoadGraph.CoordOf(k);
                    asphalt.AddFace(new Vector3(c, 0.04f, 0), Vector3.up, Vector3.right, RW, len, new Vector2(6f, 6f));
                    asphalt.AddFace(new Vector3(0, 0.041f, c), Vector3.up, Vector3.right, len, RW, new Vector2(6f, 6f));
                    cb.Collider(new Vector3(c, -0.11f, 0), new Vector3(RW, 0.3f, len), Quaternion.identity, SurfaceType.Asphalt, 1f);
                    cb.Collider(new Vector3(0, -0.109f, c), new Vector3(len, 0.3f, RW), Quaternion.identity, SurfaceType.Asphalt, 1f);
                }
                // markings: dashed centre lines, crosswalks
                var white = cb.For(m.white);
                for (int k = 0; k < RoadGraph.N; k++)
                {
                    float c = RoadGraph.CoordOf(k);
                    for (int s = 0; s < RoadGraph.N - 1; s++)
                    {
                        float a = RoadGraph.CoordOf(s) + RW * 0.5f + 3f, b = RoadGraph.CoordOf(s + 1) - RW * 0.5f - 3f;
                        for (float t = a; t < b - 3f; t += 9f)
                        {
                            white.AddFace(new Vector3(c, 0.055f, t + 1.5f), Vector3.up, Vector3.right, 0.16f, 3f, Vector2.zero);
                            white.AddFace(new Vector3(t + 1.5f, 0.056f, c), Vector3.up, Vector3.right, 3f, 0.16f, Vector2.zero);
                        }
                    }
                }
                for (int i = 0; i < RoadGraph.N; i++)
                    for (int j = 0; j < RoadGraph.N; j++)
                    {
                        float x = RoadGraph.CoordOf(i), z = RoadGraph.CoordOf(j);
                        if (j > 0) PropBuilder.Crosswalk(cb, m, new Vector3(x, 0, z - 10.5f), 0f, 11f);
                        if (j < RoadGraph.N - 1) PropBuilder.Crosswalk(cb, m, new Vector3(x, 0, z + 10.5f), 0f, 11f);
                        if (i > 0) PropBuilder.Crosswalk(cb, m, new Vector3(x - 10.5f, 0, z), 90f, 11f);
                        if (i < RoadGraph.N - 1) PropBuilder.Crosswalk(cb, m, new Vector3(x + 10.5f, 0, z), 90f, 11f);
                    }
                var go = cb.Build("Roads", parent, false);
                info.chunks.Add(go.transform);
            }

            // ---------- blocks ----------
            for (int bi = 0; bi < 5; bi++)
                for (int bj = 0; bj < 5; bj++)
                {
                    float cx = (RoadGraph.CoordOf(bi) + RoadGraph.CoordOf(bi + 1)) * 0.5f;
                    float cz = (RoadGraph.CoordOf(bj) + RoadGraph.CoordOf(bj + 1)) * 0.5f;
                    char t = layout[bj][bi];
                    var r = new System.Random(rng.Next() ^ (bi * 733 + bj * 91));
                    BuildBlock(info, parent, t, new Vector2(cx, cz), r, bi, bj);
                }

            // ---------- lamps + traffic lights (own chunk, kept always) ----------
            {
                var cb = new ChunkBuilder();
                var glowBuilder = new ChunkBuilder();
                for (int k = 0; k < RoadGraph.N; k++)
                {
                    float c = RoadGraph.CoordOf(k);
                    for (int s = 0; s < RoadGraph.N - 1; s++)
                    {
                        float a = RoadGraph.CoordOf(s) + 18f, b = RoadGraph.CoordOf(s + 1) - 18f;
                        int count = 0;
                        for (float t = a; t <= b; t += 29f, count++)
                        {
                            bool side = (count & 1) == 0;
                            // vertical road at x=c
                            PropBuilder.Lamp(cb, m, new Vector3(c + (side ? 7.6f : -7.6f), 0.15f, t), side ? -90f : 90f, info.lamps);
                            // horizontal road at z=c
                            PropBuilder.Lamp(cb, m, new Vector3(t, 0.15f, c + (side ? 7.6f : -7.6f)), side ? 180f : 0f, info.lamps);
                        }
                    }
                }
                for (int i = 1; i < RoadGraph.N - 1; i++)
                    for (int j = 1; j < RoadGraph.N - 1; j++)
                    {
                        float x = RoadGraph.CoordOf(i), z = RoadGraph.CoordOf(j);
                        bool a = ((i + j) & 1) == 0;
                        PropBuilder.TrafficLightPole(cb, m, new Vector3(x - 8f, 0.15f, z + 9f), 0f, true, a);
                        PropBuilder.TrafficLightPole(cb, m, new Vector3(x + 8f, 0.15f, z - 9f), 180f, true, a);
                        PropBuilder.TrafficLightPole(cb, m, new Vector3(x - 9f, 0.15f, z - 8f), -90f, false, a);
                        PropBuilder.TrafficLightPole(cb, m, new Vector3(x + 9f, 0.15f, z + 8f), 90f, false, a);
                    }
                for (int i = 0; i < info.lamps.Count; i += 2) PropBuilder.LampGlowQuad(cb, m, info.lamps[i]);
                var go = cb.Build("StreetFurniture", parent, false);
                info.chunks.Add(go.transform);
            }
        }

        private static void BuildBlock(WorldInfo info, Transform parent, char type, Vector2 c, System.Random r, int bi, int bj)
        {
            var m = info.mats;
            var cb = new ChunkBuilder();
            const float S = 80f;
            Material slabMat = m.sidewalk; SurfaceType slabType = SurfaceType.Concrete; float slabGrip = 1f;
            if (type == 'P') { slabMat = m.grass; slabType = SurfaceType.Grass; slabGrip = 0.6f; }
            if (type == 'X' || type == 'S') { slabMat = m.asphalt; slabType = SurfaceType.Asphalt; slabGrip = 1f; }
            cb.For(slabMat).AddFace(new Vector3(c.x, PropBuilder.Slab, c.y), Vector3.up, Vector3.right, S, S, new Vector2(slabMat == m.grass ? 8f : 5f, slabMat == m.grass ? 8f : 5f));
            cb.Box(m.sidewalk, new Vector3(c.x, PropBuilder.Slab * 0.5f, c.y), new Vector3(S, PropBuilder.Slab, S), Quaternion.identity, new Vector2(5f, 5f), false, false);
            cb.Collider(new Vector3(c.x, PropBuilder.Slab * 0.5f - 0.2f, c.y), new Vector3(S, PropBuilder.Slab + 0.4f, S), Quaternion.identity, slabType, slabGrip);

            float y0 = PropBuilder.Slab;
            switch (type)
            {
                case 'R':
                    {
                        int v = r.Next(4);
                        // four panel slabs around a courtyard + one tower
                        PropBuilder.PanelHouse(cb, m, new Vector2(c.x, c.y + 31f), 64f, 12f, r.Next(0, 2) == 0 ? 5 : 9, 0f, v);
                        PropBuilder.PanelHouse(cb, m, new Vector2(c.x, c.y - 31f), 64f, 12f, r.Next(0, 2) == 0 ? 5 : 9, 0f, v + 1);
                        PropBuilder.PanelHouse(cb, m, new Vector2(c.x - 33f, c.y), 12f, 40f, 9, 0f, v + 2);
                        PropBuilder.Tower(cb, m, new Vector2(c.x + 28f, c.y + 2f), 14f, 16, 0f, v + 3);
                        for (int k = 0; k < 14; k++)
                            PropBuilder.Tree(cb, m, new Vector3(c.x - 18f + (float)r.NextDouble() * 36f, y0, c.y - 18f + (float)r.NextDouble() * 36f), 0.9f + (float)r.NextDouble() * 0.5f, r);
                        // playground
                        cb.Box(m.dirt, new Vector3(c.x - 5f, y0 + 0.01f, c.y + 6f), new Vector3(12f, 0.02f, 9f), Quaternion.identity, Vector2.zero, true, false);
                        cb.Box(m.curbRed, new Vector3(c.x - 3f, y0 + 1.0f, c.y + 6f), new Vector3(3f, 0.15f, 0.15f), Quaternion.identity, Vector2.zero, true, false);
                        cb.Box(m.signYellow, new Vector3(c.x - 3f, y0 + 0.5f, c.y + 6f), new Vector3(0.12f, 1f, 0.12f), Quaternion.identity, Vector2.zero, true, false);
                        info.citySpectators.Add(new Vector3(c.x + 40f - 3f, y0, c.y - 38f));
                        break;
                    }
                case 'G':
                    {
                        // cooperative: 3 rows of garages facing each other
                        for (int row = 0; row < 3; row++)
                        {
                            float z = c.y - 26f + row * 26f;
                            PropBuilder.Garages(cb, m, new Vector2(c.x, z), 18, row % 2 == 0 ? 180f : 0f, r);
                        }
                        cb.Box(m.dirt, new Vector3(c.x, y0 + 0.01f, c.y), new Vector3(S - 6f, 0.02f, S - 6f), Quaternion.identity, Vector2.zero, true, false);
                        for (int k = 0; k < 6; k++)
                            PropBuilder.Tree(cb, m, new Vector3(c.x + (r.Next(2) == 0 ? -37f : 37f), y0, c.y - 30f + k * 12f), 1f, r);
                        break;
                    }
                case 'S':
                    PropBuilder.GasStation(cb, m, new Vector2(c.x - 4f, c.y + 12f), 0f);
                    for (int k = 0; k < 3; k++) PropBuilder.Tree(cb, m, new Vector3(c.x - 34f + k * 5f, y0, c.y - 34f), 1f, r);
                    PropBuilder.ParkingLines(cb, m, new Vector2(c.x, c.y - 24f), 40f, 20f, 0f, y0);
                    break;
                case 'P':
                    for (int k = 0; k < 46; k++)
                    {
                        PropBuilder.Tree(cb, m, new Vector3(c.x - 36f + (float)r.NextDouble() * 72f, y0, c.y - 36f + (float)r.NextDouble() * 72f), 1f + (float)r.NextDouble() * 0.6f, r);
                    }
                    // paths + fountain
                    cb.Box(m.sidewalk, new Vector3(c.x, y0 + 0.02f, c.y), new Vector3(3f, 0.04f, S - 4f), Quaternion.identity, new Vector2(3f, 3f), true, false);
                    cb.Box(m.sidewalk, new Vector3(c.x, y0 + 0.02f, c.y), new Vector3(S - 4f, 0.04f, 3f), Quaternion.identity, new Vector2(3f, 3f), true, false);
                    cb.For(m.concrete).AddCylinder(new Vector3(c.x, y0 + 0.4f, c.y), 5f, 0.8f, Quaternion.identity, 20, true);
                    cb.Collider(new Vector3(c.x, y0 + 0.4f, c.y), new Vector3(10f, 0.8f, 10f), Quaternion.identity);
                    info.citySpectators.Add(new Vector3(c.x + 6f, y0, c.y + 6f));
                    break;
                case 'M':
                    {
                        // market street: shops along the edges with neon signs
                        Material[] signs = { m.signBlue, m.signRed, m.signGreen, m.signYellow };
                        for (int k = 0; k < 4; k++)
                        {
                            PropBuilder.Shop(cb, m, new Vector2(c.x - 30f + k * 20f, c.y + 33f), 17f, 12f, 0f, k, signs[(k + bi + bj) & 3]);
                            PropBuilder.Shop(cb, m, new Vector2(c.x - 30f + k * 20f, c.y - 33f), 17f, 12f, 180f, k, signs[(k + bi) & 3]);
                        }
                        PropBuilder.Billboard(cb, m, new Vector3(c.x - 36f, y0, c.y), 90f, signs[(bi + bj) & 3]);
                        for (int k = 0; k < 10; k++)
                            PropBuilder.Tree(cb, m, new Vector3(c.x - 30f + k * 6.5f, y0, c.y + (k % 2 == 0 ? 8f : -8f)), 0.8f, r);
                        break;
                    }
                case 'X':
                    {
                        PropBuilder.ParkingLines(cb, m, new Vector2(c.x, c.y + 18f), 70f, 40f, 0f, y0);
                        PropBuilder.ParkingLines(cb, m, new Vector2(c.x, c.y - 18f), 70f, 40f, 0f, y0);
                        for (int k = 0; k < 14; k++)
                        {
                            float px = c.x - 33f + (k % 7) * 11f, pz = c.y + (k < 7 ? 20f : -20f);
                            var paint = m.containers[r.Next(m.containers.Length)];
                            if (r.Next(3) > 0) PropBuilder.LowPolyCar(cb, m, new Vector3(px + (float)r.NextDouble() * 0.3f, y0, pz), 90f + (r.Next(2) == 0 ? 0f : 180f), paint);
                        }
                        // the legendary plaza obelisk
                        cb.SolidBox(m.concrete, new Vector3(c.x, y0 + 7f, c.y), new Vector3(2.5f, 14f, 2.5f), Quaternion.identity, new Vector2(4f, 4f));
                        cb.Box(m.signRed, new Vector3(c.x, y0 + 14.4f, c.y), new Vector3(3f, 1.0f, 3f), Quaternion.identity, Vector2.zero, true, false);
                        info.citySpectators.Add(new Vector3(c.x + 25f, y0, c.y));
                        break;
                    }
            }
            var go = cb.Build("Block_" + bi + "_" + bj, parent);
            info.chunks.Add(go.transform);
        }
    }
}
