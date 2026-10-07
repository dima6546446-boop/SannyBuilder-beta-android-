using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Industrial zone east of the city: asphalt apron with a drift circuit, factories, chimneys, containers, tyre walls.</summary>
    public static class IndustrialBuilder
    {
        public static void Build(WorldInfo info, Transform parent, System.Random rng)
        {
            var m = info.mats;
            Vector3 C = info.industrialCenter;

            // ---------- path (relative control points) ----------
            Vector2[] rel =
            {
                new Vector2(-115,-80), new Vector2(-40,-88), new Vector2(45,-84), new Vector2(110,-62), new Vector2(135,-10),
                new Vector2(112,50), new Vector2(60,80), new Vector2(0,72), new Vector2(-48,54), new Vector2(-90,68),
                new Vector2(-128,40), new Vector2(-142,-15), new Vector2(-136,-58)
            };
            var ctrl = new List<Vector3>();
            foreach (var p in rel) ctrl.Add(new Vector3(C.x + p.x, 0.05f, C.z + p.y));
            info.industrialLoop = TrackPath.FromControl("industrial", ctrl, true, 4f, 16f);
            var path = info.industrialLoop;

            var cb = new ChunkBuilder();
            float ax0 = C.x - 165f, ax1 = C.x + 160f, az0 = C.z - 112f, az1 = C.z + 108f;
            float aw = ax1 - ax0, ad = az1 - az0;
            Vector3 ac = new Vector3((ax0 + ax1) * 0.5f, 0.04f, (az0 + az1) * 0.5f);

            // apron surface + collider
            cb.For(m.asphalt).AddFace(ac, Vector3.up, Vector3.right, aw, ad, new Vector2(8f, 8f));
            cb.Collider(new Vector3(ac.x, -0.11f, ac.z), new Vector3(aw, 0.3f, ad), Quaternion.identity, SurfaceType.Asphalt, 1f);
            // access road from the city ring (x = 242 .. apron)
            float rx0 = 242f;
            cb.For(m.asphalt).AddFace(new Vector3((rx0 + ax0) * 0.5f, 0.042f, 0f), Vector3.up, Vector3.right, ax0 - rx0, 14f, new Vector2(6f, 6f));
            cb.Collider(new Vector3((rx0 + ax0) * 0.5f, -0.109f, 0f), new Vector3(ax0 - rx0, 0.3f, 14f), Quaternion.identity, SurfaceType.Asphalt, 1f);

            // track surface paint: slightly darker rubbered racing line + white edge lines
            var white = cb.For(m.white);
            int n = path.Count;
            for (int i = 0; i < n; i += 1)
            {
                Vector3 a = path.Point(i), b = path.Point(i + 1);
                Vector3 t = (b - a).normalized;
                Vector3 r = Vector3.Cross(Vector3.up, t);
                float hw = path.width * 0.5f;
                if ((i % 3) != 2)
                {
                    white.AddQuad(a + r * hw + Vector3.up * 0.012f, b + r * hw + Vector3.up * 0.012f, b + r * (hw - 0.25f) + Vector3.up * 0.012f, a + r * (hw - 0.25f) + Vector3.up * 0.012f);
                    white.AddQuad(a - r * (hw - 0.25f) + Vector3.up * 0.012f, b - r * (hw - 0.25f) + Vector3.up * 0.012f, b - r * hw + Vector3.up * 0.012f, a - r * hw + Vector3.up * 0.012f);
                }
            }
            // start/finish line
            {
                Vector3 s = path.Point(0), t = path.Tangent(0), r = Vector3.Cross(Vector3.up, t);
                for (int k = -3; k <= 3; k++)
                    cb.For(((k & 1) == 0) ? m.white : m.black).AddQuad(
                        s + r * (k * 2f + 1f) + Vector3.up * 0.013f - t * 0.6f, s + r * (k * 2f + 1f) + Vector3.up * 0.013f + t * 0.6f,
                        s + r * (k * 2f - 1f) + Vector3.up * 0.013f + t * 0.6f, s + r * (k * 2f - 1f) + Vector3.up * 0.013f - t * 0.6f);
            }

            // ---------- perimeter walls (gap on the west side for the access road) ----------
            PropBuilder.Wall(cb, m, new Vector3(ax0, 0, az0), new Vector3(ax1, 0, az0), 2.2f, 0.6f, m.concrete);
            PropBuilder.Wall(cb, m, new Vector3(ax1, 0, az0), new Vector3(ax1, 0, az1), 2.2f, 0.6f, m.concrete);
            PropBuilder.Wall(cb, m, new Vector3(ax1, 0, az1), new Vector3(ax0, 0, az1), 2.2f, 0.6f, m.concrete);
            PropBuilder.Wall(cb, m, new Vector3(ax0, 0, az1), new Vector3(ax0, 0, 12f), 2.2f, 0.6f, m.concrete);
            PropBuilder.Wall(cb, m, new Vector3(ax0, 0, -12f), new Vector3(ax0, 0, az0), 2.2f, 0.6f, m.concrete);

            // ---------- factory complex (north) ----------
            PropBuilder.Factory(cb, m, new Vector3(C.x - 60f, 0, az1 + 22f), new Vector3(90f, 18f, 30f), 0f, 0);
            PropBuilder.Factory(cb, m, new Vector3(C.x + 55f, 0, az1 + 20f), new Vector3(70f, 24f, 28f), 0f, 1);
            PropBuilder.Factory(cb, m, new Vector3(C.x + 140f, 0, az1 + 25f), new Vector3(40f, 14f, 36f), 0f, 2);
            PropBuilder.Chimney(cb, m, new Vector3(C.x - 5f, 0, az1 + 45f), 48f);
            PropBuilder.Chimney(cb, m, new Vector3(C.x + 15f, 0, az1 + 48f), 38f);
            PropBuilder.Silo(cb, m, new Vector3(C.x + 100f, 0, az1 + 52f), 5f, 22f);
            PropBuilder.Silo(cb, m, new Vector3(C.x + 112f, 0, az1 + 52f), 5f, 22f);
            // pipes
            cb.For(m.metal).AddBox(new Vector3(C.x - 20f, 9f, az1 + 3f), new Vector3(60f, 0.7f, 0.7f), Quaternion.identity, Vector2.zero, true, true);
            // containers south of the apron
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 14; k++)
                    if (rng.Next(4) > 0)
                        PropBuilder.Container(cb, m, new Vector3(ax0 + 18f + k * 7.2f, 0.0f, az0 - 14f - row * 9f), 90f, rng.Next(6), 1 + rng.Next(3));
            // east side: silos + warehouses beyond the wall
            for (int k = 0; k < 5; k++)
                PropBuilder.Factory(cb, m, new Vector3(ax1 + 30f, 0, az0 + 30f + k * 40f), new Vector3(30f, 10f + k * 2f, 24f), 90f, k);
            // trees outside
            for (int k = 0; k < 24; k++)
                PropBuilder.Tree(cb, m, new Vector3(ax0 - 20f - (float)rng.NextDouble() * 20f, 0f, az0 + (float)rng.NextDouble() * ad), 1.1f, rng);

            // ---------- track-edge props ----------
            var zoneIdx = new List<int>();
            for (int i = 0; i < n; i += 2)
            {
                float curv = path.Curvature(i, 12f);
                Vector3 p = path.Point(i), t = path.Tangent(i), r = Vector3.Cross(Vector3.up, t);
                float hw = path.width * 0.5f;
                float ac2 = Mathf.Abs(curv);
                if (ac2 > 22f)
                {
                    Vector3 outside = (curv > 0 ? -r : r);
                    // tyre walls on the outside edge, cones + curbs on the inside
                    PropBuilder.TireStack(cb, m, p + outside * (hw + 1.4f), 3);
                    PropBuilder.TireStack(cb, m, p + outside * (hw + 1.4f) + t * 2f, 3);
                    if ((i % 4) == 0) PropBuilder.Cone(cb, m, p - outside * (hw - 1.2f));
                    if ((i % 6) == 0) PropBuilder.ConcreteBarrier(cb, m, p + outside * (hw + 3.5f), Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg, true);
                }
                else if ((i % 12) == 0)
                {
                    PropBuilder.ConcreteBarrier(cb, m, p + r * (hw + 2.5f), Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg, false);
                    PropBuilder.ConcreteBarrier(cb, m, p - r * (hw + 2.5f), Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg, false);
                }
                if (ac2 > 30f && (zoneIdx.Count == 0 || i - zoneIdx[zoneIdx.Count - 1] > 14)) zoneIdx.Add(i);
            }

            // ---------- drift zones & spectator spots at the apexes ----------
            foreach (int idx in zoneIdx)
            {
                Vector3 p = path.Point(idx), t = path.Tangent(idx), r = Vector3.Cross(Vector3.up, t);
                float curv = path.Curvature(idx, 12f);
                info.industrialZones.Add(new DriftZoneDef
                {
                    center = p + Vector3.up * 1f, size = new Vector3(path.width + 14f, 6f, 44f),
                    rot = Quaternion.LookRotation(t, Vector3.up), multiplier = 2f
                });
                Vector3 outside = curv > 0 ? -r : r;
                info.industrialSpectators.Add(p + outside * (path.width * 0.5f + 7f));
                info.industrialSpectators.Add(p + outside * (path.width * 0.5f + 9f) + t * 6f);
            }

            var go = cb.Build("Industrial", parent);
            info.chunks.Add(go.transform);

            // chimney smoke
            AddSmoke(go.transform, new Vector3(C.x - 5f, 49f, az1 + 45f));
            AddSmoke(go.transform, new Vector3(C.x + 15f, 39f, az1 + 48f));
        }

        private static void AddSmoke(Transform parent, Vector3 pos)
        {
            var mat = MatLib.Alpha(ProcTex.SoftCircle(), Color.white);
            var ps = RussianDrift.Core.WorldFx.MakeSmoke("ChimneySmoke", parent, mat, pos);
        }
    }
}
