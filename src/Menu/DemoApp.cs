using System.Collections.Generic;
using CascadeMenu.UI;
using CascadeMenu.Mods;
using UnityEngine;

namespace CascadeMenu
{
    /// <summary>
    /// Builds the page tree. Every open rebuilds it so live data (players, room)
    /// is fresh. Categories are declared in <see cref="FeatureCatalog"/>.
    /// </summary>
    internal static class DemoApp
    {
        public static List<CascadePage> Build(CascadeMenuBehaviour menu, MenuConfig config, DemoState state)
        {
            var pages = new List<CascadePage>();
            pages.Add(BuildHome(menu, config));
            pages.Add(PlayerActions.BuildPlayersPage(menu));
            pages.Add(BuildNotifications(menu, config));
            pages.Add(BuildFun(menu, config));
            pages.Add(BuildMovement(menu, config));
            pages.Add(BuildVisual(menu, config));
            pages.Add(CosmeticBrowser.BuildCategoriesPage(menu));
            pages.Add(CosmeticShop.BuildPage(menu));

            // One page for every discovered Mod subclass, grouped by module.
            pages.Add(ModRegistry.BuildAllPage(menu));

            pages.Add(Buildutilities(menu, config));
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
                .Button("Re-center window", "Place the menu level, in front of you", "Recenter", menu.RecenterMenu, true)
                .Button("Send a notification", "Preview the Cascade toast style", "Notify",
                    () => menu.ShowToast("Cascade", "This is a notification."), false)
                .Label("Inspired by Cascade", "Open-source Luau UI library by cascadeui", "macOS 15");

            // Live lobby details live here now; the Players tab is just the
            // roster and the per-player actions.
            PlayerActions.AppendRoomSection(home);

            home.Section("Quick appearance")
                .Dropdown("Theme", "Dark and light appearances are included",
                    new[] { "Dark", "Light" },
                    () => config.ThemeIndex.Value,
                    index =>
                    {
                        config.ThemeIndex.Value = index;
                        menu.QueueRebuild();
                    })
                .Dropdown("Accent", "Selection, switches and primary buttons",
                    CascadeTheme.AccentNames,
                    () => config.AccentIndex.Value,
                    index =>
                    {
                        config.AccentIndex.Value = index;
                        menu.QueueRebuild();
                    });

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
                .Toggle("Tag gun", "Aim at a gorilla and pull the trigger to really tag them",
                    () => ModRegistry.IsEnabledByName("Tag gun"),
                    value => ModRegistry.SetEnabledByName("Tag gun", value))
                .Toggle("Teleport gun", "Aim anywhere and pull the trigger to teleport there",
                    () => ModRegistry.IsEnabledByName("Teleport (Gun)"),
                    value => ModRegistry.SetEnabledByName("Teleport (Gun)", value))
                .Button("Tag all players", "Send a real tag for everyone in the room", "Tag all",
                    () =>
                    {
                        int tagged = PlayerActions.TagAll();
                        menu.ShowToast("Tag all", tagged == 0
                            ? "No tags sent - no players or outside tag range."
                            : "Sent tags for " + tagged + " player(s).");
                    }, false)
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
                .Button("Acrobatics settings", "Arming, smoothing and the double-tap gesture", "Open",
                    () => menu.OpenModuleSettings("acrobatics"), false);

            return movement;
        }

        // -----------------------------------------------------------------
        // Visual
        // -----------------------------------------------------------------
        private static CascadePage BuildVisual(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var visual = FeatureCatalog.BuildPage(config, "Visual", "HUD, ESP and target markers", CascadeIcon.Eye, menu);

            visual.Section("Module settings", "Range, colors and the HUD layout")
                .Button("ESP settings", "Range, color and overlay options", "Open",
                    () => menu.OpenModuleSettings("esp"), false)
                .Button("HUD settings", "Choose what the overlay shows", "Open",
                    () => menu.OpenModuleSettings("hud"), false)
                .Button("Gun settings", "Gun behaviour and target ring", "Open",
                    () => menu.OpenModuleSettings("gun"), false)
                .Button("Cosmetic settings", "Cosmetic ESP, alerts and catalog", "Open",
                    () => menu.OpenModuleSettings("cosmetics"), false);

            return visual;
        }
        
        private static CascadePage Buildutilities(CascadeMenuBehaviour menu, MenuConfig config)
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
            var settings = new CascadePage("Settings", "Appearance, placement and behavior", CascadeIcon.Gear);

            settings.Section("Placement", "The menu is a world object: it spawns in front of you and stays level.")
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
                .Slider("Re-anchor distance", "Walk further than this and the menu comes back (0 = never)",
                    0f, 6f,
                    () => config.MenuReanchorDistance.Value,
                    value => config.MenuReanchorDistance.Value = value,
                    value => value <= 0.01f ? "off" : value.ToString("0.0") + " m")
                .Toggle("Smooth follow", "Optional: glide after the head instead of staying put",
                    () => config.FollowHead.Value,
                    value => config.FollowHead.Value = value);

            settings.Section("Appearance")
                .Slider("UI scale", "Size of the entire window",
                    0.8f, 1.3f,
                    () => config.Scale.Value,
                    value => config.Scale.Value = value,
                    value => value.ToString("0.00") + "x")
                .Toggle("Animations", "Sidebar, page fades and notifications",
                    () => config.Animations.Value,
                    value => config.Animations.Value = value)
                .Button("Collapse sidebar", "Hide the navigation rail",
                    "Collapse", menu.ToggleSidebar, false);

            settings.Section("Notifications", "Join, leave, master and room alerts")
                .Button("Notification settings", "Choose every event and where the card appears", "Open",
                    () => menu.OpenPage("cascade.notifications", "Notifications"), false);

            settings.Section("Window")
                .Button("Re-center window", "Snap level in front of you",
                    "Recenter", menu.RecenterMenu, true)
                .Button("Restore defaults", "Reset every setting in this menu",
                    "Restore", menu.RestoreDefaults, false)
                .Label("Close", "Menu hand button, or the menu key", "Toggle menu");

            settings.Section("Module settings", "Every tuning control lives on its module's own page")
                .Button("Flight settings", "Speed, direction and stick mapping", "Open",
                    () => menu.OpenModuleSettings("fly"), false)
                .Button("Acrobatics settings", "Arming, smoothing and the double-tap gesture", "Open",
                    () => menu.OpenModuleSettings("acrobatics"), false)
                .Button("ESP settings", "Boxes, names, tracers and range", "Open",
                    () => menu.OpenModuleSettings("esp"), false)
                .Button("HUD settings", "Choose what the overlay shows", "Open",
                    () => menu.OpenModuleSettings("hud"), false)
                .Button("Gun settings", "Gun behaviour and target ring", "Open",
                    () => menu.OpenModuleSettings("gun"), false)
                .Button("Cosmetic settings", "Cosmetic ESP, alerts and catalog", "Open",
                    () => menu.OpenModuleSettings("cosmetics"), false);

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
                .Button("Menu key",
                    "Keyboard or controller bind (currently " +
                    Keybinds.Name(Keybinds.Parse(config.MenuKey.Value, UnityEngine.InputSystem.Key.F1)) + ")",
                    "Rebind", () => menu.BeginRebind("menu"), false)
                .Dropdown("Menu button", "VR button that also toggles the menu",
                    VrInput.ButtonNames,
                    () => VrInput.IndexOf(config.MenuButton.Value),
                    index => config.MenuButton.Value = VrInput.Format(VrInput.FromIndex(index)))
                .Button("Acrobatics key",
                    "Keyboard or controller bind (currently " +
                    Keybinds.Name(Keybinds.Parse(config.AcrobaticsKey.Value, UnityEngine.InputSystem.Key.Delete)) + ")",
                    "Rebind", () => menu.BeginRebind("acrobatics"), false)
                .Dropdown("Acrobatics button", "VR button that arms acrobatics",
                    VrInput.ButtonNames,
                    () => VrInput.IndexOf(config.AcrobaticsButton.Value),
                    index => config.AcrobaticsButton.Value = VrInput.Format(VrInput.FromIndex(index)))
                .Slider("Double-press window",
                    "Maximum gap between taps for a mod set to Double press",
                    0.1f, 1f,
                    () => config.DoublePressWindow.Value,
                    value => config.DoublePressWindow.Value = value,
                    value => value.ToString("0.00") + " s")
                .Label("Always available", "Insert also opens the menu", "F1 / Insert");

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
                .Label("Renderer", "World-space Unity Canvas", "No EventSystem");

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
                .Label("Acrobatics engage", "Double-tap A while armed", "2x A")
                .Label("Mod keybinds", "Mods tab: enable, then Bind", "Single / Double")
                .Label("Double-press window", "Settings > Input", "0.30 s default")
                .Label("VR keyboard", "Shown while the search field is focused", "tap keys");

            return about;
        }

        // -----------------------------------------------------------------
        // Component gallery (kept as a living style guide)
        // -----------------------------------------------------------------
        private static CascadePage BuildGallery(CascadeMenuBehaviour menu, DemoState state)
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
