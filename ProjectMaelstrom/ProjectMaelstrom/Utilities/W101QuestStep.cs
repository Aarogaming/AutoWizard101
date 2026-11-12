using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Optional game-owned text, never chat. Null is unknown/unavailable.</summary>
internal sealed record W101QuestContext(string? ObjectiveText, string? DialogueText, bool? AdvanceEnabled);
/// <summary>One selected objective transition, not a quest chain or permission.
/// The expected objective is explicit; merely closing dialogue is not success.</summary>
internal sealed record W101QuestStepSelection(string Id, string ObjectiveBefore, string DialogueBefore, string ObjectiveAfter);

/// <summary>Projects qualified quest evidence into the existing menu module.
/// No new scheduler, input transport, hook, retry loop or cleanup action.</summary>
internal static class W101QuestStep
{
    internal const string ActionId = "w101.quest.advance-dialogue";
    internal const string ControlId = "quest.dialogue.advance";
    internal const string Fact = "quest.selected-step";
    internal const string BeforeScene = "SelectedQuestDialogue";
    internal const string AfterScene = "SelectedQuestObjectiveObserved";
    internal const int MaximumAgeMs = 2000;

    internal static OperationModuleBundle Compile(W101QuestStepSelection selected)
    {
        Validate(selected);
        var spec = OperationModules.Spec("selected-quest-dialogue", "menu", "wizard101",
            new MenuConfiguration([new("advance-once", BeforeScene, AfterScene,
                new(ActionId, "verified-control-click", null, 40, ControlId))], TimeoutMs: 15000, TransitionTimeoutMs: 10000), Fact);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selected)))).ToLowerInvariant();
        return new(1, "quest-step-" + selected.Id, "wizard101", "quest." + selected.Id + "." + fingerprint, 15000, 1, [spec]);
    }

    internal static string TargetBinding(WizwalkerTarget target) => FormattableString.Invariant($"w101.{target.ProcessId}.{target.StartedUtcTicks}.{target.Handle}");

    /// <summary>The caller supplies current target/owner/takeover authority. Text
    /// alone never grants any of it. Use the same visit binding before and after.
    /// After an accepted/released receipt, pass its proposal ID only for a newly
    /// collected observation; ModuleRunner enforces receipt timing and identity.</summary>
    internal static ModuleObservation Project(W101QuestStepSelection selected, WizwalkerTarget target,
        W101UiTreeResult reading, long nowUtcTicks, RoutineObservation authority)
    {
        Validate(selected);
        var context = reading.QuestContext;
        bool State(string key, bool expected) => reading.States?.GetValueOrDefault(key) == expected &&
            reading.StableStates?.GetValueOrDefault(key) == expected;
        bool ready = authority.Binding.TargetId == TargetBinding(target) &&
            authority.Binding.ContextId.StartsWith(Compile(selected).ContextId + ".visit.", StringComparison.Ordinal) &&
            EvidenceReady(target, reading, nowUtcTicks);
        bool before = ready && State("dialogue", true) && context!.AdvanceEnabled == true &&
            context.ObjectiveText == selected.ObjectiveBefore && context.DialogueText == selected.DialogueBefore;
        // A new objective is required, and the dialogue must have actually closed.
        // A new dialogue page, a vendor panel or an arbitrary changed objective cannot pass.
        bool after = ready && State("dialogue", false) && context!.DialogueText is null &&
            context.ObjectiveText == selected.ObjectiveAfter;
        return new(authority with
        {
            // Preserve acquisition time, not the time a caller replays an old read.
            // The shared runner rejects outcome samples predating its input receipt.
            TimeMs = ready ? authority.TimeMs - TimeSpan.FromTicks(nowUtcTicks - reading.StatesObservedUtcTicks).TotalMilliseconds : authority.TimeMs,
            Scene = before ? BeforeScene : after ? AfterScene : reading.Loading == true ? "Loading" : "Unknown",
            Fresh = authority.Fresh && ready,
            ContextVerified = authority.ContextVerified && (before || after),
            ControlVerified = authority.ControlVerified && before
        }, before || after ? [Fact] : [], before ? [ControlId] : []);
    }

    internal static bool EvidenceReady(WizwalkerTarget target, W101UiTreeResult reading, long nowUtcTicks) =>
            reading.Target == target && reading.Operation == W101UiTreeOperation.Version && reading.SchemaVersion == 1 &&
            reading.SourceSha256 == WizwalkerOperation.SourceSha256 && reading.Status == "observed" &&
            reading.CleanupConfirmed && reading.HookRestored && reading.TreeSamples >= 2 &&
            reading.Loading == false && W101UiTreeOperation.ValidQuestContext(reading, true, nowUtcTicks) &&
            reading.QuestContext is not null && reading.QuestContext == reading.StableQuestContext &&
            nowUtcTicks >= reading.ObservedUtcTicks && nowUtcTicks - reading.ObservedUtcTicks <= TimeSpan.FromMilliseconds(MaximumAgeMs).Ticks &&
            nowUtcTicks - reading.StatesObservedUtcTicks <= TimeSpan.FromMilliseconds(MaximumAgeMs).Ticks &&
            reading.States?.GetValueOrDefault("menu") == false && reading.StableStates?.GetValueOrDefault("menu") == false &&
            reading.States?.GetValueOrDefault("nativeIdle") == false && reading.StableStates?.GetValueOrDefault("nativeIdle") == false;

    internal static bool ValidText(string? text, int limit, bool optional = false) =>
        text is null ? optional : !string.IsNullOrWhiteSpace(text) && text.Length <= limit &&
            text == text.Trim() && !text.Any(char.IsControl);

    private static void Validate(W101QuestStepSelection selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        OperationModules.CheckId(selected.Id);
        if (selected.Id.Length > 60 || !ValidText(selected.ObjectiveBefore, 256) || !ValidText(selected.ObjectiveAfter, 256) ||
            !ValidText(selected.DialogueBefore, 2048) || selected.ObjectiveBefore == selected.ObjectiveAfter)
            throw new InvalidDataException("Choose one identified dialogue and distinct expected before/after objective text. Unknown or unchanged objectives cannot verify quest progress.");
    }
}
