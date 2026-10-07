using System;
using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    public enum DriveType { RWD = 0, AWD = 1 }

    [Serializable]
    public class ControlPlacement
    {
        public string id;
        public float x;      // normalised offset 0..1 of screen width (anchor-relative)
        public float y;
        public float scale = 1f;
    }

    [Serializable]
    public class GameSettings
    {
        public int language = 0;            // 0 = RU, 1 = EN
        public int graphicsQuality = 3;     // 0 low, 1 medium, 2 high, 3 auto
        public int targetFps = 60;
        public float masterVolume = 1f;
        public float musicVolume = 0.6f;
        public float sfxVolume = 1f;
        public int controlScheme = 0;       // 0 buttons, 1 tilt, 2 on-screen wheel
        public float tiltSensitivity = 1f;
        public float tiltNeutral = 0f;
        public bool steeringAssist = true;
        public bool driftAssist = true;
        public bool abs = true;
        public bool tcs = false;
        public bool autoGearbox = true;
        public bool cameraShake = true;
        public bool motionBlur = true;
        public bool showMinimap = true;
        public bool useMph = false;
        public bool vibration = true;
        public bool tiltInvert = false;
        public float controlOpacity = 0.7f;
        public List<ControlPlacement> layout = new List<ControlPlacement>();

        public ControlPlacement GetPlacement(string id)
        {
            for (int i = 0; i < layout.Count; i++) if (layout[i].id == id) return layout[i];
            var p = new ControlPlacement { id = id, x = 0f, y = 0f, scale = 1f };
            layout.Add(p);
            return p;
        }
    }

    [Serializable]
    public class CarSetup
    {
        public string carId;
        // performance upgrade levels (0..max)
        public int engine, turbo, suspension, brakes, tires, differential, weight;
        public int drive = 0;                 // DriveType
        public float clearance = 0f;          // -1..1 additional ride height offset
        public float camber = 0f;             // 0..1 visual + handling
        // visual
        public string colorHex = "";
        public string wrapId = "none";
        public string spoilerId = "none";
        public string bodykitId = "none";
        public string rimId = "rim_5star";
        public string rimColorHex = "C8C8C8";
        public string neonId = "none";
        public int tint = 1;                  // 0..3
        public string decalId = "none";
        public string plate = "A777AA";
        public string region = "77";
        public List<string> ownedParts = new List<string>();

        public int GetLevel(UpgradeCategory c)
        {
            switch (c)
            {
                case UpgradeCategory.Engine: return engine;
                case UpgradeCategory.Turbo: return turbo;
                case UpgradeCategory.Suspension: return suspension;
                case UpgradeCategory.Brakes: return brakes;
                case UpgradeCategory.Tires: return tires;
                case UpgradeCategory.Differential: return differential;
                default: return weight;
            }
        }

        public void SetLevel(UpgradeCategory c, int v)
        {
            switch (c)
            {
                case UpgradeCategory.Engine: engine = v; break;
                case UpgradeCategory.Turbo: turbo = v; break;
                case UpgradeCategory.Suspension: suspension = v; break;
                case UpgradeCategory.Brakes: brakes = v; break;
                case UpgradeCategory.Tires: tires = v; break;
                case UpgradeCategory.Differential: differential = v; break;
                default: weight = v; break;
            }
        }

        public CarSetup Clone() { return JsonUtility.FromJson<CarSetup>(JsonUtility.ToJson(this)); }
    }

    [Serializable]
    public class DriverLook
    {
        public int outfit = 0;     // 0..4 jacket styles
        public int cap = 0;        // 0 none, 1..3 caps
        public int glasses = 0;    // 0 none, 1..2
        public string skinHex = "E0B48C";
        public string outfitHex = "2B3A55";
    }

    [Serializable]
    public class QuestProgress
    {
        public string id;
        public float progress;
        public bool claimed;
    }

    [Serializable]
    public class KeyFloat { public string key; public float value; }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public long currency = 25000;
        public int xp = 0;
        public int level = 1;
        public string selectedCar = "kopeyka";
        public List<string> ownedCars = new List<string>(new[] { "kopeyka" });
        public List<CarSetup> setups = new List<CarSetup>();
        public DriverLook driver = new DriverLook();
        public GameSettings settings = new GameSettings();
        public List<string> achievements = new List<string>();
        public List<KeyFloat> stats = new List<KeyFloat>();
        public List<KeyFloat> bestScores = new List<KeyFloat>();
        public List<QuestProgress> quests = new List<QuestProgress>();
        public string questDay = "";
        public string lastDailyReward = "";
        public int dailyStreak = 0;
        public bool tutorialDone = false;
        public long lastSavedUtcTicks = 0;

        public CarSetup GetSetup(string carId)
        {
            for (int i = 0; i < setups.Count; i++) if (setups[i].carId == carId) return setups[i];
            var s = new CarSetup { carId = carId };
            setups.Add(s);
            return s;
        }

        public float GetStat(string key)
        {
            for (int i = 0; i < stats.Count; i++) if (stats[i].key == key) return stats[i].value;
            return 0f;
        }

        public void SetStat(string key, float v)
        {
            for (int i = 0; i < stats.Count; i++) if (stats[i].key == key) { stats[i].value = v; return; }
            stats.Add(new KeyFloat { key = key, value = v });
        }

        public void AddStat(string key, float delta) { SetStat(key, GetStat(key) + delta); }

        public float GetBest(string key)
        {
            for (int i = 0; i < bestScores.Count; i++) if (bestScores[i].key == key) return bestScores[i].value;
            return 0f;
        }

        public void SetBest(string key, float v)
        {
            for (int i = 0; i < bestScores.Count; i++) if (bestScores[i].key == key) { bestScores[i].value = v; return; }
            bestScores.Add(new KeyFloat { key = key, value = v });
        }
    }
}
