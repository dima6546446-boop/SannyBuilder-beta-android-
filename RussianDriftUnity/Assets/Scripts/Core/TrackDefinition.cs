using System;
using UnityEngine;

namespace RussianDrift.Core
{
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
}
