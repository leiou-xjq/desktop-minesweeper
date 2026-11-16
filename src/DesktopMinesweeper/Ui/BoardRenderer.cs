using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DesktopMinesweeper.Game;

namespace DesktopMinesweeper.Ui;

/// <summary>
/// Draws the glass board on the transparent overlay. Everything here is a copy of
/// the desktop captured at startup; real icons are never touched.
/// </summary>
internal static class BoardRenderer
{
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");
    private static readonly FontFamily NumberFont = new("Cascadia Mono, Consolas");

    private static readonly Brush GlassOverlay = Gradient(
        Color.FromArgb(0x66, 0x0C, 0x14, 0x1B),
        Color.FromArgb(0xA6, 0x08, 0x0D, 0x12));
    private static readonly Brush FallbackGlass = Gradient(
        Color.FromArgb(0x59, 0x10, 0x1A, 0x22),
        Color.FromArgb(0x99, 0x08, 0x0D, 0x12));
    private static readonly Brush RevealedWash = Brush(0x1A, 0xF2, 0xFF, 0xFF);
    private static readonly Brush HoverSheen = Gradient(
        Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF),
        Color.FromArgb(0x05, 0xFF, 0xFF, 0xFF));
    private static readonly Brush NormalBorder = Brush(0x33, 0xFF, 0xFF, 0xFF);
    private static readonly Brush HoverBorder = Brush(0x66, 0xE5, 0xFF, 0xF7);
    private static readonly Brush ClearedBorder = Brush(0x4D, 0x5E, 0xEA, 0xD4);
    private static readonly Brush TopHighlight = Brush(0x4D, 0xFF, 0xFF, 0xFF);
    private static readonly Brush BottomShadow = Brush(0x40, 0x00, 0x00, 0x00);
    private static readonly Brush BadgeFill = Brush(0xD9, 0x0A, 0x10, 0x15);
    private static readonly Brush BadgeBorder = Brush(0x40, 0xFF, 0xFF, 0xFF);
    private static readonly Brush FlagBrush = Brush(0xFF, 0xFB, 0xBF, 0x24);
    private static readonly Brush FlagBadgeBorder = Brush(0x66, 0xFB, 0xBF, 0x24);
    private static readonly Brush MineScrim = Brush(0xA6, 0x7A, 0x1F, 0x1F);
    private static readonly Brush ExplodedScrim = Brush(0xE0, 0xC6, 0x28, 0x28);
    private static readonly Brush ExplodedBorder = Brush(0xFF, 0xFF, 0x52, 0x52);
    private static readonly Brush GlyphDark = Brush(0xFF, 0x08, 0x0B, 0x0E);
    private static readonly Brush GlyphLight = Brush(0xFF, 0xE8, 0xF6, 0xF8);
    private static readonly Brush LabelFill = Brush(0xB4, 0x00, 0x00, 0x00);
    private static readonly Brush LabelText = Brush(0xFF, 0xE0, 0xF7, 0xFA);

    private static readonly Brush[] NumberBrushes =
    {
        Brushes.White,
        Brush(0xFF, 0x6F, 0xB7, 0xFF),
        Brush(0xFF, 0x7B, 0xE3, 0x8A),
        Brush(0xFF, 0xFF, 0x7B, 0x7B),
        Brush(0xFF, 0xC3, 0x9B, 0xFF),
        Brush(0xFF, 0xFF, 0xC2, 0x66),
        Brush(0xFF, 0x66, 0xE0, 0xD6),
        Brush(0xFF, 0xE0, 0xE0, 0xE0),
        Brush(0xFF, 0xBD, 0xBD, 0xBD)
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
            var revealedMine = cell.IsRevealed && cell.IsMine;

            var tile = new Border
            {
                Width = visual.Width,
                Height = visual.Height,
                CornerRadius = new CornerRadius(10),
                Background = Brushes.Transparent,
                BorderBrush = revealedMine && cell.Exploded ? ExplodedBorder : cell.IsRevealed ? ClearedBorder : NormalBorder,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = interactive ? Cursors.Hand : Cursors.Arrow,
                SnapsToDevicePixels = true
            };

            var content = new Grid();

            var backdropSource = cell.IsRevealed
                ? visual.Thumbnail
                : visual.GlassThumbnail ?? visual.Thumbnail;
            if (backdropSource is BitmapSource backdrop)
            {
                content.Children.Add(new Image
                {
                    Source = backdrop,
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true
                });
            }

            Rectangle? mineScrim = null;
            if (revealedMine)
            {
                mineScrim = new Rectangle
                {
                    Fill = cell.Exploded ? ExplodedScrim : MineScrim,
                    IsHitTestVisible = false
                };
                content.Children.Add(mineScrim);
            }
            else if (cell.IsRevealed)
            {
                content.Children.Add(new Rectangle { Fill = RevealedWash, IsHitTestVisible = false });
            }
            else
            {
                content.Children.Add(new Rectangle
                {
                    Fill = backdropSource is null ? FallbackGlass : GlassOverlay,
                    IsHitTestVisible = false
                });
            }

            var sheen = new Rectangle
            {
                Fill = HoverSheen,
                Opacity = 0,
                IsHitTestVisible = false
            };
            if (interactive)
            {
                content.Children.Add(sheen);
            }

            if (revealedMine)
            {
                content.Children.Add(CreateMineGlyph());
            }
            else if (cell.IsRevealed)
            {
                if (cell.AdjacentMines > 0)
                {
                    content.Children.Add(CreateNumberBadge(cell.AdjacentMines));
                }
            }
            else if (cell.IsFlagged)
            {
                content.Children.Add(CreateFlagBadge());
            }

            if (backdropSource is null && !string.IsNullOrWhiteSpace(visual.Text))
            {
                content.Children.Add(new TextBlock
                {
                    Text = visual.Text,
                    FontFamily = UiFont,
                    FontSize = 10.5,
                    Foreground = LabelText,
                    Background = LabelFill,
                    Padding = new Thickness(2, 0, 2, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    IsHitTestVisible = false
                });
            }

            content.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = TopHighlight,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            });
            content.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = BottomShadow,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false
            });

            if (interactive)
            {
                var hoverRing = new Border
                {
                    BorderBrush = HoverBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Background = Brushes.Transparent,
                    Opacity = 0,
                    IsHitTestVisible = false
                };
                content.Children.Add(hoverRing);

                tile.MouseEnter += (_, _) =>
                {
                    AnimateOpacity(sheen, 1, 150);
                    AnimateOpacity(hoverRing, 1, 150);
                };
                tile.MouseLeave += (_, _) =>
                {
                    AnimateOpacity(sheen, 0, 150);
                    AnimateOpacity(hoverRing, 0, 150);
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

            tile.Child = content;

            Canvas.SetLeft(tile, visual.X);
            Canvas.SetTop(tile, visual.Y);
            canvas.Children.Add(tile);

            if (game.LastRevealed.Contains(cell))
            {
                PlayReveal(tile);
            }

            if (game.Status == GameStatus.Lost && cell.IsMine)
            {
                PlayLossEffect(tile, mineScrim, cell.Exploded);
            }
        }
    }

    private static void PlayReveal(Border tile)
    {
        tile.Opacity = 0;
        var scale = new ScaleTransform(0.94, 0.94);
        tile.RenderTransform = scale;
        tile.RenderTransformOrigin = new Point(0.5, 0.5);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        tile.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
    }

    private static void PlayLossEffect(Border tile, Rectangle? scrim, bool exploded)
    {
        if (scrim is not null)
        {
            scrim.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.4,
                    Duration = TimeSpan.FromMilliseconds(300),
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(3)
                });
        }

        if (!exploded)
        {
            return;
        }

        var shake = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(550),
            RepeatBehavior = new RepeatBehavior(2)
        };
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0.0)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(8, KeyTime.FromPercent(0.12)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-8, KeyTime.FromPercent(0.24)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(6, KeyTime.FromPercent(0.36)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-6, KeyTime.FromPercent(0.48)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(4, KeyTime.FromPercent(0.60)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-3, KeyTime.FromPercent(0.72)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(2, KeyTime.FromPercent(0.84)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

        var transform = new TranslateTransform();
        tile.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.XProperty, shake);
    }

    private static void AnimateOpacity(UIElement element, double to, int milliseconds)
    {
        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private static Border CreateNumberBadge(int number)
    {
        return new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = BadgeFill,
            BorderBrush = BadgeBorder,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontFamily = NumberFont,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = NumberBrushes[Math.Clamp(number, 0, NumberBrushes.Length - 1)],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static Border CreateFlagBadge()
    {
        return new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = BadgeFill,
            BorderBrush = FlagBadgeBorder,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Child = CreateFlagGlyph()
        };
    }

    private static Canvas CreateFlagGlyph()
    {
        var canvas = new Canvas
        {
            Width = 18,
            Height = 20,
            IsHitTestVisible = false
        };

        var pole = new Rectangle
        {
            Width = 2.5,
            Height = 15,
            Fill = FlagBrush,
            RadiusX = 1.25,
            RadiusY = 1.25
        };
        Canvas.SetLeft(pole, 5);
        Canvas.SetTop(pole, 2);
        canvas.Children.Add(pole);

        canvas.Children.Add(new Polygon
        {
            Fill = FlagBrush,
            Points = new PointCollection
            {
                new Point(7, 2),
                new Point(17, 5.5),
                new Point(7, 9)
            }
        });

        var foot = new Rectangle
        {
            Width = 10,
            Height = 2.5,
            Fill = FlagBrush,
            RadiusX = 1.25,
            RadiusY = 1.25
        };
        Canvas.SetLeft(foot, 1);
        Canvas.SetTop(foot, 16);
        canvas.Children.Add(foot);

        return canvas;
    }

    private static Canvas CreateMineGlyph()
    {
        var canvas = new Canvas
        {
            Width = 22,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        var centre = new Point(11, 11);
        for (var i = 0; i < 4; i++)
        {
            var angle = Math.PI * i / 4;
            var dx = Math.Cos(angle) * 9;
            var dy = Math.Sin(angle) * 9;
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
            Width = 12,
            Height = 12,
            Fill = GlyphDark,
            Stroke = GlyphLight,
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(body, 5);
        Canvas.SetTop(body, 5);
        canvas.Children.Add(body);

        return canvas;
    }

    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Gradient(Color top, Color bottom)
    {
        var brush = new LinearGradientBrush(top, bottom, new Point(0.5, 0), new Point(0.5, 1));
        brush.Freeze();
        return brush;
    }
}
