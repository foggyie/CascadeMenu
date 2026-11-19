using System;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Camera-parented clock with an optional date line. The canvas is pinned to
    /// one corner of the view, so it follows the head without per-frame maths.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class Clock : Mod
    {
        private const float CanvasWidth = 300f;
        private const float CanvasHeight = 84f;
        private const float CanvasScale = 0.0005f;
        private const float ViewDistance = 0.6f;

        private static readonly string[] PositionNames =
        {
            "Top left", "Top right", "Bottom left", "Bottom right"
        };

        private const int PositionTopLeft = 0;
        private const int PositionTopRight = 1;
        private const int PositionBottomLeft = 2;
        private const int PositionBottomRight = 3;

        private int _position = PositionTopLeft;
        private bool _use24Hour = true;
        private bool _showSeconds = true;
        private bool _showDate = true;
        private float _size = 1f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private TextMeshProUGUI _timeLabel;
        private TextMeshProUGUI _dateLabel;
        private int _lastSecond = -1;
        private int _lastDay = -1;

        public Clock()
        {
            Name = "Clock";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("position", "Position",
                "Corner of the view that holds the clock",
                PositionNames,
                () => _position,
                value =>
                {
                    _position = value;
                    ApplyPlacement();
                }));

            AddSetting(ModSetting.Toggle("24hour", "24-hour time",
                "Show 18:30 instead of 6:30 PM",
                () => _use24Hour,
                value =>
                {
                    _use24Hour = value;
                    _lastSecond = -1;
                }));

            AddSetting(ModSetting.Toggle("seconds", "Seconds",
                "Include seconds in the clock",
                () => _showSeconds,
                value =>
                {
                    _showSeconds = value;
                    _lastSecond = -1;
                }));

            AddSetting(ModSetting.Toggle("date", "Date",
                "Show the day and date below the time",
                () => _showDate,
                value =>
                {
                    _showDate = value;
                    _lastDay = -1;
                }));

            AddSetting(ModSetting.Slider("size", "Size",
                "Clock scale",
                0.6f, 1.6f,
                () => _size, value => _size = value,
                value => value.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            _lastSecond = -1;
            _lastDay = -1;
            CreateHud();
            ProfilerStats.Register("clock", "Clock", "Corner clock with optional date");
            Plugin.Log?.LogInfo("Cascade Clock active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Clock stopped.");
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
                ApplyPlacement();
            }

            float scale = CanvasScale * Mathf.Max(0.1f, _size);
            if (!Mathf.Approximately(_canvasRect.localScale.x, scale))
            {
                _canvasRect.localScale = Vector3.one * scale;
            }

            DateTime now = DateTime.Now;
            if (now.Second == _lastSecond && now.DayOfYear == _lastDay) return;

            _lastSecond = now.Second;
            _lastDay = now.DayOfYear;
            UpdateText(now);
        }

        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            Transform fallback = Plugin.Root != null ? Plugin.Root.transform : null;
            _canvas = OverlayCanvas.Create("CascadeClock", _camera, fallback, 225,
                new Vector2(CanvasWidth, CanvasHeight),
                CanvasScale * Mathf.Max(0.1f, _size),
                Vector3.zero, out _canvasRect, out _);

            RectTransform background = CascadeUI.Rect("Background", _canvasRect);
            CascadeUI.Stretch(background);
            CascadeUI.Panel(background, new Color(0.04f, 0.045f, 0.06f, 0.72f), 10);

            RectTransform timeRect = CascadeUI.Rect("Time", _canvasRect);
            timeRect.anchorMin = new Vector2(0f, 0f);
            timeRect.anchorMax = new Vector2(1f, 1f);
            timeRect.pivot = new Vector2(0.5f, 0.5f);
            timeRect.offsetMin = new Vector2(16f, 30f);
            timeRect.offsetMax = new Vector2(-16f, -8f);
            _timeLabel = CascadeUI.Text(timeRect, "--:--:--", 34f, Color.white,
                TextAlignmentOptions.Left, true, false);

            RectTransform dateRect = CascadeUI.Rect("Date", _canvasRect);
            dateRect.anchorMin = new Vector2(0f, 0f);
            dateRect.anchorMax = new Vector2(1f, 0f);
            dateRect.pivot = new Vector2(0.5f, 0f);
            dateRect.offsetMin = new Vector2(16f, 8f);
            dateRect.offsetMax = new Vector2(-16f, 30f);
            _dateLabel = CascadeUI.Text(dateRect, string.Empty, 17f,
                new Color(0.78f, 0.82f, 0.88f, 1f), TextAlignmentOptions.Left, false, false);

            ApplyPlacement();
            UpdateText(DateTime.Now);
        }

        private void UpdateText(DateTime now)
        {
            if (_timeLabel == null) return;

            string time;
            if (_use24Hour)
            {
                time = _showSeconds ? now.ToString("HH:mm:ss") : now.ToString("HH:mm");
            }
            else
            {
                time = _showSeconds ? now.ToString("h:mm:ss tt") : now.ToString("h:mm tt");
            }
            OverlayText.Set(_timeLabel, time);

            if (_dateLabel != null)
            {
                bool show = _showDate;
                _dateLabel.gameObject.SetActive(show);
                if (show) OverlayText.Set(_dateLabel, now.ToString("ddd, MMM d"));
            }
        }

        private void ApplyPlacement()
        {
            if (_canvasRect == null) return;

            bool right = _position == PositionTopRight || _position == PositionBottomRight;
            bool bottom = _position == PositionBottomLeft || _position == PositionBottomRight;

            _canvasRect.pivot = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);

            // The canvas pivot sits on the chosen screen corner, so text grows
            // inward from that edge.
            float x = right ? 0.24f : -0.24f;
            float y = bottom ? -0.16f : 0.16f;
            _canvasRect.localPosition = new Vector3(x, y, ViewDistance);

            TextAlignmentOptions alignment = right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            if (_timeLabel != null) _timeLabel.alignment = alignment;
            if (_dateLabel != null) _dateLabel.alignment = alignment;
        }

        private void DestroyHud()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
                _canvasRect = null;
                _timeLabel = null;
                _dateLabel = null;
            }
        }
    }
}
