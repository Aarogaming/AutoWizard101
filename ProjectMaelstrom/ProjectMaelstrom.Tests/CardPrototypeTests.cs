using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class CardPrototypeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "card-prototype-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, JobRoutine> _routines = JobDeckRoutines.Create().ToDictionary(routine => routine.Info.Id);
    private string Graph => Path.Combine(_root, "graph.json");
    private string Session => Path.Combine(_root, "session.json");
    private string Game => RoutineGraphs.BuiltIn("dodge-movement-draft").GameId;
    public CardPrototypeTests()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "Routines", "dodge");
        File.Copy(Path.Combine(source, "session.json"), Session);
        File.Copy(Path.Combine(source, "events.jsonl"), Path.Combine(_root, "events.jsonl"));
        File.WriteAllText(Graph, JsonSerializer.Serialize(RoutineGraphs.BuiltIn("dodge-movement-draft"), RoutineGraphs.Json));
    }
    [Theory]
    [InlineData(1280, 680)]
    [InlineData(1024, 720)]
    public void WorkshopWindowFitsTheActiveDisplayWorkingArea(int width, int height)
    {
        var size = CardPrototypeForm.FitToWorkingArea(new(width, height));
        Assert.InRange(size.Width, 640, width);
        Assert.InRange(size.Height, 520, height);
    }

    private CardPrototypeDocument Replay()
    {
        var document = CardPrototype.Create("review.routine", Game, "My Dodge experiment", _routines);
        return document with { Card = document.Card with { Settings = new() { ["graphFile"] = Graph, ["sessionFile"] = Session } } };
    }
    [Fact]
    public void NewPrototypeUsesCatalogDefaultsAndNeverAcquiresInput()
    {
        var document = CardPrototype.Create("w101.pet.dance", "wizard101", "My Dance card", _routines);
        Assert.False(document.Card.InDeck);
        Assert.Equal("1", document.Card.Settings!["level"]);
        Assert.Equal("none", document.Card.Settings["snacks"]);
        Assert.Empty(CardPrototype.History(_root, document.Card.Id));
        Assert.False(Directory.Exists(Path.Combine(_root, "card-prototype-tests")));
        Assert.Null(GameInputLease.Current);
        Assert.Throws<InvalidDataException>(() => CardPrototype.Create("unregistered.script", Game, "Unknown action", _routines));
    }
    [Fact]
    public void SaveReopenPreservesConfigurationAndExistingVersions()
    {
        var document = Replay();
        document = document with { Card = document.Card with
        {
            Runs = 2, AttemptLimit = 4, CooldownSeconds = 10,
            Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(8))],
            Filters = [new("area", ["My Home"], ["Bazaar"])]
        } };
        var file = Path.Combine(_root, "prototype.json");
        CardPrototype.Save(file, document, _routines);
        var reopened = CardPrototype.Load(file, _routines);
        Assert.Equal(RecordedCardReview.Hash(document.Card), RecordedCardReview.Hash(reopened.Card));
        Assert.Equal(document.GameId, reopened.GameId);
        Assert.False(reopened.Card.InDeck);
        Assert.Throws<IOException>(() => CardPrototype.Save(file, document, _routines));
        Assert.Equal(RecordedCardReview.Hash(document.Card), RecordedCardReview.Hash(CardPrototype.Load(file, _routines).Card));
    }
    [Fact]
    public void ImportRejectsStageClaimsDealtCardsAndDuplicateFields()
    {
        var document = Replay(); var file = Path.Combine(_root, "import.json");
        var json = JsonSerializer.SerializeToNode(document, JobDeckRules.Json)!;
        json["qualification"] = "Repeatable";
        File.WriteAllText(file, json.ToJsonString());
        Assert.Throws<JsonException>(() => CardPrototype.Load(file, _routines));
        Assert.Throws<InvalidDataException>(() => CardPrototype.Validate(document with { Card = document.Card with { InDeck = true } }, _routines));
        var raw = JsonSerializer.Serialize(document, JobDeckRules.Json);
        File.WriteAllText(file, raw.Replace("{", "{\"schemaVersion\":1,", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => CardPrototype.Load(file, _routines));
    }
    [Fact]
    public void ExactConfiguredReplayPersistsAndRechecksEvidenceWithoutExecutionPermission()
    {
        var document = Replay();
        var result = CardPrototype.Test(_root, document, _routines);
        Assert.Equal(CardQualificationStage.ReplayPassed, result.Qualification.Stage);
        Assert.False(result.Qualification.GrantsExecutionPermission);
        Assert.Equal(result.Attempt.Id, Assert.Single(CardPrototype.History(_root, document.Card.Id)).Id);
        var status = CardPrototype.ReviewHistory(_root, document, result.Attempt.Id, _routines);
        Assert.Equal(CardQualificationStage.ReplayPassed, status.Stage);
        Assert.False(status.GrantsExecutionPermission);
        Assert.False(document.Card.InDeck);
        Assert.Null(GameInputLease.Current);
    }
    [Fact]
    public void ChangedCardOrConfiguredGraphInvalidatesOldAttemptWithoutErasingIt()
    {
        var document = Replay(); var result = CardPrototype.Test(_root, document, _routines);
        var changed = document with { Card = document.Card with { CooldownSeconds = 20 } };
        Assert.Equal(CardQualificationStage.Unqualified, CardPrototype.ReviewHistory(_root, changed, result.Attempt.Id, _routines).Stage);
        File.AppendAllText(Graph, " ");
        Assert.Equal(CardQualificationStage.Unqualified, CardPrototype.ReviewHistory(_root, document, result.Attempt.Id, _routines).Stage);
        Assert.Single(CardPrototype.History(_root, document.Card.Id));
        Assert.True(File.Exists(Path.Combine(result.ReportDirectory, "evidence", "evidence.json")));
    }
    [Fact]
    public void AnotherCatalogActionCannotBorrowPassingGraphToQualify()
    {
        var document = CardPrototype.Create("w101.pet.dance", "wizard101", "My Dance card", _routines);
        var error = Assert.Throws<InvalidOperationException>(() => CardPrototype.Test(_root, document, _routines));
        Assert.Contains("no prototype replay association", error.Message);
        Assert.Empty(CardPrototype.History(_root, document.Card.Id));
        Assert.Null(GameInputLease.Current);
    }
    [Fact]
    public void MissingPathsAndWrongWorkspaceRejectBeforeHistoryWrites()
    {
        var empty = CardPrototype.Create("review.routine", Game, "Empty replay", _routines);
        Assert.Throws<InvalidDataException>(() => CardPrototype.Test(_root, empty, _routines));
        var wrong = Replay() with { GameId = "wizard101" };
        Assert.NotEqual("wizard101", Game);
        Assert.Throws<InvalidDataException>(() => CardPrototype.Test(_root, wrong, _routines));
        Assert.False(Directory.Exists(Path.Combine(_root, "card-prototype-tests")));
    }
    [Fact]
    public void UnobservedRequirementsAndMissingReceiptsRemainUnqualified()
    {
        var document = Replay();
        var requirements = document with { Card = document.Card with { Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(8))] } };
        var blocked = CardPrototype.Test(_root, requirements, _routines);
        Assert.Equal(CardQualificationStage.Unqualified, blocked.Qualification.Stage);
        Assert.Single(CardPrototype.History(_root, document.Card.Id));
        var events = Path.Combine(_root, "events.jsonl");
        File.WriteAllLines(events, File.ReadAllLines(events).Where(line => !line.Contains("action_receipt", StringComparison.Ordinal)).ToArray());
        var incomplete = CardPrototype.Test(_root, document, _routines);
        Assert.Equal(CardQualificationStage.Unqualified, incomplete.Qualification.Stage);
        Assert.Equal(2, CardPrototype.History(_root, document.Card.Id).Length);
        Assert.False(File.Exists(Path.Combine(incomplete.ReportDirectory, "evidence", "evidence.json")));
        Assert.Null(GameInputLease.Current);
    }
    [Fact]
    public void ForgedHistoryLabelsCannotGrantOrAdvertiseQualification()
    {
        var document = Replay(); var result = CardPrototype.Test(_root, document, _routines);
        var file = Path.Combine(result.ReportDirectory, "attempt.json");
        var history = JsonNode.Parse(File.ReadAllText(file))!; history["recordedOutcome"] = "LivePassed";
        File.WriteAllText(file, history.ToJsonString());
        Assert.Empty(CardPrototype.History(_root, document.Card.Id));
        Assert.Equal(CardQualificationStage.ReplayPassed, CardPrototype.ReviewHistory(_root, document, result.Attempt.Id, _routines).Stage);
        Assert.Throws<InvalidDataException>(() => CardPrototype.ReviewHistory(_root, document, "../other", _routines));
    }
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
