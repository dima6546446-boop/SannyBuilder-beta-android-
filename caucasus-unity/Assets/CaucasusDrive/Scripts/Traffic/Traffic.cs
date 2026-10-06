using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Положение игрока для ИИ (машина или пешеход).</summary>
    public class PlayerProbe { public float x, z, heading, speed; }
    public class PedProbe { public bool active; public float x, z; }

    /// <summary>
    /// Машина трафика (порт TrafficCar.js, координаты Unity): движение по полосе, повороты по кривой Безье,
    /// IDM-следование за лидером, светофоры, уступание встречным при левом повороте, перестроения,
    /// объезд стоящих, гудок игроку. Рисуется LODGroup с «плоской» моделью (1 материал).
    /// </summary>
    public class TrafficCar
    {
        const float CarLen = 4.2f, HalfLen = 2.1f;
        static readonly float[] TurnSpeed = { 0f, 5.5f, 7.5f, 4f };
        const float IdmA = 1.8f, IdmB = 3f, IdmS0 = 2.2f, IdmT = 1.3f;

        public readonly int id;
        public bool active;
        public float x, z, heading, vx, vz, speed;
        public CarDef def;
        public int color;
        public char ind = ' ';      // ' ', 'L', 'R', 'H'
        public bool braking, waitingLight;
        public GameObject go;
        public Rigidbody rb;

        TrafficManager ctx;
        int a, b, c, lane, nextLane;
        bool turnMode, laneChange;
        float dx, dz, rx, rz, edgeLen, sx, sz, lat, latTarget, s, cruise, blocked, stun, ghost, honkCd, turnLen;
        int axis;
        Turn turn;
        int wantLane;
        readonly float[] bz = new float[6];

        public TrafficCar(int id) { this.id = id; }

        public void Spawn(TrafficManager m, int a, int b, int lane, float s0, float cruise, CarDef def, int color)
        {
            ctx = m; active = true;
            this.cruise = cruise; this.def = def; this.color = color;
            ind = ' '; speed = cruise * 0.7f; blocked = 0; stun = 0; ghost = 0;
            honkCd = 2f + m.rnd.Next() * 3f;
            laneChange = false;
            EnterEdge(a, b, lane);
            s = s0;
            Pose();
        }

        public void Bump(float impact) { stun = 1.5f + Mathf.Min(impact, 10f) * 0.2f; speed = 0f; }

        void EnterEdge(int na, int nb, int ln)
        {
            var g = ctx.graph;
            turnMode = false; a = na; b = nb; lane = ln;
            var A = g.nodes[a]; var B = g.nodes[b];
            var d = g.Dir(a, b);
            dx = d.x; dz = d.y;
            rx = dz; rz = -dx; // «вправо» в Unity
            edgeLen = Mathf.Sqrt((B.x - A.x) * (B.x - A.x) + (B.z - A.z) * (B.z - A.z)) - 2f * CityC.HALF;
            sx = A.x + dx * CityC.HALF; sz = A.z + dz * CityC.HALF;
            axis = Mathf.Abs(dz) > 0.5f ? 0 : 1;
            lat = CityC.LANES[lane]; latTarget = lat; laneChange = false;
            c = g.PickNext(b, a, ctx.rnd);
            turn = g.TurnType(a, b, c);
            wantLane = turn == Turn.Right ? 1 : turn == Turn.Left || turn == Turn.UTurn ? 0 : lane;
            s = 0f;
        }

        void EnterTurn()
        {
            var g = ctx.graph;
            var B = g.nodes[b];
            var d2 = g.Dir(b, c);
            float r2x = d2.y, r2z = -d2.x;
            nextLane = turn == Turn.Right ? 1 : turn == Turn.Left || turn == Turn.UTurn ? 0 : lane;
            float p0x = sx + dx * edgeLen + rx * lat, p0z = sz + dz * edgeLen + rz * lat;
            float p2x = B.x + d2.x * CityC.HALF + r2x * CityC.LANES[nextLane], p2z = B.z + d2.y * CityC.HALF + r2z * CityC.LANES[nextLane];
            float p1x, p1z;
            if (turn == Turn.Straight || turn == Turn.UTurn)
            {
                p1x = (p0x + p2x) / 2f; p1z = (p0z + p2z) / 2f;
                if (turn == Turn.UTurn) { p1x = B.x; p1z = B.z; }
            }
            else
            {
                float t = (p2x - p0x) * dx + (p2z - p0z) * dz;
                p1x = p0x + dx * t; p1z = p0z + dz * t;
            }
            bz[0] = p0x; bz[1] = p0z; bz[2] = p1x; bz[3] = p1z; bz[4] = p2x; bz[5] = p2z;
            float len = 0f, px = p0x, pz = p0z;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f, u = 1f - t;
                float qx = u * u * p0x + 2 * u * t * p1x + t * t * p2x, qz = u * u * p0z + 2 * u * t * p1z + t * t * p2z;
                len += Mathf.Sqrt((qx - px) * (qx - px) + (qz - pz) * (qz - pz)); px = qx; pz = qz;
            }
            turnLen = len;
            turnMode = true;
        }

        bool LaneFree(float deltaLat)
        {
            float fx = Mathf.Sin(heading), fz = Mathf.Cos(heading), rgx = fz, rgz = -fx;
            System.Func<float, float, bool> clear = (ox, oz) =>
            {
                float ddx = ox - x, ddz = oz - z;
                float along = ddx * fx + ddz * fz, side = ddx * rgx + ddz * rgz;
                return !(along > -9f && along < 14f && Mathf.Abs(side - deltaLat) < 2.2f);
            };
            foreach (var o in ctx.cars) if (o != this && !clear(o.x, o.z)) return false;
            return clear(ctx.player.x, ctx.player.z);
        }

        bool Oncoming(float fx, float fz, float rgx, float rgz)
        {
            System.Func<float, float, float, float, Turn, int, bool> test = (ox, oz, oh, ov, oturn, oid) =>
            {
                if (ov < 1.5f) return false;
                if (oturn == Turn.Left && oid > id) return false;
                float ddx = ox - x, ddz = oz - z;
                float along = ddx * fx + ddz * fz, side = ddx * rgx + ddz * rgz;
                if (along < 0f || along > 50f || side > -0.5f || side < -9f) return false; // встречка — слева
                return Mathf.Sin(oh) * fx + Mathf.Cos(oh) * fz < -0.7f;
            };
            foreach (var o in ctx.cars) if (o != this && test(o.x, o.z, o.heading, o.speed, o.turn, o.id)) return true;
            var p = ctx.player;
            return test(p.x, p.z, p.heading, p.speed, Turn.Straight, -1);
        }

        public void Update(float dt)
        {
            if (stun > 0f) { stun -= dt; speed = 0f; ind = 'H'; braking = true; Pose(); return; }
            if (ghost > 0f) ghost -= dt;
            float fx = Mathf.Sin(heading), fz = Mathf.Cos(heading), rgx = fz, rgz = -fx;

            float v0 = cruise;
            if (turnMode) { if (turn != Turn.Straight) v0 = TurnSpeed[(int)turn]; }
            else if (turn != Turn.Straight)
            {
                float toEnd = edgeLen - s;
                if (toEnd < 30f) v0 = Mathf.Min(v0, TurnSpeed[(int)turn] + (cruise - TurnSpeed[(int)turn]) * (toEnd / 30f));
            }

            float gap = 1e9f, leadV = 0f;
            TrafficCar leader = null; bool leaderIsPlayer = false;
            if (ghost <= 0f)
            {
                foreach (var o in ctx.cars)
                {
                    if (o == this) continue;
                    if (turnMode && o.turnMode && o.id < id && o.b == b)
                    {
                        float ddx = o.x - x, ddz = o.z - z;
                        if (ddx * ddx + ddz * ddz < 36f && ddx * fx + ddz * fz > 0f) { gap = 0.01f; leadV = 0f; leader = o; continue; }
                    }
                    if (Scan(o.x, o.z, Mathf.Sin(o.heading), Mathf.Cos(o.heading), o.speed, fx, fz, rgx, rgz, ref gap, ref leadV)) { leader = o; leaderIsPlayer = false; }
                }
                var p = ctx.player;
                if (Scan(p.x, p.z, Mathf.Sin(p.heading), Mathf.Cos(p.heading), p.speed, fx, fz, rgx, rgz, ref gap, ref leadV)) { leader = null; leaderIsPlayer = true; }
                // игрок пешком на проезжей части: стоим перед ним (через пару секунд — гудок)
                var ped = ctx.ped;
                if (ped.active)
                {
                    float pdx = ped.x - x, pdz = ped.z - z;
                    float along = pdx * fx + pdz * fz, side = pdx * rgx + pdz * rgz;
                    if (along > 0.5f && along < 30f && Mathf.Abs(side) < 1.6f)
                    {
                        float gp = along - HalfLen - 1.2f;
                        if (gp < gap) { gap = Mathf.Max(gp, 0.01f); leadV = 0f; leader = null; leaderIsPlayer = true; }
                    }
                }
            }

            waitingLight = false;
            if (!turnMode)
            {
                var sig = ctx.lights.Get(b, axis);
                if (sig != Signal.Green)
                {
                    float dist = edgeLen - CityC.STOP_BACK - s - HalfLen;
                    bool canStop = dist > speed * speed / (2f * 3.5f);
                    if (dist > -1f && (sig == Signal.Red || canStop))
                    {
                        if (dist < gap) { gap = Mathf.Max(dist, 0.01f); leadV = 0f; leader = null; leaderIsPlayer = false; }
                        waitingLight = dist < 25f;
                    }
                }
                if (turn == Turn.Left)
                {
                    float dist = edgeLen - CityC.STOP_BACK - s - HalfLen;
                    if (dist > -1f && dist < 30f && Oncoming(fx, fz, rgx, rgz))
                    {
                        if (dist < gap) { gap = Mathf.Max(dist, 0.01f); leadV = 0f; leader = null; leaderIsPlayer = false; }
                        waitingLight = true;
                    }
                }
            }

            float v = speed, dv = v - leadV;
            float sStar = IdmS0 + Mathf.Max(0f, v * IdmT + v * dv / (2f * Mathf.Sqrt(IdmA * IdmB)));
            float accel = IdmA * (1f - Mathf.Pow(v / Mathf.Max(v0, 0.1f), 4f));
            if (gap < 1e8f) accel -= IdmA * Mathf.Pow(sStar / Mathf.Max(gap, 0.1f), 2f);
            accel = Mathf.Clamp(accel, -9f, IdmA);
            speed = Mathf.Max(0f, v + accel * dt);
            braking = accel < -0.8f || (speed < 0.3f && gap < 5f);

            bool leaderStopped = (leader != null && leader.speed < 0.5f && !leader.waitingLight) || (leaderIsPlayer && ctx.player.speed < 0.5f);
            if ((leader != null || leaderIsPlayer) && speed < 0.5f && gap < 10f && leaderStopped) blocked += dt;
            else blocked = Mathf.Max(0f, blocked - dt);

            if (!turnMode && !laneChange)
            {
                float toEnd = edgeLen - s;
                if (blocked > 2f && toEnd > 18f)
                {
                    int other = 1 - lane;
                    if (LaneFree(CityC.LANES[other] - lat)) { StartLaneChange(other); blocked = 0f; }
                }
                else if (wantLane != lane && toEnd < 70f && toEnd > 22f)
                {
                    if (LaneFree(CityC.LANES[wantLane] - lat)) StartLaneChange(wantLane);
                }
            }
            if (blocked > 8f && turnMode) { ghost = 2f; blocked = 0f; }

            honkCd -= dt;
            if (leaderIsPlayer && blocked > 2.5f && honkCd <= 0f) { honkCd = 4f + ctx.rnd.Next() * 4f; ctx.OnHonk(this); }

            if (laneChange) ind = latTarget > lat ? 'R' : 'L';
            else if (turn == Turn.Right && (turnMode || edgeLen - s < 45f)) ind = 'R';
            else if ((turn == Turn.Left || turn == Turn.UTurn) && (turnMode || edgeLen - s < 45f)) ind = 'L';
            else ind = ' ';

            s += speed * dt;
            if (!turnMode)
            {
                if (lat != latTarget)
                {
                    float step = 1.8f * dt * Mathf.Min(1f, speed / 3f + 0.3f);
                    float d = latTarget - lat;
                    lat = Mathf.Abs(d) <= step ? latTarget : lat + Mathf.Sign(d) * step;
                    if (lat == latTarget) laneChange = false;
                }
                if (s >= edgeLen) { s -= edgeLen; EnterTurn(); }
            }
            if (turnMode && s >= turnLen)
            {
                float rest = s - turnLen;
                EnterEdge(b, c, nextLane);
                s = rest;
            }
            Pose();
        }

        bool Scan(float ox, float oz, float ohx, float ohz, float ov, float fx, float fz, float rgx, float rgz, ref float gap, ref float leadV)
        {
            float ddx = ox - x, ddz = oz - z;
            if (ddx > 45f || ddx < -45f || ddz > 45f || ddz < -45f) return false;
            float along = ddx * fx + ddz * fz;
            if (along <= 0.5f || along > 40f) return false;
            float side = ddx * rgx + ddz * rgz;
            float width = turnMode ? 2.4f : 1.9f;
            if (Mathf.Abs(side) > width) return false;
            if (ohx * fx + ohz * fz < -0.5f && along > 10f) return false;
            float gg = along - CarLen;
            if (gg < gap) { gap = gg; leadV = Mathf.Max(0f, ov * (ohx * fx + ohz * fz)); return true; }
            return false;
        }

        void StartLaneChange(int ln) { lane = ln; latTarget = CityC.LANES[ln]; laneChange = true; }

        void Pose()
        {
            if (!turnMode)
            {
                x = sx + dx * s + rx * lat;
                z = sz + dz * s + rz * lat;
                float latVel = laneChange ? Mathf.Sign(latTarget - lat) : 0f;
                heading = Mathf.Atan2(dx, dz) + latVel * 0.12f;
            }
            else
            {
                float t = Mathf.Min(s / turnLen, 1f), u = 1f - t;
                x = u * u * bz[0] + 2 * u * t * bz[2] + t * t * bz[4];
                z = u * u * bz[1] + 2 * u * t * bz[3] + t * t * bz[5];
                float tx = 2 * u * (bz[2] - bz[0]) + 2 * t * (bz[4] - bz[2]);
                float tz = 2 * u * (bz[3] - bz[1]) + 2 * t * (bz[5] - bz[3]);
                heading = Mathf.Atan2(tx, tz);
            }
            vx = Mathf.Sin(heading) * speed;
            vz = Mathf.Cos(heading) * speed;
        }
    }

    /// <summary>Пул машин трафика: спавн в кольце 70–190 м вокруг игрока вне поля зрения, деспавн дальше 240 м.</summary>
    public class TrafficManager
    {
        const float SpawnMin = 70f, SpawnMax = 190f, Despawn = 240f;
        public readonly RoadGraph graph;
        public readonly TrafficLights lights;
        public readonly Rng rnd = new Rng(777);
        public readonly List<TrafficCar> cars = new List<TrafficCar>();
        readonly Stack<TrafficCar> free = new Stack<TrafficCar>();
        public readonly PlayerProbe player = new PlayerProbe();
        public readonly PedProbe ped = new PedProbe();
        public Vector3 center;
        public int target;
        public float density = 1f;
        public bool enabled = true;
        public System.Action<TrafficCar> onHonk;
        readonly Transform root;
        float spawnTimer;
        readonly Plane[] planes = new Plane[6];
        float totalW;
        readonly List<KeyValuePair<float, CarDef>> weights = new List<KeyValuePair<float, CarDef>>();
        readonly Dictionary<string, GameObject[]> idle = new Dictionary<string, GameObject[]>();

        public TrafficManager(Transform parent, RoadGraph g, TrafficLights l, int maxCars)
        {
            graph = g; lights = l; target = maxCars;
            root = new GameObject("Traffic").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < maxCars; i++) free.Push(new TrafficCar(i));
            foreach (var c in Cars.All) { totalW += c.traffic; weights.Add(new KeyValuePair<float, CarDef>(totalW, c)); }
            totalW += 0.8f; weights.Add(new KeyValuePair<float, CarDef>(totalW, Cars.Get("taxi")));
            totalW += 0.4f; weights.Add(new KeyValuePair<float, CarDef>(totalW, Cars.Get("police")));
        }

        public void OnHonk(TrafficCar c) { onHonk?.Invoke(c); }

        CarDef PickModel(out int color)
        {
            float r = rnd.Next() * totalW;
            foreach (var w in weights)
                if (r <= w.Key)
                {
                    var d = w.Value;
                    color = d.fixedColor >= 0 ? d.fixedColor : d.colors[rnd.Range(d.colors.Length)];
                    return d;
                }
            color = 0xffffff;
            return Cars.All[0];
        }

        /// <summary>Объект машины трафика: LODGroup (LOD0 → LOD1 → скрыта), кинематическое тело для столкновений.</summary>
        public static GameObject MakeCarObject(CarDef def, int color, Transform parent, bool kinematicBody, bool shadows)
        {
            var go = new GameObject("Car_" + def.id);
            go.transform.SetParent(parent, false);
            var mat = CarMeshLibrary.PaletteMaterial(color);
            var l0 = CarMeshLibrary.Get(def.id, "LOD0"); var l1 = CarMeshLibrary.Get(def.id, "LOD1");
            var rs = new List<Renderer>();
            var lods = new List<LOD>();
            if (l0 != null && l0.flat != null)
            {
                var a = new GameObject("LOD0"); a.transform.SetParent(go.transform, false);
                a.AddComponent<MeshFilter>().sharedMesh = l0.flat;
                var r0 = a.AddComponent<MeshRenderer>(); r0.sharedMaterial = mat;
                r0.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                lods.Add(new LOD(0.12f, new Renderer[] { r0 }));
            }
            if (l1 != null && l1.flat != null)
            {
                var b = new GameObject("LOD1"); b.transform.SetParent(go.transform, false);
                b.AddComponent<MeshFilter>().sharedMesh = l1.flat;
                var r1 = b.AddComponent<MeshRenderer>(); r1.sharedMaterial = mat;
                r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lods.Add(new LOD(0.015f, new Renderer[] { r1 }));
            }
            if (lods.Count > 0) { var lg = go.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray()); lg.RecalculateBounds(); }
            var d = def.dims;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.75f, (d.front + d.rear) / 2f);
            col.size = new Vector3(d.W, 1.3f, d.front - d.rear);
            if (kinematicBody)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            go.AddComponent<Obstacle>().kind = "car";
            return go;
        }

        public void Clear()
        {
            for (int i = cars.Count - 1; i >= 0; i--) Release(cars[i]);
        }

        void Release(TrafficCar c)
        {
            c.active = false;
            if (c.go) Object.Destroy(c.go);
            c.go = null;
            cars.Remove(c);
            free.Push(c);
        }

        void TrySpawn(Camera cam)
        {
            if (free.Count == 0) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var e = graph.edges[rnd.Range(graph.edges.Count)];
                var A = graph.nodes[e.x]; var B = graph.nodes[e.y];
                var d = graph.Dir(e.x, e.y);
                float len = Mathf.Sqrt((B.x - A.x) * (B.x - A.x) + (B.z - A.z) * (B.z - A.z)) - 2f * CityC.HALF;
                float s = 6f + rnd.Next() * (len - 26f);
                int lane = rnd.Next() < 0.5f ? 0 : 1;
                float x = A.x + d.x * (CityC.HALF + s) + d.y * CityC.LANES[lane];
                float z = A.z + d.y * (CityC.HALF + s) - d.x * CityC.LANES[lane];
                float dist = Mathf.Sqrt((x - center.x) * (x - center.x) + (z - center.z) * (z - center.z));
                if (dist < SpawnMin || dist > SpawnMax) continue;
                if (dist < 150f && GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(x, 1f, z), new Vector3(4, 2, 4)))) continue;
                bool close = false;
                foreach (var o in cars) if (Mathf.Abs(o.x - x) < 12f && Mathf.Abs(o.z - z) < 12f) { close = true; break; }
                if (close) continue;
                var car = free.Pop();
                int color;
                var def = PickModel(out color);
                car.Spawn(this, e.x, e.y, lane, s, 10f + rnd.Next() * 5.5f, def, color);
                car.go = MakeCarObject(def, color, root, true, false);
                car.rb = car.go.GetComponent<Rigidbody>();
                car.go.transform.SetPositionAndRotation(new Vector3(car.x, 0, car.z), M.Yaw(car.heading));
                cars.Add(car);
                return;
            }
        }

        public void Prefill(Camera cam)
        {
            for (int i = 0; i < target * 3 && cars.Count < target; i++) TrySpawn(cam);
        }

        public void Update(float dt, Camera cam)
        {
            if (!enabled) return;
            spawnTimer -= dt;
            if (spawnTimer <= 0f && cars.Count < Mathf.RoundToInt(target * density))
            {
                spawnTimer = 0.25f;
                TrySpawn(cam);
            }
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                var c = cars[i];
                c.Update(dt);
                float ddx = c.x - center.x, ddz = c.z - center.z;
                if (ddx * ddx + ddz * ddz > Despawn * Despawn) { Release(c); continue; }
                var p = new Vector3(c.x, 0f, c.z); var q = M.Yaw(c.heading);
                c.go.transform.SetPositionAndRotation(p, q);
            }
        }

        /// <summary>Фары, стопы и поворотники трафика — в общую систему светящихся точек.</summary>
        public void RenderGlow(Glow glow, float night, bool blink, Vector3 cam)
        {
            foreach (var c in cars)
            {
                float ddx = c.x - cam.x, ddz = c.z - cam.z;
                if (ddx * ddx + ddz * ddz > 180f * 180f) continue;
                var f = M.Fwd(c.heading); var r = M.Right(c.heading);
                var p = new Vector3(c.x, 0f, c.z); var d = c.def;
                bool lit = night > 0.3f;
                if (lit) foreach (int sd in new[] { -1, 1 }) glow.Add(p + f * d.dims.front + r * sd * d.headX + Vector3.up * d.headY, new Color(1f, 0.92f, 0.75f), 0.9f);
                if (lit || c.braking) foreach (int sd in new[] { -1, 1 }) glow.Add(p + f * d.dims.rear + r * sd * d.tailX + Vector3.up * d.tailY, new Color(1f, 0.08f, 0.04f), c.braking ? 0.9f : 0.5f);
                if (blink && c.ind != ' ')
                {
                    if (c.ind == 'L' || c.ind == 'H') { glow.Add(p + f * d.dims.front - r * (d.dims.W / 2f - 0.1f) + Vector3.up * d.headY, new Color(1f, 0.55f, 0.05f), 0.55f); glow.Add(p + f * d.dims.rear - r * (d.dims.W / 2f - 0.1f) + Vector3.up * d.tailY, new Color(1f, 0.55f, 0.05f), 0.55f); }
                    if (c.ind == 'R' || c.ind == 'H') { glow.Add(p + f * d.dims.front + r * (d.dims.W / 2f - 0.1f) + Vector3.up * d.headY, new Color(1f, 0.55f, 0.05f), 0.55f); glow.Add(p + f * d.dims.rear + r * (d.dims.W / 2f - 0.1f) + Vector3.up * d.tailY, new Color(1f, 0.55f, 0.05f), 0.55f); }
                }
                if (d.police) glow.Add(p + Vector3.up * 1.65f + r * (blink ? -0.3f : 0.3f), blink ? new Color(0.1f, 0.2f, 1f) : new Color(1f, 0.1f, 0.1f), 0.9f);
            }
        }
    }
}
