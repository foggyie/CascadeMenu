using System.Collections.Generic;
using System;
using System.Reflection;
using System.Text;
using GorillaNetworking;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Visual player overlay: ESP boxes (3D, Corner, 2D), bone skeletons,
    /// player-color matching, optional names + distance, chams, markers and tracers.
    ///
    /// One pooled slot per visible player is reused every frame, so the mod never
    /// allocates renderers during gameplay. Everything is client-side and drawn
    /// with depth-independent LineRenderers.
    /// </summary>
    internal sealed class VisualsController : MonoBehaviour
    {
        private const int BoxEdgeCount = 12;
        private const int CornerBoxEdgeCount = 24;
        private const int Box2DEdgeCount = 4;
        private const int MaxBoxLines = 24;
        private const int MaxSkeletonLines = 64;

        private static readonly Color DefaultBoxColor = new Color(0.36f, 1f, 0.62f, 0.88f);
        private static readonly Color DefaultTracerColor = new Color(0.36f, 0.68f, 1f, 0.72f);
        private static readonly Color InfectedAlertColor = new Color(1f, 0.23f, 0.12f, 0.94f);

        private sealed class Slot
        {
            public GameObject Root;
            public VRRig Rig;
            public Renderer[] Renderers;
            public SkinnedMeshRenderer SkinnedBody;
            public readonly List<int> BonePairs = new List<int>(64);
            public readonly List<Transform> DirectFingerPairs = new List<Transform>(48);
            public readonly Vector3[] Corners = new Vector3[8];
            public LineRenderer[] BoxEdges;
            public LineRenderer[] SkeletonLines;
            public LineRenderer Tracer;
            public LineRenderer[] Marker;
            public TextMeshPro Name;
            public TextMeshPro CosmeticTag;
            public string CosmeticText;
            public SpriteRenderer CosmeticIcon;
            public Sprite CosmeticIconSprite;
            public Material ChamMaterial;
            public Color LastChamColor;
            public readonly Dictionary<Renderer, Material[]> OriginalMaterials = new Dictionary<Renderer, Material[]>();
        }

        private MenuConfig _config;
        private Shader _overlayShader;
        private Material _lineMaterial;
        private Material _highlightMaterial;
        private static readonly Color[] HighlightColors =
        {
            new Color(0.1f, 0.85f, 1f, 0.68f),
            new Color(0.25f, 1f, 0.35f, 0.68f),
            new Color(1f, 0.48f, 0.12f, 0.68f),
            new Color(1f, 0.18f, 0.78f, 0.68f)
        };
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly HashSet<string> _announcedCosmetics = new HashSet<string>();
        private readonly HashSet<string> _presentCosmeticIds = new HashSet<string>();
        private static readonly List<string> CosmeticScratch = new List<string>(4);
        private static readonly List<string> CosmeticFakeScratch = new List<string>(4);
        private static readonly Color FakeSpecialColor = new Color(1f, 0.36f, 0.3f, 1f);
        private static FieldInfo _playerColorField;
        private static bool _cosmeticFieldsResolved;
        private string _roomName;

        /// <summary>
        /// Everything the ESP needs to know about one rig's special cosmetics.
        /// Recomputed at most twice a second instead of every frame.
        /// </summary>
        private sealed class CosmeticMeta
        {
            public string Text = string.Empty;
            public bool Has;
            public Color Color = Color.white;
            public Sprite Icon;
            public float NextRefresh;
        }

        private readonly Dictionary<VRRig, CosmeticMeta> _cosmeticMeta = new Dictionary<VRRig, CosmeticMeta>();
        private const float CosmeticRefreshInterval = 0.5f;

        internal void Initialize(MenuConfig config)
        {
            _config = config;

            // GUI/Text Shader is unlit, alpha blended and ZTest Always, so
            // boxes, skeletons, tracers, names and chams stay visible through walls.
            Shader shader = Shader.Find("GUI/Text Shader");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Plugin.Log?.LogWarning("Cascade ESP: no usable shader found.");
                return;
            }

            _overlayShader = shader;
            _lineMaterial = CreateOverlayMaterial(shader, Color.white);
            _highlightMaterial = CreateOverlayMaterial(shader, HighlightColors[0]);
            Plugin.Log?.LogInfo("Cascade ESP ready (" + shader.name + "). Enabled=" + config.EspEnabled.Value);
        }

        private void LateUpdate()
        {
            if (_config == null) return;

            PlayerBoxEspMod.Sync(_config);
            PlayerSkeletonEspMod.Sync(_config);
            PlayerChamsEspMod.Sync(_config);
            PlayerTracerEspMod.Sync(_config);
            PlayerMarkerEspMod.Sync(_config);
            CosmeticTagsEspMod.Sync(_config);

            bool drawEsp = _config.EspEnabled.Value || _config.EspSkeleton.Value ||
                           _config.EspNames.Value || _config.EspTracers.Value ||
                           _config.EspChams.Value || _config.EspMarker.Value;
            bool cosmeticTags = _config.EspCosmeticTags.Value;
            bool cosmeticNotify = _config.CosmeticNotifications.Value;
            if (!drawEsp && !cosmeticTags && !cosmeticNotify)
            {
                ClearSlots();
                return;
            }

            Color presetColor = ResolvePresetColor();
            if (_highlightMaterial != null)
            {
                Color hl = presetColor;
                hl.a = 0.62f;
                _highlightMaterial.color = hl;
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            Vector3 eye = camera != null ? camera.transform.position : transform.position;
            float range = _config.EspRange.Value;
            bool matchPlayerColor = _config.EspMatchPlayerColor.Value;
            float widthMult = Mathf.Clamp(_config.EspLineWidth.Value, 0.4f, 3f);

            List<VRRig> rigs = PlayerActions.GetRemoteRigs();
            NetworkSystem network = NetworkSystem.Instance;
            string room = network != null && network.InRoom ? network.RoomName ?? string.Empty : string.Empty;
            if (!string.Equals(room, _roomName, StringComparison.Ordinal))
            {
                _roomName = room;
                _announcedCosmetics.Clear();
                _cosmeticMeta.Clear();
            }

            Vector3 tracerOrigin = GetTracerOrigin(camera, _config.EspTracerOrigin.Value);
            int used = 0;
            _presentCosmeticIds.Clear();

            for (int i = 0; i < rigs.Count; i++)
            {
                VRRig rig = rigs[i];
                if (rig == null || rig.gameObject == null || !rig.gameObject.activeInHierarchy) continue;

                NetPlayer player = GunLibController.GetPlayerFromRig(rig);
                string playerName = player != null
                    ? PlayerActions.SafeName(player)
                    : (!string.IsNullOrEmpty(rig.playerNameVisible) ? rig.playerNameVisible : "Player");

                CosmeticMeta meta = GetCosmeticMeta(rig);
                if (meta.Has)
                {
                    string id = player != null && !string.IsNullOrEmpty(player.UserId)
                        ? player.UserId
                        : rig.GetInstanceID().ToString();
                    string key = id + "|" + meta.Text;
                    _presentCosmeticIds.Add(key);
                    if (cosmeticNotify && _announcedCosmetics.Add(key))
                    {
                        StandaloneNotificationOverlay.Notify(
                            "Cosmetic",
                            playerName + " has " + meta.Text + ".");
                    }
                }

                Vector3 center = GetRigCenter(rig);
                float dist = Vector3.Distance(eye, center);
                bool inRange = dist <= range;
                if (!inRange && !cosmeticTags) continue;
                if (!drawEsp && !(cosmeticTags && meta.Has)) continue;
                if (drawEsp && !inRange && !(cosmeticTags && meta.Has)) continue;

                bool isInfected = PlayerActions.IsInfected(rig, player);
                int filterMode = _config.EspFilterMode.Value;
                bool passesFilter = filterMode == 0 ||
                                    (filterMode == 1 && isInfected) ||
                                    (filterMode == 2 && !isInfected);

                if (!passesFilter && !(cosmeticTags && meta.Has)) continue;

                Slot slot = GetSlot(used++);

                EnsureRigRenderers(slot, rig);
                Color rigColor = BoostRigColor(PlayerActions.GetRigColor(rig));
                bool highlightInfected = _config.EspInfectedColor.Value && isInfected;
                Color activeColor = highlightInfected
                    ? InfectedAlertColor
                    : (matchPlayerColor ? rigColor : presetColor);

                bool drawThis = drawEsp && inRange && passesFilter && _lineMaterial != null;
                if (drawThis && _config.EspEnabled.Value)
                {
                    Color boxCol = highlightInfected
                        ? InfectedAlertColor
                        : (matchPlayerColor ? rigColor : (_config.EspColorIndex.Value == 0 ? DefaultBoxColor : presetColor));
                    boxCol.a = 0.88f;
                    EnsureBox(slot);
                    UpdateBox(slot, rig, camera, boxCol, _config.EspBoxStyle.Value, 0.008f * widthMult);
                }
                else DestroyBox(slot);

                if (drawThis && _config.EspSkeleton.Value)
                {
                    Color skelCol = activeColor;
                    skelCol.a = 0.92f;
                    EnsureSkeleton(slot);
                    UpdateSkeleton(slot, rig, skelCol, 0.0075f * widthMult, _config.EspSkeletonFingers.Value);
                }
                else DestroySkeleton(slot);

                if (drawThis && _config.EspChams.Value)
                {
                    Color chamCol = activeColor;
                    chamCol.a = 0.58f;
                    ApplyChams(slot, chamCol);
                }
                else RestoreChams(slot);

                if (drawThis && _config.EspMarker.Value)
                {
                    Color markerCol = activeColor;
                    markerCol.a = 0.92f;
                    UpdateMarker(slot, rig, markerCol, 0.013f * widthMult);
                }
                else DestroyMarker(slot);

                if (drawThis && _config.EspTracers.Value)
                {
                    Color trCol = highlightInfected
                        ? InfectedAlertColor
                        : (matchPlayerColor ? rigColor : (_config.EspColorIndex.Value == 0 ? DefaultTracerColor : presetColor));
                    EnsureTracer(slot);
                    float trWidth = 0.0045f * widthMult;
                    slot.Tracer.startWidth = trWidth;
                    slot.Tracer.endWidth = trWidth * 0.75f;
                    Color startCol = trCol;
                    startCol.a = 0.35f;
                    Color endCol = trCol;
                    endCol.a = 0.9f;
                    slot.Tracer.startColor = startCol;
                    slot.Tracer.endColor = endCol;
                    slot.Tracer.SetPosition(0, tracerOrigin);
                    slot.Tracer.SetPosition(1, center);
                    slot.Tracer.enabled = true;
                }
                else
                {
                    DestroyTracer(slot);
                }

                if (_config.EspNames.Value && inRange && passesFilter)
                {
                    string displayLabel = _config.EspShowDistance.Value
                        ? playerName + " [" + Mathf.RoundToInt(dist) + "m]"
                        : playerName;
                    if (_config.EspShowInfectedTag.Value && isInfected)
                    {
                        displayLabel += " [INFECTED]";
                    }
                    Color nameCol = highlightInfected
                        ? InfectedAlertColor
                        : (matchPlayerColor ? Color.Lerp(rigColor, Color.white, 0.25f) : Color.white);
                    nameCol.a = 0.96f;
                    UpdateName(slot, displayLabel, nameCol, rig, camera);
                }
                else if (slot.Name != null)
                {
                    Destroy(slot.Name.gameObject);
                    slot.Name = null;
                }

                if (cosmeticTags && meta.Has)
                    UpdateCosmeticTag(slot, rig, meta, _config.EspNames.Value && inRange);
                else
                    DestroyCosmeticTag(slot);
            }

            if (_announcedCosmetics.Count > 0)
            {
                _announcedCosmetics.RemoveWhere(key => !_presentCosmeticIds.Contains(key));
            }

            RemoveSlotsFrom(used);
        }

        private Color ResolvePresetColor()
        {
            int idx = _config != null ? _config.EspColorIndex.Value : 0;
            if (idx == 4)
            {
                CascadeTheme theme = CascadeTheme.Active;
                return theme != null ? (Color)theme.Accent : HighlightColors[0];
            }
            if (idx == 5)
            {
                float hue = Mathf.Repeat(Time.unscaledTime * 0.22f, 1f);
                return Color.HSVToRGB(hue, 0.85f, 1f);
            }
            return HighlightColors[Mathf.Clamp(idx, 0, HighlightColors.Length - 1)];
        }

        /// <summary>
        /// Ensures dark gorilla colors (like pitch-black or deep brown) remain
        /// bright enough to read clearly through dark walls while preserving hue.
        /// </summary>
        private static Color BoostRigColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.38f) v = 0.38f;
            if (s > 0.12f && s < 0.45f) s = 0.45f;
            Color boosted = Color.HSVToRGB(h, s, v);
            boosted.a = 1f;
            return boosted;
        }

        // -----------------------------------------------------------------
        // Slots
        // -----------------------------------------------------------------

        private Slot GetSlot(int index)
        {
            while (_slots.Count <= index)
            {
                _slots.Add(CreateSlot(_slots.Count));
            }
            return _slots[index];
        }

        private Slot CreateSlot(int index)
        {
            var slot = new Slot();
            slot.Root = new GameObject("CascadeEsp_" + index);
            slot.Root.transform.SetParent(transform, false);

            return slot;
        }

        private void EnsureRigRenderers(Slot slot, VRRig rig)
        {
            if (slot.Rig == rig && slot.Renderers != null) return;
            RestoreChams(slot);
            slot.Rig = rig;
            slot.SkinnedBody = null;
            slot.BonePairs.Clear();
            slot.DirectFingerPairs.Clear();

            var renderers = new List<Renderer>(4);
            AddBodyRenderer(renderers, rig.mainSkin);
            AddBodyRenderer(renderers, rig.faceSkin);
            if (rig.bodyRenderer != null)
            {
                AddBodyRenderer(renderers, rig.bodyRenderer.ActiveBody);
                AddBodyRenderer(renderers, rig.bodyRenderer.faceRenderer);
            }
            slot.Renderers = renderers.ToArray();

            SkinnedMeshRenderer smr = rig.mainSkin;
            if (smr == null && rig.bodyRenderer != null)
            {
                smr = rig.bodyRenderer.ActiveBody as SkinnedMeshRenderer;
            }
            if (smr == null)
            {
                smr = rig.GetComponentInChildren<SkinnedMeshRenderer>();
            }
            slot.SkinnedBody = smr;
            CacheSkeletonBones(slot, rig, smr);
        }

        private static void CacheSkeletonBones(Slot slot, VRRig rig, SkinnedMeshRenderer smr)
        {
            slot.BonePairs.Clear();
            slot.DirectFingerPairs.Clear();

            // 1. Cache exact Gorilla Tag finger bone transforms from VRRig's VRMapThumb / VRMapIndex / VRMapMiddle
            //    and collect all finger transforms so they are excluded from the main body bone pass.
            var fingerSet = new HashSet<Transform>();
            if (rig != null)
            {
                Transform lWrist = (rig.leftHand != null && rig.leftHand.rigTarget != null)
                    ? rig.leftHand.rigTarget
                    : rig.leftHandTransform;
                Transform rWrist = (rig.rightHand != null && rig.rightHand.rigTarget != null)
                    ? rig.rightHand.rigTarget
                    : rig.rightHandTransform;

                if (rig.leftThumb != null)
                    AddFingerChain(slot.DirectFingerPairs, fingerSet, lWrist, rig.leftThumb.fingerBone1, rig.leftThumb.fingerBone2, null);
                if (rig.leftIndex != null)
                    AddFingerChain(slot.DirectFingerPairs, fingerSet, lWrist, rig.leftIndex.fingerBone1, rig.leftIndex.fingerBone2, rig.leftIndex.fingerBone3);
                if (rig.leftMiddle != null)
                    AddFingerChain(slot.DirectFingerPairs, fingerSet, lWrist, rig.leftMiddle.fingerBone1, rig.leftMiddle.fingerBone2, rig.leftMiddle.fingerBone3);

                if (rig.rightThumb != null)
                    AddFingerChain(slot.DirectFingerPairs, fingerSet, rWrist, rig.rightThumb.fingerBone1, rig.rightThumb.fingerBone2, null);
                if (rig.rightIndex != null)
                    AddFingerChain(slot.DirectFingerPairs, fingerSet, rWrist, rig.rightIndex.fingerBone1, rig.rightIndex.fingerBone2, rig.rightIndex.fingerBone3);
                if (rig.rightMiddle != null)
                    AddFingerChain(slot.DirectFingerPairs, fingerSet, rWrist, rig.rightMiddle.fingerBone1, rig.rightMiddle.fingerBone2, rig.rightMiddle.fingerBone3);
            }

            if (smr == null || smr.bones == null || smr.bones.Length == 0) return;

            Transform[] bones = smr.bones;
            var indexMap = new Dictionary<Transform, int>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                Transform b = bones[i];
                if (b != null && !indexMap.ContainsKey(b))
                    indexMap[b] = i;
            }

            bool needFallbackFingers = slot.DirectFingerPairs.Count == 0;

            for (int i = 0; i < bones.Length; i++)
            {
                Transform b = bones[i];
                if (b == null) continue;
                if (fingerSet.Contains(b)) continue;

                string bName = b.name.ToLowerInvariant();
                // Skip non-skeleton cosmetic attachment nodes and face detail bones.
                if (bName.Contains("cosmetic") || bName.Contains("badge") ||
                    bName.Contains("hat") || bName.Contains("eye") ||
                    bName.Contains("lip") || bName.Contains("jaw") || bName.Contains("ear"))
                {
                    continue;
                }

                bool isFinger = bName.Contains("finger") || bName.Contains("thumb") ||
                                bName.Contains("index") || bName.Contains("middle") ||
                                bName.Contains("ring") || bName.Contains("pinky") ||
                                bName.Contains("f_");

                if (isFinger && !needFallbackFingers)
                {
                    // Already captured directly from VRRig finger maps
                    continue;
                }

                Transform p = b.parent;
                int depth = 0;
                while (p != null && depth < 3)
                {
                    if (indexMap.TryGetValue(p, out int parentIdx) && parentIdx != i)
                    {
                        if (isFinger)
                        {
                            if (slot.DirectFingerPairs.Count < 48)
                            {
                                slot.DirectFingerPairs.Add(p);
                                slot.DirectFingerPairs.Add(b);
                            }
                        }
                        else
                        {
                            if (slot.BonePairs.Count < 64)
                            {
                                slot.BonePairs.Add(parentIdx);
                                slot.BonePairs.Add(i);
                            }
                        }
                        break;
                    }
                    p = p.parent;
                    depth++;
                }
            }
        }

        private static void AddFingerChain(
            List<Transform> pairs,
            HashSet<Transform> fingerSet,
            Transform wrist,
            Transform bone1,
            Transform bone2,
            Transform bone3)
        {
            if (bone1 != null) fingerSet.Add(bone1);
            if (bone2 != null) fingerSet.Add(bone2);
            if (bone3 != null) fingerSet.Add(bone3);

            Transform root = wrist;
            if (root == null && bone1 != null) root = bone1.parent;

            if (root != null && bone1 != null)
            {
                pairs.Add(root);
                pairs.Add(bone1);
            }
            if (bone1 != null && bone2 != null)
            {
                pairs.Add(bone1);
                pairs.Add(bone2);
            }
            if (bone2 != null && bone3 != null)
            {
                pairs.Add(bone2);
                pairs.Add(bone3);
            }

            // If the final bone has a tip child transform, connect to the fingertip as well
            Transform lastBone = bone3 != null ? bone3 : bone2;
            if (lastBone != null && lastBone.childCount > 0)
            {
                Transform tip = lastBone.GetChild(0);
                if (tip != null)
                {
                    fingerSet.Add(tip);
                    pairs.Add(lastBone);
                    pairs.Add(tip);
                }
            }
        }

        private static void AddBodyRenderer(List<Renderer> renderers, Renderer renderer)
        {
            if (renderer != null && !renderers.Contains(renderer)) renderers.Add(renderer);
        }

        private static string JoinRoles(List<string> roles)
        {
            if (roles == null || roles.Count == 0) return string.Empty;
            if (roles.Count == 1) return roles[0];
            if (roles.Count == 2) return roles[0] + " & " + roles[1];
            return string.Join(", ", roles.ToArray(), 0, roles.Count - 1) + " & " + roles[roles.Count - 1];
        }

        private static void ResolveCosmeticFields(VRRig rig)
        {
            if (_cosmeticFieldsResolved) return;
            _cosmeticFieldsResolved = true;

            Type type = rig.GetType();
            _playerColorField = type.GetField("playerColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Color GetRigColor(VRRig rig)
        {
            ResolveCosmeticFields(rig);
            if (_playerColorField != null && _playerColorField.FieldType == typeof(Color))
                return (Color)_playerColorField.GetValue(rig);
            if (rig.mainSkin != null && rig.mainSkin.material != null) return rig.mainSkin.material.color;
            return Color.white;
        }

        private void UpdateCosmeticTag(Slot slot, VRRig rig, CosmeticMeta meta, bool namesVisible)
        {
            if (slot.CosmeticTag == null)
            {
                var tagObject = new GameObject("CosmeticRoleTag");
                tagObject.transform.SetParent(slot.Root.transform, false);
                slot.CosmeticTag = tagObject.AddComponent<TextMeshPro>();
                if (CascadeUI.Font != null) slot.CosmeticTag.font = CascadeUI.Font;
                slot.CosmeticTag.fontSize = 4.8f;
                slot.CosmeticTag.alignment = TextAlignmentOptions.Center;
                slot.CosmeticTag.rectTransform.sizeDelta = new Vector2(8f, 1.1f);
                slot.CosmeticTag.outlineWidth = 0.18f;
                slot.CosmeticTag.outlineColor = new Color(0f, 0f, 0f, 0.85f);
                MakeTextThroughWalls(slot.CosmeticTag);
            }

            if (!string.Equals(slot.CosmeticText, meta.Text, StringComparison.Ordinal))
            {
                slot.CosmeticTag.text = meta.Text;
                slot.CosmeticText = meta.Text;
            }

            slot.CosmeticTag.color = meta.Color;

            Transform head = rig.headConstraint != null ? rig.headConstraint : rig.transform;
            float height = namesVisible ? 0.62f : 0.42f;
            Vector3 tagPos = head.position + Vector3.up * (height * rig.scaleFactor);
            slot.CosmeticTag.transform.position = tagPos;
            slot.CosmeticTag.transform.localScale = Vector3.one * (0.25f * rig.scaleFactor);

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            Quaternion facing = slot.CosmeticTag.transform.rotation;
            if (camera != null)
            {
                Vector3 look = tagPos - camera.transform.position;
                if (look.sqrMagnitude > 0.0001f)
                    facing = Quaternion.LookRotation(look.normalized, Vector3.up);
                slot.CosmeticTag.transform.rotation = facing;
            }

            UpdateCosmeticIcon(slot, meta, rig, tagPos, facing);
        }

        private void UpdateCosmeticIcon(Slot slot, CosmeticMeta meta, VRRig rig, Vector3 tagPos, Quaternion facing)
        {
            if (meta.Icon == null)
            {
                if (slot.CosmeticIcon != null)
                {
                    Destroy(slot.CosmeticIcon.gameObject);
                    slot.CosmeticIcon = null;
                    slot.CosmeticIconSprite = null;
                }
                return;
            }

            if (slot.CosmeticIcon == null)
            {
                var iconObject = new GameObject("CosmeticIcon");
                iconObject.transform.SetParent(slot.Root.transform, false);
                slot.CosmeticIcon = iconObject.AddComponent<SpriteRenderer>();
                slot.CosmeticIcon.sharedMaterial = _lineMaterial;
                slot.CosmeticIcon.rendererPriority = 5000;
                slot.CosmeticIcon.sortingOrder = 5000;
                slot.CosmeticIcon.shadowCastingMode = ShadowCastingMode.Off;
                slot.CosmeticIcon.receiveShadows = false;
            }

            if (slot.CosmeticIconSprite != meta.Icon)
            {
                slot.CosmeticIcon.sprite = meta.Icon;
                slot.CosmeticIconSprite = meta.Icon;
            }

            slot.CosmeticIcon.color = meta.Color;

            float scale = rig.scaleFactor;
            slot.CosmeticIcon.transform.position = tagPos + facing * new Vector3(-0.09f * scale, 0f, 0f);
            slot.CosmeticIcon.transform.rotation = facing;
            slot.CosmeticIcon.transform.localScale = Vector3.one * (0.11f * scale);
        }

        private void DestroyCosmeticTag(Slot slot)
        {
            if (slot.CosmeticTag != null)
            {
                Destroy(slot.CosmeticTag.gameObject);
                slot.CosmeticTag = null;
                slot.CosmeticText = null;
            }
            if (slot.CosmeticIcon != null)
            {
                Destroy(slot.CosmeticIcon.gameObject);
                slot.CosmeticIcon = null;
                slot.CosmeticIconSprite = null;
            }
        }

        private CosmeticMeta GetCosmeticMeta(VRRig rig)
        {
            if (!_cosmeticMeta.TryGetValue(rig, out CosmeticMeta meta))
            {
                meta = new CosmeticMeta { NextRefresh = 0f };
                _cosmeticMeta[rig] = meta;
            }

            if (Time.unscaledTime < meta.NextRefresh) return meta;

            meta.NextRefresh = Time.unscaledTime + CosmeticRefreshInterval;

            CosmeticChecker.GetSpecialRoles(rig, CosmeticScratch, CosmeticFakeScratch);
            bool verified = CosmeticScratch.Count > 0;
            bool fake = CosmeticFakeScratch.Count > 0;
            meta.Has = verified || fake;

            if (verified && fake)
            {
                meta.Text = JoinRoles(CosmeticScratch) + " / " + JoinRoles(CosmeticFakeScratch);
            }
            else
            {
                meta.Text = JoinRoles(verified ? CosmeticScratch : CosmeticFakeScratch);
            }

            meta.Color = fake && !verified ? FakeSpecialColor : GetRigColor(rig);
            meta.Icon = verified ? GetSpecialIcon(rig) : null;
            return meta;
        }

        private static Sprite GetSpecialIcon(VRRig rig)
        {
            return CosmeticChecker.VerifiedSpecialIcon(rig);
        }

        private void EnsureBox(Slot slot)
        {
            if (slot.BoxEdges != null) return;
            slot.BoxEdges = new LineRenderer[MaxBoxLines];
            for (int edge = 0; edge < MaxBoxLines; edge++)
            {
                var edgeObject = new GameObject("BoxEdge_" + edge);
                edgeObject.transform.SetParent(slot.Root.transform, false);
                LineRenderer line = edgeObject.AddComponent<LineRenderer>();
                ConfigureLine(line, DefaultBoxColor, 2, 0.008f);
                slot.BoxEdges[edge] = line;
            }
        }

        private void EnsureSkeleton(Slot slot)
        {
            if (slot.SkeletonLines != null) return;
            slot.SkeletonLines = new LineRenderer[MaxSkeletonLines];
            for (int i = 0; i < MaxSkeletonLines; i++)
            {
                var boneObj = new GameObject("Bone_" + i);
                boneObj.transform.SetParent(slot.Root.transform, false);
                LineRenderer line = boneObj.AddComponent<LineRenderer>();
                ConfigureLine(line, Color.white, 2, 0.0075f);
                slot.SkeletonLines[i] = line;
            }
        }

        private void DestroySkeleton(Slot slot)
        {
            if (slot.SkeletonLines == null) return;
            for (int i = 0; i < slot.SkeletonLines.Length; i++)
                if (slot.SkeletonLines[i] != null) Destroy(slot.SkeletonLines[i].gameObject);
            slot.SkeletonLines = null;
        }

        private void EnsureTracer(Slot slot)
        {
            if (slot.Tracer != null) return;
            var tracerObject = new GameObject("Tracer");
            tracerObject.transform.SetParent(slot.Root.transform, false);
            slot.Tracer = tracerObject.AddComponent<LineRenderer>();
            ConfigureLine(slot.Tracer, DefaultTracerColor, 2, 0.004f);
        }

        private void DestroyBox(Slot slot)
        {
            if (slot.BoxEdges == null) return;
            for (int i = 0; i < slot.BoxEdges.Length; i++)
                if (slot.BoxEdges[i] != null) Destroy(slot.BoxEdges[i].gameObject);
            slot.BoxEdges = null;
        }

        private void DestroyTracer(Slot slot)
        {
            if (slot.Tracer != null) Destroy(slot.Tracer.gameObject);
            slot.Tracer = null;
        }

        private void EnsureMarker(Slot slot)
        {
            if (slot.Marker != null) return;
            slot.Marker = new LineRenderer[5];
            for (int i = 0; i < slot.Marker.Length; i++)
            {
                var markerObject = new GameObject("Marker_" + i);
                markerObject.transform.SetParent(slot.Root.transform, false);
                slot.Marker[i] = markerObject.AddComponent<LineRenderer>();
                ConfigureLine(slot.Marker[i], Color.white, 2, 0.014f);
            }
        }

        private void DestroyMarker(Slot slot)
        {
            if (slot.Marker == null) return;
            for (int i = 0; i < slot.Marker.Length; i++)
                if (slot.Marker[i] != null) Destroy(slot.Marker[i].gameObject);
            slot.Marker = null;
        }

        private void ApplyChams(Slot slot, Color color)
        {
            if (_overlayShader == null) return;
            if (slot.ChamMaterial == null)
            {
                slot.ChamMaterial = CreateOverlayMaterial(_overlayShader, color);
                slot.LastChamColor = color;
            }
            else if (slot.LastChamColor != color)
            {
                slot.ChamMaterial.color = color;
                slot.LastChamColor = color;
            }

            for (int i = 0; i < slot.Renderers.Length; i++)
            {
                Renderer renderer = slot.Renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer || slot.OriginalMaterials.ContainsKey(renderer)) continue;

                WhitenVertexColors(renderer);

                Material[] originals = renderer.sharedMaterials;
                slot.OriginalMaterials.Add(renderer, originals);
                var replacement = new Material[originals.Length];
                for (int materialIndex = 0; materialIndex < replacement.Length; materialIndex++)
                    replacement[materialIndex] = slot.ChamMaterial;
                renderer.sharedMaterials = replacement;
            }
        }

        private static readonly HashSet<Mesh> WhitenedMeshes = new HashSet<Mesh>();

        private static void WhitenVertexColors(Renderer renderer)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            else
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }

            if (mesh == null || WhitenedMeshes.Contains(mesh)) return;
            WhitenedMeshes.Add(mesh);

            Color32[] colors = mesh.colors32;
            if (colors == null || colors.Length == 0) return;

            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < colors.Length; i++) colors[i] = white;
            mesh.colors32 = colors;
        }

        private static void RestoreChams(Slot slot)
        {
            foreach (KeyValuePair<Renderer, Material[]> item in slot.OriginalMaterials)
                if (item.Key != null) item.Key.sharedMaterials = item.Value;
            slot.OriginalMaterials.Clear();
        }

        private void UpdateMarker(Slot slot, VRRig rig, Color color, float width)
        {
            EnsureMarker(slot);
            Transform basis = rig.transform;
            Vector3 center = GetRigCenter(rig) + Vector3.up * 0.5f;
            Vector3 right = basis.right * 0.18f;
            Vector3 forward = basis.forward * 0.18f;
            Vector3[] points =
            {
                center + forward, center + right, center - forward, center - right
            };
            for (int i = 0; i < 4; i++)
            {
                LineRenderer line = slot.Marker[i];
                line.startWidth = width;
                line.endWidth = width;
                line.startColor = color;
                line.endColor = color;
                line.SetPosition(0, points[i]);
                line.SetPosition(1, points[(i + 1) % 4]);
                line.enabled = true;
            }
            LineRenderer stem = slot.Marker[4];
            stem.startWidth = width;
            stem.endWidth = width;
            stem.startColor = color;
            stem.endColor = color;
            stem.SetPosition(0, center - Vector3.up * 0.22f);
            stem.SetPosition(1, center);
            stem.enabled = true;
        }

        // -----------------------------------------------------------------
        // Skeleton ESP (Body + Finger phalanx chains)
        // -----------------------------------------------------------------

        private static void UpdateSkeleton(Slot slot, VRRig rig, Color color, float width, bool drawFingers)
        {
            if (slot.SkeletonLines == null) return;

            int lineIndex = 0;
            SkinnedMeshRenderer smr = slot.SkinnedBody;
            if (smr != null && smr.bones != null && slot.BonePairs.Count >= 10)
            {
                Transform[] bones = smr.bones;
                int pairCount = slot.BonePairs.Count / 2;
                for (int p = 0; p < pairCount && lineIndex < MaxSkeletonLines; p++)
                {
                    int idxA = slot.BonePairs[p * 2];
                    int idxB = slot.BonePairs[p * 2 + 1];
                    if (idxA < 0 || idxA >= bones.Length || idxB < 0 || idxB >= bones.Length) continue;

                    Transform boneA = bones[idxA];
                    Transform boneB = bones[idxB];
                    if (boneA == null || boneB == null) continue;

                    Vector3 posA = boneA.position;
                    Vector3 posB = boneB.position;
                    if ((posA - posB).sqrMagnitude < 0.0001f) continue;

                    SetSkeletonLine(slot.SkeletonLines[lineIndex++], posA, posB, color, width);
                }

                if (drawFingers)
                {
                    DrawRigFingers(slot, ref lineIndex, color, width);
                }
            }
            else
            {
                // Procedural gorilla body fallback using rig transforms (Head, Neck, Spine, Shoulders, Elbows, Hands, Hips)
                float scale = rig.scaleFactor > 0.01f ? rig.scaleFactor : 1f;
                Transform basis = rig.transform;
                Vector3 head = rig.headConstraint != null ? rig.headConstraint.position : basis.position + basis.up * (0.35f * scale);
                Vector3 neck = head - basis.up * (0.14f * scale);
                Vector3 pelvis = basis.position - basis.up * (0.32f * scale);
                Vector3 midSpine = Vector3.Lerp(neck, pelvis, 0.5f);

                Vector3 leftShoulder = neck - basis.right * (0.18f * scale);
                Vector3 rightShoulder = neck + basis.right * (0.18f * scale);

                Vector3 leftHand = (rig.leftHand != null && rig.leftHand.rigTarget != null)
                    ? rig.leftHand.rigTarget.position
                    : (rig.leftHandTransform != null
                        ? rig.leftHandTransform.position
                        : leftShoulder - basis.right * (0.28f * scale) - basis.up * (0.22f * scale));
                Vector3 rightHand = (rig.rightHand != null && rig.rightHand.rigTarget != null)
                    ? rig.rightHand.rigTarget.position
                    : (rig.rightHandTransform != null
                        ? rig.rightHandTransform.position
                        : rightShoulder + basis.right * (0.28f * scale) - basis.up * (0.22f * scale));

                Vector3 leftElbow = Vector3.Lerp(leftShoulder, leftHand, 0.5f) - basis.forward * (0.06f * scale) - basis.right * (0.05f * scale);
                Vector3 rightElbow = Vector3.Lerp(rightShoulder, rightHand, 0.5f) - basis.forward * (0.06f * scale) + basis.right * (0.05f * scale);

                SetSkeletonLine(slot.SkeletonLines[lineIndex++], head, neck, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], neck, midSpine, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], midSpine, pelvis, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], neck, leftShoulder, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], neck, rightShoulder, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], leftShoulder, leftElbow, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], leftElbow, leftHand, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], rightShoulder, rightElbow, color, width);
                SetSkeletonLine(slot.SkeletonLines[lineIndex++], rightElbow, rightHand, color, width);

                if (drawFingers)
                {
                    DrawRigFingers(slot, ref lineIndex, color, width);
                }
            }

            for (int i = lineIndex; i < slot.SkeletonLines.Length; i++)
            {
                if (slot.SkeletonLines[i] != null) slot.SkeletonLines[i].enabled = false;
            }
        }

        private static void DrawRigFingers(Slot slot, ref int lineIndex, Color color, float width)
        {
            if (slot.DirectFingerPairs.Count < 2) return;

            float fingerWidth = width * 0.65f;
            Color fingerColor = Color.Lerp(color, Color.white, 0.16f);
            fingerColor.a = color.a;

            int fCount = slot.DirectFingerPairs.Count / 2;
            for (int p = 0; p < fCount && lineIndex < MaxSkeletonLines; p++)
            {
                Transform boneA = slot.DirectFingerPairs[p * 2];
                Transform boneB = slot.DirectFingerPairs[p * 2 + 1];
                if (boneA == null || boneB == null) continue;

                Vector3 posA = boneA.position;
                Vector3 posB = boneB.position;
                if ((posA - posB).sqrMagnitude < 0.000004f) continue;

                SetSkeletonLine(slot.SkeletonLines[lineIndex++], posA, posB, fingerColor, fingerWidth);
            }
        }

        private static void SetSkeletonLine(LineRenderer line, Vector3 a, Vector3 b, Color color, float width)
        {
            if (line == null) return;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.enabled = true;
        }

        /// <summary>
        /// Turns a material into a depth-independent overlay: no depth test, no
        /// depth write, drawn last. Walls can no longer hide the ESP.
        /// </summary>
        private static Material CreateOverlayMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { color = color };
            MakeMaterialThroughWalls(material);
            return material;
        }

        private static void MakeMaterialThroughWalls(Material material)
        {
            if (material == null) return;

            material.renderQueue = 5000;
            material.SetOverrideTag("RenderType", "Transparent");

            if (material.HasProperty("_ZTest"))
                material.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (material.HasProperty("_ZTestMode"))
                material.SetFloat("_ZTestMode", (float)CompareFunction.Always);

            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", (float)CullMode.Off);

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        private static void MakeTextThroughWalls(TextMeshPro text)
        {
            if (text == null) return;
            try
            {
                MakeMaterialThroughWalls(text.fontMaterial);
                if (text.renderer != null)
                {
                    text.renderer.sortingOrder = 5000;
                    text.renderer.shadowCastingMode = ShadowCastingMode.Off;
                    text.renderer.receiveShadows = false;
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade ESP text through-wall setup failed: " + exception.Message);
            }
        }

        private void ConfigureLine(LineRenderer line, Color color, int points, float width)
        {
            line.positionCount = points;
            line.useWorldSpace = true;
            line.loop = false;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = _lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 300;
            line.enabled = false;
        }

        private void RemoveSlotsFrom(int first)
        {
            for (int i = _slots.Count - 1; i >= first; i--)
            {
                Slot slot = _slots[i];
                RestoreChams(slot);
                if (slot.ChamMaterial != null) Destroy(slot.ChamMaterial);
                if (slot.Root != null) Destroy(slot.Root);
                _slots.RemoveAt(i);
            }
        }

        private void ClearSlots()
        {
            RemoveSlotsFrom(0);
        }

        // -----------------------------------------------------------------
        // Geometry
        // -----------------------------------------------------------------

        private static Vector3 GetRigCenter(VRRig rig)
        {
            if (rig == null) return Vector3.zero;
            if (rig.headConstraint != null) return rig.headConstraint.position;
            return rig.transform.position + Vector3.up * 1.2f;
        }

        private static readonly int[] EdgeCorners =
        {
            0,1, 1,2, 2,3, 3,0, 4,5, 5,6, 6,7, 7,4, 0,4, 1,5, 2,6, 3,7
        };

        private static void UpdateBox(Slot slot, VRRig rig, Camera camera, Color color, int style, float width)
        {
            if (slot.BoxEdges == null) return;

            Transform basis = rig.transform;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            bool found = false;
            for (int i = 0; i < slot.Renderers.Length; i++)
            {
                Renderer renderer = slot.Renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer) continue;

                Bounds bounds = renderer.bounds;
                Vector3 ext = bounds.extents;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = bounds.center + new Vector3(
                        (corner & 1) == 0 ? -ext.x : ext.x,
                        (corner & 2) == 0 ? -ext.y : ext.y,
                        (corner & 4) == 0 ? -ext.z : ext.z);
                    Vector3 local = basis.InverseTransformPoint(world);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                    found = true;
                }
            }

            if (!found)
            {
                Vector3 center = basis.InverseTransformPoint(rig.transform.position + Vector3.up * 0.9f);
                min = center + new Vector3(-0.28f, -0.9f, -0.22f);
                max = center + new Vector3(0.28f, 0.9f, 0.22f);
            }
            if (max.y - min.y < 0.8f) { min.y -= 0.4f; max.y += 0.4f; }
            if (max.x - min.x < 0.25f) { min.x -= 0.125f; max.x += 0.125f; }
            if (max.z - min.z < 0.2f) { min.z -= 0.1f; max.z += 0.1f; }

            slot.Corners[0] = basis.TransformPoint(new Vector3(min.x, min.y, min.z));
            slot.Corners[1] = basis.TransformPoint(new Vector3(max.x, min.y, min.z));
            slot.Corners[2] = basis.TransformPoint(new Vector3(max.x, min.y, max.z));
            slot.Corners[3] = basis.TransformPoint(new Vector3(min.x, min.y, max.z));
            slot.Corners[4] = basis.TransformPoint(new Vector3(min.x, max.y, min.z));
            slot.Corners[5] = basis.TransformPoint(new Vector3(max.x, max.y, min.z));
            slot.Corners[6] = basis.TransformPoint(new Vector3(max.x, max.y, max.z));
            slot.Corners[7] = basis.TransformPoint(new Vector3(min.x, max.y, max.z));

            int usedLines = 0;

            if (style == 2 && camera != null)
            {
                // 2D camera-facing billboard rectangle enclosing the 8 corners
                Vector3 camRight = camera.transform.right;
                Vector3 camUp = camera.transform.up;
                Vector3 centerWorld = (slot.Corners[0] + slot.Corners[6]) * 0.5f;

                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 offset = slot.Corners[c] - centerWorld;
                    float x = Vector3.Dot(offset, camRight);
                    float y = Vector3.Dot(offset, camUp);
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

                Vector3 bl = centerWorld + camRight * minX + camUp * minY;
                Vector3 br = centerWorld + camRight * maxX + camUp * minY;
                Vector3 tr = centerWorld + camRight * maxX + camUp * maxY;
                Vector3 tl = centerWorld + camRight * minX + camUp * maxY;

                SetBoxEdge(slot.BoxEdges[usedLines++], bl, br, color, width);
                SetBoxEdge(slot.BoxEdges[usedLines++], br, tr, color, width);
                SetBoxEdge(slot.BoxEdges[usedLines++], tr, tl, color, width);
                SetBoxEdge(slot.BoxEdges[usedLines++], tl, bl, color, width);
            }
            else if (style == 1)
            {
                // 3D Corner Box (24 bracket segments, 28% of each edge from each vertex)
                const float frac = 0.28f;
                for (int edge = 0; edge < BoxEdgeCount; edge++)
                {
                    Vector3 a = slot.Corners[EdgeCorners[edge * 2]];
                    Vector3 b = slot.Corners[EdgeCorners[edge * 2 + 1]];
                    SetBoxEdge(slot.BoxEdges[usedLines++], a, Vector3.Lerp(a, b, frac), color, width);
                    SetBoxEdge(slot.BoxEdges[usedLines++], b, Vector3.Lerp(b, a, frac), color, width);
                }
            }
            else
            {
                // Full 12-edge 3D wireframe box
                for (int edge = 0; edge < BoxEdgeCount; edge++)
                {
                    Vector3 a = slot.Corners[EdgeCorners[edge * 2]];
                    Vector3 b = slot.Corners[EdgeCorners[edge * 2 + 1]];
                    SetBoxEdge(slot.BoxEdges[usedLines++], a, b, color, width);
                }
            }

            for (int i = usedLines; i < slot.BoxEdges.Length; i++)
            {
                if (slot.BoxEdges[i] != null) slot.BoxEdges[i].enabled = false;
            }
        }

        private static void SetBoxEdge(LineRenderer line, Vector3 a, Vector3 b, Color color, float width)
        {
            if (line == null) return;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.enabled = true;
        }

        private static Vector3 GetTracerOrigin(Camera camera, int originMode)
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            switch (originMode)
            {
                case 1: // Left hand
                    if (tagger != null && tagger.leftHandTransform != null && tagger.leftHandTransform.gameObject.activeInHierarchy)
                        return tagger.leftHandTransform.position;
                    break;
                case 2: // Screen bottom
                    if (camera != null)
                        return camera.transform.position + camera.transform.forward * 0.45f - camera.transform.up * 0.24f;
                    break;
                case 3: // Screen center
                    if (camera != null)
                        return camera.transform.position + camera.transform.forward * 0.45f;
                    break;
                default: // Right hand
                    if (tagger != null && tagger.rightHandTransform != null && tagger.rightHandTransform.gameObject.activeInHierarchy)
                        return tagger.rightHandTransform.position;
                    break;
            }

            if (camera != null)
            {
                return camera.transform.position + camera.transform.forward * 0.35f - camera.transform.up * 0.18f;
            }
            return Vector3.zero;
        }

        private void UpdateName(Slot slot, string playerName, Color nameColor, VRRig rig, Camera camera)
        {
            if (slot.Name == null)
            {
                try
                {
                    var nameObject = new GameObject("Name");
                    nameObject.transform.SetParent(slot.Root.transform, false);

                    TextMeshPro text = nameObject.AddComponent<TextMeshPro>();
                    if (CascadeUI.Font != null) text.font = CascadeUI.Font;
                    text.fontSize = 36f;
                    text.alignment = TextAlignmentOptions.Center;
                    text.color = nameColor;
                    text.outlineWidth = 0.16f;
                    text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
                    text.rectTransform.sizeDelta = new Vector2(40f, 8f);
                    nameObject.transform.localScale = Vector3.one * 0.02f;
                    MakeTextThroughWalls(text);
                    slot.Name = text;
                }
                catch (System.Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade ESP name text failed: " + exception.Message);
                    return;
                }
            }

            if (!slot.Name.gameObject.activeSelf) slot.Name.gameObject.SetActive(true);
            slot.Name.text = playerName;
            slot.Name.color = nameColor;

            Transform head = rig.headConstraint != null ? rig.headConstraint : rig.transform;
            Vector3 position = head.position + Vector3.up * 0.42f;
            slot.Name.transform.position = position;

            if (camera != null)
            {
                Vector3 look = position - camera.transform.position;
                if (look.sqrMagnitude > 0.0001f)
                {
                    slot.Name.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                }
            }
        }

        private void OnDestroy()
        {
            ClearSlots();
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_highlightMaterial != null) Destroy(_highlightMaterial);
        }
    }
}
