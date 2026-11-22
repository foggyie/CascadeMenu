using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Overlays;
namespace CascadeMenu.Mods.Movement
{
    /// <summary>
    /// One-switch wrapper around the existing noclip controller. Enabling walks
    /// through walls; disabling restores every collider the controller cached.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class Noclip : Mod
    {
        public Noclip()
        {
            Name = "Noclip";
            isToggle = true;
        }

        public override void OnEnable()
        {
            NoclipController.SetActive(true);
            Plugin.Log?.LogInfo("Cascade Noclip active.");
        }

        public override void OnDisable()
        {
            NoclipController.SetActive(false);
            Plugin.Log?.LogInfo("Cascade Noclip stopped.");
        }
    }
}
