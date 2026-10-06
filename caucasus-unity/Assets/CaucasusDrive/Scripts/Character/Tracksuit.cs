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
            Collar(); Zip(); Cap(); Shoes(); Hands();
            RemoveBodyParts("Foot", "Toe");           // стопы целиком закрыты кроссовками
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

        /// <summary>
        /// Кисти: исходная «доска» вместо ладони убирается из сетки, вместо неё — ладонь и пять пальцев с фалангами
        /// (Limbs.Hand). Ориентация: пальцы вдоль предплечья, тыл наружу, большой палец вперёд.
        /// </summary>
        static void Hands()
        {
            var hips = rig.root.InverseTransformPoint(rig.B["hips"].position);
            foreach (var sd in new[] { "L", "R" })
            {
                var fore = rig.B["fore" + sd]; var hand = rig.B["hand" + sd];
                var hp = rig.root.InverseTransformPoint(hand.position);
                var F = (hp - rig.root.InverseTransformPoint(fore.position)).normalized;
                float side = hp.x >= hips.x ? 1f : -1f;
                var outward = new Vector3(side, 0, 0); outward = (outward - F * Vector3.Dot(outward, F)).normalized;
                var q = Quaternion.LookRotation(F, outward);                              // локально: Z — пальцы, Y — тыл кисти
                var rightV = q * Vector3.right;
                float thumbSide = Vector3.Dot(rightV, Vector3.forward) >= 0f ? 1f : -1f;
                // размер по реальной кисти: от запястья до кончиков пальцев
                float maxF = 0.1f;
                for (int vi = 0; vi < verts.Count; vi++)
                {
                    int o = owner[vi]; if (o < 0 || o >= skinBones.Length) continue;
                    var nm = skinBones[o].name;
                    if (nm.Contains("Hand") && (sd == "L" ? nm.StartsWith("Left") : nm.StartsWith("Right")))
                        maxF = Mathf.Max(maxF, Vector3.Dot(verts[vi] - hp, F));
                }
                float scale = Mathf.Clamp(maxF / 0.185f, 0.9f, 1.25f);
                var go = Anchor(hand, hp, q, "Hand_" + sd);
                Limbs.Hand(0xe2b48f, thumbSide, scale).ToObject("H", go.transform);
            }
            RemoveBodyParts("Hand");
        }

        /// <summary>Убирает из копии меша треугольники, большей частью принадлежащие костям с именами, содержащими любую из частей.</summary>
        static void RemoveBodyParts(params string[] parts)
        {
            var mesh = rig.smr.sharedMesh;
            if (mesh == null || !mesh.isReadable) return;
            var bw = mesh.boneWeights; var bones = rig.smr.bones;
            if (bw.Length != mesh.vertexCount) return;
            var dead = new bool[mesh.vertexCount];
            for (int i = 0; i < dead.Length; i++)
            {
                int b = bw[i].boneIndex0;
                if (b < 0 || b >= bones.Length) continue;
                foreach (var p in parts) if (bones[b].name.Contains(p)) { dead[i] = true; break; }
            }
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var tri = mesh.GetTriangles(sm); var keep = new List<int>(tri.Length);
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    int c = (dead[tri[i]] ? 1 : 0) + (dead[tri[i + 1]] ? 1 : 0) + (dead[tri[i + 2]] ? 1 : 0);
                    if (c >= 2) continue;
                    keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
                }
                mesh.SetTriangles(keep, sm);
            }
        }

        /// <summary>
        /// Кепка: купол-эллипсоид подбирается так, чтобы накрывать ВСЕ вершины головы выше линии бровей
        /// (волосы не торчат сквозь ткань), козырёк вперёд, белая кромка, пуговка.
        /// </summary>
        static void Cap()
        {
            var head = rig.B["head"];
            var hp = rig.root.InverseTransformPoint(head.position);
            float top = hp.y + 0.2f;
            foreach (var v in verts) if (v.y > hp.y && Vector2.Distance(new Vector2(v.x, v.z), new Vector2(hp.x, hp.z)) < 0.2f) top = Mathf.Max(top, v.y);
            float H = top - hp.y;
            float baseY = hp.y + H * 0.62f;                          // линия бровей: глаза остаются открытыми
            // вершины головы выше линии бровей (кожа лба и волосы)
            var hv = new List<Vector3>();
            foreach (var v in verts) if (v.y > baseY - 0.005f && v.y < top + 0.05f && Vector2.Distance(new Vector2(v.x, v.z), new Vector2(hp.x, hp.z)) < 0.2f) hv.Add(v);
            float minX = 9, maxX = -9, minZ = 9, maxZ = -9;
            foreach (var v in hv) { minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z); }
            if (hv.Count < 5) { minX = hp.x - 0.1f; maxX = hp.x + 0.1f; minZ = hp.z - 0.1f; maxZ = hp.z + 0.1f; }
            float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
            float ax = (maxX - minX) * 0.5f + 0.012f, az = (maxZ - minZ) * 0.5f + 0.012f;
            // форма: короткая эллиптическая «стенка» + полуэллипсоид сверху; масштабируем, пока все вершины не окажутся внутри
            float hc = (top - baseY) * 0.35f;
            float ay = Mathf.Max(0.04f, top - baseY - hc) + 0.015f;
            float k = 1f;
            foreach (var v in hv)
            {
                float ex = (v.x - cx) / ax, ez = (v.z - cz) / az, dy = v.y - baseY;
                float q = ex * ex + ez * ez;
                float m = dy <= hc ? Mathf.Sqrt(q) : Mathf.Sqrt(q + ((dy - hc) / ay) * ((dy - hc) / ay));
                k = Mathf.Max(k, m);
            }
            k *= 1.03f; ax *= k; az *= k; ay *= k; hc *= k;
            var go = Anchor(head, new Vector3(hp.x, hp.y, hp.z), Quaternion.identity, "Cap");
            var o = new Vector3(cx - hp.x, baseY - hp.y, cz - hp.z);          // центр основания купола относительно кости
            var pm = new PaletteMesh();
            var prof = new List<Vector2> { new Vector2(ax, -0.004f), new Vector2(ax, hc) };
            for (int i = 1; i <= 8; i++) { float a8 = i / 8f * Mathf.PI * 0.5f; prof.Add(new Vector2(ax * Mathf.Cos(a8), hc + ay * Mathf.Sin(a8))); }
            pm.Lathe(o, prof.ToArray(), 20, az / ax, CapC);
            float front = o.z + az;
            pm.xf = Matrix4x4.TRS(new Vector3(o.x, o.y + 0.002f, front + 0.04f), Quaternion.Euler(9, 0, 0), Vector3.one);
            pm.RBox(Vector3.zero, new Vector3(ax * 1.9f, 0.012f, 0.11f), 0.005f, CapC);
            pm.xf = Matrix4x4.identity;
            pm.Box(new Vector3(o.x, o.y + 0.012f, front - 0.004f), new Vector3(ax * 1.5f, 0.012f, 0.02f), White);              // белая кромка над козырьком
            pm.Sphere(o + new Vector3(0, hc + ay * 0.99f, 0), 0.013f, White, 6, 4);                                                // пуговка
            pm.ToObject("C", go.transform);
        }

        /// <summary>
        /// Белые кроссовки по форме стопы: подошва, носок с резиновым мыском, высокий задник с воротом,
        /// язычок и шнурки. Размеры берутся по вершинам стопы (кости foot + toe).
        /// </summary>
        static void Shoes()
        {
            foreach (var sd in new[] { "L", "R" })
            {
                var foot = rig.B["foot" + sd]; var toe = rig.B["toe" + sd];
                var fp = rig.root.InverseTransformPoint(foot.position);
                var td = rig.root.InverseTransformPoint(toe.position) - fp; td.y = 0;
                // направление стопы — главная ось облака вершин стопы (кость носка в позе покоя может быть повёрнута)
                double sxx = 0, szz = 0, sxz = 0, mx = 0, mz = 0; int cnt = 0;
                for (int vi = 0; vi < verts.Count; vi++) if (Owns(vi, foot, toe)) { mx += verts[vi].x; mz += verts[vi].z; cnt++; }
                var f = Vector3.forward;
                if (cnt > 6)
                {
                    mx /= cnt; mz /= cnt;
                    for (int vi = 0; vi < verts.Count; vi++) if (Owns(vi, foot, toe)) { double dx = verts[vi].x - mx, dz = verts[vi].z - mz; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
                    double ang = 0.5 * System.Math.Atan2(2 * sxz, sxx - szz);          // угол главной оси от +X
                    f = new Vector3((float)System.Math.Cos(ang), 0, (float)System.Math.Sin(ang));
                    if (Vector3.Dot(f, Vector3.forward) < 0f) f = -f;                  // носок смотрит вперёд
                }
                var rt = new Vector3(f.z, 0, -f.x);
                float minF = 9, maxF = -9, minR = 9, maxR = -9, minY = 9; int n = 0;
                for (int vi = 0; vi < verts.Count; vi++)
                {
                    if (!Owns(vi, foot, toe)) continue;
                    var v = verts[vi];
                    float pf = (v.x - fp.x) * f.x + (v.z - fp.z) * f.z, pr = (v.x - fp.x) * rt.x + (v.z - fp.z) * rt.z;
                    minF = Mathf.Min(minF, pf); maxF = Mathf.Max(maxF, pf); minR = Mathf.Min(minR, pr); maxR = Mathf.Max(maxR, pr); minY = Mathf.Min(minY, v.y); n++;
                }
                if (n < 4) { minF = -0.07f; maxF = 0.17f; minR = -0.04f; maxR = 0.04f; }
                float w = Mathf.Clamp(maxR - minR, 0.07f, 0.12f) + 0.014f;
                float z0 = minF - 0.012f, z1 = maxF + 0.02f, len = Mathf.Clamp(z1 - z0, 0.2f, 0.34f);
                float ankleY = Mathf.Max(fp.y + 0.03f, 0.1f);
                float floorY = n >= 4 ? Mathf.Min(0f, minY) - 0.004f : 0f;                              // подошва ниже самой нижней точки стопы
                var origin = new Vector3(fp.x, floorY, fp.z) + f * z0 + rt * ((minR + maxR) * 0.5f);     // пятка на полу
                var go = Anchor(foot, origin, Quaternion.LookRotation(f, Vector3.up), "Sneaker_" + sd);
                var pm = Limbs.Shoe(len, w, ankleY);
                pm.ToObject("Sh", go.transform);
            }
        }
    }
}
