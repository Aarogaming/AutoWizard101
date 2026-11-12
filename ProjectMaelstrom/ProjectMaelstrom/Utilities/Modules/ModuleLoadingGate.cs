namespace ProjectMaelstrom.Utilities;

/// <summary>An optional condition supplied by a trusted game adapter, not a pack.
/// Time uses the same monotonic clock as capture. A clear flag never supplies
/// scene recognition, controls, context facts or action outcome evidence.</summary>
internal sealed record ModuleLoadingObservation(RoutineBinding Binding, string Source, double SampledMs, bool? Loading);
internal sealed record ModuleLoadingRequirement(string Source, double MaximumAgeMs = 1000);

internal static class ModuleLoadingGate
{
    internal static void Validate(ModuleLoadingRequirement requirement)
    {
        OperationModules.CheckId(requirement.Source);
        if (!double.IsFinite(requirement.MaximumAgeMs) || requirement.MaximumAgeMs is <= 0 or > 1000)
            throw new InvalidDataException("Bound the optional loading observation to at most one second.");
    }

    // Loading, unavailable, stale, future and mismatched readings all withhold input.
    internal static bool IsClear(ModuleLoadingRequirement? requirement, ModuleLoadingObservation? sample,
        RoutineBinding binding, double now) => requirement is null ||
        IsCurrent(requirement, sample, binding, now) && sample!.Loading == false;

    internal static bool IsCurrent(ModuleLoadingRequirement requirement, ModuleLoadingObservation? sample,
        RoutineBinding binding, double now) => sample is not null && sample.Binding == binding &&
        sample.Source == requirement.Source && double.IsFinite(sample.SampledMs) && sample.SampledMs >= 0 &&
        double.IsFinite(now) && now >= sample.SampledMs && now - sample.SampledMs <= requirement.MaximumAgeMs;

    internal static ModuleObservation Apply(ModuleObservation evidence, ModuleLoadingRequirement? requirement,
        ModuleLoadingObservation? sample, RoutineBinding binding)
    {
        if (IsClear(requirement, sample, binding, evidence.Observation.TimeMs)) return evidence;
        var scene = requirement is not null && IsCurrent(requirement, sample, binding, evidence.Observation.TimeMs) &&
            sample!.Loading == true ? "Loading" : "Unknown";
        return evidence with
        {
            Observation = evidence.Observation with { Scene = scene, Fresh = false, ControlVerified = false, OutcomeForProposalId = null },
            VerifiedFacts = [], VerifiedControls = []
        };
    }
}
