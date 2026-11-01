using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;
using CascadeMenu.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Thin, clientâ€‘side playerâ€‘targeting â€œgunâ€.
    /// Works from the right hand in VR and from the mouse/view camera on PC.
    /// The beam can stick to a player briefly; the trigger runs the configured
    /// module (Select, Mark, Copy Name).  All effects are local/visual only.
    /// </summary>
    [DefaultExecutionOrder(10005)]
    internal sealed class GunLibController : MonoBehaviour
    {
        private const float MaxDistance = 30f;
        private const float StickySeconds = 0.35f;
        private const float BeamStartWidth = 0.006f;
        private const float BeamEndWidth = 0.0025f;
        private const float RingWidth = 0.01f;

        // How far from the beam a player rig still counts as "under the gun".
        // Remote rigs often have trigger-only or missing colliders, so a pure
        // raycast can pass straight through a gorilla. This corridor makes the
        // beam grab the rig itself instead of relying on physics colliders.
        private const float AimAssistRadius = 0.55f;

        internal static GunLibController Instance { get; private set; }
        internal static string SelectedTargetName { get; private set; } = "None";
        internal static VRRig SelectedRig { get; private set; }
        internal static VRRig MarkedRig { get; private set; }
        internal static Action<RaycastHit, VRRig, NetPlayer> ActiveGunAction { get; private set; }
        internal static float ActiveGunCooldown { get; private set; } = 0.25f;
        private static float _lastFireTime;

        internal static void SetActiveGun(Action<RaycastHit, VRRig, NetPlayer> action, float cooldown = 0.25f)
        {
            ActiveGunAction = action;
            ActiveGunCooldown = Mathf.Max(0.05f, cooldown);
        }

        internal static void ClearActiveGun()
        {
            ActiveGunAction = null;
            ActiveGunCooldown = 0.25f;
        }

        internal static void CycleColor()
        {
            if (Instance != null && Instance._config != null)
            {
                Instance._config.AccentIndex.Value = (Instance._config.AccentIndex.Value + 1) % CascadeTheme.AccentNames.Length;
                Instance.RefreshAccent();
            }
        }

        internal static NetPlayer GetPlayerFromRig(VRRig rig)
        {
            if (rig == null) return null;
            if (rig.Creator != null) return rig.Creator;
            var containers = VRRigCache.ActiveRigContainers;
            if (containers != null)
            {
                for (int i = 0; i < containers.Count; i++)
                {
                    var c = containers[i];
                    if (c != null && c.Rig == rig) return c.Creator;
                }
            }
            return null;
        }

        /// <summary>Mark and select a player from the menu.</summary>
        internal static void MarkTarget(VRRig rig)
        {
            if (rig == null) return;
            MarkedRig = rig;
            SelectedRig = rig;
            SelectedTargetName = GetRigName(rig);
        }

        /// <summary>Select a player without drawing the ring.</summary>
        internal static void SelectTarget(VRRig rig)
        {
            if (rig == null) return;
            SelectedRig = rig;
            SelectedTargetName = GetRigName(rig);
        }

        private MenuConfig _config;
        private LineRenderer _beam;
        private LineRenderer _ring;
        private Material _material;
        private VRRig _lockedRig;
        private float _lockUntil;
        private bool _wasTrigger;
        private Color _accent = new Color(0.3f, 0.7f, 1f, 0.9f);
        private static readonly RaycastHit[] _raycastHits = new RaycastHit[64];
        // IgnoreRaycast/UI layers are excluded, and triggers are ignored below.
        private LayerMask _raycastMask = Physics.DefaultRaycastLayers;

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            Instance = this;
            BuildVisuals();
            RefreshAccent();
            Plugin.Log?.LogInfo("Cascade gun ready. Enabled=" + config.GunEnabled.Value +
                                " Module=" + config.GunModule.Value);
        }

        private void BuildVisuals()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Plugin.Log?.LogWarning("Cascade gun: no usable shader found.");
                return;
            }

            _material = new Material(shader) { color = Color.white };
            _material.renderQueue = 4100;

            var beamObject = new GameObject("CascadeGunBeam");
            beamObject.transform.SetParent(transform, false);
            _beam = beamObject.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.startWidth = BeamStartWidth;
            _beam.endWidth = BeamEndWidth;
            _beam.numCapVertices = 4;
            _beam.numCornerVertices = 2;
            _beam.alignment = LineAlignment.View;
            _beam.shadowCastingMode = ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            _beam.material = _material;
            _beam.sortingOrder = 330;
            _beam.enabled = false;

            var ringObject = new GameObject("CascadeGunTargetRing");
            ringObject.transform.SetParent(transform, false);
            _ring = ringObject.AddComponent<LineRenderer>();
            _ring.positionCount = 32;
            _ring.loop = true;
            _ring.useWorldSpace = true;
            _ring.startWidth = RingWidth;
            _ring.endWidth = RingWidth;
            _ring.numCapVertices = 4;
            _ring.numCornerVertices = 4;
            _ring.alignment = LineAlignment.View;
            _ring.shadowCastingMode = ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.material = _material;
            _ring.sortingOrder = 331;
            _ring.enabled = false;
        }

        private void RefreshAccent()
        {
            if (_config == null) return;
            CascadeTheme theme = CascadeTheme.Build(_config.ThemeMode, _config.AccentIndex.Value);
            _accent = theme.Accent;
            _accent.a = 0.9f;
        }

        private void LateUpdate()
        {
            if (_config == null || _beam == null || _ring == null) return;

            // Do not fight the menu pointer. Also refresh the accent occasionally
            // when the configured theme changes.
            bool menuOpen = CascadeMenuBehaviour.IsMenuOpen;
            bool show = (_config.GunEnabled.Value || ActiveGunAction != null) && !menuOpen;
            if (!show)
            {
                if (_beam != null) _beam.enabled = false;
                _wasTrigger = true;

                // The mark ring stays visible while the menu is open so the
                // "Mark with gun" action gives instant feedback.
                if (_config.GunShowMarker.Value && MarkedRig != null && IsRigAlive(MarkedRig))
                    UpdateRing(MarkedRig);
                else if (_ring != null)
                    _ring.enabled = false;
                return;
            }

            RefreshAccent();

            Vector3 origin;
            Vector3 direction;
            if (!GetAimRay(out origin, out direction))
            {
                HideVisuals();
                return;
            }

            _beam.enabled = true;
            _beam.startColor = _accent;
            _beam.endColor = new Color(_accent.r, _accent.g, _accent.b, 0.25f);

            VRRig currentRig = null;
            RaycastHit solidHit = default;
            bool solidFound = FindNearestHit(origin, direction, out solidHit);

            // Walls/props clamp how far a player can still be targeted, so a
            // gorilla behind a wall is never selected.
            float solidDistance = solidFound ? solidHit.distance : MaxDistance;

            RaycastHit fireHit = solidHit;
            Vector3 hitPoint = solidFound ? solidHit.point : origin + direction * MaxDistance;
            float hitDistance = solidDistance;

            if (solidFound)
                currentRig = GetRemoteRig(solidHit.collider);

            // Rig proximity pass. This is what makes the gun actually connect:
            // remote rig colliders can be triggers (ignored by the solid pass)
            // or absent entirely.
            RaycastHit rigHit;
            VRRig nearRig = FindRigAlongBeam(origin, direction, solidDistance + 0.05f, out rigHit);
            if (nearRig != null)
            {
                currentRig = nearRig;
                hitPoint = rigHit.point;
                hitDistance = rigHit.distance;

                // Keep a real collider for modules that need a surface, but end
                // the beam on the player instead of the wall behind them.
                fireHit.point = rigHit.point;
                fireHit.distance = rigHit.distance;
                fireHit.normal = -direction;
            }

            if (currentRig != null && !IsRigAlive(currentRig)) currentRig = null;

            if (solidFound || nearRig != null)
            {
                fireHit.point = hitPoint;
                fireHit.distance = hitDistance;
            }

            if (currentRig != null)
            {
                _lockedRig = currentRig;
                _lockUntil = Time.time + StickySeconds;
            }
            else if (!_config.GunSticky.Value || Time.time > _lockUntil)
            {
                _lockedRig = null;
            }

            // The beam stops on the player when a rig is under it, otherwise on
            // the first wall/prop (or at max range).
            _beam.SetPosition(0, GetBeamStart(origin));
            _beam.SetPosition(1, hitPoint);

            // Trigger handling
            bool trigger = IsTriggerHeld();
            float cooldown = ActiveGunAction != null ? ActiveGunCooldown : 0.25f;
            if (trigger)
            {
                if (!_wasTrigger || Time.time >= _lastFireTime + cooldown)
                {
                    _lastFireTime = Time.time;
                    VRRig targetRig = currentRig != null ? currentRig : (_config.GunSticky.Value ? _lockedRig : null);
                    Fire(fireHit, targetRig);
                }
            }
            _wasTrigger = trigger;

            // Target ring
            VRRig ringRig = _lockedRig != null ? _lockedRig : MarkedRig;
            if (_config.GunShowMarker.Value && ringRig != null && IsRigAlive(ringRig))
            {
                UpdateRing(ringRig);
            }
            else
            {
                _ring.enabled = false;
            }
        }

        private void Fire(RaycastHit hit, VRRig rig)
        {
            NetPlayer player = GetPlayerFromRig(rig);

            if (ActiveGunAction != null)
            {
                try
                {
                    ActiveGunAction(hit, rig, player);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning("Gun action error: " + ex);
                }
                return;
            }

            if (rig == null)
            {
                Plugin.Log?.LogInfo("Cascade gun: no player under the beam.");
                return;
            }

            string name = GetRigName(rig);
            int module = Mathf.Clamp(_config.GunModule.Value, 0, 2);

            switch (module)
            {
                case 0: // Select
                    SelectedRig = rig;
                    SelectedTargetName = name;
                    Plugin.Log?.LogInfo("Cascade gun selected player: " + name);
                    break;

                case 1: // Mark
                    MarkedRig = rig;
                    SelectedRig = rig;
                    SelectedTargetName = name;
                    Plugin.Log?.LogInfo("Cascade gun marked player: " + name);
                    break;

                case 2: // Copy Name
                    GUIUtility.systemCopyBuffer = name;
                    Plugin.Log?.LogInfo("Cascade gun copied player name: " + name);
                    break;
            }
        }

        private bool IsTriggerHeld()
        {
            if (XRSettings.isDeviceActive)
            {
                ControllerInputPoller poller = ControllerInputPoller.instance;
                return poller != null &&
                       (poller.rightControllerIndexFloat > 0.55f || poller.rightControllerTriggerButton);
            }

            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private bool GetAimRay(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            Transform hand = GorillaTagger.Instance != null ? GorillaTagger.Instance.rightHandTransform : null;

            // VR aims straight down the controller.
            if (XRSettings.isDeviceActive && hand != null)
            {
                origin = hand.position + hand.forward * 0.05f;
                direction = hand.forward;
                return true;
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();

            // Desktop: the mouse/crosshair camera ray is the authoritative aim
            // ray. Casting from the hand here would parallax away from what the
            // player is actually pointing at.
            if (camera != null && Mouse.current != null)
            {
                Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                origin = ray.origin + ray.direction * 0.15f;
                direction = ray.direction;
                return true;
            }

            if (hand != null)
            {
                origin = hand.position;
                direction = hand.forward;
                return true;
            }

            if (camera == null) return false;
            origin = camera.transform.position + camera.transform.forward * 0.2f;
            direction = camera.transform.forward;
            return true;
        }

        /// <summary>
        /// Where the visible beam starts. VR fires from the controller, so the
        /// beam starts there; on desktop the aim ray comes from the camera but
        /// the beam is still drawn from the hand for readability.
        /// </summary>
        private static Vector3 GetBeamStart(Vector3 aimOrigin)
        {
            if (XRSettings.isDeviceActive) return aimOrigin;

            Transform hand = GorillaTagger.Instance != null ? GorillaTagger.Instance.rightHandTransform : null;
            return hand != null ? hand.position : aimOrigin;
        }

        /// <summary>
        /// Nearest solid surface the beam can touch: walls, props and any rig
        /// collider count; non-player triggers do not. The local player's own
        /// body is always filtered out.
        /// </summary>
        private bool FindNearestHit(Vector3 origin, Vector3 direction, out RaycastHit best)
        {
            best = default;
            bool found = false;
            float nearest = float.MaxValue;

            int hitCount = Physics.RaycastNonAlloc(
                origin, direction, _raycastHits, MaxDistance, _raycastMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _raycastHits[i];
                if (candidate.collider == null) continue;
                if (candidate.distance >= nearest) continue;
                if (IsLocalPlayerCollider(candidate.collider)) continue;

                // Pickup/zone triggers should not stop the beam, but a remote
                // rig's trigger colliders are valid targets.
                if (candidate.collider.isTrigger && GetRemoteRig(candidate.collider) == null)
                    continue;

                nearest = candidate.distance;
                best = candidate;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Finds the nearest remote rig whose body sits inside a small corridor
        /// around the aim ray. This is the fallback that makes the gun connect
        /// even when a rig has no raycastable colliders at all. The result is
        /// clamped to <paramref name="maxDistance"/>, so walls still block.
        /// </summary>
        private static VRRig FindRigAlongBeam(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            VRRig bestRig = null;
            float bestDistance = float.MaxValue;
            Vector3 bestPoint = origin;

            var containers = VRRigCache.ActiveRigContainers;
            if (containers == null) return null;

            for (int i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                if (container == null) continue;

                VRRig rig = container.Rig;
                if (!IsRigAlive(rig) || rig == VRRig.LocalRig) continue;

                float scale = rig.scaleFactor > 0.01f ? rig.scaleFactor : 1f;
                float radius = AimAssistRadius * scale;
                Vector3 basePos = rig.transform.position;

                // Use the rig's head when available; otherwise estimate it. The
                // three samples cover feet/torso/head regardless of where the
                // rig root sits.
                Vector3 head = rig.headConstraint != null
                    ? rig.headConstraint.position
                    : basePos + Vector3.up * (1.5f * scale);
                Vector3 mid = Vector3.Lerp(basePos, head, 0.5f);

                Vector3 sample = basePos;
                if (TryRayCorridor(origin, direction, sample, radius, maxDistance, out float along, out Vector3 point) &&
                    along < bestDistance)
                {
                    bestRig = rig;
                    bestDistance = along;
                    bestPoint = point;
                }

                sample = mid;
                if (TryRayCorridor(origin, direction, sample, radius, maxDistance, out along, out point) &&
                    along < bestDistance)
                {
                    bestRig = rig;
                    bestDistance = along;
                    bestPoint = point;
                }

                sample = head;
                if (TryRayCorridor(origin, direction, sample, radius, maxDistance, out along, out point) &&
                    along < bestDistance)
                {
                    bestRig = rig;
                    bestDistance = along;
                    bestPoint = point;
                }
            }

            if (bestRig == null) return null;

            hit.point = bestPoint;
            hit.distance = bestDistance;
            hit.normal = -direction;
            return bestRig;
        }

        private static bool TryRayCorridor(Vector3 origin, Vector3 direction, Vector3 sample,
            float radius, float maxDistance, out float along, out Vector3 point)
        {
            along = Vector3.Dot(sample - origin, direction);
            point = origin;

            if (along < 0.15f || along > maxDistance) return false;

            point = origin + direction * along;
            return Vector3.Distance(point, sample) <= radius;
        }

        private static VRRig GetRemoteRig(Collider collider)
        {
            if (collider == null) return null;
            VRRig rig = collider.GetComponentInParent<VRRig>();
            if (rig == null || rig == VRRig.LocalRig) return null;
            return rig;
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

        private static bool IsRigAlive(VRRig rig)
        {
            return rig != null && rig.gameObject != null && rig.gameObject.activeInHierarchy;
        }

        private static string GetRigName(VRRig rig)
        {
            if (rig == null) return "Unknown";
            string name = rig.playerNameVisible;
            return string.IsNullOrEmpty(name) ? "Player" : name;
        }

        private static Vector3 GetRigCenter(VRRig rig)
        {
            if (rig == null) return Vector3.zero;
            return rig.transform.position + Vector3.up * 1.2f;
        }

        private void UpdateRing(VRRig rig)
        {
            Vector3 center = GetRigCenter(rig);
            float radius = 0.38f;

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.forward;

            int count = _ring.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 point = center +
                                right * (Mathf.Cos(angle) * radius) +
                                up * (Mathf.Sin(angle) * radius);
                _ring.SetPosition(i, point);
            }
            _ring.startColor = _accent;
            _ring.endColor = _accent;
            _ring.enabled = true;
        }

        private void HideVisuals()
        {
            if (_beam != null) _beam.enabled = false;
            if (_ring != null) _ring.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_material != null) Destroy(_material);
        }
    }
}
