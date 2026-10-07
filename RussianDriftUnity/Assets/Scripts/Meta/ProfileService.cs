using System;
using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Meta
{
    public enum PurchaseResult { Ok, NotEnoughMoney, LevelTooLow, AlreadyOwned, MaxLevel, Invalid }

    /// <summary>Player profile logic over SaveData: currency, XP/levels, garage ownership, upgrades and cosmetics.</summary>
    public class ProfileService
    {
        private readonly SaveSystem save;
        public SaveData Data { get { return save.Data; } }
        public SaveSystem Save { get { return save; } }

        public ProfileService(SaveSystem s) { save = s; }

        // ------------------------------------------------------------ currency & xp
        public long Currency { get { return Data.currency; } }

        public void AddCurrency(long amount)
        {
            if (amount == 0) return;
            Data.currency = Math.Max(0, Data.currency + amount);
            GameEvents.RaiseCurrency(Data.currency);
        }

        public bool TrySpend(long amount)
        {
            if (amount < 0 || Data.currency < amount) return false;
            Data.currency -= amount;
            GameEvents.RaiseCurrency(Data.currency);
            return true;
        }

        public static int XpForLevel(int level) { return Mathf.RoundToInt(400f * Mathf.Pow(level, 1.45f)); }
        public int Level { get { return Data.level; } }
        public int Xp { get { return Data.xp; } }
        public int XpToNext { get { return XpForLevel(Data.level); } }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            Data.xp += amount;
            while (Data.xp >= XpForLevel(Data.level) && Data.level < 99)
            {
                Data.xp -= XpForLevel(Data.level);
                Data.level++;
                AddCurrency(1000 + Data.level * 250);
                GameEvents.RaiseLevelUp(Data.level);
            }
        }

        // ------------------------------------------------------------ cars
        public bool OwnsCar(string id) { return Data.ownedCars.Contains(id); }

        public CarSetup CurrentSetup { get { return Data.GetSetup(Data.selectedCar); } }
        public CarDefinition CurrentCar { get { return GameCatalog.GetCar(Data.selectedCar); } }

        public void SelectCar(string id)
        {
            if (!OwnsCar(id)) return;
            Data.selectedCar = id;
            var s = Data.GetSetup(id);
            if (string.IsNullOrEmpty(s.colorHex)) s.colorHex = ColorUtility.ToHtmlStringRGB(GameCatalog.GetCar(id).defaultColor);
            s.drive = s.drive == 0 && GameCatalog.GetCar(id).defaultDrive == DriveType.AWD ? 1 : s.drive;
            save.Save();
        }

        public PurchaseResult BuyCar(CarDefinition def)
        {
            if (def == null) return PurchaseResult.Invalid;
            if (OwnsCar(def.id)) return PurchaseResult.AlreadyOwned;
            if (Data.level < def.requiredLevel) return PurchaseResult.LevelTooLow;
            if (!TrySpend(def.price)) return PurchaseResult.NotEnoughMoney;
            Data.ownedCars.Add(def.id);
            var s = Data.GetSetup(def.id);
            s.colorHex = ColorUtility.ToHtmlStringRGB(def.defaultColor);
            s.drive = (int)def.defaultDrive;
            Data.selectedCar = def.id;
            Data.AddStat("cars_bought", 1);
            save.Save();
            return PurchaseResult.Ok;
        }

        public long SellValue(CarDefinition def) { return Mathf.RoundToInt(def.price * 0.5f); }

        // ------------------------------------------------------------ upgrades
        public int UpgradePrice(UpgradeCategory cat, CarSetup setup)
        {
            var u = GameCatalog.GetUpgrade(cat);
            int next = setup.GetLevel(cat) + 1;
            return u == null ? 0 : u.PriceForLevel(next);
        }

        public PurchaseResult BuyUpgrade(UpgradeCategory cat, CarSetup setup)
        {
            var u = GameCatalog.GetUpgrade(cat);
            if (u == null) return PurchaseResult.Invalid;
            int lv = setup.GetLevel(cat);
            if (lv >= u.maxLevel) return PurchaseResult.MaxLevel;
            if (!TrySpend(u.PriceForLevel(lv + 1))) return PurchaseResult.NotEnoughMoney;
            setup.SetLevel(cat, lv + 1);
            Data.AddStat("upgrades_bought", 1);
            save.Save();
            return PurchaseResult.Ok;
        }

        // ------------------------------------------------------------ cosmetics
        private static string Key(CosmeticCategory c, string id) { return c + ":" + id; }

        public bool OwnsCosmetic(CarSetup setup, CosmeticDefinition d)
        {
            return d.price == 0 || setup.ownedParts.Contains(Key(d.category, d.id));
        }

        public PurchaseResult BuyCosmetic(CarSetup setup, CosmeticDefinition d)
        {
            if (d == null) return PurchaseResult.Invalid;
            if (OwnsCosmetic(setup, d)) return PurchaseResult.AlreadyOwned;
            if (Data.level < d.requiredLevel) return PurchaseResult.LevelTooLow;
            if (!TrySpend(d.price)) return PurchaseResult.NotEnoughMoney;
            setup.ownedParts.Add(Key(d.category, d.id));
            save.Save();
            return PurchaseResult.Ok;
        }

        public static void Equip(CarSetup setup, CosmeticDefinition d)
        {
            switch (d.category)
            {
                case CosmeticCategory.Wrap: setup.wrapId = d.id; break;
                case CosmeticCategory.Spoiler: setup.spoilerId = d.id; break;
                case CosmeticCategory.BodyKit: setup.bodykitId = d.id; break;
                case CosmeticCategory.Rims: setup.rimId = d.id; break;
                case CosmeticCategory.Neon: setup.neonId = d.id; break;
                case CosmeticCategory.Decal: setup.decalId = d.id; break;
            }
        }

        public static string EquippedId(CarSetup setup, CosmeticCategory c)
        {
            switch (c)
            {
                case CosmeticCategory.Wrap: return setup.wrapId;
                case CosmeticCategory.Spoiler: return setup.spoilerId;
                case CosmeticCategory.BodyKit: return setup.bodykitId;
                case CosmeticCategory.Rims: return setup.rimId;
                case CosmeticCategory.Neon: return setup.neonId;
                default: return setup.decalId;
            }
        }

        // ------------------------------------------------------------ stats / records
        public void RecordBest(string key, float value, bool higherIsBetter = true)
        {
            float cur = Data.GetBest(key);
            if (cur <= 0f || (higherIsBetter ? value > cur : value < cur)) Data.SetBest(key, value);
        }
    }
}
