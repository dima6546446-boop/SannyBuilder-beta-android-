using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    /// <summary>
    /// Сборщик процедурной геометрии: квадраты, коробки, цилиндры с UV и цветом вершин.
    /// Весь квартал собирается в один меш (1 draw call) — главная оптимизация города.
    /// Лицевые грани — по часовой стрелке (как требует Unity).
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>(1024);
        public readonly List<Vector3> n = new List<Vector3>(1024);
        public readonly List<Vector2> uv = new List<Vector2>(1024);
        public readonly List<Color32> col = new List<Color32>(1024);
        public readonly List<int> tri = new List<int>(2048);
        public Color32 color = new Color32(255, 255, 255, 255);

        public int Count => v.Count;

        /// <summary>Четырёхугольник a-b-c-d (по часовой, если смотреть на лицевую сторону).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(uva); uv.Add(uvb); uv.Add(uvc); uv.Add(uvd);
            col.Add(color); col.Add(color); col.Add(color); col.Add(color);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        /// <summary>Горизонтальный прямоугольник (лицом вверх) с курсом h. uvScale — метров на повтор текстуры.</summary>
        public void Flat(float cx, float cz, float y, float w, float l, float h = 0f, float uvScale = 0f)
        {
            var f = M.Fwd(h); var r = M.Right(h);
            var c = new Vector3(cx, y, cz);
            Vector3 A = c - r * (w / 2) - f * (l / 2), B = c - r * (w / 2) + f * (l / 2), C = c + r * (w / 2) + f * (l / 2), D = c + r * (w / 2) - f * (l / 2);
            if (uvScale > 0f)
                Quad(A, B, C, D, Vector3.up, new Vector2(A.x, A.z) / uvScale, new Vector2(B.x, B.z) / uvScale, new Vector2(C.x, C.z) / uvScale, new Vector2(D.x, D.z) / uvScale);
            else
                Quad(A, B, C, D, Vector3.up, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        /// <summary>Вертикальная стена от (x0,z0) до (x1,z1), лицом вправо от направления 0→1.</summary>
        public void Wall(float x0, float z0, float x1, float z1, float y0, float y1, float uScale, float vScale, float u0 = 0f)
        {
            var a = new Vector3(x0, y0, z0); var b = new Vector3(x1, y0, z1);
            var dir = (b - a); float len = dir.magnitude; dir /= len;
            var nrm = new Vector3(dir.z, 0, -dir.x);
            float u1 = u0 + len / uScale;
            Quad(a, new Vector3(x0, y1, z0), new Vector3(x1, y1, z1), b, nrm,
                new Vector2(u0, y0 / vScale), new Vector2(u0, y1 / vScale), new Vector2(u1, y1 / vScale), new Vector2(u1, y0 / vScale));
        }

        /// <summary>Коробка по центру (cx, y0..y0+h, cz) размером sx×sz, с поворотом h. top/bottom — рисовать ли крышку/дно.</summary>
        public void Box(Vector3 c, Vector3 size, float h = 0f, bool top = true, bool bottom = false, float uvScale = 1f)
        {
            var f = M.Fwd(h); var r = M.Right(h);
            float hx = size.x / 2, hz = size.z / 2, y0 = c.y - size.y / 2, y1 = c.y + size.y / 2;
            Vector3 p = new Vector3(c.x, 0, c.z);
            Vector3 c00 = p - r * hx - f * hz, c10 = p + r * hx - f * hz, c11 = p + r * hx + f * hz, c01 = p - r * hx + f * hz;
            // стороны (обход по часовой при взгляде снаружи)
            // обход так, чтобы «вправо» от направления a→b смотрело наружу
            Side(c00, c10, y0, y1, uvScale); // зад
            Side(c10, c11, y0, y1, uvScale); // право
            Side(c11, c01, y0, y1, uvScale); // перед
            Side(c01, c00, y0, y1, uvScale); // лево
            if (top)
            {
                Vector3 up = Vector3.up * y1;
                Quad(c00 + up, c01 + up, c11 + up, c10 + up, Vector3.up, new Vector2(0, 0), new Vector2(0, size.z / uvScale), new Vector2(size.x / uvScale, size.z / uvScale), new Vector2(size.x / uvScale, 0));
            }
            if (bottom)
            {
                Vector3 dn = Vector3.up * y0;
                Quad(c10 + dn, c11 + dn, c01 + dn, c00 + dn, Vector3.down, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            }
        }

        void Side(Vector3 a, Vector3 b, float y0, float y1, float uvScale)
        {
            var d = b - a; float len = d.magnitude;
            var nrm = new Vector3(d.z, 0, -d.x) / Mathf.Max(len, 1e-4f); // «вправо» от a→b
            Quad(new Vector3(a.x, y0, a.z), new Vector3(a.x, y1, a.z), new Vector3(b.x, y1, b.z), new Vector3(b.x, y0, b.z), nrm,
                new Vector2(0, y0 / uvScale), new Vector2(0, y1 / uvScale), new Vector2(len / uvScale, y1 / uvScale), new Vector2(len / uvScale, y0 / uvScale));
        }

        /// <summary>Цилиндр вдоль оси X (колесо): центр, радиус, ширина, сегменты. caps — закрыть торцы.</summary>
        public void CylinderX(Vector3 c, float radius, float width, int seg, bool caps = true, float capInset = 0f)
        {
            float x0 = c.x - width / 2, x1 = c.x + width / 2;
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float y = Mathf.Cos(a), z = Mathf.Sin(a);
                var nn = new Vector3(0, y, z);
                v.Add(new Vector3(x0, c.y + y * radius, c.z + z * radius)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 0)); col.Add(color);
                v.Add(new Vector3(x1, c.y + y * radius, c.z + z * radius)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 1)); col.Add(color);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2, b = a + 1, cc = a + 2, d = a + 3;
                tri.Add(a); tri.Add(cc); tri.Add(b);
                tri.Add(b); tri.Add(cc); tri.Add(d);
            }
            if (caps) { Disc(new Vector3(x0 + capInset, c.y, c.z), radius, seg, -1); Disc(new Vector3(x1 - capInset, c.y, c.z), radius, seg, 1); }
        }

        /// <summary>Диск в плоскости YZ, нормаль ±X.</summary>
        public void Disc(Vector3 c, float radius, int seg, int side)
        {
            int center = v.Count;
            var nn = new Vector3(side, 0, 0);
            v.Add(c); n.Add(nn); uv.Add(new Vector2(0.5f, 0.5f)); col.Add(color);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(c.x, c.y + Mathf.Cos(a) * radius, c.z + Mathf.Sin(a) * radius)); n.Add(nn);
                uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f)); col.Add(color);
            }
            for (int i = 0; i < seg; i++)
            {
                if (side > 0) { tri.Add(center); tri.Add(center + 1 + i); tri.Add(center + 2 + i); }
                else { tri.Add(center); tri.Add(center + 2 + i); tri.Add(center + 1 + i); }
            }
        }

        /// <summary>Цилиндр по оси Y (столб, ствол дерева, конус при r1 ≠ r0).</summary>
        public void CylinderY(Vector3 b, float r0, float r1, float height, int seg, bool top = true)
        {
            int start = v.Count;
            float slope = (r0 - r1) / Mathf.Max(height, 1e-3f);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float x = Mathf.Sin(a), z = Mathf.Cos(a);
                var nn = new Vector3(x, slope, z).normalized;
                v.Add(new Vector3(b.x + x * r0, b.y, b.z + z * r0)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 0)); col.Add(color);
                v.Add(new Vector3(b.x + x * r1, b.y + height, b.z + z * r1)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 1)); col.Add(color);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2, bb = a + 1, c = a + 2, d = a + 3;
                tri.Add(a); tri.Add(c); tri.Add(bb);
                tri.Add(bb); tri.Add(c); tri.Add(d);
            }
            if (top && r1 > 0.001f)
            {
                int center = v.Count;
                v.Add(b + Vector3.up * height); n.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f)); col.Add(color);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(b.x + Mathf.Sin(a) * r1, b.y + height, b.z + Mathf.Cos(a) * r1)); n.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f)); col.Add(color);
                }
                for (int i = 0; i < seg; i++) { tri.Add(center); tri.Add(center + 1 + i); tri.Add(center + 2 + i); }
            }
        }

        /// <summary>Низкополигональная «сфера» (крона дерева): октаэдр, подразделённый один раз, со сдвигом шума.</summary>
        public void Blob(Vector3 c, Vector3 r, int seed)
        {
            Vector3[] o = { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
            int[,] f = { { 0, 1, 2 }, { 0, 2, 3 }, { 0, 3, 4 }, { 0, 4, 1 }, { 5, 2, 1 }, { 5, 3, 2 }, { 5, 4, 3 }, { 5, 1, 4 } };
            var rng = new Rng(seed);
            for (int k = 0; k < 8; k++)
            {
                Vector3 a = o[f[k, 0]], b = o[f[k, 1]], cc = o[f[k, 2]];
                Vector3 ab = (a + b).normalized, bc = (b + cc).normalized, ca = (cc + a).normalized;
                Vector3[][] sub = { new[] { a, ab, ca }, new[] { ab, b, bc }, new[] { ca, bc, cc }, new[] { ab, bc, ca } };
                foreach (var t in sub)
                {
                    int i = v.Count;
                    for (int q = 0; q < 3; q++)
                    {
                        var d = t[q];
                        float j = 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(d.x * 7.1f + d.y * 3.3f + d.z * 5.7f + seed));
                        v.Add(c + Vector3.Scale(d, r) * j); n.Add(d); uv.Add(new Vector2(d.x * 0.5f + 0.5f, d.y * 0.5f + 0.5f)); col.Add(color);
                    }
                    tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
                }
            }
            rng.Next();
        }

        public Mesh ToMesh(string name, bool keepReadable = false)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(col);
            m.SetTriangles(tri, 0, true);
            m.RecalculateBounds();
            if (!keepReadable) m.UploadMeshData(true);
            return m;
        }

        /// <summary>Создать объект с мешем. collider — добавить MeshCollider (меш тогда остаётся читаемым).</summary>
        public GameObject ToObject(string name, Material mat, Transform parent, bool shadows = true, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            var mesh = ToMesh(name, collider);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
    }
}
