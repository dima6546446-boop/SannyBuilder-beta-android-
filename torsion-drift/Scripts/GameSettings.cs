using UnityEngine;

/// <summary>All player settings (graphics, sound, gameplay). Saved in PlayerPrefs, applied with Apply().</summary>
public static class GameSettings
{
    public static readonly string[] QualityNames = { "Низкое", "Среднее", "Высокое", "Ультра", "Своё" };
    public static readonly string[] ShadowNames = { "Выкл", "Жёсткие", "Мягкие" };
    public static readonly string[] MsaaNames = { "Выкл", "2x", "4x", "8x" };
    public static readonly string[] AnisoNames = { "Выкл", "Вкл", "Максимум" };
    public static readonly string[] FpsNames = { "30", "60", "120", "Без лимита" };
    public static readonly string[] TexNames = { "Высокое", "Среднее", "Низкое" };
    public static readonly string[] UnitNames = { "км/ч", "миль/ч" };

    public static int quality = 2;
    public static int shadows = 2;
    public static int shadowDistance = 120;
    public static int msaa = 2;
    public static int aniso = 2;
    public static int textures = 0;
    public static int fpsLimit = 1;
    public static bool vsync = false;
    public static int resWidth = 0, resHeight = 0;
    public static bool fullscreen = true;
    public static bool postFx = true;
    public static bool scenery = true;
    public static bool fog = true;
    public static int renderDistance = 900;
    public static float fov = 62.0f;
    public static float smoke = 1.0f;
    public static bool skidMarks = true;
    public static float master = 0.9f;
    public static float sfx = 0.8f;
    public static float assist = 0.7f;
    public static int units = 0;
    public static bool showHud = true;
    public static bool cameraShake = true;

    public static int selectedCar = 0;
    public static int selectedColor = 0;
    public static float bestScore = 0.0f;

    public static void Load()
    {
        quality = PlayerPrefs.GetInt("quality", 2);
        shadows = PlayerPrefs.GetInt("shadows", 2);
        shadowDistance = PlayerPrefs.GetInt("shadowDistance", 120);
        msaa = PlayerPrefs.GetInt("msaa", 2);
        aniso = PlayerPrefs.GetInt("aniso", 2);
        textures = PlayerPrefs.GetInt("textures", 0);
        fpsLimit = PlayerPrefs.GetInt("fpsLimit", 1);
        vsync = PlayerPrefs.GetInt("vsync", 0) == 1;
        resWidth = PlayerPrefs.GetInt("resW", 0);
        resHeight = PlayerPrefs.GetInt("resH", 0);
        fullscreen = PlayerPrefs.GetInt("fullscreen", 1) == 1;
        postFx = PlayerPrefs.GetInt("postFx", 1) == 1;
        scenery = PlayerPrefs.GetInt("scenery", 1) == 1;
        fog = PlayerPrefs.GetInt("fog", 1) == 1;
        renderDistance = PlayerPrefs.GetInt("renderDistance", 900);
        fov = PlayerPrefs.GetFloat("fov", 62.0f);
        smoke = PlayerPrefs.GetFloat("smoke", 1.0f);
        skidMarks = PlayerPrefs.GetInt("skid", 1) == 1;
        master = PlayerPrefs.GetFloat("master", 0.9f);
        sfx = PlayerPrefs.GetFloat("sfx", 0.8f);
        assist = PlayerPrefs.GetFloat("assist", 0.7f);
        units = PlayerPrefs.GetInt("units", 0);
        showHud = PlayerPrefs.GetInt("hud", 1) == 1;
        cameraShake = PlayerPrefs.GetInt("shake", 1) == 1;
        selectedCar = PlayerPrefs.GetInt("car", 0);
        selectedColor = PlayerPrefs.GetInt("color", 0);
        bestScore = PlayerPrefs.GetFloat("bestScore", 0.0f);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("quality", quality);
        PlayerPrefs.SetInt("shadows", shadows);
        PlayerPrefs.SetInt("shadowDistance", shadowDistance);
        PlayerPrefs.SetInt("msaa", msaa);
        PlayerPrefs.SetInt("aniso", aniso);
        PlayerPrefs.SetInt("textures", textures);
        PlayerPrefs.SetInt("fpsLimit", fpsLimit);
        PlayerPrefs.SetInt("vsync", vsync ? 1 : 0);
        PlayerPrefs.SetInt("resW", resWidth);
        PlayerPrefs.SetInt("resH", resHeight);
        PlayerPrefs.SetInt("fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("postFx", postFx ? 1 : 0);
        PlayerPrefs.SetInt("scenery", scenery ? 1 : 0);
        PlayerPrefs.SetInt("fog", fog ? 1 : 0);
        PlayerPrefs.SetInt("renderDistance", renderDistance);
        PlayerPrefs.SetFloat("fov", fov);
        PlayerPrefs.SetFloat("smoke", smoke);
        PlayerPrefs.SetInt("skid", skidMarks ? 1 : 0);
        PlayerPrefs.SetFloat("master", master);
        PlayerPrefs.SetFloat("sfx", sfx);
        PlayerPrefs.SetFloat("assist", assist);
        PlayerPrefs.SetInt("units", units);
        PlayerPrefs.SetInt("hud", showHud ? 1 : 0);
        PlayerPrefs.SetInt("shake", cameraShake ? 1 : 0);
        PlayerPrefs.SetInt("car", selectedCar);
        PlayerPrefs.SetInt("color", selectedColor);
        PlayerPrefs.SetFloat("bestScore", bestScore);
        PlayerPrefs.Save();
    }

    /// <summary>Quality presets 0..3 fill all graphics fields; 4 = custom (leaves them untouched).</summary>
    public static void ApplyPreset(int q)
    {
        quality = q;
        switch (q)
        {
            case 0: shadows = 0; shadowDistance = 40; msaa = 0; aniso = 0; textures = 2; postFx = false; scenery = false; fog = true; renderDistance = 350; smoke = 0.5f; skidMarks = false; fpsLimit = 1; break;
            case 1: shadows = 1; shadowDistance = 70; msaa = 0; aniso = 1; textures = 1; postFx = false; scenery = true; fog = true; renderDistance = 550; smoke = 0.8f; skidMarks = true; fpsLimit = 1; break;
            case 2: shadows = 2; shadowDistance = 120; msaa = 2; aniso = 2; textures = 0; postFx = true; scenery = true; fog = true; renderDistance = 900; smoke = 1.0f; skidMarks = true; fpsLimit = 1; break;
            case 3: shadows = 2; shadowDistance = 220; msaa = 3; aniso = 2; textures = 0; postFx = true; scenery = true; fog = true; renderDistance = 1600; smoke = 1.0f; skidMarks = true; fpsLimit = 2; break;
        }
    }

    public static void Apply()
    {
        QualitySettings.shadows = shadows == 0 ? ShadowQuality.Disable : (shadows == 1 ? ShadowQuality.HardOnly : ShadowQuality.All);
        QualitySettings.shadowDistance = shadowDistance;
        QualitySettings.shadowResolution = shadows == 2 ? ShadowResolution.High : ShadowResolution.Medium;
        QualitySettings.antiAliasing = msaa == 0 ? 0 : (1 << msaa);
        QualitySettings.anisotropicFiltering = aniso == 0 ? AnisotropicFiltering.Disable : (aniso == 1 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable);
#if UNITY_2022_2_OR_NEWER
        QualitySettings.globalTextureMipmapLimit = textures;
#else
        QualitySettings.masterTextureLimit = textures;
#endif
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        Application.targetFrameRate = vsync ? -1 : (fpsLimit == 0 ? 30 : (fpsLimit == 1 ? 60 : (fpsLimit == 2 ? 120 : -1)));
        QualitySettings.lodBias = quality == 0 ? 0.7f : 1.0f;
        QualitySettings.pixelLightCount = quality == 0 ? 1 : 4;

        if (resWidth > 0 && resHeight > 0)
        {
            if (Screen.width != resWidth || Screen.height != resHeight || Screen.fullScreen != fullscreen)
                Screen.SetResolution(resWidth, resHeight, fullscreen);
        }
        else if (Screen.fullScreen != fullscreen) Screen.fullScreen = fullscreen;

        AudioListener.volume = master;
        RenderSettings.fog = fog;
        RenderSettings.fogStartDistance = renderDistance * 0.25f;
        RenderSettings.fogEndDistance = renderDistance * 0.85f;
        Camera cam = Camera.main;
        if (cam != null) cam.farClipPlane = renderDistance + 200.0f;

        GameObject pp = GameObject.Find("Post Processing");
        if (pp != null) pp.SetActive(postFx);

        DriftTrack track = Object.FindObjectOfType<DriftTrack>();
        if (track != null) track.SetScenery(scenery);
        Vehicle v = Object.FindObjectOfType<Vehicle>();
        if (v != null) v.assist = assist;
    }
}
