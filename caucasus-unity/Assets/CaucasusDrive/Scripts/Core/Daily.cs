using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Задания дня (порт Daily.js): каждый день — 3 случайных задания из пула (одинаковые весь день), прогресс
    /// копится в сохранении, награду забирают в меню «Задания». max — засчитывается лучший результат, иначе сумма.
    /// </summary>
    public class Daily
    {
        class Kind { public string kind; public int[] goals; public bool max; public System.Func<int, string> text; public System.Func<int, int> reward; }
        static readonly Kind[] Pool = {
            new Kind { kind = "km", goals = new[] { 3, 5, 8 }, text = n => "Проехать " + n + " км", reward = n => n * 450 },
            new Kind { kind = "taxi", goals = new[] { 1, 2, 3 }, text = n => "Выполнить " + n + (n == 1 ? " заказ" : " заказа") + " такси", reward = n => n * 1300 },
            new Kind { kind = "drift", goals = new[] { 400, 900, 1600 }, max = true, text = n => "Набрать " + n + " очков дрифта или «шашек» за одну серию", reward = n => 800 + n * 2 },
            new Kind { kind = "food", goals = new[] { 1, 2 }, text = n => n == 1 ? "Перекусить в ларьке или кафе" : "Купить еду " + n + " раза", reward = n => 600 * n },
            new Kind { kind = "park", goals = new[] { 1, 2, 3 }, text = n => "Пройти " + n + (n == 1 ? " уровень" : " уровня") + " парковки", reward = n => n * 1100 },
            new Kind { kind = "whistle", goals = new[] { 2, 3 }, text = n => "Свистнуть прохожим " + n + " раза", reward = n => 900 },
            new Kind { kind = "fuel", goals = new[] { 1 }, text = n => "Заправиться на АЗС", reward = n => 700 },
            new Kind { kind = "speed", goals = new[] { 110, 130, 150 }, max = true, text = n => "Разогнаться до " + n + " км/ч", reward = n => n * 12 },
            new Kind { kind = "walk", goals = new[] { 200, 400 }, text = n => "Пройти пешком " + n + " м", reward = n => n * 3 },
        };

        readonly SaveData save;
        public System.Action<DailyTask, string, int> onDone;
        float savedAt;

        public Daily(SaveData s) { save = s; Refresh(); }

        static Kind Find(string k) { foreach (var p in Pool) if (p.kind == k) return p; return Pool[0]; }

        public void Refresh()
        {
            var d = save.d.daily;
            var now = System.DateTime.Now;
            string date = now.Year + "-" + now.Month + "-" + now.Day;
            if (d.date == date && d.tasks.Count > 0) return;
            int seed = 0;
            unchecked { foreach (char ch in date) seed = seed * 31 + ch; }
            var r = new Rng(seed);
            var pool = new System.Collections.Generic.List<Kind>(Pool);
            d.tasks.Clear();
            while (d.tasks.Count < 3 && pool.Count > 0)
            {
                var k = pool[r.Range(pool.Count)]; pool.Remove(k);
                d.tasks.Add(new DailyTask { kind = k.kind, goal = k.goals[r.Range(k.goals.Length)] });
            }
            d.date = date;
            save.Commit();
        }

        public string Text(DailyTask t) { return Find(t.kind).text(t.goal); }
        public int Reward(DailyTask t) { return Mathf.RoundToInt(Find(t.kind).reward(t.goal) / 50f) * 50; }
        public System.Collections.Generic.List<DailyTask> Tasks => save.d.daily.tasks;

        public void Progress(string kind, float value = 1f)
        {
            bool changed = false, finished = false;
            foreach (var t in Tasks)
            {
                if (t.kind != kind || t.done) continue;
                float v = Find(kind).max ? Mathf.Max(t.progress, value) : t.progress + value;
                if (v == t.progress) continue;
                t.progress = Mathf.Min(t.goal, v);
                changed = true;
                if (t.progress >= t.goal) { t.done = true; finished = true; onDone?.Invoke(t, Text(t), Reward(t)); }
            }
            // километры/метры копятся каждый кадр — сохраняем не чаще раза в 3 с
            if (changed && (finished || (kind != "km" && kind != "walk") || Time.unscaledTime - savedAt > 3f)) { savedAt = Time.unscaledTime; save.Commit(); }
        }

        public int Claim(int i)
        {
            if (i < 0 || i >= Tasks.Count) return 0;
            var t = Tasks[i];
            if (!t.done || t.claimed) return 0;
            t.claimed = true;
            int r = Reward(t);
            save.AddMoney(r);
            return r;
        }

        public int ReadyCount { get { int n = 0; foreach (var t in Tasks) if (t.done && !t.claimed) n++; return n; } }
    }
}
