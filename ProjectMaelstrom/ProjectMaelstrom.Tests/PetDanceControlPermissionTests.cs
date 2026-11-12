using System.Text;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceControlPermissionTests
{
    [Fact]
    public void MissingPolicyDeniesAndDoesNotCreateAnything()
    {
        using var fixture = new Fixture();
        var absentDirectory = Path.Combine(fixture.DirectoryPath, "absent");
        var policy = Path.Combine(absentDirectory, "execution_policy.conf");
        Assert.False(new PetDanceControlPermission(policy, () => 100).IsAllowed());
        Assert.False(File.Exists(policy));
        Assert.False(Directory.Exists(absentDirectory));
    }

    [Theory]
    [InlineData("ALLOW_LIVE_AUTOMATION=false")]
    [InlineData("EXECUTION_PROFILE=ExperimentalSimulation")]
    [InlineData("")]
    [InlineData("# ALLOW_LIVE_AUTOMATION=true")]
    public void InitialOffOrMissingExplicitEnablementDenies(string contents)
    {
        using var fixture = new Fixture(contents);
        Assert.False(fixture.Create().IsAllowed());
    }

    [Fact]
    public void ExplicitTrueSupportsExistingWhitespaceCaseCommentsAndUtf8Bom()
    {
        using var fixture = new Fixture("\uFEFF# installed policy\r\n allow_live_automation = TRUE \r\nEXECUTION_PROFILE=ExperimentalSimulation\r\n");
        var before = File.ReadAllBytes(fixture.PolicyPath);
        Assert.True(fixture.Create().IsAllowed());
        Assert.Equal(before, File.ReadAllBytes(fixture.PolicyPath));
    }

    [Fact]
    public void ToggleOffIsObservedAtTheCacheBoundaryAndCanLaterBeReenabled()
    {
        using var fixture = new Fixture("ALLOW_LIVE_AUTOMATION=true");
        double now = 100;
        var permission = fixture.Create(() => now);
        Assert.True(permission.IsAllowed());
        fixture.Write("ALLOW_LIVE_AUTOMATION=false");
        now = 349.999;
        Assert.True(permission.IsAllowed());
        now = 350;
        Assert.False(permission.IsAllowed());
        fixture.Write("ALLOW_LIVE_AUTOMATION=true");
        now = 600;
        Assert.True(permission.IsAllowed());
        File.Delete(fixture.PolicyPath);
        now = 850;
        Assert.False(permission.IsAllowed());
    }

    [Theory]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nALLOW_LIVE_AUTOMATION=true")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nallow_live_automation=false")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nMETA=one\nmeta=two")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nnot a setting")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true=ignored")]
    [InlineData("ALLOW_LIVE_AUTOMATION=1")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true # inline comments are not values")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nEXECUTION_PROFILE=not-a-profile")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nEMPTY=")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\n=empty-key")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nBAD KEY=value")]
    [InlineData("ALLOW_LIVE_AUTOMATION=true\nMETA=has\0control")]
    public void MalformedOrDuplicateSettingsDenyEvenAlongsideTrue(string contents)
    {
        using var fixture = new Fixture(contents);
        Assert.False(fixture.Create().IsAllowed());
    }

    [Fact]
    public void PolicyBytesHaveAHardSizeLimitAndStrictUtf8Decoding()
    {
        using var fixture = new Fixture();
        const string prefix = "ALLOW_LIVE_AUTOMATION=true\n#";
        fixture.Write(prefix + new string('x', PetDanceControlPermission.MaximumPolicyBytes - prefix.Length));
        Assert.True(fixture.Create().IsAllowed());
        File.AppendAllText(fixture.PolicyPath, "x", new UTF8Encoding(false));
        Assert.False(fixture.Create().IsAllowed());
        File.WriteAllBytes(fixture.PolicyPath, Encoding.UTF8.GetBytes(prefix).Concat(new byte[] { 0xFF }).ToArray());
        Assert.False(fixture.Create().IsAllowed());
    }

    [Fact]
    public void LockedPolicyAndDirectoryPathDenyWithoutMutatingTheFile()
    {
        using var fixture = new Fixture("ALLOW_LIVE_AUTOMATION=true");
        using (var held = new FileStream(fixture.PolicyPath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(fixture.Create().IsAllowed());
        Assert.True(fixture.Create().IsAllowed());
        Assert.False(new PetDanceControlPermission(fixture.DirectoryPath, () => 100).IsAllowed());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void InvalidClockRevokesCachedPermission(double invalid)
    {
        using var fixture = new Fixture("ALLOW_LIVE_AUTOMATION=true");
        double now = 100;
        var permission = fixture.Create(() => now);
        Assert.True(permission.IsAllowed());
        now = invalid;
        Assert.False(permission.IsAllowed());
        fixture.Write("ALLOW_LIVE_AUTOMATION=false");
        now = 101; // Recovery must read again, not revive the earlier cached true.
        Assert.False(permission.IsAllowed());
    }

    [Fact]
    public void BackwardsClockDeniesUntilThePreviousHighWaterMarkIsRecovered()
    {
        using var fixture = new Fixture("ALLOW_LIVE_AUTOMATION=true");
        double now = 100;
        var permission = fixture.Create(() => now);
        Assert.True(permission.IsAllowed());
        now = 99; Assert.False(permission.IsAllowed());
        now = 99.5; Assert.False(permission.IsAllowed());
        fixture.Write("ALLOW_LIVE_AUTOMATION=false");
        now = 100; Assert.False(permission.IsAllowed());
    }

    [Fact]
    public void ClockFailureAndInvalidPathsDenyWithoutReadingAnAlternatePolicy()
    {
        using var fixture = new Fixture("ALLOW_LIVE_AUTOMATION=true");
        Assert.False(fixture.Create(() => throw new InvalidOperationException("clock unavailable")).IsAllowed());
        foreach (var path in new[] { "", "execution_policy.conf", "invalid\0path", null! })
            Assert.False(new PetDanceControlPermission(path, () => 100).IsAllowed());
    }

    private sealed class Fixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "maelstrom-control-permission-" + Guid.NewGuid().ToString("N"));
        public string PolicyPath => Path.Combine(DirectoryPath, "execution_policy.conf");
        public Fixture(string? contents = null)
        {
            Directory.CreateDirectory(DirectoryPath);
            if (contents != null) Write(contents);
        }
        public void Write(string contents) => File.WriteAllText(PolicyPath, contents, new UTF8Encoding(false));
        public PetDanceControlPermission Create(Func<double>? clock = null) => new(PolicyPath, clock ?? (() => 100));
        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
