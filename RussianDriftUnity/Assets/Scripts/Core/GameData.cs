using System;
using UnityEngine;

namespace RussianDrift.Core
{
    public enum BodyStyle { Classic, Hatch, Sedan, BigSedan, Wagon, Coupe, SportCoupe }
    public enum UpgradeCategory { Engine, Turbo, Suspension, Brakes, Tires, Differential, Weight }
    public enum CosmeticCategory { Wrap, Spoiler, BodyKit, Rims, Neon, Decal }
    public enum GameMode { FreeRoam, DriftTrack, TimeAttack, DriftBattle }
    public enum TrackKind { IndustrialLoop, CityRing, Serpentine }
    public enum Difficulty { Easy, Normal, Hard, Pro }

    /// <summary>Static description of a car. Created as assets by Tools/Drift/Build Everything, or in memory by DefaultCatalog.</summary>
    [CreateAssetMenu(menuName = "Drift/Car Definition", fileName = "Car")]
    public class CarDefinition : ScriptableObject
    {
        public string id = "car";
        public string nameRu = "Машина";
        public string nameEn = "Car";
        [TextArea] public string descRu = "";
        [TextArea] public string descEn = "";
        public BodyStyle bodyStyle = BodyStyle.Classic;
        public int price = 0;
        public int requiredLevel = 1;

        [Header("Dimensions (m)")]
        public float length = 4.1f;
        public float width = 1.62f;
        public float height = 1.4f;
        public float wheelbase = 2.42f;
        public float trackFront = 1.36f;
        public float trackRear = 1.34f;
        public float wheelRadius = 0.30f;
        public float tireWidth = 0.205f;

        [Header("Mass / chassis")]
        public float mass = 1050f;
        public float comHeight = 0.42f;      // centre of mass height above ground
        public float springFrequency = 1.9f;  // Hz
        public float dampingRatio = 0.42f;
        public float travel = 0.26f;
        public float rideHeight = 0.16f;
        public float maxSteerAngle = 36f;
        public float baseGrip = 1.02f;        // tyre friction coefficient
        public float downforce = 0.0f;

        [Header("Engine")]
        public float peakTorque = 160f;       // Nm (before upgrades)
        public float idleRpm = 900f;
        public float peakTorqueRpm = 4200f;
        public float redlineRpm = 6800f;
        public float[] gearRatios = { 3.6f, 2.15f, 1.45f, 1.08f, 0.86f };
        public float reverseRatio = 3.5f;
        public float finalDrive = 3.9f;
        public float brakeForce = 9000f;      // N total
        public float turboStockBoost = 0f;    // 0 = naturally aspirated
        public int cylinders = 4;
        public float engineTone = 1f;         // audio character multiplier
        public DriveType defaultDrive = DriveType.RWD;
        public Color defaultColor = new Color(0.78f, 0.1f, 0.1f);

        [Header("Optional imported model (see ASSETS.md); leave empty to use the procedural model")]
        public GameObject modelPrefab;
    }

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

    [CreateAssetMenu(menuName = "Drift/Track Definition", fileName = "Track")]
    public class TrackDefinition : ScriptableObject
    {
        public string id = "track";
        public string nameRu, nameEn;
        public GameMode mode = GameMode.DriftTrack;
        public TrackKind kind = TrackKind.IndustrialLoop;
        public int laps = 3;
        public float timeLimit = 120f;       // seconds (drift track / battle)
        public float[] medalScores = { 5000, 12000, 25000 };   // bronze, silver, gold (drift) or times for TA (gold,silver,bronze seconds)
        public int reward = 1500;
        public int requiredLevel = 1;
        public float startTime = 17f;        // hour of day
        public int weather = 0;              // 0 clear, 1 rain, 2 fog
    }

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
