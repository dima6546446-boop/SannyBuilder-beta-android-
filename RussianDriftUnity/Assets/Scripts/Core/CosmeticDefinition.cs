using System;
using UnityEngine;

namespace RussianDrift.Core
{
    [CreateAssetMenu(menuName = "Drift/Cosmetic Definition", fileName = "Cosmetic")]
    public class CosmeticDefinition : ScriptableObject
    {
        public CosmeticCategory category;
        public string id = "none";
        public string nameRu, nameEn;
        public int price = 0;
        public int requiredLevel = 1;
        public int variant = 0;           // meaning depends on category (spoiler type, rim spokes, wrap pattern...)
        public Color color = Color.white; // neon colour, etc.
    }
}
