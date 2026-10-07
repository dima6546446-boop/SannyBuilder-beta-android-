using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    public enum SurfaceType { Asphalt, Concrete, Grass, Dirt, Curb, Wet }

    /// <summary>Attach to colliders to override tyre friction (grass patches, dirt, curbs...).</summary>
    public class SurfaceInfo : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Asphalt;
        public float grip = 1f;
        public float rolling = 0.015f;
    }

    public struct Puddle
    {
        public Vector3 center;
        public float radius;
    }

    /// <summary>Global environment state shared between World (writers) and Vehicle/Audio (readers).</summary>
    public static class WorldConditions
    {
        public static float TimeOfDay = 17f;      // 0..24
        public static float Rain;                 // 0..1
        public static float Wetness;              // 0..1 (road wetness lags behind rain)
        public static float Fog;                  // 0..1
        public static float NightFactor;          // 0 day .. 1 night
        public static readonly List<Puddle> Puddles = new List<Puddle>();

        public static float GripMultiplier { get { return Mathf.Lerp(1f, 0.72f, Wetness); } }

        public static bool InPuddle(Vector3 p)
        {
            for (int i = 0; i < Puddles.Count; i++)
            {
                var pd = Puddles[i];
                float dx = p.x - pd.center.x, dz = p.z - pd.center.z;
                if (dx * dx + dz * dz < pd.radius * pd.radius) return true;
            }
            return false;
        }

        public static void Reset()
        {
            Rain = 0; Wetness = 0; Fog = 0; NightFactor = 0; TimeOfDay = 17f;
            Puddles.Clear();
        }
    }

    /// <summary>What the player picked in the menu; read by GameWorld.</summary>
    public static class GameSession
    {
        public static GameMode Mode = GameMode.FreeRoam;
        public static string TrackId = "";
        public static Difficulty Difficulty = Difficulty.Normal;
        public static string QuestHint = "";
        public static float FreeHour = 17f;      // free roam time of day
        public static int FreeWeather = 0;       // 0 clear, 1 rain, 2 fog

        public static void Reset() { Mode = GameMode.FreeRoam; TrackId = ""; Difficulty = Difficulty.Normal; }
    }

    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string Garage = "Garage";
        public const string GameWorld = "GameWorld";
    }
}
