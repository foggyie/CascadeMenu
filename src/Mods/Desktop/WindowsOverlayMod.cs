using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Desktop
{
    /// <summary>
    /// Cascade-styled Windows 10/11 Overlay Manager for Gorilla Tag (VR & PC).
    /// - Click "Add Windows" on the Cascade Dock to open a dedicated Cascade Window Chooser UI
    ///   displaying real Windows 10/11 app icons, titles, process names and resolutions.
    /// - Click any app card to spawn it into the world (up to 4 simultaneous windows).
    /// - Dock any window to your HUD (stays smoothly on screen) or undock it into
    ///   Stationary world space (grab & move with Grip in VR or Middle Mouse on PC).
    /// - Thin white laser + cursor dot: Trigger / LMB = Left Click & Drag,
    ///   Double-press B (or RMB on PC) = Right Click, Thumbstick / Wheel = Scroll.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Desktop")]
    public sealed class WindowsOverlayMod : Mod
    {
        private static readonly string[] ModeNames = { "Stationary (In World)", "Docked to HUD (On Screen)" };
        private static readonly string[] HandNames = { "Right Hand", "Left Hand" };
        private static readonly string[] DockPlacementNames = { "Follow Lower View (HUD)", "Stationary (Grab with Grip)" };
        private static readonly string[] FpsPresetNames =
        {
            "5 FPS (Ultra Eco)",
            "10 FPS (Eco)",
            "15 FPS",
            "30 FPS",
            "60 FPS",
            "120 FPS",
            "240 FPS",
            "Native (Uncapped)"
        };
        private static readonly float[] FpsPresetValues = { 5f, 10f, 15f, 30f, 60f, 120f, 240f, 0f };
        private static readonly string[] QualityPresetNames =
        {
            "Performance (720×405)",
            "Balanced (1280×720)",
            "Full HD (1920×1080)",
            "Native (1:1 Window Quality)"
        };
        private static readonly string[] PcClickModeNames =
        {
            "Direct Message (No Focus Steal)",
            "Real OS Click (On Release — Recommended)"
        };

        private int _defaultWindowMode = 0;
        private int _pointerHand = 0;
        private int _dockPlacement = 0;
        private int _fpsPreset = 3;      // Default: 30 FPS (can go down to 5 FPS or up to 240/Native)
        private int _qualityPreset = 3;  // Default: Native (1:1 Window Quality)
        private int _pcClickMode = 1;    // Default: Real OS Click on release (works in Chrome/Discord/Spotify)
        private float _scale = 1f;
        private bool _alwaysOnTop = true;
        private bool _showDock = true;
        private bool _showMediaBar = true;
        private bool _pcUnlockCursor = true;
        private bool _clickTestMode = false;

        private GameObject _host;
        private WindowsVrOverlayController _controller;

        public WindowsOverlayMod()
        {
            Name = "Windows VR Overlay";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "mode", "New window placement",
                "Whether newly opened Windows apps start Stationary in world space or Docked to your HUD",
                ModeNames,
                () => _defaultWindowMode,
                value =>
                {
                    _defaultWindowMode = Mathf.Clamp(value, 0, ModeNames.Length - 1);
                    SyncController();
                }));

            AddSetting(ModSetting.Dropdown(
                "dockMode", "Cascade Dock placement",
                "Keep the Cascade overlay dock in your lower view or leave it stationary in world space",
                DockPlacementNames,
                () => _dockPlacement,
                value =>
                {
                    _dockPlacement = Mathf.Clamp(value, 0, DockPlacementNames.Length - 1);
                    SyncController();
                }));

            AddSetting(ModSetting.Dropdown(
                "hand", "VR pointer hand",
                "Which VR controller aims the thin white laser, clicks, grabs with Grip, and double-taps B/Y to right-click",
                HandNames,
                () => _pointerHand,
                value =>
                {
                    _pointerHand = Mathf.Clamp(value, 0, HandNames.Length - 1);
                    SyncController();
                }));

            AddSetting(ModSetting.Dropdown(
                "fpsPreset", "Capture FPS (Note: accesses your PC)",
                "Note: This mod accesses your local Windows PC windows, mouse & keyboard. Pick 60, 240, or Native uncapped capture FPS.",
                FpsPresetNames,
                () => _fpsPreset,
                value =>
                {
                    _fpsPreset = Mathf.Clamp(value, 0, FpsPresetNames.Length - 1);
                    SyncController();
                }));

            AddSetting(ModSetting.Dropdown(
                "qualityPreset", "Window capture quality",
                "Native captures your Windows app at 1:1 full resolution with zero downscaling (local PC access).",
                QualityPresetNames,
                () => _qualityPreset,
                value =>
                {
                    _qualityPreset = Mathf.Clamp(value, 0, QualityPresetNames.Length - 1);
                    SyncController();
                }));

            AddSetting(ModSetting.Slider(
                "scale", "Default window scale",
                "Base scale multiplier for Windows overlays",
                0.5f, 2.0f,
                () => _scale,
                value =>
                {
                    _scale = Mathf.Clamp(value, 0.5f, 2.0f);
                    SyncController();
                },
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Toggle(
                "dock", "Show Cascade Dock",
                "Show the floating Cascade Dock bar with Add Windows, app tiles, keyboard and clock",
                () => _showDock,
                value =>
                {
                    _showDock = value;
                    SyncController();
                }));

            AddSetting(ModSetting.Toggle(
                "media", "Show media controls",
                "Show the compact Prev / Play-Pause / Next bar under the Cascade Dock",
                () => _showMediaBar,
                value =>
                {
                    _showMediaBar = value;
                    SyncController();
                }));

            AddSetting(ModSetting.Toggle(
                "pcCursor", "PC free mouse cursor",
                "Unlock the mouse cursor on PC so you can freely point and click the Dock, Add Windows UI, and apps",
                () => _pcUnlockCursor,
                value =>
                {
                    _pcUnlockCursor = value;
                    SyncController();
                }));

            AddSetting(ModSetting.Dropdown(
                "pcClickMode", "PC click method",
                "Direct Message never steals focus; Real OS Click fires safely on mouse release for Chrome/Discord/Electron.",
                PcClickModeNames,
                () => _pcClickMode,
                value =>
                {
                    _pcClickMode = Mathf.Clamp(value, 0, PcClickModeNames.Length - 1);
                    SyncController();
                }));

            AddSetting(ModSetting.Toggle(
                "clickTest", "Click Test overlay",
                "Shows live click coordinates, hit counter, and a visual marker wherever you click on the window",
                () => _clickTestMode,
                value =>
                {
                    _clickTestMode = value;
                    SyncController();
                }));

            AddSetting(ModSetting.Toggle(
                "onTop", "Render over walls",
                "Keep Windows overlays, dock, chooser and white laser visible through world geometry",
                () => _alwaysOnTop,
                value =>
                {
                    _alwaysOnTop = value;
                    if (_controller != null) _controller.SetAlwaysOnTop(value);
                }));
        }

        public override void OnEnable()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = new GameObject("CascadeWindowsOverlayHost");
            if (Plugin.Root != null) _host.transform.SetParent(Plugin.Root.transform, false);

            _controller = _host.AddComponent<WindowsVrOverlayController>();
            _controller.Initialize(this);
            SyncController();
            Plugin.Log?.LogInfo("Cascade Windows VR Overlay active.");
        }

        public override void OnDisable()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
                _controller = null;
            }
            Plugin.Log?.LogInfo("Cascade Windows VR Overlay stopped.");
        }

        private void SyncController()
        {
            if (_controller == null) return;
            _controller.DefaultDocked = _defaultWindowMode == 1;
            _controller.DockStationary = _dockPlacement == 1;
            _controller.UseLeftHand = _pointerHand == 1;
            _controller.CaptureFps = FpsPresetValues[Mathf.Clamp(_fpsPreset, 0, FpsPresetValues.Length - 1)];
            _controller.CaptureQuality = (Win32DesktopBridge.CaptureQualityMode)Mathf.Clamp(_qualityPreset, 0, 3);
            _controller.DefaultScale = _scale;
            _controller.PcUnlockCursor = _pcUnlockCursor;
            _controller.PcRealOsClickOnRelease = _pcClickMode == 1;
            _controller.ClickTestEnabled = _clickTestMode;
            _controller.SetDockVisible(_showDock);
            _controller.SetMediaBarVisible(_showMediaBar);
            _controller.SetAlwaysOnTop(_alwaysOnTop);
        }
    }

    // =========================================================================
    // Cascade Multi-Window Overlay Manager (VR + PC)
    // =========================================================================
    internal sealed class WindowsVrOverlayController : MonoBehaviour
    {
        private const float BaseCanvasScale = 0.001f;
        private const float BaseScreenHeightPx = 380f;
        private const float HeaderHeightPx = 28f;
        private const float DoublePressWindow = 0.38f;
        private const float LaserMaxDistance = 8f;
        private const int MaxSimultaneousWindows = 4;

        private const ushort VK_MEDIA_NEXT_TRACK = 0xB0;
        private const ushort VK_MEDIA_PREV_TRACK = 0xB1;
        private const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;

        private sealed class InteractiveRect
        {
            public RectTransform Rect;
            public Image Background;
            public Color NormalColor;
            public Color HoverColor;
            public Action OnClick;
            public string Tooltip;
        }

        private sealed class DesktopWindowPanel : IDisposable
        {
            public Win32DesktopBridge.WindowEntry Entry;
            public Win32DesktopBridge.CaptureSession Capture;
            public GameObject Root;
            public Canvas Canvas;
            public RectTransform RootRect;
            public RectTransform HeaderRect;
            public RectTransform MoveHandleRect;
            public RectTransform ScreenRect;
            public RawImage ScreenImage;
            public TextMeshProUGUI TitleText;
            public TextMeshProUGUI ModePillText;
            public readonly List<InteractiveRect> Buttons = new List<InteractiveRect>();

            public bool DockedToHud;
            public int HudSlotIndex;
            public float Scale = 1f;
            public float NextCaptureTime;
            public bool Visible = true;

            public void Dispose()
            {
                if (Capture != null)
                {
                    Capture.Dispose();
                    Capture = null;
                }
                if (Root != null)
                {
                    UnityEngine.Object.Destroy(Root);
                    Root = null;
                }
                Buttons.Clear();
            }
        }

        private sealed class KeyCapBinding
        {
            public string Normal;
            public string Shifted;
            public TextMeshProUGUI Label;
        }

        internal bool DefaultDocked { get; set; }
        internal bool DockStationary { get; set; }
        internal bool UseLeftHand { get; set; }
        internal float CaptureFps { get; set; } = 60f;
        internal Win32DesktopBridge.CaptureQualityMode CaptureQuality { get; set; } = Win32DesktopBridge.CaptureQualityMode.Native;
        internal float DefaultScale { get; set; } = 1f;
        internal bool PcUnlockCursor { get; set; } = true;
        internal bool PcRealOsClickOnRelease { get; set; }
        internal bool ClickTestEnabled { get; set; }

        private WindowsOverlayMod _owner;
        private readonly List<Win32DesktopBridge.WindowEntry> _availableWindows = new List<Win32DesktopBridge.WindowEntry>();
        private readonly Dictionary<IntPtr, Texture2D> _iconCache = new Dictionary<IntPtr, Texture2D>();
        private readonly List<DesktopWindowPanel> _panels = new List<DesktopWindowPanel>();
        private DesktopWindowPanel _focusedPanel;
        private bool _alwaysOnTop = true;

        // PC cursor restoration
        private bool _cursorManaged;

        // Shared materials
        private Material _laserMaterial;
        private Material _uiOverlayMaterial;

        // Cascade Dock
        private GameObject _dockRoot;
        private Canvas _dockCanvas;
        private RectTransform _dockBarRect;
        private RectTransform _dockTilesContainer;
        private TextMeshProUGUI _dockTooltipText;
        private TextMeshProUGUI _clockTimeText;
        private TextMeshProUGUI _clockDateText;
        private RectTransform _mediaBarRect;
        private bool _dockVisible = true;
        private bool _mediaVisible = true;
        private float _nextClockUpdate;
        private readonly List<InteractiveRect> _dockButtons = new List<InteractiveRect>();
        private readonly List<InteractiveRect> _dockWindowTileButtons = new List<InteractiveRect>();

        // Dedicated Floating "Add Window" Chooser UI (separate centered window!)
        private GameObject _pickerRoot;
        private Canvas _pickerCanvas;
        private RectTransform _pickerRect;
        private RectTransform _pickerGridContainer;
        private bool _pickerVisible;
        private readonly List<InteractiveRect> _pickerButtons = new List<InteractiveRect>();

        // Cascade VR Keyboard
        private GameObject _keyboardRoot;
        private Canvas _keyboardCanvas;
        private RectTransform _keyboardRect;
        private bool _keyboardVisible;
        private bool _shiftActive;
        private bool _capsActive;
        private readonly List<InteractiveRect> _keyboardButtons = new List<InteractiveRect>();
        private readonly List<KeyCapBinding> _keyCaps = new List<KeyCapBinding>();

        // Thin white laser + crisp arrow pointer cursor + click test marker
        private LineRenderer _laser;
        private GameObject _cursorObj;
        private Image _cursorArrow;
        private Sprite _cursorArrowSprite;
        private GameObject _clickMarkerObj;
        private Image _clickMarkerImg;
        private float _clickMarkerTimer;
        private float _cursorPulseTimer;
        private Color _cursorPulseColor = Color.white;
        private int _clickTestCount;

        // Input & grab state
        private bool _wasTriggerDown;
        private bool _leftMouseHeldOnWindow;
        private bool _wasSecondaryDown;
        private bool _wasPcRightDown;
        private float _lastSecondaryPressTime = -10f;
        private float _ignoreInputUntil;
        private float _scrollAccum;
        private InteractiveRect _hoveredButton;

        private Transform _grabbedTransform;
        private bool _grabbedWithTrigger;
        private float _pcDragDistance;
        private Vector3 _pcDragOffset;
        private Vector3 _grabLocalPos;
        private Quaternion _grabLocalRot;

        // Cached Cascade theme colors
        private Color _colGlassBg;
        private Color _colTitleBg;
        private Color _colBorder;
        private Color _colAccent;
        private Color _colText;
        private Color _colMuted;
        private Color _colClose;
        private Color _colMin;
        private Color _colZoom;

        internal void Initialize(WindowsOverlayMod owner)
        {
            _owner = owner;
            RefreshThemePalette();
            BuildMaterials();
            BuildLaserAndCursor();
            BuildCascadeDock();
            BuildAddWindowChooserUi();
            BuildVrKeyboard();

            RefreshWindowList();
            if (_availableWindows.Count > 0)
            {
                OpenOrFocusWindow(_availableWindows[0]);
            }

            PositionDockInFrontOfHead();
        }

        private void OnDestroy()
        {
            ReleaseHeldMouse();
            RestorePcCursor();

            for (int i = 0; i < _panels.Count; i++)
                _panels[i].Dispose();
            _panels.Clear();
            _focusedPanel = null;

            foreach (var kv in _iconCache)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            _iconCache.Clear();

            if (_dockRoot != null) Destroy(_dockRoot);
            if (_pickerRoot != null) Destroy(_pickerRoot);
            if (_keyboardRoot != null) Destroy(_keyboardRoot);
            if (_laser != null) Destroy(_laser.gameObject);
            if (_cursorObj != null) Destroy(_cursorObj);
            if (_clickMarkerObj != null) Destroy(_clickMarkerObj);
            if (_cursorArrowSprite != null)
            {
                if (_cursorArrowSprite.texture != null) Destroy(_cursorArrowSprite.texture);
                Destroy(_cursorArrowSprite);
            }
            if (_laserMaterial != null) Destroy(_laserMaterial);
            if (_uiOverlayMaterial != null) Destroy(_uiOverlayMaterial);
        }

        private void RefreshThemePalette()
        {
            CascadeTheme theme = CascadeTheme.Active;
            if (theme != null)
            {
                _colGlassBg = theme.ContentBg.WithAlpha(0.68f);
                _colTitleBg = theme.TitlebarBg.WithAlpha(0.76f);
                _colBorder = new Color(1f, 1f, 1f, 0.36f);
                _colAccent = theme.Accent.WithAlpha(0.95f);
                _colText = theme.TextPrimary;
                _colMuted = theme.TextSecondary;
                _colClose = theme.TrafficClose;
                _colMin = theme.TrafficMinimize;
                _colZoom = theme.TrafficZoom;
            }
            else
            {
                _colGlassBg = new Color(0.06f, 0.08f, 0.12f, 0.68f);
                _colTitleBg = new Color(0.09f, 0.11f, 0.16f, 0.76f);
                _colBorder = new Color(1f, 1f, 1f, 0.36f);
                _colAccent = new Color(0.26f, 0.60f, 1f, 0.95f);
                _colText = Color.white;
                _colMuted = new Color(0.72f, 0.78f, 0.88f, 0.9f);
                _colClose = new Color(1f, 0.37f, 0.34f, 1f);
                _colMin = new Color(1f, 0.74f, 0.18f, 1f);
                _colZoom = new Color(0.16f, 0.80f, 0.25f, 1f);
            }
        }

        internal void SetAlwaysOnTop(bool onTop)
        {
            _alwaysOnTop = onTop;
            ApplyOverlayMaterialSettings();
        }

        internal void SetDockVisible(bool visible)
        {
            _dockVisible = visible;
            if (_dockRoot != null) _dockRoot.SetActive(visible);
        }

        internal void SetMediaBarVisible(bool visible)
        {
            _mediaVisible = visible;
            if (_mediaBarRect != null) _mediaBarRect.gameObject.SetActive(visible);
        }

        private void SetPickerVisible(bool visible)
        {
            _pickerVisible = visible;
            if (visible)
            {
                RefreshWindowList();
                PositionChooserInFrontOfHead();
            }
            if (_pickerRoot != null) _pickerRoot.SetActive(visible);
        }

        private void SetKeyboardVisible(bool visible)
        {
            _keyboardVisible = visible;
            if (_keyboardRoot != null) _keyboardRoot.SetActive(visible);
            if (visible) PositionKeyboardUnderAnchor();
        }

        private void BuildMaterials()
        {
            Shader laserShader = Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Hidden/Internal-Colored");

            if (laserShader != null)
            {
                _laserMaterial = new Material(laserShader) { color = Color.white };
                _laserMaterial.renderQueue = 4150;
                if (_laserMaterial.HasProperty("_ZTest"))
                    _laserMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                if (_laserMaterial.HasProperty("_ZWrite"))
                    _laserMaterial.SetFloat("_ZWrite", 0f);
                if (_laserMaterial.HasProperty("unity_GUIZTestMode"))
                    _laserMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            }

            Shader uiShader = Shader.Find("UI/Default")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent");

            if (uiShader != null)
            {
                _uiOverlayMaterial = new Material(uiShader) { color = Color.white };
                ApplyOverlayMaterialSettings();
            }
        }

        private void ApplyOverlayMaterialSettings()
        {
            if (_uiOverlayMaterial == null) return;
            int ztest = _alwaysOnTop ? (int)CompareFunction.Always : (int)CompareFunction.LessEqual;
            _uiOverlayMaterial.renderQueue = _alwaysOnTop ? 4020 : 3000;
            if (_uiOverlayMaterial.HasProperty("unity_GUIZTestMode"))
                _uiOverlayMaterial.SetInt("unity_GUIZTestMode", ztest);
            if (_uiOverlayMaterial.HasProperty("_ZTest"))
                _uiOverlayMaterial.SetFloat("_ZTest", ztest);

            int order = _alwaysOnTop ? 360 : 50;
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i].Canvas != null) _panels[i].Canvas.sortingOrder = order + i;
            }
            if (_dockCanvas != null) _dockCanvas.sortingOrder = order + 20;
            if (_keyboardCanvas != null) _keyboardCanvas.sortingOrder = order + 25;
            if (_pickerCanvas != null) _pickerCanvas.sortingOrder = order + 30;
        }

        private void ApplyUiMat(Graphic g)
        {
            if (g != null && _uiOverlayMaterial != null) g.material = _uiOverlayMaterial;
        }

        private Texture2D GetCachedAppIcon(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            if (_iconCache.TryGetValue(hwnd, out Texture2D existing) && existing != null)
                return existing;

            Texture2D icon = Win32DesktopBridge.ExtractWindowIcon(hwnd);
            if (icon != null) _iconCache[hwnd] = icon;
            return icon;
        }

        // -----------------------------------------------------------------
        // Multi-Window Panel Lifecycle
        // -----------------------------------------------------------------
        private void RefreshWindowList()
        {
            _availableWindows.Clear();
            _availableWindows.AddRange(Win32DesktopBridge.EnumerateWindows());
            RebuildChooserCards();
        }

        private void OpenOrFocusWindow(Win32DesktopBridge.WindowEntry entry)
        {
            if (entry == null) return;

            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i].Entry != null && _panels[i].Entry.Hwnd == entry.Hwnd)
                {
                    _panels[i].Visible = true;
                    if (_panels[i].Root != null) _panels[i].Root.SetActive(true);
                    _focusedPanel = _panels[i];
                    RebuildDockWindowTiles();
                    return;
                }
            }

            if (_panels.Count >= MaxSimultaneousWindows)
            {
                ClosePanel(_panels[0]);
            }

            DesktopWindowPanel panel = CreateWindowPanel(entry);
            _panels.Add(panel);
            _focusedPanel = panel;
            ApplyOverlayMaterialSettings();
            RebuildDockWindowTiles();
        }

        private void ClosePanel(DesktopWindowPanel panel)
        {
            if (panel == null) return;
            if (_grabbedTransform == (panel.Root != null ? panel.Root.transform : null))
                _grabbedTransform = null;

            _panels.Remove(panel);
            panel.Dispose();

            if (_focusedPanel == panel)
                _focusedPanel = _panels.Count > 0 ? _panels[_panels.Count - 1] : null;

            RebuildDockWindowTiles();
        }

        private DesktopWindowPanel CreateWindowPanel(Win32DesktopBridge.WindowEntry entry)
        {
            var panel = new DesktopWindowPanel
            {
                Entry = entry,
                Capture = new Win32DesktopBridge.CaptureSession(),
                DockedToHud = DefaultDocked,
                HudSlotIndex = FindFreeHudSlot(),
                Scale = Mathf.Clamp(DefaultScale, 0.5f, 2.0f)
            };

            panel.Root = new GameObject("CascadeWinPanel_" + entry.ProcessName);
            panel.Root.transform.SetParent(transform, false);

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(panel.Root.transform, false);
            panel.Canvas = canvasGo.AddComponent<Canvas>();
            panel.Canvas.renderMode = RenderMode.WorldSpace;
            panel.Canvas.worldCamera = CascadeMenuBehaviour.ResolveViewCamera();

            float aspect = (entry.Width > 0 && entry.Height > 0)
                ? Mathf.Clamp((float)entry.Width / entry.Height, 0.7f, 2.3f)
                : 16f / 9f;
            float screenH = BaseScreenHeightPx;
            float screenW = Mathf.Clamp(screenH * aspect, 420f, 760f);

            panel.RootRect = canvasGo.GetComponent<RectTransform>();
            panel.RootRect.sizeDelta = new Vector2(screenW + 14f, screenH + HeaderHeightPx + 14f);
            panel.RootRect.localScale = Vector3.one * (BaseCanvasScale * panel.Scale);

            // Cascade macOS glass frame
            RectTransform frame = CascadeUI.Rect("Frame", panel.RootRect);
            CascadeUI.Stretch(frame);
            ApplyUiMat(CascadeUI.Panel(frame, _colGlassBg, 14));

            RectTransform border = CascadeUI.Rect("Border", panel.RootRect);
            CascadeUI.Stretch(border);
            ApplyUiMat(CascadeUI.Outline(border, _colBorder, 14));

            // macOS Titlebar with traffic-light dots (drag this bar with Left Click / Trigger to move!)
            RectTransform header = CascadeUI.Rect("Header", panel.RootRect);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = new Vector2(0f, -2f);
            header.sizeDelta = new Vector2(-4f, HeaderHeightPx);
            ApplyUiMat(CascadeUI.Panel(header, _colTitleBg, 12));
            panel.HeaderRect = header;

            AddTrafficDot(panel.Buttons, header, 14f, _colClose, "Close window", () => ClosePanel(panel));
            AddTrafficDot(panel.Buttons, header, 32f, _colMin, "Dock / Undock", () => TogglePanelDock(panel));
            AddTrafficDot(panel.Buttons, header, 50f, _colZoom, "Cycle size", () =>
            {
                panel.Scale = panel.Scale < 1.15f ? 1.30f : (panel.Scale < 1.45f ? 0.75f : 1.0f);
            });

            RectTransform titleRt = CascadeUI.Rect("Title", header);
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0.76f, 1f);
            titleRt.offsetMin = new Vector2(68f, 0f);
            titleRt.offsetMax = Vector2.zero;
            panel.TitleText = CascadeUI.Text(titleRt, entry.DisplayLabel, 11.5f, _colText,
                TextAlignmentOptions.MidlineLeft, true, false);

            RectTransform pillRt = CascadeUI.Rect("ModePill", header);
            pillRt.anchorMin = new Vector2(0.78f, 0.16f);
            pillRt.anchorMax = new Vector2(1f, 0.84f);
            pillRt.offsetMin = Vector2.zero;
            pillRt.offsetMax = new Vector2(-8f, 0f);
            ApplyUiMat(CascadeUI.Panel(pillRt, _colAccent.WithAlpha(0.28f), 7));
            RectTransform pillTxtRt = CascadeUI.Rect("Lbl", pillRt);
            CascadeUI.Stretch(pillTxtRt);
            panel.ModePillText = CascadeUI.Text(pillTxtRt, panel.DockedToHud ? "HUD" : "WORLD", 9.5f,
                _colText, TextAlignmentOptions.Center, true, false);

            // Live captured Windows surface (dark placeholder until first frame arrives so it never flashes white)
            panel.ScreenRect = CascadeUI.Center("Screen", panel.RootRect, 0f, -HeaderHeightPx * 0.5f, screenW, screenH);
            panel.ScreenImage = panel.ScreenRect.gameObject.AddComponent<RawImage>();
            panel.ScreenImage.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            panel.ScreenImage.raycastTarget = false;
            ApplyUiMat(panel.ScreenImage);

            // Minimal Cascade floating action pill right beneath the window
            RectTransform bottomPill = CascadeUI.Rect("BottomPill", panel.RootRect);
            bottomPill.anchorMin = new Vector2(0.5f, 0f);
            bottomPill.anchorMax = new Vector2(0.5f, 0f);
            bottomPill.pivot = new Vector2(0.5f, 1f);
            bottomPill.anchoredPosition = new Vector2(0f, -10f);
            bottomPill.sizeDelta = new Vector2(236f, 34f);
            ApplyUiMat(CascadeUI.Panel(bottomPill, _colGlassBg, 17));
            RectTransform bpOutline = CascadeUI.Rect("Outline", bottomPill);
            CascadeUI.Stretch(bpOutline);
            ApplyUiMat(CascadeUI.Outline(bpOutline, _colBorder, 17));

            float bx = 8f;
            // Dedicated Move (4-way cross-arrow) drag handle — hold Left Click / Trigger to move window anywhere!
            RectTransform moveRt = CascadeUI.TopLeft("MoveHandle", bottomPill, bx, 4f, 34f, 26f);
            bx += 40f;
            Image moveBg = CascadeUI.Panel(moveRt, _colAccent.WithAlpha(0.45f), 10);
            ApplyUiMat(moveBg);
            RectTransform moveIcRt = CascadeUI.Center("Icon", moveRt, 0f, 0f, 16f, 16f);
            ApplyUiMat(CascadeUI.Image(moveIcRt, CascadeIcons.Get(CascadeIcon.Move), _colText, Image.Type.Simple));
            panel.MoveHandleRect = moveRt;

            AddIconButton(panel.Buttons, bottomPill, "Dock", ref bx, 32f, 26f,
                CascadeIcon.Monitor, _colAccent.WithAlpha(0.28f), "Dock to HUD / Undock World",
                () => TogglePanelDock(panel));

            AddIconButton(panel.Buttons, bottomPill, "Kb", ref bx, 32f, 26f,
                CascadeIcon.Keyboard, new Color(1f, 1f, 1f, 0.12f), "Toggle VR Keyboard", () =>
                {
                    _focusedPanel = panel;
                    SetKeyboardVisible(!_keyboardVisible);
                });

            AddIconButton(panel.Buttons, bottomPill, "Minus", ref bx, 28f, 26f,
                CascadeIcon.Minus, new Color(1f, 1f, 1f, 0.10f), "Shrink window",
                () => panel.Scale = Mathf.Clamp(panel.Scale - 0.12f, 0.45f, 2.0f));

            AddIconButton(panel.Buttons, bottomPill, "Plus", ref bx, 28f, 26f,
                CascadeIcon.Plus, new Color(1f, 1f, 1f, 0.10f), "Enlarge window",
                () => panel.Scale = Mathf.Clamp(panel.Scale + 0.12f, 0.45f, 2.0f));

            AddIconButton(panel.Buttons, bottomPill, "Close", ref bx, 32f, 26f,
                CascadeIcon.Close, _colClose.WithAlpha(0.45f), "Close window",
                () => ClosePanel(panel));

            PlacePanelInWorld(panel, _panels.Count);
            return panel;
        }

        private void AddTrafficDot(List<InteractiveRect> list, RectTransform header, float left,
            Color dotColor, string tooltip, Action onClick)
        {
            RectTransform rt = CascadeUI.TopLeft("Dot_" + left, header, left, 8f, 12f, 12f);
            Image img = CascadeUI.Image(rt, CascadeSprites.Circle(), dotColor, Image.Type.Simple);
            ApplyUiMat(img);
            list.Add(new InteractiveRect
            {
                Rect = rt,
                Background = img,
                NormalColor = dotColor,
                HoverColor = Color.Lerp(dotColor, Color.white, 0.35f),
                OnClick = onClick,
                Tooltip = tooltip
            });
        }

        private Image AddIconButton(List<InteractiveRect> list, RectTransform parent, string name,
            ref float x, float w, float h, CascadeIcon icon, Color bgColor, string tooltip, Action onClick)
        {
            RectTransform rt = CascadeUI.TopLeft(name, parent, x, (parent.sizeDelta.y - h) * 0.5f, w, h);
            x += w + 6f;

            Image bg = CascadeUI.Panel(rt, bgColor, 10);
            ApplyUiMat(bg);

            RectTransform icRt = CascadeUI.Center("Icon", rt, 0f, 0f, 16f, 16f);
            Image icImg = CascadeUI.Image(icRt, CascadeIcons.Get(icon), _colText, Image.Type.Simple);
            ApplyUiMat(icImg);

            list.Add(new InteractiveRect
            {
                Rect = rt,
                Background = bg,
                NormalColor = bgColor,
                HoverColor = Color.Lerp(bgColor, _colAccent, 0.55f),
                OnClick = onClick,
                Tooltip = tooltip
            });
            return icImg;
        }

        private void TogglePanelDock(DesktopWindowPanel panel)
        {
            if (panel == null) return;
            panel.DockedToHud = !panel.DockedToHud;
            if (panel.DockedToHud) panel.HudSlotIndex = FindFreeHudSlot(panel);
            if (panel.ModePillText != null)
                panel.ModePillText.text = panel.DockedToHud ? "HUD" : "WORLD";
            RebuildDockWindowTiles();
        }

        private int FindFreeHudSlot(DesktopWindowPanel ignore = null)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                bool used = false;
                for (int i = 0; i < _panels.Count; i++)
                {
                    if (_panels[i] != ignore && _panels[i].DockedToHud && _panels[i].HudSlotIndex == slot)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used) return slot;
            }
            return 0;
        }

        private void PlacePanelInWorld(DesktopWindowPanel panel, int indexOffset)
        {
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            if (cam == null || panel.Root == null) return;

            Transform head = cam.transform;
            Vector3 flatFwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = head.forward;
            flatFwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flatFwd).normalized;

            float sideOffset = indexOffset == 0 ? 0f : (indexOffset % 2 == 1 ? -0.52f : 0.52f);
            Vector3 pos = head.position + flatFwd * 1.15f + right * sideOffset + Vector3.up * 0.06f;
            panel.Root.transform.position = pos;
            panel.Root.transform.rotation = Quaternion.LookRotation((pos - head.position).normalized, Vector3.up);
        }

        // -----------------------------------------------------------------
        // Dedicated Floating "Add Window" Chooser UI (Real App Icons + Cards)
        // -----------------------------------------------------------------
        private void BuildAddWindowChooserUi()
        {
            _pickerRoot = new GameObject("CascadeAddWindowChooser");
            _pickerRoot.transform.SetParent(transform, false);

            var canvasGo = new GameObject("ChooserCanvas");
            canvasGo.transform.SetParent(_pickerRoot.transform, false);
            _pickerCanvas = canvasGo.AddComponent<Canvas>();
            _pickerCanvas.renderMode = RenderMode.WorldSpace;
            _pickerCanvas.worldCamera = CascadeMenuBehaviour.ResolveViewCamera();

            _pickerRect = canvasGo.GetComponent<RectTransform>();
            _pickerRect.sizeDelta = new Vector2(560f, 408f);
            _pickerRect.localScale = Vector3.one * 0.0009f;

            // Cascade macOS glass frame
            ApplyUiMat(CascadeUI.Panel(_pickerRect, _colGlassBg, 16));
            RectTransform outline = CascadeUI.Rect("Outline", _pickerRect);
            CascadeUI.Stretch(outline);
            ApplyUiMat(CascadeUI.Outline(outline, _colAccent.WithAlpha(0.55f), 16));

            // Titlebar
            RectTransform header = CascadeUI.Rect("Header", _pickerRect);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = new Vector2(0f, -2f);
            header.sizeDelta = new Vector2(-4f, 36f);
            ApplyUiMat(CascadeUI.Panel(header, _colTitleBg, 14));

            RectTransform titleRt = CascadeUI.TopLeft("Title", header, 40f, 4f, 300f, 28f);
            CascadeUI.Text(titleRt, "Add Window to VR World", 14f, _colText,
                TextAlignmentOptions.MidlineLeft, true, false);

            // Clear PC Access Notice Banner
            RectTransform noticeRt = CascadeUI.TopLeft("PcNotice", _pickerRect, 14f, 42f, 532f, 24f);
            ApplyUiMat(CascadeUI.Panel(noticeRt, _colAccent.WithAlpha(0.18f), 7));
            RectTransform noticeLbl = CascadeUI.Rect("Lbl", noticeRt);
            CascadeUI.Stretch(noticeLbl);
            CascadeUI.Text(noticeLbl, "Note: Accessing your local Windows PC windows, mouse & keyboard.", 10.5f,
                _colMuted, TextAlignmentOptions.Center, true, false);

            _pickerGridContainer = CascadeUI.TopLeft("Grid", _pickerRect, 14f, 74f, 532f, 320f);
            _pickerRoot.SetActive(false);
        }

        private void RebuildChooserCards()
        {
            if (_pickerRect == null || _pickerGridContainer == null) return;
            _pickerButtons.Clear();

            // Clear old header buttons & grid cards
            Transform header = _pickerRect.Find("Header");
            if (header != null)
            {
                for (int i = header.childCount - 1; i >= 0; i--)
                {
                    Transform c = header.GetChild(i);
                    if (c.name != "Title") Destroy(c.gameObject);
                }
                RectTransform headerRt = header.GetComponent<RectTransform>();
                AddTrafficDot(_pickerButtons, headerRt, 14f, _colClose, "Close Add Window UI", () => SetPickerVisible(false));

                float rx = 456f;
                AddTextButton(_pickerButtons, headerRt, "RefreshApps", ref rx, 86f, 24f, "Refresh",
                    _colAccent.WithAlpha(0.38f), "Rescan open Windows apps", RefreshWindowList);
            }

            for (int i = _pickerGridContainer.childCount - 1; i >= 0; i--)
                Destroy(_pickerGridContainer.GetChild(i).gameObject);

            int maxCards = Mathf.Min(_availableWindows.Count, 10);
            const float cardW = 260f;
            const float cardH = 56f;
            const float gapX = 12f;
            const float gapY = 8f;

            for (int i = 0; i < maxCards; i++)
            {
                Win32DesktopBridge.WindowEntry entry = _availableWindows[i];
                int col = i % 2;
                int row = i / 2;
                float left = col * (cardW + gapX);
                float top = row * (cardH + gapY);

                RectTransform cardRt = CascadeUI.TopLeft("Card_" + i, _pickerGridContainer, left, top, cardW, cardH);
                Color cardBgCol = new Color(1f, 1f, 1f, 0.08f);
                Image cardBg = CascadeUI.Panel(cardRt, cardBgCol, 10);
                ApplyUiMat(cardBg);

                // App icon box on left
                RectTransform iconHolder = CascadeUI.TopLeft("IconBox", cardRt, 10f, 10f, 36f, 36f);
                ApplyUiMat(CascadeUI.Panel(iconHolder, new Color(1f, 1f, 1f, 0.08f), 8));

                Texture2D appIcon = entry.IsFullScreen ? null : GetCachedAppIcon(entry.Hwnd);
                if (appIcon != null)
                {
                    RectTransform rawRt = CascadeUI.Center("RawIcon", iconHolder, 0f, 0f, 28f, 28f);
                    RawImage raw = rawRt.gameObject.AddComponent<RawImage>();
                    raw.texture = appIcon;
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                    ApplyUiMat(raw);
                }
                else
                {
                    RectTransform symRt = CascadeUI.Center("SymIcon", iconHolder, 0f, 0f, 22f, 22f);
                    ApplyUiMat(CascadeUI.Image(symRt, CascadeIcons.Get(entry.IsFullScreen ? CascadeIcon.Monitor : CascadeIcon.Windows),
                        _colAccent, Image.Type.Simple));
                }

                // Title & Subtitle (process name + resolution)
                string cleanTitle = string.IsNullOrEmpty(entry.Title) ? "Untitled Window" : entry.Title;
                if (cleanTitle.Length > 26) cleanTitle = cleanTitle.Substring(0, 23) + "...";

                RectTransform tRt = CascadeUI.TopLeft("CardTitle", cardRt, 54f, 8f, cardW - 62f, 20f);
                CascadeUI.Text(tRt, cleanTitle, 11.5f, _colText, TextAlignmentOptions.MidlineLeft, true, false);

                string sub = (string.IsNullOrEmpty(entry.ProcessName) ? "App" : entry.ProcessName)
                             + " · " + entry.Width + "×" + entry.Height;
                RectTransform sRt = CascadeUI.TopLeft("CardSub", cardRt, 54f, 29f, cardW - 62f, 18f);
                CascadeUI.Text(sRt, sub, 9.5f, _colMuted, TextAlignmentOptions.MidlineLeft, false, false);

                Win32DesktopBridge.WindowEntry captured = entry;
                _pickerButtons.Add(new InteractiveRect
                {
                    Rect = cardRt,
                    Background = cardBg,
                    NormalColor = cardBgCol,
                    HoverColor = _colAccent.WithAlpha(0.42f),
                    Tooltip = "Open " + captured.DisplayLabel,
                    OnClick = () =>
                    {
                        OpenOrFocusWindow(captured);
                        SetPickerVisible(false);
                    }
                });
            }
        }

        private void PositionChooserInFrontOfHead()
        {
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            if (cam == null || _pickerRoot == null) return;

            Transform head = cam.transform;
            Vector3 fwd = head.forward;
            _pickerRoot.transform.position = head.position + fwd * 0.92f;
            _pickerRoot.transform.rotation = Quaternion.LookRotation(fwd, head.up);
        }

        // -----------------------------------------------------------------
        // Cascade Floating Dock (macOS Glass Bar + Media Controls)
        // -----------------------------------------------------------------
        private void BuildCascadeDock()
        {
            _dockRoot = new GameObject("CascadeWindowsDock");
            _dockRoot.transform.SetParent(transform, false);

            var canvasGo = new GameObject("DockCanvas");
            canvasGo.transform.SetParent(_dockRoot.transform, false);
            _dockCanvas = canvasGo.AddComponent<Canvas>();
            _dockCanvas.renderMode = RenderMode.WorldSpace;
            _dockCanvas.worldCamera = CascadeMenuBehaviour.ResolveViewCamera();

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(460f, 130f);
            canvasRt.localScale = Vector3.one * 0.00085f;

            // Hover tooltip above dock
            RectTransform tipRt = CascadeUI.Center("Tooltip", canvasRt, 0f, 44f, 360f, 20f);
            _dockTooltipText = CascadeUI.Text(tipRt, "Cascade Windows Overlay", 11.5f, _colText,
                TextAlignmentOptions.Center, true, false);

            // Main Cascade Dock pill
            _dockBarRect = CascadeUI.Center("DockBar", canvasRt, 0f, 8f, 452f, 54f);
            ApplyUiMat(CascadeUI.Panel(_dockBarRect, _colGlassBg, 18));
            RectTransform dockOutline = CascadeUI.Rect("Outline", _dockBarRect);
            CascadeUI.Stretch(dockOutline);
            ApplyUiMat(CascadeUI.Outline(dockOutline, _colBorder, 18));

            float x = 10f;
            AddIconButton(_dockButtons, _dockBarRect, "AddWin", ref x, 38f, 38f,
                CascadeIcon.Windows, _colAccent.WithAlpha(0.45f), "Add Windows App",
                () => SetPickerVisible(!_pickerVisible));

            AddIconButton(_dockButtons, _dockBarRect, "DockKb", ref x, 38f, 38f,
                CascadeIcon.Keyboard, new Color(1f, 1f, 1f, 0.12f), "Toggle VR Keyboard",
                () => SetKeyboardVisible(!_keyboardVisible));

            AddIconButton(_dockButtons, _dockBarRect, "DockRecenter", ref x, 38f, 38f,
                CascadeIcon.Recenter, new Color(1f, 1f, 1f, 0.12f), "Recenter Dock & Window", () =>
                {
                    PositionDockInFrontOfHead();
                    if (_focusedPanel != null && !_focusedPanel.DockedToHud)
                        PlacePanelInWorld(_focusedPanel, 0);
                });

            _dockTilesContainer = CascadeUI.TopLeft("Tiles", _dockBarRect, x + 4f, 8f, 180f, 38f);

            // Live Clock & Date
            RectTransform clockRt = CascadeUI.TopLeft("Clock", _dockBarRect, 346f, 8f, 96f, 38f);
            RectTransform timeRt = CascadeUI.TopLeft("Time", clockRt, 0f, 0f, 96f, 20f);
            _clockTimeText = CascadeUI.Text(timeRt, DateTime.Now.ToString("h:mm tt"), 12f, _colText,
                TextAlignmentOptions.MidlineRight, true, false);
            RectTransform dateRt = CascadeUI.TopLeft("Date", clockRt, 0f, 19f, 96f, 18f);
            _clockDateText = CascadeUI.Text(dateRt, DateTime.Now.ToString("MM/dd/yyyy"), 9.5f, _colMuted,
                TextAlignmentOptions.MidlineRight, false, false);

            // Compact Media Controls pill right beneath the Dock (proper crisp SF-Symbol media icons!)
            _mediaBarRect = CascadeUI.Center("MediaBar", canvasRt, 0f, -40f, 166f, 36f);
            ApplyUiMat(CascadeUI.Panel(_mediaBarRect, _colGlassBg, 14));
            RectTransform mOutline = CascadeUI.Rect("Outline", _mediaBarRect);
            CascadeUI.Stretch(mOutline);
            ApplyUiMat(CascadeUI.Outline(mOutline, _colBorder, 14));

            float mx = 10f;
            AddIconButton(_dockButtons, _mediaBarRect, "PrevTrack", ref mx, 44f, 26f,
                CascadeIcon.PrevTrack, new Color(1f, 1f, 1f, 0.10f), "Previous Track",
                () => Win32DesktopBridge.SendVirtualKey(VK_MEDIA_PREV_TRACK));
            AddIconButton(_dockButtons, _mediaBarRect, "PlayPause", ref mx, 48f, 26f,
                CascadeIcon.Pause, _colAccent.WithAlpha(0.36f), "Play / Pause Media",
                () => Win32DesktopBridge.SendVirtualKey(VK_MEDIA_PLAY_PAUSE));
            AddIconButton(_dockButtons, _mediaBarRect, "NextTrack", ref mx, 44f, 26f,
                CascadeIcon.NextTrack, new Color(1f, 1f, 1f, 0.10f), "Next Track",
                () => Win32DesktopBridge.SendVirtualKey(VK_MEDIA_NEXT_TRACK));

            _dockRoot.SetActive(_dockVisible);
            _mediaBarRect.gameObject.SetActive(_mediaVisible);
        }

        private void AddTextButton(List<InteractiveRect> list, RectTransform parent, string name,
            ref float x, float w, float h, string label, Color bgColor, string tooltip, Action onClick)
        {
            RectTransform rt = CascadeUI.TopLeft(name, parent, x, (parent.sizeDelta.y - h) * 0.5f, w, h);
            x += w + 7f;
            Image bg = CascadeUI.Panel(rt, bgColor, 9);
            ApplyUiMat(bg);
            RectTransform lblRt = CascadeUI.Rect("Lbl", rt);
            CascadeUI.Stretch(lblRt);
            CascadeUI.Text(lblRt, label, 11f, _colText, TextAlignmentOptions.Center, true, false);
            list.Add(new InteractiveRect
            {
                Rect = rt,
                Background = bg,
                NormalColor = bgColor,
                HoverColor = Color.Lerp(bgColor, _colAccent, 0.55f),
                OnClick = onClick,
                Tooltip = tooltip
            });
        }

        private void RebuildDockWindowTiles()
        {
            if (_dockTilesContainer == null) return;
            _dockWindowTileButtons.Clear();

            for (int i = _dockTilesContainer.childCount - 1; i >= 0; i--)
                Destroy(_dockTilesContainer.GetChild(i).gameObject);

            float x = 0f;
            for (int i = 0; i < _panels.Count; i++)
            {
                DesktopWindowPanel p = _panels[i];
                bool isFocused = p == _focusedPanel;
                Color bgCol = isFocused ? _colAccent.WithAlpha(0.42f) : new Color(1f, 1f, 1f, 0.12f);

                RectTransform rt = CascadeUI.TopLeft("Tile_" + i, _dockTilesContainer, x, 0f, 38f, 38f);
                x += 43f;
                Image bg = CascadeUI.Panel(rt, bgCol, 10);
                ApplyUiMat(bg);

                Texture2D iconTex = p.Entry.IsFullScreen ? null : GetCachedAppIcon(p.Entry.Hwnd);
                if (iconTex != null)
                {
                    RectTransform rawRt = CascadeUI.Center("Icon", rt, 0f, 1f, 22f, 22f);
                    RawImage raw = rawRt.gameObject.AddComponent<RawImage>();
                    raw.texture = iconTex;
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                    ApplyUiMat(raw);
                }
                else
                {
                    string tag = p.Entry.IsFullScreen
                        ? "PC"
                        : (!string.IsNullOrEmpty(p.Entry.ProcessName)
                            ? p.Entry.ProcessName.Substring(0, Math.Min(2, p.Entry.ProcessName.Length)).ToUpperInvariant()
                            : "W" + (i + 1));
                    RectTransform tagRt = CascadeUI.Rect("Tag", rt);
                    CascadeUI.Stretch(tagRt);
                    CascadeUI.Text(tagRt, tag, 11f, _colText, TextAlignmentOptions.Center, true, false);
                }

                if (p.Visible)
                {
                    RectTransform dot = CascadeUI.Center("Dot", rt, 0f, -15f, 5f, 5f);
                    ApplyUiMat(CascadeUI.Image(dot, CascadeSprites.Circle(),
                        p.DockedToHud ? _colAccent : Color.white, Image.Type.Simple));
                }

                DesktopWindowPanel captured = p;
                _dockWindowTileButtons.Add(new InteractiveRect
                {
                    Rect = rt,
                    Background = bg,
                    NormalColor = bgCol,
                    HoverColor = Color.Lerp(bgCol, Color.white, 0.3f),
                    Tooltip = captured.Entry.DisplayLabel + (captured.DockedToHud ? " (HUD)" : " (World)"),
                    OnClick = () =>
                    {
                        if (_focusedPanel == captured && captured.Visible)
                        {
                            captured.Visible = false;
                            if (captured.Root != null) captured.Root.SetActive(false);
                        }
                        else
                        {
                            captured.Visible = true;
                            if (captured.Root != null) captured.Root.SetActive(true);
                            _focusedPanel = captured;
                        }
                        RebuildDockWindowTiles();
                    }
                });
            }
        }

        private void PositionDockInFrontOfHead()
        {
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            if (cam == null || _dockRoot == null) return;
            Transform head = cam.transform;
            Vector3 flatFwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = head.forward;
            flatFwd.Normalize();

            _dockRoot.transform.position = head.position + flatFwd * 0.72f - Vector3.up * 0.24f;
            _dockRoot.transform.rotation = Quaternion.LookRotation(flatFwd, Vector3.up) * Quaternion.Euler(14f, 0f, 0f);
        }

        // -----------------------------------------------------------------
        // Cascade VR Keyboard
        // -----------------------------------------------------------------
        private void BuildVrKeyboard()
        {
            _keyboardRoot = new GameObject("CascadeWindowsKeyboard");
            _keyboardRoot.transform.SetParent(transform, false);

            var canvasGo = new GameObject("KbCanvas");
            canvasGo.transform.SetParent(_keyboardRoot.transform, false);
            _keyboardCanvas = canvasGo.AddComponent<Canvas>();
            _keyboardCanvas.renderMode = RenderMode.WorldSpace;
            _keyboardCanvas.worldCamera = CascadeMenuBehaviour.ResolveViewCamera();

            _keyboardRect = canvasGo.GetComponent<RectTransform>();
            _keyboardRect.sizeDelta = new Vector2(660f, 208f);
            _keyboardRect.localScale = Vector3.one * 0.00085f;

            ApplyUiMat(CascadeUI.Panel(_keyboardRect, _colGlassBg, 14));
            RectTransform outline = CascadeUI.Rect("Outline", _keyboardRect);
            CascadeUI.Stretch(outline);
            ApplyUiMat(CascadeUI.Outline(outline, _colBorder, 14));

            BuildKeyRow(0, new[]
            {
                ("`","~",1f), ("1","!",1f), ("2","@",1f), ("3","#",1f), ("4","$",1f), ("5","%",1f),
                ("6","^",1f), ("7","&",1f), ("8","*",1f), ("9","(",1f), ("0",")",1f), ("-","_",1f),
                ("=","+",1f), ("Back","Back",1.7f)
            });
            BuildKeyRow(1, new[]
            {
                ("Tab","Tab",1.4f), ("q","Q",1f), ("w","W",1f), ("e","E",1f), ("r","R",1f), ("t","T",1f),
                ("y","Y",1f), ("u","U",1f), ("i","I",1f), ("o","O",1f), ("p","P",1f), ("[","{",1f),
                ("]","}",1f), ("\\","|",1.3f)
            });
            BuildKeyRow(2, new[]
            {
                ("Caps","Caps",1.6f), ("a","A",1f), ("s","S",1f), ("d","D",1f), ("f","F",1f), ("g","G",1f),
                ("h","H",1f), ("j","J",1f), ("k","K",1f), ("l","L",1f), (";",":",1f), ("'","\"",1f),
                ("Enter","Enter",2.1f)
            });
            BuildKeyRow(3, new[]
            {
                ("Shift","Shift",2.0f), ("z","Z",1f), ("x","X",1f), ("c","C",1f), ("v","V",1f), ("b","B",1f),
                ("n","N",1f), ("m","M",1f), (",","<",1f), (".",">",1f), ("/","?",1f), ("Up","Up",1.3f),
                ("Del","Del",1.4f)
            });
            BuildKeyRow(4, new[]
            {
                ("Esc","Esc",1.3f), ("Copy","Copy",1.4f), ("Paste","Paste",1.5f), ("Space","Space",5.2f),
                ("Left","Left",1.2f), ("Down","Down",1.2f), ("Right","Right",1.2f), ("Hide","Hide",1.7f)
            });

            _keyboardRoot.SetActive(false);
        }

        private void BuildKeyRow(int rowIndex, (string normal, string shifted, float weight)[] keys)
        {
            float pad = 10f;
            float gap = 5f;
            float usableW = 660f - pad * 2f - gap * (keys.Length - 1);
            float totalWeight = 0f;
            for (int i = 0; i < keys.Length; i++) totalWeight += keys[i].weight;

            float y = 10f + rowIndex * 38f;
            float x = pad;

            for (int i = 0; i < keys.Length; i++)
            {
                string norm = keys[i].normal;
                string shft = keys[i].shifted;
                float w = usableW * (keys[i].weight / totalWeight);

                RectTransform rt = CascadeUI.TopLeft("Key_" + rowIndex + "_" + i, _keyboardRect, x, y, w, 33f);
                x += w + gap;

                bool isSpecial = norm.Length > 1;
                Color keyCol = isSpecial ? _colAccent.WithAlpha(0.24f) : new Color(1f, 1f, 1f, 0.13f);
                Image bg = CascadeUI.Panel(rt, keyCol, 6);
                ApplyUiMat(bg);

                RectTransform lblRt = CascadeUI.Rect("Lbl", rt);
                CascadeUI.Stretch(lblRt);
                TextMeshProUGUI lbl = CascadeUI.Text(lblRt, norm, isSpecial ? 10f : 12.5f, _colText,
                    TextAlignmentOptions.Center, true, false);

                if (!isSpecial)
                    _keyCaps.Add(new KeyCapBinding { Normal = norm, Shifted = shft, Label = lbl });

                string cNorm = norm;
                string cShift = shft;
                _keyboardButtons.Add(new InteractiveRect
                {
                    Rect = rt,
                    Background = bg,
                    NormalColor = keyCol,
                    HoverColor = _colAccent.WithAlpha(0.65f),
                    Tooltip = cNorm,
                    OnClick = () => HandleKeyClick(cNorm, cShift)
                });
            }
        }

        private void HandleKeyClick(string normal, string shifted)
        {
            IntPtr hwnd = (_focusedPanel != null && _focusedPanel.Entry != null) ? _focusedPanel.Entry.Hwnd : IntPtr.Zero;
            bool useDirectPost = !XRSettings.isDeviceActive && hwnd != IntPtr.Zero;

            if (!useDirectPost && hwnd != IntPtr.Zero)
                Win32DesktopBridge.FocusWindow(hwnd);

            switch (normal)
            {
                case "Back": SendKey(useDirectPost, hwnd, 0x08); return;
                case "Tab": SendKey(useDirectPost, hwnd, 0x09); return;
                case "Enter": SendKey(useDirectPost, hwnd, 0x0D); return;
                case "Esc": SendKey(useDirectPost, hwnd, 0x1B); return;
                case "Space": SendKey(useDirectPost, hwnd, 0x20); return;
                case "Del": SendKey(useDirectPost, hwnd, 0x2E); return;
                case "Left": SendKey(useDirectPost, hwnd, 0x25); return;
                case "Up": SendKey(useDirectPost, hwnd, 0x26); return;
                case "Right": SendKey(useDirectPost, hwnd, 0x27); return;
                case "Down": SendKey(useDirectPost, hwnd, 0x28); return;
                case "Copy": Win32DesktopBridge.SendVirtualKey(0x43, ctrl: true); return;
                case "Paste": Win32DesktopBridge.SendVirtualKey(0x56, ctrl: true); return;
                case "Hide": SetKeyboardVisible(false); return;
                case "Shift":
                    _shiftActive = !_shiftActive;
                    RefreshKeyCapLabels();
                    return;
                case "Caps":
                    _capsActive = !_capsActive;
                    RefreshKeyCapLabels();
                    return;
            }

            bool upper = _shiftActive ^ (_capsActive && normal.Length == 1 && char.IsLetter(normal[0]));
            string text = upper ? shifted : normal;
            if (!string.IsNullOrEmpty(text))
            {
                if (useDirectPost) Win32DesktopBridge.PostWindowChar(hwnd, text[0]);
                else Win32DesktopBridge.SendUnicodeChar(text[0]);
            }

            if (_shiftActive)
            {
                _shiftActive = false;
                RefreshKeyCapLabels();
            }
        }

        private static void SendKey(bool directPost, IntPtr hwnd, ushort vk)
        {
            if (directPost) Win32DesktopBridge.PostWindowVirtualKey(hwnd, vk);
            else Win32DesktopBridge.SendVirtualKey(vk);
        }

        private void RefreshKeyCapLabels()
        {
            for (int i = 0; i < _keyCaps.Count; i++)
            {
                KeyCapBinding cap = _keyCaps[i];
                if (cap.Label == null) continue;
                bool isLetter = cap.Normal.Length == 1 && char.IsLetter(cap.Normal[0]);
                bool upper = _shiftActive ^ (_capsActive && isLetter);
                cap.Label.text = upper ? cap.Shifted : cap.Normal;
            }
        }

        private void PositionKeyboardUnderAnchor()
        {
            if (_keyboardRoot == null) return;
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            if (cam == null) return;

            Transform head = cam.transform;
            Vector3 fwd = head.forward;
            _keyboardRoot.transform.position = head.position + fwd * 0.78f - head.up * 0.16f;
            _keyboardRoot.transform.rotation = Quaternion.LookRotation(fwd, head.up) * Quaternion.Euler(14f, 0f, 0f);
        }

        // -----------------------------------------------------------------
        // Ultra-Thin White Laser & Crisp Arrow Pointer Cursor (No Dot!)
        // -----------------------------------------------------------------
        private void BuildLaserAndCursor()
        {
            var laserGo = new GameObject("CascadeWindowsLaser");
            laserGo.transform.SetParent(transform, false);
            _laser = laserGo.AddComponent<LineRenderer>();
            _laser.useWorldSpace = true;
            _laser.positionCount = 2;
            _laser.startWidth = 0.0014f;
            _laser.endWidth = 0.0009f;
            _laser.numCapVertices = 4;
            _laser.shadowCastingMode = ShadowCastingMode.Off;
            _laser.receiveShadows = false;
            _laser.lightProbeUsage = LightProbeUsage.Off;
            _laser.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _laser.sortingOrder = 410;
            if (_laserMaterial != null) _laser.material = _laserMaterial;

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.32f, 0f), new GradientAlphaKey(0.96f, 1f) });
            _laser.colorGradient = grad;
            _laser.enabled = false;

            // Authentic Windows default mouse cursor (IDC_ARROW from user32.dll, with classic WinXP fallback)
            Vector2 cursorPivot;
            _cursorArrowSprite = BuildArrowCursorSprite(out cursorPivot);
            _cursorObj = new GameObject("CascadeWindowsCursor");
            _cursorObj.transform.SetParent(transform, false);
            Canvas c = _cursorObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 415;
            c.worldCamera = CascadeMenuBehaviour.ResolveViewCamera();

            RectTransform crt = _cursorObj.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(32f, 32f);
            crt.pivot = cursorPivot;
            crt.localScale = Vector3.one * 0.00095f;

            _cursorArrow = crt.gameObject.AddComponent<Image>();
            _cursorArrow.sprite = _cursorArrowSprite;
            _cursorArrow.color = Color.white;
            _cursorArrow.raycastTarget = false;
            ApplyUiMat(_cursorArrow);
            _cursorObj.SetActive(false);

            // Click Test visual ring marker (appears briefly at exact click point)
            _clickMarkerObj = new GameObject("CascadeWindowsClickMarker");
            _clickMarkerObj.transform.SetParent(transform, false);
            Canvas mc = _clickMarkerObj.AddComponent<Canvas>();
            mc.renderMode = RenderMode.WorldSpace;
            mc.sortingOrder = 414;
            mc.worldCamera = CascadeMenuBehaviour.ResolveViewCamera();

            RectTransform mrt = _clickMarkerObj.GetComponent<RectTransform>();
            mrt.sizeDelta = new Vector2(26f, 26f);
            mrt.localScale = Vector3.one * 0.0009f;
            _clickMarkerImg = CascadeUI.Outline(mrt, _colAccent, 13);
            ApplyUiMat(_clickMarkerImg);
            _clickMarkerObj.SetActive(false);
        }

        private static Sprite BuildArrowCursorSprite(out Vector2 pivot)
        {
            Texture2D winCursor = Win32DesktopBridge.ExtractSystemArrowCursor(out pivot);
            if (winCursor != null)
            {
                return Sprite.Create(winCursor, new Rect(0, 0, winCursor.width, winCursor.height), pivot, 100f);
            }

            // Classic Windows XP arrow cursor fallback if system cursor extraction is unavailable
            pivot = new Vector2(0.04f, 0.96f);
            string[] mask =
            {
                "1.......................",
                "11......................",
                "121.....................",
                "1221....................",
                "12221...................",
                "122221..................",
                "1222221.................",
                "12222221................",
                "122222221...............",
                "1222222221..............",
                "12222222221.............",
                "122222222221............",
                "1222221111111...........",
                "1222121.................",
                "1221.121................",
                "121..121................",
                "11....121...............",
                "1.....121...............",
                ".......121..............",
                ".......11...............",
                "........................",
                "........................",
                "........................",
                "........................"
            };

            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                string row = mask[size - 1 - y]; // Unity Texture2D y=0 is bottom
                for (int x = 0; x < size; x++)
                {
                    char ch = row[x];
                    if (ch == '1') pixels[y * size + x] = new Color32(12, 14, 18, 255);
                    else if (ch == '2') pixels[y * size + x] = new Color32(255, 255, 255, 255);
                    else pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.04f, 0.96f), 100f);
        }

        // -----------------------------------------------------------------
        // Per-Frame Update
        // -----------------------------------------------------------------
        private void LateUpdate()
        {
            UpdatePcCursorUnlock();
            UpdateClock();
            UpdatePoses();
            UpdateCaptures();
            UpdateInteraction();
            UpdateCursorPulse();
        }

        private void UpdatePcCursorUnlock()
        {
            if (XRSettings.isDeviceActive) return;

            if (PcUnlockCursor || _pickerVisible || _keyboardVisible)
            {
                _cursorManaged = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (_cursorManaged)
            {
                RestorePcCursor();
            }
        }

        private void RestorePcCursor()
        {
            if (!_cursorManaged) return;
            // Never trap the user by re-locking their mouse if they're using menus on PC
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorManaged = false;
        }

        private void UpdateClock()
        {
            if (Time.unscaledTime < _nextClockUpdate) return;
            _nextClockUpdate = Time.unscaledTime + 1f;
            if (_clockTimeText != null) _clockTimeText.text = DateTime.Now.ToString("h:mm tt");
            if (_clockDateText != null) _clockDateText.text = DateTime.Now.ToString("MM/dd/yyyy");
        }

        private void UpdatePoses()
        {
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            if (cam == null) return;
            Transform head = cam.transform;
            float smooth = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);

            for (int i = 0; i < _panels.Count; i++)
            {
                DesktopWindowPanel p = _panels[i];
                if (p.Root == null || !p.Visible) continue;

                float sc = BaseCanvasScale * Mathf.Clamp(p.Scale, 0.45f, 2.0f);
                if (p.RootRect != null) p.RootRect.localScale = Vector3.one * sc;

                if (p.DockedToHud && _grabbedTransform != p.Root.transform)
                {
                    Vector3 localOffset = GetHudSlotOffset(p.HudSlotIndex);
                    Vector3 targetPos = head.TransformPoint(localOffset);
                    Quaternion targetRot = Quaternion.LookRotation((targetPos - head.position).normalized, head.up);
                    p.Root.transform.position = Vector3.Lerp(p.Root.transform.position, targetPos, smooth);
                    p.Root.transform.rotation = Quaternion.Slerp(p.Root.transform.rotation, targetRot, smooth);
                }
            }

            if (_dockVisible && _dockRoot != null && !DockStationary && _grabbedTransform != _dockRoot.transform)
            {
                Vector3 dockTarget = head.TransformPoint(new Vector3(0f, -0.24f, 0.72f));
                Quaternion dockRot = Quaternion.LookRotation((dockTarget - head.position).normalized, head.up) * Quaternion.Euler(8f, 0f, 0f);
                _dockRoot.transform.position = Vector3.Lerp(_dockRoot.transform.position, dockTarget, smooth);
                _dockRoot.transform.rotation = Quaternion.Slerp(_dockRoot.transform.rotation, dockRot, smooth);
            }
        }

        private static Vector3 GetHudSlotOffset(int slot)
        {
            switch (slot)
            {
                case 0: return new Vector3(0.30f, 0.16f, 0.88f);
                case 1: return new Vector3(-0.30f, 0.16f, 0.88f);
                case 2: return new Vector3(0.30f, -0.14f, 0.88f);
                default: return new Vector3(-0.30f, -0.14f, 0.88f);
            }
        }

        private void UpdateCaptures()
        {
            // CaptureFps <= 0 means Native (Uncapped): kick as fast as the background worker finishes
            float interval = CaptureFps > 0.5f ? (1f / Mathf.Clamp(CaptureFps, 5f, 240f)) : 0f;
            float now = Time.unscaledTime;

            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                DesktopWindowPanel p = _panels[i];
                if (!p.Visible || p.Capture == null || p.ScreenImage == null) continue;

                p.Capture.QualityMode = CaptureQuality;

                bool shouldKick = interval <= 0f || now >= p.NextCaptureTime;
                if (shouldKick) p.NextCaptureTime = now + interval;

                IntPtr hwnd = p.Entry != null ? p.Entry.Hwnd : IntPtr.Zero;
                bool alive = p.Capture.PollAndKickCapture(hwnd, shouldKick);
                if (p.Capture.Texture != null && p.ScreenImage.texture != p.Capture.Texture)
                {
                    p.ScreenImage.texture = p.Capture.Texture;
                    p.ScreenImage.color = Color.white;
                }

                if (!alive && hwnd != IntPtr.Zero)
                {
                    ClosePanel(p);
                }
            }
        }

        private void UpdateInteraction()
        {
            if (!TryGetPointerRay(out Vector3 rayOrigin, out Vector3 rayDir, out Vector3 beamVisualStart, out Transform handTransform))
            {
                HidePointer();
                return;
            }

            SampleInputs(out bool triggerDown, out bool gripDown, out bool secondaryDown, out bool pcRightDown, out Vector2 stick, out float pcScrollNotches);

            bool triggerJustPressed = triggerDown && !_wasTriggerDown;
            bool triggerJustReleased = !triggerDown && _wasTriggerDown;
            _wasTriggerDown = triggerDown;

            bool secondaryJustPressed = secondaryDown && !_wasSecondaryDown;
            _wasSecondaryDown = secondaryDown;

            bool pcRightJustPressed = pcRightDown && !_wasPcRightDown;
            _wasPcRightDown = pcRightDown;

            bool fireRightClick = pcRightJustPressed;
            if (secondaryJustPressed)
            {
                float now = Time.unscaledTime;
                if (now - _lastSecondaryPressTime <= DoublePressWindow)
                {
                    fireRightClick = true;
                    _lastSecondaryPressTime = -10f;
                }
                else
                {
                    _lastSecondaryPressTime = now;
                }
            }

            // 1. Continue active Grip or Titlebar/Move-Handle drag
            if (_grabbedTransform != null)
            {
                bool stillHolding = _grabbedWithTrigger ? triggerDown : gripDown;
                if (!stillHolding || handTransform == null)
                {
                    _grabbedTransform = null;
                    _grabbedWithTrigger = false;
                }
                else
                {
                    float smooth = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
                    if (XRSettings.isDeviceActive)
                    {
                        // VR: Joystick forward moves window OUT (away), backward moves it IN (closer)
                        if (Mathf.Abs(stick.y) > 0.15f)
                            _grabLocalPos.z = Mathf.Clamp(_grabLocalPos.z + stick.y * 1.8f * Time.unscaledDeltaTime, 0.35f, 5.0f);

                        Vector3 desiredPos = handTransform.TransformPoint(_grabLocalPos);
                        Quaternion desiredRot = handTransform.rotation * _grabLocalRot;
                        _grabbedTransform.position = Vector3.Lerp(_grabbedTransform.position, desiredPos, smooth);
                        _grabbedTransform.rotation = Quaternion.Slerp(_grabbedTransform.rotation, desiredRot, smooth);
                    }
                    else
                    {
                        // PC: Mouse scroll wheel pushes window OUT (up) or pulls it IN (down) cleanly
                        if (Mathf.Abs(pcScrollNotches) > 0.01f)
                            _pcDragDistance = Mathf.Clamp(_pcDragDistance + pcScrollNotches * 0.18f, 0.40f, 5.0f);

                        Vector3 desiredPos = rayOrigin + rayDir * _pcDragDistance + _pcDragOffset;
                        Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
                        Quaternion desiredRot = cam != null
                            ? Quaternion.LookRotation((desiredPos - cam.transform.position).normalized, Vector3.up)
                            : _grabbedTransform.rotation;
                        _grabbedTransform.position = Vector3.Lerp(_grabbedTransform.position, desiredPos, smooth);
                        _grabbedTransform.rotation = Quaternion.Slerp(_grabbedTransform.rotation, desiredRot, smooth);
                    }

                    ShowLaser(beamVisualStart, _grabbedTransform.position, _grabbedTransform.rotation, false);
                    return;
                }
            }

            // 2. Hit test (Dedicated Chooser UI > Keyboard > Dock > All Open Windows)
            InteractiveRect bestButton = null;
            DesktopWindowPanel bestPanel = null;
            Transform grabbableTarget = null;
            DesktopWindowPanel dragHandlePanel = null;
            Vector3 bestHit = Vector3.zero;
            Quaternion bestRot = Quaternion.identity;
            Vector2 bestUv = Vector2.zero;
            bool bestIsScreen = false;
            float bestDist = float.MaxValue;

            // Dedicated "Add Window" Chooser UI gets top priority when open
            if (_pickerVisible && _pickerRoot != null && _pickerRoot.activeInHierarchy)
            {
                CheckButtonList(_pickerButtons, rayOrigin, rayDir, ref bestDist, ref bestButton, ref bestHit, ref bestRot, ref bestIsScreen);
                if (bestButton == null && TryHitRect(_pickerRect, rayOrigin, rayDir, out Vector3 pHit, out _, out float pDist) && pDist < bestDist)
                {
                    bestDist = pDist;
                    bestHit = pHit;
                    bestRot = _pickerRect.rotation;
                    grabbableTarget = _pickerRoot.transform;
                }
            }

            if (_keyboardVisible && _keyboardRoot != null && _keyboardRoot.activeInHierarchy)
            {
                CheckButtonList(_keyboardButtons, rayOrigin, rayDir, ref bestDist, ref bestButton, ref bestHit, ref bestRot, ref bestIsScreen);
                if (bestButton == null && TryHitRect(_keyboardRect, rayOrigin, rayDir, out Vector3 kbHit, out _, out float kbDist) && kbDist < bestDist)
                {
                    bestDist = kbDist;
                    bestHit = kbHit;
                    bestRot = _keyboardRect.rotation;
                    grabbableTarget = _keyboardRoot.transform;
                }
            }

            if (_dockVisible && _dockRoot != null && _dockRoot.activeInHierarchy)
            {
                CheckButtonList(_dockButtons, rayOrigin, rayDir, ref bestDist, ref bestButton, ref bestHit, ref bestRot, ref bestIsScreen);
                CheckButtonList(_dockWindowTileButtons, rayOrigin, rayDir, ref bestDist, ref bestButton, ref bestHit, ref bestRot, ref bestIsScreen);
                if (bestButton == null && DockStationary && TryHitRect(_dockBarRect, rayOrigin, rayDir, out Vector3 dHit, out _, out float dDist) && dDist < bestDist)
                {
                    bestDist = dDist;
                    bestHit = dHit;
                    bestRot = _dockBarRect.rotation;
                    grabbableTarget = _dockRoot.transform;
                }
            }

            for (int i = 0; i < _panels.Count; i++)
            {
                DesktopWindowPanel p = _panels[i];
                if (!p.Visible || p.Root == null) continue;

                // 1. Check this panel's interactive buttons FIRST (traffic dots + bottom pill buttons).
                // Never allow coplanar HeaderRect / ScreenRect / RootRect of the same panel to overwrite a button hit!
                InteractiveRect panelBtn = null;
                Vector3 panelBtnHit = Vector3.zero;
                Quaternion panelBtnRot = Quaternion.identity;
                float panelBtnDist = float.MaxValue;
                bool dummyScreen = false;
                CheckButtonList(p.Buttons, rayOrigin, rayDir, ref panelBtnDist, ref panelBtn, ref panelBtnHit, ref panelBtnRot, ref dummyScreen);

                if (panelBtn != null && panelBtnDist < bestDist + 0.015f)
                {
                    bestDist = panelBtnDist;
                    bestButton = panelBtn;
                    bestHit = panelBtnHit;
                    bestRot = panelBtnRot;
                    bestIsScreen = false;
                    bestPanel = p;
                    dragHandlePanel = null;
                    grabbableTarget = p.DockedToHud ? null : p.Root.transform;
                    continue;
                }

                if (TryHitRect(p.MoveHandleRect, rayOrigin, rayDir, out Vector3 mHit, out _, out float mDist) && mDist < bestDist)
                {
                    bestDist = mDist;
                    bestHit = mHit;
                    bestRot = p.MoveHandleRect.rotation;
                    bestButton = null;
                    bestIsScreen = false;
                    bestPanel = p;
                    dragHandlePanel = p;
                    grabbableTarget = p.Root.transform;
                }
                else if (TryHitRect(p.HeaderRect, rayOrigin, rayDir, out Vector3 hHit, out _, out float hDist) && hDist < bestDist)
                {
                    bestDist = hDist;
                    bestHit = hHit;
                    bestRot = p.HeaderRect.rotation;
                    bestButton = null;
                    bestIsScreen = false;
                    bestPanel = p;
                    dragHandlePanel = p;
                    grabbableTarget = p.Root.transform;
                }
                else if (TryHitRect(p.ScreenRect, rayOrigin, rayDir, out Vector3 sHit, out Vector2 sUv, out float sDist) && sDist < bestDist)
                {
                    bestDist = sDist;
                    bestHit = sHit;
                    bestRot = p.ScreenRect.rotation;
                    bestUv = sUv;
                    bestButton = null;
                    bestIsScreen = true;
                    bestPanel = p;
                    dragHandlePanel = null;
                    grabbableTarget = p.DockedToHud ? null : p.Root.transform;
                }
                else if (TryHitRect(p.RootRect, rayOrigin, rayDir, out Vector3 fHit, out _, out float fDist) && fDist < bestDist)
                {
                    bestDist = fDist;
                    bestHit = fHit;
                    bestRot = p.RootRect.rotation;
                    bestButton = null;
                    bestIsScreen = false;
                    bestPanel = p;
                    dragHandlePanel = null;
                    grabbableTarget = p.DockedToHud ? null : p.Root.transform;
                }
            }

            UpdateButtonHover(bestButton);

            if (bestDist == float.MaxValue)
            {
                ReleaseHeldMouse();
                HidePointer();
                return;
            }

            // Start drag via Top Titlebar / Move Handle (Left Click / Trigger) OR Grip anywhere on stationary window
            bool startTriggerDrag = triggerJustPressed && dragHandlePanel != null && bestButton == null;
            bool startGripDrag = gripDown && grabbableTarget != null;

            if ((startTriggerDrag || startGripDrag) && handTransform != null)
            {
                ReleaseHeldMouse();
                if (dragHandlePanel != null && dragHandlePanel.DockedToHud)
                {
                    // Dragging a docked window automatically undocks it to Stationary world space
                    TogglePanelDock(dragHandlePanel);
                }

                _grabbedTransform = dragHandlePanel != null ? dragHandlePanel.Root.transform : grabbableTarget;
                _grabbedWithTrigger = startTriggerDrag;
                _grabLocalPos = handTransform.InverseTransformPoint(_grabbedTransform.position);
                _grabLocalRot = Quaternion.Inverse(handTransform.rotation) * _grabbedTransform.rotation;
                _pcDragDistance = Vector3.Distance(rayOrigin, bestHit);
                _pcDragOffset = _grabbedTransform.position - bestHit;
                ShowLaser(beamVisualStart, bestHit, bestRot, false);
                return;
            }

            ShowLaser(beamVisualStart, bestHit, bestRot, true);

            // Allow pushing a Stationary window OUT / pulling it IN directly while pointing at it:
            // - PC: Mouse scroll wheel on Titlebar / Move Handle / Frame (or anywhere on a stationary window)
            // - VR: Joystick forward (out) / backward (in) while pointing at Titlebar / Move Handle / Frame
            if (bestPanel != null && !bestPanel.DockedToHud && bestPanel.Root != null)
            {
                bool onChromeOrFrame = !bestIsScreen || dragHandlePanel != null;
                float depthDelta = 0f;
                if (XRSettings.isDeviceActive && onChromeOrFrame && Mathf.Abs(stick.y) > 0.18f)
                {
                    depthDelta = stick.y * 1.8f * Time.unscaledDeltaTime;
                }
                else if (!XRSettings.isDeviceActive && Mathf.Abs(pcScrollNotches) > 0.01f)
                {
                    depthDelta = pcScrollNotches * 0.18f;
                }

                if (Mathf.Abs(depthDelta) > 0.0001f)
                {
                    Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
                    Vector3 anchor = XRSettings.isDeviceActive ? rayOrigin : (cam != null ? cam.transform.position : rayOrigin);
                    Vector3 toPanel = bestPanel.Root.transform.position - anchor;
                    float curDist = toPanel.magnitude;
                    Vector3 dir = curDist > 0.01f ? (toPanel / curDist) : rayDir;
                    float newDist = Mathf.Clamp(curDist + depthDelta, 0.40f, 5.0f);
                    bestPanel.Root.transform.position = anchor + dir * newDist;
                }
            }

            // 3. Button click
            if (bestButton != null)
            {
                ReleaseHeldMouse();
                if (triggerJustPressed)
                {
                    PulseCursor(_colAccent);
                    try { bestButton.OnClick?.Invoke(); }
                    catch (Exception ex) { Plugin.Log?.LogWarning("[WindowsOverlay] Click failed: " + ex.Message); }
                }
                return;
            }

            // 4. Desktop screen interaction
            if (bestIsScreen && bestPanel != null)
            {
                _focusedPanel = bestPanel;
                IntPtr hwnd = bestPanel.Entry != null ? bestPanel.Entry.Hwnd : IntPtr.Zero;
                bool isVr = XRSettings.isDeviceActive;

                if (isVr)
                {
                    // VR: controller is independent of OS mouse — direct SendInput works cleanly
                    Win32DesktopBridge.MoveCursorToUV(hwnd, bestUv.x, bestUv.y);

                    if (triggerJustPressed)
                    {
                        if (hwnd != IntPtr.Zero) Win32DesktopBridge.FocusWindow(hwnd);
                        Win32DesktopBridge.SendMouseButton(false, true);
                        _leftMouseHeldOnWindow = true;
                        PulseCursor(_colAccent);
                    }
                    else if (triggerJustReleased && _leftMouseHeldOnWindow)
                    {
                        Win32DesktopBridge.SendMouseButton(false, false);
                        _leftMouseHeldOnWindow = false;
                    }

                    if (fireRightClick)
                    {
                        if (hwnd != IntPtr.Zero) Win32DesktopBridge.FocusWindow(hwnd);
                        Win32DesktopBridge.SendRightClick();
                        PulseCursor(new Color(1f, 0.74f, 0.22f, 1f));
                    }

                    if (Mathf.Abs(stick.y) > 0.2f)
                    {
                        _scrollAccum += stick.y * 540f * Time.unscaledDeltaTime;
                        int wheel = Mathf.RoundToInt(_scrollAccum);
                        if (Mathf.Abs(wheel) >= 40)
                        {
                            Win32DesktopBridge.SendMouseWheel(wheel);
                            _scrollAccum -= wheel;
                        }
                    }
                    else
                    {
                        _scrollAccum = 0f;
                    }
                }
                else
                {
                    if (hwnd != IntPtr.Zero)
                    {
                        Win32DesktopBridge.PostWindowMouseMove(hwnd, bestUv.x, bestUv.y);
                    }

                    if (!PcRealOsClickOnRelease)
                    {
                        // Mode 0: Safe Direct Message (never moves PC mouse, never steals Gorilla Tag focus)
                        if (triggerJustPressed)
                        {
                            int cx = 0, cy = 0;
                            bool ok = hwnd != IntPtr.Zero && Win32DesktopBridge.PostWindowMouseButton(hwnd, bestUv.x, bestUv.y, false, true, out cx, out cy);
                            PulseCursor(_colAccent);
                            ShowClickTestFeedback(bestHit, bestRot, "LMB DOWN", cx, cy, bestUv, ok);
                        }
                        else if (triggerJustReleased && hwnd != IntPtr.Zero)
                        {
                            Win32DesktopBridge.PostWindowMouseButton(hwnd, bestUv.x, bestUv.y, false, false, out _, out _);
                        }

                        if (fireRightClick && hwnd != IntPtr.Zero)
                        {
                            Win32DesktopBridge.PostWindowMouseButton(hwnd, bestUv.x, bestUv.y, true, true, out int cx, out int cy);
                            Win32DesktopBridge.PostWindowMouseButton(hwnd, bestUv.x, bestUv.y, true, false, out _, out _);
                            PulseCursor(new Color(1f, 0.74f, 0.22f, 1f));
                            ShowClickTestFeedback(bestHit, bestRot, "RMB", cx, cy, bestUv, true);
                        }
                    }
                    else
                    {
                        // Mode 1: Reliable async OS click on mouse release (works in Chrome, Discord, Spotify, etc.
                        // even when behind Gorilla Tag, and immediately restores Gorilla Tag + PC cursor!)
                        if (triggerJustReleased && Time.unscaledTime >= _ignoreInputUntil)
                        {
                            _ignoreInputUntil = Time.unscaledTime + 0.25f;
                            bool ok = Win32DesktopBridge.PerformAsyncWindowClick(hwnd, bestUv.x, bestUv.y, false);
                            PulseCursor(_colAccent);
                            ShowClickTestFeedback(bestHit, bestRot, "OS LMB", Mathf.RoundToInt(bestUv.x * 100f), Mathf.RoundToInt(bestUv.y * 100f), bestUv, ok);
                        }

                        if (fireRightClick && Time.unscaledTime >= _ignoreInputUntil)
                        {
                            _ignoreInputUntil = Time.unscaledTime + 0.25f;
                            bool ok = Win32DesktopBridge.PerformAsyncWindowClick(hwnd, bestUv.x, bestUv.y, true);
                            PulseCursor(new Color(1f, 0.74f, 0.22f, 1f));
                            ShowClickTestFeedback(bestHit, bestRot, "OS RMB", Mathf.RoundToInt(bestUv.x * 100f), Mathf.RoundToInt(bestUv.y * 100f), bestUv, ok);
                        }
                    }

                    if (Mathf.Abs(stick.y) > 0.1f && hwnd != IntPtr.Zero)
                    {
                        _scrollAccum += stick.y * 540f * Time.unscaledDeltaTime;
                        int wheel = Mathf.RoundToInt(_scrollAccum);
                        if (Mathf.Abs(wheel) >= 40)
                        {
                            Win32DesktopBridge.PostWindowMouseWheel(hwnd, bestUv.x, bestUv.y, wheel);
                            _scrollAccum -= wheel;
                        }
                    }
                    else
                    {
                        _scrollAccum = 0f;
                    }
                }
            }
            else
            {
                ReleaseHeldMouse();
            }
        }

        private void ReleaseHeldMouse()
        {
            if (_leftMouseHeldOnWindow)
            {
                Win32DesktopBridge.SendMouseButton(false, false);
                _leftMouseHeldOnWindow = false;
            }
        }

        private void UpdateButtonHover(InteractiveRect next)
        {
            if (_hoveredButton == next) return;
            if (_hoveredButton != null && _hoveredButton.Background != null)
                _hoveredButton.Background.color = _hoveredButton.NormalColor;

            _hoveredButton = next;
            if (_hoveredButton != null && _hoveredButton.Background != null)
                _hoveredButton.Background.color = _hoveredButton.HoverColor;

            if (_dockTooltipText != null)
            {
                _dockTooltipText.text = (_hoveredButton != null && !string.IsNullOrEmpty(_hoveredButton.Tooltip))
                    ? _hoveredButton.Tooltip
                    : (_focusedPanel != null && _focusedPanel.Entry != null ? _focusedPanel.Entry.DisplayLabel : "Cascade Windows");
            }
        }

        private void ShowLaser(Vector3 origin, Vector3 hitPoint, Quaternion surfaceRot, bool showCursor)
        {
            if (_laser != null)
            {
                _laser.enabled = true;
                _laser.SetPosition(0, origin);
                _laser.SetPosition(1, hitPoint);
            }

            if (_cursorObj != null)
            {
                _cursorObj.SetActive(showCursor);
                if (showCursor)
                {
                    _cursorObj.transform.position = hitPoint - (surfaceRot * Vector3.forward) * 0.0025f;
                    _cursorObj.transform.rotation = surfaceRot;
                }
            }
        }

        private void HidePointer()
        {
            if (_laser != null) _laser.enabled = false;
            if (_cursorObj != null) _cursorObj.SetActive(false);
            UpdateButtonHover(null);
        }

        private void ShowClickTestFeedback(Vector3 hitPoint, Quaternion surfaceRot, string kind, int px, int py, Vector2 uv, bool delivered)
        {
            _clickTestCount++;

            if (_clickMarkerObj != null)
            {
                _clickMarkerObj.SetActive(true);
                _clickMarkerObj.transform.position = hitPoint - (surfaceRot * Vector3.forward) * 0.002f;
                _clickMarkerObj.transform.rotation = surfaceRot;
                _clickMarkerTimer = 0.45f;
            }

            if (ClickTestEnabled && _dockTooltipText != null)
            {
                _dockTooltipText.text = $"Click #{_clickTestCount} [{kind}] UV({uv.x:0.00},{uv.y:0.00}) -> ({px},{py}) {(delivered ? "OK" : "NO HWND")}";
            }
        }

        private void PulseCursor(Color color)
        {
            _cursorPulseColor = color;
            _cursorPulseTimer = 0.18f;
        }

        private void UpdateCursorPulse()
        {
            if (_cursorArrow != null)
            {
                if (_cursorPulseTimer > 0f)
                {
                    _cursorPulseTimer = Mathf.Max(0f, _cursorPulseTimer - Time.unscaledDeltaTime);
                    _cursorArrow.color = Color.Lerp(Color.white, _cursorPulseColor, _cursorPulseTimer / 0.18f);
                }
                else
                {
                    _cursorArrow.color = Color.white;
                }
            }

            if (_clickMarkerObj != null && _clickMarkerObj.activeSelf)
            {
                _clickMarkerTimer -= Time.unscaledDeltaTime;
                if (_clickMarkerTimer <= 0f)
                {
                    _clickMarkerObj.SetActive(false);
                }
                else if (_clickMarkerImg != null)
                {
                    float a = Mathf.Clamp01(_clickMarkerTimer / 0.45f);
                    _clickMarkerImg.color = _colAccent.WithAlpha(a);
                }
            }
        }

        // -----------------------------------------------------------------
        // Ray & Input Sampling (VR + PC with reliable Win32 mouse fallback)
        // -----------------------------------------------------------------
        private bool TryGetPointerRay(out Vector3 origin, out Vector3 direction, out Vector3 beamVisualStart, out Transform handTransform)
        {
            handTransform = null;
            origin = Vector3.zero;
            direction = Vector3.forward;
            beamVisualStart = Vector3.zero;

            GorillaTagger tagger = GorillaTagger.Instance;
            if (XRSettings.isDeviceActive && tagger != null)
            {
                handTransform = UseLeftHand ? tagger.leftHandTransform : tagger.rightHandTransform;
                if (handTransform != null)
                {
                    origin = handTransform.position + handTransform.forward * 0.04f;
                    direction = handTransform.forward;
                    beamVisualStart = origin;
                    return true;
                }
            }

            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            if (cam == null) return false;
            handTransform = cam.transform;

            Vector2 screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (Mouse.current != null && Cursor.lockState != CursorLockMode.Locked)
            {
                screenPos = Mouse.current.position.ReadValue();
            }

            Ray r = cam.ScreenPointToRay(screenPos);
            origin = r.origin;
            direction = r.direction;

            // Visual laser starts cleanly from lower-right corner of view on PC
            beamVisualStart = cam.transform.position
                + cam.transform.right * 0.18f
                - cam.transform.up * 0.16f
                + cam.transform.forward * 0.25f;
            return true;
        }

        private void SampleInputs(out bool trigger, out bool grip, out bool secondary, out bool pcRight, out Vector2 stick, out float pcScrollNotches)
        {
            trigger = false;
            grip = false;
            secondary = false;
            pcRight = false;
            stick = Vector2.zero;
            pcScrollNotches = 0f;

            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (XRSettings.isDeviceActive && poller != null)
            {
                if (UseLeftHand)
                {
                    trigger = poller.leftControllerIndexFloat > 0.6f;
                    grip = poller.leftControllerGripFloat > 0.6f;
                    secondary = poller.leftControllerSecondaryButton;
                    stick = poller.leftControllerPrimary2DAxis;
                }
                else
                {
                    trigger = poller.rightControllerIndexFloat > 0.6f;
                    grip = poller.rightControllerGripFloat > 0.6f;
                    secondary = poller.rightControllerSecondaryButton;
                    stick = poller.rightControllerPrimary2DAxis;
                }
                return;
            }

            // PC: combine Unity Input System + Win32 GetAsyncKeyState so clicks never drop
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;

            bool rawLeft = (Win32DesktopBridge.GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool rawRight = (Win32DesktopBridge.GetAsyncKeyState(0x02) & 0x8000) != 0;
            bool rawMid = (Win32DesktopBridge.GetAsyncKeyState(0x04) & 0x8000) != 0;
            bool rawB = (Win32DesktopBridge.GetAsyncKeyState(0x42) & 0x8000) != 0;

            trigger = rawLeft || (mouse != null && mouse.leftButton.isPressed);
            pcRight = rawRight || (mouse != null && mouse.rightButton.isPressed);
            grip = rawMid || (mouse != null && mouse.middleButton.isPressed);
            secondary = rawB || (kb != null && kb.bKey.isPressed);

            if (mouse != null)
            {
                float rawScroll = mouse.scroll.ReadValue().y;
                // Unity Input System reports ~120 per standard mouse wheel notch (or small floats on trackpads)
                if (Mathf.Abs(rawScroll) > 0.01f)
                {
                    pcScrollNotches = Mathf.Abs(rawScroll) >= 20f ? (rawScroll / 120f) : rawScroll;
                }
                stick = new Vector2(0f, Mathf.Clamp(pcScrollNotches, -2f, 2f));
            }
        }

        private static void CheckButtonList(List<InteractiveRect> list, Vector3 origin, Vector3 dir,
            ref float bestDist, ref InteractiveRect bestBtn, ref Vector3 bestHit, ref Quaternion bestRot,
            ref bool bestIsScreen)
        {
            for (int i = 0; i < list.Count; i++)
            {
                InteractiveRect item = list[i];
                if (item.Rect == null || !item.Rect.gameObject.activeInHierarchy) continue;
                if (TryHitRect(item.Rect, origin, dir, out Vector3 hit, out _, out float dist) && dist < bestDist)
                {
                    bestDist = dist;
                    bestBtn = item;
                    bestHit = hit;
                    bestRot = item.Rect.rotation;
                    bestIsScreen = false;
                }
            }
        }

        private static bool TryHitRect(RectTransform rt, Vector3 origin, Vector3 dir,
            out Vector3 worldHit, out Vector2 uv, out float distance)
        {
            worldHit = Vector3.zero;
            uv = Vector2.zero;
            distance = float.MaxValue;
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;

            Plane plane = new Plane(rt.forward, rt.position);
            Ray ray = new Ray(origin, dir);
            float enter;
            if (!plane.Raycast(ray, out enter))
            {
                // Also support hitting from the opposite facing normal if tilted
                plane = new Plane(-rt.forward, rt.position);
                if (!plane.Raycast(ray, out enter)) return false;
            }
            if (enter < 0.02f || enter > LaserMaxDistance) return false;

            worldHit = ray.GetPoint(enter);
            Vector3 local = rt.InverseTransformPoint(worldHit);
            Rect rect = rt.rect;
            if (!rect.Contains(new Vector2(local.x, local.y))) return false;

            distance = enter;
            uv = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
            return true;
        }
    }
}
