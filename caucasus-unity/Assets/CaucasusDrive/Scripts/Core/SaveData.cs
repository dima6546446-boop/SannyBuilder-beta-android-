using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    [Serializable]
    public class CarTune
    {
        public string id;
        public int color = -1;     // -1 — заводской цвет
        public int wheels, height, tint, engine, tires, neon, horn, exhaust;
        public string plateText = "Е213КХ", plateRegion = "26";
        public float damage, dirt;   // 0..1: повреждения кузова и грязь
        // купленные позиции тюнинга (битовые маски по индексу варианта)
        public int ownWheels = 1, ownHeight = 1, ownTint = 1, ownEngine = 1, ownTires = 1, ownNeon = 1, ownHorn = 1, ownExhaust = 1;
    }

    [Serializable]
    public class Settings
    {
        public int quality = -1;       // -1 — автоопределение
        public int controls;           // 0 — руль, 1 — стрелки, 2 — наклон
        public bool assist = true;     // помощь при заносе
        public bool manual;            // механика
        public float volume = 0.8f;
        public bool fines = true;
        public int camera;
        public bool showFps;
        public int radio;
        public bool askFines = true;   // спрашивать «со штрафами или без» при входе в город
        public bool sensors = true;    // парктроник в городе
        public string nick = "";       // ник в онлайне
        public string lastIp = "";     // последний адрес комнаты
    }

    [Serializable]
    public class DailyTask { public string kind; public int goal; public float progress; public bool done, claimed; }

    [Serializable]
    public class DailyState { public string date = ""; public List<DailyTask> tasks = new List<DailyTask>(); }

    [Serializable]
    public class Stats { public float km, walk; public int fines, taxi, bestCombo, crashes, food, washes, online; }

    [Serializable]
    public class DriftStats { public int best, bestSeries, runs; }

    [Serializable]
    public class RaceStats { public float best; public int runs; }

    [Serializable]
    public class SaveState
    {
        public int money = 15000;
        public string current = "vaz2107";
        public List<string> owned = new List<string> { "vaz2107" };
        public List<CarTune> tuning = new List<CarTune>();
        public List<int> stars = new List<int>();
        public Settings settings = new Settings();
        public int driftBest, freeBest;
        public float km;
        public bool license;
        public Stats stats = new Stats();
        public DriftStats drift = new DriftStats();
        public RaceStats race = new RaceStats();
        public DailyState daily = new DailyState();
        public List<string> ach = new List<string>();
    }

    /// <summary>Прогресс в PlayerPrefs (JSON): деньги, гараж, тюнинг, звёзды, настройки.</summary>
    public class SaveData
    {
        const string Key = "caucasusdrive.unity.save.v1";
        public SaveState d;
        public event Action Changed;

        public SaveData()
        {
            try { d = JsonUtility.FromJson<SaveState>(PlayerPrefs.GetString(Key, "")); } catch { d = null; }
            if (d == null) d = new SaveState();
            if (d.owned == null || d.owned.Count == 0) d.owned = new List<string> { "vaz2107" };
            if (d.settings == null) d.settings = new Settings();
            if (d.stats == null) d.stats = new Stats();
            if (d.drift == null) d.drift = new DriftStats();
            if (d.daily == null) d.daily = new DailyState();
            if (d.ach == null) d.ach = new List<string>();
            if (d.daily.tasks == null) d.daily.tasks = new List<DailyTask>();
            while (d.stars.Count < Levels.Count) d.stars.Add(0);
        }

        public void Commit()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public void Reset() { d = new SaveState(); while (d.stars.Count < Levels.Count) d.stars.Add(0); Commit(); }

        public int Money => d.money;
        public void AddMoney(int v) { d.money = Mathf.Max(0, d.money + v); Commit(); }
        public bool Spend(int v) { if (d.money < v) return false; d.money -= v; Commit(); return true; }
        public bool Owns(string id) { return d.owned.Contains(id); }

        public bool Buy(string id)
        {
            var def = Cars.Get(id);
            if (Owns(id) || !Spend(def.price)) return false;
            d.owned.Add(id);
            d.current = id;
            Commit();
            return true;
        }

        public void Select(string id) { if (Owns(id)) { d.current = id; Commit(); } }

        public CarTune Tune(string id)
        {
            foreach (var t in d.tuning) if (t.id == id) return t;
            var n = new CarTune { id = id };
            d.tuning.Add(n);
            return n;
        }

        public int ColorOf(string id)
        {
            var t = Tune(id);
            return t.color >= 0 ? t.color : Cars.Get(id).colors[0];
        }

        public int StarsTotal { get { int s = 0; foreach (var v in d.stars) s += v; return s; } }
        public void SetStars(int level, int s) { if (s > d.stars[level]) { d.stars[level] = s; Commit(); } }
    }
}
