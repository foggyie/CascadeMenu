using CascadeMenu.UI;
using TMPro;
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
    /// Camera-parented compass strip at the top of the view. Tick marks and
    /// cardinal letters slide as the head turns, and the current heading is
    /// printed underneath.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class Compass : Mod
    {
        private const float CanvasWidth = 300f;
        private const float CanvasHeight = 90f;
        private const float CanvasScale = 0.0005f;
        private const float ViewDistance = 0.6f;
        private const float PixelsPerDegree = 2.4f;
        private const float StripHalfWidth = 128f;
        private const int TickCount = 9;

        private static readonly Vector3 CameraOffset = new Vector3(0f, 0.19f, ViewDistance);
        private static readonly string[] Cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private static readonly string[] CardinalLetters = { "N", "E", "S", "W" };

        private static readonly Color TickColor = new Color(0.72f, 0.78f, 0.86f, 0.75f);
        private static readonly Color CenterTickColor = new Color(0.4f, 0.9f, 1f, 1f);

        private bool _showDegrees = true;
        private float _size = 1f;
        private float _opacity = 0.72f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private Image _panel;
        private TextMeshProUGUI _headingLabel;

        private readonly RectTransform[] _tickRects = new RectTransform[TickCount];
        private readonly Image[] _tickImages = new Image[TickCount];
        private readonly RectTransform[] _cardinalRects = new RectTransform[4];

        private int _lastHeading = int.MinValue;

        public Compass()
        {
            Name = "Compass";
            isToggle = true;

            AddSetting(ModSetting.Toggle("degrees", "Show degrees",
                "Print the numeric heading next to the cardinal direction",
                () => _showDegrees, value =>
                {
                    _showDegrees = value;
                    _lastHeading = int.MinValue;
                }));

            AddSetting(ModSetting.Slider("size", "Size",
                "Compass scale",
                0.6f, 1.6f,
                () => _size, value => _size = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider("opacity", "Opacity",
                "Background transparency",
                0.15f, 1f,
                () => _opacity, value => _opacity = value,
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            _lastHeading = int.MinValue;
            CreateHud();
            ProfilerStats.Register("compass", "Compass", "Sliding heading strip");
            Plugin.Log?.LogInfo("Cascade Compass active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Compass stopped.");
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
                _canvasRect.localPosition = CameraOffset;
                _canvasRect.localRotation = Quaternion.identity;
            }

            float scale = CanvasScale * Mathf.Max(0.1f, _size);
            if (!Mathf.Approximately(_canvasRect.localScale.x, scale))
            {
                _canvasRect.localScale = Vector3.one * scale;
            }

            ApplyOpacity();
            if (_camera == null) return;

            int heading = Mathf.RoundToInt(Mathf.Repeat(_camera.transform.eulerAngles.y, 360f));
            if (heading == _lastHeading) return;

            _lastHeading = heading;
            UpdateHeading(heading);
        }

        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            Transform fallback = Plugin.Root != null ? Plugin.Root.transform : null;
            _canvas = OverlayCanvas.Create("CascadeCompass", _camera, fallback, 224,
                new Vector2(CanvasWidth, CanvasHeight),
                CanvasScale * Mathf.Max(0.1f, _size),
                CameraOffset, out _canvasRect, out _);

            RectTransform background = CascadeUI.Rect("Background", _canvasRect);
            CascadeUI.Stretch(background);
            _panel = CascadeUI.Panel(background, new Color(0.04f, 0.045f, 0.06f, _opacity), 10);

            // Tick marks ride on a line above the cardinal row.
            for (int i = 0; i < TickCount; i++)
            {
                RectTransform tick = CascadeUI.Rect("Tick" + i, _canvasRect);
                tick.anchorMin = tick.anchorMax = new Vector2(0.5f, 0.5f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.sizeDelta = new Vector2(2f, i == TickCount / 2 ? 18f : 12f);
                _tickRects[i] = tick;
                _tickImages[i] = CascadeUI.Solid(tick, TickColor);
            }

            for (int i = 0; i < 4; i++)
            {
                RectTransform labelRect = CascadeUI.Rect("Cardinal" + i, _canvasRect);
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = new Vector2(42f, 20f);
                _cardinalRects[i] = labelRect;
                CascadeUI.Text(labelRect, CardinalLetters[i], 15f,
                    new Color(0.72f, 0.78f, 0.86f, 1f), TextAlignmentOptions.Center, true, false);
            }

            RectTransform headingRect = CascadeUI.Rect("Heading", _canvasRect);
            headingRect.anchorMin = new Vector2(0f, 0f);
            headingRect.anchorMax = new Vector2(1f, 0f);
            headingRect.pivot = new Vector2(0.5f, 0f);
            headingRect.offsetMin = new Vector2(10f, 8f);
            headingRect.offsetMax = new Vector2(-10f, 34f);
            _headingLabel = CascadeUI.Text(headingRect, "N 0°", 22f, Color.white,
                TextAlignmentOptions.Center, true, false);

            UpdateHeading(0);
        }

        private void UpdateHeading(int heading)
        {
            float centerTick = Mathf.Round(heading / 15f) * 15f;

            for (int i = 0; i < TickCount; i++)
            {
                float angle = centerTick + (i - TickCount / 2) * 15f;
                float x = Mathf.DeltaAngle(heading, angle) * PixelsPerDegree;
                bool visible = Mathf.Abs(x) <= StripHalfWidth;

                _tickRects[i].anchoredPosition = new Vector2(x, 18f);
                _tickRects[i].gameObject.SetActive(visible);

                bool center = Mathf.Abs(Mathf.DeltaAngle(heading, angle)) < 7.5f;
                _tickImages[i].color = center ? CenterTickColor : TickColor;
            }

            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.DeltaAngle(heading, i * 90f) * PixelsPerDegree;
                bool visible = Mathf.Abs(x) <= StripHalfWidth;
                _cardinalRects[i].anchoredPosition = new Vector2(x, -4f);
                _cardinalRects[i].gameObject.SetActive(visible);
            }

            if (_headingLabel != null)
            {
                int index = Mathf.RoundToInt(heading / 45f) % 8;
                if (index < 0) index += 8;
                OverlayText.Set(_headingLabel, _showDegrees
                    ? Cardinals[index] + "  " + heading + "°"
                    : Cardinals[index]);
            }
        }

        private void ApplyOpacity()
        {
            if (_panel == null) return;
            Color color = _panel.color;
            color.a = _opacity;
            _panel.color = color;
        }

        private void DestroyHud()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
                _canvasRect = null;
                _panel = null;
                _headingLabel = null;
            }
        }
    }
}
