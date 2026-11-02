using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Runtime state for one <see cref="CascadePlayerRow"/>. Polling here (instead
    /// of rebuilding the page) keeps the list stable while players tap, get muted
    /// or become the selected target.
    /// </summary>
    internal sealed class CascadePlayerView : MonoBehaviour
    {
        public Image Background;
        public Image SelectionBar;
        public Image Avatar;
        public GameObject TapBadge;
        public TMP_Text NameLabel;
        public TMP_Text MuteLabel;

        public Func<bool> IsSelected;
        public Func<bool> IsTapping;
        public Func<bool> IsMuted;
        public Func<Color> GetColor;

        private CascadeTheme _theme;
        private bool _selected;
        private bool _tapping;
        private bool _muted;
        private Color _avatarColor = new Color(-1f, -1f, -1f, -1f);
        private float _nextPoll;

        public void Configure(
            CascadeTheme theme,
            Func<bool> selected,
            Func<bool> tapping,
            Func<bool> muted)
        {
            _theme = theme;
            IsSelected = selected;
            IsTapping = tapping;
            IsMuted = muted;
            Apply(true);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.12f;
            Apply(false);
        }

        private void Apply(bool force)
        {
            if (Avatar != null && GetColor != null)
            {
                Color color = GetColor();
                if (force || color != _avatarColor)
                {
                    _avatarColor = color;
                    Avatar.color = color;
                }
            }

            bool selected = IsSelected != null && IsSelected();
            bool tapping = IsTapping != null && IsTapping();
            bool muted = IsMuted != null && IsMuted();

            if (!force && selected == _selected && tapping == _tapping && muted == _muted)
            {
                return;
            }

            _selected = selected;
            _tapping = tapping;
            _muted = muted;

            if (Background != null && _theme != null)
            {
                Background.color = selected
                    ? new Color(_theme.Accent.r, _theme.Accent.g, _theme.Accent.b, _theme.IsDark ? 0.22f : 0.16f)
                    : (_theme.IsDark ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.025f));
            }

            if (SelectionBar != null)
            {
                SelectionBar.gameObject.SetActive(selected);
            }

            if (TapBadge != null)
            {
                TapBadge.SetActive(tapping);
            }

            if (NameLabel != null && _theme != null)
            {
                NameLabel.color = muted ? _theme.TextTertiary : _theme.TextPrimary;
            }

            if (MuteLabel != null)
            {
                MuteLabel.text = muted ? "Unmute" : "Mute";
            }
        }
    }
}
