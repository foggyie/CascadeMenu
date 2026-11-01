using System;

namespace CascadeMenu.Mods
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginPreferencedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class PluginToggleableAttribute : Attribute { }

    /// <summary>
    /// Base class for modular toggles and actions imported from ResurgencePlugin,
    /// adapted for CascadeMenu.
    /// </summary>
    public abstract class Mod
    {
        public string Name { get; set; } = string.Empty;
        public bool isToggle { get; set; }
        public bool Enabled { get; private set; }

        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        public virtual void Update() { }
        public virtual void LateUpdate() { }
        public virtual void Execute() { }

        public void SetEnabled(bool value)
        {
            if (Enabled == value) return;
            Enabled = value;
            if (Enabled)
            {
                OnEnable();
            }
            else
            {
                OnDisable();
            }
        }

        public void Toggle()
        {
            SetEnabled(!Enabled);
        }
    }
}

