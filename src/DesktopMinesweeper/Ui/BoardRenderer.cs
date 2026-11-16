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
    private static readonly Brush ClearedScrim = new SolidColorBrush(Color.FromArgb(0x14, 0xE8, 0xFF, 0xFF));
    private static readonly Brush FallbackFill = new SolidColorBrush(Color.FromArgb(0x66, 0x10, 0x14, 0x18));
    private static readonly Brush HoverFallbackFill = new SolidColorBrush(Color.FromArgb(0x99, 0x10, 0x14, 0x18));
    private static readonly Brush ClearedFallback = new SolidColorBrush(Color.FromArgb(0x33, 0xE8, 0xFF, 0xFF));
    private static readonly Brush NormalBorder = new SolidColorBrush(Color.FromArgb(0x59, 0x00, 0xE5, 0xFF));
    private static readonly Brush HoverBorder = new SolidColorBrush(Color.FromArgb(0xE6, 0x00, 0xE5, 0xFF));
    private static readonly Brush ClearedBorder = new SolidColorBrush(Color.FromArgb(0x80, 0x69, 0xF0, 0xAE));
    private static readonly Brush MineScrim = new SolidColorBrush(Color.FromArgb(0xB4, 0x7A, 0x1F, 0x1F));
    private static readonly Brush ExplodedScrim = new SolidColorBrush(Color.FromArgb(0xE0, 0xC6, 0x28, 0x28));
    private static readonly Brush ExplodedBorder = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x52, 0x52));
    private static readonly Brush GlyphDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x08, 0x0B, 0x0E));
    private static readonly Brush GlyphLight = new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xF6, 0xF8));
    private static readonly Brush FlagBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xC2, 0x66));
    private static readonly Brush BadgeFill = new SolidColorBrush(Color.FromArgb(0xE0, 0x08, 0x0B, 0x0E));
    private static readonly Brush LabelFill = new SolidColorBrush(Color.FromArgb(0xB4, 0x00, 0x00, 0x00));
    private static readonly Brush LabelText = new SolidColorBrush(Color.FromRgb(0xE0, 0xF7, 0xFA));

    private static readonly Brush[] NumberBrushes =
    {
        Brushes.White,
        new SolidColorBrush(Color.FromRgb(0x6F, 0xB7, 0xFF)),
        new SolidColorBrush(Color.FromRgb(0x7B, 0xE3, 0x8A)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0x7B, 0x7B)),
        new SolidColorBrush(Color.FromRgb(0xC3, 0x9B, 0xFF)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0xC2, 0x66)),
        new SolidColorBrush(Color.FromRgb(0x66, 0xE0, 0xD6)),
        new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
        new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD))
    };

    public static void Render(
        Canvas canvas,
        MinesweeperGame game,
        Action<GameCell> onReveal,
        Action<GameCell> onFlag)
    {
        canvas.Children.Clear();

        foreach (var cell in game.Cells)
        {
            var visual = cell.Visual;
            var interactive = !game.IsFinished && !cell.IsRevealed;

            var tile = new Border
            {
                Width = visual.Width,
                Height = visual.Height,
                CornerRadius = new CornerRadius(6),
                Background = FallbackFill,
                BorderBrush = NormalBorder,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = interactive ? Cursors.Hand : Cursors.Arrow,
                SnapsToDevicePixels = true
            };

            var content = new Grid();

            if (visual.Thumbnail is BitmapSource thumbnail)
            {
                content.Children.Add(new Image
                {
                    Source = thumbnail,
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true
                });
            }

            var scrim = new Rectangle { IsHitTestVisible = false };
            content.Children.Add(scrim);

            var baseScrim = ScrimFill;
            var baseBorder = NormalBorder;
            var baseBackground = FallbackFill;

            if (cell.IsRevealed && cell.IsMine)
            {
                baseScrim = cell.Exploded ? ExplodedScrim : MineScrim;
                baseBorder = cell.Exploded ? ExplodedBorder : NormalBorder;
                content.Children.Add(CreateMineGlyph());
            }
            else if (cell.IsRevealed)
            {
                baseScrim = ClearedScrim;
                baseBorder = ClearedBorder;
                baseBackground = ClearedFallback;
                if (cell.AdjacentMines > 0)
                {
                    content.Children.Add(CreateNumberBadge(cell.AdjacentMines));
                }
            }
            else if (cell.IsFlagged)
            {
                content.Children.Add(CreateFlagGlyph());
            }

            if (visual.Thumbnail is null && !string.IsNullOrWhiteSpace(visual.Text))
            {
                content.Children.Add(new TextBlock
                {
                    Text = visual.Text,
                    FontSize = 10.5,
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                    Foreground = LabelText,
                    Background = LabelFill,
                    Padding = new Thickness(2, 0, 2, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    IsHitTestVisible = false
                });
            }

            scrim.Fill = baseScrim;
            tile.BorderBrush = baseBorder;
            tile.Background = baseBackground;
            tile.Child = content;

            if (interactive)
            {
                tile.MouseEnter += (_, _) =>
                {
                    tile.BorderBrush = HoverBorder;
                    if (visual.Thumbnail is not null)
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
                    tile.BorderBrush = baseBorder;
                    scrim.Fill = baseScrim;
                    tile.Background = baseBackground;
                };
                tile.MouseLeftButtonDown += (_, args) =>
                {
                    if (!cell.IsFlagged)
                    {
                        onReveal(cell);
                    }
                    args.Handled = true;
                };
                tile.MouseRightButtonDown += (_, args) =>
                {
                    onFlag(cell);
                    args.Handled = true;
                };
            }

            Canvas.SetLeft(tile, visual.X);
            Canvas.SetTop(tile, visual.Y);
            canvas.Children.Add(tile);
        }
    }

    private static Border CreateNumberBadge(int number)
    {
        var text = new TextBlock
        {
            Text = number.ToString(),
            FontFamily = new FontFamily("Consolas, Cascadia Mono"),
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = NumberBrushes[Math.Clamp(number, 0, NumberBrushes.Length - 1)],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        return new Border
        {
            Child = text,
            Background = BadgeFill,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Width = 24,
            Height = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
    }

    private static Canvas CreateFlagGlyph()
    {
        var canvas = new Canvas
        {
            Width = 22,
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        var pole = new Rectangle
        {
            Width = 3,
            Height = 20,
            Fill = FlagBrush,
            RadiusX = 1.5,
            RadiusY = 1.5
        };
        Canvas.SetLeft(pole, 7);
        Canvas.SetTop(pole, 4);
        canvas.Children.Add(pole);

        var cloth = new Polygon
        {
            Fill = FlagBrush,
            Points = new PointCollection
            {
                new Point(9, 4),
                new Point(21, 8),
                new Point(9, 13)
            }
        };
        canvas.Children.Add(cloth);

        var foot = new Rectangle
        {
            Width = 12,
            Height = 3,
            Fill = FlagBrush,
            RadiusX = 1.5,
            RadiusY = 1.5
        };
        Canvas.SetLeft(foot, 3);
        Canvas.SetTop(foot, 23);
        canvas.Children.Add(foot);

        return canvas;
    }

    private static Canvas CreateMineGlyph()
    {
        var canvas = new Canvas
        {
            Width = 24,
            Height = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        var centre = new Point(12, 12);
        for (var i = 0; i < 4; i++)
        {
            var angle = Math.PI * i / 4;
            var dx = Math.Cos(angle) * 10;
            var dy = Math.Sin(angle) * 10;
            canvas.Children.Add(new Line
            {
                X1 = centre.X - dx,
                Y1 = centre.Y - dy,
                X2 = centre.X + dx,
                Y2 = centre.Y + dy,
                Stroke = GlyphLight,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        var body = new Ellipse
        {
            Width = 13,
            Height = 13,
            Fill = GlyphDark,
            Stroke = GlyphLight,
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(body, 5.5);
        Canvas.SetTop(body, 5.5);
        canvas.Children.Add(body);

        return canvas;
    }
}
