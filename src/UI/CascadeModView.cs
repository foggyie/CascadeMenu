using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Live state for one generated mod row. Active, Enabled and the button
    /// labels are polled so a keybind never has to rebuild the menu.
    /// </summary>
    internal sealed class CascadeModView : MonoBehaviour
    {
        public Image Dot;
        public Image Background;
        public TMP_Text StatusLabel;

        public Func<bool> GetEnabled;
        public Func<bool> GetActive;
        public Func<string> GetKeyLabel;
        public bool IsToggle;

        private CascadeTheme _theme;
        private bool _enabled;
        private bool _active;
        private string _status = string.Empty;
        private float _nextPoll;
        private int _themeVersion = -1;

        public void Configure(CascadeTheme theme)
        {
            _theme = theme;
            Apply(true);
        }

        private void Update()
        {
            // Theme switches must repaint immediately, even between the slower
            // state polls.
            if (_theme != null && _themeVersion != _theme.Version)
            {
                Apply(true);
                _nextPoll = Time.unscaledTime + 0.1f;
                return;
            }

            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.1f;
            Apply(false);
        }

        private void Apply(bool force)
        {
            bool enabled = GetEnabled != null && GetEnabled();
            bool active = GetActive != null && GetActive();

            string status = BuildStatus(enabled, active);

            bool themeChanged = _theme != null && _themeVersion != _theme.Version;
            if (!force && !themeChanged && enabled == _enabled && active == _active &&
                status == _status)
            {
                return;
            }

            if (_theme != null) _themeVersion = _theme.Version;
            _enabled = enabled;
            _active = active;
            _status = status;

            if (Dot != null && _theme != null)
            {
                if (!enabled)
                {
                    CascadeTheme.SetGraphicColor(Dot, _theme.TextTertiary);
                }
                else if (active && IsToggle)
                {
                    CascadeTheme.SetGraphicColor(Dot, _theme.ActiveColor);
                }
                else
                {
                    CascadeTheme.SetGraphicColor(Dot, _theme.Accent);
                }
            }

            if (Background != null && _theme != null)
            {
                if (enabled && active && IsToggle)
                {
                    CascadeTheme.SetGraphicColor(Background, _theme.ActiveSoft);
                }
                else
                {
                    CascadeTheme.SetGraphicColor(Background, _theme.ContentBg.WithAlpha(0f));
                }
            }

            if (StatusLabel != null) StatusLabel.text = status;
        }

        private string BuildStatus(bool enabled, bool active)
        {
            if (!enabled) return "Off";
            if (!IsToggle) return "Ran once";
            return active ? "Running" : "On";
        }
    }
}
