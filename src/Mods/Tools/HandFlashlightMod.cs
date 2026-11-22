using UnityEngine;
using CascadeMenu.Mods.Core;
using CascadeMenu.Menu;

namespace CascadeMenu.Mods.Tools
{
    /// <summary>
    /// Attaches a real dynamic Unity spotlight flashlight to your right hand,
    /// left hand, or head — ideal for dark maps like Caves or Night.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class HandFlashlightMod : Mod
    {
        private static readonly string[] MountNames = { "Right Hand", "Left Hand", "Head / Camera" };
        private static readonly string[] TintNames = { "Warm White", "Cool Daylight", "Tactical Cyan", "Amber" };

        private int _mount = 0;
        private int _tint = 0;
        private float _range = 32f;
        private float _intensity = 4.5f;
        private float _spotAngle = 58f;

        private GameObject _lightObj;
        private Light _light;

        public HandFlashlightMod()
        {
            Name = "VR Hand Flashlight";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("mount", "Mount on",
                "Where the dynamic spotlight attaches",
                MountNames, () => _mount, v => _mount = Mathf.Clamp(v, 0, MountNames.Length - 1)));

            AddSetting(ModSetting.Dropdown("tint", "Beam tint",
                "Light color temperature",
                TintNames, () => _tint, v => _tint = Mathf.Clamp(v, 0, TintNames.Length - 1)));

            AddSetting(ModSetting.Slider("range", "Range",
                "How far the beam reaches in metres",
                8f, 80f, () => _range, v => _range = v, v => Mathf.RoundToInt(v) + " m"));

            AddSetting(ModSetting.Slider("intensity", "Brightness",
                "Spotlight intensity",
                1f, 12f, () => _intensity, v => _intensity = v, v => v.ToString("0.0")));

            AddSetting(ModSetting.Slider("angle", "Beam width",
                "Spotlight cone angle in degrees",
                20f, 110f, () => _spotAngle, v => _spotAngle = v, v => Mathf.RoundToInt(v) + "°"));
        }

        public override void OnEnable()
        {
            CreateLight();
        }

        public override void OnDisable()
        {
            if (_lightObj != null)
            {
                Object.Destroy(_lightObj);
                _lightObj = null;
                _light = null;
            }
        }

        public override void LateUpdate()
        {
            if (_light == null) CreateLight();
            if (_light == null) return;

            Transform target = ResolveMount();
            if (target != null)
            {
                _lightObj.transform.position = target.position;
                _lightObj.transform.rotation = target.rotation;
            }

            _light.range = _range;
            _light.intensity = _intensity;
            _light.spotAngle = _spotAngle;
            _light.color = ResolveTint();
        }

        private void CreateLight()
        {
            if (_lightObj != null) Object.Destroy(_lightObj);
            _lightObj = new GameObject("CascadeFlashlight");
            if (Plugin.Root != null) _lightObj.transform.SetParent(Plugin.Root.transform, false);
            _light = _lightObj.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.shadows = LightShadows.None;
        }

        private Transform ResolveMount()
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (_mount == 0 && tagger != null && tagger.rightHandTransform != null) return tagger.rightHandTransform;
            if (_mount == 1 && tagger != null && tagger.leftHandTransform != null) return tagger.leftHandTransform;
            Camera cam = CascadeMenuBehaviour.ResolveViewCamera();
            return cam != null ? cam.transform : null;
        }

        private Color ResolveTint()
        {
            switch (_tint)
            {
                case 1: return new Color(0.88f, 0.95f, 1f);
                case 2: return new Color(0.45f, 0.92f, 1f);
                case 3: return new Color(1f, 0.78f, 0.42f);
                default: return new Color(1f, 0.96f, 0.88f);
            }
        }
    }
}
