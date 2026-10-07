using System;
using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Procedural textures: everything the game needs to look decent without imported art.</summary>
    public static class ProcTex
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static float ValueNoise(float x, float y, int seed, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            int x0 = ((xi % period) + period) % period, x1 = (x0 + 1) % period;
            int y0 = ((yi % period) + period) % period, y1 = (y0 + 1) % period;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Tileable fractal noise 0..1.</summary>
        public static float Fbm(float x, float y, int seed, int basePeriod, int octaves)
        {
            float amp = 0.5f, sum = 0f, norm = 0f;
            int per = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(x * per, y * per, seed + o * 17, per);
                norm += amp;
                amp *= 0.5f; per *= 2;
            }
            return sum / norm;
        }

        private static Texture2D Make(int w, int h, Func<int, int, Color32> px, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Repeat, int aniso = 4)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, false);
            var data = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) data[y * w + x] = px(x, y);
            t.SetPixels32(data);
            t.wrapMode = wrap;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = aniso;
            t.Apply(mips, false);
            return t;
        }

        private static Color32 C(float r, float g, float b, float a = 1f)
        {
            return new Color32((byte)(Mathf.Clamp01(r) * 255), (byte)(Mathf.Clamp01(g) * 255), (byte)(Mathf.Clamp01(b) * 255), (byte)(Mathf.Clamp01(a) * 255));
        }

        public static Texture2D Get(string key, Func<Texture2D> make)
        {
            Texture2D t;
            if (cache.TryGetValue(key, out t) && t != null) return t;
            // an imported texture named Resources/Textures/<key>.(png|jpg) replaces the procedural one
            t = Resources.Load<Texture2D>("Textures/" + key);
            if (t == null) t = make();
            t.name = key;
            cache[key] = t;
            return t;
        }

        public static Texture2D Asphalt()
        {
            return Get("asphalt", () => Make(256, 256, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                float n = Fbm(u, v, 3, 4, 4);
                float grain = Hash(x, y, 9);
                float g = 0.20f + n * 0.12f + (grain - 0.5f) * 0.09f;
                if (Hash(x / 3, y / 3, 21) > 0.985f) g += 0.12f;      // aggregate stones
                return C(g, g, g * 1.03f);
            }));
        }

        public static Texture2D Concrete()
        {
            return Get("concrete", () => Make(256, 256, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                float n = Fbm(u, v, 11, 4, 4);
                float g = 0.52f + n * 0.20f + (Hash(x, y, 5) - 0.5f) * 0.05f;
                return C(g, g * 0.99f, g * 0.95f);
            }));
        }

        /// <summary>Soviet-style panel facade with window grid; atlas of 4 horizontal variants (colour tint).</summary>
        public static Texture2D Facade(int variant)
        {
            return Get("facade" + variant, () =>
            {
                Color32[] palette = {
                    C(0.72f, 0.70f, 0.64f), C(0.62f, 0.66f, 0.72f), C(0.74f, 0.62f, 0.52f), C(0.55f, 0.58f, 0.52f)
                };
                Color32 wall = palette[variant % palette.Length];
                return Make(256, 256, (x, y) =>
                {
                    // one tile = one floor (256px) with two windows
                    float u = x / 256f, v = y / 256f;
                    float n = Fbm(u, v, 31 + variant, 4, 3);
                    float wallShade = 0.82f + n * 0.25f;
                    float r = wall.r / 255f * wallShade, g = wall.g / 255f * wallShade, b = wall.b / 255f * wallShade;
                    // panel seams
                    if (x < 3 || y < 3) { r *= 0.62f; g *= 0.62f; b *= 0.62f; }
                    if (Mathf.Abs(u - 0.5f) < 0.006f) { r *= 0.75f; g *= 0.75f; b *= 0.75f; }
                    // windows
                    for (int w = 0; w < 2; w++)
                    {
                        float cx = 0.25f + w * 0.5f;
                        float dx = Mathf.Abs(u - cx), dy = Mathf.Abs(v - 0.52f);
                        if (dx < 0.115f && dy < 0.27f)
                        {
                            bool frame = dx > 0.095f || dy > 0.25f || Mathf.Abs(u - cx) < 0.008f;
                            if (frame) { r = 0.9f; g = 0.9f; b = 0.88f; }
                            else
                            {
                                float lit = Hash(w + variant * 7, 0, 77) > 0.5f ? 1f : 0f;
                                float sky = 0.12f + 0.25f * (v - 0.25f);
                                r = 0.10f + sky * 0.4f; g = 0.14f + sky * 0.55f; b = 0.20f + sky * 0.8f;
                            }
                        }
                        // balcony slab
                        if (dx < 0.13f && v > 0.18f && v < 0.215f) { r *= 0.7f; g *= 0.7f; b *= 0.7f; }
                    }
                    return C(r, g, b);
                });
            });
        }

        /// <summary>Emissive window mask for night (white where windows are). Same layout as Facade.</summary>
        public static Texture2D FacadeEmission()
        {
            return Get("facade_em", () => Make(256, 256, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                for (int w = 0; w < 2; w++)
                {
                    float cx = 0.25f + w * 0.5f;
                    float dx = Mathf.Abs(u - cx), dy = Mathf.Abs(v - 0.52f);
                    if (dx < 0.092f && dy < 0.245f && Mathf.Abs(u - cx) > 0.008f)
                    {
                        float k = (v > 0.4f) ? 1f : 0.8f;
                        return C(1.0f * k, 0.82f * k, 0.5f * k);
                    }
                }
                return C(0, 0, 0);
            }));
        }

        public static Texture2D Brick()
        {
            return Get("brick", () => Make(256, 256, (x, y) =>
            {
                int row = y / 16;
                int bx = (x + (row % 2) * 16) % 32;
                bool mortar = (y % 16) < 2 || bx < 2;
                float n = Hash(x / 32 + row * 31, row, 4);
                float r = mortar ? 0.55f : 0.50f + n * 0.14f;
                float g = mortar ? 0.53f : 0.22f + n * 0.07f;
                float b = mortar ? 0.50f : 0.16f + n * 0.05f;
                return C(r, g, b);
            }));
        }

        public static Texture2D Grass()
        {
            return Get("grass", () => Make(256, 256, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                float n = Fbm(u, v, 41, 8, 4);
                float g = Hash(x, y, 14) * 0.12f;
                return C(0.16f + n * 0.12f + g * 0.4f, 0.32f + n * 0.20f + g, 0.10f + n * 0.05f);
            }));
        }

        public static Texture2D Dirt()
        {
            return Get("dirt", () => Make(256, 256, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                float n = Fbm(u, v, 51, 6, 4);
                return C(0.36f + n * 0.2f, 0.27f + n * 0.14f, 0.18f + n * 0.09f);
            }));
        }

        public static Texture2D Metal()
        {
            return Get("metal", () => Make(128, 128, (x, y) =>
            {
                float g = 0.55f + (Hash(x, 0, 6) - 0.5f) * 0.10f + (Hash(x, y, 8) - 0.5f) * 0.04f;
                return C(g, g * 1.0f, g * 1.04f);
            }));
        }

        public static Texture2D Tire()
        {
            return Get("tire", () => Make(64, 64, (x, y) =>
            {
                float g = 0.07f + ((x % 8) < 3 ? 0.04f : 0f) + Hash(x, y, 2) * 0.02f;
                return C(g, g, g);
            }));
        }

        public static Texture2D SoftCircle()
        {
            return Get("softcircle", () => Make(64, 64, (x, y) =>
            {
                float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                float n = 0.85f + 0.15f * Hash(x, y, 1);
                return C(1f, 1f, 1f, a * n);
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static Texture2D Glow()
        {
            return Get("glow", () => Make(64, 64, (x, y) =>
            {
                float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                return C(a, a, a, a);
            }, false, TextureWrapMode.Clamp, 1));
        }

        /// <summary>Vertical gradient (bright at v=0, fading to 0 at v=1) for headlight beams.</summary>
        public static Texture2D BeamGradient()
        {
            return Get("beam", () => Make(32, 64, (x, y) =>
            {
                float v = y / 63f, u = Mathf.Abs(x / 31f - 0.5f) * 2f;
                float a = Mathf.Pow(1f - v, 1.6f) * Mathf.Pow(1f - u, 0.8f);
                return C(a, a, a, a);
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static Texture2D SkidMark()
        {
            return Get("skid", () => Make(32, 32, (x, y) =>
            {
                float u = Mathf.Abs(x / 31f - 0.5f) * 2f;
                float edge = Mathf.Clamp01((1f - u) * 3f);
                float n = 0.65f + 0.35f * Hash(x, y, 33);
                return C(0.02f, 0.02f, 0.02f, edge * n * 0.75f);
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static Texture2D Scratch()
        {
            return Get("scratch", () => Make(64, 16, (x, y) =>
            {
                float v = Mathf.Abs(y / 15f - 0.5f) * 2f;
                float a = Mathf.Clamp01(1f - v * 2.5f) * (0.5f + 0.5f * Hash(x, 0, 12));
                return C(0.08f, 0.08f, 0.08f, a);
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static Texture2D Rain()
        {
            return Get("raindrop", () => Make(8, 64, (x, y) =>
            {
                float u = Mathf.Abs(x / 7f - 0.5f) * 2f, v = y / 63f;
                float a = Mathf.Clamp01(1f - u) * Mathf.Sin(v * Mathf.PI) * 0.8f;
                return C(0.8f, 0.85f, 0.95f, a);
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static Texture2D Spark()
        {
            return Get("spark", () => Make(16, 16, (x, y) =>
            {
                float dx = (x - 7.5f) / 8f, dy = (y - 7.5f) / 8f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return C(1f, 0.8f, 0.4f, a * a);
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static Texture2D Puddle()
        {
            return Get("puddle", () => Make(64, 64, (x, y) =>
            {
                float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
                float ang = Mathf.Atan2(dy, dx);
                float wob = 0.8f + 0.18f * Mathf.Sin(ang * 3f + 1f) + 0.08f * Mathf.Sin(ang * 5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / wob;
                float a = Mathf.Clamp01((1f - d) * 6f);
                return C(0.03f, 0.04f, 0.05f, a * 0.85f);
            }, false, TextureWrapMode.Clamp, 1));
        }

        // ---------- Wraps & decals ----------

        /// <summary>
        /// Opaque paint texture for the car shell. u runs around the body cross-section starting at the roof centre (clockwise),
        /// v runs rear -> front. Not cached: the caller owns (and destroys) the texture.
        /// </summary>
        public static Texture2D WrapPaint(int variant, Color baseC, Color accent)
        {
            var t = new Texture2D(128, 128, TextureFormat.RGBA32, true, false);
            var data = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = x / 128f, v = y / 128f;
                    float d = Mathf.Min(u, 1f - u);           // 0 at roof centre, 0.5 at the underside
                    Color c = baseC;
                    switch (variant)
                    {
                        case 1: // racing stripes over roof/hood/trunk
                            if (d < 0.028f || (d > 0.05f && d < 0.075f)) c = accent;
                            break;
                        case 2: // flames licking back from the front along the flanks
                            {
                                float side = Mathf.Abs(d - 0.25f);
                                float edge = 0.55f + 0.2f * Mathf.Sin(d * 90f) * Mathf.Sin(d * 23f + 1f) + 0.1f * Mathf.Sin(d * 170f);
                                if (side < 0.14f && v > edge - (0.14f - side) * 1.6f) c = Color.Lerp(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.85f, 0.2f), Mathf.Clamp01((v - edge) * 3f));
                                break;
                            }
                        case 3: // chequered band around the nose
                            if (v > 0.9f) c = (((x / 8) + (y / 8)) % 2 == 0) ? accent : baseC;
                            break;
                        case 4: // camo
                            {
                                float n = Fbm(u, v, 91, 3, 3);
                                c = n < 0.42f ? Color.Lerp(baseC, new Color(0.25f, 0.30f, 0.16f), 0.8f) : (n < 0.56f ? new Color(0.45f, 0.42f, 0.25f) : new Color(0.14f, 0.17f, 0.10f));
                                break;
                            }
                        case 5: // carbon weave
                            {
                                bool wv = ((x / 3) + (y / 3)) % 2 == 0;
                                float g = wv ? 0.12f : 0.05f;
                                g += 0.04f * ((x % 3) / 3f);
                                c = new Color(g, g, g * 1.05f);
                                break;
                            }
                        case 6: // gradient fade to accent toward the nose
                            c = Color.Lerp(baseC, accent, Mathf.SmoothStep(0f, 1f, v));
                            break;
                    }
                    data[y * 128 + x] = (Color32)c;
                }
            t.SetPixels32(data);
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 2;
            t.Apply(true, false);
            return t;
        }

        public static Texture2D Decal(int variant)
        {
            return Get("decal" + variant, () => Make(128, 128, (x, y) =>
            {
                float u = x / 128f, v = y / 128f;
                switch (variant)
                {
                    case 1: // racing number "86"-like roundel
                        {
                            float dx = u - 0.5f, dy = v - 0.5f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < 0.45f)
                            {
                                if (d > 0.40f) return C(0.05f, 0.05f, 0.05f);
                                // "7" glyph
                                bool bar = v > 0.68f && v < 0.80f && u > 0.28f && u < 0.72f;
                                bool diag = Mathf.Abs((u - 0.72f) + (v - 0.8f) * 0.55f) < 0.07f && v < 0.8f && v > 0.2f;
                                if (bar || diag) return C(0.05f, 0.05f, 0.05f);
                                return C(1, 1, 1);
                            }
                            return C(0, 0, 0, 0);
                        }
                    case 2: // side stripe
                        return (v > 0.38f && v < 0.62f) ? C(1, 1, 1) : (v > 0.28f && v < 0.33f ? C(1, 1, 1) : C(0, 0, 0, 0));
                    case 3: // stars
                        {
                            for (int s = 0; s < 3; s++)
                            {
                                float cx = 0.2f + s * 0.3f, cy = 0.5f;
                                float dx = u - cx, dy = v - cy;
                                float ang = Mathf.Atan2(dy, dx);
                                float rad = 0.1f * (0.55f + 0.45f * Mathf.Abs(Mathf.Cos(ang * 2.5f)));
                                if (Mathf.Sqrt(dx * dx + dy * dy) < rad) return C(1, 1, 1);
                            }
                            return C(0, 0, 0, 0);
                        }
                    case 4: // door flames
                        {
                            float edge = 0.25f + 0.15f * Mathf.Sin(u * 30f) * Mathf.Sin(u * 9f);
                            return v < edge + 0.2f * (1f - u) ? C(1f, 0.5f + v * 0.6f, 0.1f) : C(0, 0, 0, 0);
                        }
                    case 5: // checker strip
                        {
                            if (v < 0.35f || v > 0.65f) return C(0, 0, 0, 0);
                            bool on = ((int)(u * 12f) + (int)(v * 12f)) % 2 == 0;
                            return on ? C(0.05f, 0.05f, 0.05f) : C(1, 1, 1);
                        }
                }
                return C(0, 0, 0, 0);
            }, true, TextureWrapMode.Clamp));
        }

        // ---------- Number plate with 5x7 bitmap font ----------

        private static readonly Dictionary<char, string> glyphs = new Dictionary<char, string>
        {
            {'0',"01110100011001110101110011000101110"},
            {'1',"00100011000010000100001000010001110"},
            {'2',"01110100010000100010001000100011111"},
            {'3',"11110000010000101110000010000111110"},
            {'4',"00010001100101010010111110001000010"},
            {'5',"11111100001111000001000011000101110"},
            {'6',"00110010001000011110100011000101110"},
            {'7',"11111000010001000100010000100001000"},
            {'8',"01110100011000101110100011000101110"},
            {'9',"01110100011000101111000010001001100"},
            {'A',"01110100011000111111100011000110001"},
            {'B',"11110100011000111110100011000111110"},
            {'E',"11111100001000011110100001000011111"},
            {'K',"10001100101010011000101001001010001"},
            {'M',"10001110111010110101100011000110001"},
            {'H',"10001100011000111111100011000110001"},
            {'O',"01110100011000110001100011000101110"},
            {'P',"11110100011000111110100001000010000"},
            {'C',"01110100011000010000100001000101110"},
            {'T',"11111001000010000100001000010000100"},
            {'Y',"10001100010101000100001000010000100"},
            {'X',"10001100010101000100010101000110001"},
        };

        public static string SanitizePlate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "A000AA";
            s = s.ToUpperInvariant();
            // map Cyrillic look-alikes to Latin plate letters
            var map = new Dictionary<char, char> { { 'А', 'A' }, { 'В', 'B' }, { 'Е', 'E' }, { 'К', 'K' }, { 'М', 'M' }, { 'Н', 'H' }, { 'О', 'O' }, { 'Р', 'P' }, { 'С', 'C' }, { 'Т', 'T' }, { 'У', 'Y' }, { 'Х', 'X' } };
            var sb = new System.Text.StringBuilder();
            foreach (char ch0 in s)
            {
                char ch = ch0;
                if (map.ContainsKey(ch)) ch = map[ch];
                if (glyphs.ContainsKey(ch)) sb.Append(ch);
            }
            string r = sb.ToString();
            if (r.Length > 6) r = r.Substring(0, 6);
            while (r.Length < 6) r += "0";
            return r;
        }

        public static bool PlateCharAllowed(char c)
        {
            return glyphs.ContainsKey(char.ToUpperInvariant(c));
        }

        /// <summary>Russian-style plate: letter 3 digits 2 letters + region box and flag. 256x56.</summary>
        public static Texture2D Plate(string text, string region)
        {
            text = SanitizePlate(text);
            region = string.IsNullOrEmpty(region) ? "77" : region;
            var t = new Texture2D(256, 56, TextureFormat.RGBA32, true, false);
            var px = new Color32[256 * 56];
            Color32 white = C(0.97f, 0.97f, 0.95f), black = C(0.05f, 0.05f, 0.06f);
            for (int y = 0; y < 56; y++)
                for (int x = 0; x < 256; x++)
                {
                    Color32 c = white;
                    if (x < 3 || x > 252 || y < 3 || y > 52) c = black;
                    px[y * 256 + x] = c;
                }
            Action<int, int, char, int> draw = (ox, oy, ch, scale) =>
            {
                string g;
                if (!glyphs.TryGetValue(ch, out g)) return;
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                    {
                        if (g[gy * 5 + gx] != '1') continue;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int X = ox + gx * scale + sx, Y = oy + (6 - gy) * scale + sy;
                                if (X >= 0 && X < 256 && Y >= 0 && Y < 56) px[Y * 256 + X] = black;
                            }
                    }
            };
            int sc = 5, step = 6 * sc + 3;
            int[] order = { 0, 1, 2, 3, 4, 5 };
            int x0 = 12;
            for (int i = 0; i < 6; i++)
            {
                draw(x0, 10, text[order[i]], sc);
                x0 += step;
            }
            // region box separator
            for (int y = 3; y < 53; y++) px[y * 256 + 188] = black;
            // region digits (smaller)
            int rx = 196;
            for (int i = 0; i < region.Length && i < 3; i++) { draw(rx, 24, region[i], 3); rx += 6 * 3 + 2; }
            // RUS flag
            for (int y = 8; y < 20; y++)
                for (int x = 214; x < 240; x++)
                {
                    int yy = y - 8;
                    px[y * 256 + x] = yy < 4 ? white : (yy < 8 ? C(0.1f, 0.2f, 0.75f) : C(0.85f, 0.1f, 0.1f));
                }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.anisoLevel = 4;
            t.Apply(true, false);
            t.name = "plate_" + text;
            return t;
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply(false, false);
            return t;
        }

        public static Texture2D RoundedRect(int w, int h, int radius, Color c)
        {
            return Get("rr_" + w + "_" + h + "_" + radius, () => Make(w, h, (x, y) =>
            {
                float dx = Mathf.Max(radius - x, 0, x - (w - 1 - radius));
                float dy = Mathf.Max(radius - y, 0, y - (h - 1 - radius));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                return C(c.r, c.g, c.b, a);
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static Texture2D Circle(int size)
        {
            return Get("circle_" + size, () => Make(size, size, (x, y) =>
            {
                float r = size * 0.5f;
                float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f));
                return C(1, 1, 1, Mathf.Clamp01(r - d));
            }, false, TextureWrapMode.Clamp, 1));
        }

        public static void ClearCache()
        {
            foreach (var kv in cache) if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            cache.Clear();
        }
    }
}
