using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RussianDrift.EditorTools
{
    /// <summary>Creates URP Low / Medium / High pipeline assets (+ renderers, SSAO on Medium/High) in Assets/Resources/URP.</summary>
    public static class UrpAssetBuilder
    {
        public static readonly string[] Names = { "Low", "Medium", "High" };

        public static void CreateAll()
        {
            AssetFactory.EnsureFolder("Assets/Resources/URP");
            AssetFactory.EnsureFolder("Assets/Settings");
            for (int i = 0; i < 3; i++) Create(i);
            AssetDatabase.SaveAssets();
            var high = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Resources/URP/URP_High.asset");
            if (high != null)
            {
                GraphicsSettings.defaultRenderPipeline = high;
                QualitySettings.renderPipeline = high;
            }
        }

        private static void Create(int tier)
        {
            string name = Names[tier];
            string rendererPath = "Assets/Settings/URP_Renderer_" + name + ".asset";
            string assetPath = "Assets/Resources/URP/URP_" + name + ".asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                if (tier >= 1)
                {
                    try
                    {
                        var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                        ssao.name = "ScreenSpaceAmbientOcclusion";
                        AssetDatabase.AddObjectToAsset(ssao, renderer);
                        renderer.rendererFeatures.Add(ssao);
                        EditorUtility.SetDirty(renderer);
                    }
                    catch (Exception e) { Debug.LogWarning("[RussianDrift] SSAO feature could not be added: " + e.Message); }
                }
                EditorUtility.SetDirty(renderer);
            }

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR", tier >= 1 ? 1 : 0);
            Set(so, "m_MSAA", tier == 2 ? 2 : 1);
            Set(so, "m_RenderScale", tier == 0 ? 0.75f : (tier == 1 ? 0.9f : 1f));
            Set(so, "m_ShadowDistance", tier == 0 ? 35f : (tier == 1 ? 60f : 90f));
            Set(so, "m_ShadowCascadeCount", tier == 0 ? 1 : 2);
            Set(so, "m_MainLightShadowmapResolution", tier == 0 ? 1024 : 2048);
            Set(so, "m_AdditionalLightsRenderingMode", 1);       // per pixel
            Set(so, "m_AdditionalLightsPerObjectLimit", tier == 0 ? 2 : (tier == 1 ? 4 : 6));
            Set(so, "m_SoftShadowsSupported", tier >= 1 ? 1 : 0);
            Set(so, "m_UseSRPBatcher", 1);
            Set(so, "m_SupportsDynamicBatching", 0);
            Set(so, "m_RequireDepthTexture", tier >= 1 ? 1 : 0);
            Set(so, "m_RequireOpaqueTexture", 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void Set(SerializedObject so, string prop, object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) return;
            try
            {
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Boolean: p.boolValue = Convert.ToInt32(value) != 0; break;
                    case SerializedPropertyType.Integer: p.intValue = Convert.ToInt32(value); break;
                    case SerializedPropertyType.Float: p.floatValue = Convert.ToSingle(value); break;
                    case SerializedPropertyType.Enum: p.enumValueIndex = Convert.ToInt32(value); break;
                }
            }
            catch (Exception) { }
        }
    }
}
