using System.Windows.Media.Imaging;
using DesktopMinesweeper.Desktop;

namespace DesktopMinesweeper.Game;

public enum BoardSource
{
    DesktopIcons,
    FixedGrid
}

public sealed record BoardCell(
    int Column,
    int Row,
    double X,
    double Y,
    double Width,
    double Height,
    string Text,
    BitmapSource? Thumbnail,
    BitmapSource? GlassThumbnail,
    BitmapSource? Icon,
    double IconX,
    double IconY,
    double IconWidth,
    double IconHeight,
    int? IconIndex);

public sealed record BoardGrid(
    IReadOnlyList<BoardCell> Cells,
    int Columns,
    int Rows,
    BoardSource Source,
    string? Notice);

/// <summary>
/// Turns desktop icon slots into a board. Only occupied icon slots become cells,
/// so a mine always has a real icon to display and shake. If icon metadata is
/// unavailable (or too sparse), a fixed 10x8 grid is used instead.
/// </summary>
public static class BoardGridBuilder
{
    public const int MinimumIconCells = 12;
    public const int FixedColumns = 10;
    public const int FixedRows = 8;

    public static BoardGrid Build(
        DesktopSnapshot snapshot,
        MonitorRect workArea,
        int dpi,
        bool forceFixedGrid = false)
    {
        var scale = 96.0 / dpi;

        if (!forceFixedGrid)
        {
            var icons = snapshot.Icons
                .Where(icon =>
                    icon.ScreenX >= workArea.Left &&
                    icon.ScreenX < workArea.Left + workArea.Width &&
                    icon.ScreenY >= workArea.Top &&
                    icon.ScreenY < workArea.Top + workArea.Height)
                .ToList();

            if (icons.Count >= MinimumIconCells)
            {
                var grid = BuildFromIcons(snapshot, icons, workArea, dpi, scale);
                if (grid is not null)
                {
                    return grid;
                }
            }
        }

        var notice = forceFixedGrid
            ? "固定网格模式（手动）"
            : snapshot.Icons.Count == 0
                ? "图标读取不可�?· 固定网格"
                : $"可用图标不足 {MinimumIconCells} �?· 固定网格";

        return BuildFixed(workArea, scale, notice);
    }

    private static BoardGrid? BuildFromIcons(
        DesktopSnapshot snapshot,
        IReadOnlyList<DesktopIconInfo> icons,
        MonitorRect workArea,
        int dpi,
        double scale)
    {
        var pitchX = DerivePitch(icons.Select(i => i.ScreenX), snapshot.SpacingX);
        var pitchY = DerivePitch(icons.Select(i => i.ScreenY), snapshot.SpacingY);
        if (pitchX <= 0 || pitchY <= 0)
        {
            return null;
        }

        var originX = icons.Min(i => i.ScreenX);
        var originY = icons.Min(i => i.ScreenY);

        var fullBitmap = DesktopBitmapCapture.Capture(snapshot.ListViewHandle, snapshot.ListViewRect);

        var seen = new HashSet<(int Column, int Row)>();
        var cells = new List<BoardCell>(icons.Count);
        var maxColumn = 0;
        var maxRow = 0;

        foreach (var icon in icons)
        {
            var column = (int)Math.Round((icon.ScreenX - originX) / (double)pitchX);
            var row = (int)Math.Round((icon.ScreenY - originY) / (double)pitchY);
            if (column < 0 || row < 0 || column > 200 || row > 200)
            {
                continue;
            }

            if (!seen.Add((column, row)))
            {
                continue;
            }

            var x = (icon.ScreenX - workArea.Left) * scale;
            var y = (icon.ScreenY - workArea.Top) * scale;
            var width = Math.Min(pitchX * scale, workArea.Width - x);
            var height = Math.Min(pitchY * scale, workArea.Height - y);
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            // The list-view anchor is the icon's top-left, not the slot's left edge.
            // Desktop labels are centered in the slot, so pad the cell horizontally
            // to avoid cutting the left part of long labels.
            var padX = Math.Max(0, (pitchX - icon.IconRect.Width) / 2);
            x = (icon.ScreenX - padX - workArea.Left) * scale;
            width = Math.Min(pitchX * scale, workArea.Width - x);
            if (width <= 0)
            {
                continue;
            }

            var cropped = DesktopBitmapCapture.Crop(
                fullBitmap,
                new PixelRect(
                    icon.IconRect.Left,
                    icon.IconRect.Top,
                    icon.IconRect.Right,
                    icon.IconRect.Bottom));

            // The cell thumbnail is a copy of the icon slot (icon + label + wallpaper).
            // Drawing it opaquely prevents the live icon underneath from showing through
            // and creating doubled labels.
            var thumbnail = DesktopBitmapCapture.Crop(
                fullBitmap,
                new PixelRect(
                    icon.ClientX - padX,
                    icon.ClientY,
                    icon.ClientX - padX + pitchX,
                    icon.ClientY + pitchY));

            // Frosted backdrop used while the cell is covered: the live icon is
            // readable as a silhouette but the cell still reads as glass.
            var glass = DesktopBitmapCapture.BlurAndDarken(thumbnail, 6, 0.8);

            var iconOffsetX = padX * scale;
            var iconOffsetY = (icon.IconRect.Top - icon.ClientY) * scale;

            cells.Add(new BoardCell(
                column,
                row,
                x,
                y,
                width,
                height,
                icon.Text,
                thumbnail,
                glass,
                cropped,
                iconOffsetX,
                iconOffsetY,
                icon.IconRect.Width * scale,
                icon.IconRect.Height * scale,
                icon.Index));

            maxColumn = Math.Max(maxColumn, column);
            maxRow = Math.Max(maxRow, row);
        }

        if (cells.Count < MinimumIconCells)
        {
            return null;
        }

        return new BoardGrid(cells, maxColumn + 1, maxRow + 1, BoardSource.DesktopIcons, null);
    }

    private static BoardGrid BuildFixed(MonitorRect workArea, double scale, string notice)
    {
        var width = workArea.Width * scale;
        var height = workArea.Height * scale;
        var cellWidth = width / FixedColumns;
        var cellHeight = height / FixedRows;

        var cells = new List<BoardCell>(FixedColumns * FixedRows);
        for (var row = 0; row < FixedRows; row++)
        {
            for (var column = 0; column < FixedColumns; column++)
            {
                cells.Add(new BoardCell(
                    column,
                    row,
                    column * cellWidth,
                    row * cellHeight,
                    cellWidth,
                    cellHeight,
                    string.Empty,
                    null,
                    null,
                    null,
                    0,
                    0,
                    0,
                    0,
                    null));
            }
        }

        return new BoardGrid(cells, FixedColumns, FixedRows, BoardSource.FixedGrid, notice);
    }

    private static int DerivePitch(IEnumerable<int> coordinates, int fallback)
    {
        var distinct = coordinates.Distinct().OrderBy(value => value).ToArray();
        if (distinct.Length < 2)
        {
            return Math.Max(fallback, 1);
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
            return Math.Max(fallback, 1);
        }

        gaps.Sort();
        return Math.Max(gaps[gaps.Count / 2], 1);
    }
}
