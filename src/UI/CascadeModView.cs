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
        public TMP_Text ActionLabel;
        public TMP_Text PressLabel;

        public Func<bool> GetEnabled;
        public Func<bool> GetActive;
        public Func<string> GetKeyLabel;
        public Func<string> GetPressLabel;
        public Func<string> GetActionLabel;
        public bool IsToggle;

        private CascadeTheme _theme;
        private bool _enabled;
        private bool _active;
        private string _status = string.Empty;
        private string _action = string.Empty;
        private string _press = string.Empty;
        private float _nextPoll;

        public void Configure(CascadeTheme theme)
        {
            _theme = theme;
            Apply(true);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.1f;
            Apply(false);
        }

        private void Apply(bool force)
        {
            bool enabled = GetEnabled != null && GetEnabled();
            bool active = GetActive != null && GetActive();

            string status = BuildStatus(enabled, active);
            string action = GetActionLabel != null ? GetActionLabel() : string.Empty;
            string press = GetPressLabel != null ? GetPressLabel() : string.Empty;

            if (!force && enabled == _enabled && active == _active &&
                status == _status && action == _action && press == _press)
            {
                return;
            }

            _enabled = enabled;
            _active = active;
            _status = status;
            _action = action;
            _press = press;

            if (Dot != null && _theme != null)
            {
                if (!enabled)
                {
                    Dot.color = _theme.TextTertiary;
                }
                else if (active && IsToggle)
                {
                    Dot.color = new Color(0.30f, 0.85f, 0.42f, 1f);
                }
                else
                {
                    Dot.color = _theme.Accent;
                }
            }

            if (Background != null && _theme != null)
            {
                Background.color = enabled && active && IsToggle
                    ? new Color(_theme.Accent.r, _theme.Accent.g, _theme.Accent.b, 0.10f)
                    : new Color(0f, 0f, 0f, 0f);
            }

            if (StatusLabel != null) StatusLabel.text = status;
            if (ActionLabel != null) ActionLabel.text = action;
            if (PressLabel != null) PressLabel.text = press;
        }

        private string BuildStatus(bool enabled, bool active)
        {
            string state;
            if (!enabled)
            {
                state = "Disabled";
            }
            else if (!IsToggle)
            {
                state = "Ready";
            }
            else
            {
                state = active ? "ACTIVE" : "Idle";
            }

            string key = GetKeyLabel != null ? GetKeyLabel() : "unbound";
            string press = GetPressLabel != null ? GetPressLabel() : "Single";
            return state + "  ·  " + key + "  ·  " + press + " press";
        }
    }
}
