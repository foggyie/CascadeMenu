using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Helpers for locating and editing BepInEx's own config file
    /// (BepInEx/config/BepInEx.cfg), separate from any individual mod's config.
    /// </summary>
    internal static class Util
    {
        internal static string GetBepInExCfgPath()
        {
            return Path.Combine(Paths.ConfigPath, "BepInEx.cfg");
        }

        internal static bool BepInExCfgExists()
        {
            return File.Exists(GetBepInExCfgPath());
        }

        // Loads BepInEx.cfg as a ConfigFile. saveOnInit:false so merely
        // opening it doesn't rewrite the file before you change anything.
        internal static ConfigFile LoadBepInExConfig()
        {
            return new ConfigFile(GetBepInExCfgPath(), false);
        }

        // Backs up BepInEx.cfg (once) alongside itself as BepInEx.cfg.bak
        internal static bool BackupBepInExConfig()
        {
            string target = GetBepInExCfgPath();
            if (!File.Exists(target)) return false;

            string backup = target + ".bak";
            File.Copy(target, backup, overwrite: true);
            return true;
        }

        // Overwrites BepInEx.cfg with a config file you ship/generate elsewhere.
        // Backs up the original first. Returns false if the source is missing.
        internal static bool ApplyCustomBepInExConfig(string sourceCfgPath)
        {
            if (!File.Exists(sourceCfgPath)) return false;

            BackupBepInExConfig();
            File.Copy(sourceCfgPath, GetBepInExCfgPath(), overwrite: true);
            return true;
        }

        // Restores BepInEx.cfg from the .bak created by BackupBepInExConfig/ApplyCustomBepInExConfig.
        internal static bool RestoreBepInExConfigBackup()
        {
            string backup = GetBepInExCfgPath() + ".bak";
            if (!File.Exists(backup)) return false;

            File.Copy(backup, GetBepInExCfgPath(), overwrite: true);
            return true;
        }

        // Example single-key edit: toggles the "show console" setting.
        internal static void SetConsoleEnabled(bool enabled)
        {
            ConfigFile cfg = LoadBepInExConfig();
            ConfigEntry<bool> entry = cfg.Bind("Logging.Console", "Enabled", false,
                "Enables showing a console for log output.");
            entry.Value = enabled;
            cfg.Save();
        }

        internal static bool GetConsoleEnabled()
        {
            ConfigFile cfg = LoadBepInExConfig();
            ConfigEntry<bool> entry = cfg.Bind("Logging.Console", "Enabled", false,
                "Enables showing a console for log output.");
            return entry.Value;
        }

        // -----------------------------------------------------------------
        // Restart
        // -----------------------------------------------------------------

        /// <summary>Gorilla Tag's Steam app id, used to relaunch through Steam.</summary>
        internal const string SteamAppId = "1533390";

        /// <summary>
        /// Quits the game and relaunches it through Steam a few seconds later.
        /// When <paramref name="withConsole"/> is set, the BepInEx console is
        /// turned on first so the relaunched game comes back with it visible.
        ///
        /// The relaunch is delegated to a detached PowerShell process, because
        /// this object stops existing the moment the game quits.
        /// </summary>
        internal static bool RestartGame(bool withConsole)
        {
            try
            {
                if (withConsole) SetConsoleEnabled(true);

                string command = "Start-Sleep -Seconds 6; Start-Process 'steam://rungameid/" + SteamAppId + "'";
                var info = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -WindowStyle Hidden -Command \"" + command + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Paths.GameRootPath
                };

                Process.Start(info);
                Plugin.Log?.LogInfo("Cascade restarting Gorilla Tag" + (withConsole ? " with the console enabled" : "") + ".");
                Application.Quit();
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade restart failed: " + exception);
                return false;
            }
        }
    }
}