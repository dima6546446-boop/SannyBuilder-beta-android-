using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    public enum CameraMode { Chase = 0, Cockpit = 1, Hood = 2, Cinematic = 3 }

    /// <summary>Follow camera: chase (dynamic FOV, drift swing), cockpit, bonnet and side cinematic, plus speed lines and shake.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public VehicleController target;
        public CameraMode mode = CameraMode.Chase;
        public float baseFov = 62f;
        public float distance = 5.6f;
        public float height = 1.9f;

        private Camera cam;
        private Vector3 smoothPos, smoothDir = Vector3.forward;
        private float fov = 62f;
        private float shake;
        private float cinAngle;
        private ParticleSystem speedLines;
        private float lookOffset;

        public Camera Cam { get { return cam; } }

        private void Awake() { cam = GetComponent<Camera>(); fov = baseFov; }

        private void OnEnable()
        {
            GameEvents.VehicleCollision += OnCollision;
            EnsureSpeedLines();
        }

        private void OnDisable() { GameEvents.VehicleCollision -= OnCollision; }

        private void OnCollision(float impulse, bool traffic) { shake = Mathf.Max(shake, Mathf.Clamp01(impulse / 20000f)); }

        public void Cycle() { mode = (CameraMode)(((int)mode + 1) % 4); snap = true; }
        public void SetMode(CameraMode m) { mode = m; snap = true; }

        private bool snap = true;

        public void SetTarget(VehicleController t)
        {
            target = t; snap = true;
        }

        private void EnsureSpeedLines()
        {
            if (speedLines != null) return;
            var mat = MatLib.Additive(ProcTex.Spark(), new Color(1, 1, 1, 0.5f));
            speedLines = VehicleEffects.MakePS("SpeedLines", transform, mat, 0.25f, 0.4f, 0.05f, 0.09f, new Color(1, 1, 1, 0.6f), 0f, false, 120);
            speedLines.transform.localPosition = new Vector3(0, 0, 4f);
            speedLines.transform.localRotation = Quaternion.identity;
            var sh = speedLines.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 4.2f; sh.angle = 0f; sh.radiusThickness = 0.15f;
            var main = speedLines.main; main.startSpeed = new ParticleSystem.MinMaxCurve(-38f, -24f);
            var r = speedLines.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 10f; r.velocityScale = 0.1f;
            var sz = speedLines.sizeOverLifetime; sz.enabled = false;
        }

        private void LateUpdate()
        {
            if (target == null || target.Model == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Transform t = target.transform;
            float speed = target.SpeedMs;
            var save = Services.Get<SaveSystem>();
            bool doShake = save == null || save.Data.settings.cameraShake;

            Vector3 pos; Quaternion rot;
            float targetFov = baseFov;

            switch (mode)
            {
                case CameraMode.Cockpit:
                    {
                        var h = target.Model.cockpitCamera;
                        pos = h.position;
                        float yaw = -target.SteerInputSmoothed * 8f;
                        rot = t.rotation * Quaternion.Euler(2f, yaw, 0f);
                        targetFov = 72f + speed * 0.12f;
                        break;
                    }
                case CameraMode.Hood:
                    {
                        var h = target.Model.hoodCamera;
                        pos = h.position;
                        rot = t.rotation * Quaternion.Euler(4f, 0, 0);
                        targetFov = 70f + speed * 0.18f;
                        break;
                    }
                case CameraMode.Cinematic:
                    {
                        cinAngle += dt * 8f;
                        float side = 100f + Mathf.Sin(cinAngle * 0.05f) * 20f;
                        Vector3 flatFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
                        Quaternion q = Quaternion.AngleAxis(side, Vector3.up);
                        Vector3 offset = q * flatFwd * (6.5f + speed * 0.04f) + Vector3.up * 1.4f;
                        Vector3 want = t.position + offset + target.Body.Vel() * 0.05f;
                        smoothPos = snap ? want : Vector3.Lerp(smoothPos, want, 1f - Mathf.Exp(-5f * dt));
                        pos = smoothPos;
                        rot = Quaternion.LookRotation((t.position + Vector3.up * 0.4f - pos).normalized, Vector3.up);
                        targetFov = 48f + speed * 0.1f;
                        break;
                    }
                default:
                    {
                        Vector3 vel = target.Body.Vel();
                        Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
                        Vector3 velDir = Vector3.ProjectOnPlane(vel, Vector3.up);
                        Vector3 dir = fwd;
                        if (velDir.magnitude > 4f && target.ForwardSpeed > 0f)
                            dir = Vector3.Slerp(fwd, velDir.normalized, Mathf.Clamp01((speed - 4f) / 10f) * 0.55f).normalized;
                        if (snap) smoothDir = dir;
                        smoothDir = Vector3.Slerp(smoothDir, dir, 1f - Mathf.Exp(-4.2f * dt));
                        float dist = distance + Mathf.Clamp(speed * 0.02f, 0f, 1.2f);
                        Vector3 focus = t.position + Vector3.up * 0.55f;
                        Vector3 want = focus - smoothDir * dist + Vector3.up * height;
                        // keep the camera out of walls
                        Vector3 toCam = want - focus;
                        RaycastHit hit;
                        if (Physics.SphereCast(focus, 0.25f, toCam.normalized, out hit, toCam.magnitude, ~0, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody != target.Body)
                            want = focus + toCam.normalized * Mathf.Max(1.2f, hit.distance - 0.1f);
                        smoothPos = snap ? want : Vector3.Lerp(smoothPos, want, 1f - Mathf.Exp(-9f * dt));
                        pos = smoothPos;
                        Vector3 look = focus + smoothDir * 3.5f + Vector3.up * 0.3f;
                        rot = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
                        targetFov = baseFov + Mathf.Clamp(speed * 0.55f, 0f, 24f) + target.AbsDriftAngle * 0.08f;
                        break;
                    }
            }

            fov = snap ? targetFov : Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-3f * dt));
            cam.fieldOfView = fov;

            float rumble = doShake ? Mathf.Clamp01((speed - 25f) / 40f) * 0.012f + shake * 0.35f : 0f;
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            Vector3 jitter = rumble > 0f ? new Vector3(Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f, 0f) * rumble : Vector3.zero;
            transform.SetPositionAndRotation(pos + rot * jitter, rot);
            snap = false;

            if (speedLines != null)
            {
                var em = speedLines.emission;
                float k = Mathf.Clamp01((speed * 3.6f - 95f) / 70f);
                em.rateOverTime = k * 70f * QualityManager.Current.particleScale;
            }
        }
    }
}
