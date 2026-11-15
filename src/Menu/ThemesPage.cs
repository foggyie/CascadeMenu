using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using UnityEngine;

namespace CascadeMenu
{
    /// <summary>
    /// The appearance tab. Every preset is one row; adding a season to
    /// <see cref="CascadeThemePreset.All"/> makes it appear here automatically.
    ///
    /// The custom picker edits the live theme in place: moving a slider or
    /// typing a hex value immediately repaints every open window, player list,
    /// tooltip and Active dot through the theme's binding cache, so the page is
    /// never rebuilt and its scroll position is retained.
    /// </summary>
    internal static class ThemesPage
    {
        internal const string Tag = "cascade.themes";

        // Quick picks for the selected palette slot. These are deliberately
        // plain colors so they can be used against any background.
        private static readonly Color[] QuickColors =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(0.08f, 0.08f, 0.09f, 1f),
            new Color(0.45f, 0.45f, 0.48f, 1f),
            new Color(1f, 0.30f, 0.30f, 1f),
            new Color(1f, 0.58f, 0.20f, 1f),
            new Color(1f, 0.84f, 0.30f, 1f),
            new Color(0.35f, 0.85f, 0.45f, 1f),
            new Color(0.30f, 0.70f, 1f, 1f),
            new Color(0.60f, 0.45f, 1f, 1f),
            new Color(1f, 0.45f, 0.75f, 1f)
        };

        internal static CascadePage Build(CascadeMenuBehaviour menu)
        {
            var page = new CascadePage("Themes", "Seasonal presets, custom colors and a live preview", CascadeIcon.Sparkle);
            page.Tag = Tag;

            BuildPresets(page, menu);
            BuildCustom(page, menu);
            BuildActions(page, menu);
            return page;
        }

        private static void BuildPresets(CascadePage page, CascadeMenuBehaviour menu)
        {
            CascadeSection section = page.Section("Presets", "Tap a row to apply it immediately");

            section
                .Toggle("Auto seasonal theme",
                    "Pick a theme from today's date when Cascade starts and whenever the menu opens",
                    () => menu.ThemeAuto,
                    menu.SetThemeAuto)
                .Label("Today's pick",
                    "Used while Auto is on; holidays win over broad seasons",
                    DescribeDate(DateTime.Now))
                .Label("Current theme", "Saved to the config file", menu.Theme.Name);

            for (int i = 0; i < CascadeThemePreset.All.Length; i++)
            {
                CascadeThemePreset preset = CascadeThemePreset.All[i];
                string description = preset.Seasonal
                    ? "Seasonal - " + DescribeRange(preset)
                    : "Available all year";

                section.ThemePreset(
                    preset.Name,
                    description,
                    preset.Swatch(),
                    () => string.Equals(menu.Theme.Name, preset.Name, StringComparison.Ordinal),
                    () => menu.SelectThemePreset(preset.Name));
            }
        }

        private static void BuildCustom(CascadePage page, CascadeMenuBehaviour menu)
        {
            string[] names = CascadeThemePalette.Names;

            page.Section("Custom colors",
                    "Edits apply live. The picker stores the result as the Custom theme.")
                .Dropdown("Color to edit", "Choose which role the sliders and hex box change",
                    names,
                    () => menu.CustomColorIndex,
                    index => menu.CustomColorIndex = index)
                .Slider("Red", "Red channel of the selected color",
                    0f, 1f,
                    () => menu.GetCustomChannel(menu.CustomColorIndex, 0),
                    value => menu.SetCustomChannel(menu.CustomColorIndex, 0, value),
                    FormatChannel)
                .Slider("Green", "Green channel of the selected color",
                    0f, 1f,
                    () => menu.GetCustomChannel(menu.CustomColorIndex, 1),
                    value => menu.SetCustomChannel(menu.CustomColorIndex, 1, value),
                    FormatChannel)
                .Slider("Blue", "Blue channel of the selected color",
                    0f, 1f,
                    () => menu.GetCustomChannel(menu.CustomColorIndex, 2),
                    value => menu.SetCustomChannel(menu.CustomColorIndex, 2, value),
                    FormatChannel)
                .TextField("Hex value", "Type #RRGGBB or RRGGBB and it applies as you type",
                    "#RRGGBB", 7,
                    () => menu.CustomHex(menu.CustomColorIndex),
                    menu.SetCustomHex)
                .Swatches("Quick picks", "Tap a chip to replace the selected color", QuickColors,
                    index => menu.SetCustomColor(menu.CustomColorIndex, QuickColors[index]))
                .ThemePreview("Preview", "Background, panel, header, text, accent and Active");
        }

        private static void BuildActions(CascadePage page, CascadeMenuBehaviour menu)
        {
            page.Section("Manage", "Your custom palette is stored in the config file")
                .Button("Save as Custom", "Make the edited palette the active theme and save it",
                    "Save", menu.SaveCustomTheme, true)
                .Button("Reset to preset", "Restore the custom palette to the selected preset",
                    "Reset", menu.ResetCustomToPreset, false)
                .Button("Reset all", "Restore the Default theme and the Default palette",
                    "Reset all", menu.ResetCustomAll, false);
        }

        private static string FormatChannel(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 255f).ToString();
        }

        private static string DescribeDate(DateTime date)
        {
            CascadeThemePreset seasonal = CascadeThemePreset.ForDate(date);
            return seasonal != null ? seasonal.Name : "No seasonal match - Default";
        }

        private static string DescribeRange(CascadeThemePreset preset)
        {
            return MonthDay(preset.StartMonth, preset.StartDay) + " to " +
                   MonthDay(preset.EndMonth, preset.EndDay);
        }

        private static string MonthDay(int month, int day)
        {
            string[] names =
            {
                "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
            };
            int index = Mathf.Clamp(month - 1, 0, 11);
            return names[index] + " " + Mathf.Clamp(day, 1, 31);
        }
    }
}
