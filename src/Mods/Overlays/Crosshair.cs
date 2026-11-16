using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Camera-parented crosshair drawn at the centre of the view. Three plain
    /// shapes (dot, cross, ring) share one world-space canvas, so switching the
    /// style never rebuilds UI or allocates.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class Crosshair : Mod
    {
        private const float CanvasSize = 100f;
        private const float CanvasScale = 0.0005f;
        private const float ViewDistance = 0.45f;

        private static readonly string[] StyleNames = { "Dot", "Cross", "Ring" };
        private static readonly string[] ColorNames = { "White", "Green", "Cyan", "Red", "Yellow" };

        private static readonly Color[] Colors =
        {
            new Color(1f, 1f, 1f, 0.95f),
            new Color(0.35f, 1f, 0.45f, 0.95f),
            new Color(0.35f, 0.9f, 1f, 0.95f),
            new Color(1f, 0.35f, 0.35f, 0.95f),
            new Color(1f, 0.85f, 0.3f, 0.95f)
        };

        private int _style;
        private int _color;
        private float _size = 1f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;

        private RectTransform _dotRect;
        private RectTransform _crossHRect;
        private RectTransform _crossVRect;
        private RectTransform _ringRect;
        private Image _dotImage;
        private Image _crossHImage;
        private Image _crossVImage;
        private Image _ringImage;

        public Crosshair()
        {
            Name = "Crosshair";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("style", "Style",
                "Shape drawn at the centre of your view",
                StyleNames,
                () => _style,
                value => _style = value));

            AddSetting(ModSetting.Dropdown("color", "Color",
                "Crosshair tint",
                ColorNames,
                () => _color,
                value => _color = value));

            AddSetting(ModSetting.Slider("size", "Size",
                "Crosshair scale",
                0.5f, 2f,
                () => _size, value => _size = value,
                value => value.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            CreateHud();
            ProfilerStats.Register("crosshair", "Crosshair",
                "Centre-of-view overlay: dot, cross or ring");
            Plugin.Log?.LogInfo("Cascade Crosshair active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Crosshair stopped.");
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

            ApplyStyle();
        }

        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            GameObject root = new GameObject("CascadeCrosshair",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Transform parent = _camera != null
                ? _camera.transform
                : (Plugin.Root != null ? Plugin.Root.transform : null);
            root.transform.SetParent(parent, false);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 230;
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

            _dotRect = Center("Dot", 8f, 8f);
            _dotImage = CascadeUI.Image(_dotRect, CascadeSprites.Circle(), Color.white, Image.Type.Simple);

            _crossHRect = Center("Cross H", 24f, 2f);
            _crossHImage = CascadeUI.Solid(_crossHRect, Color.white);

            _crossVRect = Center("Cross V", 2f, 24f);
            _crossVImage = CascadeUI.Solid(_crossVRect, Color.white);

            _ringRect = Center("Ring", 26f, 26f);
            _ringImage = CascadeUI.Image(_ringRect, CascadeSprites.CircleRing(1.5f), Color.white, Image.Type.Simple);

            ApplyStyle();
            Plugin.Log?.LogInfo("Cascade Crosshair ready.");
        }

        private RectTransform Center(string name, float width, float height)
        {
            RectTransform rect = CascadeUI.Rect(name, _canvasRect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private void ApplyStyle()
        {
            if (_canvasRect == null) return;

            float unit = 10f * Mathf.Max(0.1f, _size);
            _dotRect.sizeDelta = new Vector2(unit * 0.55f, unit * 0.55f);
            _crossHRect.sizeDelta = new Vector2(unit * 2.4f, Mathf.Max(1.2f, unit * 0.16f));
            _crossVRect.sizeDelta = new Vector2(Mathf.Max(1.2f, unit * 0.16f), unit * 2.4f);
            _ringRect.sizeDelta = new Vector2(unit * 2.6f, unit * 2.6f);

            Color color = Colors[Mathf.Clamp(_color, 0, Colors.Length - 1)];
            _dotImage.color = color;
            _crossHImage.color = color;
            _crossVImage.color = color;
            _ringImage.color = color;

            int style = Mathf.Clamp(_style, 0, 2);
            _dotRect.gameObject.SetActive(style == 0);
            _crossHRect.gameObject.SetActive(style == 1);
            _crossVRect.gameObject.SetActive(style == 1);
            _ringRect.gameObject.SetActive(style == 2);
        }

        private void DestroyHud()
        {
            if (_canvas != null)
            {
                Object.Destroy(_canvas.gameObject);
                _canvas = null;
                _canvasRect = null;
            }
        }
    }
}
