using System;
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
    /// A mod has two independent states:
    /// - <see cref="Enabled"/>: the menu toggle. It arms the mod and lets its
    ///   keybind work, but does not start the effect by itself.
    /// - <see cref="IsActive"/>: the effect is actually running. Only the keybind
    ///   or the menu's Activate button changes this.
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
        /// Enables or disables the mod. Enabling only arms the mod; the effect
        /// starts when the keybind or the menu's Activate button is used.
        /// Disabling clears Active and runs the cleanup path exactly when an
        /// effect was running.
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
