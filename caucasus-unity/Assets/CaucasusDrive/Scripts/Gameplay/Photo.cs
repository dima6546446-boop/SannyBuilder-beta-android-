using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CaucasusDrive
{
    /// <summary>
    /// Фоторежим: мир замирает, камера свободно облетает машину (или персонажа): свайп — вращать, щипок / колесо / «+» «−» — приблизить,
    /// кнопки меняют время суток и дождь. «Снять» — снимок без интерфейса: в галерею (Android 10+, папка Pictures/CaucasusDrive),
    /// запасной вариант — папка игры.
    /// </summary>
    public class Photo
    {
        readonly App app;
        public bool active;
        readonly Canvas canvas;
        RectTransform ui;
        float yaw, pitch, dist, lastPinch;
        Vector3 focus;
        Vector2 last; bool dragging, busy;
        Text timeLabel, rainLabel, hint;
        bool wasHud;

        public Photo(App a)
        {
            app = a;
            canvas = UIKit.MakeCanvas("Photo", 15);
            ui = UIKit.Fill(canvas.transform, "Ui");
            canvas.gameObject.SetActive(false);
        }

        public void Open()
        {
            if (active) return;
            active = true; busy = false;
            wasHud = app.hud.canvas.gameObject.activeSelf;
            app.hud.Show(false);
            app.audio.SetEngine(850, 0, 0, false); app.audio.SetHorn(false);
            var cam = app.cam.transform;
            focus = app.Focus + Vector3.up * (app.onFoot ? 1.0f : 0.9f);
            var off = cam.position - focus;
            dist = Mathf.Clamp(off.magnitude, 3f, 12f);
            yaw = Mathf.Atan2(off.x, off.z); pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(off.y / Mathf.Max(0.01f, off.magnitude), -1f, 1f)), 0.05f, 1.2f);
            Build();
            canvas.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (!active) return;
            active = false;
            canvas.gameObject.SetActive(false);
            if (wasHud) app.hud.Show(true);
            app.cameraRig.Snap();
        }

        void Build()
        {
            UIKit.Clear(ui);
            hint = UIKit.LabelAt(ui, "Свайп — вращать · щипок или + / − — приблизить", 18, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(900, 30));
            hint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            UIKit.RoundButton(ui, "back", null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -60), new Vector2(66, 66), Close);
            var shot = UIKit.RoundButton(ui, "photo", null, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-80, 0), new Vector2(104, 104), () => { if (!busy) app.StartCoroutine(Shoot()); });
            UIKit.RoundButton(ui, null, "+", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 50), new Vector2(66, 66), () => dist = Mathf.Max(2.2f, dist - 1.2f), 38);
            UIKit.RoundButton(ui, null, "−", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, -40), new Vector2(66, 66), () => dist = Mathf.Min(16f, dist + 1.2f), 38);
            var tb = UIKit.RoundButton(ui, null, DayNight.PresetNames[app.timePreset % 4], new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-50, 60), new Vector2(84, 84), () => { app.NextTime(); timeLabel.text = DayNight.PresetNames[app.timePreset % 4]; }, 15);
            timeLabel = tb.GetComponentInChildren<Text>();
            var rb = UIKit.RoundButton(ui, null, app.rainLevel > 0.5f ? "ДОЖДЬ" : "ЯСНО", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(50, 60), new Vector2(84, 84), () => { bool r = app.rainLevel < 0.5f; app.SetWeather(r ? 1f : 0f, false); app.rainLevel = r ? 1f : 0f; rainLabel.text = r ? "ДОЖДЬ" : "ЯСНО"; }, 15);
            rainLabel = rb.GetComponentInChildren<Text>();
        }

        public void Update(float dt)
        {
            if (!active) return;
            focus = app.Focus + Vector3.up * (app.onFoot ? 1.0f : 0.9f);
            Vector2 mp; bool down;
            float pd;
            if (In.Pinch(out pd))
            {
                if (lastPinch > 0f) dist = Mathf.Clamp(dist - (pd - lastPinch) * 0.012f, 2.2f, 16f);
                lastPinch = pd; dragging = false;
            }
            else
            {
                lastPinch = 0f;
                if (In.Pointer(out mp, out down))
                {
                    if (down) { last = mp; dragging = !App.UiUnder(mp); }
                    if (dragging && !busy)
                    {
                        var d = mp - last;
                        yaw -= d.x * 0.006f; pitch = Mathf.Clamp(pitch + d.y * 0.005f, 0.03f, 1.3f);
                    }
                    last = mp;
                }
                else dragging = false;
            }
            float sc = In.Scroll; if (Mathf.Abs(sc) > 0.01f) dist = Mathf.Clamp(dist - Mathf.Sign(sc) * 0.8f, 2.2f, 16f);
            var dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
            var pos = focus + dir * dist;
            pos.y = Mathf.Max(pos.y, 0.35f);
            var cam = app.cam.transform;
            cam.position = pos;
            cam.rotation = Quaternion.LookRotation(focus - pos, Vector3.up);
        }

        IEnumerator Shoot()
        {
            busy = true;
            canvas.gameObject.SetActive(false);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);     // без модуля ScreenCapture: читаем экран напрямую
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            var png = tex.EncodeToPNG();
            Object.Destroy(tex);
            string name = "CaucasusDrive_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string where = Save(png, name);
            canvas.gameObject.SetActive(true);
            app.audio.Shutter();
            StartFlash();
            hint.text = where != null ? "Снимок сохранён: " + where : "Не удалось сохранить снимок";
            busy = false;
        }

        void StartFlash()
        {
            var fl = UIKit.Fill(ui, "Flash");
            var im = fl.gameObject.AddComponent<Image>(); im.color = new Color(1, 1, 1, 0.85f); im.raycastTarget = false;
            Object.Destroy(fl.gameObject, 0.12f);
        }

        static string Save(byte[] png, string name)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var res = act.Call<AndroidJavaObject>("getContentResolver"))
                using (var values = new AndroidJavaObject("android.content.ContentValues"))
                using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
                {
                    values.Call("put", "_display_name", name);
                    values.Call("put", "mime_type", "image/png");
                    values.Call("put", "relative_path", "Pictures/CaucasusDrive");
                    using (var uri = res.Call<AndroidJavaObject>("insert", media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"), values))
                    {
                        if (uri != null)
                        {
                            using (var os = res.Call<AndroidJavaObject>("openOutputStream", uri))
                            {
                                os.Call("write", png);
                                os.Call("close");
                            }
                            return "галерея → Pictures/CaucasusDrive";
                        }
                    }
                }
            }
            catch (System.Exception) { /* запасной путь ниже */ }
#endif
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "Photos");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, name);
                File.WriteAllBytes(path, png);
                return path;
            }
            catch (System.Exception) { return null; }
        }
    }
}
