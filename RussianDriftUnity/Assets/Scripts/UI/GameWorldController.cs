using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.AI;
using RussianDrift.Audio;
using RussianDrift.Core;
using RussianDrift.Meta;
using RussianDrift.Vehicle;
using RussianDrift.World;

namespace RussianDrift.UI
{
    /// <summary>
    /// GameWorld scene orchestrator: builds the world, spawns the car, wires camera/HUD/audio/modes,
    /// runs intro + countdown, pause menu and the results flow.
    /// </summary>
    public class GameWorldController : MonoBehaviour
    {
        private enum State { Loading, Intro, Countdown, Running, Finished }

        public WeatherSystem Weather { get; private set; }
        public WorldInfo World { get { return world; } }

        private WorldInfo world;
        private DayNightCycle dayNight;
        private PostFxController postFx;
        private VehicleController player;
        private DriftScorer scorer;
        private PlayerInputSource input;
        private EngineAudio engineAudio;
        private CameraRig camRig;
        private MiniMapCamera minimap;
        private Hud hud;
        private DriverCharacter driverChar;
        private ModeController modeCtl;
        private ProfileService profile;
        private GameSettings settings;
        private TrackDefinition track;
        private Transform root;
        private State state = State.Loading;
        private bool paused;
        private Canvas menuCanvas;
        private RectTransform pauseUi, resultsUi;
        private Image loadFill;
        private Canvas loadCanvas;
        private GameObject loadCamGo;

        // ------------------------------------------------------------------ startup
        private IEnumerator Start()
        {
            profile = Services.Get<ProfileService>();
            if (profile == null) { SceneLoader.Load(SceneNames.Boot); yield break; }
            settings = profile.Data.settings;
            GameCatalog.EnsureLoaded();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 1f / 60f;
            AudioListener.pause = false;
            QualityManager.Apply();
            PoolManager.Clear();
            VehicleController.ClearSurfaceCache();
            WorldConditions.Reset();
            TouchInput.ResetHeld();

            BuildLoadingScreen();
            yield return null;

            var mode = GameSession.Mode;
            track = string.IsNullOrEmpty(GameSession.TrackId) ? null : GameCatalog.GetTrack(GameSession.TrackId);
            if (mode != GameMode.FreeRoam && track == null) { mode = GameMode.FreeRoam; }
            float startHour = track != null ? track.startTime : GameSession.FreeHour;
            int weather = track != null ? track.weather : GameSession.FreeWeather;

            root = new GameObject("GameRoot").transform;
            yield return StartCoroutine(WorldBuilder.BuildAsync(root, 20240607, p => SetLoad(0.05f + 0.55f * p), w => world = w));
            SetLoad(0.62f);
            yield return null;

            dayNight = DayNightCycle.Create(root, startHour);
            dayNight.frozen = mode != GameMode.FreeRoam;
            Weather = WeatherSystem.Create(root, world);
            Weather.SetWeather((WeatherState)weather, true);
            postFx = PostFxController.Create(root);
            ReflectionController refl = ReflectionController.Create(root);
            NearbyLampLights.Create(root, world);
            ChunkCuller.Create(root, world);
            SetLoad(0.7f);
            yield return null;

            // ---- player ----
            input = new GameObject("PlayerInput").AddComponent<PlayerInputSource>();
            input.transform.SetParent(root, false);
            var def = profile.CurrentCar;
            var setup = profile.CurrentSetup;
            if (string.IsNullOrEmpty(setup.colorHex)) setup.colorHex = ColorUtility.ToHtmlStringRGB(def.defaultColor);
            var res = VehicleFactory.Create(def, setup, world.freeRoamSpawn + Vector3.up, Quaternion.identity, true, input, true);
            player = res.vc; scorer = res.scorer;
            player.transform.SetParent(root, true);
            player.Frozen = true;
            engineAudio = EngineAudio.Attach(player, true);
            refl.follow = player.transform;
            SetLoad(0.8f);
            yield return null;

            // ---- camera ----
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f; cam.nearClipPlane = 0.3f; cam.farClipPlane = Mathf.Max(300f, QualityManager.Current.viewDistance * 1.15f);
            cam.allowHDR = true; cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            UrpBridge.SetupCamera(cam, true, true);
            camRig = camGo.AddComponent<CameraRig>();
            camRig.SetTarget(player);
            camRig.SetMode(CameraMode.Cinematic);
            Destroy(loadCamGo);
            loadCamGo = null;

            minimap = MiniMapCamera.Create(root, QualityManager.Current.index == 0 ? 192 : 256);
            minimap.target = player.transform;
            driverChar = DriverCharacter.Create(root, profile.Data.driver);

            // ---- mode ----
            var ctx = new ModeContext { gw = this, world = world, player = player, scorer = scorer, track = track, profile = profile, camRig = camRig, root = root };
            hud = Hud.Create(player, scorer, minimap.texture, TogglePause, CycleCamera, Respawn, OnEditDone);
            ctx.hud = hud;
            switch (mode)
            {
                case GameMode.DriftTrack: modeCtl = new DriftTrackMode(); break;
                case GameMode.TimeAttack: modeCtl = new TimeAttackMode(); break;
                case GameMode.DriftBattle: modeCtl = new BattleMode(); break;
                default: modeCtl = new FreeRoamMode(); break;
            }
            modeCtl.Setup(ctx);
            SetLoad(0.88f);
            yield return null;

            if (modeCtl.UsesTraffic)
            {
                int count = QualityManager.Current.trafficCount;
                if (mode != GameMode.FreeRoam) count = Mathf.RoundToInt(count * 0.5f);
                TrafficManager.Create(root, world, player, count);
            }
            var kind = track != null ? track.kind : TrackKind.CityRing;
            var spots = mode == GameMode.FreeRoam ? world.citySpectators : world.SpectatorsFor(kind);
            SpectatorCrowd.Create(root, spots, mode == GameMode.FreeRoam ? null : world.PathFor(kind), QualityManager.Current.spectatorCount, player);
            SetLoad(0.96f);
            yield return null;

            input.PausePressed += TogglePause;
            input.CameraTogglePressed += CycleCamera;
            GameEvents.NearMiss += OnNearMiss;
            GameEvents.VehicleCollision += OnCollision;
            AudioManager.Ensure().StartPlaylist("music_phonk1", "music_drive", "music_phonk2", "music_night");
            menuCanvas = Ui.CreateCanvas("MenuCanvas", 20, root);

            yield return new WaitForSeconds(0.35f);
            // let the car settle on its suspension while still frozen
            yield return new WaitForFixedUpdate();
            SetLoad(1f);
            Destroy(loadCanvas.gameObject);
            SceneLoader.FadeIn();

            // ---- intro: driver walks to the car and gets in ----
            state = State.Intro;
            camRig.SetMode(CameraMode.Cinematic);
            hud.SetVisible(false);
            yield return StartCoroutine(driverChar.EnterCar(player));
            AudioManager.Ensure().Play("impact_l0", 0.35f, 1.3f);       // door
            camRig.SetMode(CameraMode.Chase);
            hud.SetVisible(true);

            if (modeCtl.Timed)
            {
                state = State.Countdown;
                for (int i = 3; i >= 1; i--)
                {
                    hud.ShowCountdown(i.ToString());
                    AudioManager.Ensure().Play("beep", 0.9f, 1f);
                    yield return new WaitForSeconds(1f);
                }
                hud.ShowCountdown("GO!");
                AudioManager.Ensure().Play("go", 1f);
            }
            player.Frozen = false;
            state = State.Running;
            modeCtl.Begin();
        }

        // ------------------------------------------------------------------ loading screen
        private void BuildLoadingScreen()
        {
            loadCamGo = new GameObject("LoadCamera");
            var c = loadCamGo.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.03f, 0.035f, 0.05f);
            loadCamGo.AddComponent<AudioListener>();
            loadCanvas = Ui.CreateCanvas("LoadCanvas", 50);
            var t = Ui.Label(loadCanvas.transform, "RUSSIAN <color=#FF6120>DRIFT</color>", 96, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(t.rectTransform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1500, 150));
            var tip = Ui.Label(loadCanvas.transform, Loc.T("tip." + Random.Range(0, 6)), 32, Theme.Dim, TextAnchor.MiddleCenter);
            Ui.Place(tip.rectTransform, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(1500, 90));
            var bar = Ui.Bar(loadCanvas.transform, Theme.Accent, new Color(1, 1, 1, 0.12f), out loadFill);
            Ui.Place(bar.rectTransform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(900, 18));
            Ui.SetBar(loadFill, 0.02f);
        }

        private void SetLoad(float p) { if (loadFill != null) Ui.SetBar(loadFill, p); }

        // ------------------------------------------------------------------ helpers used by modes
        public void PlaceCar(VehicleController v, Vector3 pos, Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            float ground = pos.y;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out hit, 40f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
            var def = v.Stats.def;
            float rest = def.comHeight + (v.Stats.rideHeight - 0.17f);
            v.Teleport(new Vector3(pos.x, ground + rest + 0.05f, pos.z), Quaternion.LookRotation(forward, Vector3.up));
        }

        // ------------------------------------------------------------------ per frame
        private void Update()
        {
            if (state == State.Loading || player == null) return;
            if (!paused)
            {
                postFx.SetSpeed(player.SpeedKmh);
                if (engineAudio != null) engineAudio.SetInterior(camRig.mode == CameraMode.Cockpit);
                if (state == State.Running && modeCtl != null)
                {
                    modeCtl.Tick(Time.deltaTime);
                    if (modeCtl.Finished) EndRun();
                }
                else if (state == State.Intro || state == State.Countdown)
                {
                    // keep the HUD populated while waiting
                }
            }
        }

        private void LateUpdate()
        {
            if (state == State.Finished && resultsUi != null) { }
        }

        private void OnDestroy()
        {
            GameEvents.NearMiss -= OnNearMiss;
            GameEvents.VehicleCollision -= OnCollision;
            if (input != null) { input.PausePressed -= TogglePause; input.CameraTogglePressed -= CycleCamera; }
            if (modeCtl != null) modeCtl.Dispose();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            TouchInput.ResetHeld();
        }

        private void OnNearMiss(float d)
        {
            if (scorer != null && state == State.Running) scorer.AddBonus(250);
        }

        private void OnCollision(float impulse, bool traffic)
        {
            if (settings.vibration && impulse > 4500f) Handheld.Vibrate();
        }

        // ------------------------------------------------------------------ player actions
        private void CycleCamera()
        {
            if (camRig == null || state == State.Finished) return;
            camRig.Cycle();
        }

        private void Respawn()
        {
            if (state != State.Running && state != State.Countdown) return;
            if (paused) return;
            modeCtl.Respawn();
        }

        private void OnEditDone()
        {
            if (paused && menuCanvas != null && pauseUi == null) ShowPause();
        }

        private void TogglePause()
        {
            if (state == State.Loading || state == State.Intro) return;
            if (hud != null && hud.controls != null && hud.controls.Editing) return;
            if (resultsUi != null) return;
            if (paused) ResumeGame(); else PauseGame();
        }

        private void PauseGame()
        {
            paused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            TouchInput.ResetHeld();
            ShowPause();
        }

        private void ShowPause()
        {
            if (pauseUi != null) Destroy(pauseUi.gameObject);
            pauseUi = GameMenus.CreatePause(menuCanvas.transform, ResumeGame, Restart, OpenSettings, ExitToMenu);
        }

        private void ResumeGame()
        {
            if (pauseUi != null) { Destroy(pauseUi.gameObject); pauseUi = null; }
            paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            profile.Save.Save();
        }

        private void OpenSettings()
        {
            if (pauseUi != null) { Destroy(pauseUi.gameObject); pauseUi = null; }
            SettingsPanel.Create(menuCanvas.transform, true, () => { if (paused && !hud.controls.Editing) ShowPause(); }, () => hud.controls.SetEditMode(true));
        }

        private void Restart() { Time.timeScale = 1f; AudioListener.pause = false; SceneLoader.Load(SceneNames.GameWorld); }
        private void ExitToMenu() { profile.Save.Save(); Time.timeScale = 1f; AudioListener.pause = false; SceneLoader.Load(SceneNames.MainMenu); }

        // ------------------------------------------------------------------ results
        private int runCounter;

        private void EndRun()
        {
            if (state == State.Finished) return;
            state = State.Finished;
            var r = modeCtl.Result;
            player.Frozen = true;
            TouchInput.ResetHeld();
            hud.SetVisible(false);
            camRig.SetMode(CameraMode.Cinematic);

            profile.AddCurrency(r.coins);
            profile.AddXp(r.xp);
            profile.Data.AddStat("runs", 1);
            if (r.medal >= 3) profile.Data.AddStat("gold_medals", 1);
            var newAch = Services.Get<AchievementService>().Check();
            profile.Save.Save();
            AudioManager.Ensure().Play(r.medal > 0 || r.win ? "levelup" : "fail", 0.9f);
            StartCoroutine(ShowResults(r, newAch));
        }

        private IEnumerator ShowResults(RunResult r, List<AchievementDef> newAch)
        {
            StartCoroutine(driverChar.ExitCar(player, 999f));
            yield return new WaitForSeconds(1.4f);
            resultsUi = GameMenus.CreateResults(menuCanvas.transform, r, newAch, true, Restart, ExitToMenu, () => { profile.Save.Save(); SceneLoader.Load(SceneNames.Garage); });
            runCounter++;
            var ads = Services.Get<IAdService>();
            if (ads != null && runCounter % 3 == 0) ads.ShowInterstitial("results", null);
        }
    }
}
