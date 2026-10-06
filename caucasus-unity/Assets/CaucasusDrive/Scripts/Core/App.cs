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
        public bool paused, inputLocked;
        public float garageOffset;
        public int timePreset;
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
            };
            glow = new Glow(worldRoot, 3000);
            new SmokeFx(worldRoot, quality.level == 0 ? 90 : 180);
            player = new PlayerCar(worldRoot);
            player.go.layer = 2; // Ignore Raycast — камера и датчики не видят собственную машину
            player.onCrash = (s, kind, col) => mode?.OnCrash(s, kind, col);
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
            mode?.Exit();
            menus.Hide();
            garage.Show(false);
            RefreshCar();
            ApplySettings();
            mode = m;
            paused = false; inputLocked = false;
            hud.Show(true);
            hud.ClearToasts();
            state = State.Play;
            m.Enter();
        }

        public void StartParking(int i) { SetTime(i % 5 == 4 ? 1 : i % 7 == 6 ? 3 : 0); restart = () => StartParking(i); Enter(new ParkingMode(this, i)); }
        public void StartFree() { restart = null; Enter(new FreeRideMode(this)); }
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
            mode?.Exit();
            mode = null;
            traffic.Clear();
            paused = false;
            menus.Show(screen);
        }

        public void NextCamera()
        {
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
            player.FixedStep(Time.fixedDeltaTime, inp, 1f);
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
            if (In.Pressed(In.K.C)) NextCamera();
            var p = player.phys;

            traffic.player.x = player.Position.x; traffic.player.z = player.Position.z;
            traffic.player.heading = player.Heading; traffic.player.speed = p.speed;
            traffic.center = player.Position;
            lights.Update(dt);
            traffic.Update(dt, cam);
            mode?.Update(dt);
            player.VisualUpdate(dt, input, dayNight.night);
            cameraRig.Update(dt, player);
            audio.SetEngine(p.rpm, p.load, p.Skid, true);
            audio.SetHorn(hud.Horn && !inputLocked);
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
            glow.End();
        }

        void OnApplicationPause(bool p) { if (p) { Pause(); save.Commit(); } }
    }
}
