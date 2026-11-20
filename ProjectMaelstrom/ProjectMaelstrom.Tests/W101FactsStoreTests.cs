using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101FactsStoreTests
{
    private const long T0 = 639000000000000000;
    private static long Ms(long value) => value * TimeSpan.TicksPerMillisecond;

    private static readonly IReadOnlyList<JobFactDefinition> Proposed =
        [new JobFactDefinition("zone.candidate", "string", "", "w101.zone-reader", false, "Candidate zone", 5)];

    [Fact]
    public void RegisteredFactPublishesObservedAndAges()
    {
        var store = new W101FactsStore();
        Assert.Equal(W101FactQualification.Observed, store.Publish(
            new W101Fact("health", "4920", "w101.resources", T0, "r15-archived", W101FactQualification.Candidate), T0));
        var view = store.Get("health", T0 + Ms(500))!;
        Assert.Equal("4920", view.Value);
        Assert.Equal(W101FactQualification.Observed, view.Qualification);
        Assert.Equal(500, view.AgeMs);
    }

    [Fact]
    public void ExpiryYieldsUnknownNotStaleValue()
    {
        var store = new W101FactsStore();
        store.Publish(new W101Fact("health", "4920", "w101.resources", T0, "r15-archived", W101FactQualification.Candidate), T0);
        var expired = store.Get("health", T0 + Ms(2500))!; // default TTL 2 s
        Assert.Equal(W101FactQualification.Unknown, expired.Qualification);
        Assert.Equal("expired", expired.Reason);
        Assert.Equal("", expired.Value);
    }

    [Fact]
    public void ProposedFactCanOnlyBeCandidate()
    {
        var store = new W101FactsStore(Proposed);
        Assert.Equal(W101FactQualification.Candidate, store.Publish(
            new W101Fact("zone.candidate", "Khrysalis/KR_Z10_I05_DryGulchCave", "w101.zone-reader", T0,
                "archived-wizwalker-client-zone", W101FactQualification.Candidate), T0));
        Assert.Equal(W101FactQualification.Candidate, store.Get("zone.candidate", T0 + Ms(100))!.Qualification);
    }

    [Fact]
    public void UnregisteredFactIsRejected()
    {
        var store = new W101FactsStore();
        Assert.Null(store.Publish(new W101Fact("pips.fake", "7", "w101.fake", T0, "x", W101FactQualification.Candidate), T0));
    }

    [Fact]
    public void TargetInvalidationClearsAllVolatileFactsUntilReattach()
    {
        var store = new W101FactsStore(Proposed);
        store.Publish(new W101Fact("health", "4920", "w101.resources", T0, "r15-archived", W101FactQualification.Candidate), T0);
        store.InvalidateTarget("target-replaced");
        Assert.Equal("target-replaced", store.Get("health", T0)!.Reason);
        Assert.Equal(W101FactQualification.Unknown, store.Get("zone.candidate", T0)!.Qualification);
        store.Reattach();
        Assert.Equal(W101FactQualification.Unknown, store.Get("health", T0)!.Qualification);
    }

    [Fact]
    public void FutureTimestampIsRejected()
    {
        var store = new W101FactsStore();
        Assert.Null(store.Publish(new W101Fact("health", "1", "w101.resources", T0 + 1, "r15-archived",
            W101FactQualification.Candidate), T0));
    }

    [Fact]
    public void MissingFactsAreExplicitlyUnknownAndNullIsRejected()
    {
        var store = new W101FactsStore();
        Assert.Throws<ArgumentNullException>(() => store.Publish(null!, T0));
        var missing = store.Get("health", T0)!;
        Assert.Equal(W101FactQualification.Unknown, missing.Qualification);
        Assert.Equal("no-current-observation", missing.Reason);
        Assert.NotEmpty(store.Snapshot(T0));
    }
}
