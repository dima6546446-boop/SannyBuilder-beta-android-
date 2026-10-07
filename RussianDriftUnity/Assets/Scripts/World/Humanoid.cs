using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    public enum PoseKind { Idle, Walk, Cheer, Clap, Sit, Wave, Celebrate }

    /// <summary>
    /// Procedural low-poly human: one skinned mesh (8 bones) + a palette texture, animated by code.
    /// Used for spectators, the driver and the garage preview. Replace with Mixamo/Animator content via ASSETS.md.
    /// </summary>
    public class Humanoid : MonoBehaviour
    {
        public Transform hips, torso, head, armL, armR, legL, legR;
        public PoseKind pose = PoseKind.Idle;
        public float speed = 1f;
        private float t;
        private float seed;
        private Material mat;
        private Texture2D palette;
        private Vector3 rootBase;
        private Quaternion[] targets = new Quaternion[7];
        private Transform[] bones;
        private float bob;

        private const int PSkin = 0, POutfit = 1, PPants = 2, PShoes = 3, PCap = 4, PBlack = 5, PHair = 6, PWhite = 7;

        private static void Box(MeshBuilder mb, Vector3 c, Vector3 size, int pal, int bone, System.Collections.Generic.List<int> boneOf)
        {
            int start = mb.VertexCount;
            mb.AddBox(c, size, Quaternion.identity, Vector2.zero, true, true);
            float u = (pal + 0.5f) / 8f;
            for (int i = start; i < mb.VertexCount; i++) { mb.uvs[i] = new Vector2(u, 0.5f); boneOf.Add(bone); }
        }

        public static Humanoid Build(Transform parent, DriverLook look, System.Random rng, bool randomize)
        {
            var go = new GameObject("Human");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Humanoid>();
            h.Create(look, rng, randomize);
            return h;
        }

        public void Rebuild(DriverLook look)
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) Destroy(smr.gameObject);
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            Create(look, null, false);
        }

        private void Create(DriverLook look, System.Random rng, bool randomize)
        {
            seed = rng != null ? (float)rng.NextDouble() * 10f : 0f;
            Color skin = VehicleStats.ParseColor(look.skinHex, new Color(0.88f, 0.7f, 0.55f));
            Color outfit = VehicleStats.ParseColor(look.outfitHex, new Color(0.17f, 0.23f, 0.33f));
            int cap = look.cap, glasses = look.glasses, style = look.outfit;
            Color pants = new Color(0.1f, 0.1f, 0.14f);
            if (randomize)
            {
                float[] tones = { 0.95f, 0.8f, 0.65f, 0.5f };
                skin = new Color(0.9f, 0.72f, 0.58f) * tones[rng.Next(tones.Length)];
                Color[] cols = { new Color(0.7f, 0.15f, 0.12f), new Color(0.15f, 0.3f, 0.6f), new Color(0.2f, 0.2f, 0.22f), new Color(0.85f, 0.85f, 0.8f), new Color(0.3f, 0.5f, 0.25f), new Color(0.8f, 0.55f, 0.1f) };
                outfit = cols[rng.Next(cols.Length)];
                pants = rng.Next(2) == 0 ? new Color(0.1f, 0.1f, 0.14f) : new Color(0.25f, 0.28f, 0.4f);
                cap = rng.Next(3) == 0 ? 1 + rng.Next(3) : 0;
                glasses = rng.Next(5) == 0 ? 1 : 0;
                style = rng.Next(5);
            }
            Color capCol = cap == 1 ? new Color(0.1f, 0.1f, 0.1f) : (cap == 2 ? new Color(0.75f, 0.12f, 0.1f) : new Color(0.15f, 0.3f, 0.7f));
            Color hair = new Color(0.12f, 0.08f, 0.06f);
            if (style == 4) outfit = Color.Lerp(outfit, new Color(0.05f, 0.05f, 0.06f), 0.85f);

            palette = new Texture2D(8, 1, TextureFormat.RGBA32, false, false);
            palette.filterMode = FilterMode.Point; palette.wrapMode = TextureWrapMode.Clamp;
            palette.SetPixels(new[] { skin, outfit, pants, new Color(0.05f, 0.05f, 0.05f), capCol, new Color(0.03f, 0.03f, 0.04f), hair, Color.white });
            palette.Apply(false, true);
            mat = MatLib.Lit(Color.white, 0.12f, 0f, palette);

            // ---- skeleton ----
            Transform B(string n, Transform p, Vector3 worldRest)
            {
                var g = new GameObject(n).transform;
                g.SetParent(p, false);
                g.position = transform.TransformPoint(worldRest);
                return g;
            }
            var root = transform;
            hips = B("hips", root, new Vector3(0, 0.92f, 0));
            legL = B("legL", hips, new Vector3(-0.10f, 0.92f, 0));
            legR = B("legR", hips, new Vector3(0.10f, 0.92f, 0));
            torso = B("torso", hips, new Vector3(0, 0.92f, 0));
            head = B("head", torso, new Vector3(0, 1.54f, 0));
            armL = B("armL", torso, new Vector3(-0.27f, 1.46f, 0));
            armR = B("armR", torso, new Vector3(0.27f, 1.46f, 0));
            bones = new[] { hips, torso, head, armL, armR, legL, legR };
            const int bHips = 0, bTorso = 1, bHead = 2, bArmL = 3, bArmR = 4, bLegL = 5, bLegR = 6;

            var mb = new MeshBuilder();
            var bi = new System.Collections.Generic.List<int>();
            // legs
            Box(mb, new Vector3(-0.10f, 0.47f, 0), new Vector3(0.15f, 0.86f, 0.17f), PPants, bLegL, bi);
            Box(mb, new Vector3(0.10f, 0.47f, 0), new Vector3(0.15f, 0.86f, 0.17f), PPants, bLegR, bi);
            Box(mb, new Vector3(-0.10f, 0.04f, 0.04f), new Vector3(0.16f, 0.09f, 0.27f), PShoes, bLegL, bi);
            Box(mb, new Vector3(0.10f, 0.04f, 0.04f), new Vector3(0.16f, 0.09f, 0.27f), PShoes, bLegR, bi);
            // torso
            Box(mb, new Vector3(0, 1.22f, 0), new Vector3(0.44f, 0.58f, 0.24f), POutfit, bTorso, bi);
            Box(mb, new Vector3(0, 0.95f, 0), new Vector3(0.45f, 0.1f, 0.25f), PPants, bHips, bi);
            if (style == 1) { Box(mb, new Vector3(0, 1.22f, 0.121f), new Vector3(0.1f, 0.58f, 0.01f), PWhite, bTorso, bi); }
            if (style == 3) { Box(mb, new Vector3(0, 1.5f, -0.12f), new Vector3(0.3f, 0.16f, 0.12f), POutfit, bTorso, bi); }
            // arms
            for (int s = -1; s <= 1; s += 2)
            {
                int ab = s < 0 ? bArmL : bArmR;
                float x = s * 0.27f;
                if (style == 2)
                {
                    Box(mb, new Vector3(x, 1.36f, 0), new Vector3(0.12f, 0.2f, 0.12f), POutfit, ab, bi);
                    Box(mb, new Vector3(x, 1.09f, 0), new Vector3(0.09f, 0.34f, 0.09f), PSkin, ab, bi);
                }
                else Box(mb, new Vector3(x, 1.21f, 0), new Vector3(0.1f, 0.5f, 0.1f), POutfit, ab, bi);
                Box(mb, new Vector3(x, 0.89f, 0), new Vector3(0.085f, 0.09f, 0.085f), PSkin, ab, bi);
            }
            // head
            Box(mb, new Vector3(0, 1.69f, 0), new Vector3(0.2f, 0.24f, 0.22f), PSkin, bHead, bi);
            if (cap == 0) Box(mb, new Vector3(0, 1.83f, -0.01f), new Vector3(0.21f, 0.06f, 0.23f), PHair, bHead, bi);
            else
            {
                Box(mb, new Vector3(0, 1.84f, -0.005f), new Vector3(0.23f, 0.08f, 0.25f), PCap, bHead, bi);
                Box(mb, new Vector3(0, 1.80f, 0.15f), new Vector3(0.2f, 0.02f, 0.12f), PCap, bHead, bi);
            }
            if (glasses > 0) Box(mb, new Vector3(0, 1.72f, 0.115f), new Vector3(0.22f, glasses == 1 ? 0.05f : 0.07f, 0.02f), PBlack, bHead, bi);

            var mesh = mb.ToMesh("Human");
            var weights = new BoneWeight[bi.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = bi[i], weight0 = 1f };
            mesh.boneWeights = weights;
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            mesh.bindposes = bind;

            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(transform, false);
            var smr = meshGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.sharedMaterial = mat;
            smr.bones = bones;
            smr.rootBone = transform;
            smr.localBounds = new Bounds(new Vector3(0, 1f, 0), new Vector3(1.4f, 2.2f, 1.4f));
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rootBase = Vector3.zero;
            for (int i = 0; i < targets.Length; i++) targets[i] = Quaternion.identity;
        }

        private void OnDestroy()
        {
            if (palette != null) Destroy(palette);
            if (mat != null) Destroy(mat);
        }

        private void LateUpdate()
        {
            if (hips == null) return;
            float dt = Time.deltaTime;
            t += dt * speed;
            float ph = t + seed;
            Quaternion q = Quaternion.identity;
            float hipsY = 0f;
            Quaternion tHips = Quaternion.identity, tTorso = Quaternion.identity, tHead = Quaternion.identity;
            Quaternion tAL = Quaternion.identity, tAR = Quaternion.identity, tLL = Quaternion.identity, tLR = Quaternion.identity;
            switch (pose)
            {
                case PoseKind.Walk:
                    {
                        float s = Mathf.Sin(ph * 7f);
                        tLL = Quaternion.Euler(-s * 38f, 0, 0); tLR = Quaternion.Euler(s * 38f, 0, 0);
                        tAL = Quaternion.Euler(s * 30f, 0, 4f); tAR = Quaternion.Euler(-s * 30f, 0, -4f);
                        hipsY = Mathf.Abs(Mathf.Cos(ph * 7f)) * 0.04f;
                        tTorso = Quaternion.Euler(4f, s * 6f, 0);
                        break;
                    }
                case PoseKind.Cheer:
                case PoseKind.Celebrate:
                    {
                        float s = Mathf.Sin(ph * 9f);
                        float jump = Mathf.Abs(Mathf.Sin(ph * 5f));
                        tAL = Quaternion.Euler(-160f + s * 14f, 0, -14f); tAR = Quaternion.Euler(-160f - s * 14f, 0, 14f);
                        hipsY = jump * 0.22f;
                        tLL = Quaternion.Euler(-jump * 18f, 0, 0); tLR = Quaternion.Euler(-jump * 18f, 0, 0);
                        tHead = Quaternion.Euler(-12f, Mathf.Sin(ph * 3f) * 15f, 0);
                        break;
                    }
                case PoseKind.Clap:
                    {
                        float s = Mathf.Abs(Mathf.Sin(ph * 8f));
                        tAL = Quaternion.Euler(-80f, 0, 14f * s); tAR = Quaternion.Euler(-80f, 0, -14f * s);
                        tTorso = Quaternion.Euler(Mathf.Sin(ph * 8f) * 2f, 0, 0);
                        break;
                    }
                case PoseKind.Sit:
                    tLL = Quaternion.Euler(-85f, 0, 0); tLR = Quaternion.Euler(-85f, 0, 0);
                    tAL = Quaternion.Euler(-70f, 0, 0); tAR = Quaternion.Euler(-70f, 0, 0);
                    hipsY = -0.38f;
                    break;
                case PoseKind.Wave:
                    tAR = Quaternion.Euler(-150f, 0, -25f + Mathf.Sin(ph * 9f) * 22f);
                    tAL = Quaternion.Euler(0, 0, 4f);
                    break;
                default:
                    {
                        float s = Mathf.Sin(ph * 1.6f);
                        tTorso = Quaternion.Euler(s * 1.2f, 0, 0);
                        tAL = Quaternion.Euler(s * 2f, 0, 5f); tAR = Quaternion.Euler(-s * 2f, 0, -5f);
                        tHead = Quaternion.Euler(0, Mathf.Sin(ph * 0.5f) * 14f, 0);
                        break;
                    }
            }
            float k = 1f - Mathf.Exp(-14f * dt);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, tTorso, k);
            head.localRotation = Quaternion.Slerp(head.localRotation, tHead, k);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, tAL, k);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, tAR, k);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, tLL, k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, tLR, k);
            bob = Mathf.Lerp(bob, hipsY, k);
            hips.localPosition = new Vector3(0f, 0.92f + bob, 0f);
        }
    }
}
