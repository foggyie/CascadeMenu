using System;
using HarmonyLib;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Installs the gameplay patches and creates the controller that runs the
    /// hand-link and joystick-fly features. The UI only writes config values.
    /// </summary>
    internal static class GameplayMods
    {
        private static Harmony _harmony;
        private static bool _initialized;

        internal static void Initialize(MenuConfig config, GameObject host)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                _harmony = new Harmony(PluginInfo.GUID + ".gameplay");
                _harmony.PatchAll(typeof(GameplayMods).Assembly);
                Plugin.Log?.LogInfo("Cascade gameplay patches installed (hand link + fly gravity).");
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade could not install gameplay patches: " + exception);
            }

            JoystickFlyController controller = host.AddComponent<JoystickFlyController>();
            controller.Initialize(config);

            AcrobaticsController acrobatics = host.AddComponent<AcrobaticsController>();
            acrobatics.Initialize(config);

            GunLibController gun = host.AddComponent<GunLibController>();
            gun.Initialize(config);
            Plugin.Log?.LogInfo("Cascade gun library created (thin beam + target ring).");

            VisualsController visuals = host.AddComponent<VisualsController>();
            visuals.Initialize(config);

            // Drives every Mod subclass found by ModRegistry (Halloween, Super, ...).
            host.AddComponent<ModHost>();
            ModRegistry.EnsureLoaded();
        }

        internal static void Shutdown()
        {
            if (_harmony == null)
            {
                return;
            }

            try
            {
                _harmony.UnpatchSelf();
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade could not remove gameplay patches: " + exception.Message);
            }

            _harmony = null;
            _initialized = false;
        }
    }
}
