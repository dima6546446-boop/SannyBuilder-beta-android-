using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;
using RussianDrift.Vehicle;

namespace RussianDrift.World
{
    /// <summary>NPC spectators standing along the track; they clap, cheer and jump when the player pulls off good drifts nearby.</summary>
    public class SpectatorCrowd : MonoBehaviour
    {
        private class Member { public Humanoid h; public Vector3 pos; public float delay; public bool near; }
        private readonly List<Member> members = new List<Member>();
        private VehicleController target;
        private float excitement;
        private float timer;
        private Transform cam;

        public static SpectatorCrowd Create(Transform parent, List<Vector3> spots, TrackPath path, int maxCount, VehicleController player)
        {
            var go = new GameObject("Spectators");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<SpectatorCrowd>();
            c.target = player;
            var rng = new System.Random(11);
            int made = 0;
            for (int i = 0; i < spots.Count && made < maxCount; i++)
            {
                int group = 1 + rng.Next(2);
                for (int g = 0; g < group && made < maxCount; g++)
                {
                    Vector3 p = spots[i] + new Vector3((float)rng.NextDouble() * 3f - 1.5f, 0f, (float)rng.NextDouble() * 3f - 1.5f);
                    // snap to the ground (spectator spots can be on slabs / hill)
                    RaycastHit hit;
                    if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
                    var h = Humanoid.Build(go.transform, new DriverLook(), rng, true);
                    h.transform.position = p;
                    Vector3 look = path != null ? path.Point(path.Nearest(p)) : p + Vector3.forward;
                    Vector3 d = look - p; d.y = 0f;
                    if (d.sqrMagnitude > 0.1f) h.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                    h.pose = PoseKind.Idle;
                    h.speed = 0.8f + (float)rng.NextDouble() * 0.5f;
                    c.members.Add(new Member { h = h, pos = p, delay = (float)rng.NextDouble() * 0.6f });
                    made++;
                }
            }
            GameEvents.DriftBanked += c.OnBanked;
            GameEvents.CrowdExcitement += c.OnExcite;
            GameEvents.VehicleCollision += c.OnCrash;
            return c;
        }

        private void OnDestroy()
        {
            GameEvents.DriftBanked -= OnBanked;
            GameEvents.CrowdExcitement -= OnExcite;
            GameEvents.VehicleCollision -= OnCrash;
        }

        private void OnBanked(int pts, float mul, float dur) { excitement = Mathf.Max(excitement, Mathf.Clamp01(pts / 3000f + 0.45f)); }
        private void OnExcite(float e) { excitement = Mathf.Max(excitement, e); }
        private void OnCrash(float impulse, bool traffic) { excitement = Mathf.Max(excitement, 0.5f); }

        private void Update()
        {
            if (cam == null) { if (Camera.main != null) cam = Camera.main.transform; }
            float dt = Time.deltaTime;
            excitement = Mathf.MoveTowards(excitement, 0f, dt * 0.25f);
            timer -= dt;
            if (timer > 0f) return;
            timer = 0.25f;
            Vector3 tp = target != null ? target.transform.position : Vector3.zero;
            bool live = target != null && target.AbsDriftAngle > 15f && target.SpeedMs > 8f;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                float dCam = cam != null ? (m.pos - cam.position).sqrMagnitude : 0f;
                bool visible = dCam < 160f * 160f;
                m.h.gameObject.SetActive(visible);
                if (!visible) continue;
                float dp = target != null ? Vector3.Distance(m.pos, tp) : 999f;
                float e = excitement;
                if (live && dp < 38f) e = Mathf.Max(e, Mathf.Clamp01(target.AbsDriftAngle / 55f) * (1f - dp / 38f) + 0.35f);
                if (e > 0.7f) m.h.pose = PoseKind.Cheer;
                else if (e > 0.3f) m.h.pose = (i & 1) == 0 ? PoseKind.Clap : PoseKind.Cheer;
                else m.h.pose = PoseKind.Idle;
                if (dp < 30f && target != null)
                {
                    Vector3 d = tp - m.pos; d.y = 0f;
                    if (d.sqrMagnitude > 1f) m.h.transform.rotation = Quaternion.Slerp(m.h.transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 0.5f);
                }
            }
        }
    }

    /// <summary>The player's avatar: walks to the car, gets in (and out again after the run, celebrating).</summary>
    public class DriverCharacter : MonoBehaviour
    {
        public Humanoid body;
        private VehicleController car;

        public static DriverCharacter Create(Transform parent, DriverLook look)
        {
            var go = new GameObject("Driver");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DriverCharacter>();
            d.body = Humanoid.Build(go.transform, look, null, false);
            return d;
        }

        private Vector3 DoorPoint(VehicleController c, float outward)
        {
            var def = c.Stats.def;
            Vector3 local = new Vector3(-(def.width * 0.5f + outward), -def.comHeight, 0.15f);
            return c.transform.TransformPoint(local);
        }

        /// <summary>Plays the "walk up and sit down" intro; the car stays frozen meanwhile.</summary>
        public System.Collections.IEnumerator EnterCar(VehicleController c)
        {
            car = c;
            body.gameObject.SetActive(true);
            Vector3 start = DoorPoint(c, 4.5f);
            Vector3 end = DoorPoint(c, 0.55f);
            body.transform.position = start;
            body.pose = PoseKind.Walk; body.speed = 1f;
            float t = 0f, dur = 2.2f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Vector3 p = Vector3.Lerp(DoorPoint(c, 4.5f), DoorPoint(c, 0.55f), Mathf.SmoothStep(0f, 1f, t / dur));
                body.transform.position = p;
                Vector3 dir = (DoorPoint(c, 0.55f) - p); dir.y = 0f;
                Vector3 face = c.transform.right; face.y = 0f;
                body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 0.2f);
                yield return null;
            }
            body.pose = PoseKind.Sit;
            yield return new WaitForSeconds(0.35f);
            body.gameObject.SetActive(false);
        }

        public System.Collections.IEnumerator ExitCar(VehicleController c, float celebrateTime)
        {
            car = c;
            body.gameObject.SetActive(true);
            body.transform.position = DoorPoint(c, 0.6f);
            body.transform.rotation = Quaternion.LookRotation(-c.transform.right, Vector3.up);
            body.pose = PoseKind.Walk;
            float t = 0f, dur = 1.8f;
            while (t < dur)
            {
                t += Time.deltaTime;
                body.transform.position = Vector3.Lerp(DoorPoint(c, 0.6f), DoorPoint(c, 3.2f), Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }
            Vector3 f = c.transform.position - body.transform.position; f.y = 0f;
            body.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            body.pose = PoseKind.Celebrate;
            yield return new WaitForSeconds(celebrateTime);
        }
    }
}
