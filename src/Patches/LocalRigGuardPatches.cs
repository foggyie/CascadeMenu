using HarmonyLib;

namespace CascadeMenu.Patches
{
    /// <summary>
    /// Keeps the local VRRig alive and prevents PostTick from running while
    /// VRRig.LocalRig is intentionally paused/disabled by rig mods.
    /// </summary>
    internal static class LocalRigGuardPatches
    {
        internal static bool Enabled;

        [HarmonyPatch(typeof(VRRig), "OnDisable")]
        internal static class OnDisablePatch
        {
            private static bool Prefix(VRRig __instance)
            {
                return !Enabled || __instance == null || !__instance.isLocal;
            }
        }

        [HarmonyPatch(typeof(VRRig), "Awake")]
        internal static class AwakePatch
        {
            private static bool Prefix(VRRig __instance)
            {
                return !Enabled || __instance == null || __instance.gameObject.name != "Local Gorilla Player(Clone)";
            }
        }

        [HarmonyPatch(typeof(VRRig), "PostTick")]
        internal static class PostTickPatch
        {
            private static bool Prefix(VRRig __instance)
            {
                return !Enabled || __instance == null || !__instance.isLocal || __instance.enabled;
            }
        }
    }
}
