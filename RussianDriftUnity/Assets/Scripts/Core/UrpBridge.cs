using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RussianDrift.Core
{
    /// <summary>Isolates the few URP-specific camera/volume calls so the rest of the code stays pipeline-agnostic.</summary>
    public static class UrpBridge
    {
        public static void SetupCamera(Camera cam, bool postProcessing, bool shadows, bool depthTexture = false)
        {
            var d = cam.GetUniversalAdditionalCameraData();
            if (d == null) return;
            d.renderPostProcessing = postProcessing;
            d.renderShadows = shadows;
            d.requiresDepthOption = depthTexture ? CameraOverrideOption.On : CameraOverrideOption.Off;
            d.requiresColorOption = CameraOverrideOption.Off;
            d.antialiasing = AntialiasingMode.None;
        }

        public static bool IsUrpActive { get { return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset; } }
    }
}
