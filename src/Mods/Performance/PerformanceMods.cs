using GorillaLocomotion;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Performance
{
    /// <summary>Disables real-time shadows.</summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class ShadowOffMod : Mod
    {
        private ShadowQuality _previous;
        private bool _saved;

        public ShadowOffMod()
        {
            Name = "Shadow Off";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previous = QualitySettings.shadows;
            _saved = true;
            QualitySettings.shadows = ShadowQuality.Disable;
        }

        public override void OnDisable()
        {
            if (_saved) QualitySettings.shadows = _previous;
            _saved = false;
        }
    }

    /// <summary>Keeps shadow maps close so they cost less.</summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class ShadowDistanceLowMod : Mod
    {
        private float _previous;
        private bool _saved;
        private float _distance = 24f;

        public ShadowDistanceLowMod()
        {
            Name = "Short Shadows";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "distance", "Distance",
                "Shadow draw distance in metres",
                5f, 80f,
                () => _distance,
                value => _distance = value,
                value => value.ToString("0") + " m"));
        }

        public override void OnEnable()
        {
            _previous = QualitySettings.shadowDistance;
            _saved = true;
            QualitySettings.shadowDistance = _distance;
        }

        public override void OnDisable()
        {
            if (_saved) QualitySettings.shadowDistance = _previous;
            _saved = false;
        }
    }

    /// <summary>Shared frame-rate cap that clears VSync while it runs.</summary>
    public abstract class FpsCapMod : Mod
    {
        private int _previousRate;
        private int _previousVSync;
        private bool _saved;

        protected FpsCapMod()
        {
            isToggle = true;
        }

        protected abstract int Cap { get; }

        public override void OnEnable()
        {
            _previousRate = Application.targetFrameRate;
            _previousVSync = QualitySettings.vSyncCount;
            _saved = true;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Cap;
            Plugin.Log?.LogInfo("Cascade " + Name + " active at " + Cap + " FPS.");
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            Application.targetFrameRate = _previousRate;
            QualitySettings.vSyncCount = _previousVSync;
            _saved = false;
        }
    }

    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class FpsCap60Mod : FpsCapMod
    {
        public FpsCap60Mod()
        {
            Name = "Cap 60 FPS";
        }

        protected override int Cap
        {
            get { return 60; }
        }
    }

    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class FpsCap72Mod : FpsCapMod
    {
        public FpsCap72Mod()
        {
            Name = "Cap 72 FPS";
        }

        protected override int Cap
        {
            get { return 72; }
        }
    }

    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class FpsCap90Mod : FpsCapMod
    {
        public FpsCap90Mod()
        {
            Name = "Cap 90 FPS";
        }

        protected override int Cap
        {
            get { return 90; }
        }
    }

    /// <summary>Hides the body meshes of every remote player.</summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class PlayerCullingMod : Mod
    {
        private float _nextApply;

        public PlayerCullingMod()
        {
            Name = "Player Culling";
            isToggle = true;
        }

        public override void OnEnable()
        {
            Apply();
            Plugin.Log?.LogInfo("Cascade Player Culling active.");
        }

        public override void OnDisable()
        {
            SetVisible(true);
            Plugin.Log?.LogInfo("Cascade Player Culling stopped.");
        }

        public override void Update()
        {
            if (Time.unscaledTime < _nextApply) return;
            _nextApply = Time.unscaledTime + 0.5f;
            Apply();
        }

        private void Apply()
        {
            SetVisible(false);
        }

        private static void SetVisible(bool visible)
        {
            var players = PlayerActions.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                VRRig rig = PlayerActions.FindRigObject(players[i]);
                if (rig == null || rig == VRRig.LocalRig) continue;
                if (rig.mainSkin != null) rig.mainSkin.enabled = visible;
                if (rig.faceSkin != null) rig.faceSkin.enabled = visible;
            }
        }
    }

    /// <summary>Lowers the level-of-detail bias.</summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class LodBiasLowMod : Mod
    {
        private float _previous;
        private bool _saved;
        private float _bias = 0.6f;

        public LodBiasLowMod()
        {
            Name = "LOD Bias";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "bias", "Bias",
                "Level-of-detail bias (lower switches models sooner)",
                0.2f, 2f,
                () => _bias,
                value => _bias = value,
                value => value.ToString("0.0")));
        }

        public override void OnEnable()
        {
            _previous = QualitySettings.lodBias;
            _saved = true;
            QualitySettings.lodBias = _bias;
        }

        public override void OnDisable()
        {
            if (_saved) QualitySettings.lodBias = _previous;
            _saved = false;
        }
    }

    /// <summary>Uses lower-resolution textures.</summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class TextureQualityLowMod : Mod
    {
        private int _previous;
        private bool _saved;
        private int _limit = 2;

        public TextureQualityLowMod()
        {
            Name = "Texture Quality";
            isToggle = true;

            AddSetting(ModSetting.Stepper(
                "limit", "Mip level",
                "How many mip levels to skip (higher is blurrier and cheaper)",
                0, 3,
                () => _limit,
                value => _limit = value,
                value => "L" + value));
        }

        public override void OnEnable()
        {
            _previous = QualitySettings.globalTextureMipmapLimit;
            _saved = true;
            QualitySettings.globalTextureMipmapLimit = _limit;
        }

        public override void OnDisable()
        {
            if (_saved) QualitySettings.globalTextureMipmapLimit = _previous;
            _saved = false;
        }
    }

    /// <summary>Turns multisample anti-aliasing off.</summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class AntiAliasingOffMod : Mod
    {
        private int _previous;
        private bool _saved;

        public AntiAliasingOffMod()
        {
            Name = "AA Off";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previous = QualitySettings.antiAliasing;
            _saved = true;
            QualitySettings.antiAliasing = 0;
        }

        public override void OnDisable()
        {
            if (_saved) QualitySettings.antiAliasing = _previous;
            _saved = false;
        }
    }

    /// <summary>Caps how many pixel lights each object may receive.</summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class PixelLightLowMod : Mod
    {
        private int _previous;
        private bool _saved;
        private int _lights = 1;

        public PixelLightLowMod()
        {
            Name = "Pixel Lights";
            isToggle = true;

            AddSetting(ModSetting.Stepper(
                "lights", "Lights",
                "Maximum pixel lights per object",
                0, 4,
                () => _lights,
                value => _lights = value));
        }

        public override void OnEnable()
        {
            _previous = QualitySettings.pixelLightCount;
            _saved = true;
            QualitySettings.pixelLightCount = _lights;
        }

        public override void OnDisable()
        {
            if (_saved) QualitySettings.pixelLightCount = _previous;
            _saved = false;
        }
    }
}
