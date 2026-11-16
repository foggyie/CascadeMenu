using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// One-switch wrapper around the existing joystick flight controller. The
    /// mod mirrors the controller's config entries so the Movement page and this
    /// row always describe the same flight state.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Flight")]
    public sealed class JoystickFly : Mod
    {
        private static readonly string[] DirectionNames = { "Head", "Controller" };

        private float _speed = 6.5f;
        private float _vertical = 4.5f;
        private float _acceleration = 8f;
        private int _direction;

        public JoystickFly()
        {
            Name = "Joystick Fly";
            isToggle = true;

            AddSetting(ModSetting.Slider("speed", "Speed",
                "Horizontal flight speed",
                1.5f, 15f,
                () => _speed,
                value =>
                {
                    _speed = value;
                    ApplyToConfig();
                },
                value => value.ToString("0.0") + " m/s"));

            AddSetting(ModSetting.Slider("vertical", "Vertical speed",
                "Up and down speed from the stick",
                1.5f, 12f,
                () => _vertical,
                value =>
                {
                    _vertical = value;
                    ApplyToConfig();
                },
                value => value.ToString("0.0") + " m/s"));

            AddSetting(ModSetting.Slider("acceleration", "Acceleration",
                "How quickly flight reaches the requested speed",
                1f, 30f,
                () => _acceleration,
                value =>
                {
                    _acceleration = value;
                    ApplyToConfig();
                },
                value => value.ToString("0.0") + " m/s²"));

            AddSetting(ModSetting.Dropdown("direction", "Aim",
                "Head uses where you look; Controller uses the right controller",
                DirectionNames,
                () => _direction,
                value =>
                {
                    _direction = value;
                    ApplyToConfig();
                }));
        }

        private static MenuConfig Config
        {
            get
            {
                CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
                return menu != null ? menu.Config : null;
            }
        }

        public override void OnEnable()
        {
            MenuConfig config = Config;
            if (config != null)
            {
                // The Movement page edits the same entries, so start from the
                // live config instead of clobbering it with stale defaults.
                _speed = config.FlySpeed.Value;
                _vertical = config.FlyVerticalSpeed.Value;
                _acceleration = config.FlyAcceleration.Value;
                _direction = config.FlyDirection.Value;
                config.JoystickFly.Value = true;
            }
            Plugin.Log?.LogInfo("Cascade Joystick Fly active.");
        }

        public override void OnDisable()
        {
            MenuConfig config = Config;
            if (config != null) config.JoystickFly.Value = false;
            Plugin.Log?.LogInfo("Cascade Joystick Fly stopped.");
        }

        private void ApplyToConfig()
        {
            MenuConfig config = Config;
            if (config == null) return;

            config.FlySpeed.Value = _speed;
            config.FlyVerticalSpeed.Value = _vertical;
            config.FlyAcceleration.Value = _acceleration;
            config.FlyDirection.Value = Mathf.Clamp(_direction, 0, 1);
        }
    }
}
