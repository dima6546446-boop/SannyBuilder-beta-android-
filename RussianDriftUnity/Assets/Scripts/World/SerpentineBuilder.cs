using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Mountain serpentine north of the city: switchback road climbing ~100 m, terrain mesh blended to the road, guard rails.</summary>
    public static class SerpentineBuilder
    {
        public const float RoadW = 12f;

        public static void Build(WorldInfo info, Transform parent, System.Random rng)
        {
            var m = info.mats;
            // ---------- turtle path ----------
            var pts = new List<Vector3>();
            Vector3 pos = new Vector3(47f, 0f, 242f);
            Vector3 heading = Vector3.forward;
            var xz = new List<Vector2>();
            var hairpinApex = new List<Vector2>();
            var hairpinDir = new List<Vector2>();
            var hairpinSide = new List<float>();
            xz.Add(new Vector2(pos.x, pos.z));

            System.Action<float> straight = len =>
            {
                int steps = Mathf.Max(1, Mathf.RoundToInt(len / 5f));
                for (int i = 0; i < steps; i++) { pos += heading * (len / steps); xz.Add(new Vector2(pos.x, pos.z)); }
            };
            System.Action<float, float> arc = (r, ang) =>
            {
                // ang > 0: right turn, ang < 0: left turn (degrees)
                float sign = Mathf.Sign(ang);
                Vector3 right = Vector3.Cross(Vector3.up, heading);
                Vector3 center = pos + right * (r * sign);
                int steps = Mathf.Max(4, Mathf.RoundToInt(Mathf.Abs(ang) / 7.5f));
                float step = ang / steps;
                Vector3 rel = pos - center;
                for (int i = 0; i < steps; i++)
                {
                    rel = Quaternion.AngleAxis(step, Vector3.up) * rel;
                    heading = Quaternion.AngleAxis(step, Vector3.up) * heading;
                    pos = center + rel;
                    xz.Add(new Vector2(pos.x, pos.z));
                    if (Mathf.Abs(ang) > 150f && i == steps / 2) { hairpinApex.Add(new Vector2(pos.x, pos.z)); hairpinDir.Add(new Vector2(heading.x, heading.z)); hairpinSide.Add(sign); }
                }
            };

            straight(70f);
            arc(24f, 90f);
            for (int pair = 0; pair < 3; pair++)
            {
                straight(150f); arc(24f, -180f);
                straight(150f); arc(24f, 180f);
            }
            straight(80f);

            // heights: flat start, then ~6 % grade
            float total = 0f;
            var dist = new float[xz.Count];
            for (int i = 1; i < xz.Count; i++) { total += Vector2.Distance(xz[i], xz[i - 1]); dist[i] = total; }
            float grade = 0.062f;
            for (int i = 0; i < xz.Count; i++)
            {
                float d = dist[i];
                float h = d < 80f ? 0.04f + grade * (d * d / 160f) : 0.04f + grade * (d - 40f);
                pts.Add(new Vector3(xz[i].x, h, xz[i].y));
            }
            var path = TrackPath.FromPoints("serpentine", pts, false, RoadW);
            info.serpentine = path;
            int n = path.Count;

            // ---------- road ribbon ----------
            var cb = new ChunkBuilder();
            var road = cb.For(m.asphalt);
            var edge = cb.For(m.white);
            var mesh = new MeshBuilder();
            float hw = RoadW * 0.5f;
            var ribbon = new MeshBuilder();
            float vAcc = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = path.Point(i), b = path.Point(i + 1);
                Vector3 ta = path.Tangent(i), tb = path.Tangent(i + 1);
                Vector3 ra = Vector3.Cross(Vector3.up, ta), rb = Vector3.Cross(Vector3.up, tb);
                float seg = Vector3.Distance(a, b);
                Vector3 aL = a - ra * hw, aR = a + ra * hw, bL = b - rb * hw, bR = b + rb * hw;
                road.AddQuad(aL, bL, bR, aR, new Vector2(0, vAcc / 6f), new Vector2(0, (vAcc + seg) / 6f), new Vector2(2f, (vAcc + seg) / 6f), new Vector2(2f, vAcc / 6f));
                ribbon.AddQuad(aL, bL, bR, aR);
                vAcc += seg;
                Vector3 up = Vector3.up * 0.02f;
                edge.AddQuad(aR - ra * 0.5f + up, bR - rb * 0.5f + up, bR - rb * 0.25f + up, aR - ra * 0.25f + up);
                edge.AddQuad(aL + ra * 0.25f + up, bL + rb * 0.25f + up, bL + rb * 0.5f + up, aL + ra * 0.5f + up);
                if ((i & 1) == 0)
                    edge.AddQuad(a - ra * 0.07f + up, b - rb * 0.07f + up, b + rb * 0.07f + up, a + ra * 0.07f + up);
            }
            var roadGo = cb.Build("SerpentineRoad", parent);
            // collider from the ribbon (thickened by an extra underside via a second offset mesh is unnecessary: one-sided is enough for rays)
            var rc = new GameObject("RoadCollider");
            rc.transform.SetParent(roadGo.transform, false);
            var meshCol = rc.AddComponent<MeshCollider>();
            meshCol.sharedMesh = ribbon.ToMesh("SerpRoadCol");
            var si = rc.AddComponent<SurfaceInfo>(); si.type = SurfaceType.Asphalt; si.grip = 1f;
            info.chunks.Add(roadGo.transform);

            // ---------- terrain ----------
            BuildTerrain(info, parent, path, m);

            // ---------- guard rails (both sides) ----------
            var rails = new ChunkBuilder();
            for (int i = 0; i < n - 1; i += 2)
            {
                Vector3 a = path.Point(i), b = path.Point(Mathf.Min(i + 2, n - 1));
                Vector3 d = b - a; float len = d.magnitude;
                if (len < 0.5f) continue;
                Vector3 t = d / len;
                Vector3 r = Vector3.Cross(Vector3.up, new Vector3(t.x, 0f, t.z).normalized);
                var rot = Quaternion.LookRotation(new Vector3(t.x, 0f, t.z).normalized, Vector3.up);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 c = (a + b) * 0.5f + r * (s * (hw + 0.9f)) + Vector3.up * 0.55f;
                    rails.For(m.fenceMat).AddBox(c, new Vector3(0.12f, 0.35f, len + 0.1f), rot, Vector2.zero, true, true);
                    rails.For(m.concrete).AddBox(c - Vector3.up * 0.28f + Vector3.zero, new Vector3(0.15f, 0.55f, 0.15f), rot, Vector2.zero, true, false);
                    rails.Collider(c, new Vector3(0.3f, 0.9f, len + 0.1f), rot);
                }
            }
            var railGo = rails.Build("GuardRails", parent, false);
            info.chunks.Add(railGo.transform);

            // ---------- summit parking + viewpoint ----------
            Vector3 top = path.Point(n - 1);
            var cb2 = new ChunkBuilder();
            Vector3 padC = top + new Vector3(30f, 0f, 0f);
            cb2.For(m.asphalt).AddFace(new Vector3(padC.x, top.y + 0.01f, padC.z), Vector3.up, Vector3.right, 60f, 40f, new Vector2(6f, 6f));
            cb2.Collider(new Vector3(padC.x, top.y - 0.14f, padC.z), new Vector3(60f, 0.3f, 40f), Quaternion.identity, SurfaceType.Asphalt, 1f);
            for (int k = 0; k < 5; k++)
                PropBuilder.Cone(cb2, m, new Vector3(top.x + 8f + k * 1.2f, top.y + 0.02f, top.z + 5f));
            cb2.For(m.signGreen).AddBox(top + new Vector3(0f, 4f, 0f), new Vector3(10f, 1.2f, 0.3f), Quaternion.identity, Vector2.zero, true, true);
            // finish gantry
            cb2.For(m.fenceMat).AddBox(top + new Vector3(-8f, 3f, 0f), new Vector3(0.4f, 6f, 0.4f), Quaternion.identity, Vector2.zero, true, true);
            cb2.For(m.fenceMat).AddBox(top + new Vector3(8f, 3f, 0f), new Vector3(0.4f, 6f, 0.4f), Quaternion.identity, Vector2.zero, true, true);
            var topGo = cb2.Build("SummitPad", parent);
            info.chunks.Add(topGo.transform);

            // ---------- zones and spectators at hairpins ----------
            for (int k = 0; k < hairpinApex.Count; k++)
            {
                Vector2 a2 = hairpinApex[k];
                int idx = path.Nearest(new Vector3(a2.x, 0, a2.y));
                Vector3 p = path.Point(idx);
                Vector3 t = path.Tangent(idx);
                Vector3 r = Vector3.Cross(Vector3.up, t);
                float side = hairpinSide[k];
                info.serpentineZones.Add(new DriftZoneDef { center = p + Vector3.up, size = new Vector3(52f, 8f, 52f), rot = Quaternion.LookRotation(t, Vector3.up), multiplier = 2f });
                // outside of a right turn (side>0) is the left side
                Vector3 outside = side > 0 ? -r : r;
                info.serpentineSpectators.Add(p + outside * (hw + 5f));
                info.serpentineSpectators.Add(p + outside * (hw + 5f) + t * 5f);
            }
        }

        private static void BuildTerrain(WorldInfo info, Transform parent, TrackPath path, WorldMaterials m)
        {
            const float cell = 6f;
            float x0 = -80f, x1 = 350f, z0 = 236f, z1 = 700f;
            int nx = Mathf.CeilToInt((x1 - x0) / cell), nz = Mathf.CeilToInt((z1 - z0) / cell);
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uvs = new Vector2[verts.Length];
            int pn = path.Count;
            // subsample the path for nearest queries
            int step = 2;
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = x0 + i * cell, z = z0 + j * cell;
                    float minD = float.MaxValue, nearestH = 0f, wSum = 0f, hSum = 0f;
                    for (int k = 0; k < pn; k += step)
                    {
                        Vector3 p = path.pts[k];
                        float dx = p.x - x, dz = p.z - z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 < minD * minD || minD == float.MaxValue) { float d = Mathf.Sqrt(d2); if (d < minD) { minD = d; nearestH = p.y; } }
                        float w = 1f / (d2 + 400f);
                        w *= w;
                        wSum += w; hSum += w * p.y;
                    }
                    float idw = hSum / Mathf.Max(1e-9f, wSum);
                    float roadBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((minD - (RoadW * 0.5f + 2f)) / 18f));
                    float h = Mathf.Lerp(nearestH - 0.08f, idw, roadBlend);
                    float falloff = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((minD - 25f) / 130f));
                    h *= falloff;
                    // small rock noise away from the road
                    h += (Mathf.PerlinNoise(x * 0.03f, z * 0.03f) - 0.5f) * 5f * roadBlend * falloff;
                    // keep the strip near the city flat/outside ground
                    if (z < 250f) h = Mathf.Lerp(0f, h, Mathf.Clamp01((z - 236f) / 14f));
                    verts[j * (nx + 1) + i] = new Vector3(x, Mathf.Max(h, -0.02f), z);
                    uvs[j * (nx + 1) + i] = new Vector2(x / 20f, z / 20f);
                }
            var tris = new List<int>();
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = (j + 1) * (nx + 1) + i, c = (j + 1) * (nx + 1) + i + 1, d = j * (nx + 1) + i + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                }
            var mesh = new Mesh { name = "SerpentineTerrain" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("SerpentineTerrain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.grass;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            var si = go.AddComponent<SurfaceInfo>(); si.type = SurfaceType.Grass; si.grip = 0.6f;
            go.isStatic = true;
            info.chunks.Add(go.transform);

            // trees on the slopes
            var cb = new ChunkBuilder();
            var rnd = new System.Random(5);
            for (int k = 0; k < 260; k++)
            {
                float x = x0 + (float)rnd.NextDouble() * (x1 - x0), z = z0 + 20f + (float)rnd.NextDouble() * (z1 - z0 - 20f);
                int j = Mathf.Clamp(Mathf.RoundToInt((z - z0) / cell), 0, nz), i = Mathf.Clamp(Mathf.RoundToInt((x - x0) / cell), 0, nx);
                Vector3 v = verts[j * (nx + 1) + i];
                int near = path.Nearest(new Vector3(x, 0, z));
                if (Vector2.Distance(new Vector2(path.pts[near].x, path.pts[near].z), new Vector2(x, z)) < RoadW + 6f) continue;
                if (v.y < 1f && (z < 260f)) continue;
                PropBuilder.Tree(cb, m, new Vector3(x, v.y, z), 1.2f + (float)rnd.NextDouble() * 0.8f, rnd);
            }
            var tg = cb.Build("SerpentineTrees", parent);
            info.chunks.Add(tg.transform);
        }
    }
}
