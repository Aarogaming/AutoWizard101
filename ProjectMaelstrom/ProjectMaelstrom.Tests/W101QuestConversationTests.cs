using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101QuestConversationTests
{
    [Fact]
    public void SavedLiveOfferReplaysItsObservedPageOrderAndReceiptsOffline()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "QuestConversation", "quinn-offer.json")));
        var fixture = document.RootElement;
        Assert.Equal(64, fixture.GetProperty("sourceReportSha256").GetString()!.Length);
        var selected = fixture.GetProperty("selection").Deserialize<W101QuestConversationSelection>(MaelstromCli.Json)!;
        var binding = Binding(selected);
        var run = new W101QuestConversation(selected, Target, binding, "saved-replay");
        double time = 1000;
        ModuleDecision? proposal = null;
        foreach (var entry in fixture.GetProperty("events").EnumerateArray())
        {
            if (entry.GetProperty("kind").GetString() == "input-receipt")
            {
                Assert.True(proposal?.IsProposal);
                run.RecordReceipt(new(proposal!.ProposalId!, proposal.ActionId!, time,
                    entry.GetProperty("accepted").GetBoolean(), entry.GetProperty("inputReleased").GetBoolean()));
            }
            else
            {
                var context = entry.GetProperty("context").Deserialize<W101QuestContext>(MaelstromCli.Json)!;
                var states = entry.GetProperty("states").Deserialize<Dictionary<string, bool?>>(MaelstromCli.Json)!;
                var read = Read(time, context.DialogueText, context.ObjectiveText) with { QuestContext = context, StableQuestContext = context, States = states, StableStates = new(states) };
                proposal = run.Observe(read, Stamp(time), new(time, "Ignored", binding, true, true, true, true, true));
            }
            time += 1000; // Explicit synthetic replay clock; this test does not measure latency.
        }
        Assert.True(run.Status.Completed); Assert.Equal(3, run.Actions);
        Assert.Equal("Go To Fireglobe Theatre in Firecat Alley", run.ObjectiveAfter);
    }

    internal static readonly W101QuestConversationSelection Selected = new("quinn", "Talk To Private Quinn in Firecat Alley", "Opening page");
    internal static readonly WizwalkerTarget Target = new(42, 123, 100, "WizardGraphicalClient", "inert", new string('a', 64));
    internal static readonly long Epoch = DateTime.UtcNow.Ticks;
    internal static long Stamp(double time) => Epoch + (long)(time * TimeSpan.TicksPerMillisecond);
    internal static W101UiTreeResult Read(double time, string? page, string? objective = null)
    {
        var states = new Dictionary<string, bool?> { ["dialogue"] = page != null, ["menu"] = false, ["nativeIdle"] = false, ["interactionPrompt"] = false };
        var context = new W101QuestContext(objective ?? Selected.ObjectiveBefore, page, page != null ? true : null);
        return new(1, "fixture", W101UiTreeOperation.Version, WizwalkerOperation.SourceSha256, Target, "observed", "fixture", false,
            states, Stamp(time), Stamp(time), 2000, true, true, 8, 2, 0, new(states), context, context);
    }
    private static RoutineBinding Binding(W101QuestConversationSelection? selection = null) =>
        W101QuestConversation.Compile(selection ?? Selected).Bind(W101QuestStep.TargetBinding(Target), "tree", "test");
    private static ModuleDecision Observe(W101QuestConversation run, double time, string? text, string? goal = null) =>
        run.Observe(Read(time, text, goal), Stamp(time), new(time, "Ignored", Binding(), true, true, true, true, true));

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void VariableLengthConversationUsesChangedPagesAndStopsAfterObjective(int pages)
    {
        var run = new W101QuestConversation(Selected, Target, Binding(), "run");
        var decision = Observe(run, 1000, Selected.DialogueBefore);
        for (int i = 0; i < pages; i++)
        {
            Assert.True(decision.IsProposal);
            run.RecordReceipt(new(decision.ProposalId!, decision.ActionId!, 1001 + i * 1000, true, true));
            decision = Observe(run, 2000 + i * 1000, i == pages - 1 ? null : "Different page " + (pages - i), i == pages - 1 ? "Defeat the test enemies" : null);
        }
        Assert.True(decision.Completed); Assert.Equal(pages, run.Actions);
        Assert.Equal("Defeat the test enemies", run.ObjectiveAfter);
        Assert.False(Observe(run, 20000, "New quest").IsProposal);
    }

    [Theory]
    [InlineData("same-page")]
    [InlineData("closed-without-objective")]
    [InlineData("missing-objective")]
    public void MissingOutcomeDoesNotRetryOrClaimQuestProgress(string fault)
    {
        var run = new W101QuestConversation(Selected, Target, Binding(), "run");
        var proposal = Observe(run, 1000, Selected.DialogueBefore);
        run.RecordReceipt(new(proposal.ProposalId!, proposal.ActionId!, 1001, true, true));
        var read = fault switch
        {
            "same-page" => Read(2000, Selected.DialogueBefore),
            "closed-without-objective" => Read(2000, null),
            _ => Read(2000, null) with { QuestContext = new(null, null, null), StableQuestContext = new(null, null, null) }
        };
        var decision = run.Observe(read, Stamp(2000), new(2000, "Ignored", Binding(), true, true, true, true, true));
        Assert.False(decision.IsProposal); Assert.False(decision.Completed);
        Assert.True(Observe(run, 61001, null).Stopped);
        Assert.Equal(1, run.Actions); Assert.Null(run.ObjectiveAfter);
    }

    [Fact]
    public void TalkObjectiveCanTurnInBeforeNextQuestOfferFinishes()
    {
        var run = new W101QuestConversation(Selected, Target, Binding(), "run");
        var first = Observe(run, 1000, Selected.DialogueBefore);
        run.RecordReceipt(new(first.ProposalId!, first.ActionId!, 1001, true, true));
        var offer = Observe(run, 2000, "I was planning to meet some Fire Elf friends here, but they didn't show up.", "Explore Dark Cave in Wizard City");
        Assert.True(offer.IsProposal); Assert.False(offer.Completed); Assert.Null(run.ObjectiveAfter);
        run.RecordReceipt(new(offer.ProposalId!, offer.ActionId!, 2001, true, true));
        Assert.True(Observe(run, 3000, null, "Newly accepted quest objective").Completed);
        Assert.Equal("Newly accepted quest objective", run.ObjectiveAfter);
    }

    [Fact]
    public void PageBudgetAndRejectedReceiptPreventMoreInput()
    {
        var selected = Selected with { MaxPages = 1 };
        var binding = Binding(selected);
        var run = new W101QuestConversation(selected, Target, binding, "run");
        var first = run.Observe(Read(1000, selected.DialogueBefore), Stamp(1000), new(1000, "Ignored", binding, true, true, true, true, true));
        run.RecordReceipt(new(first.ProposalId!, first.ActionId!, 1001, true, true));
        var last = run.Observe(Read(2000, "New page"), Stamp(2000), new(2000, "Ignored", binding, true, true, true, true, true));
        Assert.True(last.Stopped); Assert.Equal(1, run.Actions);
        var other = new W101QuestConversation(Selected, Target, Binding(), "other");
        var proposal = Observe(other, 1000, Selected.DialogueBefore);
        other.RecordReceipt(new(proposal.ProposalId!, proposal.ActionId!, 1001, false, true));
        Assert.True(Observe(other, 2000, "Changed anyway").Stopped);
    }

    [Fact]
    public void FullConversationRequiresExactInitialSelectionAndCurrentAuthority()
    {
        var run = new W101QuestConversation(Selected, Target, Binding(), "run");
        Assert.False(Observe(run, 1000, "Unselected page").IsProposal);
        var takeover = run.Observe(Read(2000, Selected.DialogueBefore), Stamp(2000),
            new(2000, "Ignored", Binding(), true, true, true, true, true, UserActive: true));
        Assert.True(takeover.Stopped); Assert.False(Observe(run, 3000, Selected.DialogueBefore).IsProposal);
    }
}
