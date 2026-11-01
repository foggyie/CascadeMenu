using System.Collections.Generic;
using GorillaLocomotion;
using GorillaTag.Gravity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Runs the real gameplay features:
    /// - double-tap A toggles head-relative joystick flight;
    /// - the left stick moves in the direction the player is looking;
    /// - gravity is cancelled for the local player while flight is active;
    /// - the Hand Link patch is enabled/disabled from the menu config.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class JoystickFlyController : MonoBehaviour
    {
        private const float DoubleTapWindow = 0.5f;

        private static JoystickFlyController _instance;

        private MenuConfig _config;
        private GTPlayer _player;

        private bool _wasPrimaryButton;
        private float _lastTapTime = -10f;
        private float _lastStickLogTime = -10f;
        private bool _missingPlayerLogged;
        private Vector3 _currentFlightVelocity;
        private Rigidbody _savedBody;
        private bool _savedUseGravity;

        private readonly List<MonkeGravityController> _gravityControllers = new List<MonkeGravityController>();
        private readonly List<float> _savedGravityMultipliers = new List<float>();

        internal static bool IsFlying { get; private set; } = false;

        /// <summary>Turns flight on/off from the menu (same as double-tapping A).</summary>
        internal static bool ToggleFromMenu()
        {
            if (_instance == null)
            {
                Plugin.Log?.LogInfo("Cascade fly: controller is not ready yet.");
                return false;
            }

            return _instance.ToggleFlight();
        }

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            _instance = this;
            HandLinkBlockPatch.Enabled = config.HandLinkBlock.Value;
        }

        private void LateUpdate()
        {
            if (_config == null)
            {
                return;
            }

            HandleDoubleTap();
            HandleDirectionHotkeys();
            HandleHandLinkSetting();

            if (!_config.JoystickFly.Value)
            {
                // Always restore gravity/physics when the master switch is off,
                // even if a previous state left a gravity multiplier at zero.
                StopFlying();
                return;
            }

        }

        private void FixedUpdate()
        {
            if (_config == null || !_config.JoystickFly.Value)
            {
                StopFlying();
                return;
            }
            ApplyFlight();
        }

        /// <summary>
        /// Keyboard escape hatch for players who want to correct a reversed axis
        /// without opening the menu (menu open = hover, so it cannot be felt live).
        /// </summary>
        private void HandleDirectionHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                ToggleFlight();
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                _config.FlyInvertVertical.Value = !_config.FlyInvertVertical.Value;
                AnnounceAxis("Up/down", _config.FlyInvertVertical.Value);
            }

            if (keyboard.f7Key.wasPressedThisFrame)
            {
                _config.FlyInvertForward.Value = !_config.FlyInvertForward.Value;
                AnnounceAxis("Forward/back", _config.FlyInvertForward.Value);
            }

            if (keyboard.f8Key.wasPressedThisFrame)
            {
                _config.FlyInvertStrafe.Value = !_config.FlyInvertStrafe.Value;
                AnnounceAxis("Strafe", _config.FlyInvertStrafe.Value);
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                _config.FlyDirection.Value = _config.FlyDirection.Value == 1 ? 0 : 1;
                string message = _config.FlyDirection.Value == 1 ? "right controller direction" : "head/camera direction";
                Plugin.Log?.LogInfo("Cascade fly direction: " + message);
                CascadeMenuBehaviour.Instance?.ShowToast("Fly direction", message);
            }
        }

        private void AnnounceAxis(string axis, bool inverted)
        {
            string message = inverted ? "reversed" : "normal";
            Plugin.Log?.LogInfo("Cascade fly " + axis + ": " + message);
            CascadeMenuBehaviour.Instance?.ShowToast("Fly " + axis, message);
        }

        private void HandleHandLinkSetting()
        {
            bool enabled = _config.HandLinkBlock.Value;
            if (HandLinkBlockPatch.Enabled != enabled)
            {
                HandLinkBlockPatch.Enabled = enabled;
                Plugin.Log?.LogInfo("Cascade hand-link block " + (enabled ? "enabled." : "disabled."));
            }
        }

        private void HandleDoubleTap()
        {
            ControllerInputPoller poller = ControllerInputPoller.instance;
            bool pressed = poller != null && poller.rightControllerPrimaryButton;

            // A is the menu's select button. Do not treat menu clicks as a
            // double-tap gesture while Cascade is open.
            if (CascadeMenuBehaviour.IsMenuOpen)
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
                    ToggleFlight();
                }
                else
                {
                    _lastTapTime = now;
                }
            }

            _wasPrimaryButton = pressed;
        }

        private bool ToggleFlight()
        {
            if (!_config.JoystickFly.Value)
            {
                // Master disabled – force off but still allow re‑enable later.
                if (IsFlying) StopFlying();
                Plugin.Log?.LogInfo("Cascade joystick fly disabled (master off).");
                return false;
            }

            if (IsFlying)
            {
                StopFlying();
                Buzz();
                Plugin.Log?.LogInfo("Cascade joystick fly disabled.");
                return true;
            }

            GTPlayer player = _player != null ? _player : GTPlayer.Instance;
            if (player == null || player.playerRigidBody == null)
            {
                Plugin.Log?.LogInfo("Cascade fly requested, but GTPlayer is not ready yet.");
                return false;
            }

            StartFlying(player);
            Buzz();
            Plugin.Log?.LogInfo("Cascade joystick fly enabled (double‑tap A).");
            return true;
        }

        private static void Buzz()
        {
            if (GorillaTagger.Instance != null)
                GorillaTagger.Instance.StartVibration(true, 0.3f, 0.08f);
        }

        private void ApplyFlight()
        {
            // Flight is only active after the player explicitly toggles it with
            // double-tap A (or the menu button). The master switch just arms it.
            if (!IsFlying)
            {
                return;
            }

            GTPlayer player = GTPlayer.Instance;
            if (player == null || player.playerRigidBody == null)
            {
                if (!_missingPlayerLogged)
                {
                    Plugin.Log?.LogInfo("Cascade fly is waiting for the local GTPlayer.");
                    _missingPlayerLogged = true;
                }

                StopFlying();
                return;
            }

            _missingPlayerLogged = false;

            if (_player != player)
            {
                StopFlying();
                StartFlying(player);
                return;
            }

            Rigidbody body = player.playerRigidBody;
            body.useGravity = false;

            Vector3 targetVelocity = GetFlightVelocity(player);
            float acceleration = _config != null ? _config.FlyAcceleration.Value : 8f;
            _currentFlightVelocity = Vector3.MoveTowards(
                _currentFlightVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            body.linearVelocity = _currentFlightVelocity;
            // GTPlayer keeps a velocity history for body movement; updating it
            // stops the locomotion code from immediately pulling us back down.
            player.SetPlayerVelocity(_currentFlightVelocity);
        }

        private Vector3 GetFlightVelocity(GTPlayer player)
        {
            // Hover in place while the menu is open so the sticks are free for
            // the UI (scrolling, selection).
            if (CascadeMenuBehaviour.IsMenuOpen)
            {
                return Vector3.zero;
            }

            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (poller == null) return Vector3.zero;

            Vector2 left = poller.leftControllerPrimary2DAxis;
            Vector2 right = poller.rightControllerPrimary2DAxis;

            // Left stick controls forward/back and strafe; right stick Y controls altitude.
            bool invertVertical = _config != null && _config.FlyInvertVertical.Value;
            bool invertForward = _config != null && _config.FlyInvertForward.Value;
            bool invertStrafe = _config != null && _config.FlyInvertStrafe.Value;

            float deadzone = _config != null ? _config.FlyDeadzone.Value : 0.18f;
            float strafe = ApplyDeadzone(left.x, deadzone);
            float forwardInput = ApplyDeadzone(left.y, deadzone);
            float vertical = ApplyDeadzone(right.y, deadzone);

            if (invertStrafe) strafe = -strafe;
            if (invertVertical) vertical = -vertical;
            if (invertForward) forwardInput = -forwardInput;

            if (Mathf.Abs(strafe) < 0.001f && Mathf.Abs(vertical) < 0.001f && Mathf.Abs(forwardInput) < 0.001f)
            {
                return Vector3.zero;
            }

            Camera camera = player.mainCamera != null ? player.mainCamera : Camera.main;
            Transform directionTransform = camera != null ? camera.transform : player.transform;
            if (_config != null && _config.FlyDirection.Value == 1 && GorillaTagger.Instance != null &&
                GorillaTagger.Instance.rightHandTransform != null)
                directionTransform = GorillaTagger.Instance.rightHandTransform;
            Vector3 forward = directionTransform.forward;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = player.transform.forward;
            }
            forward.Normalize();

            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = player.transform.forward;
            flatForward.Normalize();
            Vector3 rightVector = Vector3.Cross(Vector3.up, flatForward);
            if (rightVector.sqrMagnitude < 0.0001f)
            {
                rightVector = camera != null ? camera.transform.right : player.transform.right;
            }
            rightVector.Normalize();

            float speed = _config != null ? _config.FlySpeed.Value : 6.5f;
            float verticalSpeed = _config != null ? _config.FlyVerticalSpeed.Value : 4.5f;

            Vector3 velocity =
                rightVector * (strafe * speed) +
                Vector3.up * (vertical * verticalSpeed) +
                forward * (forwardInput * speed);

            float maxSpeed = Mathf.Max(speed, verticalSpeed) * 1.414f;
            if (velocity.magnitude > maxSpeed) velocity = velocity.normalized * maxSpeed;

            LogStickSample(left, right, velocity);
            return velocity;
        }

        /// <summary>
        /// Logs a throttled stick/velocity sample so an inverted axis can be
        /// identified from the BepInEx log without guesswork.
        /// </summary>
        private void LogStickSample(Vector2 left, Vector2 right, Vector3 velocity)
        {
            if (Time.unscaledTime - _lastStickLogTime < 2f)
            {
                return;
            }

            float strongest = Mathf.Max(Mathf.Abs(left.x), Mathf.Abs(left.y), Mathf.Abs(right.y));
            if (strongest < 0.5f)
            {
                return;
            }

            _lastStickLogTime = Time.unscaledTime;
            Plugin.Log?.LogInfo(string.Format(
                "Cascade fly stick L=({0:0.00},{1:0.00}) R=({2:0.00},{3:0.00}) -> vel=({4:0.00},{5:0.00},{6:0.00})",
                left.x, left.y, right.x, right.y, velocity.x, velocity.y, velocity.z));
        }

        private static float ApplyDeadzone(float value, float deadzone)
        {
            float abs = Mathf.Abs(value);
            if (abs <= deadzone) return 0f;
            return Mathf.Sign(value) * Mathf.Clamp01((abs - deadzone) / (1f - deadzone));
        }

        private void StartFlying(GTPlayer player)
        {
            if (player == null || player.playerRigidBody == null)
            {
                Plugin.Log?.LogWarning("Cascade fly: GTPlayer is not ready.");
                IsFlying = false;
                return;
            }

            // Always restore any previous gravity state before snapshotting a
            // fresh baseline. This prevents saving an already-zero multiplier.
            StopFlying();

            IsFlying = true;
            _player = player;

            Rigidbody body = player.playerRigidBody;
            _savedBody = body;
            _savedUseGravity = body.useGravity;

            CacheAndDisableGravity(player);
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            _currentFlightVelocity = Vector3.zero;
            player.SetPlayerVelocity(Vector3.zero);
            Plugin.Log?.LogInfo("Cascade joystick fly started.");
            if (_config != null)
            {
                Plugin.Log?.LogInfo(
                    "Cascade fly mapping: direction=" + _config.FlyDirection.Value +
                    " acceleration=" + _config.FlyAcceleration.Value +
                    " deadzone=" + _config.FlyDeadzone.Value +
                    " invertV=" + _config.FlyInvertVertical.Value +
                    " invertF=" + _config.FlyInvertForward.Value +
                    " invertStrafe=" + _config.FlyInvertStrafe.Value +
                    " flatForward=" + _config.FlyForwardHorizontal.Value);
            }

        }

        private void StopFlying()
        {
            bool wasFlying = IsFlying;
            IsFlying = false;

            if (wasFlying)
            {
                Plugin.Log?.LogInfo("Cascade joystick fly stopped.");
            }

            // Restore gravity even if the flying flag was already cleared by a
            // failed/partial start. This fixes the “zero gravity after disabling
            // the mod” bug.
            for (int i = 0; i < _gravityControllers.Count; i++)
            {
                MonkeGravityController controller = _gravityControllers[i];
                if (controller != null && i < _savedGravityMultipliers.Count)
                {
                    controller.GravityMultiplier = _savedGravityMultipliers[i];
                }
            }

            _gravityControllers.Clear();
            _savedGravityMultipliers.Clear();

            if (_savedBody != null)
            {
                _savedBody.useGravity = _savedUseGravity;
                _savedBody = null;
            }

            if (_player != null && _player.playerRigidBody != null)
            {
                _player.playerRigidBody.linearVelocity = Vector3.zero;
                _player.SetPlayerVelocity(Vector3.zero);
            }

            _player = null;
            _currentFlightVelocity = Vector3.zero;
            _missingPlayerLogged = false;
        }

        private void CacheAndDisableGravity(GTPlayer player)
        {
            _gravityControllers.Clear();
            _savedGravityMultipliers.Clear();

            Rigidbody body = player.playerRigidBody;
            CacheGravityControllers(player.GetComponentsInChildren<MonkeGravityController>(true), body);
            CacheGravityControllers(player.GetComponentsInParent<MonkeGravityController>(true), body);

            if (_gravityControllers.Count == 0)
            {
                Plugin.Log?.LogWarning("Cascade fly: no MonkeGravityController found; using force-skip patch only.");
            }
        }

        private void CacheGravityControllers(MonkeGravityController[] controllers, Rigidbody body)
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                MonkeGravityController controller = controllers[i];
                if (controller == null || controller.TargetRigidBody != body || _gravityControllers.Contains(controller))
                {
                    continue;
                }

                _gravityControllers.Add(controller);
                _savedGravityMultipliers.Add(controller.GravityMultiplier);
                controller.GravityMultiplier = 0f;
            }
        }

        private void OnEnable()
        {
            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            StopFlying();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            StopFlying();
        }
    }
}
