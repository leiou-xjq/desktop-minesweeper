using System.IO;
using System.Text;
using DesktopMinesweeper.Game;

namespace DesktopMinesweeper.Diagnostics;

/// <summary>
/// Headless rules check. Run with <c>--selftest &lt;path&gt;</c>; it plays several
/// synthetic boards and writes a PASS/FAIL report without touching the desktop.
/// </summary>
internal static class GameSelfTest
{
    private static readonly (int Column, int Row)[] NeighbourOffsets =
    {
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1)
    };

    public static int Run(string path)
    {
        var log = new StringBuilder();
        var failures = 0;

        void Check(string name, bool ok, string? detail = null)
        {
            log.AppendLine($"{(ok ? "PASS" : "FAIL")} {name}{(detail is null ? string.Empty : " :: " + detail)}");
            if (!ok)
            {
                failures++;
            }
        }

        // 1. Fresh state.
        var game = new MinesweeperGame(FixedGrid(10, 8));
        Check("board has 80 cells", game.Cells.Count == 80, game.Cells.Count.ToString());
        Check("starts Ready", game.Status == GameStatus.Ready);
        Check("mine count is sane", game.MineCount is >= 3 and < 80, game.MineCount.ToString());
        Check("no mines before first click", game.Cells.All(cell => !cell.IsMine));
        Check("remaining equals mine count", game.MinesRemaining == game.MineCount);

        // 2. First click safety (centre).
        var first = game.Cells[4 * 10 + 5];
        game.Reveal(first);
        Check("first click starts the game", game.Status == GameStatus.Playing);
        Check("first click is safe", !first.IsMine && first.IsRevealed);
        Check("first click neighbours are safe", Neighbours(game, first).All(cell => !cell.IsMine));
        Check("mines were placed", game.Cells.Count(cell => cell.IsMine) == game.MineCount);

        // 3. Adjacency numbers must match the real neighbourhood.
        var adjacencyOk = true;
        foreach (var cell in game.Cells)
        {
            var expected = Neighbours(game, cell).Count(neighbour => neighbour.IsMine);
            if (cell.AdjacentMines != expected)
            {
                adjacencyOk = false;
                break;
            }
        }

        Check("adjacent numbers are correct", adjacencyOk);

        // 4. Zero regions must be fully expanded.
        var floodOk = game.Cells
            .Where(cell => cell.IsRevealed && cell.AdjacentMines == 0)
            .All(cell => Neighbours(game, cell)
                .All(neighbour => neighbour.IsMine || neighbour.IsRevealed || neighbour.IsFlagged));
        Check("zero cells expand their neighbours", floodOk);

        // 5. Flags block reveals.
        var flagGame = new MinesweeperGame(FixedGrid(10, 8));
        var target = flagGame.Cells[0];
        flagGame.ToggleFlag(target);
        Check("flag increments counter", flagGame.FlagsUsed == 1 && flagGame.MinesRemaining == flagGame.MineCount - 1);
        flagGame.Reveal(target);
        Check("flagged cell cannot be revealed", !target.IsRevealed && flagGame.Status == GameStatus.Ready);
        flagGame.ToggleFlag(target);
        Check("flag can be removed", flagGame.FlagsUsed == 0);

        // 6. Losing reveals every mine.
        var loseGame = new MinesweeperGame(FixedGrid(10, 8));
        loseGame.Reveal(loseGame.Cells[6 * 10 + 4]);
        var mine = loseGame.Cells.First(cell => cell.IsMine);
        loseGame.Reveal(mine);
        Check("revealing a mine loses", loseGame.Status == GameStatus.Lost);
        Check("lost board shows all mines", loseGame.Cells.Where(cell => cell.IsMine).All(cell => cell.IsRevealed));
        Check("exploded cell is marked", mine.Exploded);
        loseGame.Reveal(loseGame.Cells.First(cell => !cell.IsMine && !cell.IsRevealed));
        Check("input is ignored after loss", loseGame.Status == GameStatus.Lost);

        // 7. Winning flags all mines and stops the game.
        var winGame = new MinesweeperGame(FixedGrid(10, 8));
        winGame.Reveal(winGame.Cells[3 * 10 + 3]);
        foreach (var cell in winGame.Cells.Where(cell => !cell.IsMine && !cell.IsRevealed))
        {
            winGame.Reveal(cell);
        }

        Check("revealing all safe cells wins", winGame.Status == GameStatus.Won);
        Check("won board flags every mine", winGame.Cells.Where(cell => cell.IsMine).All(cell => cell.IsFlagged));
        Check("remaining is zero after win", winGame.MinesRemaining == 0);

        // 8. Restart clears everything.
        winGame.Restart();
        Check("restart resets status", winGame.Status == GameStatus.Ready);
        Check("restart clears mines", winGame.Cells.All(cell => !cell.IsMine));
        Check("restart clears flags", winGame.Cells.All(cell => !cell.IsFlagged));
        Check("restart clears reveals", winGame.Cells.All(cell => !cell.IsRevealed));

        // 9. Corner first click stays safe.
        var cornerGame = new MinesweeperGame(FixedGrid(10, 8));
        var corner = cornerGame.Cells[0];
        cornerGame.Reveal(corner);
        Check("corner first click is safe", !corner.IsMine);
        Check("corner neighbours are safe", Neighbours(cornerGame, corner).All(cell => !cell.IsMine));

        // 10. Sparse icon slots still playable.
        var sparseGame = new MinesweeperGame(SparseGrid());
        sparseGame.Reveal(sparseGame.Cells[sparseGame.Cells.Count / 2]);
        Check("sparse board starts", sparseGame.Status == GameStatus.Playing);
        Check("sparse board placed mines", sparseGame.Cells.Count(cell => cell.IsMine) == sparseGame.MineCount);
        Check("sparse first click is safe", !sparseGame.Cells[sparseGame.Cells.Count / 2].IsMine);

        log.AppendLine();
        log.AppendLine(failures == 0 ? "RESULT PASS" : $"RESULT FAIL ({failures})");
        File.WriteAllText(path, log.ToString(), new UTF8Encoding(false));
        return failures == 0 ? 0 : 1;
    }

    private static IEnumerable<GameCell> Neighbours(MinesweeperGame game, GameCell cell)
    {
        var lookup = game.Cells.ToDictionary(item => (item.Column, item.Row));
        foreach (var (columnOffset, rowOffset) in NeighbourOffsets)
        {
            if (lookup.TryGetValue((cell.Column + columnOffset, cell.Row + rowOffset), out var neighbour))
            {
                yield return neighbour;
            }
        }
    }

    private static BoardGrid FixedGrid(int columns, int rows)
    {
        var cells = new List<BoardCell>(columns * rows);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                cells.Add(new BoardCell(column, row, column * 32, row * 32, 32, 32, string.Empty, null, null, 0, 0, 0, 0, null));
            }
        }

        return new BoardGrid(cells, columns, rows, BoardSource.FixedGrid, null);
    }

    private static BoardGrid SparseGrid()
    {
        var cells = new List<BoardCell>();
        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                // Leave holes so adjacency must rely on coordinates, not list order.
                if ((row + column) % 3 == 0)
                {
                    continue;
                }

                cells.Add(new BoardCell(column, row, column * 32, row * 32, 32, 32, string.Empty, null, null, 0, 0, 0, 0, null));
            }
        }

        return new BoardGrid(cells, 8, 6, BoardSource.FixedGrid, null);
    }
}
