using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceTrialOptionsTests
{
    [Fact]
    public void ResearchLaunchWithoutOptionsRemainsExplicitlyUnbound()
    {
        var options = PetDanceTrialOptions.Parse([]);
        Assert.False(options.RespectControlSetting);
        Assert.Null(options.ExpectedTarget);
        Assert.Null(options.ClientLoadingWorkspace);
        Assert.True(options.ReturnToNativeIdle);
    }

    [Fact]
    public void OptionalLoadingRequiresExplicitVisitIdentityAndIdleContinuation()
    {
        var option = "--client-loading-workspace=" + Path.GetTempPath();
        Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse([option]));
        Assert.True(PetDanceTrialOptions.Parse([option, "--target-process=42:123"]).ReturnToNativeIdle);
        Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse([option, "--return-native-idle"]));
        Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse([option, option, "--return-native-idle", "--target-process=42:123"]));
        Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse(["--client-loading-workspace=relative", "--return-native-idle", "--target-process=42:123"]));
        var parsed = PetDanceTrialOptions.Parse([option, "--return-native-idle", "--target-process=42:123"]);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()), parsed.ClientLoadingWorkspace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledLaunchPreservesIdentityInEitherOptionOrder(bool reverse)
    {
        string[] arguments = ["--respect-control-setting", "--target-process=42:12345678"];
        var options = PetDanceTrialOptions.Parse(reverse ? arguments.Reverse() : arguments);
        Assert.True(options.RespectControlSetting);
        var target = new GameWindow((nint)1, 42, 12345678, "WizardGraphicalClient", "Wizard101");
        Assert.True(options.ExpectedTarget!.Matches(target));
        Assert.False(options.ExpectedTarget.Matches(target with { ProcessId = 43 }));
        Assert.False(options.ExpectedTarget.Matches(target with { StartedUtcTicks = 12345679 }));
    }

    [Theory]
    [InlineData("--target-process=0:1")]
    [InlineData("--target-process=42:0")]
    [InlineData("--target-process=-42:1")]
    [InlineData("--target-process=42:-1")]
    [InlineData("--target-process=42:1:2")]
    [InlineData("--target-process=42")]
    [InlineData("--target-process= 42:1")]
    [InlineData("--target-process=2147483648:1")]
    [InlineData("--target-process=42:9223372036854775808")]
    [InlineData("--target-process=42:no")]
    [InlineData("--unknown")]
    [InlineData("--respect-control-setting")]
    public void MalformedOrUnboundInstalledOptionsFailBeforeRun(string argument)
        => Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse([argument]));

    [Fact]
    public void DuplicateOptionsAreRejected()
    {
        Assert.False(PetDanceTrialOptions.Parse(["--leave-awake"]).ReturnToNativeIdle);
        Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse(["--leave-awake", "--return-native-idle"]));
        Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse([
            "--respect-control-setting", "--respect-control-setting", "--target-process=42:1"]));
        Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse([
            "--target-process=42:1", "--target-process=42:1"]));
    }

    [Fact]
    public void InvalidExpectedIdentityNeverMatches()
    {
        var target = new GameWindow((nint)1, 0, 0, "WizardGraphicalClient", "Wizard101");
        Assert.False(new PetDanceTargetIdentity(0, 0).Matches(target));
    }
}
