using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Camera-parented frame-rate graph. Sixty thin bars scroll right-to-left so
    /// short hitches and long-term trends are visible at a glance.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class FpsGraph : Mod
    {
        private const float CanvasWidth = 270f;
        private const float CanvasHeight = 150f;
        private const float CanvasScale = 0.0005f;
        private const float ViewDistance = 0.6f;
        private const int SampleCount = 60;
        private const float GraphWidth = 242f;
        private const float GraphHeight = 88f;
        private const float BarGap = 1.5f;

        private static readonly string[] PositionNames = { "Top right", "Top left", "Bottom right", "Bottom left" };
        private static readonly string[] ScaleNames = { "60 FPS", "90 FPS", "120 FPS", "144 FPS" };
        private static readonly int[] ScaleValues = { 60, 90, 120, 144 };

        private static readonly Color BarHigh = new Color(0.35f, 0.9f, 0.5f, 0.95f);
        private static readonly Color BarMid = new Color(1f, 0.82f, 0.36f, 0.95f);
        private static readonly Color BarLow = new Color(1f, 0.42f, 0.36f, 0.95f);

        private int _position;
        private int _scale = 2;
        private float _size = 1f;
        private float _opacity = 0.72f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private Image _panel;
        private TextMeshProUGUI _valueLabel;
        private RectTransform _chartRect;
        private readonly RectTransform[] _bars = new RectTransform[SampleCount];
        private readonly Image[] _barImages = new Image[SampleCount];
        private readonly float[] _samples = new float[SampleCount];

        private float _fpsAccum;
        private int _fpsFrames;
        private float _fps;
        private float _nextSample;
        private int _head;

        public FpsGraph()
        {
            Name = "FPS Graph";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("position", "Position",
                "Corner of the view that holds the graph",
                PositionNames,
                () => _position,
                value =>
                {
                    _position = value;
                    ApplyPlacement();
                }));

            AddSetting(ModSetting.Dropdown("scale", "Scale",
                "Highest frame rate shown at the top of the graph",
                ScaleNames,
                () => _scale,
                value => _scale = value));

            AddSetting(ModSetting.Slider("size", "Size",
                "Graph scale",
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
            _fpsAccum = 0f;
            _fpsFrames = 0;
            _fps = 0f;
            _nextSample = 0f;
            _head = 0;
            for (int i = 0; i < _samples.Length; i++) _samples[i] = 0f;
            CreateHud();
            ProfilerStats.Register("fpsgraph", "FPS Graph", "Scrolling frame-rate bars");
            Plugin.Log?.LogInfo("Cascade FPS Graph active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade FPS Graph stopped.");
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

            UpdateFps();
            ApplyOpacity();

            float now = Time.unscaledTime;
            if (now >= _nextSample)
            {
                _nextSample = now + 0.1f;
                PushSample(_fps);
            }

            if (_valueLabel != null)
            {
                _valueLabel.text = _fps > 0f ? Mathf.RoundToInt(_fps).ToString() : "-";
            }
        }

        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            GameObject root = new GameObject("CascadeFpsGraph",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Transform parent = _camera != null
                ? _camera.transform
                : (Plugin.Root != null ? Plugin.Root.transform : null);
            root.transform.SetParent(parent, false);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 221;
            _canvas.worldCamera = _camera;

            _canvasRect = (RectTransform)root.transform;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _canvasRect.localScale = Vector3.one * (CanvasScale * Mathf.Max(0.1f, _size));
            _canvasRect.localRotation = Quaternion.identity;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform background = CascadeUI.Rect("Background", _canvasRect);
            CascadeUI.Stretch(background);
            _panel = CascadeUI.Panel(background, new Color(0.04f, 0.045f, 0.06f, _opacity), 10);

            RectTransform captionRect = CascadeUI.TopLeft("Caption", _canvasRect, 14f, 8f, 90f, 18f);
            CascadeUI.Text(captionRect, "FPS", 13f,
                new Color(0.72f, 0.78f, 0.86f, 1f), TextAlignmentOptions.Left, true, false);

            RectTransform valueRect = CascadeUI.Rect("Value", _canvasRect);
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.pivot = new Vector2(1f, 1f);
            valueRect.anchoredPosition = new Vector2(-14f, -4f);
            valueRect.sizeDelta = new Vector2(80f, 26f);
            _valueLabel = CascadeUI.Text(valueRect, "-", 20f, Color.white,
                TextAlignmentOptions.Right, true, false);

            _chartRect = CascadeUI.TopLeft("Chart", _canvasRect, 14f, 36f, GraphWidth, GraphHeight);

            RectTransform chartBackground = CascadeUI.Rect("ChartBackground", _chartRect);
            CascadeUI.Stretch(chartBackground);
            CascadeUI.Panel(chartBackground, new Color(1f, 1f, 1f, 0.06f), 6);

            float barWidth = (GraphWidth - (SampleCount - 1) * BarGap) / SampleCount;
            for (int i = 0; i < SampleCount; i++)
            {
                RectTransform bar = CascadeUI.Rect("Bar" + i, _chartRect);
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(0f, 0f);
                bar.pivot = new Vector2(0f, 0f);
                bar.anchoredPosition = new Vector2(2f + i * (barWidth + BarGap), 2f);
                bar.sizeDelta = new Vector2(barWidth, 0f);
                _bars[i] = bar;
                _barImages[i] = CascadeUI.Solid(bar, BarHigh);
            }

            ApplyPlacement();
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

        private void PushSample(float value)
        {
            _samples[_head] = value;
            _head = (_head + 1) % SampleCount;

            int scale = ScaleValues[Mathf.Clamp(_scale, 0, ScaleValues.Length - 1)];
            float maxHeight = GraphHeight - 4f;
            float barWidth = (GraphWidth - (SampleCount - 1) * BarGap) / SampleCount;

            for (int i = 0; i < SampleCount; i++)
            {
                // Oldest sample on the left, newest on the right.
                int index = (_head + i) % SampleCount;
                float sample = _samples[index];
                float height = Mathf.Clamp01(sample / Mathf.Max(1, scale)) * maxHeight;

                _bars[i].sizeDelta = new Vector2(barWidth, height);

                Color color;
                if (sample >= 60f) color = BarHigh;
                else if (sample >= 30f) color = BarMid;
                else color = BarLow;
                _barImages[i].color = sample > 0f ? color : new Color(1f, 1f, 1f, 0.12f);
            }
        }

        private void ApplyPlacement()
        {
            if (_canvasRect == null) return;

            bool right = _position == 0 || _position == 2;
            bool bottom = _position == 2 || _position == 3;

            _canvasRect.pivot = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
            _canvasRect.localPosition = new Vector3(
                right ? 0.24f : -0.24f,
                bottom ? -0.18f : 0.18f,
                ViewDistance);
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
                _chartRect = null;
            }
        }
    }
}
