using System.IO;
using System.Text;
using System.Windows;
using DesktopMinesweeper.Desktop;
using DesktopMinesweeper.Safety;

namespace DesktopMinesweeper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ExitGuard.Attach(this);

        // Diagnostic mode used by the phase-0 acceptance check. It only reads icon
        // metadata and writes a plain-text report to the path the caller supplied.
        if (e.Args.Length >= 2 && e.Args[0] == "--dump")
        {
            WriteDiagnostics(e.Args[1]);
            Shutdown(0);
            return;
        }

        var probe = e.Args.Any(arg => string.Equals(arg, "--probe", StringComparison.OrdinalIgnoreCase));
        var window = new OverlayWindow { ProbeRequested = probe };
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
