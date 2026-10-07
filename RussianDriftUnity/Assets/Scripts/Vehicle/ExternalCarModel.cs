using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>
    /// Adapter that turns an imported car prefab into a CarModel. Expected hierarchy (names are case-insensitive, matched by "contains"):
    ///   wheels: "wheel_fl" "wheel_fr" "wheel_rl" "wheel_rr"  (or fl/fr/rl/rr in the name)
    ///   optional: "bumper_f", "bumper_r", "hood", "mirror_l", "mirror_r", "spoiler" (detach on hard hits)
    ///   paintable renderers: any material whose name contains "paint" or "body"
    /// Prefab convention: metres, +Z forward, origin at ground level between the wheels.
    /// </summary>
    public static class ExternalCarModel
    {
        public static CarModel Build(CarDefinition def, CarSetup setup, Transform parent)
        {
            if (def.modelPrefab == null) return null;
            var root = new GameObject("CarVisual");
            root.transform.SetParent(parent, false);
            var model = root.AddComponent<CarModel>();
            model.visualRoot = root.transform;
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            model.bodyRoot = body;
            model.length = def.length; model.width = def.width; model.height = def.height;
            model.groundOffset = -def.comHeight + def.rideHeight;

            var inst = Object.Instantiate(def.modelPrefab, body);
            inst.transform.localPosition = new Vector3(0f, -def.comHeight, 0f);
            inst.transform.localRotation = Quaternion.identity;

            // wheels
            string[] keys = { "fl", "fr", "rl", "rr" };
            var visuals = new List<WheelVisual>();
            var all = inst.GetComponentsInChildren<Transform>(true);
            foreach (var k in keys)
            {
                Transform w = null;
                foreach (var t in all)
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("wheel") && (n.Contains("_" + k) || n.EndsWith(k) || n.Contains(k + "_"))) { w = t; break; }
                }
                if (w == null) continue;
                bool front = k[0] == 'f', right = k[1] == 'r';
                Vector3 p = body.InverseTransformPoint(w.position);
                var steer = new GameObject(k.ToUpper() + "_Steer").transform;
                steer.SetParent(body, false);
                steer.localPosition = p;
                var spin = new GameObject("Spin").transform;
                spin.SetParent(steer, false);
                w.SetParent(spin, true);
                w.localPosition = Vector3.zero;
                visuals.Add(new WheelVisual { steerPivot = steer, spinPivot = spin, front = front, right = right, x = p.x, z = p.z });
            }
            if (visuals.Count != 4)
            {
                Debug.LogWarning("[RussianDrift] External model '" + def.modelPrefab.name + "' needs 4 wheel transforms (wheel_FL/FR/RL/RR); falling back to the procedural car.");
                Object.Destroy(root);
                return null;
            }
            model.wheels = visuals.ToArray();

            // paint
            Color paint = VehicleStats.ParseColor(setup.colorHex, def.defaultColor);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                model.allRenderers.Add(r);
                foreach (var m in r.materials)
                {
                    string n = m.name.ToLowerInvariant();
                    if (n.Contains("paint") || n.Contains("body")) { MatLib.SetColor(m, paint); model.paintMaterial = m; }
                }
            }

            // detachable parts by name
            var det = new List<Transform>();
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("bumper") || n.Contains("hood") || n.Contains("mirror") || n.Contains("spoiler")) det.Add(t);
            }
            model.detachables = det.ToArray();

            // camera anchors & lights helper
            var cp = new GameObject("CockpitCam").transform; cp.SetParent(body, false);
            cp.localPosition = new Vector3(-def.width * 0.17f, def.height * 0.78f - def.comHeight, 0.1f);
            model.cockpitCamera = cp;
            var hc = new GameObject("HoodCam").transform; hc.SetParent(body, false);
            hc.localPosition = new Vector3(0f, def.height * 0.62f - def.comHeight + 0.2f, def.length * 0.28f);
            model.hoodCamera = hc;
            var ex = new GameObject("Exhaust").transform; ex.SetParent(body, false);
            ex.localPosition = new Vector3(-def.width * 0.3f, -def.comHeight + 0.25f, -def.length * 0.5f);
            model.exhaust = ex;
            model.lights = root.AddComponent<CarLights>();     // no emissive meshes: lights script only drives the spot lights
            var spots = new List<Light>();
            for (int s = -1; s <= 1; s += 2)
            {
                var lg = new GameObject("HeadSpot"); lg.transform.SetParent(body, false);
                lg.transform.localPosition = new Vector3(s * def.width * 0.3f, -def.comHeight + 0.65f, def.length * 0.5f);
                var l = lg.AddComponent<Light>(); l.type = LightType.Spot; l.range = 38f; l.spotAngle = 62f; l.intensity = 2.4f; l.enabled = false; l.shadows = LightShadows.None;
                spots.Add(l);
            }
            model.lights.headLights = spots.ToArray();
            return model;
        }
    }
}
