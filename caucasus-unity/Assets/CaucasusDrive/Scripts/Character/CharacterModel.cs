using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    public class Outfit
    {
        public int suit, stripe, pants, shoes, sole, skin, hair;
        public int cap = -1, capBrim = -1;   // -1 — без кепки
        public static readonly Outfit Player = new Outfit { suit = 0x17181c, stripe = 0xf2f2f2, pants = 0x17181c, cap = 0x2b2d33, shoes = 0xf4f4f4, sole = 0xdadada, skin = 0xe2b48f, hair = 0x2a1d14, capBrim = 0x1c1d22 };
    }

    /// <summary>
    /// Процедурный персонаж (порт CharacterModel.js): «пацан с района» — олимпийка с лампасами, кепка, кроссовки.
    /// 17 костей, все детали в ОДНОМ SkinnedMesh с жёсткой привязкой (1 draw call). Персонаж смотрит в +Z,
    /// левая рука на −X (Unity). Позы задаются углами как в веб-версии (Bone(name, rx, ry, rz) с переводом осей),
    /// руки к цели ставит двухзвенная IK.
    /// </summary>
    public class Character
    {
        static readonly string[] Names = { "hips", "spine", "chest", "neck", "head", "upperArmL", "forearmL", "handL", "upperArmR", "forearmR", "handR", "thighL", "shinL", "footL", "thighR", "shinR", "footR" };
        static readonly string[] Parents = { null, "hips", "spine", "chest", "neck", "chest", "upperArmL", "forearmL", "chest", "upperArmR", "forearmR", "hips", "thighL", "shinL", "hips", "thighR", "shinR" };
        static readonly Vector3[] Pos = {
            new Vector3(0, 0.98f, 0), new Vector3(0, 0.1f, 0), new Vector3(0, 0.22f, 0), new Vector3(0, 0.2f, 0), new Vector3(0, 0.08f, 0),
            new Vector3(-0.2f, 0.15f, 0), new Vector3(0, -0.29f, 0), new Vector3(0, -0.25f, 0),
            new Vector3(0.2f, 0.15f, 0), new Vector3(0, -0.29f, 0), new Vector3(0, -0.25f, 0),
            new Vector3(-0.1f, -0.04f, 0), new Vector3(0, -0.45f, 0), new Vector3(0, -0.44f, 0),
            new Vector3(0.1f, -0.04f, 0), new Vector3(0, -0.45f, 0), new Vector3(0, -0.44f, 0),
        };
        public const float UpperArm = 0.29f, Forearm = 0.25f;

        public Transform root, body;
        public SkinnedMeshRenderer smr;
        public readonly Dictionary<string, Transform> B = new Dictionary<string, Transform>();
        public GameObject cigarette;
        public Transform cigTip, mouth;
        public Material emberMat;
        static readonly Dictionary<int, Mesh> meshCache = new Dictionary<int, Mesh>();

        public Character(Outfit o, Transform parent, bool shadows, int cacheKey = 0)
        {
            root = new GameObject("Character").transform;
            root.SetParent(parent, false);
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            var bones = new Transform[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                var b = new GameObject(Names[i]).transform;
                b.SetParent(Parents[i] == null ? body : B[Parents[i]], false);
                b.localPosition = Pos[i];
                B[Names[i]] = b; bones[i] = b;
            }
            Mesh mesh;
            if (cacheKey == 0 || !meshCache.TryGetValue(cacheKey, out mesh))
            {
                mesh = BuildMesh(o, bones);
                if (cacheKey != 0) meshCache[cacheKey] = mesh;
            }
            var mg = new GameObject("Mesh");
            mg.transform.SetParent(body, false);
            smr = mg.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = B["hips"];
            smr.sharedMaterial = PaletteMesh.Material;
            smr.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(2.2f, 2.6f, 2.2f));
            smr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.quality = SkinQuality.Bone1;

            // сигарета в правой руке (видна только при курении)
            var cg = new PaletteMesh();
            cg.Cyl(new Vector3(0, 0.035f, 0), 0.0055f, 0.0055f, 0.07f, 0xf2f2ee, 6);
            cg.Cyl(new Vector3(0, -0.011f, 0), 0.0058f, 0.0058f, 0.022f, 0xd28a3c, 6);
            cigarette = cg.ToObject("Cigarette", B["handR"]);
            cigarette.transform.localPosition = new Vector3(0.012f, -0.085f, 0.035f);
            cigarette.transform.localRotation = PaletteMesh.Q3(Mathf.PI / 2 - 0.25f, 0, 0.35f);
            var eg = new PaletteMesh();
            eg.Cyl(Vector3.zero, 0.0058f, 0.0058f, 0.006f, 0xffffff, 6);
            emberMat = Mats.Unlit().Col(new Color(1f, 0.35f, 0.1f));
            var ember = eg.ToObject("Ember", cigarette.transform, emberMat);
            ember.transform.localPosition = new Vector3(0, 0.071f, 0);
            cigTip = new GameObject("Tip").transform; cigTip.SetParent(cigarette.transform, false); cigTip.localPosition = new Vector3(0, 0.075f, 0);
            cigarette.SetActive(false);
            mouth = new GameObject("Mouth").transform; mouth.SetParent(B["head"], false); mouth.localPosition = new Vector3(0, 0.06f, 0.12f);
        }

        // ------------------------------------------------------------------ геометрия
        static Mesh BuildMesh(Outfit o, Transform[] bones)
        {
            var pm = new PaletteMesh();
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < Names.Length; i++) idx[Names[i]] = i;
            // мировые (относительно тела) матрицы костей в позе привязки
            var bind = new Dictionary<string, Matrix4x4>();
            for (int i = 0; i < Names.Length; i++)
                bind[Names[i]] = (Parents[i] == null ? Matrix4x4.identity : bind[Parents[i]]) * Matrix4x4.Translate(Pos[i]);
            System.Action<string, float, float, float, float, float, float> at = (b, x, y, z, rx, ry, rz) =>
            {
                pm.curBone = idx[b];
                pm.xf = bind[b] * Matrix4x4.TRS(new Vector3(-x, y, z), PaletteMesh.Q3(rx, ry, rz), Vector3.one);
            };
            System.Func<float, float, float, float, Vector2[]> torso = (br, tr, h, unused) =>
            {
                var pts = new Vector2[9];
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    float r = br + (tr - br) * Mathf.Pow(t, 1.4f) - Mathf.Max(0, t - 0.86f) * 0.9f;
                    pts[i] = new Vector2(Mathf.Max(0.02f, r), t * h);
                }
                return pts;
            };
            at("hips", 0, -0.1f, 0, 0, 0, 0); pm.Lathe(Vector3.zero, torso(0.15f, 0.165f, 0.2f, 0), 10, 0.72f, o.pants);
            at("spine", 0, -0.02f, 0, 0, 0, 0); pm.Lathe(Vector3.zero, torso(0.15f, 0.175f, 0.24f, 0), 10, 0.7f, o.suit);
            at("chest", 0, -0.02f, 0, 0, 0, 0); pm.Lathe(Vector3.zero, torso(0.175f, 0.205f, 0.22f, 0), 10, 0.66f, o.suit);
            at("chest", 0, 0.15f, -0.005f, 0, 0, Mathf.PI / 2); pm.Capsule(Vector3.zero, 0.075f, 0.3f, o.suit);
            at("chest", 0, -0.02f, 0.138f, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.012f, 0.42f, 0.01f), o.stripe);
            at("chest", 0, 0.18f, 0, 0, 0, 0); pm.Lathe(Vector3.zero, torso(0.065f, 0.06f, 0.07f, 0), 10, 1f, o.suit);
            at("neck", 0, 0.02f, 0, 0, 0, 0); pm.Capsule(Vector3.zero, 0.048f, 0.06f, o.skin);
            at("head", 0, 0.11f, 0.01f, 0, 0, 0); pm.Sphere(Vector3.zero, 0.108f, o.skin, 12, 10, new Vector3(1, 1.18f, 1.08f));
            at("head", 0, 0.105f, 0.128f, -0.15f, 0, 0); pm.Box(Vector3.zero, new Vector3(0.032f, 0.055f, 0.04f), o.skin);
            foreach (float ex in new[] { 0.04f, -0.04f })
            {
                at("head", ex, 0.135f, 0.112f, 0, 0, 0); pm.Sphere(Vector3.zero, 0.019f, 0xf4f4f0, 8, 6);
                at("head", ex, 0.135f, 0.128f, 0, 0, 0); pm.Sphere(Vector3.zero, 0.011f, 0x2a1d14, 6, 4);
                at("head", ex * 1.05f, 0.166f, 0.122f, 0, 0, -Mathf.Sign(ex) * 0.12f); pm.Box(Vector3.zero, new Vector3(0.042f, 0.011f, 0.012f), o.hair);
            }
            at("head", 0, 0.06f, 0.12f, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.046f, 0.009f, 0.012f), 0x8a4a44);
            at("head", 0.108f, 0.115f, 0, 0, 0, 0); pm.Sphere(Vector3.zero, 0.025f, o.skin, 6, 5);
            at("head", -0.108f, 0.115f, 0, 0, 0, 0); pm.Sphere(Vector3.zero, 0.025f, o.skin, 6, 5);
            at("head", 0, 0.13f, -0.012f, 0, 0, 0); pm.Sphere(Vector3.zero, 0.112f, o.hair, 12, 7, new Vector3(1, 1.05f, 1.08f));
            if (o.cap >= 0)
            {
                int brim = o.capBrim >= 0 ? o.capBrim : o.cap;
                at("head", 0, 0.17f, -0.005f, 0, 0, 0); pm.Sphere(Vector3.zero, 0.118f, o.cap, 14, 8, new Vector3(1, 0.85f, 1.08f), true);
                at("head", 0, 0.175f, 0.09f, 0.12f, 0, 0); pm.Cyl(Vector3.zero, 0.105f, 0.105f, 0.012f, brim, 14);
                at("head", 0, 0.27f, 0, 0, 0, 0); pm.Sphere(Vector3.zero, 0.014f, brim, 6, 4);
            }
            foreach (var s in new[] { "L", "R" })
            {
                float sx = s == "L" ? 1 : -1;
                at("upperArm" + s, 0, 0, 0, 0, 0, 0); pm.Sphere(Vector3.zero, 0.068f, o.suit);
                at("upperArm" + s, 0, -0.145f, 0, 0, 0, 0); pm.Capsule(Vector3.zero, 0.058f, 0.2f, o.suit);
                at("upperArm" + s, sx * 0.057f, -0.14f, 0, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.012f, 0.27f, 0.012f), o.stripe);
                at("forearm" + s, 0, -0.12f, 0, 0, 0, 0); pm.Capsule(Vector3.zero, 0.05f, 0.17f, o.suit);
                at("forearm" + s, sx * 0.048f, -0.11f, 0, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.012f, 0.22f, 0.012f), o.stripe);
                at("forearm" + s, 0, -0.235f, 0, 0, 0, 0); pm.Capsule(Vector3.zero, 0.044f, 0.02f, o.stripe);
                at("hand" + s, 0, -0.05f, 0.006f, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.06f, 0.09f, 0.03f), o.skin);
                at("hand" + s, -sx * 0.025f, -0.035f, 0.025f, 0.3f, 0, sx * 0.3f); pm.Box(Vector3.zero, new Vector3(0.022f, 0.06f, 0.025f), o.skin);
                at("thigh" + s, 0, 0, 0, 0, 0, 0); pm.Sphere(Vector3.zero, 0.085f, o.pants);
                at("thigh" + s, 0, -0.22f, 0, 0, 0, 0); pm.Capsule(Vector3.zero, 0.075f, 0.3f, o.pants);
                at("thigh" + s, sx * 0.076f, -0.22f, 0, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.012f, 0.42f, 0.012f), o.stripe);
                at("shin" + s, 0, -0.2f, 0, 0, 0, 0); pm.Capsule(Vector3.zero, 0.062f, 0.3f, o.pants);
                at("shin" + s, sx * 0.062f, -0.2f, 0, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.012f, 0.36f, 0.012f), o.stripe);
                at("foot" + s, 0, -0.035f, 0.05f, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.1f, 0.075f, 0.26f), o.shoes);
                at("foot" + s, 0, -0.075f, 0.05f, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.104f, 0.028f, 0.27f), o.sole);
                at("foot" + s, 0, -0.02f, 0, 0, 0, 0); pm.Box(Vector3.zero, new Vector3(0.104f, 0.012f, 0.06f), o.stripe == o.shoes ? o.sole : o.stripe);
            }
            var mesh = pm.ToMesh("Character");
            mesh.boneWeights = pm.Weights();
            var bp = new Matrix4x4[Names.Length];
            for (int i = 0; i < Names.Length; i++) bp[i] = bind[Names[i]].inverse;
            mesh.bindposes = bp;
            return mesh;
        }

        // ------------------------------------------------------------------ позы
        /// <summary>Поворот кости углами веб-версии (Эйлер XYZ Three.js).</summary>
        public void Bone(string name, float rx, float ry, float rz) { B[name].localRotation = PaletteMesh.Q3(rx, ry, rz); }

        public void ResetPose()
        {
            for (int i = 0; i < Names.Length; i++) { B[Names[i]].localRotation = Quaternion.identity; B[Names[i]].localPosition = Pos[i]; }
            body.localRotation = Quaternion.identity;
        }

        public float HipsY { get { return B["hips"].localPosition.y; } set { var p = B["hips"].localPosition; p.y = value; B["hips"].localPosition = p; } }

        /// <summary>
        /// Двухзвенная IK руки (плечо → локоть → кисть в мировую точку target), локоть вниз-наружу.
        /// pole — подсказка направления локтя в осях веб-версии (x — наружу для левой руки). w — вес.
        /// </summary>
        public void ArmIK(char side, Vector3 target, float w, Vector3? pole = null)
        {
            var up = B[side == 'L' ? "upperArmL" : "upperArmR"]; var fo = B[side == 'L' ? "forearmL" : "forearmR"];
            var chest = B["chest"];
            var d = chest.InverseTransformPoint(target) - up.localPosition;
            float L1 = UpperArm, L2 = Forearm;
            float dist = Mathf.Min(Mathf.Max(d.magnitude, 0.08f), L1 + L2 - 0.002f);
            var dir = d.normalized;
            float a = (L1 * L1 - L2 * L2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
            float sx = side == 'L' ? -1f : 1f;                      // наружу (Unity: левая сторона — −X)
            var pw = pole ?? new Vector3(0.7f, -1f, -0.15f);
            var pv = new Vector3(pw.x * sx, pw.y, pw.z);
            pv = (pv - dir * Vector3.Dot(pv, dir)).normalized;
            var elbow = dir * a + pv * h;
            var hand = dir * dist;
            var q = Quaternion.FromToRotation(Vector3.down, elbow.normalized);
            up.localRotation = Quaternion.Slerp(up.localRotation, q, w);
            var f = Quaternion.Inverse(up.localRotation) * (hand - elbow).normalized;
            fo.localRotation = Quaternion.Slerp(fo.localRotation, Quaternion.FromToRotation(Vector3.down, f), w);
        }

        public void SetVisible(bool v) { root.gameObject.SetActive(v); }
    }
}
