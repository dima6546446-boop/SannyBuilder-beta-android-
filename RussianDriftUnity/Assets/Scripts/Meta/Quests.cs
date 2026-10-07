using System;
using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Meta
{
    public enum QuestMetric { DriftPoints, BestChain, DistanceKm, NearMiss, MaxCombo, BattleWins, Backfires, Laps, Chains }

    public class QuestDef
    {
        public string id;
        public QuestMetric metric;
        public float target;
        public int reward;
        public int xp;
        public string ruFormat, enFormat;
        public bool isMax;     // progress = max value reached (not cumulative)
    }

    /// <summary>Daily quests (3 per day, deterministic from the date), achievements and the 7-day login reward.</summary>
    public class QuestService
    {
        private readonly ProfileService profile;
        public readonly List<QuestDef> today = new List<QuestDef>();

        private static readonly QuestDef[] pool =
        {
            new QuestDef { id = "q_pts1", metric = QuestMetric.DriftPoints, target = 8000, reward = 1500, xp = 150, ruFormat = "Набери {0} очков дрифта", enFormat = "Score {0} drift points" },
            new QuestDef { id = "q_pts2", metric = QuestMetric.DriftPoints, target = 25000, reward = 3500, xp = 300, ruFormat = "Набери {0} очков дрифта", enFormat = "Score {0} drift points" },
            new QuestDef { id = "q_chain1", metric = QuestMetric.BestChain, target = 3000, reward = 1800, xp = 200, ruFormat = "Один занос на {0}+ очков", enFormat = "A single drift worth {0}+ points", isMax = true },
            new QuestDef { id = "q_chain2", metric = QuestMetric.BestChain, target = 8000, reward = 3200, xp = 300, ruFormat = "Один занос на {0}+ очков", enFormat = "A single drift worth {0}+ points", isMax = true },
            new QuestDef { id = "q_dist", metric = QuestMetric.DistanceKm, target = 6, reward = 1200, xp = 120, ruFormat = "Проедь {0} км", enFormat = "Drive {0} km" },
            new QuestDef { id = "q_near", metric = QuestMetric.NearMiss, target = 8, reward = 1600, xp = 160, ruFormat = "Сделай {0} опасных обгонов", enFormat = "Do {0} near misses" },
            new QuestDef { id = "q_combo", metric = QuestMetric.MaxCombo, target = 5, reward = 2200, xp = 220, ruFormat = "Достигни множителя x{0}", enFormat = "Reach a x{0} multiplier", isMax = true },
            new QuestDef { id = "q_battle", metric = QuestMetric.BattleWins, target = 2, reward = 4000, xp = 400, ruFormat = "Выиграй {0} баттла", enFormat = "Win {0} battles" },
            new QuestDef { id = "q_back", metric = QuestMetric.Backfires, target = 15, reward = 1000, xp = 100, ruFormat = "Сделай {0} выстрелов из выхлопа", enFormat = "Fire {0} exhaust flames" },
            new QuestDef { id = "q_laps", metric = QuestMetric.Laps, target = 5, reward = 2000, xp = 200, ruFormat = "Проедь {0} кругов", enFormat = "Complete {0} laps" },
            new QuestDef { id = "q_chains", metric = QuestMetric.Chains, target = 12, reward = 1700, xp = 170, ruFormat = "Заверши {0} дрифт-цепочек", enFormat = "Bank {0} drift chains" },
        };

        public QuestService(ProfileService p)
        {
            profile = p;
            Refresh();
            GameEvents.DriftBanked += OnBanked;
            GameEvents.DistanceDriven += OnDistance;
            GameEvents.NearMiss += OnNear;
            GameEvents.Backfire += OnBackfire;
            GameEvents.LapCompleted += OnLap;
        }

        public static string DayKey { get { return DateTime.UtcNow.ToString("yyyy-MM-dd"); } }

        public void Refresh()
        {
            var d = profile.Data;
            string key = DayKey;
            if (d.questDay != key)
            {
                d.questDay = key;
                d.quests.Clear();
                int seed = 17; foreach (char ch in key) seed = seed * 31 + ch;   // stable across platforms (string.GetHashCode is not)
                var rnd = new System.Random(seed);
                var idx = new List<int>();
                for (int i = 0; i < pool.Length; i++) idx.Add(i);
                for (int n = 0; n < 3; n++)
                {
                    int pick = rnd.Next(idx.Count);
                    var q = pool[idx[pick]];
                    // avoid two quests of the same metric
                    idx.RemoveAll(i => pool[i].metric == q.metric);
                    d.quests.Add(new QuestProgress { id = q.id, progress = 0f, claimed = false });
                }
                profile.Save.Save();
            }
            today.Clear();
            foreach (var qp in d.quests) { var q = Find(qp.id); if (q != null) today.Add(q); }
        }

        public static QuestDef Find(string id) { foreach (var q in pool) if (q.id == id) return q; return null; }

        public QuestProgress ProgressOf(string id)
        {
            foreach (var qp in profile.Data.quests) if (qp.id == id) return qp;
            return null;
        }

        public static string Describe(QuestDef q)
        {
            string val = q.metric == QuestMetric.DistanceKm ? q.target.ToString("0") : Mathf.RoundToInt(q.target).ToString();
            return string.Format(Loc.IsRu ? q.ruFormat : q.enFormat, val);
        }

        private void Add(QuestMetric m, float amount)
        {
            bool changed = false;
            foreach (var q in today)
            {
                if (q.metric != m) continue;
                var qp = ProgressOf(q.id);
                if (qp == null || qp.claimed) continue;
                float before = qp.progress;
                qp.progress = q.isMax ? Mathf.Max(qp.progress, amount) : Mathf.Min(q.target, qp.progress + amount);
                if (qp.progress > before) changed = true;
            }
            if (changed) GameEvents.RaiseSettings();   // lightweight "profile changed" ping for UI refresh
        }

        private void OnBanked(int pts, float mul, float dur)
        {
            Add(QuestMetric.DriftPoints, pts);
            Add(QuestMetric.BestChain, pts);
            Add(QuestMetric.MaxCombo, mul);
            Add(QuestMetric.Chains, 1);
            var d = profile.Data;
            d.AddStat("drift_total", pts);
            d.AddStat("chains", 1);
            if (pts > d.GetStat("best_chain")) d.SetStat("best_chain", pts);
            if (mul > d.GetStat("best_combo")) d.SetStat("best_combo", mul);
        }

        private void OnDistance(float m) { Add(QuestMetric.DistanceKm, m / 1000f); profile.Data.AddStat("distance_m", m); }
        private void OnNear(float d) { Add(QuestMetric.NearMiss, 1); profile.Data.AddStat("near_miss", 1); }
        private void OnBackfire() { Add(QuestMetric.Backfires, 1); profile.Data.AddStat("backfires", 1); }
        private void OnLap(int lap) { Add(QuestMetric.Laps, 1); profile.Data.AddStat("laps", 1); }
        public void ReportBattleWin() { Add(QuestMetric.BattleWins, 1); profile.Data.AddStat("battle_wins", 1); }

        public bool IsComplete(QuestDef q) { var p = ProgressOf(q.id); return p != null && p.progress >= q.target - 0.001f; }

        public bool Claim(QuestDef q)
        {
            var p = ProgressOf(q.id);
            if (p == null || p.claimed || !IsComplete(q)) return false;
            p.claimed = true;
            profile.AddCurrency(q.reward);
            profile.AddXp(q.xp);
            profile.Data.AddStat("quests_done", 1);
            profile.Save.Save();
            return true;
        }

        // ------------------------------------------------------------ daily login reward
        public static readonly int[] DailyRewards = { 1000, 1500, 2500, 3500, 5000, 7500, 15000 };

        public bool DailyAvailable { get { return profile.Data.lastDailyReward != DayKey; } }

        public int NextDailyIndex
        {
            get
            {
                var d = profile.Data;
                if (string.IsNullOrEmpty(d.lastDailyReward)) return 0;
                DateTime last;
                if (!DateTime.TryParse(d.lastDailyReward, out last)) return 0;
                double days = (DateTime.UtcNow.Date - last.Date).TotalDays;
                if (days > 1.5) return 0;                  // streak broken
                return d.dailyStreak % DailyRewards.Length;
            }
        }

        public int ClaimDaily()
        {
            if (!DailyAvailable) return 0;
            var d = profile.Data;
            // anti clock-rollback: refuse if the device clock is earlier than the last save
            if (d.lastSavedUtcTicks > 0 && DateTime.UtcNow.Ticks < d.lastSavedUtcTicks - TimeSpan.TicksPerHour * 2) return 0;
            int idx = NextDailyIndex;
            int reward = DailyRewards[idx];
            d.dailyStreak = idx + 1;
            d.lastDailyReward = DayKey;
            profile.AddCurrency(reward);
            profile.Save.Save();
            return reward;
        }
    }

    public class AchievementDef
    {
        public string id, ruName, enName, ruDesc, enDesc, stat;
        public float target;
        public int reward;
    }

    public class AchievementService
    {
        private readonly ProfileService profile;
        public static readonly AchievementDef[] All =
        {
            new AchievementDef { id = "a_first", stat = "chains", target = 1, reward = 500, ruName = "Первый занос", enName = "First slide", ruDesc = "Заверши первую дрифт-цепочку", enDesc = "Bank your first drift chain" },
            new AchievementDef { id = "a_100k", stat = "drift_total", target = 100000, reward = 5000, ruName = "Сто тысяч", enName = "Hundred thousand", ruDesc = "Набери 100 000 очков дрифта", enDesc = "Score 100,000 total drift points" },
            new AchievementDef { id = "a_1m", stat = "drift_total", target = 1000000, reward = 30000, ruName = "Миллионщик", enName = "Millionaire", ruDesc = "Набери 1 000 000 очков дрифта", enDesc = "Score 1,000,000 total drift points" },
            new AchievementDef { id = "a_chain10k", stat = "best_chain", target = 10000, reward = 4000, ruName = "Долгий занос", enName = "Long slide", ruDesc = "Одна цепочка на 10 000 очков", enDesc = "A single chain worth 10,000 points" },
            new AchievementDef { id = "a_combo8", stat = "best_combo", target = 8, reward = 3000, ruName = "Мастер комбо", enName = "Combo master", ruDesc = "Множитель x8", enDesc = "Reach a x8 multiplier" },
            new AchievementDef { id = "a_dist", stat = "distance_m", target = 100000, reward = 6000, ruName = "Дальнобойщик", enName = "Long hauler", ruDesc = "Проедь 100 км", enDesc = "Drive 100 km" },
            new AchievementDef { id = "a_near", stat = "near_miss", target = 50, reward = 3500, ruName = "На волоске", enName = "Close shave", ruDesc = "50 опасных обгонов", enDesc = "50 near misses" },
            new AchievementDef { id = "a_flames", stat = "backfires", target = 100, reward = 2500, ruName = "Огнедышащий", enName = "Fire breather", ruDesc = "100 выстрелов из выхлопа", enDesc = "100 exhaust flames" },
            new AchievementDef { id = "a_cars3", stat = "cars_bought", target = 3, reward = 5000, ruName = "Коллекционер", enName = "Collector", ruDesc = "Купи 3 машины", enDesc = "Buy 3 cars" },
            new AchievementDef { id = "a_tuner", stat = "upgrades_bought", target = 15, reward = 4000, ruName = "Тюнер", enName = "Tuner", ruDesc = "Купи 15 улучшений", enDesc = "Buy 15 upgrades" },
            new AchievementDef { id = "a_battle", stat = "battle_wins", target = 5, reward = 6000, ruName = "Король баттлов", enName = "Battle king", ruDesc = "Выиграй 5 дрифт-баттлов", enDesc = "Win 5 drift battles" },
            new AchievementDef { id = "a_laps", stat = "laps", target = 25, reward = 3000, ruName = "Круговой", enName = "Circuit runner", ruDesc = "Проедь 25 кругов", enDesc = "Complete 25 laps" },
            new AchievementDef { id = "a_quests", stat = "quests_done", target = 10, reward = 5000, ruName = "Исполнительный", enName = "Reliable", ruDesc = "Выполни 10 ежедневных заданий", enDesc = "Complete 10 daily quests" },
        };

        public AchievementService(ProfileService p) { profile = p; }

        public float Value(AchievementDef a) { return profile.Data.GetStat(a.stat); }
        public bool IsUnlocked(AchievementDef a) { return profile.Data.achievements.Contains(a.id); }

        /// <summary>Checks all achievements; unlocks and pays out new ones. Call after results and on profile changes.</summary>
        public List<AchievementDef> Check()
        {
            var newly = new List<AchievementDef>();
            foreach (var a in All)
            {
                if (IsUnlocked(a)) continue;
                if (Value(a) >= a.target)
                {
                    profile.Data.achievements.Add(a.id);
                    profile.AddCurrency(a.reward);
                    newly.Add(a);
                    GameEvents.RaiseAchievement(a.id);
                }
            }
            if (newly.Count > 0) profile.Save.Save();
            return newly;
        }
    }
}
