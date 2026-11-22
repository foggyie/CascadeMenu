using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Patches;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Flight;

namespace CascadeMenu.Mods.Game
{
    /// <summary>
    /// Installs the gameplay patches and creates the controller that runs the
    /// hand-link and joystick-fly features. The UI only writes config values.
    /// </summary>
    internal static class GameplayMods
    {
        private static bool _initialized;

        internal static void Initialize(MenuConfig config, GameObject host)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            PatchManager.Install();

            JoystickFlyController controller = host.AddComponent<JoystickFlyController>();
            controller.Initialize(config);

            AcrobaticsController acrobatics = host.AddComponent<AcrobaticsController>();
            acrobatics.Initialize(config);

            NoclipController noclip = host.AddComponent<NoclipController>();
            noclip.Initialize(config);

            GunLibController gun = host.AddComponent<GunLibController>();
            gun.Initialize(config);
            Plugin.Log?.LogInfo("Cascade gun library created (thin beam + target ring).");

            VisualsController visuals = host.AddComponent<VisualsController>();
            visuals.Initialize(config);

            // Drives every Mod subclass found by ModRegistry.
            host.AddComponent<ModHost>();
            ModRegistry.Initialize(config);

            // The gun predates the mod system, so its thin Mod wrapper needs the
            // config after the registry created every mod instance.
            GunLibMod.EnsureBound(config);
        }

        internal static void Shutdown()
        {
            PatchManager.Uninstall();
            _initialized = false;
        }
    }
}
