namespace ProjectMaelstrom.Utilities;

/// <summary>Holds the current in-process W101 fact store for local API consumers.
/// There is deliberately NO pipe command that publishes or mutates facts: a pipe
/// client cannot invent game observations. Only an owned resident reader inside
/// Maelstrom may populate this store, and it must supply exact target identity,
/// reviewed producer pins and cleanup evidence before any fact becomes Observed.</summary>
internal static class W101BackgroundFacts
{
    private static readonly object Gate = new();
    private static W101FactsStore _store = new();

    internal static IReadOnlyList<W101FactView> Snapshot()
    {
        lock (Gate) return _store.Snapshot(DateTime.UtcNow.Ticks);
    }

    /// <summary>Internal replacement for a freshly attached session. Resets all
    /// volatile facts; never callable from the local pipe or MCP surface.</summary>
    internal static void ReplaceStore(W101FactsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        lock (Gate) _store = store;
    }
}
