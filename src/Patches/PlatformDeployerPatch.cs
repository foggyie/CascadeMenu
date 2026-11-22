using System;
using System.Reflection;
using HarmonyLib;

namespace CascadeMenu.Patches
{
    /// <summary>
    /// Removes the cooldown delay on the Super Infection Platform Deployer gadget
    /// (authority-side remainingRechargeTime = 0).
    /// </summary>
    [HarmonyPatch]
    internal static class PlatformDeployerPatch
    {
        internal static bool Enabled;
        private static Type _deployerType;
        private static FieldInfo _rechargeField;

        private static bool Prepare()
        {
            _deployerType = AccessTools.TypeByName("SIGadgetPlatformDeployer");
            if (_deployerType == null) return false;
            MethodInfo m = AccessTools.Method(_deployerType, "OnUpdateAuthority");
            if (m == null) return false;
            _rechargeField = _deployerType.GetField("remainingRechargeTime",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return _rechargeField != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(_deployerType, "OnUpdateAuthority");
        }

        private static void Prefix(object __instance)
        {
            if (!Enabled || __instance == null || _rechargeField == null) return;
            try
            {
                _rechargeField.SetValue(__instance, 0f);
            }
            catch { }
        }
    }
}
