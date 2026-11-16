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

        // Automated exit-path checks: show the real overlay, then close it through the
        // same code path as the Exit button / Esc.
        if (e.Args.Length >= 2 && e.Args[0] == "--auto-exit" && int.TryParse(e.Args[1], out var exitMs))
        {
            var exitWindow = new OverlayWindow();
            exitWindow.Show();
            var exitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(200, exitMs)) };
            exitTimer.Tick += (_, _) =>
            {
                exitTimer.Stop();
                exitWindow.Close();
                Shutdown(0);
            };
            exitTimer.Start();
            return;
        }

        // Automated exit-path checks: raise an unhandled dispatcher exception so
        // ExitGuard has to remove the overlay.
        if (e.Args.Length >= 2 && e.Args[0] == "--crash" && int.TryParse(e.Args[1], out var crashMs))
        {
            var crashWindow = new OverlayWindow();
            crashWindow.Show();
            var crashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(200, crashMs)) };
            crashTimer.Tick += (_, _) =>
            {
                crashTimer.Stop();
                throw new InvalidOperationException("Diagnostic crash requested by --crash.");
            };
            crashTimer.Start();
            return;
        }

        // Headless rules check; no window is created.
        if (e.Args.Length >= 2 && e.Args[0] == "--selftest")
        {
            Shutdown(GameSelfTest.Run(e.Args[1]));
            return;
        }

        // Diagnostic mode used by the phase-0 acceptance check. It only reads icon
        // metadata and writes a plain-text report to the path the caller supplied.
        if (e.Args.Length >= 2 && e.Args[0] == "--dump")
        {
            WriteDiagnostics(e.Args[1]);
            Shutdown(0);
            return;
        }

        // Developer-only: render the overlay, optionally play demo moves, capture the
        // physical screen from this DPI-aware process, then exit.
        if (e.Args.Length >= 2 && e.Args[0] == "--capture")
        {
            var captureWindow = new OverlayWindow
            {
                FixedGrid = e.Args.Any(arg => string.Equals(arg, "--fixed-grid", StringComparison.OrdinalIgnoreCase))
            };
            var demo = e.Args.Any(arg => string.Equals(arg, "--demo", StringComparison.OrdinalIgnoreCase));
            var demoLose = e.Args.Any(arg => string.Equals(arg, "--demo-lose", StringComparison.OrdinalIgnoreCase));

            captureWindow.Show();
            var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            settle.Tick += (_, _) =>
            {
                settle.Stop();

                if (demo || demoLose)
                {
                    captureWindow.PrepareCaptureDemo(demoLose);
                }

                var shot = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                shot.Tick += (_, _) =>
                {
                    shot.Stop();
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
                shot.Start();
            };
            settle.Start();
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
