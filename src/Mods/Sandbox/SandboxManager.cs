using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace CascadeMenu.Mods.Sandbox
{
    public enum SandboxToolMode
    {
        None = 0,
        PhysicsGun = 1,
        ConstructionPlacer = 2,
        Thruster = 3,
        Balloon = 4,
        Weld = 5,
        Remover = 6
    }

    public enum SandboxPropType
    {
        Crate,
        Barrel,
        BouncyBall,
        Ramp,
        Trampoline,
        ExplosiveBarrel,
        Ragdoll,
        HoverBalloon,
        // Construction blocks (solid, climbable Gorilla Tag colliders)
        WallBlock,
        FloorSlab,
        Pillar,
        Stairs,
        BridgePlank
    }

    /// <summary>
    /// PropForge all-in-one Sandbox & Construction engine.
    /// Uses Gorilla Tag's exact locomotion layer + GorillaSurfaceOverride so every
    /// solid sandbox & construction block is 100% climbable/standable, with trigger-safe,
    /// player-filtered raycasting for all tools and live ghost placement preview.
    /// </summary>
    internal sealed class SandboxManager : MonoBehaviour
    {
        private static SandboxManager _instance;

        internal static SandboxToolMode ActiveTool = SandboxToolMode.None;
        internal static SandboxPropType SelectedBuildBlock = SandboxPropType.WallBlock;
        internal static float PropScale = 1.0f;
        internal static bool SpawnFrozen = false;
        internal static bool SolidColliders = true;
        internal static bool SnapToGrid = true;
        internal static int ColorPreset = 0; // 0=Default, 1=Neon Cyan, 2=Hot Pink, 3=Sunset Gold, 4=Mint Green, 5=Random
        internal static bool SpawnAtRightHand = false;
        internal static float CustomBounciness = 0.25f;

        private readonly List<GameObject> _props = new List<GameObject>();
        private readonly List<SandboxThruster> _thrusters = new List<SandboxThruster>();

        private LineRenderer _beam;
        private GameObject _ghostPreview;
        private SandboxPropType _ghostType;
        private Rigidbody _grabbedBody;
        private float _grabDistance = 2.0f;
        private bool _prevTrigger;
        private bool _prevPrimary;
        private Rigidbody _weldFirst;
        private static int _cachedLocomotionLayer = -1;

        internal static int PropCount => _instance != null ? _instance.CleanAndCount() : 0;

        internal static SandboxManager Ensure()
        {
            if (_instance != null) return _instance;
            if (Plugin.Root == null) return null;
            _instance = Plugin.Root.GetComponent<SandboxManager>();
            if (_instance == null) _instance = Plugin.Root.AddComponent<SandboxManager>();
            return _instance;
        }

        /// <summary>
        /// Resolves the exact Unity layer Gorilla Tag's GTPlayer hands & body collide with
        /// (typically Layer 9 "Gorilla Object" or the first bit in locomotionEnabledLayers).
        /// </summary>
        private static int ResolveLocomotionLayer()
        {
            if (_cachedLocomotionLayer >= 0) return _cachedLocomotionLayer;

            int named = LayerMask.NameToLayer("Gorilla Object");
            if (named >= 0 && named < 32)
            {
                _cachedLocomotionLayer = named;
                return _cachedLocomotionLayer;
            }

            GTPlayer player = GTPlayer.Instance;
            if (player != null)
            {
                int mask = player.locomotionEnabledLayers.value;
                for (int bit = 0; bit < 32; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                    {
                        _cachedLocomotionLayer = bit;
                        return _cachedLocomotionLayer;
                    }
                }
            }

            _cachedLocomotionLayer = 9;
            return _cachedLocomotionLayer;
        }

        private int CleanAndCount()
        {
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                if (_props[i] == null) _props.RemoveAt(i);
            }
            return _props.Count;
        }

        internal static void SpawnProp(SandboxPropType type)
        {
            SandboxManager mgr = Ensure();
            if (mgr == null) return;
            Vector3 pos = mgr.GetSpawnPosition(out Quaternion rot);
            mgr.SpawnPropAt(type, pos, rot, IsConstructionType(type) || SpawnFrozen);
        }

        private static bool IsConstructionType(SandboxPropType type)
        {
            return type == SandboxPropType.WallBlock ||
                   type == SandboxPropType.FloorSlab ||
                   type == SandboxPropType.Pillar ||
                   type == SandboxPropType.Stairs ||
                   type == SandboxPropType.BridgePlank;
        }

        internal static void ClearAll()
        {
            if (_instance == null) return;
            for (int i = 0; i < _instance._props.Count; i++)
            {
                if (_instance._props[i] != null) Destroy(_instance._props[i]);
            }
            _instance._props.Clear();
            _instance._thrusters.Clear();
            _instance._grabbedBody = null;
            _instance._weldFirst = null;
        }

        internal static void UnfreezeAll()
        {
            if (_instance == null) return;
            for (int i = 0; i < _instance._props.Count; i++)
            {
                GameObject go = _instance._props[i];
                if (go == null) continue;
                Rigidbody[] bodies = go.GetComponentsInChildren<Rigidbody>();
                for (int j = 0; j < bodies.Length; j++) bodies[j].isKinematic = false;
            }
        }

        internal static void DetonateAllExplosives()
        {
            if (_instance == null) return;
            for (int i = _instance._props.Count - 1; i >= 0; i--)
            {
                GameObject go = _instance._props[i];
                if (go == null) continue;
                SandboxExplosive exp = go.GetComponent<SandboxExplosive>();
                if (exp != null) exp.Detonate();
            }
        }

        internal static void SpawnPreset(int presetId)
        {
            SandboxManager mgr = Ensure();
            if (mgr == null) return;
            mgr.SpawnPresetInternal(presetId);
        }

        private void SpawnPresetInternal(int presetId)
        {
            Vector3 basePos = GetSpawnPosition(out Quaternion rot);
            float s = Mathf.Clamp(PropScale, 0.25f, 3.0f);

            if (presetId == 0) // Crate Tower
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = basePos + Vector3.up * (i * 0.68f * s);
                    GameObject go = CreatePrimitiveProp("PropForge_TowerCrate", PrimitiveType.Cube, p, rot,
                        Vector3.one * (0.65f * s), ResolveColor(new Color(0.68f, 0.48f, 0.26f)), 3.5f, CustomBounciness, SpawnFrozen);
                    RegisterProp(go);
                }
            }
            else if (presetId == 1) // Ramp + Trampoline Jump
            {
                GameObject ramp = CreatePrimitiveProp("PropForge_Ramp", PrimitiveType.Cube, basePos, rot * Quaternion.Euler(-22f, 0f, 0f),
                    new Vector3(1.6f, 0.14f, 2.4f) * s, ResolveColor(new Color(0.32f, 0.72f, 0.64f)), 12f, 0.05f, true);
                RegisterProp(ramp);

                Vector3 padPos = basePos + (rot * Vector3.forward) * (2.6f * s);
                GameObject pad = CreatePrimitiveProp("PropForge_Trampoline", PrimitiveType.Cylinder, padPos, Quaternion.identity,
                    new Vector3(1.5f, 0.09f, 1.5f) * s, ResolveColor(new Color(0.20f, 0.88f, 0.45f)), 15f, 0.85f, true);
                pad.AddComponent<SandboxTrampoline>();
                RegisterProp(pad);
            }
            else if (presetId == 2) // Bounce Ring
            {
                GameObject pad = CreatePrimitiveProp("PropForge_Trampoline", PrimitiveType.Cylinder, basePos, Quaternion.identity,
                    new Vector3(1.4f, 0.09f, 1.4f) * s, ResolveColor(new Color(0.20f, 0.88f, 0.45f)), 15f, 0.85f, true);
                pad.AddComponent<SandboxTrampoline>();
                RegisterProp(pad);

                for (int i = 0; i < 3; i++)
                {
                    float ang = i * Mathf.PI * 2f / 3f;
                    Vector3 p = basePos + new Vector3(Mathf.Cos(ang), 0.7f, Mathf.Sin(ang)) * (0.9f * s);
                    GameObject ball = CreatePrimitiveProp("PropForge_Ball", PrimitiveType.Sphere, p, Quaternion.identity,
                        Vector3.one * (0.48f * s), ResolveColor(new Color(0.95f, 0.35f, 0.55f)), 1.5f, 0.95f, SpawnFrozen);
                    RegisterProp(ball);
                }
            }
            else if (presetId == 3) // Construction Fort (Floor + 3 Walls)
            {
                RegisterProp(CreatePrimitiveProp("PropForge_FortFloor", PrimitiveType.Cube, basePos, rot,
                    new Vector3(3.2f, 0.18f, 3.2f) * s, ResolveColor(new Color(0.45f, 0.50f, 0.58f)), 20f, 0f, true));
                RegisterProp(CreatePrimitiveProp("PropForge_FortWallBack", PrimitiveType.Cube,
                    basePos + (rot * Vector3.forward) * (1.5f * s) + Vector3.up * (1.0f * s), rot,
                    new Vector3(3.2f, 2.0f, 0.18f) * s, ResolveColor(new Color(0.54f, 0.60f, 0.68f)), 20f, 0f, true));
                RegisterProp(CreatePrimitiveProp("PropForge_FortWallL", PrimitiveType.Cube,
                    basePos + (rot * Vector3.left) * (1.5f * s) + Vector3.up * (1.0f * s), rot,
                    new Vector3(0.18f, 2.0f, 3.2f) * s, ResolveColor(new Color(0.54f, 0.60f, 0.68f)), 20f, 0f, true));
                RegisterProp(CreatePrimitiveProp("PropForge_FortWallR", PrimitiveType.Cube,
                    basePos + (rot * Vector3.right) * (1.5f * s) + Vector3.up * (1.0f * s), rot,
                    new Vector3(0.18f, 2.0f, 3.2f) * s, ResolveColor(new Color(0.54f, 0.60f, 0.68f)), 20f, 0f, true));
            }
        }

        private void RegisterProp(GameObject go)
        {
            if (go == null) return;
            _props.Add(go);
            if (_props.Count > 60)
            {
                if (_props[0] != null) Destroy(_props[0]);
                _props.RemoveAt(0);
            }
        }

        private static Color ResolveColor(Color fallback)
        {
            switch (ColorPreset)
            {
                case 1: return new Color(0.18f, 0.88f, 1.0f);
                case 2: return new Color(1.0f, 0.30f, 0.65f);
                case 3: return new Color(1.0f, 0.74f, 0.22f);
                case 4: return new Color(0.22f, 0.94f, 0.62f);
                case 5: return UnityEngine.Random.ColorHSV(0f, 1f, 0.75f, 0.85f, 0.90f, 1f);
                default: return fallback;
            }
        }

        private Vector3 GetSpawnPosition(out Quaternion rot)
        {
            if (SpawnAtRightHand && VRRig.LocalRig != null && VRRig.LocalRig.rightHandTransform != null)
            {
                Transform rh = VRRig.LocalRig.rightHandTransform;
                rot = rh.rotation;
                return rh.position + rh.forward * (0.55f * PropScale);
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();
                rot = Quaternion.LookRotation(fwd, Vector3.up);
                return cam.transform.position + cam.transform.forward * (1.55f + 0.35f * PropScale);
            }
            rot = Quaternion.identity;
            return Vector3.up * 2f;
        }

        private static Vector3 GetPropDimensions(SandboxPropType type, float s)
        {
            switch (type)
            {
                case SandboxPropType.WallBlock: return new Vector3(2.2f, 2.0f, 0.22f) * s;
                case SandboxPropType.FloorSlab: return new Vector3(2.4f, 0.18f, 2.4f) * s;
                case SandboxPropType.Pillar: return new Vector3(0.45f, 2.4f, 0.45f) * s;
                case SandboxPropType.Stairs: return new Vector3(1.5f, 0.20f, 2.2f) * s;
                case SandboxPropType.BridgePlank: return new Vector3(0.95f, 0.14f, 3.6f) * s;
                case SandboxPropType.Ramp: return new Vector3(1.6f, 0.14f, 2.4f) * s;
                case SandboxPropType.Trampoline: return new Vector3(1.4f, 0.09f, 1.4f) * s;
                case SandboxPropType.Barrel: return new Vector3(0.55f, 0.45f, 0.55f) * s;
                case SandboxPropType.ExplosiveBarrel: return new Vector3(0.55f, 0.48f, 0.55f) * s;
                case SandboxPropType.BouncyBall: return Vector3.one * (0.55f * s);
                case SandboxPropType.HoverBalloon: return Vector3.one * (0.48f * s);
                default: return Vector3.one * (0.65f * s);
            }
        }

        private void SpawnPropAt(SandboxPropType type, Vector3 pos, Quaternion rot, bool frozen)
        {
            float s = Mathf.Clamp(PropScale, 0.25f, 3.0f);
            GameObject go = null;

            switch (type)
            {
                case SandboxPropType.Crate:
                    go = CreatePrimitiveProp("PropForge_Crate", PrimitiveType.Cube, pos, rot,
                        GetPropDimensions(type, s), new Color(0.68f, 0.48f, 0.26f), 4f, 0.1f, frozen);
                    break;

                case SandboxPropType.Barrel:
                    go = CreatePrimitiveProp("PropForge_Barrel", PrimitiveType.Cylinder, pos, rot,
                        GetPropDimensions(type, s), new Color(0.24f, 0.46f, 0.72f), 5f, 0.15f, frozen);
                    break;

                case SandboxPropType.BouncyBall:
                    go = CreatePrimitiveProp("PropForge_Ball", PrimitiveType.Sphere, pos, rot,
                        GetPropDimensions(type, s), new Color(0.95f, 0.35f, 0.55f), 1.5f, 0.92f, frozen);
                    break;

                case SandboxPropType.Ramp:
                    go = CreatePrimitiveProp("PropForge_Ramp", PrimitiveType.Cube, pos, rot * Quaternion.Euler(-22f, 0f, 0f),
                        GetPropDimensions(type, s), new Color(0.32f, 0.72f, 0.64f), 12f, 0.05f, frozen);
                    break;

                case SandboxPropType.Trampoline:
                    go = CreatePrimitiveProp("PropForge_Trampoline", PrimitiveType.Cylinder, pos, Quaternion.identity,
                        GetPropDimensions(type, s), new Color(0.20f, 0.88f, 0.45f), 15f, 0.85f, frozen);
                    go.AddComponent<SandboxTrampoline>();
                    break;

                case SandboxPropType.ExplosiveBarrel:
                    go = CreatePrimitiveProp("PropForge_Explosive", PrimitiveType.Cylinder, pos, rot,
                        GetPropDimensions(type, s), new Color(0.88f, 0.18f, 0.18f), 4.5f, 0.1f, frozen);
                    go.AddComponent<SandboxExplosive>();
                    break;

                case SandboxPropType.Ragdoll:
                    go = CreateRagdoll(pos, rot, s);
                    break;

                case SandboxPropType.HoverBalloon:
                    go = CreatePrimitiveProp("PropForge_Balloon", PrimitiveType.Sphere, pos, rot,
                        GetPropDimensions(type, s), new Color(0.98f, 0.82f, 0.22f), 0.35f, 0.5f, frozen);
                    Rigidbody brb = go.GetComponent<Rigidbody>();
                    if (brb != null) brb.useGravity = false;
                    go.AddComponent<SandboxBalloonLift>();
                    break;

                case SandboxPropType.WallBlock:
                    go = CreatePrimitiveProp("PropForge_Wall", PrimitiveType.Cube, pos, rot,
                        GetPropDimensions(type, s), new Color(0.56f, 0.62f, 0.70f), 25f, 0f, frozen);
                    break;

                case SandboxPropType.FloorSlab:
                    go = CreatePrimitiveProp("PropForge_Floor", PrimitiveType.Cube, pos, rot,
                        GetPropDimensions(type, s), new Color(0.42f, 0.48f, 0.56f), 25f, 0f, frozen);
                    break;

                case SandboxPropType.Pillar:
                    go = CreatePrimitiveProp("PropForge_Pillar", PrimitiveType.Cube, pos, rot,
                        GetPropDimensions(type, s), new Color(0.64f, 0.68f, 0.75f), 20f, 0f, frozen);
                    break;

                case SandboxPropType.Stairs:
                    go = CreatePrimitiveProp("PropForge_Stairs", PrimitiveType.Cube, pos, rot * Quaternion.Euler(-30f, 0f, 0f),
                        GetPropDimensions(type, s), new Color(0.52f, 0.64f, 0.74f), 20f, 0f, frozen);
                    break;

                case SandboxPropType.BridgePlank:
                    go = CreatePrimitiveProp("PropForge_Bridge", PrimitiveType.Cube, pos, rot,
                        GetPropDimensions(type, s), new Color(0.64f, 0.46f, 0.28f), 18f, 0f, frozen);
                    break;
            }

            RegisterProp(go);
        }

        private GameObject CreatePrimitiveProp(string name, PrimitiveType primitive, Vector3 pos, Quaternion rot,
            Vector3 scale, Color color, float mass, float bounciness, bool isFrozen)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<SandboxPropTag>();

            // Critical Gorilla Tag fix: assign to Gorilla Tag's exact locomotion layer
            // so gorilla hands & body physically touch, stand on, and climb the block!
            go.layer = SolidColliders ? ResolveLocomotionLayer() : 2; // 2 = Ignore Raycast only if ghost

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                if (sh != null) r.material = new Material(sh);
                r.material.color = ResolveColor(color);
            }

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                // Replace default CapsuleCollider on Cylinder with a crisp BoxCollider / MeshCollider so standing/climbing feels solid
                if (primitive == PrimitiveType.Cylinder && col is CapsuleCollider)
                {
                    Destroy(col);
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(1f, 2f, 1f);
                    col = bc;
                }

                col.enabled = true;
                col.isTrigger = !SolidColliders;

                if (SolidColliders)
                {
                    float bounce = Mathf.Max(bounciness, CustomBounciness);
                    if (bounce > 0.05f)
                    {
                        PhysicsMaterial pm = new PhysicsMaterial("PropForgeMat")
                        {
                            bounciness = bounce,
                            bounceCombine = PhysicsMaterialCombine.Maximum
                        };
                        col.material = pm;
                    }

                    // Attach GorillaSurfaceOverride so Gorilla Tag hand taps & friction work natively
                    if (go.GetComponent<GorillaSurfaceOverride>() == null)
                    {
                        try { go.AddComponent<GorillaSurfaceOverride>(); } catch { }
                    }
                }
            }

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = isFrozen;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = isFrozen
                ? CollisionDetectionMode.Discrete
                : CollisionDetectionMode.ContinuousDynamic;
            return go;
        }

        private GameObject CreateRagdoll(Vector3 pos, Quaternion rot, float s)
        {
            GameObject root = new GameObject("PropForge_Ragdoll");
            root.transform.position = pos;
            root.transform.rotation = rot;
            root.AddComponent<SandboxPropTag>();

            GameObject torso = CreatePrimitiveProp("Ragdoll_Torso", PrimitiveType.Cube, pos, rot,
                new Vector3(0.45f, 0.55f, 0.28f) * s, new Color(0.32f, 0.36f, 0.44f), 5f, 0.1f, SpawnFrozen);
            torso.transform.SetParent(root.transform, true);
            Rigidbody torsoRb = torso.GetComponent<Rigidbody>();

            GameObject head = CreatePrimitiveProp("Ragdoll_Head", PrimitiveType.Sphere, pos + Vector3.up * (0.45f * s), rot,
                Vector3.one * (0.34f * s), new Color(0.86f, 0.68f, 0.54f), 2f, 0.1f, SpawnFrozen);
            head.transform.SetParent(root.transform, true);
            ConnectJoint(head, torsoRb);

            GameObject leftArm = CreatePrimitiveProp("Ragdoll_LArm", PrimitiveType.Capsule, pos + new Vector3(-0.38f, 0.05f, 0f) * s, rot,
                new Vector3(0.16f, 0.34f, 0.16f) * s, new Color(0.38f, 0.42f, 0.52f), 1.5f, 0.1f, SpawnFrozen);
            leftArm.transform.SetParent(root.transform, true);
            ConnectJoint(leftArm, torsoRb);

            GameObject rightArm = CreatePrimitiveProp("Ragdoll_RArm", PrimitiveType.Capsule, pos + new Vector3(0.38f, 0.05f, 0f) * s, rot,
                new Vector3(0.16f, 0.34f, 0.16f) * s, new Color(0.38f, 0.42f, 0.52f), 1.5f, 0.1f, SpawnFrozen);
            rightArm.transform.SetParent(root.transform, true);
            ConnectJoint(rightArm, torsoRb);

            return root;
        }

        private static void ConnectJoint(GameObject part, Rigidbody parentBody)
        {
            CharacterJoint cj = part.AddComponent<CharacterJoint>();
            cj.connectedBody = parentBody;
        }

        // --- Clean, player-filtered & trigger-ignoring raycast helpers ---

        private static bool IsLocalPlayerCollider(Collider col)
        {
            if (col == null) return true;
            if (GorillaTagger.Instance != null)
            {
                if (col == GorillaTagger.Instance.bodyCollider || col == GorillaTagger.Instance.headCollider) return true;
            }
            if (GTPlayer.Instance != null && col.transform.IsChildOf(GTPlayer.Instance.transform)) return true;
            if (VRRig.LocalRig != null && col.transform.IsChildOf(VRRig.LocalRig.transform)) return true;
            return false;
        }

        private bool RaycastProp(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit bestHit, out SandboxPropTag bestTag)
        {
            bestHit = default;
            bestTag = null;
            // Include Trigger colliders ONLY for ghost props that have SandboxPropTag, while still skipping Gorilla Tag zone triggers!
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return false;

            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || IsLocalPlayerCollider(c)) continue;
                if (_ghostPreview != null && c.transform.IsChildOf(_ghostPreview.transform)) continue;

                SandboxPropTag tag = c.GetComponentInParent<SandboxPropTag>();
                if (tag == null) continue;

                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                    bestTag = tag;
                }
            }
            return bestTag != null;
        }

        private bool RaycastWorldSurface(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            int locoMask = GTPlayer.Instance != null ? GTPlayer.Instance.locomotionEnabledLayers.value : ~0;
            int mask = locoMask | (1 << ResolveLocomotionLayer());
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxDist, mask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || IsLocalPlayerCollider(c)) continue;
                if (_ghostPreview != null && c.transform.IsChildOf(_ghostPreview.transform)) continue;

                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        private void Update()
        {
            if (ActiveTool == SandboxToolMode.None)
            {
                HideBeam();
                HideGhost();
                _grabbedBody = null;
                return;
            }

            GetAimRay(out Vector3 origin, out Vector3 dir);
            bool trigger = ReadRightTrigger();
            bool triggerDown = trigger && !_prevTrigger;
            bool primary = ReadRightPrimary();
            bool primaryDown = primary && !_prevPrimary;
            _prevTrigger = trigger;
            _prevPrimary = primary;

            if (ActiveTool == SandboxToolMode.ConstructionPlacer)
            {
                _grabbedBody = null;
                UpdateConstructionPlacer(origin, dir, triggerDown);
                return;
            }
            HideGhost();

            if (ActiveTool == SandboxToolMode.PhysicsGun)
            {
                UpdatePhysicsGun(origin, dir, trigger, triggerDown, primaryDown);
                return;
            }

            _grabbedBody = null;
            bool hitProp = RaycastProp(origin, dir, 30f, out RaycastHit hit, out SandboxPropTag tag);
            Vector3 endPt = hitProp ? hit.point : origin + dir * 12f;
            ShowBeam(origin, endPt, GetToolColor(ActiveTool));

            if (!triggerDown || !hitProp || tag == null) return;

            Rigidbody rb = hit.collider.GetComponentInParent<Rigidbody>();
            switch (ActiveTool)
            {
                case SandboxToolMode.Remover:
                    Destroy(tag.gameObject);
                    break;

                case SandboxToolMode.Thruster:
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        nozzle.name = "PropForge_Thruster";
                        Destroy(nozzle.GetComponent<Collider>());
                        nozzle.transform.position = hit.point;
                        nozzle.transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90f, 0f, 0f);
                        nozzle.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
                        nozzle.transform.SetParent(rb.transform, true);
                        Renderer nr = nozzle.GetComponent<Renderer>();
                        if (nr != null) nr.material.color = new Color(1f, 0.45f, 0.1f);

                        SandboxThruster th = nozzle.AddComponent<SandboxThruster>();
                        th.TargetBody = rb;
                        th.LocalForceDir = rb.transform.InverseTransformDirection(-hit.normal);
                        _thrusters.Add(th);
                    }
                    break;

                case SandboxToolMode.Balloon:
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        GameObject balloon = CreatePrimitiveProp("PropForge_AttachedBalloon", PrimitiveType.Sphere,
                            hit.point + Vector3.up * 0.9f, Quaternion.identity, Vector3.one * 0.36f,
                            UnityEngine.Random.ColorHSV(0f, 1f, 0.75f, 0.85f, 0.9f, 1f), 0.25f, 0.4f, false);
                        Rigidbody brb = balloon.GetComponent<Rigidbody>();
                        brb.useGravity = false;
                        balloon.AddComponent<SandboxBalloonLift>().LiftForce = 18f;
                        SpringJoint sj = balloon.AddComponent<SpringJoint>();
                        sj.connectedBody = rb;
                        sj.maxDistance = 1.1f;
                        sj.spring = 35f;
                        sj.damper = 3f;
                        RegisterProp(balloon);
                    }
                    break;

                case SandboxToolMode.Weld:
                    if (rb != null)
                    {
                        if (_weldFirst == null || _weldFirst == rb)
                        {
                            _weldFirst = rb;
                        }
                        else
                        {
                            FixedJoint fj = _weldFirst.gameObject.AddComponent<FixedJoint>();
                            fj.connectedBody = rb;
                            _weldFirst = null;
                        }
                    }
                    break;
            }
        }

        private void UpdateConstructionPlacer(Vector3 origin, Vector3 dir, bool placeDown)
        {
            float s = Mathf.Clamp(PropScale, 0.25f, 3.0f);
            Vector3 dims = GetPropDimensions(SelectedBuildBlock, s);

            Vector3 placePos;
            Quaternion placeRot;

            Camera cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : dir;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            placeRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            if (RaycastWorldSurface(origin, dir, 22f, out RaycastHit hit))
            {
                // Offset outward along hit normal by half the block extent so it rests flush on surfaces!
                float halfExtent = Mathf.Abs(hit.normal.y) > 0.6f ? dims.y * 0.5f : Mathf.Min(dims.x, dims.z) * 0.5f;
                placePos = hit.point + hit.normal * halfExtent;
                ShowBeam(origin, hit.point, GetToolColor(SandboxToolMode.ConstructionPlacer));
            }
            else
            {
                placePos = origin + dir * 4.5f;
                ShowBeam(origin, placePos, new Color(0.25f, 0.95f, 0.65f, 0.45f));
            }

            if (SnapToGrid)
            {
                const float grid = 0.25f;
                placePos = new Vector3(
                    Mathf.Round(placePos.x / grid) * grid,
                    Mathf.Round(placePos.y / grid) * grid,
                    Mathf.Round(placePos.z / grid) * grid);
                float yaw = Mathf.Round(placeRot.eulerAngles.y / 45f) * 45f;
                placeRot = Quaternion.Euler(0f, yaw, 0f);
            }

            EnsureGhost(SelectedBuildBlock);
            if (_ghostPreview != null)
            {
                _ghostPreview.SetActive(true);
                _ghostPreview.transform.position = placePos;
                _ghostPreview.transform.rotation = SelectedBuildBlock == SandboxPropType.Stairs
                    ? placeRot * Quaternion.Euler(-30f, 0f, 0f)
                    : placeRot;
                _ghostPreview.transform.localScale = dims;
            }

            if (placeDown)
            {
                SpawnPropAt(SelectedBuildBlock, placePos, placeRot, true);
            }
        }

        private void EnsureGhost(SandboxPropType type)
        {
            if (_ghostPreview != null && _ghostType == type) return;
            if (_ghostPreview != null) Destroy(_ghostPreview);
            _ghostType = type;

            _ghostPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghostPreview.name = "PropForge_GhostPreview";
            Collider col = _ghostPreview.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = _ghostPreview.GetComponent<Renderer>();
            if (r != null)
            {
                Shader sh = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    Material m = new Material(sh);
                    m.color = new Color(0.25f, 0.95f, 0.65f, 0.35f);
                    m.renderQueue = 3500;
                    r.material = m;
                }
            }
        }

        private void HideGhost()
        {
            if (_ghostPreview != null) _ghostPreview.SetActive(false);
        }

        private void UpdatePhysicsGun(Vector3 origin, Vector3 dir, bool trigger, bool triggerDown, bool primaryDown)
        {
            if (triggerDown && _grabbedBody == null)
            {
                if (RaycastProp(origin, dir, 30f, out RaycastHit hit, out SandboxPropTag tag) && tag != null)
                {
                    Rigidbody rb = hit.collider.GetComponentInParent<Rigidbody>();
                    if (rb != null)
                    {
                        _grabbedBody = rb;
                        _grabbedBody.isKinematic = false;
                        _grabDistance = Mathf.Clamp(hit.distance, 0.7f, 15f);
                    }
                }
            }

            if (!trigger)
            {
                _grabbedBody = null;
                ShowBeam(origin, origin + dir * 8f, new Color(0.25f, 0.75f, 1f, 0.45f));
                return;
            }

            if (_grabbedBody != null)
            {
                float scroll = 0f;
                if (Mouse.current != null) scroll = Mouse.current.scroll.ReadValue().y * 0.002f;
                UnityEngine.XR.InputDevice rightDev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (rightDev.isValid && rightDev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
                    scroll += axis.y * Time.deltaTime * 3.5f;

                _grabDistance = Mathf.Clamp(_grabDistance + scroll, 0.6f, 18f);
                Vector3 target = origin + dir * _grabDistance;
                Vector3 delta = target - _grabbedBody.position;
                _grabbedBody.linearVelocity = delta * 12f;
                _grabbedBody.angularVelocity *= 0.85f;

                if (primaryDown)
                {
                    _grabbedBody.isKinematic = true;
                    _grabbedBody = null;
                    return;
                }

                ShowBeam(origin, _grabbedBody.position, new Color(0.20f, 0.82f, 1f, 0.95f));
            }
            else
            {
                ShowBeam(origin, origin + dir * 10f, new Color(0.25f, 0.75f, 1f, 0.55f));
            }
        }

        private static void GetAimRay(out Vector3 origin, out Vector3 dir)
        {
            VRRig localRig = VRRig.LocalRig;
            if (localRig != null && localRig.rightHandTransform != null && XRSettings.isDeviceActive)
            {
                origin = localRig.rightHandTransform.position;
                dir = localRig.rightHandTransform.forward;
                return;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                origin = cam.transform.position + cam.transform.right * 0.15f - cam.transform.up * 0.08f;
                dir = cam.transform.forward;
                return;
            }
            origin = Vector3.zero;
            dir = Vector3.forward;
        }

        private static bool ReadRightTrigger()
        {
            if (ControllerInputPoller.instance != null && ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f)
                return true;
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                return true;
            return false;
        }

        private static bool ReadRightPrimary()
        {
            if (ControllerInputPoller.instance != null && ControllerInputPoller.instance.rightControllerPrimaryButton)
                return true;
            if (Keyboard.current != null && Keyboard.current.fKey.isPressed)
                return true;
            return false;
        }

        private static Color GetToolColor(SandboxToolMode mode)
        {
            switch (mode)
            {
                case SandboxToolMode.PhysicsGun: return new Color(0.20f, 0.82f, 1.0f);
                case SandboxToolMode.ConstructionPlacer: return new Color(0.25f, 0.95f, 0.65f);
                case SandboxToolMode.Thruster: return new Color(1.0f, 0.52f, 0.16f);
                case SandboxToolMode.Balloon: return new Color(0.98f, 0.84f, 0.25f);
                case SandboxToolMode.Weld: return new Color(0.66f, 0.45f, 1.0f);
                case SandboxToolMode.Remover: return new Color(0.96f, 0.26f, 0.30f);
                default: return Color.white;
            }
        }

        private void ShowBeam(Vector3 start, Vector3 end, Color col)
        {
            if (_beam == null)
            {
                GameObject go = new GameObject("PropForgeBeam");
                go.transform.SetParent(transform, false);
                _beam = go.AddComponent<LineRenderer>();
                _beam.positionCount = 2;
                _beam.startWidth = 0.014f;
                _beam.endWidth = 0.006f;
                _beam.useWorldSpace = true;
                Shader sh = Shader.Find("Sprites/Default");
                if (sh != null) _beam.material = new Material(sh);
            }
            _beam.enabled = true;
            _beam.startColor = col;
            _beam.endColor = new Color(col.r, col.g, col.b, 0.25f);
            _beam.SetPosition(0, start);
            _beam.SetPosition(1, end);
        }

        private void HideBeam()
        {
            if (_beam != null) _beam.enabled = false;
        }
    }

    internal sealed class SandboxPropTag : MonoBehaviour { }

    internal sealed class SandboxTrampoline : MonoBehaviour
    {
        private float _cooldown;
        private void OnCollisionEnter(Collision col)
        {
            if (Time.time < _cooldown) return;
            GTPlayer player = GTPlayer.Instance;
            if (player != null && player.playerRigidBody != null &&
                (col.rigidbody == player.playerRigidBody || col.gameObject.name.Contains("Gorilla")))
            {
                _cooldown = Time.time + 0.25f;
                Vector3 v = player.playerRigidBody.linearVelocity;
                v.y = 14.5f;
                player.playerRigidBody.linearVelocity = v;
            }
        }
    }

    internal sealed class SandboxBalloonLift : MonoBehaviour
    {
        internal float LiftForce = 6.5f;
        private Rigidbody _rb;
        private void Awake() { _rb = GetComponent<Rigidbody>(); }
        private void FixedUpdate()
        {
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.AddForce(Vector3.up * LiftForce, ForceMode.Force);
            }
        }
    }

    internal sealed class SandboxThruster : MonoBehaviour
    {
        internal Rigidbody TargetBody;
        internal Vector3 LocalForceDir = Vector3.forward;
        private void FixedUpdate()
        {
            if (TargetBody != null && !TargetBody.isKinematic)
            {
                Vector3 worldDir = TargetBody.transform.TransformDirection(LocalForceDir);
                TargetBody.AddForceAtPosition(worldDir * 28f, transform.position, ForceMode.Force);
            }
        }
    }

    internal sealed class SandboxExplosive : MonoBehaviour
    {
        private bool _detonated;

        private void OnCollisionEnter(Collision col)
        {
            if (col.relativeVelocity.magnitude > 7.5f) Detonate();
        }

        internal void Detonate()
        {
            if (_detonated) return;
            _detonated = true;

            Vector3 center = transform.position;
            Collider[] hits = Physics.OverlapSphere(center, 6.5f);
            for (int i = 0; i < hits.Length; i++)
            {
                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(900f, center, 6.5f, 1.5f);
                }
            }

            GTPlayer player = GTPlayer.Instance;
            if (player != null && player.playerRigidBody != null)
            {
                float dist = Vector3.Distance(center, player.transform.position);
                if (dist < 6.5f)
                {
                    Vector3 push = (player.transform.position - center).normalized + Vector3.up * 0.65f;
                    player.playerRigidBody.AddForce(push.normalized * (18f * (1f - dist / 6.5f)), ForceMode.VelocityChange);
                }
            }

            Destroy(gameObject);
        }
    }
}
