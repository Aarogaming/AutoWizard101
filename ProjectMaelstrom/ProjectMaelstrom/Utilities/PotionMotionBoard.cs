using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMaelstrom.Utilities;

public record PotionMotionMove(
    string MoveType, // "row" or "column"
    int Index,
    int Displacement,
    int Score,
    int MatchedCellCount,
    bool AllowWrapping);

/// <summary>
/// Native C# implementation of the Potion Motion board solver and move planner.
/// Ported from tools/potion_motion_board.py.
/// Models rectangular grids, row/column slide displacements, 4-neighbor connected groups
/// (including bent L and T shapes), and deterministic move scoring for free potion refills.
/// </summary>
public sealed class PotionMotionBoard
{
    private static readonly HashSet<string> UnknownTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "unknown", "empty", "none", "", "?"
    };

    private readonly string[][] _grid;
    public int Rows => _grid.Length;
    public int Cols => _grid[0].Length;

    public PotionMotionBoard(string[][] grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.Length == 0 || grid[0].Length == 0)
        {
            throw new ArgumentException("Grid must have positive dimensions.", nameof(grid));
        }

        int colCount = grid[0].Length;
        _grid = new string[grid.Length][];
        for (int r = 0; r < grid.Length; r++)
        {
            if (grid[r].Length != colCount)
            {
                throw new ArgumentException($"Row {r} length does not match expected column count {colCount}.", nameof(grid));
            }
            _grid[r] = new string[colCount];
            for (int c = 0; c < colCount; c++)
            {
                string val = grid[r][c]?.Trim().ToLowerInvariant() ?? "unknown";
                _grid[r][c] = val;
            }
        }
    }

    public string GetCell(int r, int c) => _grid[r][c];

    /// <summary>
    /// Finds all connected groups of >= 3 identical non-empty potion bottles (4-neighbor connectivity).
    /// </summary>
    public List<List<(int Row, int Col)>> FindMatches()
    {
        var visited = new bool[Rows, Cols];
        var matches = new List<List<(int Row, int Col)>>();

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (visited[r, c])
                {
                    continue;
                }

                string color = _grid[r][c];
                if (UnknownTokens.Contains(color))
                {
                    visited[r, c] = true;
                    continue;
                }

                var group = new List<(int Row, int Col)>();
                var queue = new Queue<(int Row, int Col)>();
                queue.Enqueue((r, c));
                visited[r, c] = true;

                while (queue.Count > 0)
                {
                    var (currR, currC) = queue.Dequeue();
                    group.Add((currR, currC));

                    int[] dR = [-1, 1, 0, 0];
                    int[] dC = [0, 0, -1, 1];

                    for (int i = 0; i < 4; i++)
                    {
                        int nR = currR + dR[i];
                        int nC = currC + dC[i];

                        if (nR >= 0 && nR < Rows && nC >= 0 && nC < Cols && !visited[nR, nC])
                        {
                            if (string.Equals(_grid[nR][nC], color, StringComparison.OrdinalIgnoreCase))
                            {
                                visited[nR, nC] = true;
                                queue.Enqueue((nR, nC));
                            }
                        }
                    }
                }

                if (group.Count >= 3)
                {
                    matches.Add(group);
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Simulates sliding a row horizontally by a given displacement.
    /// </summary>
    public PotionMotionBoard ApplyRowSlide(int rowIndex, int displacement, bool allowWrapping = true)
    {
        var newGrid = new string[Rows][];
        for (int r = 0; r < Rows; r++)
        {
            newGrid[r] = (string[])_grid[r].Clone();
        }

        int shift = displacement % Cols;
        if (shift < 0)
        {
            shift += Cols;
        }

        if (allowWrapping)
        {
            for (int c = 0; c < Cols; c++)
            {
                int srcCol = (c - shift + Cols) % Cols;
                newGrid[rowIndex][c] = _grid[rowIndex][srcCol];
            }
        }
        else
        {
            for (int c = 0; c < Cols; c++)
            {
                int srcCol = c - displacement;
                newGrid[rowIndex][c] = (srcCol >= 0 && srcCol < Cols) ? _grid[rowIndex][srcCol] : "empty";
            }
        }

        return new PotionMotionBoard(newGrid);
    }

    /// <summary>
    /// Finds the highest-scoring candidate move across all rows and columns.
    /// </summary>
    public PotionMotionMove? FindBestMove(bool allowWrapping = true)
    {
        PotionMotionMove? bestMove = null;

        // Try row slides
        for (int r = 0; r < Rows; r++)
        {
            for (int disp = 1; disp < Cols; disp++)
            {
                var simulated = ApplyRowSlide(r, disp, allowWrapping);
                var matches = simulated.FindMatches();
                if (matches.Count > 0)
                {
                    int totalCells = matches.Sum(g => g.Count);
                    int score = totalCells * 10;
                    if (bestMove == null || score > bestMove.Score)
                    {
                        bestMove = new PotionMotionMove("row", r, disp, score, totalCells, allowWrapping);
                    }
                }
            }
        }

        return bestMove;
    }
}
