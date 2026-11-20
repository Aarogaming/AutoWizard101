namespace ProjectMaelstrom.Utilities;

/// <summary>Pure admission budget; the existing host owns all observation execution.</summary>
internal sealed class DeckObservationRefresh
{
    private string? _run;
    private DateTime _next;
    internal int Attempts { get; private set; }
    internal string Reason { get; private set; } = "not-requested";
    internal static bool CanPublish(long generation, long currentGeneration, DateTime now, DateTime? deadline, bool stopping) =>
        generation == currentGeneration && !stopping && (deadline is null || now < deadline);
    internal bool Admit(string? run, DateTime now, DateTime? deadline, DateTime? observed,
        bool waiting, bool enabled, bool busy, bool blocked)
    {
        if (!waiting || !enabled || busy || blocked || string.IsNullOrEmpty(run))
        { Reason = "not-eligible"; return false; }
        if (_run != run) { _run = run; Attempts = 0; _next = DateTime.MinValue; }
        if (deadline is null || deadline.Value - now < TimeSpan.FromSeconds(5))
        { Reason = "deadline"; return false; }
        if (Attempts >= 30) { Reason = "refresh-budget-exhausted"; return false; }
        if (now < _next) { Reason = "cooldown"; return false; }
        if (observed is { } stamp && stamp <= now && now - stamp <= TimeSpan.FromSeconds(2))
        { Reason = "facts-fresh"; return false; }
        Attempts++; _next = now.AddSeconds(5); Reason = "admitted"; return true;
    }
}
