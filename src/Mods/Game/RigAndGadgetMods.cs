using CascadeMenu.Mods.Core;
using CascadeMenu.Patches;

namespace CascadeMenu.Mods.Game
{
    /// <summary>
    /// Keeps the local VRRig alive and prevents PostTick from running while
    /// VRRig.LocalRig is intentionally paused/disabled by rig mods.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class LocalRigGuardMod : Mod
    {
        public LocalRigGuardMod()
        {
            Name = "Local Rig Guard";
            isToggle = true;
        }

        public override void OnEnable()
        {
            LocalRigGuardPatches.Enabled = true;
        }

        public override void OnDisable()
        {
            LocalRigGuardPatches.Enabled = false;
        }
    }

    /// <summary>
    /// Removes the cooldown delay on the Super Infection Platform Deployer gadget
    /// (authority-side remainingRechargeTime = 0).
    /// </summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class InstantPlatformGadgetMod : Mod
    {
        public InstantPlatformGadgetMod()
        {
            Name = "Instant Platform Gadget";
            isToggle = true;
        }

        public override void OnEnable()
        {
            PlatformDeployerPatch.Enabled = true;
        }

        public override void OnDisable()
        {
            PlatformDeployerPatch.Enabled = false;
        }
    }
}
