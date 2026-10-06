using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Детерминированный ГСЧ (mulberry32) — те же раскладки уровней и города, что в веб-версии.</summary>
    public class Rng
    {
        uint a;
        public Rng(int seed) { a = (uint)seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5;
                uint t = a;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return ((t ^ (t >> 14)) >> 0) / 4294967296f;
            }
        }
        public int Range(int n) { return Mathf.Min(n - 1, (int)(Next() * n)); }
        public float Range(float a0, float a1) { return a0 + (a1 - a0) * Next(); }
        public T Pick<T>(T[] arr) { return arr[Range(arr.Length)]; }
    }

    public static class M
    {
        public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        public static float Smoothstep(float e0, float e1, float x)
        {
            float t = Clamp((x - e0) / (e1 - e0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
        /// <summary>Экспоненциальное сглаживание, не зависящее от FPS.</summary>
        public static float Damp(float a, float b, float k, float dt) { return Mathf.Lerp(a, b, 1f - Mathf.Exp(-k * dt)); }
        public static float WrapAngle(float a)
        {
            while (a > Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }
        public static float DampAngle(float a, float b, float k, float dt) { return a + WrapAngle(b - a) * (1f - Mathf.Exp(-k * dt)); }
        public static float Sign(float v) { return v > 0 ? 1f : v < 0 ? -1f : 0f; }
        public static float Tanh(float x)
        {
            if (x > 9f) return 1f;
            if (x < -9f) return -1f;
            float e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }
        public static Color Hex(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        }
        public static string Rub(int v) { return v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ") + " ₽"; }
        /// <summary>Курс (рад, вперёд = (sin h, cos h)) → поворот Unity.</summary>
        public static Quaternion Yaw(float h) { return Quaternion.Euler(0f, h * Mathf.Rad2Deg, 0f); }
        public static Vector3 Fwd(float h) { return new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h)); }
        /// <summary>«Вправо» для курса h в координатах Unity (+X вправо).</summary>
        public static Vector3 Right(float h) { return new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h)); }
    }
}
