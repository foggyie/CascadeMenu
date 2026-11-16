using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
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

            GameObject root = new GameObject("CascadeScreenTint",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Transform parent = _camera != null
                ? _camera.transform
                : (Plugin.Root != null ? Plugin.Root.transform : null);
            root.transform.SetParent(parent, false);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 190;
            _canvas.worldCamera = _camera;

            _canvasRect = (RectTransform)root.transform;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);
            _canvasRect.sizeDelta = new Vector2(CanvasSize, CanvasSize);
            _canvasRect.localScale = Vector3.one * CanvasScale;
            _canvasRect.localPosition = new Vector3(0f, 0f, ViewDistance);
            _canvasRect.localRotation = Quaternion.identity;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

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
