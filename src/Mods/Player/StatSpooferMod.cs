using UnityEngine;
using CascadeMenu.Mods.Core;
using CascadeMenu.Patches;

namespace CascadeMenu.Mods.Player
{
    /// <summary>
    /// Overrides the local FPS, Target FPS, and Ping values reported by GTPlayerStats.
    /// Uses safe reflection in Prepare/TargetMethod so it never breaks across game updates.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class StatSpooferMod : Mod
    {
        private bool _spoofFps = true;
        private bool _spoofTargetFps = false;
        private bool _spoofPing = false;
        private float _fps = 90f;
        private float _targetFps = 90f;
        private float _ping = 40f;

        public StatSpooferMod()
        {
            Name = "FPS & Ping Spoofer";
            isToggle = true;

            AddSetting(ModSetting.Toggle("spoofFps", "Spoof FPS",
                "Override your reported FPS value",
                () => _spoofFps, v => { _spoofFps = v; Sync(); }));

            AddSetting(ModSetting.Slider("fps", "FPS value",
                "Reported FPS value",
                1f, 999f, () => _fps, v => { _fps = v; Sync(); }, v => Mathf.RoundToInt(v) + " FPS"));

            AddSetting(ModSetting.Toggle("spoofTarget", "Spoof Target FPS",
                "Override your reported Target FPS value",
                () => _spoofTargetFps, v => { _spoofTargetFps = v; Sync(); }));

            AddSetting(ModSetting.Slider("targetFps", "Target FPS value",
                "Reported Target FPS value",
                30f, 240f, () => _targetFps, v => { _targetFps = v; Sync(); }, v => Mathf.RoundToInt(v) + " FPS"));

            AddSetting(ModSetting.Toggle("spoofPing", "Spoof Ping",
                "Override your reported Ping value",
                () => _spoofPing, v => { _spoofPing = v; Sync(); }));

            AddSetting(ModSetting.Slider("ping", "Ping value",
                "Reported Ping in milliseconds",
                1f, 999f, () => _ping, v => { _ping = v; Sync(); }, v => Mathf.RoundToInt(v) + " ms"));
        }

        public override void OnEnable()
        {
            GTPlayerStatsSpoofPatch.Enabled = true;
            Sync();
        }

        public override void OnDisable()
        {
            GTPlayerStatsSpoofPatch.Enabled = false;
        }

        private void Sync()
        {
            GTPlayerStatsSpoofPatch.SpoofFPS = _spoofFps;
            GTPlayerStatsSpoofPatch.SpoofTargetFPS = _spoofTargetFps;
            GTPlayerStatsSpoofPatch.SpoofPing = _spoofPing;
            GTPlayerStatsSpoofPatch.FPS = (short)Mathf.Clamp(Mathf.RoundToInt(_fps), 1, 999);
            GTPlayerStatsSpoofPatch.TargetFPS = (short)Mathf.Clamp(Mathf.RoundToInt(_targetFps), 30, 240);
            GTPlayerStatsSpoofPatch.Ping = (short)Mathf.Clamp(Mathf.RoundToInt(_ping), 1, 999);
        }
    }
}
