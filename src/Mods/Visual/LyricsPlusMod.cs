using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CascadeMenu.Mods.Core;

namespace CascadeMenu.Mods.Visual
{
    /// <summary>
    /// Live synced-lyrics floating VR HUD inspired by LyricsPlus.
    /// Automatically detects currently playing tracks from Windows media apps
    /// (Spotify, YouTube Music, Apple Music, VLC, browsers) or accepts a manual
    /// "Artist - Song" query, fetches synced LRC lyrics from LRCLIB, and renders
    /// a frosted-glass floating lyrics bar in front of your view.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class LyricsPlusMod : Mod
    {
        private struct LyricLine
        {
            public float TimeSeconds;
            public string Text;
        }

        private static readonly HttpClient Http = CreateHttpClient();
        private static readonly Regex LrcRegex = new Regex(@"\[(\d{1,2}):(\d{2})(?:\.(\d{1,3}))?\](.*)", RegexOptions.Compiled);

        private string _manualQuery = "";
        private bool _autoDetect = true;
        private bool _paused = false;
        private float _timeOffset = 0f;
        private float _distance = 1.45f;
        private float _verticalOffset = -0.34f;
        private float _scale = 1.0f;

        private string _currentTrack = "";
        private string _statusText = "Waiting for music...";
        private float _trackStartTime;
        private float _pausedElapsed;
        private float _nextWindowPoll;
        private bool _isFetching;

        private readonly object _lock = new object();
        private List<LyricLine> _lines = new List<LyricLine>();
        private bool _hasSyncedLyrics;

        private GameObject _hudRoot;
        private TextMeshProUGUI _headerTmp;
        private TextMeshProUGUI _prevTmp;
        private TextMeshProUGUI _currTmp;
        private TextMeshProUGUI _nextTmp;

        public LyricsPlusMod()
        {
            Name = "LyricWave";
            isToggle = true;

            AddSetting(ModSetting.Toggle("autoDetect", "Auto-detect Windows music",
                "Automatically detect playing songs from Spotify, YouTube Music, Apple Music & VLC",
                () => _autoDetect, v => _autoDetect = v));

            AddSetting(ModSetting.Text("manualTrack", "Manual track (Artist - Song)",
                "Type 'Artist - Song' to load lyrics manually (leave blank for auto-detect)",
                () => _manualQuery,
                v =>
                {
                    _manualQuery = (v ?? "").Trim();
                    if (!string.IsNullOrEmpty(_manualQuery))
                    {
                        RequestLyrics(_manualQuery);
                    }
                }));

            AddSetting(ModSetting.Toggle("paused", "Pause lyric timer",
                "Freeze or resume the synced lyric clock",
                () => _paused,
                v =>
                {
                    if (v && !_paused) _pausedElapsed = GetElapsed();
                    else if (!v && _paused) _trackStartTime = Time.unscaledTime - _pausedElapsed;
                    _paused = v;
                }));

            AddSetting(ModSetting.Slider("offset", "Sync offset",
                "Nudge lyrics earlier or later in seconds",
                -15f, 15f, () => _timeOffset, v => _timeOffset = v, v => (v >= 0 ? "+" : "") + v.ToString("0.0") + "s"));

            AddSetting(ModSetting.Slider("distance", "HUD distance",
                "How far in front of your face the lyrics bar floats",
                0.7f, 2.5f, () => _distance, v => _distance = v, v => v.ToString("0.00") + " m"));

            AddSetting(ModSetting.Slider("vertOffset", "Vertical offset",
                "Move the lyrics bar higher or lower in your view",
                -0.6f, 0.5f, () => _verticalOffset, v => _verticalOffset = v, v => v.ToString("0.00") + " m"));

            AddSetting(ModSetting.Slider("scale", "HUD scale",
                "Size multiplier for the LyricsPlus display",
                0.6f, 1.8f, () => _scale, v => _scale = v, v => v.ToString("0.00") + "x"));
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            try { client.DefaultRequestHeaders.UserAgent.ParseAdd("CascadeMenu-LyricsPlus/1.0"); } catch { }
            return client;
        }

        public override void OnEnable()
        {
            EnsureHud();
            _nextWindowPoll = 0f;
            if (!string.IsNullOrEmpty(_manualQuery))
            {
                RequestLyrics(_manualQuery);
            }
        }

        public override void OnDisable()
        {
            if (_hudRoot != null)
            {
                UnityEngine.Object.Destroy(_hudRoot);
                _hudRoot = null;
            }
        }

        public override void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            EnsureHud();

            // Smoothly follow camera view
            Vector3 forward = cam.transform.forward;
            Vector3 up = cam.transform.up;
            Vector3 targetPos = cam.transform.position + forward * _distance + up * _verticalOffset;
            _hudRoot.transform.position = Vector3.Lerp(_hudRoot.transform.position, targetPos, Mathf.Clamp01(Time.unscaledDeltaTime * 8f));
            _hudRoot.transform.rotation = Quaternion.Slerp(
                _hudRoot.transform.rotation,
                Quaternion.LookRotation(_hudRoot.transform.position - cam.transform.position, Vector3.up),
                Mathf.Clamp01(Time.unscaledDeltaTime * 8f));
            _hudRoot.transform.localScale = Vector3.one * (0.00115f * _scale);

            if (_autoDetect && string.IsNullOrEmpty(_manualQuery) && Time.unscaledTime >= _nextWindowPoll)
            {
                _nextWindowPoll = Time.unscaledTime + 2.0f;
                string detected = DetectWindowsPlayingTrack();
                if (!string.IsNullOrEmpty(detected) && !string.Equals(detected, _currentTrack, StringComparison.OrdinalIgnoreCase))
                {
                    RequestLyrics(detected);
                }
            }

            UpdateDisplay();
        }

        private float GetElapsed()
        {
            if (_paused) return _pausedElapsed + _timeOffset;
            return (Time.unscaledTime - _trackStartTime) + _timeOffset;
        }

        private void UpdateDisplay()
        {
            if (_headerTmp == null) return;

            string title = string.IsNullOrEmpty(_currentTrack) ? "LyricWave" : _currentTrack;
            _headerTmp.text = "♪  " + title + (_paused ? "  <color=#FBBF24>[PAUSED]</color>" : "");

            List<LyricLine> snapshot;
            bool synced;
            string status;
            lock (_lock)
            {
                snapshot = _lines;
                synced = _hasSyncedLyrics;
                status = _statusText;
            }

            if (snapshot == null || snapshot.Count == 0)
            {
                _prevTmp.text = "";
                _currTmp.text = status;
                _nextTmp.text = "";
                return;
            }

            int idx = 0;
            if (synced)
            {
                float t = GetElapsed();
                for (int i = 0; i < snapshot.Count; i++)
                {
                    if (snapshot[i].TimeSeconds <= t) idx = i;
                    else break;
                }
            }
            else
            {
                // Auto-scroll plain lyrics gently (~3.2s per line)
                idx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, GetElapsed()) / 3.2f), 0, snapshot.Count - 1);
            }

            _prevTmp.text = idx > 0 ? snapshot[idx - 1].Text : "";
            _currTmp.text = snapshot[idx].Text;
            _nextTmp.text = idx + 1 < snapshot.Count ? snapshot[idx + 1].Text : "";
        }

        private void RequestLyrics(string trackQuery)
        {
            if (_isFetching) return;
            _currentTrack = trackQuery;
            _trackStartTime = Time.unscaledTime;
            _pausedElapsed = 0f;
            _isFetching = true;

            lock (_lock)
            {
                _lines = new List<LyricLine>();
                _hasSyncedLyrics = false;
                _statusText = "Searching lyrics...";
            }

            Task.Run(async () =>
            {
                try
                {
                    string url = "https://lrclib.net/api/search?q=" + Uri.EscapeDataString(trackQuery);
                    string json = await Http.GetStringAsync(url).ConfigureAwait(false);
                    ParseLrcLibResponse(json);
                }
                catch
                {
                    lock (_lock)
                    {
                        _statusText = "No lyrics found (offline or unavailable)";
                    }
                }
                finally
                {
                    _isFetching = false;
                }
            });
        }

        private void ParseLrcLibResponse(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length < 4)
            {
                lock (_lock) { _statusText = "No lyrics found for this track."; }
                return;
            }

            string syncedRaw = ExtractJsonStringField(json, "syncedLyrics");
            if (!string.IsNullOrEmpty(syncedRaw))
            {
                var parsed = ParseLrcLines(syncedRaw);
                if (parsed.Count > 0)
                {
                    lock (_lock)
                    {
                        _lines = parsed;
                        _hasSyncedLyrics = true;
                        _statusText = "";
                    }
                    return;
                }
            }

            string plainRaw = ExtractJsonStringField(json, "plainLyrics");
            if (!string.IsNullOrEmpty(plainRaw))
            {
                string[] split = plainRaw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var parsed = new List<LyricLine>(split.Length);
                for (int i = 0; i < split.Length; i++)
                {
                    string line = split[i].Trim();
                    if (line.Length > 0) parsed.Add(new LyricLine { TimeSeconds = i * 3.2f, Text = line });
                }
                lock (_lock)
                {
                    _lines = parsed;
                    _hasSyncedLyrics = false;
                    _statusText = "";
                }
                return;
            }

            lock (_lock) { _statusText = "No lyrics found for this track."; }
        }

        private static List<LyricLine> ParseLrcLines(string raw)
        {
            var list = new List<LyricLine>();
            string[] lines = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                Match m = LrcRegex.Match(lines[i]);
                if (!m.Success) continue;

                int min = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int sec = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                float frac = 0f;
                if (m.Groups[3].Success && m.Groups[3].Value.Length > 0)
                {
                    string f = m.Groups[3].Value;
                    frac = float.Parse("0." + f, CultureInfo.InvariantCulture);
                }

                string txt = m.Groups[4].Value.Trim();
                if (string.IsNullOrEmpty(txt)) txt = "♪  ·  ·  ·";
                list.Add(new LyricLine { TimeSeconds = min * 60f + sec + frac, Text = txt });
            }
            list.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            return list;
        }

        private static string ExtractJsonStringField(string json, string fieldName)
        {
            string needle = "\"" + fieldName + "\":\"";
            int start = json.IndexOf(needle, StringComparison.Ordinal);
            if (start < 0) return null;
            start += needle.Length;

            var sb = new StringBuilder();
            bool escaped = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    switch (c)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': break;
                        case 't': sb.Append(' '); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append(c); break;
                    }
                    escaped = false;
                    continue;
                }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void EnsureHud()
        {
            if (_hudRoot != null) return;

            _hudRoot = new GameObject("CascadeLyricsPlusHUD");
            if (Plugin.Root != null) _hudRoot.transform.SetParent(Plugin.Root.transform, false);

            Canvas canvas = _hudRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rt = _hudRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(680f, 195f);

            // Frosted glass backdrop
            GameObject bgGo = new GameObject("Backdrop");
            bgGo.transform.SetParent(_hudRoot.transform, false);
            RectTransform bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.07f, 0.08f, 0.12f, 0.72f);

            _headerTmp = CreateLine("Header", 72f, 20f, new Color(0.55f, 0.78f, 1.0f, 0.95f), FontStyles.Bold);
            _prevTmp = CreateLine("Prev", 32f, 20f, new Color(0.75f, 0.80f, 0.88f, 0.50f), FontStyles.Normal);
            _currTmp = CreateLine("Curr", -12f, 28f, Color.white, FontStyles.Bold);
            _nextTmp = CreateLine("Next", -56f, 20f, new Color(0.75f, 0.80f, 0.88f, 0.55f), FontStyles.Normal);
        }

        private TextMeshProUGUI CreateLine(string name, float yPos, float fontSize, Color color, FontStyles style)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_hudRoot.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 0.5f);
            rt.anchorMax = new Vector2(0.96f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(0f, 44f);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        // --- Lightweight Win32 window title scan for Spotify / YT Music / VLC / Apple Music ---
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static string DetectWindowsPlayingTrack()
        {
            string found = null;
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    var sb = new StringBuilder(256);
                    if (GetWindowTextW(hWnd, sb, sb.Capacity) <= 0) return true;
                    string title = sb.ToString().Trim();
                    if (title.Length < 4) return true;

                    // YouTube Music / YouTube browser tabs
                    int ytIdx = title.IndexOf(" - YouTube Music", StringComparison.OrdinalIgnoreCase);
                    if (ytIdx > 2)
                    {
                        found = title.Substring(0, ytIdx).Trim();
                        return false;
                    }

                    // VLC Media Player
                    int vlcIdx = title.IndexOf(" - VLC media player", StringComparison.OrdinalIgnoreCase);
                    if (vlcIdx > 2)
                    {
                        found = title.Substring(0, vlcIdx).Trim();
                        return false;
                    }

                    // Spotify desktop shows "Artist - Track" directly when playing
                    if (title.Contains(" - ") &&
                        !title.Contains("Visual Studio") &&
                        !title.Contains("Gorilla Tag") &&
                        !title.Contains("Discord") &&
                        !title.Contains("Chrome") &&
                        !title.Contains("Edge") &&
                        !title.Contains("Firefox") &&
                        !title.Contains("Explorer") &&
                        !title.Contains("Steam"))
                    {
                        found = title;
                        return false;
                    }

                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            return found;
        }
    }
}
