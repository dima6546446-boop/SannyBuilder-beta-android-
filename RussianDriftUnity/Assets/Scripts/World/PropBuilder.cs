using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Procedural city/industrial props. Every function writes into a ChunkBuilder (visual + colliders).</summary>
    public static class PropBuilder
    {
        private static readonly Vector2 FacadeTile = new Vector2(6f, 3f);
        private static readonly Vector2 ConcTile = new Vector2(4f, 4f);
        public const float Slab = 0.15f;

        private static Quaternion Yaw(float deg) { return Quaternion.Euler(0f, deg, 0f); }

        public static void PanelHouse(ChunkBuilder cb, WorldMaterials m, Vector2 pos, float w, float d, int floors, float yaw, int variant)
        {
            float h = floors * 3f;
            var rot = Yaw(yaw);
            Vector3 c = new Vector3(pos.x, Slab + h * 0.5f, pos.y);
            cb.Box(m.facades[variant & 3], c, new Vector3(w, h, d), rot, FacadeTile, false, false);
            cb.Box(m.roof, new Vector3(pos.x, Slab + h + 0.15f, pos.y), new Vector3(w + 0.5f, 0.3f, d + 0.5f), rot, ConcTile, true, false);
            // stair/lift housing on the roof
            Vector3 off = rot * new Vector3(w * 0.3f, 0f, 0f);
            cb.Box(m.concrete, new Vector3(pos.x + off.x, Slab + h + 1.6f, pos.y + off.z), new Vector3(3f, 2.6f, Mathf.Min(d, 4f)), rot, ConcTile, true, false);
            // entrance canopies
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 e = rot * new Vector3(k * w * 0.25f, 0f, d * 0.5f + 0.8f);
                cb.Box(m.black, new Vector3(pos.x + e.x, Slab + 2.6f, pos.y + e.z), new Vector3(2.6f, 0.2f, 1.6f), rot, Vector2.zero, true, false);
            }
            cb.Collider(c, new Vector3(w, h, d), rot);
        }

        public static void Tower(ChunkBuilder cb, WorldMaterials m, Vector2 pos, float w, int floors, float yaw, int variant)
        {
            PanelHouse(cb, m, pos, w, w, floors, yaw, variant);
        }

        public static void Tree(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float scale, System.Random r)
        {
            float th = 2.4f * scale;
            cb.For(m.trunk).AddCylinder(pos + Vector3.up * (th * 0.5f), 0.18f * scale, th, Quaternion.identity, 5, false);
            float cr = (1.7f + (float)r.NextDouble() * 0.9f) * scale;
            var prof = new[] { new Vector2(0f, 0f), new Vector2(cr, 0f), new Vector2(cr * 0.75f, cr * 0.9f), new Vector2(0.05f, cr * 1.9f) };
            var mb = cb.For(m.leaves);
            mb.AddLathe(prof, 8, pos + Vector3.up * (th * 0.8f), Quaternion.identity);
            var prof2 = new[] { new Vector2(0f, 0f), new Vector2(cr * 0.75f, 0f), new Vector2(cr * 0.5f, cr * 0.8f), new Vector2(0.05f, cr * 1.5f) };
            mb.AddLathe(prof2, 8, pos + Vector3.up * (th * 0.8f + cr * 1.1f), Quaternion.identity);
            cb.SoftCollider(pos + Vector3.up * 1.5f, new Vector3(0.4f, 3f, 0.4f), Quaternion.identity);
        }

        public static void Lamp(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float yaw, List<Vector3> lamps)
        {
            var rot = Yaw(yaw);
            float H = 7.5f;
            cb.For(m.fenceMat).AddBox(pos + Vector3.up * (H * 0.5f), new Vector3(0.14f, H, 0.14f), Quaternion.identity, Vector2.zero, false, false);
            Vector3 arm = rot * new Vector3(0f, 0f, 1.4f);
            cb.For(m.fenceMat).AddBox(pos + Vector3.up * H + arm * 0.5f, new Vector3(0.1f, 0.1f, 1.5f), rot, Vector2.zero, true, true);
            Vector3 head = pos + Vector3.up * (H - 0.05f) + arm;
            cb.For(m.lampHead).AddBox(head, new Vector3(0.45f, 0.12f, 0.8f), rot, Vector2.zero, true, true);
            cb.SoftCollider(pos + Vector3.up * 1.5f, new Vector3(0.25f, 3f, 0.25f), Quaternion.identity);
            if (lamps != null) lamps.Add(head);
        }

        public static void LampGlowQuad(ChunkBuilder cb, WorldMaterials m, Vector3 headPos)
        {
            cb.For(m.lampGlow).AddFace(new Vector3(headPos.x, Slab + 0.03f, headPos.z), Vector3.up, Vector3.right, 16f, 16f, Vector2.zero);
        }

        public static void TrafficLightPole(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float yawFacing, bool northSouth, bool groupA)
        {
            var rot = Yaw(yawFacing);
            cb.For(m.black).AddBox(pos + Vector3.up * 2.6f, new Vector3(0.15f, 5.2f, 0.15f), Quaternion.identity, Vector2.zero, true, false);
            Vector3 headPos = pos + Vector3.up * 5.0f;
            cb.For(m.black).AddBox(headPos, new Vector3(0.45f, 0.45f, 0.35f), rot, Vector2.zero, true, true);
            Material lm = northSouth ? (groupA ? m.lightNSa : m.lightNSb) : (groupA ? m.lightEWa : m.lightEWb);
            Vector3 fwd = rot * Vector3.forward;
            cb.For(lm).AddFace(headPos + fwd * 0.19f, fwd, Vector3.Cross(fwd, Vector3.up), 0.3f, 0.3f, Vector2.zero);
            cb.SoftCollider(pos + Vector3.up * 1.5f, new Vector3(0.3f, 3f, 0.3f), Quaternion.identity);
        }

        public static void Garages(ChunkBuilder cb, WorldMaterials m, Vector2 pos, int count, float yaw, System.Random r)
        {
            var rot = Yaw(yaw);
            float gw = 3.4f, gd = 6.5f, gh = 2.7f;
            float total = count * gw;
            Vector3 c = new Vector3(pos.x, Slab + gh * 0.5f, pos.y);
            cb.Box(m.concrete, c, new Vector3(total, gh, gd), rot, ConcTile, false, false);
            cb.Box(m.roof, new Vector3(pos.x, Slab + gh + 0.1f, pos.y), new Vector3(total + 0.3f, 0.2f, gd + 0.3f), rot, ConcTile, true, false);
            for (int i = 0; i < count; i++)
            {
                float x = -total * 0.5f + gw * (i + 0.5f);
                Vector3 p = rot * new Vector3(x, 0f, gd * 0.5f + 0.03f);
                var mat = m.containers[r.Next(m.containers.Length)];
                cb.Box(mat, new Vector3(pos.x + p.x, Slab + 1.15f, pos.y + p.z), new Vector3(gw - 0.25f, 2.3f, 0.06f), rot, Vector2.zero, true, false);
            }
            cb.Collider(c, new Vector3(total, gh, gd), rot);
        }

        public static void GasStation(ChunkBuilder cb, WorldMaterials m, Vector2 pos, float yaw)
        {
            var rot = Yaw(yaw);
            Vector3 P(float x, float y, float z) { Vector3 o = rot * new Vector3(x, 0, z); return new Vector3(pos.x + o.x, y, pos.y + o.z); }
            cb.Box(m.white, P(-8f, Slab + 5.6f, -6f), new Vector3(20f, 0.7f, 12f), rot, Vector2.zero, true, true);
            cb.Box(m.red, P(-8f, Slab + 5.25f, -6f), new Vector3(20.2f, 0.12f, 12.2f), rot, Vector2.zero, true, true);
            for (int ix = 0; ix < 2; ix++) for (int iz = 0; iz < 2; iz++)
                {
                    Vector3 pp = P(-16f + ix * 16f, Slab + 2.6f, -10.5f + iz * 9f);
                    cb.SolidBox(m.concrete, pp, new Vector3(0.6f, 5.2f, 0.6f), rot, ConcTile);
                }
            for (int i = 0; i < 3; i++)
            {
                Vector3 pp = P(-13f + i * 5f, Slab + 0.8f, -6f);
                cb.SolidBox(m.red, pp, new Vector3(0.7f, 1.6f, 1.2f), rot, Vector2.zero);
                cb.Box(m.white, pp + Vector3.up * 0.3f, new Vector3(0.72f, 0.3f, 1.22f), rot, Vector2.zero, true, false);
            }
            // shop
            cb.Box(m.facades[1], P(14f, Slab + 2.2f, -4f), new Vector3(14f, 4.4f, 9f), rot, FacadeTile, false, false);
            cb.Box(m.roof, P(14f, Slab + 4.5f, -4f), new Vector3(14.4f, 0.3f, 9.4f), rot, ConcTile, true, false);
            Vector3 face = rot * Vector3.forward;
            cb.Box(m.glass, P(14f, Slab + 1.6f, 0.52f), new Vector3(10f, 2.4f, 0.1f), rot, Vector2.zero, true, false);
            cb.Box(m.signRed, P(14f, Slab + 4.0f, 0.55f), new Vector3(8f, 0.6f, 0.1f), rot, Vector2.zero, true, false);
            cb.Collider(P(14f, Slab + 2.2f, -4f), new Vector3(14f, 4.4f, 9f), rot);
            // price pylon
            cb.Box(m.concrete, P(-22f, Slab + 4f, 4f), new Vector3(0.5f, 8f, 0.5f), rot, ConcTile, true, false);
            cb.Box(m.signYellow, P(-22f, Slab + 7.4f, 4f), new Vector3(2.2f, 1.6f, 0.3f), rot, Vector2.zero, true, false);
            cb.Collider(P(-22f, Slab + 4f, 4f), new Vector3(0.5f, 8f, 0.5f), rot);
        }

        public static void LowPolyCar(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float yaw, Material paint)
        {
            var rot = Yaw(yaw);
            cb.Box(paint, pos + Vector3.up * 0.62f, new Vector3(1.7f, 0.7f, 4.2f), rot, Vector2.zero, true, false);
            Vector3 off = rot * new Vector3(0f, 0f, -0.2f);
            cb.Box(m.glass, pos + Vector3.up * 1.22f + off, new Vector3(1.5f, 0.55f, 2.2f), rot, Vector2.zero, true, false);
            cb.Box(m.tire, pos + Vector3.up * 0.25f, new Vector3(1.75f, 0.5f, 3.4f), rot, Vector2.zero, false, false);
            cb.Collider(pos + Vector3.up * 0.9f, new Vector3(1.7f, 1.4f, 4.2f), rot);
        }

        public static void ParkingLines(ChunkBuilder cb, WorldMaterials m, Vector2 center, float w, float d, float yaw, float baseY)
        {
            var rot = Yaw(yaw);
            int slots = Mathf.FloorToInt(w / 2.8f);
            for (int row = -1; row <= 1; row += 2)
                for (int i = 0; i <= slots; i++)
                {
                    Vector3 o = rot * new Vector3(-w * 0.5f + i * 2.8f, 0f, row * d * 0.3f);
                    var mb = cb.For(m.white);
                    Vector3 dirF = rot * Vector3.forward, dirR = rot * Vector3.right;
                    Vector3 p = new Vector3(center.x + o.x, baseY + 0.012f, center.y + o.z);
                    mb.AddFace(p, Vector3.up, dirR, 0.12f, 5.2f, Vector2.zero);
                    // note: AddFace(u=right) => width along right, length along cross(right, up) == forward axis
                }
        }

        public static void Container(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float yaw, int colorIdx, int stack)
        {
            var rot = Yaw(yaw);
            for (int s = 0; s < stack; s++)
            {
                Vector3 c = pos + Vector3.up * (1.3f + s * 2.6f);
                cb.Box(m.containers[colorIdx % m.containers.Length], c, new Vector3(2.45f, 2.6f, 6.1f), rot, new Vector2(3f, 3f), true, false);
            }
            cb.Collider(pos + Vector3.up * (1.3f * stack), new Vector3(2.45f, 2.6f * stack, 6.1f), rot);
        }

        public static void TireStack(ChunkBuilder cb, WorldMaterials m, Vector3 pos, int height)
        {
            for (int i = 0; i < height; i++)
                cb.For(m.tire).AddCylinder(pos + Vector3.up * (0.16f + i * 0.32f), 0.46f, 0.3f, Quaternion.identity, 10, true);
            cb.SoftCollider(pos + Vector3.up * (0.16f * height), new Vector3(0.9f, 0.32f * height, 0.9f), Quaternion.identity);
        }

        public static void ConcreteBarrier(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float yaw, bool paint)
        {
            var rot = Yaw(yaw);
            cb.Box(paint ? m.curbWhite : m.concrete, pos + Vector3.up * 0.45f, new Vector3(0.55f, 0.9f, 3f), rot, ConcTile, true, false);
            if (paint) cb.Box(m.curbRed, pos + Vector3.up * 0.65f, new Vector3(0.57f, 0.25f, 3.01f), rot, Vector2.zero, true, false);
            cb.Collider(pos + Vector3.up * 0.45f, new Vector3(0.55f, 0.9f, 3f), rot);
        }

        public static void Cone(ChunkBuilder cb, WorldMaterials m, Vector3 pos)
        {
            var prof = new[] { new Vector2(0.01f, 0f), new Vector2(0.22f, 0f), new Vector2(0.03f, 0.55f) };
            cb.For(m.orange).AddLathe(prof, 8, pos, Quaternion.identity);
            cb.For(m.orange).AddBox(pos + Vector3.up * 0.02f, new Vector3(0.46f, 0.04f, 0.46f), Quaternion.identity, Vector2.zero, true, false);
            cb.SoftCollider(pos + Vector3.up * 0.25f, new Vector3(0.35f, 0.5f, 0.35f), Quaternion.identity);
        }

        public static void Wall(ChunkBuilder cb, WorldMaterials m, Vector3 a, Vector3 b, float height, float thick, Material mat)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.1f) return;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            Vector3 c = (a + b) * 0.5f + Vector3.up * (height * 0.5f);
            cb.Box(mat, c, new Vector3(thick, height, len), rot, ConcTile, true, false);
            cb.Collider(c, new Vector3(thick, height, len), rot);
        }

        public static void Chimney(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float height)
        {
            cb.For(m.concrete).AddCylinder(pos + Vector3.up * (height * 0.5f), 3f, height, Quaternion.identity, 14, false);
            for (int i = 0; i < 4; i++)
                cb.For(m.red).AddCylinder(pos + Vector3.up * (height - 4f - i * 7f), 3.06f, 3f, Quaternion.identity, 14, false);
            cb.Collider(pos + Vector3.up * (height * 0.5f), new Vector3(5.6f, height, 5.6f), Quaternion.identity);
        }

        public static void Silo(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float radius, float height)
        {
            cb.For(m.metal).AddCylinder(pos + Vector3.up * (height * 0.5f), radius, height, Quaternion.identity, 16, true);
            cb.Collider(pos + Vector3.up * (height * 0.5f), new Vector3(radius * 1.8f, height, radius * 1.8f), Quaternion.identity);
        }

        public static void Factory(ChunkBuilder cb, WorldMaterials m, Vector3 pos, Vector3 size, float yaw, int variant)
        {
            var rot = Yaw(yaw);
            Vector3 c = pos + Vector3.up * (size.y * 0.5f);
            cb.Box(m.metal, c, size, rot, new Vector2(6f, 4f), false, false);
            cb.Box(m.roof, pos + Vector3.up * (size.y + 0.4f), new Vector3(size.x + 0.6f, 0.8f, size.z + 0.6f), rot, ConcTile, true, false);
            // clerestory windows band
            cb.Box(m.glass, c + rot * new Vector3(0, size.y * 0.25f, size.z * 0.5f + 0.05f), new Vector3(size.x * 0.9f, 1.6f, 0.1f), rot, Vector2.zero, true, false);
            cb.Collider(c, size, rot);
        }

        public static void Billboard(ChunkBuilder cb, WorldMaterials m, Vector3 pos, float yaw, Material sign)
        {
            var rot = Yaw(yaw);
            cb.Box(m.fenceMat, pos + Vector3.up * 4f, new Vector3(0.4f, 8f, 0.4f), Quaternion.identity, Vector2.zero, true, false);
            cb.Box(m.black, pos + Vector3.up * 8.6f, new Vector3(9.4f, 4.4f, 0.3f), rot, Vector2.zero, true, false);
            Vector3 f = rot * Vector3.forward * 0.17f;
            cb.Box(sign, pos + Vector3.up * 8.6f + f, new Vector3(8.8f, 3.8f, 0.1f), rot, Vector2.zero, true, false);
            cb.Collider(pos + Vector3.up * 4f, new Vector3(0.5f, 8f, 0.5f), Quaternion.identity);
        }

        public static void Shop(ChunkBuilder cb, WorldMaterials m, Vector2 pos, float w, float d, float yaw, int variant, Material sign)
        {
            var rot = Yaw(yaw);
            float h = 4.6f;
            Vector3 c = new Vector3(pos.x, Slab + h * 0.5f, pos.y);
            cb.Box(m.brick, c, new Vector3(w, h, d), rot, new Vector2(4f, 4f), false, false);
            cb.Box(m.roof, new Vector3(pos.x, Slab + h + 0.15f, pos.y), new Vector3(w + 0.4f, 0.3f, d + 0.4f), rot, ConcTile, true, false);
            Vector3 f = rot * new Vector3(0f, 0f, d * 0.5f + 0.05f);
            cb.Box(m.glass, new Vector3(pos.x + f.x, Slab + 1.6f, pos.y + f.z), new Vector3(w * 0.8f, 2.2f, 0.1f), rot, Vector2.zero, true, false);
            cb.Box(sign, new Vector3(pos.x + f.x * 1.001f, Slab + 3.6f, pos.y + f.z * 1.001f), new Vector3(w * 0.6f, 0.7f, 0.12f), rot, Vector2.zero, true, false);
            cb.Collider(c, new Vector3(w, h, d), rot);
        }

        public static void Crosswalk(ChunkBuilder cb, WorldMaterials m, Vector3 center, float yaw, float length)
        {
            var rot = Yaw(yaw);
            int stripes = Mathf.FloorToInt(length / 1.0f);
            for (int i = 0; i < stripes; i++)
            {
                Vector3 o = rot * new Vector3(-length * 0.5f + i * 1.0f + 0.5f, 0, 0);
                cb.For(m.white).AddFace(center + o + Vector3.up * 0.052f, Vector3.up, rot * Vector3.right, 0.5f, 3f, Vector2.zero);
            }
        }
    }
}
