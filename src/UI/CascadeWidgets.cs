using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    internal sealed class CascadeButtonView
    {
        public RectTransform Root;
        public Image Background;
        public Image Hover;
        public TextMeshProUGUI Label;
    }

    internal sealed class CascadeToggleView : MonoBehaviour
    {
        public Image Background;
        public RectTransform Knob;
        public Func<bool> Get;
        public bool On;

        public RectTransform Root
        {
            get { return (RectTransform)transform; }
        }

        private CascadeTheme _theme;
        private float _value;

        public void Configure(CascadeTheme theme, bool value)
        {
            _theme = theme;
            On = value;
            _value = value ? 1f : 0f;
            Apply();
        }

        public void Set(bool value, bool instant = false)
        {
            On = value;
            if (instant)
            {
                _value = value ? 1f : 0f;
                Apply();
            }
        }

        private void Update()
        {
            if (Get != null)
            {
                bool current = Get();
                if (current != On) On = current;
            }

            float target = On ? 1f : 0f;
            if (Mathf.Abs(_value - target) > 0.001f)
            {
                _value = Mathf.MoveTowards(_value, target, Time.unscaledDeltaTime * 7f);
                Apply();
            }
        }

        private void Apply()
        {
            if (_theme == null || Background == null || Knob == null) return;
            Background.color = Color.Lerp(_theme.ToggleOff, _theme.AccentSoft, _value);
            Knob.anchoredPosition = new Vector2(Mathf.Lerp(8f, 20f, _value), 0f);
        }
    }

    internal sealed class CascadeSliderView : MonoBehaviour
    {
        public RectTransform Hit;
        public Image Fill;
        public RectTransform Thumb;
        public TMP_Text ValueLabel;

        private CascadeTheme _theme;
        private float _min;
        private float _max;
        private Func<float> _get;
        private Action<float> _set;
        private Func<float, string> _format;
        private float _lastValue = float.NaN;
        private float _lastWidth = -1f;
        private float _thumbScale = 1f;

        public void Configure(
            CascadeTheme theme,
            float min,
            float max,
            Func<float> get,
            Action<float> set,
            Func<float, string> format)
        {
            _theme = theme;
            _min = min;
            _max = Mathf.Max(min + 0.0001f, max);
            _get = get;
            _set = set;
            _format = format;
            Refresh(true);
        }

        public void DragToWorld(Vector3 worldPoint)
        {
            if (Hit == null) return;
            Rect rect = Hit.rect;
            if (rect.width <= 0.001f) return;
            Vector3 local = Hit.InverseTransformPoint(worldPoint);
            float t = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            float value = Mathf.Lerp(_min, _max, t);
            _set?.Invoke(value);
            Refresh(true);
        }

        public void Refresh(bool force = false)
        {
            if (Hit == null) return;
            float width = Hit.rect.width;
            float value = _get != null ? _get() : _min;
            if (!force && Mathf.Abs(value - _lastValue) < 0.0001f && Mathf.Abs(width - _lastWidth) < 0.1f) return;
            _lastValue = value;
            _lastWidth = width;

            float t = Mathf.InverseLerp(_min, _max, value);
            if (Fill != null)
            {
                var fillRect = Fill.rectTransform;
                Vector2 size = fillRect.sizeDelta;
                fillRect.sizeDelta = new Vector2(Mathf.Max(0f, t * width), size.y);
            }
            if (Thumb != null)
            {
                Thumb.anchoredPosition = new Vector2(Mathf.Lerp(7f, Mathf.Max(7f, width - 7f), t), 0f);
                Thumb.localScale = Vector3.one * Mathf.Lerp(Thumb.localScale.x, _thumbScale, 0.35f);
            }
            if (ValueLabel != null)
            {
                ValueLabel.text = _format != null ? _format(value) : value.ToString("0.##");
            }
        }

        public void SetHover(bool hovered)
        {
            _thumbScale = hovered ? 1.18f : 1f;
        }

        private void Update()
        {
            Refresh(false);
            if (Thumb != null && Mathf.Abs(Thumb.localScale.x - _thumbScale) > 0.01f)
            {
                Thumb.localScale = Vector3.one * Mathf.Lerp(Thumb.localScale.x, _thumbScale, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            }
        }
    }

    internal sealed class CascadeStepperView : MonoBehaviour
    {
        public RectTransform Root;
        public TMP_Text ValueLabel;
        public int Min;
        public int Max;
        public Func<int> Get;
        public Action<int> Set;
        public Func<int, string> Format;

        public void Refresh()
        {
            int value = Mathf.Clamp(Get != null ? Get() : Min, Min, Max);
            if (ValueLabel != null) ValueLabel.text = Format != null ? Format(value) : value.ToString();
        }

        public void Step(int delta)
        {
            int value = Mathf.Clamp(Get != null ? Get() : Min, Min, Max);
            int next = Mathf.Clamp(value + delta, Min, Max);
            if (next == value) return;
            Set?.Invoke(next);
            Refresh();
        }
    }

    internal sealed class CascadeRadioView : MonoBehaviour
    {
        public string[] Options;
        public Func<int> Get;
        public Action<int> Set;
        public Image[] Fills;
        public Image[] Rings;
        public Image[] Dots;
        public TMP_Text[] Labels;
        public CascadeTheme Theme;

        public RectTransform Root
        {
            get { return (RectTransform)transform; }
        }

        public void Refresh()
        {
            int selected = Mathf.Clamp(Get != null ? Get() : 0, 0, Mathf.Max(0, Options.Length - 1));
            for (int i = 0; i < Fills.Length; i++)
            {
                bool on = i == selected;
                Fills[i].color = on ? Theme.Accent : new Color(0f, 0f, 0f, 0f);
                Rings[i].color = on ? Theme.Accent : Theme.TextTertiary;
                Dots[i].gameObject.SetActive(on);
                Labels[i].color = on ? Theme.TextPrimary : Theme.TextSecondary;
            }
        }
    }

    internal sealed class CascadeDropdownView
    {
        public RectTransform Root;
        public TMP_Text Label;
        public Image Hover;
        public string[] Options;
        public Func<int> Get;
        public Action<int> Set;
        public CascadeTheme Theme;

        public void Refresh()
        {
            int index = Mathf.Clamp(Get != null ? Get() : 0, 0, Mathf.Max(0, Options.Length - 1));
            if (Label != null) Label.text = Options.Length > 0 ? Options[index] : string.Empty;
        }
    }

    internal sealed class CascadeSearchField
    {
        public RectTransform Root;
        public RectTransform TextRect;
        public TMP_Text Text;
        public TMP_Text Placeholder;
        public RectTransform ClearRoot;
        public Image Background;
        public Image Border;
        public CascadeInteractable Interactable;
        public CascadeInteractable ClearInteractable;
        public string Query = string.Empty;
        public bool Focused;

        private CascadeTheme _theme;
        private Action<string> _changed;

        public void Configure(CascadeTheme theme, Action<string> changed)
        {
            _theme = theme;
            _changed = changed;
        }

        public void SetFocused(bool focused)
        {
            Focused = focused;
            if (Border != null) Border.color = focused ? _theme.FieldFocus : _theme.FieldBorder;
            if (Background != null)
            {
                Color baseColor = _theme.FieldBg;
                if (focused) baseColor = Color.Lerp(baseColor, _theme.Accent, 0.08f);
                Background.color = baseColor;
            }
        }

        public void SetQuery(string query, bool notify = true)
        {
            Query = query ?? string.Empty;
            if (Text != null) Text.text = Query;
            if (Placeholder != null) Placeholder.gameObject.SetActive(Query.Length == 0);
            if (ClearRoot != null) ClearRoot.gameObject.SetActive(Query.Length > 0);
            if (notify) _changed?.Invoke(Query);
        }

        public void Append(char character)
        {
            if (character < 32 || character == 127) return;
            if (Query.Length >= 32) return;
            SetQuery(Query + character);
        }

        public void Backspace()
        {
            if (Query.Length == 0) return;
            SetQuery(Query.Substring(0, Query.Length - 1));
        }
    }

    internal static class CascadeWidgets
    {
        public static CascadeButtonView Button(
            CascadeWindow window,
            RectTransform parent,
            string text,
            bool primary,
            Action click,
            CascadeLayerGroup group = CascadeLayerGroup.Content)
        {
            CascadeTheme theme = window.Theme;
            RectTransform root = CascadeUI.Rect("Button", parent);
            var view = new CascadeButtonView { Root = root };

            if (primary)
            {
                RectTransform shadow = CascadeUI.Rect("Shadow", root);
                CascadeUI.Stretch(shadow, 0f, 0f, 1f, -1f);
                CascadeUI.Image(shadow, CascadeSprites.Rounded(6), theme.ShadowColor, Image.Type.Sliced);
            }

            RectTransform background = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(background);
            view.Background = CascadeUI.Image(background, CascadeSprites.Rounded(6), Color.white, Image.Type.Sliced);
            var gradient = background.gameObject.AddComponent<CascadeGradient>();
            if (primary)
            {
                gradient.Top = theme.ButtonPrimaryTop;
                gradient.Bottom = theme.ButtonPrimaryBottom;
            }
            else
            {
                gradient.Top = theme.ButtonSecondaryTop;
                gradient.Bottom = theme.ButtonSecondaryBottom;
                RectTransform border = CascadeUI.Rect("Border", background);
                CascadeUI.Stretch(border);
                CascadeUI.Outline(border, theme.FieldBorder, 6);
            }

            RectTransform hover = CascadeUI.Rect("Hover", root);
            CascadeUI.Stretch(hover);
            Color hoverTint = primary
                ? new Color(1f, 1f, 1f, 0.14f)
                : (theme.IsDark ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.05f));
            view.Hover = CascadeUI.Image(hover, CascadeSprites.Rounded(6), hoverTint, Image.Type.Sliced);
            view.Hover.gameObject.SetActive(false);

            RectTransform labelRect = CascadeUI.Rect("Label", root);
            CascadeUI.Stretch(labelRect, 6f, 6f, 1f, 1f);
            view.Label = CascadeUI.Text(
                labelRect,
                text,
                12f,
                primary ? theme.ButtonText : theme.ButtonSecondaryText,
                TextAlignmentOptions.Center,
                true);

            CascadeInteractable interactable = window.Register(
                root,
                group,
                click,
                press: () => { if (root != null) root.localScale = new Vector3(0.98f, 0.98f, 1f); },
                release: () => { if (root != null) root.localScale = Vector3.one; },
                hover: hovered => { if (view.Hover != null) view.Hover.gameObject.SetActive(hovered); });
            view.Root = root;
            return view;
        }

        public static CascadeToggleView Toggle(CascadeTheme theme, RectTransform parent, bool value)
        {
            RectTransform root = CascadeUI.Rect("Toggle", parent);
            root.sizeDelta = new Vector2(28f, 16f);
            var view = root.gameObject.AddComponent<CascadeToggleView>();

            RectTransform switchRect = CascadeUI.Rect("Switch", root);
            CascadeUI.Stretch(switchRect);
            view.Background = CascadeUI.Image(switchRect, CascadeSprites.Pill(), theme.ToggleOff, Image.Type.Simple);

            RectTransform knob = CascadeUI.Rect("Knob", root);
            knob.anchorMin = knob.anchorMax = new Vector2(0f, 0.5f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.anchoredPosition = new Vector2(8f, 0f);
            knob.sizeDelta = new Vector2(14f, 14f);

            RectTransform knobShadow = CascadeUI.Rect("KnobShadow", knob);
            CascadeUI.Stretch(knobShadow, -1f, -1f, -1f, -1f);
            CascadeUI.Image(knobShadow, CascadeSprites.Circle(), new Color(0f, 0f, 0f, 0.20f), Image.Type.Simple);

            RectTransform knobFace = CascadeUI.Rect("KnobFace", knob);
            CascadeUI.Stretch(knobFace);
            CascadeUI.Image(knobFace, CascadeSprites.Circle(), theme.ToggleKnob, Image.Type.Simple);

            view.Knob = knob;
            view.Configure(theme, value);
            return view;
        }

        public static CascadeSliderView Slider(
            CascadeWindow window,
            CascadeTheme theme,
            RectTransform parent,
            float min,
            float max,
            Func<float> get,
            Action<float> set,
            Func<float, string> format,
            out TMP_Text valueLabel)
        {
            RectTransform hit = CascadeUI.Rect("Slider", parent);
            var view = hit.gameObject.AddComponent<CascadeSliderView>();
            view.Hit = hit;

            RectTransform track = CascadeUI.Rect("Track", hit);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(0f, -2f);
            track.offsetMax = new Vector2(0f, 2f);
            CascadeUI.Image(track, CascadeSprites.Rounded(2), theme.SliderTrack, Image.Type.Simple);

            RectTransform fill = CascadeUI.Rect("Fill", hit);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(0f, 0f);
            fill.sizeDelta = new Vector2(0f, 4f);
            view.Fill = CascadeUI.Image(fill, CascadeSprites.Rounded(2), theme.SliderFill, Image.Type.Simple);

            RectTransform thumb = CascadeUI.Rect("Thumb", hit);
            thumb.anchorMin = thumb.anchorMax = new Vector2(0f, 0.5f);
            thumb.pivot = new Vector2(0.5f, 0.5f);
            thumb.sizeDelta = new Vector2(14f, 14f);
            view.Thumb = thumb;

            RectTransform thumbShadow = CascadeUI.Rect("Shadow", thumb);
            CascadeUI.Stretch(thumbShadow, -1f, -1f, -1f, -1f);
            CascadeUI.Image(thumbShadow, CascadeSprites.Circle(), new Color(0f, 0f, 0f, 0.18f), Image.Type.Simple);

            RectTransform thumbFace = CascadeUI.Rect("Face", thumb);
            CascadeUI.Stretch(thumbFace);
            CascadeUI.Image(thumbFace, CascadeSprites.Circle(), theme.SliderThumb, Image.Type.Simple);

            RectTransform thumbRing = CascadeUI.Rect("Ring", thumb);
            CascadeUI.Stretch(thumbRing);
            CascadeUI.Image(thumbRing, CascadeSprites.CircleRing(5f), theme.SliderThumbBorder, Image.Type.Simple);

            RectTransform valueRect = CascadeUI.Rect("Value", parent);
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.pivot = new Vector2(1f, 1f);
            valueRect.anchoredPosition = new Vector2(-12f, -9f);
            valueRect.sizeDelta = new Vector2(74f, 18f);
            valueLabel = CascadeUI.Text(valueRect, string.Empty, 11f, theme.TextSecondary, TextAlignmentOptions.Right);

            view.ValueLabel = valueLabel;
            view.Configure(theme, min, max, get, set, format);

            window.Register(
                hit,
                CascadeLayerGroup.Content,
                dragWorld: world => view.DragToWorld(world),
                hover: hovered => view.SetHover(hovered),
                slider: true);

            return view;
        }

        public static CascadeStepperView Stepper(
            CascadeWindow window,
            CascadeTheme theme,
            RectTransform parent,
            int min,
            int max,
            Func<int> get,
            Action<int> set,
            Func<int, string> format)
        {
            RectTransform root = CascadeUI.Rect("Stepper", parent);
            root.sizeDelta = new Vector2(88f, 26f);
            var view = root.gameObject.AddComponent<CascadeStepperView>();
            view.Root = root;
            view.Min = min;
            view.Max = max;
            view.Get = get;
            view.Set = set;
            view.Format = format;

            RectTransform background = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(background);
            CascadeUI.Image(background, CascadeSprites.Rounded(6), theme.ButtonSecondaryBottom, Image.Type.Sliced);
            RectTransform border = CascadeUI.Rect("Border", background);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, theme.FieldBorder, 6);

            RectTransform minus = CascadeUI.Rect("Minus", root);
            minus.anchorMin = minus.anchorMax = new Vector2(0f, 0.5f);
            minus.pivot = new Vector2(0f, 0.5f);
            minus.anchoredPosition = new Vector2(0f, 0f);
            minus.sizeDelta = new Vector2(26f, 26f);
            RectTransform minusIcon = CascadeUI.Center("Icon", minus, 0f, 0f, 10f, 10f);
            CascadeUI.Image(minusIcon, CascadeIcons.Get(CascadeIcon.Minus), theme.TextSecondary, Image.Type.Simple);
            Image minusHover = HoverOverlay(minus, 6, theme);

            RectTransform plus = CascadeUI.Rect("Plus", root);
            plus.anchorMin = plus.anchorMax = new Vector2(1f, 0.5f);
            plus.pivot = new Vector2(1f, 0.5f);
            plus.anchoredPosition = new Vector2(0f, 0f);
            plus.sizeDelta = new Vector2(26f, 26f);
            RectTransform plusIcon = CascadeUI.Center("Icon", plus, 0f, 0f, 10f, 10f);
            CascadeUI.Image(plusIcon, CascadeIcons.Get(CascadeIcon.Plus), theme.TextSecondary, Image.Type.Simple);
            Image plusHover = HoverOverlay(plus, 6, theme);

            RectTransform valueRect = CascadeUI.Rect("Value", root);
            CascadeUI.Stretch(valueRect, 26f, 26f, 0f, 0f);
            view.ValueLabel = CascadeUI.Text(valueRect, string.Empty, 11f, theme.TextPrimary, TextAlignmentOptions.Center);

            RectTransform sepLeft = CascadeUI.Rect("SeparatorLeft", root);
            sepLeft.anchorMin = sepLeft.anchorMax = new Vector2(0f, 0.5f);
            sepLeft.pivot = new Vector2(0f, 0.5f);
            sepLeft.anchoredPosition = new Vector2(26f, 0f);
            sepLeft.sizeDelta = new Vector2(1f, 14f);
            CascadeUI.Solid(sepLeft, theme.Separator);

            RectTransform sepRight = CascadeUI.Rect("SeparatorRight", root);
            sepRight.anchorMin = sepRight.anchorMax = new Vector2(1f, 0.5f);
            sepRight.pivot = new Vector2(1f, 0.5f);
            sepRight.anchoredPosition = new Vector2(-26f, 0f);
            sepRight.sizeDelta = new Vector2(1f, 14f);
            CascadeUI.Solid(sepRight, theme.Separator);

            window.Register(minus, CascadeLayerGroup.Content, () => view.Step(-1), hover: h => minusHover.gameObject.SetActive(h));
            window.Register(plus, CascadeLayerGroup.Content, () => view.Step(1), hover: h => plusHover.gameObject.SetActive(h));
            view.Refresh();
            return view;
        }

        public static CascadeRadioView Radio(
            CascadeWindow window,
            CascadeTheme theme,
            RectTransform parent,
            string[] options,
            Func<int> get,
            Action<int> set)
        {
            int count = options.Length;
            RectTransform root = CascadeUI.Rect("Radio", parent);
            root.sizeDelta = new Vector2(count * 88f + Mathf.Max(0, count - 1) * 6f, 24f);
            var view = root.gameObject.AddComponent<CascadeRadioView>();
            view.Options = options;
            view.Get = get;
            view.Set = set;
            view.Theme = theme;
            view.Fills = new Image[count];
            view.Rings = new Image[count];
            view.Dots = new Image[count];
            view.Labels = new TMP_Text[count];

            for (int i = 0; i < count; i++)
            {
                int index = i;
                RectTransform option = CascadeUI.Rect("Option" + i, root);
                option.anchorMin = option.anchorMax = new Vector2(0f, 0.5f);
                option.pivot = new Vector2(0f, 0.5f);
                option.anchoredPosition = new Vector2(i * 94f, 0f);
                option.sizeDelta = new Vector2(88f, 24f);

                RectTransform fillRect = CascadeUI.Rect("Fill", option);
                fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
                fillRect.pivot = new Vector2(0.5f, 0.5f);
                fillRect.anchoredPosition = new Vector2(6f, 0f);
                fillRect.sizeDelta = new Vector2(12f, 12f);
                CascadeUI.Image(fillRect, CascadeSprites.Circle(), Color.clear, Image.Type.Simple);
                view.Fills[i] = fillRect.GetComponent<Image>();

                RectTransform ringRect = CascadeUI.Rect("Ring", option);
                ringRect.anchorMin = ringRect.anchorMax = new Vector2(0f, 0.5f);
                ringRect.pivot = new Vector2(0.5f, 0.5f);
                ringRect.anchoredPosition = new Vector2(6f, 0f);
                ringRect.sizeDelta = new Vector2(12f, 12f);
                CascadeUI.Image(ringRect, CascadeSprites.CircleRing(5f), theme.TextTertiary, Image.Type.Simple);
                view.Rings[i] = ringRect.GetComponent<Image>();

                RectTransform dotRect = CascadeUI.Rect("Dot", option);
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = new Vector2(6f, 0f);
                dotRect.sizeDelta = new Vector2(5f, 5f);
                CascadeUI.Image(dotRect, CascadeSprites.Circle(), Color.white, Image.Type.Simple);
                view.Dots[i] = dotRect.GetComponent<Image>();

                RectTransform labelRect = CascadeUI.Rect("Label", option);
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.offsetMin = new Vector2(17f, 0f);
                labelRect.offsetMax = new Vector2(0f, 0f);
                view.Labels[i] = CascadeUI.Text(labelRect, options[i], 11.5f, theme.TextSecondary, TextAlignmentOptions.Left);

                RectTransform hover = CascadeUI.Rect("Hover", option);
                CascadeUI.Stretch(hover, 0f, 0f, 1f, 1f);
                Image hoverImage = CascadeUI.Image(hover, CascadeSprites.Rounded(5), theme.RowHover, Image.Type.Sliced);
                hoverImage.transform.SetAsFirstSibling();
                hoverImage.gameObject.SetActive(false);

                window.Register(
                    option,
                    CascadeLayerGroup.Content,
                    () =>
                    {
                        view.Set?.Invoke(index);
                        view.Refresh();
                    },
                    hover: h =>
                    {
                        hoverImage.gameObject.SetActive(h);
                        view.Labels[index].color = h || index == view.Get() ? theme.TextPrimary : theme.TextSecondary;
                    });
            }

            view.Refresh();
            return view;
        }

        public static CascadeDropdownView Dropdown(
            CascadeWindow window,
            CascadeTheme theme,
            RectTransform parent,
            string[] options,
            Func<int> get,
            Action<int> set)
        {
            RectTransform root = CascadeUI.Rect("Dropdown", parent);
            root.sizeDelta = new Vector2(140f, 26f);
            var view = new CascadeDropdownView
            {
                Root = root,
                Options = options,
                Get = get,
                Set = set,
                Theme = theme
            };

            RectTransform background = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(background);
            CascadeUI.Image(background, CascadeSprites.Rounded(6), theme.FieldBg, Image.Type.Sliced);
            RectTransform border = CascadeUI.Rect("Border", background);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, theme.FieldBorder, 6);

            RectTransform hover = CascadeUI.Rect("Hover", root);
            CascadeUI.Stretch(hover);
            view.Hover = CascadeUI.Image(hover, CascadeSprites.Rounded(6), theme.RowHover, Image.Type.Sliced);
            view.Hover.gameObject.SetActive(false);

            RectTransform labelRect = CascadeUI.Rect("Label", root);
            CascadeUI.Stretch(labelRect, 10f, 22f, 0f, 0f);
            view.Label = CascadeUI.Text(labelRect, string.Empty, 11.5f, theme.TextPrimary, TextAlignmentOptions.Left);

            RectTransform chevron = CascadeUI.Rect("Chevron", root);
            chevron.anchorMin = chevron.anchorMax = new Vector2(1f, 0.5f);
            chevron.pivot = new Vector2(1f, 0.5f);
            chevron.anchoredPosition = new Vector2(-8f, 0f);
            chevron.sizeDelta = new Vector2(9f, 9f);
            CascadeUI.Image(chevron, CascadeIcons.Get(CascadeIcon.ChevronDown), theme.TextSecondary, Image.Type.Simple);

            window.Register(
                root,
                CascadeLayerGroup.Content,
                () => window.OpenDropdown(view),
                hover: h => view.Hover.gameObject.SetActive(h));

            view.Refresh();
            return view;
        }

        public static CascadeSearchField Search(
            CascadeWindow window,
            RectTransform parent,
            CascadeTheme theme,
            Action<string> changed)
        {
            RectTransform root = CascadeUI.Rect("Search", parent);
            root.sizeDelta = new Vector2(140f, 28f);
            var field = new CascadeSearchField { Root = root };
            field.Configure(theme, changed);

            RectTransform background = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(background);
            field.Background = CascadeUI.Image(background, CascadeSprites.Rounded(6), theme.FieldBg, Image.Type.Sliced);

            RectTransform border = CascadeUI.Rect("Border", root);
            CascadeUI.Stretch(border);
            field.Border = CascadeUI.Outline(border, theme.FieldBorder, 6);

            RectTransform icon = CascadeUI.Rect("Icon", root);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(9f, 0f);
            icon.sizeDelta = new Vector2(12f, 12f);
            CascadeUI.Image(icon, CascadeIcons.Get(CascadeIcon.Search), theme.TextSecondary, Image.Type.Simple);

            RectTransform textRect = CascadeUI.Rect("Text", root);
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(26f, 1f);
            textRect.offsetMax = new Vector2(-24f, -1f);
            field.TextRect = textRect;
            field.Text = CascadeUI.Text(textRect, string.Empty, 11.5f, theme.TextPrimary, TextAlignmentOptions.Left);
            field.Text.richText = false;

            RectTransform placeholder = CascadeUI.Rect("Placeholder", root);
            placeholder.anchorMin = new Vector2(0f, 0f);
            placeholder.anchorMax = new Vector2(1f, 1f);
            placeholder.pivot = new Vector2(0.5f, 0.5f);
            placeholder.offsetMin = new Vector2(26f, 1f);
            placeholder.offsetMax = new Vector2(-24f, -1f);
            field.Placeholder = CascadeUI.Text(placeholder, "Search", 11.5f, theme.TextTertiary, TextAlignmentOptions.Left);
            field.Placeholder.richText = false;

            RectTransform clear = CascadeUI.Rect("Clear", root);
            clear.anchorMin = clear.anchorMax = new Vector2(1f, 0.5f);
            clear.pivot = new Vector2(1f, 0.5f);
            clear.anchoredPosition = new Vector2(-6f, 0f);
            clear.sizeDelta = new Vector2(18f, 18f);
            field.ClearRoot = clear;
            RectTransform clearIcon = CascadeUI.Center("Icon", clear, 0f, 0f, 8f, 8f);
            CascadeUI.Image(clearIcon, CascadeIcons.Get(CascadeIcon.Close), theme.TextSecondary, Image.Type.Simple);
            Image clearHover = HoverOverlay(clear, 5, theme);

            field.Interactable = window.Register(
                root,
                CascadeLayerGroup.Chrome,
                () => window.FocusSearch(field),
                hover: h =>
                {
                    if (!field.Focused && field.Border != null)
                        field.Border.color = h ? theme.TextSecondary : theme.FieldBorder;
                });
            field.ClearInteractable = window.Register(
                clear,
                CascadeLayerGroup.Chrome,
                () => field.SetQuery(string.Empty),
                hover: h => clearHover.gameObject.SetActive(h));

            field.SetQuery(string.Empty, false);
            field.SetFocused(false);
            return field;
        }

        private static Image HoverOverlay(RectTransform parent, int radius, CascadeTheme theme)
        {
            RectTransform hover = CascadeUI.Rect("Hover", parent);
            CascadeUI.Stretch(hover);
            Color tint = theme != null && !theme.IsDark
                ? new Color(0f, 0f, 0f, 0.06f)
                : new Color(1f, 1f, 1f, 0.09f);
            Image image = CascadeUI.Image(hover, CascadeSprites.Rounded(radius), tint, Image.Type.Sliced);
            image.gameObject.SetActive(false);
            return image;
        }
    }
}
