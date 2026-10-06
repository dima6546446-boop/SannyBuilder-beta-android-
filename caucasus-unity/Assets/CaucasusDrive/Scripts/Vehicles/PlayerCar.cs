using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Передаёт столкновения кузова игроку (удар о стену, конус, машину трафика).</summary>
    public class CarCollisionRelay : MonoBehaviour
    {
        public System.Action<Collision> onHit;
        void OnCollisionEnter(Collision c) { onHit?.Invoke(c); }
    }

    /// <summary>
    /// Машина игрока. Физика — VehiclePhysics (порт веб-версии), столкновения — PhysX:
    /// каждый физический кадр состояние читается из Rigidbody (позиция/скорость после контактов),
    /// шаг модели шин считает новую скорость и рыскание, и они записываются обратно в Rigidbody.
    /// Координаты модели зеркальны по X (как в веб-версии): x → −x, курс → −курс.
    /// </summary>
    public class PlayerCar
    {
        public GameObject go;
        public Rigidbody rb;
        public CarVisual vis;
        public VehiclePhysics phys;
        public CarDef def;
        public CarTune tune;
        public Surface surface;
        public float y;
        public bool lightsOn;
        public int lightsMode;        // 0 — авто, 1 — вкл, 2 — выкл
        public char indicator = ' ';  // 'L', 'R', 'H'
        public bool blinkOn;
        float blinkT, cool;
        public System.Action<float, string, Collider> onCrash; // сила 0..1, тип препятствия
        public System.Action onBlink;
        SkidMarks skids;
        readonly Transform parent;
        string lod;

        public float Speed => phys.speed;
        public float Heading => -phys.heading;                        // курс в Unity
        public Vector3 Position => go.transform.position;
        public Vector3 Velocity => GetVel();
        public float time, lastIndL = -99f, lastIndR = -99f;
        public System.Action onPop;
        public float flame;                      // вспышка из прямотока (для свечения)
        int popQueue; float popT, prevThr;
        GameObject taxiSign;
        public Interior interior;

        public PlayerCar(Transform parent)
        {
            this.parent = parent;
            go = new GameObject("PlayerCar");
            go.transform.SetParent(parent, false);
            rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0f; rb.angularDamping = 0f;
#else
            rb.drag = 0f; rb.angularDrag = 0f;
#endif
            go.AddComponent<CarCollisionRelay>().onHit = OnHit;
            skids = new SkidMarks(parent);
        }

        /// <summary>Поставить машину и тюнинг. lodName: LOD_ultra (высокое качество) или LOD_hi.</summary>
        public void SetCar(CarDef d, CarTune t, int color, string lodName, bool assist, bool manual)
        {
            def = d; tune = t; lod = lodName;
            var keepPos = go.transform.position; var keepRot = go.transform.rotation;
            if (vis != null) vis.Destroy();
            vis = CarVisual.Build(d, t, color, lodName, true, go.transform);
            var spec = PhysSpec.Make(d, Tuning.Engine[Mathf.Clamp(t.engine, 0, 3)].value, Tuning.Tires[Mathf.Clamp(t.tires, 0, 1)].value, Tuning.Height[Mathf.Clamp(t.height, 0, 3)].value);
            spec.stabilityAssist = assist ? 0.35f : 0f;
            spec.driftAssist = assist ? 1f : 0f;
            if (phys == null) phys = new VehiclePhysics(spec); else phys.SetSpec(spec);
            phys.SetManual(manual);
            rb.mass = spec.mass;
            // коллайдер кузова: коробка по габаритам, физ. материал без трения (скользим вдоль стен)
            foreach (var c in go.GetComponents<Collider>()) Object.Destroy(c);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.72f, (d.dims.front + d.dims.rear) / 2f);
            box.size = new Vector3(d.dims.W - 0.04f, 1.2f, d.dims.front - d.dims.rear - 0.04f);
#if UNITY_6000_0_OR_NEWER
            box.sharedMaterial = new PhysicsMaterial("Car") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0.12f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Average };
#else
            box.sharedMaterial = new PhysicMaterial("Car") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0.12f, frictionCombine = PhysicMaterialCombine.Minimum, bounceCombine = PhysicMaterialCombine.Average };
#endif
            go.transform.SetPositionAndRotation(keepPos, keepRot);
            interior?.Destroy();
            interior = Interior.Build(d, vis.body);
            SetTaxiSign(taxiOn);
        }

        public void Place(float x, float z, float h)
        {
            rb.position = new Vector3(x, 0, z);
            rb.rotation = M.Yaw(h);
            go.transform.SetPositionAndRotation(rb.position, rb.rotation);
            SetVel(Vector3.zero, Vector3.zero);
            phys.Reset(-x, z, -h);
            phys.fuel = Mathf.Max(phys.fuel, phys.spec.tank * 0.5f);
            skids.Clear();
            y = 0f;
        }

        void SetVel(Vector3 v, Vector3 w)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
            rb.angularVelocity = w;
        }

        Vector3 GetVel()
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        /// <summary>Физический кадр (FixedUpdate).</summary>
        public void FixedStep(float dt, CarInput input, float muScale)
        {
            var p = rb.position; var v = GetVel();
            float yaw = rb.rotation.eulerAngles.y * Mathf.Deg2Rad;
            phys.x = -p.x; phys.z = p.z; phys.heading = -yaw;
            phys.vx = -v.x; phys.vz = v.z; phys.yawRate = -rb.angularVelocity.y;
            surface = App.I.city.SurfaceAt(p.x, p.z);
            phys.Update(dt, input, surface.mu * muScale);
            SetVel(new Vector3(-phys.vx, 0, phys.vz), new Vector3(0, -phys.yawRate, 0));
            if (cool > 0) cool -= dt;
        }

        void OnHit(Collision c)
        {
            var obs = c.collider.GetComponentInParent<Obstacle>();
            string kind = obs ? obs.kind : "building";
            float imp = c.relativeVelocity.magnitude;
            if (kind == "car")
            {
                foreach (var t in App.I.traffic.cars) if (t.go == obs.gameObject) t.Bump(imp);
            }
            // любое касание — режим решает сам (парковка: провал, город: удар при силе > 0.08)
            if (cool <= 0f)
            {
                cool = 0.3f;
                onCrash?.Invoke(Mathf.Min(1f, imp / 14f), kind, c.collider);
            }
        }

        /// <summary>Визуал (каждый кадр): крен, колёса, фары, поворотники, следы и дым.</summary>
        public void VisualUpdate(float dt, CarInput input, float night)
        {
            var p = phys;
            y = M.Damp(y, surface.y, 18f, dt);
            vis.root.transform.localPosition = new Vector3(0, y, 0);
            // «клевок» при торможении (+X в Unity — нос вниз) и крен наружу поворота (+Z — правый борт вверх)
            vis.body.localRotation = Quaternion.Euler(M.Clamp(-p.axLong * 0.009f, -0.05f, 0.05f) * Mathf.Rad2Deg, 0, M.Clamp(p.ayLat * 0.011f, -0.07f, 0.07f) * Mathf.Rad2Deg);
            float R = def.dims.wheelR;
            float spin = p.vLong / R * dt * Mathf.Rad2Deg;
            foreach (var w in vis.wheels)
            {
                float s = (w.front || !input.handbrake) ? spin : 0f;
                bool driven = p.spec.drive == Drive.AWD || (p.spec.drive == Drive.FWD) == w.front;
                if (driven && p.wheelSpin > 0.1f && p.load > 0.5f) s += 35f * Mathf.Sign(p.vLong == 0 ? 1 : p.vLong);
                w.spin.Rotate(s, 0, 0, Space.Self); // +X — верх колеса вперёд
                if (w.front) w.pivot.localRotation = Quaternion.Euler(0, p.steer * Mathf.Rad2Deg, 0); // steer > 0 — вправо
            }

            time += dt;
            if (indicator == 'L') lastIndL = time;
            if (indicator == 'R') lastIndR = time;
            // прямоток: резкий сброс газа на высоких оборотах — 2–5 «выстрелов»
            if (tune != null && tune.exhaust > 0)
            {
                if (prevThr > 0.6f && input.throttle < 0.15f && p.rpm > 3200f && popQueue <= 0) { popQueue = 2 + Random.Range(0, 4); popT = 0.05f; }
                prevThr = input.throttle;
                if (popQueue > 0 && (popT -= dt) <= 0f) { popQueue--; popT = 0.08f + Random.value * 0.1f; flame = 0.07f; onPop?.Invoke(); }
                if (flame > 0f) flame -= dt;
            }
            if (interior != null) interior.UpdateState(dt, p, lightsOn, night, App.I.dayNight.time, App.I.rainLevel > 0.15f);
            if (indicator != ' ')
            {
                blinkT += dt;
                bool on = (blinkT % 0.7f) < 0.35f;
                if (on != blinkOn) { blinkOn = on; onBlink?.Invoke(); }
                if (indicator != 'H')
                {
                    if (Mathf.Abs(p.steer) > 0.25f) wasTurning = true;
                    else if (wasTurning && Mathf.Abs(p.steer) < 0.05f && p.speed > 3f) { wasTurning = false; indicator = ' '; }
                }
            }
            else { blinkOn = false; wasTurning = false; }

            lightsOn = lightsMode == 1 || (lightsMode == 0 && night > 0.3f);
            vis.SetLamps(lightsOn, p.braking, p.reversing,
                blinkOn && (indicator == 'L' || indicator == 'H'), blinkOn && (indicator == 'R' || indicator == 'H'));

            // следы и дым задних колёс
            float sk = p.Skid;
            bool skid = sk > 0.35f || (input.handbrake && p.speed > 3f);
            float driftK = p.sliding * Mathf.Min(1f, Mathf.Abs(p.driftAngle) / 0.6f) * Mathf.Min(1f, p.speed / 14f);
            float markK = Mathf.Max(driftK, Mathf.Min(1f, (sk - 0.35f) * 1.5f));
            float smokeK = Mathf.Max(driftK, (sk - 0.45f) * 1.2f);
            var t = go.transform;
            var d = def.dims;
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? 1 : -1;
                var wp = t.TransformPoint(new Vector3(side * d.track / 2f, 0, d.axleR));
                wp.y = y;
                skids.Add(i, wp, t.right, skid && surface.type != 2, markK);
                bool grass = surface.type == 2 && p.speed > 4f;
                // дым — реже и только при заметном заносе; пыль на траве — изредка
                if ((smokeK > 0.05f && surface.type != 2 && Random.value < 0.12f + smokeK * 0.5f) || (grass && Random.value < 0.18f))
                    SmokeFx.I?.Emit(wp, GetVel(), grass, Mathf.Min(1f, smokeK), 1f - night * 0.7f);
            }
            skids.Flush();
        }

        bool wasTurning;

        /// <summary>Углы кузова в мировых координатах (для проверки «встал в зону»).</summary>
        public Vector3[] Corners()
        {
            var d = def.dims; var t = go.transform; float w = d.W / 2f;
            return new[] { t.TransformPoint(new Vector3(w, 0, d.front)), t.TransformPoint(new Vector3(-w, 0, d.front)), t.TransformPoint(new Vector3(-w, 0, d.rear)), t.TransformPoint(new Vector3(w, 0, d.rear)) };
        }

        /// <summary>Мир-координаты выхлопной трубы (слева сзади).</summary>
        public Vector3 ExhaustPos => vis.body.TransformPoint(new Vector3(-0.42f, 0.3f, def.dims.rear - 0.14f));

        /// <summary>Шашечки «ТАКСИ» на крыше.</summary>
        public bool taxiOn;
        public void SetTaxiSign(bool on)
        {
            taxiOn = on;
            if (taxiSign) Object.Destroy(taxiSign);
            taxiSign = null;
            if (!on || vis == null) return;
            var pm = new PaletteMesh();
            pm.Box(new Vector3(0, 0, 0), new Vector3(0.5f, 0.15f, 0.2f), 0xffd000);
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 4; k++)
                    pm.Box(new Vector3(s * 0.253f, -0.03f + (k % 2) * 0.06f, -0.15f + k * 0.1f), new Vector3(0.005f, 0.06f, 0.1f), 0x111111);
            taxiSign = pm.ToObject("TaxiSign", vis.body);
            float roof = 0.9f;
            foreach (var r in vis.body.GetComponentsInChildren<MeshRenderer>()) if (r.gameObject != taxiSign && r.transform.parent == vis.body) roof = Mathf.Max(roof, r.bounds.max.y - vis.body.position.y);
            taxiSign.transform.localPosition = new Vector3(0, Mathf.Min(roof, 1.6f) + 0.07f, (def.dims.front + def.dims.rear) * 0.5f - 0.3f);
            taxiSign.layer = 2;
        }

        public void CycleLights() { lightsMode = (lightsMode + 1) % 3; }
        public void ToggleIndicator(char side) { indicator = indicator == side ? ' ' : side; blinkT = 0; }
        public void ToggleHazard() { indicator = indicator == 'H' ? ' ' : 'H'; blinkT = 0; }
    }
}
