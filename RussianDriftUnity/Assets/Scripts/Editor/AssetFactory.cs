using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RussianDrift.Core;
using RussianDrift.Vehicle;

namespace RussianDrift.EditorTools
{
    /// <summary>Creates the data ScriptableObjects, base materials and car prefabs from the code-defined defaults.</summary>
    public static class AssetFactory
    {
        public static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        // ------------------------------------------------------------------ materials
        public static bool CreateBaseMaterials()
        {
            EnsureFolder("Assets/Resources/Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null || unlit == null) { Debug.LogWarning("[RussianDrift] URP shaders not found yet; base materials will be created on the next setup run."); return false; }

            var m = new Material(lit) { name = "BaseLit", enableInstancing = true };
            SaveMaterial(m, "Assets/Resources/Materials/BaseLit.mat");

            var e = new Material(lit) { name = "BaseLitEmissive", enableInstancing = true };
            e.EnableKeyword("_EMISSION");
            e.SetColor("_EmissionColor", new Color(0.01f, 0.01f, 0.01f));
            e.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveMaterial(e, "Assets/Resources/Materials/BaseLitEmissive.mat");

            var u = new Material(unlit) { name = "BaseUnlit", enableInstancing = true };
            SaveMaterial(u, "Assets/Resources/Materials/BaseUnlit.mat");
            return true;
        }

        private static void SaveMaterial(Material m, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(m); }
            else AssetDatabase.CreateAsset(m, path);
        }

        // ------------------------------------------------------------------ data assets
        private static T Save<T>(T so, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) { EditorUtility.CopySerialized(so, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(so); return existing; }
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        public static void CreateDataAssets()
        {
            string root = "Assets/ScriptableObjects";
            EnsureFolder(root + "/Cars"); EnsureFolder(root + "/Upgrades"); EnsureFolder(root + "/Cosmetics"); EnsureFolder(root + "/Tracks");
            EnsureFolder("Assets/Resources");

            var cars = new List<CarDefinition>();
            foreach (var c in DefaultCatalog.CreateCars()) cars.Add(Save(c, root + "/Cars/Car_" + c.id + ".asset"));
            var ups = new List<UpgradeDefinition>();
            foreach (var u in DefaultCatalog.CreateUpgrades()) ups.Add(Save(u, root + "/Upgrades/Upgrade_" + u.category + ".asset"));
            var cos = new List<CosmeticDefinition>();
            foreach (var c in DefaultCatalog.CreateCosmetics()) cos.Add(Save(c, root + "/Cosmetics/" + c.category + "_" + c.id + ".asset"));
            var trs = new List<TrackDefinition>();
            foreach (var t in DefaultCatalog.CreateTracks()) trs.Add(Save(t, root + "/Tracks/Track_" + t.id + ".asset"));

            var reg = ScriptableObject.CreateInstance<GameCatalogAsset>();
            reg.cars = cars.ToArray(); reg.upgrades = ups.ToArray(); reg.cosmetics = cos.ToArray(); reg.tracks = trs.ToArray();
            Save(reg, "Assets/Resources/GameCatalog.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[RussianDrift] Data assets: " + cars.Count + " cars, " + ups.Count + " upgrades, " + cos.Count + " cosmetics, " + trs.Count + " tracks.");
        }

        // ------------------------------------------------------------------ prefabs
        /// <summary>
        /// Builds one visual prefab per car. Procedural meshes/materials are stored as sub-assets of a ModelAssets container
        /// so the prefab keeps valid references (gameplay still builds cars at runtime from the same CarBuilder).
        /// </summary>
        public static void CreateCarPrefabs()
        {
            EnsureFolder("Assets/Prefabs/Cars");
            GameCatalog.EnsureLoaded();
            foreach (var def in GameCatalog.Cars)
            {
                var setup = new CarSetup { carId = def.id, colorHex = ColorUtility.ToHtmlStringRGB(def.defaultColor) };
                var holder = new GameObject("Car_" + def.id);
                var model = CarBuilder.Build(def, setup, holder.transform, false);
                model.visualRoot.localPosition = new Vector3(0, def.comHeight, 0);

                string dataPath = "Assets/Prefabs/Cars/Car_" + def.id + "_Assets.asset";
                string prefabPath = "Assets/Prefabs/Cars/Car_" + def.id + ".prefab";
                AssetDatabase.DeleteAsset(dataPath);
                var container = ScriptableObject.CreateInstance<ModelAssets>();
                AssetDatabase.CreateAsset(container, dataPath);
                var seen = new HashSet<UnityEngine.Object>();
                foreach (var o in model.owned)
                {
                    if (o == null || !seen.Add(o) || AssetDatabase.Contains(o)) continue;
                    o.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(o, container);
                }
                // textures referenced by the materials (cached procedural textures) must live in the container too
                foreach (var o in new List<UnityEngine.Object>(seen))
                {
                    var mat = o as Material;
                    if (mat == null) continue;
                    foreach (var pn in mat.GetTexturePropertyNames())
                    {
                        var tex = mat.GetTexture(pn);
                        if (tex == null || AssetDatabase.Contains(tex) || !seen.Add(tex)) continue;
                        tex.hideFlags = HideFlags.None;
                        AssetDatabase.AddObjectToAsset(tex, container);
                    }
                }
                model.owned.Clear();       // prevent the model from destroying assets that now belong to the container
                EditorUtility.SetDirty(container);
                PrefabUtility.SaveAsPrefabAsset(holder, prefabPath);
                UnityEngine.Object.DestroyImmediate(holder);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[RussianDrift] Car prefabs created in Assets/Prefabs/Cars.");
        }
    }
}
