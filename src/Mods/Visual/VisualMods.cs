using System;
using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    // -------------------------------------------------------------------------
    // Camera effects
    // -------------------------------------------------------------------------

    /// <summary>Widens the camera field of view.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class FovBoostMod : Mod
    {
        private Camera _camera;
        private float _previous;
        private bool _saved;
        private float _delta = 12f;

        public FovBoostMod()
        {
            Name = "FOV Boost";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "delta", "Extra FOV",
                "Degrees added to the camera field of view",
                -30f, 40f,
                () => _delta,
                value => _delta = value,
                value => value.ToString("0") + "°"));
        }

        public override void OnEnable()
        {
            Bind();
            Plugin.Log?.LogInfo("Cascade FOV Boost active.");
        }

        public override void OnDisable()
        {
            Restore();
            Plugin.Log?.LogInfo("Cascade FOV Boost stopped.");
        }

        public override void Update()
        {
            Camera camera = ModKit.ViewCamera;
            if (camera == null || camera == _camera) return;

            Restore();
            Bind();
        }

        private void Bind()
        {
            Camera camera = ModKit.ViewCamera;
            if (camera == null) return;

            _camera = camera;
            _previous = camera.fieldOfView;
            _saved = true;
            camera.fieldOfView = Mathf.Clamp(_previous + _delta, 20f, 150f);
        }

        private void Restore()
        {
            if (_camera != null && _saved) _camera.fieldOfView = _previous;
            _saved = false;
            _camera = null;
        }
    }

    /// <summary>Narrows the camera field of view.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class FovNarrowMod : Mod
    {
        private Camera _camera;
        private float _previous;
        private bool _saved;
        private float _delta = -15f;

        public FovNarrowMod()
        {
            Name = "FOV Narrow";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "delta", "FOV change",
                "Degrees removed from the camera field of view",
                -60f, -2f,
                () => _delta,
                value => _delta = value,
                value => value.ToString("0") + "°"));
        }

        public override void OnEnable()
        {
            Bind();
        }

        public override void OnDisable()
        {
            Restore();
        }

        public override void Update()
        {
            Camera camera = ModKit.ViewCamera;
            if (camera == null || camera == _camera) return;

            Restore();
            Bind();
        }

        private void Bind()
        {
            Camera camera = ModKit.ViewCamera;
            if (camera == null) return;

            _camera = camera;
            _previous = camera.fieldOfView;
            _saved = true;
            camera.fieldOfView = Mathf.Clamp(_previous + _delta, 15f, 150f);
        }

        private void Restore()
        {
            if (_camera != null && _saved) _camera.fieldOfView = _previous;
            _saved = false;
            _camera = null;
        }
    }

    /// <summary>Doubles the far clip plane so distant geometry stays visible.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class ExtendedViewMod : Mod
    {
        private Camera _camera;
        private float _previous;
        private bool _saved;
        private float _multiplier = 2f;

        public ExtendedViewMod()
        {
            Name = "Extended View";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "distance", "Distance",
                "Far clip plane multiplier",
                1.2f, 4f,
                () => _multiplier,
                value => _multiplier = value,
                value => value.ToString("0.0") + "x"));
        }

        public override void OnEnable()
        {
            Bind();
        }

        public override void OnDisable()
        {
            Restore();
        }

        public override void Update()
        {
            Camera camera = ModKit.ViewCamera;
            if (camera == null || camera == _camera) return;

            Restore();
            Bind();
        }

        private void Bind()
        {
            Camera camera = ModKit.ViewCamera;
            if (camera == null) return;

            _camera = camera;
            _previous = camera.farClipPlane;
            _saved = true;
            camera.farClipPlane = Mathf.Max(_previous, 100f) * _multiplier;
        }

        private void Restore()
        {
            if (_camera != null && _saved) _camera.farClipPlane = _previous;
            _saved = false;
            _camera = null;
        }
    }

    // -------------------------------------------------------------------------
    // Full-view filters
    // -------------------------------------------------------------------------

    /// <summary>Darkened edges around the whole view.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class VignetteMod : Mod
    {
        private ScreenCanvas _canvas;
        private Image _image;
        private float _strength = 0.7f;

        public VignetteMod()
        {
            Name = "Vignette";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "strength", "Strength",
                "How dark the edges become",
                0.1f, 1f,
                () => _strength,
                value =>
                {
                    _strength = value;
                    Apply();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _canvas = new ScreenCanvas("CascadeVignette", 95);
            _image = _canvas.Image("Vignette", ModKit.VignetteSprite(), Color.black, 1600f, 1600f).GetComponent<Image>();
            Apply();
        }

        public override void OnDisable()
        {
            if (_canvas != null) _canvas.Destroy();
            _canvas = null;
            _image = null;
        }

        public override void LateUpdate()
        {
            if (_canvas == null) return;
            _canvas.Follow();
        }

        private void Apply()
        {
            if (_image == null) return;
            _image.color = new Color(0f, 0f, 0f, _strength);
        }
    }

    /// <summary>Cinematic black bars on the top and bottom of the view.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class LetterboxMod : Mod
    {
        private ScreenCanvas _canvas;
        private RectTransform _top;
        private RectTransform _bottom;
        private float _height = 130f;

        public LetterboxMod()
        {
            Name = "Letterbox";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "height", "Bar height",
                "Height of each black bar in canvas units",
                30f, 300f,
                () => _height,
                value =>
                {
                    _height = value;
                    Apply();
                },
                value => value.ToString("0")));
        }

        public override void OnEnable()
        {
            _canvas = new ScreenCanvas("CascadeLetterbox", 95);
            _top = _canvas.Image("TopBar", CascadeSprites.White(), Color.black, 1600f, _height).rectTransform;
            _bottom = _canvas.Image("BottomBar", CascadeSprites.White(), Color.black, 1600f, _height).rectTransform;
            Apply();
        }

        public override void OnDisable()
        {
            if (_canvas != null) _canvas.Destroy();
            _canvas = null;
            _top = null;
            _bottom = null;
        }

        public override void LateUpdate()
        {
            if (_canvas == null) return;
            _canvas.Follow();
        }

        private void Apply()
        {
            if (_top != null)
            {
                _top.sizeDelta = new Vector2(1600f, _height);
                _top.anchoredPosition = new Vector2(0f, 800f - _height * 0.5f);
            }
            if (_bottom != null)
            {
                _bottom.sizeDelta = new Vector2(1600f, _height);
                _bottom.anchoredPosition = new Vector2(0f, -800f + _height * 0.5f);
            }
        }
    }

    /// <summary>Thin horizontal lines across the view.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class ScanlinesMod : Mod
    {
        private ScreenCanvas _canvas;
        private Image _image;
        private float _strength = 0.22f;

        public ScanlinesMod()
        {
            Name = "Scanlines";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "strength", "Strength",
                "Line opacity",
                0.02f, 0.8f,
                () => _strength,
                value =>
                {
                    _strength = value;
                    Apply();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _canvas = new ScreenCanvas("CascadeScanlines", 95);
            _image = _canvas.Image("Scanlines", ModKit.ScanlineSprite(), Color.black, 1600f, 1600f).GetComponent<Image>();
            Apply();
        }

        public override void OnDisable()
        {
            if (_canvas != null) _canvas.Destroy();
            _canvas = null;
            _image = null;
        }

        public override void LateUpdate()
        {
            if (_canvas == null) return;
            _canvas.Follow();
        }

        private void Apply()
        {
            if (_image != null) _image.color = new Color(0f, 0f, 0f, _strength);
        }
    }

    /// <summary>Animated noise over the whole view.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class FilmGrainMod : Mod
    {
        private static Sprite[] _frames;

        private ScreenCanvas _canvas;
        private Image _image;
        private float _strength = 0.16f;
        private float _nextFrame;
        private int _frame;

        public FilmGrainMod()
        {
            Name = "Film Grain";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "strength", "Strength",
                "Noise opacity",
                0.02f, 0.5f,
                () => _strength,
                value =>
                {
                    _strength = value;
                    Apply();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _canvas = new ScreenCanvas("CascadeFilmGrain", 95);
            _image = _canvas.Image("Grain", Frames()[0], Color.white, 1600f, 1600f).GetComponent<Image>();
            _frame = 0;
            Apply();
        }

        public override void OnDisable()
        {
            if (_canvas != null) _canvas.Destroy();
            _canvas = null;
            _image = null;
        }

        public override void LateUpdate()
        {
            if (_canvas == null) return;
            _canvas.Follow();

            if (Time.unscaledTime < _nextFrame) return;
            _nextFrame = Time.unscaledTime + 0.1f;
            _frame = (_frame + 1) % _frames.Length;
            if (_image != null) _image.sprite = _frames[_frame];
        }

        private void Apply()
        {
            if (_image != null) _image.color = new Color(1f, 1f, 1f, _strength);
        }

        private static Sprite[] Frames()
        {
            if (_frames != null) return _frames;

            _frames = new Sprite[4];
            for (int i = 0; i < _frames.Length; i++)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "CascadeGrain" + i;
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Point;

                var random = new System.Random(1000 + i);
                var pixels = new Color32[size * size];
                for (int p = 0; p < pixels.Length; p++)
                {
                    byte value = (byte)random.Next(0, 170);
                    pixels[p] = new Color32(255, 255, 255, value);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                _frames[i] = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _frames;
        }
    }

    /// <summary>Rule-of-thirds grid for framing and building.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class AlignmentGridMod : Mod
    {
        private static readonly string[] ColorNames = { "Cyan", "White", "Green", "Magenta" };
        private static readonly Color[] Colors =
        {
            new Color(0.3f, 0.85f, 1f), Color.white,
            new Color(0.35f, 1f, 0.5f), new Color(1f, 0.4f, 0.9f)
        };

        private ScreenCanvas _canvas;
        private Image _image;
        private int _color;
        private float _strength = 0.35f;

        public AlignmentGridMod()
        {
            Name = "Alignment Grid";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "color", "Color",
                "Grid tint",
                ColorNames,
                () => _color,
                value =>
                {
                    _color = value;
                    Apply();
                }));

            AddSetting(ModSetting.Slider(
                "strength", "Strength",
                "Grid opacity",
                0.05f, 1f,
                () => _strength,
                value =>
                {
                    _strength = value;
                    Apply();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _canvas = new ScreenCanvas("CascadeGrid", 95);
            _image = _canvas.Image("Grid", ModKit.GridSprite(), Color.white, 1600f, 1600f).GetComponent<Image>();
            Apply();
        }

        public override void OnDisable()
        {
            if (_canvas != null) _canvas.Destroy();
            _canvas = null;
            _image = null;
        }

        public override void LateUpdate()
        {
            if (_canvas == null) return;
            _canvas.Follow();
        }

        private void Apply()
        {
            if (_image == null) return;
            Color color = Colors[Mathf.Clamp(_color, 0, Colors.Length - 1)];
            color.a = _strength;
            _image.color = color;
        }
    }

    /// <summary>Frame guides that mark the safe part of the view.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class SafeZoneMod : Mod
    {
        private ScreenCanvas _canvas;
        private readonly Image[] _bars = new Image[4];
        private readonly RectTransform[] _rects = new RectTransform[4];

        public SafeZoneMod()
        {
            Name = "Safe Zone";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _canvas = new ScreenCanvas("CascadeSafeZone", 95);
            Color color = new Color(0.4f, 0.9f, 1f, 0.5f);

            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                _bars[i] = _canvas.Image("Bar" + i, CascadeSprites.White(), color,
                    horizontal ? 1400f : 3f,
                    horizontal ? 3f : 1400f).GetComponent<Image>();
                _rects[i] = _bars[i].rectTransform;

                float sign = i % 2 == 0 ? 1f : -1f;
                _rects[i].anchoredPosition = horizontal
                    ? new Vector2(0f, sign * 700f)
                    : new Vector2(sign * 700f, 0f);
            }
        }

        public override void OnDisable()
        {
            if (_canvas != null) _canvas.Destroy();
            _canvas = null;
        }

        public override void LateUpdate()
        {
            if (_canvas == null) return;
            _canvas.Follow();
        }
    }

    /// <summary>Green night-vision filter with dark edges.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class NightVisionMod : Mod
    {
        private ScreenCanvas _canvas;
        private Image _tint;
        private Image _vignette;
        private float _strength = 0.28f;

        public NightVisionMod()
        {
            Name = "Night Vision";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "strength", "Strength",
                "Green filter opacity",
                0.05f, 0.7f,
                () => _strength,
                value =>
                {
                    _strength = value;
                    Apply();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _canvas = new ScreenCanvas("CascadeNightVision", 95);
            _tint = _canvas.Image("Tint", CascadeSprites.White(), Color.clear, 1600f, 1600f).GetComponent<Image>();
            _vignette = _canvas.Image("Vignette", ModKit.VignetteSprite(), Color.black, 1600f, 1600f).GetComponent<Image>();
            Apply();
        }

        public override void OnDisable()
        {
            if (_canvas != null) _canvas.Destroy();
            _canvas = null;
            _tint = null;
            _vignette = null;
        }

        public override void LateUpdate()
        {
            if (_canvas == null) return;
            _canvas.Follow();
        }

        private void Apply()
        {
            if (_tint != null) _tint.color = new Color(0.18f, 1f, 0.32f, _strength);
            if (_vignette != null) _vignette.color = new Color(0f, 0f, 0f, 0.85f);
        }
    }

    // -------------------------------------------------------------------------
    // Lighting and quality
    // -------------------------------------------------------------------------

    /// <summary>Flat, bright ambient light so dark corners stay visible.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class FullbrightMod : Mod
    {
        private Color _previousLight;
        private float _previousIntensity;
        private AmbientMode _previousMode;
        private bool _saved;
        private float _intensity = 1.4f;

        public FullbrightMod()
        {
            Name = "Fullbright";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "intensity", "Intensity",
                "Ambient light multiplier",
                0.5f, 3f,
                () => _intensity,
                value => _intensity = value,
                value => value.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            _previousLight = RenderSettings.ambientLight;
            _previousIntensity = RenderSettings.ambientIntensity;
            _previousMode = RenderSettings.ambientMode;
            _saved = true;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientIntensity = _intensity;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            RenderSettings.ambientMode = _previousMode;
            RenderSettings.ambientLight = _previousLight;
            RenderSettings.ambientIntensity = _previousIntensity;
            _saved = false;
        }
    }

    /// <summary>Turns scene fog off.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class FogOffMod : Mod
    {
        private bool _previous;
        private bool _saved;

        public FogOffMod()
        {
            Name = "Fog Off";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previous = RenderSettings.fog;
            _saved = true;
            RenderSettings.fog = false;
        }

        public override void OnDisable()
        {
            if (_saved) RenderSettings.fog = _previous;
            _saved = false;
        }
    }

    /// <summary>Adds or removes distance fog with a density slider.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class FogDensityMod : Mod
    {
        private bool _previousFog;
        private float _previousDensity;
        private FogMode _previousMode;
        private bool _saved;
        private float _density = 0.008f;

        public FogDensityMod()
        {
            Name = "Fog Density";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "density", "Density",
                "Fog density while the mod is on",
                0f, 0.08f,
                () => _density,
                value => _density = value,
                value => value.ToString("0.000")));
        }

        public override void OnEnable()
        {
            _previousFog = RenderSettings.fog;
            _previousDensity = RenderSettings.fogDensity;
            _previousMode = RenderSettings.fogMode;
            _saved = true;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = _density;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            RenderSettings.fog = _previousFog;
            RenderSettings.fogDensity = _previousDensity;
            RenderSettings.fogMode = _previousMode;
            _saved = false;
        }
    }

    /// <summary>Pushes the shadow draw distance further out.</summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class LongShadowsMod : Mod
    {
        private float _previous;
        private bool _saved;
        private float _multiplier = 2f;

        public LongShadowsMod()
        {
            Name = "Long Shadows";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "distance", "Distance",
                "Shadow distance multiplier",
                1.2f, 4f,
                () => _multiplier,
                value => _multiplier = value,
                value => value.ToString("0.0") + "x"));
        }

        public override void OnEnable()
        {
            _previous = QualitySettings.shadowDistance;
            _saved = true;
            QualitySettings.shadowDistance = _previous * _multiplier;
        }

        public override void OnDisable()
        {
            if (_saved) QualitySettings.shadowDistance = _previous;
            _saved = false;
        }
    }

    // -------------------------------------------------------------------------
    // ESP wrappers around the existing visual controller
    // -------------------------------------------------------------------------

    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class ChamsEspMod : Mod
    {
        private bool _previous;
        private bool _saved;

        public ChamsEspMod()
        {
            Name = "Player Chams";
            isToggle = true;
        }

        public override void OnEnable()
        {
            MenuConfig config = CurrentConfig();
            if (config == null) return;
            _previous = config.EspChams.Value;
            _saved = true;
            config.EspChams.Value = true;
            Plugin.Log?.LogInfo("Cascade Player Chams active.");
        }

        public override void OnDisable()
        {
            MenuConfig config = CurrentConfig();
            if (config != null && _saved) config.EspChams.Value = _previous;
            _saved = false;
        }

        private static MenuConfig CurrentConfig()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            return menu != null ? menu.Config : null;
        }
    }

    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class TracerEspMod : Mod
    {
        private bool _previous;
        private bool _saved;

        public TracerEspMod()
        {
            Name = "Player Tracers";
            isToggle = true;
        }

        public override void OnEnable()
        {
            MenuConfig config = CurrentConfig();
            if (config == null) return;
            _previous = config.EspTracers.Value;
            _saved = true;
            config.EspTracers.Value = true;
            Plugin.Log?.LogInfo("Cascade Player Tracers active.");
        }

        public override void OnDisable()
        {
            MenuConfig config = CurrentConfig();
            if (config != null && _saved) config.EspTracers.Value = _previous;
            _saved = false;
        }

        private static MenuConfig CurrentConfig()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            return menu != null ? menu.Config : null;
        }
    }

    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class BoxEspMod : Mod
    {
        private bool _previous;
        private bool _saved;

        public BoxEspMod()
        {
            Name = "Player Boxes";
            isToggle = true;
        }

        public override void OnEnable()
        {
            MenuConfig config = CurrentConfig();
            if (config == null) return;
            _previous = config.EspEnabled.Value;
            _saved = true;
            config.EspEnabled.Value = true;
            Plugin.Log?.LogInfo("Cascade Player Boxes active.");
        }

        public override void OnDisable()
        {
            MenuConfig config = CurrentConfig();
            if (config != null && _saved) config.EspEnabled.Value = _previous;
            _saved = false;
        }

        private static MenuConfig CurrentConfig()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            return menu != null ? menu.Config : null;
        }
    }

    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class MarkerEspMod : Mod
    {
        private bool _previous;
        private bool _saved;

        public MarkerEspMod()
        {
            Name = "Player Markers";
            isToggle = true;
        }

        public override void OnEnable()
        {
            MenuConfig config = CurrentConfig();
            if (config == null) return;
            _previous = config.EspMarker.Value;
            _saved = true;
            config.EspMarker.Value = true;
            Plugin.Log?.LogInfo("Cascade Player Markers active.");
        }

        public override void OnDisable()
        {
            MenuConfig config = CurrentConfig();
            if (config != null && _saved) config.EspMarker.Value = _previous;
            _saved = false;
        }

        private static MenuConfig CurrentConfig()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            return menu != null ? menu.Config : null;
        }
    }

    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class CosmeticEspMod : Mod
    {
        private bool _previous;
        private bool _saved;

        public CosmeticEspMod()
        {
            Name = "Cosmetic Tags";
            isToggle = true;
        }

        public override void OnEnable()
        {
            MenuConfig config = CurrentConfig();
            if (config == null) return;
            _previous = config.EspCosmeticTags.Value;
            _saved = true;
            config.EspCosmeticTags.Value = true;
            Plugin.Log?.LogInfo("Cascade Cosmetic Tags active.");
        }

        public override void OnDisable()
        {
            MenuConfig config = CurrentConfig();
            if (config != null && _saved) config.EspCosmeticTags.Value = _previous;
            _saved = false;
        }

        private static MenuConfig CurrentConfig()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            return menu != null ? menu.Config : null;
        }
    }
}
