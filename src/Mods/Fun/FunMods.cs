using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Fun
{
    /// <summary>
    /// Fixed-capacity particle list drawn by one <see cref="BillboardCloud"/>.
    /// Particles are aged here and removed with a swap, so no allocation
    /// happens after construction.
    /// </summary>
    internal sealed class ParticlePool
    {
        private readonly BillboardCloud _cloud;
        private readonly Vector3[] _positions;
        private readonly Vector3[] _velocities;
        private readonly Color[] _baseColors;
        private readonly float[] _life;
        private readonly float[] _maxLife;
        private readonly float[] _sizes;
        private readonly float _gravity;
        private int _count;

        internal int Count
        {
            get { return _count; }
        }

        internal ParticlePool(string name, int capacity, bool soft, float gravity)
        {
            _cloud = new BillboardCloud(name, capacity, soft);
            _positions = new Vector3[capacity];
            _velocities = new Vector3[capacity];
            _baseColors = new Color[capacity];
            _life = new float[capacity];
            _maxLife = new float[capacity];
            _sizes = new float[capacity];
            _gravity = gravity;
        }

        internal void Spawn(Vector3 position, Vector3 velocity, Color color, float life, float size)
        {
            if (_count >= _positions.Length) return;

            int index = _count++;
            _positions[index] = position;
            _velocities[index] = velocity;
            _baseColors[index] = color;
            _life[index] = life;
            _maxLife[index] = Mathf.Max(0.01f, life);
            _sizes[index] = size;
        }

        internal void Update(float deltaTime)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                _life[i] -= deltaTime;
                if (_life[i] <= 0f)
                {
                    Remove(i);
                    continue;
                }

                _velocities[i] += Vector3.down * (_gravity * deltaTime);
                _positions[i] += _velocities[i] * deltaTime;
            }
        }

        internal void Draw(Camera camera)
        {
            _cloud.Begin();

            for (int i = 0; i < _count; i++)
            {
                float fade = Mathf.Clamp01(_life[i] / _maxLife[i]);
                Color color = _baseColors[i];
                color.a *= fade;
                _cloud.Add(_positions[i], _sizes[i] * Mathf.Lerp(0.5f, 1f, fade), color);
            }

            _cloud.End(camera);
        }

        internal void Clear()
        {
            _count = 0;
            _cloud.Begin();
            _cloud.End(null);
        }

        internal void Destroy()
        {
            _cloud.Destroy();
        }

        private void Remove(int index)
        {
            int last = --_count;
            if (index == last) return;

            _positions[index] = _positions[last];
            _velocities[index] = _velocities[last];
            _baseColors[index] = _baseColors[last];
            _life[index] = _life[last];
            _maxLife[index] = _maxLife[last];
            _sizes[index] = _sizes[last];
        }
    }

    /// <summary>Colour and timing helpers shared by the fun mods.</summary>
    internal static class FunKit
    {
        internal static Vector2 RandomCircle()
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(UnityEngine.Random.value);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }

    // -------------------------------------------------------------------------
    // Trails
    // -------------------------------------------------------------------------

    /// <summary>Ribbon that follows the local player's body.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class MotionTrailMod : Mod
    {
        private const int Points = 64;

        private LineRenderer _line;
        private readonly Vector3[] _history = new Vector3[Points];
        private int _filled;
        private int _head;
        private float _nextSample;

        public MotionTrailMod()
        {
            Name = "Motion Trail";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _line = ModKit.Line("CascadeMotionTrail", null, 238, 0.06f);
            _line.startColor = new Color(0.4f, 0.85f, 1f, 0.75f);
            _line.endColor = new Color(0.4f, 0.85f, 1f, 0f);
            _filled = 0;
            _head = 0;
            _nextSample = 0f;
        }

        public override void OnDisable()
        {
            if (_line != null) UnityEngine.Object.Destroy(_line.gameObject);
            _line = null;
        }

        public override void Update()
        {
            if (_line == null) return;
            if (Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + 0.03f;

            Vector3 point = ModKit.Rig != null ? ModKit.Rig.transform.position : ModKit.HeadPosition;

            _history[_head] = point;
            _head = (_head + 1) % Points;
            if (_filled < Points) _filled++;

            _line.positionCount = _filled;
            for (int i = 0; i < _filled; i++)
            {
                int index = (_head - _filled + i + Points * 2) % Points;
                _line.SetPosition(i, _history[index]);
            }
        }
    }

    /// <summary>One ribbon per hand.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class HandTrailMod : Mod
    {
        private const int Points = 32;

        private LineRenderer _leftLine;
        private LineRenderer _rightLine;
        private readonly Vector3[] _left = new Vector3[Points];
        private readonly Vector3[] _right = new Vector3[Points];
        private int _filled;
        private int _head;
        private float _nextSample;

        public HandTrailMod()
        {
            Name = "Hand Trails";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "width", "Width",
                "Ribbon thickness in metres",
                0.01f, 0.12f,
                () => _width,
                value => _width = value,
                value => value.ToString("0.000") + " m"));
        }

        private float _width = 0.035f;

        public override void OnEnable()
        {
            _leftLine = ModKit.Line("CascadeHandTrailL", null, 238, _width);
            _rightLine = ModKit.Line("CascadeHandTrailR", null, 238, _width);
            _leftLine.startColor = new Color(0.45f, 1f, 0.6f, 0.85f);
            _leftLine.endColor = new Color(0.45f, 1f, 0.6f, 0f);
            _rightLine.startColor = new Color(1f, 0.55f, 0.5f, 0.85f);
            _rightLine.endColor = new Color(1f, 0.55f, 0.5f, 0f);
            _filled = 0;
            _head = 0;
            _nextSample = 0f;
        }

        public override void OnDisable()
        {
            if (_leftLine != null) UnityEngine.Object.Destroy(_leftLine.gameObject);
            if (_rightLine != null) UnityEngine.Object.Destroy(_rightLine.gameObject);
            _leftLine = null;
            _rightLine = null;
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player == null || _leftLine == null) return;
            if (Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + 0.02f;

            _left[_head] = player.LeftHand.GetCurrentHandPosition();
            _right[_head] = player.RightHand.GetCurrentHandPosition();
            _head = (_head + 1) % Points;
            if (_filled < Points) _filled++;

            _leftLine.positionCount = _filled;
            _rightLine.positionCount = _filled;
            for (int i = 0; i < _filled; i++)
            {
                int index = (_head - _filled + i + Points * 2) % Points;
                _leftLine.SetPosition(i, _left[index]);
                _rightLine.SetPosition(i, _right[index]);
            }
        }
    }

    /// <summary>Body trail with a hue cycle running along it.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class RainbowTrailMod : Mod
    {
        private const int Points = 72;

        private LineRenderer _line;
        private readonly Vector3[] _history = new Vector3[Points];
        private readonly Gradient _gradient = new Gradient();
        private readonly GradientColorKey[] _colorKeys = new GradientColorKey[4];
        private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[4];
        private int _filled;
        private int _head;
        private float _nextSample;
        private float _hue;

        public RainbowTrailMod()
        {
            Name = "Rainbow Trail";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _line = ModKit.Line("CascadeRainbowTrail", null, 238, 0.075f);
            _line.colorGradient = _gradient;

            _alphaKeys[0] = new GradientAlphaKey(0.8f, 0f);
            _alphaKeys[1] = new GradientAlphaKey(0.8f, 0.4f);
            _alphaKeys[2] = new GradientAlphaKey(0.3f, 0.75f);
            _alphaKeys[3] = new GradientAlphaKey(0f, 1f);
            _gradient.SetKeys(_colorKeys, _alphaKeys);

            _filled = 0;
            _head = 0;
            _nextSample = 0f;
            _hue = 0f;
        }

        public override void OnDisable()
        {
            if (_line != null) UnityEngine.Object.Destroy(_line.gameObject);
            _line = null;
        }

        public override void Update()
        {
            if (_line == null) return;

            _hue = Mathf.Repeat(_hue + Time.unscaledDeltaTime * 0.25f, 1f);
            for (int i = 0; i < _colorKeys.Length; i++)
            {
                float offset = i / (float)(_colorKeys.Length - 1);
                _colorKeys[i] = new GradientColorKey(ModKit.Hsv(_hue + offset * 0.5f), offset);
            }
            _gradient.SetKeys(_colorKeys, _alphaKeys);

            if (Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + 0.03f;

            Vector3 point = ModKit.Rig != null ? ModKit.Rig.transform.position : ModKit.HeadPosition;
            _history[_head] = point;
            _head = (_head + 1) % Points;
            if (_filled < Points) _filled++;

            _line.positionCount = _filled;
            for (int i = 0; i < _filled; i++)
            {
                int index = (_head - _filled + i + Points * 2) % Points;
                _line.SetPosition(i, _history[index]);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Pointers and markers
    // -------------------------------------------------------------------------

    /// <summary>Thin red line from the right hand with a dot where it lands.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class LaserSightMod : Mod
    {
        private LineRenderer _line;
        private BillboardCloud _dot;
        private Vector3 _hitPoint;
        private bool _hit;

        public LaserSightMod()
        {
            Name = "Laser Sight";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _line = ModKit.Line("CascadeLaser", null, 239, 0.012f);
            _line.positionCount = 2;
            _line.startColor = new Color(1f, 0.18f, 0.18f, 0.9f);
            _line.endColor = new Color(1f, 0.4f, 0.3f, 0.25f);

            _dot = new BillboardCloud("CascadeLaserDot", 2, true);
        }

        public override void OnDisable()
        {
            if (_line != null) UnityEngine.Object.Destroy(_line.gameObject);
            if (_dot != null) _dot.Destroy();
            _line = null;
            _dot = null;
        }

        public override void Update()
        {
            GTPlayer player = ModKit.Player;
            if (player == null || _line == null) return;

            Vector3 start = player.RightHand.GetCurrentHandPosition();
            Vector3 direction = ModKit.Head != null ? ModKit.Head.forward : Vector3.forward;

            RaycastHit hit;
            _hit = Physics.Raycast(start, direction, out hit, 200f, ~0, QueryTriggerInteraction.Ignore);
            _hitPoint = _hit ? hit.point : start + direction * 60f;

            _line.SetPosition(0, start);
            _line.SetPosition(1, _hitPoint);

            _dot.Begin();
            _dot.Add(_hitPoint, _hit ? 0.14f : 0.07f, new Color(1f, 0.3f, 0.25f, 0.9f));
            _dot.End(ModKit.ViewCamera);
        }
    }

    /// <summary>Coloured orbs circling the player's head.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class OrbitingOrbsMod : Mod
    {
        private const int Count = 8;
        private static readonly string[] Patterns = { "Wave Orbit", "Flat Halo", "Double Helix" };

        private BillboardCloud _cloud;
        private float _angle;
        private int _pattern = 0;
        private float _scale = 1.0f;

        public OrbitingOrbsMod()
        {
            Name = "Orbiting Orbs";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "pattern", "Pattern",
                "How the orbs move around you",
                Patterns,
                () => _pattern,
                v => _pattern = Mathf.Clamp(v, 0, Patterns.Length - 1)));

            AddSetting(ModSetting.Slider(
                "scale", "Orb scale",
                "Size multiplier for each orb",
                0.4f, 2.5f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Orbit radius in metres",
                0.4f, 3f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.00") + " m"));

            AddSetting(ModSetting.Slider(
                "speed", "Speed",
                "Revolutions per second",
                0.05f, 2f,
                () => _speed,
                value => _speed = value,
                value => value.ToString("0.00") + " Hz"));
        }

        private float _radius = 1.1f;
        private float _speed = 0.35f;

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeOrbs", Count, true);
        }

        public override void OnDisable()
        {
            if (_cloud != null) _cloud.Destroy();
            _cloud = null;
        }

        public override void Update()
        {
            if (_cloud == null) return;

            _angle = Mathf.Repeat(_angle + Time.unscaledDeltaTime * _speed * 360f, 360f);
            Vector3 center = ModKit.HeadPosition + Vector3.up * 0.35f;

            _cloud.Begin();
            for (int i = 0; i < Count; i++)
            {
                float angle = (_angle + i * (360f / Count)) * Mathf.Deg2Rad;
                float yOff = 0f;
                if (_pattern == 0) yOff = Mathf.Sin(angle * 2f) * 0.18f;
                else if (_pattern == 2) yOff = ((i % 2 == 0) ? 1f : -1f) * Mathf.Sin(angle) * 0.45f;

                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * _radius,
                    yOff,
                    Mathf.Sin(angle) * _radius);
                _cloud.Add(point, 0.16f * _scale, ModKit.Hsv(i / (float)Count));
            }
            _cloud.End(ModKit.ViewCamera);
        }
    }

    /// <summary>Slowly circling coloured lights around the room.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class PartyLightsMod : Mod
    {
        private const int Count = 24;
        private static readonly string[] Patterns = { "Carousel", "Strobe Pulse", "Spiral Rise" };

        private BillboardCloud _cloud;
        private float _angle;
        private float _hue;
        private int _pattern = 0;
        private float _scale = 1.0f;
        private float _speed = 1.0f;

        public PartyLightsMod()
        {
            Name = "Party Lights";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "pattern", "Pattern",
                "Choose how the party lights animate",
                Patterns,
                () => _pattern,
                v => _pattern = Mathf.Clamp(v, 0, Patterns.Length - 1)));

            AddSetting(ModSetting.Slider(
                "scale", "Light scale",
                "Size multiplier for each party light",
                0.4f, 2.5f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "speed", "Speed",
                "Animation speed multiplier",
                0.25f, 3.0f,
                () => _speed,
                value => _speed = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Distance from the player",
                1f, 12f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.0") + " m"));
        }

        private float _radius = 5f;

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadePartyLights", Count, true);
        }

        public override void OnDisable()
        {
            if (_cloud != null) _cloud.Destroy();
            _cloud = null;
        }

        public override void Update()
        {
            if (_cloud == null) return;

            _angle = Mathf.Repeat(_angle + Time.unscaledDeltaTime * 40f * _speed, 360f);
            _hue = Mathf.Repeat(_hue + Time.unscaledDeltaTime * 0.18f * _speed, 1f);
            Vector3 center = ModKit.HeadPosition;

            _cloud.Begin();
            for (int i = 0; i < Count; i++)
            {
                float angle = (_angle + i * (360f / Count)) * Mathf.Deg2Rad;
                float height = _pattern == 2
                    ? Mathf.Repeat((Time.unscaledTime * _speed * 0.8f) + (i / (float)Count) * 4f, 4f)
                    : 0.8f + Mathf.Abs(Mathf.Sin(angle * 2f)) * 3.2f;
                float pulse = _pattern == 1 ? (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 10f * _speed + i)) : 1f;

                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * _radius,
                    height,
                    Mathf.Sin(angle) * _radius);
                _cloud.Add(point, 0.28f * _scale * pulse, ModKit.Hsv(_hue + i * 0.04f));
            }
            _cloud.End(ModKit.ViewCamera);
        }
    }

    /// <summary>Warm dots drifting around the player like fireflies.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class FirefliesMod : Mod
    {
        private const int Count = 40;
        private static readonly string[] Styles = { "Warm Gold", "Neon Cyan", "Rainbow" };

        private BillboardCloud _cloud;
        private readonly Vector3[] _seeds = new Vector3[Count];
        private int _style = 0;
        private float _scale = 1.0f;

        public FirefliesMod()
        {
            Name = "Fireflies";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "style", "Color style",
                "Glow palette for drifting fireflies",
                Styles,
                () => _style,
                v => _style = Mathf.Clamp(v, 0, Styles.Length - 1)));

            AddSetting(ModSetting.Slider(
                "scale", "Firefly scale",
                "Size multiplier for each firefly",
                0.4f, 2.5f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Area the fireflies drift in",
                1f, 12f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.0") + " m"));
        }

        private float _radius = 5f;

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeFireflies", Count, true);
            for (int i = 0; i < Count; i++)
            {
                _seeds[i] = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(0.2f, 2.6f),
                    UnityEngine.Random.Range(-1f, 1f));
            }
        }

        public override void OnDisable()
        {
            if (_cloud != null) _cloud.Destroy();
            _cloud = null;
        }

        public override void Update()
        {
            if (_cloud == null) return;

            float time = Time.unscaledTime;
            Vector3 center = ModKit.HeadPosition;

            _cloud.Begin();
            for (int i = 0; i < Count; i++)
            {
                Vector3 seed = _seeds[i];
                Vector3 point = center + new Vector3(
                    seed.x * _radius + Mathf.Sin(time * 0.5f + i) * 0.4f,
                    seed.y + Mathf.Sin(time * 0.8f + i * 1.7f) * 0.35f,
                    seed.z * _radius + Mathf.Cos(time * 0.45f + i) * 0.4f);

                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2f + i);
                float alpha = Mathf.Lerp(0.25f, 1f, pulse);
                Color color;
                if (_style == 1) color = new Color(0.35f, 0.95f, 1f, alpha);
                else if (_style == 2) color = ModKit.Hsv(Mathf.Repeat(time * 0.15f + i * 0.05f, 1f), 0.85f, 1f, alpha);
                else color = new Color(1f, 0.92f, 0.45f, alpha);

                _cloud.Add(point, Mathf.Lerp(0.05f, 0.11f, pulse) * _scale, color);
            }
            _cloud.End(ModKit.ViewCamera);
        }
    }

    // -------------------------------------------------------------------------
    // Weather
    // -------------------------------------------------------------------------

    /// <summary>Snow falling in a cylinder around the player with Light/Normal/Moderate/Blizzard modes.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class SnowfallMod : Mod
    {
        private const int MaxCount = 360;
        private static readonly string[] IntensityModes = { "Light Snow", "Normal Snow", "Moderate Snow", "Blizzard" };

        private BillboardCloud _cloud;
        private readonly Vector3[] _points = new Vector3[MaxCount];
        private readonly float[] _speeds = new float[MaxCount];
        private bool _seeded;

        private int _mode = 1; // 0=Light, 1=Normal, 2=Moderate, 3=Blizzard
        private float _speedMult = 1.0f;
        private float _flakeScale = 1.0f;
        private float _radius = 9f;

        public SnowfallMod()
        {
            Name = "Snowfall";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "mode", "Snow intensity",
                "Light Snow, Normal Snow, Moderate Snow, or howling Blizzard",
                IntensityModes,
                () => _mode,
                v => _mode = Mathf.Clamp(v, 0, IntensityModes.Length - 1)));

            AddSetting(ModSetting.Slider(
                "speed", "Fall speed",
                "How fast the snow falls and blows",
                0.25f, 4.0f,
                () => _speedMult,
                value => _speedMult = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "scale", "Flake scale",
                "Size multiplier for each snowflake",
                0.30f, 3.0f,
                () => _flakeScale,
                value => _flakeScale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Snow field radius around you",
                2f, 20f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.0") + " m"));
        }

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeSnow", MaxCount, true);
            _seeded = false;
        }

        public override void OnDisable()
        {
            if (_cloud != null) _cloud.Destroy();
            _cloud = null;
            _seeded = false;
        }

        public override void Update()
        {
            if (_cloud == null) return;

            Vector3 center = ModKit.HeadPosition;
            float deltaTime = Mathf.Min(0.05f, Time.unscaledDeltaTime);

            if (!_seeded)
            {
                _seeded = true;
                for (int i = 0; i < MaxCount; i++) Spawn(i, center, true);
            }

            int activeCount;
            float modeSpeed;
            float windX;
            float windZ;
            float sway;
            float baseSize;

            switch (_mode)
            {
                case 0: // Light Snow
                    activeCount = 75;
                    modeSpeed = 0.65f;
                    windX = 0.15f;
                    windZ = 0.05f;
                    sway = 0.25f;
                    baseSize = 0.062f;
                    break;
                case 2: // Moderate Snow
                    activeCount = 240;
                    modeSpeed = 1.45f;
                    windX = 1.15f;
                    windZ = 0.45f;
                    sway = 0.55f;
                    baseSize = 0.085f;
                    break;
                case 3: // Blizzard
                    activeCount = MaxCount;
                    modeSpeed = 2.65f;
                    windX = 4.20f;
                    windZ = 1.65f;
                    sway = 1.10f;
                    baseSize = 0.105f;
                    break;
                default: // Normal Snow
                    activeCount = 150;
                    modeSpeed = 1.0f;
                    windX = 0.45f;
                    windZ = 0.15f;
                    sway = 0.35f;
                    baseSize = 0.075f;
                    break;
            }

            float speedFactor = modeSpeed * _speedMult;
            float flakeSize = baseSize * _flakeScale;
            float time = Time.unscaledTime;

            _cloud.Begin();
            for (int i = 0; i < activeCount; i++)
            {
                Vector3 point = _points[i];
                point.y -= _speeds[i] * speedFactor * deltaTime;
                point.x += (windX + Mathf.Sin(time * 0.9f + i) * sway) * _speedMult * deltaTime;
                point.z += (windZ + Mathf.Cos(time * 0.7f + i * 1.3f) * sway * 0.6f) * _speedMult * deltaTime;

                if (point.y < center.y - 4f || Vector2.Distance(
                        new Vector2(point.x, point.z),
                        new Vector2(center.x, center.z)) > _radius)
                {
                    Spawn(i, center, false);
                    point = _points[i];
                }

                _points[i] = point;
                _cloud.Add(point, flakeSize, new Color(0.93f, 0.97f, 1f, _mode == 3 ? 0.96f : 0.88f));
            }
            _cloud.End(ModKit.ViewCamera);
        }

        private void Spawn(int index, Vector3 center, bool anywhere)
        {
            Vector2 offset = FunKit.RandomCircle() * _radius;
            _points[index] = new Vector3(
                center.x + offset.x,
                center.y + (anywhere ? UnityEngine.Random.Range(-3f, 7f) : UnityEngine.Random.Range(5f, 7.5f)),
                center.z + offset.y);
            _speeds[index] = UnityEngine.Random.Range(0.7f, 1.8f);
        }
    }

    /// <summary>Fast vertical streaks falling around the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class RainfallMod : Mod
    {
        private const int MaxCount = 260;
        private static readonly string[] IntensityModes = { "Light Drizzle", "Normal Rain", "Heavy Downpour", "Storm" };

        private BillboardCloud _cloud;
        private readonly Vector3[] _points = new Vector3[MaxCount];
        private readonly float[] _speeds = new float[MaxCount];
        private bool _seeded;

        private int _mode = 1;
        private float _speedMult = 1.0f;
        private float _scale = 1.0f;
        private float _radius = 7f;

        public RainfallMod()
        {
            Name = "Rainfall";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "mode", "Rain intensity",
                "Drizzle, Normal Rain, Heavy Downpour, or Storm",
                IntensityModes,
                () => _mode,
                v => _mode = Mathf.Clamp(v, 0, IntensityModes.Length - 1)));

            AddSetting(ModSetting.Slider(
                "speed", "Fall speed",
                "How fast raindrops fall",
                0.25f, 3.0f,
                () => _speedMult,
                value => _speedMult = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "scale", "Drop scale",
                "Size multiplier for raindrops",
                0.4f, 2.5f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Rain field radius",
                2f, 18f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.0") + " m"));
        }

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeRain", MaxCount, false);
            _seeded = false;
        }

        public override void OnDisable()
        {
            if (_cloud != null) _cloud.Destroy();
            _cloud = null;
            _seeded = false;
        }

        public override void Update()
        {
            if (_cloud == null) return;

            Vector3 center = ModKit.HeadPosition;
            float deltaTime = Mathf.Min(0.05f, Time.unscaledDeltaTime);

            if (!_seeded)
            {
                _seeded = true;
                for (int i = 0; i < MaxCount; i++) Spawn(i, center, true);
            }

            int activeCount = _mode == 0 ? 80 : (_mode == 1 ? 150 : (_mode == 2 ? 210 : MaxCount));
            float wind = _mode == 3 ? 2.6f : (_mode == 2 ? 0.8f : 0f);

            _cloud.Begin();
            for (int i = 0; i < activeCount; i++)
            {
                Vector3 point = _points[i];
                point.y -= _speeds[i] * _speedMult * deltaTime;
                point.x += wind * _speedMult * deltaTime;

                if (point.y < center.y - 4f)
                {
                    Spawn(i, center, false);
                    point = _points[i];
                }

                _points[i] = point;
                _cloud.Add(point, 0.032f * _scale, new Color(0.6f, 0.80f, 1f, 0.55f));
            }
            _cloud.End(ModKit.ViewCamera);
        }

        private void Spawn(int index, Vector3 center, bool anywhere)
        {
            Vector2 offset = FunKit.RandomCircle() * _radius;
            _points[index] = new Vector3(
                center.x + offset.x,
                center.y + (anywhere ? UnityEngine.Random.Range(-3f, 7f) : UnityEngine.Random.Range(5f, 7f)),
                center.z + offset.y);
            _speeds[index] = UnityEngine.Random.Range(8f, 14f);
        }
    }

    // -------------------------------------------------------------------------
    // Bursts
    // -------------------------------------------------------------------------

    /// <summary>Repeated confetti bursts above the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class ConfettiMod : Mod
    {
        private static readonly string[] Styles = { "Overhead Burst", "Fountain", "Spiral Shower" };

        private ParticlePool _particles;
        private float _nextBurst;
        private int _style = 0;
        private float _scale = 1.0f;
        private float _interval = 1.6f;

        public ConfettiMod()
        {
            Name = "Confetti";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "style", "Burst style",
                "Choose how confetti launches",
                Styles,
                () => _style,
                v => _style = Mathf.Clamp(v, 0, Styles.Length - 1)));

            AddSetting(ModSetting.Slider(
                "scale", "Confetti scale",
                "Size multiplier for confetti pieces",
                0.4f, 2.5f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "rate", "Burst rate",
                "Seconds between bursts",
                0.4f, 6f,
                () => _interval,
                value => _interval = value,
                value => value.ToString("0.0") + " s"));
        }

        public override void OnEnable()
        {
            _particles = new ParticlePool("CascadeConfetti", 260, false, 7f);
            _nextBurst = 0f;
        }

        public override void OnDisable()
        {
            if (_particles != null) _particles.Destroy();
            _particles = null;
        }

        public override void Update()
        {
            if (_particles == null) return;

            if (Time.unscaledTime >= _nextBurst)
            {
                _nextBurst = Time.unscaledTime + _interval;
                Burst();
            }

            _particles.Update(Mathf.Min(0.05f, Time.unscaledDeltaTime));
            _particles.Draw(ModKit.ViewCamera);
        }

        private void Burst()
        {
            Vector3 center = ModKit.HeadPosition + Vector3.up * (_style == 1 ? 0.1f : 0.6f);

            for (int i = 0; i < 34; i++)
            {
                Vector3 velocity;
                if (_style == 1)
                {
                    Vector2 spread = FunKit.RandomCircle() * 1.4f;
                    velocity = new Vector3(spread.x, UnityEngine.Random.Range(5.5f, 9.0f), spread.y);
                }
                else if (_style == 2)
                {
                    float a = (i / 34f) * Mathf.PI * 4f + Time.unscaledTime * 2f;
                    velocity = new Vector3(Mathf.Cos(a) * 3.2f, UnityEngine.Random.Range(2.5f, 5.5f), Mathf.Sin(a) * 3.2f);
                }
                else
                {
                    Vector2 spread = FunKit.RandomCircle() * 3.2f;
                    velocity = new Vector3(spread.x, UnityEngine.Random.Range(2.5f, 6.5f), spread.y);
                }

                _particles.Spawn(
                    center,
                    velocity,
                    ModKit.Hsv(UnityEngine.Random.value, 0.85f, 1f, 0.95f),
                    UnityEngine.Random.Range(1.4f, 2.6f),
                    UnityEngine.Random.Range(0.05f, 0.11f) * _scale);
            }
        }
    }

    /// <summary>Colour bursts high above the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class FireworksMod : Mod
    {
        private static readonly string[] Styles = { "Sphere Burst", "Ring Burst", "Double Cluster" };

        private ParticlePool _particles;
        private float _nextBurst;
        private int _style = 0;
        private float _scale = 1.0f;
        private float _interval = 2.4f;
        private float _height = 12f;

        public FireworksMod()
        {
            Name = "Fireworks";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "style", "Firework style",
                "Explosion shape in the sky",
                Styles,
                () => _style,
                v => _style = Mathf.Clamp(v, 0, Styles.Length - 1)));

            AddSetting(ModSetting.Slider(
                "scale", "Spark scale",
                "Size multiplier for firework sparks",
                0.4f, 2.5f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "rate", "Launch rate",
                "Seconds between fireworks",
                0.8f, 8f,
                () => _interval,
                value => _interval = value,
                value => value.ToString("0.0") + " s"));

            AddSetting(ModSetting.Slider(
                "height", "Height",
                "Explosion height above the player",
                4f, 30f,
                () => _height,
                value => _height = value,
                value => value.ToString("0") + " m"));
        }

        public override void OnEnable()
        {
            _particles = new ParticlePool("CascadeFireworks", 360, true, 2.5f);
            _nextBurst = 0f;
        }

        public override void OnDisable()
        {
            if (_particles != null) _particles.Destroy();
            _particles = null;
        }

        public override void Update()
        {
            if (_particles == null) return;

            if (Time.unscaledTime >= _nextBurst)
            {
                _nextBurst = Time.unscaledTime + _interval;
                Burst();
            }

            _particles.Update(Mathf.Min(0.05f, Time.unscaledDeltaTime));
            _particles.Draw(ModKit.ViewCamera);
        }

        private void Burst()
        {
            Vector2 offset = FunKit.RandomCircle() * 7f;
            Vector3 center = ModKit.HeadPosition + new Vector3(offset.x, _height, offset.y);
            float hue = UnityEngine.Random.value;
            float speed = UnityEngine.Random.Range(4f, 8f);

            int bursts = _style == 2 ? 2 : 1;
            for (int b = 0; b < bursts; b++)
            {
                Vector3 burstCenter = b == 0 ? center : center + UnityEngine.Random.onUnitSphere * 2.5f;
                float burstHue = Mathf.Repeat(hue + b * 0.35f, 1f);
                for (int i = 0; i < 42; i++)
                {
                    Vector3 direction;
                    if (_style == 1)
                    {
                        float a = (i / 42f) * Mathf.PI * 2f;
                        direction = new Vector3(Mathf.Cos(a), UnityEngine.Random.Range(-0.15f, 0.15f), Mathf.Sin(a)).normalized;
                    }
                    else
                    {
                        direction = UnityEngine.Random.onUnitSphere;
                    }

                    Vector3 velocity = direction * speed * UnityEngine.Random.Range(0.6f, 1f);
                    _particles.Spawn(
                        burstCenter,
                        velocity,
                        ModKit.Hsv(burstHue + UnityEngine.Random.Range(-0.06f, 0.06f), 0.9f, 1f, 0.95f),
                        UnityEngine.Random.Range(1.2f, 2.2f),
                        UnityEngine.Random.Range(0.08f, 0.16f) * _scale);
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Ground markers
    // -------------------------------------------------------------------------

    /// <summary>Ring drawn on the floor under the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class GroundRingMod : Mod
    {
        private const int Segments = 64;
        private static readonly string[] Styles = { "Cyan Glow", "Rainbow Pulse", "Emerald Aura" };

        private LineRenderer _ring;
        private int _style = 0;
        private float _scale = 1.0f;
        private float _radius = 1.4f;

        public GroundRingMod()
        {
            Name = "Ground Ring";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "style", "Ring style",
                "Color and pulse style for the ground ring",
                Styles,
                () => _style,
                v => _style = Mathf.Clamp(v, 0, Styles.Length - 1)));

            AddSetting(ModSetting.Slider(
                "scale", "Line thickness",
                "Thickness multiplier for the ring",
                0.5f, 3.0f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Ring radius in metres",
                0.3f, 6f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.00") + " m"));
        }

        public override void OnEnable()
        {
            _ring = ModKit.Line("CascadeGroundRing", null, 238, 0.03f, true);
            _ring.positionCount = Segments;
            _ring.startColor = new Color(0.45f, 0.9f, 1f, 0.8f);
            _ring.endColor = _ring.startColor;
        }

        public override void OnDisable()
        {
            if (_ring != null) UnityEngine.Object.Destroy(_ring.gameObject);
            _ring = null;
        }

        public override void Update()
        {
            if (_ring == null) return;

            _ring.startWidth = 0.03f * _scale;
            _ring.endWidth = 0.03f * _scale;
            Color col = _style == 1
                ? ModKit.Hsv(Mathf.Repeat(Time.unscaledTime * 0.25f, 1f), 0.85f, 1f, 0.85f)
                : (_style == 2 ? new Color(0.25f, 0.95f, 0.55f, 0.85f) : new Color(0.45f, 0.9f, 1f, 0.8f));
            _ring.startColor = col;
            _ring.endColor = col;

            Vector3 head = ModKit.HeadPosition;
            float ground = head.y;

            RaycastHit hit;
            if (Physics.Raycast(head + Vector3.up * 0.1f, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point.y;
            }

            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(
                    head.x + Mathf.Cos(angle) * _radius,
                    ground + 0.02f,
                    head.z + Mathf.Sin(angle) * _radius));
            }
        }
    }

    /// <summary>Vertical light shaft marking the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class BeaconLightMod : Mod
    {
        private const int Count = 16;
        private static readonly string[] Styles = { "Ice Cyan", "Rainbow", "Golden Pillar" };

        private BillboardCloud _cloud;
        private int _style = 0;
        private float _scale = 1.0f;

        public BeaconLightMod()
        {
            Name = "Beacon Light";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "style", "Beacon color",
                "Color style for the vertical light beam",
                Styles,
                () => _style,
                v => _style = Mathf.Clamp(v, 0, Styles.Length - 1)));

            AddSetting(ModSetting.Slider(
                "scale", "Beam scale",
                "Width multiplier for the light pillar",
                0.4f, 2.5f,
                () => _scale,
                value => _scale = value,
                value => value.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeBeaconLight", Count, true);
        }

        public override void OnDisable()
        {
            if (_cloud != null) _cloud.Destroy();
            _cloud = null;
        }

        public override void Update()
        {
            if (_cloud == null) return;

            Vector3 basePoint = ModKit.HeadPosition;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);

            _cloud.Begin();
            for (int i = 0; i < Count; i++)
            {
                float t = i / (float)(Count - 1);
                Vector3 point = basePoint + Vector3.up * (t * 18f);
                float alpha = (1f - t) * Mathf.Lerp(0.35f, 0.85f, pulse);
                Color col;
                if (_style == 1) col = ModKit.Hsv(Mathf.Repeat(Time.unscaledTime * 0.2f + t * 0.5f, 1f), 0.85f, 1f, alpha);
                else if (_style == 2) col = new Color(1f, 0.82f, 0.32f, alpha);
                else col = new Color(0.55f, 0.9f, 1f, alpha);

                _cloud.Add(point, Mathf.Lerp(0.9f, 0.4f, t) * _scale, col);
            }
            _cloud.End(ModKit.ViewCamera);
        }
    }

    /// <summary>Drops a marker every few metres so you can retrace the path.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class BreadcrumbsMod : Mod
    {
        private const int MaxPoints = 48;

        private BillboardCloud _cloud;
        private readonly List<Vector3> _points = new List<Vector3>(MaxPoints);
        private Vector3 _last;
        private bool _hasLast;

        public BreadcrumbsMod()
        {
            Name = "Breadcrumbs";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "spacing", "Spacing",
                "Distance between dropped markers",
                0.5f, 8f,
                () => _spacing,
                value => _spacing = value,
                value => value.ToString("0.0") + " m"));
        }

        private float _spacing = 2f;

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeBreadcrumbs", MaxPoints, true);
            _points.Clear();
            _hasLast = false;
        }

        public override void OnDisable()
        {
            if (_cloud != null) _cloud.Destroy();
            _cloud = null;
            _points.Clear();
            _hasLast = false;
        }

        public override void Update()
        {
            if (_cloud == null) return;

            Vector3 point = ModKit.Rig != null ? ModKit.Rig.transform.position : ModKit.HeadPosition;
            if (!_hasLast || Vector3.Distance(point, _last) >= _spacing)
            {
                _last = point;
                _hasLast = true;

                _points.Add(point + Vector3.up * 0.12f);
                if (_points.Count > MaxPoints) _points.RemoveAt(0);
            }

            _cloud.Begin();
            for (int i = 0; i < _points.Count; i++)
            {
                float age = (i + 1) / (float)_points.Count;
                _cloud.Add(_points[i], Mathf.Lerp(0.06f, 0.2f, age),
                    new Color(1f, 0.72f, 0.3f, Mathf.Lerp(0.2f, 0.9f, age)));
            }
            _cloud.End(ModKit.ViewCamera);
        }
    }
}
