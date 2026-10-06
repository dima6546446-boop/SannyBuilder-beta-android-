using UnityEngine;
using UnityEngine.UI;

namespace CaucasusDrive
{
    /// <summary>Экраны онлайна: главная, поиск комнат в Wi-Fi, ввод IP цифровой клавиатурой.</summary>
    public partial class Menus
    {
        NetDiscovery disc;
        string lanSig = "";
        string ipText = "";
        static readonly string[] NickA = { "Лихой", "Тихий", "Быстрый", "Дерзкий", "Весёлый", "Ночной", "Кавказский", "Дрифтовый", "Железный", "Рыжий" };
        static readonly string[] NickB = { "Жигуль", "Бомбила", "Гонщик", "Дальнобой", "Копейка", "Нивовод", "Джигит", "Шумахер", "Орёл", "Мастер" };

        string Nick()
        {
            var st = app.save.d.settings;
            if (string.IsNullOrEmpty(st.nick)) { st.nick = NickA[Random.Range(0, NickA.Length)] + NickB[Random.Range(0, NickB.Length)]; app.save.Commit(); }
            return st.nick;
        }

        void OnlineScreen()
        {
            StopLan();
            var s = Screen(true);
            TopBar(s, "Онлайн", "main");
            var nick = Nick();
            var m = UIKit.Panel(s, "Nick", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(760, 76), UIKit.Bg);
            UIKit.LabelAt(m.transform, "Твой ник:  <color=#ffffff>" + nick + "</color>", 28, Color.white, new Vector2(0, 0), new Vector2(1, 1), new Vector2(-110, 0), new Vector2(-260, 0), TextAnchor.MiddleLeft).supportRichText = true;
            UIKit.Button(m.transform, "ДРУГОЙ", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(190, 54), UIKit.Bg2, () =>
            {
                app.save.d.settings.nick = NickA[Random.Range(0, NickA.Length)] + NickB[Random.Range(0, NickB.Length)]; app.save.Commit(); Show("online");
            }, 20);
            string[] titles = { "СОЗДАТЬ КОМНАТУ", "НАЙТИ В WI-FI", "ПО IP-АДРЕСУ", "ЧЕРЕЗ ИНТЕРНЕТ" };
            string[] subs = { "Ты хост: друзья заходят к тебе", "Друзья в той же сети · авто-поиск", "Свой сервер или адрес хоста", "Сервер Cloudflare · по коду комнаты" };
            System.Action[] acts = { () => app.StartOnline(true, ""), () => Show("lan"), () => { ipText = app.save.d.settings.lastIp ?? ""; Show("ip"); }, () => Show("cf") };
            Color[] cols = { UIKit.Light, UIKit.Bg2, UIKit.Bg2, UIKit.Bg2 };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = UIKit.Button(s, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-382 + i * 255, 10), new Vector2(245, 130), cols[i], acts[k]);
                UIKit.IconAt(b.transform, i == 0 ? "play" : i == 3 ? "speedo" : "people", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(38, 38), i == 0 ? UIKit.Ink : new Color(1, 1, 1, 0.9f));
                UIKit.LabelAt(b.transform, titles[i], 22, i == 0 ? UIKit.Ink : Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 84), new Vector2(-20, 36), TextAnchor.MiddleLeft);
                UIKit.LabelAt(b.transform, subs[i], 14, i == 0 ? new Color(0, 0, 0, 0.65f) : new Color(1, 1, 1, 0.78f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 42), new Vector2(-20, 44), TextAnchor.MiddleLeft, false);
            }
            UIKit.LabelAt(s, "Как играть вместе: один включает «Точку доступа» (хотспот) на телефоне или все подключаются к одному Wi-Fi,\nпотом один жмёт «Создать комнату», остальные — «Найти в Wi-Fi». До " + NetP.Max + " игроков. В игре есть фразы (кнопка ЧАТ),\nобщая погода и время суток и заезды на время (кнопка ГОНКА). Через интернет — свой сервер: Server/relay.js (см. README).", 17, UIKit.Muted, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(1100, 100), TextAnchor.MiddleCenter, false);
        }

        // ------------------------------------------------------------------ поиск комнат
        void StopLan() { if (disc != null) { disc.Stop(); disc = null; } lanSig = ""; }

        void LanScreen()
        {
            if (disc == null) { disc = new NetDiscovery(); try { disc.Start(); } catch (System.Exception) { disc = null; } }
            lanSig = "?";
            var s = Screen(true);
            TopBar(s, "Комнаты в Wi-Fi", "online");
            UIKit.LabelAt(s, disc == null ? "Не удалось открыть сеть" : "Ищу комнаты в сети…", 20, UIKit.Muted, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(800, 30), TextAnchor.MiddleCenter, false).name = "Hint";
            int i = 0;
            if (disc != null)
                foreach (var r in disc.rooms)
                {
                    var room = r;
                    var row = UIKit.Panel(s, "Room", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190 - i * 84), new Vector2(820, 74), UIKit.Bg);
                    UIKit.LabelAt(row.transform, room.name, 26, Color.white, new Vector2(0, 0), new Vector2(1, 1), new Vector2(-120, 12), new Vector2(-300, -26), TextAnchor.MiddleLeft);
                    UIKit.LabelAt(row.transform, room.ep.Address + " · игроков " + room.players + "/" + room.max, 15, UIKit.Muted, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-120, 16), new Vector2(-300, 26), TextAnchor.MiddleLeft, false);
                    UIKit.Button(row.transform, room.players >= room.max ? "ПОЛНАЯ" : "ВОЙТИ", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(170, 56), room.players >= room.max ? UIKit.Bg2 : UIKit.Green, () => { if (room.players < room.max) { string a = room.ep.Address + ":" + room.ep.Port; StopLan(); app.StartOnline(false, a); } }, 20);
                    if (++i >= 6) break;
                }
        }

        /// <summary>Вызывается каждый кадр в меню: обновляет список найденных комнат.</summary>
        public void Tick(float dt)
        {
            PollKb();
            if (disc == null) return;
            if (current != "lan") { StopLan(); return; }
            disc.Update(dt);
            string sig = "";
            foreach (var r in disc.rooms) sig += r.ep + r.name + r.players + ";";
            if (sig != lanSig) { lanSig = sig; LanScreenRefresh(); }
        }

        void LanScreenRefresh() { string keep = lanSig; LanScreen(); lanSig = keep; }

        // ------------------------------------------------------------------ сервер Cloudflare
        TouchScreenKeyboard kb; System.Action<string> kbSet; string kbLast = "";

        void OpenKb(string initial, TouchScreenKeyboardType type, System.Action<string> set)
        {
            if (!TouchScreenKeyboard.isSupported) { Toast("Клавиатура недоступна — используй «Вставить»", HUD.Bad); return; }
            kb = TouchScreenKeyboard.Open(initial ?? "", type, false, false, false, false, "");
            kbSet = set; kbLast = initial ?? "";
        }

        /// <summary>https://x.workers.dev/ABCD → wss://x.workers.dev ; пробелы и перевод строки убираем.</summary>
        static string CleanUrl(string raw)
        {
            string t = (raw ?? "").Trim();
            if (t.Length == 0) return "";
            if (t.StartsWith("https://")) t = "wss://" + t.Substring(8);
            else if (t.StartsWith("http://")) t = "ws://" + t.Substring(7);
            else if (!t.StartsWith("wss://") && !t.StartsWith("ws://")) t = "wss://" + t;
            int q = t.IndexOf('?'); if (q > 0) t = t.Substring(0, q);
            int sl = t.IndexOf('/', t.IndexOf("://") + 3); if (sl > 0) t = t.Substring(0, sl);
            return t.Replace(" ", "");
        }

        static string NewCode()
        {
            const string A = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var c = new char[4]; for (int i = 0; i < 4; i++) c[i] = A[Random.Range(0, A.Length)];
            return new string(c);
        }

        void CloudScreen()
        {
            StopLan();
            var st = app.save.d.settings;
            if (string.IsNullOrEmpty(st.cfRoom)) { st.cfRoom = NewCode(); app.save.Commit(); }
            var s = Screen(true);
            TopBar(s, "Через интернет · Cloudflare", "online");
            UIKit.LabelAt(s, "Адрес сервера (его выдаёт Cloudflare после деплоя, см. Server/cloudflare в README):", 18, UIKit.Muted, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(1000, 28), TextAnchor.MiddleCenter, false);
            var url = UIKit.Panel(s, "Url", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-150, -170), new Vector2(640, 62), UIKit.Bg);
            var ut = UIKit.Label(url.transform, string.IsNullOrEmpty(st.cfUrl) ? "wss://caucasus-drive.имя.workers.dev" : st.cfUrl, 21, string.IsNullOrEmpty(st.cfUrl) ? UIKit.Muted : Color.white);
            UIKit.Button(s, "ВСТАВИТЬ", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(270, -170), new Vector2(170, 62), UIKit.Light, () => { var u = CleanUrl(GUIUtility.systemCopyBuffer); if (u.Length < 8) { Toast("В буфере нет адреса сервера", HUD.Bad); return; } st.cfUrl = u; app.save.Commit(); Show("cf"); }, 19);
            UIKit.Button(s, "ВВЕСТИ", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(460, -170), new Vector2(170, 62), UIKit.Bg2, () => OpenKb(st.cfUrl, TouchScreenKeyboardType.URL, v => { st.cfUrl = CleanUrl(v); ut.text = st.cfUrl.Length == 0 ? "wss://…" : st.cfUrl; }), 19);
            UIKit.LabelAt(s, "Код комнаты (у всех друзей одинаковый):", 18, UIKit.Muted, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(1000, 28), TextAnchor.MiddleCenter, false);
            var code = UIKit.Panel(s, "Code", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-150, -320), new Vector2(300, 74), UIKit.Bg);
            var ct = UIKit.Label(code.transform, st.cfRoom, 44, Color.white);
            UIKit.Button(s, "НОВЫЙ КОД", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(110, -320), new Vector2(210, 74), UIKit.Bg2, () => { st.cfRoom = NewCode(); app.save.Commit(); ct.text = st.cfRoom; }, 19);
            UIKit.Button(s, "ВВЕСТИ", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(340, -320), new Vector2(190, 74), UIKit.Bg2, () => OpenKb(st.cfRoom, TouchScreenKeyboardType.ASCIICapable, v => { var c = new System.Text.StringBuilder(); foreach (char ch in v.ToUpper()) if (char.IsLetterOrDigit(ch) && ch < 128 && c.Length < 12) c.Append(ch); st.cfRoom = c.ToString(); ct.text = st.cfRoom; }), 19);
            UIKit.Button(s, "ВОЙТИ В КОМНАТУ", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(460, 80), UIKit.Light, () =>
            {
                if (string.IsNullOrEmpty(st.cfUrl) || string.IsNullOrEmpty(st.cfRoom)) { Toast("Укажи адрес сервера и код комнаты", HUD.Bad); return; }
                app.save.Commit();
                app.StartOnline(false, st.cfUrl + "/" + st.cfRoom);
            }, 26);
            UIKit.LabelAt(s, "Сервер бесплатный: Cloudflare Workers. Один код — одна комната до 8 игроков. Друзья вводят тот же адрес и код.", 16, UIKit.Muted, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(1000, 30), TextAnchor.MiddleCenter, false);
        }

        void PollKb()
        {
            if (kb == null) return;
            if (kb.text != kbLast) { kbLast = kb.text; kbSet?.Invoke(kbLast); }
            if (kb.status != TouchScreenKeyboard.Status.Visible) { var done = kb; kb = null; if (done.status == TouchScreenKeyboard.Status.Done) { app.save.Commit(); Show("cf"); } }
        }

        // ------------------------------------------------------------------ ввод IP
        void IpScreen()
        {
            StopLan();
            var s = Screen(true);
            TopBar(s, "Подключиться по IP", "online");
            var disp = UIKit.Panel(s, "Display", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(560, 74), UIKit.Bg);
            var t = UIKit.Label(disp.transform, ipText.Length == 0 ? "192.168.0.10" : ipText, 38, ipText.Length == 0 ? UIKit.Muted : Color.white);
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", ":" };
            for (int i = 0; i < 12; i++)
            {
                string k = keys[i];
                UIKit.Button(s, k, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110 + (i % 3) * 110, -250 - (i / 3) * 78), new Vector2(100, 68), UIKit.Bg2, () => { if (ipText.Length < 21) { ipText += k; t.text = ipText; t.color = Color.white; } }, 30);
            }
            UIKit.Button(s, "⌫", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(250, -250), new Vector2(120, 68), UIKit.Bg2, () => { if (ipText.Length > 0) { ipText = ipText.Substring(0, ipText.Length - 1); t.text = ipText.Length == 0 ? "192.168.0.10" : ipText; t.color = ipText.Length == 0 ? UIKit.Muted : Color.white; } }, 30);
            UIKit.Button(s, "ВОЙТИ", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(250, -332), new Vector2(120, 146), UIKit.Green, () => { if (ipText.Length >= 7) app.StartOnline(false, ipText); else Toast("Введи адрес, например 192.168.0.10", HUD.Bad); }, 22);
            UIKit.LabelAt(s, "Порт по умолчанию " + NetP.Port + ". Другой порт — через двоеточие: 203.0.113.5:7777", 17, UIKit.Muted, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(900, 30), TextAnchor.MiddleCenter, false);
        }
    }
}
