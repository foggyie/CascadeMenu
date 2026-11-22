using System;
using HarmonyLib;

namespace CascadeMenu.Patches
{
    /// <summary>
    /// Installs and uninstalls all Harmony patches in <c>CascadeMenu.Patches</c>.
    /// Each patch is applied individually with safe error handling so a game update
    /// affecting one target never breaks the rest of the menu.
    /// </summary>
    internal static class PatchManager
    {
        private static Harmony _harmony;

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
                    "Cascade: " + feature + " patch skipped (" + exception.Message + ").");
                return false;
            }
        }

        internal static void Install()
        {
            if (_harmony != null)
            {
                return;
            }

            _harmony = new Harmony(PluginInfo.GUID + ".patches");

            int installed = 0;
            if (TryPatch(typeof(HandLinkBlockPatch), "hand link block")) installed++;
            if (TryPatch(typeof(FlyGravityPatch), "fly gravity")) installed++;
            if (TryPatch(typeof(LocalRigGuardPatches.OnDisablePatch), "rig guard OnDisable")) installed++;
            if (TryPatch(typeof(LocalRigGuardPatches.AwakePatch), "rig guard Awake")) installed++;
            if (TryPatch(typeof(LocalRigGuardPatches.PostTickPatch), "rig guard PostTick")) installed++;
            if (TryPatch(typeof(PlatformDeployerPatch), "instant platform gadget")) installed++;
            if (TryPatch(typeof(QuitBoxSafetyPatch), "no quit box")) installed++;
            if (TryPatch(typeof(GTPlayerStatsSpoofPatch), "stat spoofer")) installed++;
            if (TryPatch(typeof(SetupVoicePatch), "high quality mic")) installed++;

            Plugin.Log?.LogInfo("Cascade installed " + installed + " Harmony patches.");
        }

        internal static void Uninstall()
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
                Plugin.Log?.LogWarning("Cascade could not remove patches: " + exception.Message);
            }

            _harmony = null;
        }
    }
}
