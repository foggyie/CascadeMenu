using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace CascadeMenu
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        /// <summary>Convenient static reference to the root GameObject so other systems can hook in.</summary>
        public static GameObject Root;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

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
