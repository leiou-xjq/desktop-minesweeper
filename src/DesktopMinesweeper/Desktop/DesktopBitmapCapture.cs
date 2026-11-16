using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopMinesweeper.Desktop;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DesktopMinesweeper.Desktop;

/// <summary>
/// Captures the desktop list view itself with PrintWindow. Unlike a screen grab,
/// this renders the wallpaper and icons without any overlapping application windows.
/// Strictly read-only.
/// </summary>
internal static class DesktopBitmapCapture
{
    private const uint PwRenderFullContent = 0x00000002;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    public static BitmapSource? Capture(IntPtr listView, PixelRect bounds)
    {
        if (listView == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        try
        {
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, GdiPixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            bool ok;
            try
            {
                ok = PrintWindow(listView, hdc, PwRenderFullContent);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }

            if (!ok)
            {
                return null;
            }

            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                GdiPixelFormat.Format32bppArgb);

            try
            {
                var source = BitmapSource.Create(
                    bitmap.Width,
                    bitmap.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    data.Scan0,
                    data.Stride * bitmap.Height,
                    data.Stride);
                source.Freeze();
                return source;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? Crop(BitmapSource? source, PixelRect rect)
    {
        if (source is null)
        {
            return null;
        }

        var x = Math.Clamp(rect.Left, 0, source.PixelWidth);
        var y = Math.Clamp(rect.Top, 0, source.PixelHeight);
        var width = Math.Min(rect.Width, source.PixelWidth - x);
        var height = Math.Min(rect.Height, source.PixelHeight - y);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var cropped = new CroppedBitmap(source, new System.Windows.Int32Rect(x, y, width, height));
        cropped.Freeze();
        return cropped;
    }
}
