using System;
using System.Collections.Generic;
using GorillaLocomotion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Sandbox;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Alternate translucent vertical frosted-glass wrist & fingertip-dot menu ("Cascade Glass Handheld").
    /// Features a soft glowing aura backdrop, rounded white-rimmed glass card, pill buttons,
    /// bottom pagination capsule, wrist mounting, and a physical fingertip pointer dot.
    /// </summary>
    internal sealed class CascadeHandheldMenu : MonoBehaviour
    {
        private const int ButtonsPerPage = 8;
        private const float CardWidth = 310f;
        private const float CardHeight = 520f;
        private const float WorldScale = 0.00088f;

        private sealed class PillEntry
        {
            public string Label;
            public bool IsCategory;
            public Func<bool> IsOn;
            public Action OnClick;
        }

        private sealed class ButtonSlot
        {
            public RectTransform Rect;
            public Image Bg;
            public Image Rim;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Chevron;
            public PillEntry Entry;
        }

        private CascadeMenuBehaviour _menu;
        private MenuConfig _config;

        private bool _open;
        private string _category = "HOME";
        private int _pageIndex;

        private GameObject _rootObj;
        private Canvas _canvas;
        private RectTransform _cardRect;
        private Image _auraImg;
        private Image _cardBg;
        private TextMeshProUGUI _titleTxt;
        private TextMeshProUGUI _subTxt;
        private TextMeshProUGUI _badgeTxt;
        private TextMeshProUGUI _pageTxt;
        private RectTransform _prevZone;
        private RectTransform _nextZone;
        private RectTransform _backZone;
        private Image _backBg;
        private TextMeshProUGUI _backTxt;

        private readonly List<ButtonSlot> _slots = new List<ButtonSlot>(ButtonsPerPage);
        private readonly List<PillEntry> _currentEntries = new List<PillEntry>(32);

        private GameObject _dotObj;
        private Renderer _dotRenderer;
        private Sprite _auraSprite;

        private float _pokeCooldown;
        private bool _prevWristTouch;
        private bool _prevMouseDown;
        private float _nextStateRefresh;

        private Vector3 _smoothPos;
        private Quaternion _smoothRot = Quaternion.identity;
        private bool _hasPose;

        internal bool IsOpen => _open;

        internal void Initialize(CascadeMenuBehaviour menu, MenuConfig config)
        {
            _menu = menu;
            _config = config;
        }

        internal void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;

            if (_open)
            {
                EnsureBuilt();
                _rootObj.SetActive(true);
                EnsureDot();
                if (_dotObj != null) _dotObj.SetActive(XRSettings.isDeviceActive);
                _hasPose = false;
                RebuildEntries();
                ApplyThemeColors();
            }
            else
            {
                if (_rootObj != null) _rootObj.SetActive(false);
                if (_dotObj != null) _dotObj.SetActive(false);
            }
        }

        internal void Toggle()
        {
            SetOpen(!_open);
        }

        private void Update()
        {
            if (_config == null || _config.MenuStyle.Value != 1)
            {
                if (_open) SetOpen(false);
                return;
            }

            // Optional Wrist-Tap gesture toggle (touch opposite hand to menu wrist)
            if (_config.HandheldAttach.Value == 2 && XRSettings.isDeviceActive && GorillaTagger.Instance != null)
            {
                bool useRight = _config.MenuHand.Value == 1;
                Transform wrist = useRight ? GorillaTagger.Instance.rightHandTransform : GorillaTagger.Instance.leftHandTransform;
                Transform finger = useRight ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
                if (wrist != null && finger != null)
                {
                    bool touching = Vector3.Distance(wrist.position + wrist.forward * 0.08f, finger.position) < 0.095f;
                    if (touching && !_prevWristTouch)
                    {
                        Toggle();
                    }
                    _prevWristTouch = touching;
                }
            }

            if (!_open || _rootObj == null) return;

            UpdatePlacement();
            UpdateDotAndInput();

            if (Time.unscaledTime >= _nextStateRefresh)
            {
                _nextStateRefresh = Time.unscaledTime + 0.2f;
                RefreshButtonStates();
            }
        }

        private void UpdatePlacement()
        {
            bool useRight = _config != null && _config.MenuHand.Value == 1;
            int attach = _config != null ? _config.HandheldAttach.Value : 0;
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();

            Vector3 targetPos;
            Quaternion targetRot;

            if (XRSettings.isDeviceActive && attach != 1 && GorillaTagger.Instance != null)
            {
                Transform hand = useRight ? GorillaTagger.Instance.rightHandTransform : GorillaTagger.Instance.leftHandTransform;
                if (hand != null)
                {
                    if (attach == 2)
                    {
                        // Floating gently above the wrist, facing the player's eyes
                        targetPos = hand.position + Vector3.up * 0.24f;
                        if (cam != null)
                        {
                            Vector3 look = targetPos - cam.transform.position;
                            targetRot = look.sqrMagnitude > 0.0001f
                                ? Quaternion.LookRotation(look.normalized, Vector3.up)
                                : hand.rotation;
                        }
                        else targetRot = hand.rotation;
                    }
                    else
                    {
                        // Mounted neatly on the side/top of the wrist angled toward your gaze
                        Vector3 sideOffset = (useRight ? -hand.right : hand.right) * 0.07f + hand.up * 0.14f + hand.forward * 0.04f;
                        targetPos = hand.position + sideOffset;
                        if (cam != null)
                        {
                            Vector3 look = targetPos - cam.transform.position;
                            targetRot = look.sqrMagnitude > 0.0001f
                                ? Quaternion.LookRotation(look.normalized, Vector3.up)
                                : hand.rotation;
                        }
                        else targetRot = hand.rotation;
                    }
                }
                else
                {
                    GetFrontPose(cam, out targetPos, out targetRot);
                }
            }
            else
            {
                GetFrontPose(cam, out targetPos, out targetRot);
            }

            float scale = (_config != null ? _config.Scale.Value : 1f) * WorldScale;
            if (XRSettings.isDeviceActive && GTPlayer.Instance != null)
                scale *= Mathf.Clamp(GTPlayer.Instance.scale, 0.4f, 3f);

            if (!_hasPose)
            {
                _smoothPos = targetPos;
                _smoothRot = targetRot;
                _hasPose = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
                _smoothPos = Vector3.Lerp(_smoothPos, targetPos, t);
                _smoothRot = Quaternion.Slerp(_smoothRot, targetRot, t);
            }

            _rootObj.transform.position = _smoothPos;
            _rootObj.transform.rotation = _smoothRot;
            _rootObj.transform.localScale = Vector3.one * scale;
        }

        private static void GetFrontPose(Camera cam, out Vector3 pos, out Quaternion rot)
        {
            if (cam != null)
            {
                pos = cam.transform.position + cam.transform.forward * 0.58f - cam.transform.up * 0.02f;
                Vector3 look = pos - cam.transform.position;
                rot = Quaternion.LookRotation(look.normalized, Vector3.up);
            }
            else
            {
                pos = Vector3.forward * 0.6f;
                rot = Quaternion.identity;
            }
        }

        private void UpdateDotAndInput()
        {
            bool useRight = _config != null && _config.MenuHand.Value == 1;

            // VR fingertip dot poking
            if (XRSettings.isDeviceActive && GorillaTagger.Instance != null)
            {
                EnsureDot();
                Transform pointerHand = useRight ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
                if (pointerHand != null && _dotObj != null)
                {
                    _dotObj.SetActive(true);
                    Vector3 tipPos = pointerHand.position + pointerHand.forward * 0.055f;
                    _dotObj.transform.position = tipPos;
                    float rigScale = GTPlayer.Instance != null ? Mathf.Clamp(GTPlayer.Instance.scale, 0.4f, 3f) : 1f;
                    _dotObj.transform.localScale = Vector3.one * (0.014f * rigScale);

                    // Convert fingertip world position into the glass card's local 2D plane
                    Vector3 local = _cardRect.InverseTransformPoint(tipPos);
                    // Within ~3.2cm of the glass surface (converted to canvas units)
                    float zThreshold = 0.032f / Mathf.Max(0.0001f, _rootObj.transform.lossyScale.z);
                    if (Mathf.Abs(local.z) <= zThreshold && Time.unscaledTime >= _pokeCooldown)
                    {
                        Vector2 local2D = new Vector2(local.x, local.y);
                        if (TryHitLocalPoint(local2D))
                        {
                            _pokeCooldown = Time.unscaledTime + 0.26f;
                            try
                            {
                                GorillaTagger.Instance.StartVibration(!useRight, 0.25f, 0.04f);
                            }
                            catch { }
                        }
                    }
                }
            }
            else if (_dotObj != null)
            {
                _dotObj.SetActive(false);
            }

            // PC desktop click support so it works seamlessly on PC too
            if (Mouse.current != null)
            {
                bool down = Mouse.current.leftButton.isPressed;
                if (down && !_prevMouseDown && Time.unscaledTime >= _pokeCooldown)
                {
                    Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
                    if (cam != null)
                    {
                        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                        Plane plane = new Plane(_cardRect.forward, _cardRect.position);
                        if (plane.Raycast(ray, out float enter) && enter > 0f && enter < 10f)
                        {
                            Vector3 worldHit = ray.GetPoint(enter);
                            Vector3 local = _cardRect.InverseTransformPoint(worldHit);
                            if (TryHitLocalPoint(new Vector2(local.x, local.y)))
                            {
                                _pokeCooldown = Time.unscaledTime + 0.16f;
                            }
                        }
                    }
                }
                _prevMouseDown = down;
            }
        }

        private bool TryHitLocalPoint(Vector2 cardLocalPoint)
        {
            if (_backZone != null && _backZone.gameObject.activeSelf && ContainsLocal(_backZone, cardLocalPoint))
            {
                _category = "HOME";
                _pageIndex = 0;
                RebuildEntries();
                return true;
            }

            if (_prevZone != null && ContainsLocal(_prevZone, cardLocalPoint))
            {
                int totalPages = Mathf.Max(1, Mathf.CeilToInt(_currentEntries.Count / (float)ButtonsPerPage));
                _pageIndex = (_pageIndex - 1 + totalPages) % totalPages;
                BindCurrentPage();
                return true;
            }

            if (_nextZone != null && ContainsLocal(_nextZone, cardLocalPoint))
            {
                int totalPages = Mathf.Max(1, Mathf.CeilToInt(_currentEntries.Count / (float)ButtonsPerPage));
                _pageIndex = (_pageIndex + 1) % totalPages;
                BindCurrentPage();
                return true;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                ButtonSlot slot = _slots[i];
                if (slot.Entry == null || !slot.Rect.gameObject.activeSelf) continue;
                if (ContainsLocal(slot.Rect, cardLocalPoint))
                {
                    try { slot.Entry.OnClick?.Invoke(); } catch { }
                    RefreshButtonStates();
                    return true;
                }
            }

            return false;
        }

        private bool ContainsLocal(RectTransform child, Vector2 cardLocalPoint)
        {
            // Transform point from _cardRect local space to child's local space
            Vector3 world = _cardRect.TransformPoint(new Vector3(cardLocalPoint.x, cardLocalPoint.y, 0f));
            Vector3 childLocal = child.InverseTransformPoint(world);
            return child.rect.Contains(new Vector2(childLocal.x, childLocal.y));
        }

        private void RebuildEntries()
        {
            _currentEntries.Clear();

            if (_category == "HOME")
            {
                AddCategoryEntry("Settings");
                AddCategoryEntry("Movement");
                AddCategoryEntry("Room");
                AddCategoryEntry("Fun");
                AddCategoryEntry("Visual");
                AddCategoryEntry("PropForge");
                AddCategoryEntry("Player");
                AddCategoryEntry("Tools");
                AddCategoryEntry("Game");
                AddCategoryEntry("Overlays");
                AddCategoryEntry("Flight");
            }
            else if (_category == "Settings")
            {
                _currentEntries.Add(new PillEntry
                {
                    Label = "Switch to macOS Window",
                    OnClick = () => _menu?.SetMenuStyle(0)
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Swap Menu Hand (L/R)",
                    IsOn = () => _config != null && _config.MenuHand.Value == 1,
                    OnClick = () =>
                    {
                        if (_config != null) _config.MenuHand.Value = _config.MenuHand.Value == 0 ? 1 : 0;
                    }
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Mount: " + MountLabel(),
                    OnClick = () =>
                    {
                        if (_config != null)
                        {
                            _config.HandheldAttach.Value = (_config.HandheldAttach.Value + 1) % 3;
                            RebuildEntries();
                        }
                    }
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Aura Glow Halo",
                    IsOn = () => _config == null || _config.HandheldAura.Value,
                    OnClick = () =>
                    {
                        if (_config != null)
                        {
                            _config.HandheldAura.Value = !_config.HandheldAura.Value;
                            ApplyThemeColors();
                        }
                    }
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Joystick Press to Open",
                    IsOn = () => _config != null && (_config.MenuOpenMode.Value == 1 || _config.MenuOpenMode.Value == 3),
                    OnClick = () =>
                    {
                        if (_config != null)
                            _config.MenuOpenMode.Value = _config.MenuOpenMode.Value == 1 ? 0 : 1;
                    }
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Save Settings Now",
                    OnClick = () => _menu?.SaveAllSettings()
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Panic Stop All Mods",
                    OnClick = () => ModRegistry.DisableAll()
                });
            }
            else if (_category == "Room")
            {
                _currentEntries.Add(new PillEntry
                {
                    Label = "Disconnect Room",
                    OnClick = () =>
                    {
                        NetworkSystem sys = NetworkSystem.Instance;
                        if (sys != null) sys.ReturnToSinglePlayer();
                    }
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Open Player List (macOS)",
                    OnClick = () =>
                    {
                        _menu?.SetMenuStyle(0);
                        _menu?.OpenPage("cascade.players", "Player List");
                    }
                });
            }
            else if (_category == "PropForge")
            {
                _currentEntries.Add(new PillEntry { Label = "Build Wall Block", OnClick = () => SandboxManager.SpawnProp(SandboxPropType.WallBlock) });
                _currentEntries.Add(new PillEntry { Label = "Build Floor Slab", OnClick = () => SandboxManager.SpawnProp(SandboxPropType.FloorSlab) });
                _currentEntries.Add(new PillEntry { Label = "Spawn Crate", OnClick = () => SandboxManager.SpawnProp(SandboxPropType.Crate) });
                _currentEntries.Add(new PillEntry { Label = "Spawn Trampoline", OnClick = () => SandboxManager.SpawnProp(SandboxPropType.Trampoline) });
                _currentEntries.Add(new PillEntry { Label = "Spawn Explosive Barrel", OnClick = () => SandboxManager.SpawnProp(SandboxPropType.ExplosiveBarrel) });
                _currentEntries.Add(new PillEntry { Label = "Spawn Ragdoll", OnClick = () => SandboxManager.SpawnProp(SandboxPropType.Ragdoll) });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Tool: Physics Gun",
                    IsOn = () => SandboxManager.ActiveTool == SandboxToolMode.PhysicsGun,
                    OnClick = () => SandboxManager.ActiveTool = SandboxManager.ActiveTool == SandboxToolMode.PhysicsGun ? SandboxToolMode.None : SandboxToolMode.PhysicsGun
                });
                _currentEntries.Add(new PillEntry
                {
                    Label = "Tool: Build Placer",
                    IsOn = () => SandboxManager.ActiveTool == SandboxToolMode.ConstructionPlacer,
                    OnClick = () => SandboxManager.ActiveTool = SandboxManager.ActiveTool == SandboxToolMode.ConstructionPlacer ? SandboxToolMode.None : SandboxToolMode.ConstructionPlacer
                });
                _currentEntries.Add(new PillEntry { Label = "Clear All Props", OnClick = SandboxManager.ClearAll });
            }
            else
            {
                IReadOnlyList<Mod> all = ModRegistry.DiscoveredMods;
                for (int i = 0; i < all.Count; i++)
                {
                    Mod mod = all[i];
                    if (mod == null || !string.Equals(ModRegistry.CategoryOf(mod), _category, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Mod captured = mod;
                    _currentEntries.Add(new PillEntry
                    {
                        Label = captured.Name,
                        IsOn = captured.isToggle ? (Func<bool>)(() => captured.Enabled && captured.IsActive) : null,
                        OnClick = () =>
                        {
                            if (captured.isToggle)
                            {
                                bool next = !(captured.Enabled && captured.IsActive);
                                ModRegistry.SetEffect(captured, next);
                            }
                            else
                            {
                                try { captured.Execute(); } catch { }
                            }
                        }
                    });
                }
            }

            _pageIndex = 0;
            BindCurrentPage();
        }

        private string MountLabel()
        {
            int m = _config != null ? _config.HandheldAttach.Value : 0;
            return m == 1 ? "Floating" : (m == 2 ? "Wrist Tap" : "Wrist");
        }

        private void AddCategoryEntry(string cat)
        {
            _currentEntries.Add(new PillEntry
            {
                Label = cat,
                IsCategory = true,
                OnClick = () =>
                {
                    _category = cat;
                    _pageIndex = 0;
                    RebuildEntries();
                }
            });
        }

        private void BindCurrentPage()
        {
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(_currentEntries.Count / (float)ButtonsPerPage));
            _pageIndex = Mathf.Clamp(_pageIndex, 0, totalPages - 1);

            if (_subTxt != null) _subTxt.text = _category.ToUpperInvariant();
            if (_backZone != null) _backZone.gameObject.SetActive(_category != "HOME");
            if (_pageTxt != null) _pageTxt.text = (_pageIndex + 1) + "/" + totalPages;

            int start = _pageIndex * ButtonsPerPage;
            for (int i = 0; i < _slots.Count; i++)
            {
                int idx = start + i;
                ButtonSlot slot = _slots[i];
                if (idx < _currentEntries.Count)
                {
                    slot.Entry = _currentEntries[idx];
                    slot.Rect.gameObject.SetActive(true);
                    slot.Label.text = slot.Entry.Label;
                    slot.Chevron.gameObject.SetActive(slot.Entry.IsCategory);
                }
                else
                {
                    slot.Entry = null;
                    slot.Rect.gameObject.SetActive(false);
                }
            }

            RefreshButtonStates();
        }

        private void RefreshButtonStates()
        {
            Color accent = ResolveGlassAccent();
            if (_badgeTxt != null)
            {
                _badgeTxt.text = "<color=#C4B5FD>●</color>  " + ModRegistry.ActiveCount;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                ButtonSlot slot = _slots[i];
                if (slot.Entry == null) continue;
                bool on = slot.Entry.IsOn != null && slot.Entry.IsOn();
                slot.Bg.color = on
                    ? new Color(accent.r, accent.g, accent.b, 0.56f)
                    : new Color(1f, 1f, 1f, 0.14f);
                slot.Rim.color = on
                    ? new Color(1f, 1f, 1f, 0.88f)
                    : new Color(1f, 1f, 1f, 0.42f);
            }
        }

        private Color ResolveGlassAccent()
        {
            CascadeTheme t = CascadeTheme.Active;
            if (t != null && !string.Equals(t.Name, "Default", StringComparison.OrdinalIgnoreCase))
                return t.Accent;
            // Dreamy lavender-periwinkle default glass palette
            return new Color(0.56f, 0.46f, 0.96f, 1f);
        }

        internal void ApplyThemeColors()
        {
            if (_cardBg == null) return;
            Color accent = ResolveGlassAccent();
            _cardBg.color = new Color(
                Mathf.Lerp(accent.r, 0.72f, 0.25f),
                Mathf.Lerp(accent.g, 0.68f, 0.25f),
                Mathf.Lerp(accent.b, 0.98f, 0.25f),
                0.56f);

            if (_auraImg != null)
            {
                bool showAura = _config == null || _config.HandheldAura.Value;
                _auraImg.gameObject.SetActive(showAura);
                _auraImg.color = new Color(accent.r, accent.g * 0.75f, accent.b, 0.46f);
            }

            if (_dotRenderer != null && _dotRenderer.material != null)
            {
                _dotRenderer.material.color = Color.Lerp(accent, Color.white, 0.55f);
            }

            RefreshButtonStates();
        }

        private void EnsureBuilt()
        {
            if (_rootObj != null) return;

            _rootObj = new GameObject("CascadeHandheldGlassMenu");
            if (Plugin.Root != null) _rootObj.transform.SetParent(Plugin.Root.transform, false);

            _canvas = _rootObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rootRt = _rootObj.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(CardWidth, CardHeight);
            _cardRect = rootRt;

            // Soft radial glowing aura behind the glass card
            RectTransform auraRt = CascadeUI.Rect("AuraHalo", _cardRect);
            auraRt.anchorMin = auraRt.anchorMax = new Vector2(0.5f, 0.5f);
            auraRt.sizeDelta = new Vector2(CardWidth * 1.62f, CardHeight * 1.28f);
            _auraSprite = CreateRadialGlowSprite(96);
            _auraImg = auraRt.gameObject.AddComponent<Image>();
            _auraImg.sprite = _auraSprite;
            _auraImg.raycastTarget = false;

            // Main frosted-glass rounded card
            RectTransform cardBgRt = CascadeUI.Rect("GlassCard", _cardRect);
            CascadeUI.Stretch(cardBgRt);
            _cardBg = CascadeUI.Image(cardBgRt, CascadeSprites.Rounded(28), new Color(0.56f, 0.48f, 0.94f, 0.58f), Image.Type.Sliced);
            _cardBg.raycastTarget = false;

            // Soft top specular gradient sheen
            RectTransform sheenRt = CascadeUI.Rect("GlassSheen", _cardRect);
            CascadeUI.Stretch(sheenRt);
            Image sheenImg = CascadeUI.Image(sheenRt, CascadeSprites.Rounded(28), Color.white, Image.Type.Sliced);
            sheenImg.raycastTarget = false;
            CascadeGradient grad = sheenImg.gameObject.AddComponent<CascadeGradient>();
            grad.Top = new Color(0.92f, 0.96f, 1f, 0.24f);
            grad.Bottom = new Color(0.42f, 0.24f, 0.86f, 0.14f);
            grad.Refresh();

            // Crisp bright white rounded outer border
            RectTransform rimRt = CascadeUI.Rect("GlassRim", _cardRect);
            CascadeUI.Stretch(rimRt);
            Image rimImg = CascadeUI.Outline(rimRt, new Color(1f, 1f, 1f, 0.85f), 28);
            if (rimImg != null) rimImg.raycastTarget = false;

            // Header Title ("Cascade") + Subtitle ("HOME")
            RectTransform titleRt = CascadeUI.Rect("Title", _cardRect);
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -18f);
            titleRt.sizeDelta = new Vector2(180f, 34f);
            _titleTxt = CascadeUI.Text(titleRt, "Cascade", 24f, Color.white, TextAlignmentOptions.Center, true);

            RectTransform subRt = CascadeUI.Rect("Subtitle", _cardRect);
            subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -50f);
            subRt.sizeDelta = new Vector2(200f, 18f);
            _subTxt = CascadeUI.Text(subRt, "HOME", 10.5f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, true);

            // Top-right badge pill (● active count)
            RectTransform badgeRt = CascadeUI.Rect("ActiveBadge", _cardRect);
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-18f, -24f);
            badgeRt.sizeDelta = new Vector2(54f, 24f);
            CascadeUI.Image(badgeRt, CascadeSprites.Rounded(12), new Color(1f, 1f, 1f, 0.22f), Image.Type.Sliced);
            RectTransform badgeRim = CascadeUI.Rect("Rim", badgeRt);
            CascadeUI.Stretch(badgeRim);
            CascadeUI.Outline(badgeRim, new Color(1f, 1f, 1f, 0.55f), 12);
            RectTransform badgeTxtRt = CascadeUI.Rect("Label", badgeRt);
            CascadeUI.Stretch(badgeTxtRt, 4f, 4f, 0f, 0f);
            _badgeTxt = CascadeUI.Text(badgeTxtRt, "● 0", 11f, Color.white, TextAlignmentOptions.Center, true);

            // Top-left Back pill (visible inside subcategories)
            _backZone = CascadeUI.Rect("BackPill", _cardRect);
            _backZone.anchorMin = _backZone.anchorMax = new Vector2(0f, 1f);
            _backZone.pivot = new Vector2(0f, 1f);
            _backZone.anchoredPosition = new Vector2(18f, -24f);
            _backZone.sizeDelta = new Vector2(54f, 24f);
            _backBg = CascadeUI.Image(_backZone, CascadeSprites.Rounded(12), new Color(1f, 1f, 1f, 0.22f), Image.Type.Sliced);
            RectTransform backRim = CascadeUI.Rect("Rim", _backZone);
            CascadeUI.Stretch(backRim);
            CascadeUI.Outline(backRim, new Color(1f, 1f, 1f, 0.55f), 12);
            RectTransform backTxtRt = CascadeUI.Rect("Label", _backZone);
            CascadeUI.Stretch(backTxtRt, 4f, 4f, 0f, 0f);
            _backTxt = CascadeUI.Text(backTxtRt, "‹ Back", 11f, Color.white, TextAlignmentOptions.Center, true);
            _backZone.gameObject.SetActive(false);

            // 8 translucent pill button slots
            const float startY = -82f;
            const float btnH = 40f;
            const float gap = 8f;
            for (int i = 0; i < ButtonsPerPage; i++)
            {
                RectTransform btnRt = CascadeUI.Rect("Pill_" + i, _cardRect);
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 1f);
                btnRt.pivot = new Vector2(0.5f, 1f);
                btnRt.anchoredPosition = new Vector2(0f, startY - i * (btnH + gap));
                btnRt.sizeDelta = new Vector2(CardWidth - 36f, btnH);

                Image bg = CascadeUI.Image(btnRt, CascadeSprites.Rounded(20), new Color(1f, 1f, 1f, 0.14f), Image.Type.Sliced);
                bg.raycastTarget = false;

                RectTransform bRimRt = CascadeUI.Rect("Rim", btnRt);
                CascadeUI.Stretch(bRimRt);
                Image bRim = CascadeUI.Outline(bRimRt, new Color(1f, 1f, 1f, 0.48f), 20);
                if (bRim != null) bRim.raycastTarget = false;

                RectTransform lblRt = CascadeUI.Rect("Label", btnRt);
                CascadeUI.Stretch(lblRt, 18f, 28f, 0f, 0f);
                TextMeshProUGUI lbl = CascadeUI.Text(lblRt, string.Empty, 13.5f, Color.white, TextAlignmentOptions.Center, true);
                lbl.overflowMode = TextOverflowModes.Ellipsis;

                RectTransform chevRt = CascadeUI.Rect("Chevron", btnRt);
                chevRt.anchorMin = chevRt.anchorMax = new Vector2(1f, 0.5f);
                chevRt.pivot = new Vector2(1f, 0.5f);
                chevRt.anchoredPosition = new Vector2(-14f, 0f);
                chevRt.sizeDelta = new Vector2(18f, 24f);
                TextMeshProUGUI chev = CascadeUI.Text(chevRt, "›", 17f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, true);

                _slots.Add(new ButtonSlot
                {
                    Rect = btnRt,
                    Bg = bg,
                    Rim = bRim,
                    Label = lbl,
                    Chevron = chev
                });
            }

            // Bottom Pagination Capsule ( ‹   1/2   › )
            RectTransform navBar = CascadeUI.Rect("PageCapsule", _cardRect);
            navBar.anchorMin = navBar.anchorMax = new Vector2(0.5f, 0f);
            navBar.pivot = new Vector2(0.5f, 0f);
            navBar.anchoredPosition = new Vector2(0f, 18f);
            navBar.sizeDelta = new Vector2(142f, 28f);
            CascadeUI.Image(navBar, CascadeSprites.Rounded(14), new Color(1f, 1f, 1f, 0.20f), Image.Type.Sliced);
            RectTransform navRim = CascadeUI.Rect("Rim", navBar);
            CascadeUI.Stretch(navRim);
            CascadeUI.Outline(navRim, new Color(1f, 1f, 1f, 0.58f), 14);

            _prevZone = CascadeUI.Rect("Prev", navBar);
            _prevZone.anchorMin = new Vector2(0f, 0f);
            _prevZone.anchorMax = new Vector2(0.35f, 1f);
            _prevZone.offsetMin = _prevZone.offsetMax = Vector2.zero;
            CascadeUI.Text(_prevZone, "‹", 16f, Color.white, TextAlignmentOptions.Center, true);

            RectTransform pgRt = CascadeUI.Rect("PageLabel", navBar);
            pgRt.anchorMin = new Vector2(0.30f, 0f);
            pgRt.anchorMax = new Vector2(0.70f, 1f);
            pgRt.offsetMin = pgRt.offsetMax = Vector2.zero;
            _pageTxt = CascadeUI.Text(pgRt, "1/1", 11.5f, Color.white, TextAlignmentOptions.Center, true);

            _nextZone = CascadeUI.Rect("Next", navBar);
            _nextZone.anchorMin = new Vector2(0.65f, 0f);
            _nextZone.anchorMax = new Vector2(1f, 1f);
            _nextZone.offsetMin = _nextZone.offsetMax = Vector2.zero;
            CascadeUI.Text(_nextZone, "›", 16f, Color.white, TextAlignmentOptions.Center, true);

            _rootObj.SetActive(false);
        }

        private void EnsureDot()
        {
            if (_dotObj != null) return;

            _dotObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _dotObj.name = "CascadeHandheldFingerDot";
            if (Plugin.Root != null) _dotObj.transform.SetParent(Plugin.Root.transform, false);
            Collider col = _dotObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _dotRenderer = _dotObj.GetComponent<Renderer>();
            if (_dotRenderer != null)
            {
                Shader sh = Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    Material mat = new Material(sh);
                    mat.color = new Color(0.88f, 0.82f, 1f, 0.96f);
                    mat.renderQueue = 4500;
                    _dotRenderer.material = mat;
                }
            }
            _dotObj.SetActive(false);
        }

        private static Sprite CreateRadialGlowSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnDestroy()
        {
            if (_rootObj != null) Destroy(_rootObj);
            if (_dotObj != null) Destroy(_dotObj);
            if (_auraSprite != null && _auraSprite.texture != null) Destroy(_auraSprite.texture);
        }
    }
}
