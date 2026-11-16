using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using DesktopMinesweeper.Desktop;
using DesktopMinesweeper.Diagnostics;
using DesktopMinesweeper.Safety;

namespace DesktopMinesweeper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The manifest requests PerMonitorV2, but some Windows 10 configurations and
        // host processes can leave the process system-aware. Set it explicitly before
        // any window exists so physical/DIP mapping is correct at 125% and above.
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        ExitGuard.Attach(this);

        // Diagnostic mode used by the phase-0 acceptance check. It only reads icon
        // metadata and writes a plain-text report to the path the caller supplied.
        if (e.Args.Length >= 2 && e.Args[0] == "--dump")
        {
            WriteDiagnostics(e.Args[1]);
            Shutdown(0);
            return;
        }

        // Developer-only: render the overlay, wait for it to settle, capture the
        // physical screen from this DPI-aware process, then exit.
        if (e.Args.Length >= 2 && e.Args[0] == "--capture")
        {
            var captureWindow = new OverlayWindow
            {
                FixedGrid = e.Args.Any(arg => string.Equals(arg, "--fixed-grid", StringComparison.OrdinalIgnoreCase))
            };
            captureWindow.Show();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    DiagnosticCapture.CaptureScreen(e.Args[1], DesktopSnapshotReader.PrimaryMonitor);
                }
                finally
                {
                    captureWindow.Close();
                    Shutdown(0);
                }
            };
            timer.Start();
            return;
        }

        var probe = e.Args.Any(arg => string.Equals(arg, "--probe", StringComparison.OrdinalIgnoreCase));
        var fixedGrid = e.Args.Any(arg => string.Equals(arg, "--fixed-grid", StringComparison.OrdinalIgnoreCase));
        var window = new OverlayWindow { ProbeRequested = probe, FixedGrid = fixedGrid };
        window.Show();
    }

    private static void WriteDiagnostics(string path)
    {
        var snapshot = DesktopSnapshotReader.Read();
        var monitor = DesktopSnapshotReader.PrimaryMonitor;
        var dpi = DesktopSnapshotReader.SystemDpi;

        var sb = new StringBuilder();
        sb.AppendLine($"listview=0x{snapshot.ListViewHandle.ToInt64():X}");
        sb.AppendLine($"count={snapshot.Count}");
        sb.AppendLine($"listview_rect={snapshot.ListViewRect.Left},{snapshot.ListViewRect.Top},{snapshot.ListViewRect.Right},{snapshot.ListViewRect.Bottom}");
        sb.AppendLine($"spacing={snapshot.SpacingX}x{snapshot.SpacingY}");
        sb.AppendLine($"monitor={monitor.Left},{monitor.Top},{monitor.Width}x{monitor.Height}");
        sb.AppendLine($"dpi={dpi}");
        sb.AppendLine($"warning={snapshot.Warning}");
        foreach (var icon in snapshot.Icons)
        {
            sb.AppendLine($"icon[{icon.Index}] client={icon.ClientX},{icon.ClientY} screen={icon.ScreenX},{icon.ScreenY} text={icon.Text}");
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
