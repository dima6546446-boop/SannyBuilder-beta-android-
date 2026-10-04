using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    /// <summary>Один уровень детализации: меш с подмешами и имена их материалов (paint, glass, chrome…).</summary>
    public class CarLod
    {
        public string name;
        public Mesh mesh;
        public string[] materials;
        public int triangles;
        public bool hasPlates;
        public Mesh flat; // LOD0/LOD1: все подмеши в одном, UV → ячейка палитры (1 материал, 1 draw call)
        public Vector3 plateFront, plateRear; // центры номерных плашек
    }

    /// <summary>
    /// Модели машин из Blender (Resources/Cars/&lt;id&gt;.bytes, gzip; см. tools/blender/export_unity.py).
    /// Уровни: LOD_ultra (субдив 3, «Высокое» качество), LOD_hi (машина игрока), LOD0 и LOD1 (трафик).
    /// </summary>
    public static class CarMeshLibrary
    {
        static readonly Dictionary<string, Dictionary<string, CarLod>> cache = new Dictionary<string, Dictionary<string, CarLod>>();

        public static string ModelKey(CarDef def) { return def.id; }

        public static CarLod Get(string id, string lod)
        {
            Dictionary<string, CarLod> lods;
            if (!cache.TryGetValue(id, out lods))
            {
                lods = Load(id);
                cache[id] = lods;
            }
            CarLod l;
            if (lods != null && lods.TryGetValue(lod, out l)) return l;
            if (lods != null && lod == "LOD_ultra" && lods.TryGetValue("LOD_hi", out l)) return l;
            return null;
        }

        static Dictionary<string, CarLod> Load(string id)
        {
            var asset = Resources.Load<TextAsset>("Cars/" + id);
            if (asset == null) { Debug.LogWarning("Нет модели Cars/" + id); return null; }
            byte[] raw = asset.bytes;
            if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b)
            {
                using (var gz = new GZipStream(new MemoryStream(raw), CompressionMode.Decompress))
                using (var ms = new MemoryStream())
                {
                    gz.CopyTo(ms);
                    raw = ms.ToArray();
                }
            }
            Resources.UnloadAsset(asset);
            var res = new Dictionary<string, CarLod>();
            using (var br = new BinaryReader(new MemoryStream(raw)))
            {
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "CDM1") { Debug.LogError("Плохой формат " + id); return null; }
                int lodCount = br.ReadInt32();
                for (int li = 0; li < lodCount; li++)
                {
                    string name = Str(br);
                    int vc = br.ReadInt32();
                    var pos = new Vector3[vc]; var nrm = new Vector3[vc]; var uv = new Vector2[vc];
                    for (int i = 0; i < vc; i++) pos[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < vc; i++) nrm[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < vc; i++) uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    int sc = br.ReadInt32();
                    var mesh = new Mesh { name = id + "_" + name };
                    if (vc > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = pos; mesh.normals = nrm; mesh.uv = uv;
                    mesh.subMeshCount = sc;
                    var mats = new string[sc];
                    int tris = 0;
                    Vector3 pf = Vector3.zero, pr = Vector3.zero;
                    bool hasPlates = false;
                    for (int s = 0; s < sc; s++)
                    {
                        mats[s] = Str(br);
                        int ic = br.ReadInt32();
                        var idx = new int[ic];
                        for (int i = 0; i < ic; i++) idx[i] = br.ReadInt32();
                        mesh.SetTriangles(idx, s, false);
                        tris += ic / 3;
                        if (mats[s] == "plate") PlateCenters(pos, idx, ref pf, ref pr, ref hasPlates);
                    }
                    mesh.RecalculateBounds();
                    Mesh flat = null;
                    if (name == "LOD0" || name == "LOD1") flat = Flatten(mesh, mats, pos, nrm, id + "_" + name + "_flat");
                    mesh.UploadMeshData(true); // данные только на GPU — экономия памяти
                    res[name] = new CarLod { name = name, mesh = mesh, materials = mats, triangles = tris, hasPlates = hasPlates, plateFront = pf, plateRear = pr, flat = flat };
                }
            }
            return res;
        }

        /// <summary>Ячейки палитры (8×1): 0 кузов, 1 стекло, 2 хром, 3 чёрный пластик, 4 резина/днище, 5 номер, 6 фары, 7 фонари.</summary>
        public static int PaletteCell(string mat)
        {
            switch (mat)
            {
                case "paint": return 0;
                case "glass": return 1;
                case "chrome": case "trim": return 2;
                case "rubber": case "under": return 4;
                case "plate": return 5;
                case "headLamp": case "reverseLamp": return 6;
                case "tailLamp": case "indL": case "indR": return 7;
                default: return 3;
            }
        }

        static Mesh Flatten(Mesh src, string[] mats, Vector3[] pos, Vector3[] nrm, string name)
        {
            // вершины подмешей с разными материалами могут совпадать по индексу — дублируем по подмешам
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var idx = src.GetTriangles(s);
                var remap = new Dictionary<int, int>();
                var cellUv = new Vector2((PaletteCell(mats[s]) + 0.5f) / 8f, 0.5f);
                foreach (int i in idx)
                {
                    int k;
                    if (!remap.TryGetValue(i, out k)) { k = v.Count; remap[i] = k; v.Add(pos[i]); n.Add(nrm[i]); uv.Add(cellUv); }
                    tri.Add(k);
                }
            }
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0, true);
            m.UploadMeshData(true);
            return m;
        }

        static readonly Dictionary<int, Material> paletteMats = new Dictionary<int, Material>();

        /// <summary>Материал трафика для цвета кузова: палитра 8×1 (общий на все машины этого цвета).</summary>
        public static Material PaletteMaterial(int color)
        {
            Material m;
            if (paletteMats.TryGetValue(color, out m)) return m;
            var t = new Texture2D(8, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "pal" + color };
            t.SetPixels32(new[] {
                (Color32)M.Hex(color), new Color32(14, 20, 26, 255), new Color32(205, 205, 205, 255), new Color32(28, 28, 28, 255),
                new Color32(16, 16, 16, 255), new Color32(236, 236, 230, 255), new Color32(225, 225, 215, 255), new Color32(120, 14, 10, 255),
            });
            t.Apply(false, true);
            m = Mats.Lit().Tex(t).Pbr(0.35f, 0.62f);
            m.name = "Traffic_" + color.ToString("x6");
            m.enableInstancing = true;
            paletteMats[color] = m;
            return m;
        }

        static void PlateCenters(Vector3[] pos, int[] idx, ref Vector3 front, ref Vector3 rear, ref bool ok)
        {
            Vector3 f = Vector3.zero, r = Vector3.zero; int nf = 0, nr = 0;
            foreach (int i in idx) { var p = pos[i]; if (p.z > 0) { f += p; nf++; } else { r += p; nr++; } }
            if (nf > 0) front = f / nf;
            if (nr > 0) rear = r / nr;
            ok = nf > 0 || nr > 0;
        }

        static string Str(BinaryReader br)
        {
            int n = br.ReadInt32();
            return Encoding.UTF8.GetString(br.ReadBytes(n));
        }
    }
}
