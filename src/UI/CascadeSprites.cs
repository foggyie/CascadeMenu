using System.Collections.Generic;
using UnityEngine;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Runtime-generated UI sprites (no asset bundle needed). Everything is white so
    /// Unity UI can tint it with the current theme color.
    /// </summary>
    internal static class CascadeSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite White()
        {
            return Cached("white", () =>
            {
                var texture = NewTexture(8, 8);
                var pixels = new Color[64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        public static Sprite Circle()
        {
            return Cached("circle", () =>
            {
                const int size = 64;
                var texture = NewTexture(size, size);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f;
                        float py = y + 0.5f;
                        float distance = Mathf.Sqrt((px - size * 0.5f) * (px - size * 0.5f) + (py - size * 0.5f) * (py - size * 0.5f)) - (size * 0.5f - 1f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Coverage(distance));
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        public static Sprite CircleRing(float thickness = 1.5f)
        {
            return Cached("circle_ring_" + thickness.ToString("0.##"), () =>
            {
                const int size = 64;
                var texture = NewTexture(size, size);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f;
                        float py = y + 0.5f;
                        float r = Mathf.Sqrt((px - size * 0.5f) * (px - size * 0.5f) + (py - size * 0.5f) * (py - size * 0.5f));
                        float outer = r - (size * 0.5f - 1f);
                        float inner = r - (size * 0.5f - 1f - thickness);
                        float alpha = Coverage(outer) - Coverage(inner);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Max(1, radius);
            return Cached("rounded_" + radius, () =>
            {
                int size = radius * 2 + 12;
                var texture = NewTexture(size, size);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, radius, 1f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Coverage(distance));
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                float border = radius + 2f;
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            });
        }

        public static Sprite RoundedTop(int radius)
        {
            radius = Mathf.Max(1, radius);
            return Cached("rounded_top_" + radius, () =>
            {
                const int size = 64;
                int r = Mathf.Clamp(radius, 1, 20);
                var texture = NewTexture(size, size);
                var pixels = new Color[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int samples = 0;
                        for (int sy = 0; sy < 4; sy++)
                        {
                            for (int sx = 0; sx < 4; sx++)
                            {
                                float px = x + (sx + 0.5f) / 4f;
                                float py = y + (sy + 0.5f) / 4f;
                                if (InsideTopRounded(px, py, size, r)) samples++;
                            }
                        }
                        pixels[y * size + x] = new Color(1f, 1f, 1f, samples / 16f);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply(false, true);
                float border = r + 2f;
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            });
        }

        public static Sprite Pill()
        {
            return Cached("pill", () =>
            {
                const int width = 64;
                const int height = 32;
                const float radius = height * 0.5f;
                var texture = NewTexture(width, height);
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float distance = RoundedRectSdf(x + 0.5f, y + 0.5f, width, height, radius, 1f);
                        pixels[y * width + x] = new Color(1f, 1f, 1f, Coverage(distance));
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        private static bool InsideTopRounded(float x, float y, int size, int radius)
        {
            if (x < 0f || x > size || y < 0f || y > size) return false;
            if (y < size - radius) return true;
            if (x >= radius && x <= size - radius) return true;
            float centerX = x < size * 0.5f ? radius : size - radius;
            float dx = x - centerX;
            float dy = y - (size - radius);
            return dx * dx + dy * dy <= radius * radius;
        }

        public static Sprite Outline(int radius, int thickness = 1)
        {
            radius = Mathf.Max(1, radius);
            thickness = Mathf.Max(1, thickness);
            return Cached("outline_" + radius + "_" + thickness, () =>
            {
                int size = radius * 2 + 12;
                var texture = NewTexture(size, size);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f;
                        float py = y + 0.5f;
                        float outer = RoundedRectSdf(px, py, size, size, radius, 1f);
                        float inner = RoundedRectSdf(px - thickness, py - thickness, size - thickness * 2f, size - thickness * 2f, Mathf.Max(1f, radius - thickness), 1f);
                        float alpha = Coverage(outer) - Coverage(inner);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                float border = radius + 2f;
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            });
        }

        public static Sprite Shadow(int radius = 18, int feather = 20)
        {
            return Cached("shadow_" + radius + "_" + feather, () =>
            {
                int size = (radius + feather) * 2 + 24;
                var texture = NewTexture(size, size);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, radius + feather, 1f);
                        float t = Mathf.Clamp01(1f - distance / feather);
                        t = t * t * (3f - 2f * t);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, t);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                float border = radius + feather + 4f;
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            });
        }

        public static Sprite Gradient(string key, Color top, Color bottom)
        {
            return Cached("gradient_" + key, () =>
            {
                const int width = 16;
                const int height = 64;
                var texture = NewTexture(width, height);
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    Color color = Color.Lerp(bottom, top, y / (float)(height - 1));
                    for (int x = 0; x < width; x++) pixels[y * width + x] = color;
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        private static Sprite Cached(string key, System.Func<Sprite> factory)
        {
            Sprite sprite;
            if (Cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
            sprite = factory();
            sprite.name = "Cascade_" + key;
            Cache[key] = sprite;
            return sprite;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }

        private static float Coverage(float distance)
        {
            return Mathf.Clamp01(0.5f - distance);
        }

        private static float RoundedRectSdf(float x, float y, float width, float height, float radius, float inset)
        {
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float dx = Mathf.Abs(x - halfW) - (halfW - radius - inset);
            float dy = Mathf.Abs(y - halfH) - (halfH - radius - inset);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius;
        }
    }
}
