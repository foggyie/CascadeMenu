using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;

namespace CascadeMenu.UI
{
    /// <summary>Every color role the menu can draw.</summary>
    internal enum CascadeRole
    {
        None = 0,
        TextPrimary,
        TextSecondary,
        TextTertiary,
        WindowBorder,
        SidebarBg,
        ContentBg,
        TitlebarBg,
        TitlebarHairline,
        ViewBg,
        ViewBorder,
        Separator,
        RowHover,
        TabHover,
        Accent,
        AccentSoft,
        SelectionText,
        ToggleOn,
        ToggleOff,
        ToggleKnob,
        SliderTrack,
        SliderFill,
        SliderThumb,
        SliderThumbBorder,
        ButtonPrimaryTop,
        ButtonPrimaryBottom,
        ButtonSecondaryTop,
        ButtonSecondaryBottom,
        ButtonSecondaryHover,
        ButtonPressed,
        ButtonText,
        ButtonSecondaryText,
        FieldBg,
        FieldBorder,
        FieldFocus,
        Scrollbar,
        TrafficClose,
        TrafficMinimize,
        TrafficZoom,
        TrafficGlyph,
        ToastBg,
        ToastBorder,
        IconNormal,
        IconSelected,
        ShadowColor,
        Active,
        ActiveSoft,
        Danger,
        Warning,
        Success,
        TapBadgeBg,
        TapBadgeText,
        ModalDim,
        Pointer,
        PointerShadow,
        AvatarBg,
        HoverTint,
        PrimaryHoverTint,
        KnobShadow,
        ThumbShadow,
        DisabledToggle,
        LiveBadge,
        Count
    }

    /// <summary>
    /// A color plus the role it came from. Theme properties return this instead
    /// of a bare <see cref="Color"/>, which lets the UI factory remember what to
    /// recolor later without rewriting every widget call site.
    /// </summary>
    internal readonly struct CascadeColor
    {
        public const float FollowRoleAlpha = -1f;

        public readonly Color Value;
        public readonly CascadeRole Role;
        public readonly float Alpha;

        public CascadeColor(Color value, CascadeRole role, float alpha = FollowRoleAlpha)
        {
            Value = value;
            Role = role;
            Alpha = alpha;
        }

        public float r { get { return Value.r; } }
        public float g { get { return Value.g; } }
        public float b { get { return Value.b; } }
        public float a { get { return Alpha >= 0f ? Alpha : Value.a; } }

        public CascadeColor WithAlpha(float alpha)
        {
            Color value = Value;
            value.a = alpha;
            return new CascadeColor(value, Role, alpha);
        }

        public static implicit operator Color(CascadeColor color)
        {
            Color value = color.Value;
            if (color.Alpha >= 0f) value.a = color.Alpha;
            return value;
        }

        public static implicit operator CascadeColor(Color color)
        {
            return new CascadeColor(color, CascadeRole.None);
        }
    }

    internal sealed class CascadeGraphicBinding
    {
        public Graphic Graphic;
        public CascadeRole Role;
        public float Alpha = CascadeColor.FollowRoleAlpha;
    }

    internal sealed class CascadeGradientBinding
    {
        public CascadeGradient Gradient;
        public CascadeRole TopRole;
        public CascadeRole BottomRole;
        public float TopAlpha = CascadeColor.FollowRoleAlpha;
        public float BottomAlpha = CascadeColor.FollowRoleAlpha;
    }

    /// <summary>
    /// The central color table. One palette is derived into every menu role, so
    /// presets and the custom picker only edit the palette.
    /// </summary>
    internal sealed class CascadeTheme
    {
        public const float WindowWidth = 850f;
        public const float WindowHeight = 530f;
        public const float SidebarWidth = 200f;
        public const float TitlebarHeight = 52f;
        public const float CornerRadius = 10f;

        private readonly Color[] _colors = new Color[(int)CascadeRole.Count];

        // A single cached list of color bindings. Recoloring never searches the
        // scene; it walks only the graphics the UI factory already registered.
        private static readonly List<CascadeGraphicBinding> Bindings = new List<CascadeGraphicBinding>();
        private static readonly List<CascadeGradientBinding> GradientBindings = new List<CascadeGradientBinding>();

        public string Name = "Default";
        public bool IsCustom;
        public bool IsDark = true;
        public bool ContrastAdjusted;

        /// <summary>Increments after a theme is applied so live widgets can refresh.</summary>
        public int Version { get; private set; }

        /// <summary>The theme currently built into the menu. Bindings resolve against it.</summary>
        public static CascadeTheme Active;

        public static Color Hex(string hex, float alpha = 1f)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(hex, out color)) color = Color.white;
            color.a = alpha;
            return color;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // -----------------------------------------------------------------
        // Role accessors. These keep the existing `theme.TextPrimary` call
        // sites working while carrying the role metadata.
        // -----------------------------------------------------------------
        private CascadeColor Role(CascadeRole role)
        {
            return new CascadeColor(_colors[(int)role], role);
        }

        public CascadeColor TextPrimary { get { return Role(CascadeRole.TextPrimary); } }
        public CascadeColor TextSecondary { get { return Role(CascadeRole.TextSecondary); } }
        public CascadeColor TextTertiary { get { return Role(CascadeRole.TextTertiary); } }
        public CascadeColor WindowBorder { get { return Role(CascadeRole.WindowBorder); } }
        public CascadeColor SidebarBg { get { return Role(CascadeRole.SidebarBg); } }
        public CascadeColor ContentBg { get { return Role(CascadeRole.ContentBg); } }
        public CascadeColor TitlebarBg { get { return Role(CascadeRole.TitlebarBg); } }
        public CascadeColor TitlebarHairline { get { return Role(CascadeRole.TitlebarHairline); } }
        public CascadeColor ViewBg { get { return Role(CascadeRole.ViewBg); } }
        public CascadeColor ViewBorder { get { return Role(CascadeRole.ViewBorder); } }
        public CascadeColor Separator { get { return Role(CascadeRole.Separator); } }
        public CascadeColor RowHover { get { return Role(CascadeRole.RowHover); } }
        public CascadeColor TabHover { get { return Role(CascadeRole.TabHover); } }
        public CascadeColor Accent { get { return Role(CascadeRole.Accent); } }
        public CascadeColor AccentSoft { get { return Role(CascadeRole.AccentSoft); } }
        public CascadeColor SelectionText { get { return Role(CascadeRole.SelectionText); } }
        public CascadeColor ToggleOn { get { return Role(CascadeRole.ToggleOn); } }
        public CascadeColor ToggleOff { get { return Role(CascadeRole.ToggleOff); } }
        public CascadeColor ToggleKnob { get { return Role(CascadeRole.ToggleKnob); } }
        public CascadeColor SliderTrack { get { return Role(CascadeRole.SliderTrack); } }
        public CascadeColor SliderFill { get { return Role(CascadeRole.SliderFill); } }
        public CascadeColor SliderThumb { get { return Role(CascadeRole.SliderThumb); } }
        public CascadeColor SliderThumbBorder { get { return Role(CascadeRole.SliderThumbBorder); } }
        public CascadeColor ButtonPrimaryTop { get { return Role(CascadeRole.ButtonPrimaryTop); } }
        public CascadeColor ButtonPrimaryBottom { get { return Role(CascadeRole.ButtonPrimaryBottom); } }
        public CascadeColor ButtonSecondaryTop { get { return Role(CascadeRole.ButtonSecondaryTop); } }
        public CascadeColor ButtonSecondaryBottom { get { return Role(CascadeRole.ButtonSecondaryBottom); } }
        public CascadeColor ButtonSecondaryHover { get { return Role(CascadeRole.ButtonSecondaryHover); } }
        public CascadeColor ButtonPressed { get { return Role(CascadeRole.ButtonPressed); } }
        public CascadeColor ButtonText { get { return Role(CascadeRole.ButtonText); } }
        public CascadeColor ButtonSecondaryText { get { return Role(CascadeRole.ButtonSecondaryText); } }
        public CascadeColor FieldBg { get { return Role(CascadeRole.FieldBg); } }
        public CascadeColor FieldBorder { get { return Role(CascadeRole.FieldBorder); } }
        public CascadeColor FieldFocus { get { return Role(CascadeRole.FieldFocus); } }
        public CascadeColor Scrollbar { get { return Role(CascadeRole.Scrollbar); } }
        public CascadeColor TrafficClose { get { return Role(CascadeRole.TrafficClose); } }
        public CascadeColor TrafficMinimize { get { return Role(CascadeRole.TrafficMinimize); } }
        public CascadeColor TrafficZoom { get { return Role(CascadeRole.TrafficZoom); } }
        public CascadeColor TrafficGlyph { get { return Role(CascadeRole.TrafficGlyph); } }
        public CascadeColor ToastBg { get { return Role(CascadeRole.ToastBg); } }
        public CascadeColor ToastBorder { get { return Role(CascadeRole.ToastBorder); } }
        public CascadeColor IconNormal { get { return Role(CascadeRole.IconNormal); } }
        public CascadeColor IconSelected { get { return Role(CascadeRole.IconSelected); } }
        public CascadeColor ShadowColor { get { return Role(CascadeRole.ShadowColor); } }
        public CascadeColor ActiveColor { get { return Role(CascadeRole.Active); } }
        public CascadeColor ActiveSoft { get { return Role(CascadeRole.ActiveSoft); } }
        public CascadeColor Danger { get { return Role(CascadeRole.Danger); } }
        public CascadeColor Warning { get { return Role(CascadeRole.Warning); } }
        public CascadeColor Success { get { return Role(CascadeRole.Success); } }
        public CascadeColor TapBadgeBg { get { return Role(CascadeRole.TapBadgeBg); } }
        public CascadeColor TapBadgeText { get { return Role(CascadeRole.TapBadgeText); } }
        public CascadeColor ModalDim { get { return Role(CascadeRole.ModalDim); } }
        public CascadeColor Pointer { get { return Role(CascadeRole.Pointer); } }
        public CascadeColor PointerShadow { get { return Role(CascadeRole.PointerShadow); } }
        public CascadeColor AvatarBg { get { return Role(CascadeRole.AvatarBg); } }
        public CascadeColor HoverTint { get { return Role(CascadeRole.HoverTint); } }
        public CascadeColor PrimaryHoverTint { get { return Role(CascadeRole.PrimaryHoverTint); } }
        public CascadeColor KnobShadow { get { return Role(CascadeRole.KnobShadow); } }
        public CascadeColor ThumbShadow { get { return Role(CascadeRole.ThumbShadow); } }
        public CascadeColor DisabledToggle { get { return Role(CascadeRole.DisabledToggle); } }
        public CascadeColor LiveBadge { get { return Role(CascadeRole.LiveBadge); } }

        // Friendly aliases used by the Themes tab. They point at the same roles
        // the menu actually draws with.
        public CascadeColor Background { get { return ContentBg; } }
        public CascadeColor Panel { get { return SidebarBg; } }
        public CascadeColor Header { get { return TitlebarBg; } }
        public CascadeColor Text { get { return TextPrimary; } }
        public CascadeColor SecondaryText { get { return TextSecondary; } }
        public CascadeColor Border { get { return WindowBorder; } }

        // -----------------------------------------------------------------
        // Binding registry
        // -----------------------------------------------------------------

        public static void Bind(Graphic graphic, CascadeColor color)
        {
            if (graphic == null || color.Role == CascadeRole.None) return;
            Bind(graphic, color.Role, color.Alpha);
        }

        public static void Bind(Graphic graphic, CascadeRole role, float alpha = CascadeColor.FollowRoleAlpha)
        {
            if (graphic == null || role == CascadeRole.None) return;

            for (int i = 0; i < Bindings.Count; i++)
            {
                CascadeGraphicBinding existing = Bindings[i];
                if (existing.Graphic == graphic)
                {
                    existing.Role = role;
                    existing.Alpha = alpha;
                    return;
                }
            }

            Bindings.Add(new CascadeGraphicBinding
            {
                Graphic = graphic,
                Role = role,
                Alpha = alpha
            });
        }

        public static void Bind(CascadeGradient gradient)
        {
            if (gradient == null) return;

            for (int i = 0; i < GradientBindings.Count; i++)
            {
                if (GradientBindings[i].Gradient == gradient)
                {
                    UpdateGradientBinding(GradientBindings[i], gradient);
                    return;
                }
            }

            var binding = new CascadeGradientBinding { Gradient = gradient };
            UpdateGradientBinding(binding, gradient);
            GradientBindings.Add(binding);
        }

        private static void UpdateGradientBinding(CascadeGradientBinding binding, CascadeGradient gradient)
        {
            binding.TopRole = gradient.Top.Role;
            binding.BottomRole = gradient.Bottom.Role;
            binding.TopAlpha = gradient.Top.Alpha;
            binding.BottomAlpha = gradient.Bottom.Alpha;
        }

        /// <summary>
        /// Applies a role color now and keeps the graphic in the recolor cache,
        /// so a later theme switch repaints it without rebuilding the page.
        /// </summary>
        public static void SetGraphicColor(Graphic graphic, CascadeColor color)
        {
            if (graphic == null) return;
            graphic.color = color;
            Bind(graphic, color);
        }

        /// <summary>Applies a raw color without registering a role binding.</summary>
        public static void SetGraphicColor(Graphic graphic, Color color)
        {
            if (graphic == null) return;
            graphic.color = color;
        }

        /// <summary>Keeps a binding's alpha in sync when a widget fades a graphic.</summary>
        public static void UpdateAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            for (int i = 0; i < Bindings.Count; i++)
            {
                if (Bindings[i].Graphic == graphic)
                {
                    Bindings[i].Alpha = alpha;
                    return;
                }
            }
        }

        // -----------------------------------------------------------------
        // Palette application
        // -----------------------------------------------------------------

        public bool Apply(CascadeThemePreset preset)
        {
            if (preset == null) preset = CascadeThemePreset.Default;

            Name = preset.Name;
            IsCustom = string.Equals(Name, "Custom", System.StringComparison.Ordinal);
            IsDark = preset.Dark;
            CascadeThemePalette p = preset.Palette;

            Color background = p[(int)CascadePaletteIndex.Background];
            Color panel = p[(int)CascadePaletteIndex.Panel];
            Color header = p[(int)CascadePaletteIndex.Header];
            Color button = p[(int)CascadePaletteIndex.Button];
            Color buttonHover = p[(int)CascadePaletteIndex.ButtonHover];
            Color buttonPressed = p[(int)CascadePaletteIndex.ButtonPressed];
            Color toggleOn = p[(int)CascadePaletteIndex.ToggleOn];
            Color toggleOff = p[(int)CascadePaletteIndex.ToggleOff];
            Color text = p[(int)CascadePaletteIndex.Text];
            Color secondary = p[(int)CascadePaletteIndex.SecondaryText];
            Color accent = p[(int)CascadePaletteIndex.Accent];
            Color border = p[(int)CascadePaletteIndex.Border];
            Color active = p[(int)CascadePaletteIndex.Active];
            Color field = p[(int)CascadePaletteIndex.Field];
            Color danger = p[(int)CascadePaletteIndex.Danger];
            Color warning = p[(int)CascadePaletteIndex.Warning];
            Color success = p[(int)CascadePaletteIndex.Success];

            float textRatio = ContrastRatio(text, background);
            ContrastAdjusted = textRatio < 3f;
            if (ContrastAdjusted) text = IsLight(background) ? Color.black : Color.white;

            if (ContrastRatio(secondary, background) < 1.8f)
                secondary = IsLight(background) ? Color.black : Color.white;

            Color selection = IsLight(accent) ? Hex("#111111") : Color.white;
            bool dark = IsDark;
            float normalAlpha = dark ? 0.94f : 0.92f;
            float secondaryAlpha = dark ? 0.68f : 0.62f;

            Set(CascadeRole.TextPrimary, WithAlpha(text, normalAlpha));
            Set(CascadeRole.TextSecondary, WithAlpha(secondary, secondaryAlpha));
            Set(CascadeRole.TextTertiary, WithAlpha(text, dark ? 0.40f : 0.38f));

            Set(CascadeRole.ContentBg, WithAlpha(Darken(background, dark ? 0.04f : 0f), dark ? 0.76f : 0.82f));
            Set(CascadeRole.SidebarBg, WithAlpha(panel, dark ? 0.62f : 0.72f));
            Set(CascadeRole.TitlebarBg, WithAlpha(header, dark ? 0.74f : 0.82f));
            Set(CascadeRole.ViewBg, WithAlpha(dark ? Lighten(panel, 0.05f) : panel, dark ? 0.62f : 0.86f));
            Set(CascadeRole.WindowBorder, WithAlpha(dark ? Color.white : border, dark ? 0.36f : 0.28f));
            Set(CascadeRole.TitlebarHairline, dark ? Hex("#FFFFFF", 0.16f) : Hex("#000000", 0.10f));
            Set(CascadeRole.ViewBorder, WithAlpha(dark ? Color.white : border, dark ? 0.18f : 0.16f));
            Set(CascadeRole.Separator, WithAlpha(dark ? Color.white : border, dark ? 0.15f : 0.14f));
            Set(CascadeRole.RowHover, dark ? Hex("#FFFFFF", 0.09f) : WithAlpha(border, 0.06f));
            Set(CascadeRole.TabHover, dark ? Hex("#FFFFFF", 0.12f) : WithAlpha(border, 0.08f));
            Set(CascadeRole.HoverTint, dark ? Hex("#FFFFFF", 0.12f) : WithAlpha(border, 0.08f));

            Set(CascadeRole.Accent, accent);
            Set(CascadeRole.AccentSoft, toggleOn);
            Set(CascadeRole.ToggleOn, toggleOn);
            Set(CascadeRole.ToggleOff, WithAlpha(toggleOff, dark ? 0.60f : 0.09f));
            Set(CascadeRole.ToggleKnob, Color.white);
            Set(CascadeRole.SelectionText, selection);
            Set(CascadeRole.ButtonText, selection);
            Set(CascadeRole.ButtonSecondaryText, WithAlpha(text, dark ? 0.92f : 0.86f));
            Set(CascadeRole.IconSelected, selection);

            Set(CascadeRole.SliderTrack, dark ? Lighten(panel, 0.07f) : WithAlpha(Color.black, 0.09f));
            Set(CascadeRole.SliderFill, toggleOn);
            Set(CascadeRole.SliderThumb, Color.white);
            Set(CascadeRole.SliderThumbBorder, dark ? WithAlpha(Color.black, 0.20f) : WithAlpha(Color.black, 0.06f));

            Set(CascadeRole.ButtonPrimaryTop, dark ? Lighten(accent, 0.18f) : Lighten(accent, 0.08f));
            Set(CascadeRole.ButtonPrimaryBottom, dark ? Darken(accent, 0.10f) : accent);
            Set(CascadeRole.ButtonSecondaryTop, button);
            Set(CascadeRole.ButtonSecondaryBottom, dark ? Darken(button, 0.03f) : Darken(button, 0.02f));
            Set(CascadeRole.ButtonSecondaryHover, buttonHover);
            Set(CascadeRole.ButtonPressed, buttonPressed);

            Set(CascadeRole.FieldBg, dark ? Hex("#FFFFFF", 0.08f) : WithAlpha(field, 0.04f));
            Set(CascadeRole.FieldBorder, dark ? Hex("#FFFFFF", 0.20f) : WithAlpha(border, 0.10f));
            Set(CascadeRole.FieldFocus, accent);
            Set(CascadeRole.Scrollbar, WithAlpha(border, dark ? 0.26f : 0.28f));

            Set(CascadeRole.TrafficClose, Hex("#FF5F57"));
            Set(CascadeRole.TrafficMinimize, Hex("#FEBC2E"));
            Set(CascadeRole.TrafficZoom, Hex("#28C840"));
            Set(CascadeRole.TrafficGlyph, Hex("#000000", 0.55f));

            Set(CascadeRole.ToastBg, WithAlpha(panel, dark ? 0.76f : 0.84f));
            Set(CascadeRole.ToastBorder, WithAlpha(border, 0.30f));
            Set(CascadeRole.IconNormal, WithAlpha(text, dark ? 0.55f : 0.50f));
            Set(CascadeRole.ShadowColor, WithAlpha(Color.black, dark ? 0.55f : 0.35f));

            Set(CascadeRole.Active, active);
            Set(CascadeRole.ActiveSoft, WithAlpha(active, 0.16f));
            Set(CascadeRole.Danger, danger);
            Set(CascadeRole.Warning, warning);
            Set(CascadeRole.Success, success);
            Set(CascadeRole.TapBadgeBg, WithAlpha(warning, 0.22f));
            Set(CascadeRole.TapBadgeText, Lighten(warning, 0.12f));
            Set(CascadeRole.ModalDim, Hex("#000000", 0.62f));
            Set(CascadeRole.Pointer, accent);
            Set(CascadeRole.PointerShadow, Hex("#000000", 0.62f));
            Set(CascadeRole.AvatarBg, WithAlpha(border, dark ? 0.11f : 0.08f));
            Set(CascadeRole.PrimaryHoverTint, Hex("#FFFFFF", 0.14f));
            Set(CascadeRole.KnobShadow, Hex("#000000", 0.20f));
            Set(CascadeRole.ThumbShadow, Hex("#000000", 0.18f));
            Set(CascadeRole.DisabledToggle, dark ? Lighten(panel, 0.12f) : Darken(panel, 0.22f));
            Set(CascadeRole.LiveBadge, Hex("#000000", 0.56f));

            Version++;
            Active = this;
            RecolorAll();
            return ContrastAdjusted;
        }

        private void Set(CascadeRole role, Color color)
        {
            _colors[(int)role] = color;
        }

        private Color RoleColor(CascadeRole role, float alpha)
        {
            Color color = _colors[(int)role];
            if (alpha >= 0f) color.a = alpha;
            return color;
        }

        /// <summary>Repaints only registered graphics, and only when their target changed.</summary>
        public void RecolorAll()
        {
            Active = this;

            for (int i = Bindings.Count - 1; i >= 0; i--)
            {
                CascadeGraphicBinding binding = Bindings[i];
                if (binding.Graphic == null)
                {
                    Bindings.RemoveAt(i);
                    continue;
                }

                Color target = RoleColor(binding.Role, binding.Alpha);
                if (binding.Graphic.color != target) binding.Graphic.color = target;
            }

            for (int i = GradientBindings.Count - 1; i >= 0; i--)
            {
                CascadeGradientBinding binding = GradientBindings[i];
                if (binding.Gradient == null)
                {
                    GradientBindings.RemoveAt(i);
                    continue;
                }

                if (binding.TopRole != CascadeRole.None)
                    binding.Gradient.Top = new CascadeColor(
                        RoleColor(binding.TopRole, binding.TopAlpha), binding.TopRole, binding.TopAlpha);
                if (binding.BottomRole != CascadeRole.None)
                    binding.Gradient.Bottom = new CascadeColor(
                        RoleColor(binding.BottomRole, binding.BottomAlpha), binding.BottomRole, binding.BottomAlpha);

                binding.Gradient.Refresh();
            }
        }

        /// <summary>
        /// Ultra-fast live RGB accent mutation (zero allocations) used when
        /// RGB Rainbow Mode is enabled. Mode 0 = Accent + Glow + Sub-Tab Bar,
        /// Mode 1 = Full Neon (Accent + Borders + Active Dots),
        /// Mode 2 = Subtle (Accent + Glow + Sub-Tab Bar softly tinted).
        /// </summary>
        public void ApplyRgbAccent(Color rgb, int mode)
        {
            rgb.a = 1f;
            Color soft = new Color(rgb.r, rgb.g, rgb.b, 0.90f);
            Color top = Lighten(rgb, 0.14f);
            Color bottom = Darken(rgb, 0.14f);

            _colors[(int)CascadeRole.Accent] = rgb;
            _colors[(int)CascadeRole.AccentSoft] = soft;
            _colors[(int)CascadeRole.Pointer] = rgb;

            if (mode != 2)
            {
                _colors[(int)CascadeRole.SliderFill] = rgb;
                _colors[(int)CascadeRole.ToggleOn] = rgb;
                _colors[(int)CascadeRole.ButtonPrimaryTop] = top;
                _colors[(int)CascadeRole.ButtonPrimaryBottom] = bottom;
                _colors[(int)CascadeRole.FieldFocus] = rgb;
            }

            if (mode == 1)
            {
                _colors[(int)CascadeRole.WindowBorder] = new Color(rgb.r, rgb.g, rgb.b, 0.65f);
                _colors[(int)CascadeRole.ViewBorder] = new Color(rgb.r, rgb.g, rgb.b, 0.42f);
                _colors[(int)CascadeRole.Active] = rgb;
                _colors[(int)CascadeRole.ActiveSoft] = new Color(rgb.r, rgb.g, rgb.b, 0.22f);
            }

            Version++;
            RecolorAll();
        }

        // -----------------------------------------------------------------
        // Config helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Builds the startup theme from the config. Auto mode picks the theme
        /// whose date range matches today. Returns one theme instance that the
        /// menu keeps and mutates for live edits.
        /// </summary>
        public static CascadeTheme FromConfig(MenuConfig config)
        {
            var theme = new CascadeTheme();

            CascadeThemePalette custom = CascadeThemePalette.Parse(config.CustomPalette.Value);
            CascadeThemePreset preset = null;
            bool auto = config.ThemeAuto.Value;

            if (auto)
            {
                preset = CascadeThemePreset.ForDate(System.DateTime.Now);
                if (preset == null) preset = CascadeThemePreset.Default;
            }
            else
            {
                string selected = config.ThemePreset.Value;
                if (string.Equals(selected, "Custom", System.StringComparison.OrdinalIgnoreCase) && custom != null)
                {
                    preset = new CascadeThemePreset("Custom", IsLight(custom[(int)CascadePaletteIndex.Background]) == false,
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Background]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Panel]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Header]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Button]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.ButtonHover]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.ButtonPressed]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.ToggleOn]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.ToggleOff]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Text]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.SecondaryText]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Accent]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Border]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Active]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Field]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Danger]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Warning]),
                        CascadeThemePalette.ToHex(custom[(int)CascadePaletteIndex.Success]));
                }
                else
                {
                    preset = CascadeThemePreset.Find(selected);
                }
            }

            if (preset == null) preset = CascadeThemePreset.Default;
            theme.Apply(preset);
            return theme;
        }

        /// <summary>Builds a preset backed by an edited palette.</summary>
        public static CascadeThemePreset CustomPreset(CascadeThemePalette palette)
        {
            if (palette == null) palette = CascadeThemePreset.DefaultPalette.Clone();
            bool dark = !IsLight(palette[(int)CascadePaletteIndex.Background]);

            return new CascadeThemePreset("Custom", dark,
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Background]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Panel]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Header]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Button]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.ButtonHover]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.ButtonPressed]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.ToggleOn]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.ToggleOff]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Text]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.SecondaryText]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Accent]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Border]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Active]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Field]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Danger]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Warning]),
                CascadeThemePalette.ToHex(palette[(int)CascadePaletteIndex.Success]));
        }

        // -----------------------------------------------------------------
        // Color math
        // -----------------------------------------------------------------

        public static bool IsLight(Color color)
        {
            return Luminance(color) > 0.55f;
        }

        public static float Luminance(Color color)
        {
            return 0.2126f * Channel(color.r) + 0.7152f * Channel(color.g) + 0.0722f * Channel(color.b);
        }

        private static float Channel(float value)
        {
            return value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        public static float ContrastRatio(Color a, Color b)
        {
            float first = Luminance(a);
            float second = Luminance(b);
            float bright = Mathf.Max(first, second);
            float dark = Mathf.Min(first, second);
            return (bright + 0.05f) / (dark + 0.05f);
        }

        public static Color Lighten(Color color, float amount)
        {
            return Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
        }

        public static Color Darken(Color color, float amount)
        {
            return Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
        }
    }
}
