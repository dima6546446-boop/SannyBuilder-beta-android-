using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CaucasusDrive
{
    /// <summary>
    /// Точка входа. Игра собирается целиком из кода — сцена может быть пустой:
    /// после загрузки любой сцены создаётся объект «CAUCASUS DRIVE» с этим компонентом.
    /// </summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (App.I != null) return;
            var go = new GameObject("CAUCASUS DRIVE");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }
    }

    /// <summary>
    /// Приложение: меню (3D-гараж) ↔ игра (режимы «Парковка», «Свободная езда»). Владеет городом, трафиком,
    /// машиной игрока, камерой, интерфейсом, звуком и регулятором качества.
    /// </summary>
    public class App : MonoBehaviour
    {
        public static App I;
        public SaveData save;
        public Quality quality;
        public Camera cam;
        public Transform worldRoot;
        public City city;
        public RoadGraph graph;
        public TrafficLights lights;
        public TrafficManager traffic;
        public PlayerCar player;
        public CameraRig cameraRig;
        public HUD hud;
        public Menus menus;
        public GameAudio audio;
        public DayNight dayNight;
        public GarageStage garage;
        public Glow glow;
        public GameMode mode;
        public Rules rules;
        public Daily daily;
        public Crowd crowd;
        public Walker walker;
        public Beacon beacon;
        public Rain rain;
        public Radio radio;
        public bool onFoot;
        public const int GroundLayer = 11;
        float weatherTarget, weatherTimer, beepT;
        bool weatherAuto;
        readonly System.Collections.Generic.List<GameObject> dpsCars = new System.Collections.Generic.List<GameObject>();
        /// <summary>Точка, вокруг которой живёт мир: машина или игрок пешком.</summary>
        public Vector3 Focus => onFoot ? walker.Pos : player.Position;
        public bool paused, inputLocked;
        public float garageOffset;
        public int timePreset;
        public float rainLevel;
        enum State { Loading, Menu, Play }
        State state = State.Loading;
        CarInput input;
        float blinkT;
        Vector2 lastTouch;
        bool dragging;
        static readonly System.Collections.Generic.List<RaycastResult> uiHits = new System.Collections.Generic.List<RaycastResult>();

        /// <summary>Есть ли элемент интерфейса под точкой экрана (чтобы свайп по кнопке не крутил машину).</summary>
        static bool UiUnder(Vector2 p)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = p }, uiHits);
            return uiHits.Count > 0;
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 1f / 60f;
            Physics.autoSyncTransforms = false;
            save = new SaveData();
            int q = save.d.settings.quality >= 0 ? save.d.settings.quality : Quality.AutoDetect();
            quality = new Quality(q);
        }

        IEnumerator Start()
        {
            // камера
            var old = Camera.main;
            if (old) Destroy(old.gameObject);
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cg.transform.SetParent(transform, false);
            cam = cg.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cg.AddComponent<AudioListener>();
            audio = new GameObject("Audio").AddComponent<GameAudio>();
            audio.transform.SetParent(transform, false);
            audio.SetVolume(save.d.settings.volume);

            menus = new Menus(this);
            menus.Intro();
            yield return new WaitForSeconds(1.8f);
            menus.Loading("СТРОИМ АВТОЗАВОДСКИЙ РАЙОН…");
            yield return null;

            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            dayNight = new DayNight(worldRoot);
            graph = new RoadGraph();
            lights = new TrafficLights(graph, new Rng(42));
            city = new City(worldRoot, graph, quality.level);
            menus.Loading("ВЫГОНЯЕМ МАШИНЫ ИЗ ГАРАЖА…");
            yield return null;
            foreach (var c in Cars.All) { CarMeshLibrary.Get(c.id, "LOD0"); }
            foreach (var p in city.parked)
            {
                var go = TrafficManager.MakeCarObject(p.def, p.color, worldRoot, false, quality.level >= 2);
                go.transform.SetPositionAndRotation(p.pos, M.Yaw(p.heading));
                go.GetComponent<Obstacle>().kind = "parked";
            }
            traffic = new TrafficManager(worldRoot, graph, lights, quality.traffic);
            traffic.onHonk = (c) =>
            {
                var d = new Vector3(c.x, 0, c.z) - player.Position;
                if (d.magnitude < 50f) hud.Toast("Би-би! Не стой на дороге", 0, 1.2f);
                var r = M.Right(player.Heading);
                audio.AiHorn(Mathf.Max(0f, 1f - d.magnitude / 60f), Vector3.Dot(d.normalized, r));
            };
            foreach (var dp in city.dpsPosts)
            {
                var go = TrafficManager.MakeCarObject(Cars.Get("police"), 0xf2f2f2, worldRoot, false, quality.level >= 2);
                go.transform.SetPositionAndRotation(dp.pos, M.Yaw(dp.heading));
                go.GetComponent<Obstacle>().kind = "parked";
                dpsCars.Add(go);
            }
            // невидимая земля: на неё падают сбитые конусы (машины и пешеход её не касаются — высота задаётся сами)
            var ground = new GameObject("GroundCollider") { layer = GroundLayer };
            ground.transform.SetParent(worldRoot, false);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.AddComponent<BoxCollider>().size = new Vector3(4000, 1, 4000);
            rules = new Rules(lights);
            daily = new Daily(save);
            daily.onDone = (t, text, reward) => { hud.Toast("Задание дня: " + text + " — забери " + M.Rub(reward) + " в меню «Задания»", HUD.Good, 4f); audio.Success(); };
            beacon = new Beacon(worldRoot);
            rain = new Rain(worldRoot, quality.level == 0 ? 500 : quality.level == 1 ? 900 : 1400);
            radio = new Radio(audio);
            radio.Set(save.d.settings.radio);
            glow = new Glow(worldRoot, 3000);
            new SmokeFx(worldRoot, quality.level == 0 ? 90 : 180);
            player = new PlayerCar(worldRoot);
            player.go.layer = 2; // Ignore Raycast — камера и датчики не видят собственную машину
            player.onCrash = (s, kind, col) => mode?.OnCrash(s, kind, col);
            player.onBlink = () => audio.Tick(player.blinkOn);
            player.onPop = () => { float dd = Vector3.Distance(cam.transform.position, player.Position); audio.Pop(Mathf.Max(0.25f, 1f - dd / 40f)); };
            walker = new Walker(this);
            crowd = new Crowd(this);
            cameraRig = new CameraRig(cam);
            cameraRig.SetMode(save.d.settings.camera);
            hud = new HUD(this);
            garage = new GarageStage(worldRoot);
            quality.Apply(dayNight.sun, cam);
            SetTime(0);
            RefreshCar();
            player.Place(0, -70, 0);
            yield return null;
            ShowMenu("main");
        }

        // ------------------------------------------------------------------ состояние
        public void RefreshCar()
        {
            var d = Cars.Get(save.d.current);
            var t = save.Tune(d.id);
            var st = save.d.settings;
            player.SetCar(d, t, save.ColorOf(d.id), quality.carLod, st.assist, st.manual);
            foreach (var r in player.go.GetComponentsInChildren<Transform>(true)) r.gameObject.layer = 2;
            audio.SetCylinders(d.id == "oka" ? 2 : 4);
            audio.SetRasp(Tuning.Engine[Mathf.Clamp(t.engine, 0, 3)].value > 1.3f ? 1.3f : 1f);
            audio.hornType = t.horn;
            ShowRadio();
            hud.ApplySettings(st);
        }

        public void ApplySettings()
        {
            var st = save.d.settings;
            audio.SetVolume(st.volume);
            hud.ApplySettings(st);
            if (player.phys != null)
            {
                player.phys.SetManual(st.manual);
                player.phys.spec.stabilityAssist = st.assist ? 0.35f : 0f;
                player.phys.spec.driftAssist = st.assist ? 1f : 0f;
            }
        }

        public void SetQuality(int q)
        {
            quality = new Quality(q);
            quality.Apply(dayNight.sun, cam);
            traffic.target = quality.traffic;
            RefreshCar();
        }

        public void SetTime(int preset)
        {
            timePreset = ((preset % 4) + 4) % 4;
            dayNight.SetTime(DayNight.PresetTimes[timePreset]);
            dayNight.SetFog(quality.drawDist);
            city.SetNight(dayNight.night);
        }

        public void NextTime() { SetTime(timePreset + 1); }

        public void OnMenu(string name)
        {
            if (name == "main" || name == "levels" || name == "garage" || (name == "settings" && !paused) || name == "help")
            {
                state = State.Menu;
                hud.Show(false);
                garage.Show(true);
                audio.SetEngine(850, 0, 0, false);
                audio.SetHorn(false);
            }
        }

        public void ShowMenu(string name) { menus.Show(name); }

        void Enter(GameMode m)
        {
            EnterCar(true);
            SetWeather(0, false); rainLevel = 0;
            mode?.Exit();
            menus.Hide();
            garage.Show(false);
            RefreshCar();
            ApplySettings();
            mode = m;
            paused = false; inputLocked = false;
            hud.Show(true);
            hud.ClearToasts();
            player.indicator = ' ';
            state = State.Play;
            m.Enter();
            hud.SetDoor(m.AllowWalk);
            cameraRig.Snap();
        }

        public void StartParking(int i) { SetTime(i % 5 == 4 ? 1 : i % 7 == 6 ? 3 : 0); restart = () => StartParking(i); Enter(new ParkingMode(this, i)); }
        public void StartFree(bool fines) { restart = null; SetTime(timePreset); Enter(new FreeRideMode(this, fines)); }
        public void StartExam() { SetTime(0); restart = StartExam; Enter(new ExamMode(this)); }
        public void StartDrift() { SetTime(timePreset == 2 ? 2 : 0); restart = StartDrift; Enter(new DriftMode(this)); }
        System.Action restart;
        public void Restart() { restart?.Invoke(); }

        public void Pause()
        {
            if (state != State.Play || paused) return;
            paused = true;
            audio.SetEngine(850, 0, 0, false); audio.SetHorn(false);
            menus.Show("pause");
        }

        public void Resume() { menus.Hide(); paused = false; }

        public void ShowResult(Result r) { paused = true; menus.ShowResult(r); }

        public void ToMenu(string screen)
        {
            EnterCar(true);
            mode?.Exit();
            mode = null;
            traffic.Clear();
            paused = false;
            menus.Show(screen);
        }

        public void NextCamera()
        {
            if (onFoot) return;
            string n = cameraRig.Next();
            save.d.settings.camera = cameraRig.mode; save.Commit();
            hud.Toast("Камера: " + n, 0, 1.2f);
        }

        public void Crash(float s)
        {
            audio.Crash(s);
            cameraRig.AddShake(s);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s > 0.3f) Handheld.Vibrate();
#endif
        }

        public void Delay(float sec, System.Action a) { StartCoroutine(DelayCo(sec, a)); }
        IEnumerator DelayCo(float sec, System.Action a) { yield return new WaitForSeconds(sec); a(); }

        // ------------------------------------------------------------------ цикл
        void FixedUpdate()
        {
            if (state != State.Play || paused || player?.phys == null) return;
            var inp = inputLocked ? new CarInput { brake = 1f } : input;
            if (onFoot) inp = new CarInput { brake = 1f, handbrake = true };
            player.FixedStep(Time.fixedDeltaTime, inp, 1f - 0.16f * rainLevel);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (state == State.Loading) return;
            if (In.Pressed(In.K.Escape))
            {
                if (state == State.Play && !paused) Pause();
                else if (state == State.Play && paused && menus.current == "pause") Resume();
                else if (state == State.Menu && menus.current != "main") menus.Show("main");
            }
            quality.Govern(Time.unscaledDeltaTime);

            if (state == State.Menu)
            {
                // вращение машины в гараже пальцем
                Vector2 mp; bool down;
                if (In.Pointer(out mp, out down) && In.TouchCount <= 1)
                {
                    if (down) { lastTouch = mp; dragging = !UiUnder(mp); }
                    if (dragging) garage.Drag(mp.x - lastTouch.x);
                    lastTouch = mp;
                }
                else dragging = false;
                garage.Update(dt, cam, garageOffset);
                return;
            }

            if (paused) { RenderWorld(0f); return; }

            input = hud.ReadInput(save.d.settings);
            Keys();
            var p = player.phys;

            traffic.player.x = player.Position.x; traffic.player.z = player.Position.z;
            traffic.player.heading = player.Heading; traffic.player.speed = p.speed;
            traffic.center = Focus;
            if (!onFoot) traffic.ped.active = false;
            lights.Update(dt);
            traffic.Update(dt, cam);
            if (!onFoot) rules.Update(dt, player, player.surface.type);
            mode?.Update(dt);
            if (onFoot) { var mv = hud.Move(); walker.Update(dt, mv.x, mv.y, In.Held(In.K.LeftShift), cameraRig.footYaw); }
            crowd.Update(dt);
            player.VisualUpdate(dt, input, dayNight.night);
            if (onFoot) cameraRig.UpdateFoot(dt, walker, hud.look.Take());
            else cameraRig.Update(dt, player);
            Weather(dt);
            rain.Update(dt, cam.transform, rainLevel, 1f - dayNight.night * 0.8f, !onFoot && cameraRig.IsInterior);
            audio.SetRain(rainLevel, !onFoot);
            float mix = onFoot ? Mathf.Max(0f, 1f - Vector3.Distance(walker.Pos, player.Position) / 30f) * 0.6f : 1f;
            float squeal = Mathf.Clamp01(p.speed / 25f - Mathf.Abs(p.driftAngle) * 0.6f * p.sliding + 0.3f);
            audio.SetEngineMix(mix, squeal, p.speed);
            audio.SetEngine(p.rpm, p.load, p.Skid, true);
            audio.SetHorn(hud.Horn && !inputLocked);
            radio.Update(onFoot ? Mathf.Min(1f, mix * 1.4f) : 1f, onFoot);
            if (mode != null && p.speed > 0.5f && !onFoot)
            {
                save.d.stats.km += p.speed * dt / 1000f;
                daily.Progress("km", p.speed * dt / 1000f);
                daily.Progress("speed", Mathf.Round(p.speed * 3.6f));
            }
            Sensors(dt);
            hud.Update(dt);
            RenderWorld(dt);
        }

        void LateUpdate() { Physics.SyncTransforms(); }

        /// <summary>Светящиеся точки: фонари (ночью), светофоры, фары трафика, мигалки ДПС.</summary>
        void RenderWorld(float dt)
        {
            blinkT += dt;
            bool blink = (blinkT % 0.7f) < 0.35f;
            float night = dayNight.night;
            var c = cam.transform.position;
            glow.Begin();
            if (night > 0.15f)
                foreach (var l in city.lampHeads)
                {
                    float dx = l.x - c.x, dz = l.z - c.z;
                    if (dx * dx + dz * dz < 300f * 300f) glow.Add(l, new Color(1f, 0.78f, 0.48f), 1.6f);
                }
            for (int i = 0; i < city.lightHeads.Count; i++)
            {
                var h = city.lightHeads[i];
                float dx = h.x - c.x, dz = h.z - c.z;
                if (dx * dx + dz * dz > 220f * 220f) continue;
                int lamp;
                lights.Get(city.lightNode[i], city.lightAxis[i], out lamp);
                var f = city.lightFacing[i] * 0.2f;
                var p0 = new Vector3(h.x, h.y, h.z) + f;
                if ((lamp & 1) != 0) glow.Add(p0, new Color(1f, 0.08f, 0.04f), 0.55f);
                if ((lamp & 2) != 0) glow.Add(p0 + Vector3.down * 0.37f, new Color(1f, 0.62f, 0.04f), 0.55f);
                if ((lamp & 4) != 0) glow.Add(p0 + Vector3.down * 0.74f, new Color(0.1f, 1f, 0.35f), 0.55f);
            }
            traffic.RenderGlow(glow, night, blink, c);
            // мигалки постов ДПС, огоньки камер «Стрелка», огонь из прямотока, сигарета
            foreach (var d in dpsCars)
            {
                var t = d.transform;
                if ((t.position - c).sqrMagnitude > 250f * 250f) continue;
                glow.Add(t.position + Vector3.up * 1.65f + t.right * (blink ? -0.3f : 0.3f), blink ? new Color(0.1f, 0.2f, 1f) : new Color(1f, 0.1f, 0.1f), 0.9f);
            }
            if (blink) foreach (var sc in city.cameras) if ((sc.lens - c).sqrMagnitude < 200f * 200f) glow.Add(sc.lens, new Color(1f, 0.1f, 0.05f), 0.25f);
            if (player.flame > 0f) glow.Add(player.ExhaustPos, new Color(1f, 0.55f + Random.value * 0.2f, 0.15f), 0.45f + Random.value * 0.3f);
            walker.RenderGlow(glow);
            mode?.RenderGlow(glow, blink);
            glow.End();
        }

        // ------------------------------------------------------------------ клавиатура (ПК/эмулятор)
        void Keys()
        {
            if (In.Pressed(In.K.C)) NextCamera();
            if (In.Pressed(In.K.F)) ToggleFoot();
            if (In.Pressed(In.K.R)) CycleRadio();
            if (In.Pressed(In.K.T)) mode?.OnTaxi();
            if (In.Pressed(In.K.E)) mode?.OnAction();
            if (In.Pressed(In.K.Q)) player.ToggleIndicator('L');
            if (In.Pressed(In.K.Z) && !onFoot) player.ToggleIndicator('R');
            if (!onFoot) return;
            if (In.Pressed(In.K.Space)) walker.Jump();
            if (In.Pressed(In.K.X)) walker.ToggleSit();
            if (In.Pressed(In.K.G)) walker.ToggleSmoking();
            if (In.Pressed(In.K.B)) walker.WhistleNow();
        }

        // ------------------------------------------------------------------ радио
        public void CycleRadio()
        {
            string n = radio.Cycle();
            save.d.settings.radio = radio.index; save.Commit();
            Toast("Радио: " + n);
            ShowRadio();
        }

        void ShowRadio()
        {
            if (radio == null || hud == null) return;
            string full = radio.Name;
            int sp = full.LastIndexOf(' ');
            string name = radio.index > 0 ? full.Substring(0, sp).ToUpper() : "", freq = radio.index > 0 ? full.Substring(sp + 1) : "";
            player.interior?.SetRadio(name, freq);
            hud.SetRadioLabel(radio.index > 0 ? freq : "");
        }

        void Toast(string t) { if (hud != null) hud.Toast(t, HUD.Good, 1.6f); }

        // ------------------------------------------------------------------ пешком
        public void ToggleFoot()
        {
            if (state != State.Play || paused) return;
            if (onFoot)
            {
                if (Vector3.Distance(walker.Pos, player.Position) < 3.4f && walker.state != Walker.St.Down) EnterCar(false);
                else hud.Toast("Подойди к своей машине", HUD.Bad);
                return;
            }
            if (mode == null || !mode.AllowWalk) return;
            if (player.Speed > 1.2f) { hud.Toast("Сначала остановись", HUD.Bad); return; }
            ExitCar();
        }

        /// <summary>Выйти через водительскую дверь (слева); если там стена или машина — справа, сзади или спереди.</summary>
        void ExitCar()
        {
            var t = player.go.transform; var d = player.def.dims; float w = d.W / 2f;
            Vector3[] spots = { new Vector3(-(w + 0.45f), 0, 0.1f), new Vector3(w + 0.45f, 0, 0.1f), new Vector3(0, 0, d.rear - 0.6f), new Vector3(0, 0, d.front + 0.6f) };
            Vector3 pick = t.TransformPoint(spots[0]);
            foreach (var s in spots)
            {
                var wp = t.TransformPoint(s);
                if (!Physics.CheckCapsule(wp + Vector3.up * 0.4f, wp + Vector3.up * 1.5f, 0.3f, ~((1 << 2) | (1 << City.StopLayer) | (1 << Walker.Layer) | (1 << GroundLayer)), QueryTriggerInteraction.Ignore)) { pick = wp; break; }
            }
            walker.Spawn(pick, Mathf.Atan2(pick.x - player.Position.x, pick.z - player.Position.z));
            player.interior?.SetDriverVisible(false);
            onFoot = true;
            cameraRig.BeginFoot(player.Heading);
            player.indicator = ' ';
            audio.Door();
            if (!footHint) { footHint = true; hud.Toast("Джойстик — ходить (сильнее — бег), свайп — камера", HUD.Good, 3.5f); }
        }
        bool footHint;

        public void EnterCar(bool silent)
        {
            if (!onFoot) return;
            walker.Hide();
            player.interior?.SetDriverVisible(true);
            traffic.ped.active = false;
            onFoot = false;
            cameraRig.Snap();
            if (!silent) audio.Door();
        }

        // ------------------------------------------------------------------ погода
        /// <summary>Дождь: 0 или 1 (плавный переход). auto — сам меняется раз в пару минут.</summary>
        public void SetWeather(float target, bool auto) { weatherTarget = target; weatherAuto = auto; weatherTimer = 100f + Random.value * 140f; }

        void Weather(float dt)
        {
            if (weatherAuto && (weatherTimer -= dt) <= 0f)
            {
                weatherTarget = weatherTarget > 0.5f ? 0f : (Random.value < 0.55f ? 1f : 0f);
                weatherTimer = 100f + Random.value * 160f;
                if (weatherTarget > 0.5f) hud.Toast("Начинается дождь — дорога скользкая", HUD.Bad, 2.5f);
            }
            rainLevel += Mathf.Clamp(weatherTarget - rainLevel, -dt / 8f, dt / 8f);
            city.asphalt.Col(Color.white * (1f - 0.38f * rainLevel));
            dayNight.Overcast(rainLevel);
        }

        // ------------------------------------------------------------------ парктроник
        void Sensors(float dt)
        {
            bool on = !onFoot && save.d.settings.sensors && player.Speed < 8f && mode != null && mode.Name != "drift";
            if (!on) { hud.Sensors(false, 9, 9); return; }
            var t = player.go.transform; var d = player.def.dims;
            float front = 3f, rear = 3f;
            int mask = ~((1 << 2) | (1 << Walker.Layer) | (1 << GroundLayer));
            foreach (float lx in new[] { -d.W / 2 + 0.1f, 0f, d.W / 2 - 0.1f })
            {
                RaycastHit h;
                var pf = t.TransformPoint(new Vector3(lx, 0.45f, d.front - 0.1f));
                if (Physics.Raycast(pf, t.forward, out h, 3.1f, mask, QueryTriggerInteraction.Ignore)) front = Mathf.Min(front, Mathf.Max(0, h.distance - 0.1f));
                var pr = t.TransformPoint(new Vector3(lx, 0.45f, d.rear + 0.1f));
                if (Physics.Raycast(pr, -t.forward, out h, 3.1f, mask, QueryTriggerInteraction.Ignore)) rear = Mathf.Min(rear, Mathf.Max(0, h.distance - 0.1f));
            }
            bool show = front < 2.95f || rear < 2.95f;
            hud.Sensors(show, front, rear);
            var ph = player.phys;
            float dd = ph.reversing ? rear : ph.speed < 4f ? front : 9f;
            beepT -= dt;
            if (dd < 2.2f && beepT <= 0f && ph.speed > 0.05f)
            {
                audio.Beep(dd < 0.4f ? 2100 : 1700, dd < 0.4f ? 0.12f : 0.06f, 0.08f);
                beepT = dd < 0.4f ? 0.12f : dd < 0.9f ? 0.2f : dd < 1.5f ? 0.38f : 0.6f;
            }
        }

        void OnApplicationPause(bool p) { if (p) { Pause(); save.Commit(); } }
    }
}
