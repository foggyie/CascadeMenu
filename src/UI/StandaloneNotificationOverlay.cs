using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu
{
    /// <summary>Small Cascade-style toast canvas that works while the menu is closed.</summary>
    internal sealed class StandaloneNotificationOverlay : MonoBehaviour
    {
        private static StandaloneNotificationOverlay _instance;
        private readonly List<StandaloneNotificationCard> _cards = new List<StandaloneNotificationCard>();
        private RectTransform _canvasRect;
        private MenuConfig _config;
        private CascadeTheme _theme;
        private int _themeIndex;
        private int _accentIndex;

        internal void Initialize(MenuConfig config)
        {
            _instance = this;
            _config = config;
            _themeIndex = config.ThemeIndex.Value;
            _accentIndex = config.AccentIndex.Value;
            _theme = CascadeTheme.Build(config.ThemeMode, _accentIndex);

            var canvasObject = new GameObject("CascadeNotifications", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(canvasObject);
            _canvasRect = (RectTransform)canvasObject.transform;
            _canvasRect.sizeDelta = new Vector2(520f, 330f);
            _canvasRect.localScale = Vector3.one * 0.00072f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 160;
        }

        internal static void Notify(string title, string message)
        {
            if (_instance != null) _instance.Show(title, message);
        }

        private void LateUpdate()
        {
            if (_canvasRect == null) return;
            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            if (camera == null) return;
            Canvas canvas = _canvasRect.GetComponent<Canvas>();
            if (canvas != null) canvas.worldCamera = camera;

            _canvasRect.position = camera.transform.position + camera.transform.forward * 0.78f +
                                   camera.transform.right * 0.23f + camera.transform.up * 0.19f;
            _canvasRect.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);

            // Keep theme changes from requiring a menu open/rebuild.
            if (_config != null && (_themeIndex != _config.ThemeIndex.Value || _accentIndex != _config.AccentIndex.Value))
            {
                _themeIndex = _config.ThemeIndex.Value;
                _accentIndex = _config.AccentIndex.Value;
                _theme = CascadeTheme.Build(_config.ThemeMode, _config.AccentIndex.Value);
            }
        }

        private void Show(string title, string message)
        {
            if (_canvasRect == null) return;
            if (_cards.Count >= 4) _cards[0].Dismiss();

            RectTransform card = CascadeUI.Rect("Notification", _canvasRect);
            card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(1f, 1f);
            card.sizeDelta = new Vector2(440f, 64f);

            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Image background = CascadeUI.Panel(card, _theme.ToastBg, 10);
            background.raycastTarget = false;

            RectTransform border = CascadeUI.Rect("Border", card);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, _theme.ToastBorder, 10);

            RectTransform iconRect = CascadeUI.TopLeft("Icon", card, 13f, 18f, 26f, 26f);
            CascadeUI.Image(iconRect, CascadeSprites.Circle(), Color.white, Image.Type.Simple);
            CascadeGradient gradient = iconRect.gameObject.AddComponent<CascadeGradient>();
            gradient.Top = _theme.ButtonPrimaryTop;
            gradient.Bottom = _theme.ButtonPrimaryBottom;
            RectTransform glyph = CascadeUI.Center("Glyph", iconRect, 0f, 0f, 13f, 13f);
            CascadeUI.Image(glyph, CascadeIcons.Get(CascadeIcon.Check), Color.white, Image.Type.Simple);

            RectTransform titleRect = LineRect("Title", card, 50f, 28f, 8f, 21f);
            CascadeUI.Text(titleRect, title, 12f, _theme.TextPrimary, TextAlignmentOptions.Left, true);
            RectTransform messageRect = LineRect("Message", card, 50f, 28f, 28f, 20f);
            CascadeUI.Text(messageRect, CascadeUI.Truncate(message, 52), 10.5f, _theme.TextSecondary, TextAlignmentOptions.Left);

            RectTransform close = CascadeUI.TopRight("Close", card, 12f, 21f, 9f, 9f);
            CascadeUI.Image(close, CascadeIcons.Get(CascadeIcon.Close), _theme.TextTertiary, Image.Type.Simple);

            StandaloneNotificationCard view = card.gameObject.AddComponent<StandaloneNotificationCard>();
            view.Initialize(this, group);
            _cards.Add(view);
            RefreshPositions();
        }

        private static RectTransform LineRect(string name, Transform parent, float left, float right, float top, float height)
        {
            RectTransform rect = CascadeUI.Rect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        internal void Remove(StandaloneNotificationCard card)
        {
            _cards.Remove(card);
            RefreshPositions();
        }

        private void RefreshPositions()
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] != null) _cards[i].SetPosition(new Vector2(-10f, -10f - i * 72f));
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }

    internal sealed class StandaloneNotificationCard : MonoBehaviour
    {
        private StandaloneNotificationOverlay _owner;
        private CanvasGroup _group;
        private RectTransform _rect;
        private float _age;
        private bool _closing;

        internal void Initialize(StandaloneNotificationOverlay owner, CanvasGroup group)
        {
            _owner = owner;
            _group = group;
            _rect = (RectTransform)transform;
        }

        internal void SetPosition(Vector2 position)
        {
            if (_rect != null) _rect.anchoredPosition = position;
        }

        internal void Dismiss()
        {
            _closing = true;
        }

        private void Update()
        {
            if (_group == null) return;
            _age += Time.unscaledDeltaTime;
            if (!_closing && _age > 4.2f) _closing = true;

            if (!_closing)
            {
                float t = Mathf.Clamp01(_age / 0.16f);
                _group.alpha = t;
                transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, t);
                return;
            }

            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime * 3.4f);
            if (_group.alpha > 0f) return;
            _owner?.Remove(this);
            Destroy(gameObject);
        }
    }
}
