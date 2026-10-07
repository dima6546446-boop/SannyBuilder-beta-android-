using System;
using System.Collections;
using System.IO;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Meta
{
    /// <summary>Coroutine host for stub services.</summary>
    public class MetaRunner : MonoBehaviour
    {
        private static MetaRunner instance;
        public static MetaRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[MetaRunner]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<MetaRunner>();
                }
                return instance;
            }
        }
    }

    /// <summary>Placeholder ad provider. Swap for an SDK adapter (AdMob / Unity Ads / Yandex Ads) by registering another IAdService.</summary>
    public class StubAdService : IAdService
    {
        public bool IsRewardedReady { get { return true; } }
        public void Initialize() { Debug.Log("[Ads] stub initialised"); }

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            Debug.Log("[Ads] stub rewarded: " + placement);
            MetaRunner.Instance.StartCoroutine(Fake(1.0f, () => { if (onComplete != null) onComplete(true); }));
        }

        public void ShowInterstitial(string placement, Action onClosed)
        {
            Debug.Log("[Ads] stub interstitial: " + placement);
            if (onClosed != null) onClosed();
        }

        public void SetBannerVisible(bool visible) { }

        private static IEnumerator Fake(float t, Action done) { yield return new WaitForSecondsRealtime(t); done(); }
    }

    /// <summary>Placeholder store. In release builds purchases are refused (no SDK) so the stub can never hand out free currency.</summary>
    public class StubIapService : IIapService
    {
        private readonly IapProduct[] products =
        {
            new IapProduct { id = "coins_small", titleRu = "Пачка монет", titleEn = "Coin pack", coinsGranted = 10000, fallbackPrice = "$0.99" },
            new IapProduct { id = "coins_medium", titleRu = "Сумка монет", titleEn = "Coin bag", coinsGranted = 60000, fallbackPrice = "$4.99" },
            new IapProduct { id = "coins_large", titleRu = "Сундук монет", titleEn = "Coin chest", coinsGranted = 250000, fallbackPrice = "$14.99" },
        };

        public IapProduct[] Products { get { return products; } }
        public void Initialize() { Debug.Log("[IAP] stub initialised"); }

        public void Purchase(string productId, Action<bool, IapProduct> onComplete)
        {
            foreach (var p in products)
                if (p.id == productId)
                {
                    bool allowed = Application.isEditor || Debug.isDebugBuild;
                    Debug.Log("[IAP] stub purchase " + productId + " allowed=" + allowed);
                    if (onComplete != null) onComplete(allowed, p);
                    return;
                }
            if (onComplete != null) onComplete(false, default(IapProduct));
        }

        public void RestorePurchases(Action<bool> onComplete) { if (onComplete != null) onComplete(true); }

        public string GetLocalizedPrice(string productId)
        {
            foreach (var p in products) if (p.id == productId) return p.fallbackPrice;
            return "";
        }
    }

    /// <summary>Local simulation of a cloud save (file copy). Replace with Google Play Games Saved Games or another backend.</summary>
    public class LocalCloudSave : ICloudSaveService
    {
        private string Path { get { return System.IO.Path.Combine(Application.persistentDataPath, "cloud_sim.json"); } }
        public bool IsAvailable { get { return true; } }

        public void Upload(string json, Action<bool> onComplete)
        {
            try { File.WriteAllText(Path, json); if (onComplete != null) onComplete(true); }
            catch (Exception e) { Debug.LogWarning("[Cloud] upload failed: " + e.Message); if (onComplete != null) onComplete(false); }
        }

        public void Download(Action<bool, string> onComplete)
        {
            try
            {
                if (File.Exists(Path)) { if (onComplete != null) onComplete(true, File.ReadAllText(Path)); }
                else if (onComplete != null) onComplete(false, null);
            }
            catch (Exception) { if (onComplete != null) onComplete(false, null); }
        }
    }

    /// <summary>Run results -> currency/XP. Single place for the reward formulas.</summary>
    public static class RewardCalculator
    {
        public struct Reward { public int coins; public int xp; public int medal; }

        public static Reward ForDrift(int score, TrackDefinition track, float timeUsed)
        {
            int medal = 0;
            if (track != null && track.medalScores != null)
                for (int i = 0; i < track.medalScores.Length; i++) if (score >= track.medalScores[i]) medal = i + 1;
            int coins = Mathf.RoundToInt(score / 12f);
            if (track != null) coins += Mathf.RoundToInt(track.reward * medal / 3f);
            return new Reward { coins = coins, xp = Mathf.RoundToInt(score / 40f) + medal * 60, medal = medal };
        }

        public static Reward ForTimeAttack(float totalTime, float trackLength, int laps, TrackDefinition track)
        {
            // medalScores holds average speeds in km/h for bronze/silver/gold
            float avg = totalTime > 0.1f ? trackLength * laps / totalTime * 3.6f : 0f;
            int medal = 0;
            if (track != null && track.medalScores != null)
                for (int i = 0; i < track.medalScores.Length; i++) if (avg >= track.medalScores[i]) medal = i + 1;
            int coins = Mathf.RoundToInt(track != null ? track.reward * (0.25f + medal * 0.3f) : 300f);
            return new Reward { coins = coins, xp = 80 + medal * 90, medal = medal };
        }

        public static Reward ForBattle(bool won, int myScore, int oppScore, TrackDefinition track, Difficulty d)
        {
            float mult = 1f + (int)d * 0.35f;
            int coins = Mathf.RoundToInt((won ? (track != null ? track.reward : 2000) : 400) * mult) + myScore / 30;
            return new Reward { coins = coins, xp = won ? Mathf.RoundToInt(250 * mult) : 60, medal = won ? 3 : 0 };
        }
    }

    /// <summary>One-call bootstrap that creates and registers all meta services.</summary>
    public static class MetaBootstrap
    {
        public static void Init(SaveSystem save)
        {
            Services.Register<SaveSystem>(save);
            var profile = new ProfileService(save);
            Services.Register<ProfileService>(profile);
            Services.Register<QuestService>(new QuestService(profile));
            Services.Register<AchievementService>(new AchievementService(profile));
            if (Services.Get<IAdService>() == null) { var ads = new StubAdService(); ads.Initialize(); Services.Register<IAdService>(ads); }
            if (Services.Get<IIapService>() == null) { var iap = new StubIapService(); iap.Initialize(); Services.Register<IIapService>(iap); }
            if (Services.Get<ICloudSaveService>() == null) Services.Register<ICloudSaveService>(new LocalCloudSave());
        }
    }
}
