using System;
using UnityEngine;

namespace RussianDrift.Core
{
    [CreateAssetMenu(menuName = "Drift/Upgrade Definition", fileName = "Upgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        public UpgradeCategory category;
        public string nameRu, nameEn, descRu, descEn;
        public int maxLevel = 5;
        public int basePrice = 1500;
        public float priceGrowth = 1.65f;
        public float valuePerLevel = 0.08f;   // meaning depends on category (see VehicleStats)

        public int PriceForLevel(int targetLevel)
        {
            // price to go from (targetLevel-1) to targetLevel
            return Mathf.RoundToInt(basePrice * Mathf.Pow(priceGrowth, Mathf.Max(0, targetLevel - 1)) / 50f) * 50;
        }
    }
}
