using CascadeMenu.Mods;
using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu
{
    /// <summary>
    /// Dedicated tab for the target gun.
    ///
    /// The old "Gun settings" page still exists because it is linked from the
    /// Fun tab's gear; this page is the complete panel: state, grip input,
    /// beam/ring appearance and links to the generated mod settings (keybind,
    /// press type, Active) and the profiler.
    ///
    /// Every control writes a config entry. The controller polls those entries,
    /// so changing a value here takes effect on the next frame without a rebuild.
    /// </summary>
    internal static class GunLibPage
    {
        internal const string Tag = "cascade.gunlib";

        internal static CascadePage Build(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var page = new CascadePage("GunLib", "Grip-gated player targeting", CascadeIcon.Person);
            page.Tag = Tag;

            // -------------------------------------------------------------
            // State
            // -------------------------------------------------------------
            page.Section("State", "Enabled arms the gun; Active starts the beam")
                .Toggle("Enabled", "Arm the gun so its keybind and grip work",
                    () => GunLibMod.IsArmed(config),
                    value => GunLibMod.SetArmed(config, value))
                .LiveLabel("Active", "Changed by the keybind, the button below or the menu",
                    () => GunLibMod.IsRunning(config) ? "ON" : "OFF",
                    () => GunLibMod.IsRunning(config))
                .Button("Toggle Active", "Same as pressing the bound key", "Start / Stop",
                    () => GunLibMod.ToggleRunning(config), true)
                .Label("Safety", "Grip is read separately from the trigger and never clicks menu rows", "isolated");

            // -------------------------------------------------------------
            // Grip
            // -------------------------------------------------------------
            page.Section("Grip", "The beam only appears while the chosen grip is held")
                .Toggle("Require grip", "Release the grip to hide the beam, ring and pending hit immediately",
                    () => config.GunRequireGrip.Value,
                    value => config.GunRequireGrip.Value = value)
                .Dropdown("Grip hand", "VR hand that arms the gun",
                    new[] { "Right", "Left", "Either" },
                    () => Mathf.Clamp(config.GunGripHand.Value, 0, 2),
                    index => config.GunGripHand.Value = Mathf.Clamp(index, 0, 2))
                .Dropdown("Beam hand", "Hand the beam and ring come from",
                    new[] { "Right", "Left", "Hand holding grip" },
                    () => Mathf.Clamp(config.GunBeamHand.Value, 0, 2),
                    index => config.GunBeamHand.Value = Mathf.Clamp(index, 0, 2))
                .Dropdown("Desktop grip", "Mouse button or key that stands in for the VR grip",
                    new[] { "Right mouse", "Left mouse", "Middle mouse", "Keyboard key" },
                    () => Mathf.Clamp(config.GunPcGrip.Value, 0, 3),
                    index => config.GunPcGrip.Value = Mathf.Clamp(index, 0, 3))
                .Button("Desktop grip key",
                    "Keyboard key used when Desktop grip is set to Keyboard key (currently " +
                    Keybinds.Name(Keybinds.Parse(config.GunPcGripKey.Value, Key.LeftAlt)) + ")",
                    "Rebind", () => menu.BeginRebind("gungrip"), false)
                .Label("Default", "Right mouse button on PC", "RMB");

            // -------------------------------------------------------------
            // Beam and ring
            // -------------------------------------------------------------
            page.Section("Beam and ring", "Thin beam and a ring on the accepted hit point")
                .Toggle("Beam visible", "Turn the beam off but keep targeting and the ring",
                    () => config.GunBeamEnabled.Value,
                    value => config.GunBeamEnabled.Value = value)
                .Toggle("Use theme colors", "Take the beam and ring color from the active Cascade theme",
                    () => config.GunUseThemeColor.Value,
                    value => config.GunUseThemeColor.Value = value)
                .TextField("Beam color", "Used when Use theme colors is off (#RRGGBB)", "#4DB2FF", 9,
                    () => config.GunBeamColor.Value,
                    value => config.GunBeamColor.Value = value)
                .TextField("Ring color", "Used when Use theme colors is off (#RRGGBB)", "#4DB2FF", 9,
                    () => config.GunRingColor.Value,
                    value => config.GunRingColor.Value = value)
                .Slider("Beam width", "Start width of the beam in metres",
                    0.001f, 0.03f,
                    () => config.GunBeamWidth.Value,
                    value => config.GunBeamWidth.Value = value,
                    value => value.ToString("0.000") + " m")
                .Slider("Ring size", "Radius of the target ring in metres",
                    0.12f, 0.9f,
                    () => config.GunRingSize.Value,
                    value => config.GunRingSize.Value = value,
                    value => value.ToString("0.00") + " m")
                .Slider("Max distance", "Longest aim ray in metres",
                    3f, 80f,
                    () => config.GunMaxDistance.Value,
                    value => config.GunMaxDistance.Value = value,
                    value => value.ToString("0") + " m")
                .Button("Cycle color", "Advance the accent and use it for the beam and ring", "Next",
                    GunLibController.CycleColor, false);

            // -------------------------------------------------------------
            // Target behaviour
            // -------------------------------------------------------------
            page.Section("Target", "What the gun does when the trigger is pulled")
                .Toggle("Target ring", "Draw a ring on the locked player",
                    () => config.GunShowMarker.Value,
                    value => config.GunShowMarker.Value = value)
                .Toggle("Sticky target", "Keep the lock briefly when the aim slips",
                    () => config.GunSticky.Value,
                    value => config.GunSticky.Value = value)
                .Dropdown("Module", "Behaviour on trigger",
                    new[] { "Select", "Mark", "Copy Name" },
                    () => Mathf.Clamp(config.GunModule.Value, 0, 2),
                    index => config.GunModule.Value = Mathf.Clamp(index, 0, 2));

            // -------------------------------------------------------------
            // Links
            // -------------------------------------------------------------
            page.Section("More", "Keybind, press type and the cost of the gun")
                .Button("GunLib mod settings", "Enabled/Active, keybind and this mod's options",
                    "Open", () =>
                    {
                        Mod mod = GunLibMod.Instance;
                        if (mod != null)
                        {
                            menu.OpenModSettings(mod);
                        }
                        else
                        {
                            menu.ShowToast("GunLib", "The gun mod is still loading.");
                        }
                    }, false)
                .Button("Gun settings (legacy)", "The older Fun-tab gear page", "Open",
                    () => menu.OpenModuleSettings("gun"), false)
                .Button("Profiler", "See the per-frame cost of the gun", "Open",
                    () => menu.OpenPage(ProfilerPage.Tag, "Profiler"), false)
                .Label("Ray filter", "Ignores triggers, disabled renderers and invisible colliders", "visible only");

            return page;
        }
    }
}
