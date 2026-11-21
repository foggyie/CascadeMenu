using System;
using System.Collections;
using System.Collections.Generic;
using CascadeMenu.Mods;
using GorillaNetworking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace CascadeMenu.UI
{
    internal sealed partial class CascadeWindow
    {
        private bool Matches(CascadeRow row)
        {
            if (string.IsNullOrEmpty(_searchQuery)) return true;
            string query = _searchQuery.ToLowerInvariant();
            return !string.IsNullOrEmpty(row.SearchText) && row.SearchText.ToLowerInvariant().Contains(query);
        }

        private void AddSectionHeader(CascadeSection section)
        {
            RectTransform header = CascadeUI.Rect("Section_" + section.Title, _content);
            bool hasDescription = !string.IsNullOrEmpty(section.Description);
            var layout = header.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = hasDescription ? 42f : 24f;

            RectTransform bar = CascadeUI.TopLeft("AccentBar", header, 2f, 3f, 3f, 15f);
            CascadeUI.Panel(bar, Theme.Accent, 2);

            RectTransform titleRect = LineRect("Title", header, 11f, 4f, 0f, 20f);
            CascadeUI.Text(titleRect, section.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);
            if (hasDescription)
            {
                RectTransform descriptionRect = LineRect("Description", header, 11f, 4f, 22f, 17f);
                CascadeUI.Text(descriptionRect, section.Description, 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);
            }
        }

        private RectTransform AddCard()
        {
            RectTransform card = CascadeUI.Rect("Card", _content);
            CascadeUI.Panel(card, Theme.ViewBg, 8);

            RectTransform border = CascadeUI.Rect("Border", card);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, Theme.ViewBorder, 8);
            var borderLayout = border.gameObject.AddComponent<LayoutElement>();
            borderLayout.ignoreLayout = true;

            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return card;
        }

        private void BuildRow(RectTransform card, CascadeRow row, bool last)
        {
            if (row is CascadeHeroRow hero)
            {
                BuildHeroRow(card, hero);
                return;
            }

            if (row is CascadePlayerRow playerRow)
            {
                BuildPlayerRow(card, playerRow);
                return;
            }

            if (row is CascadePortraitRow portraitRow)
            {
                BuildPortraitRow(card, portraitRow);
                return;
            }

            if (row is CascadeModRow modRow)
            {
                BuildModRow(card, modRow, last);
                return;
            }

            if (row is CascadePresetRow presetRow)
            {
                BuildPresetRow(card, presetRow);
                return;
            }

            if (row is CascadeTextFieldRow textRow)
            {
                BuildTextFieldRow(card, textRow);
                return;
            }

            if (row is CascadeSwatchRow swatchRow)
            {
                BuildSwatchRow(card, swatchRow);
                return;
            }

            if (row is CascadePreviewRow previewRow)
            {
                BuildPreviewRow(card, previewRow);
                return;
            }

            if (row is CascadeConsoleRow consoleRow)
            {
                BuildConsoleRow(card, consoleRow);
                return;
            }

            if (row is CascadeChipRow chipRow)
            {
                BuildChipRow(card, chipRow, last);
                return;
            }

            RectTransform root = CascadeUI.Rect("Row", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            RectTransform hoverRect = CascadeUI.Rect("Hover", root);
            CascadeUI.Stretch(hoverRect, 3f, 3f, 2f, 2f);
            Image hover = CascadeUI.Panel(hoverRect, Theme.RowHover, 6);
            hover.gameObject.SetActive(false);

            if (!last)
            {
                RectTransform separator = CascadeUI.Rect("Separator", root);
                separator.anchorMin = new Vector2(0f, 0f);
                separator.anchorMax = new Vector2(1f, 0f);
                separator.pivot = new Vector2(0.5f, 0f);
                separator.offsetMin = new Vector2(12f, 0f);
                separator.offsetMax = new Vector2(-12f, 1f);
                CascadeUI.Solid(separator, Theme.Separator);
            }

            float rightOffset = 14f;
            float leftOffset = 12f;
            bool sliderRow = false;
            bool toggleRow = false;

            if (row is CascadeToggleRow toggle)
            {
                toggleRow = true;
                CascadeToggleView view = CascadeWidgets.Toggle(Theme, root, toggle.Get());
                view.Get = toggle.Get;
                view.Background.color = toggle.Enabled
                    ? (toggle.Get() ? Theme.AccentSoft : Theme.ToggleOff)
                    : Theme.DisabledToggle;
                AnchorRightCenter(view.Root, 14f, 28f, 16f);
                rightOffset = 48f;

                CascadeInteractable toggleInteraction = Register(root, CascadeLayerGroup.Content, () =>
                {
                    if (!toggle.Enabled) return;
                    bool next = !toggle.Get();
                    toggle.Set(next);
                    view.Set(next);
                    if (toggle.Title == "ESP Box") _menu.QueueRebuild();
                }, hover: hovered => hover.gameObject.SetActive(hovered));
                toggleInteraction.Enabled = toggle.Enabled;

                // Optional gear button: opens the detailed settings page for the
                // module this toggle belongs to. Registered last so it sits above
                // the row-wide toggle click.
                if (toggle.OpenSettings != null)
                {
                    CascadeButtonView gear = CascadeWidgets.Button(this, root, string.Empty, false, toggle.OpenSettings);
                    AnchorRightCenter(gear.Root, 48f, 26f, 24f);
                    if (gear.Label != null) gear.Label.gameObject.SetActive(false);
                    RectTransform gearIcon = CascadeUI.Center("Gear", gear.Root, 0f, 0f, 14f, 14f);
                    CascadeUI.Image(gearIcon, CascadeIcons.Get(CascadeIcon.Gear), Theme.TextSecondary, Image.Type.Simple);
                    rightOffset = 84f;
                }
                else
                {
                    rightOffset = 48f;
                }
            }
            else if (row is CascadeSliderRow slider)
            {
                sliderRow = true;
                TMP_Text valueLabel;
                CascadeSliderView view = CascadeWidgets.Slider(
                    this, Theme, root, slider.Min, slider.Max, slider.Get, slider.Set, slider.Format, out valueLabel);
                RectTransform hit = view.Hit;
                hit.anchorMin = new Vector2(0f, 0f);
                hit.anchorMax = new Vector2(1f, 0f);
                hit.pivot = new Vector2(0.5f, 0f);
                hit.offsetMin = new Vector2(12f, 6f);
                hit.offsetMax = new Vector2(-92f, 26f);
                rightOffset = 92f;
            }
            else if (row is CascadeButtonRow buttonRow)
            {
                float buttonWidth = Mathf.Clamp(26f + buttonRow.ButtonText.Length * 6.8f, 76f, 140f);
                CascadeButtonView view = CascadeWidgets.Button(this, root, buttonRow.ButtonText, buttonRow.Primary, buttonRow.Click);
                AnchorRightCenter(view.Root, 12f, buttonWidth, 26f);
                rightOffset = buttonWidth + 22f;
            }
            else if (row is CascadeDualButtonRow dual)
            {
                float secondaryWidth = Mathf.Clamp(26f + dual.SecondaryText.Length * 6.8f, 58f, 96f);
                float primaryWidth = Mathf.Clamp(26f + dual.PrimaryText.Length * 6.8f, 64f, 116f);

                CascadeButtonView secondary = CascadeWidgets.Button(
                    this, root, dual.SecondaryText, false, dual.Secondary);
                AnchorRightCenter(secondary.Root, 12f, secondaryWidth, 26f);

                CascadeButtonView primary = CascadeWidgets.Button(
                    this, root, dual.PrimaryText, dual.PrimaryAccent, dual.Primary);
                AnchorRightCenter(primary.Root, 12f + secondaryWidth + 6f, primaryWidth, 26f);

                rightOffset = 12f + secondaryWidth + 6f + primaryWidth + 12f;
            }
            else if (row is CascadeCosmeticRow cosmetic)
            {
                const float iconSize = 42f;
                RectTransform iconRect = CascadeUI.Rect("Icon", root);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(12f, 0f);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                CascadeUI.Panel(iconRect, Theme.FieldBg, 6);

                if (cosmetic.Icon != null)
                {
                    RectTransform iconInner = CascadeUI.Rect("Sprite", iconRect);
                    CascadeUI.Stretch(iconInner, 3f, 3f, 3f, 3f);
                    Image iconImage = CascadeUI.Image(iconInner, cosmetic.Icon, Color.white, Image.Type.Simple);
                    iconImage.preserveAspect = true;
                }
                else
                {
                    RectTransform letterRect = CascadeUI.Rect("Letter", iconRect);
                    CascadeUI.Stretch(letterRect);
                    string letter = string.IsNullOrEmpty(cosmetic.Title)
                        ? "?"
                        : cosmetic.Title.Substring(0, 1).ToUpperInvariant();
                    CascadeUI.Text(letterRect, letter, 16f, Theme.TextSecondary,
                        TextAlignmentOptions.Center, true);
                }

                RectTransform dotRect = CascadeUI.Rect("Owned", iconRect);
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(1f, 0f);
                dotRect.pivot = new Vector2(1f, 0f);
                dotRect.anchoredPosition = new Vector2(3f, -3f);
                dotRect.sizeDelta = new Vector2(13f, 13f);
                CascadeColor dotColor = cosmetic.Owned ? Theme.Success : Theme.TextTertiary;
                CascadeUI.Panel(dotRect, dotColor, 6);

                leftOffset = 12f + iconSize + 10f;

                if (!cosmetic.Available)
                {
                    // Nothing to press: show a clear red warning instead.
                    RectTransform warnRect = CascadeUI.TopRight(
                        "Warning", root, 12f, (row.Height - 18f) * 0.5f, 220f, 18f);
                    CascadeUI.Text(warnRect, "Can't try on or buy", 10.5f,
                        Theme.Danger, TextAlignmentOptions.Right, true);
                    rightOffset = 244f;
                }
                else
                {
                    string cartText = cosmetic.InCart ? "Remove" : "Cart";
                    float secondaryWidth = Mathf.Clamp(26f + cartText.Length * 6.8f, 58f, 96f);
                    float primaryWidth = Mathf.Clamp(26f + 6 * 6.8f, 64f, 116f);

                    CascadeButtonView cartButton = CascadeWidgets.Button(
                        this, root, cartText, false, cosmetic.Cart);
                    AnchorRightCenter(cartButton.Root, 12f, secondaryWidth, 26f);

                    CascadeButtonView tryButton = CascadeWidgets.Button(
                        this, root, "Try on", true, cosmetic.TryOn);
                    AnchorRightCenter(tryButton.Root, 12f + secondaryWidth + 6f, primaryWidth, 26f);

                    rightOffset = 12f + secondaryWidth + 6f + primaryWidth + 12f;
                }
            }
            else if (row is CascadeStepperRow stepper)
            {
                CascadeStepperView view = CascadeWidgets.Stepper(
                    this, Theme, root, stepper.Min, stepper.Max, stepper.Get, stepper.Set, stepper.Format);
                AnchorRightCenter(view.Root, 12f, 88f, 26f);
                rightOffset = 110f;
            }
            else if (row is CascadeRadioRow radio)
            {
                CascadeRadioView view = CascadeWidgets.Radio(this, Theme, root, radio.Options, radio.Get, radio.Set);
                float width = radio.Options.Length * 88f + Mathf.Max(0, radio.Options.Length - 1) * 6f;
                AnchorRightCenter(view.Root, 12f, width, 24f);
                rightOffset = width + 22f;
            }
            else if (row is CascadeDropdownRow dropdown)
            {
                CascadeDropdownView view = CascadeWidgets.Dropdown(this, Theme, root, dropdown.Options, dropdown.Get, dropdown.Set);
                AnchorRightCenter(view.Root, 12f, 140f, 26f);
                rightOffset = 162f;
            }
            else if (row is CascadeLabelRow labelRow)
            {
                RectTransform valueRect = CascadeUI.TopRight("Value", root, 12f, (row.Height - 18f) * 0.5f, 150f, 18f);
                string value = labelRow.GetValue != null ? (labelRow.GetValue() ?? string.Empty) : labelRow.Value;
                TMP_Text valueLabel = CascadeUI.Text(valueRect, value, 11f, Theme.TextSecondary,
                    TextAlignmentOptions.Right);

                if (labelRow.GetValue != null)
                {
                    // Poll the value so toggling Active from a keybind updates
                    // this row without rebuilding the page.
                    var view = root.gameObject.AddComponent<CascadeLabelView>();
                    view.Label = valueLabel;
                    view.GetValue = labelRow.GetValue;
                    view.IsHighlighted = labelRow.IsHighlighted;
                    view.Theme = Theme;
                }

                rightOffset = 174f;
            }

            bool hasSubtitle = !string.IsNullOrEmpty(row.Subtitle);
            float blockHeight = hasSubtitle ? 41f : 22f;
            float top = sliderRow ? 3f : (row.Height - blockHeight) * 0.5f;

            RectTransform titleRect = LineRect("Title", root, leftOffset, rightOffset, top, 22f);
            CascadeUI.Text(titleRect, row.Title, 12.5f,
                row is CascadeToggleRow disabledToggle && !disabledToggle.Enabled ? Theme.TextTertiary : Theme.TextPrimary,
                TextAlignmentOptions.Left);

            if (hasSubtitle)
            {
                RectTransform subtitleRect = LineRect("Subtitle", root, leftOffset, rightOffset, top + 22f, 19f);
                CascadeUI.Text(subtitleRect, row.Subtitle, 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);
            }

            if (!toggleRow && !sliderRow)
            {
                hover.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// One auto-generated mod: name, live Active dot/status, Enabled switch,
        /// action button, Single/Double selector and keybind button.
        /// </summary>
        private void BuildModRow(RectTransform card, CascadeModRow row, bool last)
        {
            RectTransform root = CascadeUI.Rect("Mod", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            if (!last)
            {
                RectTransform separator = CascadeUI.Rect("Separator", root);
                separator.anchorMin = new Vector2(0f, 0f);
                separator.anchorMax = new Vector2(1f, 0f);
                separator.pivot = new Vector2(0.5f, 0f);
                separator.offsetMin = new Vector2(12f, 0f);
                separator.offsetMax = new Vector2(-12f, 1f);
                CascadeUI.Solid(separator, Theme.Separator);
            }

            RectTransform backgroundRect = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(backgroundRect, 3f, 3f, 2f, 2f);
            Image background = CascadeUI.Panel(backgroundRect, Color.clear, 6);

            RectTransform hoverRect = CascadeUI.Rect("Hover", root);
            CascadeUI.Stretch(hoverRect, 3f, 3f, 2f, 2f);
            Image hover = CascadeUI.Panel(hoverRect, Theme.RowHover, 6);
            hover.gameObject.SetActive(false);

            RectTransform dotRect = CascadeUI.Rect("ActiveDot", root);
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 1f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = new Vector2(17f, -16f);
            dotRect.sizeDelta = new Vector2(7f, 7f);
            Image dot = CascadeUI.Panel(dotRect, Theme.TextTertiary, 4);

            const float textLeft = 28f;
            float rightOffset = row.OpenSettings != null ? 92f : 58f;

            RectTransform titleRect = LineRect("Title", root, textLeft, rightOffset, 5f, 22f);
            TMP_Text title = CascadeUI.Text(
                titleRect, row.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);
            title.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform statusRect = LineRect("Status", root, textLeft, rightOffset, 27f, 18f);
            TMP_Text status = CascadeUI.Text(
                statusRect, string.Empty, 10f, Theme.TextSecondary, TextAlignmentOptions.Left);
            status.overflowMode = TextOverflowModes.Ellipsis;

            CascadeToggleView toggle = CascadeWidgets.Toggle(Theme, root, row.GetEnabled());
            toggle.Get = row.GetEnabled;
            AnchorRightCenter(toggle.Root, 14f, 28f, 16f);

            // A larger invisible hit area makes the tiny switch easy to press
            // with a VR trigger.
            RectTransform toggleHit = CascadeUI.Rect("ToggleHit", root);
            AnchorRightCenter(toggleHit, 6f, 46f, 32f);
            Register(toggleHit, CascadeLayerGroup.Content, () =>
                {
                    bool next = !row.GetEnabled();
                    row.SetEnabled(next);
                    toggle.Set(next);
                },
                hover: hovered => hover.gameObject.SetActive(hovered));

            // The row stays minimal: settings gear and Enabled switch only.
            // Keybind, press type and Active controls live in the gear page.
            if (row.OpenSettings != null)
            {
                CascadeButtonView gear = CascadeWidgets.Button(
                    this, root, string.Empty, false, row.OpenSettings);
                AnchorRightCenter(gear.Root, 56f, 26f, 24f);
                if (gear.Label != null) gear.Label.gameObject.SetActive(false);
                RectTransform gearIcon = CascadeUI.Center("Gear", gear.Root, 0f, 0f, 14f, 14f);
                CascadeUI.Image(gearIcon, CascadeIcons.Get(CascadeIcon.Gear), Theme.TextSecondary, Image.Type.Simple);
            }

            var view = root.gameObject.AddComponent<CascadeModView>();
            view.Dot = dot;
            view.Background = background;
            view.StatusLabel = status;
            view.GetEnabled = row.GetEnabled;
            view.GetActive = row.GetActive;
            view.GetKeyLabel = row.GetKeyLabel;
            view.IsToggle = row.IsToggle;
            view.Configure(Theme);
        }

        /// <summary>
        /// One preset theme: name, a miniature swatch of its palette and a Use
        /// button. Pressing the row or the button applies the theme live.
        /// </summary>
        private void BuildPresetRow(RectTransform card, CascadePresetRow row)
        {
            RectTransform root = CascadeUI.Rect("Preset", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            RectTransform hoverRect = CascadeUI.Rect("Hover", root);
            CascadeUI.Stretch(hoverRect, 3f, 3f, 2f, 2f);
            Image hover = CascadeUI.Panel(hoverRect, Theme.RowHover, 6);
            hover.gameObject.SetActive(false);

            const float swatchWidth = 18f;
            const float swatchGap = 3f;
            float stripWidth = row.Swatch.Length * swatchWidth +
                               Mathf.Max(0, row.Swatch.Length - 1) * swatchGap;

            RectTransform strip = CascadeUI.Rect("Swatch", root);
            strip.anchorMin = strip.anchorMax = new Vector2(1f, 0.5f);
            strip.pivot = new Vector2(1f, 0.5f);
            strip.anchoredPosition = new Vector2(-78f, 0f);
            strip.sizeDelta = new Vector2(stripWidth, swatchWidth);

            for (int i = 0; i < row.Swatch.Length; i++)
            {
                RectTransform chip = CascadeUI.Rect("Chip" + i, strip);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                chip.anchoredPosition = new Vector2(i * (swatchWidth + swatchGap), 0f);
                chip.sizeDelta = new Vector2(swatchWidth, swatchWidth);
                CascadeUI.Panel(chip, row.Swatch[i], 4);
            }

            CascadeButtonView use = CascadeWidgets.Button(this, root, "Use", false, row.Apply);
            AnchorRightCenter(use.Root, 12f, 56f, 24f);

            bool selected = row.IsSelected != null && row.IsSelected();
            RectTransform titleRect = LineRect("Title", root, 12f, stripWidth + 90f, 10f, 22f);
            TextMeshProUGUI titleLabel = CascadeUI.Text(titleRect, row.Title, 12.5f,
                selected ? Theme.Accent : Theme.TextPrimary, TextAlignmentOptions.Left, selected);

            RectTransform subtitleRect = LineRect("Subtitle", root, 12f, stripWidth + 90f, 31f, 16f);
            TextMeshProUGUI subtitleLabel = CascadeUI.Text(subtitleRect,
                selected ? "Selected - " + row.Subtitle : row.Subtitle,
                10f, selected ? Theme.AccentSoft : Theme.TextSecondary, TextAlignmentOptions.Left);

            // Keeps the selected marker and its colors in sync across a live
            // theme switch or a different preset being picked.
            var view = root.gameObject.AddComponent<CascadePresetView>();
            view.Title = titleLabel;
            view.Subtitle = subtitleLabel;
            view.BaseSubtitle = row.Subtitle;
            view.IsSelected = row.IsSelected;
            view.Theme = Theme;

            Register(root, CascadeLayerGroup.Content, row.Apply,
                hover: hovered => hover.gameObject.SetActive(hovered));
        }

        /// <summary>One editable text row; the field routes typing through the window.</summary>
        private void BuildTextFieldRow(RectTransform card, CascadeTextFieldRow row)
        {
            RectTransform root = CascadeUI.Rect("TextFieldRow", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            CascadeTextField field = CascadeWidgets.TextField(
                this, root, Theme, row.Placeholder, row.MaxLength, row.Get, row.Set);
            AnchorRightCenter(field.Root, 12f, 150f, 26f);

            bool hasSubtitle = !string.IsNullOrEmpty(row.Subtitle);
            float top = hasSubtitle ? 7f : (row.Height - 22f) * 0.5f;
            RectTransform titleRect = LineRect("Title", root, 12f, 174f, top, 22f);
            CascadeUI.Text(titleRect, row.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left);
            if (hasSubtitle)
            {
                RectTransform subtitleRect = LineRect("Subtitle", root, 12f, 174f, top + 21f, 17f);
                CascadeUI.Text(subtitleRect, row.Subtitle, 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);
            }
        }

        /// <summary>A row of color chips; tapping one writes that color.</summary>
        private void BuildSwatchRow(RectTransform card, CascadeSwatchRow row)
        {
            RectTransform root = CascadeUI.Rect("Swatches", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            const float size = 20f;
            const float gap = 5f;
            int count = row.Colors.Length;
            float width = count * size + Mathf.Max(0, count - 1) * gap;

            RectTransform strip = CascadeUI.Rect("Strip", root);
            strip.anchorMin = strip.anchorMax = new Vector2(1f, 0.5f);
            strip.pivot = new Vector2(1f, 0.5f);
            strip.anchoredPosition = new Vector2(-12f, 0f);
            strip.sizeDelta = new Vector2(width, size);

            for (int i = 0; i < count; i++)
            {
                int index = i;
                RectTransform chip = CascadeUI.Rect("Chip" + i, strip);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                chip.anchoredPosition = new Vector2(i * (size + gap), 0f);
                chip.sizeDelta = new Vector2(size, size);
                CascadeUI.Panel(chip, row.Colors[i], 5);

                // The outline lives on a child so the chip keeps exactly one
                // Graphic. Adding it to the chip would fail and reintroduce the
                // theme crash.
                RectTransform chipBorder = CascadeUI.Rect("Border", chip);
                CascadeUI.Stretch(chipBorder);
                CascadeUI.Outline(chipBorder, Theme.FieldBorder, 5);

                RectTransform hoverRect = CascadeUI.Rect("Hover", chip);
                CascadeUI.Stretch(hoverRect);
                Image hover = CascadeUI.Image(hoverRect, CascadeSprites.Rounded(5), Theme.HoverTint, Image.Type.Sliced);
                hover.gameObject.SetActive(false);

                Register(chip, CascadeLayerGroup.Content, () => row.Pick?.Invoke(index),
                    hover: h => hover.gameObject.SetActive(h));
            }

            bool hasSubtitle = !string.IsNullOrEmpty(row.Subtitle);
            float top = hasSubtitle ? 7f : (row.Height - 22f) * 0.5f;
            RectTransform titleRect = LineRect("Title", root, 12f, width + 24f, top, 22f);
            CascadeUI.Text(titleRect, row.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left);
            if (hasSubtitle)
            {
                RectTransform subtitleRect = LineRect("Subtitle", root, 12f, width + 24f, top + 21f, 17f);
                CascadeUI.Text(subtitleRect, row.Subtitle, 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);
            }
        }

        /// <summary>
        /// The theme preview. Every piece is created with a role color, so the
        /// binding cache repaints it automatically on a live theme change.
        /// </summary>
        private void BuildPreviewRow(RectTransform card, CascadePreviewRow row)
        {
            RectTransform root = CascadeUI.Rect("Preview", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            RectTransform titleRect = LineRect("Title", root, 12f, 12f, 4f, 18f);
            CascadeUI.Text(titleRect, row.Title, 11.5f, Theme.TextSecondary, TextAlignmentOptions.Left, true);

            RectTransform preview = CascadeUI.Rect("Window", root);
            preview.anchorMin = new Vector2(0f, 0f);
            preview.anchorMax = new Vector2(1f, 1f);
            preview.pivot = new Vector2(0.5f, 0.5f);
            preview.offsetMin = new Vector2(12f, 8f);
            preview.offsetMax = new Vector2(-12f, -26f);
            CascadeUI.Panel(preview, Theme.Background, 8);
            RectTransform previewBorder = CascadeUI.Rect("Border", preview);
            CascadeUI.Stretch(previewBorder);
            CascadeUI.Outline(previewBorder, Theme.Border, 8);

            RectTransform sidebar = CascadeUI.Rect("Panel", preview);
            sidebar.anchorMin = new Vector2(0f, 0f);
            sidebar.anchorMax = new Vector2(0f, 1f);
            sidebar.pivot = new Vector2(0f, 0.5f);
            sidebar.sizeDelta = new Vector2(96f, 0f);
            CascadeUI.Panel(sidebar, Theme.Panel, 8);

            RectTransform header = CascadeUI.Rect("Header", preview);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -20f);
            header.offsetMax = new Vector2(0f, 0f);
            CascadeUI.Panel(header, Theme.Header, 8);

            RectTransform text = CascadeUI.Rect("Text", sidebar);
            CascadeUI.Stretch(text, 10f, 10f, 8f, 8f);
            CascadeUI.Text(text, "Aa", 15f, Theme.Text, TextAlignmentOptions.Left, true);

            RectTransform secondary = CascadeUI.Rect("Secondary", preview);
            secondary.anchorMin = new Vector2(0f, 1f);
            secondary.anchorMax = new Vector2(1f, 1f);
            secondary.pivot = new Vector2(0.5f, 1f);
            secondary.offsetMin = new Vector2(108f, -40f);
            secondary.offsetMax = new Vector2(-12f, -24f);
            CascadeUI.Text(secondary, "Secondary text", 9.5f, Theme.SecondaryText, TextAlignmentOptions.Left);

            RectTransform button = CascadeUI.Rect("Button", preview);
            button.anchorMin = button.anchorMax = new Vector2(1f, 0f);
            button.pivot = new Vector2(1f, 0f);
            button.anchoredPosition = new Vector2(-12f, 9f);
            button.sizeDelta = new Vector2(58f, 18f);
            CascadeUI.Panel(button, Theme.ButtonSecondaryTop, 5);

            RectTransform accent = CascadeUI.Rect("Accent", preview);
            accent.anchorMin = accent.anchorMax = new Vector2(0f, 0f);
            accent.pivot = new Vector2(0f, 0f);
            accent.anchoredPosition = new Vector2(108f, 9f);
            accent.sizeDelta = new Vector2(44f, 18f);
            CascadeUI.Panel(accent, Theme.Accent, 5);

            RectTransform active = CascadeUI.Rect("Active", preview);
            active.anchorMin = active.anchorMax = new Vector2(0f, 0f);
            active.pivot = new Vector2(0f, 0f);
            active.anchoredPosition = new Vector2(160f, 14f);
            active.sizeDelta = new Vector2(8f, 8f);
            CascadeUI.Panel(active, Theme.ActiveColor, 4);
        }

        // -----------------------------------------------------------------
        // Debug console row
        // -----------------------------------------------------------------

        /// <summary>
        /// A tall dark panel containing a scrollable rich-text log view.
        /// The text is polled via <see cref="CascadeConsoleRow.GetText"/> so it
        /// updates live while the page is visible.
        /// </summary>
        private void BuildConsoleRow(RectTransform card, CascadeConsoleRow row)
        {
            RectTransform wrapper = CascadeUI.Rect("ConsoleRow", card);
            var wLayout = wrapper.gameObject.AddComponent<LayoutElement>();
            wLayout.preferredHeight = row.Height;
            wLayout.flexibleWidth   = 1f;

            // dark background panel
            var bgColor   = CascadeMenu.Mods.Tools.DebugConsole.ColBg;
            var bordColor = CascadeMenu.Mods.Tools.DebugConsole.ColBorder;
            CascadeUI.Image(wrapper, CascadeSprites.Rounded(6), bgColor, Image.Type.Sliced);
            RectTransform bord = CascadeUI.Rect("Border", wrapper);
            CascadeUI.Stretch(bord);
            CascadeUI.Outline(bord, bordColor, 6);

            // inner scroll
            RectTransform scrollRoot = CascadeUI.Rect("Scroll", wrapper);
            CascadeUI.Stretch(scrollRoot, 4f, 4f, 4f, 4f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal    = false;
            scroll.vertical      = true;
            scroll.movementType  = ScrollRect.MovementType.Clamped;
            scroll.inertia       = false;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = CascadeUI.Rect("Viewport", scrollRoot);
            CascadeUI.Stretch(viewport);
            CascadeUI.Clip(viewport);
            scroll.viewport = viewport;

            RectTransform content = CascadeUI.Rect("Content", viewport);
            content.anchorMin        = new Vector2(0f, 1f);
            content.anchorMax        = new Vector2(1f, 1f);
            content.pivot            = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta        = Vector2.zero;

            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding              = new RectOffset(6, 6, 4, 4);
            vl.spacing              = 0f;
            vl.childAlignment       = TextAnchor.UpperLeft;
            vl.childControlWidth    = true;
            vl.childControlHeight   = true;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;

            var cFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            cFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            // text
            var textGo = new GameObject("LogText", typeof(RectTransform));
            textGo.transform.SetParent(content, false);
            var tLayout = textGo.AddComponent<LayoutElement>();
            tLayout.flexibleWidth = 1f;

            var txt = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            txt.font           = CascadeUI.Font;
            txt.fontSize       = 9f;
            txt.color          = Color.white;
            txt.alignment      = TMPro.TextAlignmentOptions.TopLeft;
            txt.overflowMode   = TMPro.TextOverflowModes.Overflow;
            txt.textWrappingMode = TMPro.TextWrappingModes.Normal;
            txt.richText       = true;
            txt.text           = "<color=#444444>Waiting for log entries…</color>";

            var tFitter = textGo.AddComponent<ContentSizeFitter>();
            tFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // subscribe – refresh and scroll to bottom on new entry
            Action refresh = () =>
            {
                if (txt == null) return;
                txt.text = row.GetText();
                Canvas.ForceUpdateCanvases();
                if (scroll != null) scroll.verticalNormalizedPosition = 0f;
            };

            var binder = textGo.AddComponent<CascadeMenu.Mods.Tools.DebugConsoleBinder>();
            binder.Text = txt;
            binder.Scroll = scroll;
            binder.GetText = row.GetText;

            // first paint
            refresh();

            // swallow pointer events so the scroll works
            Register(scrollRoot, CascadeLayerGroup.Content, () => { });
        }

        /// <summary>
        /// Renders one live player: name + short ID on the left, TAP badge and the
        /// per-player action buttons on the right. The whole left area opens the
        /// player page.
        /// </summary>
        private void BuildPlayerRow(RectTransform card, CascadePlayerRow row)        {
            RectTransform root = CascadeUI.Rect("Player", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            RectTransform backgroundRect = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(backgroundRect, 2f, 2f, 0f, 0f);
            Image background = CascadeUI.Panel(backgroundRect, Color.white, 8);

            RectTransform barRect = CascadeUI.Rect("Selection", root);
            barRect.anchorMin = new Vector2(0f, 0.5f);
            barRect.anchorMax = new Vector2(0f, 0.5f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.anchoredPosition = new Vector2(4f, 0f);
            barRect.sizeDelta = new Vector2(3f, row.Height - 18f);
            Image bar = CascadeUI.Panel(barRect, Theme.Accent, 2);

            RectTransform hoverRect = CascadeUI.Rect("Hover", root);
            CascadeUI.Stretch(hoverRect, 2f, 2f, 0f, 0f);
            Image hover = CascadeUI.Panel(hoverRect, Theme.RowHover, 8);
            hover.gameObject.SetActive(false);

            // --- Live 3D portrait of the real rig ----------------------------
            // The tinted face is only a placeholder until the first rendered
            // frame lands (or while the player's rig is not spawned).
            Color avatarColor = row.GetColor != null ? row.GetColor() : Color.white;
            RectTransform avatarRect = CascadeUI.Rect("Avatar", root);
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(9f, 0f);
            avatarRect.sizeDelta = new Vector2(44f, 44f);
            CascadeUI.Image(avatarRect, CascadeSprites.Circle(),
                Theme.AvatarBg,
                Image.Type.Simple);

            RectTransform faceRect = CascadeUI.Rect("Face", avatarRect);
            CascadeUI.Stretch(faceRect, 4f, 4f, 4f, 4f);
            Image avatar = CascadeUI.Image(faceRect, CascadeSprites.GorillaFace(), avatarColor, Image.Type.Simple);

            RectTransform liveRect = CascadeUI.Rect("Live", avatarRect);
            CascadeUI.Stretch(liveRect, 1f, 1f, 1f, 1f);
            var liveImage = liveRect.gameObject.AddComponent<RawImage>();
            liveImage.raycastTarget = false;
            liveImage.color = Color.white;

            var portrait = avatarRect.gameObject.AddComponent<CascadeRigPortrait>();
            portrait.Configure(liveImage, avatar, row.GetRig, row.GetColor);

            // --- Buttons, laid out from the right edge ---------------------
            float right = 8f;

            string muteText = row.IsMuted != null && row.IsMuted() ? "Unmute" : "Mute";
            CascadeButtonView muteButton = AddRowButton(root, muteText, ref right, row.ToggleMute, "Unmute");
            AddRowButton(root, "Report", ref right, row.Report);
            AddRowButton(root, "ID", ref right, row.CopyId);
            AddRowButton(root, "Name", ref right, row.CopyName);

            // TAP badge sits to the left of the buttons.
            RectTransform tapRect = CascadeUI.Rect("Tap", root);
            tapRect.anchorMin = tapRect.anchorMax = new Vector2(1f, 0.5f);
            tapRect.pivot = new Vector2(1f, 0.5f);
            tapRect.anchoredPosition = new Vector2(-right, 0f);
            tapRect.sizeDelta = new Vector2(40f, 18f);
            CascadeUI.Image(tapRect, CascadeSprites.Rounded(4), Theme.TapBadgeBg, Image.Type.Sliced);
            RectTransform tapText = CascadeUI.Rect("Text", tapRect);
            CascadeUI.Stretch(tapText, 2f, 2f, 0f, 0f);
            CascadeUI.Text(tapText, "TAP", 8f, Theme.TapBadgeText, TextAlignmentOptions.Center, true);
            tapRect.gameObject.SetActive(false);
            right += 46f;

            // --- Name and ID -------------------------------------------------
            const float textLeft = 62f;
            RectTransform nameRect = LineRect("Name", root, textLeft, right, 9f, 22f);
            TMP_Text nameLabel = CascadeUI.Text(
                nameRect, row.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform idRect = LineRect("Id", root, textLeft, right, 33f, 18f);
            TMP_Text idLabel = CascadeUI.Text(
                idRect, row.IdDisplay, 9.5f, Theme.TextSecondary, TextAlignmentOptions.Left);
            idLabel.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform openRect = CascadeUI.Rect("Open", root);
            openRect.anchorMin = new Vector2(0f, 0f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.offsetMin = new Vector2(6f, 4f);
            openRect.offsetMax = new Vector2(-(right + 6f), -4f);
            Register(openRect, CascadeLayerGroup.Content, row.Open, hover: hovered => hover.gameObject.SetActive(hovered));

            var view = root.gameObject.AddComponent<CascadePlayerView>();
            view.Background = background;
            view.SelectionBar = bar;
            view.Avatar = avatar;
            view.GetColor = row.GetColor;
            view.TapBadge = tapRect.gameObject;
            view.NameLabel = nameLabel;
            view.MuteLabel = muteButton != null ? muteButton.Label : null;
            view.Configure(Theme, row.IsSelected, row.IsTapping, row.IsMuted);
        }

        /// <summary>
        /// Large live portrait (the player's actual rig) beside their name, shown
        /// at the top of a player's page.
        /// </summary>
        private void BuildPortraitRow(RectTransform card, CascadePortraitRow row)
        {
            RectTransform root = CascadeUI.Rect("Portrait", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            RectTransform backgroundRect = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(backgroundRect, 2f, 2f, 0f, 0f);
            CascadeUI.Panel(backgroundRect, Theme.RowHover, 8);

            RectTransform frameRect = CascadeUI.Rect("Live", root);
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0f, 0.5f);
            frameRect.pivot = new Vector2(0f, 0.5f);
            frameRect.anchoredPosition = new Vector2(16f, 0f);
            frameRect.sizeDelta = new Vector2(128f, 128f);
            CascadeUI.Panel(frameRect, Theme.FieldBg, 14);

            RectTransform viewRect = CascadeUI.Rect("View", frameRect);
            CascadeUI.Stretch(viewRect, 4f, 4f, 4f, 4f);
            var view = viewRect.gameObject.AddComponent<RawImage>();
            view.raycastTarget = false;
            view.color = Color.white;

            var portrait = frameRect.gameObject.AddComponent<CascadeRigPortrait>();
            portrait.Configure(view, null, row.GetRig, row.GetColor);

            RectTransform titleRect = LineRect("Name", root, 164f, 16f, 30f, 30f);
            TMP_Text title = CascadeUI.Text(
                titleRect, row.Title, 17f, Theme.TextPrimary, TextAlignmentOptions.Left, true);
            title.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform subtitleRect = LineRect("Subtitle", root, 164f, 16f, 66f, 24f);
            TMP_Text subtitle = CascadeUI.Text(
                subtitleRect, row.Subtitle, 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);
            subtitle.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform hintRect = LineRect("Hint", root, 164f, 16f, 100f, 20f);
            CascadeUI.Text(hintRect, "Live rig - moves as they move", 9.5f, Theme.TextTertiary,
                TextAlignmentOptions.Left);
        }

        /// <summary>
        /// Adds one compact action button to a player row. <paramref name="minText"/>
        /// reserves room for the widest label the button can show (e.g. Mute/Unmute).
        /// </summary>
        private CascadeButtonView AddRowButton(
            RectTransform root,
            string text,
            ref float right,
            Action click,
            string minText = null)
        {
            float width = Mathf.Clamp(18f + text.Length * 6.6f, 40f, 92f);
            if (!string.IsNullOrEmpty(minText))
            {
                width = Mathf.Max(width, Mathf.Clamp(18f + minText.Length * 6.6f, 40f, 92f));
            }

            CascadeButtonView view = CascadeWidgets.Button(this, root, text, false, click);
            AnchorRightCenter(view.Root, right, width, 24f);
            right += width + 6f;
            return view;
        }

        private void BuildHeroRow(RectTransform card, CascadeHeroRow row)
        {
            RectTransform root = CascadeUI.Rect("Hero", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

            RectTransform background = CascadeUI.Rect("Background", root);
            CascadeUI.Stretch(background);
            Image backgroundImage = CascadeUI.Panel(background, Color.white, 8);
            var gradient = background.gameObject.AddComponent<CascadeGradient>();
            gradient.Top = Theme.Accent.WithAlpha(0.13f);
            gradient.Bottom = Theme.Accent.WithAlpha(0.01f);

            RectTransform iconRect = CascadeUI.TopLeft("Icon", root, 12f, 15f, 46f, 46f);
            Image iconBackground = CascadeUI.Image(iconRect, CascadeSprites.Rounded(12), Color.white, Image.Type.Sliced);
            var iconGradient = iconRect.gameObject.AddComponent<CascadeGradient>();
            iconGradient.Top = Theme.ButtonPrimaryTop;
            iconGradient.Bottom = Theme.ButtonPrimaryBottom;

            RectTransform icon = CascadeUI.Center("Glyph", iconRect, 0f, 0f, 24f, 24f);
            CascadeUI.Image(icon, CascadeIcons.Get(row.Icon), Color.white, Image.Type.Simple);

            RectTransform titleRect = LineRect("Title", root, 70f, 100f, 8f, 30f);
            CascadeUI.Text(titleRect, row.Title, 17.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);

            RectTransform subtitleRect = LineRect("Subtitle", root, 70f, 100f, 39f, 20f);
            CascadeUI.Text(subtitleRect, row.Subtitle, 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);

            RectTransform pill = CascadeUI.TopRight("Value", root, 12f, 27f, 64f, 22f);
            CascadeUI.Image(pill, CascadeSprites.Pill(), Theme.FieldBg, Image.Type.Simple);
            RectTransform pillText = CascadeUI.Rect("Text", pill);
            CascadeUI.Stretch(pillText, 4f, 4f, 0f, 0f);
            CascadeUI.Text(pillText, row.Value, 10f, Theme.TextSecondary, TextAlignmentOptions.Center, true);
        }

        private void AnchorRightCenter(RectTransform rect, float right, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-right, 0f);
            rect.sizeDelta = new Vector2(width, height);
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
    }
}
