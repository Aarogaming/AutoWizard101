using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceSpecialistContractTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dance-specialist-" + Guid.NewGuid().ToString("N"));
    private static readonly GameWindow Target = new((nint)123, 42, 100, "WizardGraphicalClient", "Synthetic target");
    private OperationContext Context(GameInputLease? lease) => new(new("test", "w101.dance-specialist", "v1", 120,
        true, ModuleOwnedInput.TargetId(Target), new Dictionary<string, string>()), _root, lease);

    [Fact]
    public void SpecialistUsesExistingOwnerWithoutAcquiringAnotherLease()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        var context = Context(lease);
        Assert.False(PetDanceSpecialistAdmission.OwnsInput(context, Target));
        using (GameInputLease.EnterScope(lease))
        {
            Assert.True(PetDanceSpecialistAdmission.OwnsInput(context, Target));
            Assert.Same(lease, GameInputLease.Current);
        }
        Assert.False(PetDanceSpecialistAdmission.OwnsInput(context, Target));
    }

    [Theory]
    [InlineData("no-owner")]
    [InlineData("read-only")]
    [InlineData("delegated")]
    [InlineData("wrong-target")]
    public async Task RejectedContextNeverOpensProfileCaptureOrControlCallbacks(string fault)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var context = Context(fault == "no-owner" ? null : lease);
        context = fault switch
        {
            "read-only" => context with { Request = context.Request with { WritesGame = false } },
            "delegated" => context with { Request = context.Request with { DelegatedInput = true } },
            "wrong-target" => context with { Request = context.Request with { Target = "another-target" } },
            _ => context
        };
        var calls = 0;
        var control = new PetDanceSpecialistControl(1, 0, () => { calls++; return true; },
            () => { calls++; return true; }, () => { calls++; return 0; },
            () => { calls++; return true; }, () => { calls++; return 500; });
        await Assert.ThrowsAsync<InvalidOperationException>(() => PetDanceLiveTrial.RunSpecialistAsync(context,
            Target, new(1280, 720), Path.Combine(_root, "absent-profile.json"), Path.Combine(_root, "no-output"), control,
            CancellationToken.None));
        Assert.Equal(0, calls);
        Assert.False(Directory.Exists(Path.Combine(_root, "no-output")));
    }

    [Fact]
    public async Task CancelledSpecialistStopsBeforeCaptureOrProfileRead()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var control = new PetDanceSpecialistControl(1, 0, () => true, () => true, () => 0, () => true, () => 500);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PetDanceLiveTrial.RunSpecialistAsync(Context(null),
            Target, new(1280, 720), "absent", _root, control, cancelled.Token));
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData("five-results-observed", 5, 0, true, true)]
    [InlineData("five-results-observed", 3, 2, true, false)]
    [InlineData("three-failures-observed", 0, 3, true, false)]
    [InlineData("duration-limit", 5, 0, false, false)]
    [InlineData("cancelled", 5, 0, false, false)]
    [InlineData("five-results-observed", 4, 0, false, false)]
    [InlineData("five-results-observed", 6, -1, false, false)]
    public void CleanEndAndPerfectRoundFeedbackAreNotVerifiedGameVictory(string reason, int successes,
        int failures, bool ended, bool allSuccess)
    {
        var result = new PetDanceSpecialistResult("report.json", true, true, 1, true, reason, successes, failures);
        Assert.Equal(ended, result.GameEnded);
        Assert.Equal(ended, result.MayContinuePostGame);
        Assert.Equal(allSuccess, result.AllRoundFeedbackSuccessful);
        Assert.False(result.GameVictoryVerified);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void UncertainCleanupOrUnarmedTrialNeverPermitsPostgame(bool cleanup, bool release, bool armed)
    {
        var result = new PetDanceSpecialistResult("report.json", cleanup, release, 1, armed,
            "five-results-observed", 5, 0);
        Assert.False(result.MayContinuePostGame);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
