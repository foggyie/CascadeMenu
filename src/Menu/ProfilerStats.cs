using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CascadeMenu
{
    /// <summary>
    /// One feature's frame cost. Values are updated in-place so reading the
    /// profiler page never allocates a new sample object.
    /// </summary>
    internal sealed class ProfilerEntry
    {
        public readonly string Key;
        public string Title;
        public string Description;

        /// <summary>Milliseconds spent in the feature on the last frame.</summary>
        public float LastMs;

        /// <summary>Smoothed milliseconds per frame.</summary>
        public float AverageMs;

        /// <summary>Highest value seen since the last reset.</summary>
        public float PeakMs;

        /// <summary>True when the feature did any work on the last frame.</summary>
        public bool Active;

        public long Frames;

        public ProfilerEntry(string key, string title, string description)
        {
            Key = key ?? string.Empty;
            Title = title ?? Key;
            Description = description ?? string.Empty;
        }
    }

    /// <summary>
    /// Tiny, allocation-free frame-cost registry shared by the Player HUD and
    /// the GunLib. A feature calls <see cref="Timestamp"/> before its work and
    /// <see cref="Record"/> afterwards; no stopwatch objects or per-frame
    /// collections are created.
    ///
    /// The profiler tab polls <see cref="Describe"/> and <see cref="All"/>.
    /// </summary>
    internal static class ProfilerStats
    {
        internal const string PlayerHudKey = "playerhud";
        internal const string NameTagsKey = "nametags";
        internal const string VoiceIndicatorKey = "voiceindicator";
        internal const string GunLibKey = "gunlib";
        internal const string ConstructionKey = "construction";

        private const float Smoothing = 0.08f;

        private static readonly List<ProfilerEntry> Entries = new List<ProfilerEntry>(6);
        private static readonly Dictionary<string, ProfilerEntry> Lookup =
            new Dictionary<string, ProfilerEntry>(StringComparer.Ordinal);

        /// <summary>Monotonic timestamp used to time one feature's frame work.</summary>
        internal static long Timestamp
        {
            get { return Stopwatch.GetTimestamp(); }
        }

        /// <summary>Milliseconds elapsed since a <see cref="Timestamp"/> call.</summary>
        internal static double ElapsedMs(long start)
        {
            long elapsed = Stopwatch.GetTimestamp() - start;
            return elapsed * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>Registers a human-readable name for a feature's key.</summary>
        internal static ProfilerEntry Register(string key, string title, string description)
        {
            ProfilerEntry entry = Get(key);
            if (entry == null) return null;
            if (!string.IsNullOrEmpty(title)) entry.Title = title;
            if (!string.IsNullOrEmpty(description)) entry.Description = description;
            return entry;
        }

        /// <summary>Finds (or lazily creates) one feature entry. No allocation after first call.</summary>
        internal static ProfilerEntry Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            ProfilerEntry entry;
            if (Lookup.TryGetValue(key, out entry)) return entry;

            entry = new ProfilerEntry(key, key, string.Empty);
            Lookup[key] = entry;
            Entries.Add(entry);
            return entry;
        }

        /// <summary>Records one frame's work. Safe for zero/disabled features.</summary>
        internal static void Record(string key, double milliseconds, bool active)
        {
            ProfilerEntry entry = Get(key);
            if (entry == null) return;

            float value = milliseconds > 0.0 ? (float)milliseconds : 0f;
            entry.LastMs = value;
            entry.Active = active;

            if (entry.Frames == 0)
            {
                entry.AverageMs = value;
            }
            else
            {
                entry.AverageMs += (value - entry.AverageMs) * Smoothing;
            }

            if (value > entry.PeakMs) entry.PeakMs = value;
            entry.Frames++;
        }

        internal static IReadOnlyList<ProfilerEntry> All
        {
            get { return Entries; }
        }

        /// <summary>One-line live description used by the profiler page.</summary>
        internal static string Describe(string key)
        {
            ProfilerEntry entry = Get(key);
            if (entry == null) return "not registered";

            var builder = new StringBuilder(64);
            builder.Append(entry.LastMs.ToString("0.000")).Append(" ms now  ·  ");
            builder.Append(entry.AverageMs.ToString("0.000")).Append(" ms avg  ·  ");
            builder.Append(entry.PeakMs.ToString("0.000")).Append(" ms peak");
            if (!entry.Active) builder.Append("  ·  idle");
            return builder.ToString();
        }

        internal static void ResetPeaks()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i].PeakMs = 0f;
                Entries[i].AverageMs = 0f;
                Entries[i].Frames = 0;
            }
        }

        internal static void LogAll()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                ProfilerEntry entry = Entries[i];
                Plugin.Log?.LogInfo("Cascade profiler: " + entry.Title + " = " + Describe(entry.Key));
            }
        }
    }
}
