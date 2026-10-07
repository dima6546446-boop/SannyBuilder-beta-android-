using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RussianDrift.UI;

namespace RussianDrift.EditorTools
{
    /// <summary>Creates Boot / MainMenu / Garage / GameWorld scenes (each holds one controller; content is generated at runtime) and registers them in Build Settings.</summary>
    public static class SceneBuilder
    {
        public static void CreateAllScenes()
        {
            AssetFactory.EnsureFolder("Assets/Scenes");
            var current = SceneManager.GetActiveScene().path;
            CreateScene("Boot", go => go.AddComponent<BootController>());
            CreateScene("MainMenu", go => go.AddComponent<MainMenuController>());
            CreateScene("Garage", go => go.AddComponent<GarageController>());
            CreateScene("GameWorld", go => go.AddComponent<GameWorldController>());

            var list = new List<EditorBuildSettingsScene>();
            foreach (var n in new[] { "Boot", "MainMenu", "Garage", "GameWorld" })
                list.Add(new EditorBuildSettingsScene("Assets/Scenes/" + n + ".unity", true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[RussianDrift] Scenes created and added to Build Settings (Boot first).");
        }

        private static void CreateScene(string name, System.Action<GameObject> setup)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(name + "Controller");
            setup(go);
            RenderSettings.skybox = null;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + name + ".unity");
        }
    }
}
