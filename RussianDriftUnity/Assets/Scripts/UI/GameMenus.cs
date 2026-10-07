using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Audio;
using RussianDrift.Core;
using RussianDrift.Meta;

namespace RussianDrift.UI
{
    /// <summary>Pause menu and results screen shown over the GameWorld.</summary>
    public static class GameMenus
    {
        public static RectTransform CreatePause(Transform canvas, Action onResume, Action onRestart, Action onSettings, Action onExit)
        {
            var root = Ui.Rect("PauseMenu", canvas);
            Ui.Stretch(root);
            var dim = Ui.Img(root, "Dim", null, new Color(0, 0, 0, 0.7f));
            dim.raycastTarget = true; Ui.Stretch(dim.rectTransform);
            var panel = Ui.Panel(root, "Panel", Theme.Bg, 28);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 800));
            var title = Ui.Loc(panel.transform, "pause.title", 64, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(600, 100), new Vector2(0.5f, 1));
            var col = Ui.VBox(panel.transform, 18, TextAnchor.UpperCenter, true, false);
            Ui.Anchor((RectTransform)col.transform, Vector2.zero, Vector2.one, new Vector2(50, 40), new Vector2(-50, -150));
            var resume = Ui.Button(col.transform, "pause.resume", onResume, Theme.Accent, 44); Ui.Size(resume.image.transform, -1, 130);
            var restart = Ui.Button(col.transform, "pause.restart", onRestart, Theme.PanelLight, 38); Ui.Size(restart.image.transform, -1, 110);
            var settings = Ui.Button(col.transform, "pause.settings", onSettings, Theme.PanelLight, 38); Ui.Size(settings.image.transform, -1, 110);
            var exit = Ui.Button(col.transform, "pause.exit", onExit, new Color(0.35f, 0.14f, 0.14f, 0.95f), 38); Ui.Size(exit.image.transform, -1, 110);
            return root;
        }

        public static RectTransform CreateResults(Transform canvas, RunResult r, List<AchievementDef> newAch, bool canDouble, Action onRetry, Action onMenu, Action onGarage)
        {
            var root = Ui.Rect("Results", canvas);
            Ui.Stretch(root);
            var dim = Ui.Img(root, "Dim", null, new Color(0, 0, 0, 0.35f));
            dim.raycastTarget = true; Ui.Stretch(dim.rectTransform);
            var panel = Ui.Panel(root, "Panel", new Color(0.04f, 0.05f, 0.08f, 0.93f), 30);
            Ui.Place(panel.rectTransform, new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(900, 1000), new Vector2(0, 0.5f));

            string heading;
            Color hc = Theme.Gold;
            switch (r.mode)
            {
                case GameMode.DriftBattle: heading = Loc.T(r.win ? "res.victory" : "res.defeat"); hc = r.win ? Theme.Gold : Theme.Bad; break;
                case GameMode.TimeAttack: heading = Loc.T("res.finish"); break;
                default: heading = Loc.T("res.timeup"); break;
            }
            var h = Ui.Label(panel.transform, heading, 78, hc, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(h.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(840, 110), new Vector2(0.5f, 1));
            var sub = Ui.Label(panel.transform, r.title, 32, Theme.Dim, TextAnchor.MiddleCenter);
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(840, 44), new Vector2(0.5f, 1));

            string main = r.mode == GameMode.TimeAttack ? Ui.FormatTime(r.time) : Ui.Money(r.score);
            var big = Ui.Label(panel.transform, main, 110, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Ui.Place(big.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -184), new Vector2(840, 140), new Vector2(0.5f, 1));
            if (r.newRecord)
            {
                var rec = Ui.Loc(panel.transform, "res.record", 36, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, true);
                Ui.Place(rec.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -318), new Vector2(840, 50), new Vector2(0.5f, 1));
            }
            // medals
            if (r.mode != GameMode.DriftBattle)
            {
                var stars = Ui.HBox(panel.transform, 20, TextAnchor.MiddleCenter, false, false);
                Ui.Place((RectTransform)stars.transform, new Vector2(0.5f, 1), new Vector2(0, -372), new Vector2(500, 110), new Vector2(0.5f, 1));
                for (int i = 0; i < 3; i++)
                {
                    var st = Ui.Img(stars.transform, "Star", UiSprites.Star(), i < r.medal ? Theme.Gold : new Color(1, 1, 1, 0.15f));
                    Ui.Size(st.transform, 100, 100);
                }
            }
            var det = Ui.Label(panel.transform, r.detail, 28, Theme.Dim, TextAnchor.MiddleCenter);
            Ui.Place(det.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -490), new Vector2(840, 80), new Vector2(0.5f, 1));

            // rewards
            var rew = Ui.Panel(panel.transform, "Rewards", new Color(1, 1, 1, 0.06f), 18);
            Ui.Place(rew.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -580), new Vector2(800, 120), new Vector2(0.5f, 1));
            var c1 = Ui.Label(rew.transform, "+" + Ui.Money(r.coins) + "  <size=26><color=#9AA0B5>" + Loc.T("res.coins") + "</color></size>", 48, Theme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Anchor(c1.rectTransform, Vector2.zero, new Vector2(0.55f, 1), new Vector2(26, 0), Vector2.zero);
            var c2 = Ui.Label(rew.transform, "+" + r.xp + "  <size=26><color=#9AA0B5>XP</color></size>", 48, Theme.Accent2, TextAnchor.MiddleRight, FontStyle.Bold);
            Ui.Anchor(c2.rectTransform, new Vector2(0.5f, 0), Vector2.one, Vector2.zero, new Vector2(-26, 0));

            if (newAch != null && newAch.Count > 0)
            {
                string t = "";
                foreach (var a in newAch) t += (Loc.IsRu ? a.ruName : a.enName) + "  +" + Ui.Money(a.reward) + "\n";
                var al = Ui.Label(panel.transform, Loc.T("res.newach") + "\n<color=#FFD133>" + t + "</color>", 26, Theme.Text, TextAnchor.UpperCenter);
                Ui.Place(al.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -720), new Vector2(820, 120), new Vector2(0.5f, 1));
            }

            var row = Ui.HBox(panel.transform, 14, TextAnchor.MiddleCenter, true, true);
            Ui.Place((RectTransform)row.transform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(840, 110), new Vector2(0.5f, 0));
            var retry = Ui.Button(row.transform, "res.retry", onRetry, Theme.Accent, 34); Ui.Size(retry.image.transform, -1, 110, 1);
            var garage = Ui.Button(row.transform, "menu.garage", onGarage, Theme.PanelLight, 30); Ui.Size(garage.image.transform, -1, 110, 1);
            var menu = Ui.Button(row.transform, "res.menu", onMenu, Theme.PanelLight, 30); Ui.Size(menu.image.transform, -1, 110, 1);

            if (canDouble && r.coins > 0)
            {
                var ads = Services.Get<IAdService>();
                ButtonHolder holder = new ButtonHolder();
                var dbl = Ui.Button(panel.transform, "res.double", null, Theme.Good * 0.9f, 32);
                Ui.Place(dbl.image.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(840, 96), new Vector2(0.5f, 0));
                dbl.button.onClick.AddListener(() =>
                {
                    if (ads == null) return;
                    dbl.button.interactable = false;
                    ads.ShowRewarded("double_reward", ok =>
                    {
                        if (!ok) { dbl.button.interactable = true; return; }
                        var p = Services.Get<ProfileService>();
                        p.AddCurrency(r.coins); p.Save.Save();
                        AudioManager.Ensure().Play("cash");
                        Toast.Show(Loc.F("shop.gotcoins", r.coins));
                    });
                });
            }
            return root;
        }

        private class ButtonHolder { }
    }
}
