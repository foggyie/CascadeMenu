using System;
using System.Collections.Generic;
using CascadeMenu.UI;

namespace CascadeMenu
{
    /// <summary>
    /// One toggleable mod. Adding a new toggle to the menu is a single line in
    /// <see cref="FeatureCatalog.Build"/>.
    /// </summary>
    internal sealed class Feature
    {
        public readonly string Category;   // "Fun", "Movement", "Visual"
        public readonly string Section;    // card inside the page
        public readonly string Title;
        public readonly string Subtitle;
        public readonly Func<bool> Get;
        public readonly Action<bool> Set;
        public readonly Func<bool> Enabled;

        /// <summary>Module settings key opened by the gear next to this toggle.</summary>
        public readonly string SettingsKey;

        public Feature(string category, string section, string title, string subtitle, Func<bool> get, Action<bool> set, Func<bool> enabled = null, string settingsKey = null)
        {
            Category = category;
            Section = section;
            Title = title;
            Subtitle = subtitle;
            Get = get;
            Set = set;
            Enabled = enabled;
            SettingsKey = settingsKey;
        }
    }

    /// <summary>
    /// Declarative list of every toggle in the menu, grouped into category pages.
    ///
    /// Usage:
    ///   FeatureCatalog.BuildPage(config, "Movement", "...", CascadeIcon.Sliders, menu);
    ///
    /// To add a mod, append one <c>new Feature(...)</c> below; the page, section
    /// and toggle row are created automatically. A SettingsKey adds a small gear
    /// that opens the matching page from <see cref="ModuleSettings"/>.
    /// </summary>
    internal static class FeatureCatalog
    {
        internal static List<Feature> Build(MenuConfig config)
        {
            return new List<Feature>
            {
                // ---------------------------------------------------------
                // Fun
                // ---------------------------------------------------------
                FeatureOf("Fun", "Toys", "Target gun", "Thin client-side player gun", config.GunEnabled, settings: "gun"),
                FeatureOf("Fun", "Toys", "Gun target ring", "Draw a ring on the locked player", config.GunShowMarker, settings: "gun"),

                // ---------------------------------------------------------
                // Movement
                // ---------------------------------------------------------
                FeatureOf("Movement", "Joystick fly", "Joystick fly (master)", "Arms double-tap A / F5 flight", config.JoystickFly, settings: "fly"),
                FeatureOf("Movement", "Gravity", "Block hand link", "Stop other players pulling you around", config.HandLinkBlock),
                new Feature("Movement", "Acrobatics", "Body rotation", "Press its bind to turn it on; it starts off",
                    () => config.Acrobatics.Value,
                    value => CascadeMenu.Mods.AcrobaticsController.SetActive(value),
                    settingsKey: "acrobatics"),

                // ---------------------------------------------------------
                // Visual (switches only; tuning lives in the gear pages)
                // ---------------------------------------------------------
                FeatureOf("Visual", "ESP", "ESP Box", "Box around each player", config.EspEnabled, settings: "esp"),
                FeatureOf("Visual", "ESP", "ESP names", "Name above each player", config.EspNames, settings: "esp"),
                FeatureOf("Visual", "ESP", "ESP tracers", "Line from your hand to each player", config.EspTracers, settings: "esp"),
                FeatureOf("Visual", "ESP", "ESP chams", "Highlight bodies through walls", config.EspChams, settings: "esp"),
                FeatureOf("Visual", "ESP", "ESP marker", "Through-wall diamond above each player", config.EspMarker, settings: "esp"),
                FeatureOf("Visual", "Cosmetics", "Cosmetic ESP", "Show recognized special cosmetic roles above players", config.EspCosmeticTags, settings: "cosmetics"),
                FeatureOf("Visual", "Cosmetics", "Cosmetic notifications", "Show a standalone toast when a role cosmetic is detected", config.CosmeticNotifications, settings: "cosmetics"),
                FeatureOf("Visual", "ESP", "Hand tap badge", "Show TAP in the player list", config.HandTapIndicator, settings: "esp"),
            };
        }

        /// <summary>Builds a whole category page from the catalog.</summary>
        internal static CascadePage BuildPage(
            MenuConfig config,
            string category,
            string subtitle,
            CascadeIcon icon,
            CascadeMenuBehaviour menu)
        {
            var page = new CascadePage(category, subtitle, icon);
            Apply(page, config, category, menu);
            return page;
        }

        /// <summary>Adds every catalog toggle of <paramref name="category"/> to a page.</summary>
        internal static void Apply(CascadePage page, MenuConfig config, string category, CascadeMenuBehaviour menu)
        {
            List<Feature> features = Build(config);
            string currentSection = null;
            CascadeSection section = null;

            for (int i = 0; i < features.Count; i++)
            {
                Feature feature = features[i];
                if (feature.Category != category) continue;

                if (section == null || feature.Section != currentSection)
                {
                    currentSection = feature.Section;
                    section = page.Section(feature.Section, null);
                }

                Feature captured = feature;
                Action openSettings = null;
                if (!string.IsNullOrEmpty(captured.SettingsKey) && menu != null)
                {
                    string key = captured.SettingsKey;
                    openSettings = () => menu.OpenModuleSettings(key);
                }

                section.Toggle(captured.Title, captured.Subtitle, captured.Get, captured.Set, true, openSettings);
            }
        }

        private static Feature FeatureOf(
            string category,
            string section,
            string title,
            string subtitle,
            BepInEx.Configuration.ConfigEntry<bool> entry,
            Func<bool> enabled = null,
            string settings = null)
        {
            return new Feature(category, section, title, subtitle, () => entry.Value, value => entry.Value = value, enabled, settings);
        }
    }
}
