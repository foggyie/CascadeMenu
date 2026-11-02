using System;
using CascadeMenu.Mods;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    /// <summary>
    /// The spectate window: a live feed of the selected player with a titlebar,
    /// a first/third person switch and an X in the top-right corner to stop.
    ///
    /// It is a sibling of the Cascade window on the menu canvas, so it inherits
    /// the PC screen-space and VR world-space layouts for free. While it is open
    /// the main window is hidden, and the menu pointer keeps working because the
    /// buttons are registered as <see cref="CascadeLayerGroup.Overlay"/> targets.
    /// </summary>
    internal sealed class CascadeSpectateOverlay : MonoBehaviour
    {
        private const float WindowWidth = 1200f;
        private const float WindowHeight = 700f;
        private const float TitleHeight = 48f;
        private const float ModeButtonWidth = 106f;
        private const float ModeButtonHeight = 28f;

        private CascadeTheme _theme;
        private SpectateController _controller;
        private CascadeWindow _window;
        private Action _stop;

        private RawImage _feed;
        private Image _closeHover;
        private Image _firstBackground;
        private Image _thirdBackground;
        private TMP_Text _firstLabel;
        private TMP_Text _thirdLabel;

        private RectTransform _closeRect;
        private RectTransform _firstRect;
        private RectTransform _thirdRect;

        private CascadeInteractable _closeInteractable;
        private CascadeInteractable _firstInteractable;
        private CascadeInteractable _thirdInteractable;

        private float _appear;
        private bool _disposed;

        /// <summary>Builds the window under the menu canvas and returns it.</summary>
        internal static CascadeSpectateOverlay Create(
            RectTransform parent,
            CascadeTheme theme,
            SpectateController controller,
            string playerName,
            Action stop)
        {
            var go = new GameObject("CascadeSpectateOverlay", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(WindowWidth, WindowHeight);
            rect.localScale = Vector3.one;

            var overlay = go.AddComponent<CascadeSpectateOverlay>();
            overlay.Build(theme, controller, playerName, stop);
            return overlay;
        }

        private void Build(
            CascadeTheme theme,
            SpectateController controller,
            string playerName,
            Action stop)
        {
            _theme = theme;
            _controller = controller;
            _stop = stop;

            RectTransform root = (RectTransform)transform;

            // --- Frame ------------------------------------------------------
            RectTransform shadow = CascadeUI.Rect("Shadow", root);
            CascadeUI.Stretch(shadow, -30f, -30f, -26f, -30f);
            CascadeUI.Image(shadow, CascadeSprites.Shadow(22, 28), _theme.ShadowColor, Image.Type.Sliced);

            RectTransform body = CascadeUI.Rect("Body", root);
            CascadeUI.Stretch(body);
            CascadeUI.Panel(body, _theme.SidebarBg, 12);

            RectTransform border = CascadeUI.Rect("Border", root);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, _theme.WindowBorder, 12);

            BuildTitlebar(root, playerName);
            BuildFeed(root);

            // The window is only ever shown while the menu pointer is active,
            // but keep the start-up pop readable.
            _appear = 0f;
            root.localScale = new Vector3(0.97f, 0.97f, 1f);
        }

        private void BuildTitlebar(RectTransform root, string playerName)
        {
            RectTransform titlebar = CascadeUI.Rect("Titlebar", root);
            titlebar.anchorMin = new Vector2(0f, 1f);
            titlebar.anchorMax = new Vector2(1f, 1f);
            titlebar.pivot = new Vector2(0.5f, 1f);
            titlebar.anchoredPosition = new Vector2(0f, -1f);
            titlebar.sizeDelta = new Vector2(-2f, TitleHeight);
            CascadeUI.Image(titlebar, CascadeSprites.RoundedTop(11), _theme.TitlebarBg, Image.Type.Sliced);

            RectTransform hairline = CascadeUI.Rect("Hairline", root);
            hairline.anchorMin = new Vector2(0f, 1f);
            hairline.anchorMax = new Vector2(1f, 1f);
            hairline.pivot = new Vector2(0.5f, 1f);
            hairline.anchoredPosition = new Vector2(0f, -TitleHeight);
            hairline.sizeDelta = new Vector2(-2f, 1f);
            CascadeUI.Solid(hairline, _theme.TitlebarHairline);

            RectTransform titleRect = TopLeft("Title", root, 20f, 9f, 700f, 22f);
            TMP_Text title = CascadeUI.Text(
                titleRect, "Spectating " + (playerName ?? "player"), 16f,
                _theme.TextPrimary, TextAlignmentOptions.Left, true);
            title.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform subtitleRect = TopLeft("Subtitle", root, 21f, 29f, 700f, 14f);
            CascadeUI.Text(subtitleRect, "Live rig feed", 9.5f, _theme.TextSecondary, TextAlignmentOptions.Left);

            // --- First / third person switch --------------------------------
            RectTransform modeRect = TopRight("Mode", root, 52f, 10f,
                ModeButtonWidth * 2f + 8f, ModeButtonHeight);

            _firstRect = LeftIn("First", modeRect, 0f, ModeButtonWidth);
            _firstBackground = CascadeUI.Panel(_firstRect, _theme.FieldBg, 7);
            RectTransform firstText = CascadeUI.Rect("Text", _firstRect);
            CascadeUI.Stretch(firstText, 2f, 2f, 0f, 0f);
            _firstLabel = CascadeUI.Text(firstText, "1st person", 9.5f,
                _theme.TextSecondary, TextAlignmentOptions.Center, true);

            _thirdRect = LeftIn("Third", modeRect, ModeButtonWidth + 8f, ModeButtonWidth);
            _thirdBackground = CascadeUI.Panel(_thirdRect, _theme.FieldBg, 7);
            RectTransform thirdText = CascadeUI.Rect("Text", _thirdRect);
            CascadeUI.Stretch(thirdText, 2f, 2f, 0f, 0f);
            _thirdLabel = CascadeUI.Text(thirdText, "3rd person", 9.5f,
                _theme.TextSecondary, TextAlignmentOptions.Center, true);

            // --- X in the top-right corner ----------------------------------
            _closeRect = TopRight("Close", root, 12f, 10f, 30f, 28f);
            CascadeUI.Panel(_closeRect, _theme.RowHover, 8);

            RectTransform hoverRect = CascadeUI.Rect("Hover", _closeRect);
            CascadeUI.Stretch(hoverRect);
            _closeHover = CascadeUI.Panel(hoverRect, _theme.TrafficClose, 8);
            _closeHover.gameObject.SetActive(false);

            RectTransform closeGlyph = CascadeUI.Rect("Glyph", _closeRect);
            CascadeUI.Stretch(closeGlyph);
            CascadeUI.Text(closeGlyph, "X", 13f, _theme.TextPrimary, TextAlignmentOptions.Center, true);
        }

        private void BuildFeed(RectTransform root)
        {
            RectTransform frame = CascadeUI.Rect("Feed", root);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.offsetMin = new Vector2(10f, 10f);
            frame.offsetMax = new Vector2(-10f, -(TitleHeight + 9f));
            CascadeUI.Panel(frame, _theme.ViewBg, 9);

            RectTransform viewRect = CascadeUI.Rect("View", frame);
            CascadeUI.Stretch(viewRect, 3f, 3f, 3f, 3f);
            _feed = viewRect.gameObject.AddComponent<RawImage>();
            _feed.raycastTarget = false;
            _feed.color = Color.white;
            _feed.texture = _controller != null ? _controller.Feed : null;

            // --- LIVE badge -------------------------------------------------
            RectTransform badge = TopLeft("LiveBadge", frame, 14f, 12f, 58f, 21f);
            CascadeUI.Panel(badge, new Color(0f, 0f, 0f, 0.56f), 10);

            RectTransform dot = TopLeft("Dot", badge, 9f, 7f, 7f, 7f);
            CascadeUI.Image(dot, CascadeSprites.Circle(), _theme.TrafficClose, Image.Type.Simple);

            RectTransform liveText = TopLeft("Text", badge, 21f, 3.5f, 34f, 14f);
            CascadeUI.Text(liveText, "LIVE", 8.5f, Color.white, TextAlignmentOptions.Left, true);

            // --- Footer hint ------------------------------------------------
            RectTransform hint = CascadeUI.Rect("Hint", frame);
            hint.anchorMin = hint.anchorMax = new Vector2(0f, 0f);
            hint.pivot = new Vector2(0f, 0f);
            hint.anchoredPosition = new Vector2(16f, 12f);
            hint.sizeDelta = new Vector2(560f, 16f);
            CascadeUI.Text(hint, "X or Esc to stop spectating", 9f, _theme.TextSecondary,
                TextAlignmentOptions.Left);
        }

        /// <summary>Registers the window buttons with the current menu window.</summary>
        internal void RegisterWith(CascadeWindow window)
        {
            if (_disposed) return;

            Unregister();
            _window = window;
            if (_window == null) return;

            _closeInteractable = _window.Register(
                _closeRect,
                CascadeLayerGroup.Overlay,
                Stop,
                hover: hovered => _closeHover.gameObject.SetActive(hovered));

            _firstInteractable = _window.Register(
                _firstRect,
                CascadeLayerGroup.Overlay,
                () => _controller.SetFirstPerson(true));

            _thirdInteractable = _window.Register(
                _thirdRect,
                CascadeLayerGroup.Overlay,
                () => _controller.SetFirstPerson(false));

            ApplyModeVisuals();
        }

        private void Stop()
        {
            if (_disposed) return;
            if (_stop != null) _stop();
        }

        private void Update()
        {
            if (_disposed) return;

            if (_appear < 1f)
            {
                _appear = Mathf.MoveTowards(_appear, 1f, Time.unscaledDeltaTime * 6f);
                float scale = Mathf.Lerp(0.97f, 1f, _appear * _appear * (3f - 2f * _appear));
                transform.localScale = new Vector3(scale, scale, 1f);
            }

            if (_controller == null) return;

            if (_feed != null && _feed.texture == null)
            {
                _feed.texture = _controller.Feed;
            }

            ApplyModeVisuals();
        }

        private void ApplyModeVisuals()
        {
            if (_controller == null || _theme == null) return;

            bool first = _controller.FirstPerson;

            if (_firstBackground != null)
                _firstBackground.color = first ? _theme.Accent : _theme.FieldBg;
            if (_thirdBackground != null)
                _thirdBackground.color = first ? _theme.FieldBg : _theme.Accent;

            if (_firstLabel != null)
                _firstLabel.color = first ? _theme.SelectionText : _theme.TextSecondary;
            if (_thirdLabel != null)
                _thirdLabel.color = first ? _theme.TextSecondary : _theme.SelectionText;
        }

        /// <summary>Detaches the buttons and stops any hover/press state.</summary>
        internal void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unregister();
        }

        private void Unregister()
        {
            if (_window != null)
            {
                _window.Unregister(_closeInteractable);
                _window.Unregister(_firstInteractable);
                _window.Unregister(_thirdInteractable);
            }

            _closeInteractable = null;
            _firstInteractable = null;
            _thirdInteractable = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        // -----------------------------------------------------------------
        // Layout helpers
        // -----------------------------------------------------------------

        private static RectTransform TopLeft(
            string name, RectTransform parent, float left, float top, float width, float height)
        {
            RectTransform rect = CascadeUI.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform TopRight(
            string name, RectTransform parent, float right, float top, float width, float height)
        {
            RectTransform rect = CascadeUI.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform LeftIn(string name, RectTransform parent, float left, float width)
        {
            RectTransform rect = CascadeUI.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, ModeButtonHeight);
            return rect;
        }

    }
}
