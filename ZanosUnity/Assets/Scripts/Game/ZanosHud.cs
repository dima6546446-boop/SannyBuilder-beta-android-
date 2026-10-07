using System.Collections.Generic;
using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>HUD: очки, серия и множитель, угол, спидометр с тахометром и передачей, миникарта, таймер, всплывающие сообщения, отсчёт, экранные кнопки.</summary>
    public sealed partial class ZanosApp
    {
        Texture2D miniTex; string miniFor; float miniS = 2f; float mx0, mz1; Texture2D arrow;

        static string Fmt(double s) { s = System.Math.Max(0, s); return ((int)(s / 60)) + ":" + ((int)(s % 60)).ToString("00"); }

        void BuildMini(MapData m)
        {
            var b = m.bounds; miniS = Mathf.Min(2f, 900f / (float)System.Math.Max(b.x1 - b.x0, b.z1 - b.z0)); mx0 = (float)b.x0; mz1 = (float)b.z1;
            int w = Mathf.CeilToInt((float)(b.x1 - b.x0) * miniS) + 2, h = Mathf.CeilToInt((float)(b.z1 - b.z0) * miniS) + 2;
            var px = new Color32[w * h]; var ground = new Color32(27, 34, 48, 255); for (int i = 0; i < px.Length; i++) px[i] = ground;
            System.Func<string, Color32> col = t => { switch (t) { case "concrete": return new Color32(58, 65, 80, 255); case "grass": return new Color32(39, 64, 44, 255); case "gravel": return new Color32(106, 98, 84, 255); case "dirt": return new Color32(90, 70, 48, 255); case "wet": return new Color32(60, 86, 104, 255); case "snow": return new Color32(138, 150, 160, 255); default: return new Color32(74, 81, 96, 255); } };
            System.Action<float, float, float, Color32> disc = (cx, cz, r, c) => { int x0 = Mathf.Max(0, (int)((cx - mx0 - r) * miniS)), x1 = Mathf.Min(w - 1, (int)((cx - mx0 + r) * miniS)), y0 = Mathf.Max(0, (int)((mz1 - cz - r) * miniS)), y1 = Mathf.Min(h - 1, (int)((mz1 - cz + r) * miniS)); for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) { float dx = x / miniS + mx0 - cx, dz = mz1 - y / miniS - cz; if (dx * dx + dz * dz <= r * r) px[y * w + x] = c; } };
            foreach (var s in m.surfaces)
            {
                var c = col(s.type);
                if (s.kind == "rect") { int x0 = Mathf.Max(0, (int)((s.x0 - mx0) * miniS)), x1 = Mathf.Min(w - 1, (int)((s.x1 - mx0) * miniS)), y0 = Mathf.Max(0, (int)((mz1 - s.z1) * miniS)), y1 = Mathf.Min(h - 1, (int)((mz1 - s.z0) * miniS)); for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) px[y * w + x] = c; }
                else if (s.kind == "circle") disc((float)s.x, (float)s.z, (float)s.r, c);
                else for (int i = 0; i + 3 < s.pts.Length; i += 2) { float ax = (float)s.pts[i], az = (float)s.pts[i + 1], bx = (float)s.pts[i + 2], bz = (float)s.pts[i + 3]; int n = Mathf.CeilToInt(Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) * 1.5f) + 1; for (int k = 0; k <= n; k++) disc(Mathf.Lerp(ax, bx, k / (float)n), Mathf.Lerp(az, bz, k / (float)n), (float)s.hw, c); }
            }
            foreach (var bx in m.boxes) disc((float)bx.x, (float)bx.z, Mathf.Max((float)bx.w, (float)bx.d) * 0.5f, new Color32(12, 15, 20, 255));
            foreach (var sg in m.segments) { int n = Mathf.CeilToInt(Mathf.Sqrt((float)((sg.c - sg.a) * (sg.c - sg.a) + (sg.d - sg.b) * (sg.d - sg.b))) * miniS) + 1; for (int k = 0; k <= n; k++) { int x = (int)(((float)(sg.a + (sg.c - sg.a) * k / n) - mx0) * miniS), y = (int)((mz1 - (float)(sg.b + (sg.d - sg.b) * k / n)) * miniS); if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = new Color32(255, 122, 0, 255); } }
            if (miniTex != null) Destroy(miniTex);
            miniTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; miniTex.SetPixels32(px); miniTex.Apply(); miniFor = m.id;
            if (arrow == null) { arrow = new Texture2D(16, 16); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) arrow.SetPixel(x, y, Mathf.Abs(x - 7.5f) < (16 - y) * 0.5f && y > 1 ? Acc : Color.clear); arrow.Apply(); }
        }

        void DrawHud()
        {
            if (session == null) return; var car = session.Car; var sc = session.Scoring;
            // очки
            shownTotal += (sc.Total - shownTotal) * Mathf.Clamp01(Time.unscaledDeltaTime * 10); if (Mathf.Abs(sc.Total - shownTotal) < 1) shownTotal = sc.Total;
            Lbl(new Rect(W / 2 - 200, 6, 400, 16), "ОЧКИ", new GUIStyle(stSmall) { alignment = TextAnchor.MiddleCenter }); Lbl(new Rect(W / 2 - 200, 18, 400, 60), Num((long)shownTotal), new GUIStyle(stHudBig));
            if (sc.Chain > 0)
            {
                Lbl(new Rect(W / 2 - 200, 80, 400, 34), "<color=#ffb347>+" + Num((long)sc.Chain) + "</color>   ×" + sc.Mult.ToString("0.##"), new GUIStyle(stHud) { alignment = TextAnchor.MiddleCenter, richText = true });
                float frac = sc.Gap > 0 ? Mathf.Max(0, 1f - (float)sc.Gap / 0.9f) : (float)(sc.ChainTime % 2.4 / 2.4); GUI.DrawTexture(new Rect(W / 2 - 110, 116, 220, 5), tPanel2); GUI.color = Acc; GUI.DrawTexture(new Rect(W / 2 - 110, 116, 220 * frac, 5), tWhite); GUI.color = Color.white;
            }
            // угол
            Lbl(new Rect(24, 8, 120, 16), "УГОЛ", stSmall); Lbl(new Rect(24, 22, 200, 60), Mathf.Round(sc.Active ? (float)sc.Angle : Mathf.Abs((float)car.DriftAngle) * Mathf.Rad2Deg) + "°", new GUIStyle(stHud) { fontSize = 48 });
            // режим
            if (session.Opts.Mode != GameMode.Free)
            {
                double left = session.TimeLimit - session.Time; var tc = new GUIStyle(stHud) { fontSize = 40, alignment = TextAnchor.UpperRight, normal = { textColor = left < 10 ? Bad : Color.white } };
                Lbl(new Rect(W - 324, 8, 300, 50), Fmt(left), tc); string goal = session.Opts.Mode == GameMode.Timed ? "Цель " + Num(session.Map.timedGoal) : session.Challenge.type == "gates" ? "Ворота " + session.GateIndex + " / " + session.Map.gates.Length : session.Challenge.type == "angle" ? "Угол " + session.Challenge.target + "°: " + session.HoldT.ToString("0.0") + " / " + session.Challenge.hold + " с" : "Цель " + Num((long)session.Challenge.target);
                Lbl(new Rect(W - 424, 54, 400, 22), goal, new GUIStyle(stSmall) { alignment = TextAnchor.UpperRight }); if (session.Challenge != null) Lbl(new Rect(W - 524, 74, 500, 22), session.Challenge.name, new GUIStyle(stSmall) { alignment = TextAnchor.UpperRight, normal = { textColor = Color.white } });
            }
            else Lbl(new Rect(W - 424, 14, 400, 22), session.Map.name.ToUpperInvariant(), new GUIStyle(stSmall) { alignment = TextAnchor.UpperRight });
            // спидометр
            DrawGauge(car);
            // миникарта
            if (miniFor != session.Map.id) BuildMini(session.Map); DrawMini(car);
            // всплывающие
            for (int i = pops.Count - 1; i >= 0; i--) { var p = pops[i]; float t = p.Value - Time.unscaledDeltaTime; pops[i] = new KeyValuePair<string, float>(p.Key, t); if (t <= 0) { pops.RemoveAt(i); popCols.RemoveAt(i); } }
            for (int i = 0; i < pops.Count; i++) { float a = Mathf.Clamp01(pops[i].Value * 2f); var c = popCols[i]; c.a = a; var st = new GUIStyle(stHud) { alignment = TextAnchor.MiddleCenter, fontSize = 30, normal = { textColor = c } }; Lbl(new Rect(W / 2 - 300, 190 + i * 38 - (1.9f - pops[i].Value) * 14, 600, 36), pops[i].Key, st); }
            if (countdown > 0 && state == AppState.Play) Lbl(new Rect(W / 2 - 150, 220, 300, 200), Mathf.CeilToInt(countdown) <= 3 ? Mathf.CeilToInt(countdown).ToString() : "", new GUIStyle(stHudBig) { fontSize = 160, normal = { textColor = Color.white } });
            if (hintT > 0 && state == AppState.Play && !Application.isMobilePlatform) Lbl(new Rect(W / 2 - 330, H - 38, 660, 24), "Газ ↑ + руль · Пробел — ручник · Shift — рывок · C — камера · H — подсказки", new GUIStyle(stSmall) { alignment = TextAnchor.MiddleCenter });
            if (camNameT > 0) Lbl(new Rect(W / 2 - 150, H - 80, 300, 24), camName, new GUIStyle(stSmall) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Acc2 } });
            if (S.showFps) Lbl(new Rect(W / 2 - 40, H - 22, 80, 18), Mathf.Round(fpsShow) + " FPS", new GUIStyle(stSmall) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Ok } });
            DrawTouch();
        }

        void DrawGauge(Car car)
        {
            var s = car.Spec; float x = W - 290, y = H - 190; GUI.Box(new Rect(x, y, 270, 170), "", stPanel);
            float kmh = (float)car.Speed * 3.6f * (S.mph ? 0.6214f : 1f);
            Lbl(new Rect(x, y + 6, 270, 80), Mathf.Round(kmh).ToString(), new GUIStyle(stHud) { fontSize = 72, alignment = TextAnchor.MiddleCenter }); Lbl(new Rect(x, y + 82, 270, 18), S.mph ? "МИЛЬ/Ч" : "КМ/Ч", new GUIStyle(stSmall) { alignment = TextAnchor.MiddleCenter });
            float max = Mathf.Ceil((float)s.Redline / 1000f) * 1000f + 500f; float frac = Mathf.Clamp01((float)car.Rpm / max); int segs = 24;
            for (int i = 0; i < segs; i++) { float t = i / (float)segs; GUI.color = t < frac ? (t > (float)(s.Redline - 500) / max ? Bad : Acc) : new Color(0.2f, 0.22f, 0.28f); GUI.DrawTexture(new Rect(x + 14 + i * 10.4f, y + 108, 8, 14), tWhite); }
            GUI.color = Color.white; Lbl(new Rect(x + 14, y + 128, 120, 36), car.Reversing ? "R" : car.Gear.ToString(), new GUIStyle(stHud) { fontSize = 34, normal = { textColor = Acc } });
            if (car.Input.Handbrake) Lbl(new Rect(x + 120, y + 134, 140, 24), "РУЧНИК", new GUIStyle(stSmall) { alignment = TextAnchor.MiddleRight, normal = { textColor = Bad } });
            if (s.Turbo > 0) { GUI.color = new Color(0.22f, 0.77f, 1f); GUI.DrawTexture(new Rect(x + 14, y + 104, 240 * (float)car.Boost * 0.33f, 3), tWhite); GUI.color = Color.white; }
        }

        void DrawMini(Car car)
        {
            if (miniTex == null) return; var r = new Rect(20, H - 200, 180, 180); float win = 150f; float cx = ((float)car.X - mx0) * miniS, cy = (mz1 - (float)car.Z) * miniS;
            GUI.Box(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), "", stPanel);
            float halfPx = win * 0.5f * miniS; var uv = new Rect((cx - halfPx) / miniTex.width, 1f - (cy + halfPx) / miniTex.height, 2 * halfPx / miniTex.width, 2 * halfPx / miniTex.height);
            GUI.DrawTextureWithTexCoords(r, miniTex, uv);
            var m = GUI.matrix; GUIUtility.RotateAroundPivot((float)(car.H * Mathf.Rad2Deg), r.center); GUI.DrawTexture(new Rect(r.center.x - 8, r.center.y - 10, 16, 20), arrow); GUI.matrix = m;
        }

        void DrawTouch()
        {
            bool touch = Application.isMobilePlatform;
            if (!touch || state != AppState.Play) { controls.TouchRects = null; return; }
            // кнопки в логических координатах → прямоугольники в экранных (y снизу) для опроса мультитача
            Rect[] logical = { new Rect(24, H - 120, 96, 96), new Rect(132, H - 120, 96, 96), new Rect(W - 130, H - 130, 106, 106), new Rect(W - 250, H - 110, 88, 88), new Rect(W - 130, H - 250, 90, 90), new Rect(W - 250, H - 210, 70, 70) };
            string[] names = { "◀", "▶", "ГАЗ", "ТОРМ", "DRIFT", "РЫВОК" }; bool[] down = { controls.TouchLeft, controls.TouchRight, controls.TouchGas, controls.TouchBrake, controls.TouchHand, controls.TouchKick };
            controls.TouchRects = new Rect[6];
            for (int i = 0; i < 6; i++)
            {
                var r = logical[i]; GUI.color = down[i] ? Acc : new Color(1, 1, 1, 0.35f); GUI.DrawTexture(r, tWhite); GUI.color = Color.white; GUI.Label(r, names[i], new GUIStyle(stBtn) { normal = { background = null, textColor = Color.white }, fontSize = i < 2 ? 34 : 15 });
                controls.TouchRects[i] = new Rect(r.x * ui, Screen.height - (r.y + r.height) * ui, r.width * ui, r.height * ui);
            }
            if (GUI.Button(new Rect(W - 64, 10, 48, 44), "II", stBtn)) PauseToggle();
        }
    }
}
