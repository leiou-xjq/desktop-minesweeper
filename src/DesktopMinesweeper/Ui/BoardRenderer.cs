using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DesktopMinesweeper.Game;

namespace DesktopMinesweeper.Ui;

/// <summary>
/// Draws the board on the transparent overlay. Every visual here is a copy; the
/// real desktop icons underneath are never touched.
/// </summary>
internal static class BoardRenderer
{
    private static readonly Brush ScrimFill = new SolidColorBrush(Color.FromArgb(0x8C, 0x08, 0x0B, 0x0E));
    private static readonly Brush HoverScrimFill = new SolidColorBrush(Color.FromArgb(0x4D, 0x08, 0x0B, 0x0E));
    private static readonly Brush FallbackFill = new SolidColorBrush(Color.FromArgb(0x66, 0x10, 0x14, 0x18));
    private static readonly Brush HoverFallbackFill = new SolidColorBrush(Color.FromArgb(0x99, 0x10, 0x14, 0x18));
    private static readonly Brush NormalBorder = new SolidColorBrush(Color.FromArgb(0x59, 0x00, 0xE5, 0xFF));
    private static readonly Brush HoverBorder = new SolidColorBrush(Color.FromArgb(0xE6, 0x00, 0xE5, 0xFF));
    private static readonly Brush LabelFill = new SolidColorBrush(Color.FromArgb(0xB4, 0x00, 0x00, 0x00));
    private static readonly Brush LabelText = new SolidColorBrush(Color.FromRgb(0xE0, 0xF7, 0xFA));

    public static void Render(Canvas canvas, BoardGrid grid, Action onCellPressed)
    {
        canvas.Children.Clear();

        foreach (var cell in grid.Cells)
        {
            var tile = new Border
            {
                Width = cell.Width,
                Height = cell.Height,
                CornerRadius = new CornerRadius(6),
                Background = FallbackFill,
                BorderBrush = NormalBorder,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true
            };

            var content = new Grid();

            if (cell.Thumbnail is BitmapSource thumbnail)
            {
                content.Children.Add(new Image
                {
                    Source = thumbnail,
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true
                });
            }

            var scrim = new Rectangle
            {
                Fill = cell.Thumbnail is null ? Brushes.Transparent : ScrimFill,
                IsHitTestVisible = false
            };
            content.Children.Add(scrim);

            if (cell.Thumbnail is null && !string.IsNullOrWhiteSpace(cell.Text))
            {
                var label = new TextBlock
                {
                    Text = cell.Text,
                    FontSize = 10.5,
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                    Foreground = LabelText,
                    Background = LabelFill,
                    Padding = new Thickness(2, 0, 2, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    IsHitTestVisible = false
                };
                content.Children.Add(label);
            }

            tile.Child = content;

            tile.MouseEnter += (_, _) =>
            {
                tile.BorderBrush = HoverBorder;
                if (cell.Thumbnail is not null)
                {
                    scrim.Fill = HoverScrimFill;
                }
                else
                {
                    tile.Background = HoverFallbackFill;
                }
            };
            tile.MouseLeave += (_, _) =>
            {
                tile.BorderBrush = NormalBorder;
                if (cell.Thumbnail is not null)
                {
                    scrim.Fill = ScrimFill;
                }
                else
                {
                    tile.Background = FallbackFill;
                }
            };
            tile.MouseLeftButtonDown += (_, args) =>
            {
                onCellPressed();
                args.Handled = true;
            };

            Canvas.SetLeft(tile, cell.X);
            Canvas.SetTop(tile, cell.Y);
            canvas.Children.Add(tile);
        }
    }
}
