using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopMinesweeper.Desktop;

namespace DesktopMinesweeper;

/// <summary>
/// Diagnostic overlay used by <c>--probe</c> and the phase 0 acceptance check.
/// It only draws outlines and labels; it never touches real desktop icons.
/// </summary>
internal static class ProbeLayer
{
    private static readonly Brush OutlineBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0xE5, 0xFF));
    private static readonly Brush FillBrush = new SolidColorBrush(Color.FromArgb(0x0D, 0x00, 0xE5, 0xFF));
    private static readonly Brush LabelBackground = new SolidColorBrush(Color.FromArgb(0xB4, 0x00, 0x00, 0x00));
    private static readonly Brush LabelForeground = new SolidColorBrush(Color.FromRgb(0xE0, 0xF7, 0xFA));

    public static void Render(Canvas canvas, DesktopSnapshot snapshot, MonitorRect workArea, int dpi)
    {
        canvas.Children.Clear();

        var scale = 96.0 / dpi;

        // LVM_GETITEMSPACING can differ slightly from the real pitch (93 vs 95 observed
        // at 125% DPI) and that error accumulates across columns. Prefer the measured
        // pitch between actual icon anchors; fall back to the reported spacing.
        var logicalWidth = DerivePitch(snapshot.Icons.Select(i => i.ScreenX), snapshot.SpacingX);
        var logicalHeight = DerivePitch(snapshot.Icons.Select(i => i.ScreenY), snapshot.SpacingY);
        var cellWidth = (logicalWidth > 0 ? logicalWidth : 75 * dpi / 96) * scale;
        var cellHeight = (logicalHeight > 0 ? logicalHeight : 75 * dpi / 96) * scale;

        foreach (var icon in snapshot.Icons)
        {
            var x = (icon.ScreenX - workArea.Left) * scale;
            var y = (icon.ScreenY - workArea.Top) * scale;

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
            canvas.Children.Add(outline);

            var anchor = new Rectangle
            {
                Width = 3,
                Height = 3,
                Fill = Brushes.Yellow,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(anchor, x);
            Canvas.SetTop(anchor, y);
            canvas.Children.Add(anchor);

            var label = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(icon.Text) ? $"#{icon.Index}" : icon.Text,
                Width = Math.Max(1, cellWidth - 2),
                FontSize = 10.5,
                Foreground = LabelForeground,
                Background = LabelBackground,
                Padding = new Thickness(2, 0, 2, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + 1);
            Canvas.SetTop(label, y + cellHeight - 16);
            canvas.Children.Add(label);
        }

        AddInfoPanel(canvas, snapshot, workArea, dpi);
    }

    private static void AddInfoPanel(Canvas canvas, DesktopSnapshot snapshot, MonitorRect workArea, int dpi)
    {
        var panel = new StackPanel { Margin = new Thickness(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "Phase 0 Probe · 只读对齐检查",
            FontSize = 13,
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
            Text = $"dpi={dpi}  spacing={snapshot.SpacingX}x{snapshot.SpacingY}  work area={workArea.Left},{workArea.Top} {workArea.Width}x{workArea.Height}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)),
            Margin = new Thickness(0, 2, 0, 0)
        });

        if (!string.IsNullOrEmpty(snapshot.Warning))
        {
            panel.Children.Add(new TextBlock
            {
                Text = snapshot.Warning,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

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

        Canvas.SetLeft(border, Math.Max(10, canvas.ActualWidth - desired.Width - 18));
        Canvas.SetTop(border, Math.Max(10, canvas.ActualHeight - desired.Height - 90));
        canvas.Children.Add(border);
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
}
