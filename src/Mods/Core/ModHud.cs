using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// HUD frame designs. Every one reuses the Cascade window / keyboard look:
    /// soft drop shadow, rounded corners, thin border, macOS traffic lights
    /// and keycap-style plates, instead of a plain box with a line.
    /// </summary>
    public enum HudDesign
    {
        /// <summary>Mini Cascade window: rounded body, titlebar with traffic lights.</summary>
        WindowCard = 0,

        /// <summary>Fully round pill with a glowing circular icon disc.</summary>
        Capsule = 1,

        /// <summary>Mini window with a rounded live progress gauge along the bottom.</summary>
        GaugeWindow = 2,

        /// <summary>Keyboard keycap: dark rounded base with a raised rounded key plate.</summary>
        KeyCap = 3,

        /// <summary>Dark terminal window with traffic lights and an accent border.</summary>
        TerminalWindow = 4
    }

    /// <summary>Where a HUD sits in the view. Each HUD picks its own default spot.</summary>
    public enum HudAnchor
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        MidLeft = 3,
        MidRight = 4,
        BottomLeft = 5,
        BottomCenter = 6,
        BottomRight = 7
    }

    /// <summary>
    /// Camera-parented HUD widget used by information and telemetry mods.
    /// </summary>
    public sealed class ModHud
    {
        private const float CanvasScale = 0.0005f;
        private const float TitleBarHeight = 26f;

        private static readonly Color TrafficRed = new Color(1f, 0.37f, 0.34f, 1f);
        private static readonly Color TrafficYellow = new Color(1f, 0.74f, 0.18f, 1f);
        private static readonly Color TrafficGreen = new Color(0.16f, 0.79f, 0.25f, 1f);

        private readonly float _distance;
        private readonly HudDesign _design;
        private Camera _camera;

        internal GameObject Root { get; }
        internal RectTransform Rect { get; }
        internal Canvas Canvas { get; }
        internal TextMeshProUGUI Label { get; }
        internal Image Background { get; }
        internal RectTransform GaugeFill { get; }
        internal Image GaugeImage { get; }

        internal ModHud(
            string name,
            int sortingOrder,
            float width,
            float height,
            float fontSize,
            bool panel,
            bool autoSize,
            TextAlignmentOptions alignment,
            HudDesign design = HudDesign.WindowCard,
            Color? accentColor = null,
            CascadeIcon icon = CascadeIcon.Sparkle,
            string badgeTitle = null)
        {
            _distance = 0.6f;
            _design = design;
            Color accent = accentColor ?? (CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.35f, 0.88f, 1f, 1f));

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

            float leftPad = 14f;
            float rightPad = 14f;
            float topPad = 10f;
            float bottomPad = 10f;

            if (panel)
            {
                switch (_design)
                {
                    case HudDesign.Capsule:
                    {
                        int radius = Mathf.Clamp(Mathf.RoundToInt(height * 0.5f), 16, 34);
                        Background = BuildShell(width, height, radius, new Color(0.035f, 0.045f, 0.07f, 0.8f), accent, 0.42f);

                        RectTransform disc = CascadeUI.TopLeft("BadgeDisc", Rect, 10f, (height - 36f) * 0.5f, 36f, 36f);
                        Color discBg = accent;
                        discBg.a = 0.2f;
                        CascadeUI.Image(disc, CascadeSprites.Circle(), discBg, Image.Type.Simple);
                        CascadeUI.Outline(disc, accent, 18);

                        RectTransform iconRect = CascadeUI.Center("Icon", disc, 0f, 0f, 18f, 18f);
                        CascadeUI.Image(iconRect, CascadeIcons.Get(icon), accent, Image.Type.Simple);

                        leftPad = 56f;
                        rightPad = 18f;
                        topPad = 8f;
                        bottomPad = 8f;
                        break;
                    }

                    case HudDesign.KeyCap:
                    {
                        // Keyboard look: dark rounded key base with a raised, lighter key plate.
                        Background = BuildShell(width, height, 14, new Color(0.02f, 0.025f, 0.04f, 0.88f), accent, 0.3f);

                        RectTransform plate = CascadeUI.Rect("KeyPlate", Rect);
                        plate.anchorMin = Vector2.zero;
                        plate.anchorMax = Vector2.one;
                        plate.offsetMin = new Vector2(5f, 9f);
                        plate.offsetMax = new Vector2(-5f, -4f);
                        CascadeUI.Panel(plate, new Color(0.13f, 0.15f, 0.2f, 0.96f), 11);

                        RectTransform shine = CascadeUI.Rect("KeyShine", plate);
                        shine.anchorMin = new Vector2(0f, 1f);
                        shine.anchorMax = new Vector2(1f, 1f);
                        shine.pivot = new Vector2(0.5f, 1f);
                        shine.offsetMin = new Vector2(8f, -3f);
                        shine.offsetMax = new Vector2(-8f, -1f);
                        CascadeUI.Panel(shine, new Color(1f, 1f, 1f, 0.12f), 1);

                        RectTransform legend = CascadeUI.TopLeft("Legend", plate, 11f, 11f, 8f, 8f);
                        CascadeUI.Image(legend, CascadeSprites.Circle(), accent, Image.Type.Simple);

                        leftPad = 26f;
                        rightPad = 16f;
                        topPad = 9f;
                        bottomPad = 16f;
                        break;
                    }

                    case HudDesign.TerminalWindow:
                    {
                        Background = BuildShell(width, height, 12, new Color(0.02f, 0.025f, 0.035f, 0.88f), accent, 0.5f);
                        BuildTitleBar(width, accent, badgeTitle, new Color(accent.r, accent.g, accent.b, 0.16f));
                        topPad = TitleBarHeight + 6f;
                        break;
                    }

                    case HudDesign.GaugeWindow:
                    {
                        Background = BuildShell(width, height, 14, new Color(0.04f, 0.048f, 0.07f, 0.8f), accent, 0.35f);
                        BuildTitleBar(width, accent, badgeTitle, new Color(1f, 1f, 1f, 0.05f));

                        RectTransform track = CascadeUI.Rect("GaugeTrack", Rect);
                        track.anchorMin = new Vector2(0f, 0f);
                        track.anchorMax = new Vector2(1f, 0f);
                        track.pivot = new Vector2(0.5f, 0f);
                        track.offsetMin = new Vector2(14f, 9f);
                        track.offsetMax = new Vector2(-14f, 17f);
                        CascadeUI.Panel(track, new Color(1f, 1f, 1f, 0.12f), 4);

                        GaugeFill = CascadeUI.TopLeft("GaugeFill", track, 0f, 0f, (width - 28f) * 0.45f, 8f);
                        GaugeImage = CascadeUI.Panel(GaugeFill, accent, 4);

                        topPad = TitleBarHeight + 6f;
                        bottomPad = 24f;
                        break;
                    }

                    default: // WindowCard
                    {
                        Background = BuildShell(width, height, 14, new Color(0.04f, 0.048f, 0.07f, 0.8f), accent, 0.35f);
                        BuildTitleBar(width, accent, badgeTitle, new Color(1f, 1f, 1f, 0.05f));
                        topPad = TitleBarHeight + 6f;
                        break;
                    }
                }
            }

            RectTransform labelRect = CascadeUI.Rect("Label", Rect);
            CascadeUI.Stretch(labelRect, leftPad, rightPad, topPad, bottomPad);
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

        /// <summary>Soft shadow, rounded body and a thin accent border (the window frame).</summary>
        private Image BuildShell(float width, float height, int radius, Color body, Color accent, float borderAlpha)
        {
            RectTransform shadow = CascadeUI.Rect("Shadow", Rect);
            CascadeUI.Stretch(shadow, -14f, -14f, -10f, -16f);
            CascadeUI.Image(shadow, CascadeSprites.Shadow(22, 28), new Color(0f, 0f, 0f, 0.55f), Image.Type.Sliced);

            RectTransform bg = CascadeUI.Rect("Body", Rect);
            CascadeUI.Stretch(bg);
            Image bodyImage = CascadeUI.Panel(bg, body, radius);

            RectTransform border = CascadeUI.Rect("Border", Rect);
            CascadeUI.Stretch(border);
            Color borderColor = accent;
            borderColor.a = borderAlpha;
            CascadeUI.Outline(border, borderColor, radius);
            return bodyImage;
        }

        /// <summary>macOS style titlebar: three traffic lights, a hairline and the title.</summary>
        private void BuildTitleBar(float width, Color accent, string title, Color barColor)
        {
            RectTransform bar = CascadeUI.Rect("Titlebar", Rect);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(1.5f, -TitleBarHeight);
            bar.offsetMax = new Vector2(-1.5f, -1.5f);
            Image barImage = CascadeUI.Image(bar, CascadeSprites.RoundedTop(12), barColor, Image.Type.Sliced);
            barImage.raycastTarget = false;

            AddDot(bar, 12f, TrafficRed);
            AddDot(bar, 26f, TrafficYellow);
            AddDot(bar, 40f, TrafficGreen);

            RectTransform hairline = CascadeUI.Rect("Hairline", bar);
            hairline.anchorMin = new Vector2(0f, 0f);
            hairline.anchorMax = new Vector2(1f, 0f);
            hairline.pivot = new Vector2(0.5f, 0f);
            hairline.offsetMin = Vector2.zero;
            hairline.offsetMax = new Vector2(0f, 1f);
            CascadeUI.Solid(hairline, new Color(1f, 1f, 1f, 0.09f));

            if (!string.IsNullOrEmpty(title))
            {
                RectTransform titleRect = CascadeUI.TopLeft("Title", bar, 56f, 5f, width - 70f, 16f);
                Color titleColor = Color.Lerp(accent, Color.white, 0.35f);
                CascadeUI.Text(titleRect, title, 10.5f, titleColor, TextAlignmentOptions.Left, true, false);
            }
        }

        private static void AddDot(RectTransform bar, float x, Color color)
        {
            RectTransform dot = CascadeUI.TopLeft("Dot", bar, x, 8f, 9f, 9f);
            CascadeUI.Image(dot, CascadeSprites.Circle(), color, Image.Type.Simple);
        }

        /// <summary>Updates the optional gauge bar ratio (0..1) on GaugeWindow HUDs.</summary>
        internal void SetGauge(float normalizedRatio)
        {
            if (GaugeFill == null) return;
            float maxW = Mathf.Max(10f, Rect.sizeDelta.x - 28f);
            GaugeFill.sizeDelta = new Vector2(maxW * Mathf.Clamp01(normalizedRatio), 8f);
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

        /// <summary>
        /// Places the HUD at one of eight spots around the view. Slots 1+ step
        /// inward from the edge so HUDs sharing a spot do not overlap.
        /// </summary>
        internal void SetAnchor(HudAnchor anchor, int slot, float size, float distance)
        {
            float x = 0f;
            float y = 0f;
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            float step = 0.08f * Mathf.Max(0.1f, size) * slot;

            switch (anchor)
            {
                case HudAnchor.TopLeft:
                    x = -0.20f; y = 0.15f - step; pivot = new Vector2(0f, 1f); break;
                case HudAnchor.TopCenter:
                    x = 0f; y = 0.16f - step; pivot = new Vector2(0.5f, 1f); break;
                case HudAnchor.TopRight:
                    x = 0.20f; y = 0.15f - step; pivot = new Vector2(1f, 1f); break;
                case HudAnchor.MidLeft:
                    x = -0.24f; y = 0.09f - step; pivot = new Vector2(0f, 1f); break;
                case HudAnchor.MidRight:
                    x = 0.24f; y = 0.09f - step; pivot = new Vector2(1f, 1f); break;
                case HudAnchor.BottomLeft:
                    x = -0.20f; y = -0.16f + step; pivot = new Vector2(0f, 0f); break;
                case HudAnchor.BottomCenter:
                    x = 0f; y = -0.17f + step; pivot = new Vector2(0.5f, 0f); break;
                default:
                    x = 0.20f; y = -0.16f + step; pivot = new Vector2(1f, 0f); break;
            }

            Rect.pivot = pivot;
            Rect.localScale = Vector3.one * (CanvasScale * Mathf.Max(0.1f, size));
            Rect.localPosition = new Vector3(x, y, distance);
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
    /// Base class for a read-only telemetry / HUD mod. Each subclass picks a
    /// <see cref="HudDesign"/>, accent color and icon; the user can switch the
    /// design in the mod's settings page.
    /// </summary>
    public abstract class OverlayMod : Mod
    {
        private static readonly string[] AnchorNames =
        {
            "Top left", "Top center", "Top right", "Mid left", "Mid right",
            "Bottom left", "Bottom center", "Bottom right"
        };

        private static readonly string[] DesignNames =
        {
            "Window Card", "Capsule", "Gauge Window", "Key Cap", "Terminal Window"
        };

        // Null until the user picks a spot; then the HUD's own default is used.
        private int? _anchorChoice;
        private float _size = 1f;
        private float _opacity = 0.8f;

        protected ModHud Hud;
        protected float CardWidth = 310f;
        protected float CardHeight = 104f;
        protected float FontSize = 19f;
        protected bool ShowPanel = true;
        protected bool AutoSize;
        protected int SortingOrder = 240;
        protected float ViewDistance = 0.6f;
        protected TextAlignmentOptions Alignment = TextAlignmentOptions.TopLeft;

        /// <summary>This HUD's own spot in the view, so HUDs do not all sit in one corner.</summary>
        protected HudAnchor DefaultAnchor = HudAnchor.TopLeft;

        /// <summary>Steps inward from the edge when several HUDs share a spot.</summary>
        protected int DefaultSlot;

        protected HudDesign Design = HudDesign.WindowCard;
        protected Color AccentColor = new Color(0.35f, 0.88f, 1f, 1f);
        internal CascadeIcon BadgeIcon = CascadeIcon.Sparkle;
        protected string BadgeTitle = null;
        protected float GaugeValue = -1f;

        // Longest gap between text rebuilds. Zero means every frame.
        protected float RefreshInterval;
        private float _nextRefresh;

        protected OverlayMod()
        {
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "design", "HUD style",
                "Window card, capsule, gauge window, keycap or terminal window",
                DesignNames,
                () => (int)Design,
                value =>
                {
                    HudDesign next = (HudDesign)Mathf.Clamp(value, 0, DesignNames.Length - 1);
                    if (Design == next) return;
                    Design = next;
                    RebuildHud();
                }));

            AddSetting(ModSetting.Dropdown(
                "anchor", "Position",
                "Where in your view this HUD sits (each HUD has its own default spot)",
                AnchorNames,
                () => _anchorChoice.HasValue ? _anchorChoice.Value : (int)DefaultAnchor,
                value =>
                {
                    _anchorChoice = Mathf.Clamp(value, 0, AnchorNames.Length - 1);
                    ApplyLayout();
                }));

            AddSetting(ModSetting.Slider(
                "size", "Size",
                "HUD scale",
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
                "HUD background transparency",
                0.15f, 1f,
                () => _opacity,
                value =>
                {
                    _opacity = value;
                    ApplyOpacity();
                },
                value => (value * 100f).ToString("0") + "%"));
        }

        private void RebuildHud()
        {
            if (Hud != null)
            {
                Hud.Destroy();
                Hud = null;
            }
            if (Enabled && IsActive)
            {
                CreateHudInstance();
                if (Hud != null)
                {
                    Hud.Label.text = Build() ?? string.Empty;
                    if (GaugeValue >= 0f) Hud.SetGauge(GaugeValue);
                }
            }
        }

        private void CreateHudInstance()
        {
            Hud = new ModHud(
                "CascadeHud_" + (string.IsNullOrEmpty(Name) ? "Widget" : Name.Replace(" ", "")),
                SortingOrder,
                CardWidth,
                CardHeight + (NeedsTitleBar(Design) ? 26f : 0f),
                FontSize,
                ShowPanel,
                AutoSize,
                Alignment,
                Design,
                AccentColor,
                BadgeIcon,
                BadgeTitle ?? Name.ToUpperInvariant());
            ApplyLayout();
            ApplyOpacity();
        }

        private static bool NeedsTitleBar(HudDesign design)
        {
            return design == HudDesign.WindowCard ||
                   design == HudDesign.GaugeWindow ||
                   design == HudDesign.TerminalWindow;
        }

        public sealed override void OnEnable()
        {
            if (Hud == null || Hud.Root == null)
            {
                CreateHudInstance();
            }

            OnOverlayStarted();
            if (Hud != null)
            {
                Hud.Label.text = Build() ?? string.Empty;
                if (GaugeValue >= 0f) Hud.SetGauge(GaugeValue);
            }
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
                CreateHudInstance();
            }

            if (Hud.Follow()) ApplyLayout();
            ApplyOpacity();

            if (RefreshInterval > 0f && Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            string text = Build();
            if (text != null) Hud.Label.text = text;
            if (GaugeValue >= 0f) Hud.SetGauge(GaugeValue);
        }

        protected virtual void OnOverlayStarted() { }
        protected virtual void OnOverlayStopped() { }
        protected abstract string Build();

        private void ApplyLayout()
        {
            if (Hud == null) return;
            HudAnchor anchor = _anchorChoice.HasValue ? (HudAnchor)_anchorChoice.Value : DefaultAnchor;
            int slot = _anchorChoice.HasValue ? 0 : DefaultSlot;
            Hud.SetAnchor(anchor, slot, _size, ViewDistance);
        }

        private void ApplyOpacity()
        {
            if (Hud != null) Hud.SetOpacity(_opacity);
        }
    }
}
