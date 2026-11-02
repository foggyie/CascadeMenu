using BepInEx.Configuration;
using UnityEngine;

namespace CascadeMenu
{
    internal sealed class MenuConfig
    {
        public readonly ConfigEntry<int> ThemeIndex;
        public readonly ConfigEntry<int> AccentIndex;
        public readonly ConfigEntry<int> UiMode;
        public readonly ConfigEntry<float> Scale;
        public readonly ConfigEntry<float> Distance;
        public readonly ConfigEntry<bool> FollowHead;
        public readonly ConfigEntry<bool> Animations;
        public readonly ConfigEntry<bool> HandLinkBlock;
        public readonly ConfigEntry<bool> JoystickFly;
        public readonly ConfigEntry<float> FlySpeed;
        public readonly ConfigEntry<float> FlyVerticalSpeed;
        public readonly ConfigEntry<float> FlyAcceleration;
        public readonly ConfigEntry<float> FlyDeadzone;
        public readonly ConfigEntry<int> FlyDirection;
        public readonly ConfigEntry<bool> FlyInvertVertical;
        public readonly ConfigEntry<bool> FlyInvertForward;
        public readonly ConfigEntry<bool> FlyInvertStrafe;
        public readonly ConfigEntry<bool> FlyForwardHorizontal;
        public readonly ConfigEntry<int> FlyStickLayout;

        // Acrobatics (body rotation)
        public readonly ConfigEntry<bool> Acrobatics;
        public readonly ConfigEntry<float> AcrobaticsSmoothing;

        // Persistent noclip (collision pass-through)
        public readonly ConfigEntry<bool> Noclip;

        // Cosmetic spoof checker
        public readonly ConfigEntry<bool> CosmeticCheck;

        // Input / keybinds
        public readonly ConfigEntry<int> MenuHand;
        public readonly ConfigEntry<string> MenuKey;
        public readonly ConfigEntry<string> MenuButton;
        public readonly ConfigEntry<string> AcrobaticsKey;
        public readonly ConfigEntry<string> AcrobaticsButton;

        // Notifications (join / leave / master / room events)
        public readonly ConfigEntry<bool> NotifyJoins;
        public readonly ConfigEntry<bool> NotifyLeaves;
        public readonly ConfigEntry<bool> NotifyMaster;
        public readonly ConfigEntry<bool> NotifyMasterChanges;
        public readonly ConfigEntry<bool> NotifyRoom;
        public readonly ConfigEntry<bool> NotifyModder;
        public readonly ConfigEntry<int> NotificationPosition;

        // Last visited tab, persisted so reopening lands on the same page.
        public readonly ConfigEntry<string> LastPageTag;
        public readonly ConfigEntry<string> LastPageTitle;

        public readonly ConfigEntry<bool> HudEnabled;
        public readonly ConfigEntry<bool> HudModName;
        public readonly ConfigEntry<bool> HudPlayerName;
        public readonly ConfigEntry<bool> HudFps;
        public readonly ConfigEntry<bool> HudArrayList;

        public readonly ConfigEntry<bool> GunEnabled;
        public readonly ConfigEntry<bool> GunSticky;
        public readonly ConfigEntry<bool> GunShowMarker;
        public readonly ConfigEntry<int> GunModule;

        // Placement / anchoring
        public readonly ConfigEntry<float> MenuHeight;
        public readonly ConfigEntry<float> MenuRaiseAbove;
        public readonly ConfigEntry<float> MenuReanchorDistance;

        // Visuals
        public readonly ConfigEntry<bool> EspEnabled;
        public readonly ConfigEntry<bool> EspNames;
        public readonly ConfigEntry<bool> EspTracers;
        public readonly ConfigEntry<bool> EspChams;
        public readonly ConfigEntry<bool> EspMarker;
        public readonly ConfigEntry<bool> EspCosmeticTags;
        public readonly ConfigEntry<bool> CosmeticNotifications;
        public readonly ConfigEntry<int> EspColorIndex;
        public readonly ConfigEntry<float> EspRange;
        public readonly ConfigEntry<bool> HandTapIndicator;

        public MenuConfig(ConfigFile config)
        {
            ThemeIndex = config.Bind(
                "Appearance",
                "Theme",
                0,
                new ConfigDescription("0 = Dark, 1 = Light", new AcceptableValueRange<int>(0, 1)));

            AccentIndex = config.Bind(
                "Appearance",
                "Accent",
                0,
                new ConfigDescription("Index into Cascade's accent palette", new AcceptableValueRange<int>(0, 7)));

            UiMode = config.Bind(
                "Window",
                "UI Mode",
                0,
                new ConfigDescription("0 = Auto, 1 = Force PC (screen), 2 = Force VR (world)",
                    new AcceptableValueRange<int>(0, 2)));

            Scale = config.Bind(
                "Window",
                "UI Scale",
                1f,
                new ConfigDescription("Scales the whole window", new AcceptableValueRange<float>(0.8f, 1.3f)));

            Distance = config.Bind(
                "Window",
                "Distance",
                0.7f,
                new ConfigDescription("Distance in front of you when the menu spawns (0.6-0.8 is comfortable)", new AcceptableValueRange<float>(0.4f, 1.6f)));

            FollowHead = config.Bind(
                "Window",
                "Follow Head",
                false,
                "When enabled, smoothly follow the head. Off by default: the menu stays in world space after spawning.");

            Animations = config.Bind(
                "UI",
                "Animations",
                true,
                "Animate the sidebar, page fades and notifications.");

            HandLinkBlock = config.Bind(
                "Gameplay",
                "Block Hand Link",
                true,
                "Prevent other players from grabbing and moving you with GTPlayer hand links.");

            JoystickFly = config.Bind(
                "Gameplay",
                "Joystick Fly",
                false,
                "Arms double-tap A in VR (or the Flying toggle) to fly with the left stick.");

            FlySpeed = config.Bind(
                "Gameplay",
                "Fly Speed",
                6.5f,
                new ConfigDescription("Fly speed in metres per second", new AcceptableValueRange<float>(1.5f, 15f)));

            FlyVerticalSpeed = config.Bind(
                "Gameplay",
                "Fly Vertical Speed",
                4.5f,
                new ConfigDescription("Vertical speed from the left stick (m/s)", new AcceptableValueRange<float>(1.5f, 12f)));

            FlyAcceleration = config.Bind(
                "Gameplay", "Fly Acceleration", 8f,
                new ConfigDescription("How quickly flight reaches the requested speed (m/s²)", new AcceptableValueRange<float>(1f, 30f)));
            FlyDeadzone = config.Bind(
                "Gameplay", "Fly Stick Deadzone", 0.18f,
                new ConfigDescription("Ignore stick movement below this amount", new AcceptableValueRange<float>(0.05f, 0.4f)));
            FlyDirection = config.Bind(
                "Gameplay", "Fly Direction", 0,
                new ConfigDescription("0 = head/camera direction, 1 = right controller direction", new AcceptableValueRange<int>(0, 1)));

            FlyInvertVertical = config.Bind(
                "Gameplay",
                "Fly Invert Up/Down",
                true,
                "Reverse the up/down stick direction. On by default because some SteamVR bindings report the stick Y axis reversed.");

            FlyInvertForward = config.Bind(
                "Gameplay",
                "Fly Invert Forward/Back",
                false,
                "Reverse the forward/back stick direction.");

            FlyInvertStrafe = config.Bind(
                "Gameplay",
                "Fly Invert Strafe",
                false,
                "Reverse the left/right strafe direction.");

            FlyForwardHorizontal = config.Bind(
                "Gameplay",
                "Fly Forward Is Flat",
                true,
                "Fly forward along the floor instead of diving when looking down.");

            FlyStickLayout = config.Bind(
                "Gameplay",
                "Fly Stick Layout",
                0,
                new ConfigDescription(
                    "0 = left stick up/down + right stick forward. 1 = left stick forward + right stick up/down.",
                    new AcceptableValueRange<int>(0, 1)));

            Acrobatics = config.Bind(
                "Gameplay",
                "Acrobatics",
                false,
                "VR only: click the right joystick to rotate your body with your head. The menu stays level.");

            AcrobaticsSmoothing = config.Bind(
                "Gameplay",
                "Acrobatics Smoothing",
                5f,
                new ConfigDescription(
                    "How quickly the body follows the head rotation (higher = snappier)",
                    new AcceptableValueRange<float>(1f, 20f)));

            Noclip = config.Bind(
                "Gameplay",
                "Noclip",
                false,
                "Walk through walls and props. Collisions are restored when it is turned off.");

            HudEnabled = config.Bind(
                "Visual",
                "HUD Enabled",
                true,
                "Show the compact Cascade HUD in front of the player.");

            HudModName = config.Bind(
                "Visual",
                "HUD Mod Name",
                true,
                "Show the Cascade mod name in the HUD.");

            HudPlayerName = config.Bind(
                "Visual",
                "HUD Player Name",
                true,
                "Show the local player name in the HUD.");

            HudFps = config.Bind(
                "Visual",
                "HUD FPS",
                true,
                "Show the current FPS in the HUD.");

            HudArrayList = config.Bind(
                "Visual",
                "HUD ArrayList",
                true,
                "Show the active mods and targets in the HUD.");

            GunEnabled = config.Bind(
                "Utility",
                "Gun Enabled",
                true,
                "Enable the thin player-targeting gun library.");

            GunSticky = config.Bind(
                "Utility",
                "Gun Sticky Target",
                true,
                "Keep the gun locked to a player briefly when the aim slips.");

            GunShowMarker = config.Bind(
                "Utility",
                "Gun Target Ring",
                true,
                "Draw a ring around the locked player.");

            GunModule = config.Bind(
                "Utility",
                "Gun Module",
                0,
                new ConfigDescription("0 = Select, 1 = Mark, 2 = Copy Name", new AcceptableValueRange<int>(0, 2)));

            // -----------------------------------------------------------------
            // Placement: the menu spawns as a world object in front of the head.
            // -----------------------------------------------------------------
            MenuHeight = config.Bind(
                "Placement",
                "Menu Height",
                1.5f,
                new ConfigDescription(
                    "Fixed world height of the menu centre in metres. The menu does not bob with your head.",
                    new AcceptableValueRange<float>(0.8f, 2.2f)));

            MenuRaiseAbove = config.Bind(
                "Placement",
                "Menu Raise Above",
                1.8f,
                new ConfigDescription(
                    "Head height threshold. Only when your head is above this does the menu spawn higher (by the difference).",
                    new AcceptableValueRange<float>(1f, 2.8f)));

            MenuReanchorDistance = config.Bind(
                "Placement",
                "Menu Re-anchor Distance",
                1.6f,
                new ConfigDescription(
                    "Move the menu back in front of you when you walk further than this (metres). 0 disables it.",
                    new AcceptableValueRange<float>(0f, 6f)));

            // -----------------------------------------------------------------
            // Visuals
            // -----------------------------------------------------------------
            EspEnabled = config.Bind(
                "Visual",
                "ESP Enabled",
                false,
                "Draw a box around other players. Independent of names, tracers, chams and markers.");

            EspNames = config.Bind(
                "Visual",
                "ESP Names",
                true,
                "Show player names above each player.");

            EspTracers = config.Bind(
                "Visual",
                "ESP Tracers",
                false,
                "Draw a line from your hand to every tracked player.");

            EspChams = config.Bind("Visual", "ESP Chams", false, "Highlight player bodies through walls.");
            EspMarker = config.Bind("Visual", "ESP Marker", false, "Show a through-wall diamond above each player.");
            EspCosmeticTags = config.Bind("Visual", "Cosmetic ESP", false, "Show special cosmetic role tags above players.");
            CosmeticNotifications = config.Bind("Visual", "Cosmetic Notifications", false, "Show a standalone Cascade notification when a special cosmetic is detected.");
            EspColorIndex = config.Bind(
                "Visual", "ESP Highlight Color", 0,
                new ConfigDescription("0 = cyan, 1 = green, 2 = orange, 3 = magenta", new AcceptableValueRange<int>(0, 3)));

            EspRange = config.Bind(
                "Visual",
                "ESP Range",
                30f,
                new ConfigDescription("Maximum ESP distance in metres", new AcceptableValueRange<float>(3f, 80f)));

            HandTapIndicator = config.Bind(
                "Visual",
                "Hand Tap Indicator",
                true,
                "Show a TAP badge in the player list when a player taps their hands together.");

            CosmeticCheck = config.Bind(
                "Visual",
                "Cosmetic Spoof Check",
                true,
                "List every worn cosmetic with its category and flag unknown, duplicate or special cosmetics on the player page.");

            // -----------------------------------------------------------------
            // Input / keybinds
            // -----------------------------------------------------------------
            MenuHand = config.Bind(
                "Input",
                "Menu Hand",
                0,
                new ConfigDescription(
                    "Controller hand that opens the menu. 0 = left hand only, 1 = right hand only.",
                    new AcceptableValueRange<int>(0, 1)));

            MenuKey = config.Bind(
                "Input",
                "Menu Key",
                "F1",
                "Keyboard key (Input System name, e.g. F1, Insert, F4) that toggles the menu.");

            MenuButton = config.Bind(
                "Input",
                "Menu Button",
                "None",
                "Controller button that also toggles the menu: None, LeftPrimary, LeftSecondary, " +
                "LeftTrigger, LeftGrip, LeftStick, RightPrimary, RightSecondary, RightTrigger, RightGrip, RightStick.");

            AcrobaticsKey = config.Bind(
                "Input",
                "Acrobatics Key",
                "Delete",
                "Keyboard key that toggles acrobatics on PC (Input System name, e.g. Delete, F6).");

            AcrobaticsButton = config.Bind(
                "Input",
                "Acrobatics Button",
                "RightStick",
                "Controller button that toggles acrobatics: None, LeftPrimary, LeftSecondary, " +
                "LeftTrigger, LeftGrip, LeftStick, RightPrimary, RightSecondary, RightTrigger, RightGrip, RightStick.");

            // -----------------------------------------------------------------
            // Notifications: a visible list of events, shown even when the menu
            // is closed. Bottom-right by default so the card is easy to see
            // without covering the top of the view.
            // -----------------------------------------------------------------
            NotifyJoins = config.Bind(
                "Notifications",
                "Player Joins",
                true,
                "Show a notification when someone joins the room.");

            NotifyLeaves = config.Bind(
                "Notifications",
                "Player Leaves",
                true,
                "Show a notification when someone leaves the room.");

            NotifyMaster = config.Bind(
                "Notifications",
                "You Become Master",
                true,
                "Show a notification when you become the master client.");

            NotifyMasterChanges = config.Bind(
                "Notifications",
                "Master Changes",
                true,
                "Show a notification when another player becomes the master client.");

            NotifyRoom = config.Bind(
                "Notifications",
                "Room Changes",
                true,
                "Show a notification when you join a different room.");

            NotifyModder = config.Bind(
                "Notifications",
                "Modder Alerts",
                false,
                "Show a notification when a joining player has spoof-flagged cosmetics.");

            NotificationPosition = config.Bind(
                "Notifications",
                "Position",
                0,
                new ConfigDescription(
                    "Where the notification card appears. 0 = bottom right (default), 1 = top right, 2 = bottom left.",
                    new AcceptableValueRange<int>(0, 2)));

            LastPageTag = config.Bind(
                "Session",
                "Last Page Tag",
                string.Empty,
                "Internal: tag of the tab that was open when the menu closed.");

            LastPageTitle = config.Bind(
                "Session",
                "Last Page Title",
                string.Empty,
                "Internal: title of the tab that was open when the menu closed.");

        }

        public CascadeMenu.UI.CascadeThemeMode ThemeMode
        {
            get { return ThemeIndex.Value == 1 ? CascadeMenu.UI.CascadeThemeMode.Light : CascadeMenu.UI.CascadeThemeMode.Dark; }
        }

        public void ResetDefaults()
        {
            ThemeIndex.Value = 0;
            AccentIndex.Value = 0;
            UiMode.Value = 0;
            Scale.Value = 1f;
            Distance.Value = 0.7f;
            FollowHead.Value = false;
            Animations.Value = true;
            HandLinkBlock.Value = true;
            JoystickFly.Value = false;
            FlySpeed.Value = 6.5f;
            FlyVerticalSpeed.Value = 4.5f;
            FlyAcceleration.Value = 8f;
            FlyDeadzone.Value = 0.18f;
            FlyDirection.Value = 0;
            FlyInvertVertical.Value = true;
            FlyInvertForward.Value = false;
            FlyInvertStrafe.Value = false;
            FlyForwardHorizontal.Value = true;
            FlyStickLayout.Value = 0;
            Acrobatics.Value = false;
            AcrobaticsSmoothing.Value = 5f;
            Noclip.Value = false;
            HudEnabled.Value = true;
            HudModName.Value = true;
            HudPlayerName.Value = true;
            HudFps.Value = true;
            HudArrayList.Value = true;
            GunEnabled.Value = true;
            GunSticky.Value = true;
            GunShowMarker.Value = true;
            GunModule.Value = 0;
            MenuHeight.Value = 1.5f;
            MenuRaiseAbove.Value = 1.8f;
            MenuReanchorDistance.Value = 1.6f;
            EspEnabled.Value = false;
            EspNames.Value = true;
            EspTracers.Value = false;
            EspChams.Value = false;
            EspMarker.Value = false;
            EspCosmeticTags.Value = false;
            CosmeticNotifications.Value = false;
            EspColorIndex.Value = 0;
            EspRange.Value = 30f;
            HandTapIndicator.Value = true;
            CosmeticCheck.Value = true;
            MenuHand.Value = 0;
            MenuKey.Value = "F1";
            MenuButton.Value = "None";
            AcrobaticsKey.Value = "Delete";
            AcrobaticsButton.Value = "RightStick";
            NotifyJoins.Value = true;
            NotifyLeaves.Value = true;
            NotifyMaster.Value = true;
            NotifyMasterChanges.Value = true;
            NotifyRoom.Value = true;
            NotifyModder.Value = false;
            NotificationPosition.Value = 0;
            LastPageTag.Value = string.Empty;
            LastPageTitle.Value = string.Empty;
        }
    }
}
