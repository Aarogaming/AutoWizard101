namespace MaelstromToolkit.Policy;

internal sealed class PolicyEvaluator
{
    private readonly PolicyDocument _document;

    public PolicyEvaluator(PolicyDocument document)
    {
        _document = document;
    }

    public EffectivePolicy Evaluate()
    {
        var active = _document.Profiles.TryGetValue(_document.Global.ActiveProfile, out var profile)
            ? profile
            : new ProfileSettings { Name = _document.Global.ActiveProfile, Mode = "catalog" };

        var operatingMode = active.Mode.Equals("live", StringComparison.OrdinalIgnoreCase)
            ? "LIVE"
            : active.Mode.ToUpperInvariant();

        var liveStatus = "N/A";
        var reasons = new List<string>();
        if (operatingMode == "LIVE")
        {
            liveStatus = "READY";
        }

        return new EffectivePolicy(
            Source: "current",
            ActiveProfile: active.Name,
            OperatingMode: operatingMode,
            LiveStatus: liveStatus,
            Reasons: reasons,
            AiProvider: _document.Ai.Provider,
            AiModel: _document.Ai.Model,
            AiKeyEnv: _document.Ai.ApiKeyEnv,
            LiveMeansLive: _document.Global.LiveMeansLive);
    }
}

internal sealed record EffectivePolicy(
    string Source,
    string ActiveProfile,
    string OperatingMode,
    string LiveStatus,
    IReadOnlyList<string> Reasons,
    string AiProvider,
    string AiModel,
    string AiKeyEnv,
    bool LiveMeansLive);
