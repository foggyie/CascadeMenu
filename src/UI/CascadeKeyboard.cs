using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    /// <summary>
    /// On-screen keyboard styled like the main Cascade window. It appears at the
    /// bottom of the window whenever the search box or an inline text field is
    /// focused, on PC (click the keys with the cursor) and in VR (press them with
    /// the pointer). The physical keyboard keeps working alongside it.
    /// </summary>
    internal sealed class CascadeKeyboard
    {
        private const float PanelWidth = 440f;
        private const float PanelHeight = 198f;
        private const float TitlebarHeight = 26f;
        private const float KeyWidth = 38f;
        private const float KeyHeight = 27f;
        private const float KeyGap = 4f;
        private const float WideKey = 52f;

        private sealed class KeyDef
        {
            public string Label;
            public float Width = KeyWidth;
            public Action Click;
            public bool Accent;
            public bool Secondary;
            public bool IsShift;
            public char Letter;                 // non-zero for a-z keys (follows Shift)
            public CascadeIcon? Icon;
        }

        private sealed class LetterKey
        {
            public char Letter;
            public TMP_Text Label;
        }

        private readonly List<CascadeInteractable> _keys = new List<CascadeInteractable>(48);
        private readonly List<LetterKey> _letters = new List<LetterKey>(26);

        private RectTransform _root;
        private CascadeWindow _window;
        private CascadeTheme _theme;
        private bool _visible;

        private bool _upper;
        private Image _shiftBackground;
        private Image _shiftIcon;

        internal void Build(CascadeWindow window, RectTransform parent)
        {
            _window = window;
            _theme = window.Theme;

            _root = CascadeUI.Rect("Keyboard", parent);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.anchoredPosition = new Vector2(0f, 10f);
            _root.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            RectTransform shadow = CascadeUI.Rect("Shadow", _root);
            CascadeUI.Stretch(shadow, -16f, -16f, -12f, -16f);
            CascadeUI.Image(shadow, CascadeSprites.Shadow(16, 20), _theme.ShadowColor, Image.Type.Sliced);

            RectTransform body = CascadeUI.Rect("Body", _root);
            CascadeUI.Stretch(body);
            CascadeUI.Panel(body, _theme.ContentBg, 10);

            RectTransform border = CascadeUI.Rect("Border", body);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, _theme.WindowBorder, 10);

            // Swallow clicks in the gaps so the page behind does not react.
            window.Register(body, CascadeLayerGroup.Chrome, () => { });

            BuildTitlebar(body);
            BuildKeys(body);

            _root.gameObject.SetActive(false);
        }

        private void BuildTitlebar(RectTransform body)
        {
            RectTransform titlebar = CascadeUI.Rect("Titlebar", body);
            titlebar.anchorMin = new Vector2(0f, 1f);
            titlebar.anchorMax = new Vector2(1f, 1f);
            titlebar.pivot = new Vector2(0.5f, 1f);
            titlebar.offsetMin = new Vector2(0f, -TitlebarHeight);
            titlebar.offsetMax = Vector2.zero;
            CascadeUI.Image(titlebar, CascadeSprites.RoundedTop(10), _theme.TitlebarBg, Image.Type.Sliced);

            AddTrafficDot(titlebar, "Close", 10f, _theme.TrafficClose, () => _window.BlurInput());
            AddTrafficDot(titlebar, "Minimize", 22f, _theme.TrafficMinimize, () => _window.BlurInput());
            AddTrafficDot(titlebar, "Zoom", 34f, _theme.TrafficZoom, null);

            RectTransform icon = CascadeUI.Center("Icon", titlebar, -34f, 0f, 13f, 13f);
            CascadeUI.Image(icon, CascadeIcons.Get(CascadeIcon.Keyboard), _theme.TextSecondary, Image.Type.Simple);

            RectTransform title = CascadeUI.Center("Title", titlebar, 0f, 0f, 90f, 18f);
            CascadeUI.Text(title, "Keyboard", 10f, _theme.TextPrimary, TextAlignmentOptions.Center, true);

            RectTransform hairline = CascadeUI.Rect("Hairline", titlebar);
            hairline.anchorMin = new Vector2(0f, 0f);
            hairline.anchorMax = new Vector2(1f, 0f);
            hairline.pivot = new Vector2(0.5f, 0f);
            hairline.offsetMin = Vector2.zero;
            hairline.offsetMax = new Vector2(0f, 1f);
            CascadeUI.Solid(hairline, _theme.TitlebarHairline);
        }

        private void BuildKeys(RectTransform body)
        {
            float y = TitlebarHeight + 8f;
            float step = KeyHeight + KeyGap;

            var digits = new List<KeyDef>();
            foreach (char c in "1234567890") digits.Add(Symbol(c));
            BuildRow(body, y, digits);

            var top = new List<KeyDef>();
            foreach (char c in "qwertyuiop") top.Add(Letter(c));
            BuildRow(body, y + step, top);

            var home = new List<KeyDef>();
            foreach (char c in "asdfghjkl") home.Add(Letter(c));
            home.Add(Symbol('\''));
            BuildRow(body, y + step * 2f, home);

            var bottom = new List<KeyDef>();
            bottom.Add(new KeyDef
            {
                Width = WideKey,
                IsShift = true,
                Secondary = true,
                Icon = CascadeIcon.ArrowUp,
                Label = "Shift",
                Click = ToggleShift
            });
            foreach (char c in "zxcvbnm") bottom.Add(Letter(c));
            bottom.Add(new KeyDef
            {
                Width = WideKey,
                Secondary = true,
                Icon = CascadeIcon.Backspace,
                Label = "Del",
                Click = () => _window.HandleBackspace()
            });
            BuildRow(body, y + step * 3f, bottom);

            var action = new List<KeyDef>();
            action.Add(new KeyDef { Label = "Clear", Width = 56f, Secondary = true, Click = () => _window.ClearInput() });
            action.Add(Symbol('@'));
            action.Add(new KeyDef { Label = "Space", Width = 150f, Click = () => _window.HandleTextInput(' ') });
            action.Add(Symbol('.'));
            action.Add(Symbol('-'));
            action.Add(new KeyDef { Label = "Done", Width = 64f, Accent = true, Click = () => _window.FinishInput() });
            BuildRow(body, y + step * 4f, action);
        }

        private KeyDef Symbol(char c)
        {
            return new KeyDef { Label = c.ToString(), Click = () => _window.HandleTextInput(c) };
        }

        private KeyDef Letter(char c)
        {
            return new KeyDef { Label = c.ToString(), Letter = c };
        }

        private void BuildRow(RectTransform body, float y, List<KeyDef> defs)
        {
            float total = -KeyGap;
            for (int i = 0; i < defs.Count; i++) total += defs[i].Width + KeyGap;

            float x = (PanelWidth - total) * 0.5f;
            for (int i = 0; i < defs.Count; i++)
            {
                CreateKey(body, x, y, defs[i]);
                x += defs[i].Width + KeyGap;
            }
        }

        private void AddTrafficDot(RectTransform parent, string name, float x, Color color, Action click)
        {
            RectTransform hit = CascadeUI.TopLeft("Dot_" + name, parent, x, 8f, 10f, 10f);
            CascadeUI.Image(hit, CascadeSprites.Circle(), color, Image.Type.Simple);
            if (click != null) _window.Register(hit, CascadeLayerGroup.Chrome, click);
        }

        internal void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (_visible == visible && _root.gameObject.activeSelf == visible) return;

            _visible = visible;
            _root.gameObject.SetActive(visible);
            if (!visible && _upper) SetUpper(false);
        }

        // -----------------------------------------------------------------
        // Shift handling
        // -----------------------------------------------------------------

        private void ToggleShift()
        {
            SetUpper(!_upper);
        }

        private void SetUpper(bool upper)
        {
            _upper = upper;
            for (int i = 0; i < _letters.Count; i++)
            {
                LetterKey key = _letters[i];
                if (key.Label == null) continue;
                key.Label.text = (upper ? char.ToUpperInvariant(key.Letter) : key.Letter).ToString();
            }

            if (_shiftBackground != null) _shiftBackground.color = upper ? (Color)_theme.Accent : (Color)_theme.ViewBg;
            if (_shiftIcon != null) _shiftIcon.color = upper ? (Color)_theme.SelectionText : (Color)_theme.TextPrimary;
        }

        private void TypeLetter(char letter)
        {
            _window.HandleTextInput(_upper ? char.ToUpperInvariant(letter) : letter);

            // Shift is one-shot: it applies to a single letter, like a phone keyboard.
            if (_upper) SetUpper(false);
        }

        // -----------------------------------------------------------------
        // Key construction
        // -----------------------------------------------------------------

        private void CreateKey(RectTransform parent, float x, float y, KeyDef def)
        {
            RectTransform key = CascadeUI.TopLeft("Key_" + def.Label, parent, x, y, def.Width, KeyHeight);

            Color background;
            if (def.Accent) background = _theme.Accent;
            else if (def.Secondary) background = _theme.ViewBg;
            else background = _theme.FieldBg;

            Image backgroundImage = CascadeUI.Image(key, CascadeSprites.Rounded(6), background, Image.Type.Sliced);

            RectTransform border = CascadeUI.Rect("Border", key);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, def.Accent ? _theme.Accent : _theme.FieldBorder, 6);

            RectTransform hover = CascadeUI.Rect("Hover", key);
            CascadeUI.Stretch(hover);
            Image hoverImage = CascadeUI.Image(hover, CascadeSprites.Rounded(6), _theme.RowHover, Image.Type.Sliced);
            hoverImage.gameObject.SetActive(false);

            Color foreground = def.Accent ? (Color)_theme.SelectionText : (Color)_theme.TextPrimary;
            TMP_Text label = null;
            if (def.Icon.HasValue)
            {
                RectTransform iconRect = CascadeUI.Center("Icon", key, 0f, 0f, 14f, 14f);
                Image icon = CascadeUI.Image(iconRect, CascadeIcons.Get(def.Icon.Value), foreground, Image.Type.Simple);
                if (def.IsShift)
                {
                    _shiftBackground = backgroundImage;
                    _shiftIcon = icon;
                }
            }
            else
            {
                RectTransform labelRect = CascadeUI.Rect("Label", key);
                CascadeUI.Stretch(labelRect, 2f, 2f, 0f, 0f);
                bool small = def.Accent || def.Secondary;
                label = CascadeUI.Text(labelRect, def.Label, small ? 10.5f : 11.5f, foreground,
                    TextAlignmentOptions.Center, true);
            }

            Action click = def.Click;
            if (def.Letter != '\0')
            {
                char letter = def.Letter;
                _letters.Add(new LetterKey { Letter = letter, Label = label });
                click = () => TypeLetter(letter);
            }

            CascadeInteractable interactable = _window.Register(
                key,
                CascadeLayerGroup.Chrome,
                click,
                hover: hovered => hoverImage.gameObject.SetActive(hovered));
            _keys.Add(interactable);
        }

        /// <summary>
        /// True for a key press. The menu uses this to keep focus while a
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
