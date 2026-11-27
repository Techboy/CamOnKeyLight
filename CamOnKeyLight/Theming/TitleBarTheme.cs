using System.Runtime.InteropServices;

namespace CamOnKeyLight
{
    internal static class TitleBarTheme
    {
        public static void Apply(IntPtr handle, bool dark)
        {
            if (handle == IntPtr.Zero)
                return;

            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, 20, ref value, sizeof(int));
            DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
            int corners = 2;
            DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
    }
}
