using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Player;

namespace CascadeMenu.Mods.Visual
{
    /// <summary>
    /// Procedural full-body kinematic estimation for Gorilla Tag players.
    /// Estimates Spine, Pelvis, Shoulders, Elbows, Hips, Knees, and Ground-Aware Feet
    /// from each player's head/hand poses and horizontal velocity, with an optional
    /// velocity prediction vector.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class BodyEstimationMod : Mod
    {
        private bool _showOthers = true;
        private bool _showLocal = true;
        private bool _showJoints = true;
        private bool _showVelocity = true;
        private bool _matchFurColor = true;
        private bool _throughWalls = true;
        private float _thickness = 1.0f;

        private GameObject _root;
        private Material _lineMat;
        private Material _jointMat;
        private Mesh _sphereMesh;
        private readonly Dictionary<VRRig, RigBodyVisual> _visuals = new Dictionary<VRRig, RigBodyVisual>();
        private readonly List<VRRig> _stale = new List<VRRig>();

        private sealed class RigBodyVisual
        {
            public GameObject Container;
            public LineRenderer SpineLine;      // Head -> Neck -> Chest -> Pelvis
            public LineRenderer LeftArmLine;    // Neck -> LShoulder -> LElbow -> LHand
            public LineRenderer RightArmLine;   // Neck -> RShoulder -> RElbow -> RHand
            public LineRenderer LeftLegLine;    // Pelvis -> LHip -> LKnee -> LFoot
            public LineRenderer RightLegLine;   // Pelvis -> RHip -> RKnee -> RFoot
            public LineRenderer VelocityLine;   // Chest -> Chest + Velocity * 0.35
            public Transform[] JointDots;       // Neck, Chest, Pelvis, LElbow, RElbow, LKnee, RKnee, LFoot, RFoot
            public Renderer[] JointRenderers;

            public Vector3 PrevHeadPos;
            public Vector3 SmoothVel;
            public float GaitPhase;
            public bool Initialized;
            public MaterialPropertyBlock PropBlock;
        }

        public BodyEstimationMod()
        {
            Name = "Body Estimation";
            isToggle = true;

            AddSetting(ModSetting.Toggle(
                "showOthers",
                "Show on other players",
                "Estimate full body posture on remote players in the room",
                () => _showOthers,
                v => _showOthers = v));

            AddSetting(ModSetting.Toggle(
                "showLocal",
                "Show on yourself",
                "Estimate your own full body (visible when looking down or in third-person)",
                () => _showLocal,
                v => _showLocal = v));

            AddSetting(ModSetting.Toggle(
                "showJoints",
                "Show joint markers",
                "Draw small glowing spheres on estimated knees, elbows, pelvis, and feet",
                () => _showJoints,
                v => _showJoints = v));

            AddSetting(ModSetting.Toggle(
                "showVelocity",
                "Show velocity vector",
                "Draw a predicted movement direction ray from each player's torso",
                () => _showVelocity,
                v => _showVelocity = v));

            AddSetting(ModSetting.Toggle(
                "matchFurColor",
                "Match fur color",
                "Tint each estimated body with that player's Gorilla color",
                () => _matchFurColor,
                v => _matchFurColor = v));

            AddSetting(ModSetting.Toggle(
                "throughWalls",
                "See through walls",
                "Render estimated bodies over map geometry so they stay visible everywhere",
                () => _throughWalls,
                v =>
                {
                    _throughWalls = v;
                    ApplyMaterialDepth();
                }));

            AddSetting(ModSetting.Slider(
                "thickness",
                "Line thickness",
                "Thickness of the estimated body bones and joint markers",
                0.5f,
                2.5f,
                () => _thickness,
                v => _thickness = v,
                v => v.ToString("0.0") + "x"));
        }

        public override void OnEnable()
        {
            EnsureResources();
        }

        public override void OnDisable()
        {
            CleanupAll();
        }

        public override void LateUpdate()
        {
            if (_root == null || _lineMat == null)
            {
                EnsureResources();
                if (_root == null || _lineMat == null) return;
            }

            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.1f);
            int groundMask = ResolveGroundMask();
            HashSet<VRRig> activeThisFrame = new HashSet<VRRig>();

            if (_showOthers)
            {
                List<VRRig> remotes = PlayerActions.GetRemoteRigs();
                if (remotes != null)
                {
                    for (int i = 0; i < remotes.Count; i++)
                    {
                        VRRig rig = remotes[i];
                        if (!IsRigUsable(rig) || rig == VRRig.LocalRig) continue;
                        activeThisFrame.Add(rig);
                        UpdateRigBody(rig, dt, groundMask, false);
                    }
                }
            }

            if (_showLocal)
            {
                VRRig localRig = VRRig.LocalRig;
                if (localRig == null && GorillaTagger.Instance != null)
                    localRig = GorillaTagger.Instance.offlineVRRig;
                if (IsRigUsable(localRig))
                {
                    activeThisFrame.Add(localRig);
                    UpdateRigBody(localRig, dt, groundMask, true);
                }
            }

            _stale.Clear();
            foreach (var kv in _visuals)
            {
                if (kv.Key == null || !activeThisFrame.Contains(kv.Key))
                    _stale.Add(kv.Key);
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                VRRig dead = _stale[i];
                if (_visuals.TryGetValue(dead, out RigBodyVisual vis))
                {
                    if (vis != null && vis.Container != null)
                        UnityEngine.Object.Destroy(vis.Container);
                    _visuals.Remove(dead);
                }
            }
        }

        private void UpdateRigBody(VRRig rig, float dt, int groundMask, bool isLocal)
        {
            if (!_visuals.TryGetValue(rig, out RigBodyVisual vis) || vis == null || vis.Container == null)
            {
                vis = CreateRigVisual(rig);
                _visuals[rig] = vis;
            }

            float scale = rig.scaleFactor > 0.05f ? rig.scaleFactor : 1f;

            // Resolve Head & Hands
            Transform headTr = rig.headMesh != null ? rig.headMesh.transform
                : (rig.headConstraint != null ? rig.headConstraint : rig.transform);
            Vector3 headPos = headTr != null ? headTr.position : rig.transform.position + Vector3.up * (0.45f * scale);

            Vector3 forward = headTr != null ? headTr.forward : rig.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = rig.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;

            Vector3 leftHand = ResolveHandPos(rig, true, isLocal, headPos - right * (0.35f * scale) - up * (0.28f * scale));
            Vector3 rightHand = ResolveHandPos(rig, false, isLocal, headPos + right * (0.35f * scale) - up * (0.28f * scale));

            // Velocity estimation
            if (!vis.Initialized)
            {
                vis.PrevHeadPos = headPos;
                vis.SmoothVel = Vector3.zero;
                vis.GaitPhase = 0f;
                vis.Initialized = true;
            }

            Vector3 rawVel = (headPos - vis.PrevHeadPos) / dt;
            if (rawVel.sqrMagnitude > 900f) rawVel = Vector3.zero; // teleport guard
            vis.PrevHeadPos = headPos;
            vis.SmoothVel = Vector3.Lerp(vis.SmoothVel, rawVel, 1f - Mathf.Exp(-12f * dt));

            Vector3 horizVel = new Vector3(vis.SmoothVel.x, 0f, vis.SmoothVel.z);
            float horizSpeed = horizVel.magnitude;

            // Dynamic forward lean from horizontal velocity
            Vector3 leanOffset = Vector3.ClampMagnitude(horizVel * 0.035f, 0.14f * scale);
            Vector3 neckPos = headPos - up * (0.14f * scale);
            Vector3 chestPos = neckPos - up * (0.19f * scale) - leanOffset * 0.45f;
            Vector3 pelvisPos = chestPos - up * (0.24f * scale) - leanOffset;

            // Shoulders & 2-bone elbow IK hints
            Vector3 lShoulder = neckPos - right * (0.17f * scale) - up * (0.04f * scale);
            Vector3 rShoulder = neckPos + right * (0.17f * scale) - up * (0.04f * scale);
            Vector3 lElbow = EstimateJointMidpoint(lShoulder, leftHand, (-right * 0.55f - forward * 0.45f - up * 0.25f).normalized, 0.34f * scale, 0.34f * scale);
            Vector3 rElbow = EstimateJointMidpoint(rShoulder, rightHand, (right * 0.55f - forward * 0.45f - up * 0.25f).normalized, 0.34f * scale, 0.34f * scale);

            // Hips
            Vector3 lHip = pelvisPos - right * (0.12f * scale);
            Vector3 rHip = pelvisPos + right * (0.12f * scale);

            // Ground probe beneath pelvis to distinguish grounded stride vs airborne tuck
            float legLength = 0.62f * scale;
            bool grounded = false;
            float groundY = pelvisPos.y - legLength;
            if (Physics.Raycast(pelvisPos + up * (0.08f * scale), Vector3.down, out RaycastHit groundHit, legLength + 0.38f * scale, groundMask, QueryTriggerInteraction.Ignore))
            {
                grounded = true;
                groundY = groundHit.point.y + 0.03f * scale;
            }

            // Advance procedural gait phase from movement speed
            if (horizSpeed > 0.25f)
            {
                vis.GaitPhase += horizSpeed * dt * (grounded ? 3.4f : 1.4f);
            }
            else
            {
                // Relax back toward neutral stance
                vis.GaitPhase = Mathf.Lerp(vis.GaitPhase, Mathf.Round(vis.GaitPhase / Mathf.PI) * Mathf.PI, 1f - Mathf.Exp(-6f * dt));
            }

            Vector3 moveDir = horizSpeed > 0.25f ? horizVel / horizSpeed : forward;
            float strideAmp = Mathf.Clamp(horizSpeed * 0.075f, 0f, 0.28f * scale);
            float liftAmp = grounded ? Mathf.Clamp(horizSpeed * 0.045f, 0f, 0.16f * scale) : 0.18f * scale;

            float sinL = Mathf.Sin(vis.GaitPhase);
            float sinR = Mathf.Sin(vis.GaitPhase + Mathf.PI);

            Vector3 lFootTarget = ComputeFootPos(lHip, moveDir, forward, sinL, strideAmp, liftAmp, legLength, grounded, groundY, groundMask, scale, vis.SmoothVel.y);
            Vector3 rFootTarget = ComputeFootPos(rHip, moveDir, forward, sinR, strideAmp, liftAmp, legLength, grounded, groundY, groundMask, scale, vis.SmoothVel.y);

            Vector3 kneeHint = (forward * 0.85f + moveDir * 0.25f).normalized;
            Vector3 lKnee = EstimateJointMidpoint(lHip, lFootTarget, (kneeHint - right * 0.18f).normalized, 0.32f * scale, 0.32f * scale);
            Vector3 rKnee = EstimateJointMidpoint(rHip, rFootTarget, (kneeHint + right * 0.18f).normalized, 0.32f * scale, 0.32f * scale);

            // Colors & thickness
            Color bodyColor = ResolveBodyColor(rig);
            Color accentColor = Color.Lerp(bodyColor, Color.white, 0.35f);
            float width = 0.014f * _thickness * scale;

            ApplyLine(vis.SpineLine, width * 1.25f, bodyColor, headPos, neckPos, chestPos, pelvisPos);
            ApplyLine(vis.LeftArmLine, width, bodyColor, neckPos, lShoulder, lElbow, leftHand);
            ApplyLine(vis.RightArmLine, width, bodyColor, neckPos, rShoulder, rElbow, rightHand);
            ApplyLine(vis.LeftLegLine, width * 1.05f, bodyColor, pelvisPos, lHip, lKnee, lFootTarget);
            ApplyLine(vis.RightLegLine, width * 1.05f, bodyColor, pelvisPos, rHip, rKnee, rFootTarget);

            // Velocity prediction vector
            bool showVel = _showVelocity && vis.SmoothVel.sqrMagnitude > 0.12f;
            vis.VelocityLine.enabled = showVel;
            if (showVel)
            {
                Vector3 tip = chestPos + Vector3.ClampMagnitude(vis.SmoothVel * 0.28f, 1.8f * scale);
                Color velColor = new Color(0.25f, 0.95f, 1f, 0.92f);
                ApplyLine(vis.VelocityLine, width * 0.85f, velColor, chestPos, tip);
            }

            // Joint dots
            if (vis.JointDots != null)
            {
                Vector3[] joints =
                {
                    neckPos, chestPos, pelvisPos,
                    lElbow, rElbow,
                    lKnee, rKnee,
                    lFootTarget, rFootTarget
                };

                float dotScale = 0.036f * _thickness * scale;
                vis.PropBlock.SetColor("_Color", accentColor);

                for (int i = 0; i < vis.JointDots.Length && i < joints.Length; i++)
                {
                    Transform dot = vis.JointDots[i];
                    if (dot == null) continue;
                    dot.gameObject.SetActive(_showJoints);
                    if (!_showJoints) continue;
                    dot.position = joints[i];
                    dot.localScale = Vector3.one * dotScale;
                    if (vis.JointRenderers[i] != null)
                        vis.JointRenderers[i].SetPropertyBlock(vis.PropBlock);
                }
            }
        }

        private static Vector3 ComputeFootPos(
            Vector3 hip,
            Vector3 moveDir,
            Vector3 forward,
            float phaseSin,
            float strideAmp,
            float liftAmp,
            float legLength,
            bool grounded,
            float baseGroundY,
            int groundMask,
            float scale,
            float verticalVel)
        {
            Vector3 horizOffset = moveDir * (phaseSin * strideAmp);
            Vector3 foot;

            if (grounded)
            {
                float lift = Mathf.Max(0f, phaseSin) * liftAmp;
                foot = new Vector3(hip.x + horizOffset.x, baseGroundY + lift, hip.z + horizOffset.z);

                // Snap planted foot cleanly to angled floors / branches
                if (lift < 0.02f * scale &&
                    Physics.Raycast(new Vector3(foot.x, hip.y, foot.z), Vector3.down, out RaycastHit footHit, legLength + 0.35f * scale, groundMask, QueryTriggerInteraction.Ignore))
                {
                    foot.y = footHit.point.y + 0.03f * scale;
                }
            }
            else
            {
                // Airborne tuck: knees pull slightly up when rising, extend down when falling
                float airTuck = Mathf.Clamp(verticalVel * 0.04f, -0.10f * scale, 0.22f * scale);
                float dangleY = hip.y - (legLength - 0.12f * scale - airTuck) + phaseSin * 0.06f * scale;
                foot = new Vector3(
                    hip.x + horizOffset.x - forward.x * (0.06f * scale),
                    dangleY,
                    hip.z + horizOffset.z - forward.z * (0.06f * scale));
            }

            // Clamp foot within max anatomical reach from hip
            Vector3 delta = foot - hip;
            float maxReach = legLength * 1.04f;
            if (delta.sqrMagnitude > maxReach * maxReach)
                foot = hip + delta.normalized * maxReach;

            return foot;
        }

        private static Vector3 EstimateJointMidpoint(Vector3 root, Vector3 end, Vector3 bendHint, float upperLen, float lowerLen)
        {
            Vector3 toEnd = end - root;
            float dist = Mathf.Clamp(toEnd.magnitude, 0.02f, (upperLen + lowerLen) * 0.995f);
            Vector3 dir = toEnd.sqrMagnitude > 0.0001f ? toEnd / toEnd.magnitude : Vector3.down;

            // Law of cosines for 2-bone IK bend height
            float cosAngle = Mathf.Clamp((upperLen * upperLen + dist * dist - lowerLen * lowerLen) / (2f * upperLen * dist), -1f, 1f);
            float along = upperLen * cosAngle;
            float perp = Mathf.Sqrt(Mathf.Max(0f, upperLen * upperLen - along * along));

            Vector3 orthoHint = bendHint - dir * Vector3.Dot(bendHint, dir);
            if (orthoHint.sqrMagnitude < 0.0001f)
                orthoHint = Vector3.Cross(dir, Vector3.right);
            orthoHint.Normalize();

            return root + dir * along + orthoHint * perp;
        }

        private static Vector3 ResolveHandPos(VRRig rig, bool left, bool isLocal, Vector3 fallback)
        {
            if (isLocal && GorillaTagger.Instance != null)
            {
                Transform hand = left ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
                if (hand != null) return hand.position;
            }

            Transform rigHand = left ? rig.leftHandTransform : rig.rightHandTransform;
            if (rigHand != null) return rigHand.position;

            return fallback;
        }

        private Color ResolveBodyColor(VRRig rig)
        {
            if (_matchFurColor && rig != null)
            {
                Color c = rig.playerColor;
                if (c.r + c.g + c.b > 0.08f)
                {
                    c.a = 0.92f;
                    return c;
                }
            }
            return new Color(0.25f, 0.78f, 1f, 0.92f);
        }

        private static void ApplyLine(LineRenderer lr, float width, Color color, params Vector3[] points)
        {
            if (lr == null) return;
            lr.enabled = true;
            if (lr.positionCount != points.Length) lr.positionCount = points.Length;
            lr.startWidth = width;
            lr.endWidth = width * 0.85f;
            lr.startColor = color;
            lr.endColor = color;
            for (int i = 0; i < points.Length; i++)
                lr.SetPosition(i, points[i]);
        }

        private RigBodyVisual CreateRigVisual(VRRig rig)
        {
            var vis = new RigBodyVisual
            {
                Container = new GameObject("BodyEst_" + (rig != null ? rig.GetInstanceID().ToString() : "Rig")),
                PropBlock = new MaterialPropertyBlock()
            };
            vis.Container.transform.SetParent(_root.transform, false);

            vis.SpineLine = CreateLine(vis.Container.transform, "Spine", 4);
            vis.LeftArmLine = CreateLine(vis.Container.transform, "LeftArm", 4);
            vis.RightArmLine = CreateLine(vis.Container.transform, "RightArm", 4);
            vis.LeftLegLine = CreateLine(vis.Container.transform, "LeftLeg", 4);
            vis.RightLegLine = CreateLine(vis.Container.transform, "RightLeg", 4);
            vis.VelocityLine = CreateLine(vis.Container.transform, "Velocity", 2);

            const int jointCount = 9;
            vis.JointDots = new Transform[jointCount];
            vis.JointRenderers = new Renderer[jointCount];
            for (int i = 0; i < jointCount; i++)
            {
                var dot = new GameObject("Joint_" + i);
                dot.transform.SetParent(vis.Container.transform, false);
                var mf = dot.AddComponent<MeshFilter>();
                mf.sharedMesh = _sphereMesh;
                var mr = dot.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _jointMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                vis.JointDots[i] = dot.transform;
                vis.JointRenderers[i] = mr;
            }

            return vis;
        }

        private LineRenderer CreateLine(Transform parent, string name, int count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = count;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = _lineMat;
            return lr;
        }

        private void EnsureResources()
        {
            if (_root == null)
            {
                _root = new GameObject("CascadeBodyEstimationRoot");
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }

            if (_lineMat == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored")
                    ?? Shader.Find("GUI/Text Shader")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    _lineMat = new Material(shader);
                    _lineMat.hideFlags = HideFlags.HideAndDontSave;
                    _jointMat = new Material(shader);
                    _jointMat.hideFlags = HideFlags.HideAndDontSave;
                    ApplyMaterialDepth();
                }
            }

            if (_sphereMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                MeshFilter mf = temp.GetComponent<MeshFilter>();
                if (mf != null) _sphereMesh = mf.sharedMesh;
                UnityEngine.Object.Destroy(temp);
            }
        }

        private void ApplyMaterialDepth()
        {
            ConfigureMat(_lineMat, _throughWalls);
            ConfigureMat(_jointMat, _throughWalls);
        }

        private static void ConfigureMat(Material mat, bool throughWalls)
        {
            if (mat == null) return;
            mat.renderQueue = throughWalls ? 4010 : 3000;
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", throughWalls ? 0 : 1);
            if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)(throughWalls ? CompareFunction.Always : CompareFunction.LessEqual));
        }

        private static int ResolveGroundMask()
        {
            int mask = Physics.DefaultRaycastLayers;
            if (GTPlayer.Instance != null && GTPlayer.Instance.locomotionEnabledLayers.value != 0)
                mask |= GTPlayer.Instance.locomotionEnabledLayers.value;
            mask &= ~LayerMask.GetMask("UI", "Ignore Raycast");
            return mask;
        }

        private static bool IsRigUsable(VRRig rig)
        {
            return rig != null && rig.gameObject != null && rig.gameObject.activeInHierarchy;
        }

        private void CleanupAll()
        {
            foreach (var kv in _visuals)
            {
                if (kv.Value != null && kv.Value.Container != null)
                    UnityEngine.Object.Destroy(kv.Value.Container);
            }
            _visuals.Clear();
            _stale.Clear();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            if (_lineMat != null)
            {
                UnityEngine.Object.Destroy(_lineMat);
                _lineMat = null;
            }
            if (_jointMat != null)
            {
                UnityEngine.Object.Destroy(_jointMat);
                _jointMat = null;
            }
        }
    }
}
