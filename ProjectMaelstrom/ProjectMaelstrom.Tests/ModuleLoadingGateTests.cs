using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ModuleLoadingGateTests
{
    private static readonly ModuleLoadingRequirement Requirement = new("test-loading");
    private static OperationModuleBundle Bundle => W101ModulePrograms.NativeIdle();
    private static RoutineBinding Binding => ModuleScreenSessionTests.Binding(Bundle);
    private static ModuleLoadingObservation Sample(double time, bool? loading) => new(Binding, Requirement.Source, time, loading);
    private static ModuleScreenSession Session() => new(Bundle, Binding, ModuleScreenSessionTests.Geometry,
        W101ModulePrograms.Policy(), "loading-test", Requirement);
    private static ModuleScreenDecision Observe(ModuleScreenSession session, double now, ModuleLoadingObservation? sample, string scene = "graphics-menu") =>
        session.Observe(ModuleScreenSessionTests.Evidence(scene, now, Binding), now + 20, ModuleScreenSessionTests.Authority, sample);

    [Fact]
    public void LoadingDiscardsEarlierStabilityAndClearStillNeedsTwoFreshVisuals()
    {
        var session = Session();
        Assert.False(Observe(session, 0, Sample(0, false)).Decision.IsProposal);
        var paused = Observe(session, 500, Sample(500, true));
        Assert.Equal("Loading", paused.Recognition.Scene); Assert.Empty(paused.Recognition.Controls);
        Assert.False(paused.Decision.IsProposal); Assert.False(paused.Stable);
        Assert.False(Observe(session, 1000, Sample(1000, false)).Decision.IsProposal);
        Assert.True(Observe(session, 1500, Sample(1500, false)).Decision.IsProposal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("nan")]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("visit")]
    [InlineData("pre-read-image")]
    public void InvalidOrUnavailableReadingCannotUseARecognizedMenu(string fault)
    {
        var session = Session(); Observe(session, 0, Sample(0, false));
        ModuleLoadingObservation? sample = Sample(500, false);
        sample = fault switch
        {
            "missing" => null,
            "unknown" => sample with { Loading = null },
            "stale" => sample with { SampledMs = 0 },
            "future" => sample with { SampledMs = 5000 },
            "nan" => sample with { SampledMs = double.NaN },
            "source" => sample with { Source = "other" },
            "target" => sample with { Binding = Binding with { TargetId = "other" } },
            "visit" => sample with { Binding = Binding with { ContextId = Binding.ContextId + "other" } },
            "pre-read-image" => sample with { SampledMs = 510 },
            _ => sample
        };
        var result = Observe(session, fault == "stale" ? 1100 : 500, sample);
        Assert.False(result.Stable); Assert.False(result.Decision.IsProposal); Assert.Null(result.OutcomeForProposalId);
        Assert.Empty(result.Recognition.Controls);
    }

    [Fact]
    public void ClearDoesNotReplaceVisualRecognitionOrGrantContext()
    {
        var session = Session();
        Observe(session, 0, Sample(0, false), "loading");
        Assert.False(Observe(session, 500, Sample(500, false), "loading").Decision.IsProposal);
        var image = ModuleScreenSessionTests.Evidence("graphics-menu", 1000, Binding);
        session.Observe(image, 1020, ModuleScreenSessionTests.Authority with { ContextVerified = false }, Sample(1000, false));
        var result = session.Observe(image with { Frame = image.Frame with { CapturedMs = 1500 } }, 1520,
            ModuleScreenSessionTests.Authority with { ContextVerified = false }, Sample(1500, false));
        Assert.False(result.Decision.IsProposal);
    }

    [Fact]
    public void PendingReceiptSurvivesLoadingButLoadingCannotAcknowledgeIt()
    {
        var session = Session(); Observe(session, 0, Sample(0, false));
        var action = Observe(session, 500, Sample(500, false));
        session.RecordReceipt(new(action.Decision.ProposalId!, action.Decision.ActionId!, 530, true, true));
        var paused = Observe(session, 1000, Sample(1000, true), "native-idle");
        Assert.False(paused.Decision.Completed); Assert.Null(paused.OutcomeForProposalId);
        Assert.False(Observe(session, 1500, Sample(1500, false), "native-idle").Decision.Completed);
        var done = Observe(session, 2000, Sample(2000, false), "native-idle");
        Assert.True(done.Decision.Completed); Assert.Equal(action.Decision.ProposalId, done.OutcomeForProposalId);
        Assert.Equal(1, session.Proposals);
    }

    [Fact]
    public void LoadingDoesNotRenewDeadlineAndTakeoverStillStops()
    {
        var session = Session(); Observe(session, 0, Sample(0, true));
        Assert.True(Observe(session, 15000, Sample(15000, true)).Decision.Stopped);
        session = Session();
        var result = session.Observe(ModuleScreenSessionTests.Evidence("graphics-menu", 0, Binding), 20,
            ModuleScreenSessionTests.Authority with { UserActive = true }, Sample(0, true));
        Assert.Equal("user-takeover", result.Decision.Reason);
    }

    [Fact]
    public void VisualOnlyDefaultIgnoresOptionalReadings()
    {
        var session = new ModuleScreenSession(Bundle, Binding, ModuleScreenSessionTests.Geometry, W101ModulePrograms.Policy());
        Observe(session, 0, Sample(0, true));
        Assert.True(Observe(session, 500, Sample(500, true)).Decision.IsProposal);
    }

    [Fact]
    public void SameConditionAppliesToSharedInteractionModuleWithoutAScreenSession()
    {
        var spec = OperationModules.Spec("dialogue", "sigil", "wizard101",
            new SigilConfiguration("NearbyNpc", "Dialogue", new("game.interact", "keyboard-pulse", "X", 40, "interaction.prompt")));
        var bundle = new OperationModuleBundle(1, "interaction", "wizard101", "interaction.context", 10000, 1, [spec]);
        var binding = bundle.Bind("target", "1280x720", "trial");
        var runner = new ModuleRunner(bundle, binding, loadingRequirement: Requirement);
        ModuleObservation Evidence(double time, string scene, string? outcome = null) =>
            new(new(time, scene, binding, Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true,
                ControlVerified: true, OutcomeForProposalId: outcome), [], ["interaction.prompt"]);
        Assert.False(runner.Observe(Evidence(0, "NearbyNpc"), new(binding, Requirement.Source, 0, true)).IsProposal);
        var action = runner.Observe(Evidence(100, "NearbyNpc"), new(binding, Requirement.Source, 100, false));
        Assert.Equal("game.interact", action.ActionId);
        runner.RecordReceipt(new(action.ProposalId!, action.ActionId!, 110, true, true));
        Assert.False(runner.Observe(Evidence(200, "Dialogue", action.ProposalId), new(binding, Requirement.Source, 200, true)).Completed);
        Assert.True(runner.Observe(Evidence(300, "Dialogue", action.ProposalId), new(binding, Requirement.Source, 300, false)).Completed);
    }

    [Fact]
    public async Task LoadingSampleExpiresBeforeDispatchEvenWhileImageIsFresh()
    {
        var root = Path.Combine(Path.GetTempPath(), "module-loading-input-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var scheduler = new OperationScheduler(root, root);
            var receipt = await ModuleOperation.SubmitOwned(scheduler, Bundle, Binding, async (context, token) =>
            {
                var session = Session(); Observe(session, 500, Sample(0, false));
                var proposal = Observe(session, 900, Sample(0, false));
                Assert.True(proposal.Decision.IsProposal);
                proposal = proposal with { Recognition = proposal.Recognition with { Frame = proposal.Recognition.Frame with { TextOrigin = "local-windows-ocr" } } };
                var input = new ModuleOwnedInput(context, Binding, ModuleScreenSessionTests.Geometry, () => true, () => 1001,
                    (_, _, _) => throw new InvalidOperationException("Expired loading sample must not dispatch."));
                Assert.False((await input.DispatchAsync(proposal, token)).Accepted);
                return OperationOutcome.Completed("Fake transport only.");
            }).Completion;
            Assert.Equal(OperationState.Completed, receipt.State);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
