using CascadeMenu.Mods;
using CascadeMenu.UI;
using UnityEngine.InputSystem;

namespace CascadeMenu
{
    /// <summary>
    /// One settings page per module.
    ///
    /// The module tabs (Movement, Visual, Fun) only keep their on/off switches.
    /// Every tuning value lives here instead, in a page that is built only when
    /// the user presses the small gear next to a row or a button in
    /// Settings -> Module settings. Pages are identified by a tag
    /// ("cascade.settings.fly", ...), so the last visited one is restored like
    /// any other tab.
    /// </summary>
    internal static class ModuleSettings
    {
        internal const string TagPrefix = "cascade.settings.";

        /// <summary>A module that has its own settings page.</summary>
        internal sealed class Module
        {
            public readonly string Key;
            public readonly string Title;
            public readonly string Description;
            public readonly CascadeIcon Icon;

            public Module(string key, string title, string description, CascadeIcon icon)
            {
                Key = key;
                Title = title;
                Description = description;
                Icon = icon;
            }
        }

        /// <summary>Order used by the Settings tab listing.</summary>
        internal static readonly Module[] Modules =
        {
            new Module("fly", "Flight settings", "Speed, direction and stick mapping", CascadeIcon.Sliders),
            new Module("acrobatics", "Acrobatics settings", "Body rotation, smoothing and its key", CascadeIcon.Recenter),
            new Module("esp", "ESP settings", "Boxes, names, tracers and range", CascadeIcon.Eye),
            new Module("hud", "HUD settings", "What the in-world overlay shows", CascadeIcon.Layers),
            new Module("gun", "Gun settings", "Target gun behaviour and ring", CascadeIcon.Person),
            new Module("cosmetics", "Cosmetic settings", "Cosmetic ESP, alerts and shortcuts", CascadeIcon.Sparkle)
        };

        internal static string Tag(string key)
        {
            return TagPrefix + (key ?? string.Empty);
        }

        internal static bool IsSettingsTag(string tag)
        {
            return !string.IsNullOrEmpty(tag) && tag.StartsWith(TagPrefix, System.StringComparison.Ordinal);
        }

        internal static Module Find(string key)
        {
            for (int i = 0; i < Modules.Length; i++)
            {
                if (Modules[i].Key == key) return Modules[i];
            }
            return Modules[0];
        }

        /// <summary>Builds the settings page for one module key.</summary>
        internal static CascadePage Build(string key, CascadeMenuBehaviour menu, MenuConfig config)
        {
            string moduleKey = key ?? "fly";
            Module module = Find(moduleKey);
            var page = new CascadePage(module.Title, module.Description, module.Icon);
            page.Tag = Tag(module.Key);
            page.HiddenFromSidebar = true;

            switch (moduleKey)
            {
                case "acrobatics":
                    BuildAcrobatics(page, menu, config);
                    break;
                case "esp":
                    BuildEsp(page, menu, config);
                    break;
                case "hud":
                    BuildHud(page, menu, config);
                    break;
                case "gun":
                    BuildGun(page, menu, config);
                    break;
                case "cosmetics":
                    BuildCosmetics(page, menu, config);
                    break;
                default:
                    BuildFlight(page, menu, config);
                    break;
            }

            return page;
        }

        // -----------------------------------------------------------------
        // Flight
        // -----------------------------------------------------------------
        private static void BuildFlight(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Flight", "Applies the next time you fly")
                .Label("Flying", "Current flight state", JoystickFlyController.IsFlying ? "ON" : "OFF")
                .Button("Toggle flight now", "Same as double-tapping A / F5", "Toggle",
                    () =>
                    {
                        bool handled = JoystickFlyController.ToggleFromMenu();
                        if (!handled)
                        {
                            menu.ShowToast("Joystick fly", "Enable the master switch on the Movement tab first.");
                        }
                        menu.QueueRebuild();
                    }, true)
                .Slider("Fly speed", "Metres per second while flying",
                    1.5f, 15f,
                    () => config.FlySpeed.Value,
                    value => config.FlySpeed.Value = value,
                    value => value.ToString("0.0") + " m/s")
                .Slider("Fly vertical speed", "Altitude speed for the vertical stick (m/s)",
                    1.5f, 12f,
                    () => config.FlyVerticalSpeed.Value,
                    value => config.FlyVerticalSpeed.Value = value,
                    value => value.ToString("0.0") + " m/s")
                .Slider("Fly acceleration", "How smoothly flight reaches speed (m/s²)",
                    1f, 30f,
                    () => config.FlyAcceleration.Value,
                    value => config.FlyAcceleration.Value = value,
                    value => value.ToString("0.0") + " m/s²")
                .Slider("Stick deadzone", "Ignore small stick drift",
                    0.05f, 0.4f,
                    () => config.FlyDeadzone.Value,
                    value => config.FlyDeadzone.Value = value,
                    value => value.ToString("0.00"));

            page.Section("Direction", "Left stick moves; right stick Y changes altitude.")
                .Dropdown("Direction source", "Choose whether movement follows your head or hand aim",
                    new[] { "Head / camera", "Right controller" },
                    () => config.FlyDirection.Value,
                    index =>
                    {
                        config.FlyDirection.Value = index;
                        menu.QueueRebuild();
                    })
                .Toggle("Invert up/down", "Vertical stick reversed when enabled",
                    () => config.FlyInvertVertical.Value,
                    value => config.FlyInvertVertical.Value = value)
                .Toggle("Invert forward/back", "Forward stick reversed when enabled",
                    () => config.FlyInvertForward.Value,
                    value => config.FlyInvertForward.Value = value)
                .Toggle("Invert strafe", "Left/right reversed when enabled",
                    () => config.FlyInvertStrafe.Value,
                    value => config.FlyInvertStrafe.Value = value)
                .Label("Hotkeys", "F6 up/down, F7 forward, F8 strafe, F9 direction", "F5 = fly");

            AddBack(page, menu, "Movement");
        }

        // -----------------------------------------------------------------
        // Acrobatics
        // -----------------------------------------------------------------
        private static void BuildAcrobatics(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Body rotation", "VR only - the menu stays level while your body rotates")
                .Toggle("Acrobatics", "Arm it here, then double-tap A in game to engage",
                    () => config.Acrobatics.Value,
                    value => AcrobaticsController.SetActive(value))
                .Button("Toggle acrobatics now", "Arm or disarm the in-game double-tap gesture", "Toggle",
                    () =>
                    {
                        bool active = AcrobaticsController.ToggleFromMenu();
                        menu.ShowToast("Acrobatics", active
                            ? "Armed - double-tap A in game."
                            : "Off.");
                        menu.QueueRebuild();
                    }, false)
                .Slider("Rotation smoothing", "How quickly the body follows the head",
                    1f, 20f,
                    () => config.AcrobaticsSmoothing.Value,
                    value => config.AcrobaticsSmoothing.Value = value,
                    value => value.ToString("0.0"));

            page.Section("Binding", "Change what arms acrobatics on PC or in VR")
                .Button("Acrobatics key",
                    "Keyboard or controller bind (currently " +
                    Keybinds.Name(Keybinds.Parse(config.AcrobaticsKey.Value, Key.Delete)) + ")",
                    "Rebind", () => menu.BeginRebind("acrobatics"), false)
                .Dropdown("Acrobatics button", "VR button that arms acrobatics",
                    VrInput.ButtonNames,
                    () => VrInput.IndexOf(config.AcrobaticsButton.Value),
                    index => config.AcrobaticsButton.Value = VrInput.Format(VrInput.FromIndex(index)))
                .Label("Default", "Right stick click", "Right Stick")
                .Label("Engage", "Double-tap A in game while armed", "2x A")
                .Label("Menu behaviour", "The window never copies head roll", "stays upright");

            AddBack(page, menu, "Movement");
        }

        // -----------------------------------------------------------------
        // ESP
        // -----------------------------------------------------------------
        private static void BuildEsp(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Overlays", "Each overlay works independently")
                .Toggle("ESP Box", "Box around each player",
                    () => config.EspEnabled.Value,
                    value => config.EspEnabled.Value = value)
                .Toggle("ESP names", "Name above each player",
                    () => config.EspNames.Value,
                    value => config.EspNames.Value = value)
                .Toggle("ESP tracers", "Line from your hand to each player",
                    () => config.EspTracers.Value,
                    value => config.EspTracers.Value = value)
                .Toggle("ESP chams", "Highlight bodies through walls",
                    () => config.EspChams.Value,
                    value => config.EspChams.Value = value)
                .Toggle("ESP marker", "Through-wall diamond above each player",
                    () => config.EspMarker.Value,
                    value => config.EspMarker.Value = value);

            page.Section("Range and color")
                .Slider("ESP range", "Maximum distance to draw a player",
                    3f, 80f,
                    () => config.EspRange.Value,
                    value => config.EspRange.Value = value,
                    value => value.ToString("0") + " m")
                .Label("Box / tracer colors", "Boxes are green, tracers blue", "fixed")
                .Dropdown("Highlight color", "Used by chams and through-wall markers",
                    new[] { "Cyan", "Green", "Orange", "Magenta" },
                    () => config.EspColorIndex.Value,
                    index => config.EspColorIndex.Value = index)
                .Toggle("Hand tap badge", "Show TAP in the player list",
                    () => config.HandTapIndicator.Value,
                    value => config.HandTapIndicator.Value = value);

            page.Section("Cosmetic labels")
                .Toggle("Cosmetic ESP", "Show recognized special cosmetic roles above players",
                    () => config.EspCosmeticTags.Value,
                    value => config.EspCosmeticTags.Value = value)
                .Toggle("Cosmetic spoof check", "Show each worn cosmetic, its category and spoof flags on player pages",
                    () => config.CosmeticCheck.Value,
                    value => config.CosmeticCheck.Value = value);

            AddBack(page, menu, "Visual");
        }

        // -----------------------------------------------------------------
        // HUD
        // -----------------------------------------------------------------
        private static void BuildHud(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("HUD", "Compact overlay drawn in the world")
                .Toggle("HUD enabled", "Master switch for the whole overlay",
                    () => config.HudEnabled.Value,
                    value => config.HudEnabled.Value = value)
                .Toggle("HUD mod name", "Show the Cascade name",
                    () => config.HudModName.Value,
                    value => config.HudModName.Value = value)
                .Toggle("HUD player name", "Show your name",
                    () => config.HudPlayerName.Value,
                    value => config.HudPlayerName.Value = value)
                .Toggle("HUD FPS", "Show frames per second",
                    () => config.HudFps.Value,
                    value => config.HudFps.Value = value)
                .Toggle("HUD ArrayList", "Show active mods and targets",
                    () => config.HudArrayList.Value,
                    value => config.HudArrayList.Value = value);

            page.Section("Note", "The HUD is drawn by the overlay, not by this window.")
                .Label("Position", "Top left of your view", "fixed");

            AddBack(page, menu, "Visual");
        }

        // -----------------------------------------------------------------
        // Gun
        // -----------------------------------------------------------------
        private static void BuildGun(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Target gun", "Thin client-side player gun")
                .Toggle("Target gun", "Enable the gun on your right hand",
                    () => config.GunEnabled.Value,
                    value => config.GunEnabled.Value = value)
                .Toggle("Gun target ring", "Draw a ring on the locked player",
                    () => config.GunShowMarker.Value,
                    value => config.GunShowMarker.Value = value)
                .Toggle("Gun sticky target", "Keep the lock when aim slips",
                    () => config.GunSticky.Value,
                    value => config.GunSticky.Value = value)
                .Dropdown("Gun module", "Behaviour on trigger",
                    new[] { "Select", "Mark", "Copy Name" },
                    () => config.GunModule.Value,
                    index =>
                    {
                        config.GunModule.Value = index;
                        menu.QueueRebuild();
                    });

            AddBack(page, menu, "Fun");
        }

        // -----------------------------------------------------------------
        // Cosmetics
        // -----------------------------------------------------------------
        private static void BuildCosmetics(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Recognition", "Cosmetic roles and spoof detection")
                .Toggle("Cosmetic ESP", "Show recognized special cosmetic roles above players",
                    () => config.EspCosmeticTags.Value,
                    value => config.EspCosmeticTags.Value = value)
                .Toggle("Cosmetic notifications", "Show a standalone toast when a role cosmetic is detected",
                    () => config.CosmeticNotifications.Value,
                    value => config.CosmeticNotifications.Value = value)
                .Toggle("Cosmetic spoof check", "Show each worn cosmetic, its category and spoof flags on player pages",
                    () => config.CosmeticCheck.Value,
                    value => config.CosmeticCheck.Value = value);

            page.Section("Browse", "The catalog is a read-only view of the real wardrobe")
                .Button("Open Cosmetics", "Browse categories and items", "Open",
                    menu.OpenCosmeticsPage, true)
                .Button("Open Shop", "Try on and build a cart", "Open",
                    menu.OpenCosmeticsShop, false);

            AddBack(page, menu, "Visual");
        }

        // -----------------------------------------------------------------
        // Shared
        // -----------------------------------------------------------------
        private static void AddBack(CascadePage page, CascadeMenuBehaviour menu, string moduleTitle)
        {
            page.Section("Module", "Back to the switches for this module")
                .Button("Back to " + moduleTitle, "Return to the " + moduleTitle + " tab", "Back",
                    () => menu.OpenPage(null, moduleTitle), false);
        }
    }
}
