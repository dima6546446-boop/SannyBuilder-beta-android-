using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Audio
{
    /// <summary>Global audio: lazily generated procedural clips, pooled SFX voices, UI sounds and the music playlist.</summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance { get { return Ensure(); } }

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource[] pool;
        private int poolIdx;
        private AudioSource musicA, musicB;
        private bool musicOnA = true;
        private List<string> playlist = new List<string>();
        private int playIdx;
        private int loopsLeft;
        private float musicFade = 1f;
        private bool musicRunning;
        private float musicDuck = 1f;
        private bool waitingForTrack, pendingFirst;

        public float SfxVolume = 1f;
        public float MusicVolume = 0.6f;

        public static AudioManager Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("[Audio]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioManager>();
            instance.Init();
            return instance;
        }

        private void Init()
        {
            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f; s.ignoreListenerPause = true;
                pool[i] = s;
            }
            musicA = gameObject.AddComponent<AudioSource>(); musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { musicA, musicB }) { m.playOnAwake = false; m.loop = false; m.spatialBlend = 0f; m.volume = 0f; m.ignoreListenerPause = true; }
            GameEvents.SettingsChanged += ApplySettings;
            ApplySettings();
        }

        private void OnDestroy() { GameEvents.SettingsChanged -= ApplySettings; }

        public void ApplySettings()
        {
            var save = Services.Get<SaveSystem>();
            if (save == null) return;
            var s = save.Data.settings;
            AudioListener.volume = Mathf.Clamp01(s.masterVolume);
            SfxVolume = Mathf.Clamp01(s.sfxVolume);
            MusicVolume = Mathf.Clamp01(s.musicVolume);
        }

        // ---------------------------------------------------------------- clips
        public AudioClip Get(string key)
        {
            AudioClip c;
            if (clips.TryGetValue(key, out c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + key);      // imported clip with the same key overrides the generated one
            if (c == null) c = Generate(key);
            if (c != null) clips[key] = c;
            return c;
        }

        // ---- background music generation (worker thread renders samples, main thread wraps them in an AudioClip) ----
        private class MusicJob { public string key; public float[] data; public volatile bool done; }
        private readonly Dictionary<string, MusicJob> musicJobs = new Dictionary<string, MusicJob>();

        private static bool IsMusic(string key) { return key.StartsWith("music_"); }

        private static SynthLib.MusicStyle StyleOf(string key, out int seed)
        {
            switch (key)
            {
                case "music_menu": seed = 3; return SynthLib.MusicStyle.Menu;
                case "music_phonk1": seed = 5; return SynthLib.MusicStyle.Phonk;
                case "music_phonk2": seed = 11; return SynthLib.MusicStyle.Phonk;
                case "music_drive": seed = 7; return SynthLib.MusicStyle.Drive;
                default: seed = 2; return SynthLib.MusicStyle.Night;
            }
        }

        /// <summary>Returns the clip if ready; otherwise starts rendering it in the background and returns null.</summary>
        public AudioClip TryGetMusic(string key)
        {
            AudioClip c;
            if (clips.TryGetValue(key, out c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + key);
            if (c != null) { clips[key] = c; return c; }
            MusicJob job;
            if (!musicJobs.TryGetValue(key, out job))
            {
                job = new MusicJob { key = key };
                musicJobs[key] = job;
                int seed; var style = StyleOf(key, out seed);
                System.Threading.ThreadPool.QueueUserWorkItem(_ => { job.data = SynthLib.MusicData(style, seed); job.done = true; });
                return null;
            }
            if (job.done)
            {
                var clip = AudioClip.Create(key, job.data.Length / 2, 2, SynthLib.MusicRate, false);
                clip.SetData(job.data, 0);
                clips[key] = clip;
                musicJobs.Remove(key);
                return clip;
            }
            return null;
        }

        private AudioClip Generate(string key)
        {
            switch (key)
            {
                case "click": return SynthLib.UiClick();
                case "confirm": return SynthLib.UiConfirm();
                case "back": return SynthLib.UiBack();
                case "cash": return SynthLib.Cash();
                case "levelup": return SynthLib.LevelUp();
                case "beep": return SynthLib.CountBeep();
                case "go": return SynthLib.GoBeep();
                case "checkpoint": return SynthLib.Checkpoint();
                case "combo": return SynthLib.ComboUp();
                case "fail": return SynthLib.Fail();
                case "blowoff": return SynthLib.BlowOff();
                case "backfire0": return SynthLib.Backfire(0);
                case "backfire1": return SynthLib.Backfire(1);
                case "backfire2": return SynthLib.Backfire(2);
                case "clunk": return SynthLib.GearClunk();
                case "skid": return SynthLib.SkidLoop();
                case "wind": return SynthLib.WindLoop();
                case "rain": return SynthLib.RainLoop();
                case "city": return SynthLib.CityAmbience();
                case "turbo": return SynthLib.TurboWhine();
                case "impact_l0": return SynthLib.Impact(0.2f, 0);
                case "impact_l1": return SynthLib.Impact(0.25f, 1);
                case "impact_h0": return SynthLib.Impact(0.8f, 2);
                case "impact_h1": return SynthLib.Impact(1f, 3);
                case "music_menu": return SynthLib.Music("menu", SynthLib.MusicStyle.Menu, 3);
                case "music_phonk1": return SynthLib.Music("phonk1", SynthLib.MusicStyle.Phonk, 5);
                case "music_phonk2": return SynthLib.Music("phonk2", SynthLib.MusicStyle.Phonk, 11);
                case "music_drive": return SynthLib.Music("drive", SynthLib.MusicStyle.Drive, 7);
                case "music_night": return SynthLib.Music("night", SynthLib.MusicStyle.Night, 2);
            }
            return null;
        }

        /// <summary>Engine layer clips are cached per cylinder count / character.</summary>
        public AudioClip[] GetEngineLayers(int cylinders, float tone)
        {
            AudioClip ia = Resources.Load<AudioClip>("Audio/engine_low"), ib = Resources.Load<AudioClip>("Audio/engine_mid"), ic = Resources.Load<AudioClip>("Audio/engine_high");
            if (ia != null && ib != null && ic != null) return new[] { ia, ib, ic };      // recorded loops (reference rpm 1500 / 3500 / 6000, 4-cyl pitch basis)
            string k = "eng_" + cylinders + "_" + Mathf.RoundToInt(tone * 10f);
            AudioClip a, b, c;
            if (!clips.TryGetValue(k + "a", out a))
            {
                float Hz(float rpm) { return rpm / 60f * cylinders * 0.5f; }
                float rough = Mathf.Clamp(0.25f / tone, 0.1f, 0.5f);
                a = SynthLib.EngineLoop(k + "a", Hz(1500f), 0.25f, rough, 1 + cylinders);
                b = SynthLib.EngineLoop(k + "b", Hz(3500f), 0.55f, rough * 0.8f, 2 + cylinders);
                c = SynthLib.EngineLoop(k + "c", Hz(6000f), 0.9f, rough * 0.6f, 3 + cylinders);
                clips[k + "a"] = a; clips[k + "b"] = b; clips[k + "c"] = c;
            }
            else { b = clips[k + "b"]; c = clips[k + "c"]; }
            return new[] { a, b, c };
        }

        public IEnumerator PrewarmCoroutine()
        {
            string[] keys = { "click", "confirm", "back", "cash", "levelup", "beep", "go", "checkpoint", "combo", "fail" };
            foreach (var k in keys) { Get(k); }
            yield return null;
            TryGetMusic("music_menu");
            yield return null;
        }

        // ---------------------------------------------------------------- one-shots
        public void Play(string key, float volume = 1f, float pitch = 1f)
        {
            var c = Get(key);
            if (c == null) return;
            var s = pool[poolIdx]; poolIdx = (poolIdx + 1) % pool.Length;
            s.clip = c; s.volume = volume * SfxVolume; s.pitch = pitch; s.spatialBlend = 0f; s.loop = false;
            s.Play();
        }

        public void PlayAt(string key, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var c = Get(key);
            if (c == null) return;
            AudioSource.PlayClipAtPoint(c, pos, Mathf.Clamp01(volume * SfxVolume));
        }

        public static void Click() { Instance.Play("click", 0.8f); }
        public static void Confirm() { Instance.Play("confirm", 0.9f); }
        public static void Back() { Instance.Play("back", 0.8f); }

        // ---------------------------------------------------------------- music
        public void StartPlaylist(params string[] tracks)
        {
            playlist = new List<string>(tracks);
            playIdx = 0;
            loopsLeft = 0;
            musicRunning = true;
            StopAllMusicFast();
            PlayNextTrack(true);
        }

        public void StopMusic() { musicRunning = false; }
        public void DuckMusic(float v) { musicDuck = v; }

        private void StopAllMusicFast()
        {
            musicA.Stop(); musicB.Stop(); musicA.volume = musicB.volume = 0f;
        }

        private AudioSource Cur { get { return musicOnA ? musicA : musicB; } }
        private AudioSource Other { get { return musicOnA ? musicB : musicA; } }

        private float retryTimer;

        private void PlayNextTrack(bool first)
        {
            if (playlist.Count == 0) return;
            string key = playlist[playIdx % playlist.Count];
            var clip = TryGetMusic(key);
            if (clip == null) { pendingFirst = first; waitingForTrack = true; return; }
            waitingForTrack = false;
            musicOnA = !musicOnA;
            var s = Cur;
            s.clip = clip; s.time = 0f; s.volume = 0f; s.Play();
            musicFade = first ? 1.5f : 0f;
            loopsLeft = 1;
            if (!first) musicFade = 0f;
        }

        private void Update()
        {
            if (!musicRunning && musicA.volume <= 0.001f && musicB.volume <= 0.001f) return;
            float dt = Time.unscaledDeltaTime;
            if (waitingForTrack && musicRunning)
            {
                retryTimer -= dt;
                if (retryTimer <= 0f) { retryTimer = 0.25f; PlayNextTrack(pendingFirst); }
            }
            float target = musicRunning ? MusicVolume * 0.7f * musicDuck : 0f;
            var cur = Cur; var other = Other;
            cur.volume = Mathf.MoveTowards(cur.volume, target, dt * 0.5f);
            other.volume = Mathf.MoveTowards(other.volume, 0f, dt * 0.5f);
            if (!musicRunning)
            {
                if (cur.volume <= 0.001f) { cur.Stop(); other.Stop(); }
                return;
            }
            if (waitingForTrack) return;
            if (cur.clip != null && cur.isPlaying && cur.clip.length - cur.time < 1.5f)
            {
                if (loopsLeft > 0) { loopsLeft--; other.clip = cur.clip; other.time = 0f; other.volume = 0f; other.Play(); musicOnA = !musicOnA; }
                else { playIdx++; PlayNextTrack(false); }
            }
            else if (!cur.isPlaying && cur.clip != null && musicRunning) { playIdx++; PlayNextTrack(false); }
        }
    }
}
