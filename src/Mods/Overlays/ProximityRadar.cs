using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    // -------------------------------------------------------------------------
    // Proximity Radar HUD (Overlays)
    // Live 360° circular radar scanner in the corner of your view showing
    // every player as a color-matched blip with rotating sweep and range rings.
    // -------------------------------------------------------------------------

    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class ProximityRadarMod : Mod
    {
        private const float CanvasSize = 180f;
        private const float CanvasScale = 0.0005f;
        private const float RadarRadius = 72f;
        private const int MaxBlips = 16;

        private static readonly string[] CornerNames = { "Top Right", "Top Left", "Bottom Right", "Bottom Left" };

        private int _corner = 0;
        private float _range = 35f;
        private float _size = 1f;
        private float _opacity = 0.78f;
        private bool _matchColor = true;
        private bool _highlightInfected = true;
        private bool _showNames = false;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private Image _panel;
        private RectTransform _sweepRect;
        private TextMeshProUGUI _rangeLabel;

        private readonly RectTransform[] _blipRects = new RectTransform[MaxBlips];
        private readonly Image[] _blipImages = new Image[MaxBlips];
        private readonly TextMeshProUGUI[] _blipLabels = new TextMeshProUGUI[MaxBlips];
        private float _sweepAngle;

        public ProximityRadarMod()
        {
            Name = "Proximity Radar";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("corner", "Position",
                "Corner of the view where the radar sits",
                CornerNames,
                () => _corner,
                value => _corner = Mathf.Clamp(value, 0, CornerNames.Length - 1)));

            AddSetting(ModSetting.Slider("range", "Radar range",
                "Maximum detection radius in metres",
                10f, 80f,
                () => _range,
                value => _range = value,
                value => value.ToString("0") + " m"));

            AddSetting(ModSetting.Toggle("matchColor", "Match player color",
                "Color each radar dot with the player's gorilla color",
                () => _matchColor,
                value => _matchColor = value));

            AddSetting(ModSetting.Toggle("infected", "Highlight infected in red",
                "Highlight infected / tagged players as red alert blips on the radar",
                () => _highlightInfected,
                value => _highlightInfected = value));

            AddSetting(ModSetting.Toggle("names", "Show initials",
                "Show short 3-letter callsigns next to radar blips",
                () => _showNames,
                value => _showNames = value));

            AddSetting(ModSetting.Slider("size", "Radar size",
                "Overall scale of the radar HUD",
                0.6f, 1.6f,
                () => _size,
                value => _size = value,
                value => value.ToString("0.00") + "x"));

            AddSetting(ModSetting.Slider("opacity", "Opacity",
                "Background transparency",
                0.2f, 1f,
                () => _opacity,
                value => _opacity = value,
                value => (value * 100f).ToString("0") + "%"));
        }

        public override void OnEnable()
        {
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            CreateHud();
            Plugin.Log?.LogInfo("Cascade Proximity Radar active.");
        }

        public override void OnDisable()
        {
            DestroyHud();
            Plugin.Log?.LogInfo("Cascade Proximity Radar stopped.");
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

            if (_camera == null) return;

            _canvasRect.localPosition = GetCornerOffset(_corner);
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

            if (_rangeLabel != null)
            {
                OverlayText.Set(_rangeLabel, "RADAR · " + Mathf.RoundToInt(_range) + "m");
            }

            _sweepAngle = Mathf.Repeat(_sweepAngle + Time.unscaledDeltaTime * 140f, 360f);
            if (_sweepRect != null)
            {
                _sweepRect.localRotation = Quaternion.Euler(0f, 0f, -_sweepAngle);
            }

            UpdateBlips();
        }

        private static Vector3 GetCornerOffset(int corner)
        {
            switch (corner)
            {
                case 1: return new Vector3(-0.21f, 0.14f, 0.62f);  // Top Left
                case 2: return new Vector3(0.21f, -0.14f, 0.62f);  // Bottom Right
                case 3: return new Vector3(-0.21f, -0.14f, 0.62f); // Bottom Left
                default: return new Vector3(0.21f, 0.14f, 0.62f);  // Top Right
            }
        }

        private void CreateHud()
        {
            DestroyHud();
            _camera = CascadeMenuBehaviour.ResolveViewCamera();
            Transform fallback = Plugin.Root != null ? Plugin.Root.transform : null;

            _canvas = OverlayCanvas.Create("CascadeProximityRadar", _camera, fallback, 226,
                new Vector2(CanvasSize, CanvasSize + 22f),
                CanvasScale * Mathf.Max(0.1f, _size),
                GetCornerOffset(_corner), out _canvasRect, out _);

            RectTransform bg = CascadeUI.Rect("Background", _canvasRect);
            CascadeUI.Stretch(bg);
            _panel = CascadeUI.Panel(bg, new Color(0.03f, 0.045f, 0.06f, _opacity), 14);

            RectTransform border = CascadeUI.Rect("Border", _canvasRect);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, new Color(0.25f, 0.78f, 0.95f, 0.32f), 14);

            RectTransform titleRect = CascadeUI.TopLeft("Header", _canvasRect, 10f, 5f, CanvasSize - 20f, 16f);
            _rangeLabel = CascadeUI.Text(titleRect, "RADAR · 35m", 10.5f,
                new Color(0.55f, 0.88f, 1f, 0.95f), TextAlignmentOptions.Center, true, false);

            RectTransform disc = CascadeUI.Center("Disc", _canvasRect, 0f, -8f, RadarRadius * 2f, RadarRadius * 2f);
            Image discBg = CascadeUI.Image(disc, CascadeSprites.Circle(), new Color(0.01f, 0.08f, 0.11f, 0.65f), Image.Type.Simple);
            discBg.raycastTarget = false;

            BuildRing(disc, RadarRadius * 2f, new Color(0.3f, 0.85f, 1f, 0.24f));
            BuildRing(disc, RadarRadius, new Color(0.3f, 0.85f, 1f, 0.14f));

            RectTransform hLine = CascadeUI.Center("HLine", disc, 0f, 0f, RadarRadius * 2f - 4f, 1f);
            CascadeUI.Solid(hLine, new Color(0.3f, 0.85f, 1f, 0.16f));
            RectTransform vLine = CascadeUI.Center("VLine", disc, 0f, 0f, 1f, RadarRadius * 2f - 4f);
            CascadeUI.Solid(vLine, new Color(0.3f, 0.85f, 1f, 0.16f));

            _sweepRect = CascadeUI.Center("Sweep", disc, 0f, 0f, 2f, RadarRadius * 2f);
            RectTransform sweepHalf = CascadeUI.Rect("Blade", _sweepRect);
            sweepHalf.anchorMin = sweepHalf.anchorMax = new Vector2(0.5f, 0.5f);
            sweepHalf.pivot = new Vector2(0.5f, 0f);
            sweepHalf.anchoredPosition = Vector2.zero;
            sweepHalf.sizeDelta = new Vector2(2f, RadarRadius - 2f);
            CascadeUI.Solid(sweepHalf, new Color(0.35f, 1f, 0.68f, 0.55f));

            RectTransform selfDot = CascadeUI.Center("Self", disc, 0f, 0f, 6f, 6f);
            CascadeUI.Image(selfDot, CascadeSprites.Circle(), Color.white, Image.Type.Simple);

            for (int i = 0; i < MaxBlips; i++)
            {
                RectTransform blip = CascadeUI.Center("Blip" + i, disc, 0f, 0f, 8f, 8f);
                _blipRects[i] = blip;
                _blipImages[i] = CascadeUI.Image(blip, CascadeSprites.Circle(), new Color(0.3f, 1f, 0.6f, 1f), Image.Type.Simple);

                RectTransform lblRect = CascadeUI.Center("Callsign", blip, 0f, 9f, 44f, 12f);
                _blipLabels[i] = CascadeUI.Text(lblRect, string.Empty, 8.5f, Color.white, TextAlignmentOptions.Center, true, false);
                blip.gameObject.SetActive(false);
            }
        }

        private static void BuildRing(RectTransform parent, float diameter, Color color)
        {
            RectTransform ring = CascadeUI.Center("Ring", parent, 0f, 0f, diameter, diameter);
            CascadeUI.Outline(ring, color, Mathf.RoundToInt(diameter * 0.5f));
        }

        private void UpdateBlips()
        {
            List<VRRig> rigs = PlayerActions.GetRemoteRigs();
            Vector3 eye = _camera.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float maxRange = Mathf.Max(5f, _range);
            int used = 0;

            for (int i = 0; i < rigs.Count && used < MaxBlips; i++)
            {
                VRRig rig = rigs[i];
                if (rig == null) continue;

                Vector3 delta = rig.transform.position - eye;
                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                float dist = flat.magnitude;
                if (dist > maxRange * 1.15f) continue;

                float rx = Vector3.Dot(flat, right) / maxRange;
                float ry = Vector3.Dot(flat, forward) / maxRange;
                Vector2 uv = new Vector2(rx, ry);
                if (uv.sqrMagnitude > 1f) uv = uv.normalized;

                RectTransform blip = _blipRects[used];
                blip.anchoredPosition = uv * (RadarRadius - 6f);
                blip.gameObject.SetActive(true);

                NetPlayer p = GunLibController.GetPlayerFromRig(rig);
                bool isInfected = _highlightInfected && PlayerActions.IsInfected(rig, p);

                Color col;
                if (isInfected)
                {
                    col = new Color(1f, 0.23f, 0.12f, 1f);
                }
                else
                {
                    col = _matchColor
                        ? PlayerActions.GetRigColor(rig)
                        : new Color(0.3f, 1f, 0.62f, 1f);
                    Color.RGBToHSV(col, out float h, out float s, out float v);
                    if (v < 0.45f) v = 0.45f;
                    col = Color.HSVToRGB(h, s, v);
                    col.a = 0.96f;
                }
                _blipImages[used].color = col;

                TextMeshProUGUI lbl = _blipLabels[used];
                if (_showNames)
                {
                    string name = p != null ? PlayerActions.SafeName(p) : (rig.playerNameVisible ?? "PLR");
                    if (name.Length > 3) name = name.Substring(0, 3).ToUpperInvariant();
                    OverlayText.Set(lbl, name);
                    lbl.color = col;
                    lbl.gameObject.SetActive(true);
                }
                else
                {
                    lbl.gameObject.SetActive(false);
                }

                used++;
            }

            for (int i = used; i < MaxBlips; i++)
            {
                if (_blipRects[i] != null && _blipRects[i].gameObject.activeSelf)
                {
                    _blipRects[i].gameObject.SetActive(false);
                }
            }
        }

        private void DestroyHud()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
                _canvasRect = null;
                _panel = null;
                _sweepRect = null;
                _rangeLabel = null;
            }
        }
    }
}
