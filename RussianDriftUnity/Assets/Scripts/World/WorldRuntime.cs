using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.World
{
    /// <summary>Realtime reflection probe that follows the camera; refreshed a few times per second.</summary>
    public class ReflectionController : MonoBehaviour
    {
        public Transform follow;
        private ReflectionProbe probe;
        private float timer;

        public static ReflectionController Create(Transform parent)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(parent, false);
            var rc = go.AddComponent<ReflectionController>();
            rc.probe = go.AddComponent<ReflectionProbe>();
            rc.probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            rc.probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            rc.probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            rc.probe.resolution = QualityManager.Current.index >= 2 ? 128 : 64;
            rc.probe.size = new Vector3(260f, 80f, 260f);
            rc.probe.boxProjection = false;
            rc.probe.intensity = 1f;
            rc.probe.nearClipPlane = 1f; rc.probe.farClipPlane = 400f;
            rc.probe.hdr = true;
            rc.enabled = QualityManager.Current.reflectionProbe;
            rc.probe.enabled = QualityManager.Current.reflectionProbe;
            return rc;
        }

        private void Update()
        {
            if (follow == null) return;
            transform.position = follow.position + Vector3.up * 3f;
            timer -= Time.deltaTime;
            if (timer <= 0f) { timer = 1.2f; probe.RenderProbe(); }
        }
    }

    /// <summary>Animates the shared traffic light materials from the synchronised cycle.</summary>
    public class TrafficLightController : MonoBehaviour
    {
        public WorldMaterials mats;
        private static readonly Color Red = new Color(3.2f, 0.12f, 0.08f), Yellow = new Color(3.2f, 2.2f, 0.1f), Green = new Color(0.1f, 3.2f, 0.4f);

        private static Color ColorOf(TrafficLights.Light l) { return l == TrafficLights.Light.Green ? Green : (l == TrafficLights.Light.Yellow ? Yellow : Red); }

        private void Update()
        {
            if (mats == null) return;
            float t = Time.time;
            MatLib.SetColor(mats.lightNSa, ColorOf(TrafficLights.State(0, 0, true, t)));
            MatLib.SetColor(mats.lightEWa, ColorOf(TrafficLights.State(0, 0, false, t)));
            MatLib.SetColor(mats.lightNSb, ColorOf(TrafficLights.State(0, 1, true, t)));
            MatLib.SetColor(mats.lightEWb, ColorOf(TrafficLights.State(0, 1, false, t)));
        }
    }

    /// <summary>A few real point lights hop between the street lamps nearest to the camera at night.</summary>
    public class NearbyLampLights : MonoBehaviour
    {
        public WorldInfo world;
        public Transform follow;
        private Light[] lights;
        private float timer;

        public static NearbyLampLights Create(Transform parent, WorldInfo info)
        {
            var go = new GameObject("LampLights");
            go.transform.SetParent(parent, false);
            var n = go.AddComponent<NearbyLampLights>();
            n.world = info;
            int count = Mathf.Clamp(QualityManager.Current.additionalLights - 1, 0, 5);
            n.lights = new Light[count];
            for (int i = 0; i < count; i++)
            {
                var g = new GameObject("Lamp" + i);
                g.transform.SetParent(go.transform, false);
                var l = g.AddComponent<Light>();
                l.type = LightType.Point; l.range = 22f; l.intensity = 2.2f; l.color = new Color(1f, 0.82f, 0.55f);
                l.shadows = LightShadows.None; l.enabled = false;
                n.lights[i] = l;
            }
            return n;
        }

        private void Update()
        {
            if (lights == null || lights.Length == 0 || world == null) return;
            if (follow == null) { if (Camera.main != null) follow = Camera.main.transform; else return; }
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.4f;
            float night = WorldConditions.NightFactor;
            bool on = night > 0.3f;
            Vector3 p = follow.position;
            // pick nearest lamps (simple selection, lamp count is a few hundred)
            var chosen = new int[lights.Length];
            var dist = new float[lights.Length];
            for (int k = 0; k < lights.Length; k++) { chosen[k] = -1; dist[k] = float.MaxValue; }
            var lamps = world.lamps;
            for (int i = 0; i < lamps.Count; i++)
            {
                float d = (lamps[i] - p).sqrMagnitude;
                for (int k = 0; k < lights.Length; k++)
                {
                    if (d < dist[k])
                    {
                        for (int s = lights.Length - 1; s > k; s--) { dist[s] = dist[s - 1]; chosen[s] = chosen[s - 1]; }
                        dist[k] = d; chosen[k] = i;
                        break;
                    }
                }
            }
            for (int k = 0; k < lights.Length; k++)
            {
                bool has = on && chosen[k] >= 0 && dist[k] < 90f * 90f;
                lights[k].enabled = has;
                if (has) { lights[k].transform.position = lamps[chosen[k]] - Vector3.up * 0.4f; lights[k].intensity = 2.2f * Mathf.Clamp01((night - 0.3f) * 2f); }
            }
        }
    }

    /// <summary>Distance-based renderer culling per chunk; a cheap stand-in for baked occlusion on a procedural city.</summary>
    public class ChunkCuller : MonoBehaviour
    {
        private class Chunk { public Renderer[] renderers; public Bounds bounds; public bool visible = true; public bool always; }
        private readonly List<Chunk> chunks = new List<Chunk>();
        private Transform cam;
        private float timer;
        public float viewDistance = 450f;

        public static ChunkCuller Create(Transform parent, WorldInfo info)
        {
            var go = new GameObject("ChunkCuller");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<ChunkCuller>();
            c.viewDistance = QualityManager.Current.viewDistance;
            foreach (var t in info.chunks)
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                bool always = t.name == "Ground" || t.name == "Roads" || t.name == "StreetFurniture";
                c.chunks.Add(new Chunk { renderers = rs, bounds = b, always = always });
            }
            return c;
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.4f;
            if (cam == null) { var c = Camera.main; if (c == null) return; cam = c.transform; }
            float max2 = viewDistance * viewDistance;
            Vector3 p = cam.position;
            for (int i = 0; i < chunks.Count; i++)
            {
                var ch = chunks[i];
                if (ch.always) continue;
                bool vis = ch.bounds.SqrDistance(p) < max2;
                if (vis == ch.visible) continue;
                ch.visible = vis;
                for (int k = 0; k < ch.renderers.Length; k++) if (ch.renderers[k] != null) ch.renderers[k].enabled = vis;
            }
        }
    }

    /// <summary>Top-down orthographic camera rendered to a RenderTexture a few times per second for the HUD mini-map.</summary>
    public class MiniMapCamera : MonoBehaviour
    {
        public Transform target;
        public RenderTexture texture;
        public float size = 85f;
        private Camera cam;
        private float timer;

        public static MiniMapCamera Create(Transform parent, int resolution)
        {
            var go = new GameObject("MiniMapCamera");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<MiniMapCamera>();
            m.texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);
            m.texture.name = "MiniMapRT";
            m.cam = go.AddComponent<Camera>();
            m.cam.orthographic = true;
            m.cam.orthographicSize = m.size;
            m.cam.targetTexture = m.texture;
            m.cam.clearFlags = CameraClearFlags.SolidColor;
            m.cam.backgroundColor = new Color(0.08f, 0.1f, 0.12f);
            m.cam.nearClipPlane = 1f; m.cam.farClipPlane = 400f;
            m.cam.cullingMask = ~0;
            m.cam.allowHDR = false; m.cam.allowMSAA = false;
            m.cam.enabled = false;
            UrpBridge.SetupCamera(m.cam, false, false);
            return m;
        }

        private void LateUpdate()
        {
            if (target == null || cam == null) return;
            timer -= Time.unscaledDeltaTime;
            Vector3 p = target.position;
            transform.position = new Vector3(p.x, p.y + 150f, p.z);
            transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
            if (timer <= 0f)
            {
                timer = QualityManager.Current.index == 0 ? 0.25f : 0.12f;
                cam.Render();
            }
        }

        private void OnDestroy() { if (texture != null) { texture.Release(); Destroy(texture); } }
    }
}
