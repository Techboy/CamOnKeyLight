using Microsoft.Win32;

namespace CamOnKeyLight
{
    internal static class WindowsTheme
    {
        public static bool? AppsUseLightTheme()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            object? value = key?.GetValue("AppsUseLightTheme");
            if (value is int number)
                return number != 0;

            return null;
        }
    }
}
