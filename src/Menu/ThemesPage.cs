using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using UnityEngine;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Desktop;
using CascadeMenu.Mods.Flight;
using CascadeMenu.Mods.Fun;
using CascadeMenu.Mods.Game;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Performance;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Snake;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Visual;

namespace CascadeMenu.Menu
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
                .LiveLabel("Current theme", "Saved to the config file", () => menu.Theme.Name, () => true);

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
                    "Drag the circular color wheel or sliders below. Edits apply live.")
                .Dropdown("Color to edit", "Choose which UI role the wheel, sliders and hex box change",
                    names,
                    () => menu.CustomColorIndex,
                    index => menu.CustomColorIndex = index)
                .LiveLabel("What it changes", "Live UI area affected by the selected color role",
                    () => CascadeThemePalette.Names[menu.CustomColorIndex] + " · " + menu.CustomHex(menu.CustomColorIndex),
                    () => true)
                .ColorWheel("Circular RGB color palette", "Drag anywhere on the color wheel for Hue/Saturation and use the bar for Brightness",
                    () => menu.CustomColor(menu.CustomColorIndex),
                    color => menu.SetCustomColor(menu.CustomColorIndex, color),
                    () => "Editing: " + CascadeThemePalette.Names[menu.CustomColorIndex])
                .Toggle("RGB Rainbow Mode", "Smoothly cycle your menu accent, outer glow, and sub-tab bar through RGB",
                    () => menu.RgbAccentEnabled,
                    menu.SetRgbAccentEnabled)
                .Dropdown("RGB target", "Choose which UI elements animate with RGB Rainbow Mode",
                    new[] { "Accent + Glow + Sub-Tab", "Full Neon (+ Borders & Active)", "Glow & Sub-Tab Only" },
                    () => menu.RgbTargetMode,
                    menu.SetRgbTargetMode)
                .Slider("RGB speed", "How fast the RGB rainbow cycles",
                    0.05f, 2.0f,
                    () => menu.RgbAccentSpeed,
                    menu.SetRgbAccentSpeed,
                    v => v.ToString("0.00") + "x")
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
                .ThemePreview("Live UI Preview & Selected Target", "Left: live mini UI · Right: exact element & color being changed");
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
