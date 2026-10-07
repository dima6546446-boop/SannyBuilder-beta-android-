using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    public class RoadNode
    {
        public int i, j;
        public Vector3 pos;
        public readonly List<RoadNode> neighbors = new List<RoadNode>();
    }

    /// <summary>City road grid: 6x6 intersections spaced 94 m apart. Right-hand traffic with a lane offset.</summary>
    public class RoadGraph
    {
        public const int N = 6;
        public const float Pitch = 94f;
        public const float RoadWidth = 14f;
        public const float LaneOffset = 3.5f;
        public readonly RoadNode[,] nodes = new RoadNode[N, N];

        public static float CoordOf(int k) { return (k - 2.5f) * Pitch; }

        public RoadGraph()
        {
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    nodes[i, j] = new RoadNode { i = i, j = j, pos = new Vector3(CoordOf(i), 0f, CoordOf(j)) };
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    var n = nodes[i, j];
                    if (i > 0) n.neighbors.Add(nodes[i - 1, j]);
                    if (i < N - 1) n.neighbors.Add(nodes[i + 1, j]);
                    if (j > 0) n.neighbors.Add(nodes[i, j - 1]);
                    if (j < N - 1) n.neighbors.Add(nodes[i, j + 1]);
                }
        }

        public RoadNode Nearest(Vector3 p)
        {
            int bi = Mathf.Clamp(Mathf.RoundToInt(p.x / Pitch + 2.5f), 0, N - 1);
            int bj = Mathf.Clamp(Mathf.RoundToInt(p.z / Pitch + 2.5f), 0, N - 1);
            return nodes[bi, bj];
        }

        public RoadNode RandomNode(System.Random r) { return nodes[r.Next(N), r.Next(N)]; }

        public RoadNode PickNext(RoadNode prev, RoadNode cur, System.Random r)
        {
            var options = new List<RoadNode>();
            for (int k = 0; k < cur.neighbors.Count; k++) if (cur.neighbors[k] != prev) options.Add(cur.neighbors[k]);
            if (options.Count == 0) return prev;
            // prefer going straight
            if (prev != null && r.NextDouble() < 0.5)
            {
                Vector3 dir = (cur.pos - prev.pos).normalized;
                for (int k = 0; k < options.Count; k++)
                    if (Vector3.Dot((options[k].pos - cur.pos).normalized, dir) > 0.9f) return options[k];
            }
            return options[r.Next(options.Count)];
        }

        /// <summary>Lane centre-line offset for travelling from a to b (to the right of travel direction).</summary>
        public static Vector3 LaneShift(Vector3 a, Vector3 b)
        {
            Vector3 d = (b - a); d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return Vector3.zero;
            d.Normalize();
            return Vector3.Cross(Vector3.up, d) * LaneOffset;     // right of travel
        }
    }

    /// <summary>Synchronised traffic light cycle. Checkerboard offset so neighbouring junctions alternate.</summary>
    public static class TrafficLights
    {
        public const float Cycle = 24f;
        public enum Light { Red, Yellow, Green }

        public static Light State(int i, int j, bool northSouth, float time)
        {
            float t = Mathf.Repeat(time + (((i + j) & 1) == 0 ? 0f : Cycle * 0.5f), Cycle);
            float start = northSouth ? 0f : 12f;
            float rel = Mathf.Repeat(t - start, Cycle);
            if (rel < 9f) return Light.Green;
            if (rel < 11f) return Light.Yellow;
            return Light.Red;
        }
    }

    public struct DriftZoneDef
    {
        public Vector3 center;
        public Vector3 size;
        public Quaternion rot;
        public float multiplier;
    }

    /// <summary>Everything gameplay needs to know about the generated world.</summary>
    public class WorldInfo
    {
        public static WorldInfo Current;

        public Transform root;
        public WorldMaterials mats;
        public RoadGraph graph;
        public TrackPath industrialLoop, cityRing, serpentine;
        public readonly List<Vector3> lamps = new List<Vector3>();
        public readonly List<DriftZoneDef> industrialZones = new List<DriftZoneDef>();
        public readonly List<DriftZoneDef> serpentineZones = new List<DriftZoneDef>();
        public readonly List<DriftZoneDef> cityZones = new List<DriftZoneDef>();
        public readonly List<Vector3> industrialSpectators = new List<Vector3>();
        public readonly List<Vector3> serpentineSpectators = new List<Vector3>();
        public readonly List<Vector3> citySpectators = new List<Vector3>();
        public readonly List<Transform> chunks = new List<Transform>();
        public readonly List<Vector3> puddleSpots = new List<Vector3>();
        public Vector3 freeRoamSpawn = new Vector3(-47f, 0.5f, -20f);
        public Quaternion freeRoamRot = Quaternion.identity;
        public Bounds bounds;
        public Vector3 industrialCenter = new Vector3(440f, 0f, 0f);

        public TrackPath PathFor(TrackKind k)
        {
            switch (k)
            {
                case TrackKind.IndustrialLoop: return industrialLoop;
                case TrackKind.CityRing: return cityRing;
                default: return serpentine;
            }
        }

        public List<DriftZoneDef> ZonesFor(TrackKind k)
        {
            switch (k)
            {
                case TrackKind.IndustrialLoop: return industrialZones;
                case TrackKind.CityRing: return cityZones;
                default: return serpentineZones;
            }
        }

        public List<Vector3> SpectatorsFor(TrackKind k)
        {
            switch (k)
            {
                case TrackKind.IndustrialLoop: return industrialSpectators;
                case TrackKind.CityRing: return citySpectators;
                default: return serpentineSpectators;
            }
        }
    }
}
