using System;
using System.Reflection;
using HarmonyLib;

namespace CascadeMenu.Patches
{
    /// <summary>
    /// Reflection-safe Harmony patch on GorillaQuitBox.OnBoxTriggered that
    /// prevents out-of-bounds quit boxes from closing the game.
    /// </summary>
    [HarmonyPatch]
    internal static class QuitBoxSafetyPatch
    {
        internal static bool BlockQuit;
        private static Type _quitBoxType;

        private static bool Prepare()
        {
            _quitBoxType = AccessTools.TypeByName("GorillaQuitBox");
            return _quitBoxType != null && AccessTools.Method(_quitBoxType, "OnBoxTriggered") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(_quitBoxType, "OnBoxTriggered");
        }

        private static bool Prefix()
        {
            return !BlockQuit;
        }
    }
}
