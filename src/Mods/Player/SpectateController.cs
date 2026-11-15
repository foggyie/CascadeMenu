using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Renders a live first- or third-person view of another player's rig into a
    /// RenderTexture.
    ///
    /// The local player, hands and camera are never moved: this is a visual feed
    /// only. That keeps spectating safe in VR (no fighting the XR rig) and means
    /// nothing is sent to the server, so it cannot desync movement or trigger an
    /// anti-cheat teleport check.
    /// </summary>
    internal sealed class SpectateController : MonoBehaviour
    {
        // Close to the on-screen feed size; low enough to stay cheap on Quest.
        private const int FeedWidth = 1024;
        private const int FeedHeight = 576;
        private const float ThirdPersonDistance = 2.1f;
        private const float ThirdPersonHeight = 0.18f;
        private const float FirstPersonForward = 0.3f;
        private const float MinWallDistance = 0.4f;

        private static SpectateController _instance;

        private readonly RaycastHit[] _wallHits = new RaycastHit[24];

        private VRRig _target;
        private Camera _camera;
        private RenderTexture _texture;
        private Canvas _menuCanvas;

        private bool _firstPerson;
        private bool _hasPose;
        private Vector3 _position;
        private Quaternion _rotation;

        internal static bool IsActive
        {
            get { return _instance != null && _instance._target != null; }
        }

        internal bool FirstPerson
        {
            get { return _firstPerson; }
        }

        /// <summary>Texture the overlay shows; null until spectating starts.</summary>
        internal Texture Feed
        {
            get { return _texture; }
        }

        /// <summary>Starts (or restarts) the feed for one rig.</summary>
        internal static SpectateController Begin(VRRig rig, Canvas menuCanvas)
        {
            if (rig == null) return null;

            SpectateController controller = Ensure();
            controller.StartFeed(rig, menuCanvas);
            return controller;
        }

        /// <summary>Stops the feed if one is running.</summary>
        internal static void StopActive()
        {
            if (_instance != null) _instance.StopFeed();
        }

        private static SpectateController Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("CascadeSpectateFeed");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SpectateController>();
            return _instance;
        }

        private void StartFeed(VRRig rig, Canvas menuCanvas)
        {
            _target = rig;
            _menuCanvas = menuCanvas;

            // Third person is the safer default: it shows the whole gorilla and
            // does not put the camera inside their face mesh.
            _firstPerson = false;
            _hasPose = false;

            EnsureCamera();
            EnsureTexture();
        }

        internal void StopFeed()
        {
            _target = null;
            _menuCanvas = null;
            _hasPose = false;

            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }

        internal void SetFirstPerson(bool firstPerson)
        {
            if (_firstPerson == firstPerson) return;

            _firstPerson = firstPerson;

            // Snap once so switching modes cannot sweep the camera through a
            // wall or through the target's body.
            _hasPose = false;
        }

        private void EnsureCamera()
        {
            if (_camera != null) return;

            var go = new GameObject("CascadeSpectateFeedCamera");
            go.transform.SetParent(transform, false);

            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
            _camera.cullingMask = ~(1 << 31);
            _camera.nearClipPlane = 0.03f;
            _camera.farClipPlane = 300f;
            _camera.fieldOfView = 70f;
            _camera.aspect = (float)FeedWidth / FeedHeight;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = true;
            _camera.stereoTargetEye = StereoTargetEyeMask.None;
            _camera.depth = -200f;
        }

        private void EnsureTexture()
        {
            if (_texture != null) Destroy(_texture);

            _texture = new RenderTexture(
                FeedWidth,
                FeedHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            _texture.name = "CascadeSpectateFeed";
            _texture.filterMode = FilterMode.Bilinear;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.antiAliasing = 2;
            _texture.hideFlags = HideFlags.DontSave;
            _texture.Create();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Transform rigTransform = _target.transform;
            if (rigTransform == null)
            {
                StopFeed();
                return;
            }

            // The head bone gives a stable aim point. If a rig has not synced
            // it yet, fall back to roughly eye height instead of the feet.
            Transform head = _target.headConstraint;
            Vector3 headPosition = head != null
                ? head.position
                : rigTransform.position + Vector3.up * 0.95f;
            Quaternion headRotation = head != null ? head.rotation : rigTransform.rotation;
            Vector3 headForward = head != null ? head.forward : rigTransform.forward;

            Vector3 targetPosition;
            Quaternion targetRotation;
            if (_firstPerson)
            {
                targetPosition = headPosition + headForward * FirstPersonForward;
                targetRotation = headRotation;
            }
            else
            {
                Vector3 focus = headPosition + Vector3.up * 0.08f;
                Vector3 back = -FlatForward(headForward, rigTransform.forward);
                float distance = WallDistance(focus, back, ThirdPersonDistance);

                targetPosition = focus + back * distance + Vector3.up * ThirdPersonHeight;
                targetRotation = Quaternion.LookRotation(focus - targetPosition, Vector3.up);
            }

            if (!_hasPose)
            {
                _position = targetPosition;
                _rotation = targetRotation;
                _hasPose = true;
            }
            else
            {
                float rate = _firstPerson ? 24f : 13f;
                float t = 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime);
                _position = Vector3.Lerp(_position, targetPosition, t);
                _rotation = Quaternion.Slerp(_rotation, targetRotation, t);
            }

            RenderFeed();
        }

        private static Vector3 FlatForward(Vector3 forward, Vector3 fallback)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = fallback;
                forward.y = 0f;
            }
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            return forward.normalized;
        }

        /// <summary>
        /// Pulls the third-person camera in when a wall is between it and the
        /// player, so the feed does not end up inside geometry.
        /// </summary>
        private float WallDistance(Vector3 focus, Vector3 back, float desired)
        {
            int count = Physics.RaycastNonAlloc(
                focus, back, _wallHits, desired, ~0, QueryTriggerInteraction.Ignore);

            float distance = desired;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _wallHits[i].collider;
                if (collider == null) continue;

                // Ignore the monkey's own colliders.
                if (_target != null && _wallHits[i].transform != null &&
                    _wallHits[i].transform.IsChildOf(_target.transform))
                    continue;

                distance = Mathf.Min(distance, Mathf.Max(MinWallDistance, _wallHits[i].distance - 0.2f));
            }

            return distance;
        }

        private void RenderFeed()
        {
            if (_camera == null || _texture == null) return;

            _camera.transform.SetPositionAndRotation(_position, _rotation);

            // In VR the menu canvas is part of the world. Hide it for the one
            // manual render so the feed cannot capture its own RawImage (which
            // would create a feedback loop). It is re-enabled in the same frame.
            bool hideCanvas = _menuCanvas != null &&
                _menuCanvas.renderMode == RenderMode.WorldSpace &&
                _menuCanvas.enabled;

            if (hideCanvas) _menuCanvas.enabled = false;

            try
            {
                _camera.targetTexture = _texture;
                _camera.Render();
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade spectate render failed: " + exception.Message);
            }
            finally
            {
                if (_camera != null) _camera.targetTexture = null;
                if (hideCanvas) _menuCanvas.enabled = true;
            }
        }
    }
}
