using System;
using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>
    /// Central read access to all static data. Loads the GameCatalog registry asset (or ScriptableObjects under Resources),
    /// and falls back to code-defined defaults (DefaultCatalog) so the game also runs before the editor build step.
    /// </summary>
    public static class GameCatalog
    {
        public static List<CarDefinition> Cars { get; private set; }
        public static List<UpgradeDefinition> Upgrades { get; private set; }
        public static List<CosmeticDefinition> Cosmetics { get; private set; }
        public static List<TrackDefinition> Tracks { get; private set; }

        private static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            var reg = Resources.Load<GameCatalogAsset>("GameCatalog");
            if (reg != null)
            {
                Cars = new List<CarDefinition>(reg.cars ?? new CarDefinition[0]);
                Upgrades = new List<UpgradeDefinition>(reg.upgrades ?? new UpgradeDefinition[0]);
                Cosmetics = new List<CosmeticDefinition>(reg.cosmetics ?? new CosmeticDefinition[0]);
                Tracks = new List<TrackDefinition>(reg.tracks ?? new TrackDefinition[0]);
            }
            else
            {
                Cars = new List<CarDefinition>(Resources.LoadAll<CarDefinition>("ScriptableObjects/Cars"));
                Upgrades = new List<UpgradeDefinition>(Resources.LoadAll<UpgradeDefinition>("ScriptableObjects/Upgrades"));
                Cosmetics = new List<CosmeticDefinition>(Resources.LoadAll<CosmeticDefinition>("ScriptableObjects/Cosmetics"));
                Tracks = new List<TrackDefinition>(Resources.LoadAll<TrackDefinition>("ScriptableObjects/Tracks"));
            }
            if (Cars.Count == 0) Cars = DefaultCatalog.CreateCars();
            if (Upgrades.Count == 0) Upgrades = DefaultCatalog.CreateUpgrades();
            if (Cosmetics.Count == 0) Cosmetics = DefaultCatalog.CreateCosmetics();
            if (Tracks.Count == 0) Tracks = DefaultCatalog.CreateTracks();
            Cars.Sort((a, b) => a.price.CompareTo(b.price));
            Tracks.Sort((a, b) => a.requiredLevel.CompareTo(b.requiredLevel));
        }

        public static CarDefinition GetCar(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < Cars.Count; i++) if (Cars[i].id == id) return Cars[i];
            return Cars[0];
        }

        public static UpgradeDefinition GetUpgrade(UpgradeCategory c)
        {
            EnsureLoaded();
            for (int i = 0; i < Upgrades.Count; i++) if (Upgrades[i].category == c) return Upgrades[i];
            return null;
        }

        public static CosmeticDefinition GetCosmetic(CosmeticCategory c, string id)
        {
            EnsureLoaded();
            for (int i = 0; i < Cosmetics.Count; i++) if (Cosmetics[i].category == c && Cosmetics[i].id == id) return Cosmetics[i];
            return null;
        }

        public static List<CosmeticDefinition> GetCosmetics(CosmeticCategory c)
        {
            EnsureLoaded();
            var l = new List<CosmeticDefinition>();
            for (int i = 0; i < Cosmetics.Count; i++) if (Cosmetics[i].category == c) l.Add(Cosmetics[i]);
            return l;
        }

        public static TrackDefinition GetTrack(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < Tracks.Count; i++) if (Tracks[i].id == id) return Tracks[i];
            return Tracks.Count > 0 ? Tracks[0] : null;
        }

        public static List<TrackDefinition> GetTracks(GameMode m)
        {
            EnsureLoaded();
            var l = new List<TrackDefinition>();
            for (int i = 0; i < Tracks.Count; i++) if (Tracks[i].mode == m) l.Add(Tracks[i]);
            return l;
        }

        public static string CarName(CarDefinition c) { return Loc.IsRu ? c.nameRu : c.nameEn; }
        public static string CarDesc(CarDefinition c) { return Loc.IsRu ? c.descRu : c.descEn; }
        public static string CosName(CosmeticDefinition c) { return Loc.IsRu ? c.nameRu : c.nameEn; }
        public static string UpgName(UpgradeDefinition u) { return Loc.IsRu ? u.nameRu : u.nameEn; }
        public static string TrackName(TrackDefinition t) { return Loc.IsRu ? t.nameRu : t.nameEn; }
    }
}
