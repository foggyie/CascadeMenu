using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Small on-screen keyboard that appears under the titlebar search field while
    /// it is focused in VR.
    ///
    /// It is built for both desktop and VR so a headset can be connected later,
    /// but <see cref="CascadeWindow"/> only makes it visible when
    /// <see cref="UnityEngine.XR.XRSettings.isDeviceActive"/> is true. On PC the
    /// keys stay hidden and typing goes through the physical keyboard only.
    /// </summary>
    internal sealed class CascadeKeyboard
    {
        private const float PanelWidth = 440f;
        private const float PanelHeight = 196f;
        private const float KeyWidth = 38f;
        private const float KeyHeight = 26f;
        private const float KeyGap = 4f;

        private static readonly string[] LetterRows =
        {
            "1234567890",
            "qwertyuiop",
            "asdfghjkl",
            "zxcvbnm"
        };

        private readonly List<CascadeInteractable> _keys = new List<CascadeInteractable>(40);

        private RectTransform _root;
        private CascadeWindow _window;
        private CascadeTheme _theme;
        private bool _visible;

        internal void Build(CascadeWindow window, RectTransform parent)
        {
            _window = window;
            _theme = window.Theme;

            _root = CascadeUI.Rect("VRKeyboard", parent);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.anchoredPosition = new Vector2(0f, 14f);
            _root.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            RectTransform background = CascadeUI.Rect("Background", _root);
            CascadeUI.Stretch(background);
            CascadeUI.Panel(background, _theme.ToastBg, 12);
            RectTransform border = CascadeUI.Rect("Border", _root);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, _theme.ToastBorder, 12);

            // Swallow clicks in the gaps so the page behind does not react.
            window.Register(background, CascadeLayerGroup.Chrome, () => { });

            RectTransform title = CascadeUI.TopLeft("Title", _root, 14f, 8f, 250f, 17f);
            CascadeUI.Text(title, "VR keyboard", 10.5f, _theme.TextPrimary, TMPro.TextAlignmentOptions.Left, true);

            RectTransform hint = CascadeUI.TopRight("Hint", _root, 14f, 8f, 250f, 17f);
            CascadeUI.Text(hint, "PC: physical keyboard only", 9.5f, _theme.TextTertiary, TMPro.TextAlignmentOptions.Right);

            for (int row = 0; row < LetterRows.Length; row++)
            {
                string keys = LetterRows[row];
                float rowWidth = keys.Length * KeyWidth + Mathf.Max(0, keys.Length - 1) * KeyGap;
                float x = (PanelWidth - rowWidth) * 0.5f;
                float y = 32f + row * 29f;

                for (int i = 0; i < keys.Length; i++)
                {
                    char key = keys[i];
                    CreateKey(x, y, KeyWidth, KeyHeight, key.ToString(), false,
                        () => _window.HandleTextInput(key));
                    x += KeyWidth + KeyGap;
                }
            }

            float actionY = 152f;
            float actionWidth = 62f + 180f + 54f + 64f + 3f * 5f;
            float actionX = (PanelWidth - actionWidth) * 0.5f;

            CreateKey(actionX, actionY, 62f, KeyHeight, "Clear", true, () => _window.ClearSearch());
            actionX += 67f;
            CreateKey(actionX, actionY, 180f, KeyHeight, "Space", false, () => _window.HandleTextInput(' '));
            actionX += 185f;
            CreateKey(actionX, actionY, 54f, KeyHeight, "Del", true, () => _window.HandleBackspace());
            actionX += 59f;
            CreateKey(actionX, actionY, 64f, KeyHeight, "Done", true, () => _window.BlurSearch());

            _root.gameObject.SetActive(false);
        }

        internal void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (_visible == visible && _root.gameObject.activeSelf == visible) return;

            _visible = visible;
            _root.gameObject.SetActive(visible);
        }

        private void CreateKey(float x, float y, float width, float height, string label, bool action, Action click)
        {
            RectTransform key = CascadeUI.TopLeft("Key_" + label, _root, x, y, width, height);

            Color background = action ? _theme.AccentSoft : _theme.FieldBg;
            CascadeUI.Image(key, CascadeSprites.Rounded(6), background, Image.Type.Sliced);

            RectTransform border = CascadeUI.Rect("Border", key);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, _theme.FieldBorder, 6);

            RectTransform hover = CascadeUI.Rect("Hover", key);
            CascadeUI.Stretch(hover);
            Image hoverImage = CascadeUI.Image(hover, CascadeSprites.Rounded(6), _theme.RowHover, Image.Type.Sliced);
            hoverImage.gameObject.SetActive(false);

            RectTransform labelRect = CascadeUI.Rect("Label", key);
            CascadeUI.Stretch(labelRect, 2f, 2f, 0f, 0f);
            CascadeUI.Text(labelRect, label, action ? 10.5f : 11.5f, _theme.TextPrimary,
                TMPro.TextAlignmentOptions.Center, true);

            CascadeInteractable interactable = _window.Register(
                key,
                CascadeLayerGroup.Chrome,
                click,
                hover: hovered => hoverImage.gameObject.SetActive(hovered));
            _keys.Add(interactable);
        }

        /// <summary>
        /// True for a key press. The menu uses this to keep search focus while a
        /// virtual key is being tapped (otherwise the press would blur the field
        /// before the character is delivered).
        /// </summary>
        internal bool Contains(CascadeInteractable interactable)
        {
            if (interactable == null) return false;
            for (int i = 0; i < _keys.Count; i++)
            {
                if (_keys[i] == interactable) return true;
            }
            return false;
        }
    }
}
