using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Removes VSync and raises the frame-rate cap. The previous values are
    /// restored when the mod is switched off.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Performance")]
    public sealed class UnlimitedFps : Mod
    {
        private static readonly string[] TargetNames = { "Unlimited", "60", "72", "90", "120", "144", "240" };
        private static readonly int[] Targets = { -1, 60, 72, 90, 120, 144, 240 };

        private int _target;
        private bool _saved;
        private int _previousTarget;
        private int _previousVSync;

        public UnlimitedFps()
        {
            Name = "Unlimited FPS";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("target", "Target FPS",
                "Frame-rate cap while the mod is on",
                TargetNames,
                () => _target,
                value =>
                {
                    _target = value;
                    if (IsActive) ApplyTarget();
                }));
        }

        public override void OnEnable()
        {
            _previousTarget = Application.targetFrameRate;
            _previousVSync = QualitySettings.vSyncCount;
            _saved = true;
            ApplyTarget();
            Plugin.Log?.LogInfo("Cascade Unlimited FPS active.");
        }

        public override void OnDisable()
        {
            if (_saved)
            {
                Application.targetFrameRate = _previousTarget;
                QualitySettings.vSyncCount = _previousVSync;
                _saved = false;
            }
            else
            {
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
            }
            Plugin.Log?.LogInfo("Cascade Unlimited FPS stopped.");
        }

        private void ApplyTarget()
        {
            int index = Mathf.Clamp(_target, 0, Targets.Length - 1);
            Application.targetFrameRate = Targets[index];
            QualitySettings.vSyncCount = 0;
        }
    }
}
