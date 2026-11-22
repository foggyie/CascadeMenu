using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Tabs inside tabs: pages with several sections get a chip bar at the top
    /// ("All" plus one chip per section). The choice is remembered per page and
    /// survives window rebuilds. Also hosts the sidebar icon mapping.
    /// </summary>
    internal sealed partial class CascadeWindow
    {
        private const int SubTabAll = -1;
        private const int MinSectionsForSubTabs = 3;

        // Static so the selection survives a rebuilt window.
        private static readonly Dictionary<string, int> SubTabState = new Dictionary<string, int>();

        private static bool UsesSubTabs(CascadePage page)
        {
            if (page == null || page.Sections.Count < MinSectionsForSubTabs) return false;

            switch (page.Title)
            {
                // Live or dynamic lists read better as one continuous page.
                case "Players":
                case "Shop":
                case "Cosmetics":
                    return false;
                default:
                    return true;
            }
        }

        private static int GetSubTab(CascadePage page)
        {
            int index;
            if (!SubTabState.TryGetValue(page.Title, out index)) return SubTabAll;
            return index >= 0 && index < page.Sections.Count ? index : SubTabAll;
        }

        private void BuildSubTabBar(CascadePage page, int selected)
        {
            _activeSubTabLine = null;
            _activeSubTabGlow = null;
            bool pillStyle = _config != null && _config.SubTabStyle.Value == 1;
            bool showIcons = _config == null || _config.SubTabShowIcons.Value;

            float maxWidth = _root.sizeDelta.x
                - (_sidebarExpanded ? CascadeTheme.SidebarWidth : 0f) - 44f;
            bool compact = maxWidth < 500f;

            RectTransform bar = CascadeUI.Rect("SubTabs", _content);
            var barLayout = bar.gameObject.AddComponent<VerticalLayoutGroup>();
            barLayout.spacing = pillStyle ? 4f : 2f;
            barLayout.padding = pillStyle ? new RectOffset(2, 2, 0, 2) : new RectOffset(2, 2, 0, 4);
            barLayout.childAlignment = TextAnchor.UpperLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.childForceExpandHeight = false;

            RectTransform row = null;
            float used = 0f;
            float spacing = pillStyle ? 5f : (compact ? 7f : 12f);
            float charWidth = compact ? 5.9f : 6.4f;

            for (int i = SubTabAll; i < page.Sections.Count; i++)
            {
                string label = i == SubTabAll ? "All" : SubTabLabel(page.Sections[i], i);
                float iconExtra = (!pillStyle && showIcons) ? (compact ? 16f : 18f) : 0f;
                float padExtra = pillStyle ? 22f : (compact ? 10f : 14f);
                float width = padExtra + iconExtra + label.Length * charWidth;

                if (row == null || used + width > maxWidth)
                {
                    row = NewSubTabRow(bar, pillStyle, spacing);
                    used = 0f;
                }

                if (pillStyle)
                {
                    AddSubTabChip(row, page, i, label, width, i == selected);
                }
                else
                {
                    AddSubTabUnderlineItem(row, page, i, label, width, i == selected, showIcons, compact);
                }
                used += width + spacing;
            }

            if (!pillStyle)
            {
                // Sleek full-width baseline divider beneath the underline sub-tab bar
                RectTransform divider = CascadeUI.Rect("SubTabDivider", bar);
                var divEl = divider.gameObject.AddComponent<LayoutElement>();
                divEl.preferredHeight = 1f;
                divEl.minHeight = 1f;
                Image divImg = CascadeUI.Solid(divider, Theme.Separator);
                if (divImg != null) divImg.raycastTarget = false;
            }
        }

        private static string SubTabLabel(CascadeSection section, int index)
        {
            return string.IsNullOrEmpty(section.Title) ? "Section " + (index + 1) : section.Title;
        }

        private static RectTransform NewSubTabRow(RectTransform bar, bool pillStyle, float spacing)
        {
            RectTransform row = CascadeUI.Rect("ChipRow", bar);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = spacing;
            rowLayout.childAlignment = TextAnchor.LowerLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            float height = pillStyle ? 24f : 31f;
            var rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = height;
            rowElement.minHeight = height;
            return row;
        }

        private void AddSubTabUnderlineItem(
            RectTransform row,
            CascadePage page,
            int index,
            string label,
            float width,
            bool selected,
            bool showIcons,
            bool compact)
        {
            RectTransform item = CascadeUI.Rect("SubTab_" + label, row);
            var element = item.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = 31f;
            element.minHeight = 31f;

            // Subtle hover backdrop
            RectTransform hoverRect = CascadeUI.Rect("Hover", item);
            CascadeUI.Stretch(hoverRect, 0f, 0f, 1f, 4f);
            Image hoverBg = CascadeUI.Image(hoverRect, CascadeSprites.Rounded(6), Theme.TabHover.WithAlpha(0.45f), Image.Type.Sliced);
            if (hoverBg != null)
            {
                hoverBg.raycastTarget = false;
                hoverBg.gameObject.SetActive(false);
            }

            float textLeft = compact ? 5f : 6f;
            Image iconImage = null;
            if (showIcons)
            {
                float iconSize = compact ? 12f : 13.5f;
                RectTransform iconRect = CascadeUI.Rect("Icon", item);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(compact ? 4f : 6f, 1.5f);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                iconImage = CascadeUI.Image(
                    iconRect,
                    CascadeIcons.Get(ResolveSubTabIcon(label, index)),
                    selected ? Theme.TextPrimary : Theme.TextSecondary,
                    Image.Type.Simple);
                if (iconImage != null) iconImage.raycastTarget = false;
                textLeft = compact ? 20f : 24f;
            }

            RectTransform labelRect = CascadeUI.Rect("Label", item);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(textLeft, 4f);
            labelRect.offsetMax = new Vector2(-3f, -1f);
            TMP_Text text = CascadeUI.Text(
                labelRect,
                label,
                compact ? 10.2f : 11f,
                selected ? Theme.TextPrimary : Theme.TextSecondary,
                TextAlignmentOptions.Left,
                selected);
            if (text != null)
            {
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
            }

            // Active color underline bar + soft glow mist right above the line
            if (selected)
            {
                Color barColor = ResolveSubTabLineColor();

                RectTransform glowRect = CascadeUI.Rect("ActiveLineGlow", item);
                glowRect.anchorMin = new Vector2(0f, 0f);
                glowRect.anchorMax = new Vector2(1f, 0f);
                glowRect.pivot = new Vector2(0.5f, 0f);
                glowRect.offsetMin = new Vector2(-4f, 0f);
                glowRect.offsetMax = new Vector2(4f, 15f);
                Image glowImg = CascadeUI.Image(glowRect, CascadeSprites.OrbAura(128), new Color(barColor.r, barColor.g, barColor.b, 0.34f), Image.Type.Simple);
                if (glowImg != null)
                {
                    glowImg.raycastTarget = false;
                    _activeSubTabGlow = glowImg;
                }

                RectTransform lineRect = CascadeUI.Rect("ActiveLine", item);
                lineRect.anchorMin = new Vector2(0f, 0f);
                lineRect.anchorMax = new Vector2(1f, 0f);
                lineRect.pivot = new Vector2(0.5f, 0f);
                lineRect.offsetMin = new Vector2(2f, 0f);
                lineRect.offsetMax = new Vector2(-2f, 2.5f);

                Image lineImg = CascadeUI.Image(lineRect, CascadeSprites.Rounded(2), barColor, Image.Type.Sliced);
                if (lineImg != null)
                {
                    lineImg.raycastTarget = false;
                    _activeSubTabLine = lineImg;
                    UpdateSubTabLineColor(true);
                }
            }

            string pageTitle = page.Title;
            Register(item, CascadeLayerGroup.Content, () =>
            {
                SubTabState[pageTitle] = index;
                RebuildContent();
            }, hover: hovered =>
            {
                if (selected) return;
                if (hoverBg != null) hoverBg.gameObject.SetActive(hovered);
                Color col = hovered ? (Color)Theme.TextPrimary : (Color)Theme.TextSecondary;
                if (text != null) text.color = col;
                if (iconImage != null) iconImage.color = col;
            });
        }

        private Color ResolveSubTabLineColor()
        {
            bool useAccent = _config == null || _config.SubTabLineUseAccent.Value;
            if (useAccent)
            {
                return Theme != null ? (Color)Theme.Accent : new Color(0.20f, 0.60f, 1f, 1f);
            }

            string hex = _config.SubTabLineColor.Value;
            if (!string.IsNullOrEmpty(hex))
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                if (ColorUtility.TryParseHtmlString(hex, out Color parsed))
                {
                    parsed.a = 1f;
                    return parsed;
                }
            }
            return new Color(1f, 0.42f, 0.42f, 1f);
        }

        private void UpdateSubTabLineColor(bool force)
        {
            if (_activeSubTabLine == null) return;

            bool useAccent = _config == null || _config.SubTabLineUseAccent.Value;
            string hex = _config != null ? (_config.SubTabLineColor.Value ?? string.Empty) : string.Empty;
            int themeVer = Theme != null ? Theme.Version : -1;

            if (!force &&
                useAccent == _lastSubTabLineUseAccent &&
                themeVer == _lastSubTabLineThemeVer &&
                string.Equals(hex, _lastSubTabLineHex, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastSubTabLineUseAccent = useAccent;
            _lastSubTabLineThemeVer = themeVer;
            _lastSubTabLineHex = hex;
            Color barCol = ResolveSubTabLineColor();
            _activeSubTabLine.color = barCol;
            if (_activeSubTabGlow != null)
            {
                _activeSubTabGlow.color = new Color(barCol.r, barCol.g, barCol.b, 0.34f);
            }
        }

        private static CascadeIcon ResolveSubTabIcon(string label, int index)
        {
            if (index == SubTabAll) return CascadeIcon.Layers;
            if (string.IsNullOrEmpty(label)) return CascadeIcon.Star;

            string key = label.ToLowerInvariant();
            if (key.Contains("general"))
                return CascadeIcon.Gear;
            if (key.Contains("appear") || key.Contains("theme") || key.Contains("color") || key.Contains("preset"))
                return CascadeIcon.Palette;
            if (key.Contains("backg") || key.Contains("glass") || key.Contains("wall") || key.Contains("image"))
                return CascadeIcon.Monitor;
            if (key.Contains("orb") || key.Contains("glow") || key.Contains("aura"))
                return CascadeIcon.Sparkle;
            if (key.Contains("sub-tab") || key.Contains("subtab") || key.Contains("tab"))
                return CascadeIcon.Sidebar;
            if (key.Contains("place") || key.Contains("move") || key.Contains("flight") || key.Contains("pos"))
                return CascadeIcon.Move;
            if (key.Contains("notif") || key.Contains("alert"))
                return CascadeIcon.Bell;
            if (key.Contains("hud") || key.Contains("visual") || key.Contains("esp") || key.Contains("tag") || key.Contains("preview"))
                return CascadeIcon.Eye;
            if (key.Contains("talk") || key.Contains("voice") || key.Contains("audio") || key.Contains("sound") || key.Contains("music"))
                return CascadeIcon.Speaker;
            if (key.Contains("player") || key.Contains("rig") || key.Contains("lobby"))
                return CascadeIcon.Person;
            if (key.Contains("key") || key.Contains("bind") || key.Contains("control") || key.Contains("input"))
                return CascadeIcon.Keyboard;
            if (key.Contains("debug") || key.Contains("console") || key.Contains("log"))
                return CascadeIcon.Terminal;
            if (key.Contains("perf") || key.Contains("stat") || key.Contains("fps") || key.Contains("profil") || key.Contains("status"))
                return CascadeIcon.Chart;
            if (key.Contains("construct") || key.Contains("sandbox") || key.Contains("block") || key.Contains("prop") || key.Contains("build"))
                return CascadeIcon.Cube;
            if (key.Contains("config") || key.Contains("bepinex") || key.Contains("backup") || key.Contains("file") || key.Contains("manage"))
                return CascadeIcon.Folder;
            if (key.Contains("window") || key.Contains("desktop") || key.Contains("screen"))
                return CascadeIcon.Monitor;
            if (key.Contains("fun") || key.Contains("toy") || key.Contains("quick") || key.Contains("game"))
                return CascadeIcon.Sparkle;
            if (key.Contains("tool") || key.Contains("util") || key.Contains("gun"))
                return CascadeIcon.Wrench;
            if (key.Contains("module") || key.Contains("slider") || key.Contains("tune"))
                return CascadeIcon.Sliders;
            if (key.Contains("safe") || key.Contains("guard") || key.Contains("protect"))
                return CascadeIcon.Shield;
            if (key.Contains("info") || key.Contains("about"))
                return CascadeIcon.Info;
            return CascadeIcon.Gear;
        }

        private void AddSubTabChip(RectTransform row, CascadePage page, int index, string label, float width, bool selected)
        {
            RectTransform chip = CascadeUI.Rect("Chip_" + label, row);
            var element = chip.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = 24f;
            element.minHeight = 24f;

            Image background = CascadeUI.Image(
                chip,
                CascadeSprites.Rounded(12),
                selected ? Theme.Accent : Theme.FieldBg,
                Image.Type.Sliced);

            RectTransform labelRect = CascadeUI.Rect("Label", chip);
            CascadeUI.Stretch(labelRect, 4f, 4f, 0f, 0f);
            TMP_Text text = CascadeUI.Text(
                labelRect,
                label,
                10.5f,
                selected ? Theme.SelectionText : Theme.TextSecondary,
                TextAlignmentOptions.Center,
                selected);

            string pageTitle = page.Title;
            Register(chip, CascadeLayerGroup.Content, () =>
            {
                SubTabState[pageTitle] = index;
                RebuildContent();
            }, hover: hovered =>
            {
                if (selected) return;
                background.color = hovered ? (Color)Theme.TabHover : (Color)Theme.FieldBg;
                text.color = hovered ? (Color)Theme.TextPrimary : (Color)Theme.TextSecondary;
            });
        }

        // -----------------------------------------------------------------
        // Chip row (used by the debug console filters)
        // -----------------------------------------------------------------

        private void BuildChipRow(RectTransform card, CascadeChipRow row, bool last)
        {
            RectTransform root = CascadeUI.Rect("ChipsRow", card);
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = row.Height;
            element.minHeight = row.Height;

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

            RectTransform titleRect = CascadeUI.TopLeft("Title", root, 12f, 10f, 74f, 18f);
            CascadeUI.Text(titleRect, row.Title, 11.5f, Theme.TextPrimary, TextAlignmentOptions.Left);

            RectTransform strip = CascadeUI.Rect("Chips", root);
            strip.anchorMin = Vector2.zero;
            strip.anchorMax = Vector2.one;
            strip.offsetMin = new Vector2(88f, 6f);
            strip.offsetMax = new Vector2(-10f, -6f);
            var stripLayout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            stripLayout.spacing = 4f;
            stripLayout.childAlignment = TextAnchor.MiddleLeft;
            stripLayout.childControlWidth = true;
            stripLayout.childControlHeight = true;
            stripLayout.childForceExpandWidth = false;
            stripLayout.childForceExpandHeight = false;

            int count = row.Options.Length;
            var backgrounds = new Image[count];
            var labels = new TMP_Text[count];
            int selected = row.Get != null ? row.Get() : 0;

            System.Action paint = () =>
            {
                for (int i = 0; i < count; i++)
                {
                    bool on = i == selected;
                    CascadeTheme.Bind(backgrounds[i], on ? Theme.Accent : Theme.FieldBg);
                    CascadeTheme.Bind(labels[i], on ? Theme.SelectionText : Theme.TextSecondary);
                    backgrounds[i].color = on ? (Color)Theme.Accent : (Color)Theme.FieldBg;
                    labels[i].color = on ? (Color)Theme.SelectionText : (Color)Theme.TextSecondary;
                }
            };

            for (int i = 0; i < count; i++)
            {
                int index = i;
                string text = row.Options[i];
                float width = 18f + text.Length * 6.2f;

                RectTransform chip = CascadeUI.Rect("Chip_" + text, strip);
                var chipElement = chip.gameObject.AddComponent<LayoutElement>();
                chipElement.preferredWidth = width;
                chipElement.minWidth = width;
                chipElement.preferredHeight = 22f;
                chipElement.minHeight = 22f;

                backgrounds[i] = CascadeUI.Image(chip, CascadeSprites.Rounded(11), Theme.FieldBg, Image.Type.Sliced);

                RectTransform labelRect = CascadeUI.Rect("Label", chip);
                CascadeUI.Stretch(labelRect, 3f, 3f, 0f, 0f);
                labels[i] = CascadeUI.Text(labelRect, text, 10f, Theme.TextSecondary, TextAlignmentOptions.Center, true);

                Register(chip, CascadeLayerGroup.Content, () =>
                {
                    selected = index;
                    if (row.Set != null) row.Set(index);
                    paint();
                }, hover: hovered =>
                {
                    if (index == selected) return;
                    backgrounds[index].color = hovered ? (Color)Theme.TabHover : (Color)Theme.FieldBg;
                    labels[index].color = hovered ? (Color)Theme.TextPrimary : (Color)Theme.TextSecondary;
                });
            }

            paint();
        }

        // -----------------------------------------------------------------
        // Icons
        // -----------------------------------------------------------------

        /// <summary>The sidebar icon for a page; falls back to the icon the page was created with.</summary>
        private static CascadeIcon PageIcon(CascadePage page)
        {
            switch (page.Title)
            {
                case "Home": return CascadeIcon.Home;
                case "Players":
                case "Player": return CascadeIcon.Person;
                case "Fun":
                case "Game":
                case "Snake": return CascadeIcon.Gamepad;
                case "Movement":
                case "Flight": return CascadeIcon.Bolt;
                case "Acrobatics": return CascadeIcon.Sparkle;
                case "Visual": return CascadeIcon.Eye;
                case "Overlays": return CascadeIcon.Layers;
                case "Desktop": return CascadeIcon.Sidebar;
                case "Cosmetics": return CascadeIcon.Sparkle;
                case "Shop": return CascadeIcon.Bag;
                case "Tools": return CascadeIcon.Wrench;
                case "Gun": return CascadeIcon.Recenter;
                case "Performance":
                case "Profiler": return CascadeIcon.Chart;
                case "Notifications": return CascadeIcon.Bell;
                case "Themes": return CascadeIcon.Palette;
                case "Utilities": return CascadeIcon.Folder;
                case "Debug Console": return CascadeIcon.Terminal;
                case "Settings": return CascadeIcon.Gear;
                case "About": return CascadeIcon.Info;
                case "Controls": return CascadeIcon.Sliders;
                default: return page.Icon;
            }
        }

        private static CascadeIcon SectionIcon(string sectionName)
        {
            switch (sectionName)
            {
                case "Main": return CascadeIcon.Home;
                case "Fun": return CascadeIcon.Gamepad;
                case "Movement": return CascadeIcon.Bolt;
                case "Visual": return CascadeIcon.Eye;
                case "Cosmetics": return CascadeIcon.Sparkle;
                case "Tools": return CascadeIcon.Wrench;
                case "Debug": return CascadeIcon.Terminal;
                default: return CascadeIcon.Gear;
            }
        }
    }
}
