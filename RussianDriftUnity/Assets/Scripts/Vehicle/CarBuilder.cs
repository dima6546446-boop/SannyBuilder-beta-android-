using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>
    /// Builds a complete car (shell, glass, wheels, lights, kits, spoilers, neon...) from a CarDefinition + CarSetup using code only.
    /// Three LOD levels; wheels are separate objects so they can spin/steer/hang on the suspension.
    /// Car space: origin = centre of mass, +Z forward, ground at y = -comHeight.
    /// </summary>
    public class CarBuilder
    {
        private struct Spec
        {
            public float belt, noseTop, hoodTop, trunkTop, cabFront, cabRear, roofFront, roofRear, n, glassTaper;
        }

        private static Spec SpecFor(BodyStyle s)
        {
            switch (s)
            {
                case BodyStyle.Classic: return new Spec { belt = 0.58f, noseTop = 0.50f, hoodTop = 0.56f, trunkTop = 0.60f, cabFront = 0.64f, cabRear = 0.20f, roofFront = 0.58f, roofRear = 0.30f, n = 5f, glassTaper = 0.90f };
                case BodyStyle.Sedan: return new Spec { belt = 0.57f, noseTop = 0.48f, hoodTop = 0.54f, trunkTop = 0.60f, cabFront = 0.62f, cabRear = 0.24f, roofFront = 0.54f, roofRear = 0.34f, n = 4f, glassTaper = 0.88f };
                case BodyStyle.Hatch: return new Spec { belt = 0.58f, noseTop = 0.47f, hoodTop = 0.54f, trunkTop = 0.64f, cabFront = 0.66f, cabRear = 0.10f, roofFront = 0.54f, roofRear = 0.18f, n = 4f, glassTaper = 0.88f };
                case BodyStyle.BigSedan: return new Spec { belt = 0.57f, noseTop = 0.50f, hoodTop = 0.56f, trunkTop = 0.62f, cabFront = 0.60f, cabRear = 0.24f, roofFront = 0.50f, roofRear = 0.34f, n = 4.5f, glassTaper = 0.86f };
                case BodyStyle.Wagon: return new Spec { belt = 0.57f, noseTop = 0.48f, hoodTop = 0.54f, trunkTop = 0.64f, cabFront = 0.63f, cabRear = 0.05f, roofFront = 0.54f, roofRear = 0.10f, n = 4f, glassTaper = 0.90f };
                case BodyStyle.Coupe: return new Spec { belt = 0.55f, noseTop = 0.42f, hoodTop = 0.50f, trunkTop = 0.57f, cabFront = 0.58f, cabRear = 0.20f, roofFront = 0.46f, roofRear = 0.30f, n = 3.6f, glassTaper = 0.80f };
                default: return new Spec { belt = 0.54f, noseTop = 0.40f, hoodTop = 0.48f, trunkTop = 0.57f, cabFront = 0.56f, cabRear = 0.18f, roofFront = 0.45f, roofRear = 0.30f, n = 3.3f, glassTaper = 0.78f };
            }
        }

        // ---- inputs / shared state ----
        private readonly CarDefinition def;
        private readonly CarSetup setup;
        private readonly bool traffic;
        private Spec sp;
        private float L, W, H, g, bot;
        private CarModel model;
        private GameObject root;
        private Color paintColor;

        private Material paintMat, trimMat, glassMat, blackMat, chromeMat, tireMat, rimMat, interiorMat, plateMatF, plateMatR;
        private Material headMat, tailMat, indLMat, indRMat, revMat, neonMat, beamMat;
        private Texture2D wrapTex;

        private readonly List<Renderer> lod0 = new List<Renderer>();
        private readonly List<Renderer> lod1 = new List<Renderer>();
        private readonly List<Renderer> lod2 = new List<Renderer>();
        private readonly List<Transform> detachables = new List<Transform>();

        private CarBuilder(CarDefinition d, CarSetup s, bool traffic)
        {
            def = d; setup = s; this.traffic = traffic;
        }

        public static CarModel Build(CarDefinition d, CarSetup s, Transform parent, bool traffic = false)
        {
            var b = new CarBuilder(d, s, traffic);
            return b.Run(parent);
        }

        private delegate void PillarFn(float x0, float y0, float z0, float x1, float y1, float z1);

        // ---------------------------------------------------------------- helpers
        private float Z(float zf) { return (zf - 0.5f) * L; }
        private float Y(float f) { return g + f * H; }

        private float TopF(float zf)
        {
            // top-of-shell height fraction along the length (rear 0 -> front 1)
            float[] zs = { 0f, 0.03f, sp.cabRear, sp.cabFront, 0.93f, 1f };
            float[] ys = { sp.trunkTop * 0.90f, sp.trunkTop, sp.trunkTop, sp.hoodTop, Mathf.Lerp(sp.hoodTop, sp.noseTop, 0.45f), sp.noseTop };
            if (zf <= zs[0]) return ys[0];
            for (int i = 0; i < zs.Length - 1; i++)
                if (zf <= zs[i + 1])
                {
                    float t = (zf - zs[i]) / Mathf.Max(0.0001f, zs[i + 1] - zs[i]);
                    return Mathf.Lerp(ys[i], ys[i + 1], t);
                }
            return ys[ys.Length - 1];
        }

        private float HalfW(float zf)
        {
            float t = 1f;
            if (zf < 0.03f) t = Mathf.Lerp(0.82f, 1f, zf / 0.03f);
            else if (zf > 0.97f) t = Mathf.Lerp(1f, 0.85f, (zf - 0.97f) / 0.03f);
            return 0.5f * W * 0.94f * t;
        }

        private float BotY(float zf)
        {
            float b = bot;
            if (zf < 0.06f) b += 0.05f * (1f - zf / 0.06f);
            if (zf > 0.94f) b += 0.06f * ((zf - 0.94f) / 0.06f);
            return b;
        }

        private static Vector3[] Ring(float z, float yBot, float yTop, float hwBot, float hwTop, float n, int segs)
        {
            var pts = new Vector3[segs];
            float cy = (yBot + yTop) * 0.5f, hy = Mathf.Max(0.005f, (yTop - yBot) * 0.5f);
            float ex = 2f / n;
            for (int k = 0; k < segs; k++)
            {
                float a = Mathf.PI * 0.5f - k / (float)segs * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float sx = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), ex);
                float sy = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), ex);
                float hw = Mathf.Lerp(hwBot, hwTop, (sy + 1f) * 0.5f);
                pts[k] = new Vector3(hw * sx, cy + hy * sy, z);
            }
            return pts;
        }

        private Renderer AddPart(string name, Transform parent, Mesh mesh, Material mat, List<Renderer> lods, bool shadow = false, params List<Renderer>[] more)
        {
            var go = MeshUtil.Make(name, parent, mesh, mat, false, shadow);
            var r = go.GetComponent<Renderer>();
            if (lods != null) lods.Add(r);
            if (more != null) foreach (var l in more) if (l != null) l.Add(r);
            model.allRenderers.Add(r);
            return r;
        }

        // ---------------------------------------------------------------- run
        private CarModel Run(Transform parent)
        {
            sp = SpecFor(def.bodyStyle);
            L = def.length; W = def.width; H = def.height;
            g = -def.comHeight;
            bot = g + 0.17f;

            root = new GameObject("CarVisual");
            root.transform.SetParent(parent, false);
            model = root.AddComponent<CarModel>();
            model.visualRoot = root.transform;
            model.length = L; model.width = W; model.height = H;
            model.groundOffset = bot;
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            model.bodyRoot = body;

            paintColor = VehicleStats.ParseColor(setup.colorHex, def.defaultColor);
            MakeMaterials();

            BuildBodyLod(0, 16, body);
            BuildBodyLod(1, 10, body);
            BuildBodyLod(2, 6, body);
            BuildDetails(body);
            BuildWheels(body);
            BuildLights(body);
            BuildInterior(body);
            BuildKitAndSpoiler(body);
            BuildDecals(body);
            BuildNeon(body);
            BuildCameras();

            // LOD group
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[]
            {
                new LOD(0.30f, lod0.ToArray()),
                new LOD(0.10f, lod1.ToArray()),
                new LOD(0.02f, lod2.ToArray()),
            });
            lg.RecalculateBounds();
            model.lodGroup = lg;
            model.detachables = detachables.ToArray();

            // everything generated here (meshes, material instances, wrap/plate textures) is released with the model
            var seen = new HashSet<UnityEngine.Object>();
            Action<UnityEngine.Object> own = o => { if (o != null && seen.Add(o)) model.owned.Add(o); };
            foreach (var r in model.allRenderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) own(mf.sharedMesh);
                foreach (var mt in r.sharedMaterials) own(mt);
            }
            own(wrapTex);
            if (plateMatF != null) own(plateMatF.mainTexture);
            return model;
        }

        // ---------------------------------------------------------------- materials
        private void MakeMaterials()
        {
            int wrapVariant = 0;
            var wrapDef = GameCatalog.GetCosmetic(CosmeticCategory.Wrap, setup.wrapId);
            if (wrapDef != null) wrapVariant = wrapDef.variant;

            paintMat = MatLib.Lit(Color.white, 0.86f, 0.30f);
            trimMat = MatLib.Lit(paintColor, 0.80f, 0.25f);
            Color accent = Color.Lerp(paintColor, paintColor.grayscale > 0.5f ? new Color(0.05f, 0.05f, 0.06f) : Color.white, 0.92f);
            if (wrapVariant == 6) accent = Color.Lerp(paintColor, new Color(0.9f, 0.2f, 0.6f), 0.7f);
            wrapTex = ProcTex.WrapPaint(wrapVariant, paintColor, accent);
            MatLib.SetTexture(paintMat, wrapTex);
            MatLib.SetColor(paintMat, Color.white);
            if (wrapVariant == 5) { trimMat.color = new Color(0.08f, 0.08f, 0.09f); }

            float tint = Mathf.Clamp(setup.tint, 0, 3) / 3f;
            glassMat = MatLib.Lit(Color.Lerp(new Color(0.20f, 0.28f, 0.34f), new Color(0.015f, 0.02f, 0.025f), tint), 0.95f, 0.25f);
            blackMat = MatLib.Lit(new Color(0.04f, 0.04f, 0.045f), 0.25f, 0f);
            chromeMat = MatLib.Lit(new Color(0.85f, 0.86f, 0.9f), 0.9f, 0.95f);
            interiorMat = MatLib.Lit(new Color(0.13f, 0.12f, 0.12f), 0.15f, 0f);
            tireMat = MatLib.Lit(new Color(0.9f, 0.9f, 0.9f), 0.18f, 0f, ProcTex.Tire());
            Color rimC = VehicleStats.ParseColor(setup.rimColorHex, new Color(0.78f, 0.78f, 0.8f));
            rimMat = MatLib.Lit(rimC, 0.85f, 0.9f);
            headMat = MatLib.Unlit(new Color(0.55f, 0.55f, 0.5f));
            tailMat = MatLib.Unlit(new Color(0.35f, 0.03f, 0.03f));
            indLMat = MatLib.Unlit(new Color(0.35f, 0.18f, 0.02f));
            indRMat = MatLib.Unlit(new Color(0.35f, 0.18f, 0.02f));
            revMat = MatLib.Unlit(new Color(0.45f, 0.45f, 0.45f));
            neonMat = MatLib.Unlit(Color.black);
            beamMat = MatLib.Additive(ProcTex.BeamGradient(), new Color(0, 0, 0, 0));
            string plate = ProcTex.SanitizePlate(setup.plate);
            plateMatF = MatLib.Lit(Color.white, 0.3f, 0f, ProcTex.Plate(plate, setup.region));
            plateMatR = plateMatF;
            model.paintMaterial = paintMat;
            model.glassMaterial = glassMat;
        }

        // ---------------------------------------------------------------- body
        private void BuildBodyLod(int lod, int segs, Transform parent)
        {
            var lodList = lod == 0 ? lod0 : (lod == 1 ? lod1 : lod2);
            string sfx = "_LOD" + lod;
            float n = sp.n;

            // shell
            var zsList = new List<float> { 0f, 0.025f, 0.07f, sp.cabRear, (sp.cabRear + sp.cabFront) * 0.5f, sp.cabFront, 0.80f, 0.92f, 0.975f, 1f };
            if (lod == 2) zsList = new List<float> { 0f, 0.07f, sp.cabRear, sp.cabFront, 0.93f, 1f };
            zsList.Sort();
            var zs = new List<float>();
            for (int i = 0; i < zsList.Count; i++) if (zs.Count == 0 || zsList[i] - zs[zs.Count - 1] > 0.012f) zs.Add(zsList[i]);
            var rings = new List<Vector3[]>();
            for (int i = 0; i < zs.Count; i++)
            {
                float zf = zs[i];
                float hw = HalfW(zf);
                rings.Add(Ring(Z(zf), BotY(zf), Y(TopF(zf)), hw * 0.93f, hw * 0.90f, n, segs));
            }
            var mb = new MeshBuilder();
            mb.AddLoftSmooth(rings, true, true, 1f, 1f);
            var shellMesh = mb.ToMesh("Shell" + sfx, lod == 0);
            var shellR = AddPart("Shell" + sfx, parent, shellMesh, paintMat, lodList, true);
            if (lod == 0) model.bodyFilter = shellR.GetComponent<MeshFilter>();

            // cabin glass + roof
            float yRoof = Y(1f);
            float hwG = 0.5f * W * 0.90f * 0.97f;
            var stations = new List<Vector3>();   // (z, yTop, taperMul)
            float zr0 = sp.cabRear, zr1 = sp.roofRear, zf1 = sp.roofFront, zf0 = sp.cabFront;
            Func<float, float> belt = zf => Y(TopF(zf));
            int sub = lod == 2 ? 1 : 2;
            var cab = new List<Vector3[]>();
            Action<float, float, float> addCab = (zf, yTop, tp) =>
            {
                float yb = belt(zf) - 0.02f;
                cab.Add(Ring(Z(zf), yb, Mathf.Max(yb + 0.02f, yTop), hwG, hwG * sp.glassTaper * tp, 4f, lod == 2 ? 6 : Mathf.Max(8, segs - 2)));
            };
            addCab(zr0, belt(zr0) + 0.03f, 1f);
            for (int k = 1; k <= sub; k++) { float t = k / (float)(sub + 1); addCab(Mathf.Lerp(zr0, zr1, t), Mathf.Lerp(belt(zr0) + 0.03f, yRoof, Mathf.SmoothStep(0f, 1f, t)), 1f); }
            addCab(zr1, yRoof, 1f);
            addCab(zf1, yRoof, 1f);
            for (int k = sub; k >= 1; k--) { float t = k / (float)(sub + 1); addCab(Mathf.Lerp(zf0, zf1, t), Mathf.Lerp(belt(zf0) + 0.03f, yRoof, Mathf.SmoothStep(0f, 1f, t)), 1f); }
            addCab(zf0, belt(zf0) + 0.03f, 1f);
            // loft order must run rear -> front
            var mc = new MeshBuilder();
            mc.AddLoftSmooth(cab, true, true);
            AddPart("Cabin" + sfx, parent, mc.ToMesh("Cabin" + sfx), glassMat, lodList, false);

            // roof slab
            var mr = new MeshBuilder();
            var roofRings = new List<Vector3[]>();
            float rw = hwG * sp.glassTaper;
            roofRings.Add(Ring(Z(zr1 - 0.015f), yRoof - 0.04f, yRoof + 0.005f, rw * 0.98f, rw * 0.9f, 3.5f, segs > 8 ? 10 : 6));
            roofRings.Add(Ring(Z(zr1), yRoof - 0.03f, yRoof + 0.018f, rw * 1.02f, rw * 0.95f, 3.5f, segs > 8 ? 10 : 6));
            roofRings.Add(Ring(Z(zf1), yRoof - 0.03f, yRoof + 0.018f, rw * 1.02f, rw * 0.95f, 3.5f, segs > 8 ? 10 : 6));
            roofRings.Add(Ring(Z(zf1 + 0.015f), yRoof - 0.04f, yRoof + 0.005f, rw * 0.98f, rw * 0.9f, 3.5f, segs > 8 ? 10 : 6));
            mr.AddLoftSmooth(roofRings, true, true, 1f, 1f);
            // pillars (boxes between belt and roof corners)
            if (lod < 2)
            {
                float th = 0.055f;
                PillarFn pillar = (x0, y0, z0, x1, y1, z1) =>
                {
                    Vector3 a = new Vector3(x0, y0, z0), b = new Vector3(x1, y1, z1);
                    Vector3 d = b - a;
                    mr.AddBox((a + b) * 0.5f, new Vector3(th, th, d.magnitude), Quaternion.LookRotation(d.normalized, Vector3.up), Vector2.zero);
                };
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float xb = sgn * hwG * 0.98f, xt = sgn * hwG * sp.glassTaper * 0.96f;
                    pillar(xb, belt(zf0), Z(zf0), xt, yRoof - 0.02f, Z(zf1));         // A
                    pillar(xb, belt(zr0), Z(zr0), xt, yRoof - 0.02f, Z(zr1));         // C
                    float zm = Mathf.Lerp(zr1, zf1, 0.48f);
                    pillar(xb * 1.0f, belt(zm), Z(zm), xt, yRoof - 0.02f, Z(zm));     // B
                }
            }
            AddPart("Roof" + sfx, parent, mr.ToMesh("Roof" + sfx), paintMat, lodList, true);

            if (lod == 2)
            {
                // merged low-poly wheels so far LODs still have wheels
                var mw = new MeshBuilder();
                float tw = def.tireWidth, R = def.wheelRadius;
                foreach (var wp in WheelPositions())
                    mw.AddCylinder(new Vector3(wp.x, wp.y, wp.z), R, tw, Quaternion.Euler(0, 0, 90f), 8, false);
                AddPart("Wheels_LOD2", parent, mw.ToMesh("Wheels_LOD2"), blackMat, lod2, false);
            }
        }

        // wheel centres (x,z) + rest y
        private List<Vector3> WheelPositions()
        {
            var l = new List<Vector3>();
            float tw = def.tireWidth;
            float xc = HalfW(0.5f) + 0.03f - tw * 0.5f;
            float zF = def.wheelbase * 0.48f, zR = -def.wheelbase * 0.52f;
            float y = g + def.wheelRadius;
            l.Add(new Vector3(-xc, y, zF)); l.Add(new Vector3(xc, y, zF));
            l.Add(new Vector3(-xc, y, zR)); l.Add(new Vector3(xc, y, zR));
            return l;
        }

        // ---------------------------------------------------------------- details (LOD0 only)
        private void BuildDetails(Transform parent)
        {
            var det = new MeshBuilder();    // black trim
            float hwFace = HalfW(1f);
            // door cut lines + handles
            float zA = sp.cabFront - 0.02f, zB = Mathf.Lerp(sp.cabRear, sp.cabFront, 0.5f), zC = sp.cabRear + 0.04f;
            float yLow = bot + 0.10f, yHigh = Y(TopF(zB)) - 0.02f;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float x = sgn * (HalfW(zB) * 0.93f + 0.004f);
                float[] zl = { zA, zB, zC };
                for (int i = 0; i < zl.Length; i++)
                {
                    float yt = Y(TopF(zl[i])) - 0.02f;
                    det.AddBox(new Vector3(x, (yLow + yt) * 0.5f, Z(zl[i])), new Vector3(0.006f, yt - yLow, 0.008f));
                }
                det.AddBox(new Vector3(x + sgn * 0.003f, yHigh - 0.08f, Z(Mathf.Lerp(zA, zB, 0.22f))), new Vector3(0.012f, 0.025f, 0.12f));
                det.AddBox(new Vector3(x + sgn * 0.003f, yHigh - 0.08f, Z(Mathf.Lerp(zB, zC, 0.22f))), new Vector3(0.012f, 0.025f, 0.12f));
            }
            // under-body
            det.AddBox(new Vector3(0, bot + 0.01f, 0), new Vector3(W * 0.82f, 0.02f, L * 0.88f));
            // grille
            det.AddBox(new Vector3(0, Mathf.Lerp(BotY(1f), Y(sp.noseTop), 0.55f), Z(1f) + 0.004f), new Vector3(W * 0.40f, 0.09f, 0.012f));
            // wheel arches (dark discs on body flank)
            foreach (var wp in WheelPositions())
            {
                float side = Mathf.Sign(wp.x);
                float xa = side * (HalfW(0.5f) * 0.93f + 0.006f);
                det.AddCylinder(new Vector3(xa, wp.y + 0.01f, wp.z), def.wheelRadius + 0.055f, 0.004f, Quaternion.Euler(0, 0, 90f), 14, true);
            }
            AddPart("Details", parent, det.ToMesh("Details"), blackMat, lod0, false, lod1);

            // chrome strips
            var chr = new MeshBuilder();
            chr.AddBox(new Vector3(0, Y(sp.belt) * 0f + bot + 0.12f, Z(1f) + 0.006f), new Vector3(W * 0.46f, 0.012f, 0.012f));
            AddPart("Chrome", parent, chr.ToMesh("Chrome"), chromeMat, lod0, false);

            // front / rear bumpers (detachable, painted)
            var bf = new MeshBuilder();
            bf.AddBox(Vector3.zero, new Vector3(W * 0.97f, 0.19f, 0.15f));
            var bfGo = MakeDetachable("Bumper_F", parent, bf, trimMat, new Vector3(0, bot + 0.13f, Z(1f) - 0.03f));
            var bfin = new MeshBuilder();
            bfin.AddBox(new Vector3(0, -0.01f, 0.076f), new Vector3(W * 0.50f, 0.07f, 0.01f));
            AddPart("Intake", bfGo.transform, bfin.ToMesh("Intake"), blackMat, lod0, false);
            var br = new MeshBuilder();
            br.AddBox(Vector3.zero, new Vector3(W * 0.97f, 0.19f, 0.15f));
            MakeDetachable("Bumper_R", parent, br, trimMat, new Vector3(0, bot + 0.13f, Z(0f) + 0.03f));

            // mirrors
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                var mm = new MeshBuilder();
                mm.AddBox(Vector3.zero, new Vector3(0.14f, 0.09f, 0.07f));
                var go = MakeDetachable(sgn < 0 ? "Mirror_L" : "Mirror_R", parent, mm, trimMat,
                    new Vector3(sgn * (hwFace * 0.93f + 0.10f), Y(TopF(sp.cabFront)) + 0.11f, Z(sp.cabFront) - 0.12f));
            }

            // hood panel line (detachable thin hood overlay)
            var hd = new MeshBuilder();
            float hz0 = sp.cabFront + 0.015f, hz1 = 0.965f;
            float hw0 = HalfW(hz0) * 0.80f;
            float yh0 = Y(TopF(hz0)) + 0.004f, yh1 = Y(TopF(hz1)) + 0.004f;
            hd.AddQuad(new Vector3(-hw0, yh0, Z(hz0)), new Vector3(hw0, yh0, Z(hz0)), new Vector3(hw0 * 0.9f, yh1, Z(hz1)), new Vector3(-hw0 * 0.9f, yh1, Z(hz1)));
            AddPart("HoodInset", parent, hd.ToMesh("HoodInset"), trimMat, lod0, false);

            // exhaust tip
            var ex = new MeshBuilder();
            ex.AddCylinder(Vector3.zero, 0.04f, 0.14f, Quaternion.Euler(90f, 0, 0), 10, false);
            var exGo = new GameObject("Exhaust").transform;
            exGo.SetParent(parent, false);
            exGo.localPosition = new Vector3(-W * 0.30f, bot + 0.08f, Z(0f) - 0.04f);
            AddPart("ExhaustTip", exGo, ex.ToMesh("Exhaust"), chromeMat, lod0, false);
            model.exhaust = exGo;

            // plates
            if (!traffic || true)
            {
                var pf = new MeshBuilder();
                pf.AddFace(Vector3.zero, Vector3.forward, Vector3.Cross(Vector3.forward, Vector3.up), 0.52f, 0.115f, Vector2.zero);
                AddPart("PlateF", parent, pf.ToMesh("PlateF"), plateMatF, lod0, false, lod1).transform.localPosition = new Vector3(0, bot + 0.13f, Z(1f) + 0.012f);
                var pr = new MeshBuilder();
                pr.AddFace(Vector3.zero, Vector3.back, Vector3.Cross(Vector3.back, Vector3.up), 0.52f, 0.115f, Vector2.zero);
                AddPart("PlateR", parent, pr.ToMesh("PlateR"), plateMatR, lod0, false, lod1).transform.localPosition = new Vector3(0, bot + 0.13f, Z(0f) - 0.012f);
            }
        }

        private GameObject MakeDetachable(string name, Transform parent, MeshBuilder mb, Material mat, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mesh = mb.ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            lod0.Add(r); lod1.Add(r);
            model.allRenderers.Add(r);
            detachables.Add(go.transform);
            return go;
        }

        // ---------------------------------------------------------------- wheels
        private void BuildWheels(Transform parent)
        {
            var positions = WheelPositions();
            var visuals = new List<WheelVisual>();
            int rimVariant = 0;
            var rimDef = GameCatalog.GetCosmetic(CosmeticCategory.Rims, setup.rimId);
            if (rimDef != null) rimVariant = rimDef.variant;
            Mesh tireR = null, rimR = null, tireL = null, rimL = null;
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                bool right = p.x > 0f;
                bool front = p.z > 0f;
                var steer = new GameObject((front ? "F" : "R") + (right ? "R" : "L") + "_Steer").transform;
                steer.SetParent(parent, false);
                steer.localPosition = p;
                var spin = new GameObject("Spin").transform;
                spin.SetParent(steer, false);

                if (right) { if (tireR == null) { BuildWheelMeshes(true, rimVariant, out tireR, out rimR); } }
                else { if (tireL == null) { BuildWheelMeshes(false, rimVariant, out tireL, out rimL); } }
                var t = right ? tireR : tireL; var rm = right ? rimR : rimL;
                AddPart("Tire", spin, t, tireMat, lod0, true, lod1);
                AddPart("Rim", spin, rm, rimMat, lod0, false, lod1);
                visuals.Add(new WheelVisual { steerPivot = steer, spinPivot = spin, front = front, right = right, x = p.x, z = p.z });
            }
            model.wheels = visuals.ToArray();
        }

        private void BuildWheelMeshes(bool right, int variant, out Mesh tire, out Mesh rim)
        {
            float R = def.wheelRadius, w = def.tireWidth;
            float rimR = R * 0.64f;
            Quaternion toX = right ? Quaternion.Euler(0, 0, -90f) : Quaternion.Euler(0, 0, 90f);   // +Y (outward) -> +/-X
            var tb = new MeshBuilder();
            Vector2[] prof =
            {
                new Vector2(rimR * 0.97f, -w * 0.5f), new Vector2(R - 0.045f, -w * 0.5f), new Vector2(R - 0.012f, -w * 0.42f),
                new Vector2(R, -w * 0.25f), new Vector2(R, w * 0.25f), new Vector2(R - 0.012f, w * 0.42f),
                new Vector2(R - 0.045f, w * 0.5f), new Vector2(rimR * 0.97f, w * 0.5f)
            };
            tb.AddLathe(prof, 24, Vector3.zero, toX);
            // brake disc + caliper (dark, visible through spokes)
            tb.AddCylinder(toX * new Vector3(0, -w * 0.18f, 0), rimR * 0.92f, 0.02f, toX, 20, true);
            tire = tb.ToMesh(right ? "TireR" : "TireL");

            var rb = new MeshBuilder();
            // barrel / lip
            float depth = variant == 3 ? 0.07f : 0.03f;
            Vector2[] lip = { new Vector2(rimR * 0.78f, w * 0.34f), new Vector2(rimR, w * 0.34f), new Vector2(rimR, w * 0.5f - 0.01f), new Vector2(rimR * 0.96f, w * 0.5f + 0.004f) };
            rb.AddLathe(lip, 24, Vector3.zero, toX);
            // hub
            rb.AddCylinder(toX * new Vector3(0, w * 0.40f, 0), rimR * 0.20f, 0.03f, toX, 12, true);
            int spokes = variant == 1 ? 6 : (variant == 2 ? 12 : (variant == 4 ? 10 : 5));
            float spokeW = variant == 2 ? 0.014f : (variant == 4 ? 0.022f : 0.045f);
            for (int s = 0; s < spokes; s++)
            {
                float a = s / (float)spokes * 360f;
                Quaternion q = toX * Quaternion.Euler(0, a, 0);
                // spoke runs along local +X of the wheel face rotated around the axle
                Vector3 mid = q * new Vector3(rimR * 0.52f, w * 0.38f - depth * 0.3f, 0);
                rb.AddBox(mid, new Vector3(rimR * 0.82f, 0.018f, spokeW), q, Vector2.zero);
                if (variant == 4)
                {
                    Quaternion q2 = toX * Quaternion.Euler(0, a + 9f, 0);
                    rb.AddBox(q2 * new Vector3(rimR * 0.52f, w * 0.38f - depth * 0.3f, 0), new Vector3(rimR * 0.82f, 0.018f, spokeW), q2, Vector2.zero);
                }
            }
            if (variant == 2 || variant == 3)
                rb.AddCylinder(toX * new Vector3(0, w * 0.37f, 0), rimR * 0.62f, 0.008f, toX, 20, false);
            rim = rb.ToMesh(right ? "RimR" : "RimL");
        }

        // ---------------------------------------------------------------- lights
        private void BuildLights(Transform parent)
        {
            float yFaceMid = Mathf.Lerp(BotY(1f), Y(sp.noseTop), 0.52f);
            float xh = 0.5f * W * 0.60f;
            var head = new MeshBuilder(); var tail = new MeshBuilder();
            var il = new MeshBuilder(); var ir = new MeshBuilder(); var rv = new MeshBuilder();
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                head.AddBox(new Vector3(sgn * xh, yFaceMid + 0.03f, Z(1f) + 0.006f), new Vector3(0.27f, 0.10f, 0.03f));
                var ind = sgn < 0 ? il : ir;
                ind.AddBox(new Vector3(sgn * (xh + 0.19f), yFaceMid, Z(1f) + 0.005f), new Vector3(0.10f, 0.07f, 0.03f));
                float yTail = Mathf.Lerp(BotY(0f), Y(sp.trunkTop * 0.90f), 0.55f);
                tail.AddBox(new Vector3(sgn * xh * 1.05f, yTail + 0.02f, Z(0f) - 0.006f), new Vector3(0.30f, 0.10f, 0.03f));
                ind.AddBox(new Vector3(sgn * (xh * 1.05f + 0.20f), yTail + 0.02f, Z(0f) - 0.005f), new Vector3(0.09f, 0.08f, 0.03f));
                rv.AddBox(new Vector3(sgn * (xh * 0.45f), yTail - 0.035f, Z(0f) - 0.006f), new Vector3(0.10f, 0.045f, 0.03f));
            }
            var hr = AddPart("Headlights", parent, head.ToMesh("Head"), headMat, lod0, false, lod1);
            AddPart("Taillights", parent, tail.ToMesh("Tail"), tailMat, lod0, false, lod1);
            AddPart("IndicatorsL", parent, il.ToMesh("IndL"), indLMat, lod0, false, lod1);
            AddPart("IndicatorsR", parent, ir.ToMesh("IndR"), indRMat, lod0, false, lod1);
            AddPart("Reverse", parent, rv.ToMesh("Rev"), revMat, lod0, false, lod1);

            var lights = root.AddComponent<CarLights>();
            lights.headMat = headMat; lights.tailMat = tailMat; lights.indLeftMat = indLMat; lights.indRightMat = indRMat;
            lights.reverseMat = revMat; lights.neonMat = neonMat; lights.beamMat = beamMat;

            // real spot lights (enabled by CarLights at night / rain)
            var spots = new List<Light>();
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                var lg = new GameObject("HeadSpot");
                lg.transform.SetParent(parent, false);
                lg.transform.localPosition = new Vector3(sgn * xh, yFaceMid + 0.03f, Z(1f) + 0.1f);
                lg.transform.localRotation = Quaternion.Euler(4f, 0, 0);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 38f; l.spotAngle = 62f; l.intensity = 2.4f; l.color = new Color(1f, 0.94f, 0.80f);
                l.shadows = LightShadows.None; l.enabled = false;
                spots.Add(l);
            }
            lights.headLights = spots.ToArray();

            // volumetric beam cones
            var beams = new List<Renderer>();
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                var cone = BuildBeamMesh();
                var go = MeshUtil.Make("Beam", parent, cone, beamMat, false, false);
                go.transform.localPosition = new Vector3(sgn * xh, yFaceMid + 0.03f, Z(1f) + 0.05f);
                go.transform.localRotation = Quaternion.Euler(3f, sgn * 2f, 0);
                var br = go.GetComponent<Renderer>();
                br.enabled = false;
                beams.Add(br);
                lod0.Add(br);
                model.allRenderers.Add(br);
            }
            lights.beamRenderers = beams.ToArray();
            model.lights = lights;
        }

        private static Mesh BuildBeamMesh()
        {
            // double-sided open cone along +Z, uv.y = distance
            var mb = new MeshBuilder();
            int seg = 8; float len = 26f, r1 = 4.8f, r0 = 0.12f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0 * 0.6f, 0), p1 = new Vector3(Mathf.Cos(a1) * r0, Mathf.Sin(a1) * r0 * 0.6f, 0);
                Vector3 q0 = new Vector3(Mathf.Cos(a0) * r1, Mathf.Sin(a0) * r1 * 0.55f - 0.5f, len), q1 = new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1 * 0.55f - 0.5f, len);
                mb.AddQuad(p0, q0, q1, p1, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                mb.AddQuad(p0, p1, q1, q0, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
            return mb.ToMesh("Beam");
        }

        // ---------------------------------------------------------------- interior
        private void BuildInterior(Transform parent)
        {
            if (traffic) return;
            var mb = new MeshBuilder();
            float zMid = Mathf.Lerp(sp.cabRear, sp.cabFront, 0.55f);
            float yS = Y(sp.belt) - 0.08f;
            float hw = 0.5f * W * 0.34f;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                mb.AddBox(new Vector3(sgn * hw, yS, Z(zMid)), new Vector3(0.46f, 0.12f, 0.46f));                      // cushion
                mb.AddBox(new Vector3(sgn * hw, yS + 0.26f, Z(zMid) - 0.22f), new Vector3(0.46f, 0.50f, 0.08f));       // backrest
            }
            mb.AddBox(new Vector3(0, yS - 0.02f, Z(sp.cabRear + 0.04f)), new Vector3(W * 0.7f, 0.2f, 0.3f));          // rear shelf
            mb.AddBox(new Vector3(0, yS + 0.05f, Z(sp.cabFront) - 0.18f), new Vector3(W * 0.82f, 0.17f, 0.34f));      // dashboard
            AddPart("Interior", parent, mb.ToMesh("Interior"), interiorMat, lod0, false);

            var sw = new MeshBuilder();
            Vector2[] prof = { new Vector2(0.17f, -0.012f), new Vector2(0.20f, -0.012f), new Vector2(0.20f, 0.012f), new Vector2(0.17f, 0.012f) };
            sw.AddLathe(prof, 18, Vector3.zero, Quaternion.Euler(70f, 0, 0));
            var swGo = new GameObject("SteeringWheel");
            swGo.transform.SetParent(parent, false);
            swGo.transform.localPosition = new Vector3(-hw, yS + 0.22f, Z(sp.cabFront) - 0.46f);
            swGo.transform.localRotation = Quaternion.identity;
            AddPart("SteeringWheelMesh", swGo.transform, sw.ToMesh("SW"), interiorMat, lod0, false);
            model.steeringWheel = swGo.transform;

            var seat = new GameObject("DriverSeat").transform;
            seat.SetParent(parent, false);
            seat.localPosition = new Vector3(-hw, yS + 0.06f, Z(zMid) - 0.02f);
            model.driverSeat = seat;
        }

        private void BuildCameras()
        {
            float zMid = Mathf.Lerp(sp.cabRear, sp.cabFront, 0.55f);
            float yS = Y(sp.belt) - 0.08f;
            float hw = 0.5f * W * 0.34f;
            var cp = new GameObject("CockpitCam").transform;
            cp.SetParent(model.bodyRoot, false);
            cp.localPosition = new Vector3(-hw, yS + 0.52f, Z(zMid) - 0.02f);
            model.cockpitCamera = cp;
            var hc = new GameObject("HoodCam").transform;
            hc.SetParent(model.bodyRoot, false);
            hc.localPosition = new Vector3(0f, Y(TopF(0.86f)) + 0.22f, Z(0.80f));
            model.hoodCamera = hc;
        }

        // ---------------------------------------------------------------- kit / spoiler
        private void BuildKitAndSpoiler(Transform parent)
        {
            var kitDef = GameCatalog.GetCosmetic(CosmeticCategory.BodyKit, setup.bodykitId);
            int kit = kitDef != null ? kitDef.variant : 0;
            if (kit > 0)
            {
                var mb = new MeshBuilder();
                float hw = HalfW(0.5f);
                // front lip + rear diffuser
                mb.AddBox(new Vector3(0, bot + 0.035f, Z(1f) + 0.03f), new Vector3(W * 0.98f, 0.045f, 0.20f));
                mb.AddBox(new Vector3(0, bot + 0.035f, Z(0f) - 0.03f), new Vector3(W * 0.94f, 0.045f, 0.16f));
                // side skirts
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    mb.AddBox(new Vector3(sgn * (hw * 0.95f + 0.02f), bot + 0.04f, Z(0.5f)), new Vector3(0.06f, 0.07f, L * 0.42f));
                    if (kit == 2)
                        foreach (var wp in WheelPositions())
                            if (Mathf.Sign(wp.x) == sgn)
                            {
                                // over-fender: a flat arch lip on top of the wheel
                                mb.AddBox(new Vector3(sgn * (hw * 0.96f + 0.05f), wp.y + def.wheelRadius * 0.95f, wp.z), new Vector3(0.14f, 0.05f, def.wheelRadius * 2.5f));
                                mb.AddBox(new Vector3(sgn * (hw * 0.96f + 0.11f), wp.y + def.wheelRadius * 0.45f, wp.z), new Vector3(0.05f, def.wheelRadius * 1.2f, def.wheelRadius * 2.4f));
                            }
                    if (kit == 3) mb.AddBox(new Vector3(sgn * (hw * 0.5f), Y(sp.hoodTop) + 0.045f, Z(0.80f)), new Vector3(0.28f, 0.05f, 0.28f)); // rally hood scoop
                }
                AddPart("BodyKit", parent, mb.ToMesh("BodyKit"), trimMat, lod0, false, lod1);
            }

            var spDef = GameCatalog.GetCosmetic(CosmeticCategory.Spoiler, setup.spoilerId);
            int sv = spDef != null ? spDef.variant : 0;
            if (sv > 0)
            {
                var mb = new MeshBuilder();
                float yDeck = Y(sp.trunkTop);
                float zr = Z(0.045f);
                switch (sv)
                {
                    case 1: mb.AddBox(new Vector3(0, 0.03f, 0), new Vector3(W * 0.78f, 0.035f, 0.12f), Quaternion.Euler(-8f, 0, 0), Vector2.zero); break;
                    case 2:
                        mb.AddBox(new Vector3(0, 0.07f, 0), new Vector3(W * 0.88f, 0.045f, 0.24f), Quaternion.Euler(-12f, 0, 0), Vector2.zero);
                        mb.AddBox(new Vector3(-W * 0.43f, 0.07f, 0), new Vector3(0.025f, 0.12f, 0.26f));
                        mb.AddBox(new Vector3(W * 0.43f, 0.07f, 0), new Vector3(0.025f, 0.12f, 0.26f));
                        break;
                    case 3:
                        mb.AddBox(new Vector3(0, 0.30f, 0), new Vector3(W * 0.96f, 0.04f, 0.30f), Quaternion.Euler(-9f, 0, 0), Vector2.zero);
                        mb.AddBox(new Vector3(-W * 0.30f, 0.14f, 0), new Vector3(0.035f, 0.30f, 0.12f));
                        mb.AddBox(new Vector3(W * 0.30f, 0.14f, 0), new Vector3(0.035f, 0.30f, 0.12f));
                        mb.AddBox(new Vector3(-W * 0.48f, 0.30f, 0), new Vector3(0.02f, 0.14f, 0.32f));
                        mb.AddBox(new Vector3(W * 0.48f, 0.30f, 0), new Vector3(0.02f, 0.14f, 0.32f));
                        break;
                    default:
                        mb.AddBox(new Vector3(0, 0.44f, 0), new Vector3(W * 1.02f, 0.05f, 0.42f), Quaternion.Euler(-12f, 0, 0), Vector2.zero);
                        mb.AddBox(new Vector3(0, 0.50f, 0.14f), new Vector3(W * 1.02f, 0.03f, 0.18f), Quaternion.Euler(-30f, 0, 0), Vector2.zero);
                        mb.AddBox(new Vector3(-W * 0.28f, 0.22f, 0), new Vector3(0.05f, 0.44f, 0.16f));
                        mb.AddBox(new Vector3(W * 0.28f, 0.22f, 0), new Vector3(0.05f, 0.44f, 0.16f));
                        mb.AddBox(new Vector3(-W * 0.51f, 0.40f, 0), new Vector3(0.025f, 0.22f, 0.46f));
                        mb.AddBox(new Vector3(W * 0.51f, 0.40f, 0), new Vector3(0.025f, 0.22f, 0.46f));
                        break;
                }
                var go = new GameObject("Spoiler");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0, yDeck, zr);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Spoiler");
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = trimMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                lod0.Add(r); lod1.Add(r);
                model.allRenderers.Add(r);
                detachables.Add(go.transform);
            }
        }

        // ---------------------------------------------------------------- decals
        private void BuildDecals(Transform parent)
        {
            var dDef = GameCatalog.GetCosmetic(CosmeticCategory.Decal, setup.decalId);
            int v = dDef != null ? dDef.variant : 0;
            if (v == 0) return;
            Color tint = paintColor.grayscale > 0.55f ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            if (v == 4) tint = Color.white;
            var tex = ProcTex.Decal(v);
            var mat = MatLib.Alpha(tex, tint);
            float zD = Mathf.Lerp(sp.cabRear, sp.cabFront, 0.5f);
            float hw = HalfW(zD) * 0.93f + 0.008f;
            float yD = Mathf.Lerp(bot + 0.05f, Y(sp.belt), 0.45f);
            float size = Mathf.Min(0.62f, (Y(sp.belt) - bot) * 0.8f);
            var mb = new MeshBuilder();
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 n = new Vector3(sgn, 0, 0);
                float length = (v == 2 || v == 5 || v == 4) ? L * 0.55f : size;
                mb.AddFace(new Vector3(sgn * hw, yD, Z(zD)), n, Vector3.Cross(n, Vector3.up), length, size, Vector2.zero);
            }
            // bonnet decal for number style
            if (v == 1)
            {
                float yh = Y(TopF(0.8f)) + 0.006f;
                mb.AddFace(new Vector3(0, yh, Z(0.80f)), Vector3.up, Vector3.right, 0.5f, 0.5f, Vector2.zero);
            }
            AddPart("Decals", parent, mb.ToMesh("Decals"), mat, lod0, false);
        }

        // ---------------------------------------------------------------- neon
        private void BuildNeon(Transform parent)
        {
            var nDef = GameCatalog.GetCosmetic(CosmeticCategory.Neon, setup.neonId);
            Color nc = nDef != null && nDef.id != "none" ? nDef.color : Color.clear;
            var root2 = new GameObject("Neon").transform;
            root2.SetParent(parent, false);
            model.neonRoot = root2;
            var mb = new MeshBuilder();
            float hw = HalfW(0.5f) * 0.82f;
            mb.AddBox(new Vector3(-hw, bot - 0.005f, 0), new Vector3(0.025f, 0.025f, L * 0.7f));
            mb.AddBox(new Vector3(hw, bot - 0.005f, 0), new Vector3(0.025f, 0.025f, L * 0.7f));
            mb.AddBox(new Vector3(0, bot - 0.005f, Z(0.85f)), new Vector3(hw * 2f, 0.025f, 0.025f));
            mb.AddBox(new Vector3(0, bot - 0.005f, Z(0.15f)), new Vector3(hw * 2f, 0.025f, 0.025f));
            var neonR = AddPart("NeonTubes", root2, mb.ToMesh("Neon"), neonMat, lod0, false, lod1);
            // ground glow quad
            var gq = new MeshBuilder();
            gq.AddFace(new Vector3(0, g + 0.035f, 0), Vector3.up, Vector3.right, W * 1.7f, L * 1.35f, Vector2.zero);
            var gm = MatLib.Additive(ProcTex.Glow(), new Color(nc.r, nc.g, nc.b, 1f) * 0.9f);
            var glow = AddPart("NeonGlow", root2, gq.ToMesh("NeonGlow"), gm, lod0, false);
            bool on = nc.a > 0.01f;
            root2.gameObject.SetActive(on);
            if (on)
            {
                model.lights.SetNeon(nc);
                if (QualityManager.Current.additionalLights >= 4)
                {
                    var lg = new GameObject("NeonLight");
                    lg.transform.SetParent(root2, false);
                    lg.transform.localPosition = new Vector3(0, g + 0.3f, 0);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Point; l.range = 6f; l.intensity = 1.6f; l.color = nc; l.shadows = LightShadows.None;
                    model.lights.neonLight = l;
                }
            }
        }
    }
}
