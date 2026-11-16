using System.Windows;

namespace DesktopMinesweeper.Safety;

/// <summary>
/// Every exit path funnels here. There is no desktop state to restore: the overlay
/// is an independent window and the real icons were only ever read.
/// </summary>
internal static class ExitGuard
{
    public static void Attach(Application app)
    {
        app.DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;

            // Request shutdown with a non-zero code before closing windows:
            // closing the last window would otherwise trigger OnLastWindowClose
            // and report a normal exit code.
            app.Shutdown(1);
            CloseAll(app);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, _) => CloseAll(app);
        app.Exit += (_, _) => CloseAll(app);
    }

    private static void CloseAll(Application app)
    {
        foreach (Window window in app.Windows)
        {
            try
            {
                window.Close();
            }
            catch
            {
                // A window that is already gone needs no cleanup.
            }
        }
    }
}
