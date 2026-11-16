using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopMinesweeper.Desktop;

namespace DesktopMinesweeper.Diagnostics;

/// <summary>
/// Headless checks for the glass-thumbnail pipeline. Runs as part of --selftest.
/// </summary>
internal static class ImageProcessingSelfTest
{
    public static void Run(Action<string, bool, string?> check)
    {
        var source = CreateSquare(32, 12, 8);
        var blurred = DesktopBitmapCapture.BlurAndDarken(source, 6, 1.0);
        check("blur returns a bitmap", blurred is not null, null);
        if (blurred is null)
        {
            return;
        }

        var pixels = Read(blurred);
        var centre = Channel(pixels, blurred.PixelWidth, 16, 16);
        var corner = Channel(pixels, blurred.PixelWidth, 1, 1);
        check("blur softens the centre", centre is > 0 and < 255, centre.ToString());
        check("blur keeps distant pixels dark", corner < 12, corner.ToString());
        check("blur keeps alpha opaque", Alpha(pixels) == 255, Alpha(pixels).ToString());

        var dimmed = DesktopBitmapCapture.BlurAndDarken(source, 6, 0.5);
        var dimCentre = dimmed is null ? -1 : Channel(Read(dimmed), dimmed.PixelWidth, 16, 16);
        check(
            "dim halves brightness",
            dimCentre >= 0 && Math.Abs(dimCentre - centre / 2.0) <= 3,
            $"{centre} -> {dimCentre}");
        check("null input is safe", DesktopBitmapCapture.BlurAndDarken(null, 6, 0.5) is null, null);
    }

    private static BitmapSource CreateSquare(int size, int offset, int length)
    {
        var stride = size * 4;
        var pixels = new byte[stride * size];
        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
        }

        for (var y = offset; y < offset + length; y++)
        {
            for (var x = offset; x < offset + length; x++)
            {
                var index = y * stride + x * 4;
                pixels[index] = 255;
                pixels[index + 1] = 255;
                pixels[index + 2] = 255;
            }
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] Read(BitmapSource source)
    {
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static int Channel(byte[] pixels, int width, int x, int y)
    {
        return pixels[(y * width + x) * 4];
    }

    private static int Alpha(byte[] pixels)
    {
        return pixels[(16 * 32 + 16) * 4 + 3];
    }
}
