using System;
using System.Reflection;
using HarmonyLib;
using CascadeMenu.Mods.Tools;

namespace CascadeMenu.Patches
{
    /// <summary>
    /// Automatically re-applies High Quality Microphone settings whenever
    /// NetworkSystemPUN.SetupVoice runs on room join.
    /// </summary>
    [HarmonyPatch]
    internal static class SetupVoicePatch
    {
        private static Type _netPunType;

        private static bool Prepare()
        {
            _netPunType = AccessTools.TypeByName("NetworkSystemPUN");
            return _netPunType != null && AccessTools.Method(_netPunType, "SetupVoice") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(_netPunType, "SetupVoice");
        }

        private static void Postfix()
        {
            if (HighQualityMicMod.Active)
            {
                HighQualityMicMod.ApplyVoiceSettings(true);
            }
        }
    }
}
