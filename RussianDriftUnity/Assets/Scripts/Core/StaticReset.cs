using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Resets static state at startup so the game also works with "Enter Play Mode Options > Disable Domain Reload".</summary>
    public static class StaticReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            GameEvents.ClearAll();
            Services.Clear();
            GameCatalog.Reset();
            WorldConditions.Reset();
            TouchInput.ResetHeld();
            GameSession.Reset();
            PoolManager.Clear();
            MatLib.ClearCache();
        }
    }
}
