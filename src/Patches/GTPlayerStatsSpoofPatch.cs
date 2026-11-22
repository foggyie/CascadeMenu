using System;
using System.Reflection;
using HarmonyLib;

namespace CascadeMenu.Patches
{
    /// <summary>
    /// Overrides the local FPS, Target FPS, and Ping values reported by GTPlayerStats.
    /// Uses safe reflection in Prepare/TargetMethod so it never breaks across game updates.
    /// </summary>
    [HarmonyPatch]
    internal static class GTPlayerStatsSpoofPatch
    {
        internal static bool Enabled;
        internal static bool SpoofFPS;
        internal static bool SpoofTargetFPS;
        internal static bool SpoofPing;
        internal static short FPS = 90;
        internal static short TargetFPS = 90;
        internal static short Ping = 40;

        private static Type _statsType;
        private static FieldInfo _fpsField;
        private static PropertyInfo _fpsProp;
        private static FieldInfo _targetFpsField;
        private static PropertyInfo _targetFpsProp;
        private static FieldInfo _pingField;
        private static PropertyInfo _pingProp;

        private static bool Prepare()
        {
            _statsType = AccessTools.TypeByName("GTPlayerStats");
            if (_statsType == null) return false;
            MethodInfo m = AccessTools.Method(_statsType, "DelayedUpdate");
            if (m == null) return false;

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            _fpsField = _statsType.GetField("FPS", flags);
            _fpsProp = _statsType.GetProperty("FPS", flags);
            _targetFpsField = _statsType.GetField("TargetFPS", flags);
            _targetFpsProp = _statsType.GetProperty("TargetFPS", flags);
            _pingField = _statsType.GetField("Ping", flags);
            _pingProp = _statsType.GetProperty("Ping", flags);
            return true;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(_statsType, "DelayedUpdate");
        }

        private static bool Prefix()
        {
            if (!Enabled || (!SpoofFPS && !SpoofTargetFPS && !SpoofPing))
                return true;

            try
            {
                if (SpoofFPS) WriteMember(_fpsField, _fpsProp, FPS);
                if (SpoofTargetFPS) WriteMember(_targetFpsField, _targetFpsProp, TargetFPS);
                if (SpoofPing) WriteMember(_pingField, _pingProp, Ping);
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static void WriteMember(FieldInfo field, PropertyInfo prop, short value)
        {
            if (field != null)
            {
                field.SetValue(null, Convert.ChangeType(value, field.FieldType));
            }
            else if (prop != null && prop.CanWrite)
            {
                prop.SetValue(null, Convert.ChangeType(value, prop.PropertyType), null);
            }
        }
    }
}
