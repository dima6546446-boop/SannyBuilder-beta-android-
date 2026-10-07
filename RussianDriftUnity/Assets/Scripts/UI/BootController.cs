using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Audio;
using RussianDrift.Core;
using RussianDrift.Meta;

namespace RussianDrift.UI
{
    /// <summary>Boot scene: initialises save, localisation, services, quality and audio, then loads the main menu.</summary>
    public class BootController : MonoBehaviour
    {
        private Image fill;
        private Text status;

        private void Start() { StartCoroutine(Run()); }

        private IEnumerator Run()
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;

            var cam = new GameObject("BootCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.03f, 0.035f, 0.05f);
            cam.gameObject.AddComponent<AudioListener>();
            var canvas = Ui.CreateCanvas("BootCanvas", 5);
            var title = Ui.Label(canvas.transform, "RUSSIAN <color=#FF6120>DRIFT</color>", 120, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(1500, 180));
            status = Ui.Label(canvas.transform, "", 30, Theme.Dim);
            Ui.Place(status.rectTransform, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(1000, 50));
            var bar = Ui.Bar(canvas.transform, Theme.Accent, new Color(1, 1, 1, 0.12f), out fill);
            Ui.Place(bar.rectTransform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(900, 18));
            Ui.SetBar(fill, 0.05f);
            yield return null;

            // ---- save + language ----
            var save = new SaveSystem();
            save.Load();
            bool firstRun = save.Data.lastSavedUtcTicks == 0;
            if (firstRun) save.Data.settings.language = Loc.SystemLanguageIndex();
            Loc.SetLanguage(save.Data.settings.language, false);
            Services.Register<SaveSystem>(save);
            Set(0.2f, Loc.T("boot.profile"));
            yield return null;

            MetaBootstrap.Init(save);
            GameCatalog.EnsureLoaded();
            var setup = save.Data.GetSetup(save.Data.selectedCar);
            if (string.IsNullOrEmpty(setup.colorHex)) setup.colorHex = ColorUtility.ToHtmlStringRGB(GameCatalog.GetCar(save.Data.selectedCar).defaultColor);
            if (firstRun) save.Save();
            Set(0.4f, Loc.T("boot.graphics"));
            yield return null;

            QualityManager.Apply();
            AdaptiveQuality.Ensure();
            Set(0.6f, Loc.T("boot.audio"));
            yield return null;

            var audio = AudioManager.Ensure();
            audio.ApplySettings();
            yield return StartCoroutine(audio.PrewarmCoroutine());
            Set(1f, Loc.T("boot.done"));
            yield return new WaitForSeconds(0.25f);
            SceneLoader.Load(SceneNames.MainMenu);
        }

        private void Set(float p, string text)
        {
            Ui.SetBar(fill, p);
            status.text = text;
        }
    }
}
