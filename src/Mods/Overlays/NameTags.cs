using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Minimal display-only name labels above every remote gorilla rig.
    ///
    /// This is deliberately much smaller than <see cref="PlayerHud"/>: one
    /// rounded text plate per player, an optional platform line, and nothing
    /// else. It is a separate mod so a player can run names without the full
    /// information card, or the card without names.
    ///
    /// Performance contract:
    /// - One label object per player, created on join and destroyed on leave.
    /// - The roster is re-read twice a second; transforms update per frame.
    /// - The label string is only rebuilt when the name or platform changes.
    /// - Update does no LINQ, no FindObjectsOfType, no string concatenation
    ///   and no collection allocation.
    /// - Disabling destroys every label, so a disabled mod costs nothing.
    ///
    /// The plate has no GraphicRaycaster, every graphic has raycastTarget =
    /// false and the CanvasGroup blocks input, so no part of it is clickable.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class NameTags : Mod
    {
        private const string LabelName = "CascadeNameTag";
        private const float PixelScale = 0.0016f;
        private const float RosterInterval = 0.5f;
        private const float TextInterval = 1f;
        private const float FadeSpeed = 7f;

        private sealed class Tag
        {
            public string Id;
            public NetPlayer Player;
            public VRRig Rig;
            public bool IsLocal;
            public bool Present;
            public bool Visible;
            public bool PlatformShown;
            public string Name;
            public string Platform;
            public float NextRefresh;
            public float Distance;
            public float Width;

            public GameObject Root;
            public RectTransform RootRect;
            public RectTransform TitleRect;
            public CanvasGroup Group;
            public Image Shadow;
            public Image Border;
            public Image Background;
            public Image Accent;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Subtitle;
        }

        // ---------------------------------------------------------------
        // Settings (persisted by ModRegistry through ModSetting)
        // ---------------------------------------------------------------
        private bool _showPlatform;
        private bool _hideSelf = true;
        private bool _hideOffscreen = true;

        private int _style;
        private int _nameColor;
        private bool _styleDirty;

        private float _size = 1f;
        private float _maxDistance = 30f;
        private float _height = 0.55f;
        private float _opacity = 0.95f;

        // ---------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------
        private readonly Dictionary<string, Tag> _tags =
            new Dictionary<string, Tag>(StringComparer.Ordinal);
        private readonly List<string> _dead = new List<string>(8);
        private readonly List<NetPlayer> _players = new List<NetPlayer>(16);

        private Material _imageMaterial;
        private Material _textMaterial;
        private bool _materialsReady;
        private bool _materialsFailed;
        private Camera _camera;
        private float _nextRoster;

        public NameTags()
        {
            Name = "Name Tags";
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

            AddSetting(ModSetting.Slider("size", "Size", "Fixed label size; distance does not change it", 0.5f, 2f,
                () => _size, value => _size = value, value => value.ToString("0.00") + "x"));
            AddSetting(ModSetting.Slider("maxdistance", "Maximum distance", "Labels fade out past this range",
                5f, 80f, () => _maxDistance, value => _maxDistance = value, value => value.ToString("0") + " m"));
            AddSetting(ModSetting.Slider("height", "Height offset", "How far above the head the label sits",
                0.1f, 2f, () => _height, value => _height = value, value => value.ToString("0.00") + " m"));
            AddSetting(ModSetting.Slider("opacity", "Opacity", "Label transparency", 0.2f, 1f,
                () => _opacity, value => _opacity = value, value => (value * 100f).ToString("0") + "%"));
            AddSetting(ModSetting.Toggle("platform", "Platform line", "Show the player's report platform",
                () => _showPlatform, value => _showPlatform = value));
            AddSetting(ModSetting.Toggle("hideself", "Hide my own", "Never draw a tag above your own head",
                () => _hideSelf, value => _hideSelf = value));
            AddSetting(ModSetting.Toggle("hideoffscreen", "Hide off-screen", "Fade out labels behind you or outside the view",
                () => _hideOffscreen, value => _hideOffscreen = value));
        }

        // ---------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------
        public override void OnEnable()
        {
            EnsureMaterials();
            _tags.Clear();
            _nextRoster = 0f;
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            ProfilerStats.Register(ProfilerStats.NameTagsKey, "Name Tags",
                "Pooled name labels above every remote rig");
            Plugin.Log?.LogInfo("Cascade Name Tags active.");
        }

        public override void OnDisable()
        {
            DestroyAll();
            ProfilerStats.Record(ProfilerStats.NameTagsKey, 0.0, false);
            Plugin.Log?.LogInfo("Cascade Name Tags stopped; every label removed.");
        }

        public override void Update()
        {
            long start = ProfilerStats.Timestamp;
            float now = Time.unscaledTime;

            if (_camera == null) _camera = CascadeMenuBehaviour.ResolveViewCamera();

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
                ProfilerStats.Record(ProfilerStats.NameTagsKey, ProfilerStats.ElapsedMs(start), false);
                return;
            }

            Vector3 eye = camera.transform.position;

            foreach (KeyValuePair<string, Tag> pair in _tags)
            {
                Tag tag = pair.Value;
                if (tag == null || tag.Root == null) continue;
                if (!tag.Present) continue;

                if (!IsRigAlive(tag.Rig))
                {
                    tag.Rig = tag.Player != null ? PlayerActions.FindRigObject(tag.Player) : null;
                    if (tag.IsLocal) tag.Rig = VRRig.LocalRig;
                }
                if (!IsRigAlive(tag.Rig)) continue;

                Vector3 head = GetHead(tag.Rig);
                tag.Distance = Vector3.Distance(eye, head);

                bool inRange = tag.Distance <= _maxDistance;
                bool onScreen = !_hideOffscreen || IsOnScreen(camera, head);

                float targetAlpha = inRange && onScreen ? _opacity : 0f;

                float fade = Mathf.Clamp01((_maxDistance - tag.Distance) /
                                           Mathf.Max(0.5f, _maxDistance * 0.15f));
                targetAlpha *= fade;

                if (targetAlpha > 0.01f)
                {
                    if (!tag.Visible)
                    {
                        tag.Visible = true;
                        tag.Root.SetActive(true);
                    }

                    UpdateTransform(tag, camera, head);

                    if (now >= tag.NextRefresh)
                    {
                        RefreshText(tag);
                        tag.NextRefresh = now + TextInterval + ((tag.GetHashCode() & 3) * 0.05f);
                    }
                }
                else if (tag.Visible)
                {
                    tag.Visible = false;
                    tag.Root.SetActive(false);
                }

                tag.Group.alpha = Mathf.MoveTowards(tag.Group.alpha, targetAlpha,
                    Time.unscaledDeltaTime * FadeSpeed);
            }

            ProfilerStats.Record(ProfilerStats.NameTagsKey, ProfilerStats.ElapsedMs(start), true);
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

            foreach (KeyValuePair<string, Tag> pair in _tags)
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

                Tag tag;
                if (!_tags.TryGetValue(id, out tag))
                {
                    tag = CreateTag(id, player);
                    _tags[id] = tag;
                }

                tag.Player = player;
                tag.Present = true;
                tag.IsLocal = player.IsLocal;
                tag.Rig = tag.IsLocal ? VRRig.LocalRig : PlayerActions.FindRigObject(player);
            }

            _dead.Clear();
            foreach (KeyValuePair<string, Tag> pair in _tags)
            {
                if (!pair.Value.Present) _dead.Add(pair.Key);
            }
            for (int i = 0; i < _dead.Count; i++)
            {
                Tag tag;
                if (_tags.TryGetValue(_dead[i], out tag)) DestroyTag(tag);
                _tags.Remove(_dead[i]);
            }
        }

        private Tag CreateTag(string id, NetPlayer player)
        {
            var tag = new Tag();
            tag.Id = id;
            tag.Player = player;
            tag.IsLocal = player != null && player.IsLocal;
            tag.NextRefresh = Time.unscaledTime + ((_tags.Count & 3) * 0.05f);

            OverlayStyle style = OverlayStyles.Get(_style);
            float width = style.TagWidth;

            GameObject root = new GameObject(LabelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            if (Plugin.Root != null) root.transform.SetParent(Plugin.Root.transform, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32750;
            canvas.worldCamera = _camera;

            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, 28f);
            rect.localScale = Vector3.one * PixelScale;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            CascadeTheme theme = Theme;
            CascadingThemeFallbacks(theme, style, out Color shadowColor, out Color border,
                out Color background, out Color accent, out Color text, out Color secondary);

            bool themeName = UsesThemeName(style);
            Color nameColor = themeName ? text : Color.white;

            RectTransform shadowRect = CascadeUI.Rect("Shadow", rect);
            CascadeUI.Stretch(shadowRect, -3f, -3f, 3f, -5f);
            tag.Shadow = ApplyImage(CascadeUI.Panel(shadowRect, shadowColor, style.CornerRadius + 2));
            CascadeTheme.Bind(tag.Shadow, CascadeRole.ShadowColor);
            tag.Shadow.gameObject.SetActive(style.ShowShadow);

            RectTransform borderRect = CascadeUI.Rect("Border", rect);
            CascadeUI.Stretch(borderRect);
            tag.Border = ApplyImage(CascadeUI.Panel(borderRect, border, style.CornerRadius + 1));
            CascadeTheme.Bind(tag.Border, CascadeRole.Accent, style.BorderAlpha);
            tag.Border.gameObject.SetActive(style.ShowBorder);

            RectTransform backgroundRect = CascadeUI.Rect("Background", rect);
            CascadeUI.Stretch(backgroundRect, 1.5f, 1.5f, 1.5f, 1.5f);
            tag.Background = ApplyImage(CascadeUI.Panel(backgroundRect, background, style.CornerRadius));
            CascadeTheme.Bind(tag.Background, CascadeRole.ContentBg, style.BackgroundAlpha);
            tag.Background.gameObject.SetActive(style.ShowBackground);

            RectTransform accentRect = CascadeUI.Rect("Accent", rect);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(1f, 0f);
            accentRect.pivot = new Vector2(0.5f, 0f);
            accentRect.offsetMin = new Vector2(10f, 2f);
            accentRect.offsetMax = new Vector2(-10f, 5f);
            tag.Accent = ApplyImage(CascadeUI.Panel(accentRect, accent, 1));
            CascadeTheme.Bind(tag.Accent, CascadeRole.Accent);
            tag.Accent.gameObject.SetActive(style.ShowAccent);

            RectTransform titleRect = CascadeUI.TopLeft("Title", rect, 10f, 4f, width - 20f, 20f);
            tag.Title = CascadeUI.Text(titleRect, string.Empty, style.NameSize, nameColor,
                style.CenterName ? TextAlignmentOptions.Center : TextAlignmentOptions.Left,
                style.BoldName, false);
            ApplyText(tag.Title);
            if (themeName) CascadeTheme.Bind(tag.Title, CascadeRole.TextPrimary);

            TextAlignmentOptions subtitleAlignment = style.CenterName
                ? TextAlignmentOptions.Center
                : TextAlignmentOptions.Left;
            RectTransform subtitleRect = CascadeUI.TopLeft("Platform", rect, 10f, 24f, width - 20f, 14f);
            tag.Subtitle = CascadeUI.Text(subtitleRect, string.Empty, 11f, secondary,
                subtitleAlignment, false, false);
            ApplyText(tag.Subtitle);
            CascadeTheme.Bind(tag.Subtitle, CascadeRole.TextSecondary);
            tag.Subtitle.gameObject.SetActive(false);

            tag.Width = width;
            tag.Root = root;
            tag.RootRect = rect;
            tag.TitleRect = titleRect;
            tag.Group = group;
            tag.Visible = true;

            return tag;
        }

        private void DestroyTag(Tag tag)
        {
            if (tag == null) return;
            if (tag.Root != null) UnityEngine.Object.Destroy(tag.Root);
            tag.Root = null;
            tag.RootRect = null;
            tag.TitleRect = null;
            tag.Group = null;
            tag.Title = null;
            tag.Subtitle = null;
            tag.Shadow = null;
            tag.Border = null;
            tag.Background = null;
            tag.Accent = null;
        }

        private void DestroyAll()
        {
            foreach (KeyValuePair<string, Tag> pair in _tags)
            {
                DestroyTag(pair.Value);
            }
            _tags.Clear();
        }

        // ---------------------------------------------------------------
        // Per-label update
        // ---------------------------------------------------------------
        private void UpdateTransform(Tag tag, Camera camera, Vector3 head)
        {
            float rigScale = tag.Rig != null && tag.Rig.scaleFactor > 0.01f ? tag.Rig.scaleFactor : 1f;
            Vector3 position = head + Vector3.up * (_height * rigScale);
            tag.Root.transform.position = position;

            Vector3 look = position - camera.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
            {
                tag.Root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            tag.Root.transform.localScale = Vector3.one * (PixelScale * _size);
        }

        private void RefreshText(Tag tag)
        {
            NetPlayer player = tag.Player;
            if (player == null) return;

            OverlayStyle style = OverlayStyles.Get(_style);

            string name = tag.IsLocal && VRRig.LocalRig != null &&
                          !string.IsNullOrEmpty(VRRig.LocalRig.playerNameVisible)
                ? VRRig.LocalRig.playerNameVisible
                : PlayerActions.SafeName(player);

            string platform = _showPlatform ? PlayerActions.SafePlatform(player) : string.Empty;

            bool nameChanged = !string.Equals(name, tag.Name, StringComparison.Ordinal);
            bool platformChanged = !string.Equals(platform, tag.Platform, StringComparison.Ordinal);
            bool layoutChanged = _showPlatform != tag.PlatformShown;

            if (nameChanged)
            {
                tag.Name = name;
                string displayName = style.UppercaseName ? name.ToUpperInvariant() : name;
                tag.Title.text = displayName;
            }

            if (platformChanged)
            {
                tag.Platform = platform;
                tag.Subtitle.text = style.UppercaseName ? platform.ToUpperInvariant() : platform;
            }

            if (layoutChanged)
            {
                tag.PlatformShown = _showPlatform;
                tag.Subtitle.gameObject.SetActive(_showPlatform);
                tag.RootRect.sizeDelta = new Vector2(tag.Width, _showPlatform ? 42f : 28f);
            }

            ApplyNameColor(tag, style);
        }

        // ---------------------------------------------------------------
        // Data helpers
        // ---------------------------------------------------------------
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

        private void ApplyNameColor(Tag tag, OverlayStyle style)
        {
            int mode = _nameColor;
            if (mode == 0) mode = style.WhiteName ? 2 : 1;

            if (mode == 2)
            {
                tag.Title.color = Color.white;
            }
            else if (mode == 3)
            {
                tag.Title.color = tag.Rig != null
                    ? PlayerActions.GetRigColor(tag.Rig)
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
        // Shared materials: labels always render on top without taking clicks
        // ---------------------------------------------------------------
        private void EnsureMaterials()
        {
            if (_materialsReady || _materialsFailed) return;
            _materialsReady = true;

            try
            {
                Shader shader = ShaderLookup.UiUnlit();
                if (shader == null)
                {
                    _materialsFailed = true;
                    Plugin.Log?.LogWarning("Cascade Name Tags: no usable shader found.");
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
                Plugin.Log?.LogWarning("Cascade Name Tags material setup failed: " + exception.Message);
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
                    textRenderer.sortingOrder = 32750;
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
    }
}
