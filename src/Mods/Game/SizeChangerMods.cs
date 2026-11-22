using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Game
{
    /// <summary>
    /// Size changer manipulation using reflection to access the game's SizeChanger system.
    /// Based on the actual SizeChanger, SizeManager, and Size LayerChanger game code.
    /// </summary>
    internal static class SizeChangerAPI
    {
        private static bool _resolved;
        private static System.Type _sizeChangerType;
        private static FieldInfo _maxScaleField;
        private static FieldInfo _minScaleField;
        private static FieldInfo _staticEasingField;

        internal static bool Ready
        {
            get
            {
                Resolve();
                return _sizeChangerType != null && _maxScaleField != null;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                // Find SizeChanger type in loaded assemblies
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    _sizeChangerType = assembly.GetType("SizeChanger");
                    if (_sizeChangerType != null) break;
                }

                if (_sizeChangerType != null)
                {
                    _maxScaleField = _sizeChangerType.GetField("maxScale", BindingFlags.NonPublic | BindingFlags.Instance);
                    _minScaleField = _sizeChangerType.GetField("minScale", BindingFlags.NonPublic | BindingFlags.Instance);
                    _staticEasingField = _sizeChangerType.GetField("staticEasing", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                Plugin.Log?.LogInfo($"SizeChanger API: {(_maxScaleField != null ? "Ready" : "Not available")}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"SizeChanger API failed: {ex.Message}");
            }
        }

        internal static List<Component> FindAllSizeChangers()
        {
            if (_sizeChangerType == null) return new List<Component>();
            Component[] found = Object.FindObjectsByType(_sizeChangerType, FindObjectsSortMode.None) as Component[];
            return found != null ? new List<Component>(found) : new List<Component>();
        }

        internal static bool SetMaxScale(Component changer, float scale)
        {
            if (_maxScaleField == null || changer == null) return false;
            try
            {
                _maxScaleField.SetValue(changer, scale);
                return true;
            }
            catch { return false; }
        }

        internal static bool SetMinScale(Component changer, float scale)
        {
            if (_minScaleField == null || changer == null) return false;
            try
            {
                _minScaleField.SetValue(changer, Mathf.Max(scale, 0.01f));
                return true;
            }
            catch { return false; }
        }

        internal static float GetMaxScale(Component changer)
        {
            if (_maxScaleField == null || changer == null) return 1f;
            try { return (float)_maxScaleField.GetValue(changer); }
            catch { return 1f; }
        }

        internal static float GetMinScale(Component changer)
        {
            if (_minScaleField == null || changer == null) return 1f;
            try { return (float)_minScaleField.GetValue(changer); }
            catch { return 1f; }
        }
    }

    // -------------------------------------------------------------------------
    // Size Changer Manipulation Mods
    // -------------------------------------------------------------------------

    /// <summary>Sets all size changers to maximum scale values.</summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class SizeChangerMaxMod : Mod
    {
        private List<Component> _changers = new List<Component>();
        private Dictionary<Component, float> _originalMax = new Dictionary<Component, float>();
        private float _maxScale = 100f;

        public SizeChangerMaxMod()
        {
            Name = "Size Changer Max";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "maxScale", "Maximum Scale",
                "Maximum player scale to set on changers",
                1f, 200f,
                () => _maxScale,
                value =>
                {
                    _maxScale = value;
                    if (IsActive) ApplyMaxScale();
                },
                value => value.ToString("0") + "x"));
        }

        public override void OnEnable()
        {
            if (!SizeChangerAPI.Ready)
            {
                ModKit.Notify("Size Changer", "SizeChanger system not available");
                return;
            }

            FindAndBackup();
            ApplyMaxScale();
            ModKit.Notify("Size Changer", $"Set {_changers.Count} changers to {_maxScale}x max");
            Plugin.Log?.LogInfo($"Size Changer Max: {_changers.Count} changers");
        }

        public override void OnDisable()
        {
            Restore();
            Plugin.Log?.LogInfo("Size Changer Max stopped.");
        }

        private void FindAndBackup()
        {
            _changers = SizeChangerAPI.FindAllSizeChangers();
            _originalMax.Clear();
            foreach (var changer in _changers)
            {
                _originalMax[changer] = SizeChangerAPI.GetMaxScale(changer);
            }
        }

        private void ApplyMaxScale()
        {
            foreach (var changer in _changers)
            {
                if (changer != null) SizeChangerAPI.SetMaxScale(changer, _maxScale);
            }
        }

        private void Restore()
        {
            foreach (var changer in _changers)
            {
                if (changer != null && _originalMax.ContainsKey(changer))
                {
                    SizeChangerAPI.SetMaxScale(changer, _originalMax[changer]);
                }
            }
            _changers.Clear();
            _originalMax.Clear();
        }
    }

    /// <summary>Sets all size changers to minimum scale values.</summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class SizeChangerMinMod : Mod
    {
        private List<Component> _changers = new List<Component>();
        private Dictionary<Component, float> _originalMin = new Dictionary<Component, float>();
        private float _minScale = 0.1f;

        public SizeChangerMinMod()
        {
            Name = "Size Changer Min";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "minScale", "Minimum Scale",
                "Minimum player scale to set on changers",
                0.01f, 1f,
                () => _minScale,
                value =>
                {
                    _minScale = value;
                    if (IsActive) ApplyMinScale();
                },
                value => value.ToString("0.00") + "x"));
        }

        public override void OnEnable()
        {
            if (!SizeChangerAPI.Ready)
            {
                ModKit.Notify("Size Changer", "SizeChanger system not available");
                return;
            }

            FindAndBackup();
            ApplyMinScale();
            ModKit.Notify("Size Changer", $"Set {_changers.Count} changers to {_minScale}x min");
            Plugin.Log?.LogInfo($"Size Changer Min: {_changers.Count} changers");
        }

        public override void OnDisable()
        {
            Restore();
            Plugin.Log?.LogInfo("Size Changer Min stopped.");
        }

        private void FindAndBackup()
        {
            _changers = SizeChangerAPI.FindAllSizeChangers();
            _originalMin.Clear();
            foreach (var changer in _changers)
            {
                _originalMin[changer] = SizeChangerAPI.GetMinScale(changer);
            }
        }

        private void ApplyMinScale()
        {
            foreach (var changer in _changers)
            {
                if (changer != null) SizeChangerAPI.SetMinScale(changer, _minScale);
            }
        }

        private void Restore()
        {
            foreach (var changer in _changers)
            {
                if (changer != null && _originalMin.ContainsKey(changer))
                {
                    SizeChangerAPI.SetMinScale(changer, _originalMin[changer]);
                }
            }
            _changers.Clear();
            _originalMin.Clear();
        }
    }

    /// <summary>Forces player to specific size regardless of changers.</summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class ForcePlayerSizeMod : Mod
    {
        private float _forcedScale = 10f;
        private List<Component> _changers = new List<Component>();

        public ForcePlayerSizeMod()
        {
            Name = "Force Player Giant";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "size", "Giant Size",
                "Force all size changers to make you this big",
                1f, 200f,
                () => _forcedScale,
                value => _forcedScale = value,
                value => value.ToString("0") + "x"));
        }

        public override void OnEnable()
        {
            if (!SizeChangerAPI.Ready)
            {
                ModKit.Notify("Player Size", "SizeChanger not available");
                return;
            }

            _changers = SizeChangerAPI.FindAllSizeChangers();
            foreach (var changer in _changers)
            {
                if (changer != null)
                {
                    SizeChangerAPI.SetMaxScale(changer, _forcedScale);
                    SizeChangerAPI.SetMinScale(changer, _forcedScale);
                }
            }
            ModKit.Notify("Player Size", $"Forced {_changers.Count} changers to {_forcedScale}x");
        }

        public override void OnDisable()
        {
            // Restore happens automatically when other mods disable
            _changers.Clear();
        }
    }

    /// <summary>Shows overlay with size changer information.</summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class SizeChangerInfoOverlay : OverlayMod
    {
        public SizeChangerInfoOverlay()
        {
            Name = "Size Changer Info";
            DefaultAnchor = HudAnchor.MidRight;
            DefaultSlot = 3;
            Design = HudDesign.TerminalWindow;
            BadgeTitle = "SIZE CHANGER SCANNER";
            AccentColor = new Color(0.85f, 0.55f, 1f, 1f);
            CardWidth = 335f;
            CardHeight = 195f;
            FontSize = 16.5f;
            AutoSize = true;
            RefreshInterval = 0.5f;
        }

        protected override string Build()
        {
            if (!SizeChangerAPI.Ready)
            {
                return "<b>SIZE CHANGERS</b>\nAPI not available";
            }

            List<Component> changers = SizeChangerAPI.FindAllSizeChangers();
            VRRig localRig = VRRig.LocalRig;

            int totalChangers = changers.Count;
            float currentScale = localRig != null ? localRig.scaleFactor : 1f;
            
            Component nearestChanger = null;
            float nearestDist = float.MaxValue;

            if (localRig != null)
            {
                Vector3 playerPos = localRig.transform.position;
                foreach (var changer in changers)
                {
                    if (changer == null) continue;
                    float dist = Vector3.Distance(changer.transform.position, playerPos);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestChanger = changer;
                    }
                }
            }

            string nearestInfo = "None nearby";
            if (nearestChanger != null && nearestDist < 50f)
            {
                float maxScale = SizeChangerAPI.GetMaxScale(nearestChanger);
                float minScale = SizeChangerAPI.GetMinScale(nearestChanger);
                nearestInfo = $"{nearestChanger.name}\n" +
                             $"                 Max: {maxScale:0.0}x  Min: {minScale:0.00}x\n" +
                             $"                 Dist: {nearestDist:0.0}m";
            }

            return "<b>SIZE CHANGERS</b>\n" +
                   "Total             " + totalChangers + "\n" +
                   "Current Scale     " + currentScale.ToString("0.00") + "x\n" +
                   "Nearest           " + nearestInfo;
        }
    }

    /// <summary>Teleports player to nearest size changer.</summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class SizeChangerTeleportMod : Mod
    {
        public SizeChangerTeleportMod()
        {
            Name = "Teleport To Size Changer";
            isToggle = false;
        }

        public override void Execute()
        {
            if (!SizeChangerAPI.Ready)
            {
                ModKit.Notify("Size Changer", "API not available");
                return;
            }

            Transform player = GorillaTagger.Instance != null ? GorillaTagger.Instance.transform : null;
            if (player == null)
            {
                ModKit.Notify("Size Changer", "Player not found");
                return;
            }

            List<Component> changers = SizeChangerAPI.FindAllSizeChangers();
            Component nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var changer in changers)
            {
                if (changer == null) continue;
                float dist = Vector3.Distance(changer.transform.position, player.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = changer;
                }
            }

            if (nearest != null)
            {
                Vector3 teleportPos = nearest.transform.position + Vector3.up * 2f;
                player.position = teleportPos;
                
                float maxScale = SizeChangerAPI.GetMaxScale(nearest);
                float minScale = SizeChangerAPI.GetMinScale(nearest);
                ModKit.Notify("Size Changer", $"→ {nearest.name}\nMax: {maxScale:0.0}x Min: {minScale:0.00}x");
            }
            else
            {
                ModKit.Notify("Size Changer", "No changers found");
            }
        }
    }

    /// <summary>Lists all size changers in console.</summary>
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class SizeChangerListMod : Mod
    {
        public SizeChangerListMod()
        {
            Name = "List Size Changers";
            isToggle = false;
        }

        public override void Execute()
        {
            if (!SizeChangerAPI.Ready)
            {
                ModKit.Notify("Size Changer", "API not available");
                return;
            }

            List<Component> changers = SizeChangerAPI.FindAllSizeChangers();

            if (changers.Count > 0)
            {
                Plugin.Log?.LogInfo("=== SIZE CHANGERS ===");
                foreach (var changer in changers)
                {
                    if (changer == null) continue;
                    float maxScale = SizeChangerAPI.GetMaxScale(changer);
                    float minScale = SizeChangerAPI.GetMinScale(changer);
                    string info = $"{changer.name} at {changer.transform.position} - Max:{maxScale:0.0}x Min:{minScale:0.00}x";
                    Plugin.Log?.LogInfo(info);
                }
                ModKit.Notify("Size Changer", $"Found {changers.Count} changers - check console");
            }
            else
            {
                ModKit.Notify("Size Changer", "No changers found");
            }
        }
    }
}
