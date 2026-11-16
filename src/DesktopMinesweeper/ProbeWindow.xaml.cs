using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopMinesweeper.Desktop;

namespace DesktopMinesweeper;

/// <summary>
/// Phase 0 alignment probe: draws read-only outlines over the real desktop icons.
/// It never moves, renames, hides or modifies an icon.
/// </summary>
public partial class ProbeWindow : Window
{
    private static readonly Brush OutlineBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0xE5, 0xFF));
    private static readonly Brush FillBrush = new SolidColorBrush(Color.FromArgb(0x0D, 0x00, 0xE5, 0xFF));
    private static readonly Brush LabelBackground = new SolidColorBrush(Color.FromArgb(0xB4, 0x00, 0x00, 0x00));
    private static readonly Brush LabelForeground = new SolidColorBrush(Color.FromRgb(0xE0, 0xF7, 0xFA));
    private static readonly Brush MutedForeground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    private static readonly Brush WarningForeground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));

    private MonitorRect _monitor;
    private int _dpi = 96;

    public ProbeWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Closing += (_, _) => { /* nothing to restore: the real desktop was never modified */ };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;

        // Keep the probe out of Alt+Tab while still allowing it to take focus for Esc.
        var exStyle = NativeMethods.GetWindowLongPtrW(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        exStyle &= ~NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtrW(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

        _monitor = DesktopSnapshotReader.PrimaryMonitor;
        _dpi = (int)NativeMethods.GetDpiForWindow(handle);
        if (_dpi <= 0)
        {
            _dpi = (int)NativeMethods.GetDpiForSystem();
        }

        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            _monitor.Left,
            _monitor.Top,
            _monitor.Width,
            _monitor.Height,
            NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOOWNERZORDER);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        Render();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Render()
    {
        RootCanvas.Children.Clear();

        var snapshot = DesktopSnapshotReader.Read();
        var scale = 96.0 / _dpi;

        // LVM_GETITEMSPACING can differ slightly from the real pitch (93 vs 95 observed
        // at 125% DPI) and that error accumulates across columns. Prefer the measured
        // pitch between actual icon anchors; fall back to the reported spacing.
        var logicalWidth = DerivePitch(snapshot.Icons.Select(i => i.ScreenX), snapshot.SpacingX);
        var logicalHeight = DerivePitch(snapshot.Icons.Select(i => i.ScreenY), snapshot.SpacingY);
        var cellWidth = (logicalWidth > 0 ? logicalWidth : 75 * _dpi / 96) * scale;
        var cellHeight = (logicalHeight > 0 ? logicalHeight : 75 * _dpi / 96) * scale;

        foreach (var icon in snapshot.Icons)
        {
            var x = (icon.ScreenX - _monitor.Left) * scale;
            var y = (icon.ScreenY - _monitor.Top) * scale;

            var outline = new Rectangle
            {
                Width = cellWidth,
                Height = cellHeight,
                Stroke = OutlineBrush,
                StrokeThickness = 1,
                Fill = FillBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(outline, x);
            Canvas.SetTop(outline, y);
            RootCanvas.Children.Add(outline);

            // Exact LVM_GETITEMPOSITION anchor: should sit on the icon's upper-left corner.
            var anchor = new Rectangle
            {
                Width = 3,
                Height = 3,
                Fill = Brushes.Yellow,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(anchor, x);
            Canvas.SetTop(anchor, y);
            RootCanvas.Children.Add(anchor);

            var label = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(icon.Text) ? $"#{icon.Index}" : icon.Text,
                Width = Math.Max(1, cellWidth - 2),
                FontSize = 10.5,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                Foreground = LabelForeground,
                Background = LabelBackground,
                Padding = new Thickness(2, 0, 2, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + 1);
            Canvas.SetTop(label, y + cellHeight - 16);
            RootCanvas.Children.Add(label);
        }

        AddStatusPanel(snapshot);
    }

    private static int DerivePitch(IEnumerable<int> coordinates, int fallback)
    {
        var distinct = coordinates.Distinct().OrderBy(value => value).ToArray();
        if (distinct.Length < 2)
        {
            return fallback;
        }

        var gaps = new List<int>();
        for (var i = 1; i < distinct.Length; i++)
        {
            var gap = distinct[i] - distinct[i - 1];
            if (gap > 4)
            {
                gaps.Add(gap);
            }
        }

        if (gaps.Count == 0)
        {
            return fallback;
        }

        gaps.Sort();
        return gaps[gaps.Count / 2];
    }

    private void AddStatusPanel(DesktopSnapshot snapshot)
    {
        var panel = new StackPanel { Margin = new Thickness(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "Desktop Minesweeper · Phase 0 Probe",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"icons={snapshot.Count} read={snapshot.Icons.Count}  listview=0x{snapshot.ListViewHandle.ToInt64():X}",
            FontSize = 11,
            Foreground = LabelForeground,
            Margin = new Thickness(0, 4, 0, 0)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"dpi={_dpi}  spacing={snapshot.SpacingX}x{snapshot.SpacingY}  monitor={_monitor.Left},{_monitor.Top} {_monitor.Width}x{_monitor.Height}",
            FontSize = 11,
            Foreground = MutedForeground,
            Margin = new Thickness(0, 2, 0, 0)
        });

        if (!string.IsNullOrEmpty(snapshot.Warning))
        {
            panel.Children.Add(new TextBlock
            {
                Text = snapshot.Warning,
                FontSize = 11,
                Foreground = WarningForeground,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = "只读探针：不移动、不修改任何桌面图标 · Esc 退出",
            FontSize = 11,
            Foreground = MutedForeground,
            Margin = new Thickness(0, 6, 0, 0)
        });

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x10, 0x14, 0x18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0xE5, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = panel,
            IsHitTestVisible = false
        };

        border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = border.DesiredSize;

        Canvas.SetLeft(border, Math.Max(10, RootCanvas.ActualWidth - desired.Width - 18));
        Canvas.SetTop(border, Math.Max(10, RootCanvas.ActualHeight - desired.Height - 18));
        RootCanvas.Children.Add(border);
    }
}
