using System;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;
using CascadeMenu.Mods.Core;
using CascadeMenu.UI;

namespace CascadeMenu.Mods.Visual
{
    /// <summary>
    /// Client-side lighting & fog remover for crisp visibility in dark maps.
    /// Restores original RenderSettings.fog and ambient light on disable.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class FullbrightNoFogMod : Mod
    {
        private bool _disableFog = true;
        private bool _boostAmbient = true;
        private float _ambientBoost = 0.85f;

        private bool _savedFog;
        private Color _savedAmbient;
        private bool _captured;
        private float _nextZoneClear;

        public FullbrightNoFogMod()
        {
            Name = "Fullbright & No Fog";
            isToggle = true;

            AddSetting(ModSetting.Toggle("fog", "Remove fog",
                "Disable Unity & Gorilla Tag ground fog",
                () => _disableFog, v => _disableFog = v));

            AddSetting(ModSetting.Toggle("ambient", "Boost ambient light",
                "Brighten dark maps like Caves and Basement",
                () => _boostAmbient, v => _boostAmbient = v));

            AddSetting(ModSetting.Slider("level", "Brightness",
                "Ambient light brightness level",
                0.35f, 1.4f, () => _ambientBoost, v => _ambientBoost = v, v => (v * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _savedFog = RenderSettings.fog;
            _savedAmbient = RenderSettings.ambientLight;
            _captured = true;
            Apply();
        }

        public override void Update()
        {
            Apply();
        }

        public override void OnDisable()
        {
            if (_captured)
            {
                RenderSettings.fog = _savedFog;
                RenderSettings.ambientLight = _savedAmbient;
                _captured = false;
            }
            TryResetZoneFog();
        }

        private void Apply()
        {
            if (_disableFog)
            {
                RenderSettings.fog = false;
                if (Time.unscaledTime >= _nextZoneClear)
                {
                    _nextZoneClear = Time.unscaledTime + 0.5f;
                    TryClearZoneFog();
                }
            }
            if (_boostAmbient)
            {
                RenderSettings.ambientLight = new Color(_ambientBoost, _ambientBoost, _ambientBoost, 1f);
            }
        }

        private static void TryClearZoneFog()
        {
            try
            {
                Type t = HarmonyLib.AccessTools.TypeByName("GorillaTag.Rendering.ZoneShaderSettings");
                if (t == null) return;
                object inst = t.GetField("activeInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null)
                           ?? t.GetProperty("activeInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null, null);
                if (inst == null) return;
                MethodInfo m = t.GetMethod("SetGroundFogValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                m?.Invoke(inst, new object[] { Color.clear, 0f, 0f, 0f });
            }
            catch { }
        }

        private static void TryResetZoneFog()
        {
            try
            {
                Type t = HarmonyLib.AccessTools.TypeByName("GorillaTag.Rendering.ZoneShaderSettings");
                if (t == null) return;
                object active = t.GetField("activeInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                object def = t.GetField("defaultsInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                if (active == null || def == null) return;
                MethodInfo m = t.GetMethod("CopySettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                m?.Invoke(active, new object[] { def });
            }
            catch { }
        }
    }

    /// <summary>
    /// Client-side weather override (Clear Sky or Rain) via BetterDayNightManager.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class WeatherControlMod : Mod
    {
        private static readonly string[] WeatherNames = { "Clear Sky", "Raining" };
        private int _weatherMode = 0;
        private BetterDayNightManager.WeatherType[] _savedCycle;

        public WeatherControlMod()
        {
            Name = "Weather Control";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("weather", "Weather",
                "Override client-side Forest weather",
                WeatherNames, () => _weatherMode, v =>
                {
                    _weatherMode = Mathf.Clamp(v, 0, WeatherNames.Length - 1);
                    ApplyWeather();
                }));
        }

        public override void OnEnable()
        {
            try
            {
                BetterDayNightManager mgr = BetterDayNightManager.instance;
                if (mgr != null && mgr.weatherCycle != null && _savedCycle == null)
                {
                    _savedCycle = (BetterDayNightManager.WeatherType[])mgr.weatherCycle.Clone();
                }
            }
            catch { }
            ApplyWeather();
        }

        public override void OnDisable()
        {
            try
            {
                BetterDayNightManager mgr = BetterDayNightManager.instance;
                if (mgr != null && _savedCycle != null && mgr.weatherCycle != null)
                {
                    int len = Mathf.Min(mgr.weatherCycle.Length, _savedCycle.Length);
                    for (int i = 0; i < len; i++) mgr.weatherCycle[i] = _savedCycle[i];
                }
            }
            catch { }
            _savedCycle = null;
        }

        private void ApplyWeather()
        {
            try
            {
                BetterDayNightManager mgr = BetterDayNightManager.instance;
                if (mgr == null || mgr.weatherCycle == null) return;
                BetterDayNightManager.WeatherType wt = _weatherMode == 1
                    ? BetterDayNightManager.WeatherType.Raining
                    : BetterDayNightManager.WeatherType.None;
                for (int i = 0; i < mgr.weatherCycle.Length; i++)
                    mgr.weatherCycle[i] = wt;
            }
            catch { }
        }
    }

    /// <summary>
    /// Glowing reactive audio ring projected onto the ground beneath your Gorilla
    /// that expands smoothly with your live microphone loudness.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class VoiceAudioVisualizerMod : Mod
    {
        private bool _rainbow = false;
        private float _sensitivity = 14f;
        private LineRenderer _ring;
        private Material _mat;
        private const int Segments = 36;

        public VoiceAudioVisualizerMod()
        {
            Name = "Voice Audio Visualizer";
            isToggle = true;

            AddSetting(ModSetting.Toggle("rainbow", "Rainbow ring",
                "Cycle rainbow hue instead of matching Cascade theme accent",
                () => _rainbow, v => _rainbow = v));

            AddSetting(ModSetting.Slider("sens", "Sensitivity",
                "How wide the ring expands when you speak",
                4f, 30f, () => _sensitivity, v => _sensitivity = v, v => v.ToString("0.0")));
        }

        public override void OnEnable()
        {
            EnsureRing();
        }

        public override void OnDisable()
        {
            if (_ring != null) { UnityEngine.Object.Destroy(_ring.gameObject); _ring = null; }
            if (_mat != null) { UnityEngine.Object.Destroy(_mat); _mat = null; }
        }

        public override void LateUpdate()
        {
            if (GorillaTagger.Instance == null || VRRig.LocalRig == null) return;
            if (_ring == null) EnsureRing();
            if (_ring == null) return;

            float loudness = 0f;
            GorillaSpeakerLoudness speaker = VRRig.LocalRig.GetComponent<GorillaSpeakerLoudness>();
            if (speaker != null) loudness = speaker.Loudness;

            float radius = Mathf.Clamp(loudness * _sensitivity * 0.12f, 0f, 2.2f);
            if (radius < 0.06f)
            {
                _ring.enabled = false;
                return;
            }

            Vector3 origin = GorillaTagger.Instance.bodyCollider != null
                ? GorillaTagger.Instance.bodyCollider.transform.position
                : VRRig.LocalRig.transform.position;

            Vector3 center = origin - Vector3.up * 0.45f;
            Vector3 normal = Vector3.up;
            if (GTPlayer.Instance != null &&
                Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f, GTPlayer.Instance.locomotionEnabledLayers))
            {
                center = hit.point + hit.normal * 0.025f;
                normal = hit.normal;
            }

            Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);
            for (int i = 0; i < Segments; i++)
            {
                float a = (i / (float)Segments) * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                _ring.SetPosition(i, center + rot * local);
            }

            Color col = _rainbow
                ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.3f, 1f), 0.9f, 1f)
                : (CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.28f, 0.72f, 1f));
            col.a = 0.85f;
            _ring.startColor = col;
            _ring.endColor = col;
            _ring.enabled = true;
        }

        private void EnsureRing()
        {
            var go = new GameObject("CascadeVoiceVisualizerRing");
            if (Plugin.Root != null) go.transform.SetParent(Plugin.Root.transform, false);
            _ring = go.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.positionCount = Segments;
            _ring.startWidth = 0.024f;
            _ring.endWidth = 0.024f;
            _ring.shadowCastingMode = ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            if (sh != null)
            {
                _mat = new Material(sh) { color = Color.white };
                _ring.material = _mat;
            }
            _ring.enabled = false;
        }
    }

    /// <summary>
    /// Draws a clean predictive parabola showing where your current airborne
    /// jump velocity will land.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class JumpArcPreviewMod : Mod
    {
        private const int Steps = 24;
        private LineRenderer _line;
        private Material _mat;

        public JumpArcPreviewMod()
        {
            Name = "Jump Trajectory Arc";
            isToggle = true;
        }

        public override void OnDisable()
        {
            if (_line != null) { UnityEngine.Object.Destroy(_line.gameObject); _line = null; }
            if (_mat != null) { UnityEngine.Object.Destroy(_mat); _mat = null; }
        }

        public override void LateUpdate()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 vel = rb.linearVelocity;
            if (vel.magnitude < 1.8f)
            {
                if (_line != null) _line.enabled = false;
                return;
            }

            if (_line == null) CreateLine();
            if (_line == null) return;

            Vector3 pos = player.transform.position;
            Vector3 g = Physics.gravity;
            const float dt = 0.07f;
            int count = 0;
            var pts = new Vector3[Steps];

            for (int i = 0; i < Steps; i++)
            {
                pts[i] = pos;
                count = i + 1;
                Vector3 nextVel = vel + g * dt;
                Vector3 nextPos = pos + vel * dt;
                Vector3 seg = nextPos - pos;
                if (i >= 1 && Physics.Raycast(pos, seg.normalized, out RaycastHit hit, seg.magnitude, player.locomotionEnabledLayers))
                {
                    if (i + 1 < Steps)
                    {
                        pts[i + 1] = hit.point;
                        count = i + 2;
                    }
                    break;
                }
                pos = nextPos;
                vel = nextVel;
            }

            Color c = CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.3f, 0.85f, 1f);
            _line.startColor = new Color(c.r, c.g, c.b, 0.85f);
            _line.endColor = new Color(c.r, c.g, c.b, 0.15f);
            _line.positionCount = count;
            for (int i = 0; i < count; i++) _line.SetPosition(i, pts[i]);
            _line.enabled = true;
        }

        private void CreateLine()
        {
            var go = new GameObject("CascadeJumpArc");
            if (Plugin.Root != null) go.transform.SetParent(Plugin.Root.transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.startWidth = 0.014f;
            _line.endWidth = 0.006f;
            _line.numCapVertices = 4;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            if (sh != null)
            {
                _mat = new Material(sh) { color = Color.white };
                _line.material = _mat;
            }
        }
    }
}
