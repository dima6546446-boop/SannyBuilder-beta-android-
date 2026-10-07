using System;
using System.Collections.Generic;

namespace Zanos.Core
{
    [Serializable] public class IntPair { public string key; public int value; }
    [Serializable] public class StringList { public string slot; public List<string> items = new List<string>(); }

    [Serializable]
    public class CarState
    {
        public string id, paint = "#b3262b", wheels = "steel", bodykit = "none", spoiler = "none", neon = "none";
        public List<IntPair> tuning = new List<IntPair>();
        public List<StringList> owned = new List<StringList>();
        public int Tune(string part) { foreach (var t in tuning) if (t.key == part) return t.value; return 0; }
        public void SetTune(string part, int v)
        {
            foreach (var t in tuning) if (t.key == part) { if (v <= 0) tuning.Remove(t); else t.value = v; return; }
            if (v > 0) tuning.Add(new IntPair { key = part, value = v });
        }
        public Dictionary<string, int> TuningMap() { var d = new Dictionary<string, int>(); foreach (var t in tuning) d[t.key] = t.value; return d; }
    }

    [Serializable]
    public class Settings
    {
        public int quality = 2; public bool shadows = true, post = true, effects = true, speedFx = true, showFps;
        public float fov = 70, master = 0.8f, engine = 0.7f, sfx = 0.8f, music = 0.4f, assist = 0.7f, steerSens = 1f, deadzone = 0.1f;
        public bool manual; public int camera; public bool mph; public bool hintsSeen;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1, money = 3000, xp = 0, level = 1;
        public List<CarState> cars = new List<CarState>();
        public string selected = "kopeyka";
        public Settings settings = new Settings();
        public List<IntPair> records = new List<IntPair>();
        public List<IntPair> challenges = new List<IntPair>();     // id -> число успешных прохождений
        public int runs, totalScore, bestChain, bestAngle, earned;
    }

    public struct FinishResult { public int Money, Xp, LevelUps, Level; public bool NewRecord, FirstChallenge; public int ChallengeMoney; }

    /// <summary>Прогресс игрока: деньги, опыт, уровни, покупки, тюнинг. Порт rcd/src/game/progression.js (хранилище подключает слой Unity).</summary>
    public sealed class Progress
    {
        public const int MaxLevel = 30; public const double SellRatio = 0.6;
        public SaveData Data;
        public Action OnChanged;

        public Progress(SaveData data = null)
        {
            Data = data ?? new SaveData();
            if (Data.cars.Count == 0) Data.cars.Add(NewCar(CarCatalog.ById("kopeyka")));
            if (Car(Data.selected) == null) Data.selected = Data.cars[0].id;
            Data.level = LevelFromXp(Data.xp);
        }

        public static int XpForLevel(int lvl) { return (int)Math.Round(260 + 140 * Math.Pow(lvl, 1.3)); }
        public static int MoneyForScore(long score) { return (int)Math.Floor(score / 3.5); }
        public static int XpForScore(long score) { return (int)Math.Floor(score / 7.0); }

        public static int LevelFromXp(int xp) { int l = 1; while (l < MaxLevel && xp >= XpForLevel(l)) { xp -= XpForLevel(l); l++; } return l; }
        public void XpInLevel(out int xp, out int need)
        {
            xp = Data.xp; int l = 1; while (l < MaxLevel && xp >= XpForLevel(l)) { xp -= XpForLevel(l); l++; }
            need = l >= MaxLevel ? 0 : XpForLevel(l);
        }

        static CarState NewCar(CarSpec def) { return new CarState { id = def.Id, paint = def.Color }; }
        void Changed() { if (OnChanged != null) OnChanged(); }

        public CarState Car(string id) { foreach (var c in Data.cars) if (c.id == id) return c; return null; }
        public bool Owns(string id) { return Car(id) != null; }
        public bool CarUnlocked(string id) { return CarCatalog.ById(id).Level <= Data.level; }
        public bool CanBuy(string id) { return !Owns(id) && CarUnlocked(id) && Data.money >= CarCatalog.ById(id).Price; }

        public string Buy(string id)
        {
            var c = CarCatalog.ById(id);
            if (Owns(id)) return "Уже куплена";
            if (!CarUnlocked(id)) return "Нужен уровень " + c.Level;
            if (Data.money < c.Price) return "Не хватает денег";
            Data.money -= c.Price; Data.cars.Add(NewCar(c)); Data.selected = id; Changed(); return null;
        }

        public int InvestedIn(string id)
        {
            var st = Car(id); if (st == null) return 0; int sum = 0;
            foreach (var t in st.tuning) for (int l = 1; l <= t.value; l++) sum += CarSpec.UpgradePrice(t.key, l);
            sum += PriceOf(CarCatalog.Wheels, st.wheels) + PriceOf(CarCatalog.BodyKits, st.bodykit) + PriceOf(CarCatalog.Spoilers, st.spoiler) + PriceOf(CarCatalog.Neons, st.neon);
            return sum;
        }
        static int PriceOf(CarCatalog.Part[] list, string id) { foreach (var p in list) if (p.Id == id) return p.Price; return 0; }
        public int SellValue(string id) { return (int)Math.Floor((CarCatalog.ById(id).Price + InvestedIn(id)) * SellRatio); }

        public string Sell(string id, out int value)
        {
            value = 0;
            var st = Car(id); if (st == null) return "Нет такой машины";
            if (Data.cars.Count <= 1) return "Нельзя продать последнюю машину";
            value = SellValue(id); Data.money += value; Data.cars.Remove(st);
            if (Data.selected == id) Data.selected = Data.cars[0].id;
            Changed(); return null;
        }
        public bool Select(string id) { if (!Owns(id)) return false; Data.selected = id; Changed(); return true; }

        // ---- тюнинг ----
        public bool NextUpgrade(string id, string part, out int level, out int price)
        {
            level = 0; price = 0; var st = Car(id);
            foreach (var p in CarCatalog.Tuning) if (p.Id == part)
            {
                int lvl = st.Tune(part); if (lvl >= p.Max) return false;
                level = lvl + 1; price = CarSpec.UpgradePrice(part, level); return true;
            }
            return false;
        }
        public string Upgrade(string id, string part)
        {
            int level, price;
            if (!NextUpgrade(id, part, out level, out price)) return "Максимум";
            if (Data.money < price) return "Не хватает денег";
            Data.money -= price; Car(id).SetTune(part, level); Changed(); return null;
        }
        public int Downgrade(string id, string part)
        {
            var st = Car(id); int lvl = st.Tune(part); if (lvl <= 0) return -1;
            int back = (int)Math.Floor(CarSpec.UpgradePrice(part, lvl) * 0.5);
            Data.money += back; st.SetTune(part, lvl - 1); Changed(); return back;
        }

        // ---- внешний вид ----
        public void SetPaint(string id, string color) { Car(id).paint = color; Changed(); }
        static CarCatalog.Part[] SlotList(string slot)
        {
            switch (slot) { case "wheels": return CarCatalog.Wheels; case "bodykit": return CarCatalog.BodyKits; case "spoiler": return CarCatalog.Spoilers; default: return CarCatalog.Neons; }
        }
        List<string> OwnedList(CarState st, string slot)
        {
            foreach (var o in st.owned) if (o.slot == slot) return o.items;
            var n = new StringList { slot = slot }; n.items.Add(SlotList(slot)[0].Id); st.owned.Add(n); return n.items;
        }
        public bool PartOwned(string id, string slot, string partId) { return OwnedList(Car(id), slot).Contains(partId); }
        public string SetPart(string id, string slot, string partId)
        {
            CarCatalog.Part it = null; foreach (var p in SlotList(slot)) if (p.Id == partId) it = p;
            if (it == null) return "Нет такого элемента";
            var st = Car(id); var have = OwnedList(st, slot);
            if (!have.Contains(partId)) { if (Data.money < it.Price) return "Не хватает денег"; Data.money -= it.Price; have.Add(partId); }
            switch (slot) { case "wheels": st.wheels = partId; break; case "bodykit": st.bodykit = partId; break; case "spoiler": st.spoiler = partId; break; default: st.neon = partId; break; }
            Changed(); return null;
        }

        // ---- заезды ----
        static int Get(List<IntPair> l, string k) { foreach (var p in l) if (p.key == k) return p.value; return 0; }
        static void Set(List<IntPair> l, string k, int v) { foreach (var p in l) if (p.key == k) { p.value = v; return; } l.Add(new IntPair { key = k, value = v }); }
        public int Record(string mapId, string modeOrChallenge) { return Get(Data.records, mapId + ":" + modeOrChallenge); }
        public bool ChallengeDone(string id) { return Get(Data.challenges, id) > 0; }
        public bool MapUnlocked(MapData m) { return m.level <= Data.level; }

        public FinishResult FinishRun(RunSummary s)
        {
            var d = Data; long score = s.Score;
            int money = MoneyForScore(score), xp = XpForScore(score);
            var res = new FinishResult();
            if (s.Mode == GameMode.Timed) { money += s.Stars * 400; xp += s.Stars * 60; }
            if (s.Mode == GameMode.Challenge && s.Success)
            {
                bool first = !ChallengeDone(s.ChallengeId); double k = first ? 1 : 0.25;
                int cm = (int)Math.Floor(s.RewardMoney * k), cx = (int)Math.Floor(s.RewardXp * k);
                money += cm; xp += cx; res.ChallengeMoney = cm; res.FirstChallenge = first;
                Set(d.challenges, s.ChallengeId, Get(d.challenges, s.ChallengeId) + 1);
            }
            d.money += money;
            int before = d.level; d.xp += Math.Max(0, xp); d.level = LevelFromXp(d.xp);
            string key = s.MapId + ":" + (s.Mode == GameMode.Challenge ? s.ChallengeId : s.Mode.ToString().ToLowerInvariant());
            res.NewRecord = score > Get(d.records, key); if (res.NewRecord) Set(d.records, key, (int)score);
            d.runs++; d.totalScore += (int)score; d.earned += money;
            d.bestChain = Math.Max(d.bestChain, (int)Math.Round(s.BestChain)); d.bestAngle = Math.Max(d.bestAngle, (int)Math.Round(s.BestAngle));
            res.Money = money; res.Xp = xp; res.LevelUps = d.level - before; res.Level = d.level;
            Changed(); return res;
        }
    }
}
