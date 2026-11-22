using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Desktop
{
    /// <summary>Shared access to the live menu config for the desktop mods.</summary>
    internal static class DesktopConfig
    {
        internal static MenuConfig Current()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            return menu != null ? menu.Config : null;
        }
    }

    /// <summary>Locks the desktop cursor to the centre of the window.</summary>
    [PluginToggleable]
    [ModCategory("Desktop")]
    public sealed class LockCursorMod : Mod
    {
        private CursorLockMode _previous;
        private bool _saved;

        public LockCursorMod()
        {
            Name = "Lock Cursor";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previous = Cursor.lockState;
            _saved = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            Cursor.lockState = _previous;
            Cursor.visible = true;
            _saved = false;
        }
    }

    /// <summary>Forces the desktop cursor visible.</summary>
    [PluginToggleable]
    [ModCategory("Desktop")]
    public sealed class ShowCursorMod : Mod
    {
        private bool _previous;
        private CursorLockMode _previousLock;
        private bool _saved;

        public ShowCursorMod()
        {
            Name = "Show Cursor";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previous = Cursor.visible;
            _previousLock = Cursor.lockState;
            _saved = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            Cursor.visible = _previous;
            Cursor.lockState = _previousLock;
            _saved = false;
        }
    }

    /// <summary>Shared menu-scale edit that restores the prior value.</summary>
    public abstract class MenuScaleMod : Mod
    {
        private float _previous;
        private bool _saved;

        protected MenuScaleMod()
        {
            isToggle = true;
        }

        protected abstract float Wanted { get; }

        public override void OnEnable()
        {
            MenuConfig config = DesktopConfig.Current();
            if (config == null) return;

            _previous = config.Scale.Value;
            _saved = true;
            config.Scale.Value = Wanted;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            MenuConfig config = DesktopConfig.Current();
            if (config != null) config.Scale.Value = _previous;
            _saved = false;
        }
    }

    [PluginToggleable]
    [ModCategory("Desktop")]
    public sealed class BigMenuMod : MenuScaleMod
    {
        private float _scale = 1.35f;

        public BigMenuMod()
        {
            Name = "Big Menu";

            AddSetting(ModSetting.Slider(
                "scale", "Scale",
                "Menu scale applied while the mod is on",
                1.05f, 2f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));
        }

        protected override float Wanted
        {
            get { return _scale; }
        }
    }

    [PluginToggleable]
    [ModCategory("Desktop")]
    public sealed class CompactMenuMod : MenuScaleMod
    {
        private float _scale = 0.75f;

        public CompactMenuMod()
        {
            Name = "Compact Menu";

            AddSetting(ModSetting.Slider(
                "scale", "Scale",
                "Menu scale applied while the mod is on",
                0.45f, 0.95f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));
        }

        protected override float Wanted
        {
            get { return _scale; }
        }
    }

    /// <summary>Pulls the menu closer to the face.</summary>
    [PluginToggleable]
    [ModCategory("Desktop")]
    public sealed class MenuNearMod : Mod
    {
        private float _previous;
        private bool _saved;
        private float _distance = 0.45f;

        public MenuNearMod()
        {
            Name = "Menu Near";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "distance", "Distance",
                "Menu distance in metres",
                0.3f, 1.2f,
                () => _distance,
                value => _distance = value,
                value => value.ToString("0.00") + " m"));
        }

        public override void OnEnable()
        {
            MenuConfig config = DesktopConfig.Current();
            if (config == null) return;

            _previous = config.Distance.Value;
            _saved = true;
            config.Distance.Value = _distance;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            MenuConfig config = DesktopConfig.Current();
            if (config != null) config.Distance.Value = _previous;
            _saved = false;
        }
    }
}
