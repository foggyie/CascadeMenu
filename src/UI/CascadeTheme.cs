using UnityEngine;

namespace CascadeMenu.UI
{
    internal enum CascadeThemeMode
    {
        Dark = 0,
        Light = 1
    }

    internal sealed class CascadeAccent
    {
        public readonly string Name;
        public readonly Color DarkSelection;
        public readonly Color DarkSwitch;
        public readonly Color DarkButtonTop;
        public readonly Color DarkButtonBottom;
        public readonly Color LightSelection;
        public readonly Color LightSwitch;
        public readonly Color LightButtonTop;
        public readonly Color LightButtonBottom;

        public CascadeAccent(
            string name,
            string darkSelection,
            string darkSwitch,
            string darkTop,
            string darkBottom,
            string lightSelection,
            string lightSwitch,
            string lightTop,
            string lightBottom)
        {
            Name = name;
            DarkSelection = CascadeTheme.Hex(darkSelection);
            DarkSwitch = CascadeTheme.Hex(darkSwitch);
            DarkButtonTop = CascadeTheme.Hex(darkTop);
            DarkButtonBottom = CascadeTheme.Hex(darkBottom);
            LightSelection = CascadeTheme.Hex(lightSelection);
            LightSwitch = CascadeTheme.Hex(lightSwitch);
            LightButtonTop = CascadeTheme.Hex(lightTop);
            LightButtonBottom = CascadeTheme.Hex(lightBottom);
        }
    }

    /// <summary>
    /// Colors and metrics lifted from Cascade's Dark/Light themes and its accent file.
    /// </summary>
    internal sealed class CascadeTheme
    {
        public const float WindowWidth = 850f;
        public const float WindowHeight = 530f;
        public const float SidebarWidth = 200f;
        public const float TitlebarHeight = 52f;
        public const float CornerRadius = 10f;

        public static readonly CascadeAccent[] Accents =
        {
            new CascadeAccent("Blue",     "#007AFF", "#0A84FF", "#4894FF", "#0A6EFF", "#007AFF", "#0A84FF", "#2B91FF", "#007AFF"),
            new CascadeAccent("Purple",   "#9B35D4", "#BF5AF2", "#AE4FE0", "#7A18B8", "#8C2EC0", "#AF52DE", "#9E42CA", "#7010A8"),
            new CascadeAccent("Pink",     "#D4163E", "#FF375F", "#E83058", "#C00030", "#C8103A", "#FF2D55", "#DC2A50", "#B4002C"),
            new CascadeAccent("Red",      "#D4201A", "#FF453A", "#E83830", "#BC0E08", "#C81A10", "#FF3B30", "#DC2E24", "#B00808"),
            new CascadeAccent("Orange",   "#C86800", "#FF9F0A", "#E07C00", "#A85000", "#BC6000", "#FF9500", "#D47000", "#A04800"),
            new CascadeAccent("Yellow",   "#A87800", "#FFD60A", "#C49000", "#8C6000", "#9C7000", "#FFCC00", "#B88400", "#845800"),
            new CascadeAccent("Green",    "#1A9030", "#32D74B", "#24A83C", "#0E7020", "#148428", "#28CD41", "#1E9C34", "#0C6818"),
            new CascadeAccent("Graphite", "#58585C", "#98989D", "#6E6E72", "#404044", "#4E4E52", "#8E8E93", "#626266", "#383838")
        };

        public CascadeThemeMode Mode;
        public string AccentName;
        public bool IsDark;

        public Color TextPrimary;
        public Color TextSecondary;
        public Color TextTertiary;

        public Color WindowBorder;
        public Color SidebarBg;
        public Color ContentBg;
        public Color TitlebarBg;
        public Color TitlebarHairline;
        public Color ViewBg;
        public Color ViewBorder;
        public Color Separator;
        public Color RowHover;
        public Color TabHover;

        public Color Accent;
        public Color AccentSoft;
        public Color SelectionText;
        public Color ToggleOff;
        public Color ToggleKnob;

        public Color SliderTrack;
        public Color SliderFill;
        public Color SliderThumb;
        public Color SliderThumbBorder;

        public Color ButtonPrimaryTop;
        public Color ButtonPrimaryBottom;
        public Color ButtonSecondaryTop;
        public Color ButtonSecondaryBottom;
        public Color ButtonSecondaryHover;
        public Color ButtonText;
        public Color ButtonSecondaryText;

        public Color FieldBg;
        public Color FieldBorder;
        public Color FieldFocus;
        public Color Scrollbar;

        public Color TrafficClose;
        public Color TrafficMinimize;
        public Color TrafficZoom;
        public Color TrafficGlyph;

        public Color ToastBg;
        public Color ToastBorder;

        public Color IconNormal;
        public Color IconSelected;
        public Color ShadowColor;

        public static string[] AccentNames
        {
            get
            {
                var names = new string[Accents.Length];
                for (int i = 0; i < Accents.Length; i++) names[i] = Accents[i].Name;
                return names;
            }
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(hex, out color)) color = Color.white;
            color.a = alpha;
            return color;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static CascadeTheme Build(CascadeThemeMode mode, int accentIndex)
        {
            CascadeAccent accent = Accents[Mathf.Clamp(accentIndex, 0, Accents.Length - 1)];
            var theme = new CascadeTheme
            {
                Mode = mode,
                IsDark = mode == CascadeThemeMode.Dark,
                AccentName = accent.Name,
                Accent = mode == CascadeThemeMode.Dark ? accent.DarkSelection : accent.LightSelection,
                AccentSoft = mode == CascadeThemeMode.Dark ? accent.DarkSwitch : accent.LightSwitch,
                SelectionText = Hex("#FFFFFF"),
                ToggleKnob = Hex("#FFFFFF"),
                TrafficClose = Hex("#FF5F57"),
                TrafficMinimize = Hex("#FEBC2E"),
                TrafficZoom = Hex("#28C840"),
                TrafficGlyph = Hex("#000000", 0.55f)
            };

            if (theme.IsDark)
            {
                theme.TextPrimary = Hex("#FFFFFF", 0.94f);
                theme.TextSecondary = Hex("#FFFFFF", 0.68f);
                theme.TextTertiary = Hex("#FFFFFF", 0.40f);

                theme.SidebarBg = Hex("#202023", 0.90f);
                theme.ContentBg = Hex("#1C1C1E", 0.97f);
                theme.TitlebarBg = Hex("#2C2C2E", 0.97f);
                theme.WindowBorder = Hex("#FFFFFF", 0.10f);
                theme.TitlebarHairline = Hex("#000000", 0.38f);
                theme.ViewBg = Hex("#1F1F21", 0.98f);
                theme.ViewBorder = Hex("#FFFFFF", 0.06f);
                theme.Separator = Hex("#FFFFFF", 0.065f);
                theme.RowHover = Hex("#FFFFFF", 0.05f);
                theme.TabHover = Hex("#FFFFFF", 0.07f);

                theme.ToggleOff = Hex("#7A7A7A", 0.60f);
                theme.SliderTrack = Hex("#2C2C2E");
                theme.SliderFill = accent.DarkSwitch;
                theme.SliderThumb = Hex("#FFFFFF");
                theme.SliderThumbBorder = Hex("#000000", 0.20f);

                theme.ButtonPrimaryTop = accent.DarkButtonTop;
                theme.ButtonPrimaryBottom = accent.DarkButtonBottom;
                theme.ButtonSecondaryTop = Hex("#3C3C3C");
                theme.ButtonSecondaryBottom = Hex("#373737");
                theme.ButtonSecondaryHover = Hex("#484848");
                theme.ButtonText = Hex("#FFFFFF");
                theme.ButtonSecondaryText = Hex("#FFFFFF", 0.92f);

                theme.FieldBg = Hex("#FFFFFF", 0.06f);
                theme.FieldBorder = Hex("#FFFFFF", 0.10f);
                theme.FieldFocus = accent.DarkSelection;
                theme.Scrollbar = Hex("#FFFFFF", 0.26f);

                theme.ToastBg = Hex("#2C2C2E", 0.98f);
                theme.ToastBorder = Hex("#FFFFFF", 0.10f);

                theme.IconNormal = Hex("#FFFFFF", 0.55f);
                theme.IconSelected = Hex("#FFFFFF");
                theme.ShadowColor = Hex("#000000", 0.55f);
            }
            else
            {
                theme.TextPrimary = Hex("#000000", 0.92f);
                theme.TextSecondary = Hex("#000000", 0.62f);
                theme.TextTertiary = Hex("#000000", 0.40f);

                theme.SidebarBg = Hex("#EAEAEA", 0.94f);
                theme.ContentBg = Hex("#FCFCFC", 0.99f);
                theme.TitlebarBg = Hex("#EEEEEE", 0.99f);
                theme.WindowBorder = Hex("#000000", 0.10f);
                theme.TitlebarHairline = Hex("#000000", 0.08f);
                theme.ViewBg = Hex("#FFFFFF", 0.99f);
                theme.ViewBorder = Hex("#000000", 0.07f);
                theme.Separator = Hex("#000000", 0.07f);
                theme.RowHover = Hex("#000000", 0.035f);
                theme.TabHover = Hex("#000000", 0.05f);

                theme.ToggleOff = Hex("#000000", 0.09f);
                theme.SliderTrack = Hex("#000000", 0.09f);
                theme.SliderFill = accent.LightSwitch;
                theme.SliderThumb = Hex("#FFFFFF");
                theme.SliderThumbBorder = Hex("#000000", 0.06f);

                theme.ButtonPrimaryTop = accent.LightButtonTop;
                theme.ButtonPrimaryBottom = accent.LightButtonBottom;
                theme.ButtonSecondaryTop = Hex("#FFFFFF");
                theme.ButtonSecondaryBottom = Hex("#F7F7F7");
                theme.ButtonSecondaryHover = Hex("#EFEFEF");
                theme.ButtonText = Hex("#FFFFFF");
                theme.ButtonSecondaryText = Hex("#000000", 0.86f);

                theme.FieldBg = Hex("#000000", 0.035f);
                theme.FieldBorder = Hex("#000000", 0.09f);
                theme.FieldFocus = accent.LightSelection;
                theme.Scrollbar = Hex("#000000", 0.28f);

                theme.ToastBg = Hex("#FFFFFF", 0.98f);
                theme.ToastBorder = Hex("#000000", 0.10f);

                theme.IconNormal = Hex("#000000", 0.50f);
                theme.IconSelected = Hex("#FFFFFF");
                theme.ShadowColor = Hex("#000000", 0.35f);
            }

            return theme;
        }
    }
}
