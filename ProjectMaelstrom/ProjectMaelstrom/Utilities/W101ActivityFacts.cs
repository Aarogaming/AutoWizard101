namespace ProjectMaelstrom.Utilities;

/// <summary>Publishes activity-flag evaluations into the shared fact store as
/// CANDIDATE-ONLY facts. A flag is a trigger signal for a future react system, not a
/// qualified game state and never an authorization to dispatch input or to promote
/// any fact to available. Proposed fact IDs are supplied to the store with
/// ProducerAvailable=false so the store cannot upgrade them.</summary>
internal static class W101ActivityFacts
{
    internal const string DiegoFlag = "activity.diego.visible";
    internal const string DiegoProducer = "w101.ui-tree-flag";

    internal static readonly IReadOnlyList<JobFactDefinition> ProposedFacts =
        Array.AsReadOnly(new[]
        {
            new JobFactDefinition(DiegoFlag, "boolean", "", DiegoProducer, false,
                "Candidate Diego activity from visible UI nodes; trigger only, never an action authority.", 2),
        });

    /// <summary>Publishes one flag evaluation. Inactive flags are published as an
    /// explicit "false" candidate so consumers can distinguish "seen inactive"
    /// from "never observed" (Unknown). Returns the stored qualification.</summary>
    internal static W101FactQualification? Publish(W101FactsStore store, string flag, bool active,
        string layoutId, long nowUtcTicks)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (flag != DiegoFlag) return null;
        var fact = new W101Fact(flag, active ? "true" : "false", DiegoProducer, nowUtcTicks, layoutId,
            W101FactQualification.Candidate, active ? "trigger-candidate" : "flag-inactive");
        return store.Publish(fact, nowUtcTicks);
    }
}
