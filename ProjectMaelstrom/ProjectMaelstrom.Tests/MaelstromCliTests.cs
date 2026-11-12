using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class MaelstromCliTests : IDisposable
{
    [Fact]
    public void LoadingWalkNeedsExplicitMeasuredForwardVisit()
    {
        var args = CheckArgs(); args[1] = "navigation-visit";
        args = [.. args, "--enable-ui-tree", "--enable-module-actions", "--key", "W", "--pulse-ms", "15000", "--include-pose-context", "--assisted-hold", "true"];
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args, _root));
        Assert.True(MaelstromCli.Parse([.. args, "--walk-until-loading", "true"], _root).WalkUntilLoading);
        args[Array.IndexOf(args, "W")] = "A";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--walk-until-loading", "true"], _root));
    }

    [Fact]
    public void IdleOnlyRecoveryCanOptIntoQuietActivation()
    {
        var args = CheckArgs(); args[1] = "idle-cycle";
        args = [.. args, "--enable-ui-tree", "--enable-module-actions", "--cycle-mode", "idle"];
        Assert.False(MaelstromCli.Parse(args, _root).VisitPolicy?.ActivateWhenQuiet ?? false);
        Assert.True(MaelstromCli.Parse([.. args, "--activate-when-quiet", "true"], _root).VisitPolicy!.ActivateWhenQuiet);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--activate-when-quiet", "true"], _root));
    }

    [Fact]
    public void NearbyWaypointNeedsMeasuredForwardVisitAndBoundedDistance()
    {
        var args = CheckArgs(); args[1] = "navigation-visit";
        args = [.. args, "--enable-ui-tree", "--enable-module-actions", "--key", "W", "--pulse-ms", "100"];
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--waypoint-distance", "40"], _root));
        args = [.. args, "--include-pose-context"];
        Assert.Equal(40, MaelstromCli.Parse([.. args, "--waypoint-distance", "40"], _root).WaypointDistance);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--waypoint-distance", "101"], _root));
        args[Array.IndexOf(args, "W")] = "A";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--waypoint-distance", "40"], _root));
    }

    [Fact]
    public void PoseObservationRequiresSeparateObserverOptIn()
    {
        var args = CheckArgs(); args[1] = "ui-tree-watch";
        args = [.. args, "--enable-ui-tree"];
        Assert.False(MaelstromCli.Parse(args, _root).IncludePoseContext);
        Assert.True(MaelstromCli.Parse([.. args, "--include-pose-context"], _root).IncludePoseContext);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--include-pose-context", "--include-pose-context"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--include-pose-context"], _root));
    }

    [Fact]
    public void VisitRecoveryOptionsAreExplicitBoundedAndUnavailableToObservers()
    {
        var args = CheckArgs(); args[1] = "navigation-visit";
        args = [.. args, "--enable-ui-tree", "--enable-module-actions", "--key", "A", "--pulse-ms", "150"];
        Assert.Equal(new W101VisitPolicy(), MaelstromCli.Parse(args, _root).VisitPolicy);
        var configured = MaelstromCli.Parse([.. args, "--resume-after-quiet", "true", "--quiet-seconds", "3", "--idle-retries", "1"], _root);
        Assert.Equal(new W101VisitPolicy(1, true, 3), configured.VisitPolicy);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--idle-retries", "3"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--resume-after-quiet", "yes"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--quiet-seconds", "0"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--resume-after-quiet", "true"], _root));
    }
    [Theory]
    [InlineData("navigation-probe")]
    [InlineData("navigation-visit")]
    public void NavigationTeachingRequiresExplicitBoundedPulse(string command)
    {
        var args = CheckArgs(); args[1] = command;
        args = [.. args, "--enable-ui-tree", "--enable-module-actions"];
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args, _root));
        var parsed = MaelstromCli.Parse([.. args, "--key", "W", "--pulse-ms", "150"], _root);
        Assert.Equal("W", parsed.NavigationKey); Assert.Equal(150, parsed.PulseMs);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--key", "X", "--pulse-ms", "150"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--key", "W", "--pulse-ms", "301"], _root));
        var read = CheckArgs(); read[1] = "module-observe";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. read, "--enable-ui-tree", "--key", "W", "--pulse-ms", "10"], _root));
    }

    [Fact]
    public void IdleCycleHasSeparateActionOptIn()
    {
        var args = CheckArgs(); args[1] = "idle-cycle";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-ui-tree"], _root));
        Assert.True(MaelstromCli.Parse([.. args, "--enable-ui-tree", "--enable-module-actions"], _root).ModuleActions);
        args[1] = "module-observe";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-ui-tree", "--enable-module-actions"], _root));
    }
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-cli-" + Guid.NewGuid().ToString("N"));
    public MaelstromCliTests() => Directory.CreateDirectory(_root);
    private string Report => Path.Combine(_root, "report.json");
    private string[] Args(string command) => ["--cli", command, "--output", Report];
    private string[] CheckArgs() => [.. Args("wizwalker-check"), "--workspace", _root, "--window", "42", "--process-id", "123", "--started-utc-ticks", "456", "--enable-client-observation"];
    private static OperationReceipt Receipt(OperationState state) => new(new(new string('a', 32), WizwalkerOperation.Id, WizwalkerOperation.Version,
        25, true, "inert fixture", new Dictionary<string, string>()), state, DateTime.UtcNow, "fixture", []);
    private static MaelstromCliRuntime Never() => new(() => throw new Exception("window discovery was not expected"), (_, _) => throw new Exception("client access was not expected"));

    [Fact]
    public void ModuleObservationRequiresBothOptInsAndCannotEnableActions()
    {
        var args = CheckArgs(); args[1] = "module-observe";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args, _root));
        var parsed = MaelstromCli.Parse([.. args, "--enable-ui-tree"], _root);
        Assert.Equal("module-observe", parsed.Command); Assert.False(parsed.QuestActions);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-ui-tree", "--enable-quest-actions"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-ui-tree", "--duration-seconds", "60"], _root));
    }

    [Fact]
    public void ConversationNeedsSelectionAndSeparateActionOptIn()
    {
        var args = CheckArgs(); args[1] = "quest-conversation";
        args = [.. args, "--enable-ui-tree", "--include-quest-context", "--selection", Path.Combine(_root, "selected.json")];
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args, _root));
        var parsed = MaelstromCli.Parse([.. args, "--enable-quest-actions"], _root);
        Assert.True(parsed.QuestActions); Assert.NotNull(parsed.SelectionPath);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--enable-quest-actions"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-quest-actions", "--enable-quest-actions"], _root));
    }

    [Fact]
    public void QuestContextIsOptionalAndOnlyAvailableInExplicitUiObservation()
    {
        var args = CheckArgs(); args[1] = "ui-tree-watch";
        args = [.. args, "--enable-ui-tree"];
        Assert.False(MaelstromCli.Parse(args, _root).IncludeQuestContext);
        Assert.True(MaelstromCli.Parse([.. args, "--include-quest-context"], _root).IncludeQuestContext);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--include-quest-context", "--include-quest-context"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--include-quest-context"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. Args("status"), "--include-quest-context"], _root));
    }

    [Fact]
    public void UiTreeNeedsItsOwnExplicitOptInAndKeepsIdentityAndDurationBounds()
    {
        var args = CheckArgs(); args[1] = "ui-tree-watch";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args, _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--enable-ui-tree"], _root));
        var parsed = MaelstromCli.Parse([.. args, "--enable-ui-tree", "--duration-seconds", "60"], _root);
        Assert.Equal("ui-tree-watch", parsed.Command); Assert.Equal(60, parsed.DurationSeconds); Assert.Equal(123, parsed.ProcessId);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-ui-tree", "--enable-ui-tree"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-ui-tree", "--duration-seconds", "61"], _root));
    }

    [Fact]
    public void CombatContextRequiresItsOwnObservationOnlyOptIn()
    {
        var args = CheckArgs(); args[1] = "ui-tree-watch";
        args = [.. args, "--enable-ui-tree"];
        Assert.False(MaelstromCli.Parse(args, _root).IncludeCombatContext);
        Assert.True(MaelstromCli.Parse([.. args, "--include-combat-context"], _root).IncludeCombatContext);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--include-combat-context", "--include-combat-context"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--include-combat-context"], _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. Args("status"), "--include-combat-context"], _root));
        var conversation = CheckArgs(); conversation[1] = "quest-conversation";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. conversation, "--enable-ui-tree", "--include-quest-context",
            "--enable-quest-actions", "--selection", Path.Combine(_root, "selection.json"), "--include-combat-context"], _root));
    }

    [Fact]
    public async Task HelpDoesNotDiscoverOrAttachAndWritesVersionedJson()
    {
        Assert.Equal(0, await MaelstromCli.RunAsync(Args("help"), TextWriter.Null, runtime: Never(), defaultDataRoot: _root));
        using var json = JsonDocument.Parse(File.ReadAllText(Report));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Contains("wizwalker-check", json.RootElement.GetProperty("commands").ToString());
        Assert.False(Directory.Exists(Path.Combine(_root, "operations")));
    }

    [Fact]
    public async Task WindowsReportsStableIdentityWithoutOpeningAClient()
    {
        var runtime = Never() with { Windows = () => [new((nint)42, 123, 456, "WizardGraphicalClient", "Wizard101")] };
        Assert.Equal(0, await MaelstromCli.RunAsync(Args("windows"), TextWriter.Null, runtime: runtime, defaultDataRoot: _root));
        using var json = JsonDocument.Parse(File.ReadAllText(Report));
        var window = json.RootElement.GetProperty("windows")[0];
        Assert.Equal(42, window.GetProperty("handle").GetInt64());
        Assert.Equal(456, window.GetProperty("startedUtcTicks").GetInt64());
    }

    [Fact]
    public async Task StatusDoesNotAcquireSchedulerOrCreateAControlLock()
    {
        Assert.Equal(0, await MaelstromCli.RunAsync(Args("status"), TextWriter.Null, runtime: Never(), defaultDataRoot: _root));
        Assert.False(Directory.Exists(Path.Combine(_root, "operations")));
        Assert.False(Directory.Exists(Path.Combine(_root, "session-locks")));
    }

    [Fact]
    public async Task McpReportsProtocolAndToolsWithoutClientAttachment()
    {
        Assert.Equal(0, await MaelstromCli.RunAsync(Args("mcp"), TextWriter.Null, runtime: Never(), defaultDataRoot: _root));
        using var json = JsonDocument.Parse(File.ReadAllText(Report));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("mcp", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("JSON-RPC 2.0", json.RootElement.GetProperty("protocol").GetString());
        Assert.Equal("tools/maelstrom_mcp_server.py", json.RootElement.GetProperty("serverScript").GetString());
        var tools = json.RootElement.GetProperty("tools").ToString();
        Assert.Contains("plan_combat_turn", tools);
        Assert.Contains("solve_potion_motion_grid", tools);
        Assert.Contains("maelstrom_deck_command", tools);
        Assert.Contains("maelstrom_step_deck_loop", tools);
    }

    [Theory]
    [InlineData("--command", "teleport")]
    [InlineData("--network", "true")]
    [InlineData("--lease-directory", "other-lock")]
    [InlineData("--output", "duplicate.json")]
    public async Task UnknownOrDuplicateOptionsFailBeforeClientAccess(string name, string value)
    {
        Assert.Equal(1, await MaelstromCli.RunAsync([.. CheckArgs(), name, value], TextWriter.Null, runtime: Never(), defaultDataRoot: _root));
        Assert.False(File.Exists(Report));
    }

    [Theory]
    [InlineData("--enable-client-observation")]
    [InlineData("--window")]
    [InlineData("--process-id")]
    [InlineData("--started-utc-ticks")]
    [InlineData("--workspace")]
    public void AllLiveIdentityAndOptInFieldsAreRequired(string remove)
    {
        var args = CheckArgs().ToList(); var index = args.IndexOf(remove);
        args.RemoveRange(index, remove == "--enable-client-observation" ? 1 : 2);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args.ToArray(), _root));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1e4")]
    [InlineData("9223372036854775808")]
    public void InvalidIdentityIsRejected(string value)
    {
        var args = CheckArgs(); args[Array.IndexOf(args, "--window") + 1] = value;
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args, _root));
    }

    [Fact]
    public async Task ExistingReportIsNeverOverwrittenAndNoCheckStarts()
    {
        File.WriteAllText(Report, "preserved");
        Assert.Equal(1, await MaelstromCli.RunAsync(CheckArgs(), TextWriter.Null, runtime: Never(), defaultDataRoot: _root));
        Assert.Equal("preserved", File.ReadAllText(Report));
    }

    [Fact]
    public async Task ExistingStopRequestPreventsClientAccess()
    {
        var stop = Path.Combine(_root, "stop.request"); File.WriteAllText(stop, "stop");
        Assert.Equal(3, await MaelstromCli.RunAsync([.. CheckArgs(), "--stop-file", stop], TextWriter.Null, runtime: Never(), defaultDataRoot: _root));
        Assert.Contains("cancelled", File.ReadAllText(Report));
    }

    [Fact]
    public async Task CancellationBeforeStartPreventsClientAccess()
    {
        Assert.Equal(3, await MaelstromCli.RunAsync(CheckArgs(), TextWriter.Null, new CancellationToken(true), Never(), _root));
    }

    [Theory]
    [InlineData("Completed", 0)]
    [InlineData("Failed", 2)]
    [InlineData("CleanupFailed", 2)]
    [InlineData("Cancelled", 3)]
    [InlineData("TimedOut", 2)]
    public async Task ResultAndExitCodeReflectTheActualReceipt(string stateName, int expected)
    {
        var state = Enum.Parse<OperationState>(stateName);
        var runtime = Never() with
        {
            Check = (options, _) =>
        {
            Assert.Equal(42, options.Handle); Assert.Equal(123, options.ProcessId); Assert.Equal(456, options.StartedUtcTicks);
            Assert.Equal(_root, options.Workspace); return Task.FromResult(Receipt(state));
        }
        };
        Assert.Equal(expected, await MaelstromCli.RunAsync(CheckArgs(), TextWriter.Null, runtime: runtime, defaultDataRoot: _root));
        Assert.Contains(state.ToString(), File.ReadAllText(Report));
    }

    [Fact]
    public void ReadOnlyCommandsRejectLiveOptions() => Assert.Throws<ArgumentException>(() =>
        MaelstromCli.Parse([.. Args("status"), "--enable-client-observation"], _root));

    [Fact]
    public async Task WatchUsesTheSameExplicitIdentityAndOptInContract()
    {
        var args = CheckArgs(); args[1] = "wizwalker-watch";
        var runtime = Never() with
        {
            Check = (options, _) =>
        {
            Assert.Equal("wizwalker-watch", options.Command); Assert.Equal(123, options.ProcessId);
            return Task.FromResult(Receipt(OperationState.Completed));
        }
        };
        Assert.Equal(0, await MaelstromCli.RunAsync(args, TextWriter.Null, runtime: runtime, defaultDataRoot: _root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args.Where(a => a != "--enable-client-observation").ToArray(), _root));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("NaN")]
    public void InvalidWatchDurationIsRejected(string duration)
    {
        var args = CheckArgs(); args[1] = "wizwalker-watch";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--duration-seconds", duration], _root));
    }

    [Fact]
    public void OnlyWatchAllowsBoundedDuration()
    {
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. CheckArgs(), "--duration-seconds", "60"], _root));
        var args = CheckArgs(); args[1] = "wizwalker-watch";
        Assert.Equal(15, MaelstromCli.Parse(args, _root).DurationSeconds);
        Assert.Equal(60, MaelstromCli.Parse([.. args, "--duration-seconds", "60"], _root).DurationSeconds);
    }

    [Fact]
    public void StopFileCannotOverwriteResult() => Assert.Throws<ArgumentException>(() =>
        MaelstromCli.Parse([.. CheckArgs(), "--stop-file", Report], _root));

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
