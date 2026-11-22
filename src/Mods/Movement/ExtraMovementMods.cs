using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CascadeMenu.Mods.Core;

namespace CascadeMenu.Mods.Movement
{
    /// <summary>
    /// Press A / Primary button (VR) or Space (PC) in mid-air for an instant
    /// upward double-jump + forward boost.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class DoubleJumpMod : Mod
    {
        private float _jumpPower = 8.5f;
        private float _forwardBoost = 3.5f;
        private float _cooldown = 0.65f;
        private float _nextJumpTime;
        private bool _wasPressed;

        public DoubleJumpMod()
        {
            Name = "Double Jump";
            isToggle = true;

            AddSetting(ModSetting.Slider("power", "Upward power",
                "Vertical boost velocity applied on mid-air jump",
                4f, 18f, () => _jumpPower, v => _jumpPower = v, v => v.ToString("0.0") + " m/s"));

            AddSetting(ModSetting.Slider("forward", "Forward boost",
                "Extra forward push in the direction you're looking",
                0f, 12f, () => _forwardBoost, v => _forwardBoost = v, v => v.ToString("0.0") + " m/s"));

            AddSetting(ModSetting.Slider("cooldown", "Cooldown",
                "Minimum seconds between mid-air jumps",
                0.2f, 2.5f, () => _cooldown, v => _cooldown = v, v => v.ToString("0.00") + " s"));
        }

        public override void Update()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;

            bool pressed = false;
            if (XRSettings.isDeviceActive && ControllerInputPoller.instance != null)
            {
                pressed = ControllerInputPoller.instance.rightControllerPrimaryButton;
            }
            else if (Keyboard.current != null)
            {
                pressed = Keyboard.current.spaceKey.isPressed;
            }

            bool justPressed = pressed && !_wasPressed;
            _wasPressed = pressed;

            if (!justPressed || Time.time < _nextJumpTime) return;
            _nextJumpTime = Time.time + _cooldown;

            Vector3 fwd = Camera.main != null ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 vel = rb.linearVelocity;
            vel.y = Mathf.Max(vel.y, 0f) + _jumpPower;
            vel += fwd * _forwardBoost;
            rb.linearVelocity = vel;
        }
    }

    /// <summary>
    /// Custom gravity multiplier (Moon Walk low-gravity float or Heavy Slam).
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class CustomGravityMod : Mod
    {
        private float _gravityScale = 0.35f;
        private Vector3 _savedGravity = new Vector3(0f, -9.81f, 0f);
        private bool _applied;

        public CustomGravityMod()
        {
            Name = "Custom Gravity (Moon Walk)";
            isToggle = true;

            AddSetting(ModSetting.Slider("scale", "Gravity scale",
                "0.15x = Moon Walk float, 1.0x = normal, 2.0x = heavy gravity",
                0.10f, 2.0f, () => _gravityScale, v =>
                {
                    _gravityScale = v;
                    if (_applied) Physics.gravity = new Vector3(0f, -9.81f * _gravityScale, 0f);
                }, v => v.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            _savedGravity = Physics.gravity;
            _applied = true;
            Physics.gravity = new Vector3(0f, -9.81f * _gravityScale, 0f);
        }

        public override void OnDisable()
        {
            if (_applied)
            {
                Physics.gravity = _savedGravity;
                _applied = false;
            }
        }
    }

    /// <summary>
    /// Freezes your Gorilla cleanly in mid-air with zero drift.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class AirFreezeMod : Mod
    {
        private bool _requireGrip = false;
        private Vector3 _lockedPos;
        private bool _hasLock;

        public AirFreezeMod()
        {
            Name = "Hover / Air Freeze";
            isToggle = true;

            AddSetting(ModSetting.Toggle("gripOnly", "Hold Left Grip to freeze",
                "When ON, only freezes while holding Left Grip (or Left Alt on PC). When OFF, freezes immediately.",
                () => _requireGrip, v => _requireGrip = v));
        }

        public override void OnEnable()
        {
            _hasLock = false;
        }

        public override void OnDisable()
        {
            _hasLock = false;
        }

        public override void Update()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;

            bool active = !_requireGrip;
            if (_requireGrip)
            {
                if (XRSettings.isDeviceActive && ControllerInputPoller.instance != null)
                    active = ControllerInputPoller.instance.leftControllerGripFloat > 0.6f;
                else if (Keyboard.current != null)
                    active = Keyboard.current.leftAltKey.isPressed;
            }

            if (!active)
            {
                _hasLock = false;
                return;
            }

            if (!_hasLock)
            {
                _lockedPos = player.transform.position;
                _hasLock = true;
            }

            rb.linearVelocity = Vector3.zero;
            player.transform.position = _lockedPos;
        }
    }
}
