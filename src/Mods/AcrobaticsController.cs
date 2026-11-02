using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Body-rotation ("acrobatics") mode, ported from GorillaAcrobatics.
    ///
    /// The config entry is the source of truth: on means "body rotation should
    /// be running", off means "body rotation should be stopped". Both the menu
    /// toggle and the bind flip that one value, so they can never disagree.
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
            // session starts idle; the bind (or a menu toggle) turns it on.
            _active = false;
            IsActive = false;
            _pendingActivate = false;
            _wasPressed = false;
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

            // The bind is just another way to flip the same on/off intent.
            HandleToggle();

            if (!_config.Acrobatics.Value)
            {
                // Config off = kill switch. This also stops a mode that was left
                // pending because the head transform was not ready yet.
                if (_active || _pendingActivate) Deactivate();
                ApplyResetFrames();
                return;
            }

            // Config on = activate. This is the path the menu toggle uses, so
            // flipping the switch is enough - the bind is not required.
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

        private void HandleToggle()
        {
            bool pressed = RightStickClicked();
            if (pressed && !_wasPressed)
            {
                bool next = !_config.Acrobatics.Value;
                _config.Acrobatics.Value = next;
                if (next) Activate();
                else Deactivate();
            }
            _wasPressed = pressed;
        }

        internal bool Toggle()
        {
            bool next = _config != null && _config.Acrobatics.Value;
            return SetActive(!next);
        }

        /// <summary>Forces acrobatics on or off (used by the menu and settings page).</summary>
        internal static bool SetActive(bool active)
        {
            if (_instance == null || _instance._config == null)
                return false;

            _instance._config.Acrobatics.Value = active;
            if (active) _instance.Activate();
            else _instance.Deactivate();

            // Report the intent, not just the instantaneous head state, so the
            // settings switch stays on while a pending activation waits for the rig.
            return _instance._config.Acrobatics.Value;
        }

        /// <summary>Flips acrobatics from the Movement page (same as the stick click).</summary>
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
