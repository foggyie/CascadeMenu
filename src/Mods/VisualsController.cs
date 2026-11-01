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
    /// Visual player overlay: ESP boxes, optional names and tracers.
    ///
    /// One pooled slot per visible player is reused every frame, so the mod never
    /// allocates renderers during gameplay. Everything is client-side and drawn
    /// with the same LineRenderer approach as the gun library.
    /// </summary>
    internal sealed class VisualsController : MonoBehaviour
    {
        private const int BoxEdgeCount = 12;
        private static readonly Color BoxColor = new Color(0.36f, 1f, 0.62f, 0.85f);
        private static readonly Color TracerColor = new Color(0.36f, 0.68f, 1f, 0.55f);

        private sealed class Slot
        {
            public GameObject Root;
            public VRRig Rig;
            public Renderer[] Renderers;
            public readonly Vector3[] Corners = new Vector3[8];
            public LineRenderer[] BoxEdges;
            public LineRenderer Tracer;
            public LineRenderer[] Marker;
            public TextMeshPro Name;
            public TextMeshPro CosmeticTag;
            public string CosmeticText;
            public SpriteRenderer CosmeticIcon;
            public Sprite CosmeticIconSprite;
            public readonly Dictionary<Renderer, Material[]> OriginalMaterials = new Dictionary<Renderer, Material[]>();
        }

        private MenuConfig _config;
        private Material _lineMaterial;
        private Material _highlightMaterial;
        private static readonly Color[] HighlightColors =
        {
            new Color(0.1f, 0.85f, 1f, 0.62f),
            new Color(0.25f, 1f, 0.35f, 0.62f),
            new Color(1f, 0.48f, 0.12f, 0.62f),
            new Color(1f, 0.18f, 0.78f, 0.62f)
        };
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<NetPlayer> _players = new List<NetPlayer>();
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
        /// Recomputed at most twice a second instead of every frame, which is
        /// where the old cosmetic tag spent most of its time (reflection +
        /// string building per player per frame).
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

            // Hidden/Internal-Colored is the one built-in shader that reliably
            // exposes _ZTest/_ZWrite, so the ESP can ignore the depth buffer.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Plugin.Log?.LogWarning("Cascade ESP: no usable shader found.");
                return;
            }

            _lineMaterial = new Material(shader) { color = Color.white };
            _lineMaterial.renderQueue = 5000;
            // Through-wall ESP: ignore the depth buffer for every overlay line.
            MakeMaterialThroughWalls(_lineMaterial);
            Shader highlightShader = Shader.Find("Hidden/Internal-Colored") ?? shader;
            _highlightMaterial = new Material(highlightShader);
            _highlightMaterial.color = HighlightColors[0];
            _highlightMaterial.renderQueue = 5000;
            MakeMaterialThroughWalls(_highlightMaterial);
            Plugin.Log?.LogInfo("Cascade ESP ready. Enabled=" + config.EspEnabled.Value);
        }

        private void LateUpdate()
        {
            if (_config == null) return;

            bool drawEsp = _config.EspEnabled.Value || _config.EspNames.Value || _config.EspTracers.Value ||
                           _config.EspChams.Value || _config.EspMarker.Value;
            bool cosmeticTags = _config.EspCosmeticTags.Value;
            bool cosmeticNotify = _config.CosmeticNotifications.Value;
            if (!drawEsp && !cosmeticTags && !cosmeticNotify)
            {
                ClearSlots();
                return;
            }

            if (_highlightMaterial != null)
            {
                int colorIndex = Mathf.Clamp(_config.EspColorIndex.Value, 0, HighlightColors.Length - 1);
                _highlightMaterial.color = HighlightColors[colorIndex];
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            Vector3 eye = camera != null ? camera.transform.position : transform.position;
            float range = _config.EspRange.Value;

            _players.Clear();
            _players.AddRange(PlayerActions.GetPlayers());
            string room = NetworkSystem.Instance != null ? NetworkSystem.Instance.RoomName : string.Empty;
            if (!string.Equals(room, _roomName, StringComparison.Ordinal))
            {
                _roomName = room;
                _announcedCosmetics.Clear();
                _cosmeticMeta.Clear();
            }

            Vector3 tracerOrigin = GetTracerOrigin(camera);
            int used = 0;
            _presentCosmeticIds.Clear();

            for (int i = 0; i < _players.Count; i++)
            {
                VRRig rig = PlayerActions.FindRigObject(_players[i]);
                if (rig == null || rig.gameObject == null || !rig.gameObject.activeInHierarchy) continue;

                CosmeticMeta meta = GetCosmeticMeta(rig);
                if (meta.Has)
                {
                    string id = _players[i] != null ? _players[i].UserId : "unknown";
                    string key = id + "|" + meta.Text;
                    _presentCosmeticIds.Add(key);
                    if (cosmeticNotify && _announcedCosmetics.Add(key))
                    {
                        StandaloneNotificationOverlay.Notify(
                            "Cosmetic",
                            PlayerActions.SafeName(_players[i]) + " has " + meta.Text + ".");
                    }
                }

                Vector3 center = GetRigCenter(rig);
                bool inRange = Vector3.Distance(eye, center) <= range;
                if (!inRange && !cosmeticTags) continue;
                if (!drawEsp && !(cosmeticTags && meta.Has)) continue;
                if (drawEsp && !inRange && !(cosmeticTags && meta.Has)) continue;

                Slot slot = GetSlot(used++);

                EnsureRigRenderers(slot, rig);
                bool drawThis = drawEsp && inRange && _lineMaterial != null;
                if (drawThis && _config.EspEnabled.Value)
                {
                    EnsureBox(slot);
                    UpdateBox(slot, rig);
                }
                else DestroyBox(slot);

                if (drawThis && _config.EspChams.Value) ApplyChams(slot);
                else RestoreChams(slot);

                if (drawThis && _config.EspMarker.Value) UpdateMarker(slot, rig);
                else DestroyMarker(slot);

                if (drawThis && _config.EspTracers.Value)
                {
                    EnsureTracer(slot);
                    slot.Tracer.SetPosition(0, tracerOrigin);
                    slot.Tracer.SetPosition(1, center);
                    slot.Tracer.enabled = true;
                }
                else
                {
                    DestroyTracer(slot);
                }

                if (_config.EspNames.Value && inRange)
                {
                    UpdateName(slot, _players[i], rig, camera);
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
            var renderers = new List<Renderer>(4);
            AddBodyRenderer(renderers, rig.mainSkin);
            AddBodyRenderer(renderers, rig.faceSkin);
            if (rig.bodyRenderer != null)
            {
                AddBodyRenderer(renderers, rig.bodyRenderer.ActiveBody);
                AddBodyRenderer(renderers, rig.bodyRenderer.faceRenderer);
            }
            slot.Renderers = renderers.ToArray();
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

            // Color comes from the cached meta, so no per-frame reflection here.
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

        /// <summary>
        /// Small world-space picture of the item, drawn to the left of the tag.
        /// Cheap: a single SpriteRenderer whose sprite only changes when the
        /// item changes.
        /// </summary>
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
                slot.CosmeticIcon.rendererPriority = 5000;
            }

            if (slot.CosmeticIconSprite != meta.Icon)
            {
                slot.CosmeticIcon.sprite = meta.Icon;
                slot.CosmeticIconSprite = meta.Icon;
            }

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

        /// <summary>
        /// Cached per-rig cosmetic info. Everything expensive (reading the
        /// cosmetic set, reflection, string joins, icon lookup) happens only
        /// when the 0.5 s timer expires, not every frame.
        /// </summary>
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

        /// <summary>First slot-verified special item on the rig that has a picture.</summary>
        private static Sprite GetSpecialIcon(VRRig rig)
        {
            return CosmeticChecker.VerifiedSpecialIcon(rig);
        }

        private void EnsureBox(Slot slot)
        {
            if (slot.BoxEdges != null) return;
            slot.BoxEdges = new LineRenderer[BoxEdgeCount];
            for (int edge = 0; edge < BoxEdgeCount; edge++)
            {
                var edgeObject = new GameObject("BoxEdge_" + edge);
                edgeObject.transform.SetParent(slot.Root.transform, false);
                LineRenderer line = edgeObject.AddComponent<LineRenderer>();
                ConfigureLine(line, BoxColor, 2, 0.008f);
                slot.BoxEdges[edge] = line;
            }
        }

        private void EnsureTracer(Slot slot)
        {
            if (slot.Tracer != null) return;
            var tracerObject = new GameObject("Tracer");
            tracerObject.transform.SetParent(slot.Root.transform, false);
            slot.Tracer = tracerObject.AddComponent<LineRenderer>();
            ConfigureLine(slot.Tracer, TracerColor, 2, 0.004f);
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
                slot.Marker[i].sharedMaterial = _highlightMaterial;
                slot.Marker[i].startColor = Color.white;
                slot.Marker[i].endColor = Color.white;
            }
        }

        private void DestroyMarker(Slot slot)
        {
            if (slot.Marker == null) return;
            for (int i = 0; i < slot.Marker.Length; i++)
                if (slot.Marker[i] != null) Destroy(slot.Marker[i].gameObject);
            slot.Marker = null;
        }

        private void ApplyChams(Slot slot)
        {
            for (int i = 0; i < slot.Renderers.Length; i++)
            {
                Renderer renderer = slot.Renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer || slot.OriginalMaterials.ContainsKey(renderer)) continue;

                Material[] originals = renderer.sharedMaterials;
                slot.OriginalMaterials.Add(renderer, originals);
                var replacement = new Material[originals.Length];
                for (int materialIndex = 0; materialIndex < replacement.Length; materialIndex++)
                    replacement[materialIndex] = _highlightMaterial;
                renderer.sharedMaterials = replacement;
            }
        }

        private static void RestoreChams(Slot slot)
        {
            foreach (KeyValuePair<Renderer, Material[]> item in slot.OriginalMaterials)
                if (item.Key != null) item.Key.sharedMaterials = item.Value;
            slot.OriginalMaterials.Clear();
        }

        private void UpdateMarker(Slot slot, VRRig rig)
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
                slot.Marker[i].SetPosition(0, points[i]);
                slot.Marker[i].SetPosition(1, points[(i + 1) % 4]);
                slot.Marker[i].enabled = true;
            }
            slot.Marker[4].SetPosition(0, center - Vector3.up * 0.22f);
            slot.Marker[4].SetPosition(1, center);
            slot.Marker[4].enabled = true;
        }

        /// <summary>
        /// Turns a material into a depth-independent overlay: no depth test, no
        /// depth write, drawn last. Walls can no longer hide the ESP.
        /// </summary>
        private static void MakeMaterialThroughWalls(Material material)
        {
            if (material == null) return;

            material.renderQueue = 5000;
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)CompareFunction.Always);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);

            // Hidden/Internal-Colored defaults to opaque. Enable normal alpha
            // blending so chams keep their translucency instead of turning every
            // player into a solid coloured block.
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }

        /// <summary>
        /// Same treatment for a TextMeshPro label. TMP owns its own font material
        /// instance, so it has to be configured after the text component exists.
        /// </summary>
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

        private static void UpdateBox(Slot slot, VRRig rig)
        {
            // Each edge is a separate two-point line. One polyline connected
            // consecutive edge endpoints and added false diagonals.
            if (slot.BoxEdges == null) throw new InvalidOperationException("Box edges were not initialized.");

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
            // Guard against transient zero-sized renderer bounds during room joins.
            if (max.y - min.y < 0.8f) { min.y -= 0.4f; max.y += 0.4f; }
            if (max.x - min.x < 0.25f) { min.x -= 0.125f; max.x += 0.125f; }
            if (max.z - min.z < 0.2f) { min.z -= 0.1f; max.z += 0.1f; }

            slot.Corners[0] = new Vector3(min.x, min.y, min.z);
            slot.Corners[1] = new Vector3(max.x, min.y, min.z);
            slot.Corners[2] = new Vector3(max.x, min.y, max.z);
            slot.Corners[3] = new Vector3(min.x, min.y, max.z);
            slot.Corners[4] = new Vector3(min.x, max.y, min.z);
            slot.Corners[5] = new Vector3(max.x, max.y, min.z);
            slot.Corners[6] = new Vector3(max.x, max.y, max.z);
            slot.Corners[7] = new Vector3(min.x, max.y, max.z);

            for (int edge = 0; edge < BoxEdgeCount; edge++)
            {
                LineRenderer line = slot.BoxEdges[edge];
                line.SetPosition(0, basis.TransformPoint(slot.Corners[EdgeCorners[edge * 2]]));
                line.SetPosition(1, basis.TransformPoint(slot.Corners[EdgeCorners[edge * 2 + 1]]));
                line.enabled = true;
            }
        }

        private static Vector3 GetTracerOrigin(Camera camera)
        {
            // Tracers come from the hand (works on desktop too), not the face.
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.rightHandTransform != null)
                return GorillaTagger.Instance.rightHandTransform.position;

            if (camera != null) return camera.transform.position;
            return Vector3.zero;
        }

        private void UpdateName(Slot slot, NetPlayer player, VRRig rig, Camera camera)
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
                    text.color = new Color(1f, 1f, 1f, 0.95f);
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
            slot.Name.text = PlayerActions.SafeName(player);

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
