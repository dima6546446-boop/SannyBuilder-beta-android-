using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Состояние машины: повреждения от ударов (больше 40% — из-под капота валит пар, мотор слабеет) и грязь
    /// (копится с пробегом, быстрее в дождь и на траве; кузов тускнеет и буреет). Ремонт и мойка — на АЗС и в гараже.
    /// Хранится отдельно для каждой машины (CarTune.damage / dirt).
    /// </summary>
    public static class CarCare
    {
        public const int WashPrice = 300;
        public const float MaxDamage = 0.95f;   // до конца не ломается — всегда можно доехать до сервиса

        public static int RepairPrice(CarTune t) { return t.damage < 0.01f ? 0 : Mathf.Max(200, Mathf.CeilToInt(t.damage * 9000f / 50f) * 50); }

        /// <summary>Множитель газа: до 40% повреждений мотор как новый, дальше слабеет до 45%.</summary>
        public static float Power(CarTune t) { return t == null || t.damage < 0.4f ? 1f : 1f - 0.55f * Mathf.Clamp01((t.damage - 0.4f) / 0.55f); }

        /// <summary>Цвет и блеск краски с учётом грязи.</summary>
        public static void ApplyLook(CarMaterials m, CarTune t, int color)
        {
            if (m == null || t == null) return;
            var c = Color.Lerp(M.Hex(color), M.Hex(0x6b5a45), t.dirt * 0.55f);
            m.paint.Col(c).Pbr(0.55f * (1f - t.dirt * 0.8f), Mathf.Lerp(0.9f, 0.25f, t.dirt));
        }

        public static void Damage(App app, float strength)
        {
            var t = app.player.tune;
            if (t == null || strength < 0.1f) return;
            float before = t.damage;
            t.damage = Mathf.Min(MaxDamage, t.damage + strength * 0.12f);
            if (before < 0.4f && t.damage >= 0.4f) app.hud.Toast("Из-под капота пар! Машина слабеет — ремонт на АЗС или в гараже", HUD.Bad, 3.5f);
            if (before < 0.8f && t.damage >= 0.8f) app.hud.Toast("Машина еле едет. Срочно в ремонт!", HUD.Bad, 3f);
        }

        public static bool Repair(App app, CarTune t)
        {
            int price = RepairPrice(t);
            if (price == 0) return false;
            if (!app.save.Spend(price)) { app.Notify("Не хватает денег на ремонт", HUD.Bad); return false; }
            t.damage = 0; app.save.Commit();
            app.audio.Coin();
            return true;
        }

        public static bool Wash(App app, CarTune t)
        {
            if (t.dirt < 0.02f) return false;
            if (!app.save.Spend(WashPrice)) { app.Notify("Не хватает денег на мойку", HUD.Bad); return false; }
            t.dirt = 0; app.save.d.stats.washes++; app.save.Commit();
            app.audio.Coin();
            return true;
        }
    }

    /// <summary>Достижения: проверяются по статистике сохранения, награда начисляется сразу.</summary>
    public static class Achievements
    {
        public class A { public string id, title, desc; public System.Func<SaveState, float> value; public float goal; public int reward; }

        static float Stars(SaveState d) { int s = 0; foreach (var v in d.stars) s += v; return s; }

        public static readonly A[] All = {
            new A { id = "km10", title = "Первые километры", desc = "Проехать 10 км", value = d => d.stats.km, goal = 10, reward = 1000 },
            new A { id = "km100", title = "Дальнобойщик", desc = "Проехать 100 км", value = d => d.stats.km, goal = 100, reward = 5000 },
            new A { id = "taxi10", title = "Бомбила", desc = "Выполнить 10 заказов такси", value = d => d.stats.taxi, goal = 10, reward = 3000 },
            new A { id = "taxi50", title = "Таксопарк", desc = "Выполнить 50 заказов такси", value = d => d.stats.taxi, goal = 50, reward = 10000 },
            new A { id = "combo2k", title = "Боком по жизни", desc = "Серия дрифта или «шашек» на 2 000 очков", value = d => d.stats.bestCombo, goal = 2000, reward = 2000 },
            new A { id = "drift10k", title = "Король заноса", desc = "10 000 очков за заезд в дрифт-зоне", value = d => d.drift.best, goal = 10000, reward = 5000 },
            new A { id = "license", title = "С правами", desc = "Сдать экзамен ГИБДД", value = d => d.license ? 1 : 0, goal = 1, reward = 2000 },
            new A { id = "stars60", title = "Отличник ДОСААФ", desc = "Набрать 60 звёзд в парковке", value = Stars, goal = 60, reward = 5000 },
            new A { id = "cars5", title = "Коллекционер", desc = "Собрать 5 машин", value = d => d.owned.Count, goal = 5, reward = 5000 },
            new A { id = "cars10", title = "Весь АвтоВАЗ", desc = "Собрать все 10 машин", value = d => d.owned.Count, goal = 10, reward = 20000 },
            new A { id = "food10", title = "Гурман", desc = "Купить еду 10 раз", value = d => d.stats.food, goal = 10, reward = 1000 },
            new A { id = "walk2k", title = "Пешеход", desc = "Пройти пешком 2 км", value = d => d.stats.walk, goal = 2000, reward = 1500 },
            new A { id = "wash5", title = "Чистюля", desc = "Помыть машину 5 раз", value = d => d.stats.washes, goal = 5, reward = 1000 },
            new A { id = "crash100", title = "Каскадёр", desc = "Попасть в 100 аварий", value = d => d.stats.crashes, goal = 100, reward = 1000 },
            new A { id = "race1", title = "На старт!", desc = "Финишировать в кольцевой гонке", value = d => d.race.runs, goal = 1, reward = 1500 },
            new A { id = "race10", title = "Гонщик", desc = "Финишировать в 10 гонках", value = d => d.race.runs, goal = 10, reward = 6000 },
            new A { id = "online3", title = "Компания", desc = "Сыграть онлайн с друзьями 3 раза", value = d => d.stats.online, goal = 3, reward = 3000 },
            new A { id = "escape3", title = "Неуловимый", desc = "Уйти от погони ДПС 3 раза", value = d => d.stats.escapes, goal = 3, reward = 4000 },
            new A { id = "fines20k", title = "Злостный нарушитель", desc = "Заплатить 20 000 ₽ штрафов", value = d => d.stats.fines, goal = 20000, reward = 500 },
        };

        public static bool Has(SaveState d, string id) { return d.ach.Contains(id); }
        public static int Count(SaveState d) { int n = 0; foreach (var a in All) if (d.ach.Contains(a.id)) n++; return n; }

        /// <summary>Проверить и выдать новые достижения (раз в пару секунд и после событий).</summary>
        public static void Check(App app)
        {
            var d = app.save.d;
            foreach (var a in All)
            {
                if (d.ach.Contains(a.id) || a.value(d) < a.goal) continue;
                d.ach.Add(a.id);
                app.save.AddMoney(a.reward);
                string text = "Достижение «" + a.title + "» · +" + M.Rub(a.reward);
                app.Notify(text, HUD.Good, 3.5f);
                app.audio.Success();
            }
        }
    }
}
