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
    /// <summary>
    /// The user-editable colors of a theme. Everything else in
    /// <see cref="CascadeTheme"/> is derived from these, so a new seasonal theme
    /// only has to supply one palette.
    /// </summary>
    internal sealed class CascadeThemePalette
    {
        public const int Count = 17;

        private readonly Color[] _colors = new Color[Count];

        public static readonly string[] Names =
        {
            "Background",
            "Panel",
            "Header",
            "Button",
            "Button hover",
            "Button pressed",
            "Toggle on",
            "Toggle off",
            "Text",
            "Secondary text",
            "Accent",
            "Border",
            "Active",
            "Field",
            "Danger",
            "Warning",
            "Success"
        };

        public static readonly string[] Descriptions =
        {
            "Main page background area behind all cards and rows",
            "Left navigation sidebar rail and grouped settings cards",
            "Top macOS titlebar, telemetry pill, and search header",
            "Secondary action buttons and neutral control pills",
            "Highlight color when hovering over a button or row",
            "Fill color while pressing or holding down a button",
            "Switch track fill when a toggle is turned ON",
            "Switch track fill when a toggle is turned OFF",
            "Primary titles, headings, and main readable labels",
            "Subtitles, descriptions, and secondary status text",
            "Active tab fill, sliders, primary buttons, and outer glow",
            "Window frame outline, card borders, and divider lines",
            "Live ON status dot, active badges, and running highlights",
            "Search box, text input fields, and dropdown surfaces",
            "Destructive actions, warnings, and disconnect button",
            "Caution labels, alerts, and warning badges",
            "Positive confirmations, owned dots, and saved badges"
        };

        public static string Describe(int index)
        {
            return Descriptions[Mathf.Clamp(index, 0, Count - 1)];
        }

        public Color this[int index]
        {
            get { return _colors[Mathf.Clamp(index, 0, Count - 1)]; }
            set { _colors[Mathf.Clamp(index, 0, Count - 1)] = new Color(value.r, value.g, value.b, 1f); }
        }

        public CascadeThemePalette Clone()
        {
            var copy = new CascadeThemePalette();
            for (int i = 0; i < Count; i++) copy._colors[i] = _colors[i];
            return copy;
        }

        /// <summary>#RRGGBB values separated by spaces, stored in the config file.</summary>
        public string Serialize()
        {
            var parts = new string[Count];
            for (int i = 0; i < Count; i++) parts[i] = ToHex(_colors[i]);
            return string.Join(" ", parts);
        }

        public static CascadeThemePalette Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            string[] parts = value.Split(new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < Count) return null;

            var palette = new CascadeThemePalette();
            for (int i = 0; i < Count; i++)
            {
                Color color;
                string part = parts[i].Trim();
                if (!part.StartsWith("#")) part = "#" + part;
                if (!ColorUtility.TryParseHtmlString(part, out color)) return null;
                palette._colors[i] = new Color(color.r, color.g, color.b, 1f);
            }
            return palette;
        }

        public static string ToHex(Color color)
        {
            Color32 value = color;
            return "#" + value.r.ToString("X2") + value.g.ToString("X2") + value.b.ToString("X2");
        }

        public static CascadeThemePalette FromPreset(CascadeThemePreset preset)
        {
            return preset != null ? preset.Palette.Clone() : CascadeThemePreset.DefaultPalette.Clone();
        }
    }

    /// <summary>
    /// One preset theme. Adding a theme is one entry in
    /// <see cref="CascadeThemePreset.All"/>. Season dates are optional; when two
    /// seasons overlap, the earlier entry wins.
    /// </summary>
    internal sealed class CascadeThemePreset
    {
        public readonly string Name;
        public readonly CascadeThemePalette Palette;
        public readonly bool Dark;

        // 0 = not a seasonal theme. A range that wraps (start > end) spans the
        // new year, e.g. 12/27 - 1/1.
        public readonly int StartMonth;
        public readonly int StartDay;
        public readonly int EndMonth;
        public readonly int EndDay;

        public CascadeThemePreset(
            string name,
            bool dark,
            string background,
            string panel,
            string header,
            string button,
            string buttonHover,
            string buttonPressed,
            string toggleOn,
            string toggleOff,
            string text,
            string secondaryText,
            string accent,
            string border,
            string active,
            string field,
            string danger,
            string warning,
            string success,
            int startMonth = 0,
            int startDay = 0,
            int endMonth = 0,
            int endDay = 0)
        {
            Name = name;
            Dark = dark;
            StartMonth = startMonth;
            StartDay = startDay;
            EndMonth = endMonth;
            EndDay = endDay;

            Palette = new CascadeThemePalette();
            Palette[(int)CascadePaletteIndex.Background] = CascadeTheme.Hex(background);
            Palette[(int)CascadePaletteIndex.Panel] = CascadeTheme.Hex(panel);
            Palette[(int)CascadePaletteIndex.Header] = CascadeTheme.Hex(header);
            Palette[(int)CascadePaletteIndex.Button] = CascadeTheme.Hex(button);
            Palette[(int)CascadePaletteIndex.ButtonHover] = CascadeTheme.Hex(buttonHover);
            Palette[(int)CascadePaletteIndex.ButtonPressed] = CascadeTheme.Hex(buttonPressed);
            Palette[(int)CascadePaletteIndex.ToggleOn] = CascadeTheme.Hex(toggleOn);
            Palette[(int)CascadePaletteIndex.ToggleOff] = CascadeTheme.Hex(toggleOff);
            Palette[(int)CascadePaletteIndex.Text] = CascadeTheme.Hex(text);
            Palette[(int)CascadePaletteIndex.SecondaryText] = CascadeTheme.Hex(secondaryText);
            Palette[(int)CascadePaletteIndex.Accent] = CascadeTheme.Hex(accent);
            Palette[(int)CascadePaletteIndex.Border] = CascadeTheme.Hex(border);
            Palette[(int)CascadePaletteIndex.Active] = CascadeTheme.Hex(active);
            Palette[(int)CascadePaletteIndex.Field] = CascadeTheme.Hex(field);
            Palette[(int)CascadePaletteIndex.Danger] = CascadeTheme.Hex(danger);
            Palette[(int)CascadePaletteIndex.Warning] = CascadeTheme.Hex(warning);
            Palette[(int)CascadePaletteIndex.Success] = CascadeTheme.Hex(success);
        }

        public bool Seasonal
        {
            get { return StartMonth > 0 && EndMonth > 0; }
        }

        public bool Contains(DateTime date)
        {
            if (!Seasonal) return false;

            var day = new DateTime(2000, date.Month, date.Day);
            int startDay = Mathf.Clamp(StartDay, 1, DateTime.DaysInMonth(2000, StartMonth));
            int endDay = Mathf.Clamp(EndDay, 1, DateTime.DaysInMonth(2000, EndMonth));
            var start = new DateTime(2000, StartMonth, startDay);
            var end = new DateTime(2000, EndMonth, endDay);

            if (start <= end) return day >= start && day <= end;
            return day >= start || day <= end;
        }

        /// <summary>The first seasonal theme that matches the date, or null.</summary>
        public static CascadeThemePreset ForDate(DateTime date)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Seasonal && All[i].Contains(date)) return All[i];
            }
            return null;
        }

        public static CascadeThemePreset Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase)) return All[i];
            }
            return null;
        }

        public static CascadeThemePreset Default
        {
            get { return All[0]; }
        }

        public static CascadeThemePalette DefaultPalette
        {
            get { return All[0].Palette; }
        }

        /// <summary>Small preview used by the Themes tab: background, panel, button, accent, text, active.</summary>
        public Color[] Swatch()
        {
            return new[]
            {
                Palette[(int)CascadePaletteIndex.Background],
                Palette[(int)CascadePaletteIndex.Panel],
                Palette[(int)CascadePaletteIndex.Button],
                Palette[(int)CascadePaletteIndex.Accent],
                Palette[(int)CascadePaletteIndex.Text],
                Palette[(int)CascadePaletteIndex.Active]
            };
        }

        // -----------------------------------------------------------------
        // The preset list. Add a theme here; no other code changes are needed.
        // Specific holidays come before broad seasons so overlap resolves to the
        // holiday (for example Valentine's Day wins over Winter).
        // -----------------------------------------------------------------
        public static readonly CascadeThemePreset[] All =
        {
            new CascadeThemePreset(
                "Default", true,
                "#1C1C1E", "#202023", "#2C2C2E", "#373737", "#484848", "#303030",
                "#0A84FF", "#7A7A7A", "#FFFFFF", "#FFFFFF", "#007AFF", "#FFFFFF",
                "#4CD964", "#FFFFFF", "#FF5C5C", "#FFB020", "#4CD964"),

            new CascadeThemePreset(
                "Halloween", true,
                "#120D18", "#1A1020", "#22152B", "#2E1A3A", "#3D224C", "#24132E",
                "#FF7A18", "#5B4A66", "#F5E9FF", "#C9A8E0", "#FF7A18", "#9B5DE5",
                "#9B5DE5", "#FFFFFF", "#FF4D4D", "#FFB020", "#8FBF3F",
                10, 1, 10, 31),

            new CascadeThemePreset(
                "Thanksgiving", true,
                "#22140C", "#2B1A10", "#332014", "#4A2E1A", "#5C3A20", "#3A2414",
                "#D97B1A", "#6B5540", "#FFF4E3", "#E3C39B", "#E08A1E", "#F0C070",
                "#8FBF3F", "#FFF4E3", "#FF6B4A", "#FFB020", "#9BCB5A",
                11, 1, 11, 30),

            new CascadeThemePreset(
                "Christmas", true,
                "#0F1A14", "#13221A", "#172920", "#1E3A2A", "#285036", "#16301F",
                "#2FAF5A", "#5A6E62", "#FFFFFF", "#CFE6D8", "#D62828", "#F5D67B",
                "#32D74B", "#FFFFFF", "#FF5A5A", "#FFD166", "#32D74B",
                12, 1, 12, 26),

            new CascadeThemePreset(
                "New Year", true,
                "#101014", "#18181E", "#20202A", "#2C2C38", "#3C3C4C", "#242430",
                "#E8C34A", "#5A5A6E", "#FFFFFF", "#D8D8E0", "#E8C34A", "#E8C34A",
                "#FFD700", "#FFFFFF", "#FF5A5A", "#FFD700", "#FFD700",
                12, 27, 1, 1),

            new CascadeThemePreset(
                "Valentine's", true,
                "#2A0E18", "#34121E", "#3D1624", "#501B2E", "#64233A", "#3E1522",
                "#FF4D7D", "#7A5A66", "#FFF0F4", "#F2B8C6", "#FF3D71", "#FF9EB5",
                "#FF6F91", "#FFFFFF", "#FF4D4D", "#FFB020", "#7FC86A",
                2, 1, 2, 14),

            new CascadeThemePreset(
                "St. Patrick's", true,
                "#0C1A10", "#112416", "#15301D", "#1D3E26", "#285234", "#152E1C",
                "#3FBF5F", "#5A7060", "#F0FFF4", "#B8E6C4", "#3FBF5F", "#C6E86B",
                "#7FE08A", "#FFFFFF", "#FF5A5A", "#FFD166", "#7FE08A",
                3, 10, 3, 17),

            new CascadeThemePreset(
                "Easter", true,
                "#1E1730", "#27203C", "#302748", "#3C3158", "#4C3E6E", "#2E2545",
                "#FF9EC4", "#6E6288", "#FFF4FB", "#D9C6F0", "#FF9EC4", "#B8E0C8",
                "#9BE7B0", "#FFFFFF", "#FF6B8A", "#FFD166", "#9BE7B0",
                3, 20, 4, 25),

            new CascadeThemePreset(
                "4th of July", true,
                "#0B1630", "#101E40", "#142550", "#1B2E5E", "#243D78", "#142348",
                "#E63946", "#5A6A8A", "#FFFFFF", "#B8C6E6", "#E63946", "#FFFFFF",
                "#3A86FF", "#FFFFFF", "#FF4D4D", "#FFD166", "#4CD964",
                6, 25, 7, 5),

            new CascadeThemePreset(
                "Summer", true,
                "#07242A", "#0B3038", "#0F3C46", "#14505C", "#1C6874", "#0E3E48",
                "#FFD166", "#5A7E84", "#F2FEFF", "#B8E6EA", "#FFD166", "#7FE0DA",
                "#4FE0C0", "#FFFFFF", "#FF6B6B", "#FFB020", "#4FE0C0",
                6, 1, 8, 31),

            new CascadeThemePreset(
                "Fall", true,
                "#1E1408", "#2A1C0C", "#332311", "#4A3218", "#5E4020", "#38260F",
                "#E07A1F", "#6B5A42", "#FFF3DF", "#E0C49A", "#E07A1F", "#D9A441",
                "#B7C94A", "#FFFFFF", "#FF6B4A", "#FFB020", "#A8C75A",
                9, 1, 9, 30),

            new CascadeThemePreset(
                "Winter", true,
                "#0E1A26", "#122434", "#163044", "#1D3C54", "#27506C", "#153040",
                "#7FD4FF", "#527084", "#F2FAFF", "#B9D8EA", "#4FC3F7", "#C9EBFF",
                "#7FD4FF", "#FFFFFF", "#FF6B6B", "#FFD166", "#7FE0B0",
                12, 27, 2, 28),

            new CascadeThemePreset(
                "Dark", true,
                "#141416", "#1B1B1E", "#242428", "#303035", "#3C3C42", "#28282C",
                "#3A8DFF", "#6E6E74", "#F5F5F7", "#C7C7CC", "#3A8DFF", "#FFFFFF",
                "#32D74B", "#FFFFFF", "#FF453A", "#FFD60A", "#32D74B"),

            new CascadeThemePreset(
                "Light", false,
                "#FCFCFC", "#EAEAEA", "#EEEEEE", "#FFFFFF", "#EFEFEF", "#E4E4E4",
                "#0A84FF", "#000000", "#000000", "#000000", "#007AFF", "#000000",
                "#34C759", "#000000", "#D70015", "#B86E00", "#248A3D")
        };

        public static readonly string[] Names = BuildNames();

        private static string[] BuildNames()
        {
            var names = new string[All.Length];
            for (int i = 0; i < All.Length; i++) names[i] = All[i].Name;
            return names;
        }
    }

    internal enum CascadePaletteIndex
    {
        Background = 0,
        Panel = 1,
        Header = 2,
        Button = 3,
        ButtonHover = 4,
        ButtonPressed = 5,
        ToggleOn = 6,
        ToggleOff = 7,
        Text = 8,
        SecondaryText = 9,
        Accent = 10,
        Border = 11,
        Active = 12,
        Field = 13,
        Danger = 14,
        Warning = 15,
        Success = 16
    }
}
