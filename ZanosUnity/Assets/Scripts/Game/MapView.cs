using System.Collections.Generic;
using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>Строит 3D-сцену карты из MapData: покрытия, разметка, здания, контейнеры, деревья, фонари, барьеры, конусы и бочки.</summary>
    public sealed class MapView
    {
        public readonly GameObject Root;
        readonly MapData map; readonly bool night; readonly string weather;
        readonly List<Transform> propTf = new List<Transform>();
        readonly List<Vector3> lampPos = new List<Vector3>();
        public IList<Vector3> Lamps { get { return lampPos; } }
        readonly List<Light> lampLights = new List<Light>();
        GameObject gateRoot; Material gateActive, gateIdle, gateDone;

        public MapView(MapData m, string time, string weather)
        {
            map = m; night = time == "night"; this.weather = weather;
            Root = new GameObject("Map-" + m.id);
            BuildSurfaces(); BuildMarkings(); BuildBuildings(); BuildContainers(); BuildParked(); BuildTrees(); BuildLamps(); BuildBarriers(); BuildProps(); BuildPads();
            if (m.theme == "mountain") BuildMountains();
        }

        string Wet(string type) { return weather == "rain" && (type == "asphalt" || type == "concrete") ? "wet" : weather == "snow" && type != "wet" ? "snow" : type; }

        static Mesh Bake(MeshBuilder mb, string name) { return mb.ToMesh(name); }
        GameObject Spawn(string name, Mesh mesh, Material mat, bool shadow = true, bool receive = true)
        {
            var go = new GameObject(name); go.transform.SetParent(Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = receive;
            return go;
        }

        static List<Vector2> Pts(double[] a) { var l = new List<Vector2>(); for (int i = 0; i + 1 < a.Length; i += 2) l.Add(new Vector2((float)a[i], (float)a[i + 1])); return l; }

        void BuildSurfaces()
        {
            // земля
            string gt = map.theme == "mountain" ? "grass" : map.theme == "parking" ? "asphalt" : map.ground;
            var gb = new MeshBuilder(); gb.AddRect(-800, -800, 800, 800, -0.02f, 8f);
            Spawn("Ground", Bake(gb, "ground"), Mats.Surface(Wet(gt)), false, true);
            int y = 0;
            foreach (var s in map.surfaces)
            {
                y++; var mb = new MeshBuilder(); float h = 0.004f * y; string type = Wet(s.type);
                if (s.kind == "rect") mb.AddRect((float)s.x0, (float)s.z0, (float)s.x1, (float)s.z1, h, 7f);
                else if (s.kind == "circle") mb.AddDisc((float)s.x, (float)s.z, (float)s.r, h, 7f);
                else mb.AddStrip(Pts(s.pts), (float)(s.hw * 2), h, s.closed, 7f);
                Spawn("Surf-" + s.type + y, Bake(mb, "surf"), Mats.Surface(type), false, true);
            }
        }

        void BuildMarkings()
        {
            var groups = new Dictionary<int, MeshBuilder>();
            foreach (var k in map.markings)
            {
                MeshBuilder mb; if (!groups.TryGetValue(k.color, out mb)) { mb = new MeshBuilder(); groups[k.color] = mb; }
                var pts = Pts(k.pts);
                if (k.dashOn <= 0) { mb.AddStrip(pts, (float)k.w, 0.06f, k.closed); continue; }
                // пунктир: идём по ломаной и добавляем отрезки «включено»
                float carry = 0; bool on = true; int n = pts.Count;
                for (int i = 0; i < (k.closed ? n : n - 1); i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % n]; float L = (b - a).magnitude; if (L < 1e-4f) continue; Vector2 d = (b - a) / L; float t = 0;
                    while (t < L)
                    {
                        float lim = on ? (float)k.dashOn : (float)k.dashOff; float step = Mathf.Min(lim - carry, L - t);
                        if (on) mb.AddStrip(new[] { a + d * t, a + d * (t + step) }, (float)k.w, 0.06f);
                        t += step; carry += step; if (carry >= lim - 1e-6f) { on = !on; carry = 0; }
                    }
                }
            }
            foreach (var kv in groups) Spawn("Marks", Bake(kv.Value, "marks"), Mats.Solid(weather == "snow" ? new Color(0.85f, 0.85f, 0.85f) : Mats.Rgb(kv.Key), 0.2f), false, true);
        }

        void BuildBuildings()
        {
            foreach (var b in map.buildings)
            {
                var mb = new MeshBuilder(); float ru = Mathf.Max(1, Mathf.Round((float)b.w / 6f)), rv = Mathf.Max(1, Mathf.Round((float)b.h / 3.4f));
                mb.AddBox(Vector3.zero, new Vector3((float)b.w, (float)b.h, (float)b.d), 0, 0, 0, ru / Mathf.Max(0.01f, (float)b.w), rv / Mathf.Max(0.01f, (float)b.h));
                var go = Spawn("Building", Bake(mb, "bld"), Mats.Facade(Mats.Rgb(b.color), night));
                go.transform.position = new Vector3((float)b.x, (float)b.h * 0.5f, (float)b.z); go.transform.rotation = Quaternion.Euler(0, (float)(b.rot * Mathf.Rad2Deg), 0);
            }
        }

        void BuildContainers()
        {
            var byColor = new Dictionary<int, MeshBuilder>();
            foreach (var c in map.containers)
            {
                MeshBuilder mb; if (!byColor.TryGetValue(c.color, out mb)) { mb = new MeshBuilder(); byColor[c.color] = mb; }
                mb.AddBox(new Vector3((float)c.x, (float)c.y + (float)(c.d > 5 ? 1.3 : 0.7), (float)c.z), new Vector3((float)c.w, c.d > 5 ? 2.6f : 1.4f, (float)c.d), (float)(c.rot * Mathf.Rad2Deg));
            }
            foreach (var kv in byColor) Spawn("Containers", Bake(kv.Value, "cont"), Mats.Solid(Mats.Rgb(kv.Key), 0.4f, 0.2f));
        }

        void BuildParked()
        {
            if (map.parked.Length == 0) return;
            var byColor = new Dictionary<int, MeshBuilder>(); var glass = new MeshBuilder(); var wheels = new MeshBuilder();
            foreach (var p in map.parked)
            {
                MeshBuilder mb; if (!byColor.TryGetValue(p.color, out mb)) { mb = new MeshBuilder(); byColor[p.color] = mb; }
                float yaw = (float)(p.rot * Mathf.Rad2Deg); var rot = Quaternion.Euler(0, yaw, 0); var c = new Vector3((float)p.x, 0, (float)p.z);
                mb.AddBox(c + rot * new Vector3(0, 0.62f, 0), new Vector3(1.8f, 0.62f, 4.2f), yaw);
                glass.AddBox(c + rot * new Vector3(0, 1.1f, -0.1f), new Vector3(1.6f, 0.5f, 2.1f), yaw);
                foreach (var w in new[] { new Vector3(-0.82f, 0.31f, 1.35f), new Vector3(0.82f, 0.31f, 1.35f), new Vector3(-0.82f, 0.31f, -1.35f), new Vector3(0.82f, 0.31f, -1.35f) })
                    wheels.AddBox(c + rot * w, new Vector3(0.22f, 0.62f, 0.62f), yaw);
            }
            foreach (var kv in byColor) Spawn("Parked", Bake(kv.Value, "parked"), Mats.Paint(Mats.Rgb(kv.Key)));
            Spawn("ParkedGlass", Bake(glass, "pg"), Mats.Solid(new Color(0.08f, 0.1f, 0.13f), 0.9f)); Spawn("ParkedWheels", Bake(wheels, "pw"), Mats.Solid(new Color(0.06f, 0.06f, 0.07f), 0.1f));
        }

        void BuildTrees()
        {
            if (map.trees.Length == 0) return;
            var trunk = new MeshBuilder(); var crowns = new Dictionary<int, MeshBuilder>(); bool pine = map.theme == "mountain"; int i = 0;
            foreach (var t in map.trees)
            {
                float s = (float)t.s; trunk.AddCylinder(new Vector3((float)t.x, 0, (float)t.z), 0.22f * s, 0.15f * s, 2.2f * s, 6);
                int key = i % 3; MeshBuilder mb; if (!crowns.TryGetValue(key, out mb)) { mb = new MeshBuilder(); crowns[key] = mb; }
                if (pine) { mb.AddCylinder(new Vector3((float)t.x, 1.4f * s, (float)t.z), 1.7f * s, 0.05f, 5.5f * s, 8); }
                else { mb.AddCylinder(new Vector3((float)t.x, 2.0f * s, (float)t.z), 1.6f * s, 0.6f * s, 3.2f * s, 8); }
                i++;
            }
            Spawn("Trunks", Bake(trunk, "trunks"), Mats.Solid(new Color(0.29f, 0.21f, 0.14f), 0.05f), true, true);
            Color[] cols = weather == "snow" ? new[] { new Color(0.85f, 0.9f, 0.93f), new Color(0.8f, 0.86f, 0.9f), new Color(0.9f, 0.93f, 0.95f) }
                          : pine ? new[] { new Color(0.12f, 0.29f, 0.16f), new Color(0.14f, 0.33f, 0.18f), new Color(0.1f, 0.25f, 0.15f) } : new[] { new Color(0.25f, 0.48f, 0.2f), new Color(0.29f, 0.54f, 0.22f), new Color(0.21f, 0.41f, 0.17f) };
            foreach (var kv in crowns) Spawn("Crowns", Bake(kv.Value, "crown"), Mats.Solid(cols[kv.Key], 0.05f));
        }

        void BuildLamps()
        {
            if (map.lamps.Length == 0) return;
            var poles = new MeshBuilder(); var heads = new MeshBuilder(); bool glow = night || map.time == "dusk";
            foreach (var l in map.lamps)
            {
                poles.AddCylinder(new Vector3((float)l.x, 0, (float)l.z), 0.12f, 0.09f, (float)l.h, 6);
                heads.AddBox(new Vector3((float)l.x, (float)l.h + 0.05f, (float)l.z), new Vector3(0.9f, 0.12f, 0.35f));
                lampPos.Add(new Vector3((float)l.x, (float)l.h - 0.3f, (float)l.z));
            }
            Spawn("LampPoles", Bake(poles, "poles"), Mats.Solid(new Color(0.27f, 0.28f, 0.31f), 0.4f, 0.5f));
            Spawn("LampHeads", Bake(heads, "heads"), glow ? Mats.Emissive(new Color(1f, 0.89f, 0.63f), 2.2f) : Mats.Solid(new Color(0.8f, 0.8f, 0.75f)), false, false);
            if (glow) for (int i = 0; i < 4; i++) { var go = new GameObject("LampLight" + i); go.transform.SetParent(Root.transform, false); var li = go.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.85f, 0.6f); li.range = 32; li.intensity = night ? 3f : 1.6f; li.shadows = LightShadows.None; lampLights.Add(li); }
        }

        /// <summary>Подтягиваем 4 ближайших световых пятна фонарей к машине (как в браузерной версии).</summary>
        public void UpdateLamps(Vector3 carPos)
        {
            if (lampLights.Count == 0) return;
            var near = new List<Vector3>(lampPos); near.Sort((a, b) => (a - carPos).sqrMagnitude.CompareTo((b - carPos).sqrMagnitude));
            for (int i = 0; i < lampLights.Count && i < near.Count; i++) lampLights[i].transform.position = near[i];
        }

        void BuildBarriers()
        {
            var byStyle = new Dictionary<string, MeshBuilder>(); var posts = new MeshBuilder();
            foreach (var b in map.barriers)
            {
                MeshBuilder mb; if (!byStyle.TryGetValue(b.style, out mb)) { mb = new MeshBuilder(); byStyle[b.style] = mb; }
                var pts = Pts(b.pts); int n = pts.Count; float h, th, y0 = 0; 
                switch (b.style) { case "wall": h = 3.2f; th = 0.6f; break; case "curb": h = 0.28f; th = 0.5f; break; case "guardrail": h = 0.34f; th = 0.1f; y0 = 0.55f; break; case "tyres": h = 0.9f; th = 0.6f; break; default: h = 0.95f; th = 0.55f; break; }
                for (int i = 0; i < (b.closed ? n : n - 1); i++)
                {
                    Vector2 a = pts[i], c = pts[(i + 1) % n]; Vector2 d = c - a; float L = d.magnitude; if (L < 0.01f) continue;
                    mb.AddBox(new Vector3((a.x + c.x) * 0.5f, y0 + h * 0.5f, (a.y + c.y) * 0.5f), new Vector3(th, h, L + 0.02f), Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);
                    if (b.style == "guardrail" && i % 3 == 0) posts.AddBox(new Vector3(a.x, 0.45f, a.y), new Vector3(0.1f, 0.9f, 0.1f));
                }
            }
            foreach (var kv in byStyle)
            {
                Color col = kv.Key == "wall" ? new Color(0.55f, 0.56f, 0.58f) : kv.Key == "curb" ? new Color(0.79f, 0.79f, 0.76f) : kv.Key == "guardrail" ? new Color(0.76f, 0.78f, 0.8f) : kv.Key == "tyres" ? new Color(0.15f, 0.15f, 0.16f) : new Color(0.73f, 0.74f, 0.75f);
                Spawn("Barrier-" + kv.Key, Bake(kv.Value, "bar"), Mats.Solid(col, kv.Key == "guardrail" ? 0.6f : 0.15f, kv.Key == "guardrail" ? 0.6f : 0f));
            }
            if (posts.VertexCount > 0) Spawn("RailPosts", Bake(posts, "posts"), Mats.Solid(new Color(0.54f, 0.56f, 0.58f), 0.4f, 0.5f));
        }

        void BuildProps()
        {
            foreach (var p in map.props)
            {
                var mb = new MeshBuilder(); Material mat;
                if (p.type == "cone") { mb.AddCylinder(Vector3.zero, 0.2f, 0.03f, 0.62f, 10); mat = Mats.Solid(new Color(1f, 0.42f, 0.1f), 0.3f); }
                else { mb.AddCylinder(Vector3.zero, 0.3f, 0.3f, 0.9f, 12); mat = Mats.Solid(p.color != 0 ? Mats.Rgb(p.color) : new Color(0.85f, 0.33f, 0.12f), 0.4f, 0.2f); }
                var go = Spawn(p.type, Bake(mb, p.type), mat); go.transform.position = new Vector3((float)p.x, 0, (float)p.z); propTf.Add(go.transform);
            }
        }

        void BuildPads()
        {
            foreach (var p in map.pads)
            {
                if (p.kind == "fountain") { var mb = new MeshBuilder(); mb.AddCylinder(Vector3.zero, (float)p.r, (float)p.r, 0.7f, 28); var go = Spawn("Fountain", Bake(mb, "f"), Mats.Solid(new Color(0.71f, 0.71f, 0.69f), 0.2f)); go.transform.position = new Vector3((float)p.x, 0, (float)p.z); }
                else if (p.kind == "tank") { var mb = new MeshBuilder(); mb.AddCylinder(Vector3.zero, (float)p.r, (float)p.r, 10f, 20); var go = Spawn("Tank", Bake(mb, "t"), Mats.Solid(new Color(0.66f, 0.68f, 0.71f), 0.5f, 0.5f)); go.transform.position = new Vector3((float)p.x, 0, (float)p.z); }
                else if (p.kind == "bollard") { var mb = new MeshBuilder(); mb.AddCylinder(Vector3.zero, (float)p.r, (float)p.r, 1.1f, 12); var go = Spawn("Bollard", Bake(mb, "b"), Mats.Solid(new Color(0.95f, 0.79f, 0.3f), 0.3f)); go.transform.position = new Vector3((float)p.x, 0, (float)p.z); }
            }
        }

        void BuildMountains()
        {
            var mb = new MeshBuilder(); var rnd = new System.Random(5);
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2, d = 420 + (float)rnd.NextDouble() * 160, h = 90 + (float)rnd.NextDouble() * 140, w = 120 + (float)rnd.NextDouble() * 120;
                mb.AddCylinder(new Vector3(Mathf.Cos(a) * d, -2, Mathf.Sin(a) * d + 100), w, 1f, h, 7);
            }
            Spawn("Mountains", Bake(mb, "mtn"), Mats.Solid(weather == "snow" ? new Color(0.9f, 0.93f, 0.95f) : new Color(0.36f, 0.4f, 0.44f), 0.05f), false, false);
        }

        // ---- динамика ----
        public void UpdateProps(IList<Prop> props)
        {
            for (int i = 0; i < props.Count && i < propTf.Count; i++)
            {
                var p = props[i]; var t = propTf[i]; float sp = Mathf.Sqrt((float)(p.Vx * p.Vx + p.Vz * p.Vz));
                float tilt = p.Hit ? Mathf.Min(85f, 20f + sp * 22f) : 0f;
                t.position = new Vector3((float)p.X, p.Hit ? 0.1f : 0f, (float)p.Z);
                t.rotation = Quaternion.AngleAxis((float)p.Rot * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(tilt, new Vector3(Mathf.Cos((float)p.Rot), 0, Mathf.Sin((float)p.Rot)));
            }
        }

        // ---- ворота испытаний ----
        public void ShowGates(bool show, int active)
        {
            if (gateRoot != null) Object.Destroy(gateRoot);
            if (!show) return;
            gateRoot = new GameObject("Gates"); gateRoot.transform.SetParent(Root.transform, false);
            gateActive = Mats.Transparent(new Color(0.26f, 1f, 0.48f), 0.4f); gateIdle = Mats.Transparent(new Color(0.22f, 0.77f, 1f), 0.16f); gateDone = Mats.Transparent(new Color(0.4f, 0.4f, 0.4f), 0.05f);
            for (int i = 0; i < map.gates.Length; i++)
            {
                var g = map.gates[i]; Vector2 d = new Vector2((float)(g.x2 - g.x1), (float)(g.z2 - g.z1)); float L = d.magnitude;
                var mb = new MeshBuilder(); mb.AddBox(Vector3.zero, new Vector3(0.05f, 4f, L)); 
                var go = new GameObject("Gate" + i); go.transform.SetParent(gateRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("gate"); var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = i == active ? gateActive : i < active ? gateDone : gateIdle; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = new Vector3((float)g.x, 2f, (float)g.z); go.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0);
            }
        }

        public void Destroy() { if (Root != null) Object.Destroy(Root); }
    }
}
