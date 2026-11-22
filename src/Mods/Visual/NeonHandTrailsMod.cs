using UnityEngine;
using UnityEngine.Rendering;
using CascadeMenu.Mods.Core;
using CascadeMenu.UI;

namespace CascadeMenu.Mods.Visual
{
    /// <summary>
    /// Smooth glowing neon ribbon trails streaming behind your left and right hands.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class NeonHandTrailsMod : Mod
    {
        private static readonly string[] ColorModes = { "Rainbow Cycle", "Match Cascade Theme", "Cyber Cyan", "Neon Rose", "Solar Gold" };

        private int _colorMode = 0;
        private float _trailTime = 0.55f;
        private float _trailWidth = 0.045f;

        private GameObject _leftObj;
        private GameObject _rightObj;
        private TrailRenderer _leftTrail;
        private TrailRenderer _rightTrail;
        private Material _material;

        public NeonHandTrailsMod()
        {
            Name = "Neon Hand Trails";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("color", "Trail color",
                "Color palette for your glowing hand ribbons",
                ColorModes, () => _colorMode, v => _colorMode = Mathf.Clamp(v, 0, ColorModes.Length - 1)));

            AddSetting(ModSetting.Slider("time", "Trail length",
                "How many seconds each hand ribbon lingers in the air",
                0.15f, 1.8f, () => _trailTime, v => _trailTime = v, v => v.ToString("0.00") + " s"));

            AddSetting(ModSetting.Slider("width", "Trail width",
                "Ribbon thickness in metres",
                0.01f, 0.12f, () => _trailWidth, v => _trailWidth = v, v => v.ToString("0.000") + " m"));
        }

        public override void OnEnable()
        {
            EnsureTrails();
        }

        public override void OnDisable()
        {
            if (_leftObj != null) { Object.Destroy(_leftObj); _leftObj = null; _leftTrail = null; }
            if (_rightObj != null) { Object.Destroy(_rightObj); _rightObj = null; _rightTrail = null; }
            if (_material != null) { Object.Destroy(_material); _material = null; }
        }

        public override void LateUpdate()
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null) return;

            if (_leftTrail == null || _rightTrail == null) EnsureTrails();

            if (_leftObj != null && tagger.leftHandTransform != null)
                _leftObj.transform.position = tagger.leftHandTransform.position;
            if (_rightObj != null && tagger.rightHandTransform != null)
                _rightObj.transform.position = tagger.rightHandTransform.position;

            Color c = ResolveColor();
            ApplyTrailStyle(_leftTrail, c);
            ApplyTrailStyle(_rightTrail, c);
        }

        private void EnsureTrails()
        {
            if (_material == null)
            {
                Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Unlit/Color");
                if (sh != null) _material = new Material(sh) { color = Color.white };
            }

            if (_leftObj == null)
            {
                _leftObj = new GameObject("CascadeLeftHandTrail");
                _leftTrail = ConfigureTrail(_leftObj);
            }
            if (_rightObj == null)
            {
                _rightObj = new GameObject("CascadeRightHandTrail");
                _rightTrail = ConfigureTrail(_rightObj);
            }
        }

        private TrailRenderer ConfigureTrail(GameObject go)
        {
            if (Plugin.Root != null) go.transform.SetParent(Plugin.Root.transform, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.minVertexDistance = 0.015f;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.numCapVertices = 4;
            if (_material != null) tr.material = _material;
            return tr;
        }

        private void ApplyTrailStyle(TrailRenderer tr, Color c)
        {
            if (tr == null) return;
            tr.time = _trailTime;
            tr.startWidth = _trailWidth;
            tr.endWidth = 0.002f;
            tr.startColor = new Color(c.r, c.g, c.b, 0.92f);
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
        }

        private Color ResolveColor()
        {
            switch (_colorMode)
            {
                case 1: return CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.28f, 0.66f, 1f);
                case 2: return new Color(0.18f, 0.90f, 1f);
                case 3: return new Color(1f, 0.28f, 0.68f);
                case 4: return new Color(1f, 0.80f, 0.22f);
                default: return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.28f, 1f), 0.88f, 1f);
            }
        }
    }
}
