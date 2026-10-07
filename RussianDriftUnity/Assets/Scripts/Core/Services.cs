using System;
using System.Collections.Generic;

namespace RussianDrift.Core
{
    /// <summary>Minimal service locator for cross-module singletons (save, ads, IAP, audio...).</summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> map = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class { map[typeof(T)] = service; }

        public static T Get<T>() where T : class
        {
            object o;
            return map.TryGetValue(typeof(T), out o) ? (T)o : null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        public static void Unregister<T>() where T : class { map.Remove(typeof(T)); }
        public static void Clear() { map.Clear(); }
    }

    /// <summary>Monetisation / platform service contracts. Concrete SDKs plug in via Services.Register.</summary>
    public interface IAdService
    {
        bool IsRewardedReady { get; }
        void Initialize();
        void ShowRewarded(string placement, Action<bool> onComplete);
        void ShowInterstitial(string placement, Action onClosed);
        void SetBannerVisible(bool visible);
    }

    public struct IapProduct
    {
        public string id;
        public string titleRu;
        public string titleEn;
        public long coinsGranted;
        public string fallbackPrice;
    }

    public interface IIapService
    {
        IapProduct[] Products { get; }
        void Initialize();
        void Purchase(string productId, Action<bool, IapProduct> onComplete);
        void RestorePurchases(Action<bool> onComplete);
        string GetLocalizedPrice(string productId);
    }

    public interface ICloudSaveService
    {
        bool IsAvailable { get; }
        void Upload(string json, Action<bool> onComplete);
        void Download(Action<bool, string> onComplete);
    }

    /// <summary>Any source of driving commands (player, AI, replay).</summary>
    public interface IVehicleInputSource
    {
        VehicleInputState Read();
    }

    public struct VehicleInputState
    {
        public float throttle;   // 0..1
        public float brake;      // 0..1 (also reverse when stopped)
        public float steer;      // -1..1
        public bool handbrake;
        public bool shiftUp;
        public bool shiftDown;
        public bool clutchKick;
    }
}
