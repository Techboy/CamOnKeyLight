namespace CamOnKeyLight.Core
{
    public enum ThemePreference
    {
        Light,
        Dark,
        FollowWindows
    }

    public enum EffectiveTheme
    {
        Light,
        Dark,
        /// <summary>
        /// Windows high contrast is active, so the app keeps system colors.
        /// </summary>
        System
    }

    public readonly record struct ThemeColor(byte R, byte G, byte B)
    {
        public static ThemeColor FromHex(uint hex)
        {
            return new ThemeColor((byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF));
        }
    }

    public readonly record struct ThemePalette(
        ThemeColor Background,
        ThemeColor Foreground,
        ThemeColor InputBackground,
        ThemeColor InputForeground,
        ThemeColor ButtonBackground,
        ThemeColor ButtonForeground,
        ThemeColor StatusOn,
        ThemeColor StatusOff,
        ThemeColor Error,
        ThemeColor Muted,
        ThemeColor Border,
        ThemeColor MenuBackground,
        ThemeColor MenuForeground,
        ThemeColor MenuHighlight,
        ThemeColor MenuHighlightText,
        ThemeColor Link,
        ThemeColor Card,
        ThemeColor Accent,
        ThemeColor AccentText)
    {
        /// <summary>
        /// Warm paper and a darker key-light gold, so gold text stays readable.
        /// </summary>
        public static ThemePalette Light { get; } = new(
            Background: ThemeColor.FromHex(0xF7F4EE),
            Foreground: ThemeColor.FromHex(0x1A1408),
            InputBackground: ThemeColor.FromHex(0xFFFDF8),
            InputForeground: ThemeColor.FromHex(0x1A1408),
            ButtonBackground: ThemeColor.FromHex(0xEFE6D4),
            ButtonForeground: ThemeColor.FromHex(0x1A1408),
            StatusOn: ThemeColor.FromHex(0x0D6B45),
            StatusOff: ThemeColor.FromHex(0x9F1D1D),
            Error: ThemeColor.FromHex(0x9F1D1D),
            Muted: ThemeColor.FromHex(0x5C564C),
            Border: ThemeColor.FromHex(0xC9BFAE),
            MenuBackground: ThemeColor.FromHex(0xF3EFE6),
            MenuForeground: ThemeColor.FromHex(0x1A1408),
            MenuHighlight: ThemeColor.FromHex(0xF5B942),
            MenuHighlightText: ThemeColor.FromHex(0x1A1408),
            Link: ThemeColor.FromHex(0x7A5208),
            Card: ThemeColor.FromHex(0xFFFDF8),
            Accent: ThemeColor.FromHex(0xF5B942),
            AccentText: ThemeColor.FromHex(0x1A1408));

        /// <summary>
        /// Dark status dashboard. The accent is the warm key-light gold.
        /// </summary>
        public static ThemePalette Dark { get; } = new(
            Background: ThemeColor.FromHex(0x16161B),
            Foreground: ThemeColor.FromHex(0xF4F1EA),
            InputBackground: ThemeColor.FromHex(0x141418),
            InputForeground: ThemeColor.FromHex(0xF4F1EA),
            ButtonBackground: ThemeColor.FromHex(0x23232A),
            ButtonForeground: ThemeColor.FromHex(0xF4F1EA),
            StatusOn: ThemeColor.FromHex(0x5DCAA5),
            StatusOff: ThemeColor.FromHex(0xFFB4AB),
            Error: ThemeColor.FromHex(0xFFB4AB),
            Muted: ThemeColor.FromHex(0xB7B3AB),
            Border: ThemeColor.FromHex(0x3A3A44),
            MenuBackground: ThemeColor.FromHex(0x141418),
            MenuForeground: ThemeColor.FromHex(0xF4F1EA),
            MenuHighlight: ThemeColor.FromHex(0xF5B942),
            MenuHighlightText: ThemeColor.FromHex(0x1A1408),
            Link: ThemeColor.FromHex(0xF5B942),
            Card: ThemeColor.FromHex(0x1E1E25),
            Accent: ThemeColor.FromHex(0xF5B942),
            AccentText: ThemeColor.FromHex(0x1A1408));

        public IEnumerable<(string Name, ThemeColor Text, ThemeColor Behind)> TextPairs()
        {
            yield return ("text", Foreground, Background);
            yield return ("input", InputForeground, InputBackground);
            yield return ("button", ButtonForeground, ButtonBackground);
            yield return ("status on", StatusOn, Background);
            yield return ("status off", StatusOff, Background);
            yield return ("error", Error, Background);
            yield return ("muted", Muted, Background);
            yield return ("menu", MenuForeground, MenuBackground);
            yield return ("menu selection", MenuHighlightText, MenuHighlight);
            yield return ("link", Link, Background);
            yield return ("card text", Foreground, Card);
            yield return ("muted on card", Muted, Card);
            yield return ("accent label", AccentText, Accent);
            yield return ("link on card", Link, Card);
            yield return ("link on menu", Link, MenuBackground);
            yield return ("status on card", StatusOn, Card);
            yield return ("error on card", Error, Card);
        }
    }

    public static class ThemePreferenceParser
    {
        public static ThemePreference Parse(string? value)
        {
            if (value != null
                && Enum.TryParse(value.Trim(), ignoreCase: true, out ThemePreference preference)
                && Enum.IsDefined(preference)
                && string.Equals(Enum.GetName(preference), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return preference;
            }

            return ThemePreference.FollowWindows;
        }
    }

    public static class ThemeResolver
    {
        public static EffectiveTheme Resolve(ThemePreference preference, bool? appsUseLightTheme, bool highContrast)
        {
            if (highContrast)
                return EffectiveTheme.System;

            if (preference == ThemePreference.Light)
                return EffectiveTheme.Light;

            if (preference == ThemePreference.Dark)
                return EffectiveTheme.Dark;

            return appsUseLightTheme == false ? EffectiveTheme.Dark : EffectiveTheme.Light;
        }

        public static ThemePalette PaletteFor(EffectiveTheme theme)
        {
            return theme == EffectiveTheme.Dark ? ThemePalette.Dark : ThemePalette.Light;
        }
    }

    public static class Contrast
    {
        public static double Ratio(ThemeColor a, ThemeColor b)
        {
            double lighter = Math.Max(Luminance(a), Luminance(b));
            double darker = Math.Min(Luminance(a), Luminance(b));
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double Luminance(ThemeColor color)
        {
            double r = Channel(color.R);
            double g = Channel(color.G);
            double b = Channel(color.B);
            return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        }

        private static double Channel(byte value)
        {
            double c = value / 255d;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
