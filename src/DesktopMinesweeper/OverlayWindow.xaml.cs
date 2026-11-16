using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopMinesweeper.Desktop;

namespace DesktopMinesweeper;

/// <summary>
/// Borderless, per-monitor DPI aware overlay. Phase 1 draws no board yet; it only
/// proves that a transparent topmost window can sit above the live desktop and
/// disappear cleanly. The desktop itself is never modified.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private MonitorRect _workArea;
    private int _dpi = 96;
    private bool _probeRequested;

    public OverlayWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => UpdateTimerText();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        KeyDown += OnKeyDown;
    }

    public bool ProbeRequested
    {
        get => _probeRequested;
        set => _probeRequested = value;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;

        var exStyle = NativeMethods.GetWindowLongPtrW(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        exStyle &= ~NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtrW(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

        _workArea = DesktopSnapshotReader.PrimaryWorkArea;
        _dpi = (int)NativeMethods.GetDpiForWindow(handle);
        if (_dpi <= 0)
        {
            _dpi = (int)NativeMethods.GetDpiForSystem();
        }

        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            _workArea.Left,
            _workArea.Top,
            _workArea.Width,
            _workArea.Height,
            NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOOWNERZORDER);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();

        _clock.Restart();
        UpdateTimerText();
        _timer.Start();

        if (_probeRequested)
        {
            var snapshot = DesktopSnapshotReader.Read();
            ProbeLayer.Render(ProbeCanvas, snapshot, _workArea, _dpi);
            MinesText.Text = snapshot.Count.ToString();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _clock.Stop();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.R)
        {
            e.Handled = true;
            Restart();
        }
    }

    private void OnRestartClick(object sender, RoutedEventArgs e) => Restart();

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void Restart()
    {
        // Phase 1: reset the clock. Phase 3 will rebuild the board here.
        _clock.Restart();
        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        var elapsed = _clock.Elapsed;
        TimerText.Text = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
