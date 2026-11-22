using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CascadeMenu.Mods.Core;

namespace CascadeMenu.Mods.Movement
{
    /// <summary>
    /// Smoothly boosts Gorilla Tag jump speed and movement multiplier,
    /// restoring defaults cleanly when turned off.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class SpeedBoostMod : Mod
    {
        private float _maxSpeed = 9.5f;
        private float _jumpMult = 1.45f;
        private float _savedMaxSpeed = 6.5f;
        private float _savedJumpMult = 1.1f;
        private bool _captured;

        public SpeedBoostMod()
        {
            Name = "Speed Boost";
            isToggle = true;

            AddSetting(ModSetting.Slider("maxSpeed", "Max jump speed",
                "Maximum push-off speed (vanilla is 6.5)",
                6.5f, 22f, () => _maxSpeed, v => _maxSpeed = v, v => v.ToString("0.0")));

            AddSetting(ModSetting.Slider("mult", "Jump multiplier",
                "Push-off acceleration multiplier (vanilla is 1.1)",
                1.1f, 3.5f, () => _jumpMult, v => _jumpMult = v, v => v.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player != null)
            {
                _savedMaxSpeed = player.maxJumpSpeed;
                _savedJumpMult = player.jumpMultiplier;
                _captured = true;
            }
        }

        public override void Update()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;
            if (!_captured)
            {
                _savedMaxSpeed = player.maxJumpSpeed;
                _savedJumpMult = player.jumpMultiplier;
                _captured = true;
            }
            player.maxJumpSpeed = _maxSpeed;
            player.jumpMultiplier = _jumpMult;
        }

        public override void OnDisable()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player != null && _captured)
            {
                player.maxJumpSpeed = _savedMaxSpeed;
                player.jumpMultiplier = _savedJumpMult;
            }
            _captured = false;
        }
    }

    /// <summary>
    /// Hold Trigger (VR) or Left Shift (PC) to thrust smoothly along your hand/camera direction.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class HandJetpackMod : Mod
    {
        private float _thrust = 22f;
        private float _maxSpeed = 16f;
        private bool _leftHand = false;

        public HandJetpackMod()
        {
            Name = "Hand Jetpack";
            isToggle = true;

            AddSetting(ModSetting.Toggle("left", "Use left hand",
                "Thrust from your left hand instead of right hand",
                () => _leftHand, v => _leftHand = v));

            AddSetting(ModSetting.Slider("thrust", "Thrust force",
                "Acceleration while holding Trigger / Shift",
                8f, 45f, () => _thrust, v => _thrust = v, v => v.ToString("0.0")));

            AddSetting(ModSetting.Slider("max", "Max air speed",
                "Top flight speed cap",
                6f, 35f, () => _maxSpeed, v => _maxSpeed = v, v => v.ToString("0.0") + " m/s"));
        }

        public override void Update()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;

            bool firing = false;
            Vector3 dir = Vector3.up;

            if (XRSettings.isDeviceActive && ControllerInputPoller.instance != null && GorillaTagger.Instance != null)
            {
                firing = _leftHand
                    ? ControllerInputPoller.instance.leftControllerIndexFloat > 0.55f
                    : ControllerInputPoller.instance.rightControllerIndexFloat > 0.55f;
                Transform hand = _leftHand ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
                if (hand != null) dir = (hand.forward + Vector3.up * 0.35f).normalized;
            }
            else if (Keyboard.current != null)
            {
                firing = Keyboard.current.leftShiftKey.isPressed;
                if (Camera.main != null) dir = (Camera.main.transform.forward + Vector3.up * 0.35f).normalized;
            }

            if (!firing) return;

            Vector3 vel = rb.linearVelocity + dir * (_thrust * Time.deltaTime);
            if (vel.magnitude > _maxSpeed) vel = vel.normalized * _maxSpeed;
            rb.linearVelocity = vel;
        }
    }

    /// <summary>
    /// Hold Grip near any wall or ceiling to pull toward the surface and walk/climb across walls.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class WallWalkMod : Mod
    {
        private float _pullStrength = 14f;
        private float _reach = 1.1f;

        public WallWalkMod()
        {
            Name = "Wall Walk (Sticky Hands)";
            isToggle = true;

            AddSetting(ModSetting.Slider("pull", "Stick force",
                "How strongly you are pulled toward nearby walls while gripping",
                5f, 30f, () => _pullStrength, v => _pullStrength = v, v => v.ToString("0.0")));

            AddSetting(ModSetting.Slider("reach", "Wall reach",
                "Detection radius around your hand",
                0.5f, 2.2f, () => _reach, v => _reach = v, v => v.ToString("0.00") + " m"));
        }

        public override void Update()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null || GorillaTagger.Instance == null) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;

            bool gripR = XRSettings.isDeviceActive && ControllerInputPoller.instance != null
                ? ControllerInputPoller.instance.rightControllerGripFloat > 0.55f
                : (Keyboard.current != null && Keyboard.current.eKey.isPressed);
            bool gripL = XRSettings.isDeviceActive && ControllerInputPoller.instance != null
                ? ControllerInputPoller.instance.leftControllerGripFloat > 0.55f
                : (Keyboard.current != null && Keyboard.current.qKey.isPressed);

            if (!gripR && !gripL) return;

            Transform hand = gripR ? GorillaTagger.Instance.rightHandTransform : GorillaTagger.Instance.leftHandTransform;
            if (hand == null) return;

            if (Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, _reach, player.locomotionEnabledLayers) ||
                Physics.Raycast(hand.position, -hand.up, out hit, _reach, player.locomotionEnabledLayers) ||
                Physics.Raycast(hand.position, hand.right, out hit, _reach, player.locomotionEnabledLayers) ||
                Physics.Raycast(hand.position, -hand.right, out hit, _reach, player.locomotionEnabledLayers))
            {
                rb.AddForce(-hit.normal * _pullStrength, ForceMode.Acceleration);
            }
        }
    }

    /// <summary>
    /// Press Right Grip + Trigger (or F on PC) to launch yourself like a slingshot in your aim direction.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class SlingshotLaunchMod : Mod
    {
        private float _launchSpeed = 16f;
        private float _cooldown = 0.8f;
        private float _nextLaunch;
        private bool _wasFired;

        public SlingshotLaunchMod()
        {
            Name = "Slingshot Launch";
            isToggle = true;

            AddSetting(ModSetting.Slider("speed", "Launch velocity",
                "Speed of each slingshot launch",
                8f, 32f, () => _launchSpeed, v => _launchSpeed = v, v => v.ToString("0.0") + " m/s"));

            AddSetting(ModSetting.Slider("cd", "Cooldown",
                "Seconds between launches",
                0.2f, 3.0f, () => _cooldown, v => _cooldown = v, v => v.ToString("0.00") + " s"));
        }

        public override void Update()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;

            bool fire = false;
            Vector3 dir = Vector3.forward;

            if (XRSettings.isDeviceActive && ControllerInputPoller.instance != null && GorillaTagger.Instance != null)
            {
                fire = ControllerInputPoller.instance.rightControllerGripFloat > 0.6f &&
                       ControllerInputPoller.instance.rightControllerIndexFloat > 0.6f;
                if (GorillaTagger.Instance.rightHandTransform != null)
                    dir = GorillaTagger.Instance.rightHandTransform.forward;
            }
            else if (Keyboard.current != null)
            {
                fire = Keyboard.current.fKey.isPressed;
                if (Camera.main != null) dir = Camera.main.transform.forward;
            }

            bool justFired = fire && !_wasFired;
            _wasFired = fire;

            if (!justFired || Time.time < _nextLaunch) return;
            _nextLaunch = Time.time + _cooldown;

            rb.linearVelocity = dir.normalized * _launchSpeed + Vector3.up * 2.5f;
        }
    }

    /// <summary>
    /// Bouncy Gorilla: smoothly bounces you upward when you hit the ground at speed.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class BouncyGorillaMod : Mod
    {
        private float _bouncePower = 1.25f;
        private float _minBounceSpeed = 6.5f;
        private float _lastYVel;
        private float _nextBounce;

        public BouncyGorillaMod()
        {
            Name = "Bouncy Gorilla";
            isToggle = true;

            AddSetting(ModSetting.Slider("power", "Bounce elasticity",
                "How high you rebound off the floor",
                0.6f, 2.2f, () => _bouncePower, v => _bouncePower = v, v => v.ToString("0.00") + "x"));
        }

        public override void Update()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;

            float curY = rb.linearVelocity.y;
            // Detect landing impact (was falling fast, now vertical velocity abruptly stopped)
            if (_lastYVel < -3.5f && curY > -0.8f && Time.time >= _nextBounce)
            {
                _nextBounce = Time.time + 0.25f;
                float rebound = Mathf.Clamp(Mathf.Abs(_lastYVel) * _bouncePower, _minBounceSpeed, 18f);
                Vector3 v = rb.linearVelocity;
                v.y = rebound;
                rb.linearVelocity = v;
            }
            _lastYVel = curY;
        }
    }
}
