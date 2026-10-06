using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Рукава и штанины спортивного костюма как «оболочка»: копия треугольников рук и ног самой модели,
    /// чуть раздутая по нормалям и с теми же весами скиннинга — гнётся вместе с кожей (локти, колени, плечи),
    /// в отличие от жёстких цилиндров на костях. Три белые полоски рисует текстура по UV: UV считаются по геометрии —
    /// «снаружи конечности» и «боком вдоль неё», поэтому полоски идут по внешней стороне рук и ног.
    /// </summary>
    public static class TracksuitShell
    {
        const int Jacket = 0x181a22, White = 0xf4f4f4;
        const float Win = 0.09f;            // окно текстуры по ширине, м
        static Texture2D tex;

        static Texture2D StripeTex(float w, float gap)
        {
            if (tex != null) return tex;
            const int TW = 128, TH = 32;
            tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SuitStripes" };
            var px = new Color32[TW * TH];
            var jc = new Color32((byte)((Jacket >> 16) & 255), (byte)((Jacket >> 8) & 255), (byte)(Jacket & 255), 255);
            var wc = new Color32((byte)((White >> 16) & 255), (byte)((White >> 8) & 255), (byte)(White & 255), 255);
            for (int y = 0; y < TH; y++)
                for (int x = 0; x < TW; x++)
                {
                    float lat = ((x + 0.5f) / TW - 0.5f) * Win;
                    float outw = ((y + 0.5f) / TH) * 2f - 1f;
                    bool s = false;
                    for (int k = -1; k <= 1; k++) if (Mathf.Abs(lat - k * gap) < w * 0.5f) s = true;
                    px[y * TW + x] = (s && outw > 0.25f) ? wc : jc;
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return tex;
        }

        struct Limb { public Transform a, b; public int sign; }

        /// <param name="rig">персонаж (меш читаемый, после Smooth)</param>
        /// <param name="cur">вершины тела в осях корня в текущей позе (порядок как у меша)</param>
        /// <param name="owner">кость-владелец каждой вершины (индекс в smr.bones)</param>
        public static SkinnedMeshRenderer Build(HumanRig rig, List<Vector3> cur, int[] owner, float inflate)
        {
            var smr = rig.smr; var mesh = smr.sharedMesh;
            if (mesh == null || !mesh.isReadable) return null;
            var V = mesh.vertices; var N = mesh.normals; var W = mesh.boneWeights;
            if (V.Length != cur.Count || W.Length != V.Length || N.Length != V.Length) return null;
            var bones = smr.bones;

            // конечности: кость → (от, до)
            var hips = rig.root.InverseTransformPoint(rig.B["hips"].position);
            var limbOf = new Dictionary<Transform, Limb>();
            string[][] pairs = { new[]{"arm","fore"}, new[]{"fore","hand"}, new[]{"thigh","shin"}, new[]{"shin","foot"} };
            foreach (var sd in new[] { "L", "R" })
                foreach (var p in pairs)
                {
                    var a = rig.B[p[0] + sd]; var b = rig.B[p[1] + sd];
                    float mx = rig.root.InverseTransformPoint((a.position + b.position) * 0.5f).x - hips.x;
                    var lm = new Limb { a = a, b = b, sign = mx >= 0 ? 1 : -1 };
                    limbOf[a] = lm;
                    if (p[0] == "arm" && a.parent != null && a.parent.name.IndexOf("Shoulder", System.StringComparison.OrdinalIgnoreCase) >= 0 && !limbOf.ContainsKey(a.parent)) limbOf[a.parent] = lm;   // ключица: закрывает плечо
                }
            var vLimb = new int[V.Length]; var limbs = new List<Limb>();
            var idx = new Dictionary<Transform, int>();
            for (int i = 0; i < V.Length; i++)
            {
                vLimb[i] = -1;
                int o = owner[i]; if (o < 0 || o >= bones.Length) continue;
                Limb l;
                if (!limbOf.TryGetValue(bones[o], out l)) continue;
                int li;
                if (!idx.TryGetValue(bones[o], out li)) { li = limbs.Count; limbs.Add(l); idx[bones[o]] = li; }
                vLimb[i] = li;
            }

            // масштаб «единица меша → метр»: по средней длине рёбер
            float sumM = 0, sumW = 0; int cnt = 0;
            var tri0 = mesh.triangles;
            for (int i = 0; i + 2 < tri0.Length && cnt < 400; i += 3)
            {
                int x = tri0[i], y = tri0[i + 1];
                if (vLimb[x] < 0 || vLimb[y] < 0) continue;
                sumM += (V[x] - V[y]).magnitude; sumW += (cur[x] - cur[y]).magnitude; cnt++;
            }
            float scale = sumM > 1e-6f ? sumW / sumM : 1f;     // метров на единицу меша
            float infl = inflate / Mathf.Max(scale, 1e-4f);

            // вершины оболочки
            var map = new int[V.Length]; for (int i = 0; i < map.Length; i++) map[i] = -1;
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nw = new List<BoneWeight>(); var nt = new List<int>();
            System.Func<int, int> take = i =>
            {
                if (map[i] >= 0) return map[i];
                var l = limbs[vLimb[i]];
                var a = rig.root.InverseTransformPoint(l.a.position); var b = rig.root.InverseTransformPoint(l.b.position);
                var d = b - a; float len = d.magnitude; d = len > 1e-5f ? d / len : Vector3.down;
                var c = cur[i];
                var off = c - (a + d * Vector3.Dot(c - a, d));
                var o = new Vector3(l.sign, 0, 0); o = (o - d * Vector3.Dot(o, d)).normalized;
                var bi = Vector3.Cross(d, o);
                float r = Mathf.Max(off.magnitude, 1e-4f);
                float u = Vector3.Dot(off, o) / r, w = Vector3.Dot(off, bi);
                map[i] = nv.Count;
                nv.Add(V[i] + N[i].normalized * infl);
                nn.Add(N[i]);
                nuv.Add(new Vector2(w / Win + 0.5f, u * 0.5f + 0.5f));
                nw.Add(W[i]);
                return map[i];
            };
            for (int i = 0; i + 2 < tri0.Length; i += 3)
            {
                int x = tri0[i], y = tri0[i + 1], z = tri0[i + 2];
                if (vLimb[x] < 0 || vLimb[y] < 0 || vLimb[z] < 0) continue;
                nt.Add(take(x)); nt.Add(take(y)); nt.Add(take(z));
            }
            if (nt.Count == 0) return null;

            var m = new Mesh { name = "SuitShell" };
            m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, nuv); m.SetTriangles(nt, 0);
            m.boneWeights = nw.ToArray(); m.bindposes = mesh.bindposes;
            m.bounds = mesh.bounds;
            var go = new GameObject("Suit");
            go.transform.SetParent(smr.transform.parent, false);
            go.transform.localPosition = smr.transform.localPosition; go.transform.localRotation = smr.transform.localRotation; go.transform.localScale = smr.transform.localScale;
            go.layer = smr.gameObject.layer;
            var r2 = go.AddComponent<SkinnedMeshRenderer>();
            r2.sharedMesh = m; r2.bones = bones; r2.rootBone = smr.rootBone;
            r2.sharedMaterial = Mats.Simple().Tex(StripeTex(0.012f, 0.021f)).Col(Color.white);
            r2.localBounds = smr.localBounds; r2.updateWhenOffscreen = false;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r2;
        }
    }
}
