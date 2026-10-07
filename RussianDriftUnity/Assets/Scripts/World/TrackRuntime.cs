using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;
using RussianDrift.Vehicle;

namespace RussianDrift.World
{
    /// <summary>Tracks monotonic progress of an object along a TrackPath (laps, wrong way). Robust against shortcuts and teleports.</summary>
    public class PathProgress
    {
        public TrackPath path;
        public int index;
        public int laps;
        public float total;          // metres along the route (can start negative before the line)
        public float lateral;        // signed distance from centre-line
        public float maxTotal;
        private float lastD;

        public PathProgress(TrackPath p) { path = p; }

        public void ResetAt(Vector3 pos, bool beforeLine)
        {
            index = path.Nearest(pos);
            lastD = path.DistanceAt(index);
            laps = (path.closed && beforeLine) ? -1 : 0;
            total = laps * path.length + lastD;
            maxTotal = total;
        }

        public void Update(Vector3 pos)
        {
            int idx = path.Nearest(pos, index, 30);
            float d = path.DistanceAt(idx);
            float delta = d - lastD;
            if (path.closed)
            {
                if (delta < -path.length * 0.5f) laps++;
                else if (delta > path.length * 0.5f) laps--;
            }
            float newTotal = laps * path.length + d;
            // ignore implausible jumps (shortcut across the infield)
            if (Mathf.Abs(newTotal - total) < 90f) { total = newTotal; index = idx; lastD = d; }
            else { laps = Mathf.RoundToInt((total - lastD) / Mathf.Max(1f, path.length)); }
            maxTotal = Mathf.Max(maxTotal, total);
            Vector3 c = path.Point(index), t = path.Tangent(index);
            lateral = Vector3.Dot(pos - c, Vector3.Cross(Vector3.up, t));
        }

        public Vector3 Tangent { get { return path.Tangent(index); } }
    }

    /// <summary>Trigger volume that multiplies drift points while a car is inside.</summary>
    public class DriftZone : MonoBehaviour
    {
        public float multiplier = 2f;

        private static DriftScorer ScorerOf(Collider c)
        {
            var rb = c.attachedRigidbody;
            return rb != null ? rb.GetComponent<DriftScorer>() : null;
        }

        private void OnTriggerEnter(Collider c)
        {
            var s = ScorerOf(c);
            if (s != null) { s.zoneDepth++; s.zoneMult = Mathf.Max(s.zoneMult, multiplier); }
        }

        private void OnTriggerExit(Collider c)
        {
            var s = ScorerOf(c);
            if (s != null) { s.zoneDepth = Mathf.Max(0, s.zoneDepth - 1); if (s.zoneDepth == 0) s.zoneMult = 1f; }
        }

        public static List<GameObject> Create(List<DriftZoneDef> defs, Transform parent)
        {
            var list = new List<GameObject>();
            var glow = MatLib.Additive(ProcTex.Glow(), new Color(1f, 0.55f, 0.1f, 0.18f));
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                var go = new GameObject("DriftZone_" + i);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(d.center, d.rot);
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true; bc.size = d.size;
                var z = go.AddComponent<DriftZone>(); z.multiplier = d.multiplier;
                var mb = new MeshBuilder();
                mb.AddFace(new Vector3(0, -d.center.y + 0.075f, 0), Vector3.up, Vector3.right, d.size.x, d.size.z, Vector2.zero);
                var vis = new GameObject("Glow");
                vis.transform.SetParent(go.transform, false);
                vis.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("zone");
                var mr = vis.AddComponent<MeshRenderer>();
                mr.sharedMaterial = glow; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                list.Add(go);
            }
            return list;
        }
    }

    /// <summary>Visual checkpoint gates placed along a path; one can be highlighted as the next target.</summary>
    public class CheckpointGates : MonoBehaviour
    {
        public readonly List<Transform> gates = new List<Transform>();
        public readonly List<float> distances = new List<float>();
        private Material dim, lit;
        private int active = -1;
        private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();

        public static CheckpointGates Create(TrackPath path, int count, Transform parent, bool includeStart)
        {
            var go = new GameObject("Gates");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<CheckpointGates>();
            g.dim = MatLib.Unlit(new Color(0.9f, 0.5f, 0.1f));
            g.lit = MatLib.Unlit(new Color(0.2f, 3.0f, 0.6f));
            for (int i = 0; i < count; i++)
            {
                float d = path.length * i / count;
                if (i == 0 && !includeStart) continue;
                Vector3 t;
                Vector3 p = path.Sample(d, out t);
                var gt = new GameObject("Gate_" + i).transform;
                gt.SetParent(go.transform, false);
                gt.position = p; gt.rotation = Quaternion.LookRotation(t, Vector3.up);
                float hw = path.width * 0.5f + 1.2f;
                var mb = new MeshBuilder();
                mb.AddBox(new Vector3(-hw, 3f, 0), new Vector3(0.45f, 6f, 0.45f));
                mb.AddBox(new Vector3(hw, 3f, 0), new Vector3(0.45f, 6f, 0.45f));
                mb.AddBox(new Vector3(0, 6f, 0), new Vector3(hw * 2f + 0.45f, 0.5f, 0.45f));
                var m = new GameObject("Frame");
                m.transform.SetParent(gt, false);
                m.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("gate");
                var mr = m.AddComponent<MeshRenderer>(); mr.sharedMaterial = g.dim; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.renderers.Add(mr);
                g.gates.Add(gt); g.distances.Add(d);
            }
            return g;
        }

        public void SetActive(int i)
        {
            active = i;
            for (int k = 0; k < renderers.Count; k++) renderers[k].sharedMaterial = k == i ? lit : dim;
        }
    }
}
