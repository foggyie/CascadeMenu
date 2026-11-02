using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Live 3D portrait of a player's own gorilla, drawn straight into the UI.
    ///
    /// The remote rig is not moved or cloned with its components: this keeps a
    /// "ghost" copy of its transform hierarchy (renderers only) parked far away
    /// from the map and copies the pose every frame. The result is the real
    /// monkey - actual skin, actual cosmetics, actual hand and head movement -
    /// rendered by a private camera into a RenderTexture.
    ///
    /// Nothing is sent to other players; the copy has no colliders, no network
    /// components and no scripts, and it is destroyed with the row.
    /// </summary>
    internal sealed class CascadeRigPortrait : MonoBehaviour
    {
        private const int Resolution = 112;

        // Far outside every map, but low enough that float precision at this
        // height is still sub-millimetre (1 mm at 8000 m).
        private const float StageBaseY = 8000f;
        private const float StageSpacing = 40f;
        private const float DefaultHeadHeight = 0.95f;

        // Ghost copies live on their own layer so the portrait camera only
        // culls them instead of walking the whole map every frame. Layer 31 is
        // not used by the menu UI; the real world is 8 km away either way.
        private const int GhostLayer = 31;

        private static Camera _camera;
        private static Transform _stageRoot;
        private static readonly List<int> FreeSlots = new List<int>();
        private static int _nextSlot;
        private static int _active;

        private readonly List<Transform> _sourceNodes = new List<Transform>();
        private readonly List<Transform> _targetNodes = new List<Transform>();
        private readonly List<Renderer> _sourceRenderers = new List<Renderer>();
        private readonly List<Renderer> _targetRenderers = new List<Renderer>();
        private readonly Dictionary<Transform, Transform> _map = new Dictionary<Transform, Transform>();
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();

        private Func<VRRig> _getRig;
        private Func<Color> _getColor;
        private RawImage _image;
        private Image _fallback;

        private GameObject _stage;
        private RenderTexture _texture;
        private Transform _previewHead;
        private VRRig _rig;
        private int _builtNodeCount;
        private bool _built;
        private bool _hasFrame;
        private bool _renderFailed;
        private float _nextRender;
        private float _nextRebuild;
        private int _slot = -1;

        /// <summary>Wires the portrait to a rig lookup and the tinted fallback face.</summary>
        internal void Configure(
            RawImage image,
            Image fallback,
            Func<VRRig> getRig,
            Func<Color> getColor)
        {
            _image = image;
            _fallback = fallback;
            _getRig = getRig;
            _getColor = getColor;

            // Stagger the first render so a long player list does not spike one frame.
            _nextRender = Time.unscaledTime + UnityEngine.Random.Range(0f, 0.12f);
        }

        private void OnEnable()
        {
            if (_image != null) _image.enabled = true;
        }

        private void Update()
        {
            if (_image == null) return;
            if (!CascadeMenuBehaviour.IsMenuOpen) return;

            float now = Time.unscaledTime;

            if (!_built)
            {
                Build();
                return;
            }

            if (_rig == null)
            {
                ShowFallback(true);
                if (now >= _nextRebuild)
                {
                    _nextRebuild = now + 1f;
                    Build();
                }
                return;
            }

            if (now >= _nextRebuild)
            {
                _nextRebuild = now + 1f;

                // Catch cosmetic swaps, body-type changes and respawns. The rig
                // itself rarely changes, but the objects under it do.
                if (_getRig == null || _getRig() != _rig || CountNodes(_rig.transform) != _builtNodeCount)
                {
                    Build();
                    return;
                }
            }

            if (now >= _nextRender)
            {
                _nextRender = now + (_active > 8 ? 0.13f : 0.08f);
                Render();
            }
        }

        // -----------------------------------------------------------------
        // Preview construction
        // -----------------------------------------------------------------

        private void Build()
        {
            _built = true;
            _hasFrame = false;
            _builtNodeCount = 0;
            _previewHead = null;
            _map.Clear();
            _sourceNodes.Clear();
            _targetNodes.Clear();
            _sourceRenderers.Clear();
            _targetRenderers.Clear();

            DestroyStage();

            _rig = _getRig != null ? _getRig() : null;
            if (_rig == null)
            {
                ShowFallback(true);
                return;
            }

            EnsureStage();
            EnsureTexture();

            CloneHierarchy(_rig.transform, _stage.transform, true);
            _builtNodeCount = _sourceNodes.Count;

            // The head bone gives the portrait a consistent framing for every
            // body size, including small monkeys.
            _previewHead = Map(_rig.headConstraint);

            AddSkinnedParts();
            AddMeshParts();

            if (_targetRenderers.Count == 0)
            {
                ShowFallback(true);
                return;
            }

            Render();
        }

        private void CloneHierarchy(Transform source, Transform parent, bool isRoot)
        {
            var go = new GameObject(source.name);
            go.layer = GhostLayer;
            Transform clone = go.transform;
            clone.SetParent(parent, false);

            // The root keeps the stage's position and a fixed heading so the
            // portrait always faces the camera. Body/head/hand motion is kept.
            clone.localPosition = isRoot ? Vector3.zero : source.localPosition;
            clone.localRotation = isRoot ? Quaternion.identity : source.localRotation;
            clone.localScale = source.localScale;

            _sourceNodes.Add(source);
            _targetNodes.Add(clone);
            _map[source] = clone;

            for (int i = 0; i < source.childCount; i++)
                CloneHierarchy(source.GetChild(i), clone, false);
        }

        private void AddSkinnedParts()
        {
            SkinnedMeshRenderer[] skins = _rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer source = skins[i];
                if (source == null || source.sharedMesh == null) continue;

                Transform node = Map(source.transform);
                if (node == null || node.GetComponent<Renderer>() != null) continue;

                var proxy = node.gameObject.AddComponent<SkinnedMeshRenderer>();
                proxy.sharedMesh = source.sharedMesh;
                proxy.sharedMaterials = source.sharedMaterials;
                proxy.bones = MapBones(source.bones, node);
                proxy.rootBone = Map(source.rootBone);
                proxy.updateWhenOffscreen = true;
                proxy.shadowCastingMode = ShadowCastingMode.Off;
                proxy.receiveShadows = false;
                proxy.lightProbeUsage = LightProbeUsage.Off;
                proxy.reflectionProbeUsage = ReflectionProbeUsage.Off;
                proxy.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                proxy.enabled = IsRendererActive(source);

                _sourceRenderers.Add(source);
                _targetRenderers.Add(proxy);
            }
        }

        private void AddMeshParts()
        {
            MeshRenderer[] meshes = _rig.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                MeshRenderer source = meshes[i];
                if (source == null) continue;

                // Name tags and TMP labels have no MeshFilter; skipping them also
                // keeps floating text out of the portrait.
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Transform node = Map(source.transform);
                if (node == null || node.GetComponent<Renderer>() != null) continue;

                var proxy = node.gameObject.AddComponent<MeshRenderer>();
                var proxyFilter = node.gameObject.AddComponent<MeshFilter>();
                proxyFilter.sharedMesh = filter.sharedMesh;
                proxy.sharedMaterials = source.sharedMaterials;
                proxy.shadowCastingMode = ShadowCastingMode.Off;
                proxy.receiveShadows = false;
                proxy.lightProbeUsage = LightProbeUsage.Off;
                proxy.reflectionProbeUsage = ReflectionProbeUsage.Off;
                proxy.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                proxy.enabled = IsRendererActive(source);

                _sourceRenderers.Add(source);
                _targetRenderers.Add(proxy);
            }
        }

        private Transform Map(Transform source)
        {
            Transform mapped;
            if (source != null && _map.TryGetValue(source, out mapped)) return mapped;
            return null;
        }

        private Transform[] MapBones(Transform[] bones, Transform fallback)
        {
            if (bones == null) return null;

            var mapped = new Transform[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = Map(bones[i]);
                mapped[i] = bone != null ? bone : fallback;
            }
            return mapped;
        }

        private static bool IsRendererActive(Renderer renderer)
        {
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
        }

        private static int CountNodes(Transform root)
        {
            if (root == null) return 0;

            int count = 1;
            for (int i = 0; i < root.childCount; i++) count += CountNodes(root.GetChild(i));
            return count;
        }

        // -----------------------------------------------------------------
        // Per-frame update
        // -----------------------------------------------------------------

        private void Render()
        {
            if (_renderFailed) return;
            if (_stage == null || _texture == null || _image == null) return;

            Camera camera = SharedCamera();
            try
            {
                CopyPose();
                PositionCamera();

                camera.targetTexture = _texture;
                camera.Render();
            }
            catch (Exception exception)
            {
                // A broken material or a stale rig must never spam the log or
                // break the rest of the menu; fall back to the tinted face.
                _renderFailed = true;
                ShowFallback(true);
                CascadeMenu.Plugin.Log?.LogWarning("Cascade rig portrait disabled: " + exception.Message);
                return;
            }
            finally
            {
                camera.targetTexture = null;
            }

            if (!_hasFrame)
            {
                _hasFrame = true;
                ShowFallback(false);
            }
        }

        private void CopyPose()
        {
            for (int i = 0; i < _sourceNodes.Count; i++)
            {
                Transform source = _sourceNodes[i];
                Transform target = _targetNodes[i];
                if (source == null || target == null) continue;

                if (i == 0)
                {
                    // The rig root stays pinned to the preview stage; only its
                    // scale (small/large monkeys) is mirrored.
                    target.localPosition = Vector3.zero;
                    target.localRotation = Quaternion.identity;
                    target.localScale = source.localScale;
                    continue;
                }

                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }

            for (int i = 0; i < _sourceRenderers.Count; i++)
            {
                Renderer source = _sourceRenderers[i];
                Renderer target = _targetRenderers[i];
                if (source == null || target == null) continue;

                target.enabled = IsRendererActive(source);

                // Player color and cosmetic tints are usually applied through a
                // property block, so the ghost needs the same values.
                source.GetPropertyBlock(_properties);
                target.SetPropertyBlock(_properties);
            }
        }

        private void PositionCamera()
        {
            if (_stage == null) return;

            Vector3 stagePosition = _stage.transform.position;
            float scale = PreviewScale();
            Vector3 target;
            float distance = 1.05f * scale;

            // Aim from the head bone, not from the stage origin. A rig root is
            // not always at the feet (some are anchored at the head), so
            // treating the stage as "ground" aimed above the monkey and left it
            // sitting low - or completely out of frame.
            //
            // The camera looks slightly below the head so the face lands in the
            // upper part of the portrait with the shoulders and chest below it.
            if (_previewHead != null)
            {
                target = new Vector3(
                    stagePosition.x,
                    _previewHead.position.y - 0.1f * scale,
                    stagePosition.z);
            }
            else
            {
                // No head bone: fall back to the renderer bounds, but only when
                // they describe a plausible gorilla. Skinned bounds can be
                // wildly oversized; ignoring them beats aiming at empty sky.
                Bounds bounds;
                if (TryGetPreviewBounds(out bounds) && bounds.size.y > 0.1f && bounds.size.y < 4f)
                {
                    target = new Vector3(
                        stagePosition.x,
                        bounds.max.y - Mathf.Min(0.16f, bounds.size.y * 0.18f),
                        stagePosition.z);
                    distance = Mathf.Clamp(bounds.size.y * 0.95f, 0.5f, 2.4f);
                }
                else
                {
                    target = stagePosition + Vector3.up * DefaultHeadHeight * scale;
                }
            }

            Camera camera = SharedCamera();
            camera.fieldOfView = 34f;
            camera.transform.position = target + Vector3.forward * distance;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        }

        /// <summary>Approximate body scale of the rig (small/large monkeys).</summary>
        private float PreviewScale()
        {
            float scale = 1f;
            if (_rig != null) scale = _rig.transform.lossyScale.y;
            else if (_stage != null && _stage.transform.childCount > 0)
                scale = _stage.transform.GetChild(0).lossyScale.y;

            if (scale < 0.01f) scale = 1f;
            return Mathf.Clamp(scale, 0.4f, 1.6f);
        }

        /// <summary>Combined bounds of the cloned renderers, in world space.</summary>
        private bool TryGetPreviewBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;

            for (int i = 0; i < _targetRenderers.Count; i++)
            {
                Renderer renderer = _targetRenderers[i];
                if (renderer == null || !renderer.enabled) continue;

                Bounds current = renderer.bounds;
                if (current.size.x < 0.001f && current.size.y < 0.001f && current.size.z < 0.001f)
                    continue;

                if (!hasBounds)
                {
                    bounds = current;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(current);
                }
            }

            return hasBounds;
        }

        private static Camera SharedCamera()
        {
            if (_camera != null) return _camera;

            var go = new GameObject("CascadeRigPortraitCamera");
            DontDestroyOnLoad(go);

            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << GhostLayer;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 25f;
            _camera.fieldOfView = 34f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            _camera.stereoTargetEye = StereoTargetEyeMask.None;
            _camera.depth = -100f;
            return _camera;
        }

        private void EnsureTexture()
        {
            if (_texture != null) return;

            _texture = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            _texture.name = "CascadeRigPortrait";
            _texture.filterMode = FilterMode.Bilinear;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.antiAliasing = 2;
            _texture.hideFlags = HideFlags.DontSave;
            _texture.Create();

            _image.texture = _texture;
            _image.color = Color.white;
            _image.raycastTarget = false;
        }

        private void EnsureStage()
        {
            if (_stage != null) return;

            if (_stageRoot == null)
            {
                var root = new GameObject("CascadeRigPortraitRoot");
                DontDestroyOnLoad(root);
                _stageRoot = root.transform;
                _stageRoot.position = new Vector3(0f, StageBaseY, 0f);
            }

            _slot = TakeSlot();
            _active++;

            _stage = new GameObject("CascadeRigPortrait");
            _stage.transform.SetParent(_stageRoot, false);
            _stage.transform.localPosition = new Vector3(_slot * StageSpacing, 0f, 0f);

            AddLight("Key", new Vector3(0.8f, 1.5f, 1.4f), 1.5f);
            AddLight("Fill", new Vector3(-1.1f, 1.0f, 0.7f), 0.8f);
            AddLight("Rim", new Vector3(0f, 1.2f, -1.2f), 0.7f);
        }

        private void AddLight(string name, Vector3 localPosition, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_stage.transform, false);
            go.transform.localPosition = localPosition;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = intensity;
            light.color = new Color(1f, 0.97f, 0.93f);
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.cullingMask = 1 << GhostLayer;
        }

        private static int TakeSlot()
        {
            if (FreeSlots.Count > 0)
            {
                int slot = FreeSlots[FreeSlots.Count - 1];
                FreeSlots.RemoveAt(FreeSlots.Count - 1);
                return slot;
            }
            return _nextSlot++;
        }

        private void ShowFallback(bool show)
        {
            if (_image != null) _image.enabled = !show;
            if (_fallback == null) return;

            _fallback.enabled = show;
            if (show && _getColor != null) _fallback.color = _getColor();
        }

        private void DestroyStage()
        {
            if (_stage != null)
            {
                Destroy(_stage);
                _stage = null;
            }

            if (_slot >= 0)
            {
                FreeSlots.Add(_slot);
                _slot = -1;
            }

            if (_texture != null)
            {
                if (_image != null) _image.texture = null;
                Destroy(_texture);
                _texture = null;
            }

            _previewHead = null;
        }

        private void OnDestroy()
        {
            if (_stage != null) _active = Mathf.Max(0, _active - 1);
            DestroyStage();
            if (_image != null) _image.enabled = true;
        }
    }
}
