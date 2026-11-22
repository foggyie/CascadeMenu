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
    /// Camera-parented speed readout at the bottom of the view. Speed is the
    /// smoothed world-space movement of the local rig, with a small fill bar so
    /// the current pace is readable at a glance.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class Speedometer : Mod
    {
        private const float CanvasWidth = 240f;
        private const float CanvasHeight = 86f;
        private const float CanvasScale = 0.0005f;
        private const float ViewDistance = 0.6f;
        private const float BarWidth = 210f;
        private const float MaxDisplaySpeed = 15f;

        private static readonly Vector3 CameraOffset = new Vector3(0f, -0.2f, ViewDistance);
        private static readonly string[] UnitNames = { "m/s", "km/h", "mph" };

        private const int UnitMeters = 0;
        private const int UnitKmh = 1;
        private const int UnitMph = 2;

        private int _unit = UnitMeters;
        private bool _showMax = true;
        private float _size = 1f;
        private float _opacity = 0.72f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private Image _panel;
        private TextMeshProUGUI _valueLabel;
        private TextMeshProUGUI _unitLabel;
        private TextMeshProUGUI _maxLabel;
        private RectTransform _fillRect;
        private Image _fill;

        private bool _hasLast;
        private Vector3 _lastPosition;
        private float _lastTime;
        private float _speed;
        private float _smoothed;
        private float _maxSpeed;
        private float _nextText;

        public Speedometer()
        {
            Name = "Speedometer";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("unit", "Units",
                "Speed unit shown in the readout",
                UnitNames,
                () => _unit,
                value => _unit = value));

            AddSetting(ModSetting.Toggle("max", "Show maximum",
                "Keep the highest speed seen while the overlay is on",
                () => _showMax, value => _showMax = value));

            AddSetting(ModSetting.Slider("size", "Size",
                "Speedometer scale",
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
            _hasLast = false;
            _speed = 0f;
            _smoothed = 0f;
            _maxSpeed = 0f;
            CreateHud();
            ProfilerStats.Register("speedometer", "Speedometer", "Local movement speed card");
            Plugin.Log?.LogInfo("Cascade Speedometer active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Speedometer stopped.");
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

            Measure();
            ApplyOpacity();
            UpdateText();
        }

        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            GameObject root = new GameObject("CascadeSpeedometer",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Transform parent = _camera != null
                ? _camera.transform
                : (Plugin.Root != null ? Plugin.Root.transform : null);
            root.transform.SetParent(parent, false);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 223;
            _canvas.worldCamera = _camera;

            _canvasRect = (RectTransform)root.transform;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _canvasRect.localScale = Vector3.one * (CanvasScale * Mathf.Max(0.1f, _size));
            _canvasRect.localPosition = CameraOffset;
            _canvasRect.localRotation = Quaternion.identity;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform background = CascadeUI.Rect("Background", _canvasRect);
            CascadeUI.Stretch(background);
            _panel = CascadeUI.Panel(background, new Color(0.04f, 0.045f, 0.06f, _opacity), 10);

            RectTransform caption = CascadeUI.TopLeft("Caption", _canvasRect, 14f, 8f, 110f, 16f);
            CascadeUI.Text(caption, "SPEED", 11f, new Color(0.72f, 0.78f, 0.86f, 1f),
                TextAlignmentOptions.Left, true, false);

            RectTransform maxRect = CascadeUI.Rect("Max", _canvasRect);
            maxRect.anchorMin = maxRect.anchorMax = new Vector2(1f, 1f);
            maxRect.pivot = new Vector2(1f, 1f);
            maxRect.anchoredPosition = new Vector2(-14f, -8f);
            maxRect.sizeDelta = new Vector2(100f, 16f);
            _maxLabel = CascadeUI.Text(maxRect, string.Empty, 11f,
                new Color(0.72f, 0.78f, 0.86f, 1f), TextAlignmentOptions.Right, false, false);

            RectTransform valueRect = CascadeUI.TopLeft("Value", _canvasRect, 14f, 24f, 130f, 38f);
            _valueLabel = CascadeUI.Text(valueRect, "0.0", 32f, Color.white,
                TextAlignmentOptions.Left, true, false);

            RectTransform unitRect = CascadeUI.TopLeft("Unit", _canvasRect, 138f, 34f, 70f, 20f);
            _unitLabel = CascadeUI.Text(unitRect, UnitNames[_unit], 15f,
                new Color(0.72f, 0.78f, 0.86f, 1f), TextAlignmentOptions.Left, false, false);

            RectTransform barBackground = CascadeUI.TopLeft("Bar", _canvasRect, 14f, 68f, BarWidth, 8f);
            CascadeUI.Panel(barBackground, new Color(1f, 1f, 1f, 0.12f), 4);

            _fillRect = CascadeUI.TopLeft("Fill", barBackground, 0f, 0f, 0f, 8f);
            _fill = CascadeUI.Panel(_fillRect, new Color(0.35f, 0.9f, 1f, 0.95f), 4);

            UpdateText();
        }

        private void Measure()
        {
            VRRig rig = VRRig.LocalRig;
            if (rig == null)
            {
                _hasLast = false;
                _speed = 0f;
                _smoothed = Mathf.MoveTowards(_smoothed, 0f, Time.unscaledDeltaTime * 6f);
                return;
            }

            Vector3 position = rig.transform.position;
            float now = Time.unscaledTime;

            if (!_hasLast)
            {
                _lastPosition = position;
                _lastTime = now;
                _hasLast = true;
                return;
            }

            float dt = now - _lastTime;
            if (dt > 0.0001f)
            {
                float instant = Vector3.Distance(position, _lastPosition) / dt;

                // Ignore respawns and room teleports.
                if (instant < 40f)
                {
                    _speed = instant;
                    _smoothed = Mathf.Lerp(_smoothed, instant, 1f - Mathf.Exp(-10f * dt));
                }
            }

            _lastPosition = position;
            _lastTime = now;

            if (_smoothed > _maxSpeed) _maxSpeed = _smoothed;
        }

        private void UpdateText()
        {
            if (_valueLabel == null) return;

            float value = Convert(_smoothed);
            float max = Convert(_maxSpeed);

            if (OverlayText.Due(ref _nextText))
            {
                OverlayText.Set(_valueLabel, value.ToString("0.0"));
                OverlayText.Set(_unitLabel, UnitNames[Mathf.Clamp(_unit, 0, UnitNames.Length - 1)]);
                OverlayText.Set(_maxLabel, _showMax ? "MAX " + max.ToString("0.0") : string.Empty);
            }

            float ratio = Mathf.Clamp01(_smoothed / MaxDisplaySpeed);
            _fillRect.sizeDelta = new Vector2(BarWidth * ratio, 8f);
            _fill.color = ratio > 0.8f
                ? new Color(1f, 0.42f, 0.36f, 0.95f)
                : ratio > 0.5f
                    ? new Color(1f, 0.82f, 0.36f, 0.95f)
                    : new Color(0.35f, 0.9f, 1f, 0.95f);
        }

        private float Convert(float metersPerSecond)
        {
            switch (_unit)
            {
                case UnitKmh: return metersPerSecond * 3.6f;
                case UnitMph: return metersPerSecond * 2.23694f;
                default: return metersPerSecond;
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
                _valueLabel = null;
                _unitLabel = null;
                _maxLabel = null;
                _fillRect = null;
                _fill = null;
            }
        }
    }
}
