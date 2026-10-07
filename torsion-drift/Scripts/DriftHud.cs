using UnityEngine;

/// <summary>Immediate-mode HUD: speed, gear, rpm, drift score and lap times. No prefabs or canvas needed.</summary>
public class DriftHud : MonoBehaviour
{
    public Vehicle vehicle;
    public DriftScore score;
    public DriftTrack track;
    public bool visible = true;

    GUIStyle big, mid, small, center;
    Texture2D white;
    public float hintTimer = 14.0f;

    void Awake()
    {
        white = new Texture2D(1, 1);
        white.SetPixel(0, 0, Color.white);
        white.Apply();
    }

    void Rect(float x, float y, float w, float h, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = Color.white;
    }

    void Label(string t, float x, float y, float w, float h, GUIStyle st, Color c)
    {
        Color old = st.normal.textColor;
        st.normal.textColor = new Color(0, 0, 0, 0.7f * c.a);
        GUI.Label(new Rect(x + 2, y + 2, w, h), t, st);
        st.normal.textColor = c;
        GUI.Label(new Rect(x, y, w, h), t, st);
        st.normal.textColor = old;
    }

    void MakeStyles()
    {
        float k = Screen.height / 720.0f;
        big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(64 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        mid = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(28 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * k), alignment = TextAnchor.MiddleLeft };
        center = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(40 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    static string Time2Str(float t)
    {
        if (t <= 0.0f) return "--:--.--";
        int m = (int)(t / 60.0f);
        return m.ToString("00") + ":" + (t - m * 60.0f).ToString("00.00");
    }

    void OnGUI()
    {
        if (vehicle == null || !visible || !GameSettings.showHud) return;
        if (big == null || Mathf.Abs(big.fontSize - 64 * Screen.height / 720.0f) > 2) MakeStyles();
        float W = Screen.width, H = Screen.height, k = H / 720.0f;
        hintTimer -= Time.unscaledDeltaTime;

        // --- speedometer (bottom right) ---
        float bx = W - 40 * k, by = H - 150 * k;
        Label(Mathf.RoundToInt(vehicle.speedKmh * (GameSettings.units == 0 ? 1.0f : 0.6214f)).ToString(), bx - 300 * k, by, 300 * k, 80 * k, big, Color.white);
        Label(GameSettings.UnitNames[GameSettings.units], bx - 300 * k, by + 70 * k, 300 * k, 30 * k, new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, new Color(1, 1, 1, 0.7f));
        Label(vehicle.gearLabel, bx - 420 * k, by, 100 * k, 80 * k, new GUIStyle(big) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(54 * k) }, new Color(1.0f, 0.8f, 0.2f));
        float rpmN = Mathf.Clamp01(vehicle.engine.engineRPM / vehicle.engine.redlineRPM);
        Rect(bx - 420 * k, by + 100 * k, 420 * k, 12 * k, new Color(0, 0, 0, 0.55f));
        Rect(bx - 420 * k, by + 100 * k, 420 * k * rpmN, 12 * k, rpmN > 0.9f ? new Color(1, 0.2f, 0.2f) : new Color(1, 0.8f, 0.2f));

        // --- drift score (top centre) ---
        if (score != null)
        {
            if (score.chainScore > 0.0f)
            {
                Color c = score.drifting ? new Color(1.0f, 0.85f, 0.2f) : new Color(1, 1, 1, 0.65f);
                Label(Mathf.RoundToInt(score.chainScore).ToString(), W / 2 - 250 * k, 30 * k, 500 * k, 60 * k, center, c);
                Label("x" + score.multiplier.ToString("0.0") + "   " + Mathf.RoundToInt(Mathf.Abs(vehicle.driftAngle)) + "°", W / 2 - 250 * k, 80 * k, 500 * k, 36 * k, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, c);
            }
            else if (score.bankedFlash > 0.0f)
            {
                Label("+" + Mathf.RoundToInt(score.lastBanked), W / 2 - 250 * k, 30 * k, 500 * k, 60 * k, center, new Color(0.4f, 1, 0.5f, Mathf.Clamp01(score.bankedFlash)));
            }
            if (score.lostFlash > 0.0f)
                Label("ЦЕПОЧКА ПОТЕРЯНА", W / 2 - 300 * k, 120 * k, 600 * k, 50 * k, center, new Color(1, 0.3f, 0.3f, Mathf.Clamp01(score.lostFlash)));

            Label("ОЧКИ  " + Mathf.RoundToInt(score.totalScore), 24 * k, 20 * k, 400 * k, 40 * k, mid, Color.white);
            Label("Лучшая серия  " + Mathf.RoundToInt(score.bestChain), 24 * k, 58 * k, 400 * k, 30 * k, small, new Color(1, 1, 1, 0.7f));
        }

        // --- laps ---
        if (track != null)
        {
            Label("КРУГ  " + (track.lapCount + 1) + "   " + Time2Str(track.lapTime), 24 * k, 92 * k, 500 * k, 30 * k, small, Color.white);
            Label("Лучший  " + Time2Str(track.bestLap) + "     Прошлый  " + Time2Str(track.lastLap), 24 * k, 118 * k, 600 * k, 30 * k, small, new Color(1, 1, 1, 0.7f));
        }

        // --- controls hint ---
        if (hintTimer > 0.0f)
        {
            float a = Mathf.Clamp01(hintTimer);
            Label("W — газ    S — тормоз / задний ход    A / D — руль\nПробел — ручник    Shift — клач-кик    R — вернуть на трассу    C — камера",
                24 * k, H - 90 * k, 1200 * k, 70 * k, small, new Color(1, 1, 1, a));
        }
    }
}
