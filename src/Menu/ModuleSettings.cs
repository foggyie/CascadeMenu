using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;
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
    /// Dedicated settings pages for each module and individual visual/ESP mod.
    ///
    /// The module tabs (Movement, Visual, Fun) keep clean on/off switches, while
    /// clicking the gear next to any visual mod opens its own specialized UI panel.
    /// </summary>
    internal static class ModuleSettings
    {
        internal const string TagPrefix = "cascade.settings.";

        private static readonly string[] EspColorPresetNames =
        {
            "Cyan", "Green", "Orange", "Magenta", "Theme Accent", "Rainbow"
        };

        private static readonly string[] EspFilterModeNames =
        {
            "All Players", "Infected / Tagged Only", "Not Infected (Survivors) Only"
        };

        /// <summary>A module or visual mod that has its own settings page.</summary>
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

        /// <summary>Order used by the Settings tab listing and gear lookups.</summary>
        internal static readonly Module[] Modules =
        {
            new Module("fly", "Flight settings", "Speed, direction and stick mapping", CascadeIcon.Sliders),
            new Module("acrobatics", "Acrobatics settings", "Body rotation, smoothing and its key", CascadeIcon.Recenter),
            new Module("esp", "ESP master settings", "Global ESP filter, infected rules, colors and range", CascadeIcon.Eye),
            new Module("esp.box", "Box ESP settings", "3D wireframe, corner brackets, 2D billboard and colors", CascadeIcon.Cube),
            new Module("esp.skeleton", "Skeleton ESP settings", "Bone chains, finger joints, thickness and infected filter", CascadeIcon.Person),
            new Module("esp.tracers", "Tracer ESP settings", "Snapline origin, thickness, player color and infected filter", CascadeIcon.Bolt),
            new Module("esp.chams", "Chams ESP settings", "Through-wall body shader, player color and infected highlight", CascadeIcon.Palette),
            new Module("esp.names", "Name ESP settings", "Through-wall name tags, distance readout and infected badge", CascadeIcon.Eye),
            new Module("esp.marker", "Marker ESP settings", "Overhead diamond beacon, thickness and infected filter", CascadeIcon.Sparkle),
            new Module("hud", "HUD & Overlay settings", "Configure every mod that has an in-world HUD", CascadeIcon.Layers),
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
                    BuildEspMaster(page, menu, config);
                    break;
                case "esp.box":
                    BuildEspBox(page, menu, config);
                    break;
                case "esp.skeleton":
                    BuildEspSkeleton(page, menu, config);
                    break;
                case "esp.tracers":
                    BuildEspTracers(page, menu, config);
                    break;
                case "esp.chams":
                    BuildEspChams(page, menu, config);
                    break;
                case "esp.names":
                    BuildEspNames(page, menu, config);
                    break;
                case "esp.marker":
                    BuildEspMarker(page, menu, config);
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
                .Button("Toggle flight now", "Same as the Movement switch", "Toggle",
                    () =>
                    {
                        bool handled = JoystickFlyController.ToggleFromMenu();
                        if (!handled)
                        {
                            menu.ShowToast("Joystick fly", "Flight is not ready yet.");
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
                .Toggle("Acrobatics", "Turn body rotation on or off",
                    () => config.Acrobatics.Value,
                    value => AcrobaticsController.SetActive(value))
                .Button("Toggle acrobatics now", "Same as the Body rotation switch", "Toggle",
                    () =>
                    {
                        bool active = AcrobaticsController.ToggleFromMenu();
                        menu.ShowToast("Acrobatics", active ? "On." : "Off.");
                        menu.QueueRebuild();
                    }, false)
                .Slider("Rotation smoothing", "How quickly the body follows the head",
                    1f, 20f,
                    () => config.AcrobaticsSmoothing.Value,
                    value => config.AcrobaticsSmoothing.Value = value,
                    value => value.ToString("0.0"));

            AddBack(page, menu, "Movement");
        }

        // -----------------------------------------------------------------
        // Dedicated Per-Visual-Mod Settings Panels
        // -----------------------------------------------------------------

        private static void AppendSharedEspFilterAndColor(CascadePage page, MenuConfig config)
        {
            page.Section("Infected & Player Filtering", "Filter or highlight players by infection / tag state")
                .Dropdown("Player filter", "Show all players, only infected/tagged players, or only survivors",
                    EspFilterModeNames,
                    () => Mathf.Clamp(config.EspFilterMode.Value, 0, 2),
                    index => config.EspFilterMode.Value = Mathf.Clamp(index, 0, 2))
                .Toggle("Highlight infected in red-orange", "Override infected / tagged player ESP color with fiery red-orange",
                    () => config.EspInfectedColor.Value,
                    value => config.EspInfectedColor.Value = value)
                .Toggle("Match player color", "Color non-infected players with their actual gorilla color",
                    () => config.EspMatchPlayerColor.Value,
                    value => config.EspMatchPlayerColor.Value = value)
                .Dropdown("Fallback preset color", "Used when Match player color is turned off",
                    EspColorPresetNames,
                    () => Mathf.Clamp(config.EspColorIndex.Value, 0, 5),
                    index => config.EspColorIndex.Value = Mathf.Clamp(index, 0, 5))
                .Slider("ESP range", "Maximum distance in metres to render ESP",
                    3f, 80f,
                    () => config.EspRange.Value,
                    value => config.EspRange.Value = value,
                    value => value.ToString("0") + " m");
        }

        private static void BuildEspBox(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Box ESP", "Through-wall bounding boxes around remote players")
                .Hero("Box ESP", "3D wireframe, 3D corner brackets or 2D camera-facing billboard", "VISUAL")
                .Toggle("Enable Box ESP", "Draw bounding boxes around players",
                    () => config.EspEnabled.Value,
                    value => config.EspEnabled.Value = value)
                .Chips("Box geometry", new[] { "3D Wireframe", "3D Corner Brackets", "2D Billboard" },
                    () => Mathf.Clamp(config.EspBoxStyle.Value, 0, 2),
                    index => config.EspBoxStyle.Value = Mathf.Clamp(index, 0, 2))
                .Slider("Box line thickness", "Thickness multiplier for box edges",
                    0.4f, 3f,
                    () => config.EspLineWidth.Value,
                    value => config.EspLineWidth.Value = value,
                    value => value.ToString("0.00") + "x");

            AppendSharedEspFilterAndColor(page, config);
            AddBack(page, menu, "Visual");
        }

        private static void BuildEspSkeleton(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Skeleton ESP", "Full anatomical bone hierarchy + 3-joint finger chains")
                .Hero("Skeleton ESP", "Live rig bone tracking with thumb, index and middle finger phalanges", "BONES")
                .Toggle("Enable Skeleton ESP", "Draw bone lines across head, spine, arms and legs",
                    () => config.EspSkeleton.Value,
                    value => config.EspSkeleton.Value = value)
                .Toggle("Include finger bones", "Draw 3-joint thumb, index and middle finger skeletons on both hands",
                    () => config.EspSkeletonFingers.Value,
                    value => config.EspSkeletonFingers.Value = value)
                .Slider("Bone line thickness", "Thickness multiplier for body and finger bones",
                    0.4f, 3f,
                    () => config.EspLineWidth.Value,
                    value => config.EspLineWidth.Value = value,
                    value => value.ToString("0.00") + "x");

            AppendSharedEspFilterAndColor(page, config);
            AddBack(page, menu, "Visual");
        }

        private static void BuildEspTracers(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Tracer ESP", "Direct snaplines connecting you to every tracked player")
                .Hero("Tracer ESP", "Through-wall target snaplines with customizable anchor origin", "LINES")
                .Toggle("Enable Tracer ESP", "Draw tracer beams to players",
                    () => config.EspTracers.Value,
                    value => config.EspTracers.Value = value)
                .Chips("Tracer origin", new[] { "Right Hand", "Left Hand", "Screen Bottom", "Screen Center" },
                    () => Mathf.Clamp(config.EspTracerOrigin.Value, 0, 3),
                    index => config.EspTracerOrigin.Value = Mathf.Clamp(index, 0, 3))
                .Slider("Tracer thickness", "Thickness multiplier for tracer beams",
                    0.4f, 3f,
                    () => config.EspLineWidth.Value,
                    value => config.EspLineWidth.Value = value,
                    value => value.ToString("0.00") + "x");

            AppendSharedEspFilterAndColor(page, config);
            AddBack(page, menu, "Visual");
        }

        private static void BuildEspChams(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Chams ESP", "Depth-independent unlit shader applied to player meshes")
                .Hero("Chams ESP", "Full-body X-ray highlight visible through walls and terrain", "SHADER")
                .Toggle("Enable Chams ESP", "Highlight player bodies through walls",
                    () => config.EspChams.Value,
                    value => config.EspChams.Value = value);

            AppendSharedEspFilterAndColor(page, config);
            AddBack(page, menu, "Visual");
        }

        private static void BuildEspNames(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Name ESP", "Through-wall floating nameplates above players")
                .Hero("Name ESP", "Crisp outlined player callsigns with live range and infection status", "TAGS")
                .Toggle("Enable Name ESP", "Show player names through walls",
                    () => config.EspNames.Value,
                    value => config.EspNames.Value = value)
                .Toggle("Show distance in metres", "Append [12m] distance readout next to each player name",
                    () => config.EspShowDistance.Value,
                    value => config.EspShowDistance.Value = value)
                .Toggle("Show [INFECTED] tag", "Append [INFECTED] badge to infected or tagged players",
                    () => config.EspShowInfectedTag.Value,
                    value => config.EspShowInfectedTag.Value = value);

            AppendSharedEspFilterAndColor(page, config);
            AddBack(page, menu, "Visual");
        }

        private static void BuildEspMarker(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Marker ESP", "3D diamond prism beacon floating above each player's head")
                .Hero("Marker ESP", "High-visibility geometric diamond crown visible through walls", "BEACON")
                .Toggle("Enable Marker ESP", "Draw through-wall diamond markers above players",
                    () => config.EspMarker.Value,
                    value => config.EspMarker.Value = value)
                .Slider("Marker thickness", "Line thickness multiplier for the diamond beacon",
                    0.4f, 3f,
                    () => config.EspLineWidth.Value,
                    value => config.EspLineWidth.Value = value,
                    value => value.ToString("0.00") + "x");

            AppendSharedEspFilterAndColor(page, config);
            AddBack(page, menu, "Visual");
        }

        // -----------------------------------------------------------------
        // Master ESP Overview Page
        // -----------------------------------------------------------------
        private static void BuildEspMaster(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Individual Visual Mod Panels", "Open the dedicated UI panel for any ESP visual mod")
                .Button("Box ESP panel", "3D Box, Corner Box, 2D Billboard & thickness", "Open",
                    () => menu.OpenModuleSettings("esp.box"), true)
                .Button("Skeleton ESP panel", "Full bone hierarchy & 3-joint finger skeletons", "Open",
                    () => menu.OpenModuleSettings("esp.skeleton"), false)
                .Button("Tracer ESP panel", "Hand or screen snaplines & thickness", "Open",
                    () => menu.OpenModuleSettings("esp.tracers"), false)
                .Button("Chams ESP panel", "Through-wall full-body shader & colors", "Open",
                    () => menu.OpenModuleSettings("esp.chams"), false)
                .Button("Name ESP panel", "Names, distance readout & [INFECTED] tag", "Open",
                    () => menu.OpenModuleSettings("esp.names"), false)
                .Button("Marker ESP panel", "Through-wall overhead diamond beacon", "Open",
                    () => menu.OpenModuleSettings("esp.marker"), false);

            AppendSharedEspFilterAndColor(page, config);

            page.Section("Quick Switches", "Toggle individual ESP layers directly")
                .Toggle("ESP Box", "Box around each player",
                    () => config.EspEnabled.Value,
                    value => config.EspEnabled.Value = value)
                .Toggle("ESP Skeleton", "Bone skeleton and finger joints on each player",
                    () => config.EspSkeleton.Value,
                    value => config.EspSkeleton.Value = value)
                .Toggle("ESP Names", "Name above each player",
                    () => config.EspNames.Value,
                    value => config.EspNames.Value = value)
                .Toggle("ESP Tracers", "Line to each player",
                    () => config.EspTracers.Value,
                    value => config.EspTracers.Value = value)
                .Toggle("ESP Chams", "Highlight bodies through walls",
                    () => config.EspChams.Value,
                    value => config.EspChams.Value = value)
                .Toggle("ESP Marker", "Through-wall diamond above each player",
                    () => config.EspMarker.Value,
                    value => config.EspMarker.Value = value)
                .Toggle("Hand tap badge", "Show TAP in the player list",
                    () => config.HandTapIndicator.Value,
                    value => config.HandTapIndicator.Value = value);

            AddBack(page, menu, "Visual");
        }

        // -----------------------------------------------------------------
        // HUD & Overlays Hub
        // -----------------------------------------------------------------
        private static void BuildHud(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Main Cascade HUD", "Compact status pill drawn in the top-left of your view")
                .Toggle("HUD enabled", "Master switch for the main Cascade HUD",
                    () => config.HudEnabled.Value,
                    value => config.HudEnabled.Value = value)
                .Toggle("Show mod title", "Display Cascade v1.0 header",
                    () => config.HudModName.Value,
                    value => config.HudModName.Value = value)
                .Toggle("Show player name", "Display your local player nickname",
                    () => config.HudPlayerName.Value,
                    value => config.HudPlayerName.Value = value)
                .Toggle("Show FPS counter", "Display live frames per second",
                    () => config.HudFps.Value,
                    value => config.HudFps.Value = value)
                .Toggle("Show Active Mods ArrayList", "List active mods and flight status",
                    () => config.HudArrayList.Value,
                    value => config.HudArrayList.Value = value);

            CascadeSection rigHuds = page.Section("Player & Voice HUDs", "World-space cards and voice activity monitors");
            AddHudModRow(rigHuds, menu, "Player HUD", "Detailed 3D telemetry card floating above each player");
            AddHudModRow(rigHuds, menu, "Name Tags", "Clean overhead nameplate with platform subtitle");
            AddHudModRow(rigHuds, menu, "Talking Players", "Live speaker list tinted with each speaker's gorilla color");

            CascadeSection customHuds = page.Section("Interactive & Graphical HUDs", "Custom instruments, gauges, radar and graphs");
            AddHudModRow(customHuds, menu, "Frosted Glass HUD", "Head or wrist frosted glass HUD with hand-to-target tracer beam");
            AddHudModRow(customHuds, menu, "Proximity Radar", "360° circular radar scanner with rotating sweep & player blips");
            AddHudModRow(customHuds, menu, "Input Visualizer", "Live controller thumbstick pads, trigger bars & button lights");
            AddHudModRow(customHuds, menu, "Compass", "Sliding cardinal heading strip at the top of your view");
            AddHudModRow(customHuds, menu, "Speedometer", "Digital velocity gauge with color-shifting pace bar");
            AddHudModRow(customHuds, menu, "FPS Graph", "60-bar scrolling frame-rate histogram");
            AddHudModRow(customHuds, menu, "Clock", "Corner digital wall clock with optional date");
            AddHudModRow(customHuds, menu, "Crosshair", "Center-of-view dot, cross or ring reticle");
            AddHudModRow(customHuds, menu, "Session Stats", "Room population, region, FPS and session uptime badge");

            CascadeSection infoHuds = page.Section("Telemetry Readout HUDs", "Distinct themed telemetry badges");
            string[] readouts =
            {
                "Coordinates", "Altitude", "Attitude", "Velocity", "Hand Speed",
                "Distance Walked", "Jump Counter", "Room Info", "Session Clock",
                "Frame Time", "Memory", "Active Mods", "Keybinds", "Target Info",
                "Room Props", "Water State", "Ground State", "Input Monitor",
                "Time Scale", "Build Stamp"
            };
            for (int i = 0; i < readouts.Length; i++)
            {
                AddHudModRow(infoHuds, menu, readouts[i], "Toggle or click the gear to customize position, scale & opacity");
            }

            AddBack(page, menu, "Settings");
        }

        internal static void AddHudModRow(CascadeSection section, CascadeMenuBehaviour menu, string modName, string subtitle)
        {
            if (section == null) return;
            Mod mod = ModRegistry.Find(modName);
            if (mod == null) return;

            Mod captured = mod;
            System.Action openSettings = captured.HasSettings && menu != null
                ? (System.Action)(() => menu.OpenModSettings(captured))
                : null;

            section.Toggle(
                captured.Name,
                subtitle,
                () => captured.Enabled && captured.IsActive,
                value => ModRegistry.SetEffect(captured, value),
                true,
                openSettings);
        }

        // -----------------------------------------------------------------
        // Gun
        // -----------------------------------------------------------------
        private static void BuildGun(CascadePage page, CascadeMenuBehaviour menu, MenuConfig config)
        {
            page.Section("Target gun", "Thin client-side player gun")
                .Toggle("Target gun", "Enable the gun on your right hand",
                    () => GunLibMod.IsArmed(config),
                    value => GunLibMod.SetArmed(config, value))
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

            page.Section("Grip and beam", "Everything else lives on the GunLib tab")
                .Toggle("Require grip", "Only aim while the selected grip is held",
                    () => config.GunRequireGrip.Value,
                    value => config.GunRequireGrip.Value = value)
                .Dropdown("Grip hand", "VR hand that arms the gun",
                    new[] { "Right", "Left", "Either" },
                    () => Mathf.Clamp(config.GunGripHand.Value, 0, 2),
                    index => config.GunGripHand.Value = Mathf.Clamp(index, 0, 2))
                .Button("Open GunLib tab", "Beam, ring, desktop grip and color controls", "Open",
                    () => menu.OpenPage(GunLibPage.Tag, "GunLib"), true);

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
            page.Section("Navigation", "Return to the previous tab")
                .Button("Back to " + moduleTitle, "Return to the " + moduleTitle + " tab", "Back",
                    () => menu.OpenPage(null, moduleTitle), false);
        }
    }
}
