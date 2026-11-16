using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace CascadeMenu.Mods
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

        private BillboardCloud _cloud;
        private float _angle;

        public OrbitingOrbsMod()
        {
            Name = "Orbiting Orbs";
            isToggle = true;

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
                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * _radius,
                    Mathf.Sin(angle * 2f) * 0.18f,
                    Mathf.Sin(angle) * _radius);
                _cloud.Add(point, 0.16f, ModKit.Hsv(i / (float)Count));
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

        private BillboardCloud _cloud;
        private float _angle;
        private float _hue;

        public PartyLightsMod()
        {
            Name = "Party Lights";
            isToggle = true;

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

            _angle = Mathf.Repeat(_angle + Time.unscaledDeltaTime * 40f, 360f);
            _hue = Mathf.Repeat(_hue + Time.unscaledDeltaTime * 0.18f, 1f);
            Vector3 center = ModKit.HeadPosition;

            _cloud.Begin();
            for (int i = 0; i < Count; i++)
            {
                float angle = (_angle + i * (360f / Count)) * Mathf.Deg2Rad;
                float height = 0.8f + Mathf.Abs(Mathf.Sin(angle * 2f)) * 3.2f;
                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * _radius,
                    height,
                    Mathf.Sin(angle) * _radius);
                _cloud.Add(point, 0.28f, ModKit.Hsv(_hue + i * 0.04f));
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

        private BillboardCloud _cloud;
        private readonly Vector3[] _seeds = new Vector3[Count];

        public FirefliesMod()
        {
            Name = "Fireflies";
            isToggle = true;

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
                Color color = new Color(1f, 0.92f, 0.45f, Mathf.Lerp(0.25f, 1f, pulse));
                _cloud.Add(point, Mathf.Lerp(0.05f, 0.11f, pulse), color);
            }
            _cloud.End(ModKit.ViewCamera);
        }
    }

    // -------------------------------------------------------------------------
    // Weather
    // -------------------------------------------------------------------------

    /// <summary>Snow falling in a cylinder around the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class SnowfallMod : Mod
    {
        private const int Count = 140;

        private BillboardCloud _cloud;
        private readonly Vector3[] _points = new Vector3[Count];
        private readonly float[] _speeds = new float[Count];
        private bool _seeded;

        public SnowfallMod()
        {
            Name = "Snowfall";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Snow field radius",
                2f, 18f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.0") + " m"));
        }

        private float _radius = 8f;

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeSnow", Count, true);
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
                for (int i = 0; i < Count; i++) Spawn(i, center, true);
            }

            _cloud.Begin();
            for (int i = 0; i < Count; i++)
            {
                Vector3 point = _points[i];
                point.y -= _speeds[i] * deltaTime;
                point.x += Mathf.Sin(Time.unscaledTime * 0.6f + i) * 0.02f;

                if (point.y < center.y - 4f || Vector2.Distance(
                        new Vector2(point.x, point.z),
                        new Vector2(center.x, center.z)) > _radius)
                {
                    Spawn(i, center, false);
                    point = _points[i];
                }

                _points[i] = point;
                _cloud.Add(point, 0.075f, new Color(0.92f, 0.97f, 1f, 0.9f));
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
            _speeds[index] = UnityEngine.Random.Range(0.7f, 1.8f);
        }
    }

    /// <summary>Fast vertical streaks falling around the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class RainfallMod : Mod
    {
        private const int Count = 160;

        private BillboardCloud _cloud;
        private readonly Vector3[] _points = new Vector3[Count];
        private readonly float[] _speeds = new float[Count];
        private bool _seeded;

        public RainfallMod()
        {
            Name = "Rainfall";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Rain field radius",
                2f, 18f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.0") + " m"));
        }

        private float _radius = 7f;

        public override void OnEnable()
        {
            _cloud = new BillboardCloud("CascadeRain", Count, false);
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
                for (int i = 0; i < Count; i++) Spawn(i, center, true);
            }

            _cloud.Begin();
            for (int i = 0; i < Count; i++)
            {
                Vector3 point = _points[i];
                point.y -= _speeds[i] * deltaTime;

                if (point.y < center.y - 4f)
                {
                    Spawn(i, center, false);
                    point = _points[i];
                }

                _points[i] = point;
                _cloud.Add(point, 0.03f, new Color(0.6f, 0.78f, 1f, 0.5f));
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
        private ParticlePool _particles;
        private float _nextBurst;

        public ConfettiMod()
        {
            Name = "Confetti";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "rate", "Burst rate",
                "Seconds between bursts",
                0.4f, 6f,
                () => _interval,
                value => _interval = value,
                value => value.ToString("0.0") + " s"));
        }

        private float _interval = 1.6f;

        public override void OnEnable()
        {
            _particles = new ParticlePool("CascadeConfetti", 220, false, 7f);
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
            Vector3 center = ModKit.HeadPosition + Vector3.up * 0.6f;

            for (int i = 0; i < 34; i++)
            {
                Vector2 spread = FunKit.RandomCircle() * 3.2f;
                Vector3 velocity = new Vector3(
                    spread.x,
                    UnityEngine.Random.Range(2.5f, 6.5f),
                    spread.y);

                _particles.Spawn(
                    center + new Vector3(spread.x * 0.2f, 0f, spread.y * 0.2f),
                    velocity,
                    ModKit.Hsv(UnityEngine.Random.value, 0.85f, 1f, 0.95f),
                    UnityEngine.Random.Range(1.4f, 2.6f),
                    UnityEngine.Random.Range(0.05f, 0.11f));
            }
        }
    }

    /// <summary>Colour bursts high above the player.</summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class FireworksMod : Mod
    {
        private ParticlePool _particles;
        private float _nextBurst;

        public FireworksMod()
        {
            Name = "Fireworks";
            isToggle = true;

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

        private float _interval = 2.4f;
        private float _height = 12f;

        public override void OnEnable()
        {
            _particles = new ParticlePool("CascadeFireworks", 320, true, 2.5f);
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

            for (int i = 0; i < 46; i++)
            {
                Vector3 direction = UnityEngine.Random.onUnitSphere;
                Vector3 velocity = direction * speed * UnityEngine.Random.Range(0.6f, 1f);

                _particles.Spawn(
                    center,
                    velocity,
                    ModKit.Hsv(hue + UnityEngine.Random.Range(-0.06f, 0.06f), 0.9f, 1f, 0.95f),
                    UnityEngine.Random.Range(1.2f, 2.2f),
                    UnityEngine.Random.Range(0.08f, 0.16f));
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

        private LineRenderer _ring;

        public GroundRingMod()
        {
            Name = "Ground Ring";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "radius", "Radius",
                "Ring radius in metres",
                0.3f, 6f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.00") + " m"));
        }

        private float _radius = 1.4f;

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

        private BillboardCloud _cloud;

        public BeaconLightMod()
        {
            Name = "Beacon Light";
            isToggle = true;
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
                _cloud.Add(point, Mathf.Lerp(0.9f, 0.4f, t), new Color(0.55f, 0.9f, 1f, alpha));
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
