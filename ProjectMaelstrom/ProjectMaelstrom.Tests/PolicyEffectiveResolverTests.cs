using MaelstromToolkit.Policy;

namespace ProjectMaelstrom.Tests;

public class PolicyEffectiveResolverTests
{
    [Fact]
    public void UsesLkgWhenFileInvalid()
    {
        var resolver = new PolicyEffectiveResolver(PolicyDefaults.DefaultPolicyText);
        var invalidText = "[global]\nschemaVersion = 2\n";
        var lkgText = PolicyDefaults.DefaultPolicyText;

        var result = resolver.Resolve(invalidText, lkgText);

        Assert.Equal("LKG", result.Source);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UsesDefaultWhenNoLkgAvailable()
    {
        var resolver = new PolicyEffectiveResolver(PolicyDefaults.DefaultPolicyText);
        var invalidText = string.Empty;

        var result = resolver.Resolve(invalidText, null);

        Assert.Equal("DEFAULT", result.Source);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void LiveProfileStaysLiveEvenWhenBlocked()
    {
        var doc = new PolicyDocument();
        doc.Global.ActiveProfile = "live_advisory";
        doc.Global.RequireAllProfilesValid = true;
        doc.Global.LiveMeansLive = true;
        doc.Global.SafeWrites = "outOnly";
        doc.Profiles["live_advisory"] = new ProfileSettings
        {
            Name = "live_advisory",
            Mode = "live",
            Autonomy = "advisory"
        };

        var snapshot = PolicySnapshot.FromDocument(doc);
        var validation = new PolicyValidator().Validate(snapshot);

        Assert.Equal("LIVE", validation.OperatingMode);
        Assert.Equal("BLOCKED", validation.LiveStatus);
        Assert.Contains("AASLIVE001", validation.Reasons);
        Assert.Equal(validation.Reasons.OrderBy(r => r, StringComparer.Ordinal).ToArray(), validation.Reasons.ToArray());
    }

    [Fact]
    public void RecognizesSafeWritesAndToolLists()
    {
        const string text = """
[global]
schemaVersion = 1
activeProfile = live_advisory
requireAllProfilesValid = true
denyUnknownCapabilities = true
denyUnknownKeys = false
liveMeansLive = true
safeWrites = outOnly

[ethics]
purpose = educational_research
requireConsentForEnvironmentControl = true

[profile catalog]
mode = catalog
[profile simulation]
mode = simulation
[profile live_advisory]
mode = live
autonomy = advisory
[profile live_pilot]
mode = live
autonomy = pilot

[ai]
enabled = true
provider = openai
apiKeyEnv = OPENAI_API_KEY
allowedTools = tool1,tool2
deniedTools = raw.shell
""";

        var parsed = new PolicyParser().Parse(text);
        var snapshot = PolicySnapshot.FromDocument(parsed.Document!);
        var validation = new PolicyValidator().Validate(snapshot);
        var codes = parsed.SortedDiagnostics().Concat(validation.Diagnostics).Select(d => d.Code).ToArray();

        Assert.DoesNotContain("AASPOL011", codes);
    }

    [Fact]
    public void SafeWritesMustBeOutOnly()
    {
        var doc = new PolicyDocument();
        doc.Global.SafeWrites = "anywhere";
        doc.Global.ActiveProfile = "catalog";
        doc.Profiles["catalog"] = new ProfileSettings { Name = "catalog", Mode = "catalog" };
        doc.Profiles["simulation"] = new ProfileSettings { Name = "simulation", Mode = "simulation" };
        doc.Profiles["live_advisory"] = new ProfileSettings { Name = "live_advisory", Mode = "live", Autonomy = "advisory" };
        doc.Profiles["live_pilot"] = new ProfileSettings { Name = "live_pilot", Mode = "live", Autonomy = "pilot" };

        var snapshot = PolicySnapshot.FromDocument(doc);
        var validation = new PolicyValidator().Validate(snapshot);

        Assert.Contains(validation.Diagnostics, d => d.Code == "AASPOL020" && d.Key == "safeWrites");
    }
}
