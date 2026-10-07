using System.Collections.Generic;
using UnityEngine;

namespace Zanos.Game
{
    /// <summary>Сборка меша из примитивов (боксы, цилиндры, ленты, выдавленные выпуклые многоугольники) — всё процедурно, без ассетов.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector3> n = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();

        public int VertexCount { get { return v.Count; } }
        public IList<Vector3> Vertices { get { return v; } }
        public IList<Vector3> Normals { get { return n; } }
        public IList<int> Triangles { get { return t; } }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float uMax = 1, float vMax = 1)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(uMax, 0)); uv.Add(new Vector2(uMax, vMax)); uv.Add(new Vector2(0, vMax));
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);          // лицевая сторона Unity: по часовой стрелке (проверено MeshWindingCheck)
        }

        /// <summary>Бокс с центром c, размерами s (x, y, z), поворотом вокруг Y (градусы) и наклоном вокруг X.</summary>
        public void AddBox(Vector3 c, Vector3 s, float yawDeg = 0, float pitchDeg = 0, float rollDeg = 0, float tileU = 1, float tileV = 1)
        {
            var rot = Quaternion.Euler(pitchDeg, yawDeg, rollDeg); var h = s * 0.5f;
            Vector3[] p = {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z) };
            for (int i = 0; i < 8; i++) p[i] = c + rot * p[i];
            Vector3 right = rot * Vector3.right, up = rot * Vector3.up, fwd = rot * Vector3.forward;
            Quad(p[0], p[3], p[2], p[1], -fwd, s.x * tileU, s.y * tileV);        // задняя
            Quad(p[5], p[6], p[7], p[4], fwd, s.x * tileU, s.y * tileV);         // передняя
            Quad(p[4], p[7], p[3], p[0], -right, s.z * tileU, s.y * tileV);      // левая
            Quad(p[1], p[2], p[6], p[5], right, s.z * tileU, s.y * tileV);       // правая
            Quad(p[3], p[7], p[6], p[2], up, s.x * tileU, s.z * tileV);          // верх
            Quad(p[4], p[0], p[1], p[5], -up, s.x * tileU, s.z * tileV);         // низ
        }

        /// <summary>Вертикальный цилиндр/конус (r0 снизу, r1 сверху).</summary>
        public void AddCylinder(Vector3 baseCenter, float r0, float r1, float height, int seg = 12)
        {
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vector3 nn = new Vector3(ca, (r0 - r1) / Mathf.Max(0.001f, height), sa).normalized;
                v.Add(baseCenter + new Vector3(ca * r0, 0, sa * r0)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(baseCenter + new Vector3(ca * r1, height, sa * r1)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
                t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c);
            }
            // крышка сверху
            int ci = v.Count; v.Add(baseCenter + Vector3.up * height); n.Add(Vector3.up); uv.Add(Vector2.zero);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2; int k = v.Count;
                v.Add(baseCenter + new Vector3(Mathf.Cos(a0) * r1, height, Mathf.Sin(a0) * r1)); v.Add(baseCenter + new Vector3(Mathf.Cos(a1) * r1, height, Mathf.Sin(a1) * r1));
                n.Add(Vector3.up); n.Add(Vector3.up); uv.Add(Vector2.zero); uv.Add(Vector2.zero);
                t.Add(ci); t.Add(k + 1); t.Add(k);
            }
        }

        /// <summary>Цилиндр вдоль оси X (колёса): центр c, радиус r, ширина w.</summary>
        public void AddWheel(Vector3 c, float r, float w, int seg = 16)
        {
            var rot = Quaternion.Euler(0, 0, 90);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                Vector3 hy = new Vector3(0, w * 0.5f, 0);
                Vector3 a = c + rot * (p0 - hy), b = c + rot * (p0 + hy), cc = c + rot * (p1 + hy), d = c + rot * (p1 - hy);
                Vector3 nn = (rot * ((p0 + p1) * 0.5f)).normalized;
                Quad(a, b, cc, d, nn);
                int k = v.Count; Vector3 ctrR = c + rot * hy, ctrL = c - rot * hy;
                v.Add(ctrR); v.Add(c + rot * (p0 + hy)); v.Add(c + rot * (p1 + hy)); for (int q = 0; q < 3; q++) { n.Add(rot * Vector3.up); uv.Add(Vector2.zero); } t.Add(k); t.Add(k + 2); t.Add(k + 1);
                k = v.Count; v.Add(ctrL); v.Add(c + rot * (p0 - hy)); v.Add(c + rot * (p1 - hy)); for (int q = 0; q < 3; q++) { n.Add(rot * Vector3.down); uv.Add(Vector2.zero); } t.Add(k); t.Add(k + 1); t.Add(k + 2);
            }
        }

        /// <summary>Плоская лента вдоль ломаной (x, z) шириной width на высоте y (для дорог, разметки, следов).</summary>
        public void AddStrip(IList<Vector2> pts, float width, float y, bool closed = false, float tile = 6f)
        {
            int count = pts.Count; if (count < 2) return;
            float dist = 0; int start = v.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = pts[closed ? (i - 1 + count) % count : Mathf.Max(0, i - 1)], b = pts[closed ? (i + 1) % count : Mathf.Min(count - 1, i + 1)];
                Vector2 d = (b - a); if (d.sqrMagnitude < 1e-9f) d = Vector2.up; d.Normalize();
                Vector2 nrm = new Vector2(-d.y, d.x) * (width * 0.5f);
                if (i > 0) dist += (pts[i] - pts[i - 1]).magnitude;
                v.Add(new Vector3(pts[i].x - nrm.x, y, pts[i].y - nrm.y)); v.Add(new Vector3(pts[i].x + nrm.x, y, pts[i].y + nrm.y));
                n.Add(Vector3.up); n.Add(Vector3.up); uv.Add(new Vector2(0, dist / tile)); uv.Add(new Vector2(width / tile, dist / tile));
            }
            int segs = closed ? count : count - 1;
            for (int i = 0; i < segs; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % count) * 2;
                t.Add(a); t.Add(a + 1); t.Add(b); t.Add(a + 1); t.Add(b + 1); t.Add(b);
            }
        }

        /// <summary>Горизонтальный прямоугольник/круг (покрытие), UV по мировым координатам / tile.</summary>
        public void AddRect(float x0, float z0, float x1, float z1, float y, float tile)
        {
            int i = v.Count;
            v.Add(new Vector3(x0, y, z0)); v.Add(new Vector3(x0, y, z1)); v.Add(new Vector3(x1, y, z1)); v.Add(new Vector3(x1, y, z0));
            for (int k = 0; k < 4; k++) n.Add(Vector3.up);
            uv.Add(new Vector2(x0 / tile, z0 / tile)); uv.Add(new Vector2(x0 / tile, z1 / tile)); uv.Add(new Vector2(x1 / tile, z1 / tile)); uv.Add(new Vector2(x1 / tile, z0 / tile));
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
        public void AddDisc(float cx, float cz, float r, float y, float tile, int seg = 40)
        {
            int c = v.Count; v.Add(new Vector3(cx, y, cz)); n.Add(Vector3.up); uv.Add(new Vector2(cx / tile, cz / tile));
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2; float x = cx + Mathf.Cos(a) * r, z = cz + Mathf.Sin(a) * r;
                v.Add(new Vector3(x, y, z)); n.Add(Vector3.up); uv.Add(new Vector2(x / tile, z / tile));
            }
            for (int i = 0; i < seg; i++) { t.Add(c); t.Add(c + i + 2); t.Add(c + i + 1); }
        }

        /// <summary>Выдавливание выпуклого многоугольника в плоскости (z, y) вдоль оси X (кабина, силуэты кузова).</summary>
        public void AddExtrudedConvex(IList<Vector2> zy, float width, float xCenter = 0)
        {
            float hx = width * 0.5f; int m = zy.Count;
            for (int side = 0; side < 2; side++)
            {
                float x = xCenter + (side == 0 ? -hx : hx); Vector3 nn = side == 0 ? Vector3.left : Vector3.right;
                int s0 = v.Count;
                for (int i = 0; i < m; i++) { v.Add(new Vector3(x, zy[i].y, zy[i].x)); n.Add(nn); uv.Add(zy[i]); }
                for (int i = 1; i < m - 1; i++) { if (side == 0) { t.Add(s0); t.Add(s0 + i + 1); t.Add(s0 + i); } else { t.Add(s0); t.Add(s0 + i); t.Add(s0 + i + 1); } }
            }
            for (int i = 0; i < m; i++)
            {
                Vector2 a = zy[i], b = zy[(i + 1) % m]; Vector2 e = b - a; Vector3 nn = new Vector3(0, e.x, -e.y).normalized;
                Quad(new Vector3(xCenter - hx, a.y, a.x), new Vector3(xCenter - hx, b.y, b.x), new Vector3(xCenter + hx, b.y, b.x), new Vector3(xCenter + hx, a.y, a.x), nn);
            }
        }

        public Mesh ToMesh(string name = "mesh")
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds(); return m;
        }
    }
}
