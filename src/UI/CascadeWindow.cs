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
    /// <summary>
    /// The Cascade window chrome: macOS titlebar, sidebar, page viewport, dropdown overlay
    /// and notification toasts. Pages are declarative <see cref="CascadePage"/> objects.
    /// </summary>
    internal sealed class CascadeWindow : MonoBehaviour
    {
        private sealed class SidebarTab
        {
            public RectTransform Root;
            public Image Background;
            public Image Icon;
            public TMP_Text Label;
            public int PageIndex;
        }

        private sealed class SidebarSection
        {
            public bool Open = true;
            public readonly List<GameObject> Tabs = new List<GameObject>();
            public RectTransform Chevron;
            public TMP_Text Label;
        }

        public CascadeTheme Theme;
        public List<CascadePage> Pages;
        public int PageIndex;
        public CascadeSearchField SearchField;
        public readonly List<CascadeInteractable> Interactables = new List<CascadeInteractable>();

        public Action CloseRequested;
        public Action RecenterRequested;

        private CascadeMenuBehaviour _menu;
        private MenuConfig _config;
        private DemoState _state;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        private RectTransform _root;
        private RectTransform _body;
        private RectTransform _sidebar;
        private CanvasGroup _sidebarGroup;
        private RectTransform _sidebarList;
        private RectTransform _sidebarViewport;
        private ScrollRect _sidebarScroll;
        private RectTransform _separator;
        private RectTransform _contentBody;
        private RectTransform _titlebar;
        private RectTransform _scrollRoot;
        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _contentFlatLeft;
        private RectTransform _titlebarFlatLeft;
        private RectTransform _windowBorder;
        private CanvasGroup _fadeGroup;
        private RectTransform _scrollThumb;
        private ScrollRect _scroll;
        private RectTransform _toastContainer;
        private RectTransform _disconnectOverlay;
        private RectTransform _disconnectPanel;

        private RectTransform _confirmOverlay;
        private RectTransform _confirmPanel;

        private TMP_Text _titleText;
        private TMP_Text _subtitleText;
        private Image _backIcon;
        private Image _forwardIcon;
        private CascadeInteractable _backInteractable;
        private CascadeInteractable _forwardInteractable;

        private readonly List<SidebarTab> _tabs = new List<SidebarTab>();
        private readonly List<SidebarSection> _sections = new List<SidebarSection>();
        private readonly List<int> _history = new List<int>();
        private int _historyIndex;

        private bool _sidebarExpanded = true;
        private float _sidebarCurrent = CascadeTheme.SidebarWidth;
        private float _sidebarTarget = CascadeTheme.SidebarWidth;

        private string _searchQuery = string.Empty;
        private bool _searchFocused;
        private CascadeKeyboard _keyboard;

        private GameObject _dropdown;
        private GameObject _dropdownBlocker;
        private CascadeDropdownView _dropdownView;
        private RectTransform _dropdownPanel;

        private bool _built;

        public void Initialize(
            Canvas canvas,
            RectTransform canvasRect,
            CascadeMenuBehaviour menu,
            MenuConfig config,
            DemoState state,
            CascadeTheme theme,
            List<CascadePage> pages,
            int pageIndex)
        {
            _canvas = canvas;
            _canvasRect = canvasRect;
            _menu = menu;
            _config = config;
            _state = state;
            Theme = theme;
            Pages = pages ?? new List<CascadePage>();

            _root = (RectTransform)transform;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = new Vector2(CascadeTheme.WindowWidth, CascadeTheme.WindowHeight);
            _root.localScale = Vector3.one;

            BuildFrame();
            BuildSidebar();
            BuildTitlebar();
            BuildViewport();
            _keyboard = new CascadeKeyboard();
            _keyboard.Build(this, _contentBody);
            BuildToasts();
            if (_windowBorder != null) _windowBorder.SetAsLastSibling();

            _history.Clear();
            _history.Add(Mathf.Clamp(pageIndex, 0, Mathf.Max(0, Pages.Count - 1)));
            _historyIndex = 0;

            _built = true;
            SetPage(_history[0], false);
        }

        // -----------------------------------------------------------------
        // Frame
        // -----------------------------------------------------------------

        private void BuildFrame()
        {
            RectTransform shadow = CascadeUI.Rect("Shadow", _root);
            CascadeUI.Stretch(shadow, -30f, -30f, -26f, -30f);
            CascadeUI.Image(shadow, CascadeSprites.Shadow(22, 28), Theme.ShadowColor, Image.Type.Sliced);

            _body = CascadeUI.Rect("Body", _root);
            CascadeUI.Stretch(_body);
            Image bodyImage = CascadeUI.Panel(_body, Theme.SidebarBg, 10);
            if (bodyImage != null) bodyImage.raycastTarget = false;

            _sidebar = CascadeUI.Rect("Sidebar", _body);
            _sidebar.anchorMin = new Vector2(0f, 0f);
            _sidebar.anchorMax = new Vector2(0f, 1f);
            _sidebar.pivot = new Vector2(0f, 0.5f);
            _sidebar.anchoredPosition = Vector2.zero;
            _sidebar.sizeDelta = new Vector2(CascadeTheme.SidebarWidth, 0f);
            CascadeUI.Panel(_sidebar, Theme.SidebarBg, 10);
            _sidebarGroup = _sidebar.gameObject.AddComponent<CanvasGroup>();

            _separator = CascadeUI.Rect("SidebarSeparator", _body);
            _separator.anchorMin = _separator.anchorMax = new Vector2(0f, 0.5f);
            _separator.pivot = new Vector2(0f, 0.5f);
            _separator.anchoredPosition = new Vector2(CascadeTheme.SidebarWidth, 0f);
            _separator.sizeDelta = new Vector2(1f, CascadeTheme.WindowHeight - 2f);
            CascadeUI.Solid(_separator, Theme.Separator);

            _contentBody = CascadeUI.Rect("ContentBody", _body);
            _contentBody.anchorMin = Vector2.zero;
            _contentBody.anchorMax = Vector2.one;
            _contentBody.pivot = new Vector2(0.5f, 0.5f);
            _contentBody.offsetMin = new Vector2(CascadeTheme.SidebarWidth + 1f, 0f);
            _contentBody.offsetMax = Vector2.zero;
            CascadeUI.Panel(_contentBody, Theme.ContentBg, 10);

            RectTransform contentBorder = CascadeUI.Rect("Border", _contentBody);
            CascadeUI.Stretch(contentBorder);
            CascadeUI.Outline(contentBorder, Theme.ViewBorder, 10);

            // The content area meets the sidebar with a straight edge. Cover the
            // rounded left corners so the 10px radius only appears on the window's
            // outer corners.
            RectTransform contentFlatLeft = CascadeUI.Rect("FlatLeft", _contentBody);
            contentFlatLeft.anchorMin = new Vector2(0f, 0f);
            contentFlatLeft.anchorMax = new Vector2(0f, 1f);
            contentFlatLeft.pivot = new Vector2(0f, 0.5f);
            contentFlatLeft.anchoredPosition = Vector2.zero;
            contentFlatLeft.sizeDelta = new Vector2(12f, 0f);
            CascadeUI.Image(contentFlatLeft, CascadeSprites.White(), Theme.ContentBg, Image.Type.Simple);
            _contentFlatLeft = contentFlatLeft;

            RectTransform windowBorder = CascadeUI.Rect("WindowBorder", _body);
            CascadeUI.Stretch(windowBorder);
            CascadeUI.Outline(windowBorder, Theme.WindowBorder, 10);
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
            layout.padding = new RectOffset(2, 2, 4, 4);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _sidebarList.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Group the tabs into clear sections, in a fixed order.
            string[] sectionOrder = { "Main", "Mods", "Cosmetics", "Modules", "System" };
            for (int s = 0; s < sectionOrder.Length; s++)
            {
                string sectionName = sectionOrder[s];
                var indices = new List<int>();
                for (int i = 0; i < Pages.Count; i++)
                {
                    if (Pages[i].HiddenFromSidebar) continue;
                    if (SectionTitleFor(Pages[i].Title) == sectionName) indices.Add(i);
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
        private static string SectionTitleFor(string pageTitle)
        {
            switch (pageTitle)
            {
                case "Home":
                case "Players":
                case "Notifications":
                    return "Main";
                case "Fun":
                case "Movement":
                case "Visual":
                case "Mods":
                    return "Mods";
                case "Cosmetics":
                case "Shop":
                    return "Cosmetics";
                case "Utilities":
                case "Settings":
                case "About":
                case "Controls":
                case "Gallery":
                    return "System";
                default:
                    // Mod category pages: Halloween, Super, PC, Flight, ...
                    return "Modules";
            }
        }

        private void AddSidebarSection(string title, List<int> pageIndices)
        {
            var section = new SidebarSection();
            _sections.Add(section);

            RectTransform header = CascadeUI.Rect("Section_" + title, _sidebarList);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 30f;

            RectTransform titleRect = LineRect("Title", header, 4f, 20f, 5f, 20f);
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
            tab.Icon = CascadeUI.Image(iconRect, CascadeIcons.Get(page.Icon), Theme.IconNormal, Image.Type.Simple);

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
            Image titlebarImage = CascadeUI.Image(_titlebar, CascadeSprites.RoundedTop(10), Theme.TitlebarBg, Image.Type.Sliced);
            titlebarImage.raycastTarget = false;

            RectTransform titleFlatLeft = CascadeUI.Rect("FlatLeft", _titlebar);
            titleFlatLeft.anchorMin = new Vector2(0f, 0f);
            titleFlatLeft.anchorMax = new Vector2(0f, 1f);
            titleFlatLeft.pivot = new Vector2(0f, 0.5f);
            titleFlatLeft.anchoredPosition = Vector2.zero;
            titleFlatLeft.sizeDelta = new Vector2(12f, 0f);
            CascadeUI.Image(titleFlatLeft, CascadeSprites.White(), Theme.TitlebarBg, Image.Type.Simple);
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

            RectTransform titleRect = LineRect("Title", _titlebar, 90f, 166f, 6f, 24f);
            _titleText = CascadeUI.Text(titleRect, string.Empty, 15.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);

            RectTransform subtitleRect = LineRect("Subtitle", _titlebar, 90f, 166f, 29f, 18f);
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

            UpdateNavButtons();
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
            Color tint = Theme.IsDark
                ? new Color(1f, 1f, 1f, alpha)
                : new Color(0f, 0f, 0f, alpha * 0.8f);
            Image image = CascadeUI.Image(hover, CascadeSprites.Rounded(radius), tint, Image.Type.Sliced);
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

        public CascadePage CurrentPage
        {
            get
            {
                if (Pages == null || PageIndex < 0 || PageIndex >= Pages.Count) return null;
                return Pages[PageIndex];
            }
        }

        public void SetPage(int index, bool animate)
        {            if (Pages == null || Pages.Count == 0) return;
            index = Mathf.Clamp(index, 0, Pages.Count - 1);
            PageIndex = index;

            CascadePage page = Pages[index];
            if (_titleText != null) _titleText.text = page.Title;
            if (_subtitleText != null) _subtitleText.text = page.Subtitle;

            _searchQuery = string.Empty;
            if (SearchField != null)
            {
                SearchField.SetQuery(string.Empty, false);
                SearchField.SetFocused(false);
            }
            _searchFocused = false;
            if (_keyboard != null) _keyboard.SetVisible(false);

            RebuildContent();
            if (_menu != null) _menu.NotifyPageChanged(page);
            UpdateSidebarSelection();
            UpdateNavButtons();

            if (animate && _config != null && _config.Animations.Value) StartCoroutine(FadeViewport());
            else if (_fadeGroup != null) _fadeGroup.alpha = 1f;
        }

        public void NavigateTo(int index, bool recordHistory)
        {
            if (Pages == null || index < 0 || index >= Pages.Count || index == PageIndex) return;
            _searchQuery = string.Empty;
            BlurSearch();

            if (recordHistory)
            {
                if (_historyIndex < _history.Count - 1)
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                _history.Add(index);
                _historyIndex = _history.Count - 1;
            }

            SetPage(index, true);
        }

        private void GoBack()
        {
            if (_historyIndex <= 0) return;
            _historyIndex--;
            SetPage(_history[_historyIndex], true);
        }

        private void GoForward()
        {
            if (_historyIndex >= _history.Count - 1) return;
            _historyIndex++;
            SetPage(_history[_historyIndex], true);
        }

        private void RebuildContent()
        {
            CloseDropdown();
            RemoveInteractables(CascadeLayerGroup.Content, CascadeLayerGroup.Overlay);

            if (_content != null)
            {
                _content.gameObject.SetActive(false);
                Destroy(_content.gameObject);
            }

            _content = CreateContent();
            if (_scroll != null) _scroll.content = _content;

            bool any = false;
            if (Pages != null && Pages.Count > 0)
            {
                CascadePage page = Pages[Mathf.Clamp(PageIndex, 0, Pages.Count - 1)];
                foreach (CascadeSection section in page.Sections)
                {
                    List<CascadeRow> rows = new List<CascadeRow>();
                    foreach (CascadeRow row in section.Rows)
                    {
                        if (Matches(row)) rows.Add(row);
                    }
                    if (rows.Count == 0) continue;
                    any = true;

                    AddSectionHeader(section);
                    RectTransform card = AddCard();
                    for (int i = 0; i < rows.Count; i++)
                    {
                        BuildRow(card, rows[i], i == rows.Count - 1);
                    }
                }
            }

            if (!any)
            {
                RectTransform empty = CascadeUI.Rect("Empty", _content);
                var emptyLayout = empty.gameObject.AddComponent<LayoutElement>();
                emptyLayout.preferredHeight = 110f;
                TMP_Text label = CascadeUI.Text(empty, "No results for \"" + _searchQuery + "\"", 12f, Theme.TextSecondary, TextAlignmentOptions.Center);
                label.richText = false;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.offsetMin = new Vector2(12f, 20f);
                label.rectTransform.offsetMax = new Vector2(-12f, -20f);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            if (_scroll != null)
            {
                _scroll.StopMovement();
                _scroll.verticalNormalizedPosition = 1f;
            }
            UpdateScrollbar();
        }

        /// <summary>Current scroll position (1 = top, 0 = bottom).</summary>
        internal float ScrollNormalized
        {
            get { return _scroll != null ? _scroll.verticalNormalizedPosition : 1f; }
            set
            {
                if (_scroll == null) return;
                _scroll.StopMovement();
                _scroll.verticalNormalizedPosition = Mathf.Clamp01(value);
            }
        }

        internal void ScrollTo(float normalized)
        {
            ScrollNormalized = normalized;
        }

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

            RectTransform titleRect = LineRect("Title", header, 4f, 4f, 0f, 20f);
            CascadeUI.Text(titleRect, section.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);
            if (hasDescription)
            {
                RectTransform descriptionRect = LineRect("Description", header, 4f, 4f, 22f, 17f);
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
                BuildModRow(card, modRow);
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
                    : (Theme.IsDark ? new Color(0.25f, 0.25f, 0.27f, 0.55f) : new Color(0.72f, 0.72f, 0.74f, 0.55f));
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
                Color dotColor = cosmetic.Owned
                    ? new Color(0.3f, 0.85f, 0.42f, 1f)
                    : Theme.TextTertiary;
                CascadeUI.Panel(dotRect, dotColor, 6);

                leftOffset = 12f + iconSize + 10f;

                if (!cosmetic.Available)
                {
                    // Nothing to press: show a clear red warning instead.
                    RectTransform warnRect = CascadeUI.TopRight(
                        "Warning", root, 12f, (row.Height - 18f) * 0.5f, 220f, 18f);
                    CascadeUI.Text(warnRect, "Can't try on or buy", 10.5f,
                        new Color(1f, 0.36f, 0.36f, 1f), TextAlignmentOptions.Right, true);
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
                CascadeUI.Text(valueRect, labelRow.Value, 11f, Theme.TextSecondary, TextAlignmentOptions.Right);
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
        private void BuildModRow(RectTransform card, CascadeModRow row)
        {
            RectTransform root = CascadeUI.Rect("Mod", card);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = row.Height;
            layoutElement.minHeight = row.Height;

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
            dotRect.anchoredPosition = new Vector2(17f, -19f);
            dotRect.sizeDelta = new Vector2(8f, 8f);
            Image dot = CascadeUI.Panel(dotRect, Theme.TextTertiary, 4);

            const float textLeft = 28f;
            const float rightOffset = 270f;

            RectTransform titleRect = LineRect("Title", root, textLeft, rightOffset, 6f, 24f);
            TMP_Text title = CascadeUI.Text(
                titleRect, row.Title, 12.5f, Theme.TextPrimary, TextAlignmentOptions.Left, true);
            title.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform statusRect = LineRect("Status", root, textLeft, rightOffset, 30f, 20f);
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

            CascadeButtonView action = CascadeWidgets.Button(
                this, root, row.GetActionLabel(), false, row.Activate);
            AnchorRightCenter(action.Root, 52f, 64f, 24f);

            CascadeButtonView press = CascadeWidgets.Button(
                this, root, row.GetPressLabel(), false, row.CyclePress);
            AnchorRightCenter(press.Root, 122f, 64f, 24f);

            CascadeButtonView bind = CascadeWidgets.Button(
                this, root, "Bind", false, row.Rebind);
            AnchorRightCenter(bind.Root, 192f, 58f, 24f);

            var view = root.gameObject.AddComponent<CascadeModView>();
            view.Dot = dot;
            view.Background = background;
            view.StatusLabel = status;
            view.ActionLabel = action.Label;
            view.PressLabel = press.Label;
            view.GetEnabled = row.GetEnabled;
            view.GetActive = row.GetActive;
            view.GetKeyLabel = row.GetKeyLabel;
            view.GetPressLabel = row.GetPressLabel;
            view.GetActionLabel = row.GetActionLabel;
            view.IsToggle = row.IsToggle;
            view.Configure(Theme);
        }

        /// <summary>
        /// Renders one live player: name + short ID on the left, TAP badge and the
        /// per-player action buttons on the right. The whole left area opens the
        /// player page.
        /// </summary>
        private void BuildPlayerRow(RectTransform card, CascadePlayerRow row)
        {
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
                Theme.IsDark ? new Color(1f, 1f, 1f, 0.11f) : new Color(0f, 0f, 0f, 0.08f),
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
            CascadeUI.Image(tapRect, CascadeSprites.Rounded(4), new Color(1f, 0.72f, 0.2f, 0.22f), Image.Type.Sliced);
            RectTransform tapText = CascadeUI.Rect("Text", tapRect);
            CascadeUI.Stretch(tapText, 2f, 2f, 0f, 0f);
            CascadeUI.Text(tapText, "TAP", 8f, new Color(1f, 0.78f, 0.32f, 1f), TextAlignmentOptions.Center, true);
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
            gradient.Top = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.13f);
            gradient.Bottom = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.01f);

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

        // -----------------------------------------------------------------
        // Search
        // -----------------------------------------------------------------

        /// <summary>Current query, used by the menu when it rebuilds the window.</summary>
        internal string SearchQuery
        {
            get { return _searchQuery; }
        }

        internal bool SearchFocused
        {
            get { return _searchFocused; }
        }

        private void OnSearchChanged(string query)
        {
            _searchQuery = query ?? string.Empty;

            // The shop catalog is generated from the query, so rows cannot be
            // filtered in place. Ask the menu for a debounced page rebuild.
            if (CurrentPage != null && (CurrentPage.Tag as string) == CosmeticShop.ShopTag)
            {
                if (_menu != null) _menu.SetShopQuery(_searchQuery);
                return;
            }

            RebuildContent();
        }

        public void FocusSearch(CascadeSearchField field)
        {
            if (field == null) return;
            SearchField = field;
            field.SetFocused(true);
            _searchFocused = true;
            if (_keyboard != null) _keyboard.SetVisible(XRSettings.isDeviceActive);
        }

        public void BlurSearch()
        {
            _searchFocused = false;
            if (SearchField != null) SearchField.SetFocused(false);
            if (_keyboard != null) _keyboard.SetVisible(false);
        }

        /// <summary>Restores a query after the window itself is rebuilt.</summary>
        internal void SetSearch(string query, bool focused)
        {
            _searchQuery = query ?? string.Empty;
            _searchFocused = focused;

            if (SearchField != null)
            {
                SearchField.SetQuery(_searchQuery, false);
                SearchField.SetFocused(false);
            }

            RebuildContent();
            if (focused) FocusSearch(SearchField);
        }

        internal void ClearSearch()
        {
            if (SearchField != null) SearchField.SetQuery(string.Empty);
        }

        public bool IsSearchInteraction(CascadeInteractable interactable)
        {
            if (interactable == null) return false;
            if (SearchField != null &&
                (interactable == SearchField.Interactable || interactable == SearchField.ClearInteractable))
                return true;
            return _keyboard != null && _keyboard.Contains(interactable);
        }

        public void HandleTextInput(char character)
        {
            if (_searchFocused && SearchField != null) SearchField.Append(character);
        }

        public void HandleBackspace()
        {
            if (_searchFocused && SearchField != null) SearchField.Backspace();
        }

        // -----------------------------------------------------------------
        // Dropdown
        // -----------------------------------------------------------------

        public void OpenDropdown(CascadeDropdownView view)
        {
            CloseDropdown();
            if (view == null || view.Options == null || view.Options.Length == 0) return;
            _dropdownView = view;

            _dropdownBlocker = new GameObject("DropdownBlocker", typeof(RectTransform));
            RectTransform blocker = (RectTransform)_dropdownBlocker.transform;
            blocker.SetParent(_canvasRect, false);
            CascadeUI.Stretch(blocker);
            Register(blocker, CascadeLayerGroup.Overlay, CloseDropdown);

            float width = Mathf.Max(100f, view.Root.rect.width);
            float height = view.Options.Length * 24f + 8f;

            _dropdown = new GameObject("DropdownPanel", typeof(RectTransform));
            _dropdownPanel = (RectTransform)_dropdown.transform;
            _dropdownPanel.SetParent(_canvasRect, false);
            _dropdownPanel.anchorMin = _dropdownPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _dropdownPanel.pivot = new Vector2(0f, 1f);
            _dropdownPanel.sizeDelta = new Vector2(width, height);

            Vector3[] corners = new Vector3[4];
            view.Root.GetWorldCorners(corners);
            Vector3 local = _canvasRect.InverseTransformPoint(corners[0]);
            float canvasWidth = _canvasRect.rect.width;
            float canvasHeight = _canvasRect.rect.height;
            float x = Mathf.Clamp(local.x, -canvasWidth * 0.5f + 8f, canvasWidth * 0.5f - width - 8f);
            float y = Mathf.Clamp(local.y - 4f, -canvasHeight * 0.5f + height + 8f, canvasHeight * 0.5f - 8f);
            _dropdownPanel.anchoredPosition = new Vector2(x, y);

            RectTransform shadow = CascadeUI.Rect("Shadow", _dropdownPanel);
            CascadeUI.Stretch(shadow, -12f, -12f, -6f, -14f);
            CascadeUI.Image(shadow, CascadeSprites.Shadow(10, 14), Theme.ShadowColor, Image.Type.Sliced);

            RectTransform background = CascadeUI.Rect("Background", _dropdownPanel);
            CascadeUI.Stretch(background);
            CascadeUI.Panel(background, Theme.ViewBg, 8);
            RectTransform border = CascadeUI.Rect("Border", _dropdownPanel);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, Theme.ViewBorder, 8);

            int selected = Mathf.Clamp(view.Get(), 0, view.Options.Length - 1);
            for (int i = 0; i < view.Options.Length; i++)
            {
                int index = i;
                RectTransform option = CascadeUI.TopLeft("Option" + i, _dropdownPanel, 4f, 4f + i * 24f, width - 8f, 24f);
                Image hover = CascadeUI.Panel(option, Theme.RowHover, 5);
                hover.color = new Color(Theme.RowHover.r, Theme.RowHover.g, Theme.RowHover.b, 0f);

                RectTransform labelRect = LineRect("Label", option, 10f, 26f, 0f, 24f);
                TMP_Text label = CascadeUI.Text(labelRect, view.Options[i], 11.5f,
                    i == selected ? Theme.TextPrimary : Theme.TextSecondary, TextAlignmentOptions.Left);

                if (i == selected)
                {
                    RectTransform check = CascadeUI.Rect("Check", option);
                    check.anchorMin = check.anchorMax = new Vector2(1f, 0.5f);
                    check.pivot = new Vector2(1f, 0.5f);
                    check.anchoredPosition = new Vector2(-9f, 0f);
                    check.sizeDelta = new Vector2(9f, 9f);
                    CascadeUI.Image(check, CascadeIcons.Get(CascadeIcon.Check), Theme.Accent, Image.Type.Simple);
                }

                Register(option, CascadeLayerGroup.Overlay, () =>
                {
                    view.Set?.Invoke(index);
                    view.Refresh();
                    CloseDropdown();
                }, hover: hovered =>
                {
                    hover.color = new Color(Theme.RowHover.r, Theme.RowHover.g, Theme.RowHover.b, hovered ? Theme.RowHover.a : 0f);
                    label.color = hovered || index == selected ? Theme.TextPrimary : Theme.TextSecondary;
                });
            }
        }

        private void OpenDisconnectConfirmation()
        {
            CloseDisconnectConfirmation();
            CloseDropdown();

            ControllerInputPoller poller = ControllerInputPoller.instance;
            bool gripHeld = poller != null &&
                (poller.leftControllerGripFloat > 0.65f || poller.rightControllerGripFloat > 0.65f);
            if (gripHeld)
            {
                DisconnectFromRoom();
                return;
            }

            _disconnectOverlay = CascadeUI.Rect("DisconnectOverlay", _root);
            CascadeUI.Stretch(_disconnectOverlay);
            Image dim = CascadeUI.Image(_disconnectOverlay, CascadeSprites.White(), new Color(0f, 0f, 0f, 0.62f), Image.Type.Simple);
            dim.raycastTarget = false;
            Register(_disconnectOverlay, CascadeLayerGroup.Overlay, () => { });

            _disconnectPanel = CascadeUI.Center("Confirmation", _disconnectOverlay, 0f, 0f, 390f, 190f);
            CascadeUI.Panel(_disconnectPanel, Theme.ViewBg, 10);
            RectTransform border = CascadeUI.Rect("Border", _disconnectPanel);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, Theme.ViewBorder, 10);

            RectTransform title = CascadeUI.Center("Title", _disconnectPanel, 0f, 44f, 350f, 30f);
            CascadeUI.Text(title, "Disconnect from lobby?", 17f, Theme.TextPrimary, TextAlignmentOptions.Center, true);
            RectTransform message = CascadeUI.Center("Message", _disconnectPanel, 0f, 12f, 350f, 26f);
            CascadeUI.Text(message, "Are you sure you want to disconnect?", 12f, Theme.TextSecondary, TextAlignmentOptions.Center);

            CascadeButtonView yes = CascadeWidgets.Button(
                this, _disconnectPanel, "Yes, disconnect", true, DisconnectFromRoom, CascadeLayerGroup.Overlay);
            yes.Root.anchorMin = yes.Root.anchorMax = new Vector2(0.5f, 0.5f);
            yes.Root.pivot = new Vector2(0.5f, 0.5f);
            yes.Root.sizeDelta = new Vector2(150f, 30f);
            yes.Root.anchoredPosition = new Vector2(-82f, -54f);

            CascadeButtonView no = CascadeWidgets.Button(
                this, _disconnectPanel, "No", false, CloseDisconnectConfirmation, CascadeLayerGroup.Overlay);
            no.Root.anchorMin = no.Root.anchorMax = new Vector2(0.5f, 0.5f);
            no.Root.pivot = new Vector2(0.5f, 0.5f);
            no.Root.sizeDelta = new Vector2(90f, 30f);
            no.Root.anchoredPosition = new Vector2(98f, -54f);
            _disconnectOverlay.SetAsLastSibling();
        }

        private void DisconnectFromRoom()
        {
            CloseDisconnectConfirmation();
            NetworkSystem system = NetworkSystem.Instance;
            if (system != null) system.ReturnToSinglePlayer();
        }

        public void CloseDisconnectConfirmation()
        {
            if (_disconnectOverlay != null) Destroy(_disconnectOverlay.gameObject);
            _disconnectOverlay = null;
            _disconnectPanel = null;
            RemoveInteractables(CascadeLayerGroup.Overlay);
        }

        // -----------------------------------------------------------------
        // Generic confirmation dialog (restart prompts, destructive actions)
        // -----------------------------------------------------------------

        /// <summary>
        /// Shows a small modal with a title, wrapped message and two buttons.
        /// The overlay dims the window and swallows clicks behind it.
        /// </summary>
        public void ShowConfirm(string title, string message, string confirmText, Action onConfirm)
        {
            CloseConfirm();
            CloseDropdown();
            CloseDisconnectConfirmation();

            _confirmOverlay = CascadeUI.Rect("ConfirmOverlay", _root);
            CascadeUI.Stretch(_confirmOverlay);
            Image dim = CascadeUI.Image(_confirmOverlay, CascadeSprites.White(), new Color(0f, 0f, 0f, 0.62f), Image.Type.Simple);
            dim.raycastTarget = false;
            Register(_confirmOverlay, CascadeLayerGroup.Overlay, () => { });

            _confirmPanel = CascadeUI.Center("Confirm", _confirmOverlay, 0f, 0f, 430f, 215f);
            CascadeUI.Panel(_confirmPanel, Theme.ViewBg, 10);
            RectTransform border = CascadeUI.Rect("Border", _confirmPanel);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, Theme.ViewBorder, 10);

            RectTransform titleRect = CascadeUI.Center("Title", _confirmPanel, 0f, 58f, 390f, 30f);
            CascadeUI.Text(titleRect, title, 17f, Theme.TextPrimary, TextAlignmentOptions.Center, true);

            RectTransform messageRect = CascadeUI.Center("Message", _confirmPanel, 0f, 10f, 380f, 54f);
            TextMeshProUGUI label = CascadeUI.Text(
                messageRect, message, 12f, Theme.TextSecondary, TextAlignmentOptions.Center, false, true);
            label.overflowMode = TextOverflowModes.Overflow;

            CascadeButtonView yes = CascadeWidgets.Button(this, _confirmPanel, confirmText, true,
                () =>
                {
                    CloseConfirm();
                    onConfirm?.Invoke();
                }, CascadeLayerGroup.Overlay);
            yes.Root.anchorMin = yes.Root.anchorMax = new Vector2(0.5f, 0.5f);
            yes.Root.pivot = new Vector2(0.5f, 0.5f);
            yes.Root.sizeDelta = new Vector2(180f, 30f);
            yes.Root.anchoredPosition = new Vector2(-98f, -68f);

            CascadeButtonView no = CascadeWidgets.Button(this, _confirmPanel, "Later", false,
                CloseConfirm, CascadeLayerGroup.Overlay);
            no.Root.anchorMin = no.Root.anchorMax = new Vector2(0.5f, 0.5f);
            no.Root.pivot = new Vector2(0.5f, 0.5f);
            no.Root.sizeDelta = new Vector2(90f, 30f);
            no.Root.anchoredPosition = new Vector2(104f, -68f);

            _confirmOverlay.SetAsLastSibling();
        }

        public void CloseConfirm()
        {
            if (_confirmOverlay != null) Destroy(_confirmOverlay.gameObject);
            _confirmOverlay = null;
            _confirmPanel = null;
            RemoveInteractables(CascadeLayerGroup.Overlay);
        }

        public void CloseDropdown()
        {
            if (_dropdownBlocker != null) Destroy(_dropdownBlocker);
            if (_dropdown != null) Destroy(_dropdown);
            _dropdownBlocker = null;
            _dropdown = null;
            _dropdownPanel = null;
            RemoveInteractables(CascadeLayerGroup.Overlay);
        }

        // -----------------------------------------------------------------
        // Toasts
        // -----------------------------------------------------------------

        private void BuildToasts()
        {
            _toastContainer = CascadeUI.TopRight("Toasts", _root, 10f, 10f, 300f, 0f);
            _toastContainer.pivot = new Vector2(1f, 1f);
            var layout = _toastContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _toastContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void ShowToast(string title, string message)
        {
            if (_toastContainer == null) return;

            RectTransform toast = CascadeUI.Rect("Toast", _toastContainer);
            var layoutElement = toast.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 58f;
            layoutElement.minHeight = 58f;
            var group = toast.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image background = CascadeUI.Panel(toast, Theme.ToastBg, 10);
            background.raycastTarget = false;

            RectTransform border = CascadeUI.Rect("Border", toast);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, Theme.ToastBorder, 10);
            border.GetComponent<Image>().raycastTarget = false;

            RectTransform iconRect = CascadeUI.TopLeft("Icon", toast, 13f, 16f, 26f, 26f);
            CascadeUI.Image(iconRect, CascadeSprites.Circle(), Color.white, Image.Type.Simple);
            var iconGradient = iconRect.gameObject.AddComponent<CascadeGradient>();
            iconGradient.Top = Theme.ButtonPrimaryTop;
            iconGradient.Bottom = Theme.ButtonPrimaryBottom;
            RectTransform icon = CascadeUI.Center("Glyph", iconRect, 0f, 0f, 13f, 13f);
            CascadeUI.Image(icon, CascadeIcons.Get(CascadeIcon.Check), Color.white, Image.Type.Simple);

            RectTransform titleRect = LineRect("Title", toast, 50f, 28f, 8f, 21f);
            CascadeUI.Text(titleRect, title, 12f, Theme.TextPrimary, TextAlignmentOptions.Left, true);

            RectTransform messageRect = LineRect("Message", toast, 50f, 28f, 28f, 20f);
            CascadeUI.Text(messageRect, CascadeUI.Truncate(message, 52), 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);

            RectTransform close = CascadeUI.TopRight("Close", toast, 12f, 21f, 9f, 9f);
            CascadeUI.Image(close, CascadeIcons.Get(CascadeIcon.Close), Theme.TextTertiary, Image.Type.Simple);

            var view = toast.gameObject.AddComponent<CascadeToastView>();
            view.Group = group;
            view.Window = this;

            Register(toast, CascadeLayerGroup.Toast, () => view.Dismiss());
            LayoutRebuilder.MarkLayoutForRebuild(_toastContainer);
        }

        internal void MarkToastLayout()
        {
            if (_toastContainer != null) LayoutRebuilder.MarkLayoutForRebuild(_toastContainer);
        }

        // -----------------------------------------------------------------
        // Interaction
        // -----------------------------------------------------------------

        public CascadeInteractable Register(
            RectTransform rect,
            CascadeLayerGroup group,
            Action click = null,
            Action press = null,
            Action release = null,
            Action<Vector3> dragWorld = null,
            Action<bool> hover = null,
            bool slider = false)
        {
            var interactable = new CascadeInteractable
            {
                Rect = rect,
                Group = group,
                Order = Interactables.Count,
                Click = click,
                Press = press,
                Release = release,
                DragWorld = dragWorld,
                Hover = hover,
                IsSlider = slider
            };
            Interactables.Add(interactable);
            return interactable;
        }

        /// <summary>
        /// Removes an interactable registered from outside the window (the
        /// spectate overlay does this when the window is rebuilt or closed).
        /// </summary>
        public void Unregister(CascadeInteractable interactable)
        {
            if (interactable == null) return;
            Interactables.Remove(interactable);
            for (int i = 0; i < Interactables.Count; i++) Interactables[i].Order = i;
        }

        /// <summary>Root rect of the window; the menu moves it when dragged.</summary>
        public RectTransform RootRect
        {
            get { return _root; }
        }

        /// <summary>Moves the window inside the canvas (canvas units).</summary>
        public void SetWindowPosition(Vector2 anchoredPosition)
        {
            if (_root != null) _root.anchoredPosition = anchoredPosition;
        }

        /// <summary>Local rotation of the window (relative to the canvas).</summary>
        public Quaternion WindowRotation
        {
            get { return _root != null ? _root.localRotation : Quaternion.identity; }
        }

        /// <summary>Rotates the window in place.</summary>
        public void SetWindowRotation(Quaternion localRotation)
        {
            if (_root != null) _root.localRotation = localRotation;
        }

        /// <summary>True when the point is inside the window rectangle.</summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            if (_root == null) return false;

            Vector3 local = _root.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.z) < 60f && _root.rect.Contains(new Vector2(local.x, local.y));
        }

        /// <summary>
        /// True when the point is on the empty part of the titlebar. Titlebar
        /// buttons are interactables, so a press over them never starts a drag.
        /// </summary>
        public bool IsTitlebarPoint(Vector3 worldPoint)
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return false;

            Vector3 local = _root.InverseTransformPoint(worldPoint);
            Rect rect = _root.rect;
            float top = rect.yMax;
            return Mathf.Abs(local.z) < 60f &&
                   local.y <= top &&
                   local.y >= top - CascadeTheme.TitlebarHeight &&
                   local.x >= rect.xMin &&
                   local.x <= rect.xMax;
        }

        /// <summary>Centre of the window in canvas units.</summary>
        public Vector2 WindowPosition
        {
            get { return _root != null ? _root.anchoredPosition : Vector2.zero; }
        }

        public CascadeInteractable HitTest(Vector3 worldPoint)
        {            CascadeInteractable best = null;
            for (int i = Interactables.Count - 1; i >= 0; i--)
            {
                CascadeInteractable interactable = Interactables[i];
                if (interactable == null || interactable.Rect == null)
                {
                    Interactables.RemoveAt(i);
                    continue;
                }
                if (!interactable.Enabled || !interactable.IsAlive) continue;
                if (interactable.Group == CascadeLayerGroup.Content && !InsideViewport(worldPoint)) continue;
                if (!interactable.Contains(worldPoint)) continue;
                if (best == null ||
                    interactable.LayerRank > best.LayerRank ||
                    (interactable.LayerRank == best.LayerRank && interactable.Order > best.Order))
                {
                    best = interactable;
                }
            }
            return best;
        }

        private bool InsideViewport(Vector3 worldPoint)
        {
            if (_viewport == null) return false;
            Vector3 local = _viewport.InverseTransformPoint(worldPoint);
            return _viewport.rect.Contains(new Vector2(local.x, local.y));
        }

        private void RemoveInteractables(params CascadeLayerGroup[] groups)
        {
            for (int i = Interactables.Count - 1; i >= 0; i--)
            {
                CascadeInteractable interactable = Interactables[i];
                if (interactable == null || interactable.Rect == null)
                {
                    Interactables.RemoveAt(i);
                    continue;
                }
                for (int g = 0; g < groups.Length; g++)
                {
                    if (interactable.Group == groups[g])
                    {
                        Interactables.RemoveAt(i);
                        break;
                    }
                }
            }

            for (int i = 0; i < Interactables.Count; i++) Interactables[i].Order = i;
        }

        public void Scroll(float delta)
        {
            if (_scroll == null) return;
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + delta);
        }

        public void ScrollToTop()
        {
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>Scrolls the sidebar tab list (0 = top, 1 = bottom).</summary>
        public void ScrollSidebar(float delta)
        {
            if (_sidebarScroll == null) return;
            _sidebarScroll.verticalNormalizedPosition =
                Mathf.Clamp01(_sidebarScroll.verticalNormalizedPosition + delta);
        }

        /// <summary>True while the given screen point is over the sidebar.</summary>
        public bool SidebarContains(Vector2 screenPoint)
        {
            if (_sidebar == null || !_sidebarExpanded || _sidebarCurrent < 0.5f) return false;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_sidebar, screenPoint, cam);
        }

        /// <summary>True when the sidebar content is taller than its viewport.</summary>
        public bool SidebarScrollable
        {
            get
            {
                if (_sidebarList == null || _sidebarViewport == null) return false;
                return _sidebarList.rect.height > _sidebarViewport.rect.height + 1f;
            }
        }

        // -----------------------------------------------------------------
        // Update
        // -----------------------------------------------------------------

        private void Update()
        {
            if (!_built) return;

            // The on-screen keyboard only exists in VR. On desktop the search
            // field keeps working with the physical keyboard only.
            if (_keyboard != null) _keyboard.SetVisible(_searchFocused && XRSettings.isDeviceActive);

            _sidebarTarget = _sidebarExpanded ? CascadeTheme.SidebarWidth : 0f;
            if (_config != null && !_config.Animations.Value)
            {
                _sidebarCurrent = _sidebarTarget;
            }
            else
            {
                _sidebarCurrent = Mathf.Lerp(_sidebarCurrent, _sidebarTarget, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
                if (Mathf.Abs(_sidebarCurrent - _sidebarTarget) < 0.15f) _sidebarCurrent = _sidebarTarget;
            }

            if (_sidebar != null)
            {
                _sidebar.sizeDelta = new Vector2(_sidebarCurrent, 0f);
                if (!_sidebarExpanded && _sidebarCurrent < 0.5f) _sidebar.gameObject.SetActive(false);
            }
            if (_separator != null)
            {
                _separator.anchoredPosition = new Vector2(_sidebarCurrent, 0f);
                _separator.gameObject.SetActive(_sidebarCurrent > 0.5f);
            }
            if (_contentBody != null)
            {
                _contentBody.offsetMin = new Vector2(_sidebarCurrent + 1f, 0f);
            }
            bool sidebarVisible = _sidebarCurrent > 0.5f;
            if (_contentFlatLeft != null) _contentFlatLeft.gameObject.SetActive(sidebarVisible);
            if (_titlebarFlatLeft != null) _titlebarFlatLeft.gameObject.SetActive(sidebarVisible);
            if (_sidebarGroup != null)
            {
                _sidebarGroup.alpha = Mathf.Clamp01(_sidebarCurrent / CascadeTheme.SidebarWidth);
                _sidebarGroup.interactable = _sidebarCurrent > 1f;
            }

            UpdateScrollbar();
        }

        private void UpdateScrollbar()
        {
            if (_scrollThumb == null || _scroll == null || _content == null || _viewport == null) return;
            float viewportHeight = _viewport.rect.height;
            float contentHeight = _content.rect.height;
            bool show = contentHeight > viewportHeight + 1f;
            _scrollThumb.gameObject.SetActive(show);
            if (!show) return;

            float trackHeight = Mathf.Max(1f, viewportHeight - 16f);
            float thumbHeight = Mathf.Clamp(viewportHeight * viewportHeight / Mathf.Max(1f, contentHeight), 28f, trackHeight);
            float y = (1f - _scroll.verticalNormalizedPosition) * (trackHeight - thumbHeight);
            _scrollThumb.sizeDelta = new Vector2(4f, thumbHeight);
            _scrollThumb.anchoredPosition = new Vector2(-6f, -8f - y);
        }

        private IEnumerator FadeViewport()
        {
            if (_fadeGroup == null) yield break;
            _fadeGroup.alpha = 0.35f;
            float time = 0f;
            while (time < 0.14f)
            {
                time += Time.unscaledDeltaTime;
                _fadeGroup.alpha = Mathf.Lerp(0.35f, 1f, time / 0.14f);
                yield return null;
            }
            _fadeGroup.alpha = 1f;
        }

        private void OnEnable()
        {
            if (_fadeGroup != null) _fadeGroup.alpha = 1f;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            CloseDropdown();
        }

        private void OnDestroy()
        {
            CloseDisconnectConfirmation();
            CloseDropdown();
            StopAllCoroutines();
        }
    }

    internal sealed class CascadeToastView : MonoBehaviour
    {
        public CanvasGroup Group;
        public CascadeWindow Window;
        public float Life = 4.2f;

        private float _age;
        private bool _closing;

        public void Dismiss()
        {
            if (!_closing) _closing = true;
        }

        private void Update()
        {
            if (Group == null) return;
            float dt = Time.unscaledDeltaTime;
            _age += dt;

            if (!_closing)
            {
                float enter = Mathf.Clamp01(_age / 0.16f);
                Group.alpha = enter;
                transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, enter);
                if (_age > Life) Dismiss();
                return;
            }

            Group.alpha -= dt * 3.4f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, Mathf.Clamp01(Group.alpha));
            if (Group.alpha > 0f) return;

            if (Window != null) Window.MarkToastLayout();
            Destroy(gameObject);
        }
    }
}
