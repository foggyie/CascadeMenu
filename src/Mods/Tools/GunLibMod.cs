using System;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Job 2 adapter for the gun library.
    ///
    /// The gun predates the generic mod system, so this tiny mod exposes its
    /// two states to the Mods tab:
    /// - Enabled arms/disarms the gun (GunEnabled).
    /// - Active starts/stops the effect (GunActive).
    ///
    /// The GunLib tab edits the actual tuning values. The mod has a settings
    /// gear and a keybind like any other mod, and the GunLib tab links to that
    /// settings page.
    /// </summary>
    [PluginToggleable]
    public sealed class GunLibMod : Mod
    {
        internal static GunLibMod Instance { get; private set; }

        private MenuConfig _config;

        public GunLibMod()
        {
            Name = "GunLib";
            isToggle = true;
            EnableSettings();
            Instance = this;
        }

        /// <summary>Loads the saved gun state once the config exists.</summary>
        internal void Bind(MenuConfig config)
        {
            _config = config;
            if (_config == null) return;

            SetEnabled(_config.GunEnabled.Value);
            if (Enabled && _config.GunActive.Value && !IsActive)
            {
                SetActive(true);
            }

            Plugin.Log?.LogInfo("Cascade GunLib mod: Enabled=" + Enabled + " Active=" + IsActive);
        }

        /// <summary>
        /// Creates every mod (if needed) and binds the gun wrapper. Called by
        /// <see cref="GameplayMods.Initialize"/> right after the registry is
        /// ready, so the gun has an Enabled/Active state before the first frame.
        /// </summary>
        internal static void EnsureBound(MenuConfig config)
        {
            ModRegistry.EnsureLoaded();
            GunLibMod mod = Instance;
            if (mod == null)
            {
                Plugin.Log?.LogWarning("Cascade GunLib mod: wrapper was not created.");
                return;
            }
            mod.Bind(config);
        }

        public override void OnActivate()
        {
            if (_config != null) _config.GunActive.Value = true;
        }

        public override void OnDeactivate()
        {
            if (_config != null) _config.GunActive.Value = false;
        }

        public override void OnEnable()
        {
            if (_config != null) _config.GunEnabled.Value = true;
        }

        public override void OnDisable()
        {
            if (_config != null)
            {
                _config.GunEnabled.Value = false;
                _config.GunActive.Value = false;
            }
        }

        /// <summary>Used by menu rows that want to arm the gun without a rebuild.</summary>
        internal static void SetArmed(MenuConfig config, bool value)
        {
            if (config != null) config.GunEnabled.Value = value;

            ModRegistry.EnsureLoaded();
            GunLibMod mod = Instance;
            if (mod != null)
            {
                mod._config = config;

                // Arming restores the saved Active state, exactly like startup.
                // Disarming goes through SetEnabled, which clears Active and
                // runs the cleanup hooks.
                mod.SetEnabled(value);
                if (value && config != null && config.GunActive.Value && !mod.IsActive)
                {
                    mod.SetActive(true);
                }
            }
        }

        internal static bool IsArmed(MenuConfig config)
        {
            ModRegistry.EnsureLoaded();
            GunLibMod mod = Instance;
            if (mod != null) return mod.Enabled;
            return config != null && config.GunEnabled.Value;
        }

        internal static bool IsRunning(MenuConfig config)
        {
            ModRegistry.EnsureLoaded();
            GunLibMod mod = Instance;
            if (mod != null) return mod.IsActive;
            return config != null && config.GunEnabled.Value && config.GunActive.Value;
        }

        internal static void ToggleRunning(MenuConfig config)
        {
            ModRegistry.EnsureLoaded();
            GunLibMod mod = Instance;
            if (mod != null)
            {
                mod._config = config;
                mod.ToggleActive();
                return;
            }

            if (config != null) config.GunActive.Value = !config.GunActive.Value;
        }
    }
}
