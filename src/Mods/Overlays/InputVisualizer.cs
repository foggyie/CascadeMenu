using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Overlays
{
    // -------------------------------------------------------------------------
    // Controller Input Visualizer HUD (Overlays)
    // Live mini telemetry dashboard showing left & right trigger/grip bars,
    // 2D thumbstick/WASD dot pads, and face button lights (A/B/X/Y).
    // -------------------------------------------------------------------------

    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class InputVisualizerMod : Mod
    {
        private const float CanvasWidth = 250f;
        private const float CanvasHeight = 104f;
        private const float CanvasScale = 0.0005f;

        private static readonly string[] PosNames = { "Bottom Left", "Bottom Right", "Top Left", "Top Right" };

        private int _pos = 0;
        private float _size = 1f;
        private float _opacity = 0.76f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private Image _panel;

        private RectTransform _leftStickDot;
        private RectTransform _rightStickDot;
        private RectTransform _lTrigFill;
        private RectTransform _lGripFill;
        private RectTransform _rTrigFill;
        private RectTransform _rGripFill;
        private Image _btnX;
        private Image _btnY;
        private Image _btnA;
        private Image _btnB;

        public InputVisualizerMod()
        {
            Name = "Input Visualizer";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("pos", "Position",
                "Where the input visualizer card sits in your view",
                PosNames,
                () => _pos,
                value => _pos = Mathf.Clamp(value, 0, PosNames.Length - 1)));

            AddSetting(ModSetting.Slider("size", "Scale",
                "Size of the input visualizer HUD",
                0.6f, 1.6f,
                () => _size,
                value => _size = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider("opacity", "Opacity",
                "Background opacity",
                0.2f, 1f,
                () => _opacity,
                value => _opacity = value,
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            CreateHud();
            Plugin.Log?.LogInfo("Cascade Input Visualizer active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Input Visualizer stopped.");
        }

        public override void LateUpdate()
        {
            if (_canvas == null)
            {
                CreateHud();
                if (_canvas == null) return;
            }

            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            if (camera != null && camera != _camera)
            {
                _camera = camera;
                _canvas.worldCamera = camera;
                _canvas.transform.SetParent(camera.transform, false);
                _canvasRect.localRotation = Quaternion.identity;
            }

            _canvasRect.localPosition = GetOffset(_pos);
            float scale = CanvasScale * Mathf.Max(0.1f, _size);
            if (!Mathf.Approximately(_canvasRect.localScale.x, scale))
            {
                _canvasRect.localScale = Vector3.one * scale;
            }

            if (_panel != null)
            {
                Color c = _panel.color;
                c.a = _opacity;
                _panel.color = c;
            }

            SampleInputs();
        }

        private static Vector3 GetOffset(int pos)
        {
            switch (pos)
            {
                case 1: return new Vector3(0.20f, -0.17f, 0.62f);  // Bottom Right
                case 2: return new Vector3(-0.20f, 0.16f, 0.62f);  // Top Left
                case 3: return new Vector3(0.20f, 0.16f, 0.62f);   // Top Right
                default: return new Vector3(-0.20f, -0.17f, 0.62f); // Bottom Left
            }
        }

        private void CreateHud()
        {
            DestroyHud();
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            Transform fallback = Plugin.Root != null ? Plugin.Root.transform : null;

            _canvas = OverlayCanvas.Create("CascadeInputVisualizer", _camera, fallback, 225,
                new Vector2(CanvasWidth, CanvasHeight),
                CanvasScale * Mathf.Max(0.1f, _size),
                GetOffset(_pos), out _canvasRect, out _);

            RectTransform bg = CascadeUI.Rect("Background", _canvasRect);
            CascadeUI.Stretch(bg);
            _panel = CascadeUI.Panel(bg, new Color(0.04f, 0.045f, 0.065f, _opacity), 12);

            RectTransform border = CascadeUI.Rect("Border", _canvasRect);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, new Color(1f, 1f, 1f, 0.16f), 12);

            RectTransform title = CascadeUI.TopLeft("Title", _canvasRect, 10f, 5f, CanvasWidth - 20f, 14f);
            CascadeUI.Text(title, "INPUT TELEMETRY", 9.5f,
                new Color(0.68f, 0.78f, 0.90f, 0.9f), TextAlignmentOptions.Center, true, false);

            _leftStickDot = BuildStickPad(_canvasRect, "LStick", new Vector2(-82f, -6f));
            _lTrigFill = BuildBar(_canvasRect, "LT", new Vector2(-36f, 10f), new Color(0.35f, 0.85f, 1f, 0.95f));
            _lGripFill = BuildBar(_canvasRect, "LG", new Vector2(-36f, -6f), new Color(0.38f, 1f, 0.65f, 0.95f));
            _btnX = BuildButtonDot(_canvasRect, "X", new Vector2(-42f, -26f));
            _btnY = BuildButtonDot(_canvasRect, "Y", new Vector2(-24f, -26f));

            _rightStickDot = BuildStickPad(_canvasRect, "RStick", new Vector2(82f, -6f));
            _rTrigFill = BuildBar(_canvasRect, "RT", new Vector2(36f, 10f), new Color(0.35f, 0.85f, 1f, 0.95f));
            _rGripFill = BuildBar(_canvasRect, "RG", new Vector2(36f, -6f), new Color(0.38f, 1f, 0.65f, 0.95f));
            _btnA = BuildButtonDot(_canvasRect, "A", new Vector2(24f, -26f));
            _btnB = BuildButtonDot(_canvasRect, "B", new Vector2(42f, -26f));
        }

        private static RectTransform BuildStickPad(RectTransform parent, string name, Vector2 pos)
        {
            RectTransform ring = CascadeUI.Center(name, parent, pos.x, pos.y, 46f, 46f);
            CascadeUI.Image(ring, CascadeSprites.Circle(), new Color(1f, 1f, 1f, 0.08f), Image.Type.Simple);
            CascadeUI.Outline(ring, new Color(1f, 1f, 1f, 0.22f), 23);

            RectTransform dot = CascadeUI.Center("Dot", ring, 0f, 0f, 10f, 10f);
            CascadeUI.Image(dot, CascadeSprites.Circle(), new Color(0.4f, 0.88f, 1f, 1f), Image.Type.Simple);
            return dot;
        }

        private static RectTransform BuildBar(RectTransform parent, string label, Vector2 pos, Color fillCol)
        {
            RectTransform track = CascadeUI.Center("Bar_" + label, parent, pos.x, pos.y, 34f, 8f);
            CascadeUI.Panel(track, new Color(1f, 1f, 1f, 0.12f), 4);

            RectTransform fill = CascadeUI.TopLeft("Fill", track, 0f, 0f, 0f, 8f);
            CascadeUI.Panel(fill, fillCol, 4);
            return fill;
        }

        private static Image BuildButtonDot(RectTransform parent, string label, Vector2 pos)
        {
            RectTransform dot = CascadeUI.Center("Btn_" + label, parent, pos.x, pos.y, 15f, 15f);
            Image img = CascadeUI.Image(dot, CascadeSprites.Circle(), new Color(1f, 1f, 1f, 0.14f), Image.Type.Simple);
            RectTransform txt = CascadeUI.Rect("Lbl", dot);
            CascadeUI.Stretch(txt);
            CascadeUI.Text(txt, label, 8f, Color.white, TextAlignmentOptions.Center, true, false);
            return img;
        }

        private void SampleInputs()
        {
            Vector2 lStick = Vector2.zero;
            Vector2 rStick = Vector2.zero;
            float lTrig = 0f, lGrip = 0f, rTrig = 0f, rGrip = 0f;
            bool xHeld = false, yHeld = false, aHeld = false, bHeld = false;

            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (XRSettings.isDeviceActive && poller != null)
            {
                lStick = poller.leftControllerPrimary2DAxis;
                rStick = poller.rightControllerPrimary2DAxis;
                lTrig = poller.leftControllerIndexFloat;
                lGrip = poller.leftControllerGripFloat;
                rTrig = poller.rightControllerIndexFloat;
                rGrip = poller.rightControllerGripFloat;
                xHeld = poller.leftControllerPrimaryButton;
                yHeld = poller.leftControllerSecondaryButton;
                aHeld = poller.rightControllerPrimaryButton;
                bHeld = poller.rightControllerSecondaryButton;
            }
            else
            {
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed) lStick.y += 1f;
                    if (kb.sKey.isPressed) lStick.y -= 1f;
                    if (kb.dKey.isPressed) lStick.x += 1f;
                    if (kb.aKey.isPressed) lStick.x -= 1f;
                    if (lStick.sqrMagnitude > 1f) lStick.Normalize();

                    xHeld = kb.qKey.isPressed;
                    yHeld = kb.eKey.isPressed;
                    aHeld = kb.spaceKey.isPressed;
                    bHeld = kb.leftShiftKey.isPressed;
                }
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    rTrig = mouse.leftButton.isPressed ? 1f : 0f;
                    rGrip = mouse.rightButton.isPressed ? 1f : 0f;
                    lTrig = mouse.middleButton.isPressed ? 1f : 0f;
                    Vector2 delta = mouse.delta.ReadValue() * 0.04f;
                    rStick = Vector2.ClampMagnitude(delta, 1f);
                }
            }

            if (_leftStickDot != null) _leftStickDot.anchoredPosition = lStick * 15f;
            if (_rightStickDot != null) _rightStickDot.anchoredPosition = rStick * 15f;
            if (_lTrigFill != null) _lTrigFill.sizeDelta = new Vector2(34f * Mathf.Clamp01(lTrig), 8f);
            if (_lGripFill != null) _lGripFill.sizeDelta = new Vector2(34f * Mathf.Clamp01(lGrip), 8f);
            if (_rTrigFill != null) _rTrigFill.sizeDelta = new Vector2(34f * Mathf.Clamp01(rTrig), 8f);
            if (_rGripFill != null) _rGripFill.sizeDelta = new Vector2(34f * Mathf.Clamp01(rGrip), 8f);

            Color onCol = new Color(0.35f, 0.92f, 1f, 0.95f);
            Color offCol = new Color(1f, 1f, 1f, 0.14f);
            if (_btnX != null) _btnX.color = xHeld ? onCol : offCol;
            if (_btnY != null) _btnY.color = yHeld ? onCol : offCol;
            if (_btnA != null) _btnA.color = aHeld ? onCol : offCol;
            if (_btnB != null) _btnB.color = bHeld ? onCol : offCol;
        }

        private void DestroyHud()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
            }
        }
    }
}
