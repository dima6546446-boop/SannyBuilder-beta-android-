using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RussianDrift.Core
{
    /// <summary>Async scene loading with a black fade overlay that survives scene changes.</summary>
    public class SceneLoader : MonoBehaviour
    {
        private static SceneLoader instance;
        private Image fade;
        private bool busy;

        private static SceneLoader Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[SceneLoader]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SceneLoader>();
                    instance.BuildOverlay();
                }
                return instance;
            }
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var imgGo = new GameObject("Fade");
            imgGo.transform.SetParent(canvasGo.transform, false);
            fade = imgGo.AddComponent<Image>();
            fade.color = new Color(0, 0, 0, 0);
            fade.raycastTarget = false;
            var rt = fade.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void Load(string scene) { Instance.StartCoroutine(Instance.Run(scene)); }

        public static void FadeIn() { Instance.StartCoroutine(Instance.Fade(1f, 0f, 0.4f)); }

        private IEnumerator Fade(float from, float to, float time)
        {
            float t = 0f;
            fade.raycastTarget = to > 0.5f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, to);
            fade.raycastTarget = to > 0.5f;
        }

        private IEnumerator Run(string scene)
        {
            if (busy) yield break;
            busy = true;
            Time.timeScale = 1f;
            yield return Fade(fade.color.a, 1f, 0.3f);
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return Fade(1f, 0f, 0.4f);
            busy = false;
        }
    }
}
