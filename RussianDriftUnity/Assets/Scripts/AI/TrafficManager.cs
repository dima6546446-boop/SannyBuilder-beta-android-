using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;
using RussianDrift.Vehicle;
using RussianDrift.World;

namespace RussianDrift.AI
{
    /// <summary>Ambient traffic on the city grid: lane following, traffic lights, car-following, near-miss detection.</summary>
    public class TrafficManager : MonoBehaviour
    {
        public Transform player;
        public VehicleController playerCar;
        public WorldInfo world;
        private readonly List<TrafficCar> cars = new List<TrafficCar>();
        private readonly List<GameObject> templates = new List<GameObject>();
        private int target;
        private System.Random rng;
        private float respawnTimer;

        public static TrafficManager Create(Transform parent, WorldInfo info, VehicleController p, int count)
        {
            var go = new GameObject("Traffic");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TrafficManager>();
            t.world = info; t.playerCar = p; t.player = p.transform;
            t.target = count;
            t.rng = new System.Random(1234);
            t.BuildTemplates();
            for (int i = 0; i < count; i++) t.SpawnCar(true);
            return t;
        }

        private void BuildTemplates()
        {
            GameCatalog.EnsureLoaded();
            string[] ids = { "kopeyka", "pyaterka", "devyatka", "desyatka", "volzhanka", "universal" };
            Color[] cols = { new Color(0.8f, 0.8f, 0.82f), new Color(0.15f, 0.15f, 0.17f), new Color(0.6f, 0.1f, 0.1f), new Color(0.2f, 0.35f, 0.6f), new Color(0.75f, 0.7f, 0.4f), new Color(0.3f, 0.45f, 0.3f) };
            for (int i = 0; i < ids.Length; i++)
            {
                var def = GameCatalog.GetCar(ids[i]);
                var setup = new CarSetup { carId = def.id, colorHex = ColorUtility.ToHtmlStringRGB(cols[i]), plate = "A" + rng.Next(100, 999) + "BC", region = "77" };
                var holder = new GameObject("TrafficTemplate_" + def.id);
                holder.transform.SetParent(transform, false);
                var model = CarBuilder.Build(def, setup, holder.transform, true);
                model.visualRoot.localPosition = new Vector3(0f, def.comHeight, 0f);
                // static traffic visuals do not need lights scripts or steering wheel
                var lights = model.lights; if (lights != null) lights.enabled = true;
                holder.SetActive(false);
                templates.Add(holder);
                holder.GetComponent<Transform>();
                // remember dimensions for the collider
                holder.name = def.id;
                var dims = holder.AddComponent<TemplateInfo>();
                dims.size = new Vector3(def.width, def.height, def.length);
            }
        }

        public class TemplateInfo : MonoBehaviour { public Vector3 size; }

        private void SpawnCar(bool anywhere)
        {
            var graph = world.graph;
            RoadNode a;
            Vector3 center = player != null ? player.position : Vector3.zero;
            int tries = 0;
            do
            {
                a = graph.RandomNode(rng);
                tries++;
            } while (!anywhere && tries < 12 && ((a.pos - center).magnitude < 90f || (a.pos - center).magnitude > 260f));
            if (a == null) return;
            var next = a.neighbors[rng.Next(a.neighbors.Count)];
            var tmpl = templates[rng.Next(templates.Count)];
            var go = Instantiate(tmpl, transform);
            go.SetActive(true);
            go.name = "Traffic";
            var info = tmpl.GetComponent<TemplateInfo>();
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(info.size.x * 0.95f, info.size.y * 0.8f, info.size.z * 0.97f);
            bc.center = new Vector3(0f, info.size.y * 0.42f + 0.1f, 0f);
            go.AddComponent<CollisionTag>().isTraffic = true;
            var car = go.AddComponent<TrafficCar>();
            car.Init(world, rng, a, next, info.size.z);
            cars.Add(car);
        }

        private void Update()
        {
            if (player == null) return;
            respawnTimer -= Time.deltaTime;
            Vector3 pp = player.position;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (c == null) continue;
                Vector3 d = c.transform.position - pp;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > 330f && respawnTimer <= 0f)
                {
                    respawnTimer = 0.4f;
                    // recycle far car to a new place around the player
                    var graph = world.graph;
                    RoadNode a = null;
                    for (int tries = 0; tries < 12; tries++)
                    {
                        var cand = graph.RandomNode(rng);
                        float dd = (cand.pos - pp).magnitude;
                        if (dd > 100f && dd < 260f) { a = cand; break; }
                    }
                    if (a != null) c.Reset(a, a.neighbors[rng.Next(a.neighbors.Count)]);
                }
                // near miss
                if (playerCar != null && dist < 4.2f && playerCar.SpeedMs > 16f && !c.nearMissCooldownActive)
                {
                    c.StartNearMissCooldown();
                    if (!c.recentlyHit) GameEvents.RaiseNearMiss(dist);
                }
            }
        }
    }

    /// <summary>One AI traffic vehicle: kinematic lane follower with light/obstacle awareness.</summary>
    public class TrafficCar : MonoBehaviour
    {
        private WorldInfo world;
        private System.Random rng;
        private RoadNode from, to, prev;
        private float speed;
        private float cruise;
        private float length = 4.2f;
        private Rigidbody rb;
        private Transform[] spinners;
        private float spin;
        private float hitTimer;
        private float nearMissCd;

        public bool nearMissCooldownActive { get { return nearMissCd > 0f; } }
        public bool recentlyHit { get { return hitTimer > 0f; } }
        public void StartNearMissCooldown() { nearMissCd = 3f; }

        public void Init(WorldInfo w, System.Random r, RoadNode a, RoadNode b, float len)
        {
            world = w; rng = r; length = len; rb = GetComponent<Rigidbody>();
            var list = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Spin") list.Add(t);
            spinners = list.ToArray();
            Reset(a, b);
        }

        public void Reset(RoadNode a, RoadNode b)
        {
            prev = null; from = a; to = b; pendingNext = null;
            cruise = 9f + (float)rng.NextDouble() * 6f;
            speed = cruise * 0.7f;
            float t = (float)rng.NextDouble() * 0.6f + 0.1f;
            Vector3 lane = RoadGraph.LaneShift(from.pos, to.pos);
            Vector3 pos = Vector3.Lerp(from.pos, to.pos, t) + lane;
            pos.y = 0.04f;
            Vector3 dir = (to.pos - from.pos).normalized;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
            if (rb != null) { rb.position = pos; rb.rotation = transform.rotation; }
            hitTimer = 0f;
        }

        private void OnCollisionEnter(Collision c) { if (c.rigidbody != null) hitTimer = 4f; }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (nearMissCd > 0f) nearMissCd -= dt;
            if (hitTimer > 0f) { hitTimer -= dt; speed = Mathf.MoveTowards(speed, 0f, 12f * dt); }

            Vector3 pos = rb.position;
            Vector3 laneVec = RoadGraph.LaneShift(from.pos, to.pos);
            Vector3 a = from.pos + laneVec, b = to.pos + laneVec;
            Vector3 segDir = b - a; float segLen = segDir.magnitude; segDir /= Mathf.Max(0.01f, segLen);
            float along = Vector3.Dot(pos - a, segDir);
            float toEnd = segLen - along;

            // ---- desired speed ----
            float vTarget = cruise;
            // traffic light before the node
            Vector3 d3 = to.pos - from.pos;
            bool ns = Mathf.Abs(d3.z) > Mathf.Abs(d3.x);
            if (to.i > 0 && to.i < RoadGraph.N - 1 && to.j > 0 && to.j < RoadGraph.N - 1)
            {
                var st = TrafficLights.State(to.i, to.j, ns, Time.time);
                float stopAt = toEnd - 11f;        // distance to stop line
                if (stopAt > -1.5f && st != TrafficLights.Light.Green)
                {
                    float brakeDist = speed * speed / (2f * 5f) + 3f;
                    if (st == TrafficLights.Light.Red || stopAt > brakeDist * 0.9f) vTarget = Mathf.Min(vTarget, Mathf.Max(0f, (stopAt - 1.5f)) * 0.9f);
                }
            }
            // obstacle ahead
            RaycastHit hit;
            Vector3 origin = pos + Vector3.up * 0.6f + transform.forward * (length * 0.5f);
            if (Physics.SphereCast(origin, 0.7f, transform.forward, out hit, 16f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody != rb)
                    vTarget = Mathf.Min(vTarget, Mathf.Max(0f, (hit.distance - 4.5f)) * 1.1f);
            }
            float acc = vTarget > speed ? 2.6f : 7f;
            speed = Mathf.MoveTowards(speed, vTarget, acc * dt);

            // ---- advance, steer toward a point ahead on the lane ----
            float look = Mathf.Clamp(5f + speed * 0.5f, 5f, 12f);
            Vector3 targetPt;
            if (toEnd > look) targetPt = a + segDir * (along + look);
            else
            {
                // next segment preview
                var next = PeekNext();
                Vector3 lane2 = RoadGraph.LaneShift(to.pos, next.pos);
                float rest = look - toEnd;
                targetPt = to.pos + lane2 + (next.pos - to.pos).normalized * rest;
            }
            targetPt.y = pos.y;
            Vector3 want = targetPt - pos; want.y = 0f;
            Quaternion rot = rb.rotation;
            if (want.sqrMagnitude > 0.01f)
            {
                Quaternion tr = Quaternion.LookRotation(want.normalized, Vector3.up);
                rot = Quaternion.RotateTowards(rot, tr, (50f + speed * 3f) * dt);
            }
            Vector3 fwd = rot * Vector3.forward;
            Vector3 np = pos + fwd * (speed * dt);
            np.y = 0.04f;
            rb.MovePosition(np);
            rb.MoveRotation(rot);
            spin += speed / 0.3f * Mathf.Rad2Deg * dt;
            if (spinners != null) for (int i = 0; i < spinners.Length; i++) if (spinners[i] != null) spinners[i].localRotation = Quaternion.Euler(spin, 0f, 0f);

            // node reached?
            if (Vector3.Dot(np - to.pos, segDir) > -2.5f)
            {
                var next = PeekNext();
                prev = from; from = to; to = next;
                pendingNext = null;
            }
        }

        private RoadNode pendingNext;
        private RoadNode PeekNext()
        {
            if (pendingNext == null) pendingNext = world.graph.PickNext(from, to, rng);
            return pendingNext;
        }
    }
}
