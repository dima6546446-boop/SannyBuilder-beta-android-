using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    public struct Surface
    {
        public int type;   // 0 — асфальт, 1 — тротуар/бордюр, 2 — трава
        public float y, mu;
    }

    public class ParkedCar
    {
        public CarDef def;
        public Vector3 pos;
        public float heading;
        public int color;
    }

    /// <summary>
    /// Город «Автозаводский район»: асфальт и разметка, кварталы (панельки 5 и 9 этажей, башни,
    /// гаражи, парк, АЗС), деревья, фонари, светофоры, билборды Telegram, забор, автодром ДОСААФ.
    /// Каждый квартал — 3–4 меша (здания, деревья) → немного draw calls; коллизии — BoxCollider.
    /// </summary>
    public class City
    {
        public readonly Transform root;
        public readonly List<ParkedCar> parked = new List<ParkedCar>();
        public readonly List<Vector3> lampHeads = new List<Vector3>();
        public readonly List<Vector4> lightHeads = new List<Vector4>(); // x, y, z, (node*4 + axis*2 + dir)
        public readonly List<int> lightNode = new List<int>();
        public readonly List<int> lightAxis = new List<int>();
        public readonly List<Vector3> lightFacing = new List<Vector3>();
        public Vector3 azs; public float azsR = 12f;
        public Material facadeMat; public Material[] facadeVariants;
        readonly Rng rnd = new Rng(2107);
        readonly RoadGraph graph;
        readonly int quality; // 0..2
        readonly Material asphalt, sidewalk, lawn, marking, roof, trunk, crown, fence, metal, brick, glass;

        public City(Transform parent, RoadGraph g, int quality)
        {
            graph = g;
            this.quality = quality;
            root = new GameObject("World").transform;
            root.SetParent(parent, false);
            asphalt = Mats.Simple().Tex(Mats.Asphalt, 1, 1).Col(Color.white);
            sidewalk = Mats.Simple().Tex(Mats.Concrete).Col(M.Hex(0xc9c6bf));
            lawn = Mats.Simple().Tex(Mats.Grass).Col(Color.white);
            marking = Mats.Simple().Col(M.Hex(0xe8e8e4));
            roof = Mats.Simple().Col(M.Hex(0x4a4c50));
            trunk = Mats.Simple().Col(M.Hex(0x4a3626));
            crown = Mats.Simple().Col(M.Hex(0x3f6a2c));
            fence = Mats.Simple().Tex(Mats.Concrete).Col(M.Hex(0xb8b4ab));
            metal = Mats.Lit().Col(M.Hex(0x2b2d2f)).Pbr(0.6f, 0.4f);
            brick = Mats.Simple().Tex(Mats.Brick).Col(Color.white);
            glass = Mats.Lit().Col(M.Hex(0x22303a)).Pbr(0.6f, 0.9f);
            int[] tints = { 0xe9e3d6, 0xd8d8d8, 0xd6c6a8, 0xc98c6c, 0xbfcbd6, 0xe6d6a8 };
            facadeVariants = new Material[tints.Length];
            for (int i = 0; i < tints.Length; i++)
                facadeVariants[i] = Mats.SimpleEmissive().Tex(Mats.Facade(false)).Col(M.Hex(tints[i])).EmissionMap(Mats.Facade(true)).Emission(Color.black);

            BuildGround();
            BuildMarkings();
            BuildBlocks();
            BuildStreetLights();
            BuildTrafficLights();
            BuildFence();
            BuildAutodrome();
            BuildBillboards();
        }

        // ------------------------------------------------------------------ поверхность
        public Surface SurfaceAt(float x, float z)
        {
            float E = CityC.Extent;
            var s = new Surface { type = 2, y = 0f, mu = 0.6f };
            if (Mathf.Abs(x) <= E && Mathf.Abs(z) <= E)
            {
                float lx = Mathf.Abs(x - Nearest(x)), lz = Mathf.Abs(z - Nearest(z));
                bool inner = Mathf.Abs(x) < CityC.Coord(CityC.N - 1) + CityC.HALF && Mathf.Abs(z) < CityC.Coord(CityC.N - 1) + CityC.HALF;
                if (!inner && x < 0 && Mathf.Abs(z) <= CityC.GateHalf) { s.type = 0; s.mu = 1f; return s; } // проезд к воротам
                if (inner && (lx <= CityC.HALF || lz <= CityC.HALF)) { s.type = 0; s.mu = 1f; return s; }
                s.y = CityC.CURB;
                bool walk = !inner || lx <= CityC.HALF + CityC.SIDEWALK || lz <= CityC.HALF + CityC.SIDEWALK;
                if (walk) { s.type = 1; s.mu = 0.95f; } else { s.type = 2; s.mu = 0.6f; }
                return s;
            }
            // коридор к автодрому и сам автодром
            if (Mathf.Abs(z) <= CityC.GateHalf && x < -E && x >= CityC.AutoX1) { s.type = 0; s.mu = 1f; return s; }
            if (x >= CityC.AutoX0 && x <= CityC.AutoX1 && z >= CityC.AutoZ0 && z <= CityC.AutoZ1) { s.type = 0; s.mu = 1f; return s; }
            return s;
        }

        static float Nearest(float v)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(v / CityC.SPACING + (CityC.N - 1) / 2f), 0, CityC.N - 1);
            return CityC.Coord(i);
        }

        GameObject Obj(MeshBuilder b, string name, Material m, bool shadows)
        {
            return b.Count == 0 ? null : b.ToObject(name, m, root, shadows);
        }

        static void BoxCol(Transform parent, Vector3 c, Vector3 size, float h, string tag)
        {
            var go = new GameObject(tag);
            go.transform.SetParent(parent, false);
            go.transform.position = c;
            go.transform.rotation = M.Yaw(h);
            go.isStatic = true;
            go.AddComponent<BoxCollider>().size = size;
            go.AddComponent<Obstacle>().kind = tag;
        }

        // ------------------------------------------------------------------ земля
        void BuildGround()
        {
            float E = CityC.Extent;
            var g = new MeshBuilder();
            g.Flat(0, 0, -0.02f, 3000, 3000, 0, 6f);
            Obj(g, "Terrain", lawn, false);

            var a = new MeshBuilder();
            a.Flat(0, 0, 0, 2 * E, 2 * E, 0, 10f);
            a.Flat((-E + CityC.AutoX1) / 2f, 0, 0, -CityC.AutoX1 - E + 0.2f, 2 * CityC.GateHalf, 0, 10f);
            Obj(a, "Asphalt", asphalt, false);

            // кварталы: тротуар (бордюр 15 см) + газон внутри
            var sw = new MeshBuilder(); var lw = new MeshBuilder();
            for (int bi = 0; bi < CityC.N - 1; bi++)
                for (int bj = 0; bj < CityC.N - 1; bj++)
                {
                    float x0 = CityC.Coord(bi) + CityC.HALF, x1 = CityC.Coord(bi + 1) - CityC.HALF;
                    float z0 = CityC.Coord(bj) + CityC.HALF, z1 = CityC.Coord(bj + 1) - CityC.HALF;
                    sw.Box(new Vector3((x0 + x1) / 2, CityC.CURB / 2, (z0 + z1) / 2), new Vector3(x1 - x0, CityC.CURB, z1 - z0), 0, true, false, 4f);
                    float s = CityC.SIDEWALK;
                    lw.Flat((x0 + x1) / 2, (z0 + z1) / 2, CityC.CURB + 0.005f, x1 - x0 - 2 * s, z1 - z0 - 2 * s, 0, 6f);
                }
            // внешнее кольцо тротуара у забора
            float c = CityC.Coord(CityC.N - 1) + CityC.HALF;
            // восточная сторона целиком, западная — с проездом к воротам автодрома
            sw.Box(new Vector3((c + E) / 2, CityC.CURB / 2, 0), new Vector3(E - c, CityC.CURB, 2 * E), 0, true, false, 4f);
            float gh = CityC.GateHalf;
            sw.Box(new Vector3(-(c + E) / 2, CityC.CURB / 2, (gh + E) / 2), new Vector3(E - c, CityC.CURB, E - gh), 0, true, false, 4f);
            sw.Box(new Vector3(-(c + E) / 2, CityC.CURB / 2, -(gh + E) / 2), new Vector3(E - c, CityC.CURB, E - gh), 0, true, false, 4f);
            foreach (int sgn in new[] { -1, 1 })
            {
                sw.Box(new Vector3(0, CityC.CURB / 2, sgn * (c + E) / 2), new Vector3(2 * c, CityC.CURB, E - c), 0, true, false, 4f);
            }
            Obj(sw, "Sidewalks", sidewalk, false);
            Obj(lw, "Lawns", lawn, false);
        }

        // ------------------------------------------------------------------ разметка
        void BuildMarkings()
        {
            var m = new MeshBuilder();
            const float y = 0.012f, lw = 0.15f;
            float seg = CityC.SPACING - 2 * CityC.HALF;
            for (int k = 0; k < CityC.N; k++)
                for (int s = 0; s < CityC.N - 1; s++)
                {
                    float a = CityC.Coord(s) + CityC.HALF, b = CityC.Coord(s + 1) - CityC.HALF, mid = (a + b) / 2;
                    float line = CityC.Coord(k);
                    foreach (int axis in new[] { 0, 1 })
                    {
                        // axis 0 — дорога вдоль X (z = line), 1 — вдоль Z (x = line)
                        System.Action<float, float, float, float> rect = (along, across, len, wid) =>
                        {
                            if (axis == 0) m.Flat(along, line + across, y, wid, len, Mathf.PI / 2); // курс 90°: длина вдоль X
                            else m.Flat(line + across, along, y, wid, len, 0);
                        };
                        // двойная сплошная по оси
                        rect(mid, -0.12f, seg - 10f, lw); rect(mid, 0.12f, seg - 10f, lw);
                        // прерывистые между полосами
                        for (float t = a + 6f; t < b - 6f; t += 9f)
                        {
                            rect(t + 1.5f, -CityC.LANE_W, 3f, lw); rect(t + 1.5f, CityC.LANE_W, 3f, lw);
                        }
                        // стоп-линии и «зебры» у обоих концов участка
                        foreach (int end in new[] { 0, 1 })
                        {
                            float e = end == 0 ? a : b, dir = end == 0 ? 1 : -1;
                            float stop = e + dir * (CityC.STOP_BACK - 0.2f);
                            // стоп-линия на полосах, въезжающих в перекрёсток (правостороннее движение)
                            float side = (axis == 0 ? 1 : -1) * dir; // сторона полос, въезжающих в перекрёсток
                            rect(stop, side * CityC.HALF / 2, 0.4f, CityC.HALF - 0.3f);
                            for (float w = -CityC.HALF + 0.6f; w < CityC.HALF - 0.4f; w += 1.2f)
                                rect(e + dir * 1.8f, w + 0.3f, 3f, 0.6f);
                        }
                    }
                }
            m.ToObject("Markings", marking, root, false).GetComponent<MeshRenderer>().receiveShadows = true;
        }

        // ------------------------------------------------------------------ кварталы
        void BuildBlocks()
        {
            for (int bi = 0; bi < CityC.N - 1; bi++)
                for (int bj = 0; bj < CityC.N - 1; bj++)
                    BuildBlock(bi, bj);
        }

        void BuildBlock(int bi, int bj)
        {
            var blk = new GameObject($"Block_{bi}_{bj}").transform;
            blk.SetParent(root, false);
            float m = CityC.HALF + CityC.SIDEWALK + 1f;
            float x0 = CityC.Coord(bi) + m, x1 = CityC.Coord(bi + 1) - m, z0 = CityC.Coord(bj) + m, z1 = CityC.Coord(bj + 1) - m;
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            var fac = new MeshBuilder[facadeVariants.Length];
            for (int i = 0; i < fac.Length; i++) fac[i] = new MeshBuilder();
            var roofs = new MeshBuilder(); var trunks = new MeshBuilder(); var crowns = new MeshBuilder(); var bricks = new MeshBuilder();
            var rects = new List<Rect>();

            float r = rnd.Next();
            bool isAzs = bi == 2 && bj == 3;
            if (isAzs)
            {
                // АЗС: навес, колонки, магазинчик
                azs = new Vector3(cx, 0, cz);
                var canopy = new MeshBuilder();
                canopy.Box(new Vector3(cx, 5.2f, cz), new Vector3(22, 0.6f, 14), 0, true, true);
                foreach (float px in new[] { -8f, 8f }) foreach (float pz in new[] { -5f, 5f })
                        canopy.CylinderY(new Vector3(cx + px, 0, cz + pz), 0.25f, 0.25f, 5f, 8);
                canopy.ToObject("AZS_Canopy", metal, blk, true);
                for (int k = -1; k <= 1; k += 2)
                {
                    var pump = new MeshBuilder();
                    pump.Box(new Vector3(cx + k * 3.5f, 0.8f, cz), new Vector3(0.8f, 1.6f, 0.5f), 0, true);
                    pump.ToObject("Pump", Mats.Lit().Col(M.Hex(0xd52b1e)).Pbr(0.2f, 0.6f), blk, true);
                    BoxCol(blk, new Vector3(cx + k * 3.5f, 0.8f, cz), new Vector3(0.8f, 1.6f, 0.5f), 0, "pole");
                }
                bricks.Box(new Vector3(cx, 1.75f, z1 - 6), new Vector3(16, 3.5f, 8), 0, true);
                BoxCol(blk, new Vector3(cx, 1.75f, z1 - 6), new Vector3(16, 3.5f, 8), 0, "building");
                var asphaltPad = new MeshBuilder();
                asphaltPad.Flat(cx, cz, CityC.CURB + 0.01f, x1 - x0, z1 - z0 - 12, 0, 10f);
                asphaltPad.ToObject("AZS_Pad", asphalt, blk, false);
            }
            else if (r < 0.45f)
            {
                // две пятиэтажки вдоль X, двор между ними
                foreach (int s in new[] { -1, 1 })
                {
                    float bz = cz + s * 20f;
                    Building(fac, roofs, rects, blk, cx, bz, 60f, 12f, 15f, 0f);
                }
                Yard(cx, cz, 50f, 18f, 0f, trunks, crowns);
            }
            else if (r < 0.7f)
            {
                // длинная девятиэтажка + деревья
                Building(fac, roofs, rects, blk, cx + (rnd.Next() - 0.5f) * 6f, cz + 12f, 64f, 13f, 27f, 0f);
                Yard(cx, cz - 14f, 56f, 20f, 0f, trunks, crowns);
            }
            else if (r < 0.82f)
            {
                // две башни
                Building(fac, roofs, rects, blk, cx - 18f, cz + 10f, 20f, 20f, 46f, 0f);
                Building(fac, roofs, rects, blk, cx + 18f, cz - 12f, 20f, 20f, 40f, 0f);
                Yard(cx + 14f, cz + 16f, 26f, 18f, 0f, trunks, crowns);
            }
            else if (r < 0.92f)
            {
                // гаражный кооператив: два ряда кирпичных боксов
                foreach (int s in new[] { -1, 1 })
                {
                    float gz = cz + s * 9f;
                    for (int k = 0; k < 14; k++)
                    {
                        float gx = x0 + 6f + k * 4.6f;
                        if (gx > x1 - 4f) break;
                        bricks.Box(new Vector3(gx, 1.35f, gz), new Vector3(4.2f, 2.7f, 6.4f), 0, true);
                    }
                    float len = Mathf.Min(14 * 4.6f, x1 - x0 - 8f);
                    BoxCol(blk, new Vector3(x0 + 4f + len / 2, 1.35f, gz), new Vector3(len, 2.7f, 6.4f), 0, "building");
                }
            }
            else
            {
                // парк
                for (int k = 0; k < 40; k++)
                {
                    float tx = Mathf.Lerp(x0 + 3, x1 - 3, rnd.Next()), tz = Mathf.Lerp(z0 + 3, z1 - 3, rnd.Next());
                    Tree(tx, tz, trunks, crowns, blk);
                }
            }

            // деревья вдоль тротуара (где нет зданий)
            for (float t = x0 + 4f; t < x1 - 2f; t += 11f)
                foreach (float tz in new[] { z0 + 0.5f, z1 - 0.5f })
                    if (!Inside(rects, t, tz, 2f)) Tree(t, tz, trunks, crowns, blk);
            for (float t = z0 + 4f; t < z1 - 2f; t += 11f)
                foreach (float tx in new[] { x0 + 0.5f, x1 - 0.5f })
                    if (!Inside(rects, tx, t, 2f)) Tree(tx, t, trunks, crowns, blk);

            for (int i = 0; i < fac.Length; i++) Obj(fac[i], "Buildings", facadeVariants[i], true)?.transform.SetParent(blk, true);
            Obj(roofs, "Roofs", roof, false)?.transform.SetParent(blk, true);
            Obj(bricks, "Garages", brick, true)?.transform.SetParent(blk, true);
            Obj(trunks, "Trunks", trunk, quality >= 2)?.transform.SetParent(blk, true);
            Obj(crowns, "Crowns", crown, quality >= 1)?.transform.SetParent(blk, true);
        }

        static bool Inside(List<Rect> rects, float x, float z, float pad)
        {
            foreach (var r in rects) if (x > r.xMin - pad && x < r.xMax + pad && z > r.yMin - pad && z < r.yMax + pad) return true;
            return false;
        }

        void Building(MeshBuilder[] fac, MeshBuilder roofs, List<Rect> rects, Transform blk, float cx, float cz, float lx, float lz, float h, float rot)
        {
            int v = rnd.Range(fac.Length);
            var b = fac[v];
            float x0 = cx - lx / 2, x1 = cx + lx / 2, z0 = cz - lz / 2, z1 = cz + lz / 2, y0 = CityC.CURB;
            // фасады: 1 повтор текстуры = 4 окна по 3 м = 12 м, 4 этажа по 3 м
            b.Wall(x0, z0, x1, z0, y0, y0 + h, 12f, 12f);      // юг (смотрит в −Z)
            b.Wall(x1, z1, x0, z1, y0, y0 + h, 12f, 12f);      // север
            b.Wall(x0, z1, x0, z0, y0, y0 + h, 12f, 12f);      // запад
            b.Wall(x1, z0, x1, z1, y0, y0 + h, 12f, 12f);      // восток
            roofs.Flat(cx, cz, y0 + h, lx, lz);
            // парапет и будки на крыше
            roofs.Box(new Vector3(cx + lx * 0.25f, y0 + h + 1.1f, cz), new Vector3(3f, 2.2f, 3f), 0, true);
            rects.Add(Rect.MinMaxRect(x0, z0, x1, z1));
            BoxCol(blk, new Vector3(cx, y0 + h / 2, cz), new Vector3(lx, h, lz), rot, "building");
        }

        void Yard(float cx, float cz, float w, float l, float rot, MeshBuilder trunks, MeshBuilder crowns)
        {
            // припаркованные машины вдоль двора и пара деревьев
            int n = 3 + rnd.Range(4);
            for (int k = 0; k < n; k++)
            {
                var def = Cars.All[rnd.Range(Cars.All.Length)];
                float x = cx - w / 2 + 4f + k * (w - 8f) / Mathf.Max(1, n - 1);
                float z = cz + (rnd.Next() < 0.5f ? -1 : 1) * (l / 2 - 3.2f);
                parked.Add(new ParkedCar { def = def, pos = new Vector3(x, CityC.CURB, z), heading = rnd.Next() < 0.5f ? 0f : Mathf.PI, color = def.colors[rnd.Range(def.colors.Length)] });
            }
            for (int k = 0; k < 4; k++) Tree(cx + (rnd.Next() - 0.5f) * w * 0.8f, cz + (rnd.Next() - 0.5f) * l * 0.3f, trunks, crowns, null);
        }

        void Tree(float x, float z, MeshBuilder trunks, MeshBuilder crowns, Transform blk)
        {
            float s = 0.8f + rnd.Next() * 0.5f;
            trunks.CylinderY(new Vector3(x, CityC.CURB, z), 0.16f * s, 0.11f * s, 3.2f * s, 6, false);
            crowns.Blob(new Vector3(x, CityC.CURB + 4.2f * s, z), new Vector3(2.0f, 2.4f, 2.0f) * s, (int)(x * 13 + z * 7));
            if (blk)
            {
                var go = new GameObject("tree"); go.transform.SetParent(blk, false);
                go.transform.position = new Vector3(x, 1.5f, z); go.isStatic = true;
                var c = go.AddComponent<CapsuleCollider>(); c.radius = 0.2f * s; c.height = 3f;
                go.AddComponent<Obstacle>().kind = "pole";
            }
        }

        // ------------------------------------------------------------------ фонари
        void BuildStreetLights()
        {
            var poles = new MeshBuilder();
            var cols = new GameObject("LampColliders").transform; cols.SetParent(root, false);
            for (int k = 0; k < CityC.N; k++)
                for (int s = 0; s < CityC.N - 1; s++)
                {
                    float a = CityC.Coord(s) + CityC.HALF + 8f, b = CityC.Coord(s + 1) - CityC.HALF - 8f;
                    float line = CityC.Coord(k);
                    for (float t = a; t <= b + 0.1f; t += (b - a) / 2f)
                        foreach (int axis in new[] { 0, 1 })
                        {
                            int side = ((int)(t / 10f) + k) % 2 == 0 ? 1 : -1;
                            float off = CityC.HALF + 0.6f;
                            Vector3 p = axis == 0 ? new Vector3(t, CityC.CURB, line + side * off) : new Vector3(line + side * off, CityC.CURB, t);
                            Vector3 arm = axis == 0 ? new Vector3(0, 0, -side) : new Vector3(-side, 0, 0);
                            poles.CylinderY(p, 0.09f, 0.06f, 8f, 6, false);
                            poles.Box(p + Vector3.up * 8f + arm * 0.9f, new Vector3(axis == 0 ? 0.3f : 1.8f, 0.12f, axis == 0 ? 1.8f : 0.3f), 0, true, true);
                            lampHeads.Add(p + Vector3.up * 7.85f + arm * 1.6f);
                            var go = new GameObject("lamp"); go.transform.SetParent(cols, false);
                            go.transform.position = p + Vector3.up * 2f; go.isStatic = true;
                            var c = go.AddComponent<CapsuleCollider>(); c.radius = 0.12f; c.height = 4f;
                            go.AddComponent<Obstacle>().kind = "pole";
                        }
                }
            Obj(poles, "StreetLightPoles", metal, quality >= 2);
        }

        // ------------------------------------------------------------------ светофоры
        void BuildTrafficLights()
        {
            var poles = new MeshBuilder();
            var cols = new GameObject("TrafficLightColliders").transform; cols.SetParent(root, false);
            Vector2[] approaches = { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0) };
            foreach (var n in graph.nodes)
                foreach (var d in approaches)
                {
                    float fromX = n.x - d.x * CityC.SPACING, fromZ = n.z - d.y * CityC.SPACING;
                    bool has = false;
                    foreach (int m in n.nb) if (Mathf.Abs(graph.nodes[m].x - fromX) < 1 && Mathf.Abs(graph.nodes[m].z - fromZ) < 1) has = true;
                    if (!has) continue;
                    // «вправо» от направления движения (правостороннее движение)
                    var rgt = new Vector2(d.y, -d.x);
                    float o = CityC.HALF + 1.2f;
                    var p = new Vector3(n.x - d.x * o + rgt.x * o, CityC.CURB, n.z - d.y * o + rgt.y * o);
                    poles.CylinderY(p, 0.1f, 0.08f, 3.2f, 6, false);
                    poles.Box(p + Vector3.up * 3.75f, new Vector3(0.34f, 1.15f, 0.34f), 0, true, true);
                    int axis = Mathf.Abs(d.y) > 0.5f ? 0 : 1;
                    var face = new Vector3(-d.x, 0, -d.y);
                    lightHeads.Add(new Vector4(p.x, p.y + 4.12f, p.z, 0));
                    lightNode.Add(n.id); lightAxis.Add(axis); lightFacing.Add(face);
                    var go = new GameObject("tl"); go.transform.SetParent(cols, false);
                    go.transform.position = p + Vector3.up * 1.6f; go.isStatic = true;
                    var c = go.AddComponent<CapsuleCollider>(); c.radius = 0.15f; c.height = 3.2f;
                    go.AddComponent<Obstacle>().kind = "pole";
                }
            Obj(poles, "TrafficLightPoles", metal, quality >= 2);
        }

        // ------------------------------------------------------------------ забор
        void BuildFence()
        {
            float E = CityC.Extent + 0.3f, h = 2.4f, t = 0.25f, gh = CityC.GateHalf;
            var f = new MeshBuilder();
            var cols = new GameObject("Fence").transform; cols.SetParent(root, false);
            void Seg(Vector3 c, Vector3 size)
            {
                f.Box(c, size, 0, true, false, 3f);
                BoxCol(cols, c, size, 0, "fence");
            }
            Seg(new Vector3(0, h / 2, E), new Vector3(2 * E, h, t));
            Seg(new Vector3(0, h / 2, -E), new Vector3(2 * E, h, t));
            Seg(new Vector3(E, h / 2, 0), new Vector3(t, h, 2 * E));
            // западная сторона — ворота к автодрому
            Seg(new Vector3(-E, h / 2, (gh + E) / 2), new Vector3(t, h, E - gh));
            Seg(new Vector3(-E, h / 2, -(gh + E) / 2), new Vector3(t, h, E - gh));
            // коридор: отбойники
            float cx0 = -E, cx1 = CityC.AutoX1, len = cx0 - cx1;
            Seg(new Vector3((cx0 + cx1) / 2, 0.45f, gh + 0.4f), new Vector3(len, 0.9f, 0.3f));
            Seg(new Vector3((cx0 + cx1) / 2, 0.45f, -gh - 0.4f), new Vector3(len, 0.9f, 0.3f));
            Obj(f, "FenceMesh", fence, true);
        }

        void BuildAutodrome()
        {
            float x0 = CityC.AutoX0, x1 = CityC.AutoX1, z0 = CityC.AutoZ0, z1 = CityC.AutoZ1, h = 1.2f;
            var a = new MeshBuilder();
            a.Flat((x0 + x1) / 2, (z0 + z1) / 2, 0.002f, x1 - x0, z1 - z0, 0, 10f);
            Obj(a, "AutodromeAsphalt", asphalt, false);
            var f = new MeshBuilder();
            var cols = new GameObject("AutodromeFence").transform; cols.SetParent(root, false);
            void Seg(Vector3 c, Vector3 size) { f.Box(c, size, 0, true, false, 3f); BoxCol(cols, c, size, 0, "fence"); }
            Seg(new Vector3((x0 + x1) / 2, h / 2, z1), new Vector3(x1 - x0, h, 0.3f));
            Seg(new Vector3((x0 + x1) / 2, h / 2, z0), new Vector3(x1 - x0, h, 0.3f));
            Seg(new Vector3(x0, h / 2, 0), new Vector3(0.3f, h, z1 - z0));
            Seg(new Vector3(x1, h / 2, (CityC.GateHalf + z1) / 2), new Vector3(0.3f, h, z1 - CityC.GateHalf));
            Seg(new Vector3(x1, h / 2, (z0 - CityC.GateHalf) / 2), new Vector3(0.3f, h, -z0 - CityC.GateHalf));
            Obj(f, "AutodromeFenceMesh", fence, false);
            // вывеска ДОСААФ
            Sign(new Vector3(x1 + 6f, 0, CityC.GateHalf + 4f), -Mathf.PI / 2, "АВТОДРОМ ДОСААФ", "парковка · экзамен · дрифт", 0x1f3c78);
        }

        // ------------------------------------------------------------------ билборды
        void BuildBillboards()
        {
            float o = CityC.HALF + CityC.SIDEWALK + 2f;
            Vector3[] spots = {
                new Vector3(CityC.Coord(3) + o, 0, CityC.Coord(1) + 30), new Vector3(CityC.Coord(1) - o, 0, CityC.Coord(4) + 40),
                new Vector3(CityC.Coord(5) + 30, 0, CityC.Coord(2) + o), new Vector3(CityC.Coord(2) - 20, 0, CityC.Coord(5) - o),
                new Vector3(-CityC.Extent - 30, 0, CityC.GateHalf + 6),
            };
            float[] faces = { -Mathf.PI / 2, Mathf.PI / 2, Mathf.PI, 0f, Mathf.PI / 2 };
            for (int i = 0; i < spots.Length; i++)
                Sign(spots[i], faces[i], "CAUCASUS DRIVE", "Telegram: t.me/caucasusdrive", 0x229ed9);
        }

        /// <summary>Рекламный щит на двух опорах; face — курс, куда смотрит лицевая сторона.</summary>
        void Sign(Vector3 p, float face, string title, string sub, int color)
        {
            var go = new GameObject("Billboard");
            go.transform.SetParent(root, false);
            go.transform.position = p;
            go.transform.rotation = M.Yaw(face + Mathf.PI); // лицевая сторона щита смотрит в −Z локально
            var mb = new MeshBuilder();
            mb.CylinderY(new Vector3(-2.5f, 0, 0.3f), 0.15f, 0.15f, 4.5f, 6, false);
            mb.CylinderY(new Vector3(2.5f, 0, 0.3f), 0.15f, 0.15f, 4.5f, 6, false);
            mb.ToObject("Legs", metal, go.transform, true).isStatic = false;
            var board = new MeshBuilder();
            board.Box(new Vector3(0, 6f, 0.1f), new Vector3(8.4f, 3.4f, 0.2f), 0, true, true);
            var bo = board.ToObject("Board", Mats.SimpleEmissive().Col(M.Hex(color)).Emission(M.Hex(color) * 0.25f), go.transform, true);
            bo.isStatic = false;
            Text3D(go.transform, title, new Vector3(0, 6.6f, -0.02f), 0.06f, Color.white);
            Text3D(go.transform, sub, new Vector3(0, 5.5f, -0.02f), 0.035f, new Color(0.9f, 0.97f, 1f));
            BoxCol(go.transform, p + Vector3.up * 2f, new Vector3(5.6f, 4f, 0.6f), face, "pole");
        }

        public static TextMesh Text3D(Transform parent, string text, Vector3 local, float size, Color c)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.font = UIKit.Font; tm.text = text; tm.fontSize = 96; tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = UIKit.Font.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }

        // ------------------------------------------------------------------ день/ночь
        public void SetNight(float night)
        {
            var e = new Color(1f, 0.85f, 0.6f) * Mathf.Clamp01((night - 0.25f) * 2f) * 1.2f;
            foreach (var m in facadeVariants) m.SetColor("_EmissionColor", e);
        }
    }

    /// <summary>Метка препятствия для режимов (конус, стена, столб, здание…).</summary>
    public class Obstacle : MonoBehaviour
    {
        public string kind = "building";
        public int index = -1;
    }
}
