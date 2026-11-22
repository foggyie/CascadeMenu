using GorillaLocomotion;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Movement
{
    /// <summary>
    /// Body-rotation ("acrobatics") mode, ported from GorillaAcrobatics.
    ///
    /// The menu switch turns it on or off directly. Arming never waits for an
    /// in-game gesture, so one button starts and stops the effect.
    ///
    /// While engaged, the body follows the head so leaning, spinning and
    /// flipping with your own movement feel natural. The Cascade menu never
    /// copies the head rotation (see <see cref="MenuAnchor"/>), so the window
    /// stays level even when the body is upside down.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    internal sealed class AcrobaticsController : MonoBehaviour
    {
        private static AcrobaticsController _instance;

        private MenuConfig _config;

        private bool _active;
        private bool _pendingActivate;
        private int _resetFrames;

        private Quaternion _referenceHeadRot = Quaternion.identity;
        private Quaternion _currentRot = Quaternion.identity;

        internal static bool IsActive { get; private set; }

        /// <summary>True when the menu switch is on.</summary>
        internal static bool IsArmed
        {
            get { return _instance != null && _instance._config != null && _instance._config.Acrobatics.Value; }
        }

        /// <summary>True while body rotation is actually running.</summary>
        internal static bool IsEngaged
        {
            get { return _instance != null && _instance._active; }
        }

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            _instance = this;

            // A saved "on" value must never rotate the body at load time. Every
            // session starts off; the menu switch turns it on directly.
            _active = false;
            IsActive = false;
            _pendingActivate = false;
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

            if (!_config.Acrobatics.Value)
            {
                // Kill switch. This also stops a mode that was left pending
                // because the head transform was not ready yet.
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

        internal bool Toggle()
        {
            bool next = _config != null && _config.Acrobatics.Value;
            return SetActive(!next);
        }

        /// <summary>
        /// Turns acrobatics on or off (used by the menu and settings page).
        /// On starts body rotation immediately.
        /// </summary>
        internal static bool SetActive(bool active)
        {
            if (_instance == null || _instance._config == null)
                return false;

            _instance._config.Acrobatics.Value = active;
            if (!active)
            {
                _instance.Deactivate();
            }

            return _instance._config.Acrobatics.Value;
        }

        /// <summary>Flips the switch from the Movement page.</summary>
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
