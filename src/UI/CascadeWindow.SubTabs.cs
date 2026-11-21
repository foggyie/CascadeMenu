using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            RectTransform bar = CascadeUI.Rect("SubTabs", _content);
            var barLayout = bar.gameObject.AddComponent<VerticalLayoutGroup>();
            barLayout.spacing = 4f;
            barLayout.padding = new RectOffset(2, 2, 0, 2);
            barLayout.childAlignment = TextAnchor.UpperLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.childForceExpandHeight = false;

            float maxWidth = _root.sizeDelta.x
                - (_sidebarExpanded ? CascadeTheme.SidebarWidth : 0f) - 52f;

            RectTransform row = null;
            float used = 0f;
            for (int i = SubTabAll; i < page.Sections.Count; i++)
            {
                string label = i == SubTabAll ? "All" : SubTabLabel(page.Sections[i], i);
                float width = 24f + label.Length * 6.4f;
                if (row == null || used + width > maxWidth)
                {
                    row = NewSubTabRow(bar);
                    used = 0f;
                }

                AddSubTabChip(row, page, i, label, width, i == selected);
                used += width + 5f;
            }
        }

        private static string SubTabLabel(CascadeSection section, int index)
        {
            return string.IsNullOrEmpty(section.Title) ? "Section " + (index + 1) : section.Title;
        }

        private static RectTransform NewSubTabRow(RectTransform bar)
        {
            RectTransform row = CascadeUI.Rect("ChipRow", bar);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 5f;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 24f;
            rowElement.minHeight = 24f;
            return row;
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
                selected ? (Color)Theme.Accent : (Color)Theme.FieldBg,
                Image.Type.Sliced);

            RectTransform labelRect = CascadeUI.Rect("Label", chip);
            CascadeUI.Stretch(labelRect, 4f, 4f, 0f, 0f);
            TMP_Text text = CascadeUI.Text(
                labelRect,
                label,
                10.5f,
                selected ? (Color)Theme.SelectionText : (Color)Theme.TextSecondary,
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
