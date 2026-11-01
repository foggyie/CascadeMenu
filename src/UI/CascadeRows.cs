using System;
using System.Collections.Generic;
using UnityEngine;

namespace CascadeMenu.UI
{
    internal sealed class CascadePage
    {
        public readonly string Title;
        public readonly string Subtitle;
        public readonly CascadeIcon Icon;
        public readonly List<CascadeSection> Sections = new List<CascadeSection>();

        /// <summary>Optional identity used to find/replace dynamic pages.</summary>
        public object Tag;

        /// <summary>
        /// Dynamic pages (cosmetic categories, player pages, module settings)
        /// are opened from a button and never get a sidebar tab, so a rebuild
        /// cannot grow the navigation rail.
        /// </summary>
        public bool HiddenFromSidebar;

        public CascadePage(string title, string subtitle, CascadeIcon icon)
        {
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Icon = icon;
        }

        public CascadeSection Section(string title, string description = null)
        {
            var section = new CascadeSection(title, description);
            Sections.Add(section);
            return section;
        }
    }

    internal sealed class CascadeSection
    {
        public readonly string Title;
        public readonly string Description;
        public readonly List<CascadeRow> Rows = new List<CascadeRow>();

        public CascadeSection(string title, string description)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public CascadeSection Label(string title, string subtitle, string value)
        {
            Rows.Add(new CascadeLabelRow(title, subtitle, value));
            return this;
        }

        public CascadeSection Hero(string title, string subtitle, string value, CascadeIcon icon = CascadeIcon.Layers)
        {
            Rows.Add(new CascadeHeroRow(title, subtitle, value, icon));
            return this;
        }

        public CascadeSection Toggle(string title, string subtitle, Func<bool> get, Action<bool> set, bool enabled = true, Action openSettings = null)
        {
            Rows.Add(new CascadeToggleRow(title, subtitle, get, set, enabled, openSettings));
            return this;
        }

        public CascadeSection Slider(
            string title,
            string subtitle,
            float min,
            float max,
            Func<float> get,
            Action<float> set,
            Func<float, string> format = null)
        {
            Rows.Add(new CascadeSliderRow(title, subtitle, min, max, get, set, format ?? (value => value.ToString("0.##"))));
            return this;
        }

        public CascadeSection Button(string title, string subtitle, string buttonText, Action click, bool primary = true)
        {
            Rows.Add(new CascadeButtonRow(title, subtitle, buttonText, click, primary));
            return this;
        }

        /// <summary>
        /// One live player entry: name, ID, selected highlight, TAP badge and the
        /// per-player action buttons. All state is polled so joins/leaves and
        /// mute changes do not need a full rebuild.
        /// </summary>
        public CascadeSection Player(CascadePlayerRow row)
        {
            Rows.Add(row);
            return this;
        }

        /// <summary>One row with two compact actions (shop try-on + cart).</summary>
        public CascadeSection AddDual(
            string title,
            string subtitle,
            string primaryText,
            string secondaryText,
            Action primary,
            Action secondary,
            bool primaryAccent = true)
        {
            Rows.Add(new CascadeDualButtonRow(
                title, subtitle, primaryText, secondaryText, primary, secondary, primaryAccent));
            return this;
        }

        /// <summary>
        /// A cosmetic card: icon on the left (with an owned dot), name and
        /// subtitle, plus "Try on" and cart buttons. Used by the Cosmetics tab.
        /// </summary>
        public CascadeSection Cosmetic(
            string title,
            string subtitle,
            Sprite icon,
            bool owned,
            bool inCart,
            bool available,
            Action tryOn,
            Action cart)
        {
            Rows.Add(new CascadeCosmeticRow(title, subtitle, icon, owned, inCart, available, tryOn, cart));
            return this;
        }

        public CascadeSection Stepper(
            string title,
            string subtitle,
            int min,
            int max,
            Func<int> get,
            Action<int> set,
            Func<int, string> format = null)
        {
            Rows.Add(new CascadeStepperRow(title, subtitle, min, max, get, set, format ?? (value => value.ToString())));
            return this;
        }

        public CascadeSection Radio(string title, string subtitle, string[] options, Func<int> get, Action<int> set)
        {
            Rows.Add(new CascadeRadioRow(title, subtitle, options, get, set));
            return this;
        }

        public CascadeSection Dropdown(string title, string subtitle, string[] options, Func<int> get, Action<int> set)
        {
            Rows.Add(new CascadeDropdownRow(title, subtitle, options, get, set));
            return this;
        }
    }

    internal abstract class CascadeRow
    {
        public readonly string Title;
        public readonly string Subtitle;

        protected CascadeRow(string title, string subtitle)
        {
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
        }

        public abstract float Height { get; }

        public virtual string SearchText
        {
            get { return Title + " " + Subtitle; }
        }
    }

    internal sealed class CascadeLabelRow : CascadeRow
    {
        public readonly string Value;

        public CascadeLabelRow(string title, string subtitle, string value) : base(title, subtitle)
        {
            Value = value ?? string.Empty;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 42f : 50f; }
        }

        public override string SearchText
        {
            get { return base.SearchText + " " + Value; }
        }
    }

    internal sealed class CascadeHeroRow : CascadeRow
    {
        public readonly string Value;
        public readonly CascadeIcon Icon;

        public CascadeHeroRow(string title, string subtitle, string value, CascadeIcon icon) : base(title, subtitle)
        {
            Value = value ?? string.Empty;
            Icon = icon;
        }

        public override float Height
        {
            get { return 76f; }
        }
    }

    internal sealed class CascadeToggleRow : CascadeRow
    {
        public readonly Func<bool> Get;
        public readonly Action<bool> Set;
        public readonly bool Enabled;

        /// <summary>When set, a small gear button opens this module's settings page.</summary>
        public readonly Action OpenSettings;

        public CascadeToggleRow(string title, string subtitle, Func<bool> get, Action<bool> set, bool enabled = true, Action openSettings = null) : base(title, subtitle)
        {
            Get = get;
            Set = set;
            Enabled = enabled;
            OpenSettings = openSettings;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 44f : 52f; }
        }
    }

    internal sealed class CascadeSliderRow : CascadeRow
    {
        public readonly float Min;
        public readonly float Max;
        public readonly Func<float> Get;
        public readonly Action<float> Set;
        public readonly Func<float, string> Format;

        public CascadeSliderRow(
            string title,
            string subtitle,
            float min,
            float max,
            Func<float> get,
            Action<float> set,
            Func<float, string> format) : base(title, subtitle)
        {
            Min = min;
            Max = Mathf.Max(min + 0.0001f, max);
            Get = get;
            Set = set;
            Format = format;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 58f : 68f; }
        }
    }

    internal sealed class CascadeButtonRow : CascadeRow
    {
        public readonly string ButtonText;
        public readonly Action Click;
        public readonly bool Primary;

        public CascadeButtonRow(string title, string subtitle, string buttonText, Action click, bool primary) : base(title, subtitle)
        {
            ButtonText = buttonText;
            Click = click;
            Primary = primary;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 46f : 52f; }
        }
    }

    /// <summary>
    /// A row with two compact actions (for example "Try on" + "Cart"). Used by
    /// the cosmetic shop, where a single button would force an extra page per
    /// catalog item.
    /// </summary>
    internal sealed class CascadeDualButtonRow : CascadeRow
    {
        public readonly string PrimaryText;
        public readonly string SecondaryText;
        public readonly Action Primary;
        public readonly Action Secondary;
        public readonly bool PrimaryAccent;

        public CascadeDualButtonRow(
            string title,
            string subtitle,
            string primaryText,
            string secondaryText,
            Action primary,
            Action secondary,
            bool primaryAccent = true) : base(title, subtitle)
        {
            PrimaryText = primaryText ?? string.Empty;
            SecondaryText = secondaryText ?? string.Empty;
            Primary = primary;
            Secondary = secondary;
            PrimaryAccent = primaryAccent;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 46f : 54f; }
        }
    }

    /// <summary>
    /// One cosmetic card row: icon, owned dot, name, subtitle and two actions.
    /// </summary>
    internal sealed class CascadeCosmeticRow : CascadeRow
    {
        public readonly Sprite Icon;
        public readonly bool Owned;
        public readonly bool InCart;
        public readonly bool Available;
        public readonly Action TryOn;
        public readonly Action Cart;

        public CascadeCosmeticRow(
            string title,
            string subtitle,
            Sprite icon,
            bool owned,
            bool inCart,
            bool available,
            Action tryOn,
            Action cart) : base(title, subtitle)
        {
            Icon = icon;
            Owned = owned;
            InCart = inCart;
            Available = available;
            TryOn = tryOn;
            Cart = cart;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 60f : 68f; }
        }
    }

    /// <summary>
    /// A live player entry rendered by <see cref="CascadeWindow"/>. Every value is
    /// exposed as a delegate so the row reflects mute/selection/tap state without
    /// rebuilding the page.
    /// </summary>
    internal sealed class CascadePlayerRow : CascadeRow
    {
        public readonly string UserId;
        public readonly string IdDisplay;
        public readonly Func<bool> IsSelected;
        public readonly Func<bool> IsTapping;
        public readonly Func<bool> IsMuted;
        public readonly Action Open;
        public readonly Action ToggleMute;
        public readonly Action Report;
        public readonly Action CopyId;
        public readonly Action CopyName;

        public CascadePlayerRow(
            string title,
            string subtitle,
            string userId,
            string idDisplay,
            Func<bool> isSelected,
            Func<bool> isTapping,
            Func<bool> isMuted,
            Action open,
            Action toggleMute,
            Action report,
            Action copyId,
            Action copyName) : base(title, subtitle)
        {
            UserId = userId ?? string.Empty;
            IdDisplay = idDisplay ?? string.Empty;
            IsSelected = isSelected;
            IsTapping = isTapping;
            IsMuted = isMuted;
            Open = open;
            ToggleMute = toggleMute;
            Report = report;
            CopyId = copyId;
            CopyName = copyName;
        }

        public override float Height
        {
            get { return 62f; }
        }

        public override string SearchText
        {
            get { return base.SearchText + " " + IdDisplay; }
        }
    }

    internal sealed class CascadeStepperRow : CascadeRow
    {
        public readonly int Min;
        public readonly int Max;
        public readonly Func<int> Get;
        public readonly Action<int> Set;
        public readonly Func<int, string> Format;

        public CascadeStepperRow(
            string title,
            string subtitle,
            int min,
            int max,
            Func<int> get,
            Action<int> set,
            Func<int, string> format) : base(title, subtitle)
        {
            Min = min;
            Max = max;
            Get = get;
            Set = set;
            Format = format;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 46f : 52f; }
        }
    }

    internal sealed class CascadeRadioRow : CascadeRow
    {
        public readonly string[] Options;
        public readonly Func<int> Get;
        public readonly Action<int> Set;

        public CascadeRadioRow(string title, string subtitle, string[] options, Func<int> get, Action<int> set) : base(title, subtitle)
        {
            Options = options ?? new string[0];
            Get = get;
            Set = set;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 50f : 56f; }
        }
    }

    internal sealed class CascadeDropdownRow : CascadeRow
    {
        public readonly string[] Options;
        public readonly Func<int> Get;
        public readonly Action<int> Set;

        public CascadeDropdownRow(string title, string subtitle, string[] options, Func<int> get, Action<int> set) : base(title, subtitle)
        {
            Options = options ?? new string[0];
            Get = get;
            Set = set;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 48f : 54f; }
        }
    }
}
