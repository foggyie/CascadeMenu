using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Flight
{
    /// <summary>Holds jump/boost to climb without the fly controller.</summary>
    [PluginToggleable]
    [ModCategory("Flight")]
    public sealed class JumpJetMod : Mod
    {
        private float _thrust = 18f;
        private float _topSpeed = 6f;

        public JumpJetMod()
        {
            Name = "Jump Jet";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "thrust", "Thrust",
                "Upward acceleration while the boost is held",
                5f, 40f,
                () => _thrust,
                value => _thrust = value,
                value => value.ToString("0") + " m/s²"));

            AddSetting(ModSetting.Slider(
                "speed", "Climb speed",
                "Fastest climb this mod will push you to",
                1f, 14f,
                () => _topSpeed,
                value => _topSpeed = value,
                value => value.ToString("0.0") + " m/s"));
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player == null || !BoostHeld()) return;
            if (player.InstantaneousVelocity.y >= _topSpeed) return;

            player.AddForce(Vector3.up * _thrust, ForceMode.Acceleration);
        }

        private static bool BoostHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.isPressed) return true;
            return VrInput.Held(VrButton.RightPrimary) || VrInput.Held(VrButton.LeftPrimary);
        }
    }

    /// <summary>Slows the fall to a gentle drift. Gliding, not flying.</summary>
    [PluginToggleable]
    [ModCategory("Flight")]
    public sealed class FeatherFallMod : Mod
    {
        private float _fallSpeed = 1.4f;

        public FeatherFallMod()
        {
            Name = "Feather Fall";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "fall", "Fall speed",
                "Fastest descent allowed while the mod is on",
                0.2f, 6f,
                () => _fallSpeed,
                value => _fallSpeed = value,
                value => value.ToString("0.0") + " m/s"));
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            Rigidbody body = player != null ? player.playerRigidBody : null;
            if (body == null) return;

            float vertical = body.linearVelocity.y;
            if (vertical >= -_fallSpeed) return;

            float correction = (-_fallSpeed - vertical) * 8f;
            player.AddForce(Vector3.up * correction, ForceMode.Acceleration);
        }
    }

    /// <summary>Tap the boost button for an instant upward kick.</summary>
    [PluginToggleable]
    [ModCategory("Flight")]
    public sealed class RocketBootsMod : Mod
    {
        private float _power = 7f;
        private bool _wasPressed;

        public RocketBootsMod()
        {
            Name = "Rocket Boots";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "power", "Kick",
                "Upward speed added by each boost",
                2f, 16f,
                () => _power,
                value => _power = value,
                value => value.ToString("0.0") + " m/s"));
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;

            Keyboard keyboard = Keyboard.current;
            bool pressed = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) ||
                           VrInput.Pressed(VrButton.RightPrimary) ||
                           VrInput.Pressed(VrButton.LeftPrimary);

            if (pressed && !_wasPressed)
            {
                player.AddForce(Vector3.up * _power, ForceMode.VelocityChange);
            }

            _wasPressed = pressed;
        }
    }

    /// <summary>Turns a fall into forward movement, like a paper plane.</summary>
    [PluginToggleable]
    [ModCategory("Flight")]
    public sealed class GlideMod : Mod
    {
        private float _forward = 7f;
        private float _fallSpeed = 3f;

        public GlideMod()
        {
            Name = "Glide";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "forward", "Forward push",
                "Horizontal acceleration while gliding",
                1f, 20f,
                () => _forward,
                value => _forward = value,
                value => value.ToString("0.0") + " m/s²"));

            AddSetting(ModSetting.Slider(
                "fall", "Fall speed",
                "Fastest descent allowed while gliding",
                0.5f, 8f,
                () => _fallSpeed,
                value => _fallSpeed = value,
                value => value.ToString("0.0") + " m/s"));
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            Transform head = ModKit.Head;
            if (player == null || head == null) return;

            Vector3 velocity = player.InstantaneousVelocity;
            if (velocity.y > -0.2f) return;

            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();

            player.AddForce(forward * _forward, ForceMode.Acceleration);

            if (velocity.y < -_fallSpeed)
            {
                player.AddForce(Vector3.up * ((-_fallSpeed - velocity.y) * 8f), ForceMode.Acceleration);
            }
        }
    }
}
