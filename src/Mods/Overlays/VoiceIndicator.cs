using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// "Talking players" panel that follows the view and lists only the players
    /// who are speaking right now.
    ///
    /// Each row is a speaker glyph plus a rounded box tinted with that player's
    /// gorilla color, so a glance tells you who is talking without reading.
    /// The panel is built from pooled rows, has no graphic raycaster and every
    /// graphic has raycastTarget = false, so it can never take a click.
    ///
    /// Voice is read from verified game members:
    /// - <see cref="VRRig.SpeakingLoudness"/> (public float property), and
    /// - <see cref="GorillaSpeakerLoudness.IsSpeaking"/> / SmoothedLoudness,
    ///   which is a public component on every rig.
    ///
    /// Disabling the mod destroys the canvas, so an off mod costs nothing.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class VoiceIndicator : Mod
    {
        // Canvas is authored at 260xN UI units, then scaled to ~13 cm wide.
        private const float CanvasWidth = 260f;
        private const float CanvasScale = 0.0005f;
        private const float Padding = 3f;
        private const float RowHeight = 26f;
        private const float RowGap = 1f;
        private const float IconSize = 15f;
        private const float IconGap = 5f;
        private const float BoxHeight = 22f;
        private const float RowWidth = CanvasWidth - Padding * 2f;
        private const float BoxX = Padding + IconSize + IconGap;
        private const float BoxWidth = CanvasWidth - BoxX - Padding;
        private const float FadeSpeed = 12f;
        private const float RosterInterval = 0.3f;

        // Right center is index 0 so installs that still have the old default
        // saved (Top left) move to the right edge with this update.
        private const int PositionRightCenter = 0;
        private const int PositionTopLeft = 1;
        private const int PositionTopRight = 2;
        private const int PositionBottomLeft = 3;
        private const int PositionBottomRight = 4;
        private const int PositionTopCenter = 5;

        private static readonly string[] PositionNames =
        {
            "Right center",
            "Top left",
            "Top right",
            "Bottom left",
            "Bottom right",
            "Top center"
        };

        private static readonly string[] NameColorNames =
        {
            "Auto on box",
            "White",
            "Dark",
            "Rig color"
        };

        private static readonly Comparison<SpeakerEntry> LoudestFirst = CompareByLevel;

        private sealed class SpeakerEntry
        {
            public VRRig Rig;
            public GorillaSpeakerLoudness Speaker;
            public bool IsLocal;
            public bool Present;
            public string Name;
            public Color Color = Color.white;
            public float Level;
            public float LastHeard = -100f;
            public float NextSpeakerCheck;
        }

        private sealed class Row
        {
            public GameObject Root;
            public CanvasGroup Group;
            public Image Glyph;
            public Image Box;
            public TextMeshProUGUI Name;
            public Color LastBox;
            public Color LastText;
            public string LastName;
        }

        // ---------------------------------------------------------------
        // Settings (persisted by ModRegistry through ModSetting)
        // ---------------------------------------------------------------
        private int _position = PositionRightCenter;
        private int _nameColor;
        private float _size = 1f;
        private float _opacity = 0.92f;
        private float _sensitivity = 0.08f;
        private float _holdTime = 0.35f;
        private float _maxDistance = 60f;
        private int _maxRows = 5;
        private bool _includeSelf = true;
        private bool _showIcon = true;

        // ---------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------
        private readonly Dictionary<string, SpeakerEntry> _entries =
            new Dictionary<string, SpeakerEntry>(StringComparer.Ordinal);
        private readonly List<SpeakerEntry> _visible = new List<SpeakerEntry>(8);
        private readonly List<Row> _rows = new List<Row>(8);
        private readonly List<string> _dead = new List<string>(8);
        private readonly List<NetPlayer> _players = new List<NetPlayer>(16);

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private float _nextRoster;
        private bool _layoutDirty = true;
        private int _builtRows = -1;

        public VoiceIndicator()
        {
            Name = "Talking Players";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("position", "Position",
                "Where the talking panel sits on screen",
                PositionNames,
                () => _position,
                value =>
                {
                    if (_position == value) return;
                    _position = value;
                    _layoutDirty = true;
                }));

            AddSetting(ModSetting.Dropdown("namecolor", "Name color",
                "Auto keeps the name readable on the player-colored box; Rig color uses a dark box",
                NameColorNames,
                () => _nameColor,
                value => _nameColor = value));

            AddSetting(ModSetting.Slider("size", "Size", "Panel scale", 0.6f, 1.6f,
                () => _size, value => _size = value, value => value.ToString("0.00") + "x"));
            AddSetting(ModSetting.Slider("opacity", "Opacity", "Box and text transparency", 0.2f, 1f,
                () => _opacity, value => _opacity = value, value => (value * 100f).ToString("0") + "%"));
            AddSetting(ModSetting.Slider("sensitivity", "Sensitivity",
                "Lower values catch quiet speech, higher values ignore background noise",
                0.02f, 0.5f,
                () => _sensitivity, value => _sensitivity = value, value => value.ToString("0.00")));
            AddSetting(ModSetting.Slider("hold", "Linger",
                "How long a name stays after the player stops talking",
                0f, 1.2f,
                () => _holdTime, value => _holdTime = value, value => value.ToString("0.00") + " s"));
            AddSetting(ModSetting.Slider("maxdistance", "Maximum distance",
                "Players further away than this are ignored",
                5f, 80f,
                () => _maxDistance, value => _maxDistance = value, value => value.ToString("0") + " m"));
            AddSetting(ModSetting.Stepper("rows", "Rows",
                "How many talking players can be listed at once",
                1, 8,
                () => _maxRows,
                value =>
                {
                    if (_maxRows == value) return;
                    _maxRows = value;
                    _layoutDirty = true;
                }));
            AddSetting(ModSetting.Toggle("self", "Include me",
                "Also list your own microphone, which makes testing easy",
                () => _includeSelf, value => _includeSelf = value));
            AddSetting(ModSetting.Toggle("icon", "Speaker icon",
                "Show the speaker glyph before every name",
                () => _showIcon, value => _showIcon = value));
        }

        // ---------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------
        public override void OnEnable()
        {
            _entries.Clear();
            _visible.Clear();
            _nextRoster = 0f;
            _layoutDirty = true;
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            CreateHud();
            ProfilerStats.Register(ProfilerStats.VoiceIndicatorKey, "Talking Players",
                "Speaker list with player-colored name boxes");
            Plugin.Log?.LogInfo("Cascade Talking Players active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            ProfilerStats.Record(ProfilerStats.VoiceIndicatorKey, 0.0, false);
            Plugin.Log?.LogInfo("Cascade Talking Players stopped.");
        }

        public override void LateUpdate()
        {
            long start = ProfilerStats.Timestamp;
            float now = Time.unscaledTime;

            if (_canvas == null || _layoutDirty) CreateHud();

            float scale = CanvasScale * _size;
            if (!Mathf.Approximately(_canvasRect.localScale.x, scale))
            {
                _canvasRect.localScale = Vector3.one * scale;
            }

            Camera camera = _camera != null ? _camera : CascadeMenuBehaviour.ResolveViewCamera();
            if (camera == null)
            {
                ProfilerStats.Record(ProfilerStats.VoiceIndicatorKey, ProfilerStats.ElapsedMs(start), false);
                return;
            }
            _camera = camera;

            if (_canvas != null && _canvas.transform.parent != camera.transform)
            {
                _canvas.transform.SetParent(camera.transform, false);
                _canvas.worldCamera = camera;
                ApplyPlacement();
            }

            if (now >= _nextRoster)
            {
                RefreshRoster();
                _nextRoster = now + RosterInterval;
            }

            CollectVisible(now, camera);

            if (_visible.Count == 0)
            {
                if (_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
                ProfilerStats.Record(ProfilerStats.VoiceIndicatorKey, ProfilerStats.ElapsedMs(start), false);
                return;
            }

            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
            UpdateRows(now);

            ProfilerStats.Record(ProfilerStats.VoiceIndicatorKey, ProfilerStats.ElapsedMs(start), true);
        }

        // ---------------------------------------------------------------
        // Roster and loudness
        // ---------------------------------------------------------------
        private void RefreshRoster()
        {
            _players.Clear();
            _players.AddRange(PlayerActions.GetPlayers());

            if (_includeSelf)
            {
                NetPlayer local = PlayerActions.LocalPlayer();
                if (local != null) _players.Add(local);
            }

            foreach (KeyValuePair<string, SpeakerEntry> pair in _entries)
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

                SpeakerEntry entry;
                if (!_entries.TryGetValue(id, out entry))
                {
                    entry = new SpeakerEntry();
                    _entries[id] = entry;
                }

                entry.Present = true;
                entry.IsLocal = player.IsLocal;

                VRRig rig = entry.IsLocal ? VRRig.LocalRig : PlayerActions.FindRigObject(player);
                if (rig != entry.Rig)
                {
                    entry.Rig = rig;
                    entry.Speaker = null;
                    entry.NextSpeakerCheck = 0f;
                }

                string name = entry.IsLocal && VRRig.LocalRig != null &&
                              !string.IsNullOrEmpty(VRRig.LocalRig.playerNameVisible)
                    ? VRRig.LocalRig.playerNameVisible
                    : PlayerActions.SafeName(player);
                entry.Name = name;
                entry.Color = PlayerActions.GetRigColor(rig);
            }

            _dead.Clear();
            foreach (KeyValuePair<string, SpeakerEntry> pair in _entries)
            {
                if (!pair.Value.Present) _dead.Add(pair.Key);
            }
            for (int i = 0; i < _dead.Count; i++)
            {
                _entries.Remove(_dead[i]);
            }
        }

        private void CollectVisible(float now, Camera camera)
        {
            _visible.Clear();
            Vector3 eye = camera.transform.position;

            foreach (KeyValuePair<string, SpeakerEntry> pair in _entries)
            {
                SpeakerEntry entry = pair.Value;
                if (!entry.Present) continue;

                VRRig rig = entry.Rig;
                if (rig == null || rig.gameObject == null || !rig.gameObject.activeInHierarchy) continue;

                if (entry.Speaker == null && now >= entry.NextSpeakerCheck)
                {
                    entry.NextSpeakerCheck = now + 2f;
                    entry.Speaker = rig.GetComponentInChildren<GorillaSpeakerLoudness>(true);
                }

                float level = 0f;

                try
                {
                    level = rig.SpeakingLoudness;
                }
                catch (Exception)
                {
                    // A rig can be torn down between the check and the read.
                }

                GorillaSpeakerLoudness speaker = entry.Speaker;
                if (speaker != null)
                {
                    float smooth = speaker.SmoothedLoudness;
                    if (smooth > level) level = smooth;
                    if (speaker.IsSpeaking && level < _sensitivity)
                    {
                        // IsSpeaking can be true before the smoothed value rises.
                        level = _sensitivity;
                    }
                }

                // Refresh the hold timer only while the player is audible and in
                // range. Everything else is left to the linger window, so names
                // do not vanish between syllables.
                if (level >= _sensitivity)
                {
                    Vector3 head = GetHead(rig);
                    if (Vector3.Distance(eye, head) <= _maxDistance)
                    {
                        // The local rig only reports our mic on some builds,
                        // which is why listing yourself is opt-in.
                        if (!entry.IsLocal || _includeSelf)
                        {
                            entry.Level = level;
                            entry.LastHeard = now;
                        }
                    }
                }

                if (now - entry.LastHeard <= _holdTime)
                {
                    _visible.Add(entry);
                }
            }

            if (_visible.Count > 1) _visible.Sort(LoudestFirst);
            if (_visible.Count > _maxRows) _visible.RemoveRange(_maxRows, _visible.Count - _maxRows);
        }

        private static int CompareByLevel(SpeakerEntry a, SpeakerEntry b)
        {
            int byLevel = b.Level.CompareTo(a.Level);
            if (byLevel != 0) return byLevel;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static Vector3 GetHead(VRRig rig)
        {
            if (rig == null) return Vector3.zero;
            if (rig.headConstraint != null) return rig.headConstraint.position;
            float scale = rig.scaleFactor > 0.01f ? rig.scaleFactor : 1f;
            return rig.transform.position + Vector3.up * (1.5f * scale);
        }

        // ---------------------------------------------------------------
        // Canvas construction
        // ---------------------------------------------------------------
        private void CreateHud()
        {
            DestroyHud();

            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            _builtRows = Mathf.Max(1, _maxRows);

            GameObject root = new GameObject("CascadeTalkingPlayers",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));

            Transform parent = _camera != null
                ? _camera.transform
                : (Plugin.Root != null ? Plugin.Root.transform : null);
            root.transform.SetParent(parent, false);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 210;
            _canvas.worldCamera = _camera;

            _canvasRect = (RectTransform)root.transform;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, PanelHeight(_builtRows));
            _canvasRect.localScale = Vector3.one * (CanvasScale * _size);
            _canvasRect.localRotation = Quaternion.identity;
            ApplyPlacement();

            // No backing panel: the colored name chips are the only graphics,
            // so the list floats free on the right side of the view.
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            _rows.Clear();
            for (int i = 0; i < _builtRows; i++)
            {
                _rows.Add(CreateRow(i));
            }

            _layoutDirty = false;
            root.SetActive(false);
        }

        private Row CreateRow(int index)
        {
            var row = new Row();
            row.LastBox = new Color(0f, 0f, 0f, 0f);
            row.LastText = new Color(0f, 0f, 0f, 0f);

            RectTransform rowRect = CascadeUI.TopLeft("Row " + index, _canvasRect,
                Padding, Padding + index * (RowHeight + RowGap), RowWidth, RowHeight);

            GameObject holder = new GameObject("Fade", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(rowRect, false);
            CascadeUI.Stretch(holderRect);
            row.Group = holder.GetComponent<CanvasGroup>();
            row.Group.alpha = 0f;
            row.Group.blocksRaycasts = false;
            row.Group.interactable = false;

            RectTransform iconRect = CascadeUI.TopLeft("Speaker", holderRect,
                0f, (RowHeight - IconSize) * 0.5f, IconSize, IconSize);
            row.Glyph = CascadeUI.Image(iconRect, CascadeIcons.Get(CascadeIcon.Speaker),
                Color.white, Image.Type.Simple);

            RectTransform boxRect = CascadeUI.TopLeft("Name box", holderRect,
                IconSize + IconGap, (RowHeight - BoxHeight) * 0.5f, BoxWidth, BoxHeight);
            row.Box = CascadeUI.Panel(boxRect, Color.white, 6);

            RectTransform nameRect = CascadeUI.TopLeft("Name", boxRect,
                8f, 0f, BoxWidth - 16f, BoxHeight);
            row.Name = CascadeUI.Text(nameRect, string.Empty, 14f, Color.white,
                TextAlignmentOptions.MidlineLeft, true, false);

            row.Root = rowRect.gameObject;
            row.Root.SetActive(false);
            row.LastName = string.Empty;
            return row;
        }

        private void DestroyHud()
        {
            _rows.Clear();
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
                _canvasRect = null;
            }
        }

        private static float PanelHeight(int rows)
        {
            int count = Mathf.Max(1, rows);
            return Padding * 2f + count * RowHeight + (count - 1) * RowGap;
        }

        private void ApplyPlacement()
        {
            if (_canvasRect == null) return;

            Vector2 pivot;
            Vector3 position;

            switch (_position)
            {
                case PositionTopLeft:
                    pivot = new Vector2(0f, 1f);
                    position = new Vector3(-0.30f, 0.13f, 0.6f);
                    break;
                case PositionTopRight:
                    pivot = new Vector2(1f, 1f);
                    position = new Vector3(0.30f, 0.13f, 0.6f);
                    break;
                case PositionBottomLeft:
                    pivot = new Vector2(0f, 0f);
                    position = new Vector3(-0.30f, -0.13f, 0.6f);
                    break;
                case PositionBottomRight:
                    pivot = new Vector2(1f, 0f);
                    position = new Vector3(0.30f, -0.13f, 0.6f);
                    break;
                case PositionTopCenter:
                    pivot = new Vector2(0.5f, 1f);
                    position = new Vector3(0f, 0.14f, 0.6f);
                    break;
                default:
                    pivot = new Vector2(1f, 0.5f);
                    position = new Vector3(0.30f, 0f, 0.6f);
                    break;
            }

            _canvasRect.pivot = pivot;
            _canvasRect.localPosition = position;
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, PanelHeight(_builtRows));
            _canvasRect.localScale = Vector3.one * (CanvasScale * _size);
        }

        // ---------------------------------------------------------------
        // Per-row update
        // ---------------------------------------------------------------
        private void UpdateRows(float now)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                SpeakerEntry entry = i < _visible.Count ? _visible[i] : null;

                if (entry == null)
                {
                    row.Group.alpha = Mathf.MoveTowards(row.Group.alpha, 0f,
                        Time.unscaledDeltaTime * FadeSpeed);
                    if (row.Group.alpha <= 0.01f && row.Root.activeSelf)
                    {
                        row.Root.SetActive(false);
                    }
                    continue;
                }

                if (!row.Root.activeSelf) row.Root.SetActive(true);
                row.Group.alpha = Mathf.MoveTowards(row.Group.alpha, 1f,
                    Time.unscaledDeltaTime * FadeSpeed);

                if (!string.Equals(row.LastName, entry.Name, StringComparison.Ordinal))
                {
                    row.LastName = entry.Name;
                    row.Name.text = entry.Name;
                }

                Color boxColor;
                Color textColor;
                ResolveColors(entry.Color, out boxColor, out textColor);

                if (row.LastBox != boxColor)
                {
                    row.LastBox = boxColor;
                    row.Box.color = boxColor;
                }
                if (row.LastText != textColor)
                {
                    row.LastText = textColor;
                    row.Name.color = textColor;
                }

                row.Glyph.enabled = _showIcon;
            }
        }

        private void ResolveColors(Color rig, out Color box, out Color text)
        {
            if (_nameColor == 3)
            {
                // Dark glass box with the name itself in the player's color.
                box = new Color(0.05f, 0.055f, 0.07f, 0.88f * _opacity);
                text = rig;
                text.a = _opacity;
                return;
            }

            box = rig;
            box.a = _opacity;

            switch (_nameColor)
            {
                case 1:
                    text = Color.white;
                    break;
                case 2:
                    text = new Color(0.05f, 0.05f, 0.07f, 1f);
                    break;
                default:
                    float luminance = 0.299f * rig.r + 0.587f * rig.g + 0.114f * rig.b;
                    text = luminance > 0.62f
                        ? new Color(0.05f, 0.05f, 0.07f, 1f)
                        : Color.white;
                    break;
            }

            text.a = Mathf.Max(0.55f, _opacity);
        }
    }
}
