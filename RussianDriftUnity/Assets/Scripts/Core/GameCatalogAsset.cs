using System;
using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Registry asset (Resources/GameCatalog.asset) that references every data ScriptableObject so they ship in the build.</summary>
    [CreateAssetMenu(menuName = "Drift/Game Catalog", fileName = "GameCatalog")]
    public class GameCatalogAsset : ScriptableObject
    {
        public CarDefinition[] cars;
        public UpgradeDefinition[] upgrades;
        public CosmeticDefinition[] cosmetics;
        public TrackDefinition[] tracks;
    }
}
