namespace MaelstromToolkit.Policy;

internal sealed class PolicyResolver
{
    private readonly PolicyService _service;

    public PolicyResolver(PolicyService service)
    {
        _service = service;
    }

    public (EffectivePolicy policy, PolicyLoadResult sourceResult, string source) Resolve(string policyPath, string outRoot)
    {
        var current = _service.Load(policyPath);
        if (!current.HasErrors && current.Document != null)
        {
            return (BuildEffective(current.Document, "current", current.Diagnostics), current, "current");
        }

        var lkg = _service.TryLoadLkg(outRoot);
        if (!lkg.HasErrors && lkg.Document != null)
        {
            var eff = BuildEffective(lkg.Document, "lkg", lkg.Diagnostics);
            return (eff with { LiveStatus = eff.LiveStatus == "READY" ? "DEGRADED" : eff.LiveStatus, Reasons = eff.Reasons.Concat(new[] { "POLICY_FALLBACK_LKG" }).ToList() }, lkg, "lkg");
        }

        var fallbackDoc = BuildDefaultPolicy();
        var effDefault = BuildEffective(fallbackDoc, "default", new List<PolicyDiagnostic>());
        effDefault = effDefault with
        {
            LiveStatus = effDefault.OperatingMode == "LIVE" ? "BLOCKED" : "N/A",
            Reasons = effDefault.Reasons.Concat(new[] { "POLICY_DEFAULT_ACTIVE" }).ToList()
        };
        var fallbackResult = new PolicyLoadResult { Document = fallbackDoc };
        return (effDefault, fallbackResult, "default");
    }

    private static PolicyDocument BuildDefaultPolicy()
    {
        var doc = new PolicyDocument();
        doc.Global.ActiveProfile = "catalog";
        doc.Profiles["catalog"] = new ProfileSettings { Name = "catalog", Mode = "catalog" };
        doc.Profiles["simulation"] = new ProfileSettings { Name = "simulation", Mode = "simulation" };
        doc.Profiles["live_advisory"] = new ProfileSettings { Name = "live_advisory", Mode = "live", Autonomy = "advisory" };
        doc.Profiles["live_pilot"] = new ProfileSettings { Name = "live_pilot", Mode = "live", Autonomy = "pilot" };
        return doc;
    }

    private static EffectivePolicy BuildEffective(PolicyDocument doc, string source, IEnumerable<PolicyDiagnostic> diagnostics)
    {
        var evaluator = new PolicyEvaluator(doc);
        var eff = evaluator.Evaluate();
        var reasons = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Code)
            .ToList();

        var liveStatus = eff.OperatingMode == "LIVE"
            ? (reasons.Count == 0 ? "READY" : "DEGRADED")
            : "N/A";

        return eff with
        {
            Source = source,
            LiveStatus = liveStatus,
            Reasons = reasons
        };
    }
}
