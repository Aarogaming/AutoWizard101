using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class CardQualificationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "card-qualification-" + Guid.NewGuid().ToString("N"));
    private readonly JobCardSpec _card = new(Guid.NewGuid().ToString("N"), "review.routine", "Saved replay");
    private string Graph => Path.Combine(_root, "graph.json");
    private string Session => Path.Combine(_root, "session.json");
    private string Report => Path.Combine(_root, "report.json");
    private string Evidence => Path.Combine(_root, "evidence");
    private CardQualificationBinding Binding => CardQualification.Bind(_card, GameExperiments.Digest(Graph), "adapter-v1", new string('a', 64), JobFactCatalog.All);
    public CardQualificationTests()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "Routines", "dodge");
        File.Copy(Path.Combine(source, "session.json"), Session);
        File.Copy(Path.Combine(source, "events.jsonl"), Path.Combine(_root, "events.jsonl"));
        File.WriteAllText(Graph, JsonSerializer.Serialize(RoutineGraphs.BuiltIn("dodge-movement-draft"), RoutineGraphs.Json));
    }
    private CardQualificationEvidence Record()
    {
        Assert.True(RoutineReplay.Run(Graph, Session, Report, _card, JobFactCatalog.All));
        return CardQualification.RecordReplay(Evidence, _card, Binding, Graph, Session, Report, JobFactCatalog.All);
    }
    [Fact]
    public void NewCardsAreUnqualifiedWithoutCreatingFilesOrGrantingInput()
    {
        var status = CardQualification.Assess(Evidence, Binding);
        Assert.Equal(CardQualificationStage.Unqualified, status.Stage);
        Assert.False(status.GrantsExecutionPermission); Assert.False(Directory.Exists(Evidence));
        Assert.Null(GameInputLease.Current);
    }
    [Fact]
    public void ExactReplaySurvivesReopenAndNeverPromotesToLiveOrRepeatable()
    {
        var evidence = Record();
        for (var i = 0; i < 4; i++)
        {
            var status = CardQualification.Assess(Evidence, Binding);
            Assert.Equal(CardQualificationStage.ReplayPassed, status.Stage);
            Assert.Equal(evidence.Id, status.EvidenceId); Assert.False(status.GrantsExecutionPermission);
        }
        Assert.Throws<IOException>(() => CardQualification.RecordReplay(Evidence, _card, Binding, Graph, Session, Report, JobFactCatalog.All));
        File.Delete(Report); // Source report deletion does not invalidate the frozen snapshot.
        Assert.Equal(CardQualificationStage.ReplayPassed, CardQualification.Assess(Evidence, Binding).Stage);
        Assert.Null(GameInputLease.Current);
    }
    [Fact]
    public void EachChangedBindingInvalidatesWithoutErasingHistoricalEvidence()
    {
        Record(); var original = File.ReadAllBytes(Path.Combine(Evidence, "evidence.json")); var binding = Binding;
        foreach (var changed in new[]
        {
            CardQualification.Bind(_card with { CooldownSeconds = 20 }, binding.ExecutionGraphHash, binding.AdapterVersion, binding.ScopeHash, JobFactCatalog.All),
            binding with { ExecutionGraphHash = new string('b', 64) }, binding with { AdapterVersion = "adapter-v2" },
            binding with { ScopeHash = new string('b', 64) }, binding with { FactSchemaHash = new string('b', 64) },
            binding with { RoutineId = "different-routine" }
        }) Assert.Equal(CardQualificationStage.Unqualified, CardQualification.Assess(Evidence, changed).Stage);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(Evidence, "evidence.json")));
    }
    [Theory]
    [InlineData("replay.json")]
    [InlineData("sources/graph.json")]
    [InlineData("sources/card.json")]
    [InlineData("sources/facts.json")]
    [InlineData("session/manifest.json")]
    [InlineData("session/events.jsonl")]
    public void ModifiedOrMissingArtifactsInvalidate(string relative)
    {
        Record(); var path = Path.Combine(Evidence, relative);
        File.AppendAllText(path, " ");
        Assert.Equal(CardQualificationStage.Unqualified, CardQualification.Assess(Evidence, Binding).Stage);
        File.Delete(path);
        Assert.Equal(CardQualificationStage.Unqualified, CardQualification.Assess(Evidence, Binding).Stage);
    }
    [Fact]
    public void ArbitraryPassingGraphCannotReplaceTrustedExecutionAssociation()
    {
        Assert.True(RoutineReplay.Run(Graph, Session, Report, _card, JobFactCatalog.All));
        Assert.Throws<InvalidDataException>(() => CardQualification.RecordReplay(Evidence, _card,
            Binding with { ExecutionGraphHash = new string('b', 64) }, Graph, Session, Report, JobFactCatalog.All));
        Assert.False(Directory.Exists(Evidence));
    }
    [Fact]
    public void CallerSuccessFlagsCannotReplaceRecordedReceiptsAndOutcomes()
    {
        var events = Path.Combine(_root, "events.jsonl");
        File.WriteAllLines(events, File.ReadAllLines(events).Where(line => !line.Contains("action_receipt", StringComparison.Ordinal)).ToArray());
        Assert.False(RoutineReplay.Run(Graph, Session, Report, _card, JobFactCatalog.All));
        var report = JsonNode.Parse(File.ReadAllText(Report))!;
        report["completed"] = true; report["stopped"] = false; report["reason"] = "observed-completion";
        report["proposal_count"] = 1; report["receipt_count"] = 1;
        File.WriteAllText(Report, report.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CardQualification.RecordReplay(Evidence, _card, Binding, Graph, Session, Report, JobFactCatalog.All));
        Assert.False(File.Exists(Path.Combine(Evidence, "evidence.json")));
        Assert.Equal(CardQualificationStage.Unqualified, CardQualification.Assess(Evidence, Binding).Stage);
    }
    [Fact]
    public void ImportedStageAssertionAndChangedFactDefinitionsAreRejected()
    {
        Record(); var path = Path.Combine(Evidence, "evidence.json");
        var evidence = JsonNode.Parse(File.ReadAllText(path))!; evidence["stage"] = "LivePassed";
        File.WriteAllText(path, evidence.ToJsonString());
        Assert.Equal(CardQualificationStage.Unqualified, CardQualification.Assess(Evidence, Binding).Stage);
        var changed = JobFactCatalog.All.Select(fact => fact with { FreshnessSeconds = fact.FreshnessSeconds + 1 }).ToArray();
        Assert.Throws<InvalidDataException>(() => CardQualification.RecordReplay(Path.Combine(_root, "changed"), _card, Binding, Graph, Session, Report, changed));
    }
    [Fact]
    public void RecomputedMetadataCannotPromoteForgedStoredSuccessWithoutReceipts()
    {
        Record();
        var events = Path.Combine(Evidence, "session", "events.jsonl");
        File.WriteAllLines(events, File.ReadAllLines(events).Where(line => !line.Contains("action_receipt", StringComparison.Ordinal)).ToArray());
        var reportPath = Path.Combine(Evidence, "replay.json");
        var report = JsonNode.Parse(File.ReadAllText(reportPath))!;
        report["completed"] = true; report["stopped"] = false; report["reason"] = "observed-completion";
        report["sha256_events"] = GameExperiments.Digest(events);
        File.WriteAllText(reportPath, report.ToJsonString());
        var evidencePath = Path.Combine(Evidence, "evidence.json");
        var evidence = JsonNode.Parse(File.ReadAllText(evidencePath))!;
        evidence["eventsHash"] = GameExperiments.Digest(events);
        evidence["reportHash"] = GameExperiments.Digest(reportPath);
        File.WriteAllText(evidencePath, evidence.ToJsonString());
        var before = File.ReadAllBytes(evidencePath);

        var status = CardQualification.Assess(Evidence, Binding);

        Assert.Equal(CardQualificationStage.Unqualified, status.Stage);
        Assert.Contains("cannot be reproduced", status.Reason);
        Assert.False(status.GrantsExecutionPermission);
        Assert.Equal(before, File.ReadAllBytes(evidencePath));
        Assert.Null(GameInputLease.Current);
    }
    [Theory]
    [InlineData(1.5)]
    [InlineData(double.MaxValue)]
    public void MalformedNumericReportFieldsRemainUnqualified(double value)
    {
        Record(); var reportPath = Path.Combine(Evidence, "replay.json");
        var report = JsonNode.Parse(File.ReadAllText(reportPath))!; report["schema_version"] = value;
        File.WriteAllText(reportPath, report.ToJsonString());
        var evidencePath = Path.Combine(Evidence, "evidence.json");
        var evidence = JsonNode.Parse(File.ReadAllText(evidencePath))!;
        evidence["reportHash"] = GameExperiments.Digest(reportPath);
        File.WriteAllText(evidencePath, evidence.ToJsonString());
        Assert.Equal(CardQualificationStage.Unqualified, CardQualification.Assess(Evidence, Binding).Stage);
    }
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
