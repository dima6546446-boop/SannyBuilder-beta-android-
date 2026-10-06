using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Параметры городской сетки (как в веб-версии): 7×7 перекрёстков, шаг 96 м, по 2 полосы в каждую сторону.</summary>
    public static class CityC
    {
        public const int N = 7;
        public const float SPACING = 96f, ROAD_W = 14f, HALF = 7f, SIDEWALK = 3f, LANE_W = 3.5f, STOP_BACK = 4.5f, CURB = 0.15f;
        public static readonly float[] LANES = { 1.75f, 5.25f };
        public static float Coord(int i) { return (i - (N - 1) / 2f) * SPACING; }
        public static float Extent => Coord(N - 1) + HALF + SIDEWALK; // до забора
        // автодром ДОСААФ за западными воротами (парковочные уровни)
        public const float AutoX0 = -570f, AutoX1 = -372f, AutoZ0 = -100f, AutoZ1 = 100f, AutoCX = -471f, AutoCZ = 0f, GateHalf = 8f;
    }

    public enum Turn { Straight, Right, Left, UTurn }

    public class RoadNode
    {
        public int id, i, j;
        public float x, z;
        public readonly List<int> nb = new List<int>();
    }

    /// <summary>Граф дорог для ИИ (правостороннее движение). Координаты Unity: +X вправо (восток), +Z вперёд (север).</summary>
    public class RoadGraph
    {
        public readonly List<RoadNode> nodes = new List<RoadNode>();
        public readonly List<Vector2Int> edges = new List<Vector2Int>();

        public RoadGraph()
        {
            int N = CityC.N;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                    nodes.Add(new RoadNode { id = j * N + i, i = i, j = j, x = CityC.Coord(i), z = CityC.Coord(j) });
            foreach (var n in nodes)
            {
                Add(n, n.i + 1, n.j); Add(n, n.i - 1, n.j); Add(n, n.i, n.j + 1); Add(n, n.i, n.j - 1);
            }
        }

        void Add(RoadNode n, int i, int j)
        {
            if (i < 0 || j < 0 || i >= CityC.N || j >= CityC.N) return;
            int m = Id(i, j);
            n.nb.Add(m);
            edges.Add(new Vector2Int(n.id, m));
        }

        public int Id(int i, int j) { return j * CityC.N + i; }

        public Vector2 Dir(int a, int b)
        {
            var A = nodes[a]; var B = nodes[b];
            return new Vector2(B.x - A.x, B.z - A.z).normalized;
        }

        /// <summary>Тип манёвра на узле b при движении a→b→c (в Unity поворот направо — «по часовой» сверху).</summary>
        public Turn TurnType(int a, int b, int c)
        {
            if (c == a) return Turn.UTurn;
            var d1 = Dir(a, b); var d2 = Dir(b, c);
            float cross = d1.y * d2.x - d1.x * d2.y;
            if (Mathf.Abs(cross) < 0.01f) return Turn.Straight;
            return cross > 0 ? Turn.Right : Turn.Left;
        }

        public int PickNext(int b, int a, Rng rnd)
        {
            var opts = new List<int>();
            foreach (int n in nodes[b].nb) if (n != a) opts.Add(n);
            if (opts.Count == 0) return a;
            float total = 0f;
            var w = new float[opts.Count];
            for (int k = 0; k < opts.Count; k++) { w[k] = TurnType(a, b, opts[k]) == Turn.Straight ? 2f : 1f; total += w[k]; }
            float r = rnd.Next() * total;
            for (int k = 0; k < opts.Count; k++) { r -= w[k]; if (r <= 0) return opts[k]; }
            return opts[opts.Count - 1];
        }
    }

    public enum Signal { Green, Yellow, Red }

    /// <summary>Светофоры: фазы со сдвигом по узлам, мигающий зелёный, «красный+жёлтый» перед зелёным.</summary>
    public class TrafficLights
    {
        const float GreenT = 12f, YellowT = 3f, AllRedT = 2f;
        const float Cycle = 2f * (GreenT + YellowT + AllRedT);
        readonly float[] offsets;
        public float time;

        public TrafficLights(RoadGraph g, Rng rnd)
        {
            offsets = new float[g.nodes.Count];
            for (int i = 0; i < offsets.Length; i++) offsets[i] = rnd.Next() * Cycle;
        }

        public void Update(float dt) { time += dt; }

        /// <summary>axis 0 — движение вдоль Z (север-юг), 1 — вдоль X.</summary>
        public Signal Get(int node, int axis) { int lamp; return Get(node, axis, out lamp); }

        /// <summary>lamp: битовая маска горящих ламп (1 — красный, 2 — жёлтый, 4 — зелёный).</summary>
        public Signal Get(int node, int axis, out int lamp)
        {
            float t = (time + offsets[node]) % Cycle;
            if (axis == 1) t = (t + Cycle / 2f) % Cycle;
            if (t < GreenT)
            {
                bool blink = t > GreenT - 3f && (t * 2f) % 1f > 0.5f;
                lamp = blink ? 0 : 4;
                return Signal.Green;
            }
            if (t < GreenT + YellowT) { lamp = 2; return Signal.Yellow; }
            float tr = t - GreenT - YellowT;
            float redLen = Cycle - GreenT - YellowT;
            lamp = tr > redLen - 1.5f ? 3 : 1; // красный + жёлтый перед зелёным
            return Signal.Red;
        }
    }
}
