namespace ProjectMaelstrom.Utilities;

/// <summary>Qualification tier of a published fact. Candidate facts are readable
/// but never authorize actions; Unknown is the explicit absence of a current fact.</summary>
internal enum W101FactQualification { Observed, Candidate, Unknown }

/// <summary>One published fact with primary-source provenance. Values are strings
/// by design: the store does not invent typed conversions or default metrics.</summary>
internal sealed record W101Fact(string Id, string Value, string Producer, long ObservedUtcTicks,
    string LayoutId, W101FactQualification Qualification, string Reason = "");

/// <summary>Read-only view returned to API/MCP/CLI consumers. Never stale by default:
/// expiry and target changes yield Unknown with an explicit reason.</summary>
internal sealed record W101FactView(string Id, string Value, string Producer, long ObservedUtcTicks,
    long AgeMs, string LayoutId, W101FactQualification Qualification, string Reason);

/// <summary>Bounded, deterministic fact store for the background W101 facts service.
///
/// Holds at most MaximumFacts entries. A fact is accepted only if its ID exists in
/// the registered catalog (or a caller-supplied proposed catalog). Registered facts
/// with an available producer publish as Observed; proposed or unavailable-producer
/// IDs can only ever publish as Candidate — the store cannot promote a research claim
/// into executable authority. TTL comes from the fact definition; expiry and target
/// invalidation produce Unknown, never a stale value. No I/O, threads or input here.</summary>
internal sealed class W101FactsStore
{
    internal const int MaximumFacts = 64;
    private readonly IReadOnlyList<JobFactDefinition> _proposed;
    private readonly Dictionary<string, W101Fact> _facts = new(StringComparer.Ordinal);
    private string _invalidationReason = "";

    internal W101FactsStore(IReadOnlyList<JobFactDefinition>? proposedFacts = null)
    {
        _proposed = proposedFacts ?? [];
        if (_proposed.Count > MaximumFacts) throw new ArgumentOutOfRangeException(nameof(proposedFacts));
    }

    /// <summary>Accepts a fact and returns the stored qualification, or null when the
    /// ID/producer/layout is not admissible. A failed publish never overwrites a prior
    /// valid fact with fabricated data.</summary>
    internal W101FactQualification? Publish(W101Fact fact, long nowUtcTicks)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (string.IsNullOrWhiteSpace(fact.Id) || string.IsNullOrWhiteSpace(fact.Producer) ||
            string.IsNullOrWhiteSpace(fact.LayoutId) || fact.Value is null) return null;
        if (fact.ObservedUtcTicks > nowUtcTicks || nowUtcTicks - fact.ObservedUtcTicks < 0) return null;
        var definition = JobFactCatalog.Find(fact.Id, _proposed);
        if (definition is null) return null;
        var isProposed = _proposed.Any(p => p.Id == definition.Id);
        if (!definition.ProducerAvailable && !isProposed) return null;
        if (!string.Equals(definition.Producer, fact.Producer, StringComparison.OrdinalIgnoreCase) &&
            !fact.Producer.StartsWith("w101.", StringComparison.Ordinal)) return null;
        var qualification = isProposed ? W101FactQualification.Candidate : W101FactQualification.Observed;
        if (_facts.Count >= MaximumFacts && !_facts.ContainsKey(definition.Id)) return null;
        _facts[definition.Id] = fact with { Id = definition.Id, Qualification = qualification };
        return qualification;
    }

    internal W101FactView? Get(string id, long nowUtcTicks)
    {
        var definition = JobFactCatalog.Find(id, _proposed);
        var key = definition?.Id ?? id;
        if (!_facts.TryGetValue(key, out var fact))
            return new W101FactView(key, "", "unavailable", 0, 0, "", W101FactQualification.Unknown,
                _invalidationReason.Length > 0 ? _invalidationReason : "no-current-observation");
        var age = (nowUtcTicks - fact.ObservedUtcTicks) / TimeSpan.TicksPerMillisecond;
        var ttl = TimeSpan.FromSeconds(definition?.FreshnessSeconds ?? 2).TotalMilliseconds;
        if (_invalidationReason.Length > 0 || age < 0 || age > ttl)
            return new W101FactView(key, "", fact.Producer, fact.ObservedUtcTicks, Math.Max(0, age),
                fact.LayoutId, W101FactQualification.Unknown,
                _invalidationReason.Length > 0 ? _invalidationReason : "expired");
        return new W101FactView(key, fact.Value, fact.Producer, fact.ObservedUtcTicks, age,
            fact.LayoutId, fact.Qualification, "");
    }

    /// <summary>Target/process/hook identity changes invalidate all volatile facts.
    /// Explicit: no auto-retarget, no stale bridge, no continuation of old readings.</summary>
    internal void InvalidateTarget(string reason)
    {
        _facts.Clear();
        _invalidationReason = string.IsNullOrWhiteSpace(reason) ? "target-changed" : reason;
    }

    /// <summary>Clears an invalidation only for a fresh, explicitly attached session.</summary>
    internal void Reattach() => _invalidationReason = "";

    internal IReadOnlyList<W101FactView> Snapshot(long nowUtcTicks)
    {
        var ids = JobFactCatalog.All.Select(f => f.Id).Concat(_proposed.Select(f => f.Id))
            .Concat(_facts.Keys).Distinct(StringComparer.Ordinal).ToArray();
        return ids.Select(id => Get(id, nowUtcTicks)!).ToArray();
    }
}
