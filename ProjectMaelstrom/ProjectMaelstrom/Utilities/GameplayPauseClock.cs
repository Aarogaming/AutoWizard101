namespace ProjectMaelstrom.Utilities;

/// <summary>Accounts elapsed run time against separate cumulative startup/loading and
/// gameplay budgets. Advance on every loop, including missing-frame iterations.
/// This clock describes the latest scene; it does not grant fresh-frame input authority.</summary>
internal sealed class GameplayPauseClock
{
    private readonly double _pauseBudgetMs;
    private readonly double _gameplayBudgetMs;
    private double _lastWallMs;
    private bool _advanced;
    private bool _gameplayObserved;

    public GameplayPauseClock(double pauseBudgetMs = 45000, double gameplayBudgetMs = 75000)
    {
        if (!double.IsFinite(pauseBudgetMs) || pauseBudgetMs <= 0) throw new ArgumentOutOfRangeException(nameof(pauseBudgetMs));
        if (!double.IsFinite(gameplayBudgetMs) || gameplayBudgetMs <= 0) throw new ArgumentOutOfRangeException(nameof(gameplayBudgetMs));
        _pauseBudgetMs = pauseBudgetMs;
        _gameplayBudgetMs = gameplayBudgetMs;
    }

    public bool Started { get; private set; }
    public bool IsLoading { get; private set; }
    public double ActiveMs { get; private set; }
    public double PausedMs { get; private set; }
    public string? StopReason { get; private set; }
    public bool GameplayAllowed => Started && !IsLoading && _gameplayObserved && StopReason == null;

    /// <summary>wallMs is elapsed milliseconds from the run's origin, not a capture
    /// timestamp. The interval since the prior call belongs to the prior scene.
    /// Before the first call, elapsed time belongs to the startup wait.</summary>
    public void Advance(double wallMs, bool loading, bool gameplayObserved)
    {
        if (!double.IsFinite(wallMs) || wallMs < 0 || (_advanced && wallMs <= _lastWallMs))
            throw new ArgumentOutOfRangeException(nameof(wallMs), "Run time must be finite, nonnegative and strictly increasing.");
        if (loading && gameplayObserved) throw new ArgumentException("Loading and gameplay cannot be observed simultaneously.");

        var delta = wallMs - _lastWallMs;
        if (!Started || IsLoading) PausedMs += delta;
        else ActiveMs += delta;

        // Judge the elapsed interval before applying a new scene. A ready frame
        // at the deadline cannot retroactively forgive an exhausted wait.
        if (StopReason == null)
        {
            if (PausedMs >= _pauseBudgetMs)
                StopReason = IsLoading ? "loading-wait-limit" : "game-start-not-observed";
            else if (ActiveMs >= _gameplayBudgetMs)
                StopReason = "game-duration-limit";
        }

        _lastWallMs = wallMs;
        _advanced = true;
        IsLoading = loading;
        _gameplayObserved = gameplayObserved;
        Started |= gameplayObserved;
    }
}
