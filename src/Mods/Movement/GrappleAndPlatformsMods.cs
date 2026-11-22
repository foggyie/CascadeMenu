using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Movement
{
    // -------------------------------------------------------------------------
    // 1. Grappling Hook & Swing Rope (Movement)
    //    Aim with left/right hand (or mouse on PC) and hold trigger/grip to
    //    fire a glowing grapple line and pull/swing yourself toward any surface!
    // -------------------------------------------------------------------------

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class GrappleHookMod : Mod
    {
        private static readonly string[] HandNames = { "Right Hand / Mouse", "Left Hand", "Both Hands" };
        private static readonly string[] InputNames = { "Grip", "Trigger" };

        private int _handMode = 0;
        private int _inputMode = 0;
        private float _pullSpeed = 14f;
        private float _maxRange = 45f;
        private float _springBoost = 1.25f;

        private sealed class HookState
        {
            public bool Attached;
            public Vector3 Anchor;
            public LineRenderer Rope;
            public LineRenderer Ring;
            public bool WasHeld;
        }

        private readonly HookState _rightHook = new HookState();
        private readonly HookState _leftHook = new HookState();
        private Material _material;
        private LayerMask _mask;

        public GrappleHookMod()
        {
            Name = "Grappling Hook";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("hand", "Grapple hand",
                "Which hand fires the grappling hook",
                HandNames,
                () => _handMode,
                value => _handMode = Mathf.Clamp(value, 0, HandNames.Length - 1)));

            AddSetting(ModSetting.Dropdown("input", "Fire button",
                "Hold Grip or Trigger (Right or Left Mouse on PC) to grapple",
                InputNames,
                () => _inputMode,
                value => _inputMode = Mathf.Clamp(value, 0, InputNames.Length - 1)));

            AddSetting(ModSetting.Slider("speed", "Pull speed",
                "How fast the grapple reels you toward the anchor point",
                5f, 30f,
                () => _pullSpeed,
                value => _pullSpeed = value,
                value => value.ToString("0.0") + " m/s"));

            AddSetting(ModSetting.Slider("range", "Max cable length",
                "Longest distance the hook can latch onto",
                10f, 100f,
                () => _maxRange,
                value => _maxRange = value,
                value => value.ToString("0") + " m"));

            AddSetting(ModSetting.Slider("release", "Release launch boost",
                "Momentum multiplier when releasing the hook mid-swing",
                0.8f, 2f,
                () => _springBoost,
                value => _springBoost = value,
                value => value.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            _mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("UI");
            Shader shader = ShaderLookup.SpriteUnlit();
            if (shader != null)
            {
                _material = new Material(shader) { color = Color.white, renderQueue = 4100 };
            }
            BuildHookVisuals(_rightHook, "Right");
            BuildHookVisuals(_leftHook, "Left");
            Plugin.Log?.LogInfo("Cascade Grappling Hook active.");
        }

        public override void OnDisable()
        {
            DestroyHookVisuals(_rightHook);
            DestroyHookVisuals(_leftHook);
            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
                _material = null;
            }
        }

        public override void Update()
        {
            if (CascadeMenuBehaviour.IsMenuOpen)
            {
                ReleaseHook(_rightHook, false);
                ReleaseHook(_leftHook, false);
                return;
            }

            bool useRight = _handMode == 0 || _handMode == 2;
            bool useLeft = _handMode == 1 || _handMode == 2;

            if (useRight) UpdateHook(_rightHook, false);
            else ReleaseHook(_rightHook, false);

            if (useLeft) UpdateHook(_leftHook, true);
            else ReleaseHook(_leftHook, false);
        }

        private void UpdateHook(HookState hook, bool isLeft)
        {
            bool held = IsHookHeld(isLeft);
            Transform hand = GetHandTransform(isLeft);

            if (held && !hook.WasHeld)
            {
                if (TryFindGrapplePoint(hand, out Vector3 hitPoint))
                {
                    hook.Attached = true;
                    hook.Anchor = hitPoint;
                }
            }
            else if (!held && hook.Attached)
            {
                ReleaseHook(hook, true);
            }

            hook.WasHeld = held;

            if (!hook.Attached)
            {
                if (hook.Rope != null) hook.Rope.enabled = false;
                if (hook.Ring != null) hook.Ring.enabled = false;
                return;
            }

            Vector3 start = hand != null ? hand.position : ModKit.HeadPosition;
            Vector3 toAnchor = hook.Anchor - start;
            float dist = toAnchor.magnitude;

            Rigidbody rb = GorillaTagger.Instance != null ? GorillaTagger.Instance.rigidbody : null;
            if (rb != null && dist > 0.6f)
            {
                Vector3 dir = toAnchor / dist;
                Vector3 desiredVel = dir * _pullSpeed;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, desiredVel, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }

            Color accent = CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.35f, 0.88f, 1f, 1f);
            if (hook.Rope != null)
            {
                hook.Rope.startColor = accent;
                hook.Rope.endColor = Color.white;
                hook.Rope.SetPosition(0, start);
                hook.Rope.SetPosition(1, hook.Anchor);
                hook.Rope.enabled = true;
            }

            if (hook.Ring != null)
            {
                DrawAnchorRing(hook.Ring, hook.Anchor, 0.16f, accent);
            }
        }

        private void ReleaseHook(HookState hook, bool boost)
        {
            if (hook.Attached && boost)
            {
                Rigidbody rb = GorillaTagger.Instance != null ? GorillaTagger.Instance.rigidbody : null;
                if (rb != null)
                {
                    rb.linearVelocity *= _springBoost;
                }
            }
            hook.Attached = false;
            hook.WasHeld = false;
            if (hook.Rope != null) hook.Rope.enabled = false;
            if (hook.Ring != null) hook.Ring.enabled = false;
        }

        private bool IsHookHeld(bool isLeft)
        {
            if (XRSettings.isDeviceActive)
            {
                ControllerInputPoller poller = ControllerInputPoller.instance;
                if (poller == null) return false;
                if (_inputMode == 0)
                {
                    return isLeft ? poller.leftControllerGripFloat > 0.55f : poller.rightControllerGripFloat > 0.55f;
                }
                return isLeft ? poller.leftControllerIndexFloat > 0.55f : poller.rightControllerIndexFloat > 0.55f;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            if (isLeft) return mouse.middleButton.isPressed;
            return _inputMode == 0 ? mouse.rightButton.isPressed : mouse.leftButton.isPressed;
        }

        private bool TryFindGrapplePoint(Transform hand, out Vector3 point)
        {
            point = Vector3.zero;
            Vector3 origin;
            Vector3 dir;

            if (XRSettings.isDeviceActive && hand != null)
            {
                origin = hand.position + hand.forward * 0.08f;
                dir = hand.forward;
            }
            else
            {
                Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
                if (cam == null) return false;
                Vector2 screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (Mouse.current != null && Cursor.lockState != CursorLockMode.Locked)
                {
                    screenPos = Mouse.current.position.ReadValue();
                }
                Ray r = cam.ScreenPointToRay(screenPos);
                origin = r.origin + r.direction * 0.2f;
                dir = r.direction;
            }

            if (Physics.Raycast(origin, dir, out RaycastHit hit, _maxRange, _mask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            return false;
        }

        private static Transform GetHandTransform(bool isLeft)
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null) return null;
            return isLeft ? tagger.leftHandTransform : tagger.rightHandTransform;
        }

        private void BuildHookVisuals(HookState hook, string side)
        {
            if (Plugin.Root == null || _material == null) return;

            var ropeGo = new GameObject("CascadeGrappleRope_" + side);
            ropeGo.transform.SetParent(Plugin.Root.transform, false);
            hook.Rope = ropeGo.AddComponent<LineRenderer>();
            hook.Rope.positionCount = 2;
            hook.Rope.useWorldSpace = true;
            hook.Rope.startWidth = 0.012f;
            hook.Rope.endWidth = 0.008f;
            hook.Rope.numCapVertices = 4;
            hook.Rope.sharedMaterial = _material;
            hook.Rope.enabled = false;

            var ringGo = new GameObject("CascadeGrappleRing_" + side);
            ringGo.transform.SetParent(Plugin.Root.transform, false);
            hook.Ring = ringGo.AddComponent<LineRenderer>();
            hook.Ring.positionCount = 20;
            hook.Ring.loop = true;
            hook.Ring.useWorldSpace = true;
            hook.Ring.startWidth = 0.009f;
            hook.Ring.endWidth = 0.009f;
            hook.Ring.sharedMaterial = _material;
            hook.Ring.enabled = false;
        }

        private static void DrawAnchorRing(LineRenderer ring, Vector3 center, float radius, Color color)
        {
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;

            int count = ring.positionCount;
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f;
                ring.SetPosition(i, center + right * (Mathf.Cos(a) * radius) + up * (Mathf.Sin(a) * radius));
            }
            ring.startColor = color;
            ring.endColor = color;
            ring.enabled = true;
        }

        private static void DestroyHookVisuals(HookState hook)
        {
            hook.Attached = false;
            if (hook.Rope != null) UnityEngine.Object.Destroy(hook.Rope.gameObject);
            if (hook.Ring != null) UnityEngine.Object.Destroy(hook.Ring.gameObject);
            hook.Rope = null;
            hook.Ring = null;
        }
    }

    // -------------------------------------------------------------------------
    // 2. Air Platforms (Movement)
    //    Hold Left or Right Grip in mid-air to spawn glowing neon platforms
    //    directly beneath your hands that you can climb and jump off of!
    // -------------------------------------------------------------------------

    [PluginToggleable]
    [ModCategory("Movement")]
    public sealed class AirPlatformsMod : Mod
    {
        private static readonly string[] StyleNames = { "Theme Accent", "Rainbow", "Player Color", "Cyan Glass" };
        private static readonly string[] ShapeNames = { "Rounded Disc", "Solid Pad" };

        private int _style = 0;
        private int _shape = 0;
        private float _padWidth = 0.34f;
        private bool _sticky = false;

        private GameObject _leftPad;
        private GameObject _rightPad;
        private Renderer _leftRenderer;
        private Renderer _rightRenderer;
        private bool _leftActive;
        private bool _rightActive;

        public AirPlatformsMod()
        {
            Name = "Air Platforms";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("style", "Platform color",
                "Color style of the spawned hand platforms",
                StyleNames,
                () => _style,
                value => _style = Mathf.Clamp(value, 0, StyleNames.Length - 1)));

            AddSetting(ModSetting.Dropdown("shape", "Platform shape",
                "Cylinder disc or rectangular block pad",
                ShapeNames,
                () => _shape,
                value =>
                {
                    _shape = Mathf.Clamp(value, 0, ShapeNames.Length - 1);
                    RebuildPads();
                }));

            AddSetting(ModSetting.Slider("width", "Platform size",
                "Width of each air platform in metres",
                0.2f, 0.7f,
                () => _padWidth,
                value => _padWidth = value,
                value => value.ToString("0.00") + " m"));

            AddSetting(ModSetting.Toggle("sticky", "Follow hand while held",
                "When on, the platform glides with your hand while grip is held instead of locking in place",
                () => _sticky,
                value => _sticky = value));
        }

        public override void OnEnable()
        {
            RebuildPads();
            Plugin.Log?.LogInfo("Cascade Air Platforms active.");
        }

        public override void OnDisable()
        {
            DestroyPads();
            Plugin.Log?.LogInfo("Cascade Air Platforms stopped.");
        }

        public override void Update()
        {
            if (CascadeMenuBehaviour.IsMenuOpen)
            {
                SetPadActive(_leftPad, ref _leftActive, false);
                SetPadActive(_rightPad, ref _rightActive, false);
                return;
            }

            if (_leftPad == null || _rightPad == null) RebuildPads();

            bool lGrip = false;
            bool rGrip = false;

            if (XRSettings.isDeviceActive && ControllerInputPoller.instance != null)
            {
                lGrip = ControllerInputPoller.instance.leftControllerGripFloat > 0.55f;
                rGrip = ControllerInputPoller.instance.rightControllerGripFloat > 0.55f;
            }
            else if (Keyboard.current != null)
            {
                lGrip = Keyboard.current.qKey.isPressed;
                rGrip = Keyboard.current.eKey.isPressed;
            }

            GorillaTagger tagger = GorillaTagger.Instance;
            Transform lHand = tagger != null ? tagger.leftHandTransform : null;
            Transform rHand = tagger != null ? tagger.rightHandTransform : null;

            UpdateOnePad(_leftPad, _leftRenderer, ref _leftActive, lGrip, lHand);
            UpdateOnePad(_rightPad, _rightRenderer, ref _rightActive, rGrip, rHand);
        }

        private void UpdateOnePad(GameObject pad, Renderer rend, ref bool active, bool grip, Transform hand)
        {
            if (pad == null || hand == null) return;

            if (grip)
            {
                if (!active || _sticky)
                {
                    pad.transform.position = hand.position - Vector3.up * 0.045f;
                    pad.transform.rotation = _sticky ? hand.rotation : Quaternion.identity;
                }
                Vector3 scale = _shape == 0
                    ? new Vector3(_padWidth, 0.025f, _padWidth)
                    : new Vector3(_padWidth, 0.045f, _padWidth);
                pad.transform.localScale = scale;

                if (rend != null && rend.material != null)
                {
                    rend.material.color = ResolvePadColor();
                }

                SetPadActive(pad, ref active, true);
            }
            else
            {
                SetPadActive(pad, ref active, false);
            }
        }

        private Color ResolvePadColor()
        {
            switch (_style)
            {
                case 1:
                    return Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.35f, 1f), 0.85f, 1f);
                case 2:
                    return VRRig.LocalRig != null ? PlayerActions.GetRigColor(VRRig.LocalRig) : Color.cyan;
                case 3:
                    return new Color(0.25f, 0.9f, 1f, 1f);
                default:
                    return CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.35f, 0.85f, 1f, 1f);
            }
        }

        private static void SetPadActive(GameObject pad, ref bool active, bool state)
        {
            if (pad != null && active != state)
            {
                active = state;
                pad.SetActive(state);
            }
        }

        private void RebuildPads()
        {
            DestroyPads();
            PrimitiveType prim = _shape == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            _leftPad = CreatePad("CascadeAirPad_Left", prim, out _leftRenderer);
            _rightPad = CreatePad("CascadeAirPad_Right", prim, out _rightRenderer);
            _leftActive = false;
            _rightActive = false;
        }

        private static GameObject CreatePad(string name, PrimitiveType type, out Renderer rend)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (Plugin.Root != null) go.transform.SetParent(Plugin.Root.transform, false);
            rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? ShaderLookup.SpriteUnlit();
                if (sh != null) rend.material = new Material(sh);
            }
            go.SetActive(false);
            return go;
        }

        private void DestroyPads()
        {
            if (_leftPad != null) UnityEngine.Object.Destroy(_leftPad);
            if (_rightPad != null) UnityEngine.Object.Destroy(_rightPad);
            _leftPad = null;
            _rightPad = null;
            _leftRenderer = null;
            _rightRenderer = null;
        }
    }
}
