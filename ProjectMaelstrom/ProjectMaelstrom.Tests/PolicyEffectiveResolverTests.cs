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
}
