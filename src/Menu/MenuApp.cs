using System.Collections.Generic;
using CascadeMenu.UI;
using UnityEngine;
using CascadeMenu.Mods.Sandbox;
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
    /// Builds the page tree. Every open rebuilds it so live data (players, room)
    /// is fresh. Categories are declared in <see cref="FeatureCatalog"/>.
    /// </summary>
    internal static class MenuApp
    {
        public static List<CascadePage> Build(CascadeMenuBehaviour menu, MenuConfig config, MenuState state)
        {
            var pages = new List<CascadePage>();
            pages.Add(BuildHome(menu, config));
            pages.Add(ThemesPage.Build(menu));
            pages.Add(GunLibPage.Build(menu, config));
            pages.Add(PlayerActions.BuildPlayersPage(menu));
            pages.Add(BuildNotifications(menu, config));
            pages.Add(BuildFun(menu, config));
            pages.Add(SandboxPage.Build(menu));
            pages.Add(BuildMovement(menu, config));
            pages.Add(BuildVisual(menu, config));
            pages.Add(CosmeticBrowser.BuildCategoriesPage(menu));
            pages.Add(CosmeticShop.BuildPage(menu));

            // One sidebar tab per discovered module category (Overlays, Tools,
            // Flight, Desktop, Performance, Super, ...). Skip categories that
            // are already merged into Fun, Movement, and Visual above.
            List<string> categories = ModRegistry.Categories();
            for (int i = 0; i < categories.Count; i++)
            {
                string cat = categories[i];
                if (cat == "Fun" || cat == "Movement" || cat == "Visual") continue;
                pages.Add(ModRegistry.BuildCategoryPage(menu, cat));
            }

            pages.Add(BuildUtilities(menu, config));
            pages.Add(ProfilerPage.Build(menu, config));
            if (config.DebugConsoleEnabled.Value)
            {
                pages.Add(CascadeMenu.Mods.Tools.DebugConsole.BuildPage(menu, config));
            }
            pages.Add(BuildSettings(menu, config));
            pages.Add(BuildAbout());
            pages.Add(BuildGallery(menu, state));
            return pages;
        }

        // -----------------------------------------------------------------
        // Home
        // -----------------------------------------------------------------
        private static CascadePage BuildHome(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var home = new CascadePage("Home", "Cascade for Gorilla Tag", CascadeIcon.Home);
            home.Section("Welcome", "A macOS Sequoia inspired interface, rebuilt for VR.")
                .Hero("Cascade", "Dark glass, traffic lights and native-feeling controls", "v1.0")
                .LiveLabel("Active mods", "Mods currently running in the background",
                    () => ModRegistry.ActiveCount + " active · " + ModRegistry.EnabledCount + " armed",
                    () => ModRegistry.ActiveCount > 0)
                .Button("Disable all active mods", "Emergency panic switch to disarm every running mod", "Panic Stop",
                    () =>
                    {
                        ModRegistry.DisableAll();
                        menu.ShowToast("Panic Stop", "All active mods have been disarmed.");
                    }, false)
                .Button("Re-center window", "Place the menu level, in front of you", "Recenter", menu.RecenterMenu, true)
                .Button("Send a notification", "Preview the Cascade toast style", "Notify",
                    () => menu.ShowToast("Cascade", "This is a notification."), false);

            // Live lobby details live here now; the Players tab is just the
            // roster and the per-player actions.
            PlayerActions.AppendRoomSection(home);

            home.Section("Appearance", "Seasonal presets and a custom palette")
                .Label("Current theme", "Applied to every tab and popup", menu.Theme.Name)
                .Button("Open Themes", "Presets, seasonal auto and the custom color picker", "Themes",
                    menu.OpenThemesPage, true);

            return home;
        }

        // -----------------------------------------------------------------
        // Notifications
        // -----------------------------------------------------------------
        private static CascadePage BuildNotifications(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var page = new CascadePage("Notifications", "Pick which events pop up, and where", CascadeIcon.Sparkle);
            page.Tag = "cascade.notifications";

            page.Section("Player events", "Shown even while the menu is closed")
                .Toggle("Player joins", "Someone enters the room",
                    () => config.NotifyJoins.Value,
                    value => config.NotifyJoins.Value = value)
                .Toggle("Player leaves", "Someone leaves the room",
                    () => config.NotifyLeaves.Value,
                    value => config.NotifyLeaves.Value = value)
                .Toggle("You become master", "You are now hosting the room",
                    () => config.NotifyMaster.Value,
                    value => config.NotifyMaster.Value = value)
                .Toggle("Master changes", "Another player becomes the host",
                    () => config.NotifyMasterChanges.Value,
                    value => config.NotifyMasterChanges.Value = value)
                .Toggle("Room changes", "You join a different room or code",
                    () => config.NotifyRoom.Value,
                    value => config.NotifyRoom.Value = value)
                .Toggle("Modder alerts", "A joining player has spoof-flagged cosmetics",
                    () => config.NotifyModder.Value,
                    value => config.NotifyModder.Value = value);

            page.Section("Cosmetic alerts", "Special cosmetic detection")
                .Toggle("Special cosmetic found", "Show a card when a special cosmetic is detected",
                    () => config.CosmeticNotifications.Value,
                    value => config.CosmeticNotifications.Value = value)
                .Toggle("Cosmetic ESP", "Tag special cosmetic roles above players",
                    () => config.EspCosmeticTags.Value,
                    value =>
                    {
                        config.EspCosmeticTags.Value = value;
                        menu.QueueRebuild();
                    });

            page.Section("Placement", "Bottom right keeps the card below eye level")
                .Dropdown("Position", "Where the notification card appears",
                    new[] { "Bottom right", "Top right", "Bottom left" },
                    () => Mathf.Clamp(config.NotificationPosition.Value, 0, 2),
                    index => config.NotificationPosition.Value = Mathf.Clamp(index, 0, 2))
                .Button("Test notification", "Preview the current placement", "Test",
                    () => StandaloneNotificationOverlay.Notify(
                        "Cascade", "Notifications are on. This is the current placement."), true)
                .Label("Saved", "Every toggle is stored in the config file", "BepInEx/config");

            return page;
        }

        // -----------------------------------------------------------------
        // Fun
        // -----------------------------------------------------------------
        private static CascadePage BuildFun(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var fun = FeatureCatalog.BuildPage(config, "Fun", "Toys and whole-lobby actions", CascadeIcon.Sparkle, menu);

            fun.Section("Lobby tools", "Client-side actions, applied immediately")
                .Button("Mark random player", "Lock the gun onto someone in the room", "Mark",
                    () =>
                    {
                        string name;
                        bool ok = FunActions.MarkRandom(out name);
                        menu.ShowToast("Mark", ok ? name + " marked." : "No player available.");
                    }, true)
                .Button("Clear selection", "Remove the list highlight and gun target", "Clear",
                    () =>
                    {
                        FunActions.ClearSelection();
                        menu.ShowToast("Selection", "Cleared.");
                    }, false)
                .Toggle("Teleport gun", "Aim anywhere and pull the trigger to teleport there",
                    () => ModRegistry.IsEnabledByName("Teleport Gun Enhanced"),
                    value => ModRegistry.SetEnabledByName("Teleport Gun Enhanced", value))
                .Button("Teleport to random player", "Move next to someone (client-side)", "Go",
                    () =>
                    {
                        NetPlayer player = FunActions.RandomPlayer();
                        if (player == null)
                        {
                            menu.ShowToast("Teleport", "No other players in this room.");
                            return;
                        }

                        bool moved = PlayerActions.TeleportTo(player);
                        menu.ShowToast("Teleport", moved
                            ? "Moved next to " + PlayerActions.SafeName(player)
                            : "That player's rig is not ready.");
                    }, false)
                .Button("Mute everyone", "Local manual mute for all players", "Mute",
                    () =>
                    {
                        int count = FunActions.MuteAll(true);
                        menu.ShowToast("Mute all", count + " player(s) muted.");
                        menu.RefreshPlayerList(true);
                    }, false)
                .Button("Unmute everyone", "Restore every manual mute", "Unmute",
                    () =>
                    {
                        int count = FunActions.MuteAll(false);
                        menu.ShowToast("Unmute all", count + " player(s) unmuted.");
                        menu.RefreshPlayerList(true);
                    }, false)
                .Button("Copy room code", "Clipboard: current room name", "Copy",
                    () => PlayerActions.Copy(FunActions.RoomName()), false)
                .Button("Copy all player names", "Clipboard: comma separated", "Copy",
                    () => PlayerActions.Copy(FunActions.AllNames()), false);

            Mod construction = ModRegistry.Find("Construction");
            if (construction != null)
            {
                Mod captured = construction;
                System.Action openSettings = captured.HasSettings && menu != null
                    ? (System.Action)(() => menu.OpenModSettings(captured))
                    : null;

                CascadeSection constructionSection = fun.Section("Construction",
                        "Physics pieces, welding and demolition")
                    .Toggle(captured.Name,
                        "Aim with the grip; trigger builds, welds or fires",
                        () => captured.Enabled && captured.IsActive,
                        value => ModRegistry.SetEffect(captured, value),
                        true,
                        openSettings)
                    .Button("Undo last piece", "Remove the newest piece in the build", "Undo",
                        () => Construction.UndoForMenu(), false)
                    .Button("Clear build", "Delete every construction piece and weld", "Clear",
                        () => Construction.ClearAllForMenu(), false);

                if (menu != null)
                {
                    constructionSection.Button("Construction settings",
                        "Tool, piece, welding, explosion and input options", "Open",
                        () => menu.OpenModSettings(captured), false);
                }
            }

            ModRegistry.AppendCategoryMods(fun, menu, "Fun");
            return fun;
        }

        // -----------------------------------------------------------------
        // Movement
        // -----------------------------------------------------------------
        private static CascadePage BuildMovement(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var movement = FeatureCatalog.BuildPage(config, "Movement", "Flight, stick mapping and gravity", CascadeIcon.Sliders, menu);

            // Tuning moved into dedicated pages: every switch above has a gear,
            // and these buttons give a second, obvious way in.
            movement.Section("Module settings", "Speed, direction, smoothing and keybinds")
                .Button("Flight settings", "Speed, stick mapping and direction", "Open",
                    () => menu.OpenModuleSettings("fly"), false)
                .Button("Acrobatics settings", "Smoothing and rotation controls", "Open",
                    () => menu.OpenModuleSettings("acrobatics"), false);

            ModRegistry.AppendCategoryMods(movement, menu, "Movement");
            return movement;
        }

        // -----------------------------------------------------------------
        // Visual
        // -----------------------------------------------------------------
        private static CascadePage BuildVisual(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var visual = FeatureCatalog.BuildPage(config, "Visual", "HUD, ESP and target markers", CascadeIcon.Eye, menu);

            // Both rig overlays draw on remote gorilla rigs, so they live
            // together as plain feature toggles (gear opens the mod settings).
            var rigs = visual.Section("Player tags (on rigs)",
                "Display-only labels and cards drawn above remote gorilla rigs");
            AddRigOverlayToggle(rigs, menu, "Name Tags",
                "Name plate above each player's gorilla rig");
            AddRigOverlayToggle(rigs, menu, "Player HUD",
                "Information card above each player's gorilla rig");

            // The talking panel follows the camera instead of a rig, so it
            // gets its own small section.
            var voice = visual.Section("Talking players",
                "A panel that follows your view and lists who is speaking");
            AddRigOverlayToggle(voice, menu, "Talking Players",
                "Speaker list with each player's color while they talk");

            visual.Section("Visual & ESP Settings Panels", "Each visual mod has its own dedicated configuration panel")
                .Button("Box ESP panel", "3D Box, Corner Brackets, 2D Billboard & thickness", "Open",
                    () => menu.OpenModuleSettings("esp.box"), true)
                .Button("Skeleton ESP panel", "Bone chains, 3-joint finger skeletons & thickness", "Open",
                    () => menu.OpenModuleSettings("esp.skeleton"), false)
                .Button("Tracer ESP panel", "Hand or screen snaplines & thickness", "Open",
                    () => menu.OpenModuleSettings("esp.tracers"), false)
                .Button("Chams ESP panel", "Through-wall full-body shader & colors", "Open",
                    () => menu.OpenModuleSettings("esp.chams"), false)
                .Button("Name ESP panel", "Names, distance readout & [INFECTED] badge", "Open",
                    () => menu.OpenModuleSettings("esp.names"), false)
                .Button("Marker ESP panel", "Through-wall overhead diamond beacon", "Open",
                    () => menu.OpenModuleSettings("esp.marker"), false)
                .Button("All ESP master settings", "Global infected filter, color matching & range", "Open",
                    () => menu.OpenModuleSettings("esp"), false)
                .Button("HUD & Overlay settings", "Configure every in-world HUD and telemetry widget", "Open",
                    () => menu.OpenModuleSettings("hud"), false)
                .Button("Cosmetic ESP settings", "Special cosmetic roles, alerts & spoof checker", "Open",
                    () => menu.OpenModuleSettings("cosmetics"), false);

            ModRegistry.AppendCategoryMods(visual, menu, "Visual");
            return visual;
        }

        /// <summary>
        /// Adds a rig overlay as a plain feature row (toggle plus gear), the
        /// same shape as the ESP rows. The gear opens the full mod settings,
        /// while the toggle starts or stops the effect immediately.
        /// </summary>
        private static void AddRigOverlayToggle(
            CascadeSection section,
            CascadeMenuBehaviour menu,
            string modName,
            string subtitle)
        {
            if (section == null) return;

            Mod mod = ModRegistry.Find(modName);
            if (mod == null) return;

            Mod captured = mod;
            System.Action openSettings = null;
            if (captured.HasSettings && menu != null)
            {
                openSettings = () => menu.OpenModSettings(captured);
            }

            section.Toggle(
                captured.Name,
                subtitle,
                () => captured.Enabled && captured.IsActive,
                value => ModRegistry.SetEffect(captured, value),
                true,
                openSettings);
        }
        
        private static CascadePage BuildUtilities(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var utilities = FeatureCatalog.BuildPage(config, "Utilities", "Things", CascadeIcon.Gear, menu);

            utilities.Section("BepInEx config", "Edits BepInEx/config/BepInEx.cfg directly")
                .Label("Config path", "Where BepInEx.cfg lives on disk", Util.GetBepInExCfgPath())
                .Toggle("Show console", "Asks to restart, then the console appears at startup",
                    () => Util.GetConsoleEnabled(),
                    value =>
                    {
                        Util.SetConsoleEnabled(value);

                        if (value)
                        {
                            // Offer the restart instead of just telling the player to do it.
                            menu.ShowConfirm(
                                "Restart required",
                                "The BepInEx console is only created at startup. Restart Gorilla Tag now with the console enabled?",
                                "Restart now",
                                () => Restart(menu, true));
                        }
                        else
                        {
                            menu.ShowToast("BepInEx.cfg", "Console disabled - restart to apply.");
                        }
                    })
                .Button("Restart with console", "Enables the console, quits and relaunches through Steam", "Restart",
                    () => menu.ShowConfirm(
                        "Restart with console",
                        "Gorilla Tag will close and relaunch through Steam in about 6 seconds with the BepInEx console visible.",
                        "Restart now",
                        () => Restart(menu, true)), false)
                .Button("Restart game", "Quit and relaunch through Steam", "Restart",
                    () => menu.ShowConfirm(
                        "Restart game",
                        "Gorilla Tag will close and relaunch through Steam in about 6 seconds.",
                        "Restart now",
                        () => Restart(menu, false)), false)
                .Button("Backup BepInEx.cfg", "Creates BepInEx.cfg.bak", "Backup",
                    () =>
                    {
                        bool ok = Util.BackupBepInExConfig();
                        menu.ShowToast("Backup", ok ? "Saved BepInEx.cfg.bak" : "BepInEx.cfg not found.");
                    }, false)
                .Button("Restore backup", "Overwrites BepInEx.cfg from the .bak", "Restore",
                    () =>
                    {
                        bool ok = Util.RestoreBepInExConfigBackup();
                        menu.ShowToast("Restore", ok ? "Restored from backup." : "No backup found.");
                    }, false);

            return utilities;
        }

        /// <summary>Restart helper shared by the Utilities buttons.</summary>
        private static void Restart(CascadeMenuBehaviour menu, bool withConsole)
        {
            if (!Util.RestartGame(withConsole))
            {
                menu.ShowToast("Restart", "Could not start the relaunch. Quit and start the game manually.");
            }
        }

        // -----------------------------------------------------------------
        // Settings
        // -----------------------------------------------------------------
        private static CascadePage BuildSettings(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var settings = new CascadePage("Settings", "Menu preferences, appearance, backgrounds and config", CascadeIcon.Gear);

            // 1. General (Save, Window & World Placement)
            settings.Section("General", "Auto-save, window controls, and world-space placement")
                .Toggle("Auto-save settings", "Automatically save menu, theme, and mod settings whenever changes occur",
                    () => config.AutoSave.Value,
                    value =>
                    {
                        config.AutoSave.Value = value;
                        if (value) menu.SaveAllSettings();
                    })
                .Slider("Auto-save interval", "Seconds between automatic saves when changes are pending",
                    2f, 60f,
                    () => config.AutoSaveInterval.Value,
                    value => config.AutoSaveInterval.Value = value,
                    value => Mathf.RoundToInt(value) + " s")
                .LiveLabel("Auto-save status", "Shows when your config was last auto-saved",
                    () => menu.AutoSaveStatusText(),
                    () => config.AutoSave.Value)
                .AddDual("Config actions", "Immediately save all settings or reset everything to defaults",
                    "Save Now", "Restore Defaults",
                    menu.SaveAllSettings, menu.RestoreDefaults, true)
                .Button("Re-center window", "Snap the menu level in front of you",
                    "Recenter", menu.RecenterMenu, false)
                .Slider("Distance", "Spawn distance in front of your head",
                    0.4f, 1.6f,
                    () => config.Distance.Value,
                    value => config.Distance.Value = value,
                    value => value.ToString("0.00") + " m")
                .Slider("Menu height", "Fixed world height, no head bob",
                    0.8f, 2.2f,
                    () => config.MenuHeight.Value,
                    value => config.MenuHeight.Value = value,
                    value => value.ToString("0.00") + " m")
                .Slider("Raise above head height", "Only above this height does the menu spawn higher",
                    1f, 2.5f,
                    () => config.MenuRaiseAbove.Value,
                    value => config.MenuRaiseAbove.Value = value,
                    value => value.ToString("0.00") + " m")
                .Slider("Re-anchor distance", "Walk further than this and the menu comes back (0 = never, 100+ for teleports)",
                    0f, 500f,
                    () => config.MenuReanchorDistance.Value,
                    value => config.MenuReanchorDistance.Value = value,
                    value => value <= 0.01f ? "off" : value.ToString("0") + " m")
                .Toggle("Smooth follow", "Optional: glide after the head instead of staying put",
                    () => config.FollowHead.Value,
                    value => config.FollowHead.Value = value);

            // 2. Appearance (Style, Scale, Sliders, RGB Rainbow)
            settings.Section("Appearance", "Menu layout, UI scale, big sliders, animations and RGB mode")
                .Dropdown("Menu style", "Switch between the macOS Cascade Window and the Glass Handheld Wrist & Dot menu",
                    new[] { "Cascade Window (macOS)", "Glass Handheld (Wrist & Dot)" },
                    () => config.MenuStyle.Value,
                    index => menu.SetMenuStyle(index))
                .Dropdown("Handheld mount", "Where the Glass Handheld menu attaches in VR",
                    new[] { "Wrist / Hand", "Floating In Front", "Wrist-Tap Toggle" },
                    () => config.HandheldAttach.Value,
                    index => config.HandheldAttach.Value = index)
                .Toggle("Handheld aura glow", "Soft glowing halo behind the Glass Handheld menu",
                    () => config.HandheldAura.Value,
                    value => config.HandheldAura.Value = value)
                .Slider("UI scale", "Scale the entire window smaller or larger",
                    0.8f, 1.3f,
                    () => config.Scale.Value,
                    value => config.Scale.Value = value,
                    value => value.ToString("0.00") + "x")
                .Toggle("Big sliders", "Use full-width chunky sliders that are easy to grab in VR & PC",
                    () => config.BigSliders.Value,
                    value =>
                    {
                        config.BigSliders.Value = value;
                        menu.QueueRebuild();
                    })
                .Toggle("Animations", "Sidebar transitions, page fades, orb drift and notifications",
                    () => config.Animations.Value,
                    value => config.Animations.Value = value)
                .Toggle("RGB Rainbow Mode", "Animate menu accent, outer glow, and sub-tab bar through the RGB spectrum",
                    () => menu.RgbAccentEnabled,
                    menu.SetRgbAccentEnabled)
                .Slider("RGB cycle speed", "Speed of the live RGB rainbow animation",
                    0.05f, 2.0f,
                    () => menu.RgbAccentSpeed,
                    menu.SetRgbAccentSpeed,
                    value => value.ToString("0.00") + "x")
                .Button("Collapse sidebar", "Toggle the left navigation rail (expands content area)",
                    "Toggle", menu.ToggleSidebar, false);

            // 3. Background (Custom PNG/JPG Upload via PowerShell + Clear / Frosted / Solid)
            string[] bgOptions = CascadeBackgroundManager.ListImageNames(config.BackgroundImagePath.Value);
            settings.Section("Background", "Upload PNG/JPG wallpapers from BepInEx/config/CascadeMenu/Backgrounds and adjust Clear, Frosted & Solid")
                .Dropdown("Surface preset", "Quick presets for window & background glass style (moving the slider switches to Custom Blend)",
                    new[] { "Frosted Glass", "Clear Transparent", "Solid Opaque", "Custom Blend" },
                    () => config.WindowSurfaceStyle.Value,
                    index =>
                    {
                        config.WindowSurfaceStyle.Value = index;
                        if (index == 0) config.WindowSolidness.Value = 0.55f;
                        else if (index == 1) config.WindowSolidness.Value = 0.10f;
                        else if (index == 2) config.WindowSolidness.Value = 1.00f;
                    })
                .Slider("Clear ↔ Solid", "Smoothly dial how clear, frosted, or solid both the window and background picture are",
                    0f, 1f,
                    () => config.WindowSolidness.Value,
                    value =>
                    {
                        config.WindowSolidness.Value = value;
                        config.WindowSurfaceStyle.Value = 3;
                    },
                    value =>
                    {
                        int pct = Mathf.RoundToInt(value * 100f);
                        string tag = value < 0.25f ? "Clear" : (value <= 0.80f ? "Frosted" : "Solid");
                        return pct + "% · " + tag;
                    })
                .Button("Upload background image", "Opens Windows File Explorer dialog via PowerShell to load any PNG/JPG into memory",
                    "Browse...", menu.BrowseBackgroundImage, true)
                .Dropdown("Background image", "Images stored in BepInEx/config/CascadeMenu/Backgrounds",
                    bgOptions,
                    () => CascadeBackgroundManager.IndexOfImage(bgOptions, config.BackgroundImagePath.Value),
                    index =>
                    {
                        if (index >= 0 && index < bgOptions.Length)
                            menu.SelectBackgroundImage(bgOptions[index]);
                    })
                .AddDual("Backgrounds folder", "Open BepInEx/config/CascadeMenu/Backgrounds in Explorer or refresh the list",
                    "Open Folder", "Refresh",
                    () => CascadeBackgroundManager.OpenFolderInExplorer(),
                    () =>
                    {
                        CascadeBackgroundManager.SyncFromConfig(config, true);
                        menu.QueueRebuild();
                        menu.ShowToast("Backgrounds", "Refreshed image list.");
                    }, false)
                .Button("Clear background", "Remove the custom background image",
                    "Clear", menu.ClearBackgroundImage, false)
                .LiveLabel("Loaded in memory", "Currently active background texture",
                    () => CascadeBackgroundManager.CurrentName,
                    () => CascadeBackgroundManager.CurrentTexture != null)
                .Slider("Background opacity", "Visibility of your uploaded PNG/JPG wallpaper inside the window",
                    0f, 1f,
                    () => config.BackgroundImageOpacity.Value,
                    value => config.BackgroundImageOpacity.Value = value,
                    value => Mathf.RoundToInt(value * 100f) + "%")
                .Slider("Panel & card opacity", "Transparency of the sidebar, backdrop, and cards over your background",
                    0.05f, 1f,
                    () => config.WindowPanelOpacity.Value,
                    value => config.WindowPanelOpacity.Value = value,
                    value => Mathf.RoundToInt(value * 100f) + "%")
                .Toggle("Dim top bar", "Keep a soft backdrop behind the titlebar for readability over bright images",
                    () => config.DimTopBar.Value,
                    value => config.DimTopBar.Value = value)
                .Toggle("Dim sidebar", "Keep a soft backdrop behind the left sidebar over bright images",
                    () => config.DimSidebar.Value,
                    value => config.DimSidebar.Value = value);

            // 4. Orb Glow (Shader-like Multi-Orb Aura Controls)
            settings.Section("Orb Glow", "Shader-like animated multi-orb glow aura behind the Cascade window")
                .Toggle("Orb aura glow", "Show the smooth multi-orb glow aura behind the main window",
                    () => config.WindowGlow.Value,
                    value => config.WindowGlow.Value = value)
                .Slider("Orb size", "Size and spread of the glowing shader orbs behind the window",
                    0.35f, 2.5f,
                    () => config.WindowGlowSize.Value,
                    value => config.WindowGlowSize.Value = value,
                    value => Mathf.RoundToInt(value * 100f) + "%")
                .Slider("Orb intensity", "Brightness of the glowing orb aura",
                    0f, 2.5f,
                    () => config.WindowGlowIntensity.Value,
                    value => config.WindowGlowIntensity.Value = value,
                    value => Mathf.RoundToInt(value * 100f) + "%")
                .Toggle("Orb follows theme accent", "Match orb aura color to your theme accent (turn off for custom orb color)",
                    () => config.WindowGlowUseAccent.Value,
                    value => config.WindowGlowUseAccent.Value = value)
                .Swatches("Orb aura color", "Pick a custom color for the glowing orb aura",
                    new[]
                    {
                        new Color(0.39f, 0.40f, 0.95f, 1f),
                        new Color(0.22f, 0.74f, 0.97f, 1f),
                        new Color(1f, 0.42f, 0.42f, 1f),
                        new Color(0.93f, 0.28f, 0.60f, 1f),
                        new Color(0.06f, 0.73f, 0.51f, 1f),
                        new Color(0.96f, 0.62f, 0.04f, 1f),
                        new Color(0.23f, 0.51f, 0.96f, 1f),
                        new Color(1f, 1f, 1f, 1f)
                    },
                    index =>
                    {
                        string[] hexes = { "#6366F1", "#38BDF8", "#FF6B6B", "#EC4899", "#10B981", "#F59E0B", "#3B82F6", "#FFFFFF" };
                        config.WindowGlowUseAccent.Value = false;
                        config.WindowGlowColor.Value = hexes[Mathf.Clamp(index, 0, hexes.Length - 1)];
                    })
                .ColorWheel("Orb color wheel", "Drag on the circle to pick any custom orb aura color",
                    () =>
                    {
                        string hex = config.WindowGlowColor.Value ?? "#6366F1";
                        if (!hex.StartsWith("#")) hex = "#" + hex;
                        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : new Color(0.39f, 0.40f, 0.95f, 1f);
                    },
                    color =>
                    {
                        config.WindowGlowUseAccent.Value = false;
                        config.WindowGlowColor.Value = CascadeThemePalette.ToHex(color);
                    },
                    () => "Orb Aura Color");

            // 5. Sub-Tabs (Icon + Color Underline Bar Customization)
            settings.Section("Sub-Tabs", "Customize the sub-tab bar style, icons, and underline color line")
                .Dropdown("Sub-tab bar style", "Choose sleek icon + color underline bar or classic pills",
                    new[] { "Icon + Color Bar Line", "Classic Pills" },
                    () => config.SubTabStyle.Value,
                    index =>
                    {
                        config.SubTabStyle.Value = index;
                        menu.QueueRebuild();
                    })
                .Toggle("Sub-tab icons", "Show a matching icon next to each sub-tab name",
                    () => config.SubTabShowIcons.Value,
                    value =>
                    {
                        config.SubTabShowIcons.Value = value;
                        menu.QueueRebuild();
                    })
                .Toggle("Sub-tab line follows theme accent", "Match the active sub-tab bar line to your theme accent (turn off for custom color)",
                    () => config.SubTabLineUseAccent.Value,
                    value => config.SubTabLineUseAccent.Value = value)
                .Swatches("Sub-tab line color", "Pick a custom color for the active sub-tab underline bar",
                    new[]
                    {
                        new Color(1f, 0.42f, 0.42f, 1f),
                        new Color(0.22f, 0.74f, 0.97f, 1f),
                        new Color(0.66f, 0.33f, 0.97f, 1f),
                        new Color(0.20f, 0.83f, 0.60f, 1f),
                        new Color(0.98f, 0.75f, 0.15f, 1f),
                        new Color(0.96f, 0.45f, 0.71f, 1f),
                        new Color(0.23f, 0.51f, 0.96f, 1f),
                        new Color(1f, 1f, 1f, 1f)
                    },
                    index =>
                    {
                        string[] hexes = { "#FF6B6B", "#38BDF8", "#A855F7", "#34D399", "#FBBF24", "#F472B6", "#3B82F6", "#FFFFFF" };
                        config.SubTabLineUseAccent.Value = false;
                        config.SubTabLineColor.Value = hexes[Mathf.Clamp(index, 0, hexes.Length - 1)];
                    })
                .TextField("Sub-tab line hex", "Custom #RRGGBB color for the sub-tab underline bar",
                    "#FF6B6B", 7,
                    () => config.SubTabLineColor.Value,
                    value =>
                    {
                        if (string.IsNullOrEmpty(value)) return;
                        string clean = value.StartsWith("#") ? value : "#" + value;
                        if (ColorUtility.TryParseHtmlString(clean, out _))
                        {
                            config.SubTabLineUseAccent.Value = false;
                            config.SubTabLineColor.Value = clean.ToUpperInvariant();
                        }
                    })
                .ColorWheel("Sub-tab line color wheel", "Drag on the circle to pick any custom color for the active sub-tab bar line",
                    () =>
                    {
                        string hex = config.SubTabLineColor.Value ?? "#FF6B6B";
                        if (!hex.StartsWith("#")) hex = "#" + hex;
                        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : new Color(1f, 0.42f, 0.42f, 1f);
                    },
                    color =>
                    {
                        config.SubTabLineUseAccent.Value = false;
                        config.SubTabLineColor.Value = CascadeThemePalette.ToHex(color);
                    },
                    () => "Sub-Tab Bar Line");

            // 6. Modules & Alerts
            settings.Section("Modules", "Dedicated settings pages for built-in modules and room notifications")
                .Button("Notification settings", "Choose join, leave, master and room alerts plus toast position", "Open",
                    () => menu.OpenPage("cascade.notifications", "Notifications"), false)
                .Button("Flight settings", "Speed, direction and stick mapping", "Open",
                    () => menu.OpenModuleSettings("fly"), false)
                .Button("Acrobatics settings", "Smoothing and rotation controls", "Open",
                    () => menu.OpenModuleSettings("acrobatics"), false)
                .Button("ESP master settings", "Infected/Non-infected filter, player color matching and range", "Open",
                    () => menu.OpenModuleSettings("esp"), false)
                .Button("HUD & Overlay settings", "Dedicated hub for every mod that has a HUD", "Open",
                    () => menu.OpenModuleSettings("hud"), true)
                .Button("Gun settings", "Gun behaviour and target ring", "Open",
                    () => menu.OpenModuleSettings("gun"), false)
                .Button("Cosmetic settings", "Cosmetic ESP, alerts and catalog", "Open",
                    () => menu.OpenModuleSettings("cosmetics"), false);

            CascadeSection hudMods = settings.Section("HUD Mods", "Toggle or open the dedicated settings panel for any mod with a HUD");
            hudMods.Toggle("Main Cascade HUD", "Top-left status pill (mod name, player name, FPS, ArrayList)",
                () => config.HudEnabled.Value,
                value => config.HudEnabled.Value = value,
                true,
                () => menu.OpenModuleSettings("hud"));
            ModuleSettings.AddHudModRow(hudMods, menu, "Player HUD", "3D telemetry card floating above each player");
            ModuleSettings.AddHudModRow(hudMods, menu, "Frosted Glass HUD", "Head or wrist frosted glass HUD with hand-to-target tracer beam");
            ModuleSettings.AddHudModRow(hudMods, menu, "Windows VR Overlay", "Bring Windows 10/11 apps into VR (HUD docked or stationary, laser, mouse & keyboard)");
            ModuleSettings.AddHudModRow(hudMods, menu, "Name Tags", "Clean overhead nameplate above each player");
            ModuleSettings.AddHudModRow(hudMods, menu, "Talking Players", "Live speaker list with player-colored badges");
            ModuleSettings.AddHudModRow(hudMods, menu, "Proximity Radar", "360° circular radar scanner with player blips");
            ModuleSettings.AddHudModRow(hudMods, menu, "Input Visualizer", "Live controller stick pads, triggers and buttons");
            ModuleSettings.AddHudModRow(hudMods, menu, "Compass", "Sliding cardinal heading strip at top of view");
            ModuleSettings.AddHudModRow(hudMods, menu, "Speedometer", "Digital velocity gauge with pace bar");
            ModuleSettings.AddHudModRow(hudMods, menu, "FPS Graph", "60-bar scrolling frame-rate histogram");
            ModuleSettings.AddHudModRow(hudMods, menu, "Clock", "Corner digital wall clock with date");
            ModuleSettings.AddHudModRow(hudMods, menu, "Crosshair", "Center-of-view dot, cross or ring reticle");
            ModuleSettings.AddHudModRow(hudMods, menu, "Session Stats", "Room population, region, FPS and uptime card");
            hudMods.Button("All HUD & Telemetry Settings", "Open the full HUD configuration hub (including all 20 telemetry readouts)", "Open Hub",
                () => menu.OpenModuleSettings("hud"), true);

            settings.Section("Input", "Bindings work on PC and in VR; values are saved to the config file.")
                .Dropdown("Menu hand", "Which controller hand opens the menu",
                    new[] { "Left hand", "Right hand" },
                    () => config.MenuHand.Value,
                    index =>
                    {
                        config.MenuHand.Value = index;
                        menu.ShowToast("Menu hand", index == 0
                            ? "Only the left controller opens the menu now."
                            : "Only the right controller opens the menu now.");
                    })
                .Dropdown("Open menu button", "Choose B/Y, Joystick Press (stick click), A/X, or both",
                    new[] { "Secondary (B / Y)", "Joystick Press (Stick Click)", "Primary (A / X)", "B/Y or Joystick Press", "Extra VR Bind Only" },
                    () => config.MenuOpenMode.Value,
                    index =>
                    {
                        config.MenuOpenMode.Value = index;
                        string[] labels = { "Secondary (B/Y)", "Joystick Press", "Primary (A/X)", "B/Y or Joystick Press", "Extra VR Bind Only" };
                        menu.ShowToast("Menu button", "Menu opens with " + labels[Mathf.Clamp(index, 0, labels.Length - 1)] + ".");
                    })
                .Button("Menu key",
                    "Keyboard or controller bind (currently " +
                    Keybinds.Name(Keybinds.Parse(config.MenuKey.Value, UnityEngine.InputSystem.Key.F1)) + ")",
                    "Rebind", () => menu.BeginRebind("menu"), false)
                .Dropdown("Extra VR bind", "Optional second VR button that also toggles the menu",
                    VrInput.ButtonNames,
                    () => VrInput.IndexOf(config.MenuButton.Value),
                    index => config.MenuButton.Value = VrInput.Format(VrInput.FromIndex(index)))
                .Label("Always available", "Insert also opens the menu", "F1 / Insert");

            settings.Section("Debug & Console", "Developer logs and in-game console viewer")
                .Toggle("Debug console tab", "Show the live BepInEx and plugin log console in the sidebar",
                    () => config.DebugConsoleEnabled.Value,
                    value =>
                    {
                        config.DebugConsoleEnabled.Value = value;
                        if (value) CascadeMenu.Mods.Tools.DebugLogListener.Attach();
                        menu.QueueRebuild();
                    })
                .Dropdown("Console tab placement", "Sidebar section for the Debug Console tab",
                    new[] { "Debug", "System", "Tools", "Main", "Fun", "Movement", "Visual", "Cosmetics" },
                    () =>
                    {
                        string val = config.DebugConsolePlacement.Value;
                        string[] opts = new[] { "Debug", "System", "Tools", "Main", "Fun", "Movement", "Visual", "Cosmetics" };
                        int idx = System.Array.IndexOf(opts, val);
                        return idx >= 0 ? idx : 0;
                    },
                    index =>
                    {
                        string[] opts = new[] { "Debug", "System", "Tools", "Main", "Fun", "Movement", "Visual", "Cosmetics" };
                        config.DebugConsolePlacement.Value = opts[Mathf.Clamp(index, 0, opts.Length - 1)];
                        menu.QueueRebuild();
                    })
                .Toggle("Debug log (Plugin.Log)", "Master toggle: when ON, all Plugin.Log output prints; when OFF, none of it prints",
                    () => config.VerboseLogging.Value,
                    value =>
                    {
                        config.VerboseLogging.Value = value;
                        CascadeLogger.Enabled = value;
                        if (value) Plugin.Log?.LogInfo("Plugin.Log debug logging enabled.");
                    })
                .Button("Open Debug Console", "Directly view captured logs", "Open",
                    () =>
                    {
                        config.DebugConsoleEnabled.Value = true;
                        menu.QueueRebuild();
                        menu.OpenPage(CascadeMenu.Mods.Tools.DebugConsole.PageTag, CascadeMenu.Mods.Tools.DebugConsole.PageTitle);
                    }, false);

            return settings;
        }

        // -----------------------------------------------------------------
        // About
        // -----------------------------------------------------------------
        private static CascadePage BuildAbout()
        {
            var about = new CascadePage("About", "Credits and controls", CascadeIcon.Info);
            about.Section("Cascade Menu", "A faithful C# recreation of the Cascade Luau UI.")
                .Hero("Cascade Menu", "macOS Sequoia style for Gorilla Tag", "v1.0")
                .Label("Cascade UI", "Original Luau interface library", "cascadeui")
                .Label("Built for", "Gorilla Tag + BepInEx", "VR / Desktop")
                .Label("Renderer", "World-space Unity Canvas", "No EventSystem")
                .Label("Game API", "Reflection targets for game data", GameApi.Summary);

            about.Section("Contributors", "People and tools behind this build")
                .Label("Maintainer", "Cascade Menu project", "foggyie")
                .Label("AI assistance", "Themes, mod lifecycle, settings panels and docs", "OpenCode");

            about.Section("Links", "Opens in your desktop browser.")
                .Button("Open Cascade on GitHub", "Original themes, screenshots and docs",
                    "Open", () => Application.OpenURL("https://github.com/cascadeui/Cascade"), true)
                .Button("Open BepInEx docs", "Plugin framework documentation",
                    "Open", () => Application.OpenURL("https://docs.bepinex.dev/"), false);

            about.Section("Controls")
                .Label("Toggle menu", "Keyboard", "Menu key / Insert")
                .Label("Toggle menu", "VR controllers", "Left hand by default")
                .Label("Menu hand", "Settings > Input", "Left or right")
                .Label("Select", "Desktop", "Left click")
                .Label("Select", "VR", "Trigger or A")
                .Label("Scroll", "Desktop / VR", "Wheel / right stick")
                .Label("Joystick fly", "Keyboard", "F5")
                .Label("Fly axes", "Keyboard", "F6 / F7 / F8 / F9")
                .Label("Bindable VR buttons", "Settings > Input", "A/X, B/Y, trigger, grip, stick")
                .Label("Acrobatics", "Default", "right stick click")
                .Label("Mod switches", "Mods tab", "One toggle per mod")
                .Label("VR keyboard", "Shown while the search field is focused", "tap keys");

            return about;
        }

        // -----------------------------------------------------------------
        // Component gallery (kept as a living style guide)
        // -----------------------------------------------------------------
        private static CascadePage BuildGallery(CascadeMenuBehaviour menu, MenuState state)
        {
            var controls = new CascadePage("Controls", "Every Cascade component", CascadeIcon.Layers);
            controls.Section("Inputs", "The full control gallery uses the same reusable UI kit.")
                .Toggle("Toggle switch", "Click anywhere on the row",
                    () => state.GetBool("galleryToggle", true), value => state.SetBool("galleryToggle", value))
                .Slider("Slider", "Drag the knob or click the track",
                    0f, 100f,
                    () => state.GetFloat("gallerySlider", 38f),
                    value => state.SetFloat("gallerySlider", value),
                    value => value.ToString("0") + "%")
                .Stepper("Stepper", "Minus and plus buttons",
                    0, 10,
                    () => state.GetInt("galleryStepper", 3),
                    value => state.SetInt("galleryStepper", value))
                .Radio("Radio group", "Mutually exclusive options",
                    new[] { "Low", "Medium", "High" },
                    () => state.GetInt("galleryRadio", 1),
                    value => state.SetInt("galleryRadio", value))
                .Dropdown("Dropdown", "Opens an overlay menu",
                    new[] { "Option one", "Option two", "Option three" },
                    () => state.GetInt("galleryDropdown", 0),
                    value => state.SetInt("galleryDropdown", value));

            controls.Section("Actions", "Buttons use accent gradients or a neutral border style.")
                .Button("Primary action", "Filled accent button", "Action",
                    () => menu.ShowToast("Primary action", "The gradient button was pressed."), true)
                .Button("Secondary action", "Bordered neutral button", "Action",
                    () => menu.ShowToast("Secondary action", "The bordered button was pressed."), false)
                .Label("Label row", "Read-only information", "Value")
                .Label("Another label", "Longer values are ellipsized when needed", "Cascade v1.0");

            return controls;
        }
    }
}
