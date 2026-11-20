using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ModuleScreenSessionTests
{
    internal static readonly Size Geometry = new(1282, 760);
    internal static ModuleScreenAuthority Authority => new(true, true, true, VerifiedFacts: W101ModulePrograms.Policy().ContextFacts);
    internal static RoutineBinding Binding(OperationModuleBundle bundle) => bundle.Bind("recorded-w101", "1282x760", "attempt1");
    internal static ModuleVisualRecognition Evidence(string name, double captured, RoutineBinding binding)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Modules", "W101Menus", "recognition-cases.json")));
        var item = json.RootElement.GetProperty("cases").EnumerateArray().Single(item => item.GetProperty("id").GetString() == name);
        var text = item.GetProperty("text").Deserialize<PetDanceMenuText>(OperationModules.Json)!;
        return W101MenuRecognition.Classify(new(item.GetProperty("source_image_sha256").GetString()!, captured, Geometry, binding, "saved-ocr"), text);
    }
    private static ModuleScreenSession Session(OperationModuleBundle bundle, string run = "test") =>
        new(bundle, Binding(bundle), Geometry, W101ModulePrograms.Policy(), run);
    private static ModuleScreenDecision Pair(ModuleScreenSession session, RoutineBinding binding, string image, double start)
    {
        Assert.False(session.Observe(Evidence(image, start, binding), start + 20, Authority).Decision.IsProposal);
        return session.Observe(Evidence(image, start + 500, binding), start + 520, Authority);
    }
    private static void Receipt(ModuleScreenSession session, ModuleScreenDecision proposal, double time) =>
        session.RecordReceipt(new(proposal.Decision.ProposalId!, proposal.Decision.ActionId!, time, true, true));

    [Fact]
    public void SavedOcrRunsRewardFinishAndIdleWithExactNewScreenOutcomes()
    {
        var bundle = W101ModulePrograms.Postgame(); var binding = Binding(bundle); var session = Session(bundle);
        var next = Pair(session, binding, "reward", 0); Assert.Equal("pet-dance.next", next.Decision.ActionId);
        Receipt(session, next, 530);
        var finish = Pair(session, binding, "snack", 1000); Assert.Equal("pet-dance.finish-without-snack", finish.Decision.ActionId);
        Assert.Equal(next.Decision.ProposalId, finish.OutcomeForProposalId); Receipt(session, finish, 1530);
        Assert.False(session.Observe(Evidence("loading", 1750, binding), 1770, Authority).Decision.IsProposal);
        var open = Pair(session, binding, "outside", 2000); Assert.Equal("game.open-menu", open.Decision.ActionId);
        Assert.Equal("OutsideReady", open.Recognition.Scene); Receipt(session, open, 2530);
        var idle = Pair(session, binding, "graphics-menu", 3000); Assert.Equal("wizard101.native-idle", idle.Decision.ActionId);
        Receipt(session, idle, 3530);
        var done = Pair(session, binding, "native-idle", 4000);
        Assert.True(done.Decision.Completed); Assert.Equal(4, session.Proposals); Assert.Equal(2, session.CompletedModules);
    }

    [Fact]
    public void GenericContextVerificationDoesNotGrantCompletedAndReleasedGameEligibility()
    {
        var bundle = W101ModulePrograms.Postgame(); var binding = Binding(bundle); var session = Session(bundle);
        var authority = Authority with { VerifiedFacts = ["wizard101.idle-context"] };
        session.Observe(Evidence("reward", 0, binding), 20, authority);
        Assert.False(session.Observe(Evidence("reward", 500, binding), 520, authority).Decision.IsProposal);
        Assert.True(session.Observe(Evidence("reward", 1000, binding), 1020, Authority).Decision.IsProposal);
    }

    [Fact]
    public void BareOutsideCandidateNeverGrantsOpenMenuOrNavigation()
    {
        var bundle = W101ModulePrograms.NativeIdle(); var binding = Binding(bundle); var session = Session(bundle);
        var result = Pair(session, binding, "outside", 0);
        Assert.Equal("OutsideCandidate", result.Recognition.Scene); Assert.Empty(result.Recognition.Controls);
        Assert.False(result.Decision.IsProposal);
    }

    [Fact]
    public void OlderOrRepeatedCapturesCannotCreateStabilityOrExtendTheClock()
    {
        var bundle = W101ModulePrograms.NativeIdle(); var binding = Binding(bundle); var session = Session(bundle);
        session.Observe(Evidence("graphics-menu", 200, binding), 200, Authority);
        Assert.False(session.Observe(Evidence("graphics-menu", 100, binding), 210, Authority).Stable);
        Assert.False(session.Observe(Evidence("graphics-menu", 200, binding), 220, Authority).Stable);
        Assert.False(session.Observe(Evidence("graphics-menu", 230, binding), 230, Authority).Decision.IsProposal);
        Assert.True(session.Observe(Evidence("graphics-menu", 240, binding), 240, Authority).Decision.IsProposal);
    }

    [Fact]
    public void BothOutcomeFramesMustPostdateRelease()
    {
        var bundle = W101ModulePrograms.NativeIdle(); var binding = Binding(bundle); var session = Session(bundle);
        var idle = Pair(session, binding, "graphics-menu", 0); Receipt(session, idle, 800);
        Assert.False(session.Observe(Evidence("native-idle", 790, binding), 810, Authority).Decision.Completed);
        Assert.False(session.Observe(Evidence("native-idle", 820, binding), 830, Authority).Decision.Completed);
        Assert.True(session.Observe(Evidence("native-idle", 850, binding), 860, Authority).Decision.Completed);
    }

    [Fact]
    public void SamePixelsAndHashCasingCannotInventAnOutcome()
    {
        var bundle = W101ModulePrograms.NativeIdle(); var binding = Binding(bundle); var session = Session(bundle);
        var idle = Pair(session, binding, "graphics-menu", 0); Receipt(session, idle, 530);
        var image = Evidence("native-idle", 1000, binding);
        image = image with { Frame = image.Frame with { ImageSha256 = idle.Recognition.Frame.ImageSha256.ToUpperInvariant() } };
        session.Observe(image, 1020, Authority);
        Assert.False(session.Observe(image with { Frame = image.Frame with { CapturedMs = 1500 } }, 1520, Authority).Decision.Completed);
    }

    [Fact]
    public void OldAndFutureImagesCannotAdvanceButStillReachFiniteDeadline()
    {
        var bundle = W101ModulePrograms.NativeIdle() with { TimeoutMs = 1000 }; var binding = Binding(bundle); var session = Session(bundle);
        session.Observe(Evidence("graphics-menu", 0, binding), 0, Authority);
        Assert.False(session.Observe(Evidence("graphics-menu", 100000, binding), 200, Authority).Stable);
        Assert.False(session.Observe(Evidence("graphics-menu", 0, binding), 900, Authority).Decision.IsProposal);
        Assert.Equal("bundle-deadline-exceeded", session.Observe(Evidence("graphics-menu", 0, binding), 1000, Authority).Decision.Reason);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("binding")]
    [InlineData("owner")]
    public void LostAuthorityStopsWithoutCleanupActions(string change)
    {
        var bundle = W101ModulePrograms.NativeIdle(); var binding = Binding(bundle); var session = Session(bundle);
        var image = Evidence("graphics-menu", 0, binding);
        if (change == "binding") image = image with { Frame = image.Frame with { Binding = binding with { TargetId = "another-window" } } };
        var result = session.Observe(image, 20, Authority with { UserActive = change == "takeover", InputOwned = change != "owner" });
        Assert.True(result.Decision.Stopped); Assert.Equal(0, session.Proposals);
    }

    [Fact]
    public void NavigationCannotUseUiSceneChangesAsMotionVerification()
    {
        var bundle = W101SharedOperations.RouteAndMenuVisit(new("route", "wizard-city", "commons", "pet-pavilion", "dance",
            [new("walk", "start", "sigil", "step-forward")]));
        Assert.Throws<InvalidDataException>(() => Session(bundle));
    }
}
