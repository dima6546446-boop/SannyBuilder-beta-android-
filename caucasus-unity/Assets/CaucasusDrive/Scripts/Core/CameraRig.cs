using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Камеры: сзади (с инерцией, облётом свайпом и защитой от стен), сзади-далеко, сверху (для парковки)
    /// и с капота. FOV растёт со скоростью, тряска при ударе. Порт CameraRig.js.
    /// </summary>
    public class CameraRig
    {
        struct Mode { public string name; public int type; public float dist, height, look; public Vector3 pos; }
        // type: 0 — сзади, 1 — сверху, 2 — на машине
        static readonly Mode[] Modes = {
            new Mode { name = "Сзади", type = 0, dist = 6.2f, height = 2.1f, look = 1.1f },
            new Mode { name = "Сзади (далеко)", type = 0, dist = 9.5f, height = 3.4f, look = 1.2f },
            new Mode { name = "Сверху", type = 1, dist = 5f, height = 13f, look = 0f },
            new Mode { name = "Капот", type = 2, pos = new Vector3(0, 1.22f, 0.75f), look = 1.0f },
            new Mode { name = "Из салона", type = 3 },
        };
        public bool IsInterior => Modes[mode].type == 3;

        public readonly Camera cam;
        public int mode;
        float yaw, shake;
        bool init;
        Vector3 pos;
        public float orbit;

        public CameraRig(Camera c) { cam = c; }

        public string ModeName => Modes[mode].name;
        public void SetMode(int i) { mode = ((i % Modes.Length) + Modes.Length) % Modes.Length; init = false; }
        public string Next() { SetMode(mode + 1); return ModeName; }
        public void Snap() { init = false; }
        public void AddShake(float v) { shake = Mathf.Min(1f, shake + v); }

        public void Update(float dt, PlayerCar car)
        {
            var m = Modes[mode];
            var t = car.go.transform;
            var basePos = t.position + Vector3.up * car.y;
            float heading = car.Heading;
            Vector3 look;
            if (m.type <= 1)
            {
                if (!init) yaw = heading;
                yaw = M.DampAngle(yaw, heading, m.type == 1 ? 3f : 4.5f, dt);
                float yw = yaw + orbit;
                var f = M.Fwd(yw);
                float dist = m.dist + (m.type == 0 ? Mathf.Clamp01(car.Speed / 30f) * 1.2f : 0f);
                if (m.type == 0)
                {
                    RaycastHit hit;
                    var from = basePos + Vector3.up * 1.2f;
                    if (Physics.SphereCast(from, 0.3f, -f, out hit, dist, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        dist = Mathf.Max(1.5f, hit.distance - 0.3f);
                }
                var target = new Vector3(basePos.x - f.x * dist, basePos.y + m.height, basePos.z - f.z * dist);
                if (!init) pos = target;
                float k = m.type == 1 ? 8f : 12f;
                pos.x = M.Damp(pos.x, target.x, k, dt);
                pos.y = M.Damp(pos.y, target.y, 6f, dt);
                pos.z = M.Damp(pos.z, target.z, k, dt);
                var hf = M.Fwd(heading);
                float ahead = m.type == 1 ? 1.5f : 2.5f;
                look = basePos + hf * ahead + Vector3.up * m.look;
            }
            else if (m.type == 3 && car.interior != null)
            {
                var e = car.interior.eye;
                pos = car.vis.body.TransformPoint(e);
                look = car.vis.body.TransformPoint(e + new Vector3(Mathf.Sin(orbit) * 10f, -0.95f, Mathf.Cos(orbit) * 10f));
            }
            else
            {
                pos = car.vis.body.TransformPoint(m.pos);
                look = car.vis.body.TransformPoint(new Vector3(Mathf.Sin(orbit) * 12f, m.look, Mathf.Cos(orbit) * 12f));
            }
            init = true;
            bool inside = m.type == 3;
            if (car.interior != null && car.interior.FirstPerson != inside) car.interior.SetFirstPerson(inside);
            cam.nearClipPlane = inside ? 0.04f : 0.3f;
            var p = pos;
            if (shake > 0.001f)
            {
                float s = shake * 0.25f;
                p.x += (Random.value - 0.5f) * s; p.y += (Random.value - 0.5f) * s;
                shake *= Mathf.Exp(-dt * 6f);
            }
            cam.transform.position = p;
            cam.transform.rotation = Quaternion.LookRotation(look - p, Vector3.up);
            float fov = (m.type == 3 ? 72f : m.type == 2 ? 68f : m.type == 1 ? 55f : 58f) + (m.type == 0 ? Mathf.Clamp01(car.Speed / 40f) * 10f : 0f);
            cam.fieldOfView = M.Damp(cam.fieldOfView, fov, 3f, dt);
        }
    
        // ------------------------------------------------------------------ пешком
        public float footYaw, footPitch = 0.25f;
        Vector3 footPos;
        bool footInit;

        public void BeginFoot(float heading) { footYaw = heading; footPitch = 0.25f; footInit = false; }

        /// <summary>Камера за плечом пешехода: свайп — облёт и наклон, стены не пускают камеру.</summary>
        public void UpdateFoot(float dt, Walker w, Vector2 look)
        {
            footYaw += look.x * 0.006f;
            footPitch = Mathf.Clamp(footPitch - look.y * 0.004f, -0.35f, 1.1f);
            if (look.sqrMagnitude < 0.01f && w.speed > 0.5f) footYaw = M.DampAngle(footYaw, w.yaw, 1.6f, dt);
            var head = w.Pos + Vector3.up * (w.Sitting ? 1.15f : 1.55f);
            var dir = new Vector3(Mathf.Sin(footYaw) * Mathf.Cos(footPitch), Mathf.Sin(footPitch), Mathf.Cos(footYaw) * Mathf.Cos(footPitch));
            float dist = 3.4f;
            RaycastHit hit;
            if (Physics.SphereCast(head, 0.25f, -dir, out hit, dist, ~((1 << 2) | (1 << Walker.Layer) | (1 << 9)), QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.6f, hit.distance - 0.2f);
            var target = head - dir * dist;
            target.y = Mathf.Max(target.y, 0.3f);
            if (!footInit) { footPos = target; footInit = true; }
            footPos = Vector3.Lerp(footPos, target, 1f - Mathf.Exp(-14f * dt));
            var p = footPos;
            if (shake > 0.001f) { p.y += (Random.value - 0.5f) * shake * 0.2f; shake *= Mathf.Exp(-dt * 6f); }
            cam.transform.position = p;
            cam.transform.rotation = Quaternion.LookRotation(head - p, Vector3.up);
            cam.nearClipPlane = 0.1f;
            cam.fieldOfView = M.Damp(cam.fieldOfView, 60f, 3f, dt);
            init = false;
        }
    }
}
