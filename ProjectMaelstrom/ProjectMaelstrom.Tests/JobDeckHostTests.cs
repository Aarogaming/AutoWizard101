using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Maelstrom.DeckApi;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class JobDeckHostTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-deck-api-test-" + Guid.NewGuid().ToString("N"));

    public JobDeckHostTests() => Directory.CreateDirectory(_root);

    private OperationScheduler Scheduler() => new(_root, Path.Combine(_root, "locks"));

    [Fact]
    public async Task CapabilitiesAreReadOnlyAndDisconnectedRefreshDoesNotSubmit()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var initial = host.Deck.State;
        var result = Result(await Call("capabilities"));
        Assert.Equal(1, result.GetProperty("schemaVersion").GetInt32());
        Assert.False(result.GetProperty("liveQualified").GetBoolean());
        Assert.NotEmpty(result.GetProperty("facts").EnumerateArray());
        Assert.NotEmpty(result.GetProperty("routines").EnumerateArray());
        await Task.Delay(1100);
        Assert.Equal(0, scheduler.Pending);
        Assert.Equal(initial.Revision, host.Deck.State.Revision);
        var snapshot = Result(await Call("snapshot"));
        Assert.False(snapshot.GetProperty("connected").GetBoolean());
        Assert.Equal(0, snapshot.GetProperty("refresh").GetProperty("attempts").GetInt32());
        var aliases = snapshot.GetProperty("aliases").EnumerateArray().ToArray();
        Assert.Equal(JobFactCatalog.All.Count, aliases.Length);
        Assert.All(aliases, alias => Assert.Equal("unknown", alias.GetProperty("quality").GetString()));
        Assert.Contains(aliases, alias => alias.GetProperty("aliases").EnumerateArray().Any(name => name.GetString() == "Idle"));
        Assert.Equal(0, scheduler.Pending);
    }

    [Fact]
    public async Task FactsCommandIsReadOnlyAndNeverPromotesPipeInput()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var store = new W101FactsStore();
        store.Publish(new W101Fact("health", "4920", "w101.resources", DateTime.UtcNow.Ticks, "r15-archived", W101FactQualification.Candidate), DateTime.UtcNow.Ticks);
        W101BackgroundFacts.ReplaceStore(store);
        try
        {
            var reply = Result(await Call("facts", new { id = "health", value = "1" }));
            Assert.NotEmpty(reply.GetProperty("facts").EnumerateArray());
            Assert.Contains("Unknown means absent or expired", reply.GetProperty("note").GetString());
            var health = reply.GetProperty("facts").EnumerateArray().First(f => f.GetProperty("id").GetString() == "health");
            Assert.Equal("4920", health.GetProperty("value").GetString());
            // The pipe cannot mutate the store; a second read is unchanged.
            var second = Result(await Call("facts"));
            Assert.Equal("4920", second.GetProperty("facts").EnumerateArray().First(f => f.GetProperty("id").GetString() == "health").GetProperty("value").GetString());
        }
        finally { W101BackgroundFacts.ReplaceStore(new W101FactsStore()); }
    }

    private Task<JsonElement> Call(string command, object? arguments = null, string? requestId = null) =>
        JobDeckWire.CallAsync(_root, JsonSerializer.SerializeToUtf8Bytes(
            new JobDeckRequest(command, JsonSerializer.SerializeToElement(arguments ?? new { }, JobDeckRules.Json), requestId), JobDeckRules.Json));

    private static JsonElement Result(JsonElement reply)
    {
        Assert.Equal(1, reply.GetProperty("schemaVersion").GetInt32());
        Assert.True(reply.GetProperty("ok").GetBoolean(), reply.GetProperty("error").ToString());
        Assert.Equal(JsonValueKind.Null, reply.GetProperty("error").ValueKind);
        return reply.GetProperty("result");
    }

    private static void Error(JsonElement reply)
    {
        Assert.Equal(1, reply.GetProperty("schemaVersion").GetInt32());
        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, reply.GetProperty("result").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(reply.GetProperty("error").GetString()));
    }

    private JobCardSpec ReplayCard() => new(Guid.NewGuid().ToString("N"), "review.routine", "Synthetic replay", true,
        Settings: new() { ["graphFile"] = Path.Combine(_root, "routine.json"), ["sessionFile"] = Path.Combine(_root, "session.json") });

    private void WriteSyntheticReplay()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "Routines", "dodge");
        File.Copy(Path.Combine(source, "session.json"), Path.Combine(_root, "session.json"));
        File.Copy(Path.Combine(source, "events.jsonl"), Path.Combine(_root, "events.jsonl"));
        File.WriteAllText(Path.Combine(_root, "routine.json"),
            JsonSerializer.Serialize(RoutineGraphs.BuiltIn("dodge-movement-draft"), RoutineGraphs.Json));
    }

    [Fact]
    public void PipeIdentityNormalizesTheSameDataRootAndSeparatesOtherOwners()
    {
        Assert.Equal(JobDeckWire.PipeName(_root), JobDeckWire.PipeName(_root.ToUpperInvariant() + Path.DirectorySeparatorChar));
        Assert.Equal(JobDeckWire.PipeName(_root), JobDeckWire.PipeName(Path.Combine(_root, ".")));
        Assert.NotEqual(JobDeckWire.PipeName(_root), JobDeckWire.PipeName(_root + "-other"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(JobDeckWire.MaximumBytes)]
    public async Task WireUsesAnExactLittleEndianLengthAndPreservesPayloadAtBothBounds(int size)
    {
        var payload = Enumerable.Repeat((byte)'x', size).ToArray();
        using var stream = new MemoryStream();
        await JobDeckWire.WriteAsync(stream, payload, CancellationToken.None);
        Assert.Equal(size + 4, stream.Length);
        Assert.Equal(size, BinaryPrimitives.ReadInt32LittleEndian(stream.ToArray().AsSpan(0, 4)));
        stream.Position = 0;
        Assert.Equal(payload, await JobDeckWire.ReadAsync(stream, CancellationToken.None));
        Assert.Equal(stream.Length, stream.Position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(JobDeckWire.MaximumBytes + 1)]
    [InlineData(int.MaxValue)]
    public async Task WireRejectsInvalidDeclaredLengthsBeforeReadingPayload(int size)
    {
        var packet = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(packet, size);
        using var stream = new MemoryStream(packet);
        await Assert.ThrowsAsync<InvalidDataException>(() => JobDeckWire.ReadAsync(stream, CancellationToken.None));
        Assert.Equal(4, stream.Position);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(JobDeckWire.MaximumBytes + 1)]
    public async Task WireRejectsInvalidOutboundSizesBeforeWritingAnything(int size)
    {
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => JobDeckWire.WriteAsync(stream, new byte[size], CancellationToken.None));
        Assert.Equal(0, stream.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task WireRejectsTruncatedHeadersAndBodies(int available)
    {
        var packet = new byte[6];
        BinaryPrimitives.WriteInt32LittleEndian(packet, 2);
        using var stream = new MemoryStream(packet[..available]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => JobDeckWire.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task FragmentedReadsConsumeExactlyOneEnvelope()
    {
        using var outgoing = new MemoryStream();
        await JobDeckWire.WriteAsync(outgoing, Encoding.UTF8.GetBytes("first"), CancellationToken.None);
        await JobDeckWire.WriteAsync(outgoing, Encoding.UTF8.GetBytes("second"), CancellationToken.None);
        using var incoming = new FragmentedStream(outgoing.ToArray());
        Assert.Equal("first", Encoding.UTF8.GetString(await JobDeckWire.ReadAsync(incoming, CancellationToken.None)));
        Assert.Equal("second", Encoding.UTF8.GetString(await JobDeckWire.ReadAsync(incoming, CancellationToken.None)));
        Assert.Equal(incoming.Length, incoming.Position);
    }

    [Fact]
    public async Task MissingOwnerCanBeCancelledWithoutStartingAHost()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JobDeckWire.CallAsync(_root, Encoding.UTF8.GetBytes("{}"), cancel.Token));
        Assert.False(Directory.Exists(Path.Combine(_root, "job-decks")));
    }

    [Fact]
    public async Task OwnerServesCatalogStateAndUnknownVitalsWithoutCreatingGameWork()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var catalog = Result(await Call("catalog")).Deserialize<JobRoutineInfo[]>(JobDeckRules.Json)!;
        Assert.Contains(catalog, routine => routine.Id == "review.routine" && routine.Available && !routine.WritesGame);
        Assert.Equal(8, catalog.Count(routine => routine.Collection == "Minigames"));
        Assert.All(catalog.Where(routine => routine.Collection == "Minigames"), routine => Assert.False(routine.Available));
        var state = Result(await Call("state")).GetProperty("state").Deserialize<JobDeckState>(JobDeckRules.Json)!;
        Assert.Equal("Stopped", state.Status);
        Assert.Equal(0, state.Revision);
        Assert.All(state.Deck.Cards, card => Assert.False(card.InDeck));
        Assert.Empty(state.Progress);
        var snapshot = Result(await Call("snapshot"));
        Assert.False(snapshot.GetProperty("connected").GetBoolean());
        Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("facts").ValueKind);
        var unavailable = snapshot.GetProperty("unavailable").Deserialize<string[]>()!;
        Assert.All(new[] { "health", "mana", "energy", "potions", "inventory", "area", "quest.type", "quest.id" },
            fact => Assert.Contains(fact, unavailable));
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task RoutineValidationUsesNativeReaderWithoutConnectionOrStateChanges()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var before = JsonSerializer.Serialize(host.Deck.State, JobDeckRules.Json);
        var graph = RoutineGraphs.BuiltIn("dodge-movement-draft");
        var element = JsonSerializer.SerializeToElement(graph, RoutineGraphs.Json);
        var result = Result(await Call("validate-routine", new { graph = element }));

        Assert.True(result.GetProperty("valid").GetBoolean());
        Assert.True(result.GetProperty("confirmed").GetBoolean());
        Assert.Equal("native-routine-graphs-v1", result.GetProperty("authority").GetString());
        Assert.Equal(graph.Id, result.GetProperty("routineId").GetString());
        Assert.Equal(graph.Nodes.Length, result.GetProperty("nodeCount").GetInt32());
        Assert.Empty(result.GetProperty("errors").EnumerateArray());
        Assert.False(result.GetProperty("liveQualified").GetBoolean());
        Assert.Equal(before, JsonSerializer.Serialize(host.Deck.State, JobDeckRules.Json));
        Assert.False(Result(await Call("snapshot")).GetProperty("connected").GetBoolean());
        Assert.Empty(OperationScheduler.History(_root));
        Assert.Equal(0, scheduler.Pending);
        Assert.Null(GameInputLease.Current);
    }

    public static IEnumerable<object[]> InvalidNativeRoutines()
    {
        var graph = RoutineGraphs.BuiltIn("dodge-movement-draft");
        var json = JsonSerializer.Serialize(graph, RoutineGraphs.Json);
        yield return ["{}"];
        yield return ["null"];
        yield return ["[]"];
        yield return ["{\"schema_version\":2,\"routine_id\":\"old-shape\",\"nodes\":[{\"id\":\"entry\",\"edges\":[{\"next_node\":\"complete\"}]}]}"];
        yield return [JsonSerializer.Serialize(graph with { SchemaVersion = 2 }, RoutineGraphs.Json)];
        yield return [JsonSerializer.Serialize(graph with { StartNode = "missing" }, RoutineGraphs.Json)];
        yield return [JsonSerializer.Serialize(graph with { TimeoutMs = 0 }, RoutineGraphs.Json)];
        var first = graph.Nodes.First(node => node.Edges.Length > 0);
        var dangling = first with { Edges = [first.Edges[0] with { NextNode = "missing" }, .. first.Edges.Skip(1)] };
        yield return [JsonSerializer.Serialize(graph with { Nodes = graph.Nodes.Select(node => node.Id == first.Id ? dangling : node).ToArray() }, RoutineGraphs.Json)];
        yield return [json.Replace("\"schema_version\": 1", "\"schema_version\": 1, \"schema_version\": 1", StringComparison.Ordinal)];
        yield return [json.Insert(1, "\"unknown_field\":true,")];
        yield return ["{\"padding\":\"" + new string('x', 65537) + "\"}"];
    }

    [Theory]
    [MemberData(nameof(InvalidNativeRoutines))]
    public async Task RoutineValidationRejectsInvalidNativeShapesAndNeverSchedules(string json)
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        using var graph = JsonDocument.Parse(json);
        var before = JsonSerializer.Serialize(host.Deck.State, JobDeckRules.Json);
        var result = Result(await Call("validate-routine", new { graph = graph.RootElement }));
        Assert.False(result.GetProperty("valid").GetBoolean());
        Assert.True(result.GetProperty("confirmed").GetBoolean());
        Assert.False(result.GetProperty("liveQualified").GetBoolean());
        Assert.NotEmpty(result.GetProperty("errors").EnumerateArray());
        Assert.Equal(before, JsonSerializer.Serialize(host.Deck.State, JobDeckRules.Json));
        Assert.Empty(OperationScheduler.History(_root));
        Assert.Equal(0, scheduler.Pending);
        Assert.Null(GameInputLease.Current);
    }

    [Fact]
    public async Task ASecondOwnerIsRejectedAndTheFirstOwnerStillResponds()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        Assert.Throws<IOException>(() => new JobDeckHost(_root, scheduler));
        Assert.Equal("Stopped", Result(await Call("state")).GetProperty("state").GetProperty("status").GetString());
    }

    [Fact]
    public async Task DealingDoesNotDispatchAndAnExplicitStepUsesOnlyTheOfflineScheduler()
    {
        WriteSyntheticReplay();
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var card = ReplayCard();
        var dealt = Result(await Call("deal", new { card, revision = 0 })).Deserialize<JobDeckState>(JobDeckRules.Json)!;
        Assert.Equal(1, dealt.Revision);
        Assert.Equal("Stopped", dealt.Status);
        Assert.Empty(dealt.Progress);
        Assert.Empty(OperationScheduler.History(_root));

        var requestId = Guid.NewGuid().ToString("N");
        Result(await Call("step", requestId: requestId));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (host.Deck.State.ActiveCard is not null) await Task.Delay(10, timeout.Token);
        Assert.Equal(1, host.Deck.State.Progress[card.Id].Completed);
        var receipt = Assert.Single(OperationScheduler.History(_root));
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.False(receipt.Request.WritesGame);
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "operations", receipt.Request.Id, "replay.json")));
        Assert.Equal(0, report.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
        Assert.False(report.RootElement.GetProperty("live_qualified").GetBoolean());

        Result(await Call("step", requestId: requestId));
        Assert.Single(OperationScheduler.History(_root));
        Assert.Equal(1, host.Deck.State.Progress[card.Id].Attempts);
    }

    [Fact]
    public async Task PreviewFactsNeverBecomeLiveFactsOrAuthorizeAStep()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var card = ReplayCard() with { Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(10))] };
        Result(await Call("deal", new { card, revision = 0 }));
        var facts = new JobFacts("wizard101", "synthetic-preview-only", DateTime.UtcNow,
            new() { ["energy"] = JsonSerializer.SerializeToElement(20) }, "synthetic");
        var preview = Result(await Call("preview", new { facts }));
        Assert.True(preview.GetProperty("simulation").GetBoolean());
        Assert.True(preview.GetProperty("eligibility").Deserialize<JobEligibility[]>(JobDeckRules.Json)!.Single(item => item.CardId == card.Id).Ready);
        Assert.Equal(JsonValueKind.Null, Result(await Call("snapshot")).GetProperty("facts").ValueKind);
        var actual = Result(await Call("step", requestId: Guid.NewGuid().ToString("N"))).Deserialize<JobDeckState>(JobDeckRules.Json)!;
        Assert.Equal("Deferred", actual.Status);
        Assert.Empty(actual.Progress);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task UnavailableCardsRemainUnplayedAfterAnExplicitStep()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var card = host.Deck.State.Deck.Cards.Single(item => item.RoutineId == "w101.minigame.conjuration") with { InDeck = true };
        Result(await Call("deal", new { card, revision = 0 }));
        var state = Result(await Call("step", requestId: Guid.NewGuid().ToString("N"))).Deserialize<JobDeckState>(JobDeckRules.Json)!;
        Assert.Equal("NoPlayableCards", state.Status);
        Assert.Empty(state.Progress);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task RevisionConflictsReturnErrorsWithoutOverwritingTheCurrentDeck()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var card = host.Deck.State.Deck.Cards[0] with { Title = "First edit" };
        Assert.Equal(1, Result(await Call("deal", new { card, revision = 0 })).GetProperty("revision").GetInt64());
        Error(await Call("deal", new { card = card with { Title = "Stale edit" }, revision = 0 }));
        Assert.Equal("First edit", host.Deck.State.Deck.Cards.Single(item => item.Id == card.Id).Title);
        Error(await Call("reset", new { revision = 0 }));
        Assert.Equal(2, Result(await Call("reset", new { revision = 1 })).GetProperty("revision").GetInt64());
        var document = host.Deck.State.Deck with { Name = "Current revision" };
        Assert.Equal(3, Result(await Call("replace", new { deck = document, revision = 2 })).GetProperty("revision").GetInt64());
        Error(await Call("replace", new { deck = document with { SchemaVersion = 99 }, revision = 3 }));
        Assert.Equal(3, host.Deck.State.Revision);
        Assert.Equal("Current revision", host.Deck.State.Deck.Name);
    }

    [Theory]
    [InlineData("execute_script", "{}")]
    [InlineData("set_health", "{}")]
    [InlineData("deal", "{}")]
    [InlineData("replace", "{}")]
    [InlineData("reset", "{\"revision\":\"invalid\"}")]
    [InlineData("step", "{}")]
    [InlineData("observe", "{}")]
    public async Task UnsupportedCommandsAndInvalidArgumentsReturnErrorsWithoutWork(string command, string arguments)
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        using var parsed = JsonDocument.Parse(arguments);
        Error(await Call(command, parsed.RootElement));
        Assert.Equal(0, host.Deck.State.Revision);
        Assert.Equal("Stopped", host.Deck.State.Status);
        Assert.Empty(host.Deck.State.Progress);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task FailedPreparationRecordsAnAttemptAndNeverReportsCompletion()
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        var card = ReplayCard();
        Result(await Call("deal", new { card, revision = 0 }));
        var state = Result(await Call("step", requestId: Guid.NewGuid().ToString("N"))).Deserialize<JobDeckState>(JobDeckRules.Json)!;
        Assert.Equal("Paused", state.Status);
        Assert.False(string.IsNullOrWhiteSpace(state.Error));
        Assert.Equal(1, state.Progress[card.Id].Attempts);
        Assert.Equal(0, state.Progress[card.Id].Completed);
        Assert.Equal("Failed", state.Progress[card.Id].Status);
        Assert.Null(state.ActiveOperation);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Theory]
    [InlineData("{broken}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"command\":\"state\",\"arguments\":{},\"extra\":true}")]
    [InlineData("{\"command\":\"state\",\"arguments\":{\"nested\":[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]}}")]
    public async Task MalformedRequestsAreDroppedAndTheOwnerAcceptsTheNextValidCall(string payload)
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        await SendRejectedFrame(Encoding.UTF8.GetBytes(payload));
        Assert.Equal(0, Result(await Call("state")).GetProperty("state").GetProperty("revision").GetInt64());
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(JobDeckWire.MaximumBytes + 1)]
    public async Task InvalidFrameLengthsAreDroppedWithoutReadingAnUnboundedBody(int declaredSize)
    {
        await using var scheduler = Scheduler();
        await using var host = new JobDeckHost(_root, scheduler);
        await SendRejectedFrame([], declaredSize);
        Result(await Call("snapshot"));
        Assert.Empty(OperationScheduler.History(_root));
    }

    private async Task SendRejectedFrame(byte[] payload, int? declaredSize = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var pipe = new NamedPipeClientStream(".", JobDeckWire.PipeName(_root), PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(1500, timeout.Token);
        var packet = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(packet, declaredSize ?? payload.Length);
        payload.CopyTo(packet, 4);
        await pipe.WriteAsync(packet, timeout.Token);
        try { Assert.Equal(0, await pipe.ReadAsync(new byte[1], timeout.Token)); }
        catch (IOException) { /* A rejected frame may close the pipe as a broken connection. */ }
    }

    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
