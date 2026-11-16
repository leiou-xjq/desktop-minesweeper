using System.Linq;

namespace DesktopMinesweeper.Game;

public enum GameStatus
{
    Ready,
    Playing,
    Won,
    Lost
}

public sealed class GameCell
{
    public GameCell(BoardCell visual)
    {
        Visual = visual;
    }

    public BoardCell Visual { get; }

    public bool IsMine { get; internal set; }

    public bool IsRevealed { get; internal set; }

    public bool IsFlagged { get; internal set; }

    public int AdjacentMines { get; internal set; }

    public bool Exploded { get; internal set; }

    public int Column => Visual.Column;

    public int Row => Visual.Row;

    public bool IsCovered => !IsRevealed;
}

/// <summary>
/// Pure Minesweeper rules over the icon slots produced by <see cref="BoardGridBuilder"/>.
/// Mines are placed after the first reveal, so the first click always lands on a
/// safe cell (and, when possible, its neighbours are safe too). The game never
/// touches the desktop; it only changes its own state.
/// </summary>
public sealed class MinesweeperGame
{
    private const double MineRatio = 0.16;
    private static readonly (int Column, int Row)[] NeighbourOffsets =
    {
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1)
    };

    private readonly Dictionary<(int Column, int Row), GameCell> _bySlot;
    private readonly Random _random = new();

    public MinesweeperGame(BoardGrid grid)
    {
        Cells = grid.Cells.Select(cell => new GameCell(cell)).ToArray();
        Columns = grid.Columns;
        Rows = grid.Rows;
        Source = grid.Source;
        Notice = grid.Notice;

        _bySlot = new Dictionary<(int Column, int Row), GameCell>(Cells.Count);
        foreach (var cell in Cells)
        {
            _bySlot[(cell.Column, cell.Row)] = cell;
        }

        MineCount = Math.Clamp(
            (int)Math.Round(Cells.Count * MineRatio),
            3,
            Math.Max(1, Cells.Count / 3));
    }

    public IReadOnlyList<GameCell> Cells { get; }

    public int Columns { get; }

    public int Rows { get; }

    public BoardSource Source { get; }

    public string? Notice { get; }

    public int MineCount { get; }

    public int FlagsUsed => Cells.Count(cell => cell.IsFlagged);

    public int MinesRemaining => MineCount - FlagsUsed;

    public GameStatus Status { get; private set; } = GameStatus.Ready;

    public bool IsFinished => Status is GameStatus.Won or GameStatus.Lost;

    private readonly List<GameCell> _lastRevealed = new();

    /// <summary>
    /// Cells uncovered by the most recent action. The renderer animates only these,
    /// so flagging or restarting never replays the reveal effect across the board.
    /// </summary>
    public IReadOnlyList<GameCell> LastRevealed => _lastRevealed;

    public event Action? Changed;

    public void Restart()
    {
        _lastRevealed.Clear();

        foreach (var cell in Cells)
        {
            cell.IsMine = false;
            cell.IsRevealed = false;
            cell.IsFlagged = false;
            cell.AdjacentMines = 0;
            cell.Exploded = false;
        }

        Status = GameStatus.Ready;
        Changed?.Invoke();
    }

    public void ToggleFlag(GameCell cell)
    {
        if (IsFinished || cell.IsRevealed)
        {
            return;
        }

        _lastRevealed.Clear();
        cell.IsFlagged = !cell.IsFlagged;
        Changed?.Invoke();
    }

    public void Reveal(GameCell cell)
    {
        if (IsFinished || cell.IsRevealed || cell.IsFlagged)
        {
            return;
        }

        _lastRevealed.Clear();

        if (Status == GameStatus.Ready)
        {
            PlaceMines(cell);
            Status = GameStatus.Playing;
        }

        if (cell.IsMine)
        {
            cell.Exploded = true;
            Lose();
            Changed?.Invoke();
            return;
        }

        FloodReveal(cell);
        CheckWin();
        Changed?.Invoke();
    }

    private void PlaceMines(GameCell first)
    {
        var safe = new HashSet<GameCell>(Neighbours(first)) { first };

        var candidates = Cells.Where(cell => !safe.Contains(cell)).ToList();
        if (candidates.Count < MineCount)
        {
            // Sparse icon layouts can make the first click's neighbourhood cover
            // most of the board; fall back to protecting only the clicked cell.
            candidates = Cells.Where(cell => cell != first).ToList();
        }

        Shuffle(candidates);
        for (var i = 0; i < MineCount && i < candidates.Count; i++)
        {
            candidates[i].IsMine = true;
        }

        foreach (var cell in Cells)
        {
            cell.AdjacentMines = Neighbours(cell).Count(neighbour => neighbour.IsMine);
        }
    }

    private void FloodReveal(GameCell start)
    {
        var stack = new Stack<GameCell>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var cell = stack.Pop();
            if (cell.IsRevealed || cell.IsFlagged || cell.IsMine)
            {
                continue;
            }

            cell.IsRevealed = true;
            _lastRevealed.Add(cell);
            if (cell.AdjacentMines != 0)
            {
                continue;
            }

            foreach (var neighbour in Neighbours(cell))
            {
                if (!neighbour.IsRevealed && !neighbour.IsFlagged && !neighbour.IsMine)
                {
                    stack.Push(neighbour);
                }
            }
        }
    }

    private void Lose()
    {
        _lastRevealed.Clear();
        Status = GameStatus.Lost;
        foreach (var cell in Cells)
        {
            if (cell.IsMine)
            {
                cell.IsRevealed = true;
            }
        }
    }

    private void CheckWin()
    {
        var covered = Cells.Count(cell => !cell.IsRevealed);
        if (covered != MineCount)
        {
            return;
        }

        Status = GameStatus.Won;
        foreach (var cell in Cells)
        {
            if (cell.IsMine)
            {
                cell.IsRevealed = false;
                cell.IsFlagged = true;
            }
        }
    }

    private IEnumerable<GameCell> Neighbours(GameCell cell)
    {
        foreach (var (columnOffset, rowOffset) in NeighbourOffsets)
        {
            if (_bySlot.TryGetValue((cell.Column + columnOffset, cell.Row + rowOffset), out var neighbour))
            {
                yield return neighbour;
            }
        }
    }

    private void Shuffle(IList<GameCell> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
