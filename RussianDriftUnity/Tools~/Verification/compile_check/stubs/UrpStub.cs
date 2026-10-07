using System;
namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public float renderScale { get; set; }
        public int msaaSampleCount { get; set; }
        public float shadowDistance { get; set; }
        public override RenderPipeline CreatePipeline() { return null; }
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData d) { return null; }
    }
    public enum CameraOverrideOption { Off, On, UsePipelineSettings }
    public enum AntialiasingMode { None, FastApproximateAntialiasing }
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
        public bool renderShadows { get; set; }
        public CameraOverrideOption requiresDepthOption { get; set; }
        public CameraOverrideOption requiresColorOption { get; set; }
        public AntialiasingMode antialiasing { get; set; }
    }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera c) { return null; } }
    public enum TonemappingMode { None, Neutral, ACES }
    public enum MotionBlurQuality { Low, Medium, High }
    public enum MotionBlurMode { CameraOnly, CameraAndObjects }
    public class Bloom : VolumeComponent { public MinFloatParameter threshold = new MinFloatParameter(); public MinFloatParameter intensity = new MinFloatParameter(); public ClampedFloatParameter scatter = new ClampedFloatParameter(); public ColorParameter tint = new ColorParameter(); }
    public class Tonemapping : VolumeComponent { public TonemappingModeParameter mode = new TonemappingModeParameter(); }
    public class ColorAdjustments : VolumeComponent { public FloatParameter postExposure = new FloatParameter(); public ClampedFloatParameter contrast = new ClampedFloatParameter(); public ClampedFloatParameter saturation = new ClampedFloatParameter(); public ColorParameter colorFilter = new ColorParameter(); }
    public class Vignette : VolumeComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(); public ClampedFloatParameter smoothness = new ClampedFloatParameter(); public ColorParameter color = new ColorParameter(); }
    public class MotionBlur : VolumeComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(); public ClampedFloatParameter clamp = new ClampedFloatParameter(); public MotionBlurQualityParameter quality = new MotionBlurQualityParameter(); public MotionBlurModeParameter mode = new MotionBlurModeParameter(); }
    public class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }
    public class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality> { }
    public class MotionBlurModeParameter : VolumeParameter<MotionBlurMode> { }
}
namespace UnityEngine.Rendering
{
    public class VolumeParameter<T> { public T value; public void Override(T v) { value = v; } }
    public class FloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : FloatParameter { }
    public class ClampedFloatParameter : FloatParameter { }
    public class ColorParameter : VolumeParameter<Color> { }
    public class VolumeComponent : ScriptableObject { public bool active; }
    public class VolumeProfile : ScriptableObject { public T Add<T>(bool overrides = false) where T : VolumeComponent { return ScriptableObject.CreateInstance<T>(); } public bool TryGet<T>(out T c) where T : VolumeComponent { c = null; return false; } }
    public class Volume : MonoBehaviour { public bool isGlobal; public VolumeProfile profile; public float priority; public float weight; }
}

namespace UnityEngine.Rendering.Universal
{
    using System.Collections.Generic;
    public class ScriptableRendererFeature : ScriptableObject { }
    public class ScreenSpaceAmbientOcclusion : ScriptableRendererFeature { }
    public class ScriptableRendererData : ScriptableObject { public List<ScriptableRendererFeature> rendererFeatures = new List<ScriptableRendererFeature>(); }
    public class UniversalRendererData : ScriptableRendererData { }
}
