using System;
using System.Collections.Generic;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;

namespace CascadeMenu.UI
{
    internal enum CascadeIcon
    {
        Home,
        Sliders,
        Eye,
        Person,
        Gear,
        Info,
        Layers,
        Search,
        ChevronLeft,
        ChevronRight,
        ChevronDown,
        Plus,
        Minus,
        Close,
        Check,
        Sidebar,
        Recenter,
        Sparkle,
        Speaker,
        Bolt,
        Gamepad,
        Wrench,
        Bell,
        Terminal,
        Bag,
        Palette,
        Keyboard,
        ArrowUp,
        Backspace,
        Chart,
        Cube,
        Folder,
        Monitor,
        Windows,
        Move,
        Pin,
        Play,
        Pause,
        PrevTrack,
        NextTrack,
        Music,
        Cursor,
        Refresh,
        Lock,
        Shield,
        Star
    }

    /// <summary>
    /// Procedural SF-Symbol-ish icons. Keeps the menu asset-free and crisp at any size.
    /// </summary>
    internal static class CascadeIcons
    {
        private static readonly Dictionary<CascadeIcon, Sprite> Cache = new Dictionary<CascadeIcon, Sprite>();

        public static Sprite Get(CascadeIcon icon)
        {
            Sprite sprite;
            if (Cache.TryGetValue(icon, out sprite) && sprite != null) return sprite;
            sprite = Build(icon);
            Cache[icon] = sprite;
            return sprite;
        }

        private static Sprite Build(CascadeIcon icon)
        {
            const int size = 64;
            Func<float, float, float> sdf = ShapeOf(icon);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            const int samples = 4;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float nx = ((x + (sx + 0.5f) / samples) / size) * 2f - 1f;
                            float ny = ((y + (sy + 0.5f) / samples) / size) * 2f - 1f;
                            float distance = sdf(nx, ny);
                            coverage += Mathf.Clamp01(0.5f - distance / 0.055f);
                        }
                    }
                    coverage /= samples * samples;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Cascade_Icon_" + icon;
            return sprite;
        }

        private static Func<float, float, float> ShapeOf(CascadeIcon icon)
        {
            switch (icon)
            {
                case CascadeIcon.Home:
                {
                    var roof = new[] { new Vector2(-0.84f, 0.02f), new Vector2(0f, 0.84f), new Vector2(0.84f, 0.02f) };
                    return (x, y) =>
                    {
                        float r = Polygon(x, y, roof);
                        float body = RoundedBox(x, y, 0f, -0.36f, 0.56f, 0.48f, 0.06f);
                        float door = RoundedBox(x, y, 0f, -0.56f, 0.17f, 0.28f, 0.05f);
                        return Mathf.Max(Mathf.Min(r, body), -door);
                    };
                }

                case CascadeIcon.Sliders:
                {
                    return (x, y) =>
                    {
                        float result = Segment(x, y, -0.82f, 0.52f, 0.82f, 0.52f, 0.065f);
                        result = Mathf.Min(result, Circle(x, y, -0.34f, 0.52f, 0.15f));
                        result = Mathf.Min(result, Segment(x, y, -0.82f, 0f, 0.82f, 0f, 0.065f));
                        result = Mathf.Min(result, Circle(x, y, 0.28f, 0f, 0.15f));
                        result = Mathf.Min(result, Segment(x, y, -0.82f, -0.52f, 0.82f, -0.52f, 0.065f));
                        result = Mathf.Min(result, Circle(x, y, -0.08f, -0.52f, 0.15f));
                        return result;
                    };
                }

                case CascadeIcon.Eye:
                {
                    return (x, y) =>
                    {
                        float ellipse = Ellipse(x, y, 0f, 0f, 0.78f, 0.50f);
                        float ring = Mathf.Abs(ellipse) - 0.065f;
                        float pupil = Circle(x, y, 0f, 0f, 0.21f);
                        return Mathf.Min(ring, pupil);
                    };
                }

                case CascadeIcon.Person:
                {
                    return (x, y) =>
                    {
                        float head = Circle(x, y, 0f, 0.38f, 0.27f);
                        float body = RoundedBox(x, y, 0f, -0.43f, 0.53f, 0.40f, 0.36f);
                        return Mathf.Min(head, body);
                    };
                }

                case CascadeIcon.Gear:
                {
                    return (x, y) =>
                    {
                        float result = Mathf.Abs(Circle(x, y, 0f, 0f, 0.50f)) - 0.105f;
                        for (int i = 0; i < 8; i++)
                        {
                            float angle = i * Mathf.PI * 0.25f;
                            float rx = x * Mathf.Cos(angle) + y * Mathf.Sin(angle);
                            float ry = -x * Mathf.Sin(angle) + y * Mathf.Cos(angle);
                            result = Mathf.Min(result, RoundedBox(rx, ry, 0.66f, 0f, 0.17f, 0.10f, 0.03f));
                        }
                        return result;
                    };
                }

                case CascadeIcon.Info:
                {
                    return (x, y) =>
                    {
                        float ring = Mathf.Abs(Circle(x, y, 0f, 0f, 0.72f)) - 0.07f;
                        float dot = Circle(x, y, 0f, 0.38f, 0.085f);
                        float stem = Segment(x, y, 0f, 0.02f, 0f, -0.44f, 0.075f);
                        return Mathf.Min(ring, Mathf.Min(dot, stem));
                    };
                }

                case CascadeIcon.Layers:
                {
                    return (x, y) =>
                    {
                        float result = Segment(x, y, -0.72f, 0.58f, 0f, 0.22f, 0.085f);
                        result = Mathf.Min(result, Segment(x, y, 0f, 0.22f, 0.72f, 0.58f, 0.085f));
                        result = Mathf.Min(result, Segment(x, y, -0.72f, 0.16f, 0f, -0.20f, 0.085f));
                        result = Mathf.Min(result, Segment(x, y, 0f, -0.20f, 0.72f, 0.16f, 0.085f));
                        result = Mathf.Min(result, Segment(x, y, -0.72f, -0.26f, 0f, -0.62f, 0.085f));
                        result = Mathf.Min(result, Segment(x, y, 0f, -0.62f, 0.72f, -0.26f, 0.085f));
                        return result;
                    };
                }

                case CascadeIcon.Search:
                {
                    return (x, y) =>
                    {
                        float ring = Mathf.Abs(Circle(x, y, -0.12f, 0.16f, 0.52f)) - 0.075f;
                        float handle = Segment(x, y, 0.25f, -0.22f, 0.72f, -0.72f, 0.085f);
                        return Mathf.Min(ring, handle);
                    };
                }

                case CascadeIcon.ChevronLeft:
                {
                    return (x, y) =>
                    {
                        float a = Segment(x, y, 0.24f, 0.62f, -0.30f, 0f, 0.09f);
                        float b = Segment(x, y, -0.30f, 0f, 0.24f, -0.62f, 0.09f);
                        return Mathf.Min(a, b);
                    };
                }

                case CascadeIcon.ChevronRight:
                {
                    return (x, y) =>
                    {
                        float a = Segment(x, y, -0.24f, 0.62f, 0.30f, 0f, 0.09f);
                        float b = Segment(x, y, 0.30f, 0f, -0.24f, -0.62f, 0.09f);
                        return Mathf.Min(a, b);
                    };
                }

                case CascadeIcon.ChevronDown:
                {
                    return (x, y) =>
                    {
                        float a = Segment(x, y, 0.62f, 0.24f, 0f, -0.30f, 0.09f);
                        float b = Segment(x, y, 0f, -0.30f, -0.62f, 0.24f, 0.09f);
                        return Mathf.Min(a, b);
                    };
                }

                case CascadeIcon.Plus:
                {
                    return (x, y) =>
                    {
                        float a = Segment(x, y, -0.62f, 0f, 0.62f, 0f, 0.085f);
                        float b = Segment(x, y, 0f, -0.62f, 0f, 0.62f, 0.085f);
                        return Mathf.Min(a, b);
                    };
                }

                case CascadeIcon.Minus:
                {
                    return (x, y) => Segment(x, y, -0.62f, 0f, 0.62f, 0f, 0.085f);
                }

                case CascadeIcon.Close:
                {
                    return (x, y) =>
                    {
                        float a = Segment(x, y, -0.58f, 0.58f, 0.58f, -0.58f, 0.085f);
                        float b = Segment(x, y, -0.58f, -0.58f, 0.58f, 0.58f, 0.085f);
                        return Mathf.Min(a, b);
                    };
                }

                case CascadeIcon.Check:
                {
                    return (x, y) =>
                    {
                        float a = Segment(x, y, -0.66f, 0.02f, -0.16f, -0.50f, 0.085f);
                        float b = Segment(x, y, -0.16f, -0.50f, 0.68f, 0.56f, 0.085f);
                        return Mathf.Min(a, b);
                    };
                }

                case CascadeIcon.Sidebar:
                {
                    return (x, y) =>
                    {
                        float outline = Mathf.Abs(RoundedBox(x, y, 0f, 0f, 0.80f, 0.62f, 0.12f)) - 0.055f;
                        float divider = Segment(x, y, -0.18f, 0.55f, -0.18f, -0.55f, 0.05f);
                        return Mathf.Min(outline, divider);
                    };
                }

                case CascadeIcon.Recenter:
                {
                    return (x, y) =>
                    {
                        float ring = Mathf.Abs(Circle(x, y, 0f, 0f, 0.48f)) - 0.065f;
                        float result = ring;
                        result = Mathf.Min(result, Segment(x, y, 0f, 0.58f, 0f, 0.82f, 0.06f));
                        result = Mathf.Min(result, Segment(x, y, 0f, -0.58f, 0f, -0.82f, 0.06f));
                        result = Mathf.Min(result, Segment(x, y, 0.58f, 0f, 0.82f, 0f, 0.06f));
                        result = Mathf.Min(result, Segment(x, y, -0.58f, 0f, -0.82f, 0f, 0.06f));
                        result = Mathf.Min(result, Circle(x, y, 0f, 0f, 0.11f));
                        return result;
                    };
                }

                case CascadeIcon.Sparkle:
                {
                    var star = new[]
                    {
                        new Vector2(0f, 0.88f), new Vector2(0.16f, 0.16f), new Vector2(0.88f, 0f),
                        new Vector2(0.16f, -0.16f), new Vector2(0f, -0.88f), new Vector2(-0.16f, -0.16f),
                        new Vector2(-0.88f, 0f), new Vector2(-0.16f, 0.16f)
                    };
                    return (x, y) => Polygon(x, y, star);
                }

                case CascadeIcon.Speaker:
                {
                    return (x, y) =>
                    {
                        float body = RoundedBox(x, y, -0.46f, 0f, 0.16f, 0.28f, 0.04f);
                        float cone = RoundedBox(x, y, -0.22f, 0f, 0.20f, 0.46f, 0.06f);
                        float wave1 = Mathf.Max(
                            Mathf.Abs(Circle(x, y, 0.04f, 0f, 0.30f)) - 0.055f,
                            0.06f - x);
                        float wave2 = Mathf.Max(
                            Mathf.Abs(Circle(x, y, 0.04f, 0f, 0.52f)) - 0.055f,
                            0.06f - x);
                        return Mathf.Min(Mathf.Min(body, cone), Mathf.Min(wave1, wave2));
                    };
                }
                case CascadeIcon.Bolt:
                {
                    var bolt = new[]
                    {
                        new Vector2(0.18f, 0.9f), new Vector2(-0.46f, -0.06f), new Vector2(-0.04f, -0.06f),
                        new Vector2(-0.2f, -0.9f), new Vector2(0.5f, 0.14f), new Vector2(0.08f, 0.14f)
                    };
                    return (x, y) => Polygon(x, y, bolt);
                }

                case CascadeIcon.Gamepad:
                {
                    return (x, y) =>
                    {
                        float body = RoundedBox(x, y, 0f, 0f, 0.88f, 0.48f, 0.3f);
                        float cuts = Segment(x, y, -0.42f, 0.22f, -0.42f, -0.12f, 0.07f);
                        cuts = Mathf.Min(cuts, Segment(x, y, -0.59f, 0.05f, -0.25f, 0.05f, 0.07f));
                        cuts = Mathf.Min(cuts, Circle(x, y, 0.36f, 0.14f, 0.09f));
                        cuts = Mathf.Min(cuts, Circle(x, y, 0.54f, -0.04f, 0.09f));
                        return Mathf.Max(body, -cuts);
                    };
                }

                case CascadeIcon.Wrench:
                {
                    return (x, y) =>
                    {
                        float handle = Segment(x, y, -0.68f, -0.68f, 0.2f, 0.2f, 0.13f);
                        float head = Mathf.Abs(Circle(x, y, 0.4f, 0.4f, 0.3f)) - 0.11f;
                        return Mathf.Min(handle, head);
                    };
                }

                case CascadeIcon.Bell:
                {
                    var skirt = new[]
                    {
                        new Vector2(-0.5f, 0.15f), new Vector2(0.5f, 0.15f),
                        new Vector2(0.74f, -0.4f), new Vector2(-0.74f, -0.4f)
                    };
                    return (x, y) =>
                    {
                        float result = Mathf.Min(Circle(x, y, 0f, 0.15f, 0.5f), Polygon(x, y, skirt));
                        result = Mathf.Min(result, Circle(x, y, 0f, -0.64f, 0.14f));
                        return Mathf.Min(result, Circle(x, y, 0f, 0.7f, 0.09f));
                    };
                }

                case CascadeIcon.Terminal:
                {
                    return (x, y) =>
                    {
                        float box = Mathf.Abs(RoundedBox(x, y, 0f, 0f, 0.84f, 0.66f, 0.14f)) - 0.06f;
                        float chevron = Mathf.Min(
                            Segment(x, y, -0.46f, 0.22f, -0.14f, 0f, 0.06f),
                            Segment(x, y, -0.14f, 0f, -0.46f, -0.22f, 0.06f));
                        float cursor = Segment(x, y, 0f, -0.22f, 0.4f, -0.22f, 0.06f);
                        return Mathf.Min(box, Mathf.Min(chevron, cursor));
                    };
                }

                case CascadeIcon.Bag:
                {
                    return (x, y) =>
                    {
                        float body = RoundedBox(x, y, 0f, -0.14f, 0.64f, 0.56f, 0.1f);
                        float handle = Mathf.Max(Mathf.Abs(Circle(x, y, 0f, 0.36f, 0.3f)) - 0.07f, 0.36f - y);
                        return Mathf.Min(body, handle);
                    };
                }

                case CascadeIcon.Palette:
                {
                    return (x, y) =>
                    {
                        float disc = Circle(x, y, 0f, 0f, 0.82f);
                        float holes = Circle(x, y, -0.32f, 0.26f, 0.12f);
                        holes = Mathf.Min(holes, Circle(x, y, 0.04f, 0.44f, 0.12f));
                        holes = Mathf.Min(holes, Circle(x, y, 0.4f, 0.2f, 0.12f));
                        holes = Mathf.Min(holes, Circle(x, y, 0.18f, -0.36f, 0.17f));
                        return Mathf.Max(disc, -holes);
                    };
                }

                case CascadeIcon.Keyboard:
                {
                    return (x, y) =>
                    {
                        float box = Mathf.Abs(RoundedBox(x, y, 0f, 0f, 0.86f, 0.54f, 0.12f)) - 0.06f;
                        float keys = float.MaxValue;
                        for (int i = 0; i < 4; i++) keys = Mathf.Min(keys, Circle(x, y, -0.5f + i * 0.33f, 0.16f, 0.07f));
                        for (int i = 0; i < 3; i++) keys = Mathf.Min(keys, Circle(x, y, -0.33f + i * 0.33f, -0.06f, 0.07f));
                        keys = Mathf.Min(keys, Segment(x, y, -0.34f, -0.3f, 0.34f, -0.3f, 0.06f));
                        return Mathf.Min(box, keys);
                    };
                }

                case CascadeIcon.ArrowUp:
                {
                    var arrow = new[]
                    {
                        new Vector2(0f, 0.82f), new Vector2(0.62f, 0.06f), new Vector2(0.26f, 0.06f),
                        new Vector2(0.26f, -0.72f), new Vector2(-0.26f, -0.72f), new Vector2(-0.26f, 0.06f),
                        new Vector2(-0.62f, 0.06f)
                    };
                    return (x, y) => Polygon(x, y, arrow);
                }

                case CascadeIcon.Backspace:
                {
                    var tag = new[]
                    {
                        new Vector2(-0.86f, 0f), new Vector2(-0.4f, 0.6f), new Vector2(0.86f, 0.6f),
                        new Vector2(0.86f, -0.6f), new Vector2(-0.4f, -0.6f)
                    };
                    return (x, y) =>
                    {
                        float outline = Mathf.Abs(Polygon(x, y, tag)) - 0.06f;
                        float cross = Mathf.Min(
                            Segment(x, y, -0.04f, 0.2f, 0.46f, -0.2f, 0.06f),
                            Segment(x, y, 0.46f, 0.2f, -0.04f, -0.2f, 0.06f));
                        return Mathf.Min(outline, cross);
                    };
                }

                case CascadeIcon.Chart:
                {
                    return (x, y) =>
                    {
                        float result = RoundedBox(x, y, -0.5f, -0.45f, 0.14f, 0.25f, 0.03f);
                        result = Mathf.Min(result, RoundedBox(x, y, 0f, -0.3f, 0.14f, 0.4f, 0.03f));
                        return Mathf.Min(result, RoundedBox(x, y, 0.5f, -0.1f, 0.14f, 0.6f, 0.03f));
                    };
                }

                case CascadeIcon.Cube:
                {
                    var hex = new[]
                    {
                        new Vector2(0f, 0.86f), new Vector2(0.74f, 0.43f), new Vector2(0.74f, -0.43f),
                        new Vector2(0f, -0.86f), new Vector2(-0.74f, -0.43f), new Vector2(-0.74f, 0.43f)
                    };
                    return (x, y) =>
                    {
                        float outline = Mathf.Abs(Polygon(x, y, hex)) - 0.06f;
                        float inner = Segment(x, y, 0f, 0f, -0.74f, 0.43f, 0.05f);
                        inner = Mathf.Min(inner, Segment(x, y, 0f, 0f, 0.74f, 0.43f, 0.05f));
                        inner = Mathf.Min(inner, Segment(x, y, 0f, 0f, 0f, -0.86f, 0.05f));
                        return Mathf.Min(outline, inner);
                    };
                }

                case CascadeIcon.Folder:
                {
                    return (x, y) =>
                    {
                        float body = RoundedBox(x, y, 0f, -0.1f, 0.82f, 0.5f, 0.1f);
                        float tab = RoundedBox(x, y, -0.42f, 0.46f, 0.32f, 0.16f, 0.05f);
                        return Mathf.Min(body, tab);
                    };
                }

                case CascadeIcon.Monitor:
                {
                    return (x, y) =>
                    {
                        float screenOuter = RoundedBox(x, y, 0f, 0.14f, 0.80f, 0.52f, 0.10f);
                        float screenInner = RoundedBox(x, y, 0f, 0.14f, 0.66f, 0.38f, 0.04f);
                        float bezel = Mathf.Max(screenOuter, -screenInner);
                        float stem = RoundedBox(x, y, 0f, -0.50f, 0.08f, 0.16f, 0.03f);
                        float foot = RoundedBox(x, y, 0f, -0.68f, 0.36f, 0.06f, 0.03f);
                        return Mathf.Min(bezel, Mathf.Min(stem, foot));
                    };
                }

                case CascadeIcon.Windows:
                {
                    return (x, y) =>
                    {
                        float backOuter = RoundedBox(x, y, 0.18f, 0.20f, 0.56f, 0.44f, 0.10f);
                        float backInner = RoundedBox(x, y, 0.18f, 0.20f, 0.44f, 0.32f, 0.04f);
                        float backWin = Mathf.Max(backOuter, -backInner);

                        float frontOuter = RoundedBox(x, y, -0.18f, -0.18f, 0.56f, 0.44f, 0.10f);
                        float frontInner = RoundedBox(x, y, -0.18f, -0.18f, 0.44f, 0.32f, 0.04f);
                        float frontWin = Mathf.Max(frontOuter, -frontInner);

                        // Cut back window behind front window so it looks layered cleanly
                        backWin = Mathf.Max(backWin, -frontOuter);
                        return Mathf.Min(backWin, frontWin);
                    };
                }

                case CascadeIcon.Move:
                {
                    return (x, y) =>
                    {
                        float vLine = Segment(x, y, 0f, -0.74f, 0f, 0.74f, 0.07f);
                        float hLine = Segment(x, y, -0.74f, 0f, 0.74f, 0f, 0.07f);
                        float d = Mathf.Min(vLine, hLine);
                        // 4 arrowheads
                        d = Mathf.Min(d, Segment(x, y, 0f, 0.78f, -0.22f, 0.52f, 0.065f));
                        d = Mathf.Min(d, Segment(x, y, 0f, 0.78f, 0.22f, 0.52f, 0.065f));
                        d = Mathf.Min(d, Segment(x, y, 0f, -0.78f, -0.22f, -0.52f, 0.065f));
                        d = Mathf.Min(d, Segment(x, y, 0f, -0.78f, 0.22f, -0.52f, 0.065f));
                        d = Mathf.Min(d, Segment(x, y, -0.78f, 0f, -0.52f, 0.22f, 0.065f));
                        d = Mathf.Min(d, Segment(x, y, -0.78f, 0f, -0.52f, -0.22f, 0.065f));
                        d = Mathf.Min(d, Segment(x, y, 0.78f, 0f, 0.52f, 0.22f, 0.065f));
                        d = Mathf.Min(d, Segment(x, y, 0.78f, 0f, 0.52f, -0.22f, 0.065f));
                        return d;
                    };
                }

                case CascadeIcon.Pin:
                {
                    return (x, y) =>
                    {
                        float head = RoundedBox(x, y, 0f, 0.54f, 0.34f, 0.14f, 0.06f);
                        float body = RoundedBox(x, y, 0f, 0.16f, 0.24f, 0.28f, 0.06f);
                        float flange = RoundedBox(x, y, 0f, -0.10f, 0.44f, 0.09f, 0.04f);
                        float needle = Segment(x, y, 0f, -0.14f, 0f, -0.82f, 0.065f);
                        return Mathf.Min(Mathf.Min(head, body), Mathf.Min(flange, needle));
                    };
                }

                case CascadeIcon.Play:
                {
                    var tri = new[]
                    {
                        new Vector2(-0.46f, 0.68f),
                        new Vector2(0.68f, 0f),
                        new Vector2(-0.46f, -0.68f)
                    };
                    return (x, y) => Polygon(x, y, tri);
                }

                case CascadeIcon.Pause:
                {
                    return (x, y) =>
                    {
                        float left = RoundedBox(x, y, -0.34f, 0f, 0.16f, 0.64f, 0.06f);
                        float right = RoundedBox(x, y, 0.34f, 0f, 0.16f, 0.64f, 0.06f);
                        return Mathf.Min(left, right);
                    };
                }

                case CascadeIcon.PrevTrack:
                {
                    var tri = new[]
                    {
                        new Vector2(0.64f, 0.62f),
                        new Vector2(-0.32f, 0f),
                        new Vector2(0.64f, -0.62f)
                    };
                    return (x, y) =>
                    {
                        float bar = RoundedBox(x, y, -0.56f, 0f, 0.11f, 0.62f, 0.04f);
                        return Mathf.Min(bar, Polygon(x, y, tri));
                    };
                }

                case CascadeIcon.NextTrack:
                {
                    var tri = new[]
                    {
                        new Vector2(-0.64f, 0.62f),
                        new Vector2(0.32f, 0f),
                        new Vector2(-0.64f, -0.62f)
                    };
                    return (x, y) =>
                    {
                        float bar = RoundedBox(x, y, 0.56f, 0f, 0.11f, 0.62f, 0.04f);
                        return Mathf.Min(bar, Polygon(x, y, tri));
                    };
                }

                case CascadeIcon.Music:
                {
                    return (x, y) =>
                    {
                        float noteL = Ellipse(x, y, -0.44f, -0.50f, 0.24f, 0.18f);
                        float noteR = Ellipse(x, y, 0.38f, -0.38f, 0.24f, 0.18f);
                        float stemL = Segment(x, y, -0.24f, -0.46f, -0.24f, 0.52f, 0.07f);
                        float stemR = Segment(x, y, 0.58f, -0.34f, 0.58f, 0.68f, 0.07f);
                        float beam = Segment(x, y, -0.24f, 0.52f, 0.58f, 0.68f, 0.11f);
                        return Mathf.Min(Mathf.Min(noteL, noteR), Mathf.Min(Mathf.Min(stemL, stemR), beam));
                    };
                }

                case CascadeIcon.Cursor:
                {
                    var ptr = new[]
                    {
                        new Vector2(-0.46f, 0.80f),
                        new Vector2(-0.46f, -0.58f),
                        new Vector2(-0.10f, -0.26f),
                        new Vector2(0.14f, -0.78f),
                        new Vector2(0.36f, -0.68f),
                        new Vector2(0.12f, -0.18f),
                        new Vector2(0.56f, -0.18f)
                    };
                    return (x, y) => Polygon(x, y, ptr);
                }

                case CascadeIcon.Refresh:
                {
                    return (x, y) =>
                    {
                        float ring = Mathf.Abs(Circle(x, y, 0f, 0f, 0.58f)) - 0.08f;
                        // Open gap in upper-right quadrant
                        float cut = RoundedBox(x, y, 0.42f, 0.42f, 0.28f, 0.28f, 0.02f);
                        float arc = Mathf.Max(ring, -cut);
                        float a1 = Segment(x, y, 0.48f, 0.14f, 0.24f, 0.46f, 0.07f);
                        float a2 = Segment(x, y, 0.48f, 0.14f, 0.78f, 0.36f, 0.07f);
                        return Mathf.Min(arc, Mathf.Min(a1, a2));
                    };
                }

                case CascadeIcon.Lock:
                {
                    return (x, y) =>
                    {
                        float body = RoundedBox(x, y, 0f, -0.26f, 0.56f, 0.44f, 0.10f);
                        float shackleOuter = Circle(x, y, 0f, 0.24f, 0.38f);
                        float shackleInner = Circle(x, y, 0f, 0.24f, 0.24f);
                        float shackle = Mathf.Max(Mathf.Max(shackleOuter, -shackleInner), -y + 0.12f);
                        return Mathf.Min(body, shackle);
                    };
                }

                case CascadeIcon.Shield:
                {
                    var poly = new[]
                    {
                        new Vector2(0f, 0.84f),
                        new Vector2(0.66f, 0.54f),
                        new Vector2(0.58f, -0.14f),
                        new Vector2(0f, -0.84f),
                        new Vector2(-0.58f, -0.14f),
                        new Vector2(-0.66f, 0.54f)
                    };
                    return (x, y) =>
                    {
                        float outer = Polygon(x, y, poly);
                        return Mathf.Abs(outer) - 0.08f;
                    };
                }

                case CascadeIcon.Star:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                        float r = (i % 2 == 0) ? 0.84f : 0.36f;
                        pts[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                    }
                    return (x, y) => Polygon(x, y, pts);
                }
            }
            return (x, y) => 1f;
        }

        // ---- SDF helpers -------------------------------------------------

        private static float Circle(float x, float y, float cx, float cy, float radius)
        {
            float dx = x - cx;
            float dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x - cx) / rx;
            float ny = (y - cy) / ry;
            return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by, float thickness)
        {
            float vx = bx - ax;
            float vy = by - ay;
            float wx = x - ax;
            float wy = y - ay;
            float lengthSq = vx * vx + vy * vy;
            float t = lengthSq <= 0.00001f ? 0f : Mathf.Clamp01((wx * vx + wy * vy) / lengthSq);
            float dx = x - (ax + vx * t);
            float dy = y - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy) - thickness;
        }

        private static float RoundedBox(float x, float y, float cx, float cy, float halfWidth, float halfHeight, float radius)
        {
            float dx = Mathf.Abs(x - cx) - (halfWidth - radius);
            float dy = Mathf.Abs(y - cy) - (halfHeight - radius);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius;
        }

        private static float Polygon(float x, float y, Vector2[] points)
        {
            bool inside = false;
            float distance = float.MaxValue;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[j];
                Vector2 b = points[i];
                distance = Mathf.Min(distance, Segment(x, y, a.x, a.y, b.x, b.y, 0f));
                if (((b.y > y) != (a.y > y)) && (x < (a.x - b.x) * (y - b.y) / (a.y - b.y) + b.x)) inside = !inside;
            }
            return inside ? -distance : distance;
        }

        /// <summary>
        /// Releases all cached icons and their textures.
        /// </summary>
        public static void ReleaseAll()
        {
            foreach (var entry in Cache)
            {
                if (entry.Value != null)
                {
                    CascadeSprites.DestroySprite(entry.Value);
                }
            }
            Cache.Clear();
        }
    }
}
