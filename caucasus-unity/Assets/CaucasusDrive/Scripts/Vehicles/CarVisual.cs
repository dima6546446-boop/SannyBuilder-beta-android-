using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    /// <summary>Набор материалов одной машины (цвет кузова, тонировка и лампы меняются без пересборки).</summary>
    public class CarMaterials
    {
        public Material paint, chrome, glass, black, rubber, under, plate, grille, headLamp, tailLamp, reverseLamp, indL, indR, rim, interior;
        readonly Dictionary<string, Material> map = new Dictionary<string, Material>();

        public CarMaterials(int color, float tint, bool high)
        {
            paint = (high ? Mats.Lit() : Mats.Lit()).Col(M.Hex(color)).Pbr(0.55f, high ? 0.9f : 0.82f);
            chrome = Mats.Lit().Col(M.Hex(0xe6e6e6)).Pbr(1f, 0.93f);
            glass = Mats.LitTransparent().Col(new Color(0.03f, 0.05f, 0.07f, tint)).Pbr(0.3f, 0.95f);
            black = Mats.Lit().Col(M.Hex(0x1b1b1b)).Pbr(0f, 0.35f);
            rubber = Mats.Simple().Col(M.Hex(0x151515));
            under = Mats.Simple().Col(M.Hex(0x0b0b0b));
            plate = Mats.Simple().Col(M.Hex(0xf2f2ee));
            grille = Mats.Lit().Col(M.Hex(0x161616)).Pbr(0.4f, 0.5f);
            headLamp = Mats.LitEmissive().Col(M.Hex(0xdddddd)).Pbr(0.4f, 0.95f).Emission(Color.black);
            tailLamp = Mats.LitEmissive().Col(M.Hex(0x5a0808)).Pbr(0.1f, 0.8f).Emission(Color.black);
            reverseLamp = Mats.LitEmissive().Col(M.Hex(0xbbbbbb)).Pbr(0.1f, 0.8f).Emission(Color.black);
            indL = Mats.LitEmissive().Col(M.Hex(0xb86a00)).Pbr(0.1f, 0.8f).Emission(Color.black);
            indR = Mats.LitEmissive().Col(M.Hex(0xb86a00)).Pbr(0.1f, 0.8f).Emission(Color.black);
            rim = Mats.Lit().Col(M.Hex(0xc9ccd0)).Pbr(0.9f, 0.7f);
            interior = Mats.Simple().Col(M.Hex(0x2e2724));
            map["paint"] = paint; map["chrome"] = chrome; map["glass"] = glass; map["black"] = black;
            map["rubber"] = rubber; map["under"] = under; map["plate"] = plate; map["grille"] = grille;
            map["headLamp"] = headLamp; map["tailLamp"] = tailLamp; map["reverseLamp"] = reverseLamp;
            map["indL"] = indL; map["indR"] = indR; map["trim"] = chrome; map["interior"] = interior;
        }

        public Material For(string name) { Material m; return map.TryGetValue(name, out m) ? m : black; }

        public void Destroy() { foreach (var m in new HashSet<Material>(map.Values)) Object.Destroy(m); Object.Destroy(rim); }
    }

    public class WheelVis
    {
        public Transform pivot, spin;
        public bool front;
        public int side;
    }

    /// <summary>
    /// Машина игрока/гаража: кузов из Blender (подмеши → материалы), колёса, номера, неон, свет фар.
    /// Иерархия: root → body (крен/тангаж) → подмеши; root → колёса.
    /// </summary>
    public class CarVisual
    {
        public GameObject root;
        public Transform body;
        public CarDef def;
        public CarMaterials mats;
        public readonly List<WheelVis> wheels = new List<WheelVis>();
        public Light[] headlights;
        public GameObject neon;
        Material neonMat;

        public static CarVisual Build(CarDef def, CarTune tune, int color, string lod, bool lights, Transform parent)
        {
            var cv = new CarVisual { def = def };
            cv.root = new GameObject("Car_" + def.id);
            if (parent) cv.root.transform.SetParent(parent, false);
            cv.body = new GameObject("Body").transform;
            cv.body.SetParent(cv.root.transform, false);
            cv.body.localPosition = new Vector3(0, Tuning.Height[Mathf.Clamp(tune.height, 0, 3)].value, 0);
            cv.mats = new CarMaterials(color, Tuning.Tint[Mathf.Clamp(tune.tint, 0, 2)].value, lod == "LOD_ultra");

            var L = CarMeshLibrary.Get(def.id, lod) ?? CarMeshLibrary.Get(def.id, "LOD_hi");
            if (L != null)
            {
                var go = new GameObject(lod);
                go.transform.SetParent(cv.body, false);
                go.AddComponent<MeshFilter>().sharedMesh = L.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                var arr = new Material[L.materials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = cv.mats.For(L.materials[i]);
                mr.sharedMaterials = arr;
                mr.shadowCastingMode = ShadowCastingMode.On;
                if (L.hasPlates) cv.AddPlates(L, tune);
            }
            cv.BuildWheels(Tuning.WheelIds[Mathf.Clamp(tune.wheels, 0, 5)]);
            cv.SetNeon(tune.neon);
            if (lights) cv.AddHeadlights();
            return cv;
        }

        // ------------------------------------------------------------------ колёса
        public void BuildWheels(string style)
        {
            foreach (var w in wheels) Object.Destroy(w.pivot.gameObject);
            wheels.Clear();
            if (style == "default") style = def.wheelStyle;
            var d = def.dims;
            var tireMesh = WheelMeshes.Tire(d.wheelR, d.wheelW);
            var rimMesh = WheelMeshes.Rim(d.wheelR, d.wheelW, style);
            Material rimMat = style == "steel" ? mats.black : style == "classic" ? mats.chrome : mats.rim;
            foreach (float z in new[] { d.axleF, d.axleR })
                foreach (int side in new[] { 1, -1 })
                {
                    var pivot = new GameObject(z > 0 ? "WheelF" : "WheelR").transform;
                    pivot.SetParent(root.transform, false);
                    pivot.localPosition = new Vector3(side * d.track / 2f, d.wheelR, z);
                    var spin = new GameObject("Spin").transform;
                    spin.SetParent(pivot, false);
                    var t = new GameObject("Tire"); t.transform.SetParent(spin, false);
                    t.AddComponent<MeshFilter>().sharedMesh = tireMesh;
                    t.AddComponent<MeshRenderer>().sharedMaterial = mats.rubber;
                    var r = new GameObject("Rim"); r.transform.SetParent(spin, false);
                    r.AddComponent<MeshFilter>().sharedMesh = rimMesh;
                    r.AddComponent<MeshRenderer>().sharedMaterial = rimMat;
                    // диск смотрит наружу: у левых колёс зеркалим
                    if (side < 0) spin.localScale = new Vector3(-1, 1, 1);
                    wheels.Add(new WheelVis { pivot = pivot, spin = spin, front = z > 0, side = side });
                }
        }

        // ------------------------------------------------------------------ номера
        void AddPlates(CarLod L, CarTune tune)
        {
            var font = UIKit.Font;
            foreach (bool front in new[] { true, false })
            {
                var c = front ? L.plateFront : L.plateRear;
                if (c == Vector3.zero) continue;
                var go = new GameObject(front ? "PlateF" : "PlateR");
                go.transform.SetParent(body, false);
                go.transform.localPosition = c + new Vector3(0, 0, front ? 0.012f : -0.012f);
                go.transform.localRotation = front ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                var tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.text = tune.plateText + " " + tune.plateRegion;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 64;
                tm.characterSize = 0.0105f;
                tm.color = new Color(0.07f, 0.07f, 0.07f);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        public void SetPlate(string text, string region)
        {
            foreach (var tm in body.GetComponentsInChildren<TextMesh>()) tm.text = text + " " + region;
        }

        // ------------------------------------------------------------------ неон
        public void SetNeon(int idx)
        {
            if (neon) Object.Destroy(neon);
            neon = null;
            int c = Tuning.NeonColors[Mathf.Clamp(idx, 0, Tuning.NeonColors.Length - 1)];
            if (c < 0) return;
            var d = def.dims;
            neon = new GameObject("Neon");
            neon.transform.SetParent(root.transform, false);
            var mb = new MeshBuilder();
            mb.Flat(0, (d.front + d.rear) / 2f, 0.04f, d.W + 1.4f, d.front - d.rear + 1f);
            neonMat = Mats.Additive().Col(M.Hex(c) * 0.9f).Tex(Mats.Glow);
            var go = mb.ToObject("Glow", neonMat, neon.transform, false);
            go.isStatic = false;
            var l = new GameObject("NeonLight").AddComponent<Light>();
            l.transform.SetParent(neon.transform, false);
            l.transform.localPosition = new Vector3(0, 0.25f, (d.front + d.rear) / 2f);
            l.type = LightType.Point; l.range = 4f; l.intensity = 1.5f; l.color = M.Hex(c);
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForceVertex;
        }

        // ------------------------------------------------------------------ свет
        void AddHeadlights()
        {
            headlights = new Light[1];
            var go = new GameObject("Headlight");
            go.transform.SetParent(body, false);
            go.transform.localPosition = new Vector3(0, def.headY, def.dims.front - 0.1f);
            go.transform.localRotation = Quaternion.Euler(8, 0, 0);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.range = 45f; l.spotAngle = 70f; l.intensity = 0f;
            l.color = new Color(1f, 0.93f, 0.8f);
            l.shadows = LightShadows.None;
            headlights[0] = l;
        }

        public void SetLamps(bool lightsOn, bool brake, bool reverse, bool indL, bool indR)
        {
            mats.headLamp.SetColor("_EmissionColor", lightsOn ? new Color(2.4f, 2.2f, 1.8f) : Color.black);
            float t = brake ? 3.2f : lightsOn ? 1f : 0f;
            mats.tailLamp.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.03f) * t);
            mats.reverseLamp.SetColor("_EmissionColor", reverse ? Color.white * 2f : Color.black);
            mats.indL.SetColor("_EmissionColor", indL ? new Color(3f, 1.6f, 0.1f) : Color.black);
            mats.indR.SetColor("_EmissionColor", indR ? new Color(3f, 1.6f, 0.1f) : Color.black);
            if (headlights != null) foreach (var l in headlights) l.intensity = lightsOn ? 6f : 0f;
        }

        public void SetColor(int hex) { mats.paint.Col(M.Hex(hex)); }
        public void SetTint(float a) { mats.glass.Col(new Color(0.03f, 0.05f, 0.07f, a)); }
        public void SetHeight(float h) { body.localPosition = new Vector3(0, h, 0); }

        public void Destroy()
        {
            mats.Destroy();
            if (neonMat) Object.Destroy(neonMat);
            Object.Destroy(root);
        }
    }

    /// <summary>Процедурные шины и диски разных стилей (кэш по размеру и стилю).</summary>
    public static class WheelMeshes
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Tire(float r, float w)
        {
            string key = "tire" + r + "_" + w;
            Mesh m;
            if (cache.TryGetValue(key, out m)) return m;
            var b = new MeshBuilder();
            b.CylinderX(Vector3.zero, r, w, 28, false);
            b.CylinderX(Vector3.zero, r * 0.94f, w * 1.04f, 28, false);
            // боковины
            b.Disc(new Vector3(-w / 2, 0, 0), r, 28, -1);
            b.Disc(new Vector3(w / 2, 0, 0), r, 28, 1);
            m = b.ToMesh("Tire");
            cache[key] = m;
            return m;
        }

        public static Mesh Rim(float r, float w, string style)
        {
            string key = "rim" + r + "_" + w + style;
            Mesh m;
            if (cache.TryGetValue(key, out m)) return m;
            var b = new MeshBuilder();
            float rr = r * 0.62f, x = w / 2 + 0.004f;
            b.Disc(new Vector3(x, 0, 0), rr, 24, 1);
            int spokes = style == "star" ? 5 : style == "sport" ? 10 : style == "mesh" ? 16 : style == "classic" ? 0 : 4;
            for (int i = 0; i < spokes; i++)
            {
                float a = i * Mathf.PI * 2f / spokes;
                var c = new Vector3(x + 0.008f, Mathf.Cos(a) * rr * 0.55f, Mathf.Sin(a) * rr * 0.55f);
                var go = Quaternion.Euler(a * Mathf.Rad2Deg, 0, 0); // ось Y спицы → радиус (0, cos a, sin a)
                // спица — плоская коробка, повёрнутая вокруг оси X
                var sb = new MeshBuilder();
                sb.Box(Vector3.zero, new Vector3(0.012f, rr * 0.8f, style == "mesh" ? 0.012f : 0.035f), 0f, true, false);
                for (int k = 0; k < sb.v.Count; k++)
                {
                    b.v.Add(c + go * sb.v[k]); b.n.Add(go * sb.n[k]); b.uv.Add(sb.uv[k]); b.col.Add(sb.col[k]);
                }
                int baseIdx = b.v.Count - sb.v.Count;
                foreach (int t in sb.tri) b.tri.Add(baseIdx + t);
            }
            if (style == "classic") b.Disc(new Vector3(x + 0.02f, 0, 0), rr * 0.95f, 24, 1); // колпак
            b.Disc(new Vector3(x + 0.03f, 0, 0), rr * 0.18f, 12, 1); // ступица
            m = b.ToMesh("Rim_" + style);
            cache[key] = m;
            return m;
        }
    }
}
