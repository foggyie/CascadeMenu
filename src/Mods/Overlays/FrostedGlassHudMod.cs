using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Overlays
{
    // =========================================================================
    // 1. Mod Registry Wrapper — Toggle & configure from the Overlays / HUD tab
    // =========================================================================
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class FrostedGlassHudMod : Mod
    {
        private static readonly string[] AnchorNames = { "Head (0.5m in front)", "Left Wrist", "Right Wrist" };
        private static readonly string[] TargetModeNames = { "Selected / Closest Player", "Closest Player", "Selected Player Only", "Tracer Off" };
        private static readonly string[] TintNames = { "Ice Crystal", "Cyber Cyan", "Emerald Glass", "Rose Quartz", "Match Target Color" };

        private int _anchorMode = 0;
        private int _targetMode = 0;
        private int _tintPreset = 0;
        private float _scale = 1f;
        private float _glassAlpha = 0.24f;
        private float _tracerWidth = 0.014f;

        private GameObject _host;
        private FrostedGlassHudAndTracer _component;
        private float _nextTextRefresh;

        public FrostedGlassHudMod()
        {
            Name = "Frosted Glass HUD";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "anchor", "Anchor position",
                "Mount the frosted glass HUD in front of your head or on your wrist",
                AnchorNames,
                () => _anchorMode,
                value =>
                {
                    _anchorMode = Mathf.Clamp(value, 0, AnchorNames.Length - 1);
                    SyncSettings();
                }));

            AddSetting(ModSetting.Dropdown(
                "target", "Tracer target",
                "Which player the hand tracer beam locks onto",
                TargetModeNames,
                () => _targetMode,
                value =>
                {
                    _targetMode = Mathf.Clamp(value, 0, TargetModeNames.Length - 1);
                    SyncSettings();
                }));

            AddSetting(ModSetting.Dropdown(
                "tint", "Glass tint",
                "Color tint for the translucent frosted glass and tracer beam",
                TintNames,
                () => _tintPreset,
                value =>
                {
                    _tintPreset = Mathf.Clamp(value, 0, TintNames.Length - 1);
                    if (_component != null) _component.RebuildVisuals();
                }));

            AddSetting(ModSetting.Slider(
                "alpha", "Glass frost opacity",
                "Top fill alpha (0.15 - 0.35 keeps the translucent glass look)",
                0.10f, 0.55f,
                () => _glassAlpha,
                value =>
                {
                    _glassAlpha = value;
                    if (_component != null) _component.RebuildVisuals();
                },
                value => (value * 100f).ToString("0") + "%"));

            AddSetting(ModSetting.Slider(
                "scale", "HUD scale",
                "Overall scale multiplier for the frosted glass panel",
                0.5f, 1.8f,
                () => _scale,
                value =>
                {
                    _scale = value;
                    SyncSettings();
                },
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider(
                "tracerWidth", "Tracer line width",
                "Thickness of the hand-to-target tracer beam in metres",
                0.004f, 0.035f,
                () => _tracerWidth,
                value =>
                {
                    _tracerWidth = value;
                    SyncSettings();
                },
                value => value.ToString("0.000") + " m"));
        }

        public override void OnEnable()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = new GameObject("CascadeFrostedGlassHost");
            if (Plugin.Root != null) _host.transform.SetParent(Plugin.Root.transform, false);

            _component = _host.AddComponent<FrostedGlassHudAndTracer>();
            SyncSettings();
            _component.RebuildVisuals();
            Plugin.Log?.LogInfo("Cascade Frosted Glass HUD active.");
        }

        public override void OnDisable()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
                _component = null;
            }
            Plugin.Log?.LogInfo("Cascade Frosted Glass HUD stopped.");
        }

        public override void LateUpdate()
        {
            if (_component == null) return;

            SyncSettings();
            UpdateTargetAndReadout();
        }

        private void SyncSettings()
        {
            if (_component == null) return;

            _component.AnchorToWrist = _anchorMode != 0;
            _component.UseRightWrist = _anchorMode == 2;
            _component.HeadCanvasScale = 0.00085f * _scale;
            _component.WristCanvasScale = 0.00045f * _scale;
            _component.TracerStartWidth = _tracerWidth;
            _component.TracerEndWidth = _tracerWidth * 0.58f;

            ApplyTintColors(null);
        }

        private void ApplyTintColors(VRRig targetRig)
        {
            if (_component == null) return;

            Color baseTint;
            switch (_tintPreset)
            {
                case 1: baseTint = new Color(0.35f, 0.90f, 1.00f, 1f); break;
                case 2: baseTint = new Color(0.38f, 1.00f, 0.68f, 1f); break;
                case 3: baseTint = new Color(1.00f, 0.52f, 0.78f, 1f); break;
                case 4:
                    baseTint = targetRig != null
                        ? PlayerActions.GetRigColor(targetRig)
                        : new Color(0.88f, 0.95f, 1.00f, 1f);
                    break;
                default: baseTint = new Color(0.88f, 0.95f, 1.00f, 1f); break;
            }

            _component.GlassTopTint = new Color(
                Mathf.Lerp(baseTint.r, 1f, 0.35f),
                Mathf.Lerp(baseTint.g, 1f, 0.35f),
                Mathf.Lerp(baseTint.b, 1f, 0.35f),
                _glassAlpha);

            _component.GlassBottomTint = new Color(
                baseTint.r * 0.68f,
                baseTint.g * 0.75f,
                baseTint.b * 0.88f,
                _glassAlpha * 0.55f);

            _component.GlassBorderColor = new Color(
                Mathf.Lerp(baseTint.r, 1f, 0.5f),
                Mathf.Lerp(baseTint.g, 1f, 0.5f),
                Mathf.Lerp(baseTint.b, 1f, 0.5f),
                0.55f);

            Color tracerCol = _tintPreset == 0 ? new Color(0.45f, 0.92f, 1f, 1f) : baseTint;
            _component.TracerStartColor = new Color(tracerCol.r, tracerCol.g, tracerCol.b, 0.92f);
            _component.TracerEndColor = new Color(tracerCol.r, tracerCol.g, tracerCol.b, 0.14f);
            _component.UpdateTracerGradient();
        }

        private void UpdateTargetAndReadout()
        {
            VRRig targetRig = ResolveTargetRig(out NetPlayer targetPlayer, out float distance);
            _component.TracerTarget = (_targetMode == 3 || targetRig == null) ? null : targetRig.transform;

            if (_tintPreset == 4) ApplyTintColors(targetRig);

            if (Time.unscaledTime < _nextTextRefresh) return;
            _nextTextRefresh = Time.unscaledTime + 0.15f;

            int fps = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            var room = PlayerActions.ReadRoomSnapshot();
            string anchorLabel = _anchorMode == 0 ? "HEAD" : (_anchorMode == 1 ? "L-WRIST" : "R-WRIST");

            string title = "CASCADE GLASS  ·  " + fps + " FPS";
            string body;
            if (targetRig != null)
            {
                string tName = targetPlayer != null
                    ? PlayerActions.SafeName(targetPlayer)
                    : (!string.IsNullOrEmpty(targetRig.playerNameVisible) ? targetRig.playerNameVisible : "Player");
                bool infected = PlayerActions.IsInfected(targetRig, targetPlayer);
                string statusTag = infected ? "<color=#FF4D38>INFECTED</color>" : "<color=#78FFB2>SURVIVOR</color>";

                body = "ANCHOR: <color=#8EE8FF>" + anchorLabel + "</color>   ROOM: <b>" + room.Name + "</b> (" + room.PlayerCount + ")\n" +
                       "TARGET: <b>" + tName + "</b>  [" + distance.ToString("0.0") + "m]\n" +
                       "STATE:  " + statusTag + "   TRACER: <color=#8EE8FF>ACTIVE</color>";
            }
            else
            {
                body = "ANCHOR: <color=#8EE8FF>" + anchorLabel + "</color>   ROOM: <b>" + room.Name + "</b> (" + room.PlayerCount + ")\n" +
                       "TARGET: <color=#B8C7D9>NONE IN RANGE</color>\n" +
                       "MODS:   <b>" + ModRegistry.ActiveCount + " ACTIVE</b>";
            }

            _component.SetText(title, body);
        }

        private VRRig ResolveTargetRig(out NetPlayer matchedPlayer, out float dist)
        {
            matchedPlayer = null;
            dist = 0f;
            Vector3 eye = ModKit.HeadPosition;

            // 1. Check selected player from Player list / GunLib
            if (_targetMode == 0 || _targetMode == 2)
            {
                if (!string.IsNullOrEmpty(PlayerActions.SelectedUserId))
                {
                    NetPlayer sel = PlayerActions.FindPlayer(PlayerActions.SelectedUserId);
                    if (sel != null)
                    {
                        VRRig rig = PlayerActions.FindRigObject(sel);
                        if (rig != null && rig != VRRig.LocalRig && rig.gameObject.activeInHierarchy)
                        {
                            matchedPlayer = sel;
                            dist = Vector3.Distance(eye, rig.transform.position);
                            return rig;
                        }
                    }
                }
                if (GunLibController.SelectedRig != null &&
                    GunLibController.SelectedRig != VRRig.LocalRig &&
                    GunLibController.SelectedRig.gameObject.activeInHierarchy)
                {
                    VRRig rig = GunLibController.SelectedRig;
                    matchedPlayer = GunLibController.GetPlayerFromRig(rig);
                    dist = Vector3.Distance(eye, rig.transform.position);
                    return rig;
                }
                if (_targetMode == 2) return null;
            }

            // 2. Closest active remote rig
            List<VRRig> rigs = PlayerActions.GetRemoteRigs();
            VRRig closest = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < rigs.Count; i++)
            {
                VRRig r = rigs[i];
                if (r == null) continue;
                float d = Vector3.Distance(eye, r.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    closest = r;
                }
            }

            if (closest != null)
            {
                matchedPlayer = GunLibController.GetPlayerFromRig(closest);
                dist = bestDist;
            }
            return closest;
        }
    }

    // =========================================================================
    // 2. Complete Standalone Frosted-Glass VR HUD + Hand-to-Target Tracer
    // =========================================================================
    public sealed class FrostedGlassHudAndTracer : MonoBehaviour
    {
        // =====================================================================
        // CONFIGURATION — Change colors, alpha, dimensions, and offsets here
        // =====================================================================

        [Header("Anchoring & Placement")]
        public bool AnchorToWrist = false; // false = Head-anchored (~0.5m in front), true = Wrist
        public bool UseRightWrist = false;
        public Transform HeadAnchor;       // e.g. GorillaTagger.Instance.headCollider.transform
        public Transform HandAnchor;       // e.g. GorillaTagger.Instance.leftHandTransform
        public Transform TracerTarget;     // Any target Transform (e.g. a remote VRRig.transform)

        // Offset from anchor: Head default = 0.52m forward, slightly down; Wrist default = 0.09m above wrist
        public Vector3 HeadLocalOffset = new Vector3(0f, -0.05f, 0.52f);
        public Vector3 WristLocalOffset = new Vector3(0.03f, 0.09f, 0.02f);

        public Vector2 CanvasSize = new Vector2(400f, 180f);
        public float HeadCanvasScale = 0.00085f;
        public float WristCanvasScale = 0.00045f;
        public float RotationSmoothSpeed = 14f;

        [Header("Frosted Glass Colors & Alpha")]
        // Top-to-bottom vertical frosted gradient (alpha 0.15 - 0.30 for translucent glass)
        public Color GlassTopTint = new Color(0.88f, 0.95f, 1.00f, 0.26f);
        public Color GlassBottomTint = new Color(0.55f, 0.72f, 0.92f, 0.15f);
        // 2px crisp outer border & top inner specular highlight
        public Color GlassBorderColor = new Color(0.92f, 0.98f, 1.00f, 0.52f);
        public Color TopHighlightColor = new Color(1.00f, 1.00f, 1.00f, 0.45f);
        public Color DropShadowColor = new Color(0.00f, 0.03f, 0.08f, 0.38f);

        [Header("HUD Text")]
        public string TitleText = "CASCADE GLASS HUD";
        public string BodyText = "STATUS: <color=#78F0FF>ONLINE</color>\nTARGET: <color=#A6FFCB>LOCKED</color>";
        public Color TextColor = Color.white;

        [Header("Tracer Line Settings")]
        public Color TracerStartColor = new Color(0.42f, 0.90f, 1.00f, 0.92f);
        public Color TracerEndColor = new Color(0.42f, 0.90f, 1.00f, 0.12f); // Fades toward target
        public float TracerStartWidth = 0.015f;
        public float TracerEndWidth = 0.009f;

        // =====================================================================
        // RUNTIME STATE
        // =====================================================================
        private GameObject _canvasRoot;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private TMP_Text _tmpTitle;
        private TMP_Text _tmpBody;
        private Text _legacyTitle;
        private Text _legacyBody;

        private LineRenderer _tracerLine;
        private Material _uiOverlayMaterial;
        private Material _lineOverlayMaterial;
        private Font _fallbackFont;
        private readonly Gradient _tracerGradient = new Gradient();
        private readonly GradientColorKey[] _gradColors = new GradientColorKey[2];
        private readonly GradientAlphaKey[] _gradAlphas = new GradientAlphaKey[2];

        private readonly List<UnityEngine.Object> _ownedAssets = new List<UnityEngine.Object>(16);

        private void OnEnable()
        {
            BuildAll();
        }

        private void Start()
        {
            if (_canvasRoot == null) BuildAll();
        }

        public void RebuildVisuals()
        {
            BuildAll();
        }

        private void BuildAll()
        {
            Cleanup();

            try
            {
                ResolveDefaultGorillaAnchors();
                BuildMaterials();
                BuildFrostedHud();
                BuildTracerLine();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("Failed to initialize FrostedGlassHudAndTracer: " + ex);
            }
        }

        private void ResolveDefaultGorillaAnchors()
        {
            try
            {
                Camera viewCam = CascadeMenuBehaviour.ResolveViewCamera();
                if (viewCam != null)
                {
                    HeadAnchor = viewCam.transform;
                }
                else if (GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null)
                {
                    HeadAnchor = GorillaTagger.Instance.headCollider.transform;
                }

                if (GorillaTagger.Instance != null)
                {
                    HandAnchor = UseRightWrist
                        ? GorillaTagger.Instance.rightHandTransform
                        : GorillaTagger.Instance.leftHandTransform;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Could not auto-resolve GorillaTagger anchors: " + ex.Message);
            }
        }

        // =====================================================================
        // SHADER FALLBACK CHAIN & ZTEST ALWAYS OVERLAY MATERIALS
        // =====================================================================
        private void BuildMaterials()
        {
            Shader uiShader = FindWorkingShader(
                "UI/Default",
                "Sprites/Default",
                "Unlit/Transparent",
                "Hidden/Internal-Colored");

            _uiOverlayMaterial = CreateOverlayMaterial(uiShader, "FrostedHud_UIMat");

            Shader lineShader = FindWorkingShader(
                "Sprites/Default",
                "UI/Default",
                "Unlit/Transparent",
                "Hidden/Internal-Colored");

            _lineOverlayMaterial = CreateOverlayMaterial(lineShader, "FrostedHud_LineMat");
        }

        private static Shader FindWorkingShader(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader s = Shader.Find(names[i]);
                if (s != null)
                {
                    Plugin.Log?.LogInfo("FrostedGlassHUD selected shader: '" + s.name + "'");
                    return s;
                }
            }
            Plugin.Log?.LogError("FrostedGlassHUD: All shader candidates returned null!");
            return null;
        }

        private Material CreateOverlayMaterial(Shader shader, string matName)
        {
            if (shader == null) return null;

            var mat = new Material(shader)
            {
                name = matName,
                color = Color.white,
                renderQueue = 4000 // Transparent overlay queue (> 3000) so it draws over world geometry
            };
            _ownedAssets.Add(mat);

            // Force ZTest Always (CompareFunction.Always == 8) so walls never clip the HUD or tracer
            if (mat.HasProperty("unity_GUIZTestMode"))
                mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            if (mat.HasProperty("_ZTest"))
                mat.SetInt("_ZTest", (int)CompareFunction.Always);
            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);
            if (mat.HasProperty("_Cull"))
                mat.SetInt("_Cull", (int)CullMode.Off);
            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

            return mat;
        }

        // =====================================================================
        // WORLD-SPACE FROSTED GLASS HUD CONSTRUCTION
        // =====================================================================
        private void BuildFrostedHud()
        {
            _canvasRoot = new GameObject("FrostedGlassVR_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _ownedAssets.Add(_canvasRoot);

            // Safety: ensure no Collider or GraphicRaycaster exists to interfere with VR physics/grabbing
            GraphicRaycaster raycaster = _canvasRoot.GetComponent<GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster);
            Collider col = _canvasRoot.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _canvas = _canvasRoot.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace; // Required for VR stereo rendering
            _canvas.sortingOrder = 32000;
            Camera viewCam = CascadeMenuBehaviour.ResolveViewCamera();
            if (viewCam != null) _canvas.worldCamera = viewCam;

            CanvasGroup group = _canvasRoot.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 1f;

            _canvasRect = _canvasRoot.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = CanvasSize;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);

            Transform parentAnchor = AnchorToWrist && HandAnchor != null ? HandAnchor : HeadAnchor;
            if (parentAnchor != null)
            {
                _canvasRect.SetParent(parentAnchor, false);
                _canvasRect.localPosition = AnchorToWrist ? WristLocalOffset : HeadLocalOffset;
                _canvasRect.localRotation = Quaternion.identity;
            }
            float scale = AnchorToWrist ? WristCanvasScale : HeadCanvasScale;
            _canvasRect.localScale = Vector3.one * scale;

            // Generate procedural 9-sliced sprites
            Sprite shadowSprite = ProceduralGlassFactory.CreateSoftShadowSprite(96, 24, 18, _ownedAssets);
            Sprite glassFillSprite = ProceduralGlassFactory.CreateFrostedGlassFillSprite(
                128, 128, 22, GlassTopTint, GlassBottomTint, _ownedAssets);
            Sprite glassBorderSprite = ProceduralGlassFactory.CreateRoundedBorderSprite(
                128, 128, 22, 2.2f, GlassBorderColor, _ownedAssets);
            Sprite topSpecularSprite = ProceduralGlassFactory.CreateTopHighlightSprite(
                128, 32, 18, TopHighlightColor, _ownedAssets);

            // 1. Soft outer drop-shadow behind the glass panel
            RectTransform shadowRect = CreateChildRect("DropShadow", _canvasRect, -18f, -18f, -14f, -22f);
            CreateLayeredImage(shadowRect, shadowSprite, DropShadowColor, true);

            // 2. Main translucent vertical-gradient frosted glass fill
            RectTransform fillRect = CreateChildRect("FrostedFill", _canvasRect, 0f, 0f, 0f, 0f);
            CreateLayeredImage(fillRect, glassFillSprite, Color.white, true);

            // 3. Inner top edge specular glass reflection strip
            RectTransform highlightRect = CreateChildRect("TopSpecularHighlight", _canvasRect, 10f, 10f, -2f, -(CanvasSize.y - 26f));
            CreateLayeredImage(highlightRect, topSpecularSprite, Color.white, true);

            // 4. Crisp 2px anti-aliased bright outer border
            RectTransform borderRect = CreateChildRect("GlassBorder", _canvasRect, 0f, 0f, 0f, 0f);
            CreateLayeredImage(borderRect, glassBorderSprite, Color.white, true);

            // 5. Subtle divider line under the header
            Sprite whiteSprite = ProceduralGlassFactory.CreateSolidWhiteSprite(_ownedAssets);
            RectTransform dividerRect = new GameObject("HeaderDivider", typeof(RectTransform)).GetComponent<RectTransform>();
            dividerRect.SetParent(_canvasRect, false);
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(1f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 1f);
            dividerRect.offsetMin = new Vector2(18f, -44f);
            dividerRect.offsetMax = new Vector2(-18f, -42.5f);
            CreateLayeredImage(dividerRect, whiteSprite, new Color(1f, 1f, 1f, 0.24f), false);

            // 6. Readable Text with subtle drop shadow (TMP with legacy UI.Text fallback)
            RectTransform titleRect = new GameObject("TitleText", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRect.SetParent(_canvasRect, false);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(20f, -40f);
            titleRect.offsetMax = new Vector2(-20f, -10f);

            RectTransform bodyRect = new GameObject("BodyText", typeof(RectTransform)).GetComponent<RectTransform>();
            bodyRect.SetParent(_canvasRect, false);
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(20f, 14f);
            bodyRect.offsetMax = new Vector2(-20f, -52f);

            BuildTextElements(titleRect, bodyRect);
        }

        private static RectTransform CreateChildRect(
            string name, RectTransform parent,
            float left, float right, float top, float bottom)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, top);
            return rt;
        }

        private Image CreateLayeredImage(RectTransform rect, Sprite sprite, Color tint, bool sliced)
        {
            Image img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            if (_uiOverlayMaterial != null) img.material = _uiOverlayMaterial;
            return img;
        }

        private void BuildTextElements(RectTransform titleRect, RectTransform bodyRect)
        {
            bool tmpReady = false;
            try
            {
                TMP_FontAsset fontAsset = CascadeMenu.UI.CascadeUI.Font != null
                    ? CascadeMenu.UI.CascadeUI.Font
                    : TMP_Settings.defaultFontAsset;

                if (fontAsset != null)
                {
                    _tmpTitle = CreateTmpText(titleRect, fontAsset, TitleText, 20f, FontStyles.Bold);
                    _tmpBody = CreateTmpText(bodyRect, fontAsset, BodyText, 16.5f, FontStyles.Normal);
                    tmpReady = _tmpTitle != null && _tmpBody != null;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("FrostedGlassHUD TMP fallback to UI.Text: " + ex.Message);
                tmpReady = false;
            }

            if (!tmpReady)
            {
                _fallbackFont = ResolveFallbackFont();
                _legacyTitle = CreateLegacyText(titleRect, TitleText, 20, FontStyle.Bold);
                _legacyBody = CreateLegacyText(bodyRect, BodyText, 16, FontStyle.Normal);
            }
        }

        private TMP_Text CreateTmpText(RectTransform rect, TMP_FontAsset fontAsset, string content, float size, FontStyles style)
        {
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = fontAsset;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = TextColor;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.alignment = TextAlignmentOptions.TopLeft;

            if (tmp.fontSharedMaterial != null)
            {
                Material mat = new Material(tmp.fontSharedMaterial);
                _ownedAssets.Add(mat);
                mat.renderQueue = 4005;
                if (mat.HasProperty("unity_GUIZTestMode"))
                    mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                if (mat.HasProperty("_ZTest"))
                    mat.SetInt("_ZTest", (int)CompareFunction.Always);
                tmp.fontSharedMaterial = mat;
            }

            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.04f, 0.1f, 0.82f);
            shadow.effectDistance = new Vector2(1.4f, -1.4f);
            return tmp;
        }

        private Text CreateLegacyText(RectTransform rect, string content, int size, FontStyle style)
        {
            Text txt = rect.gameObject.AddComponent<Text>();
            if (_fallbackFont != null) txt.font = _fallbackFont;
            txt.text = content;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = TextColor;
            txt.supportRichText = true;
            txt.raycastTarget = false;
            txt.alignment = TextAnchor.UpperLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (_uiOverlayMaterial != null) txt.material = _uiOverlayMaterial;

            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.04f, 0.1f, 0.82f);
            shadow.effectDistance = new Vector2(1.4f, -1.4f);

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);
            return txt;
        }

        private Font ResolveFallbackFont()
        {
            try
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Roboto" }, 20);
                if (osFont != null)
                {
                    _ownedAssets.Add(osFont);
                    return osFont;
                }
            }
            catch (Exception) { }

            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (builtin != null) return builtin;
            }
            catch (Exception) { }

            return null;
        }

        // =====================================================================
        // TRACER LINE RENDERER CONSTRUCTION
        // =====================================================================
        private void BuildTracerLine()
        {
            var lineObj = new GameObject("FrostedTracerLine");
            lineObj.transform.SetParent(transform, false);
            _ownedAssets.Add(lineObj);

            _tracerLine = lineObj.AddComponent<LineRenderer>();
            _tracerLine.useWorldSpace = true;
            _tracerLine.positionCount = 2;
            _tracerLine.startWidth = TracerStartWidth;
            _tracerLine.endWidth = TracerEndWidth;
            _tracerLine.numCapVertices = 4;
            _tracerLine.numCornerVertices = 2;
            _tracerLine.shadowCastingMode = ShadowCastingMode.Off;
            _tracerLine.receiveShadows = false;
            _tracerLine.lightProbeUsage = LightProbeUsage.Off;
            _tracerLine.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _tracerLine.sortingOrder = 32000;

            if (_lineOverlayMaterial != null)
                _tracerLine.sharedMaterial = _lineOverlayMaterial;

            UpdateTracerGradient();
            _tracerLine.enabled = false;
        }

        public void UpdateTracerGradient()
        {
            if (_tracerLine == null) return;
            _gradColors[0] = new GradientColorKey(new Color(TracerStartColor.r, TracerStartColor.g, TracerStartColor.b, 1f), 0f);
            _gradColors[1] = new GradientColorKey(new Color(TracerEndColor.r, TracerEndColor.g, TracerEndColor.b, 1f), 1f);
            _gradAlphas[0] = new GradientAlphaKey(TracerStartColor.a, 0f);
            _gradAlphas[1] = new GradientAlphaKey(TracerEndColor.a, 1f);
            _tracerGradient.SetKeys(_gradColors, _gradAlphas);
            _tracerLine.colorGradient = _tracerGradient;
        }

        /// <summary>Updates the HUD text at runtime.</summary>
        public void SetText(string title, string body)
        {
            TitleText = title ?? string.Empty;
            BodyText = body ?? string.Empty;
            if (_tmpTitle != null) _tmpTitle.text = TitleText;
            if (_tmpBody != null) _tmpBody.text = BodyText;
            if (_legacyTitle != null) _legacyTitle.text = TitleText;
            if (_legacyBody != null) _legacyBody.text = BodyText;
        }

        // =====================================================================
        // PER-FRAME LATEUPDATE (Smooth Camera Facing & Tracer Positions)
        // =====================================================================
        private void LateUpdate()
        {
            ResolveDefaultGorillaAnchors();
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();

            // 1. Update HUD anchor & smooth rotation toward the camera (no jitter)
            if (_canvasRect != null)
            {
                Transform desiredParent = AnchorToWrist && HandAnchor != null ? HandAnchor : HeadAnchor;
                if (desiredParent != null && _canvasRect.parent != desiredParent)
                {
                    _canvasRect.SetParent(desiredParent, false);
                }

                _canvasRect.localPosition = AnchorToWrist ? WristLocalOffset : HeadLocalOffset;
                float targetScale = AnchorToWrist ? WristCanvasScale : HeadCanvasScale;
                if (!Mathf.Approximately(_canvasRect.localScale.x, targetScale))
                {
                    _canvasRect.localScale = Vector3.one * targetScale;
                }

                Transform lookRef = cam != null ? cam.transform : HeadAnchor;
                if (lookRef != null)
                {
                    Vector3 toHud = _canvasRect.position - lookRef.position;
                    if (toHud.sqrMagnitude > 0.0001f)
                    {
                        Vector3 upAxis = lookRef.up;
                        Quaternion targetRot = Quaternion.LookRotation(toHud.normalized, upAxis);
                        float t = 1f - Mathf.Exp(-RotationSmoothSpeed * Time.unscaledDeltaTime);
                        _canvasRect.rotation = Quaternion.Slerp(_canvasRect.rotation, targetRot, t);
                    }
                }
            }

            // 2. Update Tracer LineRenderer from hand to target
            if (_tracerLine != null)
            {
                Transform startTransform = HandAnchor != null ? HandAnchor : (HeadAnchor != null ? HeadAnchor : transform);
                bool hasValidTarget = TracerTarget != null && TracerTarget.gameObject != null && TracerTarget.gameObject.activeInHierarchy;

                if (hasValidTarget && startTransform != null)
                {
                    if (!_tracerLine.enabled) _tracerLine.enabled = true;
                    _tracerLine.startWidth = TracerStartWidth;
                    _tracerLine.endWidth = TracerEndWidth;
                    _tracerLine.SetPosition(0, startTransform.position);
                    _tracerLine.SetPosition(1, TracerTarget.position);
                }
                else if (_tracerLine.enabled)
                {
                    _tracerLine.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            if (_tracerLine != null) _tracerLine.enabled = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            for (int i = _ownedAssets.Count - 1; i >= 0; i--)
            {
                if (_ownedAssets[i] != null)
                {
                    Destroy(_ownedAssets[i]);
                }
            }
            _ownedAssets.Clear();
            _canvasRoot = null;
            _canvas = null;
            _canvasRect = null;
            _tracerLine = null;
            _uiOverlayMaterial = null;
            _lineOverlayMaterial = null;
        }
    }

    // =========================================================================
    // PROCEDURAL TEXTURE & 9-SLICED SPRITE GENERATOR (Zero external assets)
    // =========================================================================
    internal static class ProceduralGlassFactory
    {
        public static Sprite CreateSolidWhiteSprite(List<UnityEngine.Object> ownerList)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "GlassSolidWhite",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            ownerList.Add(sp);
            ownerList.Add(tex);
            return sp;
        }

        /// <summary>
        /// Anti-aliased 9-sliced rounded rectangle with a vertical frosted gradient
        /// and subtle inner rim glow.
        /// </summary>
        public static Sprite CreateFrostedGlassFillSprite(
            int width, int height, float radius,
            Color topColor, Color bottomColor,
            List<UnityEngine.Object> ownerList)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ProceduralFrostedFill",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                Color rowColor = Color.Lerp(bottomColor, topColor, v);

                for (int x = 0; x < width; x++)
                {
                    float dist = SignedDistanceRoundedBox(x + 0.5f - halfW, y + 0.5f - halfH, halfW - 1.5f, halfH - 1.5f, radius);
                    float edgeAlpha = Mathf.Clamp01(0.5f - dist);

                    float innerRim = Mathf.Clamp01(1f - Mathf.Abs(dist + 2.5f) / 4f) * 0.08f;
                    Color c = rowColor + new Color(innerRim, innerRim, innerRim, innerRim * 0.5f);
                    c.a = rowColor.a * edgeAlpha;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            float slice = Mathf.Ceil(radius + 3f);
            var border = new Vector4(slice, slice, slice, slice);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            ownerList.Add(sp);
            ownerList.Add(tex);
            return sp;
        }

        /// <summary>
        /// Anti-aliased 1-2px bright rounded border ring (9-sliced).
        /// </summary>
        public static Sprite CreateRoundedBorderSprite(
            int width, int height, float radius, float thickness,
            Color borderColor,
            List<UnityEngine.Object> ownerList)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ProceduralFrostedBorder",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float verticalRimScale = Mathf.Lerp(0.65f, 1.15f, v);

                for (int x = 0; x < width; x++)
                {
                    float dist = SignedDistanceRoundedBox(x + 0.5f - halfW, y + 0.5f - halfH, halfW - 1.5f, halfH - 1.5f, radius);
                    float outer = Mathf.Clamp01(0.5f - dist);
                    float inner = Mathf.Clamp01(0.5f - (dist + thickness));
                    float ringAlpha = Mathf.Clamp01(outer - inner);

                    Color c = borderColor;
                    c.a = Mathf.Clamp01(borderColor.a * verticalRimScale) * ringAlpha;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            float slice = Mathf.Ceil(radius + thickness + 2f);
            var border = new Vector4(slice, slice, slice, slice);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            ownerList.Add(sp);
            ownerList.Add(tex);
            return sp;
        }

        /// <summary>
        /// Thin inner specular highlight along the top edge of the glass card.
        /// </summary>
        public static Sprite CreateTopHighlightSprite(
            int width, int height, float radius,
            Color highlightColor,
            List<UnityEngine.Object> ownerList)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ProceduralTopHighlight",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float fadeY = Mathf.Pow(Mathf.Clamp01((v - 0.55f) / 0.45f), 2.2f);

                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float fadeX = Mathf.Sin(u * Mathf.PI);
                    float dist = SignedDistanceRoundedBox(x + 0.5f - halfW, y + 0.5f - halfH, halfW - 2f, halfH - 2f, radius);
                    float inside = Mathf.Clamp01(0.5f - dist);

                    Color c = highlightColor;
                    c.a = highlightColor.a * fadeY * fadeX * inside;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            float slice = Mathf.Ceil(radius + 2f);
            var border = new Vector4(slice, 2f, slice, slice);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            ownerList.Add(sp);
            ownerList.Add(tex);
            return sp;
        }

        /// <summary>
        /// Soft Gaussian-like rounded drop shadow sprite (9-sliced).
        /// </summary>
        public static Sprite CreateSoftShadowSprite(
            int size, float radius, float blurSpread,
            List<UnityEngine.Object> ownerList)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralSoftShadow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float boxHalf = half - blurSpread;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = SignedDistanceRoundedBox(x + 0.5f - half, y + 0.5f - half, boxHalf, boxHalf, radius);
                    float normalized = Mathf.Clamp01(1f - Mathf.Max(0f, dist) / Mathf.Max(1f, blurSpread));
                    float softAlpha = normalized * normalized * (3f - 2f * normalized);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, softAlpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            float slice = Mathf.Ceil(radius + blurSpread);
            var border = new Vector4(slice, slice, slice, slice);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            ownerList.Add(sp);
            ownerList.Add(tex);
            return sp;
        }

        private static float SignedDistanceRoundedBox(float px, float py, float bx, float by, float r)
        {
            float qx = Mathf.Abs(px) - bx + r;
            float qy = Mathf.Abs(py) - by + r;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r;
        }
    }
}
