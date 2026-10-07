using System;
using UnityEngine;

namespace RussianDrift.Core
{
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
}
