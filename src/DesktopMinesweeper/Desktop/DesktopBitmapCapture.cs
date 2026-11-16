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

    /// <summary>
    /// Produces the frosted-glass backdrop for a covered cell: two separable box
    /// passes (a cheap Gaussian approximation), a brightness factor and opaque alpha.
    /// Runs once while the board is built; the returned bitmap is frozen.
    /// </summary>
    public static BitmapSource? BlurAndDarken(BitmapSource? source, int radius, double dim)
    {
        if (source is null || source.PixelWidth < 2 || source.PixelHeight < 2)
        {
            return null;
        }

        try
        {
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            var effectiveRadius = Math.Clamp(radius, 1, Math.Max(1, Math.Min(width, height) / 3));
            BoxBlurHorizontal(pixels, width, height, effectiveRadius);
            BoxBlurVertical(pixels, width, height, effectiveRadius);

            var factor = Math.Clamp(dim, 0.0, 1.0);
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = (byte)(pixels[i] * factor);
                pixels[i + 1] = (byte)(pixels[i + 1] * factor);
                pixels[i + 2] = (byte)(pixels[i + 2] * factor);
            }

            var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static void BoxBlurHorizontal(byte[] pixels, int width, int height, int radius)
    {
        var temp = new byte[pixels.Length];
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            int sumB = 0, sumG = 0, sumR = 0;
            var left = 0;
            var right = Math.Min(radius, width - 1);
            for (var x = left; x <= right; x++)
            {
                var i = row + x * 4;
                sumB += pixels[i];
                sumG += pixels[i + 1];
                sumR += pixels[i + 2];
            }

            for (var x = 0; x < width; x++)
            {
                var count = right - left + 1;
                var o = row + x * 4;
                temp[o] = (byte)(sumB / count);
                temp[o + 1] = (byte)(sumG / count);
                temp[o + 2] = (byte)(sumR / count);
                temp[o + 3] = 255;

                var remove = x - radius;
                if (remove >= 0)
                {
                    var i = row + remove * 4;
                    sumB -= pixels[i];
                    sumG -= pixels[i + 1];
                    sumR -= pixels[i + 2];
                    left++;
                }

                var add = x + radius + 1;
                if (add < width)
                {
                    var i = row + add * 4;
                    sumB += pixels[i];
                    sumG += pixels[i + 1];
                    sumR += pixels[i + 2];
                    right++;
                }
            }
        }

        Buffer.BlockCopy(temp, 0, pixels, 0, pixels.Length);
    }

    private static void BoxBlurVertical(byte[] pixels, int width, int height, int radius)
    {
        var temp = new byte[pixels.Length];
        for (var x = 0; x < width; x++)
        {
            int sumB = 0, sumG = 0, sumR = 0;
            var top = 0;
            var bottom = Math.Min(radius, height - 1);
            for (var y = top; y <= bottom; y++)
            {
                var i = (y * width + x) * 4;
                sumB += pixels[i];
                sumG += pixels[i + 1];
                sumR += pixels[i + 2];
            }

            for (var y = 0; y < height; y++)
            {
                var count = bottom - top + 1;
                var o = (y * width + x) * 4;
                temp[o] = (byte)(sumB / count);
                temp[o + 1] = (byte)(sumG / count);
                temp[o + 2] = (byte)(sumR / count);
                temp[o + 3] = 255;

                var remove = y - radius;
                if (remove >= 0)
                {
                    var i = (remove * width + x) * 4;
                    sumB -= pixels[i];
                    sumG -= pixels[i + 1];
                    sumR -= pixels[i + 2];
                    top++;
                }

                var add = y + radius + 1;
                if (add < height)
                {
                    var i = (add * width + x) * 4;
                    sumB += pixels[i];
                    sumG += pixels[i + 1];
                    sumR += pixels[i + 2];
                    bottom++;
                }
            }
        }

        Buffer.BlockCopy(temp, 0, pixels, 0, pixels.Length);
    }
}
