using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>Камера: плавное преследование с наклоном «в сторону движения» (виден угол заноса), 6 режимов. Порт rcd/src/render/camera.js.</summary>
    public sealed class CamRig
    {
        public static readonly string[] Names = { "Сзади", "Далеко", "Капот", "Бампер", "Сверху", "ТВ" };
        public int Mode; public float BaseFov = 70; public bool SpeedFx = true;
        readonly Camera cam; float angle, fov = 70, shakeT; Vector3 pos, look, tvPos; bool init, tvInit;

        public CamRig(Camera c) { cam = c; }
        public void SetMode(int m) { Mode = ((m % Names.Length) + Names.Length) % Names.Length; init = false; tvInit = false; }
        public void Next() { SetMode(Mode + 1); }
        public void Snap() { init = false; }

        static float AngDiff(float a, float b) { float d = b - a; while (d > Mathf.PI) d -= Mathf.PI * 2; while (d < -Mathf.PI) d += Mathf.PI * 2; return d; }

        public void Update(float dt, Car car, float shake)
        {
            var P = new Vector3((float)car.X, 0, (float)car.Z);
            float fwdAng = Mathf.Atan2(Mathf.Sin((float)car.H), Mathf.Cos((float)car.H));        // направление носа: (sin h, cos h)
            float speed = (float)car.Speed, sf = Mathf.Clamp01(speed / 40f);
            float desiredFov = BaseFov + (SpeedFx ? sf * 13f : 0);
            float velAng = fwdAng; if (speed > 4 && car.FwdSpeed > 0) velAng = Mathf.Atan2((float)car.Vx, (float)car.Vz);
            if (!init) { angle = fwdAng; init = true; pos = P; look = P; }
            float k = 1 - Mathf.Exp(-dt * 8); shakeT += dt;
            float sh = shake * 0.25f + sf * 0.012f; float shx = (Mathf.Sin(shakeT * 53) + Mathf.Sin(shakeT * 31)) * sh, shy = Mathf.Sin(shakeT * 47) * sh;
            var tr = cam.transform;
            if (Mode == 0 || Mode == 1)
            {
                bool far = Mode == 1; float blend = Mathf.Clamp01((speed - 5) / 14f) * 0.6f;
                float target = fwdAng + AngDiff(fwdAng, velAng) * blend;
                angle += AngDiff(angle, target) * (1 - Mathf.Exp(-dt * (far ? 2.6f : 3.4f)));
                float dist = (far ? 10.5f : 6f) + sf * (far ? 3f : 1.6f), height = far ? 4.2f : 2f + sf * 0.3f;
                float dx = Mathf.Sin(angle), dz = Mathf.Cos(angle);
                pos = Vector3.Lerp(pos, new Vector3(P.x - dx * dist, height, P.z - dz * dist), k);
                look = Vector3.Lerp(look, new Vector3(P.x + dx * (far ? 3 : 4.5f), 0.9f, P.z + dz * (far ? 3 : 4.5f)), 1 - Mathf.Exp(-dt * 12));
                tr.position = pos + new Vector3(shx, shy, 0); tr.LookAt(look);
            }
            else if (Mode == 2 || Mode == 3)
            {
                bool hood = Mode == 2; Vector3 f = new Vector3(Mathf.Sin((float)car.H), 0, Mathf.Cos((float)car.H)); float off = hood ? 0.4f : 2.0f, h = hood ? 1.12f : 0.55f;
                tr.position = P + f * off + new Vector3(shx, h + shy, 0); tr.LookAt(P + f * 30 + Vector3.up * (h - 0.1f));
                tr.Rotate(0, 0, (float)car.Roll * 0.8f * Mathf.Rad2Deg, Space.Self); desiredFov = BaseFov + 6 + (SpeedFx ? sf * 14f : 0);
            }
            else if (Mode == 4)
            {
                var want = new Vector3(P.x + (float)car.Vx * 0.35f, 34 + sf * 22, P.z + (float)car.Vz * 0.35f);
                pos = Vector3.Lerp(pos, want, k * 0.7f); tr.position = pos; tr.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); desiredFov = 55;
            }
            else
            {
                float d = (tvPos - P).magnitude;
                if (!tvInit || d > 55 || d < 4)
                {
                    Vector3 dir = new Vector3(Mathf.Sin(velAng), 0, Mathf.Cos(velAng)); Vector3 side = new Vector3(dir.z, 0, -dir.x);
                    tvPos = new Vector3(P.x + dir.x * 30 + side.x * 13, 3.2f, P.z + dir.z * 30 + side.z * 13); tvInit = true;
                }
                tr.position = tvPos; look = Vector3.Lerp(look, P + Vector3.up * 0.8f, 1 - Mathf.Exp(-dt * 7)); tr.LookAt(look);
                desiredFov = Mathf.Clamp(52 - (tvPos - P).magnitude * 0.2f, 24, 55);
            }
            fov += (desiredFov - fov) * (1 - Mathf.Exp(-dt * 5)); cam.fieldOfView = fov;
        }
    }
}
