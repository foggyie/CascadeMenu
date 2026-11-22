using CascadeMenu.Mods.Core;
using CascadeMenu.Patches;

namespace CascadeMenu.Mods.Game
{
    /// <summary>
    /// Prevents accidentally falling into Gorilla Tag's out-of-bounds quit box
    /// from closing the game.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class SafeQuitBoxMod : Mod
    {
        public SafeQuitBoxMod()
        {
            Name = "No Quit Box (Safe Boundary)";
            isToggle = true;
        }

        public override void OnEnable()
        {
            QuitBoxSafetyPatch.BlockQuit = true;
        }

        public override void OnDisable()
        {
            QuitBoxSafetyPatch.BlockQuit = false;
        }
    }
}
