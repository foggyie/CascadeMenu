using System;
using System.Collections.Generic;
using System.Text;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Small camera-parented HUD in the top-left of the view.
    ///
    /// It is a separate world-space canvas scaled to ~35 cm across, parented to
    /// the active view camera so it follows the head with zero per-frame maths.
    /// Everything it shows is driven by config toggles in the "HUD" section.
    ///
    /// Adding a new line is one <c>lines.Add(...)</c> below.
    /// </summary>
    internal sealed class HudOverlay : MonoBehaviour
    {
        // Canvas is authored at 500x160 UI units, then scaled down to real size.
        private const float CanvasWidth = 500f;
        private const float CanvasHeight = 160f;
        private const float CanvasScale = 0.0005f;

        // Offset from the camera: left, up, forward (metres).
        private static readonly Vector3 CameraOffset = new Vector3(-0.16f, 0.11f, 0.6f);

        private MenuConfig _config;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private TextMeshProUGUI _label;
        private Camera _camera;

        private float _fpsAccum;
        private int _fpsFrames;
        private float _fps;

        // Reused buffer so the HUD does not allocate every frame.
        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _activeMods = new List<string>();
        private readonly StringBuilder _builder = new StringBuilder(256);

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            CreateHud();
        }

        // -----------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------

        private void CreateHud()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();

            GameObject root = new GameObject("CascadeHUD", typeof(RectTransform), typeof(Canvas));

            Transform parent = _camera != null ? _camera.transform : transform;
            root.transform.SetParent(parent, false);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 200;
            _canvas.worldCamera = _camera;

            _canvasRect = (RectTransform)root.transform;
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _canvasRect.localScale = Vector3.one * CanvasScale;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);
            _canvasRect.localPosition = CameraOffset;
            _canvasRect.localRotation = Quaternion.identity;

            // Rounded dark glass background.
            RectTransform backgroundRect = CascadeUI.Rect("Background", _canvasRect);
            CascadeUI.Stretch(backgroundRect);
            Image background = backgroundRect.gameObject.AddComponent<Image>();
            background.sprite = CascadeSprites.Rounded(8);
            background.type = Image.Type.Sliced;
            background.color = new Color(0.04f, 0.045f, 0.06f, 0.78f);
            background.raycastTarget = false;

            // Single text block; no layout components, so it stays cheap.
            RectTransform labelRect = CascadeUI.Rect("Label", _canvasRect);
            CascadeUI.Stretch(labelRect, 12f, 12f, 8f, 8f);
            _label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (CascadeUI.Font != null) _label.font = CascadeUI.Font;
            _label.fontSize = 24f;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.TopLeft;
            _label.raycastTarget = false;
            _label.richText = true;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.overflowMode = TextOverflowModes.Overflow;

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Hidden until the first LateUpdate decides the config state.
            root.SetActive(false);
        }

        // -----------------------------------------------------------------
        // Per-frame update
        // -----------------------------------------------------------------

        private void LateUpdate()
        {
            if (_config == null) return;

            bool enabled = _config.HudEnabled.Value;

            if (_canvas == null)
            {
                if (enabled) CreateHud();
                return;
            }

            if (_canvas.gameObject.activeSelf != enabled)
            {
                _canvas.gameObject.SetActive(enabled);
            }
            if (!enabled) return;

            // If the game swaps cameras (third-person mods, VR re-init), re-parent.
            Camera camera = CascadeMenuBehaviour.ResolveViewCamera();
            if (camera != null && camera != _camera)
            {
                _camera = camera;
                _canvas.worldCamera = camera;
                _canvas.transform.SetParent(camera.transform, false);
                _canvasRect.localPosition = CameraOffset;
                _canvasRect.localRotation = Quaternion.identity;
            }

            UpdateFps();
            RebuildText();
        }

        private void UpdateFps()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum < 0.25f) return;

            _fps = _fpsFrames / _fpsAccum;
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }

        private void RebuildText()
        {
            _lines.Clear();
            _activeMods.Clear();

            string playerName = "Local";
            if (VRRig.LocalRig != null && !string.IsNullOrEmpty(VRRig.LocalRig.playerNameVisible))
            {
                playerName = VRRig.LocalRig.playerNameVisible;
            }

            if (_config.HudModName.Value) _lines.Add("<b>Cascade v1.0</b>");
            if (_config.HudPlayerName.Value) _lines.Add("Player: " + playerName);
            if (_config.HudFps.Value) _lines.Add("FPS: " + (_fps > 0f ? Mathf.RoundToInt(_fps).ToString() : "-"));

            if (_config.HudArrayList.Value)
            {
                CollectActiveMods();
                _lines.Add("Active: " + (_activeMods.Count > 0 ? string.Join(", ", _activeMods.ToArray()) : "None"));

                if (JoystickFlyController.IsFlying)
                {
                    _lines.Add("Fly aim: " + (_config.FlyDirection.Value == 1 ? "controller" : "head") +
                               " | D" + _config.FlyDeadzone.Value.ToString("0.00") +
                               " | Accel " + _config.FlyAcceleration.Value.ToString("0.0"));
                }
            }

            _builder.Length = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                if (i > 0) _builder.Append('\n');
                _builder.Append(_lines[i]);
            }
            _label.text = _builder.ToString();
        }

        /// <summary>Everything the HUD lists under "Active:". Add new mods here.</summary>
        private void CollectActiveMods()
        {
            if (HandLinkBlockPatch.Enabled) _activeMods.Add("HandLink");
            if (JoystickFlyController.IsFlying) _activeMods.Add("Fly");
            if (_config.GunEnabled.Value) _activeMods.Add("Gun");
            if (_config.EspEnabled.Value) _activeMods.Add("ESP Box");
            if (_config.EspNames.Value) _activeMods.Add("ESP Names");
            if (_config.EspTracers.Value) _activeMods.Add("ESP Tracers");
            if (_config.EspChams.Value) _activeMods.Add("ESP Chams");
            if (_config.EspMarker.Value) _activeMods.Add("ESP Marker");
            if (_config.EspCosmeticTags.Value) _activeMods.Add("Cosmetic ESP");
            if (_config.CosmeticNotifications.Value) _activeMods.Add("Cosmetic Notify");
        }
    }
}
