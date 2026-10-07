using UnityEngine;
using UnityEngine.EventSystems;
using RussianDrift.Core;
using RussianDrift.Vehicle;
using RussianDrift.World;

namespace RussianDrift.UI
{
    /// <summary>Procedural studio used by the main menu and garage: turntable, light strips, orbit camera, car + driver preview.</summary>
    public class Showroom : MonoBehaviour
    {
        public Camera cam;
        public CarModel car;
        public Humanoid driver;
        public Transform turntable;
        public float yaw = 35f, pitch = 12f, distance = 6.8f;
        public bool autoRotate = true;
        public Vector3 focus = new Vector3(0f, 0.75f, 0f);

        private float yawVel, idleTime;
        private float targetDistance;
        private ReflectionProbe probe;
        private PostFxController post;
        private Transform carRoot;
        private float carYaw;
        private bool garageMode;

        public static Showroom Create(bool garage)
        {
            var go = new GameObject("Showroom");
            var s = go.AddComponent<Showroom>();
            s.garageMode = garage;
            s.Build();
            return s;
        }

        private void Build()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.24f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

            var camGo = new GameObject("ShowroomCamera");
            camGo.transform.SetParent(transform, false);
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.04f);
            cam.fieldOfView = 36f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 80f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            UrpBridge.SetupCamera(cam, true, true);
            post = PostFxController.Create(transform);

            // floor + rim
            var mb = new MeshBuilder();
            mb.AddCylinder(new Vector3(0, -0.06f, 0), 5.5f, 0.12f, Quaternion.identity, 48, true);
            var floor = MeshUtil.Make("Floor", transform, mb.ToMesh("Floor"), MatLib.Lit(new Color(0.05f, 0.055f, 0.07f), 0.88f, 0.1f), false, true);
            var rim = new MeshBuilder();
            var rimProf = new[] { new Vector2(5.5f, -0.1f), new Vector2(5.62f, -0.1f), new Vector2(5.62f, 0.01f), new Vector2(5.5f, 0.01f) };
            rim.AddLathe(rimProf, 64, Vector3.zero, Quaternion.identity);
            MeshUtil.Make("Rim", transform, rim.ToMesh("Rim"), MatLib.Unlit(new Color(0.2f, 1.2f, 2.6f)), false, false);

            // curved backdrop strips (reflect in the paint)
            var strips = new MeshBuilder();
            for (int i = 0; i < 9; i++)
            {
                float a = (-70f + i * 17.5f) * Mathf.Deg2Rad + Mathf.PI;
                Vector3 p = new Vector3(Mathf.Sin(a) * 9f, 3.5f, Mathf.Cos(a) * 9f);
                strips.AddBox(p, new Vector3(0.35f, 5f + (i % 2) * 1.5f, 0.35f), Quaternion.Euler(0, a * Mathf.Rad2Deg, 0), Vector2.zero);
            }
            MeshUtil.Make("Strips", transform, strips.ToMesh("Strips"), MatLib.Unlit(new Color(3f, 3f, 3.4f)), false, false);
            var top = new MeshBuilder();
            top.AddBox(new Vector3(0, 7f, 0), new Vector3(7f, 0.2f, 1.2f));
            MeshUtil.Make("TopLight", transform, top.ToMesh("TopLight"), MatLib.Unlit(new Color(3.4f, 3.2f, 3f)), false, false);

            // lights
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional; key.intensity = 1.25f; key.color = new Color(1f, 0.96f, 0.9f);
            key.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            key.shadows = LightShadows.Soft;
            AddSpot(new Vector3(-5f, 4f, 3f), new Color(1f, 0.35f, 0.1f), 55f);
            AddSpot(new Vector3(5f, 3.5f, -3f), new Color(0.1f, 0.6f, 1f), 55f);

            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(transform, false);

            // static reflection from the studio
            var pg = new GameObject("StudioProbe");
            pg.transform.SetParent(transform, false);
            pg.transform.position = new Vector3(0, 1.2f, 0);
            probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128; probe.size = new Vector3(20, 10, 20); probe.hdr = true;
            probe.nearClipPlane = 0.3f; probe.farClipPlane = 30f;
            targetDistance = distance;
        }

        private void AddSpot(Vector3 pos, Color col, float angle)
        {
            var go = new GameObject("Spot");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.LookAt(new Vector3(0, 0.6f, 0));
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.color = col; l.intensity = 22f; l.range = 20f; l.spotAngle = angle; l.shadows = LightShadows.None;
        }

        public void SetCar(CarDefinition def, CarSetup setup)
        {
            if (car != null) Destroy(car.gameObject);
            if (carRoot != null) Destroy(carRoot.gameObject);
            carRoot = new GameObject("CarRoot").transform;
            carRoot.SetParent(turntable, false);
            car = VehicleFactory.CreateShowcase(def, setup, carRoot);
            // wheels default to a straight stance with slight steering to show the rim face
            if (car.lights != null) car.lights.headlightsForced = true;
            focus = new Vector3(0f, def.height * 0.5f, 0f);
            targetDistance = Mathf.Max(5.6f, def.length * 1.55f);
            probe.RenderProbe();
        }

        public void SetDriver(DriverLook look, Vector3 pos)
        {
            if (driver == null)
            {
                driver = Humanoid.Build(turntable, look, null, false);
                driver.pose = PoseKind.Idle;
            }
            else driver.Rebuild(look);
            driver.transform.localPosition = pos;
            driver.transform.localRotation = Quaternion.Euler(0, 160f, 0);
        }

        public void HideDriver() { if (driver != null) driver.gameObject.SetActive(false); }
        public void ShowDriver() { if (driver != null) driver.gameObject.SetActive(true); }

        public void FocusDriver(bool on)
        {
            if (on && driver != null) { focus = driver.transform.position + Vector3.up * 1.0f; targetDistance = 3.2f; }
            else if (car != null) { focus = new Vector3(0f, car.height * 0.5f, 0f); targetDistance = Mathf.Max(5.6f, car.length * 1.55f); }
        }

        public void Orbit(Vector2 delta)
        {
            yawVel = -delta.x * 0.35f;
            yaw += yawVel;
            pitch = Mathf.Clamp(pitch - delta.y * 0.18f, 2f, 55f);
            idleTime = 0f;
        }

        public void Zoom(float d) { targetDistance = Mathf.Clamp(targetDistance + d, 3f, 11f); }

        private void LateUpdate()
        {
            idleTime += Time.unscaledDeltaTime;
            if (autoRotate && idleTime > 2.5f) yaw += Time.unscaledDeltaTime * 9f;
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            Quaternion r = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = focus - r * Vector3.forward * distance;
            cam.transform.SetPositionAndRotation(pos, r);
        }
    }

    /// <summary>Invisible full-screen drag area that orbits the showroom camera (placed behind all UI).</summary>
    public class OrbitPad : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public Showroom showroom;
        public void OnDrag(PointerEventData e) { if (showroom != null) showroom.Orbit(e.delta); }
        public void OnScroll(PointerEventData e) { if (showroom != null) showroom.Zoom(-e.scrollDelta.y * 0.4f); }
    }
}
