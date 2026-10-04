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
        };

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
            else
            {
                pos = car.vis.body.TransformPoint(m.pos);
                look = car.vis.body.TransformPoint(new Vector3(Mathf.Sin(orbit) * 12f, m.look, Mathf.Cos(orbit) * 12f));
            }
            init = true;
            var p = pos;
            if (shake > 0.001f)
            {
                float s = shake * 0.25f;
                p.x += (Random.value - 0.5f) * s; p.y += (Random.value - 0.5f) * s;
                shake *= Mathf.Exp(-dt * 6f);
            }
            cam.transform.position = p;
            cam.transform.rotation = Quaternion.LookRotation(look - p, Vector3.up);
            float fov = (m.type == 2 ? 68f : m.type == 1 ? 55f : 58f) + (m.type == 0 ? Mathf.Clamp01(car.Speed / 40f) * 10f : 0f);
            cam.fieldOfView = M.Damp(cam.fieldOfView, fov, 3f, dt);
        }
    }
}
