using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Tools
{
    /// <summary>
    /// One-shot action that writes a PNG next to the game's persistent data.
    /// The row's switch fires <see cref="Execute"/> once; there is no running
    /// effect to clean up.
    ///
    /// ScreenCapture lives in UnityEngine.ScreenCaptureModule, which this
    /// project does not reference, so the call is resolved through reflection
    /// and skipped cleanly on builds where the type is unavailable.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class Screenshot : Mod
    {
        private const string FolderName = "CascadeScreenshots";

        private bool _supersize;

        public Screenshot()
        {
            Name = "Screenshot";
            isToggle = false;

            AddSetting(ModSetting.Toggle("supersize", "Supersize",
                "Capture at double resolution",
                () => _supersize, value => _supersize = value));
        }

        public override void Execute()
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, FolderName);
                Directory.CreateDirectory(folder);

                string file = Path.Combine(
                    folder,
                    "cascade_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");

                if (!Capture(file, _supersize ? 2 : 1))
                {
                    Plugin.Log?.LogWarning(
                        "Cascade screenshot failed: UnityEngine.ScreenCapture is unavailable.");
                    ShowToast("Screenshot", "Capture unavailable.");
                    return;
                }

                Plugin.Log?.LogInfo("Cascade screenshot saved to " + file);
                ShowToast("Screenshot", "Saved to " + FolderName);
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade screenshot failed: " + exception);
                ShowToast("Screenshot", "Capture failed.");
            }
        }

        private static void ShowToast(string title, string message)
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            if (menu != null)
            {
                menu.ShowToast(title, message);
            }
        }

        private static bool Capture(string file, int multiplier)
        {
            try
            {
                Type type = ResolveScreenCapture();
                if (type == null) return false;

                MethodInfo method = type.GetMethod(
                    "CaptureScreenshot",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(int) },
                    null);

                if (method != null)
                {
                    method.Invoke(null, new object[] { file, Mathf.Max(1, multiplier) });
                    return true;
                }

                // Older Unity builds only expose the single-argument overload.
                method = type.GetMethod(
                    "CaptureScreenshot",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
                if (method == null) return false;

                method.Invoke(null, new object[] { file });
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning(
                    "Cascade screenshot capture failed: " + exception.Message);
                return false;
            }
        }

        private static Type ResolveScreenCapture()
        {
            Type type = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            if (type != null) return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType("UnityEngine.ScreenCapture", false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
