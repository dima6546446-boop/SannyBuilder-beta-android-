using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    /// <summary>Следы шин: кольцевой буфер четырёхугольников в одном динамическом меше (1 draw call).</summary>
    public class SkidMarks
    {
        const int Max = 1200;
        readonly Mesh mesh;
        readonly Vector3[] v = new Vector3[Max * 4];
        readonly Vector3[] n = new Vector3[Max * 4];
        readonly Vector2[] uv = new Vector2[Max * 4];
        readonly Color[] col = new Color[Max * 4];
        int next;
        bool dirty;
        readonly Vector3[] last = new Vector3[4];
        readonly bool[] has = new bool[4];
        readonly Vector3[] lastL = new Vector3[4], lastR = new Vector3[4];

        public SkidMarks(Transform parent)
        {
            mesh = new Mesh { name = "SkidMarks" };
            mesh.MarkDynamic();
            var tri = new int[Max * 6];
            for (int i = 0; i < Max; i++)
            {
                tri[i * 6] = i * 4; tri[i * 6 + 1] = i * 4 + 1; tri[i * 6 + 2] = i * 4 + 2;
                tri[i * 6 + 3] = i * 4; tri[i * 6 + 4] = i * 4 + 2; tri[i * 6 + 5] = i * 4 + 3;
            }
            for (int i = 0; i < v.Length; i++) n[i] = Vector3.up;
            mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.colors = col;
            mesh.triangles = tri;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 5000f);
            var go = new GameObject("SkidMarks");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = Mats.Particles().Tex(Mats.Skid);
            m.name = "Skid";
            m.renderQueue = 2460;
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>Точка следа колеса wheel (0..3). on — колесо скользит; k — насыщенность.</summary>
        public void Add(int wheel, Vector3 p, Vector3 right, bool on, float k)
        {
            if (!on) { has[wheel] = false; return; }
            p.y += 0.02f;
            var l = p - right * 0.09f; var r = p + right * 0.09f;
            if (has[wheel] && (p - last[wheel]).sqrMagnitude > 0.09f)
            {
                int i = next * 4;
                v[i] = lastL[wheel]; v[i + 1] = l; v[i + 2] = r; v[i + 3] = lastR[wheel];
                var c = new Color(1, 1, 1, Mathf.Clamp01(0.35f + k * 0.6f));
                col[i] = col[i + 1] = col[i + 2] = col[i + 3] = c;
                uv[i] = new Vector2(0, 0); uv[i + 1] = new Vector2(0, 1); uv[i + 2] = new Vector2(1, 1); uv[i + 3] = new Vector2(1, 0);
                next = (next + 1) % Max;
                dirty = true;
                last[wheel] = p; lastL[wheel] = l; lastR[wheel] = r;
            }
            else if (!has[wheel]) { last[wheel] = p; lastL[wheel] = l; lastR[wheel] = r; }
            has[wheel] = true;
        }

        public void Flush()
        {
            if (!dirty) return;
            dirty = false;
            mesh.vertices = v; mesh.colors = col; mesh.uv = uv;
        }

        public void Clear()
        {
            for (int i = 0; i < v.Length; i++) { v[i] = Vector3.zero; col[i] = Color.clear; }
            for (int w = 0; w < 4; w++) has[w] = false;
            dirty = true;
            Flush();
        }
    }
}
