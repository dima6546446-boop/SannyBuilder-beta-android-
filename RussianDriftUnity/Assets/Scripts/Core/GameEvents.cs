using System;

namespace RussianDrift.Core
{
    /// <summary>Snapshot of the live drift state, broadcast every frame while a chain is active.</summary>
    public struct DriftSnapshot
    {
        public float chainScore;
        public float multiplier;
        public float angle;
        public float speedKmh;
        public float chainTime;
        public bool active;
    }

    /// <summary>Static event hub. Keeps modules decoupled (Vehicle never references UI/Meta).</summary>
    public static class GameEvents
    {
        public static event Action<DriftSnapshot> DriftUpdated;
        public static event Action<int, float, float> DriftBanked;      // points, maxMultiplier, duration
        public static event Action<float> DriftPenalty;                  // lost points
        public static event Action<float, bool> VehicleCollision;       // impulse, withTraffic
        public static event Action<float> DistanceDriven;                // metres
        public static event Action Backfire;
        public static event Action<int> LapCompleted;
        public static event Action<int> CheckpointPassed;
        public static event Action<string> AchievementUnlocked;
        public static event Action<long> CurrencyChanged;
        public static event Action<int> LevelUp;
        public static event Action LanguageChanged;
        public static event Action SettingsChanged;
        public static event Action<float> NearMiss;
        public static event Action<float> CrowdExcitement;               // 0..1 for spectators

        public static void RaiseDriftUpdated(DriftSnapshot s) { if (DriftUpdated != null) DriftUpdated(s); }
        public static void RaiseDriftBanked(int p, float m, float d) { if (DriftBanked != null) DriftBanked(p, m, d); }
        public static void RaiseDriftPenalty(float p) { if (DriftPenalty != null) DriftPenalty(p); }
        public static void RaiseCollision(float i, bool t) { if (VehicleCollision != null) VehicleCollision(i, t); }
        public static void RaiseDistance(float m) { if (DistanceDriven != null) DistanceDriven(m); }
        public static void RaiseBackfire() { if (Backfire != null) Backfire(); }
        public static void RaiseLap(int l) { if (LapCompleted != null) LapCompleted(l); }
        public static void RaiseCheckpoint(int c) { if (CheckpointPassed != null) CheckpointPassed(c); }
        public static void RaiseAchievement(string id) { if (AchievementUnlocked != null) AchievementUnlocked(id); }
        public static void RaiseCurrency(long v) { if (CurrencyChanged != null) CurrencyChanged(v); }
        public static void RaiseLevelUp(int l) { if (LevelUp != null) LevelUp(l); }
        public static void RaiseLanguage() { if (LanguageChanged != null) LanguageChanged(); }
        public static void RaiseSettings() { if (SettingsChanged != null) SettingsChanged(); }
        public static void RaiseNearMiss(float d) { if (NearMiss != null) NearMiss(d); }
        public static void RaiseCrowd(float e) { if (CrowdExcitement != null) CrowdExcitement(e); }
    }
}
