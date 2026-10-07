using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RussianDrift.Core
{
    /// <summary>
    /// CPU mesh assembler used for every procedural model (cars, buildings, roads, props).
    /// Winding follows Unity's clockwise-front convention: normal = cross(b-a, c-a).
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> norms = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<Color32> cols = new List<Color32>();
        public readonly List<int> tris = new List<int>();
        public Color32 currentColor = new Color32(255, 255, 255, 255);

        public int VertexCount { get { return verts.Count; } }

        public int AddVert(Vector3 p, Vector3 n, Vector2 uv)
        {
            verts.Add(p); norms.Add(n); uvs.Add(uv); cols.Add(currentColor);
            return verts.Count - 1;
        }

        public void AddTri(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }

        /// <summary>Flat quad, clockwise a,b,c,d seen from the front.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            n.Normalize();
            int i0 = AddVert(a, n, uva), i1 = AddVert(b, n, uvb), i2 = AddVert(c, n, uvc), i3 = AddVert(d, n, uvd);
            AddTri(i0, i1, i2); AddTri(i0, i2, i3);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddQuad(a, b, c, d, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        public void AddQuadSmooth(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
            Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
        {
            int i0 = AddVert(a, na, uva), i1 = AddVert(b, nb, uvb), i2 = AddVert(c, nc, uvc), i3 = AddVert(d, nd, uvd);
            AddTri(i0, i1, i2); AddTri(i0, i2, i3);
        }

        /// <summary>Face with outward normal n; u axis given, v = cross(u, n).</summary>
        public void AddFace(Vector3 center, Vector3 n, Vector3 u, float su, float sv, Vector2 uvTile)
        {
            Vector3 v = Vector3.Cross(u, n);
            Vector3 hu = u * (su * 0.5f), hv = v * (sv * 0.5f);
            float tu = uvTile.x > 0f ? su / uvTile.x : 1f;
            float tv = uvTile.y > 0f ? sv / uvTile.y : 1f;
            AddQuad(center - hu - hv, center - hu + hv, center + hu + hv, center + hu - hv,
                new Vector2(0, 0), new Vector2(0, tv), new Vector2(tu, tv), new Vector2(tu, 0));
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, Vector2 uvTile, bool top = true, bool bottom = true)
        {
            Vector3 r = rot * Vector3.right, u = rot * Vector3.up, f = rot * Vector3.forward;
            Vector3 h = size * 0.5f;
            AddFace(center + r * h.x, r, Vector3.Cross(r, u), size.z, size.y, uvTile);
            AddFace(center - r * h.x, -r, Vector3.Cross(-r, u), size.z, size.y, uvTile);
            AddFace(center + f * h.z, f, Vector3.Cross(f, u), size.x, size.y, uvTile);
            AddFace(center - f * h.z, -f, Vector3.Cross(-f, u), size.x, size.y, uvTile);
            if (top) AddFace(center + u * h.y, u, r, size.x, size.z, uvTile);
            if (bottom) AddFace(center - u * h.y, -u, r, size.x, size.z, uvTile);
        }

        public void AddBox(Vector3 center, Vector3 size)
        {
            AddBox(center, size, Quaternion.identity, Vector2.zero);
        }

        public void AddCylinder(Vector3 center, float radius, float height, Quaternion rot, int segments, bool caps = true)
        {
            Vector3 ax = rot * Vector3.up;
            Vector3 ex = rot * Vector3.right, ez = rot * Vector3.forward;
            Vector3 b = center - ax * (height * 0.5f), t = center + ax * (height * 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 d0 = ex * Mathf.Cos(a0) + ez * Mathf.Sin(a0), d1 = ex * Mathf.Cos(a1) + ez * Mathf.Sin(a1);
                AddQuadSmooth(b + d0 * radius, t + d0 * radius, t + d1 * radius, b + d1 * radius, d0, d0, d1, d1,
                    new Vector2(i / (float)segments, 0), new Vector2(i / (float)segments, 1),
                    new Vector2((i + 1) / (float)segments, 1), new Vector2((i + 1) / (float)segments, 0));
                if (caps)
                {
                    int c0 = AddVert(t, ax, new Vector2(0.5f, 0.5f));
                    int c1 = AddVert(t + d1 * radius, ax, new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f));
                    int c2 = AddVert(t + d0 * radius, ax, new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f));
                    AddTri(c0, c1, c2);
                    int e0 = AddVert(b, -ax, new Vector2(0.5f, 0.5f));
                    int e1 = AddVert(b + d0 * radius, -ax, new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f));
                    int e2 = AddVert(b + d1 * radius, -ax, new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f));
                    AddTri(e0, e1, e2);
                }
            }
        }

        /// <summary>Revolve a (radius, axial) profile around local Y. Profile order must follow the outline so (da,-dr) is outward.</summary>
        public void AddLathe(Vector2[] profile, int segments, Vector3 center, Quaternion rot)
        {
            Vector3 ax = rot * Vector3.up, ex = rot * Vector3.right, ez = rot * Vector3.forward;
            for (int j = 0; j < profile.Length - 1; j++)
            {
                Vector2 p0 = profile[j], p1 = profile[j + 1];
                Vector2 tan = p1 - p0;
                Vector2 nrm = new Vector2(tan.y, -tan.x).normalized;   // (radial, axial)
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                    Vector3 d0 = ex * Mathf.Cos(a0) + ez * Mathf.Sin(a0), d1 = ex * Mathf.Cos(a1) + ez * Mathf.Sin(a1);
                    Vector3 n0 = d0 * nrm.x + ax * nrm.y, n1 = d1 * nrm.x + ax * nrm.y;
                    Vector3 v00 = center + d0 * p0.x + ax * p0.y, v01 = center + d0 * p1.x + ax * p1.y;
                    Vector3 v10 = center + d1 * p0.x + ax * p0.y, v11 = center + d1 * p1.x + ax * p1.y;
                    float u0 = i / (float)segments, u1 = (i + 1) / (float)segments;
                    AddQuadSmooth(v00, v01, v11, v10, n0, n0, n1, n1,
                        new Vector2(u0, p0.y), new Vector2(u0, p1.y), new Vector2(u1, p1.y), new Vector2(u1, p0.y));
                }
            }
        }

        /// <summary>Loft rings along a path. Each ring is a closed polygon of equal point count, ordered so the surface faces outward.</summary>
        public void AddLoft(List<Vector3[]> rings, bool capStart, bool capEnd, bool smooth)
        {
            int n = rings[0].Length;
            for (int r = 0; r < rings.Count - 1; r++)
            {
                Vector3[] A = rings[r], B = rings[r + 1];
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Vector3 a = A[i], b = B[i], c = B[j], d = A[j];
                    float u0 = i / (float)n, u1 = (i + 1) / (float)n;
                    float v0 = r / (float)(rings.Count - 1), v1 = (r + 1) / (float)(rings.Count - 1);
                    AddQuad(a, b, c, d, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
                }
            }
            if (capStart) Cap(rings[0], false);
            if (capEnd) Cap(rings[rings.Count - 1], true);
        }


        /// <summary>Loft with shared ring vertices and smooth averaged normals (rings clockwise seen along +Z). Seam column is duplicated for clean UVs.</summary>
        public void AddLoftSmooth(List<Vector3[]> rings, bool capStart, bool capEnd, float uScale = 1f, float vScale = 1f)
        {
            int n = rings[0].Length;
            int cols1 = n + 1;
            int baseIdx = verts.Count;
            int rc = rings.Count;
            var nrm = new Vector3[rc * cols1];
            for (int r = 0; r < rc; r++)
                for (int i = 0; i < cols1; i++)
                {
                    verts.Add(rings[r][i % n]); norms.Add(Vector3.up);
                    uvs.Add(new Vector2(i / (float)n * uScale, r / (float)(rc - 1) * vScale)); cols.Add(currentColor);
                }
            for (int r = 0; r < rc - 1; r++)
                for (int i = 0; i < n; i++)
                {
                    int a = r * cols1 + i, b = (r + 1) * cols1 + i, c = (r + 1) * cols1 + i + 1, d = r * cols1 + i + 1;
                    Vector3 fn = Vector3.Cross(verts[baseIdx + b] - verts[baseIdx + a], verts[baseIdx + c] - verts[baseIdx + a]);
                    if (fn.sqrMagnitude < 1e-14f) fn = Vector3.Cross(verts[baseIdx + c] - verts[baseIdx + a], verts[baseIdx + d] - verts[baseIdx + a]);
                    nrm[a] += fn; nrm[b] += fn; nrm[c] += fn; nrm[d] += fn;
                    AddTri(baseIdx + a, baseIdx + b, baseIdx + c); AddTri(baseIdx + a, baseIdx + c, baseIdx + d);
                }
            for (int r = 0; r < rc; r++) { nrm[r * cols1] += nrm[r * cols1 + n]; nrm[r * cols1 + n] = nrm[r * cols1]; }
            for (int k = 0; k < nrm.Length; k++)
                norms[baseIdx + k] = nrm[k].sqrMagnitude > 1e-14f ? nrm[k].normalized : Vector3.up;
            if (capStart) Cap(rings[0], false);
            if (capEnd) Cap(rings[rc - 1], true);
        }

        private void Cap(Vector3[] ring, bool end)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) c += ring[i];
            c /= ring.Length;
            for (int i = 0; i < ring.Length; i++)
            {
                int j = (i + 1) % ring.Length;
                Vector3 a = end ? ring[j] : ring[i], b = end ? ring[i] : ring[j];
                Vector3 nrm = Vector3.Cross(a - c, b - c).normalized;
                int i0 = AddVert(c, nrm, new Vector2(0.5f, 0.5f)), i1 = AddVert(a, nrm, new Vector2(1, 0)), i2 = AddVert(b, nrm, new Vector2(0, 1));
                AddTri(i0, i1, i2);
            }
        }

        public void Append(MeshBuilder o, Matrix4x4 m)
        {
            int baseIndex = verts.Count;
            for (int i = 0; i < o.verts.Count; i++)
            {
                verts.Add(m.MultiplyPoint3x4(o.verts[i]));
                norms.Add(m.MultiplyVector(o.norms[i]).normalized);
                uvs.Add(o.uvs[i]);
                cols.Add(o.cols[i]);
            }
            for (int i = 0; i < o.tris.Count; i++) tris.Add(o.tris[i] + baseIndex);
        }

        public Mesh ToMesh(string name, bool markDynamic = false)
        {
            var m = new Mesh();
            m.name = name;
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            if (markDynamic) m.MarkDynamic();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        public void Clear() { verts.Clear(); norms.Clear(); uvs.Clear(); cols.Clear(); tris.Clear(); }
    }

    public static class MeshUtil
    {
        /// <summary>Creates a child object with mesh renderer; optionally a collider.</summary>
        public static GameObject Make(string name, Transform parent, Mesh mesh, Material mat, bool collider = false, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
            return go;
        }
    }
}
