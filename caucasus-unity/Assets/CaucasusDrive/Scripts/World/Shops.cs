using System.Collections.Generic;
using UnityEngine;

namespace CaucasusDrive
{
    public class ShopItem { public string name; public int price, bites; public bool drink; }

    /// <summary>
    /// Магазины «для реализма» (порт Shops.js): подходишь пешком к ларьку или кафе, покупаешь еду, персонаж ест/пьёт.
    /// Никакого голода — только деньги и анимация. Точки продаж строит City (ларьки у остановок, кафе при АЗС).
    /// </summary>
    public static class Shops
    {
        public static readonly Dictionary<string, ShopItem> Items = new Dictionary<string, ShopItem> {
            { "sandwich", new ShopItem { name = "Сэндвич с ветчиной", price = 150, bites = 4 } },
            { "cola", new ShopItem { name = "Кола 0,5 л", price = 90, bites = 3, drink = true } },
            { "shawarma", new ShopItem { name = "Шаурма", price = 230, bites = 5 } },
            { "chips", new ShopItem { name = "Чипсы «Хрустим»", price = 110, bites = 4 } },
            { "seeds", new ShopItem { name = "Семечки «От Мартина»", price = 60, bites = 5 } },
            { "coffee", new ShopItem { name = "Кофе 3 в 1", price = 120, bites = 3, drink = true } },
            { "icecream", new ShopItem { name = "Пломбир в стаканчике", price = 70, bites = 4 } },
            { "kvass", new ShopItem { name = "Квас «Очаковский»", price = 80, bites = 3, drink = true } },
            { "pie", new ShopItem { name = "Пирожок с капустой", price = 50, bites = 3 } },
        };

        public static readonly Dictionary<string, string[]> Menus = new Dictionary<string, string[]> {
            { "kiosk", new[] { "shawarma", "sandwich", "cola", "chips", "seeds", "kvass" } },
            { "shawarma", new[] { "shawarma", "cola", "kvass", "coffee" } },
            { "grocery", new[] { "sandwich", "cola", "chips", "seeds", "icecream", "kvass" } },
            { "bakery", new[] { "pie", "sandwich", "kvass", "coffee" } },
            { "cafe", new[] { "coffee", "sandwich", "pie", "icecream", "cola" } },
        };

        static readonly string[] Thanks = { "Приходи ещё, брат!", "Спасибо за покупку!", "Сдачи нет, извини", "На здоровье!", "Хорошего дня!" };
        static readonly string[] YumT = { "Вкусно!", "Самое то!", "Жить стало лучше", "Кайф", "Ещё бы одну…" };
        public static string Yum() { return YumT[Random.Range(0, YumT.Length)]; }
        public static string Thank() { return Thanks[Random.Range(0, Thanks.Length)]; }

        /// <summary>Ближайшая точка продаж к пешеходу (≤ 2.3 м) или null.</summary>
        public static ShopPoint Near(City city, Vector3 p)
        {
            foreach (var s in city.shops)
                if (Vector2.Distance(new Vector2(s.pos.x, s.pos.z), new Vector2(p.x, p.z)) < 2.3f) return s;
            return null;
        }

        /// <summary>Еда/напиток в руке: маленький меш из примитивов (ось Y — «вверх» предмета).</summary>
        public static GameObject ItemMesh(string kind, Transform parent)
        {
            var P = new PaletteMesh();
            System.Action<float, float, float, float> at = (x, y, z, rx) => { P.xf = Matrix4x4.TRS(new Vector3(-x, y, z), PaletteMesh.Q3(rx, 0, 0), Vector3.one); };
            switch (kind)
            {
                case "sandwich":
                    at(0, 0.026f, 0, 0); P.Box(Vector3.zero, new Vector3(0.11f, 0.022f, 0.09f), 0xe8c27a);
                    at(0, 0.011f, 0, 0); P.Box(Vector3.zero, new Vector3(0.115f, 0.012f, 0.095f), 0xd06a6a);
                    at(0, 0.002f, 0, 0); P.Box(Vector3.zero, new Vector3(0.118f, 0.006f, 0.098f), 0x6fbf4a);
                    at(0, -0.014f, 0, 0); P.Box(Vector3.zero, new Vector3(0.11f, 0.022f, 0.09f), 0xe8c27a);
                    break;
                case "cola":
                    at(0, 0, 0, 0); P.Cyl(Vector3.zero, 0.033f, 0.033f, 0.115f, 0xc4161c, 12);
                    at(0, 0.063f, 0, 0); P.Cyl(Vector3.zero, 0.03f, 0.033f, 0.012f, 0xc8c8c8, 12);
                    at(0, 0.01f, 0.03f, 0); P.Box(Vector3.zero, new Vector3(0.068f, 0.02f, 0.01f), 0xf2f2f2);
                    break;
                case "shawarma":
                    at(0, -0.03f, 0, 0); P.Cyl(Vector3.zero, 0.036f, 0.032f, 0.11f, 0xf4f2ea, 10);
                    at(0, 0.06f, 0, 0); P.Cyl(Vector3.zero, 0.034f, 0.036f, 0.08f, 0xd9b277, 10);
                    at(0, 0.1f, 0, 0); P.Sphere(Vector3.zero, 0.034f, 0xb86b3c, 10, 6, null, true);
                    break;
                case "chips":
                    at(0, 0, 0, 0); P.Box(Vector3.zero, new Vector3(0.12f, 0.16f, 0.035f), 0xf2c21a);
                    at(0, 0.01f, 0, 0); P.Box(Vector3.zero, new Vector3(0.08f, 0.05f, 0.037f), 0xd2381a);
                    break;
                case "seeds":
                    at(0, 0, 0, 0); P.Box(Vector3.zero, new Vector3(0.07f, 0.1f, 0.025f), 0x1a1a1a);
                    at(0, 0.005f, 0, Mathf.PI / 2); P.Cyl(Vector3.zero, 0.022f, 0.022f, 0.027f, 0xf2c21a, 10);
                    break;
                case "coffee":
                    at(0, 0, 0, 0); P.Cyl(Vector3.zero, 0.036f, 0.027f, 0.1f, 0xf2f2f2, 12);
                    at(0, 0.055f, 0, 0); P.Cyl(Vector3.zero, 0.038f, 0.038f, 0.012f, 0x5a3a24, 12);
                    at(0, -0.005f, 0, 0); P.Cyl(Vector3.zero, 0.037f, 0.03f, 0.03f, 0x8a5a34, 12);
                    break;
                case "icecream":
                    at(0, 0, 0, 0); P.Cyl(Vector3.zero, 0.032f, 0.026f, 0.07f, 0xe0b36a, 12);
                    at(0, 0.045f, 0, 0); P.Sphere(Vector3.zero, 0.034f, 0xfbf6ea, 10, 8);
                    break;
                case "kvass":
                    at(0, 0, 0, 0); P.Cyl(Vector3.zero, 0.03f, 0.032f, 0.17f, 0x5a2a10, 10);
                    at(0, 0.11f, 0, 0); P.Cyl(Vector3.zero, 0.012f, 0.02f, 0.05f, 0x5a2a10, 8);
                    at(0, 0, 0.028f, 0); P.Box(Vector3.zero, new Vector3(0.064f, 0.06f, 0.01f), 0xe8d6a0);
                    break;
                default:
                    at(0, 0, 0, 0); P.Sphere(Vector3.zero, 0.045f, 0xd99a4a, 10, 8, new Vector3(1.3f, 0.55f, 0.8f));
                    break;
            }
            return P.ToObject("Food_" + kind, parent);
        }
    }
}
