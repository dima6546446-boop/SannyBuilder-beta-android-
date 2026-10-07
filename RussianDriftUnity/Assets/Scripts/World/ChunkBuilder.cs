using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Collects geometry per material and box colliders for one world chunk, then emits a single combined GameObject.</summary>
    public class ChunkBuilder
    {
        private readonly Dictionary<Material, MeshBuilder> builders = new Dictionary<Material, MeshBuilder>();
        private readonly List<BoxDef> boxes = new List<BoxDef>();

        private struct BoxDef
        {
            public Vector3 center, size; public Quaternion rot; public SurfaceType surf; public float grip; public bool tag; public bool soft;
        }

        public MeshBuilder For(Material m)
        {
            MeshBuilder b;
            if (!builders.TryGetValue(m, out b)) { b = new MeshBuilder(); builders[m] = b; }
            return b;
        }

        public void Box(Material m, Vector3 center, Vector3 size, Quaternion rot, Vector2 uvTile, bool top = true, bool bottom = false)
        {
            For(m).AddBox(center, size, rot, uvTile, top, bottom);
        }

        public void Collider(Vector3 center, Vector3 size, Quaternion rot, SurfaceType type = SurfaceType.Concrete, float grip = 1f)
        {
            boxes.Add(new BoxDef { center = center, size = size, rot = rot, surf = type, grip = grip });
        }

        public void SoftCollider(Vector3 center, Vector3 size, Quaternion rot)
        {
            boxes.Add(new BoxDef { center = center, size = size, rot = rot, surf = SurfaceType.Concrete, grip = 1f, soft = true });
        }

        /// <summary>Visual box plus a matching collider.</summary>
        public void SolidBox(Material m, Vector3 center, Vector3 size, Quaternion rot, Vector2 uvTile, bool top = true)
        {
            Box(m, center, size, rot, uvTile, top, false);
            Collider(center, size, rot);
        }

        public GameObject Build(string name, Transform parent, bool shadows = true)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            foreach (var kv in builders)
            {
                if (kv.Value.VertexCount == 0) continue;
                var mesh = kv.Value.ToMesh(name + "_" + kv.Key.name);
                var go = MeshUtil.Make(kv.Key.name, root.transform, mesh, kv.Key, false, shadows);
                go.isStatic = true;
            }
            if (boxes.Count > 0)
            {
                var colGo = new GameObject("Colliders");
                colGo.transform.SetParent(root.transform, false);
                colGo.isStatic = true;
                // group by surface type so a SurfaceInfo can be attached per group
                var groups = new Dictionary<long, GameObject>();
                for (int i = 0; i < boxes.Count; i++)
                {
                    var b = boxes[i];
                    long key = (long)b.surf * 100000 + Mathf.RoundToInt(b.grip * 100f) * 10 + (b.soft ? 1 : 0);
                    GameObject g;
                    if (!groups.TryGetValue(key, out g))
                    {
                        g = new GameObject("C_" + b.surf);
                        g.transform.SetParent(colGo.transform, false);
                        g.isStatic = true;
                        var si = g.AddComponent<SurfaceInfo>(); si.type = b.surf; si.grip = b.grip;
                        if (b.soft) g.AddComponent<CollisionTag>().isSoft = true;
                        groups[key] = g;
                    }
                    if (b.rot == Quaternion.identity)
                    {
                        var bc = g.AddComponent<BoxCollider>();
                        bc.center = b.center;
                        bc.size = b.size;
                    }
                    else
                    {
                        var rg = new GameObject("Rot");
                        rg.transform.SetParent(g.transform, false);
                        rg.isStatic = true;
                        rg.transform.localPosition = b.center; rg.transform.localRotation = b.rot;
                        var rbc = rg.AddComponent<BoxCollider>(); rbc.size = b.size;
                        var si2 = rg.AddComponent<SurfaceInfo>(); si2.type = b.surf; si2.grip = b.grip;
                        if (b.soft) rg.AddComponent<CollisionTag>().isSoft = true;
                    }
                }
            }
            return root;
        }
    }
}
