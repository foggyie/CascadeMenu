using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using CascadeMenu.UI;
using UnityEngine;

namespace CascadeMenu.Mods.Tools
{
    // -----------------------------------------------------------------------
    // Log filter
    // -----------------------------------------------------------------------
    internal enum DebugFilter { All, Info, Warning, Error }

    // -----------------------------------------------------------------------
    // One captured log line
    // -----------------------------------------------------------------------
    internal sealed class DebugLogEntry
    {
        public readonly LogLevel Level;
        public readonly string   Source;
        public readonly string   Message;
        public readonly string   Timestamp;

        public DebugLogEntry(LogLevel level, string source, string message)
        {
            Level     = level;
            Source    = source  ?? "?";
            Message   = (message ?? string.Empty).Trim();
            Timestamp = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    // -----------------------------------------------------------------------
    // BepInEx log listener – singleton, captures all logs into a ring buffer
    // -----------------------------------------------------------------------
    internal sealed class DebugLogListener : ILogListener
    {
        private const int MaxEntries = 600;

        public static DebugLogListener Instance { get; private set; }

        private readonly List<DebugLogEntry> _entries = new List<DebugLogEntry>(MaxEntries);
        private readonly object _lock = new object();
        private int _version;

        /// <summary>Bumped on every change. The UI polls it on the main thread.</summary>
        public int Version { get { return _version; } }

        private DebugLogListener() { }

        public static void Attach()
        {
            if (Instance != null) return;
            Instance = new DebugLogListener();
            BepInEx.Logging.Logger.Listeners.Add(Instance);
        }

        public static void Detach()
        {
            if (Instance == null) return;
            BepInEx.Logging.Logger.Listeners.Remove(Instance);
            Instance = null;
        }

        // ILogListener. May be called from any thread, so it never touches Unity.
        public void LogEvent(object sender, LogEventArgs e)
        {
            var entry = new DebugLogEntry(e.Level, e.Source?.SourceName, e.Data?.ToString());
            lock (_lock)
            {
                AddEntry(entry);
                _version++;
            }
        }

        public void Dispose() { }

        private void AddEntry(DebugLogEntry entry)
        {
            if (_entries.Count >= MaxEntries) _entries.RemoveAt(0);
            _entries.Add(entry);
        }

        /// <summary>Thread-safe snapshot of all stored entries.</summary>
        public List<DebugLogEntry> Snapshot()
        {
            lock (_lock) { return new List<DebugLogEntry>(_entries); }
        }

        /// <summary>Thread-safe clear.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _version++;
            }
        }
    }

    /// <summary>
    /// Keeps a console text box in sync with the log on the main thread,
    /// at most a few times per second, and follows the newest line.
    /// </summary>
    internal sealed class DebugConsoleBinder : MonoBehaviour
    {
        private const float RefreshInterval = 0.2f;

        public TMPro.TMP_Text Text;
        public UnityEngine.UI.ScrollRect Scroll;
        public Func<string> GetText;

        private int _seenVersion = -1;
        private int _seenStamp = -1;
        private float _nextRefresh;

        private void Update()
        {
            if (Text == null || GetText == null) return;
            if (Time.unscaledTime < _nextRefresh) return;

            DebugLogListener listener = DebugLogListener.Instance;
            int version = listener != null ? listener.Version : 0;
            int stamp = DebugConsole.FilterStamp;
            if (version == _seenVersion && stamp == _seenStamp) return;

            _seenVersion = version;
            _seenStamp = stamp;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Text.text = GetText();
            if (Scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                Scroll.verticalNormalizedPosition = 0f;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Debug console page builder
    // -----------------------------------------------------------------------
    internal static class DebugConsole
    {
        public const string PageTitle = "Debug Console";
        public const string PageTag   = "cascade.debug.console";

        private static DebugFilter _filter = DebugFilter.All;
        private static bool _onlyPlugin = false;
        private static string _text = string.Empty;
        private static int _filterStamp;

        /// <summary>Bumped whenever a filter changes so the open console repaints.</summary>
        public static int FilterStamp { get { return _filterStamp; } }

        private static readonly string[] LevelOptions = { "All", "Info", "Warnings", "Errors" };
        private static readonly string[] SourceOptions = { "All sources", "Plugin only" };

        // Terminal color palette: Black background, Yellow warnings, Red errors
        internal static readonly Color ColBg      = new Color(0.04f, 0.04f, 0.05f, 1f);
        internal static readonly Color ColBorder   = new Color(0.20f, 0.20f, 0.22f, 1f);
        internal static readonly Color ColInfo     = new Color(0.92f, 0.92f, 0.92f, 1f);
        internal static readonly Color ColWarning  = new Color(1.00f, 0.85f, 0.15f, 1f); // Yellow
        internal static readonly Color ColError    = new Color(1.00f, 0.24f, 0.24f, 1f); // Red
        internal static readonly Color ColFatal    = new Color(1.00f, 0.10f, 0.10f, 1f); // Darker Red
        internal static readonly Color ColDebug    = new Color(0.35f, 0.75f, 1.00f, 1f); // Cyan
        internal static readonly Color ColTs       = new Color(0.40f, 0.40f, 0.42f, 1f); // Gray
        internal static readonly Color ColSrc      = new Color(0.45f, 0.85f, 0.45f, 1f); // Green

        public static DebugFilter CurrentFilter => _filter;

        // -----------------------------------------------------------------------
        // Page builder — called from MenuApp.Build()
        // -----------------------------------------------------------------------
        public static CascadePage BuildPage(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var page = new CascadePage(PageTitle, "Live in-game BepInEx console", CascadeIcon.Layers);
            page.Tag = PageTag;

            // ---- The black console box ----
            page.Section("Console", "Live log · yellow warnings · red errors · newest at the bottom")
                .Console(BuildRichText);

            // ---- Filters and actions, kept under the box ----
            page.Section("Filter & actions", "Narrow the log down, then copy it")
                .Chips("Level", LevelOptions,
                    () => (int)_filter,
                    index => { _filter = (DebugFilter)index; _filterStamp++; })
                .Chips("Source", SourceOptions,
                    () => _onlyPlugin ? 1 : 0,
                    index => { _onlyPlugin = index == 1; _filterStamp++; })
                .TextField("Contains", "Only lines containing this text", "type to filter", 40,
                    () => _text,
                    value => { _text = value ?? string.Empty; _filterStamp++; })
                .LiveLabel("Showing", "Lines matching the filters", GetStatusText)
                .Button("Copy visible", "Copy the filtered lines to the clipboard", "Copy",
                    () => CopyLog(menu, true), true)
                .Button("Copy everything", "Copy every captured line, ignoring filters", "Copy all",
                    () => CopyLog(menu, false), false)
                .Button("Clear", "Empty the console buffer", "Clear",
                    () =>
                    {
                        DebugLogListener.Instance?.Clear();
                        menu?.ShowToast("Debug Console", "Console cleared.");
                    }, false);

            return page;
        }

        private static string GetStatusText()
        {
            var entries = DebugLogListener.Instance?.Snapshot();
            int total = entries != null ? entries.Count : 0;
            int count = 0;
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (Passes(e)) count++;
                }
            }
            return count + " of " + total;
        }

        // Called to produce display text inside CascadeConsoleRow
        public static string BuildRichText()
        {
            var entries = DebugLogListener.Instance?.Snapshot();
            if (entries == null || entries.Count == 0)
                return "<color=#555555>(no log entries captured yet)</color>";

            var sb = new StringBuilder();
            int shown = 0;
            foreach (var e in entries)
            {
                if (!Passes(e)) continue;
                shown++;
                sb.Append("<color=#").Append(Hex(ColTs)).Append(">").Append(e.Timestamp).Append("</color>");
                sb.Append(" <color=#").Append(Hex(ColSrc)).Append(">[").Append(Safe(e.Source)).Append("]</color>");
                sb.Append(" <color=#").Append(Hex(LevelColor(e.Level))).Append(">").Append(Safe(e.Message)).Append("</color>\n");
            }

            return shown == 0
                ? "<color=#555555>(no entries match current filter)</color>"
                : sb.ToString();
        }

        // -----------------------------------------------------------------------
        // Filtering
        // -----------------------------------------------------------------------
        public static bool Passes(DebugLogEntry e)
        {
            if (_onlyPlugin)
            {
                string src = e.Source ?? "";
                if (!src.Equals("CascadeMenu", StringComparison.OrdinalIgnoreCase) &&
                    !src.Contains("CascadeMenu") &&
                    !src.Contains(PluginInfo.NAME))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(_text))
            {
                bool inMessage = (e.Message ?? string.Empty).IndexOf(_text, StringComparison.OrdinalIgnoreCase) >= 0;
                bool inSource = (e.Source ?? string.Empty).IndexOf(_text, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!inMessage && !inSource) return false;
            }

            switch (_filter)
            {
                case DebugFilter.Info:    return e.Level == LogLevel.Info || e.Level == LogLevel.Debug || e.Level == LogLevel.Message;
                case DebugFilter.Warning: return e.Level == LogLevel.Warning;
                case DebugFilter.Error:   return e.Level == LogLevel.Error || e.Level == LogLevel.Fatal;
                default:                  return true;
            }
        }

        public static Color LevelColor(LogLevel l)
        {
            switch (l)
            {
                case LogLevel.Warning: return ColWarning; // Yellow
                case LogLevel.Error:   return ColError;   // Red
                case LogLevel.Fatal:   return ColFatal;   // Red
                case LogLevel.Debug:   return ColDebug;   // Cyan
                default:               return ColInfo;    // White
            }
        }

        private static void CopyLog(CascadeMenuBehaviour menu, bool visibleOnly)
        {
            var entries = DebugLogListener.Instance?.Snapshot();
            if (entries == null || entries.Count == 0)
            {
                GUIUtility.systemCopyBuffer = "(empty)";
                menu?.ShowToast("Debug Console", "Nothing to copy.");
                return;
            }
            var sb = new StringBuilder();
            int count = 0;
            foreach (var e in entries)
            {
                if (visibleOnly && !Passes(e)) continue;
                count++;
                sb.AppendLine("[" + e.Timestamp + "] [" + e.Level + "] [" + e.Source + "] " + e.Message);
            }
            GUIUtility.systemCopyBuffer = sb.ToString();
            menu?.ShowToast("Debug Console", count + " line(s) copied to clipboard.");
        }

        internal static string Hex(Color c)
        {
            return ((int)(c.r * 255)).ToString("X2") +
                   ((int)(c.g * 255)).ToString("X2") +
                   ((int)(c.b * 255)).ToString("X2");
        }

        private static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("<", "\u003c").Replace(">", "\u003e");
        }
    }
}
