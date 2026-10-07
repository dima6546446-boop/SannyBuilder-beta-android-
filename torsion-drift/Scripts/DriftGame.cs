using System.Collections.Generic;
using UnityEngine;

/// <summary>Game flow + all menus (main menu, garage, settings, pause). Pure IMGUI: no prefabs or canvas needed.</summary>
public class DriftGame : MonoBehaviour
{
    public enum State { Main, Garage, Settings, Playing, Paused }

    public Vehicle vehicle;
    public DriftTrack track;
    public DriftScore score;
    public DriftHud hud;
    public DriftCamera cam;
    public TireEffects fx;

    State state = State.Main;
    State settingsReturn = State.Main;
    int settingsTab;
    Vector2 scroll;
    float k = 1.0f;
    int lastW, lastH;

    Texture2D tPanel, tBtn, tBtnHot, tBar, tAccent, tWhite, tDim;
    GUIStyle sTitle, sH1, sText, sSmall, sBtn, sBtnBig, sSliderBar, sSliderThumb, sCenter;
    List<Vector2Int> resolutions = new List<Vector2Int>();

    static readonly string[] Tabs = { "Графика", "Звук", "Игра", "Управление" };

    // ---------------------------------------------------------------- flow
    void Start()
    {
        GameSettings.Load();
        BuildResolutions();
        GameSettings.selectedCar = Mathf.Clamp(GameSettings.selectedCar, 0, CarCatalog.Cars.Length - 1);
        GameSettings.selectedColor = Mathf.Clamp(GameSettings.selectedColor, 0, CarCatalog.Paints.Length - 1);
        GameSettings.Apply();
        SpawnSelectedCar();
        EnterMenu(State.Main);
    }

    void BuildResolutions()
    {
        resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width < 800) continue;
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(v)) resolutions.Add(v);
        }
        Vector2Int cur = new Vector2Int(Screen.width, Screen.height);
        if (resolutions.Count == 0 || !resolutions.Contains(cur)) resolutions.Add(cur);
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        if (GameSettings.resWidth == 0) { GameSettings.resWidth = cur.x; GameSettings.resHeight = cur.y; }
    }

    void SpawnSelectedCar()
    {
        vehicle.ApplyCar(CarCatalog.Cars[GameSettings.selectedCar], GameSettings.selectedColor);
        track.Respawn(true);
    }

    void SetMute(bool mute)
    {
        AudioListener.pause = false;
        foreach (AudioSource a in vehicle.GetComponentsInChildren<AudioSource>()) a.mute = mute;
    }

    void EnterMenu(State s)
    {
        state = s;
        Time.timeScale = 1.0f;
        AudioListener.pause = false;
        vehicle.inputLocked = true;
        track.active = false;
        cam.showroom = true;
        hud.visible = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (fx != null) fx.Silence();
        if (score != null) { score.Reset(); }
    }

    void StartRun()
    {
        state = State.Playing;
        Time.timeScale = 1.0f;
        track.Respawn(true);
        vehicle.inputLocked = false;
        track.active = true;
        track.bestLap = 0.0f; track.lastLap = 0.0f;
        cam.showroom = false;
        hud.visible = true;
        hud.hintTimer = 14.0f;
        if (score != null) { score.Reset(); score.totalScore = 0.0f; score.bestChain = 0.0f; }
        Cursor.visible = false;
    }

    void Pause()
    {
        state = State.Paused;
        Time.timeScale = 0.0f;
        AudioListener.pause = true;
        Cursor.visible = true;
    }

    void Resume()
    {
        state = State.Playing;
        Time.timeScale = 1.0f;
        AudioListener.pause = false;
        Cursor.visible = false;
    }

    void BackToMenu()
    {
        if (score != null) { GameSettings.bestScore = Mathf.Max(GameSettings.bestScore, score.totalScore); GameSettings.Save(); }
        track.Respawn(true);
        EnterMenu(State.Main);
    }

    void Quit()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == State.Playing) Pause();
            else if (state == State.Paused) Resume();
            else if (state == State.Settings) { GameSettings.Save(); state = settingsReturn; }
            else if (state == State.Garage) { GameSettings.Save(); state = State.Main; }
        }
        if (state == State.Playing && score != null && score.totalScore > GameSettings.bestScore) GameSettings.bestScore = score.totalScore;
    }

    void OnApplicationQuit() { GameSettings.Save(); }

    // ---------------------------------------------------------------- styles / widgets
    static Texture2D Tex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void EnsureStyles()
    {
        if (tPanel == null)
        {
            tPanel = Tex(new Color(0.04f, 0.045f, 0.06f, 0.82f));
            tBtn = Tex(new Color(0.13f, 0.14f, 0.18f, 0.95f));
            tBtnHot = Tex(new Color(0.95f, 0.32f, 0.10f, 1.0f));
            tBar = Tex(new Color(0.25f, 0.27f, 0.32f, 1.0f));
            tAccent = Tex(new Color(0.95f, 0.32f, 0.10f, 1.0f));
            tWhite = Tex(Color.white);
            tDim = Tex(new Color(0, 0, 0, 0.55f));
        }
        if (sTitle != null && lastW == Screen.width && lastH == Screen.height) return;
        lastW = Screen.width; lastH = Screen.height;
        k = Screen.height / 720.0f;
        GUISkin skin = GUI.skin;
        sTitle = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(64 * k), fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleLeft };
        sH1 = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(32 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        sText = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(20 * k), alignment = TextAnchor.MiddleLeft, wordWrap = true };
        sSmall = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(15 * k), alignment = TextAnchor.MiddleLeft, wordWrap = true };
        sCenter = new GUIStyle(skin.label) { fontSize = Mathf.RoundToInt(20 * k), alignment = TextAnchor.MiddleCenter };
        sBtn = new GUIStyle(skin.button) { fontSize = Mathf.RoundToInt(20 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sBtnBig = new GUIStyle(sBtn) { fontSize = Mathf.RoundToInt(28 * k), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(Mathf.RoundToInt(24 * k), 0, 0, 0) };
        foreach (GUIStyle st in new[] { sBtn, sBtnBig })
        {
            st.normal.background = tBtn; st.hover.background = tBtnHot; st.active.background = tBtnHot; st.focused.background = tBtn;
            st.normal.textColor = Color.white; st.hover.textColor = Color.white; st.active.textColor = Color.white; st.focused.textColor = Color.white;
            st.border = new RectOffset(0, 0, 0, 0);
        }
        sSliderBar = new GUIStyle(skin.horizontalSlider) { fixedHeight = 8 * k };
        sSliderBar.normal.background = tBar; sSliderBar.hover.background = tBar; sSliderBar.active.background = tBar;
        sSliderBar.border = new RectOffset(0, 0, 0, 0);
        sSliderThumb = new GUIStyle(skin.horizontalSliderThumb) { fixedWidth = 16 * k, fixedHeight = 26 * k };
        sSliderThumb.normal.background = tAccent; sSliderThumb.hover.background = tWhite; sSliderThumb.active.background = tWhite;
        sSliderThumb.border = new RectOffset(0, 0, 0, 0);
    }

    void Fill(Rect r, Texture2D t, Color c) { GUI.color = c; GUI.DrawTexture(r, t); GUI.color = Color.white; }

    void Shadow(string text, Rect r, GUIStyle st, Color c)
    {
        Color old = st.normal.textColor;
        st.normal.textColor = new Color(0, 0, 0, 0.7f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
        st.normal.textColor = c;
        GUI.Label(r, text, st);
        st.normal.textColor = old;
    }

    bool Btn(Rect r, string text, GUIStyle st = null) { return GUI.Button(r, text, st ?? sBtn); }

    // settings rows -----------------------------------------------------
    float rowY, rowX, rowW;
    const float RowH = 44.0f;

    void BeginRows(float x, float y, float w) { rowX = x; rowY = y; rowW = w; }

    Rect RowRight()
    {
        float lw = rowW * 0.42f;
        return new Rect(rowX + lw, rowY, rowW - lw, RowH * k - 6 * k);
    }

    void RowLabel(string label)
    {
        GUI.Label(new Rect(rowX, rowY, rowW * 0.42f, RowH * k - 6 * k), label, sText);
    }

    void EndRow() { rowY += RowH * k; }

    int Selector(string label, string[] opts, int idx)
    {
        RowLabel(label);
        Rect r = RowRight();
        float bw = 40 * k;
        if (Btn(new Rect(r.x, r.y, bw, r.height), "◄")) idx = (idx - 1 + opts.Length) % opts.Length;
        Fill(new Rect(r.x + bw + 4 * k, r.y, r.width - 2 * bw - 8 * k, r.height), tBtn, new Color(1, 1, 1, 0.7f));
        GUI.Label(new Rect(r.x + bw, r.y, r.width - 2 * bw, r.height), opts[Mathf.Clamp(idx, 0, opts.Length - 1)], sCenter);
        if (Btn(new Rect(r.xMax - bw, r.y, bw, r.height), "►")) idx = (idx + 1) % opts.Length;
        EndRow();
        return idx;
    }

    bool Toggle(string label, bool v)
    {
        RowLabel(label);
        Rect r = RowRight();
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = v ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
        if (GUI.Button(r, v ? "ВКЛ" : "ВЫКЛ", sBtn)) v = !v;
        GUI.backgroundColor = old;
        EndRow();
        return v;
    }

    float Slider(string label, float v, float min, float max, string fmt)
    {
        RowLabel(label);
        Rect r = RowRight();
        Rect bar = new Rect(r.x, r.y + r.height * 0.5f - 13 * k, r.width - 90 * k, 26 * k);
        v = GUI.HorizontalSlider(bar, v, min, max, sSliderBar, sSliderThumb);
        GUI.Label(new Rect(r.xMax - 80 * k, r.y, 80 * k, r.height), string.Format(fmt, v), sCenter);
        EndRow();
        return v;
    }

    void Changed() { GameSettings.quality = 4; GameSettings.Apply(); }

    // ---------------------------------------------------------------- OnGUI
    void OnGUI()
    {
        EnsureStyles();
        if (state == State.Playing) return;
        float W = Screen.width, H = Screen.height;
        switch (state)
        {
            case State.Main: DrawMain(W, H); break;
            case State.Garage: DrawGarage(W, H); break;
            case State.Settings: DrawSettings(W, H); break;
            case State.Paused: DrawPause(W, H); break;
        }
    }

    void DrawMain(float W, float H)
    {
        Fill(new Rect(0, 0, 470 * k, H), tPanel, Color.white);
        Fill(new Rect(470 * k, 0, 4 * k, H), tAccent, Color.white);
        Shadow("TORSION", new Rect(40 * k, 50 * k, 420 * k, 70 * k), sTitle, Color.white);
        Shadow("DRIFT", new Rect(40 * k, 112 * k, 420 * k, 70 * k), sTitle, new Color(0.95f, 0.32f, 0.10f));
        GUI.Label(new Rect(44 * k, 188 * k, 400 * k, 30 * k), "Выбрана машина: " + CarCatalog.Cars[GameSettings.selectedCar].name, sSmall);

        float y = 250 * k, h = 62 * k, gap = 12 * k, x = 30 * k, w = 410 * k;
        if (Btn(new Rect(x, y, w, h), "ЗАЕЗД", sBtnBig)) StartRun(); y += h + gap;
        if (Btn(new Rect(x, y, w, h), "ГАРАЖ", sBtnBig)) { state = State.Garage; } y += h + gap;
        if (Btn(new Rect(x, y, w, h), "НАСТРОЙКИ", sBtnBig)) { settingsReturn = State.Main; state = State.Settings; } y += h + gap;
        if (Btn(new Rect(x, y, w, h), "ВЫХОД", sBtnBig)) Quit();

        GUI.Label(new Rect(44 * k, H - 90 * k, 420 * k, 30 * k), "Лучший результат: " + Mathf.RoundToInt(GameSettings.bestScore), sText);
        GUI.Label(new Rect(44 * k, H - 56 * k, 420 * k, 30 * k), "W S A D — управление, Пробел — ручник", sSmall);
    }

    void DrawGarage(float W, float H)
    {
        CarSpec c = CarCatalog.Cars[GameSettings.selectedCar];
        float pw = 520 * k;
        Fill(new Rect(0, H - 250 * k, W, 250 * k), tPanel, Color.white);
        Fill(new Rect(0, H - 250 * k, W, 3 * k), tAccent, Color.white);
        Shadow("ГАРАЖ", new Rect(40 * k, 24 * k, 400 * k, 50 * k), sH1, Color.white);

        //arrows
        if (Btn(new Rect(30 * k, H * 0.42f, 70 * k, 90 * k), "◄", sBtnBig)) ChangeCar(-1);
        if (Btn(new Rect(W - 100 * k, H * 0.42f, 70 * k, 90 * k), "►", sBtnBig)) ChangeCar(1);

        float x = 40 * k, y = H - 235 * k;
        Shadow(c.name + "   " + (GameSettings.selectedCar + 1) + "/" + CarCatalog.Cars.Length, new Rect(x, y, pw, 44 * k), sH1, Color.white);
        GUI.Label(new Rect(x, y + 46 * k, pw, 56 * k), c.description, sText);

        //stats
        float sx = x + pw + 40 * k, sy = y + 6 * k, sw = 340 * k;
        string[] names = { "Мощность", "Сцепление", "Дрифт", "Масса" };
        int[] vals = { c.starPower, c.starGrip, c.starDrift, c.starWeight };
        for (int i = 0; i < 4; i++)
        {
            GUI.Label(new Rect(sx, sy + i * 40 * k, 120 * k, 30 * k), names[i], sSmall);
            Fill(new Rect(sx + 120 * k, sy + i * 40 * k + 9 * k, sw, 12 * k), tBar, Color.white);
            Fill(new Rect(sx + 120 * k, sy + i * 40 * k + 9 * k, sw * vals[i] / 10.0f, 12 * k), tAccent, Color.white);
        }

        //paint
        float px = sx + 120 * k + sw + 60 * k;
        GUI.Label(new Rect(px, y, 300 * k, 30 * k), "Цвет: " + CarCatalog.PaintNames[GameSettings.selectedColor], sSmall);
        for (int i = 0; i < CarCatalog.Paints.Length; i++)
        {
            Rect r = new Rect(px + (i % 4) * 52 * k, y + 36 * k + (i / 4) * 52 * k, 44 * k, 44 * k);
            if (i == GameSettings.selectedColor) Fill(new Rect(r.x - 4 * k, r.y - 4 * k, r.width + 8 * k, r.height + 8 * k), tWhite, Color.white);
            Fill(r, tWhite, CarCatalog.Paints[i]);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { GameSettings.selectedColor = i; SpawnSelectedCar(); }
        }

        if (Btn(new Rect(W - 330 * k, H - 90 * k, 290 * k, 60 * k), "ГОТОВО", sBtnBig)) { GameSettings.Save(); state = State.Main; }
        GUI.Label(new Rect(W - 330 * k, H - 150 * k, 290 * k, 50 * k), "Тяни мышью — вращать камеру", sSmall);
    }

    void ChangeCar(int d)
    {
        int n = CarCatalog.Cars.Length;
        GameSettings.selectedCar = (GameSettings.selectedCar + d + n) % n;
        SpawnSelectedCar();
    }

    void DrawPause(float W, float H)
    {
        Fill(new Rect(0, 0, W, H), tDim, Color.white);
        float w = 440 * k, h = 62 * k, gap = 12 * k, x = W / 2 - w / 2, y = H / 2 - 190 * k;
        Shadow("ПАУЗА", new Rect(x, y - 80 * k, w, 60 * k), sH1, Color.white);
        if (Btn(new Rect(x, y, w, h), "ПРОДОЛЖИТЬ", sBtnBig)) Resume(); y += h + gap;
        if (Btn(new Rect(x, y, w, h), "НАЧАТЬ ЗАНОВО", sBtnBig)) StartRun(); y += h + gap;
        if (Btn(new Rect(x, y, w, h), "НАСТРОЙКИ", sBtnBig)) { settingsReturn = State.Paused; state = State.Settings; } y += h + gap;
        if (Btn(new Rect(x, y, w, h), "В ГЛАВНОЕ МЕНЮ", sBtnBig)) BackToMenu();
        if (score != null)
            GUI.Label(new Rect(x, y + h + 20 * k, w, 30 * k), "Очки: " + Mathf.RoundToInt(score.totalScore) + "   Лучшая серия: " + Mathf.RoundToInt(score.bestChain), sCenter);
    }

    void DrawSettings(float W, float H)
    {
        Fill(new Rect(0, 0, W, H), tDim, Color.white);
        float pw = Mathf.Min(900 * k, W - 80 * k), px = W / 2 - pw / 2;
        Fill(new Rect(px, 30 * k, pw, H - 60 * k), tPanel, Color.white);
        Shadow("НАСТРОЙКИ", new Rect(px + 30 * k, 40 * k, 400 * k, 50 * k), sH1, Color.white);

        float tw = (pw - 60 * k) / Tabs.Length;
        for (int i = 0; i < Tabs.Length; i++)
        {
            Rect r = new Rect(px + 30 * k + i * tw, 100 * k, tw - 6 * k, 44 * k);
            Color old = GUI.backgroundColor;
            if (i == settingsTab) Fill(new Rect(r.x, r.yMax - 4 * k, r.width, 4 * k), tAccent, Color.white);
            if (Btn(r, Tabs[i])) { settingsTab = i; scroll = Vector2.zero; }
            GUI.backgroundColor = old;
        }

        Rect view = new Rect(px + 30 * k, 160 * k, pw - 60 * k, H - 160 * k - 110 * k);
        float contentH = 20 * RowH * k;
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 20 * k, contentH));
        BeginRows(0, 0, view.width - 30 * k);
        switch (settingsTab)
        {
            case 0: GraphicsRows(); break;
            case 1: SoundRows(); break;
            case 2: GameRows(); break;
            case 3: ControlRows(); break;
        }
        GUI.EndScrollView();

        if (Btn(new Rect(px + pw - 290 * k, H - 90 * k, 260 * k, 54 * k), "НАЗАД", sBtnBig)) { GameSettings.Save(); state = settingsReturn; }
        if (settingsTab == 0 && Btn(new Rect(px + 30 * k, H - 90 * k, 260 * k, 54 * k), "ПО УМОЛЧАНИЮ", sBtn)) { GameSettings.ApplyPreset(2); GameSettings.Apply(); }
    }

    void GraphicsRows()
    {
        int q = Selector("Пресет качества", GameSettings.QualityNames, GameSettings.quality);
        if (q != GameSettings.quality) { if (q < 4) { GameSettings.ApplyPreset(q); } else GameSettings.quality = 4; GameSettings.Apply(); }

        //resolution
        int ri = 0;
        for (int i = 0; i < resolutions.Count; i++) if (resolutions[i].x == GameSettings.resWidth && resolutions[i].y == GameSettings.resHeight) ri = i;
        string[] rn = new string[resolutions.Count];
        for (int i = 0; i < rn.Length; i++) rn[i] = resolutions[i].x + " x " + resolutions[i].y;
        int nri = Selector("Разрешение", rn, ri);
        if (nri != ri) { GameSettings.resWidth = resolutions[nri].x; GameSettings.resHeight = resolutions[nri].y; GameSettings.Apply(); }

        bool fs = Toggle("Полный экран", GameSettings.fullscreen);
        if (fs != GameSettings.fullscreen) { GameSettings.fullscreen = fs; GameSettings.Apply(); }
        bool vs = Toggle("Вертикальная синхронизация", GameSettings.vsync);
        if (vs != GameSettings.vsync) { GameSettings.vsync = vs; GameSettings.Apply(); }
        int fp = Selector("Лимит FPS", GameSettings.FpsNames, GameSettings.fpsLimit);
        if (fp != GameSettings.fpsLimit) { GameSettings.fpsLimit = fp; GameSettings.Apply(); }

        int sh = Selector("Тени", GameSettings.ShadowNames, GameSettings.shadows);
        if (sh != GameSettings.shadows) { GameSettings.shadows = sh; Changed(); }
        float sd = Slider("Дальность теней", GameSettings.shadowDistance, 20, 300, "{0:0} м");
        if (Mathf.RoundToInt(sd) != GameSettings.shadowDistance) { GameSettings.shadowDistance = Mathf.RoundToInt(sd / 10.0f) * 10; Changed(); }
        int ms = Selector("Сглаживание (MSAA)", GameSettings.MsaaNames, GameSettings.msaa);
        if (ms != GameSettings.msaa) { GameSettings.msaa = ms; Changed(); }
        int an = Selector("Анизотропная фильтрация", GameSettings.AnisoNames, GameSettings.aniso);
        if (an != GameSettings.aniso) { GameSettings.aniso = an; Changed(); }
        int tx = Selector("Качество текстур", GameSettings.TexNames, GameSettings.textures);
        if (tx != GameSettings.textures) { GameSettings.textures = tx; Changed(); }
        bool pp = Toggle("Постобработка (блум, цвет)", GameSettings.postFx);
        if (pp != GameSettings.postFx) { GameSettings.postFx = pp; Changed(); }
        bool sc = Toggle("Деревья и декор", GameSettings.scenery);
        if (sc != GameSettings.scenery) { GameSettings.scenery = sc; Changed(); }
        bool fg = Toggle("Туман", GameSettings.fog);
        if (fg != GameSettings.fog) { GameSettings.fog = fg; Changed(); }
        float rd = Slider("Дальность прорисовки", GameSettings.renderDistance, 300, 2000, "{0:0} м");
        if (Mathf.RoundToInt(rd) != GameSettings.renderDistance) { GameSettings.renderDistance = Mathf.RoundToInt(rd / 50.0f) * 50; Changed(); }
        float fv = Slider("Поле зрения", GameSettings.fov, 50, 85, "{0:0}°");
        GameSettings.fov = Mathf.Round(fv);
        float sm = Slider("Дым из-под колёс", GameSettings.smoke, 0, 1, "{0:0.0}");
        GameSettings.smoke = sm;
        bool sk = Toggle("Следы шин", GameSettings.skidMarks);
        if (sk != GameSettings.skidMarks) { GameSettings.skidMarks = sk; }
    }

    void SoundRows()
    {
        float m = Slider("Общая громкость", GameSettings.master, 0, 1, "{0:0%}");
        if (!Mathf.Approximately(m, GameSettings.master)) { GameSettings.master = m; GameSettings.Apply(); }
        GameSettings.sfx = Slider("Визг шин и эффекты", GameSettings.sfx, 0, 1, "{0:0%}");
    }

    void GameRows()
    {
        float a = Slider("Помощник дрифта", GameSettings.assist, 0, 1, "{0:0.00}");
        if (!Mathf.Approximately(a, GameSettings.assist)) { GameSettings.assist = a; GameSettings.Apply(); }
        BeginNote("0 — чистая физика, 0.7 — удобный дрифт, 1 — максимум помощи");
        GameSettings.units = Selector("Единицы скорости", GameSettings.UnitNames, GameSettings.units);
        GameSettings.showHud = Toggle("Показывать HUD", GameSettings.showHud);
        GameSettings.cameraShake = Toggle("Тряска камеры", GameSettings.cameraShake);
        if (Btn(new Rect(rowX + rowW * 0.42f, rowY, rowW * 0.58f, RowH * k - 6 * k), "Сбросить рекорд (" + Mathf.RoundToInt(GameSettings.bestScore) + ")")) GameSettings.bestScore = 0.0f;
        rowY += RowH * k;
    }

    void BeginNote(string text)
    {
        GUI.Label(new Rect(rowX, rowY - 6 * k, rowW, 28 * k), text, sSmall);
        rowY += 26 * k;
    }

    void ControlRows()
    {
        string[,] keys =
        {
            { "W", "Газ" }, { "S", "Тормоз / задний ход" }, { "A / D", "Руль влево / вправо" }, { "Пробел", "Ручник" },
            { "Left Shift", "Клач-кик (удерживай и отпусти)" }, { "R", "Вернуть машину на трассу" }, { "C", "Сменить камеру" }, { "Esc", "Пауза / назад" },
        };
        for (int i = 0; i < keys.GetLength(0); i++)
        {
            GUI.Label(new Rect(rowX, rowY, rowW * 0.3f, RowH * k - 6 * k), keys[i, 0], sH1.fontSize > 0 ? sText : sText);
            GUI.Label(new Rect(rowX + rowW * 0.3f, rowY, rowW * 0.7f, RowH * k - 6 * k), keys[i, 1], sText);
            rowY += RowH * k;
        }
        BeginNote("Совет: разгонись до 60–80 км/ч, дай газ и поверни — машина уйдёт в занос. Держи A/D — помощник удержит угол.");
    }
}
