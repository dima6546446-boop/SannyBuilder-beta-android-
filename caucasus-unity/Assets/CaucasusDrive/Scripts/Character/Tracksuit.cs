using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// «Пацан с района»: спортивный костюм с тремя белыми полосками на рукавах и штанинах, кепка с козырьком,
    /// воротник-стойка, молния, манжеты, кроссовки. Все детали — маленькие меши на костях модели, их размеры
    /// подгоняются под тело: радиус рук/ног и глубина груди измеряются по реальной сетке в позе покоя.
    /// Логотипов брендов нет — только полоски.
    /// </summary>
    public static class Tracksuit
    {
        const int White = 0xf4f4f4, Jacket = 0x181a22, JacketLight = 0x2a2d3a, CapC = 0x14161a;
        static List<Vector3> verts;     // вершины тела в осях корня персонажа
        static int[] owner;             // индекс кости-владельца каждой вершины (по весам скиннинга)
        static Transform[] skinBones;
        static HumanRig rig;

        public static void Apply(HumanRig r)
        {
            rig = r;
            var baked = new Mesh();
            r.smr.BakeMesh(baked, true);
            var src = baked.vertices;
            verts = new List<Vector3>(src.Length);
            foreach (var v in src) verts.Add(r.root.InverseTransformPoint(r.smr.transform.TransformPoint(v)));
            Object.Destroy(baked);
            var bw = r.smr.sharedMesh.boneWeights; skinBones = r.smr.bones;
            owner = new int[verts.Count];
            for (int i = 0; i < owner.Length; i++) owner[i] = i < bw.Length ? bw[i].boneIndex0 : -1;

            Limb("thighL", "shinL", 0.08f, 0.96f, true); Limb("thighR", "shinR", 0.08f, 0.96f, true);
            Limb("shinL", "footL", 0.04f, 0.8f, true); Limb("shinR", "footR", 0.04f, 0.8f, true);
            Limb("armL", "foreL", 0.1f, 0.95f, false); Limb("armR", "foreR", 0.1f, 0.95f, false);
            Limb("foreL", "handL", 0.04f, 0.8f, false); Limb("foreR", "handR", 0.04f, 0.8f, false);
            Cuff("shinL", "footL", 0.86f); Cuff("shinR", "footR", 0.86f);
            Cuff("foreL", "handL", 0.84f); Cuff("foreR", "handR", 0.84f);
            Collar(); Zip(); Cap();
            verts = null; rig = null; owner = null; skinBones = null;
        }

        // ------------------------------------------------------------------ измерения
        static float Median(List<float> a) { a.Sort(); return a[a.Count / 2]; }

        /// <summary>Радиус конечности между точками a и b (в осях корня) — медиана расстояний до оси.</summary>
        static bool Owns(int vi, Transform b1, Transform b2)
        {
            int o = owner[vi];
            return o >= 0 && o < skinBones.Length && (skinBones[o] == b1 || skinBones[o] == b2);
        }

        static float Radius(Vector3 a, Vector3 b, float t0, float t1, float fallback, Transform b1 = null, Transform b2 = null)
        {
            var d = b - a; float len = d.magnitude; if (len < 1e-4f) return fallback; d /= len;
            var list = new List<float>();
            for (int vi = 0; vi < verts.Count; vi++)
            {
                var v = verts[vi];
                if (b1 != null && !Owns(vi, b1, b2)) continue;                    // только вершины этой конечности
                float t = Vector3.Dot(v - a, d) / len;
                if (t < t0 || t > t1) continue;
                float dist = (v - (a + d * (t * len))).magnitude;
                if (dist < 0.22f) list.Add(dist);
            }
            return list.Count > 4 ? Median(list) : fallback;
        }

        /// <summary>Передняя поверхность тела на высоте y (в осях корня): max z вершин у центральной линии.</summary>
        static float FrontZ(float y, float halfWidth = 0.05f, float fallback = 0.1f)
        {
            float best = -9f;
            foreach (var v in verts) if (Mathf.Abs(v.y - y) < 0.04f && Mathf.Abs(v.x) < halfWidth) best = Mathf.Max(best, v.z);
            return best > -8f ? best : fallback;
        }

        static GameObject Anchor(Transform bone, Vector3 localPos, Quaternion localRot, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bone, true);
            go.transform.SetPositionAndRotation(rig.root.TransformPoint(localPos), rig.root.rotation * localRot);
            return go;
        }

        // ------------------------------------------------------------------ детали
        /// <summary>Три белые полоски вдоль конечности по внешней стороне.</summary>
        static void Limb(string from, string to, float t0, float t1, bool leg)
        {
            var bone = rig.B[from];
            var a = rig.root.InverseTransformPoint(bone.position); var b = rig.root.InverseTransformPoint(rig.B[to].position);
            float r = Radius(a, b, 0.3f, 0.7f, leg ? 0.07f : 0.045f, bone, rig.B[to]);
            var dir = (b - a).normalized; float len = (b - a).magnitude;
            var hips = rig.root.InverseTransformPoint(rig.B["hips"].position);
            float side = Mathf.Sign(((a + b) * 0.5f - hips).x == 0 ? 1f : ((a + b) * 0.5f - hips).x);
            var n = new Vector3(side, 0, 0);
            n = (n - dir * Vector3.Dot(n, dir)).normalized;
            var mid = a + dir * (len * (t0 + t1) * 0.5f);
            if (!leg)
            {
                // длинный рукав поверх голой руки футболки (плечо → кисть)
                var sg = Anchor(bone, a + dir * (len * 0.5f), Quaternion.FromToRotation(Vector3.up, dir), "Sleeve_" + from);
                var sp = new PaletteMesh();
                float st = from.StartsWith("arm") ? 0.02f : 0f, en = from.StartsWith("arm") ? 1.02f : 0.9f;
                sp.Cyl(new Vector3(0, len * ((st + en) * 0.5f - 0.5f), 0), r + 0.008f, r + 0.008f, len * (en - st), Jacket, 10, false);
                if (from.StartsWith("arm")) sp.Sphere(new Vector3(0, len * 0.5f, 0), r + 0.008f, Jacket, 10, 6);   // локоть
                sp.ToObject("Sl", sg.transform);
                r += 0.01f;
            }
            var go = Anchor(bone, mid + n * (r + 0.001f), Quaternion.LookRotation(dir, n), "Stripes_" + from);
            var pm = new PaletteMesh();
            float seg = len * (t1 - t0), w = Mathf.Min(0.011f, r * 0.2f), gap = Mathf.Min(0.019f, r * 0.34f);
            for (int i = -1; i <= 1; i++) pm.Box(new Vector3(i * gap, 0, 0), new Vector3(w, 0.007f, seg), White);
            pm.ToObject("S", go.transform);
        }

        /// <summary>Резинка-манжета на конце рукава/штанины.</summary>
        static void Cuff(string from, string to, float t)
        {
            var bone = rig.B[from];
            var a = rig.root.InverseTransformPoint(bone.position); var b = rig.root.InverseTransformPoint(rig.B[to].position);
            float r = Radius(a, b, 0.55f, 0.85f, 0.045f, bone, rig.B[to]);
            var dir = (b - a).normalized; float len = (b - a).magnitude;
            var go = Anchor(bone, a + dir * (len * t), Quaternion.FromToRotation(Vector3.up, dir), "Cuff_" + from);
            var pm = new PaletteMesh();
            pm.Cyl(Vector3.zero, r + (from.StartsWith("fore") ? 0.014f : 0.008f), r + (from.StartsWith("fore") ? 0.014f : 0.008f), 0.035f, JacketLight, 12);
            pm.ToObject("C", go.transform);
        }

        static void Collar()
        {
            var bone = rig.B["neck"];
            var a = rig.root.InverseTransformPoint(bone.position); var b = rig.root.InverseTransformPoint(rig.B["head"].position);
            float r = Radius(a, b, 0.1f, 0.7f, 0.05f, bone, rig.B["head"]);
            var go = Anchor(bone, a + Vector3.up * 0.01f, Quaternion.identity, "Collar");
            var pm = new PaletteMesh();
            pm.Cyl(new Vector3(0, 0.012f, 0), r + 0.016f, r + 0.026f, 0.06f, Jacket, 14, false);
            pm.Cyl(new Vector3(0, -0.012f, 0), r + 0.03f, r + 0.03f, 0.014f, JacketLight, 14);
            pm.ToObject("C", go.transform);
        }

        /// <summary>Молния по центру груди и живота: верх на кости груди, низ на позвоночнике.</summary>
        static void Zip()
        {
            float yNeck = rig.root.InverseTransformPoint(rig.B["neck"].position).y;
            float yChest = rig.root.InverseTransformPoint(rig.B["chest"].position).y;
            float ySpine = rig.root.InverseTransformPoint(rig.B["spine"].position).y;
            float yHips = rig.root.InverseTransformPoint(rig.B["hips"].position).y;
            Strip("chest", yNeck - 0.01f, ySpine + 0.02f);
            Strip("spine", ySpine + 0.02f, yHips - 0.02f);
            // бегунок и белая кромка воротника
        }

        static void Strip(string boneName, float yTop, float yBot)
        {
            var bone = rig.B[boneName];
            float yMid = (yTop + yBot) * 0.5f, h = Mathf.Abs(yTop - yBot);
            float z = FrontZ(yMid) + 0.002f;
            var go = Anchor(bone, new Vector3(0, yMid, z), Quaternion.identity, "Zip_" + boneName);
            var pm = new PaletteMesh();
            pm.Box(Vector3.zero, new Vector3(0.012f, h, 0.006f), White);
            pm.Box(new Vector3(0, 0, -0.002f), new Vector3(0.026f, h, 0.004f), JacketLight);   // планка
            pm.ToObject("Z", go.transform);
        }

        /// <summary>Кепка: купол, козырёк вперёд, пуговка.</summary>
        static void Cap()
        {
            var head = rig.B["head"];
            var hp = rig.root.InverseTransformPoint(head.position);
            float top = hp.y + 0.2f;
            foreach (var v in verts) if (v.y > hp.y && Vector3.Distance(new Vector3(v.x, 0, v.z), new Vector3(hp.x, 0, hp.z)) < 0.2f) top = Mathf.Max(top, v.y);
            float H = top - hp.y;                                   // высота головы над костью
            // ширина головы на уровне бровей
            float half = 0.08f;
            var wl = new List<float>();
            foreach (var v in verts) if (Mathf.Abs(v.y - (hp.y + H * 0.5f)) < 0.025f && Mathf.Abs(v.z - hp.z) < 0.1f) wl.Add(Mathf.Abs(v.x - hp.x));
            if (wl.Count > 4) { wl.Sort(); half = Mathf.Max(0.06f, wl[Mathf.Clamp((int)(wl.Count * 0.9f), 0, wl.Count - 1)]); }
            float brow = H * 0.62f;                                    // линия бровей: глаза остаются открытыми
            var go = Anchor(head, new Vector3(hp.x, hp.y, hp.z), Quaternion.identity, "Cap");
            var pm = new PaletteMesh();
            pm.Sphere(new Vector3(0, brow - 0.012f, -0.006f), half * 1.17f, CapC, 16, 8, new Vector3(1f, 0.95f, 1.1f), true);
            pm.xf = Matrix4x4.TRS(new Vector3(0, brow + 0.005f, half * 1.0f + 0.035f), Quaternion.Euler(10, 0, 0), Vector3.one);
            pm.RBox(Vector3.zero, new Vector3(half * 2f * 0.98f, 0.012f, 0.1f), 0.005f, CapC);
            pm.xf = Matrix4x4.identity;
            pm.Box(new Vector3(0, brow + 0.015f, half * 1.0f - 0.01f), new Vector3(half * 1.6f, 0.012f, 0.02f), White);           // белая кромка над козырьком
            pm.Sphere(new Vector3(0, brow + half * 1.1f, -0.006f), 0.012f, White, 6, 4);
            pm.ToObject("C", go.transform);
        }
    }
}
