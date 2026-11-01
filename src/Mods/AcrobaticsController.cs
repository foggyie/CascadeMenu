using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Body-rotation ("acrobatics") mode, ported from GorillaAcrobatics.
    ///
    /// Click the right joystick to start tracking the head. While active the body
    /// rotation follows the difference between the current head rotation and the
    /// rotation that was recorded when the mode was armed.
    ///
    /// The Cascade menu never copies the head rotation (see <see cref="MenuAnchor"/>),
    /// so the window stays level even when the body is upside down.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    internal sealed class AcrobaticsController : MonoBehaviour
    {
        private static AcrobaticsController _instance;

        private MenuConfig _config;

        private bool _wasPressed;
        private bool _active;
        private bool _pendingActivate;
        private int _resetFrames;

        private Quaternion _referenceHeadRot = Quaternion.identity;
        private Quaternion _currentRot = Quaternion.identity;

        internal static bool IsActive { get; private set; }

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            _instance = this;

            // A saved "on" value must never arm body rotation at load time. Every
            // session starts idle; the bind (or the settings toggle) turns it on.
            _active = false;
            IsActive = false;
            _pendingActivate = false;
            _wasPressed = false;
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

            // The config mirrors the live state, so an "off" value acts as a
            // kill switch. It is never used to auto-activate on startup.
            if (!_config.Acrobatics.Value) Deactivate();

            // Pressing the bind flips the mode on/off, exactly like joystick fly.
            HandleToggle();

            if (!_active)
            {
                // The head transform can show up a frame or two after the bind
                // was pressed, so keep retrying while the mode is pending.
                if (_pendingActivate) Activate();
                if (!_active)
                {
                    ApplyResetFrames();
                    return;
                }
            }

            Transform head = GetHead();
            if (head == null)
            {
                // Head not yet available - pend activation for the next frame.
                _pendingActivate = true;
                return;
            }

            Quaternion target = head.rotation * Quaternion.Inverse(_referenceHeadRot);
            float smoothing = Mathf.Max(0.01f, _config.AcrobaticsSmoothing.Value);
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            _currentRot = Quaternion.Slerp(_currentRot, target, t);

            GTPlayerTransform.ApplyRotationOverride(_currentRot, Time.frameCount);
            IsActive = true;
        }

        private void HandleToggle()
        {
            bool pressed = RightStickClicked();
            if (pressed && !_wasPressed)
            {
                Toggle();
                // Keep the persisted flag in sync so the settings page shows the
                // real state, and so the bind alone turns the mode on. A pending
                // activation (head not ready yet) still counts as "on".
                _config.Acrobatics.Value = _active || _pendingActivate;
            }
            _wasPressed = pressed;
        }

        internal bool Toggle()
        {
            if (_active) Deactivate();
            else Activate();
            return _active;
        }

        /// <summary>Forces acrobatics on or off (used by the settings page).</summary>
        internal static bool SetActive(bool active)
        {
            if (_instance == null || _instance._config == null)
                return false;

            if (active) _instance.Activate();
            else _instance.Deactivate();

            _instance._config.Acrobatics.Value = _instance._active || _instance._pendingActivate;
            return _instance._active;
        }

        /// <summary>Flips acrobatics from the Movement page (same as the stick click).</summary>
        internal static bool ToggleFromMenu()
        {
            if (_instance == null || _instance._config == null)
                return false;

            bool on = _instance._active || _instance._pendingActivate;
            return SetActive(!on);
        }

        private void Activate()
        {
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
            Plugin.Log?.LogInfo("Cascade acrobatics enabled.");
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
            Plugin.Log?.LogInfo("Cascade acrobatics disabled.");
        }

        private void ApplyResetFrames()
        {
            if (_resetFrames <= 0) return;

            GTPlayerTransform.ApplyRotationOverride(Quaternion.identity, Time.frameCount);
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