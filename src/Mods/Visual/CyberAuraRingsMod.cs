using System.Collections.Generic;
using CascadeMenu.UI;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Visual
{
    // -------------------------------------------------------------------------
    // Cyber Aura Rings (Visual)
    // Animated 3D holographic halo & base rings orbiting your gorilla (or
    // every player in the room) with customizable rings, speed, and color.
    // -------------------------------------------------------------------------

    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class CyberAuraRingsMod : Mod
    {
        private const int RingSegments = 36;
        private static readonly string[] TargetNames = { "Self Only", "All Players", "Other Players Only" };
        private static readonly string[] ColorModes = { "Match Player Color", "Theme Accent", "Rainbow Wave", "Cyber Cyan" };

        private int _targetMode = 1;
        private int _colorMode = 0;
        private float _radius = 0.55f;
        private float _spinSpeed = 120f;
        private bool _crownHalo = true;

        private sealed class AuraSlot
        {
            public GameObject Root;
            public LineRenderer BaseRing;
            public LineRenderer OrbitRing;
            public LineRenderer HaloRing;
        }

        private readonly List<AuraSlot> _slots = new List<AuraSlot>();
        private Material _material;

        public CyberAuraRingsMod()
        {
            Name = "Cyber Aura Rings";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("targets", "Targets",
                "Draw aura rings around yourself, everyone, or other players",
                TargetNames,
                () => _targetMode,
                value => _targetMode = Mathf.Clamp(value, 0, TargetNames.Length - 1)));

            AddSetting(ModSetting.Dropdown("color", "Color mode",
                "Match each player's color, theme accent, or rainbow wave",
                ColorModes,
                () => _colorMode,
                value => _colorMode = Mathf.Clamp(value, 0, ColorModes.Length - 1)));

            AddSetting(ModSetting.Slider("radius", "Ring radius",
                "Radius of the orbiting aura rings",
                0.3f, 1.2f,
                () => _radius,
                value => _radius = value,
                value => value.ToString("0.00") + " m"));

            AddSetting(ModSetting.Slider("speed", "Orbit speed",
                "Rotation speed in degrees per second",
                20f, 360f,
                () => _spinSpeed,
                value => _spinSpeed = value,
                value => value.ToString("0") + "°/s"));

            AddSetting(ModSetting.Toggle("halo", "Crown halo",
                "Include a floating halo ring above the player's head",
                () => _crownHalo,
                value => _crownHalo = value));
        }

        public override void OnEnable()
        {
            Shader sh = Shader.Find("GUI/Text Shader") ?? ShaderLookup.SpriteUnlit();
            if (sh != null)
            {
                _material = new Material(sh) { color = Color.white, renderQueue = 4200 };
            }
            Plugin.Log?.LogInfo("Cascade Cyber Aura Rings active.");
        }

        public override void OnDisable()
        {
            ClearSlots(0);
            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
                _material = null;
            }
        }

        public override void LateUpdate()
        {
            if (_material == null) return;

            int used = 0;
            float time = Time.unscaledTime;

            if (_targetMode == 0 || _targetMode == 1)
            {
                if (VRRig.LocalRig != null)
                {
                    DrawAuraForRig(GetSlot(used++), VRRig.LocalRig, time, 0);
                }
            }

            if (_targetMode == 1 || _targetMode == 2)
            {
                List<VRRig> rigs = PlayerActions.GetRemoteRigs();
                for (int i = 0; i < rigs.Count; i++)
                {
                    VRRig r = rigs[i];
                    if (r == null) continue;
                    DrawAuraForRig(GetSlot(used++), r, time, i + 1);
                }
            }

            ClearSlots(used);
        }

        private void DrawAuraForRig(AuraSlot slot, VRRig rig, float time, int seed)
        {
            float scale = rig.scaleFactor > 0.01f ? rig.scaleFactor : 1f;
            Color col = ResolveAuraColor(rig, time, seed);

            Vector3 torso = rig.transform.position;
            float r = _radius * scale;
            float angle = (time * _spinSpeed + seed * 45f) * Mathf.Deg2Rad;

            float pulse = 1f + Mathf.Sin(time * 3f + seed) * 0.08f;
            Vector3 baseCenter = torso - Vector3.up * (0.32f * scale);
            DrawCircle(slot.BaseRing, baseCenter, Quaternion.identity, r * pulse, col, 0.008f);

            Quaternion tilt = Quaternion.Euler(
                Mathf.Sin(angle) * 28f,
                time * _spinSpeed,
                Mathf.Cos(angle) * 28f);
            Color orbitCol = Color.Lerp(col, Color.white, 0.25f);
            orbitCol.a = 0.78f;
            DrawCircle(slot.OrbitRing, torso, tilt, r * 0.92f, orbitCol, 0.0065f);

            if (_crownHalo)
            {
                Transform head = rig.headConstraint != null ? rig.headConstraint : rig.transform;
                Vector3 haloCenter = head.position + Vector3.up * (0.26f * scale);
                Color haloCol = Color.Lerp(col, new Color(1f, 0.95f, 0.55f, 1f), 0.35f);
                DrawCircle(slot.HaloRing, haloCenter, Quaternion.identity, r * 0.42f, haloCol, 0.007f);
            }
            else if (slot.HaloRing != null)
            {
                slot.HaloRing.enabled = false;
            }
        }

        private Color ResolveAuraColor(VRRig rig, float time, int seed)
        {
            Color c;
            switch (_colorMode)
            {
                case 1:
                    c = CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.35f, 0.85f, 1f, 1f);
                    break;
                case 2:
                    c = Color.HSVToRGB(Mathf.Repeat(time * 0.25f + seed * 0.12f, 1f), 0.85f, 1f);
                    break;
                case 3:
                    c = new Color(0.2f, 0.92f, 1f, 1f);
                    break;
                default:
                    c = PlayerActions.GetRigColor(rig);
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    if (v < 0.45f) v = 0.45f;
                    c = Color.HSVToRGB(h, s, v);
                    break;
            }
            c.a = 0.88f;
            return c;
        }

        private static void DrawCircle(LineRenderer lr, Vector3 center, Quaternion rot, float radius, Color color, float width)
        {
            if (lr == null) return;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;

            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                lr.SetPosition(i, center + rot * local);
            }
            lr.enabled = true;
        }

        private AuraSlot GetSlot(int index)
        {
            while (_slots.Count <= index)
            {
                var slot = new AuraSlot();
                slot.Root = new GameObject("CascadeAura_" + _slots.Count);
                if (Plugin.Root != null) slot.Root.transform.SetParent(Plugin.Root.transform, false);
                slot.BaseRing = MakeLoop(slot.Root.transform, "BaseRing");
                slot.OrbitRing = MakeLoop(slot.Root.transform, "OrbitRing");
                slot.HaloRing = MakeLoop(slot.Root.transform, "HaloRing");
                _slots.Add(slot);
            }
            return _slots[index];
        }

        private LineRenderer MakeLoop(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = RingSegments;
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = _material;
            lr.enabled = false;
            return lr;
        }

        private void ClearSlots(int from)
        {
            for (int i = _slots.Count - 1; i >= from; i--)
            {
                if (_slots[i].Root != null) UnityEngine.Object.Destroy(_slots[i].Root);
                _slots.RemoveAt(i);
            }
        }
    }
}
