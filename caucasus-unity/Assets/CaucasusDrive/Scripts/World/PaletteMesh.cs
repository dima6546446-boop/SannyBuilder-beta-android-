using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Меш «из примитивов с цветом»: коробки, скруглённые коробки, цилиндры, сферы, капсулы, торы, тела вращения.
    /// Цвет кодируется UV-координатой в общей палитре 32×32 (стандартные шейдеры URP не умеют цвет вершин),
    /// поэтому все персонажи, салоны и ларьки рисуются одним материалом (SRP Batcher). Для скиннинга каждая
    /// вершина привязывается к одной кости (bone). Ориентация треугольников выставляется по нормали автоматически.
    /// Координаты — Unity (+X вправо, +Y вверх, +Z вперёд).
    /// </summary>
    public class PaletteMesh
    {
        // ------------------------------------------------------------------ общая палитра
        const int PS = 32;
        static Texture2D palette;
        static readonly Dictionary<int, int> slots = new Dictionary<int, int>();
        static Color32[] palPx;
        static bool dirty;
        static Material litMat, emisMat;

        static Vector2 UV(int hex)
        {
            int s;
            if (!slots.TryGetValue(hex, out s))
            {
                s = slots.Count < PS * PS ? slots.Count : PS * PS - 1;
                slots[hex] = s;
                if (palPx == null) palPx = new Color32[PS * PS];
                palPx[s] = new Color32((byte)((hex >> 16) & 255), (byte)((hex >> 8) & 255), (byte)(hex & 255), 255);
                dirty = true;
            }
            return new Vector2((s % PS + 0.5f) / PS, (s / PS + 0.5f) / PS);
        }

        public static Texture2D Palette
        {
            get
            {
                if (palette == null)
                {
                    palette = new Texture2D(PS, PS, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Palette" };
                    if (palPx == null) palPx = new Color32[PS * PS];
                    dirty = true;
                }
                if (dirty) { palette.SetPixels32(palPx); palette.Apply(false, false); dirty = false; }
                return palette;
            }
        }

        /// <summary>Общий материал (Simple Lit + палитра).</summary>
        public static Material Material { get { var p = Palette; if (litMat == null) litMat = Mats.Simple().Tex(p); return litMat; } }

        /// <summary>Материал с подсветкой по цвету палитры (салон: в нём нет прямого солнца).</summary>
        public static Material FillMaterial { get { var p = Palette; if (emisMat == null) emisMat = Mats.SimpleEmissive().Tex(p).EmissionMap(p).Emission(Color.gray * 0.35f); return emisMat; } }

        // ------------------------------------------------------------------ данные
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();
        readonly List<int> bone = new List<int>();
        public Matrix4x4 xf = Matrix4x4.identity;   // текущее преобразование для добавляемых примитивов
        public int curBone;

        public int Count => v.Count;

        int V(Vector3 p, Vector3 nn, Vector2 c)
        {
            v.Add(xf.MultiplyPoint3x4(p));
            n.Add(xf.MultiplyVector(nn).normalized);
            uv.Add(c);
            bone.Add(curBone);
            return v.Count - 1;
        }

        void Tri(int a, int b, int c)
        {
            var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(fn, n[a] + n[b] + n[c]) >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
            else { t.Add(a); t.Add(c); t.Add(b); }
        }

        /// <summary>Поворот для деталей, заданных в веб-версии (Three.js): Эйлер XYZ, зеркало по X.</summary>
        public static Quaternion Q3(float rx, float ry, float rz)
        {
            return Quaternion.AngleAxis(rx * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(-ry * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(-rz * Mathf.Rad2Deg, Vector3.forward);
        }

        /// <summary>Поставить локальное преобразование детали (позиция, поворот) поверх базового.</summary>
        public PaletteMesh At(Matrix4x4 baseM, Vector3 pos, Quaternion rot, Vector3? scale = null)
        {
            xf = baseM * Matrix4x4.TRS(pos, rot, scale ?? Vector3.one);
            return this;
        }

        // ------------------------------------------------------------------ примитивы (в локальных координатах xf)
        public void Box(Vector3 c, Vector3 s, int hex)
        {
            var col = UV(hex);
            Vector3 h = s * 0.5f;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                Vector3 a = d.x != 0 ? Vector3.up : Vector3.right, b = Vector3.Cross(d, a);
                Vector3 fc = c + Vector3.Scale(d, h);
                Vector3 ea = Vector3.Scale(a, h), eb = Vector3.Scale(b, h);
                int i0 = V(fc - ea - eb, d, col), i1 = V(fc + ea - eb, d, col), i2 = V(fc + ea + eb, d, col), i3 = V(fc - ea + eb, d, col);
                Tri(i0, i1, i2); Tri(i0, i2, i3);
            }
        }

        /// <summary>Скруглённая коробка: коробка со срезанными рёбрами (8 граней-фасок), выглядит мягко при малом числе вершин.</summary>
        public void RBox(Vector3 c, Vector3 s, float r, int hex)
        {
            r = Mathf.Min(r, s.x / 2, s.y / 2, s.z / 2) * 0.99f;
            if (r < 0.004f) { Box(c, s, hex); return; }
            // ядро-«крест» из трёх коробок + фаски по 12 рёбрам (цилиндры по 4 сегмента)
            Box(c, new Vector3(s.x, s.y - 2 * r, s.z - 2 * r), hex);
            Box(c, new Vector3(s.x - 2 * r, s.y, s.z - 2 * r), hex);
            Box(c, new Vector3(s.x - 2 * r, s.y - 2 * r, s.z), hex);
            var h = s * 0.5f - Vector3.one * r;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    EdgeCyl(c + new Vector3(sx * h.x, sy * h.y, 0), r, s.z - 2 * r, 2, hex, new Vector2(sx, sy));
                    EdgeCyl(c + new Vector3(sx * h.x, 0, sy * h.z), r, s.y - 2 * r, 1, hex, new Vector2(sx, sy));
                    EdgeCyl(c + new Vector3(0, sx * h.y, sy * h.z), r, s.x - 2 * r, 0, hex, new Vector2(sx, sy));
                }
        }

        /// <summary>Четверть цилиндра по ребру (ось axis: 0 — X, 1 — Y, 2 — Z), квадрант задан знаками q.</summary>
        void EdgeCyl(Vector3 c, float r, float len, int axis, int hex, Vector2 q)
        {
            var col = UV(hex);
            const int seg = 3;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI / 2 / seg, a1 = (k + 1) * Mathf.PI / 2 / seg;
                Vector3 d0 = Dir2(axis, q.x * Mathf.Cos(a0), q.y * Mathf.Sin(a0)), d1 = Dir2(axis, q.x * Mathf.Cos(a1), q.y * Mathf.Sin(a1));
                Vector3 ax = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                Vector3 e = ax * (len / 2);
                int i0 = V(c + d0 * r - e, d0, col), i1 = V(c + d1 * r - e, d1, col), i2 = V(c + d1 * r + e, d1, col), i3 = V(c + d0 * r + e, d0, col);
                Tri(i0, i1, i2); Tri(i0, i2, i3);
            }
        }

        static Vector3 Dir2(int axis, float a, float b)
        {
            // два направления, перпендикулярные оси (для X: Y,Z; для Y: X,Z; для Z: X,Y)
            switch (axis) { case 0: return new Vector3(0, a, b); case 1: return new Vector3(a, 0, b); default: return new Vector3(a, b, 0); }
        }

        /// <summary>Цилиндр вдоль Y (как в Three.js): радиусы низа/верха, высота, центр c.</summary>
        public void Cyl(Vector3 c, float rTop, float rBot, float h, int hex, int seg = 10, bool caps = true)
        {
            var col = UV(hex);
            float y0 = c.y - h / 2, y1 = c.y + h / 2;
            float slope = (rBot - rTop) / Mathf.Max(1e-4f, h);
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2 / seg, a1 = (k + 1) * Mathf.PI * 2 / seg;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                Vector3 n0 = (d0 + Vector3.up * slope).normalized, n1 = (d1 + Vector3.up * slope).normalized;
                int i0 = V(new Vector3(c.x, y0, c.z) + d0 * rBot, n0, col), i1 = V(new Vector3(c.x, y0, c.z) + d1 * rBot, n1, col);
                int i2 = V(new Vector3(c.x, y1, c.z) + d1 * rTop, n1, col), i3 = V(new Vector3(c.x, y1, c.z) + d0 * rTop, n0, col);
                Tri(i0, i1, i2); Tri(i0, i2, i3);
                if (caps)
                {
                    if (rTop > 1e-4f) Tri(V(new Vector3(c.x, y1, c.z), Vector3.up, col), V(new Vector3(c.x, y1, c.z) + d0 * rTop, Vector3.up, col), V(new Vector3(c.x, y1, c.z) + d1 * rTop, Vector3.up, col));
                    if (rBot > 1e-4f) Tri(V(new Vector3(c.x, y0, c.z), Vector3.down, col), V(new Vector3(c.x, y0, c.z) + d1 * rBot, Vector3.down, col), V(new Vector3(c.x, y0, c.z) + d0 * rBot, Vector3.down, col));
                }
            }
        }

        /// <summary>Сфера (эллипсоид при scale): ws × hs сегментов; полусфера при half.</summary>
        public void Sphere(Vector3 c, float r, int hex, int ws = 8, int hs = 6, Vector3? scale = null, bool half = false)
        {
            var col = UV(hex);
            var sc = scale ?? Vector3.one;
            int rows = hs;
            float thetaMax = half ? Mathf.PI / 2 : Mathf.PI;
            int[,] idx = new int[rows + 1, ws + 1];
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= ws; x++)
                {
                    float th = y * thetaMax / rows, ph = x * Mathf.PI * 2 / ws;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    var p = c + Vector3.Scale(d * r, sc);
                    var nn = new Vector3(d.x / sc.x, d.y / sc.y, d.z / sc.z).normalized;
                    idx[y, x] = V(p, nn, col);
                }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < ws; x++)
                {
                    int a = idx[y, x], b = idx[y, x + 1], cc = idx[y + 1, x + 1], d = idx[y + 1, x];
                    if (y > 0) Tri(a, b, cc);
                    if (y < rows - 1 || half) Tri(a, cc, d);
                }
        }

        /// <summary>Капсула вдоль Y: радиус r, длина цилиндрической части len.</summary>
        public void Capsule(Vector3 c, float r, float len, int hex, int rs = 7)
        {
            var col = UV(hex);
            int hs = 3;
            int rows = hs * 2 + 1;
            var idx = new int[rows + 1, rs + 1];
            for (int y = 0; y <= rows; y++)
            {
                float th; float yOff;
                if (y <= hs) { th = y * (Mathf.PI / 2) / hs; yOff = len / 2; }
                else { th = Mathf.PI / 2 + (y - hs - 1) * (Mathf.PI / 2) / hs; yOff = -len / 2; }
                for (int x = 0; x <= rs; x++)
                {
                    float ph = x * Mathf.PI * 2 / rs;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    idx[y, x] = V(c + d * r + Vector3.up * yOff, d, col);
                }
            }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < rs; x++)
                {
                    int a = idx[y, x], b = idx[y, x + 1], cc = idx[y + 1, x + 1], d = idx[y + 1, x];
                    if (y > 0) Tri(a, b, cc);
                    if (y < rows - 1) Tri(a, cc, d);
                }
        }

        /// <summary>Тор в плоскости XY (ось — Z), как TorusGeometry.</summary>
        public void Torus(Vector3 c, float R, float r, int hex, int rs = 8, int ts = 36)
        {
            var col = UV(hex);
            var idx = new int[ts + 1, rs + 1];
            for (int i = 0; i <= ts; i++)
            {
                float u = i * Mathf.PI * 2 / ts;
                var cu = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0);
                for (int j = 0; j <= rs; j++)
                {
                    float w = j * Mathf.PI * 2 / rs;
                    var nn = cu * Mathf.Cos(w) + Vector3.forward * Mathf.Sin(w);
                    idx[i, j] = V(c + cu * R + nn * r, nn, col);
                }
            }
            for (int i = 0; i < ts; i++)
                for (int j = 0; j < rs; j++) { Tri(idx[i, j], idx[i + 1, j], idx[i + 1, j + 1]); Tri(idx[i, j], idx[i + 1, j + 1], idx[i, j + 1]); }
        }

        /// <summary>Тело вращения вокруг Y по профилю (радиус, высота); depth — сплющивание по Z.</summary>
        public void Lathe(Vector3 c, Vector2[] prof, int seg, float depth, int hex)
        {
            var col = UV(hex);
            var idx = new int[prof.Length, seg + 1];
            for (int i = 0; i < prof.Length; i++)
            {
                Vector2 dp = i + 1 < prof.Length ? prof[i + 1] - prof[i] : prof[i] - prof[i - 1];
                for (int k = 0; k <= seg; k++)
                {
                    float a = k * Mathf.PI * 2 / seg;
                    var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    var nn = new Vector3(d.x * dp.y, -dp.x, d.z * dp.y / depth).normalized;
                    idx[i, k] = V(c + new Vector3(d.x * prof[i].x, prof[i].y, d.z * prof[i].x * depth), nn, col);
                }
            }
            for (int i = 0; i + 1 < prof.Length; i++)
                for (int k = 0; k < seg; k++) { Tri(idx[i, k], idx[i, k + 1], idx[i + 1, k + 1]); Tri(idx[i, k], idx[i + 1, k + 1], idx[i + 1, k]); }
        }

        /// <summary>Произвольный четырёхугольник (обшивка двери и т. п.) с нормалью nn.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nn, int hex)
        {
            var col = UV(hex);
            int i0 = V(a, nn, col), i1 = V(b, nn, col), i2 = V(c, nn, col), i3 = V(d, nn, col);
            Tri(i0, i1, i2); Tri(i0, i2, i3);
        }

        /// <summary>Поверхность по сечениям: rings — замкнутые контуры одинакового размера; нормали сглаженные; торцы закрываются.</summary>
        public void Loft(List<Vector3[]> rings, int hex, bool capStart = true, bool capEnd = true)
        {
            var col = UV(hex);
            int R = rings.Count, M = rings[0].Length;
            var idx = new int[R][];
            var centers = new Vector3[R];
            for (int i = 0; i < R; i++) { var c = Vector3.zero; foreach (var q in rings[i]) c += q; centers[i] = c / M; }
            for (int i = 0; i < R; i++)
            {
                idx[i] = new int[M];
                for (int j = 0; j < M; j++)
                {
                    var p = rings[i][j];
                    var tj = rings[i][(j + 1) % M] - rings[i][(j + M - 1) % M];
                    var ti = rings[Mathf.Min(i + 1, R - 1)][j] - rings[Mathf.Max(i - 1, 0)][j];
                    var nn = Vector3.Cross(tj, ti);
                    if (nn.sqrMagnitude < 1e-12f) nn = p - centers[i];
                    nn.Normalize();
                    if (Vector3.Dot(nn, p - centers[i]) < 0f) nn = -nn;
                    idx[i][j] = V(p, nn, col);
                }
            }
            for (int i = 0; i < R - 1; i++)
                for (int j = 0; j < M; j++)
                {
                    int a0 = idx[i][j], b0 = idx[i][(j + 1) % M], c0 = idx[i + 1][(j + 1) % M], d0 = idx[i + 1][j];
                    Tri(a0, b0, c0); Tri(a0, c0, d0);
                }
            for (int e = 0; e < 2; e++)
            {
                if ((e == 0 && !capStart) || (e == 1 && !capEnd)) continue;
                int i = e == 0 ? 0 : R - 1;
                var axis = (e == 0 ? centers[0] - centers[Mathf.Min(1, R - 1)] : centers[R - 1] - centers[Mathf.Max(R - 2, 0)]).normalized;
                int cv = V(centers[i], axis, col);
                for (int j = 0; j < M; j++)
                {
                    int p0 = V(rings[i][j], axis, col), p1 = V(rings[i][(j + 1) % M], axis, col);
                    Tri(cv, p0, p1);
                }
            }
        }

        /// <summary>Wavefront OBJ с цветами вершин (для проверки геометрии вне Unity).</summary>
        public string ToObj()
        {
            var inv = new Dictionary<Vector2, int>();
            foreach (var kv in slots) inv[UV(kv.Key)] = kv.Key;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < v.Count; i++)
            {
                int hex; if (!inv.TryGetValue(uv[i], out hex)) hex = 0x808080;
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "v {0} {1} {2} {3} {4} {5}\n", v[i].x, v[i].y, v[i].z, ((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
            }
            for (int i = 0; i + 2 < t.Count; i += 3) sb.AppendFormat("f {0} {1} {2}\n", t[i] + 1, t[i + 1] + 1, t[i + 2] + 1);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ сборка
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        public GameObject ToObject(string name, Transform parent, Material mat = null, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat ?? Material;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Скиннинг: каждая вершина целиком принадлежит своей кости (жёсткая привязка).</summary>
        public BoneWeight[] Weights()
        {
            var w = new BoneWeight[bone.Count];
            for (int i = 0; i < w.Length; i++) w[i] = new BoneWeight { boneIndex0 = bone[i], weight0 = 1f };
            return w;
        }
    }
}
