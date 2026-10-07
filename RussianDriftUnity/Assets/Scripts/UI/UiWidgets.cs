using System;
using UnityEngine;
using UnityEngine.UI;
using RussianDrift.Core;

namespace RussianDrift.UI
{
    /// <summary>Reusable composite widgets: modal dialog, stat bars, currency chip, level chip.</summary>
    public static class UiWidgets
    {
        public class Modal
        {
            public RectTransform root;
            public RectTransform content;
            public Image panel;
            public Action onClose;
            public void Close() { if (onClose != null) onClose(); if (root != null) UnityEngine.Object.Destroy(root.gameObject); }
        }

        public static Modal CreateModal(Transform parent, string titleKey, Vector2 size, Action onClose = null)
        {
            var m = new Modal();
            m.root = Ui.Rect("Modal", parent);
            Ui.Stretch(m.root);
            var dim = Ui.Img(m.root, "Dim", null, new Color(0, 0, 0, 0.72f));
            dim.raycastTarget = true; Ui.Stretch(dim.rectTransform);
            m.panel = Ui.Panel(m.root, "Panel", Theme.Bg, 28);
            Ui.Place(m.panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var title = Ui.Loc(m.panel.transform, titleKey, 50, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(40, -18), new Vector2(size.x - 380, 90), new Vector2(0, 1));
            m.onClose = onClose;
            var close = Ui.Button(m.panel.transform, "common.close", () => m.Close(), Theme.Accent, 32);
            Ui.Place(close.image.rectTransform, new Vector2(1, 1), new Vector2(-30, -24), new Vector2(240, 76), new Vector2(1, 1));
            m.content = Ui.Rect("Content", m.panel.transform);
            Ui.Anchor(m.content, Vector2.zero, Vector2.one, new Vector2(30, 24), new Vector2(-30, -120));
            return m;
        }

        public class StatBars
        {
            public Image[] fills = new Image[5];
            public Text[] labels = new Text[5];
            public RectTransform root;

            public void Set(VehicleStats s)
            {
                float[] v = { s.Power01, s.Speed01, s.Handling01, s.Brake01, s.Drift01 };
                for (int i = 0; i < 5; i++) Ui.SetBar(fills[i], v[i]);
            }

            public void SetCompare(VehicleStats cur, VehicleStats pre)
            {
                Set(pre);
            }
        }

        public static StatBars CreateStatBars(Transform parent, float width, float rowH = 46f)
        {
            var sb = new StatBars();
            var box = Ui.VBox(parent, 8, TextAnchor.UpperLeft, true, false);
            sb.root = (RectTransform)box.transform;
            string[] keys = { "stat.power", "stat.speed", "stat.handling", "stat.brake", "stat.drift" };
            Color[] cols = { Theme.Accent, Theme.Gold, Theme.Accent2, Theme.Good, new Color(0.8f, 0.4f, 1f) };
            for (int i = 0; i < 5; i++)
            {
                var row = Ui.HBox(box.transform, 10, TextAnchor.MiddleLeft, false, true);
                Ui.Size(row.transform, width, rowH);
                var l = Ui.Loc(row.transform, keys[i], 24, Theme.Dim, TextAnchor.MiddleLeft);
                Ui.Size(l.transform, 150, rowH);
                Image fill;
                var bar = Ui.Bar(row.transform, cols[i], new Color(1, 1, 1, 0.12f), out fill);
                Ui.Size(bar.transform, width - 170, 18);
                sb.fills[i] = fill;
            }
            return sb;
        }

        public class ProfileBar
        {
            public Text level, money;
            public Image xpFill;
            public RectTransform root;

            public void Refresh()
            {
                var p = Services.Get<RussianDrift.Meta.ProfileService>();
                if (p == null) return;
                level.text = p.Level.ToString();
                money.text = Ui.Money(p.Currency);
                Ui.SetBar(xpFill, p.XpToNext > 0 ? (float)p.Xp / p.XpToNext : 0f);
            }
        }

        public static ProfileBar CreateProfileBar(Transform parent)
        {
            var pb = new ProfileBar();
            var bg = Ui.Panel(parent, "ProfileBar", new Color(0.05f, 0.06f, 0.09f, 0.8f), 24);
            pb.root = bg.rectTransform;
            Ui.Place(pb.root, new Vector2(1, 1), new Vector2(-30, -26), new Vector2(760, 96), new Vector2(1, 1));
            // level badge
            var badge = Ui.Img(bg.transform, "Badge", UiSprites.Circle(), Theme.Accent);
            Ui.Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(76, 76), new Vector2(0, 0.5f));
            pb.level = Ui.Label(badge.transform, "1", 38, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(pb.level.rectTransform);
            var xpLab = Ui.Loc(bg.transform, "common.level", 22, Theme.Dim, TextAnchor.MiddleLeft);
            Ui.Place(xpLab.rectTransform, new Vector2(0, 1), new Vector2(104, -10), new Vector2(200, 30), new Vector2(0, 1));
            Image fill;
            var bar = Ui.Bar(bg.transform, Theme.Accent2, new Color(1, 1, 1, 0.15f), out fill);
            Ui.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(104, 14), new Vector2(230, 16), new Vector2(0, 0));
            pb.xpFill = fill;
            var coin = Ui.Img(bg.transform, "Coin", UiSprites.Circle(), Theme.Gold);
            Ui.Place(coin.rectTransform, new Vector2(1, 0.5f), new Vector2(-330, 0), new Vector2(46, 46), new Vector2(1, 0.5f));
            var cl = Ui.Label(coin.transform, "$", 30, new Color(0.45f, 0.3f, 0f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(cl.rectTransform);
            pb.money = Ui.Label(bg.transform, "0", 44, Theme.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
            Ui.Place(pb.money.rectTransform, new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(300, 80), new Vector2(1, 0.5f));
            pb.Refresh();
            return pb;
        }
    }
}
