using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
namespace CascadeMenu.Mods.Overlays
{
    /// <summary>
    /// Appearance preset shared by <see cref="NameTags"/> and
    /// <see cref="PlayerHud"/>.
    ///
    /// Every style only changes presentation: colors, corners, background,
    /// border, accent, name alignment/case and width. The overlay layout and
    /// performance contract stay the same.
    /// </summary>
    internal sealed class OverlayStyle
    {
        public string Name;
        public float CardWidth;
        public float TagWidth;
        public bool ShowShadow;
        public bool ShowBackground;
        public float BackgroundAlpha;
        public bool ShowBorder;
        public float BorderAlpha;
        public bool ShowAccent;
        public bool CenterName;
        public bool UppercaseName;
        public bool BoldName;
        public float NameSize;
        public int CornerRadius;
        public bool WhiteName;
    }

    /// <summary>Named appearance presets shown in each overlay's gear page.</summary>
    internal static class OverlayStyles
    {
        internal static readonly string[] Names =
        {
            "Classic",
            "VRChat",
            "Fortnite",
            "Minimal",
            "Glass"
        };

        private static readonly OverlayStyle[] Styles =
        {
            // Classic: the original Cascade card.
            new OverlayStyle
            {
                Name = "Classic",
                CardWidth = 252f,
                TagWidth = 220f,
                ShowShadow = true,
                ShowBackground = true,
                BackgroundAlpha = 0.86f,
                ShowBorder = true,
                BorderAlpha = 0.35f,
                ShowAccent = true,
                CenterName = false,
                UppercaseName = false,
                BoldName = true,
                NameSize = 16f,
                CornerRadius = 12,
                WhiteName = false
            },

            // VRChat: centered white name on a soft, nearly transparent plate.
            new OverlayStyle
            {
                Name = "VRChat",
                CardWidth = 236f,
                TagWidth = 244f,
                ShowShadow = false,
                ShowBackground = true,
                BackgroundAlpha = 0.22f,
                ShowBorder = false,
                BorderAlpha = 0f,
                ShowAccent = false,
                CenterName = true,
                UppercaseName = false,
                BoldName = true,
                NameSize = 19f,
                CornerRadius = 12,
                WhiteName = true
            },

            // Fortnite: compact centered banner with uppercase text and a bar.
            new OverlayStyle
            {
                Name = "Fortnite",
                CardWidth = 252f,
                TagWidth = 252f,
                ShowShadow = true,
                ShowBackground = true,
                BackgroundAlpha = 0.96f,
                ShowBorder = true,
                BorderAlpha = 0.8f,
                ShowAccent = true,
                CenterName = true,
                UppercaseName = true,
                BoldName = true,
                NameSize = 16.5f,
                CornerRadius = 3,
                WhiteName = true
            },

            // Minimal: just a readable centered name with a faint backing.
            new OverlayStyle
            {
                Name = "Minimal",
                CardWidth = 224f,
                TagWidth = 224f,
                ShowShadow = false,
                ShowBackground = true,
                BackgroundAlpha = 0.12f,
                ShowBorder = false,
                BorderAlpha = 0f,
                ShowAccent = false,
                CenterName = true,
                UppercaseName = false,
                BoldName = false,
                NameSize = 15f,
                CornerRadius = 6,
                WhiteName = true
            },

            // Glass: larger radius, translucent background and a thin border.
            new OverlayStyle
            {
                Name = "Glass",
                CardWidth = 264f,
                TagWidth = 240f,
                ShowShadow = true,
                ShowBackground = true,
                BackgroundAlpha = 0.24f,
                ShowBorder = true,
                BorderAlpha = 0.6f,
                ShowAccent = true,
                CenterName = false,
                UppercaseName = false,
                BoldName = true,
                NameSize = 16f,
                CornerRadius = 20,
                WhiteName = false
            }
        };

        internal static OverlayStyle Get(int index)
        {
            if (index < 0 || index >= Styles.Length) return Styles[0];
            return Styles[index];
        }
    }
}
