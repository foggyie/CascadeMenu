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
    /// Shared plumbing for the gravity mods. The game asks every registered
    /// override for its acceleration each physics step, so adding the difference
    /// between the requested gravity and the real one scales the player only.
    /// </summary>
    public abstract class GravityMod : Mod
    {
        private GTPlayer _player;
        private UnityEngine.Object _key;

        protected GravityMod()
        {
            isToggle = true;
        }

        protected abstract float GravityScale { get; }

        public override void OnEnable()
        {
            Register(ModKit.Player);
        }

        public override void OnDisable()
        {
            if (_player != null && _key != null) _player.UnsetGravityOverride(_key);
            if (_key != null) UnityEngine.Object.Destroy(_key);
            _key = null;
            _player = null;
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player != null && player != _player) Register(player);
        }

        private void Register(GTPlayer player)
        {
            if (player == null || player == _player) return;

            if (_key == null)
            {
                var keyObject = new GameObject("CascadeGravityOverride");
                keyObject.hideFlags = HideFlags.HideAndDontSave;
                _key = keyObject;
            }

            _player = player;
            player.SetGravityOverride(_key, Apply);
        }

        private void Apply(GTPlayer player)
        {
            if (player == null) return;
            Rigidbody body = player.playerRigidBody;
            if (body == null) return;

            body.AddForce(Physics.gravity * (GravityScale - 1f) * body.mass, ForceMode.Force);
        }
    }

    /// <summary>Gravity multiplier with a slider, from feather-light to heavy.</summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class GravityScaleMod : GravityMod
    {
        private float _scale = 0.5f;

        public GravityScaleMod()
        {
            Name = "Gravity Scale";

            AddSetting(ModSetting.Slider(
                "scale", "Gravity",
                "Multiplier applied to world gravity while this mod is on",
                0.05f, 3f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));
        }

        protected override float GravityScale
        {
            get { return _scale; }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Plugin.Log?.LogInfo("Cascade Gravity Scale active at " + ModKit.F(_scale, "0.00") + "x.");
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class WeightlessMod : GravityMod
    {
        public WeightlessMod()
        {
            Name = "Weightless";
        }

        protected override float GravityScale
        {
            get { return 0.04f; }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Plugin.Log?.LogInfo("Cascade Weightless active.");
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class HeavyGravityMod : GravityMod
    {
        public HeavyGravityMod()
        {
            Name = "Heavy Gravity";
        }

        protected override float GravityScale
        {
            get { return 2.4f; }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Plugin.Log?.LogInfo("Cascade Heavy Gravity active.");
        }
    }

    /// <summary>Base for mods that simply push one GTPlayer float and restore it.</summary>
    public abstract class PlayerValueMod : Mod
    {
        private float _previous;
        private bool _saved;
        private GTPlayer _player;

        protected PlayerValueMod()
        {
            isToggle = true;
        }

        protected abstract float Wanted { get; }

        protected virtual float Read(GTPlayer player) { return 0f; }
        protected virtual void Write(GTPlayer player, float value) { }

        public override void OnEnable()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;

            _player = player;
            _previous = Read(player);
            _saved = true;
            Write(player, Wanted);
        }

        public override void OnDisable()
        {
            GTPlayer player = ModKit.Player;
            if (player != null && _saved) Write(player, _previous);
            _saved = false;
            _player = null;
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player == null || player == _player) return;

            // A map load replaces the GTPlayer; re-apply without touching the
            // value saved from the first instance.
            _player = player;
            Write(player, Wanted);
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class MoonJumpMod : PlayerValueMod
    {
        public MoonJumpMod()
        {
            Name = "Moon Jump";
        }

        protected override float Wanted
        {
            get { return 2.2f; }
        }

        protected override float Read(GTPlayer player)
        {
            return player.jumpMultiplier;
        }

        protected override void Write(GTPlayer player, float value)
        {
            player.jumpMultiplier = value;
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class SuperJumpMod : PlayerValueMod
    {
        private float _multiplier = 3.6f;

        public SuperJumpMod()
        {
            Name = "Super Jump";

            AddSetting(ModSetting.Slider(
                "multiplier", "Jump power",
                "Jump multiplier applied while the mod is on",
                1.5f, 6f,
                () => _multiplier,
                value => _multiplier = value,
                value => value.ToString("0.0") + "x"));
        }

        protected override float Wanted
        {
            get { return _multiplier; }
        }

        protected override float Read(GTPlayer player)
        {
            return player.jumpMultiplier;
        }

        protected override void Write(GTPlayer player, float value)
        {
            player.jumpMultiplier = value;
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class HighJumpSpeedMod : PlayerValueMod
    {
        public HighJumpSpeedMod()
        {
            Name = "Jump Speed";
        }

        protected override float Wanted
        {
            get { return 30f; }
        }

        protected override float Read(GTPlayer player)
        {
            return player.maxJumpSpeed;
        }

        protected override void Write(GTPlayer player, float value)
        {
            player.maxJumpSpeed = value;
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class NoSlideMod : PlayerValueMod
    {
        public NoSlideMod()
        {
            Name = "No Slide";
        }

        protected override float Wanted
        {
            get { return 0f; }
        }

        protected override float Read(GTPlayer player)
        {
            return player.slideVelocityLimit;
        }

        protected override void Write(GTPlayer player, float value)
        {
            player.slideVelocityLimit = value;
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class IceSkatesMod : PlayerValueMod
    {
        private float _friction = 0.02f;

        public IceSkatesMod()
        {
            Name = "Ice Skates";

            AddSetting(ModSetting.Slider(
                "friction", "Friction",
                "Body friction while the mod is on (lower is slicker)",
                0.01f, 1f,
                () => _friction,
                value => _friction = value,
                value => value.ToString("0.00")));
        }

        protected override float Wanted
        {
            get { return _friction; }
        }

        protected override float Read(GTPlayer player)
        {
            return player.frictionConstant;
        }

        protected override void Write(GTPlayer player, float value)
        {
            player.frictionConstant = value;
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class LongArmsMod : Mod
    {
        private float _previous;
        private bool _saved;

        public LongArmsMod()
        {
            Name = "Long Arms";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "length", "Arm length",
                "Maximum reach in metres",
                1.5f, 6f,
                () => _length,
                value => _length = value,
                value => value.ToString("0.0") + " m"));
        }

        private float _length = 3.5f;

        public override void OnEnable()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;

            _previous = player.MaxArmLengthAllowed;
            _saved = true;
            player.MaxArmLengthAllowed = _length;
            Plugin.Log?.LogInfo("Cascade Long Arms active.");
        }

        public override void OnDisable()
        {
            GTPlayer player = ModKit.Player;
            if (player != null && _saved) player.MaxArmLengthAllowed = _previous;
            _saved = false;
            Plugin.Log?.LogInfo("Cascade Long Arms stopped.");
        }
    }

    /// <summary>Changes the local body scale and restores the previous value.</summary>
    public abstract class BodyScaleMod : Mod
    {
        private float _previous;
        private bool _saved;

        protected BodyScaleMod()
        {
            isToggle = true;
        }

        protected abstract float Wanted { get; }

        public override void OnEnable()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;

            _previous = player.ScaleMultiplier;
            _saved = true;
            player.SetScaleMultiplier(Wanted);
        }

        public override void OnDisable()
        {
            GTPlayer player = ModKit.Player;
            if (player != null && _saved) player.SetScaleMultiplier(_previous);
            _saved = false;
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class TinyBodyMod : BodyScaleMod
    {
        public TinyBodyMod()
        {
            Name = "Tiny Body";
        }

        protected override float Wanted
        {
            get { return 0.6f; }
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class GiantBodyMod : BodyScaleMod
    {
        public GiantBodyMod()
        {
            Name = "Giant Body";
        }

        protected override float Wanted
        {
            get { return 1.6f; }
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class SlowMotionMod : Mod
    {
        private float _previous = 1f;
        private bool _saved;
        private float _scale = 0.5f;

        public SlowMotionMod()
        {
            Name = "Slow Motion";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "scale", "Time scale",
                "Engine time multiplier while the mod is on",
                0.1f, 0.9f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            _previous = Time.timeScale;
            _saved = true;
            Time.timeScale = _scale;
            Plugin.Log?.LogInfo("Cascade Slow Motion active.");
        }

        public override void OnDisable()
        {
            if (_saved) Time.timeScale = _previous;
            _saved = false;
            Plugin.Log?.LogInfo("Cascade Slow Motion stopped.");
        }
    }

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class BulletTimeMod : Mod
    {
        private float _previous = 1f;
        private bool _saved;

        public BulletTimeMod()
        {
            Name = "Bullet Time";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previous = Time.timeScale;
            _saved = true;
            Time.timeScale = 0.18f;
            Plugin.Log?.LogInfo("Cascade Bullet Time active.");
        }

        public override void OnDisable()
        {
            if (_saved) Time.timeScale = _previous;
            _saved = false;
        }
    }

    /// <summary>Bounces the player back up on a hard landing.</summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class BouncyFeetMod : Mod
    {
        private bool _wasGrounded = true;
        private float _fallSpeed;
        private float _strength = 0.8f;

        public BouncyFeetMod()
        {
            Name = "Bouncy Feet";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "strength", "Bounce",
                "How much of the landing speed is returned",
                0.2f, 1.8f,
                () => _strength,
                value => _strength = value,
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;

            bool grounded = player.BodyOnGround;
            if (grounded && !_wasGrounded && _fallSpeed < -3f)
            {
                float bounce = Mathf.Min(14f, -_fallSpeed * _strength);
                player.AddForce(Vector3.up * bounce, ForceMode.VelocityChange);
            }

            _fallSpeed = player.InstantaneousVelocity.y;
            _wasGrounded = grounded;
        }
    }

    /// <summary>One-shot impulse in the direction you are looking.</summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class AirDashMod : Mod
    {
        private float _power = 9f;

        public AirDashMod()
        {
            Name = "Air Dash";
            isToggle = false;

            AddSetting(ModSetting.Slider(
                "power", "Power",
                "Impulse speed added in the look direction",
                2f, 20f,
                () => _power,
                value => _power = value,
                value => value.ToString("0.0") + " m/s"));
        }

        public override void Execute()
        {
            GTPlayer player = ModKit.Player;
            Transform head = ModKit.Head;
            if (player == null || head == null) return;

            Vector3 direction = head.forward;
            player.AddForce(direction * _power + Vector3.up * 2.5f, ForceMode.VelocityChange);
            Plugin.Log?.LogInfo("Cascade Air Dash used.");
        }
    }

    /// <summary>One-shot vertical launch.</summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class RocketJumpMod : Mod
    {
        private float _power = 9f;

        public RocketJumpMod()
        {
            Name = "Rocket Jump";
            isToggle = false;

            AddSetting(ModSetting.Slider(
                "power", "Lift",
                "Upward speed added on use",
                2f, 20f,
                () => _power,
                value => _power = value,
                value => value.ToString("0.0") + " m/s"));
        }

        public override void Execute()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;

            player.AddForce(Vector3.up * _power, ForceMode.VelocityChange);
            Plugin.Log?.LogInfo("Cascade Rocket Jump used.");
        }
    }

    /// <summary>Keeps a gentle forward push going while you are on the ground.</summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class AutoWalkMod : Mod
    {
        private float _maxSpeed = 4.5f;
        private float _acceleration = 6f;

        public AutoWalkMod()
        {
            Name = "Auto Walk";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "speed", "Top speed",
                "Speed the automatic forward push stops at",
                1f, 10f,
                () => _maxSpeed,
                value => _maxSpeed = value,
                value => value.ToString("0.0") + " m/s"));

            AddSetting(ModSetting.Slider(
                "acceleration", "Acceleration",
                "How quickly the push builds up",
                1f, 20f,
                () => _acceleration,
                value => _acceleration = value,
                value => value.ToString("0.0") + " m/s²"));
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            Transform head = ModKit.Head;
            if (player == null || head == null || !player.BodyOnGround) return;

            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();

            Vector3 velocity = player.InstantaneousVelocity;
            float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
            if (horizontal >= _maxSpeed) return;

            player.AddForce(forward * _acceleration, ForceMode.Acceleration);
        }
    }

    /// <summary>Raises the velocity caps so the game never clamps fast movement.</summary>
    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class SpeedCapMod : Mod
    {
        private float _previousVelocity;
        private float _previousSlide;
        private bool _saved;

        private float _cap = 30f;

        public SpeedCapMod()
        {
            Name = "Speed Cap";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "cap", "Velocity cap",
                "Maximum body speed allowed by the locomotion code",
                10f, 60f,
                () => _cap,
                value => _cap = value,
                value => value.ToString("0") + " m/s"));
        }

        public override void OnEnable()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return;

            _previousVelocity = player.velocityLimit;
            _previousSlide = player.slideVelocityLimit;
            _saved = true;
            player.velocityLimit = _cap;
            player.slideVelocityLimit = _cap;
        }

        public override void OnDisable()
        {
            GTPlayer player = ModKit.Player;
            if (player != null && _saved)
            {
                player.velocityLimit = _previousVelocity;
                player.slideVelocityLimit = _previousSlide;
            }
            _saved = false;
        }
    }
}
