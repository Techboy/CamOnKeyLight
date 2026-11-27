using System.Windows;
using System.Windows.Media;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    internal static class BrandTheme
    {
        private static readonly string[] Keys =
        {
            "WindowBackgroundBrush", "TextBrush", "MutedBrush", "CardBrush", "InputBrush", "InputTextBrush",
            "ButtonBrush", "ButtonTextBrush", "BorderBrush", "AccentBrush", "AccentTextBrush", "LinkBrush",
            "StatusOnBrush", "StatusOffBrush", "ErrorBrush", "MenuBrush", "MenuTextBrush", "HighlightBrush",
            "HighlightTextBrush", "PillOnBrush", "PillOffBrush", "PillAutoBrush", "NavSelectedBrush",
            "PillOnTextBrush", "PillOffTextBrush", "PillAutoTextBrush"
        };

        public static void Apply(EffectiveTheme theme)
        {
            ResourceDictionary dictionary = (ResourceDictionary)Application.Current.Resources.MergedDictionaries[0];
            Dictionary<string, Color> colors = theme == EffectiveTheme.System
                ? SystemPalette()
                : Brand(ThemeResolver.PaletteFor(theme), theme == EffectiveTheme.Dark);
            foreach (string key in Keys)
            {
                Color color = colors[key];
                if (dictionary[key] is SolidColorBrush brush && !brush.IsFrozen)
                    brush.Color = color;
                else
                    dictionary[key] = new SolidColorBrush(color);
            }
        }

        private static Dictionary<string, Color> Brand(ThemePalette palette, bool dark)
        {
            return new Dictionary<string, Color>
            {
                ["WindowBackgroundBrush"] = Media(palette.Background),
                ["TextBrush"] = Media(palette.Foreground),
                ["MutedBrush"] = Media(palette.Muted),
                ["CardBrush"] = Media(palette.Card),
                ["InputBrush"] = Media(palette.InputBackground),
                ["InputTextBrush"] = Media(palette.InputForeground),
                ["ButtonBrush"] = Media(palette.ButtonBackground),
                ["ButtonTextBrush"] = Media(palette.ButtonForeground),
                ["BorderBrush"] = Media(palette.Border),
                ["AccentBrush"] = Media(palette.Accent),
                ["AccentTextBrush"] = Media(palette.AccentText),
                ["LinkBrush"] = Media(palette.Link),
                ["StatusOnBrush"] = Media(palette.StatusOn),
                ["StatusOffBrush"] = Media(palette.StatusOff),
                ["ErrorBrush"] = Media(palette.Error),
                ["MenuBrush"] = Media(palette.MenuBackground),
                ["MenuTextBrush"] = Media(palette.MenuForeground),
                ["HighlightBrush"] = Media(palette.MenuHighlight),
                ["HighlightTextBrush"] = Media(palette.MenuHighlightText),
                ["PillOnBrush"] = Hex(dark ? 0x1A3A32 : 0xE5F6EF),
                ["PillOffBrush"] = Hex(dark ? 0x2A2A32 : 0xEFE6D4),
                ["PillAutoBrush"] = Hex(dark ? 0x3A3018 : 0xF8E7C0),
                ["PillOnTextBrush"] = Media(palette.StatusOn),
                ["PillOffTextBrush"] = Media(palette.Muted),
                ["PillAutoTextBrush"] = Media(palette.Link),
                ["NavSelectedBrush"] = dark ? Color.FromArgb(0x28, 0xF5, 0xB9, 0x42) : Hex(0xF8E7C0)
            };
        }

        private static Dictionary<string, Color> SystemPalette()
        {
            return new Dictionary<string, Color>
            {
                ["WindowBackgroundBrush"] = SystemColors.WindowColor,
                ["TextBrush"] = SystemColors.WindowTextColor,
                ["MutedBrush"] = SystemColors.GrayTextColor,
                ["CardBrush"] = SystemColors.ControlColor,
                ["InputBrush"] = SystemColors.WindowColor,
                ["InputTextBrush"] = SystemColors.WindowTextColor,
                ["ButtonBrush"] = SystemColors.ControlColor,
                ["ButtonTextBrush"] = SystemColors.ControlTextColor,
                ["BorderBrush"] = SystemColors.ControlDarkColor,
                ["AccentBrush"] = SystemColors.HighlightColor,
                ["AccentTextBrush"] = SystemColors.HighlightTextColor,
                ["LinkBrush"] = SystemColors.HotTrackColor,
                ["StatusOnBrush"] = SystemColors.WindowTextColor,
                ["StatusOffBrush"] = SystemColors.WindowTextColor,
                ["ErrorBrush"] = SystemColors.WindowTextColor,
                ["MenuBrush"] = SystemColors.MenuColor,
                ["MenuTextBrush"] = SystemColors.MenuTextColor,
                ["HighlightBrush"] = SystemColors.HighlightColor,
                ["HighlightTextBrush"] = SystemColors.HighlightTextColor,
                ["PillOnBrush"] = SystemColors.ControlColor,
                ["PillOffBrush"] = SystemColors.ControlColor,
                ["PillAutoBrush"] = SystemColors.HighlightColor,
                ["PillOnTextBrush"] = SystemColors.ControlTextColor,
                ["PillOffTextBrush"] = SystemColors.ControlTextColor,
                ["PillAutoTextBrush"] = SystemColors.HighlightTextColor,
                ["NavSelectedBrush"] = SystemColors.HighlightColor
            };
        }

        private static Color Media(ThemeColor color)
        {
            return Color.FromRgb(color.R, color.G, color.B);
        }

        private static Color Hex(int hex)
        {
            return Color.FromRgb((byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF));
        }
    }
}
