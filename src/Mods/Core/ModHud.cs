using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// A small camera-parented text card shared by every information overlay.
    /// It owns the canvas, background and label, follows the active view
    /// camera when it changes, and can sit in any of the four corners.
    /// </summary>
    public sealed class ModHud
    {
        private const float CanvasScale = 0.0005f;

        private readonly float _distance;
        private Camera _camera;

        internal GameObject Root { get; }
        internal RectTransform Rect { get; }
        internal Canvas Canvas { get; }
        internal TextMeshProUGUI Label { get; }
        internal Image Background { get; }

        internal ModHud(
            string name,
            int sortingOrder,
            float width,
            float height,
            float fontSize,
            bool panel,
            bool autoSize,
            TextAlignmentOptions alignment)
        {
            _distance = 0.6f;

            Root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Canvas = Root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.sortingOrder = sortingOrder;

            Rect = (RectTransform)Root.transform;
            Rect.anchorMin = new Vector2(0.5f, 0.5f);
            Rect.anchorMax = new Vector2(0.5f, 0.5f);
            Rect.pivot = new Vector2(0f, 1f);
            Rect.sizeDelta = new Vector2(width, height);
            Rect.localScale = Vector3.one * CanvasScale;
            Rect.localPosition = new Vector3(-0.2f, 0.15f, _distance);

            CanvasGroup group = Root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            if (panel)
            {
                RectTransform backgroundRect = CascadeUI.Rect("Background", Rect);
                CascadeUI.Stretch(backgroundRect);
                Background = CascadeUI.Panel(backgroundRect, new Color(0.04f, 0.045f, 0.06f, 0.72f), 10);
            }

            RectTransform labelRect = CascadeUI.Rect("Label", Rect);
            CascadeUI.Stretch(labelRect, 14f, 14f, 10f, 10f);
            Label = CascadeUI.Text(labelRect, string.Empty, fontSize, Color.white, alignment, true, false);
            Label.textWrappingMode = TextWrappingModes.NoWrap;
            Label.overflowMode = TextOverflowModes.Overflow;
            Label.raycastTarget = false;

            if (autoSize)
            {
                Label.enableAutoSizing = true;
                Label.fontSizeMin = 8f;
                Label.fontSizeMax = fontSize * CascadeUI.FontScale;
            }

            Follow();
        }

        /// <summary>Reparents to the live camera. Returns true when it moved.</summary>
        internal bool Follow()
        {
            if (Root == null) return false;

            Camera camera = ModKit.ViewCamera;
            if (camera == null || camera == _camera) return false;

            _camera = camera;
            Canvas.worldCamera = camera;
            Root.transform.SetParent(camera.transform, false);
            return true;
        }

        internal void SetCorner(int corner, float size, float distance)
        {
            bool right = corner == 1 || corner == 3;
            bool bottom = corner == 2 || corner == 3;

            Rect.pivot = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
            Rect.localScale = Vector3.one * (CanvasScale * Mathf.Max(0.1f, size));
            Rect.localPosition = new Vector3(
                right ? 0.20f : -0.20f,
                bottom ? -0.16f : 0.15f,
                distance);
            Rect.localRotation = Quaternion.identity;
        }

        internal void SetOpacity(float opacity)
        {
            if (Background == null) return;
            Color color = Background.color;
            color.a = Mathf.Clamp01(opacity);
            Background.color = color;
        }

        internal void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }
    }

    /// <summary>
    /// Base class for a read-only information card. The mod only has to return
    /// one line of rich text; lifecycle, camera following, corner placement,
    /// scale and opacity are handled here.
    /// </summary>
    public abstract class OverlayMod : Mod
    {
        private static readonly string[] CornerNames =
        {
            "Top left", "Top right", "Bottom left", "Bottom right"
        };

        private int _corner;
        private float _size = 1f;
        private float _opacity = 0.72f;

        protected ModHud Hud;
        protected float CardWidth = 300f;
        protected float CardHeight = 96f;
        protected float FontSize = 20f;
        protected bool ShowPanel = true;
        protected bool AutoSize;
        protected int SortingOrder = 240;
        protected float ViewDistance = 0.6f;
        protected TextAlignmentOptions Alignment = TextAlignmentOptions.TopLeft;

        // Longest gap between text rebuilds. Zero means every frame.
        protected float RefreshInterval;
        private float _nextRefresh;

        protected OverlayMod()
        {
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "corner", "Position",
                "Corner of the view that holds this card",
                CornerNames,
                () => _corner,
                value =>
                {
                    _corner = value;
                    ApplyLayout();
                }));

            AddSetting(ModSetting.Slider(
                "size", "Size",
                "Card scale",
                0.6f, 1.6f,
                () => _size,
                value =>
                {
                    _size = value;
                    ApplyLayout();
                },
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "opacity", "Opacity",
                "Card background transparency",
                0.15f, 1f,
                () => _opacity,
                value =>
                {
                    _opacity = value;
                    ApplyOpacity();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        public sealed override void OnEnable()
        {
            if (Hud == null || Hud.Root == null)
            {
                Hud = new ModHud(
                    "Cascade" + GetType().Name,
                    SortingOrder,
                    CardWidth,
                    CardHeight,
                    FontSize,
                    ShowPanel,
                    AutoSize,
                    Alignment);
            }

            ApplyLayout();
            ApplyOpacity();
            OnOverlayStarted();
            if (Hud != null) Hud.Label.text = Build() ?? string.Empty;
        }

        public sealed override void OnDisable()
        {
            if (Hud != null)
            {
                Hud.Destroy();
                Hud = null;
            }
            OnOverlayStopped();
        }

        public sealed override void LateUpdate()
        {
            if (Hud == null || Hud.Root == null)
            {
                Hud = new ModHud(
                    "Cascade" + GetType().Name,
                    SortingOrder,
                    CardWidth,
                    CardHeight,
                    FontSize,
                    ShowPanel,
                    AutoSize,
                    Alignment);
                ApplyLayout();
                ApplyOpacity();
            }

            if (Hud.Follow()) ApplyLayout();
            ApplyOpacity();

            if (RefreshInterval > 0f && Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            string text = Build();
            if (text != null) Hud.Label.text = text;
        }

        protected virtual void OnOverlayStarted() { }
        protected virtual void OnOverlayStopped() { }
        protected abstract string Build();

        private void ApplyLayout()
        {
            if (Hud != null) Hud.SetCorner(_corner, _size, ViewDistance);
        }

        private void ApplyOpacity()
        {
            if (Hud != null) Hud.SetOpacity(_opacity);
        }
    }
}
