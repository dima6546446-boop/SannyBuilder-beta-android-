using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Уровень игрока: опыт считается из сохранённой статистики (км, такси, гонки, дрифт, достижения, звёзды…), за новый уровень — деньги.</summary>
    public static class Progress
    {
        static readonly int[] TitleFrom = { 1, 3, 5, 8, 12, 16, 20, 26 };
        static readonly string[] Titles = { "Новичок", "Ученик автошколы", "Бомбила", "Гонщик", "Дрифтер", "Мастер руля", "Легенда района", "Король Кавказа" };

        public static float Xp(SaveState d)
        {
            float stars = 0; foreach (var v in d.stars) stars += v;
            return d.stats.km * 12f + d.stats.walk * 0.01f + d.stats.taxi * 60f + d.race.runs * 250f
                 + d.drift.best * 0.02f + d.drift.bestSeries * 0.02f + d.ach.Count * 200f + d.stats.escapes * 300f
                 + d.stats.online * 150f + stars * 30f + (d.license ? 500f : 0f) + d.stats.food * 5f;
        }

        public static float XpFor(int level) { return 100f * (level - 1) * (level - 1); }
        public static int Level(SaveState d) { return Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(Xp(d) / 100f)) + 1); }

        public static string Title(int level)
        {
            int k = 0;
            for (int i = 0; i < TitleFrom.Length; i++) if (level >= TitleFrom[i]) k = i;
            return Titles[k];
        }

        /// <summary>0..1 до следующего уровня.</summary>
        public static float Fraction(SaveState d)
        {
            int l = Level(d); float a = XpFor(l), b = XpFor(l + 1);
            return Mathf.Clamp01((Xp(d) - a) / (b - a));
        }

        public static void Check(App app)
        {
            var d = app.save.d;
            int l = Level(d);
            if (d.level <= 0) { d.level = l; app.save.Commit(); return; }       // старые сохранения: без «наград за прошлое»
            if (l <= d.level) return;
            int reward = 0;
            for (int i = d.level + 1; i <= l; i++) reward += i * 500;
            d.level = l;
            app.save.AddMoney(reward);
            app.Notify("Новый уровень " + l + " · «" + Title(l) + "» · +" + M.Rub(reward), HUD.Good, 4f);
            app.audio.Success();
        }
    }
}
