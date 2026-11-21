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

        private static bool TryPatch(Type patchType, string feature)
        {
            try
            {
                _harmony.CreateClassProcessor(patchType).Patch();
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning(
                    "Cascade: " + feature + " disabled, patch target not found (game update?). " + exception.Message);
                return false;
            }
        }

        internal static void Initialize(MenuConfig config, GameObject host)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _harmony = new Harmony(PluginInfo.GUID + ".gameplay");
            bool handLink = TryPatch(typeof(HandLinkBlockPatch), "hand link block");
            bool flyGravity = TryPatch(typeof(FlyGravityPatch), "fly gravity");
            if (handLink && flyGravity)
            {
                Plugin.Log?.LogInfo("Cascade gameplay patches installed (hand link + fly gravity).");
            }
            else if (handLink)
            {
                Plugin.Log?.LogInfo("Cascade gameplay patches installed (hand link only).");
            }
            else if (flyGravity)
            {
                Plugin.Log?.LogInfo("Cascade gameplay patches installed (fly gravity only).");
            }

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

            // Drives every Mod subclass found by ModRegistry (Halloween, Super, ...).
            host.AddComponent<ModHost>();
            ModRegistry.Initialize(config);

            // The gun predates the mod system, so its thin Mod wrapper needs the
            // config after the registry created every mod instance.
            GunLibMod.EnsureBound(config);
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
