using System;
using System.Collections;
using System.Collections.Generic;
using GorillaNetworking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;

namespace CascadeMenu.UI
{
    internal sealed partial class CascadeWindow
    {
        private void BuildFrame()
        {
            // Organic shader-like multi-orb luminous aura behind the window (zero boxy edges)
            Sprite orbSprite = CascadeSprites.OrbAura(256);

            _windowGlowCoreRect = CascadeUI.Rect("WindowAuraCore", _root);
            _windowGlowCoreRect.anchorMin = _windowGlowCoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowGlowCoreRect.pivot = new Vector2(0.5f, 0.5f);
            _windowGlowCoreRect.anchoredPosition = Vector2.zero;
            _windowGlowCoreRect.sizeDelta = new Vector2(CascadeTheme.WindowWidth * 1.48f, CascadeTheme.WindowHeight * 1.66f);
            _windowGlowCore = CascadeUI.Image(_windowGlowCoreRect, orbSprite, Color.clear, Image.Type.Simple);
            if (_windowGlowCore != null) _windowGlowCore.raycastTarget = false;

            _windowGlowOrbARect = CascadeUI.Rect("WindowAuraOrbA", _root);
            _windowGlowOrbARect.anchorMin = _windowGlowOrbARect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowGlowOrbARect.pivot = new Vector2(0.5f, 0.5f);
            _windowGlowOrbARect.anchoredPosition = new Vector2(-210f, 120f);
            _windowGlowOrbARect.sizeDelta = new Vector2(680f, 680f);
            _windowGlowOrbA = CascadeUI.Image(_windowGlowOrbARect, orbSprite, Color.clear, Image.Type.Simple);
            if (_windowGlowOrbA != null) _windowGlowOrbA.raycastTarget = false;

            _windowGlowOrbBRect = CascadeUI.Rect("WindowAuraOrbB", _root);
            _windowGlowOrbBRect.anchorMin = _windowGlowOrbBRect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowGlowOrbBRect.pivot = new Vector2(0.5f, 0.5f);
            _windowGlowOrbBRect.anchoredPosition = new Vector2(215f, -120f);
            _windowGlowOrbBRect.sizeDelta = new Vector2(640f, 640f);
            _windowGlowOrbB = CascadeUI.Image(_windowGlowOrbBRect, orbSprite, Color.clear, Image.Type.Simple);
            if (_windowGlowOrbB != null) _windowGlowOrbB.raycastTarget = false;

            RectTransform shadow = CascadeUI.Rect("Shadow", _root);
            CascadeUI.Stretch(shadow, -24f, -24f, -22f, -24f);
            CascadeUI.Image(shadow, CascadeSprites.Shadow(22, 28), Theme.ShadowColor, Image.Type.Sliced);

            UpdateWindowGlow(true);

            _body = CascadeUI.Rect("Body", _root);
            CascadeUI.Stretch(_body);
            _bodyBgImage = CascadeUI.Panel(_body, Theme.SidebarBg.WithAlpha(0.32f), 12);
            if (_bodyBgImage != null) _bodyBgImage.raycastTarget = false;

            // Custom uploaded PNG/JPG background image loaded in memory with baked rounded corners
            RectTransform customBgRect = CascadeUI.Rect("CustomBackground", _body);
            CascadeUI.Stretch(customBgRect);
            _customBgImage = customBgRect.gameObject.AddComponent<RawImage>();
            _customBgImage.raycastTarget = false;
            _customBgImage.color = Color.clear;
            customBgRect.gameObject.SetActive(false);

            // Frosted glass cool-white vertical sheen layer across the entire window
            RectTransform glassSheen = CascadeUI.Rect("GlassSheen", _body);
            CascadeUI.Stretch(glassSheen);
            _glassSheenImage = CascadeUI.Panel(glassSheen, Color.white, 12);
            if (_glassSheenImage != null)
            {
                _glassSheenImage.raycastTarget = false;
                CascadeGradient sheenGrad = _glassSheenImage.gameObject.AddComponent<CascadeGradient>();
                sheenGrad.Top = new Color(0.84f, 0.92f, 1f, 0.16f);
                sheenGrad.Bottom = new Color(0.75f, 0.85f, 1f, 0.02f);
                sheenGrad.Refresh();
            }

            _sidebar = CascadeUI.Rect("Sidebar", _body);
            _sidebar.anchorMin = new Vector2(0f, 0f);
            _sidebar.anchorMax = new Vector2(0f, 1f);
            _sidebar.pivot = new Vector2(0f, 0.5f);
            _sidebar.anchoredPosition = Vector2.zero;
            _sidebar.sizeDelta = new Vector2(CascadeTheme.SidebarWidth, 0f);
            _sidebarBgImage = CascadeUI.Panel(_sidebar, Theme.SidebarBg, 12);
            _sidebarGroup = _sidebar.gameObject.AddComponent<CanvasGroup>();

            _separator = CascadeUI.Rect("SidebarSeparator", _body);
            _separator.anchorMin = new Vector2(0f, 0f);
            _separator.anchorMax = new Vector2(0f, 1f);
            _separator.pivot = new Vector2(0f, 0.5f);
            _separator.anchoredPosition = new Vector2(CascadeTheme.SidebarWidth, 0f);
            _separator.sizeDelta = new Vector2(1f, -2f);
            CascadeUI.Solid(_separator, Theme.Separator);

            _contentBody = CascadeUI.Rect("ContentBody", _body);
            _contentBody.anchorMin = Vector2.zero;
            _contentBody.anchorMax = Vector2.one;
            _contentBody.pivot = new Vector2(0.5f, 0.5f);
            _contentBody.offsetMin = new Vector2(CascadeTheme.SidebarWidth + 1f, 0f);
            _contentBody.offsetMax = Vector2.zero;
            _contentBgImage = CascadeUI.Panel(_contentBody, Theme.ContentBg, 12);

            RectTransform contentBorder = CascadeUI.Rect("Border", _contentBody);
            CascadeUI.Stretch(contentBorder);
            CascadeUI.Outline(contentBorder, Theme.ViewBorder, 12);

            // The content area meets the sidebar with a straight edge. Cover the
            // rounded left corners so the 12px radius only appears on the window's
            // outer corners.
            RectTransform contentFlatLeft = CascadeUI.Rect("FlatLeft", _contentBody);
            contentFlatLeft.anchorMin = new Vector2(0f, 0f);
            contentFlatLeft.anchorMax = new Vector2(0f, 1f);
            contentFlatLeft.pivot = new Vector2(0f, 0.5f);
            contentFlatLeft.anchoredPosition = Vector2.zero;
            contentFlatLeft.sizeDelta = new Vector2(12f, 0f);
            _contentFlatLeftImage = CascadeUI.Image(contentFlatLeft, CascadeSprites.White(), Theme.ContentBg, Image.Type.Simple);
            _contentFlatLeft = contentFlatLeft;

            // Specular frosted-glass top rim highlight
            RectTransform topHighlight = CascadeUI.Rect("GlassTopHighlight", _body);
            topHighlight.anchorMin = new Vector2(0f, 1f);
            topHighlight.anchorMax = new Vector2(1f, 1f);
            topHighlight.pivot = new Vector2(0.5f, 1f);
            topHighlight.offsetMin = new Vector2(12f, -1.5f);
            topHighlight.offsetMax = new Vector2(-12f, 0f);
            _topHighlightImage = CascadeUI.Solid(topHighlight, new Color(0.90f, 0.96f, 1f, 0.42f));
            if (_topHighlightImage != null) _topHighlightImage.raycastTarget = false;

            // Subtle bottom glass rim reflection
            RectTransform bottomHighlight = CascadeUI.Rect("GlassBottomHighlight", _body);
            bottomHighlight.anchorMin = new Vector2(0f, 0f);
            bottomHighlight.anchorMax = new Vector2(1f, 0f);
            bottomHighlight.pivot = new Vector2(0.5f, 0f);
            bottomHighlight.offsetMin = new Vector2(12f, 0f);
            bottomHighlight.offsetMax = new Vector2(-12f, 1f);
            _bottomHighlightImage = CascadeUI.Solid(bottomHighlight, new Color(0.85f, 0.92f, 1f, 0.14f));
            if (_bottomHighlightImage != null) _bottomHighlightImage.raycastTarget = false;

            RectTransform windowBorder = CascadeUI.Rect("WindowBorder", _body);
            CascadeUI.Stretch(windowBorder);
            CascadeUI.Outline(windowBorder, Theme.WindowBorder, 12);
            windowBorder.SetAsLastSibling();
            _windowBorder = windowBorder;
        }

        // -----------------------------------------------------------------
        // Sidebar
        // -----------------------------------------------------------------

        private void BuildSidebar()
        {
            RectTransform toolbar = CascadeUI.Rect("Toolbar", _sidebar);
            toolbar.anchorMin = new Vector2(0f, 1f);
            toolbar.anchorMax = new Vector2(1f, 1f);
            toolbar.pivot = new Vector2(0.5f, 1f);
            toolbar.offsetMin = new Vector2(0f, -52f);
            toolbar.offsetMax = Vector2.zero;

            BuildTrafficLights(toolbar);

            // The sidebar list lives inside a clipped, scrollable viewport so
            // tabs can never overlap the footer when there are many pages.
            RectTransform sidebarScrollRoot = CascadeUI.Rect("SidebarScroll", _sidebar);
            CascadeUI.Stretch(sidebarScrollRoot, 6f, 6f, 52f, 32f);
            _sidebarScroll = sidebarScrollRoot.gameObject.AddComponent<ScrollRect>();
            _sidebarScroll.horizontal = false;
            _sidebarScroll.vertical = true;
            _sidebarScroll.movementType = ScrollRect.MovementType.Clamped;
            _sidebarScroll.inertia = false;
            _sidebarScroll.scrollSensitivity = 30f;

            _sidebarViewport = CascadeUI.Rect("Viewport", sidebarScrollRoot);
            CascadeUI.Stretch(_sidebarViewport);
            CascadeUI.Clip(_sidebarViewport);
            _sidebarScroll.viewport = _sidebarViewport;

            _sidebarList = CascadeUI.Rect("List", _sidebarViewport);
            _sidebarList.anchorMin = new Vector2(0f, 1f);
            _sidebarList.anchorMax = new Vector2(1f, 1f);
            _sidebarList.pivot = new Vector2(0.5f, 1f);
            _sidebarList.anchoredPosition = Vector2.zero;
            _sidebarList.sizeDelta = new Vector2(0f, 200f);
            _sidebarScroll.content = _sidebarList;

            var layout = _sidebarList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(2, 2, 8, 10);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _sidebarList.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Group the tabs into clear labelled sections.
            string[] sectionOrder = { "Main", "Fun", "Movement", "Visual", "Cosmetics", "Tools", "Debug", "System" };
            for (int s = 0; s < sectionOrder.Length; s++)
            {
                string sectionName = sectionOrder[s];
                var indices = new List<int>();
                var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < Pages.Count; i++)
                {
                    if (Pages[i].HiddenFromSidebar) continue;
                    if (SectionTitleFor(Pages[i].Title) == sectionName)
                    {
                        if (seenTitles.Add(Pages[i].Title)) indices.Add(i);
                    }
                }
                if (indices.Count > 0) AddSidebarSection(sectionName, indices);
            }

            RectTransform footerLine = CascadeUI.Rect("FooterLine", _sidebar);
            footerLine.anchorMin = new Vector2(0f, 0f);
            footerLine.anchorMax = new Vector2(1f, 0f);
            footerLine.pivot = new Vector2(0.5f, 0f);
            footerLine.offsetMin = new Vector2(12f, 26f);
            footerLine.offsetMax = new Vector2(-12f, 27f);
            CascadeUI.Solid(footerLine, Theme.Separator);

            RectTransform footer = CascadeUI.Rect("Footer", _sidebar);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = new Vector2(12f, 8f);
            footer.offsetMax = new Vector2(-12f, 24f);
            CascadeUI.Text(footer, "CASCADE 1.0", 9f, Theme.TextTertiary, TextAlignmentOptions.Left, true);
        }

        private void BuildTrafficLights(RectTransform toolbar)
        {
            AddTrafficLight(toolbar, "Close", 11f, Theme.TrafficClose, CascadeIcon.Close, () => CloseRequested?.Invoke());
            AddTrafficLight(toolbar, "Minimize", 29f, Theme.TrafficMinimize, CascadeIcon.Minus, () => CloseRequested?.Invoke());
            AddTrafficLight(toolbar, "Zoom", 47f, Theme.TrafficZoom, CascadeIcon.Recenter, () => RecenterRequested?.Invoke());
        }

        private void AddTrafficLight(RectTransform toolbar, string name, float x, Color color, CascadeIcon glyphIcon, Action click)
        {
            RectTransform hit = CascadeUI.TopLeft("Traffic" + name, toolbar, x, 12f, 18f, 18f);
            RectTransform dot = CascadeUI.Center("Dot", hit, 0f, 0f, 12f, 12f);
            CascadeUI.Image(dot, CascadeSprites.Circle(), color, Image.Type.Simple);

            RectTransform glyph = CascadeUI.Center("Glyph", hit, 0f, 0f, 8f, 8f);
            Image glyphImage = CascadeUI.Image(glyph, CascadeIcons.Get(glyphIcon), Theme.TrafficGlyph, Image.Type.Simple);
            glyphImage.gameObject.SetActive(false);

            Register(hit, CascadeLayerGroup.Chrome, click, hover: hovered =>
            {
                if (glyphImage != null) glyphImage.gameObject.SetActive(hovered);
            });
        }

        /// <summary>Which sidebar section a page belongs to.</summary>
        private string SectionTitleFor(string pageTitle)
        {
            if (pageTitle == CascadeMenu.Mods.Tools.DebugConsole.PageTitle)
            {
                string placement = _config?.DebugConsolePlacement?.Value;
                return !string.IsNullOrEmpty(placement) ? placement : "Debug";
            }

            switch (pageTitle)
            {
                // ---- Main ----
                case "Home":
                case "Players":
                case "Player List":
                case "Player":
                    return "Main";

                // ---- Fun ----
                case "Fun":
                case "Game":
                case "Snake":
                    return "Fun";

                // ---- Movement ----
                case "Movement":
                case "Flight":
                case "Acrobatics":
                    return "Movement";

                // ---- Visual ----
                case "Visual":
                case "Overlays":
                case "Desktop":
                    return "Visual";

                // ---- Cosmetics ----
                case "Cosmetics":
                case "Shop":
                    return "Cosmetics";

                // ---- Tools ----
                case "Tools":
                case "Gun":
                case "Performance":
                    return "Tools";

                // ---- System ----
                case "Settings":
                case "Notifications":
                case "Themes":
                case "Utilities":
                case "About":
                case "Controls":
                case "Gallery":
                    return "System";

                case "Profiler":
                    return "Debug";

                default:
                    // Any unrecognised mod category falls into Fun as a catch-all.
                    return "Fun";
            }
        }

        private void AddSidebarSection(string title, List<int> pageIndices)
        {
            var section = new SidebarSection();
            _sections.Add(section);

            RectTransform header = CascadeUI.Rect("Section_" + title, _sidebarList);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 30f;

            RectTransform sectionIconRect = CascadeUI.Rect("Icon", header);
            sectionIconRect.anchorMin = sectionIconRect.anchorMax = new Vector2(0f, 0.5f);
            sectionIconRect.pivot = new Vector2(0f, 0.5f);
            sectionIconRect.anchoredPosition = new Vector2(6f, 0f);
            sectionIconRect.sizeDelta = new Vector2(11f, 11f);
            CascadeUI.Image(sectionIconRect, CascadeIcons.Get(SectionIcon(title)), Theme.TextTertiary, Image.Type.Simple);

            RectTransform titleRect = LineRect("Title", header, 22f, 20f, 5f, 20f);
            section.Label = CascadeUI.Text(titleRect, title, 9.5f, Theme.TextTertiary, TextAlignmentOptions.Left, true);

            RectTransform chevron = CascadeUI.Rect("Chevron", header);
            chevron.anchorMin = chevron.anchorMax = new Vector2(1f, 0.5f);
            chevron.pivot = new Vector2(1f, 0.5f);
            chevron.anchoredPosition = new Vector2(-8f, 0f);
            chevron.sizeDelta = new Vector2(8f, 8f);
            CascadeUI.Image(chevron, CascadeIcons.Get(CascadeIcon.ChevronDown), Theme.TextTertiary, Image.Type.Simple);
            section.Chevron = chevron;

            Register(header, CascadeLayerGroup.Chrome, () =>
            {
                section.Open = !section.Open;
                foreach (GameObject tab in section.Tabs) tab.SetActive(section.Open);
                section.Chevron.localRotation = Quaternion.Euler(0f, 0f, section.Open ? 0f : -90f);
                UpdateSidebarSelection();
            }, hover: hovered =>
            {
                if (section.Label != null) section.Label.color = hovered ? Theme.TextSecondary : Theme.TextTertiary;
            });

            for (int i = 0; i < pageIndices.Count; i++)
            {
                AddSidebarTab(pageIndices[i], section);
            }
        }

        private void AddSidebarTab(int pageIndex, SidebarSection section)
        {
            CascadePage page = Pages[pageIndex];
            RectTransform root = CascadeUI.Rect("Tab_" + page.Title, _sidebarList);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 28f;
            layoutElement.minHeight = 28f;

            var tab = new SidebarTab
            {
                Root = root,
                PageIndex = pageIndex,
                Background = CascadeUI.Image(root, CascadeSprites.Rounded(6), Color.clear, Image.Type.Sliced)
            };

            RectTransform iconRect = CascadeUI.Rect("Icon", root);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(14f, 14f);
            tab.Icon = CascadeUI.Image(iconRect, CascadeIcons.Get(PageIcon(page)), Theme.IconNormal, Image.Type.Simple);

            RectTransform labelRect = LineRect("Label", root, 29f, 8f, 0f, 28f);
            tab.Label = CascadeUI.Text(labelRect, page.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left);

            Register(root, CascadeLayerGroup.Chrome, () => NavigateTo(pageIndex, true), hover: hovered =>
            {
                if (tab.PageIndex == PageIndex) return;
                tab.Background.color = hovered ? Theme.TabHover : Color.clear;
                tab.Label.color = Theme.TextPrimary;
                tab.Icon.color = Theme.IconNormal;
            });

            // Selected tabs stay readable after hover.
            tab.Label.color = Theme.TextSecondary;
            _tabs.Add(tab);
            section.Tabs.Add(root.gameObject);
        }

        private void UpdateSidebarSelection()
        {
            foreach (SidebarTab tab in _tabs)
            {
                bool selected = tab.PageIndex == PageIndex;
                if (tab.Background != null) tab.Background.color = selected ? Theme.Accent : Color.clear;
                if (tab.Icon != null) tab.Icon.color = selected ? Theme.IconSelected : Theme.IconNormal;
                if (tab.Label != null) tab.Label.color = selected ? Theme.SelectionText : Theme.TextPrimary;
            }
        }

        public void ToggleSidebar()
        {
            _sidebarExpanded = !_sidebarExpanded;
            if (_sidebarExpanded && _sidebar != null) _sidebar.gameObject.SetActive(true);
            UpdateSidebarSelection();
            RebuildContent();
        }

        // -----------------------------------------------------------------
        // Titlebar
        // -----------------------------------------------------------------

        private void BuildTitlebar()
        {
            _titlebar = CascadeUI.Rect("Titlebar", _contentBody);
            _titlebar.anchorMin = new Vector2(0f, 1f);
            _titlebar.anchorMax = new Vector2(1f, 1f);
            _titlebar.pivot = new Vector2(0.5f, 1f);
            _titlebar.offsetMin = new Vector2(0f, -CascadeTheme.TitlebarHeight);
            _titlebar.offsetMax = Vector2.zero;
            _titlebarBgImage = CascadeUI.Image(_titlebar, CascadeSprites.RoundedTop(10), Theme.TitlebarBg, Image.Type.Sliced);
            if (_titlebarBgImage != null) _titlebarBgImage.raycastTarget = false;

            RectTransform titleFlatLeft = CascadeUI.Rect("FlatLeft", _titlebar);
            titleFlatLeft.anchorMin = new Vector2(0f, 0f);
            titleFlatLeft.anchorMax = new Vector2(0f, 1f);
            titleFlatLeft.pivot = new Vector2(0f, 0.5f);
            titleFlatLeft.anchoredPosition = Vector2.zero;
            titleFlatLeft.sizeDelta = new Vector2(12f, 0f);
            _titlebarFlatLeftImage = CascadeUI.Image(titleFlatLeft, CascadeSprites.White(), Theme.TitlebarBg, Image.Type.Simple);
            _titlebarFlatLeft = titleFlatLeft;

            RectTransform hairline = CascadeUI.Rect("Hairline", _titlebar);
            hairline.anchorMin = new Vector2(0f, 0f);
            hairline.anchorMax = new Vector2(1f, 0f);
            hairline.pivot = new Vector2(0.5f, 0f);
            hairline.offsetMin = Vector2.zero;
            hairline.offsetMax = new Vector2(0f, 1f);
            CascadeUI.Solid(hairline, Theme.TitlebarHairline);

            RectTransform sidebarButton = CascadeUI.TopLeft("SidebarButton", _titlebar, 10f, 12f, 26f, 28f);
            Image sidebarHover = AddHover(sidebarButton, 5, 0.07f);
            RectTransform sidebarIcon = CascadeUI.Center("Icon", sidebarButton, 0f, 0f, 17f, 15f);
            CascadeUI.Image(sidebarIcon, CascadeIcons.Get(CascadeIcon.Sidebar), Theme.TextSecondary, Image.Type.Simple);
            Register(sidebarButton, CascadeLayerGroup.Chrome, ToggleSidebar, hover: hovered => sidebarHover.gameObject.SetActive(hovered));

            RectTransform back = CascadeUI.TopLeft("Back", _titlebar, 40f, 12f, 22f, 28f);
            Image backHover = AddHover(back, 5, 0.06f);
            RectTransform backIconRect = CascadeUI.Center("Icon", back, 0f, 0f, 9f, 14f);
            _backIcon = CascadeUI.Image(backIconRect, CascadeIcons.Get(CascadeIcon.ChevronLeft), Theme.TextSecondary, Image.Type.Simple);
            _backInteractable = Register(back, CascadeLayerGroup.Chrome, GoBack, hover: hovered => backHover.gameObject.SetActive(hovered));

            RectTransform forward = CascadeUI.TopLeft("Forward", _titlebar, 62f, 12f, 22f, 28f);
            Image forwardHover = AddHover(forward, 5, 0.06f);
            RectTransform forwardIconRect = CascadeUI.Center("Icon", forward, 0f, 0f, 9f, 14f);
            _forwardIcon = CascadeUI.Image(forwardIconRect, CascadeIcons.Get(CascadeIcon.ChevronRight), Theme.TextSecondary, Image.Type.Simple);
            _forwardInteractable = Register(forward, CascadeLayerGroup.Chrome, GoForward, hover: hovered => forwardHover.gameObject.SetActive(hovered));

            RectTransform titleRect = LineRect("Title", _titlebar, 90f, 262f, 6f, 24f);
            _titleText = CascadeUI.Text(titleRect, string.Empty, 15.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);

            RectTransform subtitleRect = LineRect("Subtitle", _titlebar, 90f, 262f, 29f, 18f);
            _subtitleText = CascadeUI.Text(subtitleRect, string.Empty, 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);

            SearchField = CascadeWidgets.Search(this, _titlebar, Theme, OnSearchChanged);
            RectTransform searchRoot = SearchField.Root;
            searchRoot.anchorMin = searchRoot.anchorMax = new Vector2(1f, 1f);
            searchRoot.pivot = new Vector2(1f, 1f);
            searchRoot.anchoredPosition = new Vector2(-10f, -12f);
            searchRoot.sizeDelta = new Vector2(140f, 28f);

            CascadeButtonView disconnect = CascadeWidgets.Button(
                this, _titlebar, "Disconnect", false, OpenDisconnectConfirmation, CascadeLayerGroup.Chrome);
            AnchorRightCenter(disconnect.Root, 162f, 88f, 26f);

            // Top-middle local telemetry pill (Name · Actor # · FPS · Room)
            RectTransform topHudPill = CascadeUI.Rect("TopTelemetryPill", _body);
            topHudPill.anchorMin = new Vector2(0.5f, 1f);
            topHudPill.anchorMax = new Vector2(0.5f, 1f);
            topHudPill.pivot = new Vector2(0.5f, 0f);
            topHudPill.anchoredPosition = new Vector2(0f, 8f);
            topHudPill.sizeDelta = new Vector2(360f, 24f);
            Image pillBg = CascadeUI.Panel(topHudPill, Theme.TitlebarBg.WithAlpha(0.80f), 12);
            if (pillBg != null) pillBg.raycastTarget = false;
            RectTransform pillBorder = CascadeUI.Rect("Border", topHudPill);
            CascadeUI.Stretch(pillBorder);
            Image pillBorderImg = CascadeUI.Outline(pillBorder, Theme.WindowBorder, 12);
            if (pillBorderImg != null) pillBorderImg.raycastTarget = false;
            RectTransform pillTxtRt = CascadeUI.Rect("Label", topHudPill);
            CascadeUI.Stretch(pillTxtRt, 10f, 10f, 0f, 0f);
            _topTelemetryText = CascadeUI.Text(pillTxtRt, string.Empty, 10.5f, Theme.TextPrimary, TextAlignmentOptions.Center, true);
            if (_topTelemetryText != null) _topTelemetryText.raycastTarget = false;
            RefreshTopTelemetry();

            UpdateNavButtons();
        }

        private void RefreshTopTelemetry()
        {
            if (_topTelemetryText == null) return;
            NetPlayer local = PlayerActions.LocalPlayer();
            string name = local != null ? PlayerActions.SafeName(local) : "Local";
            if (local == null && VRRig.LocalRig != null && !string.IsNullOrEmpty(VRRig.LocalRig.playerNameVisible))
                name = VRRig.LocalRig.playerNameVisible;

            int actor = local != null ? local.ActorNumber : 0;
            int fps = PlayerActions.LocalFps();
            string room = "Offline";
            NetworkSystem sys = NetworkSystem.Instance;
            if (sys != null && sys.InRoom && !string.IsNullOrEmpty(sys.RoomName))
                room = sys.RoomName;

            string actorPart = actor > 0 ? " · #" + actor : string.Empty;
            _topTelemetryText.text = name + actorPart + "  ·  " + fps + " FPS  ·  " + room;
        }

        private void UpdateNavButtons()
        {
            bool canBack = _historyIndex > 0;
            bool canForward = _historyIndex < _history.Count - 1;
            if (_backIcon != null) _backIcon.color = canBack ? Theme.TextSecondary : Theme.TextTertiary;
            if (_forwardIcon != null) _forwardIcon.color = canForward ? Theme.TextSecondary : Theme.TextTertiary;
            if (_backInteractable != null) _backInteractable.Enabled = canBack;
            if (_forwardInteractable != null) _forwardInteractable.Enabled = canForward;
        }

        private Image AddHover(RectTransform parent, int radius, float alpha)
        {
            RectTransform hover = CascadeUI.Rect("Hover", parent);
            CascadeUI.Stretch(hover);
            Image image = CascadeUI.Image(hover, CascadeSprites.Rounded(radius), Theme.HoverTint, Image.Type.Sliced);
            image.gameObject.SetActive(false);
            return image;
        }

        // -----------------------------------------------------------------
        // Viewport / content
        // -----------------------------------------------------------------

        private void BuildViewport()
        {
            _scrollRoot = CascadeUI.Rect("Scroll", _contentBody);
            CascadeUI.Stretch(_scrollRoot, 1f, 1f, CascadeTheme.TitlebarHeight, 1f);
            _fadeGroup = _scrollRoot.gameObject.AddComponent<CanvasGroup>();
            _scroll = _scrollRoot.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false;
            _scroll.scrollSensitivity = 30f;

            _viewport = CascadeUI.Rect("Viewport", _scrollRoot);
            CascadeUI.Stretch(_viewport);
            CascadeUI.Clip(_viewport);
            _scroll.viewport = _viewport;

            _content = CreateContent();
            _scroll.content = _content;

            _scrollThumb = CascadeUI.Rect("Scrollbar", _contentBody);
            _scrollThumb.anchorMin = _scrollThumb.anchorMax = new Vector2(1f, 1f);
            _scrollThumb.pivot = new Vector2(1f, 1f);
            _scrollThumb.anchoredPosition = new Vector2(-6f, -60f);
            _scrollThumb.sizeDelta = new Vector2(4f, 40f);
            CascadeUI.Image(_scrollThumb, CascadeSprites.Rounded(2), Theme.Scrollbar, Image.Type.Sliced);
        }

        private RectTransform CreateContent()
        {
            RectTransform content = CascadeUI.Rect("Content", _viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }
    }
}
