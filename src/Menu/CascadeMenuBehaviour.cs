using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using CascadeMenu.Mods;
using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace CascadeMenu
{
    internal sealed class CascadeMenuBehaviour : MonoBehaviour
    {
        private const float PressCooldown = 0.15f;

        private static CascadeMenuBehaviour _instance;

        private MenuConfig _config;

        private DemoState _demo;

        private List<CascadePage> _pages;

        private CascadeTheme _theme;

        private GameObject _canvasObject;

        private Canvas _canvas;

        private RectTransform _canvasRect;

        private CanvasScaler _canvasScaler;

        private bool _screenMode;

        private float _nextModeCheck;

        private CascadeWindow _window;

        // Live first/third person view of another player. The overlay is a
        // sibling of the window so it survives window rebuilds while the player
        // list changes underneath it.
        private CascadeSpectateOverlay _spectateOverlay;

        private CanvasGroup _windowGroup;

        private LineRenderer _pointerLine;

        private LineRenderer _pointerRing;

        private LineRenderer _pointerRingShadow;

        private GameObject _pointerDot;

        private Renderer _pointerDotRenderer;

        private Material _pointerMaterial;

        private GameObject _desktopCursorObject;

        private RectTransform _desktopCursorRect;

        private Image _desktopCursorDot;

        private bool _desktopPointer;

        private Keyboard _keyboard;

        private bool _textSubscribed;

        private bool _open;

        private bool _rebuildQueued;

        private bool _scrollTopQueued;

        private bool _snap = true;

        private int _lastPage;

        private float _lastScroll = 1f;

        private string _shopQuery = string.Empty;

        private bool _shopQueryDirty;

        private float _shopQueryTime;

        private bool _previousToggle;

        private bool _previousDown;

        private CascadeInteractable _hovered;

        private CascadeInteractable _pressed;

        private CascadeInteractable _draggingSlider;

        private Vector3 _hitPoint;

        private Vector3 _smoothPointerOrigin;

        private Vector3 _smoothPointerDirection = Vector3.forward;

        private Vector3 _smoothHit;

        private bool _pointerSmoothed;

        private bool _hitSmoothed;

        private bool _cursorSaved;

        private CursorLockMode _savedCursorLock;

        private bool _savedCursorVisible;

        private string _pendingToastTitle;

        private string _pendingToastMessage;

        private static Camera _lastViewCamera;

        private static Camera _cachedCamera;

        private static int _cachedFrame = -1;

        private float _lastPlayerListCheck;

        private string _playerListSignature;

        private bool _playerListDirty;

        // Room event tracking for join / leave / master notifications. The
        // roster is diffed on a timer so toasts also appear while the menu is
        // closed (they use StandaloneNotificationOverlay then).
        private readonly HashSet<string> _knownPlayers = new HashSet<string>();

        private readonly Dictionary<string, string> _knownPlayerNames = new Dictionary<string, string>();

        private string _knownRoom = string.Empty;

        private string _knownMaster = string.Empty;

        private bool _roomEventsReady;

        private float _nextRoomEventCheck;

        // Last visited tab, kept here and mirrored into the config file so it
        // survives a menu close/open and even a game restart.
        private string _lastPageTag = string.Empty;

        private string _lastPageTitle = string.Empty;

        // Which bind is currently waiting for a key press ("menu" / "acrobatics").
        private string _rebindTarget;

        // When the rebind wait gives up, so a stray click cannot leave the menu
        // listening for a key forever.
        private float _rebindDeadline;

        internal static bool IsMenuOpen
        {
            get
            {
                if ((object)_instance != null)
                {
                    return _instance._open;
                }
                return false;
            }
        }

        internal static CascadeMenuBehaviour Instance => _instance;

        internal MenuConfig Config => _config;

        private void Start()
        {
            _instance = this;
            _config = new MenuConfig(Plugin.Instance.Config);
            GameplayMods.Initialize(_config, base.gameObject);
            Plugin.Root.AddComponent<StandaloneNotificationOverlay>().Initialize(_config);
            Plugin.Root.AddComponent<HudOverlay>().Initialize(_config);
            _demo = new DemoState();
            _lastPageTag = _config.LastPageTag.Value;
            _lastPageTitle = _config.LastPageTitle.Value;
            _pages = DemoApp.Build(this, _config, _demo);
            BuildCanvas();
            BuildPointer();
            Plugin.Log.LogInfo("Cascade menu ready. " + Keybinds.Name(Keybinds.Parse(_config.MenuKey.Value, Key.F1)) +
                               " or Insert on keyboard, secondary button on the chosen hand in VR.");
        }

        private void BuildCanvas()
        {
            _canvasObject = new GameObject("CascadeMenu_Canvas", new Type[] { typeof(RectTransform), typeof(Canvas) });
            Object.DontDestroyOnLoad(_canvasObject);
            _canvasRect = (RectTransform)_canvasObject.transform;
            _canvasRect.sizeDelta = new Vector2(1200f, 700f);
            _canvasRect.localScale = Vector3.one * 0.001f;
            _canvas = _canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;
            _canvas.worldCamera = ResolveViewCamera();
            _canvasScaler = _canvasObject.GetComponent<CanvasScaler>();
            if (_canvasScaler == null)
            {
                _canvasScaler = _canvasObject.AddComponent<CanvasScaler>();
            }
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _canvasScaler.matchWidthOrHeight = 0.5f;
            _canvasScaler.enabled = false;
            ApplyUiMode(log: true);
        }

        private void ApplyUiMode(bool log)
        {
            bool flag = IsVrActive();
            int num = (_config != null) ? _config.UiMode.Value : 0;
            bool flag2 = num switch
            {
                1 => true,
                2 => false,
                _ => !flag,
            };
            if (flag2 == _screenMode && !log)
            {
                return;
            }
            _screenMode = flag2;
            if (_canvas == null || _canvasRect == null)
            {
                return;
            }
            if (flag2)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.worldCamera = null;
                _canvasRect.anchorMin = Vector2.zero;
                _canvasRect.anchorMax = Vector2.one;
                _canvasRect.pivot = new Vector2(0.5f, 0.5f);
                _canvasRect.offsetMin = Vector2.zero;
                _canvasRect.offsetMax = Vector2.zero;
                _canvasRect.localRotation = Quaternion.identity;
                _canvasRect.localScale = Vector3.one;
                _canvasRect.localPosition = Vector3.zero;
                if (_canvasScaler != null)
                {
                    _canvasScaler.enabled = true;
                }
            }
            else
            {
                _canvas.renderMode = RenderMode.WorldSpace;
                if (_canvasScaler != null)
                {
                    _canvasScaler.enabled = false;
                }
                _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
                _canvasRect.pivot = new Vector2(0.5f, 0.5f);
                _canvasRect.sizeDelta = new Vector2(1200f, 700f);
                _canvasRect.localScale = Vector3.one * 0.001f;
                _canvas.worldCamera = ResolveViewCamera();
                _snap = true;
            }
            if (log)
            {
                string text = num switch
                {
                    1 => "forced PC",
                    2 => "forced VR",
                    _ => "auto",
                };
                Plugin.Log.LogInfo("Cascade UI mode: " + (_screenMode ? "PC screen-space" : "VR world-space") + " (" + text + ", headset " + (flag ? "active" : "not active") + ").");
            }
        }

        internal static bool IsVrActive()
        {
            try
            {
                if (XRSettings.isDeviceActive)
                {
                    return true;
                }
                string loadedDeviceName = XRSettings.loadedDeviceName;
                if (!string.IsNullOrEmpty(loadedDeviceName) && loadedDeviceName.IndexOf("None", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Cascade XR detection failed: " + ex.Message);
            }
            return false;
        }

        private void BuildPointer()
        {
            _pointerMaterial = new Material(FindShader(new string[] { "Sprites/Default", "UI/Default", "Unlit/Color" }))
            {
                color = Color.white
            };
            _pointerMaterial.renderQueue = 4100;
            GameObject gameObject = new GameObject("CascadePointerLine");
            gameObject.transform.SetParent(base.transform, false);
            _pointerLine = gameObject.AddComponent<LineRenderer>();
            _pointerLine.positionCount = 2;
            _pointerLine.useWorldSpace = true;
            _pointerLine.startWidth = 0.0042f;
            _pointerLine.endWidth = 0.0022f;
            _pointerLine.numCapVertices = 6;
            _pointerLine.numCornerVertices = 2;
            _pointerLine.alignment = LineAlignment.View;
            _pointerLine.shadowCastingMode = ShadowCastingMode.Off;
            _pointerLine.receiveShadows = false;
            _pointerLine.material = _pointerMaterial;
            _pointerLine.sortingOrder = 310;
            _pointerLine.enabled = false;
            _pointerRingShadow = BuildPointerRing("CascadePointerRingShadow", 0.0065f, 319);
            _pointerRingShadow.startColor = new Color(0f, 0f, 0f, 0.62f);
            _pointerRingShadow.endColor = new Color(0f, 0f, 0f, 0.62f);
            _pointerRing = BuildPointerRing("CascadePointerRing", 0.0032f, 320);
            _pointerRing.startColor = Color.white;
            _pointerRing.endColor = Color.white;
            _pointerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _pointerDot.name = "CascadePointerDot";
            Collider component = _pointerDot.GetComponent<Collider>();
            if (component != null)
            {
                Object.Destroy(component);
            }
            _pointerDot.transform.SetParent(base.transform, false);
            _pointerDot.transform.localScale = Vector3.one * 0.008f;
            _pointerDotRenderer = _pointerDot.GetComponent<Renderer>();
            if (_pointerDotRenderer != null)
            {
                _pointerDotRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _pointerDotRenderer.receiveShadows = false;
                _pointerDotRenderer.sortingOrder = 330;
                Material material = new Material(FindShader(new string[] { "Sprites/Default", "UI/Default", "Unlit/Color" }));
                material.renderQueue = 4100;
                _pointerDotRenderer.material = material;
            }
            _pointerDot.SetActive(false);
            BuildDesktopCursor();
        }

        private LineRenderer BuildPointerRing(string name, float width, int sortingOrder)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(base.transform, false);
            LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 24;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.material = _pointerMaterial;
            lineRenderer.sortingOrder = sortingOrder;
            lineRenderer.enabled = false;
            return lineRenderer;
        }

        private void BuildDesktopCursor()
        {
            _desktopCursorObject = new GameObject("CascadeMenu_Cursor", new Type[] { typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler) });
            Object.DontDestroyOnLoad(_desktopCursorObject);
            Canvas canvas = _desktopCursorObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = _desktopCursorObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            GameObject cursorObject = new GameObject("Cursor", new Type[] { typeof(RectTransform) });
            _desktopCursorRect = (RectTransform)cursorObject.transform;
            _desktopCursorRect.SetParent(_desktopCursorObject.transform, false);
            RectTransform desktopCursorRect = _desktopCursorRect;
            desktopCursorRect.anchorMin = new Vector2(0f, 0f);
            desktopCursorRect.anchorMax = new Vector2(0f, 0f);
            _desktopCursorRect.pivot = new Vector2(0.5f, 0.5f);
            _desktopCursorRect.anchoredPosition = Vector2.zero;
            _desktopCursorRect.sizeDelta = new Vector2(28f, 28f);
            CascadeUI.Image(CursorImageRect("Shadow", 38f), CascadeSprites.CircleRing(9f), new Color(0f, 0f, 0f, 0.58f), Image.Type.Simple);
            CascadeUI.Image(CursorImageRect("Ring", 28f), CascadeSprites.CircleRing(5f), Color.white, Image.Type.Simple);
            RectTransform rectTransform = CursorImageRect("Dot", 5f);
            _desktopCursorDot = CascadeUI.Image(rectTransform, CascadeSprites.Circle(), Color.white, Image.Type.Simple);
            _desktopCursorObject.SetActive(false);
        }

        private RectTransform CursorImageRect(string name, float size)
        {
            RectTransform rectTransform = (RectTransform)new GameObject(name, new Type[] { typeof(RectTransform) }).transform;
            rectTransform.SetParent(_desktopCursorRect, false);
            Vector2 vector = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = vector;
            rectTransform.anchorMin = vector;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(size, size);
            return rectTransform;
        }

        private static Shader FindShader(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null)
                {
                    return shader;
                }
            }
            return Shader.Find("UI/Default");
        }

        internal static Camera ResolveViewCamera()
        {
            if (_cachedFrame == Time.frameCount && _cachedCamera != null)
            {
                return _cachedCamera;
            }
            Camera main = Camera.main;
            if (XRSettings.isDeviceActive)
            {
                _cachedFrame = Time.frameCount;
                _cachedCamera = main;
                return main;
            }
            Camera camera = main;
            float num = (main != null) ? main.depth : float.NegativeInfinity;
            foreach (Camera camera2 in Camera.allCameras)
            {
                if (camera2 != null && camera2.isActiveAndEnabled && camera2.targetTexture == null && camera2 != main)
                {
                    string text = camera2.name.ToLowerInvariant();
                    if (!text.Contains("ui") && !text.Contains("hud") && !text.Contains("cursor") && !text.Contains("overlay"))
                    {
                        if (text.Contains("third") || text.Contains("thirdperson") || text.Contains("spectator") || text.Contains("tp camera"))
                        {
                            camera = camera2;
                            break;
                        }
                        if (camera2.depth > num + 0.001f)
                        {
                            num = camera2.depth;
                            camera = camera2;
                        }
                    }
                }
            }
            if (camera != _lastViewCamera)
            {
                _lastViewCamera = camera;
                Plugin.Log.LogInfo("Cascade view camera: " + ((camera != null) ? (camera.name + " (depth " + camera.depth + ")") : "none"));
            }
            _cachedFrame = Time.frameCount;
            _cachedCamera = camera;
            if (camera == null)
            {
                return main;
            }
            return camera;
        }

        private void Update()
        {
            SubscribeTextInput();

            // A rebind row is waiting for the next key press. The menu keeps
            // responding (pointer, scrolling, buttons) while we listen; only the
            // global shortcuts and the toggle key are suppressed so the new bind
            // cannot also open/close the menu.
            if (_rebindTarget != null)
            {
                UpdateRebind();
            }

            if (Time.unscaledTime >= _nextModeCheck)
            {
                _nextModeCheck = Time.unscaledTime + 1f;
                ApplyUiMode(false);
            }
            if (_rebuildQueued)
            {
                _rebuildQueued = false;
                if (_open)
                {
                    RebuildWindow();
                }
            }
            if (_shopQueryDirty && Time.unscaledTime - _shopQueryTime >= 0.18f)
            {
                _shopQueryDirty = false;
                if (_open)
                {
                    RebuildWindow();
                }
            }
            if (_rebindTarget == null)
            {
                HandleToggleInput();
            }

            // Runs even when the menu is closed so join/leave/master alerts are
            // visible in game.
            UpdateRoomNotifications();

            if (!_open)
            {
                return;
            }
            if (_window == null)
            {
                CloseMenu();
                return;
            }

            // The rig is destroyed when its owner leaves the room. Close the
            // feed and put the menu back instead of showing a frozen frame.
            if (_spectateOverlay != null && !SpectateController.IsActive)
            {
                StopSpectating();
                ShowToast("Spectate ended", "That player's rig is no longer available.");
            }

            if (_rebindTarget == null)
            {
                HandleKeyboardShortcuts();
            }
            if (!_open)
            {
                return;
            }
            if (_desktopPointer)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            UpdateCanvasTransform(forceSnap: false);
            UpdateInteraction();
            HandleScroll();
            UpdateLivePlayerList();
        }

        /// <summary>
        /// Diffs the room roster and master client every half second and turns
        /// the changes into notifications. Each event has its own config toggle,
        /// shown on the Notifications page.
        /// </summary>
        private void UpdateRoomNotifications()
        {
            if (Time.unscaledTime < _nextRoomEventCheck)
            {
                return;
            }
            _nextRoomEventCheck = Time.unscaledTime + 0.5f;

            NetworkSystem system = NetworkSystem.Instance;
            if (system == null || !system.InRoom)
            {
                _roomEventsReady = false;
                _knownPlayers.Clear();
                _knownPlayerNames.Clear();
                _knownRoom = string.Empty;
                _knownMaster = string.Empty;
                return;
            }

            NetPlayer local = PlayerActions.LocalPlayer();
            string localId = local != null && !string.IsNullOrEmpty(local.UserId) ? local.UserId : "local";
            bool localMaster = system.IsMasterClient;
            NetPlayer master = system.MasterClient;
            string masterId = master != null && !string.IsNullOrEmpty(master.UserId)
                ? master.UserId
                : (localMaster ? localId : string.Empty);
            string room = system.RoomName ?? string.Empty;
            List<NetPlayer> players = PlayerActions.GetPlayers();

            if (!_roomEventsReady || room != _knownRoom)
            {
                bool changed = _roomEventsReady && room != _knownRoom;
                _roomEventsReady = true;
                _knownRoom = room;
                _knownMaster = masterId;
                _knownPlayers.Clear();
                _knownPlayerNames.Clear();
                RememberPlayers(players);
                if (changed && _config != null && _config.NotifyRoom.Value)
                {
                    ShowToast("Room changed", string.IsNullOrEmpty(room) ? "Joined a new room." : room);
                }
                return;
            }

            bool joinsEnabled = _config != null && _config.NotifyJoins.Value;
            bool leavesEnabled = _config != null && _config.NotifyLeaves.Value;
            bool modderAlerts = _config != null && _config.NotifyModder.Value;
            var current = new HashSet<string>();
            var joinedNames = new List<string>();
            var joinedPlayers = new List<NetPlayer>();
            for (int i = 0; i < players.Count; i++)
            {
                NetPlayer player = players[i];
                if (player == null || string.IsNullOrEmpty(player.UserId)) continue;
                current.Add(player.UserId);
                _knownPlayerNames[player.UserId] = PlayerActions.SafeName(player);
                if (!_knownPlayers.Contains(player.UserId))
                {
                    joinedNames.Add(PlayerActions.SafeName(player));
                    joinedPlayers.Add(player);
                }
            }

            var leftNames = new List<string>();
            foreach (string id in _knownPlayers)
            {
                if (current.Contains(id)) continue;
                string name;
                leftNames.Add(_knownPlayerNames.TryGetValue(id, out name) && !string.IsNullOrEmpty(name) ? name : id);
            }

            _knownPlayers.Clear();
            foreach (string id in current) _knownPlayers.Add(id);

            if (joinsEnabled && joinedNames.Count > 0)
            {
                ShowToast(
                    joinedNames.Count == 1 ? "Player joined" : "Players joined",
                    DescribeNames(joinedNames));
            }

            if (leavesEnabled && leftNames.Count > 0)
            {
                ShowToast(
                    leftNames.Count == 1 ? "Player left" : "Players left",
                    DescribeNames(leftNames));
            }

            if (modderAlerts && joinedPlayers.Count > 0)
            {
                for (int i = 0; i < joinedPlayers.Count; i++)
                {
                    string summary;
                    if (!PlayerActions.IsFlaggedModder(joinedPlayers[i], out summary)) continue;
                    ShowToast("Possible modder", PlayerActions.SafeName(joinedPlayers[i]) + " - " + summary + ".");
                    break;
                }
            }

            if (masterId != _knownMaster)
            {
                bool hadMaster = !string.IsNullOrEmpty(_knownMaster);
                _knownMaster = masterId;
                if (!hadMaster || _config == null) return;
                if (localMaster && _config.NotifyMaster.Value)
                {
                    ShowToast("You are master client", "You now host this room.");
                }
                else if (!localMaster && _config.NotifyMasterChanges.Value)
                {
                    string name = master != null ? PlayerActions.SafeName(master) : "Another player";
                    ShowToast("Master changed", name + " is now the master client.");
                }
            }
        }

        private void RememberPlayers(List<NetPlayer> players)
        {
            for (int i = 0; i < players.Count; i++)
            {
                NetPlayer player = players[i];
                if (player == null || string.IsNullOrEmpty(player.UserId)) continue;
                _knownPlayers.Add(player.UserId);
                _knownPlayerNames[player.UserId] = PlayerActions.SafeName(player);
            }
        }

        private static string DescribeNames(List<string> names)
        {
            const int max = 4;
            int shown = Mathf.Min(max, names.Count);
            string text = string.Join(", ", names.GetRange(0, shown).ToArray());
            if (names.Count > shown) text += " and " + (names.Count - shown) + " more";
            return text;
        }

        private void UpdateLivePlayerList()
        {
            if (_window == null)
            {
                return;
            }
            if (Time.unscaledTime - _lastPlayerListCheck < 0.75f)
            {
                return;
            }
            _lastPlayerListCheck = Time.unscaledTime;
            string text = PlayerActions.PlayerSignature();
            if (text != _playerListSignature)
            {
                _playerListSignature = text;
                _playerListDirty = true;
            }
            CascadePage currentPage = _window.CurrentPage;
            if (currentPage == null)
            {
                return;
            }
            string userId;
            if (PlayerActions.IsPlayerPage(currentPage, out userId))
            {
                if (!PlayerActions.PlayerExists(userId))
                {
                    ShowToast("Player left", "That player is no longer in this room.");
                    GoToPlayersPage();
                }
                return;
            }
            if (!PlayerActions.IsPlayerListPage(currentPage) || !_playerListDirty)
            {
                return;
            }
            _playerListDirty = false;
            float scrollNormalized = _window.ScrollNormalized;
            RefreshPlayerList(silent: true);
            if (_window != null)
            {
                _window.ScrollTo(scrollNormalized);
            }
        }

        private void HandleToggleInput()
        {
            // Only the configured hand's B/Y button opens the menu: left by
            // default, right when the "Menu hand" setting is switched. Read both
            // the Input System XR device and the game's poller so a controller
            // layout that renames the button still works.
            bool useRightHand = _config != null && _config.MenuHand.Value == 1;
            bool handPressed = VrInput.Held(
                useRightHand ? VrButton.RightSecondary : VrButton.LeftSecondary);

            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (poller != null)
            {
                handPressed |= useRightHand
                    ? poller.rightControllerSecondaryButton
                    : poller.leftControllerSecondaryButton;
            }

            bool keyPressed = false;
            if (Keyboard.current != null)
            {
                Key menuKey = Keybinds.Parse(_config != null ? _config.MenuKey.Value : "F1", Key.F1);
                keyPressed = Keybinds.Pressed(menuKey) || Keyboard.current.insertKey.wasPressedThisFrame;
            }

            // Optional VR bind: any of the face buttons, trigger, grip or stick.
            bool buttonPressed = _config != null &&
                VrInput.Pressed(VrInput.Parse(_config.MenuButton.Value));

            bool flag3 = handPressed || keyPressed || buttonPressed;
            if (flag3 && !_previousToggle)
            {
                if (_open)
                {
                    CloseMenu();
                }
                else
                {
                    OpenMenu();
                }
            }
            _previousToggle = flag3;
        }

        private void HandleKeyboardShortcuts()
        {
            Keyboard current = Keyboard.current;
            if (current == null)
            {
                return;
            }
            if (!current.escapeKey.wasPressedThisFrame)
            {
                if (current.backspaceKey.wasPressedThisFrame && _window.SearchField != null && _window.SearchField.Focused)
                {
                    _window.HandleBackspace();
                }
                return;
            }
            if (_window.SearchField != null && _window.SearchField.Focused)
            {
                _window.BlurSearch();
                return;
            }
            CloseMenu();
        }

        private void OpenMenu()
        {
            if (_open)
            {
                return;
            }
            _pages = DemoApp.Build(this, _config, _demo);
            int page = ResolveSavedPage();
            _open = true;
            BuildWindow(page);
            if (_window == null)
            {
                _open = false;
                return;
            }
            _window.ScrollTo(_lastScroll);
            Plugin.Log.LogInfo("Cascade menu opened.");
            base.StopAllCoroutines();
            _window.gameObject.SetActive(true);
            _windowGroup.alpha = 0f;
            _window.transform.localScale = Vector3.one * 0.97f;
            _hovered = null;
            _pressed = null;
            _draggingSlider = null;
            _previousDown = IsSelectHeld();
            _pointerSmoothed = false;
            _hitSmoothed = false;
            _snap = true;
            UpdateCanvasTransform(forceSnap: true);
            UnlockCursor();
            _playerListSignature = PlayerActions.PlayerSignature();
            _playerListDirty = false;
            _lastPlayerListCheck = Time.unscaledTime;
            if (_pendingToastTitle != null)
            {
                _window.ShowToast(_pendingToastTitle, _pendingToastMessage);
                _pendingToastTitle = null;
                _pendingToastMessage = null;
            }
            base.StartCoroutine(OpenAnimation());
        }

        /// <summary>
        /// Finds (or rebuilds) the tab that was open when the menu closed.
        ///
        /// Static pages are matched by tag first, then by title. Dynamic pages
        /// (cosmetic categories and per-player pages) do not exist in a freshly
        /// built page list, so they are rebuilt from their tag before falling
        /// back to the Players tab.
        /// </summary>
        private int ResolveSavedPage()
        {
            string tag = _lastPageTag ?? string.Empty;
            string title = _lastPageTitle ?? string.Empty;

            if (!string.IsNullOrEmpty(tag))
            {
                int index = _pages.FindIndex((CascadePage p) => (p.Tag as string) == tag);
                if (index >= 0)
                {
                    return index;
                }

                // Module settings page (Flight settings, HUD settings, ...).
                if (ModuleSettings.IsSettingsTag(tag))
                {
                    string key = tag.Substring(ModuleSettings.TagPrefix.Length);
                    _pages.Add(ModuleSettings.Build(key, this, _config));
                    return _pages.Count - 1;
                }

                // Cosmetic category tab (Hats, Badges, All cosmetics, ...).
                if (tag.StartsWith(CosmeticBrowser.CategoryTagPrefix, StringComparison.Ordinal))
                {
                    string key = tag.Substring(CosmeticBrowser.CategoryTagPrefix.Length);
                    _pages.Add(CosmeticBrowser.BuildCategoryPage(this, key));
                    return _pages.Count - 1;
                }

                // Per-player page: rebuild it if that player is still here.
                if (tag.StartsWith(PlayerActions.PlayerTagPrefix, StringComparison.Ordinal))
                {
                    string userId = tag.Substring(PlayerActions.PlayerTagPrefix.Length);
                    NetPlayer player = PlayerActions.FindPlayer(userId);
                    if (player != null)
                    {
                        _pages.Add(PlayerActions.BuildPlayerPage(this, player));
                        return _pages.Count - 1;
                    }
                }

                // Report page: same idea, kept only while the player is present.
                if (tag.StartsWith(PlayerActions.ReportTagPrefix, StringComparison.Ordinal))
                {
                    string userId = tag.Substring(PlayerActions.ReportTagPrefix.Length);
                    NetPlayer player = PlayerActions.FindPlayer(userId);
                    if (player != null)
                    {
                        _pages.Add(PlayerActions.BuildReportPage(this, player));
                        return _pages.Count - 1;
                    }
                }
            }

            if (!string.IsNullOrEmpty(title))
            {
                int byTitle = _pages.FindIndex((CascadePage p) => p.Title == title);
                if (byTitle >= 0)
                {
                    return byTitle;
                }
            }

            int players = _pages.FindIndex((CascadePage p) => (p.Tag as string) == PlayerActions.PlayersTag);
            if (players >= 0)
            {
                return players;
            }
            return Mathf.Clamp(_lastPage, 0, Mathf.Max(0, _pages.Count - 1));
        }

        private void CloseMenu()
        {
            if (!_open)
            {
                return;
            }

            // Never leave a feed running behind a closed menu.
            if (_spectateOverlay != null)
            {
                StopSpectating();
            }

            _open = false;
            Plugin.Log.LogInfo("Cascade menu closed.");
            if (_window != null)
            {
                _lastPage = _window.PageIndex;
            }
            if (_window != null)
            {
                _lastScroll = _window.ScrollNormalized;
            }
            SaveCurrentTab();
            _shopQuery = string.Empty;
            _shopQueryDirty = false;
            _rebindTarget = null;
            CosmeticShop.Query = string.Empty;
            if (_window != null)
            {
                _window.BlurSearch();
                _window.CloseDropdown();
                _window.CloseDisconnectConfirmation();
                _window.CloseConfirm();
            }
            if (Mouse.current != null)
            {
                Cursor.visible = true;
            }
            RestoreCursor();
            if (_pointerLine != null)
            {
                _pointerLine.enabled = false;
            }
            if (_pointerRing != null)
            {
                _pointerRing.enabled = false;
            }
            if (_pointerRingShadow != null)
            {
                _pointerRingShadow.enabled = false;
            }
            if (_pointerDot != null)
            {
                _pointerDot.SetActive(false);
            }
            if (_desktopCursorObject != null)
            {
                _desktopCursorObject.SetActive(false);
            }
            if (_hovered != null && _hovered.IsAlive)
            {
                Action<bool> hover = _hovered.Hover;
                if (hover != null)
                {
                    hover(false);
                }
            }
            if (_pressed != null && _pressed.IsAlive)
            {
                Action release = _pressed.Release;
                if (release != null)
                {
                    release();
                }
            }
            _hovered = null;
            _pressed = null;
            _draggingSlider = null;
            _pointerSmoothed = false;
            _hitSmoothed = false;
            _previousDown = false;
            base.StopAllCoroutines();
            if (_window != null)
            {
                base.StartCoroutine(CloseAnimation());
            }
        }

        private IEnumerator OpenAnimation()
        {
            float time = 0f;
            const float duration = 0.16f;
            while (time < duration && _open && _window != null)
            {
                time += Time.unscaledDeltaTime;
                float num = Mathf.Clamp01(time / duration);
                float num2 = num * num * (3f - 2f * num);
                if (_windowGroup != null)
                {
                    _windowGroup.alpha = num2;
                }
                if (_window != null)
                {
                    _window.transform.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, num2);
                }
                yield return null;
            }
            if (_open && _windowGroup != null)
            {
                _windowGroup.alpha = 1f;
            }
            if (_open && _window != null)
            {
                _window.transform.localScale = Vector3.one;
            }
        }

        private IEnumerator CloseAnimation()
        {
            float time = 0f;
            const float duration = 0.12f;
            while (time < duration && !_open && _window != null)
            {
                time += Time.unscaledDeltaTime;
                float num = Mathf.Clamp01(time / duration);
                if (_windowGroup != null)
                {
                    _windowGroup.alpha = 1f - num;
                }
                if (_window != null)
                {
                    _window.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, num);
                }
                yield return null;
            }
            if (!_open && _window != null)
            {
                _window.gameObject.SetActive(false);
                _window.transform.localScale = Vector3.one;
                if (_windowGroup != null)
                {
                    _windowGroup.alpha = 1f;
                }
            }
        }

        private void BuildWindow(int page)
        {
            if (_canvasRect == null)
            {
                return;
            }
            if (_window != null)
            {
                _window.gameObject.SetActive(false);
                Object.Destroy(_window.gameObject);
                _window = null;
                _windowGroup = null;
            }
            _hovered = null;
            _pressed = null;
            _draggingSlider = null;
            _theme = CascadeTheme.Build(_config.ThemeMode, _config.AccentIndex.Value);
            GameObject gameObject = new GameObject("CascadeWindow", new Type[] { typeof(RectTransform) });
            RectTransform rectTransform = (RectTransform)gameObject.transform;
            rectTransform.SetParent(_canvasRect, false);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            _windowGroup = gameObject.AddComponent<CanvasGroup>();
            _windowGroup.alpha = 1f;
            _window = gameObject.AddComponent<CascadeWindow>();
            _window.CloseRequested = CloseMenu;
            _window.RecenterRequested = RecenterMenu;
            _window.Initialize(_canvas, _canvasRect, this, _config, _demo, _theme, _pages, page);
            if (!_open)
            {
                gameObject.SetActive(false);
            }
            UpdatePointerColor();
        }

        private void RebuildWindow()
        {
            int page = (_window != null) ? _window.PageIndex : _lastPage;
            float normalized = (_window != null) ? _window.ScrollNormalized : 1f;
            string text = (_window != null) ? _window.SearchQuery : string.Empty;
            bool flag = _window != null && _window.SearchFocused;
            BuildWindow(page);
            if (_open && _window != null)
            {
                _window.gameObject.SetActive(true);
                if (_windowGroup != null)
                {
                    _windowGroup.alpha = 1f;
                }
                _window.transform.localScale = Vector3.one;
                UpdateCanvasTransform(forceSnap: false);
                if (!string.IsNullOrEmpty(text) || flag)
                {
                    _window.SetSearch(text, flag);
                }
                if (_scrollTopQueued)
                {
                    _scrollTopQueued = false;
                    _window.ScrollTo(1f);
                }
                else
                {
                    _window.ScrollTo(normalized);
                }
            }
            ApplySpectateVisibility();
            if (_pendingToastTitle != null && _open && _window != null)
            {
                _window.ShowToast(_pendingToastTitle, _pendingToastMessage);
                _pendingToastTitle = null;
                _pendingToastMessage = null;
            }
        }

        public void QueueRebuild()
        {
            _rebuildQueued = true;
        }

        /// <summary>Rebuild and jump back to the top of the page (used by pagers).</summary>
        public void QueueRebuildTop()
        {
            _scrollTopQueued = true;
            _rebuildQueued = true;
        }

        /// <summary>
        /// Starts listening for the next keyboard key or controller button so a
        /// settings row can change a bind. <paramref name="target"/> is "menu" or
        /// "acrobatics".
        /// </summary>
        internal void BeginRebind(string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return;
            }

            // Stop the search field from eating the key we are about to capture.
            if (_window != null)
            {
                _window.BlurSearch();
            }

            // The trigger/grip that pressed this button is still held; do not
            // treat it as the newly chosen bind.
            VrInput.PrimeHeld();

            _rebindTarget = target;
            _rebindDeadline = Time.unscaledTime + 6f;
            ShowToast("Rebind", "Press a key or controller button for " + RebindLabel(target) + ".");
        }

        private static string RebindLabel(string target)
        {
            return target == "acrobatics" ? "acrobatics" : "the menu";
        }

        private void UpdateRebind()
        {
            if (_rebindTarget == null)
            {
                return;
            }

            if (Time.unscaledTime > _rebindDeadline)
            {
                _rebindTarget = null;
                ShowToast("Rebind cancelled", "No key or button pressed, the old bind is unchanged.");
                return;
            }

            Key pressed = Keybinds.CapturePressed();
            if (pressed == Key.Escape)
            {
                _rebindTarget = null;
                ShowToast("Rebind cancelled", "The old bind is unchanged.");
                return;
            }

            if (pressed != Key.None)
            {
                if (_rebindTarget == "acrobatics")
                {
                    _config.AcrobaticsKey.Value = pressed.ToString();
                }
                else
                {
                    _config.MenuKey.Value = pressed.ToString();
                }

                string keyLabel = RebindLabel(_rebindTarget);
                _rebindTarget = null;
                ShowToast("Rebind saved", keyLabel + " is now " + Keybinds.Name(pressed) + ".");

                // Rebuild the page list so the settings row shows the new key
                // straight away (not just after the next menu close/open).
                RefreshPlayerList(true);
                return;
            }

            VrButton vrButton;
            if (!VrInput.CapturePressed(out vrButton))
            {
                return;
            }

            if (_rebindTarget == "acrobatics")
            {
                _config.AcrobaticsButton.Value = VrInput.Format(vrButton);
            }
            else
            {
                _config.MenuButton.Value = VrInput.Format(vrButton);
            }

            string buttonLabel = RebindLabel(_rebindTarget);
            _rebindTarget = null;
            ShowToast("Rebind saved", buttonLabel + " is now " + VrInput.Name(vrButton) + ".");
            RefreshPlayerList(true);
        }

        internal void SetShopQuery(string query)
        {
            query = query ?? string.Empty;
            if (query == _shopQuery)
            {
                return;
            }
            _shopQuery = query;
            CosmeticShop.Query = query;
            _shopQueryDirty = true;
            _shopQueryTime = Time.unscaledTime;
        }

        internal void NotifyPageChanged(CascadePage page)
        {
            SaveTab(page);

            if (page != null && page.Tag as string == "cascade.shop")
            {
                return;
            }
            if (CosmeticShop.Query.Length == 0 && _shopQuery.Length == 0)
            {
                return;
            }
            CosmeticShop.Query = string.Empty;
            _shopQuery = string.Empty;
            _shopQueryDirty = false;
            _rebuildQueued = true;
        }

        /// <summary>Stores the tab that is currently visible.</summary>
        private void SaveCurrentTab()
        {
            CascadePage page = (_window != null) ? _window.CurrentPage : null;
            SaveTab(page);
        }

        /// <summary>Mirrors a tab title/tag into the config file.</summary>
        private void SaveTab(CascadePage page)
        {
            _lastPageTitle = (page != null) ? (page.Title ?? string.Empty) : string.Empty;
            _lastPageTag = (page != null) ? ((page.Tag as string) ?? string.Empty) : string.Empty;

            if (_config == null)
            {
                return;
            }
            try
            {
                _config.LastPageTag.Value = _lastPageTag;
                _config.LastPageTitle.Value = _lastPageTitle;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade could not save the last tab: " + exception.Message);
            }
        }

        public void RefreshPlayerList()
        {
            RefreshPlayerList(silent: false);
        }

        public void RefreshPlayerList(bool silent)
        {
            float normalized = (_window != null) ? _window.ScrollNormalized : 1f;
            string text = (_window != null) ? _window.SearchQuery : string.Empty;
            bool flag = _window != null && _window.SearchFocused;
            _pages = DemoApp.Build(this, _config, _demo);
            // Tag first: keeps cosmetic categories, player pages and module
            // settings pages alive instead of jumping back to Players.
            int num = ResolveSavedPage();
            if (num < 0)
            {
                num = 0;
            }
            if (!_open || _window == null)
            {
                _lastPage = num;
                return;
            }
            BuildWindow(num);
            if (_window != null)
            {
                _window.gameObject.SetActive(true);
                if (_windowGroup != null)
                {
                    _windowGroup.alpha = 1f;
                }
                _window.transform.localScale = Vector3.one;
                UpdateCanvasTransform(forceSnap: false);
                if (!string.IsNullOrEmpty(text) || flag)
                {
                    _window.SetSearch(text, flag);
                }
                _window.ScrollTo(normalized);
            }
            ApplySpectateVisibility();
            _playerListSignature = PlayerActions.PlayerSignature();
            _playerListDirty = false;
            if (!silent)
            {
                int count = PlayerActions.GetPlayers().Count;
                ShowToast("Player list", (count == 0) ? "No other players in this room." : ("Found " + count + ((count == 1) ? " other player." : " other players.")));
            }
        }

        private void GoToPlayersPage()
        {
            if (_window != null)
            {
                int num = _window.Pages.FindIndex((CascadePage p) => p.Tag as string == "cascade.players");
                if (num >= 0)
                {
                    _window.NavigateTo(num, recordHistory: false);
                }
            }
        }

        /// <summary>
        /// Opens the live spectate window for one player.
        ///
        /// Only the feed camera moves: the local player, hands and game camera
        /// stay exactly where they are, so this is safe in VR and nothing about
        /// the local position is sent to the server.
        /// </summary>
        internal void StartSpectating(NetPlayer player)
        {
            if (!_open || _window == null || _canvasRect == null || _theme == null || player == null)
            {
                return;
            }

            VRRig rig = PlayerActions.FindRigObject(player);
            if (rig == null)
            {
                ShowToast("Spectate", "That player's rig is not ready yet.");
                return;
            }

            if (_spectateOverlay != null) StopSpectating();

            string name = PlayerActions.SafeName(player);
            SpectateController controller = SpectateController.Begin(rig, _canvas);
            if (controller == null)
            {
                ShowToast("Spectate", "Could not start the spectate camera.");
                return;
            }

            if (_window != null)
            {
                _window.CloseDropdown();
                _window.CloseConfirm();
                _window.BlurSearch();

                // Hide before registering: OnDisable closes the dropdown, which
                // also clears every Overlay interactable. Registering first
                // would lose the spectate buttons immediately.
                _window.gameObject.SetActive(false);
            }

            _spectateOverlay = CascadeSpectateOverlay.Create(
                _canvasRect, _theme, controller, name, StopSpectating);
            _spectateOverlay.RegisterWith(_window);
            _spectateOverlay.transform.SetAsLastSibling();

            _hovered = null;
            _pressed = null;
            _draggingSlider = null;
            Plugin.Log.LogInfo("Spectating " + name + ".");
        }

        /// <summary>Closes the spectate feed and restores the menu window.</summary>
        internal void StopSpectating()
        {
            if (_spectateOverlay != null)
            {
                _spectateOverlay.Dispose();
                Object.Destroy(_spectateOverlay.gameObject);
                _spectateOverlay = null;
            }

            SpectateController.StopActive();

            if (_window != null && _open)
            {
                _window.gameObject.SetActive(true);
                _window.transform.localScale = Vector3.one;
            }

            _hovered = null;
            _pressed = null;
            _draggingSlider = null;
        }

        /// <summary>
        /// A rebuild replaces the window (and its interactable list), so the
        /// spectate buttons must attach to the new window and the new window
        /// must be hidden again.
        /// </summary>
        private void ApplySpectateVisibility()
        {
            if (_spectateOverlay == null || _window == null) return;

            _window.gameObject.SetActive(false);
            _spectateOverlay.RegisterWith(_window);
            _spectateOverlay.transform.SetAsLastSibling();
        }

        public void OpenPlayerPage(NetPlayer player)
        {
            if (!_open || _window == null || player == null)
            {
                return;
            }
            PlayerActions.Select(player.UserId);
            string tag = PlayerActions.PlayerTag(player.UserId);
            CascadePage cascadePage = PlayerActions.BuildPlayerPage(this, player);
            int num = _window.Pages.FindIndex((CascadePage p) => p.Tag as string == tag);
            if (num >= 0)
            {
                _window.Pages[num] = cascadePage;
            }
            else
            {
                _window.Pages.Add(cascadePage);
                num = _window.Pages.Count - 1;
            }
            if (num == _window.PageIndex)
            {
                _window.SetPage(num, animate: false);
                return;
            }
            _window.NavigateTo(num, recordHistory: true);
        }

        public void OpenReportPage(NetPlayer player)
        {
            if (!_open || _window == null || player == null)
            {
                return;
            }
            string tag = PlayerActions.ReportTag(player.UserId);
            CascadePage cascadePage = PlayerActions.BuildReportPage(this, player);
            int num = _window.Pages.FindIndex((CascadePage p) => p.Tag as string == tag);
            if (num >= 0)
            {
                _window.Pages[num] = cascadePage;
            }
            else
            {
                _window.Pages.Add(cascadePage);
                num = _window.Pages.Count - 1;
            }
            if (num == _window.PageIndex)
            {
                _window.SetPage(num, animate: false);
                return;
            }
            _window.NavigateTo(num, recordHistory: true);
        }

        public void RefreshPlayerPage(NetPlayer player)
        {
            if (!_open || _window == null || player == null)
            {
                return;
            }
            string tag = PlayerActions.PlayerTag(player.UserId);
            int num = _window.Pages.FindIndex((CascadePage p) => p.Tag as string == tag);
            if (num >= 0)
            {
                _window.Pages[num] = PlayerActions.BuildPlayerPage(this, player);
                if (num == _window.PageIndex)
                {
                    _window.SetPage(num, animate: false);
                }
            }
        }

        /// <summary>
        /// Opens a page by tag, falling back to its title. Used by module settings
        /// pages to return to their module tab.
        /// </summary>
        public void OpenPage(string tag, string fallbackTitle)
        {
            if (!_open || _window == null)
            {
                return;
            }

            int index = -1;
            if (!string.IsNullOrEmpty(tag))
            {
                index = _window.Pages.FindIndex((CascadePage p) => (p.Tag as string) == tag);
            }
            if (index < 0 && !string.IsNullOrEmpty(fallbackTitle))
            {
                index = _window.Pages.FindIndex((CascadePage p) => p.Title == fallbackTitle);
            }
            if (index >= 0)
            {
                _window.NavigateTo(index, recordHistory: true);
            }
        }

        /// <summary>
        /// Opens (or refreshes) the settings page for a module and selects it.
        /// The page is added on demand so the sidebar is not cluttered with one
        /// tab per slider group.
        /// </summary>
        public void OpenModuleSettings(string key)
        {
            if (!_open || _window == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            string tag = ModuleSettings.Tag(key);
            CascadePage page = ModuleSettings.Build(key, this, _config);
            int index = _window.Pages.FindIndex((CascadePage p) => (p.Tag as string) == tag);
            if (index >= 0)
            {
                _window.Pages[index] = page;
            }
            else
            {
                _window.Pages.Add(page);
                index = _window.Pages.Count - 1;
            }

            if (index == _window.PageIndex)
            {
                _window.SetPage(index, animate: false);
                return;
            }
            _window.NavigateTo(index, recordHistory: true);
        }

        public void OpenCosmeticsPage()
        {
            if (!_open || _window == null)
            {
                return;
            }
            int num = _window.Pages.FindIndex((CascadePage p) => p.Tag as string == "cascade.cosmetics");
            if (num < 0)
            {
                num = _window.Pages.FindIndex((CascadePage p) => p.Title == "Cosmetics");
            }
            if (num >= 0)
            {
                _window.NavigateTo(num, recordHistory: true);
            }
        }

        public void OpenCosmeticsShop()
        {
            if (!_open || _window == null)
            {
                return;
            }
            int num = _window.Pages.FindIndex((CascadePage p) => p.Tag as string == "cascade.shop");
            if (num >= 0)
            {
                _window.NavigateTo(num, recordHistory: true);
            }
        }

        public void OpenCosmeticsCategory(string categoryKey)
        {            if (!_open || _window == null)
            {
                return;
            }
            string tag = CosmeticBrowser.CategoryTag(categoryKey);
            CascadePage cascadePage = CosmeticBrowser.BuildCategoryPage(this, categoryKey);
            int num = _window.Pages.FindIndex((CascadePage p) => p.Tag as string == tag);
            if (num >= 0)
            {
                _window.Pages[num] = cascadePage;
            }
            else
            {
                _window.Pages.Add(cascadePage);
                num = _window.Pages.Count - 1;
            }
            if (num == _window.PageIndex)
            {
                _window.SetPage(num, animate: false);
                return;
            }
            _window.NavigateTo(num, recordHistory: true);
        }

        public void RecenterMenu()
        {
            _snap = true;
            if (_open)
            {
                UpdateCanvasTransform(forceSnap: true);
            }
            if (_window != null)
            {
                _window.ScrollToTop();
            }
        }

        public void ToggleSidebar()
        {
            if (_window != null)
            {
                _window.ToggleSidebar();
            }
        }

        public void ShowToast(string title, string message)
        {
            if (_open && _window != null)
            {
                _window.ShowToast(title, message);
                return;
            }
            StandaloneNotificationOverlay.Notify(title, message);
        }

        public void ShowConfirm(string title, string message, string confirmText, Action onConfirm)
        {
            if (!_open || _window == null)
            {
                return;
            }
            _window.ShowConfirm(title, message, confirmText, onConfirm);
        }

        public void RestoreDefaults()
        {
            _config.ResetDefaults();
            _pendingToastTitle = "Settings restored";
            _pendingToastMessage = "Cascade defaults have been reapplied.";
            QueueRebuild();
            RecenterMenu();
        }

        private void UpdateCanvasTransform(bool forceSnap)
        {
            if (_screenMode)
            {
                _snap = false;
                return;
            }
            Camera camera = ResolveViewCamera();
            if (camera == null)
            {
                return;
            }
            _canvas.worldCamera = camera;
            float num = Mathf.Max(0.3f, _config.Distance.Value);
            float num2 = XRSettings.isDeviceActive ? 0.00145f : 0.0013f;
            float num3 = Mathf.Clamp(num / 0.95f, 0.35f, 1.7f);
            _canvasRect.localScale = Vector3.one * (_config.Scale.Value * num2 * num3);
            if (forceSnap || _snap)
            {
                SnapCanvasPose(camera);
                _snap = false;
                return;
            }
            if (_config.FollowHead.Value)
            {
                Vector3 position;
                Quaternion rotation;
                MenuAnchor.GetPose(_config, camera, out position, out rotation);
                float t = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
                _canvasRect.position = Vector3.Lerp(_canvasRect.position, position, t);
                _canvasRect.rotation = Quaternion.Slerp(_canvasRect.rotation, rotation, t);
                return;
            }
            if (MenuAnchor.NeedsReanchor(_config, _canvasRect.position, camera.transform.position))
            {
                SnapCanvasPose(camera);
            }
        }

        private void SnapCanvasPose(Camera camera)
        {
            Vector3 position;
            Quaternion rotation;
            MenuAnchor.GetPose(_config, camera, out position, out rotation);
            _canvasRect.position = position;
            _canvasRect.rotation = rotation;
        }

        private void UpdateInteraction()
        {
            Vector3 vector;
            bool flag = UpdatePointer(out vector);
            _hitPoint = vector;
            CascadeInteractable cascadeInteractable = flag ? _window.HitTest(vector) : null;
            if (cascadeInteractable != _hovered)
            {
                if (_hovered != null && _hovered.IsAlive)
                {
                    Action<bool> hover = _hovered.Hover;
                    if (hover != null)
                    {
                        hover(false);
                    }
                }
                _hovered = cascadeInteractable;
                if (_hovered != null)
                {
                    Action<bool> hover2 = _hovered.Hover;
                    if (hover2 != null)
                    {
                        hover2(true);
                    }
                }
            }
            bool flag2 = IsSelectHeld();
            if (flag2 && !_previousDown)
            {
                CascadeInteractable cascadeInteractable2 = cascadeInteractable;
                if (cascadeInteractable2 == null || !_window.IsSearchInteraction(cascadeInteractable2))
                {
                    _window.BlurSearch();
                }
                if (cascadeInteractable2 != null && cascadeInteractable2.Enabled && Time.unscaledTime - cascadeInteractable2.LastActivated >= 0.15f)
                {
                    cascadeInteractable2.LastActivated = Time.unscaledTime;
                    _pressed = cascadeInteractable2;
                    Action press = cascadeInteractable2.Press;
                    if (press != null)
                    {
                        press();
                    }
                    if (cascadeInteractable2.IsSlider)
                    {
                        Action<Vector3> dragWorld = cascadeInteractable2.DragWorld;
                        if (dragWorld != null)
                        {
                            dragWorld(vector);
                        }
                        _draggingSlider = cascadeInteractable2;
                    }
                    else
                    {
                        Action click = cascadeInteractable2.Click;
                        if (click != null)
                        {
                            click();
                        }
                    }
                }
            }
            if (flag2 && _draggingSlider != null && flag && _open)
            {
                Action<Vector3> dragWorld2 = _draggingSlider.DragWorld;
                if (dragWorld2 != null)
                {
                    dragWorld2(vector);
                }
            }
            if (!flag2 && _previousDown)
            {
                if (_pressed != null)
                {
                    Action release = _pressed.Release;
                    if (release != null)
                    {
                        release();
                    }
                }
                _pressed = null;
                _draggingSlider = null;
            }
            _previousDown = flag2;
        }

        private bool UpdatePointer(out Vector3 hit)
        {
            hit = Vector3.zero;
            _desktopPointer = false;
            Vector3 vector;
            Vector3 vector2;
            bool flag;
            if (!GetPointerRay(out vector, out vector2, out flag))
            {
                if (_pointerLine != null)
                {
                    _pointerLine.enabled = false;
                }
                if (_pointerRing != null)
                {
                    _pointerRing.enabled = false;
                }
                if (_pointerRingShadow != null)
                {
                    _pointerRingShadow.enabled = false;
                }
                if (_pointerDot != null)
                {
                    _pointerDot.SetActive(false);
                }
                UpdateDesktopCursor();
                return false;
            }
            _desktopPointer = flag;
            if (!flag)
            {
                SmoothPointerRay(ref vector, ref vector2);
            }
            else
            {
                _pointerSmoothed = false;
                _hitSmoothed = false;
            }
            bool flag2 = false;
            Plane plane = new Plane(-_canvasRect.forward, _canvasRect.position);
            Ray ray = new Ray(vector, vector2);
            float num;
            if (plane.Raycast(ray, out num) && num > 0f && num < 6f)
            {
                Vector3 point = ray.GetPoint(num);
                Vector3 local = _canvasRect.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) <= 600f && Mathf.Abs(local.y) <= 350f)
                {
                    hit = SmoothMenuHit(plane, point);
                    flag2 = true;
                }
            }
            if (!flag2)
            {
                _hitSmoothed = false;
            }
            Vector3 front = -_canvasRect.forward;
            Vector3 cursorPoint = flag2 ? (hit + front * 0.012f) : Vector3.zero;
            if (_pointerLine != null)
            {
                bool showLine = !_desktopPointer;
                _pointerLine.enabled = showLine;
                if (showLine)
                {
                    Vector3 end = flag2 ? cursorPoint : vector + vector2 * 2f;
                    _pointerLine.SetPosition(0, vector);
                    _pointerLine.SetPosition(1, end);
                }
            }
            bool worldCursor = flag2 && !_screenMode;
            if (_pointerRingShadow != null)
            {
                _pointerRingShadow.enabled = worldCursor;
            }
            if (_pointerRing != null)
            {
                _pointerRing.enabled = worldCursor;
            }
            if (_pointerDot != null)
            {
                _pointerDot.SetActive(worldCursor);
                if (worldCursor)
                {
                    _pointerDot.transform.position = cursorPoint + front * 0.001f;
                }
            }
            if (flag2)
            {
                float distanceScale = Mathf.Clamp(_config.Distance.Value / 0.95f, 0.7f, 1.55f);
                float radius = 0.015f * distanceScale;
                Vector3 right = _canvasRect.right;
                Vector3 up = _canvasRect.up;
                UpdatePointerRing(_pointerRingShadow, cursorPoint, right, up, radius + 0.004f);
                UpdatePointerRing(_pointerRing, cursorPoint, right, up, radius);
            }
            UpdateDesktopCursor();
            return flag2;
        }

        private void SmoothPointerRay(ref Vector3 origin, ref Vector3 direction)
        {
            if (!_pointerSmoothed)
            {
                _smoothPointerOrigin = origin;
                _smoothPointerDirection = direction.normalized;
                _pointerSmoothed = true;
                return;
            }
            float rate = _draggingSlider != null ? 28f : 16f;
            float t = 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime);
            _smoothPointerOrigin = Vector3.Lerp(_smoothPointerOrigin, origin, t);
            _smoothPointerDirection = Vector3.Slerp(_smoothPointerDirection, direction.normalized, t).normalized;
            origin = _smoothPointerOrigin;
            direction = _smoothPointerDirection;
        }

        private Vector3 SmoothMenuHit(Plane plane, Vector3 point)
        {
            if (_desktopPointer)
            {
                _smoothHit = point;
                _hitSmoothed = true;
                return point;
            }
            if (!_hitSmoothed)
            {
                _smoothHit = point;
                _hitSmoothed = true;
                return point;
            }
            float num = _draggingSlider != null ? 32f : 22f;
            float t = 1f - Mathf.Exp(-num * Time.unscaledDeltaTime);
            Vector3 vector = _canvasRect.InverseTransformPoint(point);
            Vector3 vector2 = _canvasRect.InverseTransformPoint(_smoothHit);
            vector2.x = Mathf.Lerp(vector2.x, vector.x, t);
            vector2.y = Mathf.Lerp(vector2.y, vector.y, t);
            vector2.z = 0f;
            Vector3 point2 = _canvasRect.TransformPoint(vector2);
            _smoothHit = plane.ClosestPointOnPlane(point2);
            return _smoothHit;
        }

        private static void UpdatePointerRing(LineRenderer ring, Vector3 center, Vector3 right, Vector3 up, float radius)
        {
            if (ring == null)
            {
                return;
            }
            int count = ring.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / (float)count * 3.1415927f * 2f;
                Vector3 point = center + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius);
                ring.SetPosition(i, point);
            }
        }

        private void UpdateDesktopCursor()
        {
            bool flag = _open && _desktopPointer && Mouse.current != null;
            if (_desktopCursorObject != null && _desktopCursorObject.activeSelf != flag)
            {
                _desktopCursorObject.SetActive(flag);
            }
            if (!flag || _desktopCursorRect == null)
            {
                return;
            }
            Vector2 position = Mouse.current.position.ReadValue();
            _desktopCursorRect.anchoredPosition = position;
            float num = Mathf.Max(1f, (float)Screen.height / 1080f);
            _desktopCursorRect.localScale = new Vector3(num, num, 1f);
        }

        private bool GetPointerRay(out Vector3 origin, out Vector3 direction, out bool desktop)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            desktop = false;
            if (XRSettings.isDeviceActive && GorillaTagger.Instance != null && GorillaTagger.Instance.rightHandTransform != null)
            {
                Transform hand = GorillaTagger.Instance.rightHandTransform;
                origin = hand.position + hand.forward * 0.04f;
                direction = hand.forward;
                return true;
            }
            Camera camera = ResolveViewCamera();
            if (camera == null)
            {
                return false;
            }
            if (_screenMode && Mouse.current != null && _canvasRect != null)
            {
                Vector2 screenPos = Mouse.current.position.ReadValue();
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out local))
                {
                    Vector3 world = _canvasRect.TransformPoint(new Vector3(local.x, local.y, 0f));
                    origin = world - _canvasRect.forward * 0.5f;
                    direction = _canvasRect.forward;
                    desktop = true;
                    return true;
                }
            }
            if (Mouse.current != null)
            {
                Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                origin = ray.origin;
                direction = ray.direction;
                desktop = true;
                return true;
            }
            origin = camera.transform.position;
            direction = camera.transform.forward;
            desktop = true;
            return true;
        }

        private bool IsSelectHeld()
        {
            ControllerInputPoller instance = ControllerInputPoller.instance;
            return (Mouse.current != null && Mouse.current.leftButton.isPressed) || (instance != null && (instance.rightControllerIndexFloat > 0.5f || instance.rightControllerPrimaryButton));
        }

        private void HandleScroll()
        {
            if (Mouse.current != null)
            {
                float y = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(y) > 0.01f)
                {
                    float num = (Mathf.Abs(y) > 10f) ? (y * 0.001f) : (Mathf.Sign(y) * 0.06f);
                    num = Mathf.Clamp(num, -0.1f, 0.1f);
                    if (_window.SidebarScrollable && _window.SidebarContains(Mouse.current.position.ReadValue()))
                    {
                        _window.ScrollSidebar(num * 1.6f);
                    }
                    else
                    {
                        _window.Scroll(num);
                    }
                }
            }
            // VR / controller stick. VrInput reads the Input System XR device
            // first, so this works with SteamVR/OpenXR where the game's poller
            // may not report the stick.
            float stickY = VrInput.RightStick().y;
            if (Mathf.Abs(stickY) > 0.25f)
            {
                // In VR the stick scrolls whichever pane the beam is over:
                // the tab list on the left edge, otherwise the page body.
                bool overSidebar = false;
                if (_window.SidebarScrollable && _canvasRect != null)
                {
                    Vector3 local = _canvasRect.InverseTransformPoint(_hitPoint);
                    overSidebar = Mathf.Abs(local.z) < 40f && local.x < -215f;
                }
                if (overSidebar)
                {
                    _window.ScrollSidebar(stickY * 1.6f * Time.unscaledDeltaTime * 0.85f);
                }
                else
                {
                    _window.Scroll(stickY * Time.unscaledDeltaTime * 0.85f);
                }
            }
        }

        private void UpdatePointerColor()
        {
            Color color = (_theme != null) ? _theme.Accent : new Color(0.3f, 0.65f, 1f);
            if (_pointerLine != null)
            {
                _pointerLine.startColor = new Color(color.r, color.g, color.b, 0.9f);
                _pointerLine.endColor = new Color(color.r, color.g, color.b, 0.2f);
            }
            if (_pointerRing != null)
            {
                _pointerRing.startColor = Color.white;
                _pointerRing.endColor = Color.white;
            }
            if (_pointerRingShadow != null)
            {
                _pointerRingShadow.startColor = new Color(0f, 0f, 0f, 0.62f);
                _pointerRingShadow.endColor = new Color(0f, 0f, 0f, 0.62f);
            }
            if (_pointerDotRenderer != null && _pointerDotRenderer.material != null)
            {
                _pointerDotRenderer.material.color = color;
            }
            if (_desktopCursorDot != null)
            {
                _desktopCursorDot.color = color;
            }
        }

        private void SubscribeTextInput()
        {
            Keyboard current = Keyboard.current;
            if (current == null)
            {
                if (_keyboard != null)
                {
                    _keyboard.onTextInput -= OnTextInput;
                    _keyboard = null;
                }
                _textSubscribed = false;
                return;
            }
            if (_keyboard == current && _textSubscribed)
            {
                return;
            }
            if (_keyboard != null)
            {
                _keyboard.onTextInput -= OnTextInput;
            }
            _keyboard = current;
            _keyboard.onTextInput += OnTextInput;
            _textSubscribed = true;
        }

        private void OnTextInput(char character)
        {
            // While rebinding, the next key belongs to the bind, not the search box.
            if (_rebindTarget != null)
            {
                return;
            }
            if (_open && _window != null)
            {
                _window.HandleTextInput(character);
            }
        }

        private void UnlockCursor()
        {
            if (_cursorSaved)
            {
                return;
            }
            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorSaved = true;
        }

        private void RestoreCursor()
        {
            if (!_cursorSaved)
            {
                return;
            }
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
            _cursorSaved = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            GameplayMods.Shutdown();
            base.StopAllCoroutines();
            if (_keyboard != null)
            {
                _keyboard.onTextInput -= OnTextInput;
                _keyboard = null;
            }
            RestoreCursor();
            if (_canvasObject != null)
            {
                Object.Destroy(_canvasObject);
            }
            if (_desktopCursorObject != null)
            {
                Object.Destroy(_desktopCursorObject);
            }
            if (_pointerMaterial != null)
            {
                Object.Destroy(_pointerMaterial);
            }
            if (_pointerDotRenderer != null && _pointerDotRenderer.material != null)
            {
                Object.Destroy(_pointerDotRenderer.material);
            }
        }

    }
}
