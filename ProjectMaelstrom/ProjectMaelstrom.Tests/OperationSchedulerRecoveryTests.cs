using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationSchedulerRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-operation-recovery-" + Guid.NewGuid().ToString("N"));
    private static OperationRequest Request() => new(Guid.NewGuid().ToString("N"), "test.recovery", "v1", 10,
        false, "offline", new Dictionary<string, string>());
    private static OperationReceipt Receipt(OperationRequest request, OperationState state = OperationState.Completed) =>
        new(request, state, DateTime.UtcNow, "saved evidence", ["preserved-report.json"]);
    private string Save(OperationReceipt receipt, string? folderId = null)
    {
        var path = Path.Combine(_root, "operations", folderId ?? receipt.Request.Id, "receipt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        GameExperiments.WriteAtomic(path, receipt);
        return path;
    }

    [Fact]
    public async Task NestedAndUnrelatedArtifactReceiptsAreNeverRecoveredOrReserved()
    {
        var parent = Request(); var nested = Request();
        var parentPath = Save(Receipt(parent));
        var artifactDirectory = Path.Combine(Path.GetDirectoryName(parentPath)!, "artifacts", nested.Id);
        Directory.CreateDirectory(artifactDirectory);
        var nestedPath = Path.Combine(artifactDirectory, "receipt.json");
        GameExperiments.WriteAtomic(nestedPath, Receipt(nested, OperationState.Running));
        var malformedPath = Path.Combine(Path.GetDirectoryName(parentPath)!, "reports", "receipt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(malformedPath)!);
        File.WriteAllText(malformedPath, "not a scheduler receipt");
        var unrelatedPath = Path.Combine(_root, "operations", "imported-artifacts", "receipt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(unrelatedPath)!);
        File.WriteAllText(unrelatedPath, "{broken json");
        var protectedPaths = new[] { parentPath, nestedPath, malformedPath, unrelatedPath };
        var bytes = protectedPaths.Select(File.ReadAllBytes).ToArray();

        await using var scheduler = new OperationScheduler(_root, _root);
        Assert.Equal(0, scheduler.Pending);
        Assert.Equal(parent.Id, Assert.Single(OperationScheduler.History(_root)).Request.Id);
        var ticket = scheduler.Submit(nested, (_, _) => Task.FromResult(OperationOutcome.Completed("new explicit run")));
        Assert.Equal(OperationState.Completed, (await ticket.Completion).State);
        for (var i = 0; i < protectedPaths.Length; i++) Assert.Equal(bytes[i], File.ReadAllBytes(protectedPaths[i]));
    }

    [Fact]
    public async Task MismatchedFolderIdentityFailsBeforeRecoveryAndReleasesJournalLock()
    {
        var request = Request(); var wrongFolder = Guid.NewGuid().ToString("N");
        var path = Save(Receipt(request, OperationState.Running), wrongFolder);
        var bytes = File.ReadAllBytes(path);
        Assert.Throws<InvalidDataException>(() => new OperationScheduler(_root, _root));
        Assert.Throws<InvalidDataException>(() => OperationScheduler.History(_root));
        Assert.Equal(bytes, File.ReadAllBytes(path));

        GameExperiments.WriteAtomic(path, Receipt(request with { Id = wrongFolder }));
        await using var recovered = new OperationScheduler(_root, _root);
        Assert.Equal(0, recovered.Pending);
    }

    [Theory]
    [InlineData("null-request")]
    [InlineData("invalid-timeout")]
    [InlineData("invalid-pin")]
    [InlineData("unknown-state")]
    [InlineData("unknown-schema")]
    public void InvalidSavedContractsFailBeforeRecovery(string problem)
    {
        var request = Request(); var receipt = Receipt(request, OperationState.Running);
        receipt = problem switch
        {
            "null-request" => receipt with { Request = null! },
            "invalid-timeout" => receipt with { Request = request with { TimeoutSeconds = 0 } },
            "invalid-pin" => receipt with { Request = request with { Pins = new Dictionary<string, string> { [Path.Combine(_root, "input.json")] = null! } } },
            "unknown-state" => receipt with { State = (OperationState)999 },
            "unknown-schema" => receipt with { SchemaVersion = 2 },
            _ => throw new InvalidOperationException()
        };
        var path = Save(receipt, request.Id); var bytes = File.ReadAllBytes(path);
        Assert.Throws<InvalidDataException>(() => new OperationScheduler(_root, _root));
        Assert.Throws<InvalidDataException>(() => OperationScheduler.History(_root));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(nameof(OperationState.Queued))]
    [InlineData(nameof(OperationState.Running))]
    public async Task OnlyIncompleteTopLevelJobsBecomeInterruptedWithoutResuming(string stateName)
    {
        var state = Enum.Parse<OperationState>(stateName);
        var request = Request(); var path = Save(Receipt(request, state));
        await using var scheduler = new OperationScheduler(_root, _root);
        var receipt = JsonSerializer.Deserialize<OperationReceipt>(File.ReadAllText(path))!;
        Assert.Equal(OperationState.Interrupted, receipt.State);
        Assert.Equal(request.Id, receipt.Request.Id);
        Assert.Equal(new[] { "preserved-report.json" }, receipt.Artifacts);
        Assert.Equal(0, scheduler.Pending);
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(request,
            (_, _) => throw new Xunit.Sdk.XunitException("Recovery must not execute work.")));
    }

    [Theory]
    [InlineData(nameof(OperationState.Completed))]
    [InlineData(nameof(OperationState.Failed))]
    [InlineData(nameof(OperationState.Cancelled))]
    [InlineData(nameof(OperationState.TimedOut))]
    [InlineData(nameof(OperationState.Interrupted))]
    [InlineData(nameof(OperationState.Unknown))]
    [InlineData(nameof(OperationState.CleanupFailed))]
    public async Task ValidTerminalEvidenceRemainsByteIdentical(string stateName)
    {
        var state = Enum.Parse<OperationState>(stateName);
        var request = Request(); var path = Save(Receipt(request, state)); var bytes = File.ReadAllBytes(path);
        await using var scheduler = new OperationScheduler(_root, _root);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(state, Assert.Single(OperationScheduler.History(_root)).State);
    }

    [Fact]
    public async Task GuidCasingCannotReplayAnExistingWindowsJobFolder()
    {
        var request = Request() with { Id = "aabbccddeeff00112233445566778899" };
        Save(Receipt(request), request.Id.ToUpperInvariant());
        await using var scheduler = new OperationScheduler(_root, _root);
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(request with { Id = request.Id.ToUpperInvariant() },
            (_, _) => throw new Xunit.Sdk.XunitException("Case-only duplicate must not run.")));
        Assert.Equal(request.Id, Assert.Single(OperationScheduler.History(_root)).Request.Id);
    }

    [Fact]
    public void HistoryStillReturnsOnlyTheNewestHundredImmediateJobs()
    {
        var first = "";
        for (var i = 0; i < 101; i++)
        {
            var request = Request(); var path = Save(Receipt(request));
            if (i == 0) first = request.Id;
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i));
        }
        var history = OperationScheduler.History(_root);
        Assert.Equal(100, history.Length);
        Assert.DoesNotContain(history, receipt => receipt.Request.Id == first);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
