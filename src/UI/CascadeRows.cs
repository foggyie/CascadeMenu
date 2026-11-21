using System;
using System.Collections.Generic;
using UnityEngine;

namespace CascadeMenu.UI
{
    internal sealed class CascadePage
    {
        public string Title { get; set; }
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

        /// <summary>
        /// A label whose value is polled while the page is visible. Used for
        /// live state such as a mod's Active flag, so the panel does not have to
        /// be rebuilt when a keybind changes it.
        /// </summary>
        public CascadeSection LiveLabel(string title, string subtitle, Func<string> getValue,
            Func<bool> highlight = null)
        {
            Rows.Add(new CascadeLabelRow(title, subtitle, getValue, highlight));
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

        /// <summary>Large live 3D portrait of a player, used on their page.</summary>
        public CascadeSection Portrait(string title, string subtitle, Func<VRRig> getRig, Func<Color> getColor)
        {
            Rows.Add(new CascadePortraitRow(title, subtitle, getRig, getColor));
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

        /// <summary>Adds a live scrolling console panel (for the debug log view).</summary>
        public CascadeSection Console(Func<string> getText)
        {
            Rows.Add(new CascadeConsoleRow(getText));
            return this;
        }

        /// <summary>A labelled row of pill chips, one of which is selected.</summary>
        public CascadeSection Chips(string title, string[] options, Func<int> get, Action<int> set)
        {
            Rows.Add(new CascadeChipRow(title, options, get, set));
            return this;
        }

        /// <summary>
        /// One auto-generated mod row: a live state line, an Enabled toggle and
        /// an optional settings gear. Keybind, press type and Active controls
        /// live on the gear page, not in the row.
        /// </summary>
        public CascadeSection Mod(
            string title,
            string subtitle,
            Func<bool> getEnabled,
            Action<bool> setEnabled,
            Func<bool> getActive,
            Func<string> getKeyLabel,
            Action rebind,
            Func<string> getPressLabel,
            Action cyclePress,
            Func<string> getActionLabel,
            Action activate,
            bool isToggle,
            Action openSettings = null)
        {
            Rows.Add(new CascadeModRow(
                title,
                subtitle,
                getEnabled,
                setEnabled,
                getActive,
                getKeyLabel,
                rebind,
                getPressLabel,
                cyclePress,
                getActionLabel,
                activate,
                isToggle,
                openSettings));
            return this;
        }

        /// <summary>Adds one preset theme with a live swatch preview.</summary>
        public CascadeSection ThemePreset(
            string name,
            string subtitle,
            Color[] swatch,
            Func<bool> selected,
            Action apply)
        {
            Rows.Add(new CascadePresetRow(name, subtitle, swatch, selected, apply));
            return this;
        }

        /// <summary>One editable line of text (hex color input).</summary>
        public CascadeSection TextField(
            string title,
            string subtitle,
            string placeholder,
            int maxLength,
            Func<string> get,
            Action<string> set)
        {
            Rows.Add(new CascadeTextFieldRow(title, subtitle, placeholder, maxLength, get, set));
            return this;
        }

        /// <summary>A row of quick-pick color swatches.</summary>
        public CascadeSection Swatches(string title, string subtitle, Color[] colors, Action<int> pick)
        {
            Rows.Add(new CascadeSwatchRow(title, subtitle, colors, pick));
            return this;
        }

        /// <summary>A live preview of the current theme, built from the theme roles.</summary>
        public CascadeSection ThemePreview(string title, string subtitle)
        {
            Rows.Add(new CascadePreviewRow(title, subtitle));
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

        /// <summary>When set, the rendered value is polled instead of frozen.</summary>
        public readonly Func<string> GetValue;

        /// <summary>Optional emphasis (for example, Active ON) applied to the value.</summary>
        public readonly Func<bool> IsHighlighted;

        public CascadeLabelRow(string title, string subtitle, string value) : base(title, subtitle)
        {
            Value = value ?? string.Empty;
        }

        public CascadeLabelRow(string title, string subtitle, Func<string> getValue,
            Func<bool> isHighlighted = null) : base(title, subtitle)
        {
            Value = string.Empty;
            GetValue = getValue;
            IsHighlighted = isHighlighted;
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

        /// <summary>Live gorilla tint used by the avatar swatch in the list.</summary>
        public readonly Func<Color> GetColor;

        /// <summary>Live rig used by the 3D portrait (null until the rig spawns).</summary>
        public readonly Func<VRRig> GetRig;

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
            Action copyName,
            Func<Color> getColor = null,
            Func<VRRig> getRig = null) : base(title, subtitle)
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
            GetColor = getColor;
            GetRig = getRig;
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

    /// <summary>
    /// Large live portrait of one player's actual gorilla, shown at the top of
    /// their page. Rendered by <see cref="CascadeRigPortrait"/>.
    /// </summary>
    internal sealed class CascadePortraitRow : CascadeRow
    {
        public readonly Func<VRRig> GetRig;
        public readonly Func<Color> GetColor;

        public CascadePortraitRow(
            string title,
            string subtitle,
            Func<VRRig> getRig,
            Func<Color> getColor) : base(title, subtitle)
        {
            GetRig = getRig;
            GetColor = getColor;
        }

        public override float Height
        {
            get { return 156f; }
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

    /// <summary>
    /// The generated row for one <c>Mod</c>. All live values are polled by
    /// <see cref="CascadeModView"/>, so pressing a keybind updates the Active
    /// indicator without rebuilding the page.
    /// </summary>
    internal sealed class CascadeModRow : CascadeRow
    {
        public readonly Func<bool> GetEnabled;
        public readonly Action<bool> SetEnabled;
        public readonly Func<bool> GetActive;
        public readonly Func<string> GetKeyLabel;
        public readonly Action Rebind;
        public readonly Func<string> GetPressLabel;
        public readonly Action CyclePress;
        public readonly Func<string> GetActionLabel;
        public readonly Action Activate;
        public readonly bool IsToggle;
        public readonly Action OpenSettings;

        public CascadeModRow(
            string title,
            string subtitle,
            Func<bool> getEnabled,
            Action<bool> setEnabled,
            Func<bool> getActive,
            Func<string> getKeyLabel,
            Action rebind,
            Func<string> getPressLabel,
            Action cyclePress,
            Func<string> getActionLabel,
            Action activate,
            bool isToggle,
            Action openSettings = null) : base(title, subtitle)
        {
            GetEnabled = getEnabled;
            SetEnabled = setEnabled;
            GetActive = getActive;
            GetKeyLabel = getKeyLabel;
            Rebind = rebind;
            GetPressLabel = getPressLabel;
            CyclePress = cyclePress;
            GetActionLabel = getActionLabel;
            Activate = activate;
            IsToggle = isToggle;
            OpenSettings = openSettings;
        }

        public override float Height
        {
            get { return 52f; }
        }

        public override string SearchText
        {
            get
            {
                string key = GetKeyLabel != null ? GetKeyLabel() : string.Empty;
                string press = GetPressLabel != null ? GetPressLabel() : string.Empty;
                return base.SearchText + " keybind " + key + " " + press + " active";
            }
        }
    }

    /// <summary>
    /// One preset theme row. The swatch strip is a miniature of the theme so
    /// picking a season is visual instead of a list of names.
    /// </summary>
    internal sealed class CascadePresetRow : CascadeRow
    {
        public readonly Color[] Swatch;
        public readonly Func<bool> IsSelected;
        public readonly Action Apply;

        public CascadePresetRow(string title, string subtitle, Color[] swatch, Func<bool> selected, Action apply)
            : base(title, subtitle)
        {
            Swatch = swatch ?? new Color[0];
            IsSelected = selected;
            Apply = apply;
        }

        public override float Height
        {
            get { return 54f; }
        }
    }

    /// <summary>A row that edits one short string (the hex color box).</summary>
    internal sealed class CascadeTextFieldRow : CascadeRow
    {
        public readonly string Placeholder;
        public readonly int MaxLength;
        public readonly Func<string> Get;
        public readonly Action<string> Set;

        public CascadeTextFieldRow(
            string title,
            string subtitle,
            string placeholder,
            int maxLength,
            Func<string> get,
            Action<string> set) : base(title, subtitle)
        {
            Placeholder = placeholder ?? string.Empty;
            MaxLength = Mathf.Max(1, maxLength);
            Get = get;
            Set = set;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 48f : 56f; }
        }
    }

    /// <summary>A row of quick-pick color chips used by the custom picker.</summary>
    internal sealed class CascadeSwatchRow : CascadeRow
    {
        public readonly Color[] Colors;
        public readonly Action<int> Pick;

        public CascadeSwatchRow(string title, string subtitle, Color[] colors, Action<int> pick)
            : base(title, subtitle)
        {
            Colors = colors ?? new Color[0];
            Pick = pick;
        }

        public override float Height
        {
            get { return string.IsNullOrEmpty(Subtitle) ? 44f : 52f; }
        }
    }

    /// <summary>Live scrolling text console (debug log output).</summary>
    internal sealed class CascadeConsoleRow : CascadeRow
    {
        /// <summary>Called to get the current rich-text log content.</summary>
        public readonly Func<string> GetText;

        public CascadeConsoleRow(Func<string> getText) : base("Console", string.Empty)
        {
            GetText = getText ?? (() => string.Empty);
        }

        public override float Height { get { return 240f; } }
    }

    /// <summary>A title with a row of pill chips; exactly one chip is selected.</summary>
    internal sealed class CascadeChipRow : CascadeRow
    {
        public readonly string[] Options;
        public readonly Func<int> Get;
        public readonly Action<int> Set;

        public CascadeChipRow(string title, string[] options, Func<int> get, Action<int> set)
            : base(title, string.Empty)
        {
            Options = options ?? new string[0];
            Get = get;
            Set = set;
        }

        public override float Height { get { return 38f; } }
    }

    /// <summary>Live preview of every important color role in the current theme.</summary>
    internal sealed class CascadePreviewRow : CascadeRow
    {
        public CascadePreviewRow(string title, string subtitle) : base(title, subtitle) { }

        public override float Height
        {
            get { return 96f; }
        }
    }
}
