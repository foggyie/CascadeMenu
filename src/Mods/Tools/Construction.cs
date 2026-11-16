using System.Collections.Generic;
using CascadeMenu.UI;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Local-only physics construction kit.
    ///
    /// Build mode places rigidbody bricks, planks, beams, logs and balls in
    /// front of the selected hand. New pieces can weld themselves to the
    /// nearest existing piece, or the Weld tool can join any two pieces by
    /// hand. The demolition gun fires a small physics projectile that explodes
    /// on impact, while Explosion mode detonates directly at the aim point.
    ///
    /// Everything is spawned under one root owned by this mod, so disabling the
    /// effect destroys every piece, joint, preview, projectile and burst in one
    /// pass. Pieces are never sent over the network.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class Construction : Mod
    {
        private const string RootName = "CascadeConstruction";
        private const int MaxProjectiles = 8;

        private enum HandSide
        {
            Right = 0,
            Left = 1
        }

        private enum Tool
        {
            Build = 0,
            Weld = 1,
            Gun = 2,
            Explode = 3
        }

        private struct PieceSpec
        {
            public PrimitiveType Primitive;
            public Vector3 Scale;
            public float Mass;
            public Color Color;
            public bool SideMount;

            /// <summary>
            /// A full cube that lives in its own cubic cell, so blocks tile
            /// seamlessly instead of overlapping or leaving half-block gaps.
            /// </summary>
            public bool IsBlock;
        }

        private static readonly PieceSpec[] Specs =
        {
            // Block - a full cube that snaps into its own world-aligned cell
            new PieceSpec
            {
                Primitive = PrimitiveType.Cube,
                Scale = new Vector3(0.5f, 0.5f, 0.5f),
                Mass = 6f,
                Color = new Color(0.74f, 0.71f, 0.68f, 1f),
                IsBlock = true
            },
            // Brick
            new PieceSpec
            {
                Primitive = PrimitiveType.Cube,
                Scale = new Vector3(0.46f, 0.24f, 0.26f),
                Mass = 7f,
                Color = new Color(0.74f, 0.32f, 0.22f, 1f)
            },
            // Plank
            new PieceSpec
            {
                Primitive = PrimitiveType.Cube,
                Scale = new Vector3(0.9f, 0.1f, 0.34f),
                Mass = 8f,
                Color = new Color(0.62f, 0.43f, 0.27f, 1f)
            },
            // Beam
            new PieceSpec
            {
                Primitive = PrimitiveType.Cube,
                Scale = new Vector3(1.4f, 0.18f, 0.18f),
                Mass = 14f,
                Color = new Color(0.52f, 0.56f, 0.62f, 1f)
            },
            // Log
            new PieceSpec
            {
                Primitive = PrimitiveType.Cylinder,
                Scale = new Vector3(0.32f, 0.62f, 0.32f),
                Mass = 11f,
                Color = new Color(0.47f, 0.3f, 0.18f, 1f),
                SideMount = true
            },
            // Ball
            new PieceSpec
            {
                Primitive = PrimitiveType.Sphere,
                Scale = new Vector3(0.38f, 0.38f, 0.38f),
                Mass = 3.5f,
                Color = new Color(0.24f, 0.65f, 0.95f, 1f)
            }
        };

        private static readonly string[] ToolNames =
        {
            "Build",
            "Weld",
            "Demolition gun",
            "Explosion"
        };

        private static readonly string[] PieceNames =
        {
            "Block",
            "Brick",
            "Plank",
            "Beam",
            "Log",
            "Ball"
        };

        private static readonly string[] ColorNames =
        {
            "Material",
            "Theme",
            "Neon",
            "Pastel",
            "Concrete"
        };

        private static readonly string[] SnapNames =
        {
            "Off",
            "0.25 m",
            "0.5 m",
            "1 m"
        };

        private static readonly float[] SnapSteps =
        {
            0f,
            0.25f,
            0.5f,
            1f
        };

        private static readonly RaycastHit[] RayHits = new RaycastHit[64];
        private static readonly Collider[] OverlapHits = new Collider[128];
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // ---------------------------------------------------------------
        // Settings (persisted by ModRegistry through ModSetting)
        // ---------------------------------------------------------------
        private int _tool;
        private int _piece;
        private float _size = 1f;
        private int _colorStyle;
        private int _snap = 2;
        private int _rotation;
        private bool _autoWeld = true;
        private bool _fixedPieces = true;
        private bool _textured = true;
        private float _weldRadius = 0.4f;
        private float _weldStrength = 3500f;
        private float _weldRange = 4f;
        private float _explosionRadius = 4f;
        private float _explosionForce = 45f;
        private float _destroyRadius = 2f;
        private int _maxPieces = 150;
        private int _grip;
        private bool _requireGrip = true;
        private int _pcGrip;
        private string _pcGripKey = "LeftAlt";
        private bool _showBeam = true;
        private float _projectileSpeed = 26f;
        private float _gunCooldown = 0.35f;
        private float _range = 12f;

        // ---------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------
        private readonly List<ConstructionPiece> _pieces = new List<ConstructionPiece>(150);
        private readonly List<ConstructionLink> _links = new List<ConstructionLink>(192);
        private readonly List<GameObject> _projectiles = new List<GameObject>(MaxProjectiles);
        private readonly List<ConstructionPiece> _destroyBuffer = new List<ConstructionPiece>(64);

        private GameObject _root;
        private GameObject _preview;
        private LineRenderer _beam;
        private LineRenderer _ring;
        private Material _beamMaterial;
        private Material _ghostMaterial;
        private Material _flatMaterial;
        private Material _projectileMaterial;
        private readonly Material[] _pieceMaterials = new Material[Specs.Length];
        private readonly Texture2D[] _pieceTextures = new Texture2D[Specs.Length];

        private ConstructionPiece _selected;
        private ConstructionPiece _hover;
        private ConstructionPiece _aimPiece;

        private bool _ready;
        private bool _hasSurface;
        private bool _placementValid;
        private bool _previewDirty = true;
        private bool _colorsDirty = true;
        private bool _highlightDirty = true;
        private Vector3 _aimPoint;
        private Vector3 _aimNormal = Vector3.up;
        private Vector3 _placementPosition;
        private Quaternion _placementRotation = Quaternion.identity;
        private int _lastThemeVersion = int.MinValue;
        private float _lastShotTime = -99f;
        private float _nextLinkPrune;
        private LayerMask _mask = Physics.DefaultRaycastLayers;

        public Construction()
        {
            Name = "Construction";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("tool", "Tool",
                "Build places pieces, Weld joins two pieces, the gun shoots explosive rounds, Explosion detonates at the aim point",
                ToolNames,
                () => _tool,
                value =>
                {
                    _tool = Mathf.Clamp(value, 0, ToolNames.Length - 1);
                    _previewDirty = true;
                }));

            AddSetting(ModSetting.Dropdown("piece", "Piece",
                "What the build tool places",
                PieceNames,
                () => _piece,
                value =>
                {
                    _piece = Mathf.Clamp(value, 0, PieceNames.Length - 1);
                    _previewDirty = true;
                }));

            AddSetting(ModSetting.Slider("size", "Piece size",
                "Overall size of a placed piece",
                0.5f, 2.5f,
                () => _size,
                value =>
                {
                    _size = value;
                    _previewDirty = true;
                },
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Dropdown("color", "Color style",
                "Material colors, theme accent, neon, pastel or concrete",
                ColorNames,
                () => _colorStyle,
                value =>
                {
                    _colorStyle = Mathf.Clamp(value, 0, ColorNames.Length - 1);
                    _colorsDirty = true;
                    _previewDirty = true;
                }));

            AddSetting(ModSetting.Toggle("textures", "Textures",
                "Give bricks, planks, beams, logs and balls their own block texture instead of flat color",
                () => _textured,
                value =>
                {
                    _textured = value;
                    RefreshPieceMaterials();
                }));

            AddSetting(ModSetting.Dropdown("snap", "Grid snap",
                "Snap the aim point to a world grid, like block building (the Block piece always uses its own cube grid)",
                SnapNames,
                () => _snap,
                value => _snap = Mathf.Clamp(value, 0, SnapNames.Length - 1)));

            AddSetting(ModSetting.Stepper("rotation", "Rotation",
                "Yaw applied to a placed piece, in 45-degree steps (blocks use whole quarter turns)",
                0, 7,
                () => _rotation,
                value => _rotation = Mathf.Clamp(value, 0, 7),
                value => (value * 45) + " deg"));

            AddSetting(ModSetting.Toggle("fixed", "Static pieces",
                "Placed blocks stay where you put them like a real build; explosions still knock them loose",
                () => _fixedPieces,
                value => _fixedPieces = value));

            AddSetting(ModSetting.Toggle("autoweld", "Auto-weld",
                "Weld each new piece to the nearest piece inside the auto-weld radius",
                () => _autoWeld,
                value => _autoWeld = value));

            AddSetting(ModSetting.Slider("weldradius", "Auto-weld radius",
                "How close another piece must be before a new piece welds to it",
                0.05f, 1.5f,
                () => _weldRadius,
                value => _weldRadius = value,
                value => value.ToString("0.00") + " m"));

            AddSetting(ModSetting.Slider("weldrange", "Weld range",
                "Maximum distance between two pieces for a manual weld",
                0.5f, 8f,
                () => _weldRange,
                value => _weldRange = value,
                value => value.ToString("0.0") + " m"));

            AddSetting(ModSetting.Slider("weldstrength", "Weld strength",
                "Break force of a weld; higher values hold heavier builds together",
                200f, 20000f,
                () => _weldStrength,
                value => _weldStrength = value,
                value => value >= 1000f
                    ? (value / 1000f).ToString("0.0") + "k"
                    : value.ToString("0")));

            AddSetting(ModSetting.Slider("explosionradius", "Explosion radius",
                "Radius that receives explosion force",
                0.5f, 12f,
                () => _explosionRadius,
                value => _explosionRadius = value,
                value => value.ToString("0.0") + " m"));

            AddSetting(ModSetting.Slider("explosionforce", "Explosion force",
                "Impulse applied to pieces in the blast radius",
                1f, 150f,
                () => _explosionForce,
                value => _explosionForce = value,
                value => value.ToString("0")));

            AddSetting(ModSetting.Slider("destroyradius", "Destroy radius",
                "Pieces this close to the blast center are deleted",
                0.2f, 10f,
                () => _destroyRadius,
                value => _destroyRadius = value,
                value => value.ToString("0.0") + " m"));

            AddSetting(ModSetting.Stepper("maxpieces", "Max pieces",
                "Upper limit for one build; raise it before clearing a large build",
                20, 300,
                () => _maxPieces,
                value => _maxPieces = Mathf.Clamp(value, 20, 300)));

            AddSetting(ModSetting.Dropdown("grip", "Grip hand",
                "Hand that holds the construction tool",
                new[] { "Right hand", "Left hand", "Either hand" },
                () => _grip,
                value => _grip = Mathf.Clamp(value, 0, 2)));

            AddSetting(ModSetting.Toggle("requiregrip", "Require grip",
                "Only aim and use the tool while the grip is held",
                () => _requireGrip,
                value => _requireGrip = value));

            AddSetting(ModSetting.Dropdown("pcgrip", "Desktop grip",
                "Mouse button or key that stands in for grip on desktop",
                new[] { "Right mouse", "Left mouse", "Middle mouse", "Keyboard key" },
                () => _pcGrip,
                value => _pcGrip = Mathf.Clamp(value, 0, 3)));

            AddSetting(ModSetting.Text("pcgripkey", "Desktop grip key",
                "Input System key name used when Desktop grip is set to Keyboard key (for example LeftAlt)",
                () => _pcGripKey,
                value => _pcGripKey = value));

            AddSetting(ModSetting.Toggle("beam", "Aim beam",
                "Show the hand beam and target ring while the tool is held",
                () => _showBeam,
                value => _showBeam = value));

            AddSetting(ModSetting.Slider("range", "Aim range",
                "Maximum construction aim distance",
                2f, 30f,
                () => _range,
                value => _range = value,
                value => value.ToString("0.0") + " m"));

            AddSetting(ModSetting.Slider("projectilespeed", "Projectile speed",
                "Speed of the demolition gun projectile",
                8f, 50f,
                () => _projectileSpeed,
                value => _projectileSpeed = value,
                value => value.ToString("0") + " m/s"));

            AddSetting(ModSetting.Slider("guncooldown", "Gun cooldown",
                "Delay between demolition shots and explosions",
                0.1f, 2f,
                () => _gunCooldown,
                value => _gunCooldown = value,
                value => value.ToString("0.00") + " s"));
        }

        // ---------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------
        public override void OnEnable()
        {
            _mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("UI");

            _root = new GameObject(RootName);
            if (Plugin.Root != null)
            {
                _root.transform.SetParent(Plugin.Root.transform, false);
            }

            BuildMaterials();
            BuildVisuals();

            _pieces.Clear();
            _links.Clear();
            _projectiles.Clear();
            _destroyBuffer.Clear();
            _selected = null;
            _hover = null;
            _aimPiece = null;
            _ready = true;
            _hasSurface = false;
            _placementValid = false;
            _previewDirty = true;
            _colorsDirty = true;
            _highlightDirty = true;
            _lastThemeVersion = int.MinValue;
            _lastShotTime = -99f;
            _nextLinkPrune = 0f;

            ProfilerStats.Register(ProfilerStats.ConstructionKey, "Construction",
                "Aim ray, preview, welds, rigid pieces and demolition");

            Plugin.Log?.LogInfo("Cascade construction ready. Tool=" + ToolNames[_tool] +
                                " Piece=" + PieceNames[_piece] + " AutoWeld=" + _autoWeld);
        }

        public override void OnDisable()
        {
            _ready = false;
            ClearAllInternal(false);
            HideVisuals();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            _root = null;
            _preview = null;
            _beam = null;
            _ring = null;
            DisposeMaterial(_beamMaterial);
            DisposeMaterial(_ghostMaterial);
            DisposeMaterial(_flatMaterial);
            DisposeMaterial(_projectileMaterial);

            for (int i = 0; i < _pieceMaterials.Length; i++)
            {
                DisposeMaterial(_pieceMaterials[i]);
                _pieceMaterials[i] = null;

                if (_pieceTextures[i] != null)
                {
                    UnityEngine.Object.Destroy(_pieceTextures[i]);
                    _pieceTextures[i] = null;
                }
            }

            _beamMaterial = null;
            _ghostMaterial = null;
            _flatMaterial = null;
            _projectileMaterial = null;
            _selected = null;
            _hover = null;
            _aimPiece = null;

            Plugin.Log?.LogInfo("Cascade construction stopped.");
        }

        // ---------------------------------------------------------------
        // Frame update
        // ---------------------------------------------------------------
        public override void Update()
        {
            if (!_ready) return;

            long start = ProfilerStats.Timestamp;
            float now = Time.unscaledTime;

            RefreshColorsIfNeeded();
            PruneProjectiles();
            PruneLinks(now);

            bool menuOpen = CascadeMenuBehaviour.IsMenuOpen;
            HandSide hand = ConfiguredHand();
            bool gripHeld = !menuOpen && IsGripHeld(out hand);

            if (!gripHeld)
            {
                HideVisuals();
                ClearAimState();
                ProfilerStats.Record(ProfilerStats.ConstructionKey,
                    ProfilerStats.ElapsedMs(start), false);
                return;
            }

            Vector3 origin;
            Vector3 direction;
            if (!GetAimRay(hand, out origin, out direction))
            {
                HideVisuals();
                ClearAimState();
                ProfilerStats.Record(ProfilerStats.ConstructionKey,
                    ProfilerStats.ElapsedMs(start), false);
                return;
            }

            UpdateAim(origin, direction);
            ComputePlacement();

            if (_aimPiece != _hover)
            {
                _hover = _aimPiece;
                _highlightDirty = true;
            }

            HandleExtraInput(hand);

            if (TriggerPressed(hand))
            {
                DoAction(hand);
            }

            UpdateVisuals(hand, origin);
            UpdateHighlights();

            ProfilerStats.Record(ProfilerStats.ConstructionKey,
                ProfilerStats.ElapsedMs(start), true);
        }

        // ---------------------------------------------------------------
        // Input
        // ---------------------------------------------------------------
        private bool IsGripHeld(out HandSide hand)
        {
            hand = ConfiguredHand();
            if (!_requireGrip) return true;

            if (XRSettings.isDeviceActive)
            {
                if (_grip == 1)
                {
                    hand = HandSide.Left;
                    return VrInput.Held(VrButton.LeftGrip);
                }

                if (_grip == 2)
                {
                    bool right = VrInput.Held(VrButton.RightGrip);
                    bool left = VrInput.Held(VrButton.LeftGrip);
                    if (right)
                    {
                        hand = HandSide.Right;
                        return true;
                    }
                    if (left)
                    {
                        hand = HandSide.Left;
                        return true;
                    }
                    return false;
                }

                hand = HandSide.Right;
                return VrInput.Held(VrButton.RightGrip);
            }

            return IsPcGripHeld();
        }

        private HandSide ConfiguredHand()
        {
            return _grip == 1 ? HandSide.Left : HandSide.Right;
        }

        private bool IsPcGripHeld()
        {
            switch (_pcGrip)
            {
                case 1:
                    return Mouse.current != null && Mouse.current.leftButton.isPressed;
                case 2:
                    return Mouse.current != null && Mouse.current.middleButton.isPressed;
                case 3:
                    Key key = Keybinds.Parse(_pcGripKey, Key.LeftAlt);
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard == null || key == Key.None) return false;
                    return keyboard[key] != null && keyboard[key].isPressed;
                default:
                    return Mouse.current != null && Mouse.current.rightButton.isPressed;
            }
        }

        private bool TriggerPressed(HandSide hand)
        {
            if (XRSettings.isDeviceActive)
            {
                switch (hand)
                {
                    case HandSide.Left:
                        return VrInput.Pressed(VrButton.LeftTrigger);
                    case HandSide.Right:
                        return VrInput.Pressed(VrButton.RightTrigger);
                    default:
                        return VrInput.Pressed(VrButton.LeftTrigger) ||
                               VrInput.Pressed(VrButton.RightTrigger);
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            // If the desktop grip is the left mouse button, use the right
            // button for the tool so one click cannot do both.
            if (_pcGrip == 1) return mouse.rightButton.wasPressedThisFrame;
            return mouse.leftButton.wasPressedThisFrame;
        }

        private void HandleExtraInput(HandSide hand)
        {
            if (XRSettings.isDeviceActive)
            {
                if (PrimaryPressed(hand))
                {
                    _rotation = (_rotation + 1) % 8;
                    _previewDirty = true;
                }
            }

            if (Keybinds.Pressed(Key.R))
            {
                _rotation = (_rotation + 1) % 8;
                _previewDirty = true;
            }

            if (Keybinds.Pressed(Key.Q))
            {
                _piece = (_piece + 1) % PieceNames.Length;
                _previewDirty = true;
                Notify("Piece: " + PieceNames[_piece]);
            }

            if (Keybinds.Pressed(Key.Z))
            {
                UndoLast();
            }

            if (Keybinds.Pressed(Key.Delete))
            {
                ClearAllInternal(true);
            }
        }

        private static bool PrimaryPressed(HandSide hand)
        {
            if (hand == HandSide.Left) return VrInput.Pressed(VrButton.LeftPrimary);
            if (hand == HandSide.Right) return VrInput.Pressed(VrButton.RightPrimary);
            return VrInput.Pressed(VrButton.LeftPrimary) ||
                   VrInput.Pressed(VrButton.RightPrimary);
        }

        private static bool GetAimRay(HandSide hand, out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            Transform handTransform = ResolveHandTransform(hand);

            if (XRSettings.isDeviceActive && handTransform != null)
            {
                origin = handTransform.position + handTransform.forward * 0.06f;
                direction = handTransform.forward;
                return true;
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            if (camera != null && Mouse.current != null)
            {
                Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                origin = ray.origin + ray.direction * 0.2f;
                direction = ray.direction;
                return true;
            }

            if (handTransform != null)
            {
                origin = handTransform.position;
                direction = handTransform.forward;
                return true;
            }

            if (camera == null) return false;

            origin = camera.transform.position + camera.transform.forward * 0.2f;
            direction = camera.transform.forward;
            return true;
        }

        private static Transform ResolveHandTransform(HandSide hand)
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null) return null;
            return hand == HandSide.Left ? tagger.leftHandTransform : tagger.rightHandTransform;
        }

        private static Vector3 GetBeamStart(Vector3 origin, HandSide hand)
        {
            if (XRSettings.isDeviceActive) return origin;

            Transform handTransform = ResolveHandTransform(hand);
            return handTransform != null ? handTransform.position : origin;
        }

        // ---------------------------------------------------------------
        // Aim and placement
        // ---------------------------------------------------------------
        private void UpdateAim(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            float maxDistance = Mathf.Clamp(_range, 2f, 30f);
            RaycastHit hit;

            if (FindAimHit(origin, direction, maxDistance, out hit))
            {
                _hasSurface = true;
                _aimPoint = hit.point;
                _aimNormal = hit.normal.sqrMagnitude > 0.0001f
                    ? hit.normal.normalized
                    : Vector3.up;
                _aimPiece = hit.collider != null
                    ? hit.collider.GetComponentInParent<ConstructionPiece>()
                    : null;
            }
            else
            {
                _hasSurface = false;
                _aimPoint = origin + direction * maxDistance;
                _aimNormal = -direction;
                _aimPiece = null;
            }

            if (_aimPiece == null && _pieces.Count > 0)
            {
                // Fallback piece detection for no-collider mode: find closest piece along aim beam
                float bestRayDistSq = 0.5f; // within 0.7m radius of beam
                for (int i = 0; i < _pieces.Count; i++)
                {
                    ConstructionPiece p = _pieces[i];
                    if (p == null) continue;
                    Vector3 toPiece = p.transform.position - origin;
                    float proj = Vector3.Dot(toPiece, direction);
                    if (proj < 0.2f || proj > maxDistance) continue;
                    Vector3 ptOnRay = origin + direction * proj;
                    float distSq = (p.transform.position - ptOnRay).sqrMagnitude;
                    if (distSq < bestRayDistSq)
                    {
                        bestRayDistSq = distSq;
                        _aimPiece = p;
                        _aimPoint = p.transform.position;
                        _hasSurface = true;
                    }
                }
            }
        }

        private bool FindAimHit(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit best)
        {
            best = default;
            bool found = false;
            float nearest = float.MaxValue;

            int count = Physics.RaycastNonAlloc(
                origin, direction, RayHits, maxDistance, _mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = RayHits[i];
                if (candidate.collider == null) continue;
                if (candidate.distance >= nearest) continue;
                if (IsLocalPlayerCollider(candidate.collider)) continue;
                if (candidate.collider.GetComponentInParent<VRRig>() != null) continue;
                if (candidate.collider.GetComponentInParent<ConstructionProjectile>() != null) continue;
                if (!IsVisibleCollider(candidate.collider)) continue;

                nearest = candidate.distance;
                best = candidate;
                found = true;
            }

            return found;
        }

        private void ComputePlacement()
        {
            int pieceIndex = Mathf.Clamp(_piece, 0, Specs.Length - 1);
            PieceSpec spec = Specs[pieceIndex];
            float size = Mathf.Max(0.1f, _size);
            Vector3 scale = spec.Scale * size;

            Vector3 normal = _aimNormal;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            normal.Normalize();

            Vector3 point = _aimPoint;

            if (spec.IsBlock)
            {
                // Minecraft-style cells: the block fills the cell the aim ray
                // enters, so floors, walls and towers tile with no gaps and no
                // half-block overlaps. Blocks also stay upright instead of
                // tilting to whatever surface they were placed against.
                float cell = Mathf.Max(0.1f, scale.x);
                Vector3 cellPoint = point + normal * (cell * 0.5f);

                _placementPosition = new Vector3(
                    (Mathf.Floor(cellPoint.x / cell) + 0.5f) * cell,
                    (Mathf.Floor(cellPoint.y / cell) + 0.5f) * cell,
                    (Mathf.Floor(cellPoint.z / cell) + 0.5f) * cell);

                // Quarter turns only, so a wall never ends up slightly askew.
                float quarter = Mathf.Round(Mathf.Clamp(_rotation, 0, 7) * 45f / 90f) * 90f;
                _placementRotation = Quaternion.AngleAxis(quarter, Vector3.up);

                _placementValid = _hasSurface && _pieces.Count < Mathf.Max(1, _maxPieces);
                return;
            }

            int snapIndex = Mathf.Clamp(_snap, 0, SnapSteps.Length - 1);
            float step = SnapSteps[snapIndex];
            if (step > 0f)
            {
                point.x = Mathf.Round(point.x / step) * step;
                point.y = Mathf.Round(point.y / step) * step;
                point.z = Mathf.Round(point.z / step) * step;
            }

            float yaw = Mathf.Clamp(_rotation, 0, 7) * 45f;
            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();

            Vector3 tangent = Vector3.ProjectOnPlane(
                camera != null ? camera.transform.right : Vector3.right, normal);
            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.ProjectOnPlane(
                    camera != null ? camera.transform.up : Vector3.forward, normal);
            }
            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.Cross(normal, Vector3.up);
            }
            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.forward;
            }
            tangent.Normalize();

            if (!_hasSurface)
            {
                _placementPosition = point;
                _placementRotation = Quaternion.AngleAxis(yaw, normal);
            }
            else if (spec.Primitive == PrimitiveType.Sphere)
            {
                _placementPosition = point + normal * (scale.x * 0.5f + 0.01f);
                _placementRotation = Quaternion.AngleAxis(yaw, normal);
            }
            else if (spec.SideMount)
            {
                _placementPosition = point + normal * (scale.x * 0.5f + 0.01f);
                _placementRotation = Quaternion.AngleAxis(yaw, normal) *
                                      Quaternion.LookRotation(normal, tangent);
            }
            else
            {
                _placementPosition = point + normal * (scale.y * 0.5f + 0.01f);
                _placementRotation = Quaternion.FromToRotation(Vector3.up, normal) *
                                     Quaternion.AngleAxis(yaw, Vector3.up);
            }

            _placementValid = _pieces.Count < Mathf.Max(1, _maxPieces);
        }

        // ---------------------------------------------------------------
        // Actions
        // ---------------------------------------------------------------
        private void DoAction(HandSide hand)
        {
            switch ((Tool)Mathf.Clamp(_tool, 0, ToolNames.Length - 1))
            {
                case Tool.Build:
                    PlacePiece();
                    break;

                case Tool.Weld:
                    DoWeld();
                    break;

                case Tool.Gun:
                    if (Time.time < _lastShotTime + _gunCooldown) break;
                    _lastShotTime = Time.time;
                    FireProjectile(hand);
                    break;

                case Tool.Explode:
                    if (Time.time < _lastShotTime + _gunCooldown) break;
                    _lastShotTime = Time.time;
                    Detonate(_aimPoint);
                    break;
            }
        }

        private void PlacePiece()
        {
            if (_pieces.Count >= Mathf.Max(1, _maxPieces))
            {
                Notify("Piece limit reached (" + _maxPieces +
                       "). Clear the build or raise Max pieces.");
                return;
            }

            if (!_placementValid)
            {
                Notify("Aim at a surface to place a piece.");
                return;
            }

            int pieceIndex = Mathf.Clamp(_piece, 0, Specs.Length - 1);
            PieceSpec spec = Specs[pieceIndex];
            float size = Mathf.Max(0.1f, _size);

            GameObject go = GameObject.CreatePrimitive(spec.Primitive);
            go.name = "CascadeConstructionPiece";
            go.transform.SetParent(_root.transform, false);
            go.transform.position = _placementPosition;
            go.transform.rotation = _placementRotation;
            go.transform.localScale = spec.Scale * size;

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(spec.Mass * size * size * size, 0.3f, 90f);
            body.maxAngularVelocity = 18f;

            // Static pieces are real building blocks: solid to stand on and
            // climb, but they stay exactly where they were placed instead of
            // rolling away. An explosion switches them back to dynamic first.
            body.isKinematic = _fixedPieces;
            if (!_fixedPieces)
            {
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            ConstructionPiece piece = go.AddComponent<ConstructionPiece>();
            piece.Kind = pieceIndex;
            piece.Body = body;
            // The collider stays enabled: a placed block is solid to stand on,
            // climb and bump into. The kinematic body is what keeps it in place.
            piece.Col = go.GetComponent<Collider>();
            piece.Render = go.GetComponent<Renderer>();
            piece.Block = new MaterialPropertyBlock();
            piece.BaseColor = BaseColorFor(pieceIndex);

            if (piece.Render != null)
            {
                Material material = PieceMaterialFor(pieceIndex);
                if (material != null) piece.Render.sharedMaterial = material;
            }

            ApplyPieceColor(piece);
            _pieces.Add(piece);
            _highlightDirty = true;

            Physics.SyncTransforms();
            if (_autoWeld && !_fixedPieces) AutoWeld(piece);
        }

        private void DoWeld()
        {
            if (_aimPiece == null)
            {
                Notify("Aim at one of your construction pieces first.");
                return;
            }

            if (_selected == null)
            {
                Select(_aimPiece);
                Notify("Selected " + PieceNames[_selected.Kind] +
                       ". Aim at another piece and pull the trigger to weld.");
                return;
            }

            if (_selected == _aimPiece)
            {
                Select(null);
                Notify("Weld selection cleared.");
                return;
            }

            float distance = Vector3.Distance(
                _selected.transform.position, _aimPiece.transform.position);

            if (distance > Mathf.Max(0.5f, _weldRange))
            {
                Notify("Those pieces are " + distance.ToString("0.0") +
                       " m apart; move closer than " + _weldRange.ToString("0.0") + " m.");
                return;
            }

            if (Weld(_selected, _aimPiece))
            {
                Select(null);
                Notify("Welded. This build now has " + _links.Count + " joint(s).");
            }
            else
            {
                Notify("Those pieces are already welded.");
            }
        }

        private void FireProjectile(HandSide hand)
        {
            if (_projectiles.Count >= MaxProjectiles)
            {
                Notify("Too many projectiles in flight.");
                return;
            }

            Vector3 origin;
            Vector3 direction;
            if (!GetAimRay(hand, out origin, out direction))
            {
                return;
            }

            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            Vector3 spawn = origin + direction * 0.45f;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CascadeConstructionShot";
            go.layer = 2; // Ignore Raycast, so shots never block construction aim.
            go.transform.SetParent(_root.transform, false);
            go.transform.position = spawn;
            go.transform.localScale = Vector3.one * 0.16f;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_projectileMaterial != null) renderer.sharedMaterial = _projectileMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Collider collider = go.GetComponent<Collider>();
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.AddForce(direction * Mathf.Clamp(_projectileSpeed, 8f, 50f),
                ForceMode.VelocityChange);

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = 0.06f;
            trail.endWidth = 0f;
            trail.sharedMaterial = _beamMaterial;
            trail.startColor = new Color(1f, 0.65f, 0.2f, 0.9f);
            trail.endColor = new Color(1f, 0.3f, 0.05f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            ConstructionProjectile projectile = go.AddComponent<ConstructionProjectile>();
            projectile.Initialize(this);
            _projectiles.Add(go);

            IgnoreLocalPlayerCollisions(collider);
        }

        private static void IgnoreLocalPlayerCollisions(Collider projectile)
        {
            if (projectile == null) return;

            GTPlayer local = GTPlayer.Instance;
            if (local == null) return;

            if (local.headCollider != null)
            {
                Physics.IgnoreCollision(projectile, local.headCollider);
            }
            if (local.bodyCollider != null)
            {
                Physics.IgnoreCollision(projectile, local.bodyCollider);
            }
        }

        internal void Detonate(Vector3 center)
        {
            float radius = Mathf.Max(0.5f, _explosionRadius);
            float force = Mathf.Max(0f, _explosionForce);
            float destroyRadius = Mathf.Min(Mathf.Max(0.1f, _destroyRadius), radius);

            int count = Physics.OverlapSphereNonAlloc(
                center, radius, OverlapHits, _mask, QueryTriggerInteraction.Ignore);

            _destroyBuffer.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider collider = OverlapHits[i];
                if (collider == null) continue;

                ConstructionPiece piece = collider.GetComponentInParent<ConstructionPiece>();
                if (piece == null || piece.Body == null) continue;

                // A static piece has to be released before force can move it.
                if (piece.Body.isKinematic)
                {
                    piece.Body.isKinematic = false;
                    piece.Body.useGravity = true;
                    piece.Body.WakeUp();
                }

                piece.Body.AddExplosionForce(force, center, radius, 1.2f, ForceMode.Impulse);

                if (!_destroyBuffer.Contains(piece))
                {
                    _destroyBuffer.Add(piece);
                }
            }

            int destroyed = 0;
            for (int i = 0; i < _destroyBuffer.Count; i++)
            {
                ConstructionPiece piece = _destroyBuffer[i];
                if (piece == null) continue;

                // Use transform position when collider is disabled (no-collider mode)
                Vector3 closest = (piece.Col != null && piece.Col.enabled)
                    ? piece.Col.ClosestPoint(center)
                    : piece.transform.position;
                if ((closest - center).sqrMagnitude > destroyRadius * destroyRadius) continue;

                DestroyPiece(piece);
                destroyed++;
            }

            SpawnBurst(center);

            Plugin.Log?.LogInfo("Cascade construction: explosion touched " +
                                _destroyBuffer.Count + " piece(s) and destroyed " +
                                destroyed + ".");
        }

        private void SpawnBurst(Vector3 center)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CascadeConstructionBurst";
            go.transform.SetParent(_root.transform, false);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * 0.2f;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }

            Material material = NewUnlitMaterial(new Color(1f, 0.55f, 0.15f, 0.55f), 3000);
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null) renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            ConstructionBurst burst = go.AddComponent<ConstructionBurst>();
            burst.Initialize(renderer, material,
                Mathf.Max(1f, _explosionRadius) * 2.2f, 0.36f);
        }

        // ---------------------------------------------------------------
        // Welds
        // ---------------------------------------------------------------
        private bool Weld(ConstructionPiece a, ConstructionPiece b)
        {
            if (a == null || b == null || a == b) return false;
            if (a.Body == null || b.Body == null) return false;
            if (HasLink(a, b)) return false;

            FixedJoint joint = a.gameObject.AddComponent<FixedJoint>();
            if (joint == null) return false;

            joint.connectedBody = b.Body;
            joint.autoConfigureConnectedAnchor = true;
            joint.breakForce = Mathf.Max(100f, _weldStrength);
            joint.breakTorque = Mathf.Max(200f, _weldStrength * 2.5f);
            joint.enableCollision = false;

            _links.Add(new ConstructionLink
            {
                A = a,
                B = b,
                Joint = joint
            });

            a.Joints++;
            b.Joints++;
            return true;
        }

        private bool HasLink(ConstructionPiece a, ConstructionPiece b)
        {
            for (int i = 0; i < _links.Count; i++)
            {
                ConstructionLink link = _links[i];
                if (link.Joint == null) continue;
                if ((link.A == a && link.B == b) || (link.A == b && link.B == a))
                {
                    return true;
                }
            }
            return false;
        }

        private void AutoWeld(ConstructionPiece piece)
        {
            if (!_autoWeld || piece == null) return;

            float weldDist = Mathf.Max(0.1f, _weldRadius);
            ConstructionPiece best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _pieces.Count; i++)
            {
                ConstructionPiece other = _pieces[i];
                if (other == null || other == piece) continue;

                float distance = (other.transform.position - piece.transform.position).sqrMagnitude;
                if (distance > weldDist * weldDist * 4f) continue;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = other;
            }

            if (best != null) Weld(piece, best);
        }

        private void RemoveLinks(ConstructionPiece piece)
        {
            for (int i = _links.Count - 1; i >= 0; i--)
            {
                ConstructionLink link = _links[i];
                if (link.A != piece && link.B != piece) continue;

                if (link.Joint != null)
                {
                    UnityEngine.Object.Destroy(link.Joint);
                }
                if (link.A != null && link.A != piece)
                {
                    link.A.Joints = Mathf.Max(0, link.A.Joints - 1);
                }
                if (link.B != null && link.B != piece)
                {
                    link.B.Joints = Mathf.Max(0, link.B.Joints - 1);
                }

                _links.RemoveAt(i);
            }
        }

        private void PruneLinks(float now)
        {
            if (now < _nextLinkPrune) return;
            _nextLinkPrune = now + 0.5f;

            for (int i = _links.Count - 1; i >= 0; i--)
            {
                ConstructionLink link = _links[i];
                if (link.A != null && link.B != null && link.Joint != null) continue;

                if (link.A != null)
                {
                    link.A.Joints = Mathf.Max(0, link.A.Joints - 1);
                }
                if (link.B != null)
                {
                    link.B.Joints = Mathf.Max(0, link.B.Joints - 1);
                }
                if (link.Joint != null)
                {
                    UnityEngine.Object.Destroy(link.Joint);
                }

                _links.RemoveAt(i);
            }
        }

        private void Select(ConstructionPiece piece)
        {
            if (_selected == piece) return;
            _selected = piece;
            _highlightDirty = true;
        }

        private void DestroyPiece(ConstructionPiece piece)
        {
            if (piece == null) return;

            RemoveLinks(piece);

            if (_selected == piece) Select(null);
            if (_hover == piece)
            {
                _hover = null;
                _highlightDirty = true;
            }

            _pieces.Remove(piece);

            if (piece.gameObject != null)
            {
                UnityEngine.Object.Destroy(piece.gameObject);
            }
        }

        private void UndoLast()
        {
            if (_pieces.Count == 0)
            {
                Notify("Nothing to undo.");
                return;
            }

            DestroyPiece(_pieces[_pieces.Count - 1]);
        }

        private void ClearAllInternal(bool notify)
        {
            int count = _pieces.Count;

            for (int i = 0; i < _links.Count; i++)
            {
                ConstructionLink link = _links[i];
                if (link != null && link.Joint != null)
                {
                    UnityEngine.Object.Destroy(link.Joint);
                }
            }
            _links.Clear();

            for (int i = 0; i < _pieces.Count; i++)
            {
                ConstructionPiece piece = _pieces[i];
                if (piece != null && piece.gameObject != null)
                {
                    UnityEngine.Object.Destroy(piece.gameObject);
                }
            }
            _pieces.Clear();

            for (int i = 0; i < _projectiles.Count; i++)
            {
                GameObject projectile = _projectiles[i];
                if (projectile != null) UnityEngine.Object.Destroy(projectile);
            }
            _projectiles.Clear();

            _selected = null;
            _hover = null;
            _aimPiece = null;
            _highlightDirty = true;

            if (notify && count > 0)
            {
                Notify("Cleared " + count + " piece(s).");
            }
        }

        // ---------------------------------------------------------------
        // Menu helpers
        // ---------------------------------------------------------------
        internal static void UndoForMenu()
        {
            Mod mod = ModRegistry.Find("Construction");
            Construction construction = mod as Construction;
            if (construction == null) return;

            if (construction._pieces.Count == 0)
            {
                Notify("Nothing to undo.");
                return;
            }

            construction.DestroyPiece(construction._pieces[construction._pieces.Count - 1]);
            Notify("Removed the newest piece.");
        }

        internal static void ClearAllForMenu()
        {
            Mod mod = ModRegistry.Find("Construction");
            Construction construction = mod as Construction;
            if (construction == null) return;

            construction.ClearAllInternal(true);
        }

        private static void Notify(string message)
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            if (menu != null)
            {
                menu.ShowToast("Construction", message);
            }
            Plugin.Log?.LogInfo("Cascade construction: " + message);
        }

        // ---------------------------------------------------------------
        // Visuals
        // ---------------------------------------------------------------
        private void BuildMaterials()
        {
            _beamMaterial = NewUnlitMaterial(Color.white, 4100);
            _ghostMaterial = NewUnlitMaterial(new Color(0.35f, 0.75f, 1f, 0.34f), 3000);
            _projectileMaterial = NewUnlitMaterial(new Color(1f, 0.6f, 0.2f, 1f), 4100);

            Shader pieceShader = FindPieceShader();
            if (pieceShader == null) return;

            _flatMaterial = new Material(pieceShader) { color = Color.white };
            MakeMatte(_flatMaterial);

            for (int i = 0; i < Specs.Length; i++)
            {
                if (_pieceTextures[i] == null)
                {
                    _pieceTextures[i] = CreatePieceTexture(i);
                }

                Material material = new Material(pieceShader) { color = Color.white };
                MakeMatte(material);
                ApplyPieceTexture(material, _pieceTextures[i]);
                _pieceMaterials[i] = material;
            }
        }

        /// <summary>The material for one piece kind, textured or flat.</summary>
        private Material PieceMaterialFor(int kind)
        {
            if (!_textured && _flatMaterial != null) return _flatMaterial;

            int index = Mathf.Clamp(kind, 0, _pieceMaterials.Length - 1);
            Material material = _pieceMaterials[index];
            return material != null ? material : _flatMaterial;
        }

        /// <summary>Re-applies materials after the Textures toggle or a respawn.</summary>
        private void RefreshPieceMaterials()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                ConstructionPiece piece = _pieces[i];
                if (piece == null || piece.Render == null) continue;

                Material material = PieceMaterialFor(piece.Kind);
                if (material != null) piece.Render.sharedMaterial = material;
            }

            _previewDirty = true;
        }

        private static void MakeMatte(Material material)
        {
            if (material == null) return;

            // Painted wood, clay brick and stone are not mirrors. Keeping the
            // gloss down stops the small pieces from turning into white specular
            // blobs under the sky and hides the texture detail.
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.16f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.16f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.04f);
        }

        private static void ApplyPieceTexture(Material material, Texture texture)
        {
            if (material == null || texture == null) return;

            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        /// <summary>
        /// Builds one small procedural texture per piece kind so a brick reads
        /// as brickwork, a plank as boards, a beam as ribbed steel, a log as
        /// bark and the ball as stitched panels. Every value stays between
        /// white and mid grey, so the piece color tints the pattern instead of
        /// being painted over by it.
        /// </summary>
        private static Texture2D CreatePieceTexture(int kind)
        {
            const int size = 64;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2,
                name = "CascadePieceTexture" + kind
            };

            Color[] pixels = new Color[size * size];

            switch (kind)
            {
                case 0: BrickPattern(pixels, size); break;
                case 1: BrickPattern(pixels, size); break;
                case 2: PlankPattern(pixels, size); break;
                case 3: MetalPattern(pixels, size); break;
                case 4: BarkPattern(pixels, size); break;
                case 5: BallPattern(pixels, size); break;
                default: FillPattern(pixels, 0.92f); break;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Running-bond brickwork: staggered courses, dark mortar, a highlight
        // along each brick's top edge and a shadow along its bottom.
        private static void BrickPattern(Color[] pixels, int size)
        {
            const int rowHeight = 13;
            const int brickWidth = 21;
            const int mortar = 2;

            for (int y = 0; y < size; y++)
            {
                int row = y / rowHeight;
                int offset = (row & 1) == 0 ? 0 : brickWidth / 2;
                int localY = y % rowHeight;

                for (int x = 0; x < size; x++)
                {
                    int localX = (x + offset) % brickWidth;
                    float value;

                    if (localY < mortar || localX < mortar)
                    {
                        value = 0.58f; // mortar
                    }
                    else
                    {
                        int brick = row * 31 + (x + offset) / brickWidth;
                        value = 0.91f + Noise(brick, row) * 0.09f;
                        if (localY < mortar + 2 || localX < mortar + 2) value += 0.07f;
                        if (localY > rowHeight - 3) value -= 0.09f;
                    }

                    pixels[y * size + x] = Grey(value);
                }
            }
        }

        // Four boards with wavy grain, a shadowed seam and a nail at each end.
        private static void PlankPattern(Color[] pixels, int size)
        {
            const int plankHeight = 16;
            const int nailInset = 4;

            for (int y = 0; y < size; y++)
            {
                int plank = y / plankHeight;
                int localY = y % plankHeight;
                float tone = 0.85f + Noise(plank, 7) * 0.12f;

                for (int x = 0; x < size; x++)
                {
                    float value = tone;

                    // Long grain lines running along the board.
                    value += Mathf.Sin(x * 0.31f + plank * 3.1f +
                                       Mathf.Sin(y * 0.5f) * 1.6f) * 0.035f;
                    value += (Noise(x, y + plank * 17) - 0.5f) * 0.05f;

                    if (localY < 2) value = 0.56f;           // seam between boards
                    else if (localY < 4) value -= 0.1f;      // shadow under the seam

                    int nailX = x % (size / 2);
                    if ((nailX == nailInset || nailX == nailInset + 1) &&
                        (localY == 6 || localY == plankHeight - 6))
                    {
                        value = 0.42f;
                    }

                    pixels[y * size + x] = Grey(value);
                }
            }
        }

        // Ribbed steel: bright ribs along the beam, dark grooves between them
        // and a rivet where each rib meets a cross seam.
        private static void MetalPattern(Color[] pixels, int size)
        {
            const int ribWidth = 16;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int rib = x % ribWidth;
                    float value;

                    if (rib < 2) value = 0.54f;
                    else if (rib < 4) value = 1f;
                    else if (rib > ribWidth - 4) value = 0.76f;
                    else value = 0.9f;

                    value += (Noise(x / 2, y) - 0.5f) * 0.05f; // brushed grain

                    if ((rib == 8 || rib == 9) && y % 32 >= 15 && y % 32 <= 17)
                    {
                        value = 1f;
                    }

                    pixels[y * size + x] = Grey(value);
                }
            }
        }

        // Bark: wobbly vertical strips so the log reads as a trunk from any side.
        private static void BarkPattern(Color[] pixels, int size)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wobble = Mathf.Sin(y * 0.17f + x * 0.05f) * 3f;
                    int band = Mathf.FloorToInt((x + wobble) / 8f);
                    float value = 0.82f + (band & 1) * 0.15f;
                    value += (Noise(band, y / 3) - 0.5f) * 0.12f;

                    if (((x + Mathf.RoundToInt(wobble)) % 8) == 0)
                    {
                        value *= 0.7f; // deep groove between bark plates
                    }

                    pixels[y * size + x] = Grey(value);
                }
            }
        }

        // Stitched ball: eight alternating panels with seam lines.
        private static void BallPattern(Color[] pixels, int size)
        {
            const int panelWidth = 8;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int panel = x / panelWidth;
                    float value = 0.82f + (panel & 1) * 0.18f;
                    value += (Noise(panel, panel * 3) - 0.5f) * 0.07f;

                    if (x % panelWidth == 0) value = 0.6f; // seam between panels
                    if (y % 21 == 0) value = 0.66f;        // cross seam

                    pixels[y * size + x] = Grey(value);
                }
            }
        }

        private static void FillPattern(Color[] pixels, float value)
        {
            Color color = Grey(value);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        }

        private static Color Grey(float value)
        {
            value = Mathf.Clamp01(value);
            return new Color(value, value, value, 1f);
        }

        /// <summary>Cheap deterministic noise, so textures rebuild identically.</summary>
        private static float Noise(int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);
            hash = (hash ^ (hash >> 13)) * 1274126177;
            return ((hash >> 8) & 255) / 255f;
        }

        private void BuildVisuals()
        {
            if (_root == null) return;

            GameObject beamObject = new GameObject("CascadeConstructionBeam");
            beamObject.transform.SetParent(_root.transform, false);
            _beam = beamObject.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.numCapVertices = 4;
            _beam.numCornerVertices = 2;
            _beam.alignment = LineAlignment.View;
            _beam.shadowCastingMode = ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            _beam.sharedMaterial = _beamMaterial;
            _beam.startWidth = 0.007f;
            _beam.endWidth = 0.003f;
            _beam.enabled = false;

            GameObject ringObject = new GameObject("CascadeConstructionRing");
            ringObject.transform.SetParent(_root.transform, false);
            _ring = ringObject.AddComponent<LineRenderer>();
            _ring.positionCount = 28;
            _ring.loop = true;
            _ring.useWorldSpace = true;
            _ring.numCapVertices = 4;
            _ring.numCornerVertices = 4;
            _ring.alignment = LineAlignment.View;
            _ring.shadowCastingMode = ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.sharedMaterial = _beamMaterial;
            _ring.startWidth = 0.008f;
            _ring.endWidth = 0.008f;
            _ring.enabled = false;
        }

        private void UpdateVisuals(HandSide hand, Vector3 origin)
        {
            Color color = ToolColor();

            if (_showBeam && _beam != null)
            {
                Vector3 beamStart = GetBeamStart(origin, hand);
                _beam.SetPosition(0, beamStart);
                _beam.SetPosition(1, _aimPoint);
                _beam.startColor = color;
                Color tail = color;
                tail.a = 0.25f;
                _beam.endColor = tail;
                _beam.enabled = true;
            }
            else if (_beam != null)
            {
                _beam.enabled = false;
            }

            UpdateRing(_aimPoint, 0.14f, color);

            if (_tool == (int)Tool.Build)
            {
                if (_preview == null || _previewDirty) RebuildPreview();

                if (_preview != null)
                {
                    _preview.SetActive(true);
                    _preview.transform.position = _placementPosition;
                    _preview.transform.rotation = _placementRotation;
                    _preview.transform.localScale =
                        Specs[Mathf.Clamp(_piece, 0, Specs.Length - 1)].Scale *
                        Mathf.Max(0.1f, _size) * 0.985f;

                    if (_ghostMaterial != null)
                    {
                        Color ghost = _placementValid
                            ? color
                            : new Color(1f, 0.22f, 0.18f, 1f);
                        ghost.a = _placementValid ? 0.34f : 0.42f;
                        _ghostMaterial.color = ghost;
                    }
                }
            }
            else if (_preview != null)
            {
                _preview.SetActive(false);
            }
        }

        private void UpdateRing(Vector3 center, float radius, Color color)
        {
            if (_ring == null) return;

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;

            int count = _ring.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 point = center +
                                right * (Mathf.Cos(angle) * radius) +
                                up * (Mathf.Sin(angle) * radius);
                _ring.SetPosition(i, point);
            }

            _ring.startColor = color;
            _ring.endColor = color;
            _ring.enabled = true;
        }

        private void HideVisuals()
        {
            if (_beam != null) _beam.enabled = false;
            if (_ring != null) _ring.enabled = false;
            if (_preview != null) _preview.SetActive(false);
        }

        private void RebuildPreview()
        {
            _previewDirty = false;

            if (_preview != null)
            {
                UnityEngine.Object.Destroy(_preview);
                _preview = null;
            }

            if (_root == null) return;

            int pieceIndex = Mathf.Clamp(_piece, 0, Specs.Length - 1);
            PieceSpec spec = Specs[pieceIndex];

            GameObject go = GameObject.CreatePrimitive(spec.Primitive);
            go.name = "CascadeConstructionPreview";
            go.layer = 2;
            go.transform.SetParent(_root.transform, false);
            go.transform.localScale = spec.Scale * Mathf.Max(0.1f, _size) * 0.985f;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_ghostMaterial != null)
                {
                    _ghostMaterial.mainTexture = _textured
                        ? _pieceTextures[Mathf.Clamp(pieceIndex, 0, _pieceTextures.Length - 1)]
                        : null;
                    renderer.sharedMaterial = _ghostMaterial;
                }
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _preview = go;
        }

        private void UpdateHighlights()
        {
            if (!_highlightDirty) return;
            _highlightDirty = false;

            for (int i = 0; i < _pieces.Count; i++)
            {
                ConstructionPiece piece = _pieces[i];
                if (piece == null) continue;

                int state = piece == _selected ? 2 : (piece == _hover ? 1 : 0);
                if (piece.Highlight == state) continue;

                piece.Highlight = state;
                ApplyPieceColor(piece);
            }
        }

        private void RefreshColorsIfNeeded()
        {
            CascadeTheme theme = CascadeTheme.Active;
            int version = theme != null ? theme.Version : -1;
            bool themeChanged = version != _lastThemeVersion;
            _lastThemeVersion = version;

            if (!_colorsDirty && !themeChanged) return;
            _colorsDirty = false;

            for (int i = 0; i < _pieces.Count; i++)
            {
                ConstructionPiece piece = _pieces[i];
                if (piece == null) continue;
                piece.BaseColor = BaseColorFor(piece.Kind);
                ApplyPieceColor(piece);
            }

            _previewDirty = true;
        }

        private Color BaseColorFor(int kind)
        {
            Color color = Specs[Mathf.Clamp(kind, 0, Specs.Length - 1)].Color;

            switch (_colorStyle)
            {
                case 1: // Theme
                    color = Color.Lerp(AccentColor(), Color.white, (kind % 3) * 0.12f);
                    break;
                case 2: // Neon
                    color = Color.HSVToRGB((0.55f + kind * 0.07f) % 1f, 0.9f, 1f);
                    break;
                case 3: // Pastel
                    color = Color.Lerp(color, Color.white, 0.55f);
                    break;
                case 4: // Concrete
                    float grey = 0.45f + kind * 0.035f;
                    color = new Color(grey, grey, grey, 1f);
                    break;
            }

            color.a = 1f;
            return color;
        }

        private void ApplyPieceColor(ConstructionPiece piece)
        {
            if (piece == null || piece.Render == null || piece.Block == null) return;

            Color color = piece.BaseColor;

            if (piece.Highlight == 2)
            {
                color = Color.Lerp(color, AccentColor(), 0.72f);
            }
            else if (piece.Highlight == 1)
            {
                color = Color.Lerp(color, Color.white, 0.65f);
            }

            color.a = 1f;
            piece.Block.Clear();
            piece.Block.SetColor(ColorId, color);
            piece.Block.SetColor(BaseColorId, color);
            piece.Render.SetPropertyBlock(piece.Block);
        }

        private Color ToolColor()
        {
            Color color;

            switch ((Tool)Mathf.Clamp(_tool, 0, ToolNames.Length - 1))
            {
                case Tool.Weld:
                    color = new Color(0.35f, 1f, 0.55f, 1f);
                    break;
                case Tool.Gun:
                    color = new Color(1f, 0.6f, 0.18f, 1f);
                    break;
                case Tool.Explode:
                    color = new Color(1f, 0.28f, 0.2f, 1f);
                    break;
                default:
                    color = AccentColor();
                    break;
            }

            color.a = 0.95f;
            return color;
        }

        private static Color AccentColor()
        {
            CascadeTheme theme = CascadeTheme.Active;
            return theme != null
                ? theme.Accent
                : new Color(0.36f, 0.68f, 1f, 1f);
        }

        // ---------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------
        private static Material NewUnlitMaterial(Color color, int renderQueue)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;

            Material material = new Material(shader);
            material.color = color;
            material.renderQueue = renderQueue;
            return material;
        }

        private static Shader FindPieceShader()
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;

            string[] names = srp
                ? new[]
                {
                    "Universal Render Pipeline/Lit",
                    "Universal Render Pipeline/Unlit",
                    "GorillaTag/UberShader",
                    "Standard",
                    "Sprites/Default",
                    "Unlit/Color"
                }
                : new[]
                {
                    "Standard",
                    "GorillaTag/UberShader",
                    "Universal Render Pipeline/Lit",
                    "Sprites/Default",
                    "Unlit/Color"
                };

            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null) return shader;
            }

            return null;
        }

        private static void DisposeMaterial(Material material)
        {
            if (material != null) UnityEngine.Object.Destroy(material);
        }

        private static bool IsLocalPlayerCollider(Collider collider)
        {
            if (collider == null) return false;

            if (VRRig.LocalRig != null)
            {
                VRRig rig = collider.GetComponentInParent<VRRig>();
                if (rig == VRRig.LocalRig) return true;
            }

            GTPlayer local = GTPlayer.Instance;
            if (local == null) return false;
            if (collider == local.headCollider || collider == local.bodyCollider) return true;
            return collider.transform.IsChildOf(local.transform);
        }

        private static bool IsVisibleCollider(Collider collider)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                return false;
            }
            if (collider.isTrigger) return false;

            Renderer renderer = FindRenderer(collider);
            if (renderer == null) return false;
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
            if (renderer is LineRenderer || renderer is TrailRenderer) return false;

            Material material = renderer.sharedMaterial;
            if (material == null) return false;

            try
            {
                if (material.HasProperty(ColorId) && material.color.a <= 0.01f)
                {
                    return false;
                }
            }
            catch
            {
                // Some custom shaders throw while reading a color; treat as visible.
            }

            return true;
        }

        private static Renderer FindRenderer(Collider collider)
        {
            Renderer renderer = collider.GetComponentInParent<Renderer>();
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return renderer;
            }

            return collider.GetComponentInChildren<Renderer>();
        }

        private void PruneProjectiles()
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                if (_projectiles[i] == null) _projectiles.RemoveAt(i);
            }
        }

        private void ClearAimState()
        {
            _hasSurface = false;
            _aimPiece = null;

            if (_hover != null)
            {
                _hover = null;
                _highlightDirty = true;
            }
        }
    }

    /// <summary>One placed construction piece and the cached render state.</summary>
    internal sealed class ConstructionPiece : MonoBehaviour
    {
        internal int Kind;
        internal int Highlight;
        internal int Joints;
        internal Rigidbody Body;
        internal Collider Col;
        internal Renderer Render;
        internal MaterialPropertyBlock Block;
        internal Color BaseColor = Color.white;
    }

    /// <summary>A fixed joint and the two pieces it connects.</summary>
    internal sealed class ConstructionLink
    {
        internal ConstructionPiece A;
        internal ConstructionPiece B;
        internal FixedJoint Joint;
    }

    /// <summary>Explodes a demolition round on the first solid contact.</summary>
    internal sealed class ConstructionProjectile : MonoBehaviour
    {
        private const float Lifetime = 6f;

        private Construction _owner;
        private bool _spent;
        private float _age;

        internal void Initialize(Construction owner)
        {
            _owner = owner;
        }

        private void Update()
        {
            // A round fired into open sky must not hold a projectile slot forever.
            _age += Time.deltaTime;
            if (_age >= Lifetime) Expire();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_spent) return;

            if (_owner != null)
            {
                _owner.Detonate(transform.position);
            }

            Expire();
        }

        private void Expire()
        {
            if (_spent) return;
            _spent = true;
            Destroy(gameObject);
        }
    }

    /// <summary>Short-lived expanding sphere used as an explosion shockwave.</summary>
    internal sealed class ConstructionBurst : MonoBehaviour
    {
        private Renderer _render;
        private Material _material;
        private Color _color = Color.white;
        private float _targetScale = 1f;
        private float _duration = 0.3f;
        private float _age;

        internal void Initialize(
            Renderer render,
            Material material,
            float targetScale,
            float duration)
        {
            _render = render;
            _material = material;
            _color = material != null ? material.color : Color.white;
            _targetScale = Mathf.Max(0.1f, targetScale);
            _duration = Mathf.Max(0.05f, duration);

            if (_render != null) _render.sharedMaterial = material;
        }

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / _duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.15f, _targetScale, t);

            if (_material != null)
            {
                Color color = _color;
                color.a = Mathf.Lerp(_color.a, 0f, t);
                _material.color = color;
            }

            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
