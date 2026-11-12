using System.Globalization;
using System.Text;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDancePostGameScene(PetDanceScene Scene, RectangleF? ActionBounds, string Reason);

/// <summary>Conservative, scale-relative recognition of slow postgame menus from local OCR.
/// A scene alone neither associates a game with this visit nor authorizes input.</summary>
internal static class PetDancePostGameClassifier
{
    public static PetDancePostGameScene Classify(PetDanceMenuText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.ImageSize.Width <= 0 || text.ImageSize.Height <= 0 || text.Words is null || text.Words.Count > 512)
            return Unknown("invalid-text-frame");
        if (text.Words.Any(word => word is null || word.Text is null || word.Text.Length > 128 ||
            !ValidBounds(word.Bounds, text.ImageSize))) return Unknown("invalid-word-bounds");
        var words = Deduplicate(text.Words.Select(word => word with { Text = Normalize(word.Text) })
            .Where(word => word.Text.Length > 0));
        RectangleF Region(float x, float y, float width, float height) => new(x * text.ImageSize.Width,
            y * text.ImageSize.Height, width * text.ImageSize.Width, height * text.ImageSize.Height);
        var header = Region(.25f, .12f, .5f, .4f);
        var controls = Region(.15f, .45f, .7f, .5f);
        var bottom = Region(.25f, .8f, .5f, .19f);
        RectangleF? Phrase(string phrase, RectangleF region) => UniquePhrase(words, phrase.Split(' '), region);

        // Native Idle retains the sigil underneath, so its explicit banner takes precedence.
        if (Phrase("YOU ARE IDLE", Region(.25f, .025f, .5f, .2f)) is not null)
            return new(PetDanceScene.NativeIdle, null, "native-idle-banner");

        var next = Phrase("NEXT", controls);
        if (Phrase("YOUR REWARD", header) is not null && next is not null)
            return new(PetDanceScene.GameResult, next, "reward-header-and-next");

        var finish = Phrase("FINISH", controls);
        var feedHeader = Phrase("FEED", header);
        var petHeader = Phrase("PET", header);
        // The middle word in the recorded header is often YOIR/YOTN. Only that word
        // is optional; FEED and PET must still form one localized, ordered header.
        if (feedHeader is not null && petHeader is not null && finish is not null &&
            SameRow([feedHeader.Value, petHeader.Value]) && petHeader.Value.Left >= feedHeader.Value.Right &&
            petHeader.Value.Left - feedHeader.Value.Right <= Math.Max(feedHeader.Value.Height, petHeader.Value.Height) * 8)
            return new(PetDanceScene.SnackOffer, finish, "snack-offer-and-finish");

        var idle = Phrase("IDLE", controls);
        var help = Phrase("HELP", controls);
        var ok = Phrase("OK", controls);
        if (Phrase("GRAPHICS", header) is not null && idle is not null && help is not null && ok is not null &&
            SameRow([idle.Value, help.Value, ok.Value]) && idle.Value.Right < help.Value.Left && help.Value.Right < ok.Value.Left)
            return new(PetDanceScene.MenuObserved, idle, "graphics-menu-and-idle-control");

        var press = Phrase("PRESS", bottom);
        var play = Phrase("TO PLAY", bottom);
        // Tiny UI's decorative sigil title and boxed X are unreadable in the fixtures.
        // This is only an outside *candidate*: the coordinator may use it solely after
        // this visit's verified Reward -> Snack -> acknowledged Finish transition.
        var modalHeader = Phrase("YOUR REWARD", header) is not null || feedHeader is not null ||
            Phrase("GRAPHICS", header) is not null || Phrase("DANCE GAME", header) is not null;
        if (!modalHeader && press is not null && play is not null && SameRow([press.Value, play.Value]) &&
            play.Value.Left > press.Value.Right && play.Value.Left - press.Value.Right <= Math.Max(press.Value.Height, play.Value.Height) * 10)
            return new(PetDanceScene.OutsideAfterGame, null, "bottom-interaction-prompt-requires-finish-transition");
        return Unknown("unrecognized-postgame-screen");
    }

    internal static bool ValidBounds(RectangleF bounds, Size image) =>
        float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) &&
        float.IsFinite(bounds.Height) && bounds.X >= 0 && bounds.Y >= 0 && bounds.Width > 0 && bounds.Height > 0 &&
        bounds.Right <= image.Width && bounds.Bottom <= image.Height;

    private static string Normalize(string text)
    {
        string decomposed;
        try { decomposed = text.Normalize(NormalizationForm.FormD); }
        catch (ArgumentException) { return string.Empty; }
        var normalized = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
            .Select(char.ToUpperInvariant).ToArray());
        return normalized == "0K" ? "OK" : normalized;
    }
    private static PetDancePostGameScene Unknown(string reason) => new(PetDanceScene.Unknown, null, reason);
    private static PetDanceMenuWord[] Deduplicate(IEnumerable<PetDanceMenuWord> words)
    {
        var unique = new List<PetDanceMenuWord>();
        foreach (var word in words)
        {
            if (unique.Any(existing =>
                {
                    if (existing.Text != word.Text) return false;
                    var overlap = RectangleF.Intersect(existing.Bounds, word.Bounds);
                    var smaller = Math.Min(existing.Bounds.Width * existing.Bounds.Height, word.Bounds.Width * word.Bounds.Height);
                    return overlap.Width > 0 && overlap.Height > 0 && overlap.Width * overlap.Height >= smaller * .6f;
                })) continue;
            unique.Add(word);
        }
        return unique.ToArray();
    }
    private static bool SameRow(RectangleF[] bounds) => bounds.Max(box => box.Top + box.Height / 2) -
        bounds.Min(box => box.Top + box.Height / 2) <= bounds.Max(box => box.Height) * .8f;

    private static RectangleF? UniquePhrase(PetDanceMenuWord[] words, string[] phrase, RectangleF region)
    {
        var eligible = words.Where(word => region.Contains(word.Bounds.Left + word.Bounds.Width / 2,
            word.Bounds.Top + word.Bounds.Height / 2)).ToArray();
        var matches = new List<RectangleF>();
        foreach (var first in eligible.Where(word => word.Text == phrase[0]))
        {
            var previous = first.Bounds;
            var combined = previous;
            var found = true;
            foreach (var term in phrase.Skip(1))
            {
                var successors = eligible.Where(word => word.Text == term &&
                    word.Bounds.Left >= previous.Right - Math.Min(previous.Height, word.Bounds.Height) * .15f &&
                    word.Bounds.Left - previous.Right <= Math.Max(previous.Height, word.Bounds.Height) * 3.5f &&
                    SameRow([previous, word.Bounds])).ToArray();
                if (successors.Length != 1) { found = false; break; }
                previous = successors[0].Bounds;
                combined = RectangleF.Union(combined, previous);
            }
            if (found) matches.Add(combined);
        }
        return matches.Count == 1 ? matches[0] : null;
    }
}

internal sealed record PetDancePostGameOptions(string TargetId, Size Geometry, string ContextId,
    bool Enabled = false, double TimeoutMs = 45000);
internal sealed record PetDancePostGameObservation(double TimeMs, PetDancePostGameScene Scene,
    string TargetId, Size Geometry, string ContextId, bool Fresh = false, bool TargetReady = false,
    bool ContextVerified = false, bool InputOwned = false, bool GameplayCompleted = false,
    bool GameplayInputReleased = false, bool UserActive = false, bool Cancelled = false);
internal sealed record PetDancePostGamePlan(PetDanceLifecycleAction Action, bool IsProposal, string Reason,
    RectangleF? ActionBounds = null, bool ResultObserved = false, bool Completed = false, bool Stopped = false);

/// <summary>Postgame-only continuation after a real teacher completes and releases its keys.
/// It preserves gameplay result evidence independently from cleanup, never feeds, and does not
/// infer or fabricate the Play/Watch milestones required by a full PetDanceLifecycle visit.
/// Call RecordDispatch for every proposed action; then supply a newer observed outcome.
/// The caller must recheck the shared lease, target, freshness and user activity at dispatch.</summary>
internal sealed class PetDancePostGame
{
    private static readonly IReadOnlyDictionary<string, PetDanceLifecycleAction> Actions =
        new Dictionary<string, PetDanceLifecycleAction>(StringComparer.Ordinal)
        {
            ["pet-dance.next"] = PetDanceLifecycleAction.Next,
            ["pet-dance.finish-without-snack"] = PetDanceLifecycleAction.FinishWithoutSnack,
            ["game.open-menu"] = PetDanceLifecycleAction.OpenMenu,
            ["wizard101.native-idle"] = PetDanceLifecycleAction.SelectIdle
        };
    private readonly PetDancePostGameOptions _options;
    private readonly RoutineRunner _runner;
    private RoutineDecision? _proposal;
    private double _lastTime = -1;
    private bool _resultObserved;

    public PetDancePostGame(PetDancePostGameOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.TargetId) || string.IsNullOrWhiteSpace(options.ContextId) ||
            options.Geometry.Width <= 0 || options.Geometry.Height <= 0 || !double.IsFinite(options.TimeoutMs) ||
            options.TimeoutMs <= 0 || options.TimeoutMs > 180000)
            throw new ArgumentException("Bind a target, geometry and context with a bounded cleanup deadline.", nameof(options));
        _options = options;
        var graph = RoutineGraphs.BuiltIn("pet-dance-postgame");
        graph = graph with
        {
            TimeoutMs = options.TimeoutMs,
            Nodes = graph.Nodes.Select(node => node with
            {
                Edges = node.Edges.Select(edge => edge with { TimeoutMs = options.TimeoutMs }).ToArray()
            }).ToArray()
        };
        _runner = new(graph, Bind(options.TargetId, options.Geometry, options.ContextId), Actions.Keys.ToHashSet(StringComparer.Ordinal));
    }

    public PetDancePostGamePlan Observe(PetDancePostGameObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(observation.Scene);
        if (!_options.Enabled) return new(PetDanceLifecycleAction.Wait, false, "postgame-disabled", Stopped: true);
        var completedGame = observation.GameplayCompleted && observation.GameplayInputReleased;
        var decision = _runner.Observe(new(observation.TimeMs, observation.Scene.Scene.ToString(),
            Bind(observation.TargetId, observation.Geometry, observation.ContextId),
            Fresh: observation.Fresh, TargetReady: observation.TargetReady,
            ContextVerified: observation.ContextVerified && completedGame, InputOwned: observation.InputOwned,
            ControlVerified: observation.Scene.ActionBounds is { } bounds &&
                PetDancePostGameClassifier.ValidBounds(bounds, _options.Geometry),
            Cancelled: observation.Cancelled, UserActive: observation.UserActive));
        if (double.IsFinite(observation.TimeMs)) _lastTime = Math.Max(_lastTime, observation.TimeMs);
        var action = decision.ActionId is { } id ? Actions[id] :
            decision.Completed ? PetDanceLifecycleAction.Done : PetDanceLifecycleAction.Wait;
        if (decision.IsProposal)
        {
            _proposal = decision;
            if (action == PetDanceLifecycleAction.Next) _resultObserved = true;
        }
        var reason = decision.Reason switch
        {
            "routine-deadline-exceeded" => "postgame-deadline-exceeded",
            "routine-cancelled" => "postgame-cancelled",
            "observed-completion" => "result-and-native-idle-observed",
            "observation-not-ready" when !completedGame => "completed-game-evidence-required",
            _ => decision.Reason
        };
        return new(action, decision.IsProposal, reason,
            decision.IsProposal ? observation.Scene.ActionBounds : null, _resultObserved, decision.Completed, decision.Stopped);
    }

    /// <summary>The runtime supplies the actual release time so queued pre-click
    /// images cannot count as post-click evidence. Omit only in synthetic replay.</summary>
    public void RecordDispatch(PetDanceLifecycleAction action, bool accepted, bool inputReleased, double? timeMs = null)
    {
        var id = Actions.FirstOrDefault(pair => pair.Value == action).Key ?? "unregistered";
        _runner.RecordReceipt(new(_proposal?.ProposalId ?? "unsolicited", id,
            timeMs ?? _lastTime, accepted, inputReleased));
        _proposal = null;
    }

    private static RoutineBinding Bind(string target, Size geometry, string context) =>
        new(target, FormattableString.Invariant($"{geometry.Width}x{geometry.Height}"), context);
}
