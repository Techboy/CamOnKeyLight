using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CamOnKeyLight
{
    internal static class WindowCapture
    {
        public static void Save(Window window, string path)
        {
            window.UpdateLayout();
            double width = window.ActualWidth;
            double height = window.ActualHeight;
            if (width < 2 || height < 2)
                throw new InvalidOperationException("The window has no size.");

            int pixelsWide = Math.Max(1, (int)Math.Round(width));
            int pixelsHigh = Math.Max(1, (int)Math.Round(height));
            RenderTargetBitmap bitmap = new RenderTargetBitmap(pixelsWide, pixelsHigh, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
