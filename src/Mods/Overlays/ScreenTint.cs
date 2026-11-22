using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.UI;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Overlays
{
    /// <summary>
    /// Full-view colour filter. A camera-parented quad covers the whole view so
    /// the tint works in VR as well as on the desktop, and the menu renders on
    /// top of it because its canvas has a much higher sorting order.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class ScreenTint : Mod
    {
        private const float CanvasSize = 1600f;
        private const float CanvasScale = 0.001f;
        private const float ViewDistance = 0.4f;

        private static readonly string[] PresetNames =
        {
            "Warm", "Cool", "Night", "Red", "Green", "Blue", "Sepia", "Mono"
        };

        private static readonly Color[] PresetColors =
        {
            new Color(1f, 0.84f, 0.62f),
            new Color(0.62f, 0.82f, 1f),
            new Color(0.05f, 0.07f, 0.18f),
            new Color(1f, 0.2f, 0.2f),
            new Color(0.3f, 1f, 0.4f),
            new Color(0.3f, 0.5f, 1f),
            new Color(0.86f, 0.68f, 0.42f),
            new Color(0.62f, 0.62f, 0.62f)
        };

        private int _preset;
        private float _opacity = 0.22f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private Image _tint;

        public ScreenTint()
        {
            Name = "Screen Tint";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("preset", "Color",
                "Tint applied to the whole view",
                PresetNames,
                () => _preset,
                value =>
                {
                    _preset = value;
                    ApplyTint();
                }));

            AddSetting(ModSetting.Slider("opacity", "Opacity",
                "How strongly the tint is applied",
                0.02f, 0.75f,
                () => _opacity,
                value =>
                {
                    _opacity = value;
                    ApplyTint();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            CreateHud();
            ProfilerStats.Register("screentint", "Screen Tint", "Full-view colour filter");
            Plugin.Log?.LogInfo("Cascade Screen Tint active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Screen Tint stopped.");
        }

        public override void LateUpdate()
        {
            if (_canvas == null)
            {
                CreateHud();
                if (_canvas == null) return;
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            if (camera != null && camera != _camera)
            {
                _camera = camera;
                _canvas.worldCamera = camera;
                _canvas.transform.SetParent(camera.transform, false);
                _canvasRect.localPosition = new Vector3(0f, 0f, ViewDistance);
                _canvasRect.localRotation = Quaternion.identity;
            }
        }

        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            Transform fallback = Plugin.Root != null ? Plugin.Root.transform : null;
            _canvas = OverlayCanvas.Create("CascadeScreenTint", _camera, fallback, 190,
                new Vector2(CanvasSize, CanvasSize), CanvasScale,
                new Vector3(0f, 0f, ViewDistance), out _canvasRect, out _);

            RectTransform rect = CascadeUI.Rect("Tint", _canvasRect);
            CascadeUI.Stretch(rect);
            _tint = CascadeUI.Solid(rect, Color.clear);

            ApplyTint();
        }

        private void ApplyTint()
        {
            if (_tint == null) return;

            Color color = PresetColors[Mathf.Clamp(_preset, 0, PresetColors.Length - 1)];
            color.a = _opacity;
            _tint.color = color;
        }

        private void DestroyHud()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
                _canvasRect = null;
                _tint = null;
            }
        }
    }
}
