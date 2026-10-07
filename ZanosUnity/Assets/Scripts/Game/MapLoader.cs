using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>Загрузка карт из Resources/Zanos/Maps/*.json (генерируются из браузерной версии: rcd/tools/export-unity.mjs).</summary>
    public static class MapLoader
    {
        public static readonly string[] Ids = { "parking", "city", "industrial", "mountain" };
        static readonly System.Collections.Generic.Dictionary<string, MapData> cache = new System.Collections.Generic.Dictionary<string, MapData>();

        public static MapData Load(string id)
        {
            MapData m; if (cache.TryGetValue(id, out m)) return m;
            var ta = Resources.Load<TextAsset>("Zanos/Maps/" + id);
            if (ta == null) throw new System.Exception("Нет карты " + id + " в Resources/Zanos/Maps");
            m = JsonUtility.FromJson<MapData>(ta.text); cache[id] = m; return m;
        }
    }
}
