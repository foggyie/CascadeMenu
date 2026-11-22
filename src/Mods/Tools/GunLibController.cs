using System;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;
using CascadeMenu.UI;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Tools
{
    /// <summary>
    /// Thin, client-side player-targeting "gun".
    ///
    /// Job 4 keeps the original beam / ring look and firing adapters, then adds:
    /// - Optional grip gating (VR grip, configurable PC mouse/key stand-in).
    /// - Selectable grip hand and beam hand.
    /// - Config-driven beam width, ring size, max distance and colors.
    /// - A non-allocating raycast that stops on everything solid: world
    ///   geometry, props and construction pieces, whether or not the collider
    ///   belongs to a visible renderer. Triggers are still ignored.
    ///
    /// The beam end and the target ring both use the same accepted hit point.
    /// Disabled or grip-released states return before the aim ray and raycast,
    /// so an idle gun costs zero physics work.
    /// </summary>
    [DefaultExecutionOrder(10005)]
    internal sealed class GunLibController : MonoBehaviour
    {
        private const float StickySeconds = 0.35f;
        private const float AimAssistRadius = 0.55f;

        internal enum HandSide
        {
            Right = 0,
            Left = 1
        }

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
            if (Instance == null || Instance._config == null) return;

            int count = CascadeThemePreset.All.Length;
            int next = (Instance._config.GunAccentIndex.Value + 1) % count;
            Instance._config.GunAccentIndex.Value = next;

            CascadeThemePalette palette = CascadeThemePreset.All[next].Palette;
            Color accent = palette[(int)CascadePaletteIndex.Accent];
            string hex = "#" + ColorUtility.ToHtmlStringRGB(accent);
            Instance._config.GunUseThemeColor.Value = false;
            Instance._config.GunBeamColor.Value = hex;
            Instance._config.GunRingColor.Value = hex;
            Instance.RefreshColors(true);
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
        private bool _wasTrigger = true;
        private bool _hasHit;
        private Vector3 _lastHitPoint;

        private Color _beamColor = new Color(0.3f, 0.7f, 1f, 0.9f);
        private Color _ringColor = new Color(0.3f, 0.7f, 1f, 0.95f);
        private int _lastThemeVersion = int.MinValue;
        private string _lastBeamHex;
        private string _lastRingHex;
        private bool _lastUseTheme;
        private float _lastBeamWidth = -1f;
        private float _lastRingWidth = -1f;

        private static readonly RaycastHit[] _raycastHits = new RaycastHit[64];
        // DefaultRaycastLayers minus UI. Triggers are ignored at the query level.
        private LayerMask _raycastMask = Physics.DefaultRaycastLayers;

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            Instance = this;
            _raycastMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("UI");
            BuildVisuals();
            RefreshColors(true);
            ProfilerStats.Register(ProfilerStats.GunLibKey, "GunLib",
                "Aim ray, solid-surface raycast, beam and ring");
            Plugin.Log?.LogInfo("Cascade gun ready. Enabled=" + config.GunEnabled.Value +
                                " RequireGrip=" + config.GunRequireGrip.Value +
                                " Module=" + config.GunModule.Value);
        }

        private void BuildVisuals()
        {
            Shader shader = Shader.Find("GUI/Text Shader");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Plugin.Log?.LogWarning("Cascade gun: no usable shader found.");
                return;
            }

            _material = new Material(shader) { color = Color.white };
            _material.renderQueue = 4100;
            if (_material.HasProperty("_ZTest"))
                _material.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (_material.HasProperty("_ZWrite"))
                _material.SetFloat("_ZWrite", 0f);
            if (_material.HasProperty("_Cull"))
                _material.SetFloat("_Cull", (float)CullMode.Off);
            if (_material.HasProperty("_SrcBlend"))
                _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_material.HasProperty("_DstBlend"))
                _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

            var beamObject = new GameObject("CascadeGunBeam");
            beamObject.transform.SetParent(transform, false);
            _beam = beamObject.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
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
            _ring.numCapVertices = 4;
            _ring.numCornerVertices = 4;
            _ring.alignment = LineAlignment.View;
            _ring.shadowCastingMode = ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.material = _material;
            _ring.sortingOrder = 331;
            _ring.enabled = false;
        }

        /// <summary>
        /// Rebuilds the shared beam / ring colors when the theme version or a
        /// configured hex changes. Called every frame but only does work when
        /// one of those inputs actually changed.
        /// </summary>
        private void RefreshColors(bool force)
        {
            if (_config == null) return;

            CascadeTheme theme = CascadeTheme.Active;
            int version = theme != null ? theme.Version : -1;
            string beamHex = _config.GunBeamColor.Value ?? string.Empty;
            string ringHex = _config.GunRingColor.Value ?? string.Empty;
            bool useTheme = _config.GunUseThemeColor.Value;

            if (!force && version == _lastThemeVersion &&
                string.Equals(beamHex, _lastBeamHex, StringComparison.Ordinal) &&
                string.Equals(ringHex, _lastRingHex, StringComparison.Ordinal) &&
                useTheme == _lastUseTheme)
            {
                return;
            }

            _lastThemeVersion = version;
            _lastBeamHex = beamHex;
            _lastRingHex = ringHex;
            _lastUseTheme = useTheme;

            Color beam;
            Color ring;

            if (useTheme && theme != null)
            {
                beam = theme.Accent;
                ring = theme.Accent;
            }
            else
            {
                if (!ColorUtility.TryParseHtmlString(beamHex, out beam))
                    beam = new Color(0.3f, 0.7f, 1f, 1f);
                if (!ColorUtility.TryParseHtmlString(ringHex, out ring))
                    ring = beam;
            }

            beam.a = 0.9f;
            ring.a = 0.95f;
            _beamColor = beam;
            _ringColor = ring;

            if (_beam != null)
            {
                _beam.startColor = beam;
                Color tail = beam;
                tail.a = 0.25f;
                _beam.endColor = tail;
            }
            if (_ring != null)
            {
                _ring.startColor = ring;
                _ring.endColor = ring;
            }
        }

        private void ApplyWidths()
        {
            if (_config == null) return;

            float beamWidth = Mathf.Clamp(_config.GunBeamWidth.Value, 0.001f, 0.03f);
            if (_beam != null && !Mathf.Approximately(beamWidth, _lastBeamWidth))
            {
                _lastBeamWidth = beamWidth;
                _beam.startWidth = beamWidth;
                _beam.endWidth = Mathf.Max(0.0008f, beamWidth * 0.42f);
            }

            float ringRadius = Mathf.Clamp(_config.GunRingSize.Value, 0.12f, 0.9f);
            float ringWidth = Mathf.Max(0.004f, ringRadius * 0.026f);
            if (_ring != null && !Mathf.Approximately(ringWidth, _lastRingWidth))
            {
                _lastRingWidth = ringWidth;
                _ring.startWidth = ringWidth;
                _ring.endWidth = ringWidth;
            }
        }

        private void LateUpdate()
        {
            if (_config == null || _beam == null || _ring == null) return;

            GunLibMod mod = GunLibMod.Instance;
            bool armed = mod != null ? mod.Enabled : _config.GunEnabled.Value;

            // The generic mod settings page and the keybind path can change the
            // wrapper's Enabled flag without going through this controller.
            // Reconcile the persisted value so the next launch restores it.
            if (mod != null && mod.Enabled != _config.GunEnabled.Value)
            {
                _config.GunEnabled.Value = mod.Enabled;
            }

            bool active = mod != null ? mod.IsActive : _config.GunActive.Value;
            bool show = (armed && active) || ActiveGunAction != null;

            bool requireGrip = _config.GunRequireGrip.Value;
            HandSide gripHand = ResolveConfiguredGripHand();
            bool gripHeld = !requireGrip || IsGripHeld(out gripHand);
            bool menuOpen = CascadeMenuBehaviour.IsMenuOpen;

            // Releasing grip (or disabling, or opening the menu) must clean up
            // the beam, the ring and any pending hit immediately.
            if (!show || menuOpen || !gripHeld)
            {
                HideVisuals();
                ClearPending();

                // The marked ring is still useful while browsing the menu, but
                // only when the gun is not grip-gated.
                if (!requireGrip && menuOpen && _config.GunShowMarker.Value &&
                    MarkedRig != null && IsRigAlive(MarkedRig))
                {
                    ApplyWidths();
                    UpdateRing(GetRigCenter(MarkedRig), _config.GunRingSize.Value);
                }

                ProfilerStats.Record(ProfilerStats.GunLibKey, 0.0, false);
                return;
            }

            long start = ProfilerStats.Timestamp;

            RefreshColors(false);
            ApplyWidths();

            if (!gripHeld)
            {
                // Defensive: the branch above already covers this.
                HideVisuals();
                ClearPending();
                ProfilerStats.Record(ProfilerStats.GunLibKey, ProfilerStats.ElapsedMs(start), false);
                return;
            }

            HandSide beamHand = ResolveBeamHand(requireGrip ? gripHand : ResolveConfiguredGripHand());
            Vector3 origin;
            Vector3 direction;
            if (!GetAimRay(beamHand, out origin, out direction))
            {
                HideVisuals();
                ClearPending();
                ProfilerStats.Record(ProfilerStats.GunLibKey, ProfilerStats.ElapsedMs(start), false);
                return;
            }

            float maxDistance = Mathf.Clamp(_config.GunMaxDistance.Value, 3f, 80f);
            bool beamVisible = _config.GunBeamEnabled.Value;
            _beam.enabled = beamVisible;

            VRRig currentRig = null;
            RaycastHit solidHit = default;

            // The beam always collides with the world. Skipping this query made
            // the beam run straight through floors, walls and props whenever
            // the active gun did not ask for a surface hit.
            bool solidFound = FindNearestHit(origin, direction, maxDistance, out solidHit);

            // Walls/props clamp how far a player can still be targeted.
            float solidDistance = solidFound ? solidHit.distance : maxDistance;

            RaycastHit fireHit = solidHit;
            Vector3 hitPoint = solidFound ? solidHit.point : origin + direction * maxDistance;
            float hitDistance = solidDistance;

            if (solidFound) currentRig = GetRemoteRig(solidHit.collider);

            // Remote rig colliders are often triggers or missing, so the
            // proximity pass remains the reliable player targeter.
            RaycastHit rigHit;
            VRRig nearRig = FindRigAlongBeam(origin, direction, solidDistance + 0.05f, out rigHit);
            if (nearRig != null)
            {
                currentRig = nearRig;
                hitPoint = rigHit.point;
                hitDistance = rigHit.distance;

                fireHit.point = rigHit.point;
                fireHit.distance = rigHit.distance;
                fireHit.normal = -direction;
            }

            if (currentRig != null && !IsRigAlive(currentRig)) currentRig = null;

            fireHit.point = hitPoint;
            fireHit.distance = hitDistance;
            if (!solidFound && nearRig == null)
            {
                fireHit.normal = Vector3.up;
            }

            _hasHit = solidFound || nearRig != null;
            _lastHitPoint = hitPoint;

            if (currentRig != null)
            {
                _lockedRig = currentRig;
                _lockUntil = Time.time + StickySeconds;
            }
            else if (!_config.GunSticky.Value || Time.time > _lockUntil)
            {
                _lockedRig = null;
            }

            if (beamVisible)
            {
                _beam.SetPosition(0, GetBeamStart(origin, beamHand));
                _beam.SetPosition(1, hitPoint);
            }

            // Trigger handling. Grip is a separate input, so a grip press never
            // reaches this path, the menu or another mod's bind.
            bool trigger = IsTriggerHeld(beamHand);
            float cooldown = ActiveGunAction != null ? ActiveGunCooldown : 0.25f;
            if (trigger && (!_wasTrigger || Time.time >= _lastFireTime + cooldown))
            {
                _lastFireTime = Time.time;
                VRRig targetRig = currentRig != null
                    ? currentRig
                    : (_config.GunSticky.Value ? _lockedRig : null);
                Fire(fireHit, targetRig);
            }
            _wasTrigger = trigger;

            if (_config.GunShowMarker.Value)
            {
                VRRig ringRig = _lockedRig != null ? _lockedRig : MarkedRig;
                if (ringRig != null && IsRigAlive(ringRig))
                {
                    // When the ring belongs to the current target, center it on
                    // the exact accepted hit point so the beam and ring agree.
                    Vector3 ringPoint = currentRig != null && ringRig == currentRig
                        ? hitPoint
                        : GetRigCenter(ringRig);
                    UpdateRing(ringPoint, _config.GunRingSize.Value);
                }
                else
                {
                    // Always draw the aiming ring at the beam tip so PC and VR
                    // users have clear visual feedback where the gun is aiming.
                    UpdateRing(hitPoint, _config.GunRingSize.Value * 0.55f);
                }
            }
            else
            {
                _ring.enabled = false;
            }

            ProfilerStats.Record(ProfilerStats.GunLibKey, ProfilerStats.ElapsedMs(start), true);
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

        // -----------------------------------------------------------------
        // Input
        // -----------------------------------------------------------------

        private HandSide ResolveConfiguredGripHand()
        {
            return _config != null && _config.GunGripHand.Value == 1
                ? HandSide.Left
                : HandSide.Right;
        }

        private HandSide ResolveBeamHand(HandSide gripHand)
        {
            if (_config == null) return HandSide.Right;

            int mode = _config.GunBeamHand.Value;
            if (mode == 1) return HandSide.Left;
            if (mode == 2) return gripHand;
            return HandSide.Right;
        }

        /// <summary>
        /// VR reads ControllerInputPoller's grip floats. On desktop the
        /// configured mouse button or key stands in for grip, so a grip press
        /// never touches the menu's click input.
        /// </summary>
        private bool IsGripHeld(out HandSide hand)
        {
            hand = ResolveConfiguredGripHand();

            if (XRSettings.isDeviceActive)
            {
                ControllerInputPoller poller = ControllerInputPoller.instance;
                if (poller == null) return false;

                bool right = poller.rightControllerGripFloat > 0.5f;
                bool left = poller.leftControllerGripFloat > 0.5f;

                switch (_config.GunGripHand.Value)
                {
                    case 1:
                        hand = HandSide.Left;
                        return left;
                    case 2:
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
                    default:
                        hand = HandSide.Right;
                        return right;
                }
            }

            return IsPcGripHeld();
        }

        private bool IsPcGripHeld()
        {
            if (_config == null) return false;

            switch (_config.GunPcGrip.Value)
            {
                case 1:
                    return Mouse.current != null && Mouse.current.leftButton.isPressed;
                case 2:
                    return Mouse.current != null && Mouse.current.middleButton.isPressed;
                case 3:
                    Key key = Keybinds.Parse(_config.GunPcGripKey.Value, Key.LeftAlt);
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard == null || key == Key.None) return false;
                    return keyboard[key] != null && keyboard[key].isPressed;
                default:
                    return Mouse.current != null && Mouse.current.rightButton.isPressed;
            }
        }

        private bool IsTriggerHeld(HandSide hand)
        {
            if (XRSettings.isDeviceActive)
            {
                ControllerInputPoller poller = ControllerInputPoller.instance;
                if (poller == null) return false;

                if (hand == HandSide.Left)
                {
                    return poller.leftControllerIndexFloat > 0.55f ||
                           poller.leftControllerTriggerButton;
                }

                return poller.rightControllerIndexFloat > 0.55f ||
                       poller.rightControllerTriggerButton;
            }

            if (Mouse.current == null) return false;

            // If PC grip is configured as Left Mouse (1), fire with Right Mouse
            // so holding grip does not immediately fire on the same button.
            if (_config != null && _config.GunPcGrip.Value == 1)
            {
                return Mouse.current.rightButton.isPressed;
            }

            return Mouse.current.leftButton.isPressed;
        }

        private bool GetAimRay(HandSide hand, out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            Transform handTransform = ResolveHandTransform(hand);

            // VR aims straight down the selected controller.
            if (XRSettings.isDeviceActive && handTransform != null)
            {
                origin = handTransform.position + handTransform.forward * 0.05f;
                direction = handTransform.forward;
                return true;
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();

            // Desktop: the mouse/crosshair camera ray is the authoritative aim
            // ray. Casting from the hand would parallax away from the pointer.
            if (camera != null)
            {
                Vector2 screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (Mouse.current != null && Cursor.lockState != CursorLockMode.Locked)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    if (mousePos.x >= 0f && mousePos.x <= Screen.width &&
                        mousePos.y >= 0f && mousePos.y <= Screen.height)
                    {
                        screenPos = mousePos;
                    }
                }
                Ray ray = camera.ScreenPointToRay(screenPos);
                origin = ray.origin + ray.direction * 0.15f;
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

        /// <summary>
        /// Where the visible beam starts. VR starts at the selected controller;
        /// on desktop the aim ray comes from the camera but the beam is still
        /// drawn from the chosen hand for readability.
        /// </summary>
        private Vector3 GetBeamStart(Vector3 aimOrigin, HandSide hand)
        {
            if (XRSettings.isDeviceActive) return aimOrigin;

            Transform handTransform = ResolveHandTransform(hand);
            if (handTransform != null && handTransform.gameObject.activeInHierarchy)
            {
                return handTransform.position;
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            if (camera != null)
            {
                float side = hand == HandSide.Left ? -0.18f : 0.18f;
                return camera.transform.position +
                       camera.transform.right * side -
                       camera.transform.up * 0.14f +
                       camera.transform.forward * 0.25f;
            }

            return aimOrigin;
        }

        private static Transform ResolveHandTransform(HandSide hand)
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null) return null;
            return hand == HandSide.Left ? tagger.leftHandTransform : tagger.rightHandTransform;
        }

        // -----------------------------------------------------------------
        // Raycast (visible surfaces only)
        // -----------------------------------------------------------------

        /// <summary>
        /// Nearest solid surface the beam can touch. Triggers are ignored by
        /// the physics query; anything with an enabled collider counts, so
        /// invisible blockers and props without a renderer still stop the beam.
        /// The array and mask are reused, so this allocates nothing.
        /// </summary>
        private bool FindNearestHit(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit best)
        {
            best = default;
            bool found = false;
            float nearest = float.MaxValue;

            int mask = _raycastMask;
            if (GTPlayer.Instance != null && GTPlayer.Instance.locomotionEnabledLayers.value != 0)
            {
                mask |= GTPlayer.Instance.locomotionEnabledLayers.value;
                mask &= ~LayerMask.GetMask("UI", "Ignore Raycast");
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin, direction, _raycastHits, maxDistance, mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _raycastHits[i];
                if (candidate.collider == null) continue;
                if (candidate.distance >= nearest) continue;
                if (IsLocalPlayerCollider(candidate.collider)) continue;
                if (!IsSolidCollider(candidate.collider)) continue;

                nearest = candidate.distance;
                best = candidate;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Everything physical blocks the gun. Only disabled colliders,
        /// triggers and overlay-only renderers (beams and trails) are skipped,
        /// so construction pieces and invisible world blockers behave like
        /// normal solid geometry.
        /// </summary>
        private static bool IsSolidCollider(Collider collider)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) return false;
            if (collider.isTrigger) return false;

            Renderer renderer = FindRenderer(collider);
            if (renderer is LineRenderer || renderer is TrailRenderer) return false;

            return true;
        }

        private static Renderer FindRenderer(Collider collider)
        {
            Renderer renderer = collider.GetComponentInParent<Renderer>();
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                return renderer;

            renderer = collider.GetComponentInChildren<Renderer>();
            return renderer;
        }

        /// <summary>
        /// Finds the nearest remote rig whose body sits inside a small corridor
        /// around the aim ray. This keeps the gun working when a rig has no
        /// raycastable colliders. The result is clamped to
        /// <paramref name="maxDistance"/>, so visible walls still block.
        /// </summary>
        private static VRRig FindRigAlongBeam(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            VRRig bestRig = null;
            float bestDistance = float.MaxValue;
            Vector3 bestPoint = origin;

            var rigs = PlayerActions.GetRemoteRigs();
            if (rigs == null || rigs.Count == 0) return null;

            for (int i = 0; i < rigs.Count; i++)
            {
                VRRig rig = rigs[i];
                if (!IsRigAlive(rig) || rig == VRRig.LocalRig) continue;
                if (!IsRigVisible(rig)) continue;

                float scale = rig.scaleFactor > 0.01f ? rig.scaleFactor : 1f;
                float radius = AimAssistRadius * scale;
                Vector3 basePos = rig.transform.position;

                Vector3 head = rig.headConstraint != null
                    ? rig.headConstraint.position
                    : basePos + Vector3.up * (1.5f * scale);
                Vector3 mid = Vector3.Lerp(basePos, head, 0.5f);

                float along;
                Vector3 point;

                if (TryRayCorridor(origin, direction, basePos, radius, maxDistance, out along, out point) &&
                    along < bestDistance)
                {
                    bestRig = rig;
                    bestDistance = along;
                    bestPoint = point;
                }

                if (TryRayCorridor(origin, direction, mid, radius, maxDistance, out along, out point) &&
                    along < bestDistance)
                {
                    bestRig = rig;
                    bestDistance = along;
                    bestPoint = point;
                }

                if (TryRayCorridor(origin, direction, head, radius, maxDistance, out along, out point) &&
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

        private static bool IsRigVisible(VRRig rig)
        {
            if (rig == null) return false;

            bool any = false;
            if (rig.mainSkin != null)
            {
                any = true;
                if (rig.mainSkin.enabled) return true;
            }
            if (rig.faceSkin != null)
            {
                any = true;
                if (rig.faceSkin.enabled) return true;
            }
            if (rig.bodyRenderer != null && rig.bodyRenderer.ActiveBody != null)
            {
                any = true;
                if (rig.bodyRenderer.ActiveBody.enabled) return true;
            }

            // If the rig exposes no body renderer at all, do not hide it.
            return !any;
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
            if (rig.headConstraint != null) return rig.headConstraint.position;
            return rig.transform.position + Vector3.up * 1.2f;
        }

        /// <summary>
        /// Draws a camera-facing ring around an exact world point. The caller
        /// passes the accepted hit point for the current target, which is what
        /// keeps the beam end and the ring in the same place.
        /// </summary>
        private void UpdateRing(Vector3 center, float radius)
        {
            if (_ring == null) return;

            radius = Mathf.Clamp(radius, 0.12f, 0.9f);

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

            _ring.startColor = _ringColor;
            _ring.endColor = _ringColor;
            _ring.enabled = true;
        }

        private void HideVisuals()
        {
            if (_beam != null) _beam.enabled = false;
            if (_ring != null) _ring.enabled = false;
        }

        private void ClearPending()
        {
            _lockedRig = null;
            _lockUntil = 0f;
            _wasTrigger = true;
            _hasHit = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_material != null) Destroy(_material);
        }
    }
}
