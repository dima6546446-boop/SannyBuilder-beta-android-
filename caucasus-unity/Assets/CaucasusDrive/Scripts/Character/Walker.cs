using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Игрок пешком (порт Walker.js): выходит из машины и гуляет по городу.
    ///  - ходьба/бег (джойстик: сила наклона — скорость), прыжок;
    ///  - «Сесть»: на лавку остановки, на капот своей машины или на корточки;
    ///  - «Курить»: прикурить, затяжки с огоньком, струйка с кончика и выдох изо рта;
    ///  - «Свист»: два пальца в рот, прохожие оборачиваются, машины сигналят в ответ;
    ///  - еда из ларька в левой руке; сбит машиной — падает и встаёт.
    /// Коллизии — CharacterController (стены, машины, столбы); павильон остановки пропускает к лавке.
    /// Анимация процедурная: целевая поза по состоянию сглаживается по каждой кости, руки — IK.
    /// </summary>
    public class Walker
    {
        public const int Layer = 10;
        const float WALK = 1.7f, RUN = 5.2f, GRAVITY = 13f, JUMP_V = 4.4f, SIT_HIPS = 0.52f, SQUAT_HIPS = 0.4f;
        public enum St { Walk, Air, Sit, Down }
        readonly App app;
        public readonly Character c;
        readonly CharacterController cc;
        public St state = St.Walk;
        public float yaw, speed, vy;
        float vx, vz, phase, t, downT, immuneUntil, landT, ikR, ikL, exhale, tipGlow = 0.4f;
        int stepIdx;
        class Seat { public string kind; public Vector3 pos, from, exit; public float yaw, fromYaw, top, k; }
        Seat seat;
        // курение
        public bool smoking; string smokeStage = "off"; float smokeT, smokeNext, tipT; int drags; bool lit, puffed;
        // свист
        float whistleT; char whistleHand; bool whistled;
        // еда
        public string food; GameObject foodObj; string foodStage; float foodT; int bites; bool foodSounded;
        static readonly string[] Bones = { "spine", "chest", "neck", "head", "upperArmL", "forearmL", "handL", "upperArmR", "forearmR", "handR", "thighL", "shinL", "footL", "thighR", "shinR", "footR" };
        readonly Dictionary<string, Vector3> cur = new Dictionary<string, Vector3>(), T = new Dictionary<string, Vector3>();
        float hipsY = 0.98f;

        public Walker(App a)
        {
            app = a;
            c = new Character(Outfit.Player, a.worldRoot, a.quality.level >= 1, 1);
            c.root.name = "Walker";
            cc = c.root.gameObject.AddComponent<CharacterController>();
            cc.radius = 0.28f; cc.height = 1.75f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.35f; cc.skinWidth = 0.03f; cc.slopeLimit = 60f;
            foreach (var tr in c.root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layer;
            Physics.IgnoreLayerCollision(Layer, City.StopLayer, true);
            foreach (var b in Bones) { cur[b] = Vector3.zero; T[b] = Vector3.zero; }
            c.SetVisible(false);
        }

        public bool Active => c.root.gameObject.activeSelf;
        public Vector3 Pos => c.root.position;
        public Vector3 HeadPos => c.B["head"].position;

        float Ground(float x, float z) { return app.city.SurfaceAt(x, z).y; }

        // ------------------------------------------------------------------ появление
        public void Spawn(Vector3 p, float yw)
        {
            c.SetVisible(true);
            cc.enabled = false;
            c.root.position = new Vector3(p.x, Ground(p.x, p.z), p.z);
            Physics.SyncTransforms();
            cc.enabled = true;
            yaw = yw; vx = vz = vy = 0; speed = 0; state = St.Walk; seat = null;
            c.ResetPose(); hipsY = 0.98f;
            foreach (var b in Bones) cur[b] = Vector3.zero;
        }

        public void Hide() { StopSmoking(true); DropFood(); c.SetVisible(false); }

        // ------------------------------------------------------------------ действия
        public void Jump()
        {
            if (state == St.Sit) { StandUp(); return; }
            if (state != St.Walk) return;
            state = St.Air; vy = JUMP_V; app.audio.Footstep(1.4f);
        }

        public void ToggleSit()
        {
            if (state == St.Sit) { StandUp(); return; }
            if (state != St.Walk) return;
            Seat best = null; float bestD = 1.8f;
            var pos = Pos;
            foreach (var b in app.city.busStops)
            {
                var along = new Vector3(Mathf.Abs(b.n.z), 0, Mathf.Abs(b.n.x));
                float a = Mathf.Clamp(Vector3.Dot(pos - b.bench, along), -1.3f, 1.3f);
                var sp = b.bench + along * a;
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(sp.x, sp.z));
                if (d < bestD) { bestD = d; best = new Seat { kind = "bench", pos = sp, yaw = b.yaw, top = sp.y, exit = sp + b.n * 0.7f }; }
            }
            var car = app.player; var f = M.Fwd(car.Heading); var cp = car.Position;
            var hood = cp + f * (car.def.dims.front - 0.32f);
            float dHood = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(cp.x + f.x * (car.def.dims.front + 0.4f), cp.z + f.z * (car.def.dims.front + 0.4f)));
            if (dHood < Mathf.Min(bestD, 1.6f) && car.Speed < 0.3f)
            {
                string id = car.def.baseId ?? car.def.id;
                float top = car.y + (id == "niva" ? 1.02f : id == "largus" || id == "vesta" ? 0.98f : 0.9f);
                best = new Seat { kind = "hood", pos = hood, yaw = car.Heading, top = top, exit = cp + f * (car.def.dims.front + 0.6f) };
            }
            seat = best ?? new Seat { kind = "squat" };
            if (best != null) { seat.from = pos; seat.fromYaw = yaw; seat.k = 0; }
            state = St.Sit; vx = vz = 0;
        }

        public void StandUp()
        {
            var s = seat;
            state = St.Walk;
            if (s != null && s.kind != "squat") Teleport(new Vector3(s.exit.x, Ground(s.exit.x, s.exit.z), s.exit.z));
            seat = null;
        }

        void Teleport(Vector3 p) { cc.enabled = false; c.root.position = p; Physics.SyncTransforms(); cc.enabled = true; }

        public void ToggleSmoking()
        {
            if (smoking) { StopSmoking(false); return; }
            smoking = true; smokeStage = "light"; smokeT = 0; drags = 0; smokeNext = 0; tipT = 0; lit = false;
            c.cigarette.SetActive(true);
            if (!warned) { warned = true; app.hud.Toast("Минздрав предупреждает: курение вредит вашему здоровью", HUD.Bad, 3f); }
        }
        static bool warned;

        public void StopSmoking(bool silent)
        {
            if (!smoking) return;
            smoking = false; smokeStage = "off";
            c.cigarette.SetActive(false);
            if (!silent)
            {
                var tip = c.cigTip.position; var f = M.Fwd(yaw);
                for (int i = 0; i < 3; i++) SmokeFx.I?.Puff(tip, f * 1.5f + Vector3.up * 0.4f, 0.06f, 0.4f, 0.5f);
            }
        }

        public void WhistleNow()
        {
            if (whistleT > 0 || state == St.Down) return;
            whistleT = 1.5f; whistled = false;
            whistleHand = smoking ? (food != null ? ' ' : 'L') : 'R';
        }

        // ------------------------------------------------------------------ еда
        public void Eat(string kind)
        {
            DropFood();
            food = kind;
            foodObj = Shops.ItemMesh(kind, c.B["handL"]);
            foodObj.transform.localPosition = new Vector3(-0.01f, -0.095f, 0.04f);
            foodObj.transform.localRotation = PaletteMesh.Q3(Mathf.PI, 0, 0);
            foreach (var tr in foodObj.GetComponentsInChildren<Transform>()) tr.gameObject.layer = Layer;
            foodStage = "hold"; foodT = 0; bites = 0;
        }

        void DropFood() { if (foodObj) Object.Destroy(foodObj); foodObj = null; food = null; }

        // ------------------------------------------------------------------ кадр
        public void Update(float dt, float mx, float my, bool walkOnly, float camYaw)
        {
            if (!Active) return;
            t += dt;
            float mag = Mathf.Min(1f, Mathf.Sqrt(mx * mx + my * my));
            if (state == St.Sit && mag > 0.35f) StandUp();
            float tvx = 0, tvz = 0;
            if ((state == St.Walk || state == St.Air) && mag > 0.08f)
            {
                var f = M.Fwd(camYaw); var r = M.Right(camYaw);
                var d = (f * my + r * mx) / Mathf.Max(mag, 1e-3f);
                float sp = walkOnly ? WALK * Mathf.Min(1f, mag / 0.6f) : mag < 0.8f ? WALK * (mag / 0.8f) : WALK + (mag - 0.8f) / 0.2f * (RUN - WALK);
                tvx = d.x * sp; tvz = d.z * sp;
                if (state == St.Walk) yaw = M.DampAngle(yaw, Mathf.Atan2(d.x, d.z), 10f, dt);
            }
            float acc = state == St.Air ? 2.5f : 12f;
            if (state != St.Sit && state != St.Down) { vx = M.Damp(vx, tvx, acc, dt); vz = M.Damp(vz, tvz, acc, dt); }
            else { vx = M.Damp(vx, 0, 8, dt); vz = M.Damp(vz, 0, 8, dt); }
            speed = Mathf.Sqrt(vx * vx + vz * vz);
            if (state == St.Walk && speed > 0.3f) app.daily.Progress("walk", speed * dt);

            var pos = Pos;
            float gy = Ground(pos.x, pos.z);
            float ny = pos.y;
            if (state == St.Air)
            {
                vy -= GRAVITY * dt;
                ny += vy * dt;
                if (ny <= gy && vy < 0) { ny = gy; state = St.Walk; landT = 0.25f; app.audio.Footstep(1.6f); }
            }
            else if (state == St.Sit && seat.kind != "squat")
            {
                var s = seat;
                s.k = Mathf.Min(1f, s.k + dt * 2.5f);
                float k = s.k * s.k * (3 - 2 * s.k);
                var p = Vector3.Lerp(s.from, s.pos, k);
                p.y = Mathf.Lerp(s.from.y, s.top + 0.06f - SIT_HIPS, k);
                yaw = s.fromYaw + M.WrapAngle(s.yaw - s.fromYaw) * k;
                cc.enabled = false; c.root.position = p; cc.enabled = true;
                if (s.kind == "hood" && app.player.Speed > 0.6f) StandUp();
            }
            else ny = gy > ny ? M.Damp(ny, gy, 25, dt) : M.Damp(ny, gy, 18, dt);

            if (state != St.Sit || seat.kind == "squat")
            {
                var before = Pos;
                cc.Move(new Vector3(vx * dt, ny - before.y, vz * dt));
                var after = Pos;
                // упёрлись — гасим скорость
                if (dt > 0) { vx = Mathf.Lerp(vx, (after.x - before.x) / dt, 0.5f); vz = Mathf.Lerp(vz, (after.z - before.z) / dt, 0.5f); }
                KnockCheck();
            }
            if (state == St.Down) { downT -= dt; if (downT <= 0) { state = St.Walk; landT = 0.3f; } }
            c.root.rotation = M.Yaw(yaw);
            float bx = Mathf.Lerp(c.body.localEulerAngles.x > 180 ? c.body.localEulerAngles.x - 360 : c.body.localEulerAngles.x,
                state == St.Down && downT > 0.5f ? -83f : 0f, 1f - Mathf.Exp(-(state == St.Down ? 9f : 5f) * dt));
            c.body.localRotation = Quaternion.Euler(bx, 0, 0);

            var ped = app.traffic.ped;
            ped.active = state != St.Sit || seat.kind == "squat";
            ped.x = Pos.x; ped.z = Pos.z;

            Animate(dt);
            Smoke(dt);
            Whistle(dt);
            EatTick(dt);
        }

        void KnockCheck()
        {
            if (state == St.Down || t < immuneUntil) return;
            var p = Pos;
            foreach (var car in app.traffic.cars)
            {
                if (!car.active || car.speed < 2.5f) continue;
                float dx = p.x - car.x, dz = p.z - car.z;
                if (dx * dx + dz * dz > 16f) continue;
                float s = Mathf.Sin(car.heading), co = Mathf.Cos(car.heading);
                float lx = dx * co - dz * s, lz = dx * s + dz * co;
                if (Mathf.Abs(lx) < 0.85f + 0.3f && lz > -2.15f - 0.3f && lz < 2.15f + 0.3f) { KnockDown(car); return; }
            }
        }

        void KnockDown(TrafficCar car)
        {
            state = St.Down; downT = 2.4f; immuneUntil = t + 5f; seat = null;
            var f = M.Fwd(car.heading);
            vx = f.x * car.speed * 0.6f; vz = f.z * car.speed * 0.6f;
            yaw = Mathf.Atan2(-f.x, -f.z);
            StopSmoking(true);
            app.audio.Crash(0.5f);
            app.cameraRig.AddShake(0.6f);
            app.hud.Toast("Смотри по сторонам, пешеход!", HUD.Bad);
            car.Bump(1f);
        }

        // ------------------------------------------------------------------ анимация
        void Set(string b, float x, float y, float z) { T[b] = new Vector3(x, y, z); }
        float Tx(string b) { return T[b].x; }
        void AddX(string b, float v) { var q = T[b]; q.x += v; T[b] = q; }
        void SetX(string b, float v) { var q = T[b]; q.x = v; T[b] = q; }
        void MulX(string b, float v) { var q = T[b]; q.x *= v; T[b] = q; }

        void Animate(float dt)
        {
            foreach (var b in Bones) T[b] = Vector3.zero;
            float hy = 0.98f, rate = 10f;
            if (state == St.Walk || (state == St.Down && downT <= 0.5f))
            {
                float v = speed, run = Mathf.Clamp01((v - WALK) / (RUN - WALK));
                if (v > 0.15f)
                {
                    phase += dt * (5.2f + v * 1.25f);
                    float k = Mathf.Clamp01(v / WALK), amp = k * (0.42f + run * 0.4f);
                    float sL = Mathf.Sin(phase), sR = -sL, cp = Mathf.Cos(phase);
                    Set("thighL", -amp * sL - run * 0.15f, 0, 0); Set("thighR", -amp * sR - run * 0.15f, 0, 0);
                    Set("shinL", 0.1f + Mathf.Max(0, cp) * (0.6f + run * 0.9f) * k, 0, 0);
                    Set("shinR", 0.1f + Mathf.Max(0, -cp) * (0.6f + run * 0.9f) * k, 0, 0);
                    Set("footL", -Tx("thighL") * 0.3f, 0, 0); Set("footR", -Tx("thighR") * 0.3f, 0, 0);
                    Set("upperArmL", amp * sL * 0.9f, 0, 0.08f); Set("upperArmR", amp * sR * 0.9f, 0, -0.08f);
                    Set("forearmL", -0.25f - run * 1.25f, 0, 0); Set("forearmR", -0.25f - run * 1.25f, 0, 0);
                    Set("spine", 0.04f + run * 0.18f, sL * 0.08f, 0); Set("chest", 0, -sL * 0.12f, 0);
                    Set("head", -run * 0.12f, 0, 0);
                    hy -= Mathf.Abs(cp) * (0.02f + run * 0.05f) + run * 0.04f;
                    rate = 16 + run * 10;
                    int step = Mathf.FloorToInt(phase / Mathf.PI);
                    if (step != stepIdx) { stepIdx = step; app.audio.Footstep(0.5f + run * 0.7f); }
                }
                else
                {
                    float br = Mathf.Sin(t * 1.7f);
                    Set("chest", br * 0.015f, 0, 0); Set("spine", 0, 0, Mathf.Sin(t * 0.4f) * 0.02f);
                    Set("upperArmL", 0, 0, 0.07f + br * 0.01f); Set("upperArmR", 0, 0, -0.07f - br * 0.01f);
                    Set("forearmL", -0.12f, 0, 0); Set("forearmR", -0.12f, 0, 0);
                    Set("thighL", 0, 0, 0.04f); Set("thighR", 0, 0, -0.04f);
                    Set("head", 0, Mathf.Sin(t * 0.31f) * Mathf.Sin(t * 0.13f) * 0.6f, 0);
                    rate = 6;
                }
                if (landT > 0) { landT -= dt; hy -= landT * 0.4f; AddX("shinL", landT * 2); AddX("shinR", landT * 2); AddX("thighL", -landT); AddX("thighR", -landT); }
            }
            else if (state == St.Air)
            {
                float up = vy > 0 ? 1 : 0.5f;
                Set("thighL", -0.9f * up, 0, 0); Set("shinL", 1.3f * up, 0, 0); Set("thighR", -0.3f, 0, 0); Set("shinR", 0.6f, 0, 0);
                Set("upperArmL", -0.4f, 0, 0.7f); Set("upperArmR", -0.4f, 0, -0.7f);
                Set("forearmL", -0.6f, 0, 0); Set("forearmR", -0.6f, 0, 0); Set("spine", 0.1f, 0, 0);
                rate = 14;
            }
            else if (state == St.Sit)
            {
                if (seat.kind == "squat")
                {
                    hy = SQUAT_HIPS;
                    Set("thighL", -1.75f, -0.2f, 0.38f); Set("thighR", -1.75f, 0.2f, -0.38f);
                    Set("shinL", 2.45f, 0, 0); Set("shinR", 2.45f, 0, 0); Set("footL", -0.75f, 0, 0); Set("footR", -0.75f, 0, 0);
                    Set("spine", 0.32f, 0, 0); Set("chest", 0.1f, 0, 0); Set("head", -0.35f, 0, 0);
                    Set("upperArmL", -0.75f, 0, 0.15f); Set("upperArmR", -0.75f, 0, -0.15f);
                    Set("forearmL", -0.5f, 0, 0); Set("forearmR", -0.5f, 0, 0);
                }
                else
                {
                    hy = SIT_HIPS;
                    float swing = seat.kind == "hood" ? Mathf.Sin(t * 2.1f) * 0.12f : 0;
                    Set("thighL", -1.5f, 0, 0.08f); Set("thighR", -1.5f, 0, -0.08f);
                    Set("shinL", 1.45f + swing, 0, 0); Set("shinR", 1.45f - swing, 0, 0);
                    Set("spine", -0.06f, 0, 0); Set("chest", 0.05f, 0, 0);
                    Set("upperArmL", -0.35f, 0, 0.12f); Set("upperArmR", -0.35f, 0, -0.12f);
                    Set("forearmL", -0.9f, 0, 0); Set("forearmR", -0.9f, 0, 0);
                    Set("head", 0, Mathf.Sin(t * 0.27f) * 0.5f, 0);
                }
                rate = 7;
            }
            else if (state == St.Down)
            {
                Set("upperArmL", 0, 0, 1.2f); Set("upperArmR", 0, 0, -1.2f); Set("thighL", 0, 0, 0.25f); Set("thighR", 0, 0, -0.25f);
                Set("shinL", 0.4f, 0, 0); Set("head", 0, 0.5f, 0);
                rate = 8;
            }
            if (food != null && state != St.Down) { SetX("forearmL", Mathf.Min(Tx("forearmL"), -1.35f)); if (state == St.Walk) MulX("upperArmL", 0.3f); }
            if (smoking && state != St.Down) { SetX("forearmR", Mathf.Min(Tx("forearmR"), -1.25f)); if (state == St.Walk) MulX("upperArmR", 0.3f); }
            if (whistleT > 0.15f && whistleT < 1.35f) SetX("head", -0.12f);

            float kk = 1f - Mathf.Exp(-rate * dt);
            foreach (var b in Bones)
            {
                var v = Vector3.Lerp(cur[b], T[b], kk);
                cur[b] = v;
                c.Bone(b, v.x, v.y, v.z);
            }
            hipsY = M.Damp(hipsY, hy, rate * 0.8f, dt);
            c.HipsY = hipsY;

            // IK рук: затяжка/свист — к губам; сидя — на колени
            bool toMouthR = smoking && (smokeStage == "light" || smokeStage == "drag");
            char wh = whistleT > 0.15f && whistleT < 1.35f ? whistleHand : ' ';
            bool sitHands = state == St.Sit;
            float wR = toMouthR || wh == 'R' ? 1 : sitHands ? 0.85f : 0;
            bool biteL = food != null && foodStage == "bite" && foodT > 0.1f && foodT < 0.85f;
            float wL = wh == 'L' || biteL ? 1 : sitHands && food == null ? 0.85f : 0;
            ikR = M.Damp(ikR, wR, 9, dt); ikL = M.Damp(ikL, wL, 9, dt);
            if (ikR > 0.01f)
            {
                var tg = toMouthR ? MouthTarget(0.07f, 0.085f) : wh == 'R' ? MouthTarget(0.05f, 0.075f) : KneeTarget('R');
                c.ArmIK('R', tg, ikR);
            }
            if (ikL > 0.01f)
            {
                var tg = wh == 'L' ? MouthTarget(0.05f, 0.075f) : biteL ? MouthTarget(0.07f, Shops.Items[food].drink ? 0.15f : 0.12f) : KneeTarget('L');
                c.ArmIK('L', tg, ikL);
            }
        }

        Vector3 MouthTarget(float fwd, float down) { return c.B["head"].TransformPoint(new Vector3(0, 0.06f - down, 0.11f + fwd)); }

        Vector3 KneeTarget(char side)
        {
            var o = c.B[side == 'L' ? "shinL" : "shinR"].position;
            bool squat = seat != null && seat.kind == "squat";
            o += M.Fwd(yaw) * (squat ? 0.16f : 0.02f);
            o.y += squat ? -0.12f : 0.05f;
            return o;
        }

        // ------------------------------------------------------------------ курение
        void Smoke(float dt)
        {
            if (!smoking) return;
            smokeT += dt;
            float glow = 0.45f + Mathf.Sin(t * 3) * 0.05f;
            if (smokeStage == "light")
            {
                if (smokeT > 0.55f && !lit) { lit = true; app.audio.Lighter(); }
                if (smokeT > 0.55f && smokeT < 1.3f) glow = 1.6f;
                if (smokeT > 1.6f) { smokeStage = "idle"; smokeT = 0; smokeNext = 2.5f; lit = false; exhale = 0.9f; }
            }
            else if (smokeStage == "idle")
            {
                smokeNext -= dt;
                if (smokeNext <= 0 && whistleT <= 0) { smokeStage = "drag"; smokeT = 0; }
            }
            else if (smokeStage == "drag")
            {
                if (smokeT > 0.45f && smokeT < 1.45f) { glow = 1f + Mathf.Sin(smokeT * 9) * 0.15f; if (!puffed) { puffed = true; app.audio.Inhale(); } }
                if (smokeT > 1.7f)
                {
                    smokeStage = "idle"; smokeT = 0; puffed = false; drags++;
                    smokeNext = 4 + Random.value * 3; exhale = 0.9f;
                    if (drags >= 7) { StopSmoking(false); app.hud.Toast("Сигарета докурена", HUD.Good); return; }
                }
            }
            c.emberMat.Col(new Color(1f, 0.25f + glow * 0.2f, 0.05f * glow));
            tipGlow = glow;
            var tip = c.cigTip.position;
            tipT -= dt;
            if (tipT <= 0) { tipT = 0.18f; SmokeFx.I?.Puff(tip + Vector3.up * 0.02f, new Vector3((Random.value - 0.5f) * 0.06f, 0.35f, (Random.value - 0.5f) * 0.06f), 0.05f, 0.22f, 1.6f); }
            if (exhale > 0)
            {
                exhale -= dt;
                if (exhale < 0.6f && exhale > 0)
                {
                    var m = c.mouth.position; var f = M.Fwd(yaw);
                    SmokeFx.I?.Puff(m, f * 0.6f + new Vector3((Random.value - 0.5f) * 0.2f, 0.1f, (Random.value - 0.5f) * 0.2f), 0.06f, 0.24f, 1.5f);
                    if (Random.value < 0.2f) app.audio.Exhale();
                }
            }
        }

        // ------------------------------------------------------------------ еда
        void EatTick(float dt)
        {
            if (food == null) return;
            foodT += dt;
            var it = Shops.Items[food];
            if (foodStage == "hold" && foodT > (bites > 0 ? 2.2f : 0.9f) && whistleT <= 0) { foodStage = "bite"; foodT = 0; foodSounded = false; }
            if (foodStage == "bite")
            {
                if (!foodSounded && foodT > 0.45f)
                {
                    foodSounded = true;
                    if (it.drink) app.audio.Gulp(); else app.audio.Bite(food == "chips" || food == "seeds");
                    if (!it.drink && foodObj) { var s = foodObj.transform.localScale; s.y *= 0.8f; foodObj.transform.localScale = s; }
                }
                if (foodT > 1f)
                {
                    bites++; foodStage = "hold"; foodT = 0;
                    if (bites >= it.bites) { DropFood(); app.hud.Toast("«" + Shops.Yum() + "»", HUD.Good, 1.8f); }
                }
            }
        }

        // ------------------------------------------------------------------ свист
        void Whistle(float dt)
        {
            if (whistleT <= 0) return;
            whistleT -= dt;
            if (!whistled && whistleT < 1.2f)
            {
                whistled = true;
                app.audio.Whistle();
                app.crowd?.OnWhistle(Pos);
                foreach (var car in app.traffic.cars)
                {
                    float d = Vector2.Distance(new Vector2(car.x, car.z), new Vector2(Pos.x, Pos.z));
                    if (car.active && d < 30f && Random.value < 0.5f) { float v = Mathf.Max(0.2f, 1f - d / 40f); app.Delay(0.5f + Random.value * 0.6f, () => app.audio.AiHorn(v, 0)); }
                }
            }
        }

        /// <summary>Огонёк сигареты и вспышка зажигалки.</summary>
        public void RenderGlow(Glow glow)
        {
            if (!Active || !smoking) return;
            glow.Add(c.cigTip.position, new Color(1f, 0.35f, 0.08f), 0.05f + tipGlow * 0.08f);
            if (smokeStage == "light" && smokeT > 0.55f && smokeT < 1.3f) glow.Add(c.mouth.position + Vector3.down * 0.02f, new Color(1f, 0.75f, 0.3f), 0.35f);
        }

        public bool Sitting => state == St.Sit;
    }
}
