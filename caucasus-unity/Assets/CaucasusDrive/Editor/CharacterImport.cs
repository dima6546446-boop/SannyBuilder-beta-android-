#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CaucasusDrive.EditorTools
{
    /// <summary>Настройки импорта персонажа (Resources/Characters/Human.fbx): Generic-скелет, анимации, без чужих материалов.</summary>
    public class CharacterImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Characters/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.optimizeGameObjects = false;
            mi.isReadable = true;   // нужно для сглаживания нормалей и подгонки одежды по сетке
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        }
    }
}
#endif
