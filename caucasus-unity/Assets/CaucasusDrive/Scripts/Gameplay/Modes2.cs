using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    // ===================================================================== свободная езда
    /// <summary>
    /// Свободная езда (порт FreeRideMode.js): такси-«бомбила», АЗС (кнопка «ЗАПРАВИТЬ»), камеры «Стрелка» и посты ДПС
    /// со штрафами (можно выключить при входе), дрифт с судейством (на автодроме ДОСААФ ×1,5), «шашки» — обгоны
    /// впритирку идут бонусом в ту же серию, рекорд серии в городе. Можно выйти из машины и гулять.
    /// </summary>
    public class FreeRideMode : GameMode
    {
        public override string Name => "free";
        public override bool Restartable => false;
        public override bool AllowWalk => pursuit == null || !pursuit.active;
        static readonly string[] Hello = { "Шеф, до ДК подбросишь?", "Только не гони, у меня рассада!", "На «Жигулях»? Ну давай, с ветерком!", "Командир, свободен? Опаздываю!", "А музыку можно погромче?", "Мне к тёще, но можно не торопиться…", "Поехали! Как Гагарин говорил.", "Главное — довези целым." };
        static readonly string[] Ouch = { "Эй, аккуратнее!", "Я на такое не подписывался!", "У меня яйца в сумке!", "Шеф, ты права где купил?" };
        static readonly string[] Bye = { "Спасибо, шеф! Сдачи не надо.", "Довёз как короля!", "Держи на бензин.", "Отлично доехали!" };
        const float FuelPrice = 56f, SEG = CityC.SPACING - 2 * CityC.HALF;

        readonly bool fines;
        Pursuit pursuit;
        readonly DriftScore drift = new DriftScore();
        readonly Rng rnd = new Rng(System.Environment.TickCount & 0xffff);
        readonly Dictionary<int, float> camCool = new Dictionary<int, float>();
        readonly Dictionary<TrafficCar, float> prevAlong = new Dictionary<TrafficCar, float>();
        float nearT, near = 99f, dpsCool, evacT = -1;
        bool inZone, lowFuelWarned;
        protected bool atPump;
        // такси
        string taxi = "off"; float taxiT, rideTime, rideLimit, rideDist; int rating; Vector3 spot, dest;
        Character ped;

        public FreeRideMode(App a, bool fines) : base(a) { this.fines = fines; }

        public override void Enter()
        {
            pursuit = new Pursuit(app);
            app.traffic.enabled = true;
            app.traffic.target = app.quality.traffic;
            app.traffic.Clear();
            float x = CityC.Coord(3) + CityC.LANES[1], z = CityC.Coord(2) + CityC.HALF + 20f;
            app.player.Place(x, z, 0f);
            app.cameraRig.Snap();
            app.traffic.center = new Vector3(x, 0, z);
            app.traffic.Prefill(app.cam);
            drift.onEvent = (t) => { app.hud.DriftTag(t, 1.2f); app.audio.Beep(t == "БЛИЗКО!" ? 1500 : 1100 + drift.mult * 120, 0.07f, 0.1f, Wave.Tri); };
            drift.onBank = (total, mult) =>
            {
                var st = app.save.d;
                int money = Mathf.RoundToInt(total / 5f);
                bool record = total > st.freeBest;
                if (record) st.freeBest = total;
                if (total > st.stats.bestCombo) st.stats.bestCombo = total;
                if (total > st.drift.bestSeries) st.drift.bestSeries = total;
                if (money > 0) app.save.AddMoney(money); else app.save.Commit();
                app.daily.Progress("drift", total);
                app.hud.DriftTag((record ? "РЕКОРД! " : "ИТОГ ") + Fmt(total) + (money > 0 ? " · +" + M.Rub(money) : ""), record ? 3.5f : 2.5f);
                if (record) app.audio.Success(); else app.audio.Coin();
            };
            drift.onLost = (reason, pts) => { app.hud.DriftTag(reason.ToUpper() + ": −" + Fmt(pts), 2f); app.audio.Beep(300, 0.18f, 0.12f, Wave.Saw); };
            app.rules.Reset();
            app.rules.onViolation = Violation;
            app.crowd.Start();
            app.SetWeather(Random.value < 0.3f ? 1f : 0f, true);
            app.hud.ShowTaxi(true);
            app.hud.ShowLimit(true);
            app.hud.Toast("Свободная езда" + (fines ? "" : " без штрафов") + ". Нажми «ТАКСИ», чтобы брать заказы", HUD.Good, 3.5f);
        }

        static string Fmt(int v) { return v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " "); }

        public override void Exit()
        {
            pursuit?.Exit();
            app.crowd.Stop();
            app.rules.onViolation = null;
            EndTaxi(true);
            drift.Bank();
            app.hud.Drift(false, 0, 1, 0);
            app.hud.ShowTaxi(false);
            app.hud.ShowLimit(false);
            app.hud.Actions(null, null, null);
            app.hud.Mission(null, null, 0);
            if (ped != null) { Object.Destroy(ped.root.gameObject); ped = null; }
        }

        // ------------------------------------------------------------------ штрафы
        bool NearDps(Vector3 p, float r = 90f)
        {
            foreach (var d in app.city.dpsPosts) if (Vector2.Distance(new Vector2(d.pos.x, d.pos.z), new Vector2(p.x, p.z)) < r) return true;
            return false;
        }

        void Fine(int amount, string text, bool camera)
        {
            if (!fines) return;
            app.save.d.money = Mathf.Max(0, app.save.d.money - amount);
            app.save.d.stats.fines += amount;
            app.save.Commit();
            if (camera) { app.hud.Flash(); app.audio.Shutter(); } else app.audio.Siren();
            app.hud.Toast((camera ? "ФОТО: " : "ДПС: ") + text + " — штраф " + M.Rub(amount), HUD.Bad, 3.5f);
        }

        void Violation(Violation v)
        {
            if (v == CaucasusDrive.Violation.Red) { Fine(v.fine, "камера «Безопасный город», проезд на красный", true); return; }
            if (v == CaucasusDrive.Violation.Speed) return; // скорость ловят камеры и посты
            if (NearDps(app.player.Position)) Fine(v.fine, "инспектор: " + v.text.ToLower(), false);
        }

        void Cameras(float dt)
        {
            var p = app.player.Position; float kmh = app.player.Speed * 3.6f;
            var keys = new List<int>(camCool.Keys);
            foreach (var k in keys) { camCool[k] -= dt; if (camCool[k] <= 0) camCool.Remove(k); }
            for (int i = 0; i < app.city.cameras.Count; i++)
            {
                if (camCool.ContainsKey(i)) continue;
                var c = app.city.cameras[i];
                if (Vector2.Distance(new Vector2(c.pos.x, c.pos.z), new Vector2(p.x, p.z)) > 22f) continue;
                if (kmh > 80f)
                {
                    float over = kmh - 60f;
                    Fine(over > 60 ? 2500 : over > 40 ? 1500 : 500, "камера «Стрелка», " + Mathf.RoundToInt(kmh) + " км/ч при 60", true);
                    if (fines && kmh > 115f && Random.value < 0.75f) pursuit?.Start("Превышение " + Mathf.RoundToInt(kmh) + " км/ч");
                    camCool[i] = 10f;
                }
            }
            dpsCool -= dt;
            if (kmh > 90f && dpsCool <= 0 && NearDps(p, 60f)) { Fine(1500, "превышение " + Mathf.RoundToInt(kmh) + " км/ч", false); dpsCool = 12f; if (fines && kmh > 110f) pursuit?.Start("Превышение " + Mathf.RoundToInt(kmh) + " км/ч"); }
        }

        // ------------------------------------------------------------------ топливо и АЗС
        void Fuel(float dt)
        {
            var p = app.player.phys;
            float k = p.fuel / p.spec.tank;
            if (k < 0.15f && !lowFuelWarned) { lowFuelWarned = true; app.hud.Toast("Мало бензина! АЗС отмечена на карте", HUD.Bad, 3f); }
            if (k > 0.3f) lowFuelWarned = false;
            if (p.fuel <= 0 && p.speed < 0.2f && evacT < 0) evacT = 1.5f;
            if (evacT >= 0) { evacT -= dt; if (evacT < 0) { app.save.d.money = Mathf.Max(0, app.save.d.money - 1500); app.save.Commit(); p.fuel = 6; app.hud.Toast("Бензин кончился. Знакомый привёз канистру: −1 500 ₽", HUD.Bad, 3.5f); } }
            var az = app.city.azs;
            atPump = !app.onFoot && az != Vector3.zero && p.speed < 0.5f && Vector3.Distance(app.player.Position, az) < app.city.azsR;
            float need = p.spec.tank - p.fuel;
            var t = app.player.tune;
            int rep = CarCare.RepairPrice(t);
            app.hud.Actions(atPump && need > 0.5f ? "ЗАПРАВИТЬ · " + M.Rub(Mathf.CeilToInt(need * FuelPrice)) : null,
                atPump && rep > 0 ? "РЕМОНТ · " + M.Rub(rep) : null,
                atPump && t.dirt > 0.05f ? "МОЙКА · " + M.Rub(CarCare.WashPrice) : null);
            if (k < 0.15f && taxi == "off") app.hud.Nav(az);
        }

        public override void OnAction(int i)
        {
            if (!atPump) return;
            if (i == 1) { if (CarCare.Repair(app, app.player.tune)) app.hud.Toast("Машину подлатали — как новая", HUD.Good); return; }
            if (i == 2)
            {
                if (!CarCare.Wash(app, app.player.tune)) return;
                app.RefreshLook();
                var c = app.player.Position;
                for (int k = 0; k < 30; k++) SmokeFx.I?.Puff(c + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.3f, 1.5f), Random.Range(-2.4f, 2.4f)), Vector3.up * 0.3f, 0.35f, 0.5f, 1.6f, 1f);
                app.hud.Toast("Помыли до блеска!", HUD.Good);
                return;
            }
            var p = app.player.phys;
            float need = p.spec.tank - p.fuel;
            float afford = Mathf.Min(need, app.save.Money / FuelPrice);
            if (afford < 0.5f) { app.hud.Toast("Не хватает денег на бензин", HUD.Bad); return; }
            app.save.Spend(Mathf.CeilToInt(afford * FuelPrice));
            p.fuel += afford;
            app.daily.Progress("fuel", 1);
            app.audio.Coin();
            app.hud.Toast("Заправлено " + afford.ToString("0.0") + " л АИ-92", HUD.Good);
        }

        // ------------------------------------------------------------------ такси
        public override void OnTaxi()
        {
            if (taxi != "off") { EndTaxi(false); return; }
            taxi = "wait"; taxiT = 2 + rnd.Next() * 3;
            app.player.SetTaxiSign(true);
            app.hud.TaxiOn(true);
            app.hud.Toast("Шашечки на крыше. Ждём заказ…", HUD.Good);
        }

        void EndTaxi(bool silent)
        {
            if (taxi == "off") return;
            taxi = "off";
            app.player.SetTaxiSign(false);
            app.beacon.Hide();
            if (ped != null) ped.SetVisible(false);
            app.hud.Mission(null, null, 0);
            app.hud.TaxiOn(false);
            if (!silent) app.hud.Toast("Смена окончена", HUD.Good);
        }

        /// <summary>Случайная точка на тротуаре у дороги: (тротуар, точка на полосе для остановки).</summary>
        bool SidewalkSpot(Vector3 near, float minD, float maxD, out Vector3 walk, out Vector3 road)
        {
            walk = road = Vector3.zero;
            for (int k = 0; k < 40; k++)
            {
                int i = rnd.Range(CityC.N), j = rnd.Range(CityC.N);
                bool horiz = rnd.Next() < 0.5f;
                if ((horiz && i >= CityC.N - 1) || (!horiz && j >= CityC.N - 1)) continue;
                float u = 20 + rnd.Next() * (SEG - 40);
                float side = rnd.Next() < 0.5f ? 1 : -1;
                float x = horiz ? CityC.Coord(i) + CityC.HALF + u : CityC.Coord(i) + side * (CityC.HALF + 1.6f);
                float z = horiz ? CityC.Coord(j) + side * (CityC.HALF + 1.6f) : CityC.Coord(j) + CityC.HALF + u;
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(near.x, near.z));
                if (d < minD || d > maxD) continue;
                walk = new Vector3(x, CityC.CURB, z);
                road = horiz ? new Vector3(x, 0, CityC.Coord(j) + side * 5.2f) : new Vector3(CityC.Coord(i) + side * 5.2f, 0, z);
                return true;
            }
            return false;
        }

        void Taxi(float dt)
        {
            if (taxi == "off") return;
            var pos = app.player.Position; float sp = app.player.Speed;
            taxiT -= dt;
            if (taxi == "wait")
            {
                app.hud.Mission("Такси", "Ждём заказ…", 0);
                if (taxiT > 0) return;
                Vector3 w, r;
                if (!SidewalkSpot(pos, 120, 380, out w, out r)) { taxiT = 1; return; }
                taxi = "pickup"; spot = r;
                app.beacon.Show(r, 0xffcc33);
                if (ped == null) ped = new Character(new Outfit { suit = 0x2d4a7a, stripe = 0xf2f2f2, pants = 0x23252b, shoes = 0x1a1a1a, sole = 0xdddddd, skin = 0xd9a57f, hair = 0x5a3a22 }, app.worldRoot, false);
                ped.SetVisible(true);
                ped.root.SetPositionAndRotation(w, M.Yaw(Mathf.Atan2(r.x - w.x, r.z - w.z)));
                ped.Bone("upperArmR", -2.6f, 0, -0.2f);    // голосует
                app.audio.Beep(1200, 0.08f); app.audio.Beep(1600, 0.08f, 0.12f, Wave.Square, 0.1f);
                app.hud.Toast("Новый заказ! Забери пассажира", HUD.Good);
            }
            else if (taxi == "pickup")
            {
                float d = Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(pos.x, pos.z));
                app.hud.Mission("Такси · посадка", "Пассажир ждёт: " + Mathf.RoundToInt(d) + " м. Остановитесь у метки", 0);
                app.hud.Nav(spot);
                if (ped != null) ped.Bone("upperArmR", -2.6f + Mathf.Sin(Time.time * 6f) * 0.25f, 0, -0.2f);
                if (d < 6 && sp < 1)
                {
                    Vector3 w, r;
                    if (!SidewalkSpot(pos, 300, 850, out w, out r)) return;
                    taxi = "ride"; dest = r;
                    rideDist = Vector3.Distance(r, pos); rideLimit = rideDist / 7.5f + 25; rideTime = 0; rating = 5;
                    if (ped != null) ped.SetVisible(false);
                    app.audio.Door();
                    app.beacon.Show(r, 0x35e07a);
                    app.hud.Toast("«" + Hello[rnd.Range(Hello.Length)] + "»", HUD.Good, 3.5f);
                }
            }
            else if (taxi == "ride")
            {
                rideTime += dt;
                float d = Vector2.Distance(new Vector2(dest.x, dest.z), new Vector2(pos.x, pos.z));
                float left = Mathf.Max(0, rideLimit - rideTime);
                app.hud.Mission("Такси · везём пассажира", Mathf.RoundToInt(d) + " м · осталось " + Mathf.CeilToInt(left) + " с · " + new string('★', rating) + new string('☆', 5 - rating), left / rideLimit);
                app.hud.Nav(dest);
                if (d < 6 && sp < 1)
                {
                    float fare = 150 + rideDist * 0.9f;
                    if (rideTime < rideLimit * 0.7f) fare *= 1.3f;
                    if (rideTime > rideLimit) fare *= 0.5f;
                    int f = Mathf.RoundToInt(fare * rating / 5f);
                    app.save.d.stats.taxi++;
                    app.save.AddMoney(f);
                    app.daily.Progress("taxi", 1);
                    app.audio.Coin(); app.audio.Door();
                    app.hud.Toast("«" + Bye[rnd.Range(Bye.Length)] + "» +" + M.Rub(f), HUD.Good, 3.5f);
                    app.beacon.Hide();
                    taxi = "wait"; taxiT = 3 + rnd.Next() * 4;
                }
            }
        }

        // ------------------------------------------------------------------ «шашки»
        void Overtakes()
        {
            if (app.onFoot) { prevAlong.Clear(); return; }
            var p = app.player.Position; var f = M.Fwd(app.player.Heading); var r = M.Right(app.player.Heading);
            float kmh = app.player.Speed * 3.6f;
            foreach (var car in app.traffic.cars)
            {
                float dx = car.x - p.x, dz = car.z - p.z;
                if (!car.active || Mathf.Abs(dx) > 30 || Mathf.Abs(dz) > 30) { prevAlong.Remove(car); continue; }
                float along = dx * f.x + dz * f.z, side = dx * r.x + dz * r.z;
                float prev;
                if (prevAlong.TryGetValue(car, out prev) && prev > 0 && along <= 0 && Mathf.Abs(side) < 3.1f && kmh > 60f && !drift.active)
                {
                    float pts = Mathf.Round((3.1f - Mathf.Abs(side)) * 80 + kmh);
                    drift.Bonus(pts, Mathf.Abs(side) < 2.2f ? "ВПРИТИРКУ!" : "ШАШКИ");
                }
                prevAlong[car] = along;
            }
        }

        public override void OnCrash(float strength, string kind, Collider col)
        {
            if (strength < 0.08f) return;
            app.Crash(strength);
            app.save.d.stats.crashes++;
            drift.Lose("Удар");
            if (taxi == "ride" && strength > 0.15f) { rating = Mathf.Max(1, rating - 1); app.hud.Toast("«" + Ouch[rnd.Range(Ouch.Length)] + "»", HUD.Bad); }
        }

        public override void OnPedHit(float speed)
        {
            drift.Lose("Пешеход");
            if (fines) { Fine(5000, "наезд на пешехода", false); pursuit?.Start("Наезд на пешехода"); }
            else app.hud.Toast("Пешеход! Аккуратнее, он еле увернулся", HUD.Bad, 2.5f);
        }

        public override void Update(float dt)
        {
            Cameras(dt);
            pursuit?.Update(dt);
            Fuel(dt);
            Taxi(dt);
            Overtakes();
            var p = app.player.phys; var pos = app.player.Position;
            bool zone = pos.x > CityC.AutoX0 && pos.x < CityC.AutoX1 && pos.z > CityC.AutoZ0 && pos.z < CityC.AutoZ1;
            if (zone != inZone) { inZone = zone; if (zone) app.hud.Toast("Дрифт-зона ДОСААФ: очки дрифта ×1,5", HUD.Good, 2.5f); }
            if (app.onFoot) drift.Bank();
            else
            {
                if (drift.active) { nearT -= dt; if (nearT <= 0) { nearT = 0.1f; near = DriftScore.NearestObstacle(app.player); } } else near = 99f;
                drift.Update(dt, p, app.player.surface.type == 2, near, zone ? 1.5f : 1f);
            }
            app.hud.Drift(drift.InSeries, drift.Total, drift.mult, drift.active ? drift.angle : 0, app.save.d.freeBest);
            app.beacon.Update(dt);
            if (taxi == "off" && !atPump && p.fuel >= p.spec.tank * 0.15f && !(pursuit != null && pursuit.active)) app.hud.Mission(null, null, 0);
        }

        public override void RenderGlow(Glow glow, bool blink)
        {
            pursuit?.RenderGlow(glow, blink);
        }
    }

    // ===================================================================== экзамен ГИБДД
    /// <summary>
    /// Экзамен ГИБДД (порт ExamMode.js): этап 1 — площадка (змейка, параллельная парковка, гараж задом), этап 2 —
    /// город по маршруту инструктора с учётом ПДД. 5 штрафных баллов — не сдал. Первая сдача — права и 10 000 ₽.
    /// </summary>
    public class ExamMode : GameMode
    {
        public override string Name => "exam";
        const int MaxPts = 5;
        readonly Rng rnd = new Rng(System.Environment.TickCount & 0xffff);
        int pts; readonly List<string> log = new List<string>();
        string stage;
        Level[] ex; int exIndex; float exTime;
        LevelScene scene;
        readonly HashSet<string> touched = new HashSet<string>();
        List<int> route; int ri; Vector3 finish; bool hasFinish;

        public ExamMode(App a) : base(a) { }

        public override void Enter()
        {
            app.traffic.enabled = false; app.traffic.Clear();
            pts = 0; log.Clear(); stage = "ground";
            ex = new[] { Levels.Slalom(0.3f, 501), Levels.Parallel(0.35f, 502), Levels.Garage(0.3f, true, 503) };
            for (int i = 0; i < ex.Length; i++) { ex[i].index = 100 + i; }
            exIndex = 0;
            Load(true);
            app.hud.ShowLimit(false);
            app.hud.Toast("Инструктор: «Пристегнулись? Зеркала настроили? Начинаем с площадки.»", HUD.Good, 4f);
            app.rules.Reset();
            app.rules.onViolation = (v) => { if (stage == "city") Penalty(v.pts, v.text); };
            app.rules.onTurn = OnTurn;
        }

        public override void Exit()
        {
            scene?.Destroy(); scene = null;
            app.crowd.Stop();
            app.rules.onViolation = null; app.rules.onTurn = null;
            app.beacon.Hide();
            app.hud.Mission(null, null, 0);
            app.hud.ShowLimit(false);
        }

        void Load(bool first)
        {
            scene?.Destroy();
            var L = ex[exIndex];
            scene = new LevelScene(app.worldRoot, L);
            if (first) { app.player.Place(L.start.x + CityC.AutoCX, L.start.y + CityC.AutoCZ, L.start.z); app.player.phys.fuel = app.player.phys.spec.tank; app.cameraRig.Snap(); }
            exTime = 0; touched.Clear();
        }

        void Penalty(int p, string text)
        {
            if (stage == "done") return;
            pts += p;
            log.Add(text + " (+" + p + ")");
            app.audio.Beep(300, 0.25f, 0.15f, Wave.Saw);
            app.hud.Toast("Инструктор: «" + text + "!» +" + p + (p == 1 ? " балл" : p < 5 ? " балла" : " баллов"), HUD.Bad, 3f);
            if (pts >= MaxPts) Finish(false);
        }

        public override void OnCrash(float strength, string kind, Collider col)
        {
            if (stage == "done") return;
            if (strength > 0.08f) app.Crash(strength);
            var o = col ? col.GetComponent<Obstacle>() : null;
            string key = kind == "cone" && o ? "c" + o.index : kind == "car" ? "car" + (col ? col.GetInstanceID() : 0) : kind;
            if (!touched.Add(key)) return;
            if (kind == "cone") { if (o && scene != null) scene.KnockCone(o.index, app.player.Position); Penalty(3, "Сбит конус"); }
            else Penalty(5, "Столкновение");
        }

        List<int> MakeRoute()
        {
            var g = app.graph;
            int a = -1, b = g.Id(0, 3);
            var path = new List<int> { b };
            int prev = g.Id(0, 3) - 1;
            for (int k = 0; k < 7; k++)
            {
                int next = g.PickNext(b, a >= 0 ? a : prev, rnd);
                if (path.Contains(next)) break;
                path.Add(next); a = b; b = next;
            }
            return path;
        }

        void StartCity()
        {
            stage = "city";
            scene?.Destroy(); scene = null;
            app.traffic.enabled = true;
            app.traffic.target = Mathf.Max(8, Mathf.RoundToInt(app.quality.traffic * 0.7f));
            app.crowd.Start();
            app.traffic.Prefill(app.cam);
            app.hud.ShowLimit(true);
            route = MakeRoute(); ri = 0; hasFinish = false;
            app.hud.Toast("Инструктор: «Площадка сдана. Теперь — в город. Выезжайте через ворота.»", HUD.Good, 4f);
            ShowNext();
        }

        RoadNode Node(int id) { return app.graph.nodes[id]; }

        void ShowNext()
        {
            if (ri >= route.Count)
            {
                var a = Node(route.Count >= 2 ? route[route.Count - 2] : route[0]); var b = Node(route[route.Count - 1]);
                float dx = Mathf.Sign(b.x - a.x), dz = Mathf.Sign(b.z - a.z);
                if (b.x == a.x) dx = 0; if (b.z == a.z) dz = 0; if (dx == 0 && dz == 0) dz = 1;
                // 40 м прямо за последним перекрёстком по правой полосе (справа от курса: (dz, −dx))
                finish = new Vector3(b.x + dx * 40 + dz * CityC.LANES[1], 0, b.z + dz * 40 - dx * CityC.LANES[1]);
                hasFinish = true;
                app.beacon.Show(finish, 0x35e07a);
                return;
            }
            var n = Node(route[ri]);
            app.beacon.Show(new Vector3(n.x, 0, n.z), 0xffcc33);
        }

        string Instruction()
        {
            var p = app.player.Position;
            if (ri == 0) return "Выезжайте с автодрома и езжайте к перекрёстку";
            if (ri >= route.Count) return "Остановитесь у зелёной метки";
            var prev = Node(route[ri - 1]); var cur = Node(route[ri]);
            int d = Mathf.RoundToInt(Vector2.Distance(new Vector2(cur.x, cur.z), new Vector2(p.x, p.z)));
            if (ri + 1 >= route.Count) return "Через " + d + " м — прямо, затем остановка";
            var t = app.graph.TurnType(prev.id, cur.id, route[ri + 1]);
            return "Через " + d + " м — " + (t == Turn.Right ? "направо" : t == Turn.Left ? "налево" : "прямо");
        }

        void OnTurn(char side, int node)
        {
            if (stage != "city" || ri == 0 || ri >= route.Count) return;
            var cur = Node(route[ri]);
            if (node != cur.id) return;
            if (ri + 1 >= route.Count) { ri++; ShowNext(); return; }
            var prev = Node(route[ri - 1]);
            var t = app.graph.TurnType(prev.id, cur.id, route[ri + 1]);
            char expect = t == Turn.Right ? 'R' : t == Turn.Left ? 'L' : 'S';
            if (side != expect)
            {
                Penalty(2, "Не выполнили указание инструктора");
                var f = M.Fwd(app.player.Heading);
                int ni = cur.i + Mathf.RoundToInt(f.x), nj = cur.j + Mathf.RoundToInt(f.z);
                if (ni >= 0 && nj >= 0 && ni < CityC.N && nj < CityC.N)
                {
                    int rest = route.Count - ri - 1;
                    route = route.GetRange(0, ri + 1);
                    route.Add(app.graph.Id(ni, nj));
                    int a = cur.id, b = app.graph.Id(ni, nj);
                    for (int k = 1; k < rest; k++) { int c = app.graph.PickNext(b, a, rnd); route.Add(c); a = b; b = c; }
                }
            }
            ri++;
            ShowNext();
        }

        void Finish(bool ok)
        {
            stage = "done";
            app.inputLocked = true;
            app.beacon.Hide();
            if (ok)
            {
                bool first = !app.save.d.license;
                app.save.d.license = true;
                int reward = first ? 10000 : 2000;
                app.save.AddMoney(reward);
                app.audio.Success();
                app.hud.Toast("Инструктор: «Поздравляю, права ваши. Только не гоняйте!»", HUD.Good, 4f);
                app.Delay(1.2f, () => app.ShowResult(new Result { ok = true, exam = true, reward = reward, log = new List<string>(log), pts = pts }));
            }
            else
            {
                app.audio.Fail();
                app.hud.Toast("Инструктор: «Экзамен не сдан. Приходите через неделю.»", HUD.Bad, 4f);
                app.Delay(1.2f, () => app.ShowResult(new Result { ok = false, exam = true, why = "Набрано 5 штрафных баллов", log = new List<string>(log), pts = pts }));
            }
        }

        public override void Update(float dt)
        {
            app.beacon.Update(dt);
            var p = app.player.Position;
            if (stage == "ground")
            {
                exTime += dt;
                var L = ex[exIndex];
                int st = scene.Check(app.player);
                if (st == 1 && app.player.Speed < 0.3f) scene.hold += dt; else scene.hold = 0;
                scene.SetZoneState(st, Mathf.Min(1f, scene.hold / 1.2f), exTime);
                app.hud.Mission("Экзамен · площадка " + (exIndex + 1) + "/3 · " + L.type, (st == -1 ? "Не той стороной!" : L.desc) + " · баллы: " + pts + "/" + MaxPts, scene.hold / 1.2f);
                app.hud.Nav(new Vector3(scene.target.x, 0, scene.target.z));
                if (exTime > 150) { Penalty(5, "Время на упражнение вышло"); return; }
                if (scene.hold >= 1.2f)
                {
                    app.audio.Beep(1300, 0.1f); app.audio.Beep(1700, 0.12f, 0.12f, Wave.Square, 0.12f);
                    exIndex++;
                    if (exIndex >= ex.Length) StartCity();
                    else { app.hud.Toast("Упражнение выполнено! Следующее — по стрелке", HUD.Good); Load(false); }
                }
                return;
            }
            if (stage != "city") return;
            if (ri == 0)
            {
                var n0 = Node(route[0]);
                if (Vector2.Distance(new Vector2(n0.x, n0.z), new Vector2(p.x, p.z)) < CityC.HALF + 2) { ri = 1; ShowNext(); }
            }
            if (ri >= route.Count && hasFinish)
            {
                if (Vector2.Distance(new Vector2(finish.x, finish.z), new Vector2(p.x, p.z)) < 6 && app.player.Speed < 0.5f) { Finish(true); return; }
                app.hud.Nav(finish);
            }
            else if (ri < route.Count) { var n = Node(route[ri]); app.hud.Nav(new Vector3(n.x, 0, n.z)); }
            app.hud.Mission("Экзамен · город", Instruction() + " · баллы: " + pts + "/" + MaxPts, 0);
        }
    }

    // ===================================================================== дрифт-зона ДОСААФ
    /// <summary>
    /// Дрифт-зона (порт DriftMode.js): заезд 90 секунд по «восьмёрке» вокруг двух островов из конусов, вдоль южной
    /// стороны — линия конусов для клиппинга. Сбитый конус или удар сжигает серию. Итог — сумма серий, рекорд,
    /// награда: итог / 4 и +1 000 ₽ за рекорд.
    /// </summary>
    public class DriftMode : GameMode
    {
        public override string Name => "drift";
        const float RUN = 90f, ISLAND_R = 8f;
        public const int ConeLayer = 9;
        readonly DriftScore drift = new DriftScore();
        readonly List<GameObject> cones = new List<GameObject>();
        Transform root;
        int total, series, best; float time, nearT, near = 99f; bool started, done;
        static Mesh coneMesh;

        public DriftMode(App a) : base(a) { }

        public override void Enter()
        {
            app.traffic.enabled = false; app.traffic.Clear();
            root = new GameObject("DriftZone").transform; root.SetParent(app.worldRoot, false);
            Physics.IgnoreLayerCollision(2, ConeLayer, true);   // машина проходит сквозь конус — он падает
            if (coneMesh == null)
            {
                var cb = new MeshBuilder();
                cb.CylinderY(Vector3.zero, 0.2f, 0.03f, 0.7f, 10, false);
                cb.Box(new Vector3(0, 0.02f, 0), new Vector3(0.44f, 0.04f, 0.44f), 0, true);
                coneMesh = cb.ToMesh("DriftCone", true);
            }
            var mat = Mats.Simple().Col(M.Hex(0xff5a14));
            var pts = new List<Vector2>();
            float cx = CityC.AutoCX, cz = CityC.AutoCZ;
            foreach (int ix in new[] { -1, 1 })
                for (int k = 0; k < 14; k++) { float a = k / 14f * Mathf.PI * 2; pts.Add(new Vector2(cx + ix * 38 + Mathf.Cos(a) * ISLAND_R, cz - 18 + Mathf.Sin(a) * ISLAND_R)); }
            for (float x = cx - 60; x <= cx + 60; x += 5) pts.Add(new Vector2(x, CityC.AutoZ0 + 14));
            foreach (var q in pts)
            {
                var go = new GameObject("cone"); go.transform.SetParent(root, false);
                go.transform.position = new Vector3(q.x, 0, q.y);
                go.AddComponent<MeshFilter>().sharedMesh = coneMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.layer = ConeLayer;
                var cap = go.AddComponent<CapsuleCollider>(); cap.radius = 0.24f; cap.height = 0.8f; cap.center = new Vector3(0, 0.4f, 0);
                cones.Add(go);
            }
            app.player.Place(cx + 10, cz + 22, -Mathf.PI / 2);
            app.player.phys.fuel = app.player.phys.spec.tank;
            app.cameraRig.Snap();
            drift.onEvent = (t) => { app.hud.DriftTag(t, 1.2f); app.audio.Beep(t == "БЛИЗКО!" ? 1500 : 1100 + drift.mult * 120, 0.07f, 0.1f, Wave.Tri); };
            drift.onBank = (tot, m) =>
            {
                total += tot; series++;
                app.daily.Progress("drift", tot);
                var st = app.save.d;
                if (tot > st.drift.bestSeries) st.drift.bestSeries = tot;
                if (tot > st.stats.bestCombo) st.stats.bestCombo = tot;
                app.hud.DriftTag("ИТОГ " + tot, 2.5f); app.audio.Coin();
            };
            drift.onLost = (reason, p) => { app.hud.DriftTag(reason.ToUpper() + ": −" + p, 2f); app.audio.Beep(300, 0.18f, 0.12f, Wave.Saw); };
            total = 0; series = 0; time = RUN; started = false; done = false;
            best = app.save.d.drift.best;
            app.hud.Toast("Дрифт-зона ДОСААФ: 90 секунд. Ручник или перегазовка — срыв, газ и контрруль — удержание", HUD.Good, 4f);
        }

        public override void Exit()
        {
            if (root) Object.Destroy(root.gameObject);
            cones.Clear();
            app.hud.Drift(false, 0, 1, 0);
            app.hud.Mission(null, null, 0);
        }

        public override void OnCrash(float strength, string kind, Collider col)
        {
            if (strength < 0.08f) return;
            app.Crash(strength);
            drift.Lose("Удар");
        }

        void Cones()
        {
            var center = app.player.Position;
            foreach (var c in cones)
            {
                if (c.GetComponent<Rigidbody>() != null) continue;
                var p = c.transform.position;
                if ((p - center).sqrMagnitude > 16) continue;
                // внутри прямоугольника кузова (+ радиус конуса)?
                var t = app.player.go.transform; var lp = t.InverseTransformPoint(p); var d = app.player.def.dims;
                if (Mathf.Abs(lp.x) < d.W / 2 + 0.24f && lp.z < d.front + 0.24f && lp.z > d.rear - 0.24f)
                {
                    Object.Destroy(c.GetComponent<Collider>());
                    var rb = c.AddComponent<Rigidbody>(); rb.mass = 3;
                    var dir = (p - center); dir.y = 0;
                    rb.AddForce(dir.normalized * 30f + app.player.Velocity * 3f + Vector3.up * 15f, ForceMode.Impulse);
                    Object.Destroy(c, 6f);
                    app.audio.Beep(500, 0.06f, 0.08f);
                    drift.Lose("Конус");
                }
            }
        }

        public override void Update(float dt)
        {
            if (done) return;
            var p = app.player.phys;
            if (!started && p.speed > 3) started = true;
            if (started) time -= dt;
            Cones();
            if (drift.active) { nearT -= dt; if (nearT <= 0) { nearT = 0.1f; near = DriftScore.NearestObstacle(app.player); } } else near = 99f;
            drift.Update(dt, p, app.player.surface.type == 2, near);
            app.hud.Drift(drift.InSeries, drift.Total, drift.mult, drift.active ? drift.angle : 0, best);
            int tt = Mathf.Max(0, Mathf.CeilToInt(time));
            app.hud.Mission("Дрифт-зона ДОСААФ", (started ? (tt / 60) + ":" + (tt % 60).ToString("00") : "Старт — по газу") + " · очки " + (total + drift.Total) + " · рекорд " + best, time / RUN);
            if (time <= 0) Finish();
        }

        void Finish()
        {
            drift.Bank();
            done = true;
            app.inputLocked = true;
            var d = app.save.d.drift;
            bool record = total > d.best;
            if (record) d.best = total;
            d.runs++;
            int reward = Mathf.RoundToInt(total / 4f) + (record && total > 0 ? 1000 : 0);
            if (reward > 0) app.save.AddMoney(reward); else app.save.Commit();
            app.audio.Success();
            int sc = total, bs = d.best, se = series;
            app.Delay(0.9f, () => app.ShowResult(new Result { drift = true, ok = true, score = sc, best = bs, record = record, reward = reward, series = se }));
        }
    }
}
