using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>
    /// Lightweight RU/EN localisation (string table lives in LocTable.cs). Chosen over the Unity Localization package
    /// so the project works with zero asset setup; keys map 1:1 to what a StringTable would hold.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>();
        private static bool built;
        public static int Language { get; private set; }
        public static bool IsRu { get { return Language == 0; } }

        public static void Add(string key, string ru, string en) { table[key] = new[] { ru, en }; }

        private static void Build()
        {
            if (built) return;
            built = true;
            LocTable.Fill();
        }

        public static void SetLanguage(int lang, bool raise = true)
        {
            Build();
            Language = Mathf.Clamp(lang, 0, 1);
            if (raise) GameEvents.RaiseLanguage();
        }

        public static string T(string key)
        {
            Build();
            string[] v;
            if (table.TryGetValue(key, out v)) return v[Language];
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static int SystemLanguageIndex()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    return 0;
                default: return 1;
            }
        }
    }
}
