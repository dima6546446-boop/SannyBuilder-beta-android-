using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RussianDrift.Core
{
    [Serializable]
    internal class SaveEnvelope
    {
        public string payload;   // base64 of XOR-obfuscated json
        public string sig;       // HMAC-SHA256 hex of payload
    }

    /// <summary>
    /// JSON save with simple anti-tamper: payload is XOR-obfuscated with a device-bound key and signed with HMAC-SHA256.
    /// A mirror copy lives in PlayerPrefs; if the file is damaged/modified the mirror is used, otherwise a fresh profile starts.
    /// This stops casual editing — it is not a substitute for server validation.
    /// </summary>
    public class SaveSystem
    {
        private const string PrefKey = "rd_save_mirror";
        private const string Secret = "rd-4f9a1c77-b13e-4d0a-9d5e-0c1f66a8";

        public SaveData Data { get; private set; }
        public bool WasTampered { get; private set; }

        private string FilePath { get { return Path.Combine(Application.persistentDataPath, "profile.rdsave"); } }

        private static byte[] Key()
        {
            string id = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier) id = "nodev";
            using (var sha = SHA256.Create()) return sha.ComputeHash(Encoding.UTF8.GetBytes(Secret + id));
        }

        private static byte[] Xor(byte[] data, byte[] key)
        {
            var r = new byte[data.Length];
            for (int i = 0; i < data.Length; i++) r[i] = (byte)(data[i] ^ key[i % key.Length]);
            return r;
        }

        private static string Sign(string payload, byte[] key)
        {
            using (var h = new HMACSHA256(key))
            {
                byte[] sig = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(sig.Length * 2);
                for (int i = 0; i < sig.Length; i++) sb.Append(sig[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Encode(SaveData d)
        {
            byte[] key = Key();
            string json = JsonUtility.ToJson(d);
            string payload = Convert.ToBase64String(Xor(Encoding.UTF8.GetBytes(json), key));
            return JsonUtility.ToJson(new SaveEnvelope { payload = payload, sig = Sign(payload, key) });
        }

        public static SaveData Decode(string envelopeJson)
        {
            if (string.IsNullOrEmpty(envelopeJson)) return null;
            try
            {
                var env = JsonUtility.FromJson<SaveEnvelope>(envelopeJson);
                if (env == null || string.IsNullOrEmpty(env.payload)) return null;
                byte[] key = Key();
                if (Sign(env.payload, key) != env.sig) return null;
                string json = Encoding.UTF8.GetString(Xor(Convert.FromBase64String(env.payload), key));
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception) { return null; }
        }

        public void Load()
        {
            SaveData d = null;
            bool fileExists = false;
            try
            {
                if (File.Exists(FilePath)) { fileExists = true; d = Decode(File.ReadAllText(FilePath)); }
            }
            catch (Exception e) { Debug.LogWarning("Save read failed: " + e.Message); }

            if (d == null && PlayerPrefs.HasKey(PrefKey))
            {
                d = Decode(PlayerPrefs.GetString(PrefKey));
                if (d == null) WasTampered = true;
            }
            if (d == null && fileExists) WasTampered = true;

            Data = d ?? new SaveData();
            Normalize(Data);
        }

        private static void Normalize(SaveData d)
        {
            if (d.settings == null) d.settings = new GameSettings();
            if (d.driver == null) d.driver = new DriverLook();
            if (d.ownedCars == null || d.ownedCars.Count == 0) d.ownedCars = new System.Collections.Generic.List<string>(new[] { "kopeyka" });
            if (string.IsNullOrEmpty(d.selectedCar) || !d.ownedCars.Contains(d.selectedCar)) d.selectedCar = d.ownedCars[0];
            if (d.currency < 0) d.currency = 0;
            if (d.level < 1) d.level = 1;
        }

        public void Save()
        {
            if (Data == null) return;
            Data.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
            string enc = Encode(Data);
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, enc);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("Save write failed: " + e.Message); }
            PlayerPrefs.SetString(PrefKey, enc);
            PlayerPrefs.Save();
        }

        public void ReplaceWith(SaveData d)
        {
            Normalize(d);
            Data = d;
            Save();
        }
    }
}
