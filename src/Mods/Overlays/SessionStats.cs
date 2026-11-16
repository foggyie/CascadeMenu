using System;
using System.Text;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Camera-parented session card in the top-left of the view. It shows the
    /// frame rate, room population, room name and how long the overlay has been
    /// running. Room reads go through the reflection-guarded snapshot helper.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class SessionStats : Mod
    {
        private const float CanvasWidth = 320f;
        private const float CanvasHeight = 190f;
        private const float CanvasScale = 0.0005f;
        private const float ViewDistance = 0.6f;
        private const float Padding = 14f;
        private const float LineHeight = 26f;
        private const float StatsInterval = 0.35f;

        private static readonly Vector3 CameraOffset = new Vector3(-0.24f, 0.18f, ViewDistance);

        private bool _showFps = true;
        private bool _showPlayers = true;
        private bool _showRoom = true;
        private bool _showUptime = true;
        private float _size = 1f;
        private float _opacity = 0.78f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private RectTransform _panelRect;
        private Image _panel;
        private TextMeshProUGUI _label;

        private float _fpsAccum;
        private int _fpsFrames;
        private float _fps;
        private float _startedAt;
        private float _nextStats;
        private int _lastLines = -1;

        private int _playerCount;
        private string _roomName = "home";
        private string _region = string.Empty;

        private readonly StringBuilder _builder = new StringBuilder(256);

        public SessionStats()
        {
            Name = "Session Stats";
            isToggle = true;

            AddSetting(ModSetting.Toggle("fps", "FPS",
                "Frames per second",
                () => _showFps, value => _showFps = value));
            AddSetting(ModSetting.Toggle("players", "Players",
                "Players in the current room, including you",
                () => _showPlayers, value => _showPlayers = value));
            AddSetting(ModSetting.Toggle("room", "Room",
                "Current room name and region",
                () => _showRoom, value => _showRoom = value));
            AddSetting(ModSetting.Toggle("uptime", "Uptime",
                "How long this overlay has been running",
                () => _showUptime, value => _showUptime = value));

            AddSetting(ModSetting.Slider("size", "Size",
                "Card scale",
                0.6f, 1.6f,
                () => _size, value => _size = value,
                value => value.ToString("0.00") + "x"));
            AddSetting(ModSetting.Slider("opacity", "Opacity",
                "Card background transparency",
                0.2f, 1f,
                () => _opacity, value => _opacity = value,
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            _startedAt = Time.unscaledTime;
            _nextStats = 0f;
            _lastLines = -1;
            CreateHud();
            ProfilerStats.Register("sessionstats", "Session Stats",
                "FPS, room, players and uptime card");
            Plugin.Log?.LogInfo("Cascade Session Stats active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Session Stats stopped.");
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

            UpdateFps();

            float now = Time.unscaledTime;
            if (now >= _nextStats)
            {
                _nextStats = now + StatsInterval;
                RefreshRoom();
            }

            RebuildText();
        }

        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            GameObject root = new GameObject("CascadeSessionStats",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Transform parent = _camera != null
                ? _camera.transform
                : (Plugin.Root != null ? Plugin.Root.transform : null);
            root.transform.SetParent(parent, false);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 222;
            _canvas.worldCamera = _camera;

            _canvasRect = (RectTransform)root.transform;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.pivot = _canvasRect.anchorMin;
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _canvasRect.localScale = Vector3.one * (CanvasScale * Mathf.Max(0.1f, _size));
            _canvasRect.localPosition = CameraOffset;
            _canvasRect.localRotation = Quaternion.identity;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            _panelRect = CascadeUI.Rect("Background", _canvasRect);
            _panelRect.anchorMin = new Vector2(0f, 1f);
            _panelRect.anchorMax = new Vector2(0f, 1f);
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.anchoredPosition = Vector2.zero;
            _panelRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _panel = CascadeUI.Panel(_panelRect, new Color(0.04f, 0.045f, 0.06f, _opacity), 10);

            RectTransform labelRect = CascadeUI.Rect("Text", _panelRect);
            CascadeUI.Stretch(labelRect, Padding, Padding, Padding, Padding);
            _label = CascadeUI.Text(labelRect, string.Empty, 19f, Color.white,
                TextAlignmentOptions.TopLeft, false, false);

            ApplyOpacity();
            RefreshRoom();
            UpdateFps();
            RebuildText();
        }

        private void UpdateFps()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum < 0.25f) return;

            _fps = _fpsFrames / _fpsAccum;
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }

        private void RefreshRoom()
        {
            PlayerActions.RoomSnapshot room = PlayerActions.ReadRoomSnapshot();
            _playerCount = room.PlayerCount;
            _roomName = string.IsNullOrEmpty(room.Name) ? "home" : room.Name;
            _region = string.IsNullOrEmpty(room.Region) ? string.Empty : room.Region;
        }

        private void RebuildText()
        {
            if (_label == null) return;

            _builder.Length = 0;
            int lines = 0;

            if (_showFps)
            {
                AppendLine("FPS  " + (_fps > 0f ? Mathf.RoundToInt(_fps).ToString() : "-"), ref lines);
            }
            if (_showPlayers)
            {
                AppendLine("Players  " + Mathf.Max(1, _playerCount), ref lines);
            }
            if (_showRoom)
            {
                AppendLine("Room  " + _roomName + (_region.Length > 0 ? " (" + _region + ")" : string.Empty), ref lines);
            }
            if (_showUptime)
            {
                AppendLine("Uptime  " + FormatUptime(Time.unscaledTime - _startedAt), ref lines);
            }

            if (lines == 0) AppendLine("Session Stats", ref lines);

            _label.text = _builder.ToString();

            if (lines != _lastLines)
            {
                _lastLines = lines;
                float height = Padding * 2f + lines * LineHeight;
                _panelRect.sizeDelta = new Vector2(CanvasWidth, height);
            }

            ApplyOpacity();
        }

        private void AppendLine(string text, ref int lines)
        {
            if (lines > 0) _builder.Append('\n');
            _builder.Append(text);
            lines++;
        }

        private void ApplyOpacity()
        {
            if (_panel != null)
            {
                Color color = _panel.color;
                color.a = _opacity;
                _panel.color = color;
            }
        }

        private static string FormatUptime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int hours = total / 3600;
            int minutes = (total / 60) % 60;
            int secs = total % 60;
            return hours > 0
                ? hours + ":" + minutes.ToString("00") + ":" + secs.ToString("00")
                : minutes.ToString("00") + ":" + secs.ToString("00");
        }

        private void DestroyHud()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
                _canvasRect = null;
                _panelRect = null;
                _panel = null;
                _label = null;
            }
        }
    }
}
