using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.World
{
    /// <summary>A polyline racing/drift path with arc-length lookup, built from control points with Catmull-Rom smoothing.</summary>
    public class TrackPath
    {
        public readonly List<Vector3> pts = new List<Vector3>();
        public float[] cum;
        public float length;
        public bool closed;
        public float width = 16f;
        public string name;

        public int Count { get { return pts.Count; } }

        public static TrackPath FromControl(string name, List<Vector3> ctrl, bool closed, float spacing, float width)
        {
            var tp = new TrackPath { closed = closed, width = width, name = name };
            int n = ctrl.Count;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = ctrl[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)];
                Vector3 p1 = ctrl[i];
                Vector3 p2 = ctrl[closed ? (i + 1) % n : i + 1];
                Vector3 p3 = ctrl[closed ? (i + 2) % n : Mathf.Min(i + 2, n - 1)];
                float d = Vector3.Distance(p1, p2);
                int steps = Mathf.Max(2, Mathf.CeilToInt(d / spacing));
                for (int s = 0; s < steps; s++) tp.pts.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
            }
            if (!closed) tp.pts.Add(ctrl[n - 1]);
            tp.Rebuild();
            return tp;
        }

        public static TrackPath FromPoints(string name, List<Vector3> points, bool closed, float width)
        {
            var tp = new TrackPath { closed = closed, width = width, name = name };
            tp.pts.AddRange(points);
            tp.Rebuild();
            return tp;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public void Rebuild()
        {
            int n = pts.Count;
            cum = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (!closed && i == n - 1) { cum[i + 1] = cum[i]; break; }
                cum[i + 1] = cum[i] + Vector3.Distance(pts[i], pts[j]);
            }
            length = cum[closed ? n : n - 1];
        }

        public Vector3 Point(int i) { int n = pts.Count; return pts[closed ? ((i % n) + n) % n : Mathf.Clamp(i, 0, n - 1)]; }

        public Vector3 Tangent(int i)
        {
            Vector3 a = Point(i - 1), b = Point(i + 1);
            Vector3 t = b - a; t.y = 0f;
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward;
        }

        public Vector3 Sample(float dist, out Vector3 tangent)
        {
            int n = pts.Count;
            if (closed) dist = Mathf.Repeat(dist, length); else dist = Mathf.Clamp(dist, 0f, length);
            int lo = 0, hi = n;
            while (lo < hi - 1) { int mid = (lo + hi) / 2; if (cum[mid] <= dist) lo = mid; else hi = mid; }
            int i = Mathf.Clamp(lo, 0, n - 1);
            Vector3 a = Point(i), b = Point(i + 1);
            float seg = Mathf.Max(0.0001f, Vector3.Distance(a, b));
            float t = Mathf.Clamp01((dist - cum[i]) / seg);
            tangent = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : Tangent(i);
            return Vector3.Lerp(a, b, t);
        }

        public Vector3 Sample(float dist) { Vector3 t; return Sample(dist, out t); }

        /// <summary>Index of the closest sample, searched near the hint for speed (pass -1 for a global search).</summary>
        public int Nearest(Vector3 p, int hint = -1, int window = 40)
        {
            int n = pts.Count;
            int best = 0; float bd = float.MaxValue;
            if (hint < 0 || window * 2 >= n)
            {
                for (int i = 0; i < n; i++) { float d = Flat(pts[i], p); if (d < bd) { bd = d; best = i; } }
                return best;
            }
            for (int k = -window; k <= window; k++)
            {
                int i = closed ? ((hint + k) % n + n) % n : hint + k;
                if (i < 0 || i >= n) continue;
                float d = Flat(pts[i], p);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        private static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz; }

        public float DistanceAt(int i) { return cum[Mathf.Clamp(i, 0, cum.Length - 1)]; }

        /// <summary>Signed turning angle (deg) between the heading `lookAhead` metres before and after the sample.</summary>
        public float Curvature(int i, float span)
        {
            float d = DistanceAt(i);
            Vector3 t0, t1;
            Sample(d - span, out t0); Sample(d + span, out t1);
            return Vector3.SignedAngle(t0, t1, Vector3.up);
        }
    }
}
