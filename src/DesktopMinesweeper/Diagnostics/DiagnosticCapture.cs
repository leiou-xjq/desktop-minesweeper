using System.Drawing;
using System.Drawing.Imaging;
using DesktopMinesweeper.Desktop;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DesktopMinesweeper.Diagnostics;

/// <summary>
/// Developer-only screenshot helper. It runs inside the Per-Monitor V2 app process,
/// so captured pixels match the overlay's real physical coordinates.
/// </summary>
internal static class DiagnosticCapture
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    public static void CaptureScreen(string path, MonitorRect bounds)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, GdiPixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size);
        bitmap.Save(path, ImageFormat.Png);
    }
}
