using CascadeMenu.UI;
using GorillaLocomotion;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Core
{
    // =========================================================================
    // CASCADE MENU — MOD AUTHORING TEMPLATE (FORK & EXTENSION GUIDE)
    // =========================================================================
    //
    // How the Mod System Works:
    // -------------------------
    // `ModRegistry` automatically scans the assembly at startup and discovers
    // every non-abstract class that inherits from `Mod` (or `OverlayMod`).
    // You do NOT need to register your mod in `MenuApp` or `Plugin.cs`.
    //
    // Step 1: Inherit from `Mod` (for gameplay, movement, visual, or gun mods)
    //         or `OverlayMod` (for camera-attached HUD / telemetry widgets).
    // Step 2: Add `[PluginToggleable]` and `[ModCategory("Movement")]`
    //         (Categories: "Movement", "Visual", "Fun", "Overlays", "Tools",
    //          "Flight", "Desktop", "Performance", "Player", "Game", "Super").
    // Step 3: Set `Name` and `isToggle` in your constructor, and optionally
    //         call `AddSetting(ModSetting.Slider / Toggle / Dropdown / Text)`
    //         to auto-generate a persistent settings gear panel!
    //
    // Uncomment any of the template classes below to try them out in-game.
    // =========================================================================

    /*
    // -------------------------------------------------------------------------
    // Example 1: Toggle Mod with Auto-Saved Settings Page
    // -------------------------------------------------------------------------
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class ExampleSpeedBoostMod : Mod
    {
        private float _multiplier = 1.4f;
        private bool _onlyOnGround = true;

        public ExampleSpeedBoostMod()
        {
            Name = "Example Speed Boost";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "multiplier", "Speed multiplier",
                "How much faster you move while active",
                1.0f, 3.0f,
                () => _multiplier,
                value => _multiplier = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Toggle(
                "groundOnly", "Grounded only",
                "Only apply the boost when touching a surface",
                () => _onlyOnGround,
                value => _onlyOnGround = value));
        }

        public override void OnEnable()
        {
            Plugin.Log?.LogInfo(Name + " enabled.");
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;
            if (_onlyOnGround && !player.BodyOnGround) return;

            player.velocityLimit = 12f * _multiplier;
        }

        public override void OnDisable()
        {
            Plugin.Log?.LogInfo(Name + " disabled.");
        }
    }

    // -------------------------------------------------------------------------
    // Example 2: Custom Non-Box HUD Mod (inherits OverlayMod)
    // -------------------------------------------------------------------------
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class ExampleCustomHudMod : OverlayMod
    {
        public ExampleCustomHudMod()
        {
            Name = "Example Telemetry HUD";
            // Choose from: WindowCard, Capsule, GaugeWindow, KeyCap, TerminalWindow
            Design = HudDesign.Capsule;
            AccentColor = new Color(0.35f, 0.90f, 1f, 1f);
            BadgeIcon = CascadeIcon.Bolt;
            CardWidth = 310f;
            CardHeight = 104f;
            RefreshInterval = 0.2f;
        }

        protected override string Build()
        {
            return "<color=#59E5FF><b>CUSTOM TELEMETRY</b></color>\n" +
                   "Status: <b>ONLINE</b>";
        }
    }
    */
}
