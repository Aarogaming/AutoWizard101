using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record W101QuestConversationSelection(string Id, string ObjectiveBefore, string DialogueBefore,
    string? ExpectedObjectiveAfter = null, int MaxPages = 12);

/// <summary>Bounded conversation adapter over the shared MenuModule/ModuleRunner.
/// Pages are events, not a fixed transcript. Each different page needs a released
/// input receipt; an unchanged page is never clicked twice. No input lives here.</summary>
internal sealed class W101QuestConversation
{
    internal const string ActionId = "w101.quest.advance-dialogue-space";
    internal const string ProgressScene = "QuestDialoguePageChanged";
    internal const int TimeoutMs = 60000;
    private readonly W101QuestConversationSelection _selected;
    private readonly WizwalkerTarget _target;
    private readonly RoutineBinding _binding;
    private readonly ModuleRunner _runner;
    private readonly HashSet<string> _pages = new(StringComparer.Ordinal);
    private string? _page, _proposal;
    private bool _accepted;
    public string? ObjectiveAfter { get; private set; }
    public ModuleDecision Status => _runner.Status;
    public int Actions => _runner.ProposedActions;

    public static OperationModuleBundle Compile(W101QuestConversationSelection selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        OperationModules.CheckId(selected.Id);
        if (selected.Id.Length > 60 || !W101QuestStep.ValidText(selected.ObjectiveBefore, 256) ||
            !W101QuestStep.ValidText(selected.DialogueBefore, 2048) || selected.MaxPages is < 1 or > 16 ||
            !W101QuestStep.ValidText(selected.ExpectedObjectiveAfter, 256, optional: true) || selected.ExpectedObjectiveAfter == selected.ObjectiveBefore)
            throw new InvalidDataException("Select the opening dialogue/objective and 1–16 page advances; an optional expected objective must differ.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selected)))).ToLowerInvariant();
        var module = OperationModules.Spec("quest-conversation", "menu", "wizard101",
            new MenuConfiguration([new("advance", W101QuestStep.BeforeScene, W101QuestStep.AfterScene,
                new(ActionId, "keyboard-pulse", "SPACE", 40, W101QuestStep.ControlId))],
                TimeoutMs: TimeoutMs, TransitionTimeoutMs: 15000, ProgressScene: ProgressScene, MaxActions: selected.MaxPages), W101QuestStep.Fact);
        return new(1, "conversation-" + selected.Id, "wizard101", "quest.conversation." + hash, TimeoutMs, selected.MaxPages, [module]);
    }

    public W101QuestConversation(W101QuestConversationSelection selected, WizwalkerTarget target, RoutineBinding binding, string runId)
    {
        var bundle = Compile(selected);
        if (binding.TargetId != W101QuestStep.TargetBinding(target)) throw new InvalidDataException("Conversation target mismatch.");
        _selected = selected; _target = target; _binding = binding; _runner = new(bundle, binding, runId);
    }

    public ModuleDecision Observe(W101UiTreeResult reading, long nowUtcTicks, RoutineObservation authority)
    {
        var ready = authority.Binding == _binding && W101QuestStep.EvidenceReady(_target, reading, nowUtcTicks);
        var context = reading.QuestContext;
        bool State(bool dialogue) => reading.States?.GetValueOrDefault("dialogue") == dialogue && reading.StableStates?.GetValueOrDefault("dialogue") == dialogue;
        bool hasPage = ready && State(true) && context!.AdvanceEnabled == true &&
            W101QuestStep.ValidText(context.DialogueText, 2048) && W101QuestStep.ValidText(context.ObjectiveText, 256);
        bool first = hasPage && _page is null && context!.DialogueText == _selected.DialogueBefore && context.ObjectiveText == _selected.ObjectiveBefore;
        // W101 can turn in the old talk objective before the NPC finishes offering
        // the next quest. Continue only on a new page after our released receipt;
        // retain objective changes as evidence, not a reason to skip remaining pages.
        bool progress = hasPage && _accepted && context!.DialogueText != _page && !_pages.Contains(context.DialogueText!);
        bool finished = ready && _accepted && State(false) && context!.DialogueText is null &&
            W101QuestStep.ValidText(context.ObjectiveText, 256) && context.ObjectiveText != _selected.ObjectiveBefore &&
            (_selected.ExpectedObjectiveAfter is null || context.ObjectiveText == _selected.ExpectedObjectiveAfter);
        var scene = first ? W101QuestStep.BeforeScene : progress ? ProgressScene : finished ? W101QuestStep.AfterScene : reading.Loading == true ? "Loading" : "Unknown";
        var decision = _runner.Observe(new(authority with
        {
            TimeMs = ready ? authority.TimeMs - TimeSpan.FromTicks(nowUtcTicks - reading.StatesObservedUtcTicks).TotalMilliseconds : authority.TimeMs,
            Scene = scene, Fresh = authority.Fresh && ready, ContextVerified = authority.ContextVerified && (first || progress || finished),
            ControlVerified = authority.ControlVerified && hasPage, OutcomeForProposalId = _accepted ? _proposal : null
        }, first || progress || finished ? [W101QuestStep.Fact] : [], hasPage ? [W101QuestStep.ControlId] : []));
        if (decision.IsProposal)
        {
            _page = context!.DialogueText!; _pages.Add(_page); _proposal = decision.ProposalId; _accepted = false;
        }
        if (decision.Completed && finished && ObjectiveAfter is null) ObjectiveAfter = context?.ObjectiveText;
        return decision;
    }

    public void RecordReceipt(RoutineReceipt receipt)
    {
        _runner.RecordReceipt(receipt);
        _accepted = !Status.Stopped && receipt.ProposalId == _proposal && receipt.Accepted && receipt.InputReleased;
    }
}
