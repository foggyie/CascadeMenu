using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using CascadeMenu.Mods.Core;

namespace CascadeMenu.Mods.Tools
{
    /// <summary>
    /// Boosts your Photon Voice Recorder bitrate and sampling rate for crisper
    /// microphone audio, and automatically re-applies whenever SetupVoice runs.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class HighQualityMicMod : Mod
    {
        internal static bool Active;
        private static int _bitrate = 128000;

        public HighQualityMicMod()
        {
            Name = "High Quality Microphone";
            isToggle = true;

            AddSetting(ModSetting.Slider("bitrate", "Voice bitrate",
                "Opus microphone bitrate in kbps (default Gorilla Tag is ~24 kbps)",
                32f, 256f, () => _bitrate / 1000f, v =>
                {
                    _bitrate = Mathf.RoundToInt(v) * 1000;
                    if (Active) ApplyVoiceSettings(true);
                }, v => Mathf.RoundToInt(v) + " kbps"));
        }

        public override void OnEnable()
        {
            Active = true;
            ApplyVoiceSettings(true);
        }

        public override void OnDisable()
        {
            Active = false;
            ApplyVoiceSettings(false);
        }

        internal static void ApplyVoiceSettings(bool highQuality)
        {
            try
            {
                Type recorderType = AccessTools.TypeByName("Photon.Voice.Unity.Recorder");
                if (recorderType == null) return;

                UnityEngine.Object[] recorders = UnityEngine.Object.FindObjectsByType(recorderType, FindObjectsSortMode.None);
                if (recorders == null || recorders.Length == 0) return;

                PropertyInfo bitrateProp = recorderType.GetProperty("Bitrate", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo restartMethod = recorderType.GetMethod("RestartRecording", BindingFlags.Instance | BindingFlags.Public);

                for (int i = 0; i < recorders.Length; i++)
                {
                    object rec = recorders[i];
                    if (rec == null) continue;
                    if (bitrateProp != null && bitrateProp.CanWrite)
                    {
                        bitrateProp.SetValue(rec, highQuality ? _bitrate : 24000, null);
                    }
                    if (restartMethod != null)
                    {
                        ParameterInfo[] p = restartMethod.GetParameters();
                        if (p.Length == 0) restartMethod.Invoke(rec, null);
                        else if (p.Length == 1 && p[0].ParameterType == typeof(bool)) restartMethod.Invoke(rec, new object[] { true });
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("HighQualityMicMod apply failed: " + ex.Message);
            }
        }
    }
}
