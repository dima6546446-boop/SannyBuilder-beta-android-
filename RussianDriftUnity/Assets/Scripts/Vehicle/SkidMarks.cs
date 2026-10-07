using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>Pooled tyre skid trails: each trail is a dynamic strip mesh; the oldest trail is recycled when the pool runs out.</summary>
    public class SkidMarks : MonoBehaviour
    {
        internal class Trail
        {
            public GameObject go;
            public Mesh mesh;
            public MeshRenderer mr;
            public List<Vector3> verts = new List<Vector3>(256);
            public List<Vector2> uvs = new List<Vector2>(256);
            public List<Color32> cols = new List<Color32>(256);
            public List<int> tris = new List<int>(512);
            public Vector3 lastPoint, lastRight;
            public bool open;
            public int age;
            public int points;
        }

        public const int MaxPointsPerTrail = 90;
        private static SkidMarks instance;
        private readonly List<Trail> trails = new List<Trail>();
        private Material mat;
        private int poolSize = 40;
        private int counter;

        public static SkidMarks Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[SkidMarks]");
                    instance = go.AddComponent<SkidMarks>();
                    instance.Build();
                }
                return instance;
            }
        }

        private void Build()
        {
            poolSize = QualityManager.Current.index == 0 ? 24 : (QualityManager.Current.index == 1 ? 48 : 80);
            mat = MatLib.Alpha(ProcTex.SkidMark(), Color.white);
            mat.renderQueue = 2460;
            for (int i = 0; i < poolSize; i++)
            {
                var t = new Trail();
                t.go = new GameObject("Skid");
                t.go.transform.SetParent(transform, false);
                t.mesh = new Mesh { name = "skid" };
                t.mesh.MarkDynamic();
                t.go.AddComponent<MeshFilter>().sharedMesh = t.mesh;
                t.mr = t.go.AddComponent<MeshRenderer>();
                t.mr.sharedMaterial = mat;
                t.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.mr.receiveShadows = false;
                t.go.SetActive(false);
                trails.Add(t);
            }
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private Trail Acquire()
        {
            Trail best = null;
            for (int i = 0; i < trails.Count; i++)
            {
                var t = trails[i];
                if (!t.go.activeSelf) { best = t; break; }
                if (!t.open && (best == null || t.age < best.age)) best = t;
            }
            if (best == null) best = trails[0];
            best.verts.Clear(); best.uvs.Clear(); best.cols.Clear(); best.tris.Clear();
            best.mesh.Clear();
            best.go.SetActive(true);
            best.open = true; best.points = 0;
            best.age = ++counter;
            return best;
        }

        /// <summary>Per-wheel handle: call Add every physics frame while sliding, End when the wheel stops marking.</summary>
        public class Writer
        {
            internal Trail trail;
            public void End() { if (trail != null) { trail.open = false; trail = null; } }
        }

        public void Add(Writer w, Vector3 point, Vector3 normal, Vector3 forward, float width, float alpha)
        {
            if (alpha < 0.05f) { w.End(); return; }
            Vector3 right = Vector3.Cross(normal, forward).normalized * (width * 0.5f);
            Vector3 p = point + normal * 0.014f;
            var t = w.trail;
            if (t == null)
            {
                t = Acquire();
                w.trail = t;
                t.lastPoint = p; t.lastRight = right;
                AddPair(t, p, right, alpha, 0f);
                return;
            }
            if ((p - t.lastPoint).sqrMagnitude < 0.09f) return;       // 0.3 m spacing
            if (t.points >= MaxPointsPerTrail)
            {
                // continue on a fresh trail starting at the last point so there is no gap
                Vector3 lp = t.lastPoint, lr = t.lastRight;
                t.open = false;
                t = Acquire();
                w.trail = t;
                AddPair(t, lp, lr, alpha, 0f);
                t.lastPoint = lp; t.lastRight = lr;
            }
            AddPair(t, p, right, alpha, t.points);
            t.lastPoint = p; t.lastRight = right;
            Flush(t);
        }

        private void AddPair(Trail t, Vector3 p, Vector3 right, float alpha, float v)
        {
            byte a = (byte)(Mathf.Clamp01(alpha) * 255);
            var c = new Color32(255, 255, 255, a);
            t.verts.Add(p - right); t.verts.Add(p + right);
            t.uvs.Add(new Vector2(0, v)); t.uvs.Add(new Vector2(1, v));
            t.cols.Add(c); t.cols.Add(c);
            int n = t.verts.Count;
            if (n >= 4)
            {
                int i = n - 4;
                t.tris.Add(i); t.tris.Add(i + 2); t.tris.Add(i + 1);
                t.tris.Add(i + 1); t.tris.Add(i + 2); t.tris.Add(i + 3);
            }
            t.points++;
        }

        private void Flush(Trail t)
        {
            t.mesh.Clear();
            t.mesh.SetVertices(t.verts);
            t.mesh.SetUVs(0, t.uvs);
            t.mesh.SetColors(t.cols);
            t.mesh.SetTriangles(t.tris, 0);
            t.mesh.RecalculateBounds();
        }

        public void ClearAll()
        {
            for (int i = 0; i < trails.Count; i++) { trails[i].go.SetActive(false); trails[i].open = false; }
        }
    }
}
