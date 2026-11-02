using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Body-rotation ("acrobatics") mode, ported from GorillaAcrobatics.
    ///
    /// It follows the same flow as the joystick fly mod: the menu switch (or
    /// its bind) arms the mod, and double-tapping A in game engages or
    /// disengages it. Arming alone never rotates the body.
    ///
    /// While engaged, the body follows the head so leaning, spinning and
    /// flipping with your own movement feel natural. The Cascade menu never
    /// copies the head rotation (see <see cref="MenuAnchor"/>), so the window
    /// stays level even when the body is upside down.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    internal sealed class AcrobaticsController : MonoBehaviour
    {
        private const float DoubleTapWindow = 0.5f;

        private static AcrobaticsController _instance;

        private MenuConfig _config;

        private bool _wasPressed;
        private bool _wasPrimaryButton;
        private float _lastTapTime = -10f;
        private bool _engaged;
        private bool _active;
        private bool _pendingActivate;
        private int _resetFrames;

        private Quaternion _referenceHeadRot = Quaternion.identity;
        private Quaternion _currentRot = Quaternion.identity;

        internal static bool IsActive { get; private set; }

        /// <summary>True when the menu switch is on (armed, waiting for the double tap).</summary>
        internal static bool IsArmed
        {
            get { return _instance != null && _instance._config != null && _instance._config.Acrobatics.Value; }
        }

        /// <summary>True while body rotation is actually running.</summary>
        internal static bool IsEngaged
        {
            get { return _instance != null && _instance._engaged; }
        }

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            _instance = this;

            // A saved "on" value must never rotate the body at load time. Every
            // session starts disarmed and disengaged; the menu switch or the
            // bind arms it, and the in-game double tap engages it.
            _engaged = false;
            _active = false;
            IsActive = false;
            _pendingActivate = false;
            _wasPressed = false;
            _wasPrimaryButton = false;
            _lastTapTime = -10f;
            _resetFrames = 0;
            if (config != null) config.Acrobatics.Value = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) Destroy(gameObject);
            else _instance = this;
        }

        private void LateUpdate()
        {
            if (_config == null) return;

            // The bind flips the same armed value the menu switch uses.
            HandleToggle();

            // Same gesture as joystick fly: double-tap A in game to engage.
            HandleDoubleTap();

            if (!_config.Acrobatics.Value)
            {
                // Kill switch. This also stops a mode that was left pending
                // because the head transform was not ready yet.
                _engaged = false;
                if (_active || _pendingActivate) Deactivate();
                ApplyResetFrames();
                return;
            }

            // Armed is not engaged. Wait for the double tap.
            if (!_engaged)
            {
                if (_active || _pendingActivate) Deactivate();
                ApplyResetFrames();
                return;
            }

            if (!_active)
            {
                Activate();
                if (!_active)
                {
                    // Head not ready yet; Activate() set the pending flag and the
                    // next frame will try again.
                    ApplyResetFrames();
                    return;
                }
            }

            Transform head = GetHead();
            if (head == null)
            {
                // Head transform disappeared (respawn / map load). Go back to
                // pending so the mode re-arms as soon as the rig is back.
                _active = false;
                _pendingActivate = true;
                IsActive = false;
                ApplyResetFrames();
                return;
            }

            Quaternion target = head.rotation * Quaternion.Inverse(_referenceHeadRot);
            float smoothing = Mathf.Max(0.01f, _config.AcrobaticsSmoothing.Value);
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            _currentRot = Quaternion.Slerp(_currentRot, target, t);

            GTPlayerTransform.ApplyRotationOverride(_currentRot, Time.frameCount);
            IsActive = true;
        }

        /// <summary>
        /// Mirrors the fly mod's activation: two quick presses of A while the
        /// menu is closed. The gesture is ignored while acrobatics is disarmed,
        /// so it can never engage by accident.
        /// </summary>
        private void HandleDoubleTap()
        {
            ControllerInputPoller poller = ControllerInputPoller.instance;
            bool pressed = poller != null && poller.rightControllerPrimaryButton;

            // A is the menu's select button. Do not treat menu clicks as a
            // double-tap gesture while Cascade is open.
            if (CascadeMenuBehaviour.IsMenuOpen || _config == null || !_config.Acrobatics.Value)
            {
                _wasPrimaryButton = pressed;
                _lastTapTime = -10f;
                return;
            }

            if (pressed && !_wasPrimaryButton)
            {
                float now = Time.unscaledTime;
                if (now - _lastTapTime <= DoubleTapWindow)
                {
                    _lastTapTime = -10f;
                    ToggleEngaged();
                }
                else
                {
                    _lastTapTime = now;
                }
            }

            _wasPrimaryButton = pressed;
        }

        /// <summary>Engages or disengages body rotation without disarming it.</summary>
        private void ToggleEngaged()
        {
            _engaged = !_engaged;
            if (_engaged) Activate();
            else Deactivate();
        }

        private void HandleToggle()
        {
            bool pressed = RightStickClicked();
            if (pressed && !_wasPressed)
            {
                bool next = !_config.Acrobatics.Value;
                SetActive(next);
                CascadeMenuBehaviour.Instance?.ShowToast("Acrobatics",
                    next ? "Armed - double-tap A in game." : "Off.");
            }
            _wasPressed = pressed;
        }

        internal bool Toggle()
        {
            bool next = _config != null && _config.Acrobatics.Value;
            return SetActive(!next);
        }

        /// <summary>
        /// Arms or disarms acrobatics (used by the menu and settings page).
        /// Arming never engages: the in-game double tap does, exactly like the
        /// joystick fly mod.
        /// </summary>
        internal static bool SetActive(bool active)
        {
            if (_instance == null || _instance._config == null)
                return false;

            _instance._config.Acrobatics.Value = active;
            if (!active)
            {
                _instance._engaged = false;
                _instance.Deactivate();
            }

            return _instance._config.Acrobatics.Value;
        }

        /// <summary>Flips the armed value from the Movement page (same as the bind).</summary>
        internal static bool ToggleFromMenu()
        {
            if (_instance == null || _instance._config == null)
                return false;

            return SetActive(!_instance._config.Acrobatics.Value);
        }

        private void Activate()
        {
            if (_config == null) return;

            Transform head = GetHead();
            if (head == null)
            {
                _pendingActivate = true;
                return;
            }

            _pendingActivate = false;
            _referenceHeadRot = head.rotation;
            _currentRot = Quaternion.identity;
            _resetFrames = 0;
            _active = true;
            IsActive = true;
            Buzz();
            Plugin.Log?.LogInfo("Cascade acrobatics engaged.");
        }

        private void Deactivate()
        {
            _pendingActivate = false;
            if (!_active) return;

            _active = false;
            IsActive = false;
            _currentRot = Quaternion.identity;
            _resetFrames = 3;
            Buzz();
            Plugin.Log?.LogInfo("Cascade acrobatics disengaged.");
        }

        private void ApplyResetFrames()
        {
            if (_resetFrames <= 0) return;

            // Put the body back upright while keeping the current yaw. Applying
            // identity here would face the body along world-forward instead.
            Transform head = GetHead();
            Quaternion upright = head != null
                ? Quaternion.Euler(0f, head.eulerAngles.y, 0f)
                : Quaternion.identity;

            GTPlayerTransform.ApplyRotationOverride(upright, Time.frameCount);
            _resetFrames--;
        }

        private static Transform GetHead()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player != null && player.headCollider != null) return player.headCollider.transform;
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null)
                return GorillaTagger.Instance.headCollider.transform;
            return null;
        }

        private bool RightStickClicked()
        {
            // VR: the configured controller button (default right stick click).
            // Works for A/X, B/Y, trigger, grip and either stick click.
            if (_config != null && VrInput.VrActive &&
                VrInput.Pressed(VrInput.Parse(_config.AcrobaticsButton.Value)))
            {
                return true;
            }

            // PC / keyboard fallback uses the rebindable "Acrobatics Key".
            Key key = Keybinds.Parse(_config != null ? _config.AcrobaticsKey.Value : "Delete", Key.Delete);
            return Keybinds.Pressed(key);
        }

        private static void Buzz()
        {
            if (GorillaTagger.Instance != null)
                GorillaTagger.Instance.StartVibration(false, 0.35f, 0.09f);
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
            Deactivate();
            ApplyResetFrames();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Deactivate();
            ApplyResetFrames();
        }
    }
}
