using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopMinesweeper.Desktop;
using DesktopMinesweeper.Game;
using DesktopMinesweeper.Ui;

namespace DesktopMinesweeper;

/// <summary>
/// Borderless, per-monitor DPI aware overlay. The board is a projection of the
/// desktop icon slots; the live desktop is never modified. All game state lives
/// in <see cref="MinesweeperGame"/> and is drawn only on this transparent window.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private MonitorRect _workArea;
    private int _dpi = 96;
    private bool _probeRequested;
    private MinesweeperGame? _game;

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

    public bool FixedGrid { get; set; }

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

        PrepareBoard();
    }

    /// <summary>
    /// Reads a fresh desktop snapshot and builds a new game. Re-reading on restart
    /// lets the board follow icon changes without ever writing to the desktop.
    /// </summary>
    private void PrepareBoard()
    {
        if (_game is not null)
        {
            _game.Changed -= OnGameChanged;
            _game = null;
        }

        _clock.Reset();
        _timer.Stop();
        UpdateTimerText();

        var snapshot = DesktopSnapshotReader.Read();

        if (_probeRequested)
        {
            ProbeLayer.Render(ProbeCanvas, snapshot, _workArea, _dpi);
            GridText.Text = snapshot.Count.ToString();
            ShowNotice(snapshot.Warning);
            return;
        }

        var grid = BoardGridBuilder.Build(snapshot, _workArea, _dpi, FixedGrid);
        _game = new MinesweeperGame(grid);
        _game.Changed += OnGameChanged;

        ShowNotice(_game.Notice ?? snapshot.Warning);
        RenderBoard();
    }

    private void OnGameChanged()
    {
        if (_game is null)
        {
            return;
        }

        if (_game.Status == GameStatus.Playing && !_clock.IsRunning)
        {
            _clock.Restart();
            _timer.Start();
        }
        else if (_game.IsFinished)
        {
            _clock.Stop();
            _timer.Stop();
        }

        RenderBoard();
    }

    private void RenderBoard()
    {
        if (_game is null)
        {
            return;
        }

        GridText.Text = _game.Cells.Count.ToString();
        MinesText.Text = _game.MinesRemaining.ToString();

        switch (_game.Status)
        {
            case GameStatus.Won:
                StateText.Text = "全部排除 · 胜利！按 R 重开";
                StateText.Foreground = new SolidColorBrush(Color.FromRgb(0x69, 0xF0, 0xAE));
                break;
            case GameStatus.Lost:
                StateText.Text = "踩到雷了 · 按 R 重开";
                StateText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
                break;
            default:
                StateText.Text = "左键翻开 · 右键插旗";
                StateText.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5));
                break;
        }

        BoardRenderer.Render(
            BoardCanvas,
            _game,
            cell =>
            {
                FocusOverlay();
                _game.Reveal(cell);
            },
            cell =>
            {
                FocusOverlay();
                _game.ToggleFlag(cell);
            });
    }

    private void FocusOverlay()
    {
        Activate();
        Focus();
    }

    /// <summary>
    /// Developer-only hook for <c>--capture --demo</c>: plays a few moves so the
    /// screenshot can show revealed numbers, flags and (optionally) a lost board.
    /// </summary>
    internal void PrepareCaptureDemo(bool lose)
    {
        if (_game is null)
        {
            return;
        }

        var cells = _game.Cells;
        _game.Reveal(cells[cells.Count / 2]);

        if (lose)
        {
            var mine = cells.FirstOrDefault(cell => cell.IsMine);
            if (mine is not null)
            {
                _game.Reveal(mine);
            }

            return;
        }

        foreach (var cell in cells.Where(cell => !cell.IsMine && !cell.IsRevealed).Take(4))
        {
            _game.Reveal(cell);
        }

        var flagTarget = cells.FirstOrDefault(cell => !cell.IsMine && !cell.IsRevealed);
        if (flagTarget is not null)
        {
            _game.ToggleFlag(flagTarget);
        }
    }

    private void ShowNotice(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            NoticeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        NoticeText.Text = message;
        NoticeBadge.Visibility = Visibility.Visible;
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
        PrepareBoard();
        FocusOverlay();
    }

    private void UpdateTimerText()
    {
        var elapsed = _clock.Elapsed;
        TimerText.Text = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
