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
    internal sealed partial class CascadeWindow : MonoBehaviour
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
        private MenuState _state;
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
        private CascadeTextField _focusedTextField;
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
            MenuState state,
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
            ApplySavedSize();
            _root.localScale = Vector3.one;

            BuildFrame();
            BuildSidebar();
            BuildTitlebar();
            BuildViewport();
            _keyboard = new CascadeKeyboard();
            _keyboard.Build(this, _contentBody);
            BuildToasts();
            BuildResizeGrip();
            if (_windowBorder != null) _windowBorder.SetAsLastSibling();

            _history.Clear();
            _history.Add(Mathf.Clamp(pageIndex, 0, Mathf.Max(0, Pages.Count - 1)));
            _historyIndex = 0;

            _built = true;
            SetPage(_history[0], false);
        }

        // -----------------------------------------------------------------
        // Pages
        // -----------------------------------------------------------------

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
            BlurInput();

            if (recordHistory)
            {
                if (_historyIndex < _history.Count - 1)
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                _history.Add(index);
                _historyIndex = _history.Count - 1;
            }

            SetPage(index, true);
        }

        public void GoBack()
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
            _focusedTextField = null;
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

                // Tabs inside tabs: a chip bar picks one section (hidden while searching).
                int subTab = SubTabAll;
                if (string.IsNullOrEmpty(_searchQuery) && UsesSubTabs(page))
                {
                    subTab = GetSubTab(page);
                    BuildSubTabBar(page, subTab);
                    any = true;
                }

                for (int sectionIndex = 0; sectionIndex < page.Sections.Count; sectionIndex++)
                {
                    if (subTab != SubTabAll && sectionIndex != subTab) continue;
                    CascadeSection section = page.Sections[sectionIndex];
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

        /// <summary>True while the search box or any inline text field has focus.</summary>
        public bool IsTyping
        {
            get { return _searchFocused || _focusedTextField != null; }
        }

        public bool TextFieldFocused
        {
            get { return _focusedTextField != null; }
        }

        /// <summary>Routes typing to one inline field, blurring the search box.</summary>
        public void FocusTextField(CascadeTextField field)
        {
            if (field == null) return;
            BlurSearch();
            if (_focusedTextField != null && _focusedTextField != field)
            {
                _focusedTextField.SetFocused(false);
            }
            _focusedTextField = field;
            field.SetFocused(true);
            if (_keyboard != null) _keyboard.SetVisible(true);
        }

        public void BlurTextField()
        {
            if (_focusedTextField != null)
            {
                _focusedTextField.SetFocused(false);
                _focusedTextField = null;
            }
        }

        /// <summary>Applies the focused field and leaves edit mode.</summary>
        public void CommitTextField()
        {
            if (_focusedTextField == null) return;
            _focusedTextField.Commit();
            _focusedTextField = null;
        }

        /// <summary>Leaves any text entry mode (used by Escape).</summary>
        public void BlurInput()
        {
            BlurSearch();
            BlurTextField();
        }

        /// <summary>Keyboard "Done": applies an inline field, then leaves text entry.</summary>
        internal void FinishInput()
        {
            if (_focusedTextField != null) CommitTextField();
            BlurInput();
        }

        /// <summary>Keyboard "Clear": empties whichever box currently has focus.</summary>
        internal void ClearInput()
        {
            if (_focusedTextField != null)
            {
                for (int i = 0; i < 128; i++) _focusedTextField.Backspace();
                return;
            }
            ClearSearch();
        }

        /// <summary>
        /// Repaints the window after the live theme was mutated. Registered
        /// graphics are recolored by the binding cache; this refreshes the few
        /// widgets that cache a computed color of their own.
        /// </summary>
        public void ApplyTheme()
        {
            if (!_built) return;
            if (_menu != null) Theme = _menu.Theme;
            if (Theme == null) return;

            CascadeTheme.Active = Theme;
            Theme.RecolorAll();

            if (SearchField != null) SearchField.RefreshTheme();
            if (_focusedTextField != null) _focusedTextField.RefreshTheme();
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
            if (_keyboard != null) _keyboard.SetVisible(true);
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
            if (_focusedTextField != null)
            {
                _focusedTextField.Append(character);
                return;
            }
            if (_searchFocused && SearchField != null) SearchField.Append(character);
        }

        public void HandleBackspace()
        {
            if (_focusedTextField != null)
            {
                _focusedTextField.Backspace();
                return;
            }
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
                // Unbound raw color: the option stays transparent until it is
                // hovered, so a live recolor cannot make every row look hovered.
                Image hover = CascadeUI.Panel(option, (Color)Theme.RowHover, 5);
                hover.color = Theme.RowHover.WithAlpha(0f);

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
                    hover.color = Theme.RowHover.WithAlpha(hovered ? Theme.RowHover.a : 0f);
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
            Image dim = CascadeUI.Image(_disconnectOverlay, CascadeSprites.White(), Theme.ModalDim, Image.Type.Simple);
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
            Image dim = CascadeUI.Image(_confirmOverlay, CascadeSprites.White(), Theme.ModalDim, Image.Type.Simple);
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

            // The on-screen keyboard shows on PC and in VR whenever a text box is focused.
            if (_keyboard != null) _keyboard.SetVisible(IsTyping);

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