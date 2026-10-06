using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    public class BusStop { public Vector3 pos, n; public Vector3 bench; public float yaw; public object used; }   // n — к дороге
    public class SpeedCam { public Vector3 pos, lens; }
    public class DpsPost { public Vector3 pos; public float heading; public int node; }
    public class ShopPoint { public Vector3 pos, n; public string kind, title; }

    /// <summary>
    /// Городские мелочи из веб-версии: остановки с лавками (на них сидят прохожие и игрок), камеры «Стрелка»,
    /// посты ДПС (будка + машина с мигалками), ларьки «24 часа» и «Шаурма», кафе при АЗС.
    /// </summary>
    public partial class City
    {
        public const int StopLayer = 8;      // павильоны остановок: машины упираются, пешеход проходит к лавке
        public readonly List<BusStop> busStops = new List<BusStop>();
        public readonly List<SpeedCam> cameras = new List<SpeedCam>();
        public readonly List<DpsPost> dpsPosts = new List<DpsPost>();
        public readonly List<ShopPoint> shops = new List<ShopPoint>();
        public Material kioskSignMat;
        static readonly Vector2Int[] DpsNodes = { new Vector2Int(1, 4), new Vector2Int(5, 2) };

        public static readonly Dictionary<string, string> ShopTitles = new Dictionary<string, string> {
            { "kiosk", "Ларёк «24 часа»" }, { "shawarma", "Шаурма у Ашота" }, { "grocery", "Продукты" }, { "bakery", "Хлеб · выпечка" }, { "cafe", "Кафе «Лада»" },
        };

        void BuildExtras()
        {
            var props = new PaletteMesh();
            var colRoot = new GameObject("PropColliders").transform; colRoot.SetParent(root, false);
            var dpsIds = new HashSet<int>();
            foreach (var d in DpsNodes) dpsIds.Add(graph.Id(d.x, d.y));
            int edgeIdx = 0;
            const int blue = 0x1d4fa0, glassC = 0x6f8fa0, dark = 0x2a2c30, wood = 0x8a5a32, white = 0xdedede;
            foreach (var e in graph.edges)
            {
                if (e.x > e.y) continue;
                var A = graph.nodes[e.x]; var B = graph.nodes[e.y];
                var a = new Vector3(A.x, 0, A.z);
                var d = (new Vector3(B.x, 0, B.z) - a).normalized;
                var r = new Vector3(d.z, 0, -d.x);
                System.Func<float, float, Vector3> P = (u, lat) => a + d * (CityC.HALF + u) + r * lat;
                bool dps = dpsIds.Contains(e.x) || dpsIds.Contains(e.y);
                edgeIdx++;
                // --- остановка
                if (!dps && edgeIdx % 4 == 1)
                {
                    float side = rnd.Next() < 0.5f ? 1f : -1f;
                    var c = P(55f, side * (CityC.HALF + 2.0f)) + Vector3.up * CityC.CURB;
                    var n = -r * side;                                   // лицом к дороге
                    float yaw = Mathf.Atan2(n.x, n.z);
                    props.xf = Matrix4x4.TRS(c, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0), Vector3.one);
                    props.Box(new Vector3(0, 2.58f, 0), new Vector3(4.2f, 0.15f, 1.5f), blue);                 // крыша
                    props.Box(new Vector3(0, 1.4f, -0.72f), new Vector3(4.2f, 2.2f, 0.06f), glassC);           // задняя стенка
                    props.Box(new Vector3(-2.0f, 1.25f, 0.6f), new Vector3(0.12f, 2.5f, 0.12f), dark);
                    props.Box(new Vector3(2.0f, 1.25f, 0.6f), new Vector3(0.12f, 2.5f, 0.12f), dark);
                    props.Box(new Vector3(-2.0f, 1.25f, -0.72f), new Vector3(0.12f, 2.5f, 0.12f), dark);
                    props.Box(new Vector3(2.0f, 1.25f, -0.72f), new Vector3(0.12f, 2.5f, 0.12f), dark);
                    props.Box(new Vector3(0, 0.46f, -0.35f), new Vector3(3.2f, 0.08f, 0.4f), wood);           // лавка
                    props.Box(new Vector3(-1.4f, 0.22f, -0.35f), new Vector3(0.08f, 0.44f, 0.36f), dark);
                    props.Box(new Vector3(1.4f, 0.22f, -0.35f), new Vector3(0.08f, 0.44f, 0.36f), dark);
                    // табличка «А»
                    var sp = c + d * 2.8f * 1f + n * 1.4f;
                    props.xf = Matrix4x4.TRS(sp, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0), Vector3.one);
                    props.Cyl(new Vector3(0, 1.4f, 0), 0.04f, 0.04f, 2.8f, 0x8a8a8a, 6);
                    props.Box(new Vector3(0, 2.6f, 0.02f), new Vector3(0.5f, 0.5f, 0.04f), 0xf2c21a);
                    var go = new GameObject("busstop"); go.transform.SetParent(colRoot, false);
                    go.transform.SetPositionAndRotation(c + Vector3.up * 1.3f, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0));
                    go.layer = StopLayer; go.isStatic = true;
                    go.AddComponent<BoxCollider>().size = new Vector3(4.2f, 2.6f, 1.5f);
                    go.AddComponent<Obstacle>().kind = "busstop";
                    busStops.Add(new BusStop { pos = c, n = n, bench = c - n * 0.35f + Vector3.up * 0.5f, yaw = yaw });
                }
                // --- камера «Стрелка» (смотрит навстречу потоку по правой стороне)
                if (!dps && edgeIdx % 7 == 3)
                {
                    var c = P(28f, CityC.HALF + 0.6f) + Vector3.up * CityC.CURB;
                    props.xf = Matrix4x4.identity;
                    props.Cyl(c + Vector3.up * 2.75f, 0.1f, 0.12f, 5.5f, 0x7a7c80, 6);
                    props.Box(c + Vector3.up * 5.5f, new Vector3(0.5f, 0.6f, 0.5f), white);
                    props.Box(c + Vector3.up * 5.5f - d * 0.3f, new Vector3(0.4f, 0.4f, 0.4f), dark);
                    var go = new GameObject("camera"); go.transform.SetParent(colRoot, false);
                    go.transform.position = c + Vector3.up * 2f; go.isStatic = true;
                    var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.15f; cc.height = 4f;
                    go.AddComponent<Obstacle>().kind = "pole";
                    cameras.Add(new SpeedCam { pos = c, lens = c + Vector3.up * 5.5f - d * 0.52f });
                    // знак «фотовидеофиксация»
                    var s = P(8f, CityC.HALF + 0.55f) + Vector3.up * CityC.CURB;
                    props.Cyl(s + Vector3.up * 1.3f, 0.04f, 0.04f, 2.6f, 0x8a8a8a, 6);
                    props.xf = Matrix4x4.TRS(s, Quaternion.LookRotation(-d), Vector3.one);
                    props.Box(new Vector3(0, 2.5f, 0.03f), new Vector3(0.6f, 0.6f, 0.03f), 0x1d4fa0);
                    props.Box(new Vector3(0, 2.5f, -0.005f), new Vector3(0.3f, 0.2f, 0.03f), 0xf2f2f2);
                }
            }
            props.xf = Matrix4x4.identity;
            // --- посты ДПС
            foreach (var dn in DpsNodes)
            {
                float nx = CityC.Coord(dn.x), nz = CityC.Coord(dn.y);
                var bc = new Vector3(nx + CityC.HALF + 1.5f, CityC.CURB, nz + 12.5f);
                props.Box(bc + Vector3.up * 1.35f, new Vector3(2.4f, 2.7f, 2.4f), 0xe8e8e8);
                props.Box(bc + Vector3.up * 1.6f, new Vector3(2.5f, 0.6f, 2.5f), 0x4a6a8a);
                props.Box(bc + Vector3.up * 2.8f, new Vector3(2.8f, 0.2f, 2.8f), 0x1b3c9e);
                BoxCol(colRoot, bc + Vector3.up * 1.35f, new Vector3(2.4f, 2.7f, 2.4f), 0, "booth");
                var t = Text3D(root, "ДПС", bc + new Vector3(-1.22f, 2.25f, 0), 0.035f, Color.white);
                t.transform.rotation = Quaternion.Euler(0, 90, 0);
                dpsPosts.Add(new DpsPost { pos = new Vector3(nx + CityC.HALF + 1.6f, CityC.CURB, nz + 24f), heading = 0f, node = graph.Id(dn.x, dn.y) });
            }
            // --- ларьки у части остановок
            Physics.SyncTransforms();
            int k = 0;
            for (int i = 0; i < busStops.Count && k < 10; i += 2)
            {
                var st = busStops[i];
                var along = new Vector3(Mathf.Abs(st.n.z), 0, Mathf.Abs(st.n.x));
                var line = st.pos + st.n * (CityC.HALF + 2.0f);          // ось дороги
                foreach (float al in new[] { 7f, -7f })
                {
                    var c = line - st.n * (CityC.HALF + CityC.SIDEWALK + 1.5f) + along * al;
                    c.y = CityC.CURB;
                    if (Physics.CheckSphere(c + Vector3.up * 1.2f, 2.0f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    string kind = k % 2 == 1 ? "shawarma" : "kiosk";
                    float yaw = Mathf.Atan2(st.n.x, st.n.z);
                    int body = kind == "kiosk" ? 0xe9edf2 : 0xf3e3c3, trim = kind == "kiosk" ? 0x1f4fb0 : 0xb71c1c;
                    props.xf = Matrix4x4.TRS(c, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0), Vector3.one);
                    props.Box(new Vector3(0, 1.25f, 0), new Vector3(2.8f, 2.5f, 2.2f), body);
                    props.Box(new Vector3(0, 2.56f, 0), new Vector3(3.1f, 0.12f, 2.6f), trim);
                    props.Box(new Vector3(0, 1.5f, 1.11f), new Vector3(2.2f, 0.9f, 0.04f), 0x8fb6c8);
                    props.Box(new Vector3(0, 1.0f, 1.25f), new Vector3(2.4f, 0.06f, 0.32f), 0x9a9a9a);
                    props.Box(new Vector3(0, 0.5f, 1.12f), new Vector3(2.8f, 0.1f, 0.05f), trim);
                    props.Box(new Vector3(0, 2.2f, 1.13f), new Vector3(2.6f, 0.5f, 0.02f), trim);
                    var tm = Text3D(root, kind == "kiosk" ? "ПРОДУКТЫ · 24 ЧАСА" : "ШАУРМА · КОФЕ", c + st.n * 1.15f + Vector3.up * 2.2f, 0.016f, kind == "kiosk" ? new Color(1f, 0.92f, 0.23f) : Color.white);
                    tm.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(-st.n.x, -st.n.z) * Mathf.Rad2Deg, 0);
                    BoxCol(colRoot, c + Vector3.up * 1.25f, new Vector3(2.8f, 2.5f, 2.2f), yaw, "kiosk");
                    shops.Add(new ShopPoint { pos = c + st.n * 1.9f, n = st.n, kind = kind, title = ShopTitles[kind] });
                    k++;
                    break;
                }
            }
            // --- кафе при АЗС (магазинчик под вывеской) и «Продукты» в первом ряду кварталов
            if (azs != Vector3.zero)
            {
                float z1 = CityC.Coord(4) - (CityC.HALF + CityC.SIDEWALK + 1f);
                var front = new Vector3(azs.x, CityC.CURB, z1 - 10f);
                var tm = Text3D(root, "КАФЕ «ЛАДА»", front + Vector3.up * 3.1f + Vector3.back * 0.05f, 0.03f, new Color(1f, 0.85f, 0.4f));
                shops.Add(new ShopPoint { pos = front + Vector3.back * 1.2f, n = Vector3.back, kind = "cafe", title = ShopTitles["cafe"] });
            }
            if (props.Count > 0) props.ToObject("CityProps", root, null, quality >= 1).isStatic = true;
        }
    }
}
