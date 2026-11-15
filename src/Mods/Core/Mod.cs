using System;
using System.Collections.Generic;
using InputKey = UnityEngine.InputSystem.Key;

namespace CascadeMenu.Mods
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginPreferencedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class PluginToggleableAttribute : Attribute { }

    /// <summary>How many presses of a mod's key are needed to toggle it.</summary>
    public enum ModPressType
    {
        Single = 0,
        Double = 1
    }

    /// <summary>
    /// Base class for modular toggles and actions imported from ResurgencePlugin,
    /// adapted for CascadeMenu.
    ///
    /// The menu exposes one switch per mod, driven through
    /// <see cref="ModRegistry.SetEffect"/>:
    /// - switching on arms the mod and starts its effect in the same click;
    /// - switching off stops the effect, runs cleanup and disarms the mod.
    ///
    /// The two underlying states remain available:
    /// - <see cref="Enabled"/>: the menu switch / keybind permission.
    /// - <see cref="IsActive"/>: the effect is actually running.
    ///
    /// Existing mods keep working unchanged: their OnEnable/OnDisable methods are
    /// called when the effect starts/stops, and OnActivate/OnDeactivate are extra
    /// hooks for new mods that need to tell the two transitions apart.
    /// </summary>
    public abstract class Mod
    {
        public string Name { get; set; } = string.Empty;
        public bool isToggle { get; set; }

        /// <summary>Keyboard key that controls this mod. Unbound by default.</summary>
        public InputKey Key { get; set; } = InputKey.None;

        /// <summary>Single press toggles immediately; double press needs two taps.</summary>
        public ModPressType PressType { get; set; } = ModPressType.Single;

        /// <summary>
        /// True when this mod declares extra options. Only these mods draw a
        /// gear on their row, so the rest keep the compact layout.
        /// </summary>
        public bool HasSettings { get; private set; }

        private readonly List<ModSetting> _settings = new List<ModSetting>();

        /// <summary>Options shown on this mod's generated settings page.</summary>
        public IReadOnlyList<ModSetting> Settings
        {
            get { return _settings; }
        }

        /// <summary>
        /// Registers one extra option and turns on the gear. Call this from the
        /// mod's constructor; the registry persists the values automatically.
        /// </summary>
        protected ModSetting AddSetting(ModSetting setting)
        {
            if (setting == null) return null;
            _settings.Add(setting);
            HasSettings = true;
            return setting;
        }

        /// <summary>
        /// Marks the mod as having a settings page even without extra options.
        /// </summary>
        protected void EnableSettings()
        {
            HasSettings = true;
        }

        /// <summary>
        /// True when this mod shows the Single/Double press selector. It stays
        /// hidden by default; call <see cref="EnablePressChoice"/> only for mods
        /// that actually want a double-press bind.
        /// </summary>
        public bool HasPressChoice { get; private set; }

        /// <summary>
        /// Opts this mod into the Single/Double press selector on its settings
        /// page. Also turns on the settings gear.
        /// </summary>
        protected void EnablePressChoice()
        {
            HasPressChoice = true;
            HasSettings = true;
        }

        /// <summary>The menu toggle: the mod is armed and its keybind works.</summary>
        public bool Enabled { get; private set; }

        /// <summary>True while the mod is armed (alias used by the new API).</summary>
        public bool IsEnabled
        {
            get { return Enabled; }
        }

        /// <summary>True while the effect is actually running.</summary>
        public bool IsActive
        {
            get { return _active; }
        }

        private bool _active;
        private bool _effectRunning;

        public virtual void OnEnable() { }
        public virtual void OnDisable() { }

        /// <summary>Called when the effect starts, before <see cref="OnEnable"/>.</summary>
        public virtual void OnActivate() { }

        /// <summary>Called when the effect stops, before <see cref="OnDisable"/>.</summary>
        public virtual void OnDeactivate() { }

        public virtual void Update() { }
        public virtual void LateUpdate() { }
        public virtual void Execute() { }

        /// <summary>
        /// Enables or disables the mod. The registry's one-switch path calls
        /// this and then starts or stops the effect, so a menu toggle never
        /// leaves a mod armed but idle. Disabling clears Active and runs the
        /// cleanup path exactly when an effect was running.
        /// </summary>
        public void SetEnabled(bool value)
        {
            if (Enabled == value) return;

            Enabled = value;
            if (!Enabled && _active)
            {
                SetActive(false);
            }
        }

        /// <summary>
        /// Starts or stops the effect of a toggle mod. One-shot mods run
        /// <see cref="Execute"/> instead and never report IsActive.
        /// </summary>
        public void SetActive(bool value)
        {
            if (!isToggle)
            {
                if (value && Enabled)
                {
                    Invoke("execute", Execute);
                }
                return;
            }

            if (_active == value) return;
            if (value && !Enabled) return;

            if (value)
            {
                _active = true;
                _effectRunning = true;
                Invoke("activate", OnActivate);
                Invoke("enable", OnEnable);
            }
            else
            {
                _active = false;
                if (!_effectRunning) return;

                _effectRunning = false;
                Invoke("deactivate", OnDeactivate);
                Invoke("disable", OnDisable);
            }
        }

        /// <summary>The keybind action: flips Active for toggles, runs actions once.</summary>
        public void ToggleActive()
        {
            if (!Enabled) return;

            if (isToggle)
            {
                SetActive(!_active);
            }
            else
            {
                Invoke("execute", Execute);
            }
        }

        public void Toggle()
        {
            SetEnabled(!Enabled);
        }

        private void Invoke(string phase, Action action)
        {
            if (action == null) return;

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError(
                    "Cascade mod \"" + Name + "\" failed to " + phase + ": " + exception);
            }
        }
    }
}
