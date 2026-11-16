using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Display-only information cards above every other player.
    ///
    /// Performance contract:
    /// - One card object per player, created on join and destroyed on leave.
    /// - Roster is re-read twice a second; transforms are updated per frame.
    /// - Text is rebuilt at most four times a second per card, with a reused
    ///   StringBuilder, and only while the card is on screen.
    /// - Update does no LINQ, no FindObjectsOfType, no string concatenation and
    ///   no collection allocation.
    /// - Disabled means OnDisable destroyed every card, and the registry stops
    ///   calling Update.
    ///
    /// The card is a world-space canvas with a rounded background, border,
    /// shadow and a colored swatch. It never has a GraphicRaycaster, every
    /// graphic has raycastTarget = false, and the CanvasGroup blocks input, so
    /// nothing on it can be clicked.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class PlayerHud : Mod
    {
        private const string CardName = "CascadePlayerHud";
        private const float CardPixelScale = 0.0016f;
        private const float RosterInterval = 0.5f;
        private const float TextInterval = 0.25f;
        private const float CosmeticsInterval = 2f;
        private const float FadeSpeed = 7f;

        private sealed class Card
        {
            public string Id;
            public NetPlayer Player;
            public VRRig Rig;
            public bool IsLocal;
            public bool Present;
            public bool Tagged;
            public bool TaggedKnown;
            public bool Visible;
            public int Cosmetics;
            public float NextRefresh;
            public float NextCosmetics;
            public float Distance;
            public float Width;
            public string DisplayName;

            public GameObject Root;
            public RectTransform RootRect;
            public CanvasGroup Group;
            public Image Shadow;
            public Image Border;
            public Image Background;
            public Image Accent;
            public Image Swatch;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Body;
            public RectTransform BodyRect;
        }

        // ---------------------------------------------------------------
        // Settings (persisted by ModRegistry through ModSetting)
        // ---------------------------------------------------------------
        private bool _showName = true;
        private bool _showSwatch = true;
        private bool _showColorCode = true;
        private bool _showId = true;
        private bool _showProps = true;
        private bool _showTagged = true;
        private bool _showPlatform = true;
        private bool _showPing = true;
        private bool _showDistance = true;
        private bool _showCosmetics = true;
        private bool _showFps;
        private bool _compact;
        private bool _taggedOnly;
        private bool _hideSelf = true;
        private bool _hideOffscreen = true;

        private int _style;
        private int _nameColor;
        private bool _styleDirty;

        private float _size = 1f;
        private float _maxDistance = 30f;
        private float _height = 0.45f;
        private float _opacity = 0.92f;

        private string _propFilter = "all";
        private int _maxProps = 3;

        // ---------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------
        private readonly Dictionary<string, Card> _cards =
            new Dictionary<string, Card>(StringComparer.Ordinal);
        private readonly List<string> _dead = new List<string>(8);
        private readonly List<NetPlayer> _players = new List<NetPlayer>(16);
        private readonly HashSet<string> _propKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _builder = new StringBuilder(512);
        private readonly StringBuilder _props = new StringBuilder(128);

        private Material _imageMaterial;
        private Material _textMaterial;
        private bool _materialsReady;
        private bool _materialsFailed;
        private Camera _camera;
        private float _nextRoster;
        private float _fps;
        private float _fpsAccum;
        private int _fpsFrames;
        private string _lastFilter;
        private bool _filterAll = true;

        public PlayerHud()
        {
            Name = "Player HUD";
            isToggle = true;

            // Appearance settings come first so a saved Style value is applied
            // before any saved manual overrides are restored.
            AddSetting(ModSetting.Dropdown("style", "Style",
                "Appearance preset: Classic, VRChat, Fortnite, Minimal or Glass",
                OverlayStyles.Names,
                () => _style,
                value =>
                {
                    if (_style == value) return;
                    _style = value;
                    _styleDirty = true;
                }));
            AddSetting(ModSetting.Dropdown("namecolor", "Name color",
                "Style default, theme text, plain white, or the player's rig color",
                new[] { "Style default", "Theme", "White", "Rig color" },
                () => _nameColor,
                value =>
                {
                    if (_nameColor == value) return;
                    _nameColor = value;
                    _styleDirty = true;
                }));

            AddSetting(ModSetting.Toggle("name", "Name", "Show the player's nickname",
                () => _showName, value => _showName = value));
            AddSetting(ModSetting.Toggle("swatch", "Color swatch", "Show a small square of their gorilla color",
                () => _showSwatch, value => _showSwatch = value));
            AddSetting(ModSetting.Toggle("colorcode", "Color code", "RGB values and the hex color",
                () => _showColorCode, value => _showColorCode = value));
            AddSetting(ModSetting.Toggle("playerid", "Player ID", "Actor number and short user ID",
                () => _showId, value => _showId = value));
            AddSetting(ModSetting.Toggle("props", "Custom properties", "Synced Photon player properties",
                () => _showProps, value => _showProps = value));
            AddSetting(ModSetting.Toggle("tagged", "Tagged status", "Game-mode tagged / infected marker",
                () => _showTagged, value => _showTagged = value));
            AddSetting(ModSetting.Toggle("platform", "Platform", "Report platform (PC, Android, ...)",
                () => _showPlatform, value => _showPlatform = value));
            AddSetting(ModSetting.Toggle("ping", "Ping", "Current room ping (from Photon)",
                () => _showPing, value => _showPing = value));
            AddSetting(ModSetting.Toggle("distance", "Distance", "Distance from your view camera",
                () => _showDistance, value => _showDistance = value));
            AddSetting(ModSetting.Toggle("cosmetics", "Cosmetics count", "Number of worn cosmetics",
                () => _showCosmetics, value => _showCosmetics = value));
            AddSetting(ModSetting.Toggle("fps", "FPS", "Local frames per second (same for every card)",
                () => _showFps, value => _showFps = value));
            AddSetting(ModSetting.Toggle("compact", "Compact layout", "Only the most useful two or three lines",
                () => _compact, value => _compact = value));
            AddSetting(ModSetting.Slider("size", "Size", "Fixed card size; distance does not change it", 0.5f, 2f,
                () => _size, value => _size = value, value => value.ToString("0.00") + "x"));
            AddSetting(ModSetting.Slider("maxdistance", "Maximum distance", "Cards fade out past this range",
                5f, 80f, () => _maxDistance, value => _maxDistance = value, value => value.ToString("0") + " m"));
            AddSetting(ModSetting.Slider("height", "Height offset", "How far above the head the card sits",
                0.1f, 2f, () => _height, value => _height = value, value => value.ToString("0.00") + " m"));
            AddSetting(ModSetting.Slider("opacity", "Opacity", "Card transparency", 0.2f, 1f,
                () => _opacity, value => _opacity = value, value => (value * 100f).ToString("0") + "%"));
            AddSetting(ModSetting.Toggle("taggedonly", "Tagged only", "Hide cards for players who are not tagged",
                () => _taggedOnly, value => _taggedOnly = value));
            AddSetting(ModSetting.Toggle("hideself", "Hide my own", "Never draw a card above your own head",
                () => _hideSelf, value => _hideSelf = value));
            AddSetting(ModSetting.Toggle("hideoffscreen", "Hide off-screen", "Fade out cards behind you or outside the view",
                () => _hideOffscreen, value => _hideOffscreen = value));
            AddSetting(ModSetting.Text("propfilter", "Custom property keys",
                "Comma-separated allow-list. \"all\" or empty shows every key; \"none\" hides them.",
                () => _propFilter, value => _propFilter = value));
            AddSetting(ModSetting.Stepper("maxprops", "Maximum custom properties", "Keys shown per card",
                0, 8, () => _maxProps, value => _maxProps = value));
        }

        // ---------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------
        public override void OnEnable()
        {
            EnsureMaterials();
            _cards.Clear();
            _nextRoster = 0f;
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            ProfilerStats.Register(ProfilerStats.PlayerHudKey, "Player HUD",
                "Pooled per-player world-space cards");
            Plugin.Log?.LogInfo("Cascade Player HUD active.");
        }

        public override void OnDisable()
        {
            DestroyAll();
            ProfilerStats.Record(ProfilerStats.PlayerHudKey, 0.0, false);
            Plugin.Log?.LogInfo("Cascade Player HUD stopped; every card removed.");
        }

        public override void Update()
        {
            long start = ProfilerStats.Timestamp;
            float now = Time.unscaledTime;

            if (_camera == null) _camera = CascadeMenuBehaviour.ResolveViewCamera();
            UpdateFps(now);

            if (_styleDirty)
            {
                _styleDirty = false;
                DestroyAll();
                _nextRoster = 0f;
            }

            if (now >= _nextRoster)
            {
                RefreshRoster();
                _nextRoster = now + RosterInterval;
            }

            Camera camera = _camera;
            if (camera == null)
            {
                ProfilerStats.Record(ProfilerStats.PlayerHudKey, ProfilerStats.ElapsedMs(start), false);
                return;
            }

            Vector3 eye = camera.transform.position;

            foreach (KeyValuePair<string, Card> pair in _cards)
            {
                Card card = pair.Value;
                if (card == null || card.Root == null) continue;
                if (!card.Present) continue;

                if (!IsRigAlive(card.Rig))
                {
                    card.Rig = card.Player != null ? PlayerActions.FindRigObject(card.Player) : null;
                    if (card.IsLocal) card.Rig = VRRig.LocalRig;
                }
                if (!IsRigAlive(card.Rig)) continue;

                Vector3 head = GetHead(card.Rig);
                card.Distance = Vector3.Distance(eye, head);

                bool inRange = card.Distance <= _maxDistance;
                bool onScreen = !_hideOffscreen || IsOnScreen(camera, head);
                bool taggedAllowed = !_taggedOnly || !TagProbe.Available || card.Tagged;

                float targetAlpha = 0f;
                if (inRange && onScreen && taggedAllowed) targetAlpha = _opacity;

                float fade = Mathf.Clamp01((_maxDistance - card.Distance) /
                                           Mathf.Max(0.5f, _maxDistance * 0.15f));
                targetAlpha *= fade;

                if (targetAlpha > 0.01f)
                {
                    if (!card.Visible)
                    {
                        card.Visible = true;
                        card.Root.SetActive(true);
                    }

                    UpdateTransform(card, camera, head);

                    if (now >= card.NextRefresh)
                    {
                        RefreshCard(card, now);
                        card.NextRefresh = now + TextInterval + ((card.GetHashCode() & 3) * 0.045f);
                    }
                }
                else if (card.Visible)
                {
                    card.Visible = false;
                    card.Root.SetActive(false);
                }

                float current = card.Group.alpha;
                card.Group.alpha = Mathf.MoveTowards(current, targetAlpha, Time.unscaledDeltaTime * FadeSpeed);
            }

            ProfilerStats.Record(ProfilerStats.PlayerHudKey, ProfilerStats.ElapsedMs(start), true);
        }

        private void UpdateFps(float now)
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum < 0.25f) return;
            _fps = _fpsFrames / Mathf.Max(0.0001f, _fpsAccum);
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }

        private void RefreshRoster()
        {
            _players.Clear();
            _players.AddRange(PlayerActions.GetPlayers());

            if (!_hideSelf)
            {
                NetPlayer local = PlayerActions.LocalPlayer();
                if (local != null) _players.Add(local);
            }

            foreach (KeyValuePair<string, Card> pair in _cards)
            {
                pair.Value.Present = false;
            }

            for (int i = 0; i < _players.Count; i++)
            {
                NetPlayer player = _players[i];
                if (player == null) continue;

                string id = !string.IsNullOrEmpty(player.UserId)
                    ? player.UserId
                    : "actor:" + player.ActorNumber;

                Card card;
                if (!_cards.TryGetValue(id, out card))
                {
                    card = CreateCard(id, player);
                    _cards[id] = card;
                }

                card.Player = player;
                card.Present = true;
                card.IsLocal = player.IsLocal;
                card.Rig = card.IsLocal ? VRRig.LocalRig : PlayerActions.FindRigObject(player);
            }

            _dead.Clear();
            foreach (KeyValuePair<string, Card> pair in _cards)
            {
                if (!pair.Value.Present) _dead.Add(pair.Key);
            }
            for (int i = 0; i < _dead.Count; i++)
            {
                Card card;
                if (_cards.TryGetValue(_dead[i], out card)) DestroyCard(card);
                _cards.Remove(_dead[i]);
            }
        }

        private Card CreateCard(string id, NetPlayer player)
        {
            var card = new Card();
            card.Id = id;
            card.Player = player;
            card.IsLocal = player != null && player.IsLocal;
            card.NextRefresh = Time.unscaledTime + ((_cards.Count & 3) * 0.05f);

            OverlayStyle style = OverlayStyles.Get(_style);
            float width = style.CardWidth;

            GameObject root = new GameObject(CardName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            if (Plugin.Root != null) root.transform.SetParent(Plugin.Root.transform, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32760;
            canvas.worldCamera = _camera;

            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, 60f);
            rect.localScale = Vector3.one * CardPixelScale;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            CascadeTheme theme = Theme;
            CascadingThemeFallbacks(theme, style, out Color shadow, out Color border, out Color background,
                out Color accent, out Color text, out Color secondary);

            bool themeName = UsesThemeName(style);
            Color nameColor = themeName ? text : Color.white;

            RectTransform shadowRect = CascadeUI.Rect("Shadow", rect);
            CascadeUI.Stretch(shadowRect, -3f, -3f, 3f, -5f);
            card.Shadow = ApplyImage(CascadeUI.Panel(shadowRect, shadow, style.CornerRadius + 2));
            CascadeTheme.Bind(card.Shadow, CascadeRole.ShadowColor);
            card.Shadow.gameObject.SetActive(style.ShowShadow);

            RectTransform borderRect = CascadeUI.Rect("Border", rect);
            CascadeUI.Stretch(borderRect);
            card.Border = ApplyImage(CascadeUI.Panel(borderRect, border, style.CornerRadius + 1));
            CascadeTheme.Bind(card.Border, CascadeRole.Accent, style.BorderAlpha);
            card.Border.gameObject.SetActive(style.ShowBorder);

            RectTransform backgroundRect = CascadeUI.Rect("Background", rect);
            CascadeUI.Stretch(backgroundRect, 1.5f, 1.5f, 1.5f, 1.5f);
            card.Background = ApplyImage(CascadeUI.Panel(backgroundRect, background, style.CornerRadius));
            CascadeTheme.Bind(card.Background, CascadeRole.ContentBg, style.BackgroundAlpha);
            card.Background.gameObject.SetActive(style.ShowBackground);

            RectTransform accentRect = CascadeUI.Rect("Accent", rect);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.offsetMin = new Vector2(2f, 7f);
            accentRect.offsetMax = new Vector2(5f, -7f);
            card.Accent = ApplyImage(CascadeUI.Panel(accentRect, accent, 2));
            CascadeTheme.Bind(card.Accent, CascadeRole.Accent);
            card.Accent.gameObject.SetActive(style.ShowAccent);

            RectTransform titleRect = CascadeUI.TopLeft("Title", rect, 12f, 7f,
                style.CenterName ? width - 24f : width - 46f, 24f);
            card.Title = CascadeUI.Text(titleRect, string.Empty, style.NameSize, nameColor,
                style.CenterName ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft,
                style.BoldName, false);
            ApplyText(card.Title);
            if (themeName) CascadeTheme.Bind(card.Title, CascadeRole.TextPrimary);

            RectTransform swatchRect = CascadeUI.TopRight("Swatch", rect, 12f, 9f, 16f, 16f);
            card.Swatch = ApplyImage(CascadeUI.Panel(swatchRect, Color.white, 4));

            RectTransform bodyRect = CascadeUI.TopLeft("Body", rect, 12f, 32f, width - 24f, 30f);
            card.Body = CascadeUI.Text(bodyRect, string.Empty, 12.5f, secondary,
                TextAlignmentOptions.TopLeft, false, false);
            ApplyText(card.Body);
            CascadeTheme.Bind(card.Body, CascadeRole.TextSecondary);

            card.Width = width;
            card.DisplayName = null;
            card.BodyRect = bodyRect;
            card.Root = root;
            card.RootRect = rect;
            card.Group = group;
            card.Visible = true;

            return card;
        }

        private void DestroyCard(Card card)
        {
            if (card == null) return;
            if (card.Root != null) UnityEngine.Object.Destroy(card.Root);
            card.Root = null;
            card.RootRect = null;
            card.Group = null;
            card.Title = null;
            card.Body = null;
            card.Swatch = null;
        }

        private void DestroyAll()
        {
            foreach (KeyValuePair<string, Card> pair in _cards)
            {
                DestroyCard(pair.Value);
            }
            _cards.Clear();
        }

        // ---------------------------------------------------------------
        // Per-card update
        // ---------------------------------------------------------------
        private void UpdateTransform(Card card, Camera camera, Vector3 head)
        {
            float rigScale = card.Rig != null && card.Rig.scaleFactor > 0.01f ? card.Rig.scaleFactor : 1f;
            Vector3 position = head + Vector3.up * (_height * rigScale);
            card.Root.transform.position = position;

            Vector3 look = position - camera.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
            {
                card.Root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            float scale = CardPixelScale * _size;
            card.Root.transform.localScale = Vector3.one * scale;
        }

        private void RefreshCard(Card card, float now)
        {
            NetPlayer player = card.Player;
            if (player == null) return;

            OverlayStyle style = OverlayStyles.Get(_style);

            string name = card.IsLocal && VRRig.LocalRig != null &&
                          !string.IsNullOrEmpty(VRRig.LocalRig.playerNameVisible)
                ? VRRig.LocalRig.playerNameVisible
                : PlayerActions.SafeName(player);

            Color rigColor = card.Rig != null ? PlayerActions.GetRigColor(card.Rig) : Color.white;

            bool tagged = false;
            bool taggedKnown = TagProbe.TryIsTagged(player, out tagged);
            card.Tagged = tagged;
            card.TaggedKnown = taggedKnown;

            if (now >= card.NextCosmetics)
            {
                card.NextCosmetics = now + CosmeticsInterval;
                card.Cosmetics = CountCosmetics(card.Rig);
            }

            _builder.Length = 0;
            int lines = 0;

            if (_showColorCode)
            {
                AppendLine(ColorCode(rigColor));
                lines++;
            }

            if (_showId)
            {
                AppendLine("ID " + player.ActorNumber + "  " + PlayerActions.ShortId(player.UserId));
                lines++;
            }

            if (_showTagged)
            {
                AppendTagged(taggedKnown, tagged);
                lines++;
            }

            if (!_compact)
            {
                if (_showPlatform)
                {
                    AppendLine("Platform  " + PlayerActions.SafePlatform(player));
                    lines++;
                }

                if (_showPing)
                {
                    AppendLine("Ping  " + PingProbe.Describe());
                    lines++;
                }

                if (_showDistance)
                {
                    AppendLine("Distance  " + card.Distance.ToString("0.0") + " m");
                    lines++;
                }

                if (_showCosmetics)
                {
                    AppendLine("Cosmetics  " + card.Cosmetics);
                    lines++;
                }

                if (_showFps)
                {
                    AppendLine("FPS  " + Mathf.RoundToInt(_fps));
                    lines++;
                }

                if (_showProps && AppendProps(player))
                {
                    lines++;
                }
            }
            else if (_showProps && _maxProps > 0 && AppendProps(player))
            {
                lines++;
            }

            bool showTitle = _showName;
            card.Title.gameObject.SetActive(showTitle);
            if (showTitle)
            {
                string display = style.UppercaseName ? name.ToUpperInvariant() : name;
                if (!string.Equals(display, card.DisplayName, StringComparison.Ordinal))
                {
                    card.DisplayName = display;
                    card.Title.text = display;
                }
            }

            ApplyNameColor(card, style);

            card.Swatch.gameObject.SetActive(_showSwatch);

            float lineHeight = _compact ? 15.5f : 17f;
            float top = showTitle ? 33f : 10f;
            float height = top + Mathf.Max(1, lines) * lineHeight + 8f;
            float width = card.Width > 1f ? card.Width : style.CardWidth;

            card.RootRect.sizeDelta = new Vector2(width, height);
            card.BodyRect.anchoredPosition = new Vector2(12f, -top);
            card.BodyRect.sizeDelta = new Vector2(width - 24f, Mathf.Max(lineHeight, lines * lineHeight + 2f));
            card.Body.text = _builder.ToString();
            card.Swatch.color = rigColor;
        }

        private void AppendLine(string value)
        {
            if (_builder.Length > 0) _builder.Append('\n');
            _builder.Append(value);
        }

        private void AppendTagged(bool known, bool tagged)
        {
            if (_builder.Length > 0) _builder.Append('\n');

            if (!known)
            {
                _builder.Append("<color=#");
                _builder.Append(ColorUtility.ToHtmlStringRGB((Color)Theme.TextTertiary));
                _builder.Append(">○ tag state unavailable</color>");
                return;
            }

            _builder.Append("<color=#");
            _builder.Append(ColorUtility.ToHtmlStringRGB((Color)(tagged ? Theme.ActiveColor : Theme.TextTertiary)));
            _builder.Append('>');
            _builder.Append(tagged ? "● TAGGED" : "○ not tagged");
            _builder.Append("</color>");
        }

        private bool AppendProps(NetPlayer player)
        {
            UpdateFilter();

            _props.Length = 0;
            if (!PhotonProps.Fill(player, _propKeys, _filterAll, _maxProps, _props)) return false;
            if (_props.Length == 0) return false;

            if (_builder.Length > 0) _builder.Append('\n');
            _builder.Append("Props  ");
            _builder.Append(_props);
            return true;
        }

        private void UpdateFilter()
        {
            string raw = _propFilter ?? string.Empty;
            if (string.Equals(raw, _lastFilter, StringComparison.Ordinal)) return;

            _lastFilter = raw;
            _propKeys.Clear();
            string trimmed = raw.Trim();
            _filterAll = trimmed.Length == 0 ||
                         trimmed.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                         trimmed == "*";

            if (_filterAll || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase)) return;

            string[] parts = trimmed.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string key = parts[i].Trim();
                if (key.Length > 0) _propKeys.Add(key);
            }
        }

        // ---------------------------------------------------------------
        // Data helpers
        // ---------------------------------------------------------------
        private static int CountCosmetics(VRRig rig)
        {
            if (rig == null) return 0;
            try
            {
                return CosmeticChecker.Inspect(rig).Total;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string ColorCode(Color color)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            return "Color  " + r + "," + g + "," + b + "  #" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static Vector3 GetHead(VRRig rig)
        {
            if (rig == null) return Vector3.zero;
            if (rig.headConstraint != null) return rig.headConstraint.position;
            float scale = rig.scaleFactor > 0.01f ? rig.scaleFactor : 1f;
            return rig.transform.position + Vector3.up * (1.5f * scale);
        }

        private static bool IsRigAlive(VRRig rig)
        {
            return rig != null && rig.gameObject != null && rig.gameObject.activeInHierarchy;
        }

        private static bool IsOnScreen(Camera camera, Vector3 point)
        {
            if (camera == null) return true;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > 0f &&
                   viewport.x > -0.05f && viewport.x < 1.05f &&
                   viewport.y > -0.05f && viewport.y < 1.05f;
        }

        private static CascadeTheme Theme
        {
            get { return CascadeTheme.Active; }
        }

        /// <summary>True when the name should follow the live theme color.</summary>
        private bool UsesThemeName(OverlayStyle style)
        {
            if (_nameColor == 1) return true;
            if (_nameColor == 2 || _nameColor == 3) return false;
            return !style.WhiteName;
        }

        private void ApplyNameColor(Card card, OverlayStyle style)
        {
            int mode = _nameColor;
            if (mode == 0) mode = style.WhiteName ? 2 : 1;

            if (mode == 2)
            {
                card.Title.color = Color.white;
            }
            else if (mode == 3)
            {
                card.Title.color = card.Rig != null
                    ? PlayerActions.GetRigColor(card.Rig)
                    : Color.white;
            }
            // Theme mode is bound at creation, so the live theme repaints it.
        }

        private static void CascadingThemeFallbacks(
            CascadeTheme theme,
            OverlayStyle style,
            out Color shadow,
            out Color border,
            out Color background,
            out Color accent,
            out Color text,
            out Color secondary)
        {
            if (theme != null)
            {
                shadow = theme.ShadowColor;
                border = theme.Accent;
                border.a = style.BorderAlpha;
                background = theme.ContentBg;
                background.a = style.BackgroundAlpha;
                accent = theme.Accent;
                text = theme.TextPrimary;
                secondary = theme.TextSecondary;
                return;
            }

            // Fallback only used before the menu has built its first theme.
            shadow = new Color(0f, 0f, 0f, 0.55f);
            border = new Color(0.55f, 0.75f, 1f, style.BorderAlpha);
            background = new Color(0.04f, 0.05f, 0.07f, style.BackgroundAlpha);
            accent = new Color(0.36f, 0.68f, 1f, 1f);
            text = Color.white;
            secondary = new Color(1f, 1f, 1f, 0.68f);
        }

        // ---------------------------------------------------------------
        // Shared materials: cards always render on top without taking clicks
        // ---------------------------------------------------------------
        private void EnsureMaterials()
        {
            if (_materialsReady || _materialsFailed) return;
            _materialsReady = true;

            try
            {
                Shader shader = Shader.Find("UI/Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    _materialsFailed = true;
                    Plugin.Log?.LogWarning("Cascade Player HUD: no usable shader found.");
                    return;
                }

                _imageMaterial = new Material(shader) { color = Color.white };
                ConfigureOverlay(_imageMaterial);

                if (CascadeUI.Font != null && CascadeUI.Font.material != null)
                {
                    _textMaterial = new Material(CascadeUI.Font.material);
                    ConfigureOverlay(_textMaterial);
                }
            }
            catch (Exception exception)
            {
                _materialsFailed = true;
                Plugin.Log?.LogWarning("Cascade Player HUD material setup failed: " + exception.Message);
            }
        }

        private Image ApplyImage(Image image)
        {
            if (image != null)
            {
                image.raycastTarget = false;
                if (_imageMaterial != null) image.material = _imageMaterial;
            }
            return image;
        }

        private void ApplyText(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.raycastTarget = false;
            if (_textMaterial != null) text.fontSharedMaterial = _textMaterial;
            try
            {
                // TMP exposes its renderer through the component, not a
                // property on this package's TextMeshProUGUI type.
                Renderer textRenderer = text.GetComponent<Renderer>();
                if (textRenderer != null)
                {
                    textRenderer.sortingOrder = 32760;
                    textRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    textRenderer.receiveShadows = false;
                }
            }
            catch (Exception)
            {
                // A TMP renderer may not exist until the first layout pass.
            }
        }

        private static void ConfigureOverlay(Material material)
        {
            if (material == null) return;
            material.renderQueue = 5000;
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)CompareFunction.Always);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }

        // ===============================================================
        // Room ping (reflection, UNVERIFIED name)
        // ===============================================================
        /// <summary>
        /// Photon exposes the local round-trip time, not a value per player.
        /// Every card therefore shows the room ping. The value is cached for a
        /// second so four text refreshes per second do not invoke reflection
        /// four times per player. UNVERIFIED: Photon.Pun.PhotonNetwork.GetPing.
        /// </summary>
        private static class PingProbe
        {
            private static bool _resolved;
            private static bool _failed;
            private static MethodInfo _getPing;
            private static float _nextRead;
            private static int _cached;

            internal static string Describe()
            {
                float now = Time.unscaledTime;
                if (now >= _nextRead)
                {
                    _nextRead = now + 1f;
                    _cached = Read();
                }

                return _cached > 0 ? _cached + " ms" : "n/a";
            }

            private static int Read()
            {
                Resolve();
                if (_failed || _getPing == null) return 0;

                try
                {
                    object value = _getPing.Invoke(null, null);
                    if (value is short) return (short)value;
                    if (value is int) return (int)value;
                }
                catch (Exception)
                {
                    // Photon can be torn down with the room.
                }
                return 0;
            }

            private static void Resolve()
            {
                if (_resolved) return;
                _resolved = true;

                try
                {
                    Type networkType = FindType("Photon.Pun.PhotonNetwork");
                    if (networkType == null)
                    {
                        _failed = true;
                        return;
                    }

                    _getPing = networkType.GetMethod("GetPing",
                        BindingFlags.Public | BindingFlags.Static,
                        null, Type.EmptyTypes, null);
                    if (_getPing == null)
                    {
                        _failed = true;
                        Plugin.Log?.LogInfo("Cascade Player HUD: Photon ping not available.");
                    }
                }
                catch (Exception exception)
                {
                    _failed = true;
                    Plugin.Log?.LogInfo("Cascade Player HUD: ping unavailable: " + exception.Message);
                }
            }

            private static Type FindType(string name)
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    try
                    {
                        Type type = assemblies[i].GetType(name, false);
                        if (type != null) return type;
                    }
                    catch (Exception)
                    {
                        // Some dynamic assemblies cannot be searched.
                    }
                }
                return null;
            }
        }

        // ===============================================================
        // Photon player custom properties (reflection, UNVERIFIED names)
        // ===============================================================
        /// <summary>
        /// Photon exposes player custom properties on Photon.Realtime.Player.
        /// Cascade deliberately does not reference Photon, so every member is
        /// found by name and a missing member only hides this one line.
        /// UNVERIFIED: "Photon.Pun.PhotonNetwork.PlayerList" and
        /// "Photon.Realtime.Player.CustomProperties".
        /// </summary>
        private static class PhotonProps
        {
            private static bool _resolved;
            private static bool _failed;
            private static PropertyInfo _playerListProperty;
            private static PropertyInfo _actorNumberProperty;
            private static PropertyInfo _customPropertiesProperty;

            internal static bool Fill(NetPlayer player, HashSet<string> allowed, bool all,
                int max, StringBuilder output)
            {
                if (player == null || output == null) return false;
                Resolve();
                if (_failed || _playerListProperty == null) return false;

                IEnumerable list;
                try
                {
                    list = _playerListProperty.GetValue(null, null) as IEnumerable;
                }
                catch (Exception)
                {
                    return false;
                }
                if (list == null) return false;

                try
                {
                    foreach (object entry in list)
                    {
                        if (entry == null) continue;
                        if (!MatchesActor(entry, player.ActorNumber)) continue;

                        IDictionary props = _customPropertiesProperty != null
                            ? _customPropertiesProperty.GetValue(entry, null) as IDictionary
                            : null;
                        if (props == null) return false;

                        int written = 0;
                        foreach (DictionaryEntry property in props)
                        {
                            string key = property.Key != null ? property.Key.ToString() : string.Empty;
                            if (string.IsNullOrEmpty(key)) continue;
                            if (!all && !allowed.Contains(key)) continue;

                            string value = property.Value != null ? property.Value.ToString() : string.Empty;
                            if (value.Length > 28) value = value.Substring(0, 25) + "...";
                            if (written > 0) output.Append("; ");
                            output.Append(key).Append('=').Append(value);
                            written++;
                            if (written >= max) break;
                        }
                        return written > 0;
                    }
                }
                catch (Exception)
                {
                    // A room can rewrite its properties while we are reading them.
                }

                return false;
            }

            private static bool MatchesActor(object photonPlayer, int actorNumber)
            {
                if (_actorNumberProperty == null) return false;
                try
                {
                    object value = _actorNumberProperty.GetValue(photonPlayer, null);
                    return value is int && (int)value == actorNumber;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private static void Resolve()
            {
                if (_resolved) return;
                _resolved = true;

                try
                {
                    Type networkType = FindType("Photon.Pun.PhotonNetwork");
                    Type playerType = FindType("Photon.Realtime.Player");
                    if (networkType == null || playerType == null)
                    {
                        _failed = true;
                        Plugin.Log?.LogInfo("Cascade Player HUD: Photon player properties not available.");
                        return;
                    }

                    _playerListProperty = networkType.GetProperty(
                        "PlayerList",
                        BindingFlags.Public | BindingFlags.Static);
                    _actorNumberProperty = playerType.GetProperty(
                        "ActorNumber",
                        BindingFlags.Public | BindingFlags.Instance);
                    _customPropertiesProperty = playerType.GetProperty(
                        "CustomProperties",
                        BindingFlags.Public | BindingFlags.Instance);
                    _failed = _playerListProperty == null || _actorNumberProperty == null;
                }
                catch (Exception exception)
                {
                    _failed = true;
                    Plugin.Log?.LogWarning("Cascade Player HUD: Photon properties unavailable: " + exception.Message);
                }
            }

            private static Type FindType(string name)
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    try
                    {
                        Type type = assemblies[i].GetType(name, false);
                        if (type != null) return type;
                    }
                    catch (Exception)
                    {
                        // Some dynamic assemblies cannot be searched.
                    }
                }
                return null;
            }
        }

        // ===============================================================
        // Tagged / infected state (reflection, UNVERIFIED names)
        // ===============================================================
        /// <summary>
        /// Gorilla Tag has no NetPlayer-wide "is tagged" field in the base API.
        /// The two common game modes expose different members, so this probe
        /// tries both by name and reports the state as unknown if neither is
        /// present. UNVERIFIED: GorillaTagManager.currentTaggedPlayer / IsTagged,
        /// GorillaInfectionManager.infectedPlayers.
        /// </summary>
        private static class TagProbe
        {
            private static bool _resolved;
            private static PropertyInfo _tagManagerInstance;
            private static MethodInfo _isTaggedMethod;
            private static FieldInfo _currentTaggedField;
            private static PropertyInfo _currentTaggedProperty;
            private static PropertyInfo _infectionInstance;
            private static FieldInfo _infectedPlayersField;
            private static PropertyInfo _infectedPlayersProperty;
            private static float _nextCheck;
            private static bool _cachedAvailable;

            internal static bool Available
            {
                get
                {
                    // Polled while Tagged only is on, so cache the manager
                    // lookup instead of reflecting once per card per frame.
                    float now = Time.unscaledTime;
                    if (now < _nextCheck) return _cachedAvailable;

                    _nextCheck = now + 1f;
                    Resolve();

                    bool available = false;
                    if (_tagManagerInstance != null)
                    {
                        try { available = _tagManagerInstance.GetValue(null, null) != null; }
                        catch (Exception) { }
                    }
                    if (!available && _infectionInstance != null)
                    {
                        try { available = _infectionInstance.GetValue(null, null) != null; }
                        catch (Exception) { }
                    }

                    _cachedAvailable = available;
                    return available;
                }
            }

            internal static bool TryIsTagged(NetPlayer player, out bool tagged)
            {
                tagged = false;
                if (player == null) return false;
                Resolve();

                if (_tagManagerInstance != null)
                {
                    object manager = null;
                    try { manager = _tagManagerInstance.GetValue(null, null); }
                    catch (Exception) { }

                    if (manager != null)
                    {
                        if (_isTaggedMethod != null)
                        {
                            try
                            {
                                object result = _isTaggedMethod.Invoke(manager, new object[] { player });
                                if (result is bool) { tagged = (bool)result; return true; }
                            }
                            catch (Exception) { }
                        }

                        object current = null;
                        try
                        {
                            if (_currentTaggedField != null) current = _currentTaggedField.GetValue(manager);
                            else if (_currentTaggedProperty != null) current = _currentTaggedProperty.GetValue(manager, null);
                        }
                        catch (Exception) { }

                        NetPlayer taggedPlayer = current as NetPlayer;
                        if (taggedPlayer != null)
                        {
                            tagged = taggedPlayer.UserId == player.UserId ||
                                     taggedPlayer.ActorNumber == player.ActorNumber;
                            return true;
                        }
                    }
                }

                if (_infectionInstance != null)
                {
                    object manager = null;
                    try { manager = _infectionInstance.GetValue(null, null); }
                    catch (Exception) { }

                    object collection = null;
                    try
                    {
                        if (_infectedPlayersField != null) collection = _infectedPlayersField.GetValue(manager);
                        else if (_infectedPlayersProperty != null) collection = _infectedPlayersProperty.GetValue(manager, null);
                    }
                    catch (Exception) { }

                    IEnumerable list = collection as IEnumerable;
                    if (list != null)
                    {
                        try
                        {
                            foreach (object item in list)
                            {
                                NetPlayer infected = item as NetPlayer;
                                if (infected == null) continue;
                                if (infected.UserId == player.UserId ||
                                    infected.ActorNumber == player.ActorNumber)
                                {
                                    tagged = true;
                                    return true;
                                }
                            }
                            return false;
                        }
                        catch (Exception) { }
                    }
                }

                return false;
            }

            private static void Resolve()
            {
                if (_resolved) return;
                _resolved = true;

                try
                {
                    Assembly assembly = typeof(GorillaTagger).Assembly;

                    Type tagType = assembly.GetType("GorillaTagManager");
                    if (tagType != null)
                    {
                        _tagManagerInstance = tagType.GetProperty("instance",
                            BindingFlags.Public | BindingFlags.Static) ??
                            tagType.GetProperty("Instance",
                                BindingFlags.Public | BindingFlags.Static);

                        _isTaggedMethod = tagType.GetMethod("IsTagged",
                            BindingFlags.Public | BindingFlags.Instance,
                            null, new[] { typeof(NetPlayer) }, null);

                        _currentTaggedField = tagType.GetField("currentTaggedPlayer",
                            BindingFlags.Public | BindingFlags.Instance);
                        _currentTaggedProperty = tagType.GetProperty("currentTaggedPlayer",
                            BindingFlags.Public | BindingFlags.Instance);
                    }

                    Type infectionType = assembly.GetType("GorillaInfectionManager");
                    if (infectionType != null)
                    {
                        _infectionInstance = infectionType.GetProperty("instance",
                            BindingFlags.Public | BindingFlags.Static) ??
                            infectionType.GetProperty("Instance",
                                BindingFlags.Public | BindingFlags.Static);
                        _infectedPlayersField = infectionType.GetField("infectedPlayers",
                            BindingFlags.Public | BindingFlags.Instance);
                        _infectedPlayersProperty = infectionType.GetProperty("infectedPlayers",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogInfo("Cascade Player HUD: tagged state unavailable: " + exception.Message);
                }
            }
        }
    }
}
