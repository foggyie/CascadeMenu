using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace CascadeMenu
{
    /// <summary>
    /// Central gated logger for CascadeMenu.
    /// When <see cref="Enabled"/> is false (controlled by the Debug Log toggle
    /// in Settings), all <c>Plugin.Log</c> calls are suppressed with zero overhead.
    /// </summary>
    internal sealed class CascadeLogger
    {
        private readonly ManualLogSource _source;

        /// <summary>Master switch synced with <see cref="MenuConfig.VerboseLogging"/>.</summary>
        public static bool Enabled { get; set; }

        public CascadeLogger(ManualLogSource source)
        {
            _source = source;
        }

        public void LogInfo(object data)
        {
            if (Enabled && _source != null) _source.LogInfo(data);
        }

        public void LogWarning(object data)
        {
            if (Enabled && _source != null) _source.LogWarning(data);
        }

        public void LogError(object data)
        {
            if (Enabled && _source != null) _source.LogError(data);
        }

        public void LogDebug(object data)
        {
            if (Enabled && _source != null) _source.LogDebug(data);
        }

        public void LogMessage(object data)
        {
            if (Enabled && _source != null) _source.LogMessage(data);
        }

        public void LogFatal(object data)
        {
            if (Enabled && _source != null) _source.LogFatal(data);
        }
    }

    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance { get; private set; }
        internal static CascadeLogger Log { get; private set; }

        /// <summary>Convenient static reference to the root GameObject so other systems can hook in.</summary>
        public static GameObject Root;

        private void Awake()
        {
            Instance = this;
            Log = new CascadeLogger(Logger);

            CascadeMenu.Mods.Tools.DebugLogListener.Attach();

            EnsureRoot();

            Log.LogInfo(PluginInfo.NAME + " v" + PluginInfo.VERSION + " loaded.");
        }

        private void EnsureRoot()
        {
            if (Root != null) return;

            Root = new GameObject("CascadeMenu_Root");
            DontDestroyOnLoad(Root);
            Root.AddComponent<CascadeMenuBehaviour>();
        }

        private void OnDestroy()
        {
            if (Root != null)
            {
                Destroy(Root);
                Root = null;
            }

            // Keep Log assigned for other components to write during shutdown
            Instance = null;
        }
    }
}
