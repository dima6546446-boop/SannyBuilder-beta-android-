using System.Collections.Generic;
using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    public enum Scr { Menu, Play, Garage, Settings, Hints, Pause, Results, None }
    public enum AppState { Menu, Play, Pause, Results }

    /// <summary>Приложение: состояния, навигация, заезд, окружение, автосалон. Интерфейс — в ZanosUi.cs (IMGUI).</summary>
    public sealed partial class ZanosApp : MonoBehaviour
    {
        // ядро и сервисы
        Progress progress; Controls controls; AudioSynth audioSynth; Camera cam; CamRig rig;
        Session session; MapView mapView; CarView carView; Fx fx; Light sun; GameObject showroom; CarView showCar; float showAngle;
        // состояние
        AppState state = AppState.Menu; Scr screen = Scr.Menu; readonly Stack<Scr> nav = new Stack<Scr>();
        float countdown; int lastCount; string envTime = "dusk", envWeather = "clear";
        // выбор заезда
        GameMode selMode = GameMode.Free; string selMap = "parking", selChallenge, selCar = "kopeyka", selTime = "auto", selWeather = "clear";
        // гараж
        string garCar = "kopeyka"; int garTab; int setTab; string toast; float toastT;
        RunSummary resultSum; FinishResult resultRew;
        readonly List<KeyValuePair<string, float>> pops = new List<KeyValuePair<string, float>>(); readonly List<Color> popCols = new List<Color>();
        float shownTotal, hintT = 9, camNameT; string camName = ""; double fpsAcc; int fpsN; float fpsShow;

        Settings S { get { return progress.Data.settings; } }

        void Awake()
        {
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            progress = new Progress(SaveStore.Load()); progress.OnChanged = () => SaveStore.Save(progress.Data);
            controls = new Controls(); audioSynth = AudioSynth.Create();
            var cgo = new GameObject("ZanosCamera"); cgo.transform.SetParent(transform, false); cam = cgo.AddComponent<Camera>(); cam.nearClipPlane = 0.1f; cam.farClipPlane = 2500; cam.clearFlags = CameraClearFlags.SolidColor; cgo.AddComponent<AudioListener>();
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c != cam) c.enabled = false;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.gameObject != cgo) l.enabled = false;
            rig = new CamRig(cam);
            var lg = new GameObject("Sun"); lg.transform.SetParent(transform, false); sun = lg.AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
            selCar = garCar = progress.Data.selected;
            controls.OnPause = () => { if (state == AppState.Play || state == AppState.Pause) PauseToggle(); };
            controls.OnCamera = () => { if (state == AppState.Play) { rig.Next(); camName = "Камера: " + CamRig.Names[rig.Mode]; camNameT = 1.6f; } };
            controls.OnReset = () => { if (state == AppState.Play && countdown <= 0) { session.Respawn(); fx.Clear(); Toast("Машина возвращена на старт"); } };
            controls.OnHints = () => { if (state == AppState.Play) { PauseToggle(); Go(Scr.Hints); } };
            ApplySettings(); EnterShowroom(selCar);
            if (!S.hintsSeen) { Go(Scr.Hints); S.hintsSeen = true; Persist(); }
        }

        void Persist() { SaveStore.Save(progress.Data); }
        void Toast(string t) { toast = t; toastT = 2.8f; }

        // ---------- настройки ----------
        void ApplySettings()
        {
            QualitySettings.SetQualityLevel(Mathf.Clamp(S.quality, 0, QualitySettings.names.Length - 1), true);
            QualitySettings.shadows = S.shadows ? ShadowQuality.All : ShadowQuality.Disable;
            rig.BaseFov = S.fov; rig.SpeedFx = S.speedFx; if (fx != null) fx.Enabled = S.effects;
            controls.SteerSens = S.steerSens; controls.Deadzone = S.deadzone;
            audioSynth.Apply(S.master, S.engine, S.sfx, S.music);
            if (session != null) { session.Opts.Assist = S.assist; session.Car.Assist = S.assist; session.Car.AutoGear = !S.manual; }
            QualitySettings.shadowDistance = S.quality >= 2 ? 90 : 50;
        }

        // ---------- окружение: небо, свет, туман ----------
        struct Env { public Color top, horizon, sun, hemi; public float sunI, ambient; public Vector3 dir; public float fogDensity; public bool lights; }
        static Env EnvFor(string time, string weather)
        {
            Env e;
            switch (time)
            {
                case "night": e = new Env { top = new Color(0.02f, 0.035f, 0.08f), horizon = new Color(0.1f, 0.15f, 0.27f), sun = new Color(0.55f, 0.65f, 1f), sunI = 0.35f, hemi = new Color(0.25f, 0.31f, 0.5f), ambient = 0.9f, dir = new Vector3(40, -30, 0), fogDensity = 0.0035f, lights = true }; break;
                case "dusk": e = new Env { top = new Color(0.16f, 0.23f, 0.4f), horizon = new Color(0.95f, 0.64f, 0.39f), sun = new Color(1f, 0.72f, 0.47f), sunI = 1.3f, hemi = new Color(0.6f, 0.65f, 0.85f), ambient = 0.7f, dir = new Vector3(14, -110, 0), fogDensity = 0.0022f, lights = true }; break;
                default: e = new Env { top = new Color(0.25f, 0.52f, 0.83f), horizon = new Color(0.82f, 0.9f, 0.96f), sun = new Color(1f, 0.94f, 0.82f), sunI = 1.5f, hemi = new Color(0.8f, 0.88f, 1f), ambient = 0.9f, dir = new Vector3(55, -35, 0), fogDensity = 0.0012f, lights = false }; break;
            }
            if (weather == "rain") { e.horizon = Color.Lerp(e.horizon, new Color(0.45f, 0.5f, 0.55f), 0.6f) * 0.8f; e.sunI *= 0.35f; e.fogDensity *= 2.2f; }
            if (weather == "snow") { e.horizon = Color.Lerp(e.horizon, new Color(0.8f, 0.83f, 0.86f), 0.6f); e.sunI *= 0.6f; e.fogDensity *= 2f; }
            return e;
        }

        void SetEnv(string time, string weather)
        {
            envTime = time; envWeather = weather; var e = EnvFor(time, weather);
            cam.backgroundColor = e.horizon; sun.color = e.sun; sun.intensity = e.sunI; sun.transform.rotation = Quaternion.Euler(e.dir);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = e.top * 1.6f * e.ambient + e.hemi * 0.25f; RenderSettings.ambientEquatorColor = e.horizon * 0.7f * e.ambient; RenderSettings.ambientGroundColor = e.horizon * 0.25f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = e.horizon; RenderSettings.fogDensity = e.fogDensity;
        }

        // ---------- навигация ----------
        void Go(Scr s, bool push = true)
        {
            if (push && screen != s && screen != Scr.None) nav.Push(screen);
            screen = s;
            if (s == Scr.Menu) { nav.Clear(); EnterShowroom(progress.Data.selected); }
            if (s == Scr.Garage) EnterShowroom(garCar);
            if (s == Scr.Play) EnterShowroom(selCar);
        }
        void Back() { screen = nav.Count > 0 ? nav.Pop() : Scr.Menu; if (screen == Scr.Garage) EnterShowroom(garCar); if (screen == Scr.Menu) EnterShowroom(progress.Data.selected); }

        // ---------- автосалон ----------
        CarState LookOf(string id) { return progress.Car(id) ?? new CarState { id = id, paint = CarCatalog.ById(id).Color }; }

        void EnterShowroom(string carId)
        {
            if (state == AppState.Play || state == AppState.Pause) return;
            if (showroom == null)
            {
                showroom = new GameObject("Showroom");
                var floor = new MeshBuilder(); floor.AddDisc(0, 0, 14, 0, 4f, 48); var f = new GameObject("Floor"); f.transform.SetParent(showroom.transform, false);
                f.AddComponent<MeshFilter>().sharedMesh = floor.ToMesh("floor"); f.AddComponent<MeshRenderer>().sharedMaterial = Mats.Solid(new Color(0.11f, 0.12f, 0.15f), 0.6f, 0.3f);
                var ring = new MeshBuilder(); ring.AddStrip(Circle(4.7f, 48), 0.18f, 0.02f, true); var rg = new GameObject("Ring"); rg.transform.SetParent(showroom.transform, false);
                rg.AddComponent<MeshFilter>().sharedMesh = ring.ToMesh("ring"); rg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Flat(new Color(1f, 0.48f, 0f));
                var key = new GameObject("Key"); key.transform.SetParent(showroom.transform, false); key.transform.position = new Vector3(7, 11, -6); key.transform.LookAt(Vector3.up * 0.5f);
                var kl = key.AddComponent<Light>(); kl.type = LightType.Spot; kl.range = 40; kl.spotAngle = 60; kl.intensity = 6; kl.shadows = LightShadows.Soft;
                var rim = new GameObject("Rim"); rim.transform.SetParent(showroom.transform, false); rim.transform.position = new Vector3(-8, 6, 7); rim.transform.LookAt(Vector3.up * 0.7f);
                var rl = rim.AddComponent<Light>(); rl.type = LightType.Spot; rl.range = 40; rl.spotAngle = 70; rl.intensity = 4; rl.color = new Color(0.42f, 0.66f, 1f);
            }
            showroom.SetActive(true); sun.enabled = false;
            if (mapView != null) mapView.Root.SetActive(false);
            SetEnv("night", "clear"); RenderSettings.fogDensity = 0.012f; cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            if (showCar != null) showCar.Destroy();
            var spec = CarSpec.Build(CarCatalog.ById(carId), LookOf(carId).TuningMap()); showCar = new CarView(spec, LookOf(carId)); showCar.Root.transform.SetParent(showroom.transform, false);
            showCar.Root.transform.position = Vector3.zero; showCar.Root.transform.rotation = Quaternion.Euler(0, -30, 0); showShown = carId;
        }
        string showShown;
        static List<Vector2> Circle(float r, int n) { var l = new List<Vector2>(); for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; l.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); } return l; }

        void RefreshShowCar(string id) { if (state == AppState.Play || state == AppState.Pause) return; EnterShowroom(id); }

        void ShowroomFrame(float dt)
        {
            showAngle += dt * 22f; float vw = Screen.width / Mathf.Max(1f, Screen.height / 720f); float gap = Mathf.Clamp((vw - 720f) / vw, 0.3f, 0.62f);
            float want = Mathf.Clamp(5.2f / (gap * 0.85f * 2f * Mathf.Tan(21f * Mathf.Deg2Rad) * cam.aspect), 8.5f, 24f);
            camDist = Mathf.Lerp(camDist, want, Mathf.Min(1, dt * 4f));
            float a = showAngle * Mathf.Deg2Rad; cam.transform.position = new Vector3(Mathf.Sin(a) * camDist, 2.6f, Mathf.Cos(a) * camDist); cam.transform.LookAt(new Vector3(0, 0.7f, 0)); cam.fieldOfView = 42;
        }
        float camDist = 11.5f;

        // ---------- заезд ----------
        void StartGame()
        {
            var map = MapLoader.Load(selMap); var st = progress.Car(selCar);
            string time = selTime == "auto" ? map.time : selTime;
            DestroyWorld();
            session = new Session(map, new SessionOptions { MapId = selMap, CarId = selCar, Tuning = st.TuningMap(), Mode = selMode, ChallengeId = selMode == GameMode.Challenge ? selChallenge : null, Weather = selWeather, Assist = S.assist, AutoGear = !S.manual });
            session.OnEvent = OnSessionEvent;
            if (showroom != null) showroom.SetActive(false);
            sun.enabled = true; SetEnv(time, selWeather);
            mapView = new MapView(map, time, selWeather);
            carView = new CarView(session.Spec, st); carView.SetHeadlights(EnvFor(time, selWeather).lights || selWeather == "rain");
            fx = new Fx(transform) { Enabled = S.effects };
            mapView.ShowGates(session.Challenge != null && session.Challenge.type == "gates", 0);
            if (showCar != null) { showCar.Destroy(); showCar = null; }
            rig.SetMode(S.camera); rig.Snap(); controls.ResetState();
            state = AppState.Play; screen = Scr.None; countdown = 3.2f; lastCount = 4; hintT = 9; shownTotal = 0; pops.Clear(); popCols.Clear();
            Persist(); ApplySettings();
        }

        void DestroyWorld()
        {
            if (mapView != null) { mapView.Destroy(); mapView = null; } if (carView != null) { carView.Destroy(); carView = null; } if (fx != null) { fx.Destroy(); fx = null; }
        }

        void RestartGame()
        {
            session.Restart(); fx.Clear(); mapView.ShowGates(session.Challenge != null && session.Challenge.type == "gates", 0);
            controls.ResetState(); state = AppState.Play; screen = Scr.None; countdown = 3.2f; lastCount = 4; shownTotal = 0; rig.Snap();
        }

        void PauseToggle()
        {
            if (state == AppState.Play) { state = AppState.Pause; screen = Scr.Pause; nav.Clear(); controls.ResetState(); }
            else if (state == AppState.Pause) { state = AppState.Play; screen = Scr.None; }
        }

        void EndRun()
        {
            if (session == null) return;
            if (!session.Finished) session.Finish(session.Opts.Mode == GameMode.Free);
            resultSum = session.Summary(); resultRew = progress.FinishRun(resultSum);
            state = AppState.Results; screen = Scr.Results; controls.ResetState();
            if (resultSum.Success) audioSynth.Success(); else audioSynth.Error();
            Persist();
        }

        void LeaveGame() { DestroyWorld(); session = null; state = AppState.Menu; countdown = 0; EnterShowroom(progress.Data.selected); }

        static void VibrateIfHard(double speed)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (speed > 4) Handheld.Vibrate();
#endif
        }

        void PopUp(string text, Color c) { pops.Add(new KeyValuePair<string, float>(text, 1.9f)); popCols.Add(c); if (pops.Count > 4) { pops.RemoveAt(0); popCols.RemoveAt(0); } }

        void OnSessionEvent(string type, object data)
        {
            var sc = data as ScoreEvent;
            switch (type)
            {
                case "impact": { var e = (ImpactEvent)data; if (fx != null) fx.Impact(e); if (e.Kind == "prop") audioSynth.Tick(); else { audioSynth.Impact(e.Speed); VibrateIfHard(e.Speed); } break; }
                case "lost": PopUp("−" + (long)sc.Points + "  СЕРИЯ СГОРЕЛА", new Color(1f, 0.3f, 0.37f)); audioSynth.Lost(); break;
                case "bank": PopUp("+" + (long)sc.Points + (sc.Mult > 1 ? "  ×" + sc.Mult.ToString("0.#") : ""), sc.Points > 2000 ? new Color(0.24f, 0.86f, 0.52f) : new Color(1f, 0.7f, 0.28f)); audioSynth.Bank(); break;
                case "transition": PopUp("СМЕНА СТОРОНЫ! множитель ×" + sc.Mult.ToString("0.##"), new Color(0.22f, 0.77f, 1f)); audioSynth.Click(); break;
                case "gate": { int idx = (int)data; PopUp("Ворота " + idx + "/" + session.Map.gates.Length, new Color(0.24f, 0.86f, 0.52f)); audioSynth.Gate(); mapView.ShowGates(true, idx); break; }
                case "respawn": if (fx != null) fx.Clear(); break;
                case "finish": EndRun(); break;
            }
        }

        // ---------- главный цикл ----------
        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            if (toastT > 0) toastT -= dt;
            if (state == AppState.Menu && screen != Scr.None || session == null) { ShowroomFrame(dt); audioSynth.UpdateCar(null, true, "asphalt"); controls.Update(dt, 0); return; }
            var car = session.Car;
            if (state == AppState.Play)
            {
                controls.PollTouch();
                if (countdown > 0)
                {
                    int n = Mathf.CeilToInt(countdown); countdown -= dt; int n2 = Mathf.CeilToInt(countdown);
                    if (n2 != lastCount) { lastCount = n2; if (n2 > 0 && n2 <= 3) audioSynth.Click(); }
                    controls.Update(dt, 0);
                    var held = new CarInput { Handbrake = true }; session.Update(dt, held); car.Vx = car.Vz = car.W = 0;
                }
                else { var inp = controls.Update(dt, car.Speed); session.Update(dt, inp); }
                audioSynth.UpdateCar(car, false, (car.ContactSurface[2] ?? Surface.Asphalt).Name);
                if (session.Finished && state == AppState.Play) EndRun();
                hintT -= dt; camNameT -= dt;
            }
            else audioSynth.UpdateCar(car, true, "asphalt");
            if (state == AppState.Menu) return;
            carView.Sync(car); mapView.UpdateProps(session.World.Props); mapView.UpdateLamps(new Vector3((float)car.X, 0, (float)car.Z));
            var e = EnvFor(envTime, envWeather); sun.transform.position = new Vector3((float)car.X, 50, (float)car.Z);
            rig.Update(state == AppState.Play ? dt : 0f, car, (float)session.CamShake);
            if (state == AppState.Play) fx.Update(dt, car, envTime, envWeather);
            fpsAcc += dt; fpsN++; if (fpsAcc > 0.5) { fpsShow = (float)(fpsN / fpsAcc); fpsAcc = 0; fpsN = 0; }
        }

        void OnApplicationPause(bool paused) { if (paused && state == AppState.Play) PauseToggle(); Persist(); }
        void OnApplicationFocus(bool focus) { if (!focus && state == AppState.Play) PauseToggle(); }
        void OnApplicationQuit() { Persist(); }
    }
}
