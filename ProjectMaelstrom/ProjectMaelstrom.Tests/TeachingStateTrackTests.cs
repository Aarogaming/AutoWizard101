using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class TeachingStateTrackTests : IDisposable
{
    [Theory]
    [InlineData("{\"loading\":true,\"states\":{\"menu\":true},\"stableStates\":{\"menu\":true}}", false)]
    [InlineData("{\"states\":{\"menu\":true},\"stableStates\":{\"menu\":null}}", false)]
    [InlineData("{\"stableStates\":{\"menu\":true}}", false)]
    [InlineData("{\"states\":{\"menu\":true},\"stableStates\":{\"menu\":true}}", true)]
    public void CanonicalFactsRequireCurrentStableAgreement(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, W101RecordedFacts.Read(document.RootElement).ContainsKey("menu"));
    }

    [Fact]
    public void OnlyStableQuestEvidenceBecomesConditions()
    {
        using var document = JsonDocument.Parse("""
            {"questContext":{"objectiveText":"unconfirmed"},
             "stableQuestContext":{"objectiveText":"Talk to Private Quinn in Firecat Alley"}}
            """);
        var facts = W101RecordedFacts.Read(document.RootElement);
        Assert.Equal("Talk to Private Quinn in Firecat Alley", facts["quest.objective"].GetString());
        Assert.True(facts.ContainsKey("quest.action"));
    }
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "teaching-state-" + Guid.NewGuid().ToString("N"));
    private static readonly TeachingTarget Target = new(10, 20, 30, "game");
    private static TeachingSession Session => new(1, "session", "game", 10000, "complete",
        [new("chunk-001", 0, 10000, "complete", 10000)],
        [new("activity", "Open menu", 100, 1000, "unsure", "menu.open", "unsure", "I opened the settings here.")], Target, 100000000);
    public TeachingStateTrackTests() => Directory.CreateDirectory(_folder);
    public void Dispose() => Directory.Delete(_folder, true);
    private static TeachingStateSample Sample(double time, string scene = "world", string status = "observed") =>
        new(1, "session", Target, time, "test-reader-v1", "zone-1", status, scene,
            new() { ["health"] = JsonSerializer.SerializeToElement(100) });
    private string Write(params TeachingStateSample[] samples)
    {
        var path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(path, samples.Select(s => JsonSerializer.Serialize(s))); return path;
    }

    [Fact]
    public void CursorNeverBorrowsFutureAndShowsStaleness()
    {
        var samples = new[] { Sample(100), Sample(4000, "menu") };
        Assert.Null(TeachingStateTrack.At(samples, 99).Sample);
        Assert.Equal("world", TeachingStateTrack.At(samples, 200).Sample!.Scene);
        Assert.Equal(100, TeachingStateTrack.At(samples, 200).AgeMs);
        Assert.Equal("stale", TeachingStateTrack.At(samples, 3000).Status);
        Assert.Equal("menu", TeachingStateTrack.At(samples, 4000).Sample!.Scene);
    }

    [Fact]
    public void UnknownAndLoadingReplaceEarlierFacts()
    {
        var unknown = Sample(200, status: "unknown") with { Facts = new(), Scene = null };
        var samples = new[] { Sample(100), unknown };
        Assert.Equal("unknown", TeachingStateTrack.At(samples, 201).Status);
        Assert.Empty(TeachingStateTrack.At(samples, 201).Sample!.Facts);
    }

    [Fact]
    public void RejectsDifferentProcessSessionAndUnorderedClock()
    {
        Assert.Throws<InvalidDataException>(() => TeachingStateTrack.Read(Write(Sample(0) with { Target = Target with { StartedUtcTicks = 21 } }), Session));
        Assert.Throws<InvalidDataException>(() => TeachingStateTrack.Read(Write(Sample(0) with { SessionId = "other" }), Session));
        Assert.Throws<InvalidDataException>(() => TeachingStateTrack.Read(Write(Sample(100), Sample(50)), Session));
    }

    [Fact]
    public void ProposalsPreserveCommentaryAndRemainUnqualified()
    {
        var proposal = Assert.Single(TeachingStateTrack.Propose(Session, [Sample(100), Sample(1000, "menu")], "hash"));
        Assert.Equal("world", proposal.Activity.Trigger); Assert.Equal("menu", proposal.Activity.Outcome);
        Assert.Equal(Session.Activities[0].Commentary, proposal.Activity.Commentary);
        Assert.Equal("needs-review", proposal.Status);
        Assert.Contains(proposal.Gaps, g => g.Contains("do not prove causation"));
        Assert.Equal("menu.open", proposal.Activity.Action);
    }

    [Fact]
    public void MissingEvidenceCannotBecomeVerifiedFromCommentary()
    {
        var proposal = Assert.Single(TeachingStateTrack.Propose(Session, [], "hash"));
        Assert.Equal("unsure", proposal.Activity.Outcome);
        Assert.Contains(proposal.Gaps, g => g.Contains("activity end"));
    }

    [Fact]
    public void HeadlessAttachReviewAndTamperDetection()
    {
        var path = Path.Combine(_folder, "teaching-session.json"); LongTeachingSession.Save(path, Session);
        TeachingReviewCli.Run(["--teaching-attach-state", path, Write(Sample(100), Sample(1000, "menu"))]);
        var output = Path.Combine(_folder, "proposals.json");
        TeachingReviewCli.Run(["--teaching-review", path, output]);
        Assert.Contains("needs-review", File.ReadAllText(output));
        Assert.Throws<IOException>(() => TeachingReviewCli.Run(["--teaching-review", path, output]));
        var saved = LongTeachingSession.Read(path);
        File.AppendAllText(Path.Combine(_folder, saved.StateTrack!), JsonSerializer.Serialize(Sample(2000)) + "\n");
        Assert.Throws<InvalidDataException>(() => TeachingReviewCli.Review(path));
    }

    [Fact]
    public void W101BridgeRequiresClockAndExactTarget()
    {
        Assert.Throws<InvalidDataException>(() => W101TeachingStateImport.Import("missing", "missing", Session with { UtcOriginTicks = 0 }));
        var request = Path.Combine(_folder, "ui-request.json");
        File.WriteAllText(request, JsonSerializer.Serialize(new { Target = new { Handle = 30, ProcessId = 11, StartedUtcTicks = 20, ProcessName = "game" } }));
        Assert.Throws<InvalidDataException>(() => W101TeachingStateImport.Import(request, Path.Combine(_folder, "teaching-session.json"), Session));
    }

    [Fact]
    public void W101BridgeRetainsTreeAndAcquisitionTimestamp()
    {
        var request = Path.Combine(_folder, "ui-request.json");
        File.WriteAllText(request, JsonSerializer.Serialize(new { Target = new { Handle = 30, ProcessId = 10, StartedUtcTicks = 20, ProcessName = "game" } }));
        File.WriteAllText(Path.Combine(_folder, "ui-observations.jsonl"), JsonSerializer.Serialize(new
        {
            kind = "tree",
            observedUtcTicks = 102000000,
            statesObservedUtcTicks = 101000000,
            states = new { menu = true, nativeIdle = false, dialogue = true },
            stableStates = new { menu = true, nativeIdle = false, dialogue = false },
            nodes = new[] { new { name = "Menu" } }
        }) + "\n");
        File.AppendAllText(Path.Combine(_folder, "ui-observations.jsonl"), JsonSerializer.Serialize(new { kind = "loading", observedUtcTicks = 103000000, loading = false }) + "\n");
        var track = W101TeachingStateImport.Import(request, Path.Combine(_folder, "teaching-session.json"), Session);
        var sample = Assert.Single(TeachingStateTrack.Read(track, Session));
        Assert.Equal(100, sample.TimeMs); Assert.Equal("menu", sample.Scene);
        Assert.True(sample.Facts["menu"].GetBoolean());
        Assert.False(sample.Facts["nativeIdle"].GetBoolean());
        Assert.False(sample.Facts.ContainsKey("dialogue"));
        Assert.False(sample.Facts.ContainsKey("energy"));
        Assert.Equal(200, TeachingStateTrack.At([sample], 300).AgeMs);
        Assert.Equal("Menu", sample.UiTree!.Value[0].GetProperty("name").GetString());
    }
}
