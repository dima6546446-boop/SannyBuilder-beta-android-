using UnityEngine;

namespace Zanos.Game
{
    /// <summary>Игра стартует сама в любой (даже пустой) сцене — ничего настраивать вручную не нужно.</summary>
    public static class ZanosBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindFirstObjectByType<ZanosApp>() != null) return;
            var go = new GameObject("ZANOS"); Object.DontDestroyOnLoad(go); go.AddComponent<ZanosApp>();
        }
    }
}
