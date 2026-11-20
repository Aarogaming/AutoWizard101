using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ActivityFactsTests
{
    private const long T0 = 639000000000000000;

    [Fact]
    public void DiegoFlagPublishesAsCandidateOnly()
    {
        var store = new W101FactsStore(W101ActivityFacts.ProposedFacts);
        Assert.Equal(W101FactQualification.Candidate,
            W101ActivityFacts.Publish(store, W101ActivityFacts.DiegoFlag, true, "r15-archived", T0));
        var view = store.Get(W101ActivityFacts.DiegoFlag, T0)!;
        Assert.Equal("true", view.Value);
        Assert.Equal(W101FactQualification.Candidate, view.Qualification);
        Assert.Equal("trigger-candidate", view.Reason);
    }

    [Fact]
    public void InactiveFlagIsExplicitFalseCandidate()
    {
        var store = new W101FactsStore(W101ActivityFacts.ProposedFacts);
        W101ActivityFacts.Publish(store, W101ActivityFacts.DiegoFlag, false, "r15-archived", T0);
        var view = store.Get(W101ActivityFacts.DiegoFlag, T0)!;
        Assert.Equal("false", view.Value);
        Assert.Equal("flag-inactive", view.Reason);
        Assert.Equal(W101FactQualification.Candidate, view.Qualification);
    }

    [Fact]
    public void UnknownFlagIsRejectedWithoutCreatingAFact()
    {
        var store = new W101FactsStore(W101ActivityFacts.ProposedFacts);
        Assert.Null(W101ActivityFacts.Publish(store, "activity.made-up", true, "r15-archived", T0));
        Assert.Equal(W101FactQualification.Unknown, store.Get("activity.made-up", T0)!.Qualification);
    }

    [Fact]
    public void CandidateFactNeverBecomesObservedAndExpires()
    {
        var store = new W101FactsStore(W101ActivityFacts.ProposedFacts);
        W101ActivityFacts.Publish(store, W101ActivityFacts.DiegoFlag, true, "r15-archived", T0);
        Assert.Equal(W101FactQualification.Unknown,
            store.Get(W101ActivityFacts.DiegoFlag, T0 + 3000 * TimeSpan.TicksPerMillisecond)!.Qualification);
    }
}
