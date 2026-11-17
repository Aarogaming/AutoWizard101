using System.Globalization;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record MaelstromCliOptions(string Command, string Output, string DataRoot,
    string? Workspace = null, long Handle = 0, int ProcessId = 0, long StartedUtcTicks = 0, string? StopFile = null, int DurationSeconds = 15,
    bool IncludeQuestContext = false, string? SelectionPath = null, bool QuestActions = false, bool IncludeCombatContext = false,
    bool ModuleActions = false, string? NavigationKey = null, int PulseMs = 0, string CycleMode = "round-trip", W101VisitPolicy? VisitPolicy = null, bool IncludePoseContext = false, int WaypointDistance = 0, bool FeedbackWaypoint = false, bool AssistedHold = false, bool WalkUntilLoading = false, string? UntilInteraction = null, bool IncludeResourceContext = false, bool IncludeDanceEntryContext = false, double? HeadingRadians = null, int MovingPoseSeconds = 0, bool SafeMode = false, string? CaptureSession = null);

internal sealed record MaelstromCliRuntime(Func<IReadOnlyList<GameWindow>> Windows,
    Func<MaelstromCliOptions, CancellationToken, Task<OperationReceipt>> Check);

/// <summary>Windowless commands over existing services. Observation sends no input;
/// the selected-conversation command separately admits bounded quest progression.
/// No UI activation, persistent server, generic script runner or second owner.</summary>
internal static class MaelstromCli
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Commands = ["help", "windows", "status", "recover-input", "wizwalker-check", "wizwalker-watch", "ui-tree-watch", "module-observe", "character-inspect", "character-roster", "character-enter", "character-verify", "character-frame", "character-home", "world-gate-inspect", "world-gate-close", "world-gate-travel-current", "travel-card", "idle-cycle", "navigation-probe", "navigation-visit", "quest-conversation", "manual-capture", "mcp"];

    internal static MaelstromCliOptions Parse(string[] args, string defaultDataRoot)
    {
        if (args.Length < 2 || args[0] != "--cli" || !Commands.Contains(args[1]))
            throw new ArgumentException("Use --cli help --output <new-report.json>.");
        var command = args[1];
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool resourceContext = false, danceEntryContext = false;
        bool enabled = false, uiEnabled = false, questContext = false, questActions = false, combatContext = false, moduleActions = false, poseContext = false, safeMode = false;
        string? captureSession = null;

        for (int i = 2; i < args.Length; i++)
        {
            var name = args[i];
            if (name == "--capture-session") { if (captureSession != null || command != "manual-capture" || i + 1 >= args.Length) throw new ArgumentException("Only manual-capture accepts --capture-session <folder>."); captureSession = Path.GetFullPath(args[++i]); continue; }
            if (name == "--safe-mode") { if (safeMode || command != "manual-capture") throw new ArgumentException("Only manual-capture accepts --safe-mode."); safeMode = true; continue; }
            if (name == "--include-dance-entry-context") { if (danceEntryContext) throw new ArgumentException("Duplicate Dance entry opt-in."); danceEntryContext = true; continue; }
            if (name == "--include-resource-context") { if (resourceContext) throw new ArgumentException("Duplicate resource opt-in."); resourceContext = true; continue; }
            if (name == "--enable-module-actions")
            {
                if (moduleActions) throw new ArgumentException("Duplicate module-action opt-in.");
                moduleActions = true; continue;
            }
            if (name == "--include-pose-context")
            {
                if (poseContext) throw new ArgumentException("Duplicate pose-context opt-in.");
                poseContext = true; continue;
            }
            if (name == "--include-combat-context")
            {
                if (combatContext) throw new ArgumentException("Duplicate combat-context opt-in.");
                combatContext = true; continue;
            }
            if (name == "--enable-quest-actions")
            {
                if (questActions) throw new ArgumentException("Duplicate quest-action opt-in.");
                questActions = true; continue;
            }
            if (name == "--include-quest-context")
            {
                if (questContext) throw new ArgumentException("Duplicate quest-context opt-in.");
                questContext = true; continue;
            }
            if (name == "--enable-ui-tree")
            {
                if (uiEnabled) throw new ArgumentException("Duplicate UI-tree opt-in.");
                uiEnabled = true; continue;
            }
            if (name == "--enable-client-observation")
            {
                if (enabled) throw new ArgumentException("Duplicate opt-in.");
                enabled = true; continue;
            }
            if (name is not ("--output" or "--data-root" or "--workspace" or "--window" or "--process-id" or "--started-utc-ticks" or "--stop-file" or "--duration-seconds" or "--selection" or "--key" or "--pulse-ms" or "--cycle-mode" or "--idle-retries" or "--resume-after-quiet" or "--quiet-seconds" or "--waypoint-distance" or "--activate-when-quiet" or "--feedback-waypoint" or "--assisted-hold" or "--walk-until-loading" or "--until-interaction" or "--heading-radians" or "--moving-pose-seconds") ||
                i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(name, args[++i]))
                throw new ArgumentException("Unknown, duplicate or incomplete command option.");
        }

        string Required(string name) => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing {name}.");
        var output = Path.GetFullPath(Required("--output"));
        if (danceEntryContext && command != "ui-tree-watch") throw new ArgumentException("Dance entry candidates require ui-tree-watch.");
        if (resourceContext && command != "ui-tree-watch") throw new ArgumentException("Resources require ui-tree-watch.");
        var root = Path.GetFullPath(values.GetValueOrDefault("--data-root", defaultDataRoot));

        if (command is not ("wizwalker-check" or "wizwalker-watch" or "ui-tree-watch" or "manual-capture" or "module-observe" or "world-gate-inspect" or "world-gate-close" or "world-gate-travel-current" or "travel-card" or "character-home" or "character-frame" or "character-verify" or "character-enter" or "character-roster" or "character-inspect" or "idle-cycle" or "navigation-probe" or "navigation-visit" or "quest-conversation"))
        {
            if (enabled || uiEnabled || questContext || questActions || combatContext || moduleActions || values.Keys.Any(k => k is not ("--output" or "--data-root")))
                throw new ArgumentException("This read-only command does not accept client-control options.");
            return new(command, output, root);
        }

        if (!enabled) throw new ArgumentException("This check requires --enable-client-observation each time.");
        if ((command is "ui-tree-watch" or "module-observe" or "world-gate-inspect" or "world-gate-close" or "world-gate-travel-current" or "travel-card" or "character-home" or "character-frame" or "character-verify" or "character-enter" or "character-roster" or "character-inspect" or "idle-cycle" or "navigation-probe" or "navigation-visit" or "quest-conversation") != uiEnabled)
            throw new ArgumentException("UI-tree and selected conversation commands require --enable-ui-tree.");
        if ((command is "world-gate-inspect" or "world-gate-close" or "world-gate-travel-current" or "travel-card" or "character-home" or "character-frame" or "character-verify" or "character-enter" or "character-roster" or "character-inspect" or "idle-cycle" or "navigation-probe" or "navigation-visit") != moduleActions)
            throw new ArgumentException("Only idle-cycle, navigation-probe and navigation-visit require and accept --enable-module-actions.");

        var untilInteraction = values.GetValueOrDefault("--until-interaction");
        if (untilInteraction is not null && (command != "navigation-visit" || !values.ContainsKey("--assisted-hold") || string.IsNullOrWhiteSpace(untilInteraction) || untilInteraction.Length > 160 || untilInteraction.Any(char.IsControl)))
            throw new ArgumentException("Choose one exact interaction title for an assisted visit.");
        var walkUntilLoading = values.ContainsKey("--walk-until-loading");
        if (walkUntilLoading && (command != "navigation-visit" || values["--walk-until-loading"] != "true" || untilInteraction is not null || !values.ContainsKey("--assisted-hold")))
            throw new ArgumentException("Loading walk requires an explicitly assisted navigation visit.");
        var assistedHold = values.ContainsKey("--assisted-hold");
        if (assistedHold && (command != "navigation-visit" || values["--assisted-hold"] != "true" || !poseContext))
            throw new ArgumentException("Assisted holds require navigation-visit, pose measurement and --assisted-hold true.");
        string? navigationKey = null; int pulseMs = 0;
        if (command is "navigation-probe" or "navigation-visit")
        {
            navigationKey = Required("--key");
            if (navigationKey is not ("W" or "S" or "A" or "D" or "HOME" or "END") ||
                !int.TryParse(Required("--pulse-ms"), NumberStyles.None, CultureInfo.InvariantCulture, out pulseMs) || pulseMs < (untilInteraction is not null ? 0 : 1) || pulseMs > (untilInteraction is not null ? 30000 : walkUntilLoading ? 15000 : assistedHold ? 3000 : 300) || assistedHold && navigationKey is not ("W" or "S"))
                throw new ArgumentException("Navigation teaching requires W, A or D and a 1–300 millisecond pulse.");
        }
        else if (values.ContainsKey("--key") || values.ContainsKey("--pulse-ms"))
            throw new ArgumentException("Only navigation-probe and navigation-visit accept a selected movement pulse.");

        W101VisitPolicy? visitPolicy = null;
        var policyOptions = new[] { "--idle-retries", "--resume-after-quiet", "--quiet-seconds" };
        if (command != "navigation-visit" && policyOptions.Any(values.ContainsKey))
            throw new ArgumentException("Only navigation-visit accepts Idle retry and quiet-resume options.");
        if (command == "navigation-visit")
        {
            if (!int.TryParse(values.GetValueOrDefault("--idle-retries", "2"), NumberStyles.None, CultureInfo.InvariantCulture, out var retries) ||
                !int.TryParse(values.GetValueOrDefault("--quiet-seconds", "2"), NumberStyles.None, CultureInfo.InvariantCulture, out var quiet) ||
                !bool.TryParse(values.GetValueOrDefault("--resume-after-quiet", "false"), out var resume))
                throw new ArgumentException("Use idle-retries 0–2, quiet-seconds 2–10, resume-after-quiet true|false.");
            visitPolicy = new(retries, resume, quiet);
            visitPolicy.Validate();
        }

        if ((navigationKey is "HOME" or "END") && (command != "navigation-visit" || pulseMs != 40 || !poseContext || assistedHold))
            throw new ArgumentException("Home calibration requires one measured 40 ms visit.");
        var cycleMode = values.GetValueOrDefault("--cycle-mode", "round-trip");
        if (cycleMode is not ("round-trip" or "wake" or "idle") || command != "idle-cycle" && values.ContainsKey("--cycle-mode"))
            throw new ArgumentException("Only idle-cycle accepts --cycle-mode round-trip|wake|idle.");

        if (questContext && command is not ("ui-tree-watch" or "quest-conversation"))
            throw new ArgumentException("Only ui-tree-watch accepts --include-quest-context; it records selected quest HUD/dialogue fields without advancing them.");
        if (poseContext && command is not ("ui-tree-watch" or "navigation-visit"))
            throw new ArgumentException("Only ui-tree-watch and navigation-visit accept --include-pose-context.");
        if (combatContext && command != "ui-tree-watch")
            throw new ArgumentException("Only ui-tree-watch accepts --include-combat-context; structural candidates do not authorize combat.");

        var conversation = command == "quest-conversation";
        if (conversation != questActions || conversation && !questContext || !conversation && command != "character-inspect" && command != "character-roster" && command != "character-enter" && command != "character-home" && command != "travel-card" && values.ContainsKey("--selection"))
            throw new ArgumentException("Only quest-conversation accepts --enable-quest-actions and --selection; it also requires --include-quest-context.");

        var selection = conversation || command is "character-inspect" or "character-roster" or "character-enter" or "character-home" or "travel-card" ? Path.GetFullPath(Required("--selection")) : null;

        static long Positive(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed : throw new ArgumentException("Use the exact positive identity values from the windows report.");

        var handle = Positive(Required("--window"));
        var pid = checked((int)Positive(Required("--process-id")));
        var ticks = Positive(Required("--started-utc-ticks"));
        var workspace = Path.GetFullPath(Required("--workspace"));
        var stop = values.TryGetValue("--stop-file", out var stopPath) ? Path.GetFullPath(stopPath) : null;
        if (stop != null && string.Equals(stop, output, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The stop request and result must use different files.");

        int duration = 15;
        if (values.TryGetValue("--duration-seconds", out var durationValue))
        {
            if (command is not ("wizwalker-watch" or "ui-tree-watch" or "manual-capture") || !int.TryParse(durationValue, NumberStyles.None, CultureInfo.InvariantCulture, out duration) ||
                duration is < 1 or > WizwalkerLoadingWatch.MaximumWatchSeconds)
                throw new ArgumentException("Only watch and manual-capture commands accept --duration-seconds, from 1 through 60.");
        }

        if (selection != null && new[] { output, stop }.Contains(selection, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Selection, output and stop paths must be distinct.");

        if (values.TryGetValue("--activate-when-quiet", out var activateText))
        {
            if (command is not ("navigation-visit" or "idle-cycle") || activateText is not ("true" or "false"))
                throw new ArgumentException("Only navigation-visit and idle-cycle accept --activate-when-quiet true|false.");
            visitPolicy = (visitPolicy ?? new()) with { ActivateWhenQuiet = activateText == "true" };
        }

        var waypointDistance = 0;
        if (values.TryGetValue("--waypoint-distance", out var waypointText))
        {
            if (command != "navigation-visit" || !poseContext || navigationKey != "W" ||
                !int.TryParse(waypointText, out waypointDistance) || waypointDistance is < 20 or > 100)
                throw new ArgumentException("A measured W navigation visit accepts a waypoint distance of 20–100 game units.");
        }

        bool feedbackWaypoint = false;
        if (values.TryGetValue("--feedback-waypoint", out var feedbackText))
        {
            if (command != "navigation-visit" || feedbackText is not ("true" or "false"))
                throw new ArgumentException("Only navigation-visit accepts --feedback-waypoint true|false.");
            feedbackWaypoint = feedbackText == "true";
            if (feedbackWaypoint && (waypointDistance == 0 || pulseMs is < 20 or > 100))
                throw new ArgumentException("Feedback requires a measured waypoint and maximum pulse of 20-100 ms.");
        }
        double? headingRadians = null;
        if (values.TryGetValue("--heading-radians", out var headingText))
        {
            if (command != "navigation-visit" || !poseContext || navigationKey is not ("A" or "D") ||
                assistedHold || feedbackWaypoint || waypointDistance != 0 || pulseMs < 20 ||
                !double.TryParse(headingText, NumberStyles.Float, CultureInfo.InvariantCulture, out var heading) ||
                !double.IsFinite(heading) || heading < 0 || heading >= Math.Tau)
                throw new ArgumentException("Heading feedback requires a measured A/D visit, 20-300 ms pulses and a heading in [0, 2pi).");
            headingRadians = heading;
        }
        int movingPoseSeconds = 0;
        if (values.TryGetValue("--moving-pose-seconds", out var movingText) &&
            (command != "ui-tree-watch" || !poseContext || !int.TryParse(movingText, out movingPoseSeconds) || movingPoseSeconds is < 1 or > 3))
            throw new ArgumentException("Moving sampling requires ui-tree-watch, pose opt-in and 1..3 seconds.");
        return new(command, output, root, workspace, handle, pid, ticks, stop, duration, questContext, selection, questActions, combatContext, moduleActions, navigationKey, pulseMs, cycleMode, visitPolicy, poseContext, waypointDistance, feedbackWaypoint, assistedHold, walkUntilLoading, untilInteraction, resourceContext, danceEntryContext, headingRadians, movingPoseSeconds, safeMode, captureSession);
    }

    internal static async Task<int> RunAsync(string[] args, TextWriter error, CancellationToken token = default,
        MaelstromCliRuntime? runtime = null, string? defaultDataRoot = null)
    {
        FileStream? output = null;
        try
        {
            var options = Parse(args, defaultDataRoot ?? WorkshopHost.DataRoot(AppContext.BaseDirectory));
            // Reserve the result before doing anything. Never overwrite user data or a prior report.
            output = new FileStream(options.Output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            runtime ??= new(GameWindows.List, CheckAsync);
            object result;
            int exit = 0;

            if (options.Command == "recover-input")
            {
                var recovery = await CliInputRecovery.RunAsync(token).ConfigureAwait(false);
                result = new { schemaVersion = 1, command = options.Command, recovery };
                exit = recovery.Stage == RecoveryFlowStage.Restored ? 0 : 2;
            }
            else if (options.Command == "help")
                result = new { schemaVersion = 1, command = "help", commands = Commands, required = "--output <new-report.json>",
                    check = "wizwalker-check --workspace <repo> --window <handle> --process-id <pid> --started-utc-ticks <ticks> --enable-client-observation [--stop-file <path>]",
                    watch = "wizwalker-watch uses the same identity/opt-in options; default 15 seconds/32 observations. Optional --duration-seconds 1..60, at most 128 serial observations under one control lease.",
                    uiTree = "ui-tree-watch also requires --enable-ui-tree; uses one scoped root-window hook with verified restoration. Staggered loading/UI reads, no inputs, default 15 seconds, maximum 60. Optional --include-quest-context reads only known objective/dialogue fields and the advance control. --include-combat-context adds bounded source-layout card/participant/duel candidates, raw layout rectangles and a combat-readiness report. Selection, geometry, input response and battle outcomes remain unqualified. Neither option sends inputs. --include-resource-context obtains two UI samples then a serialized stats snapshot; HUD-correlated health/mana/energy current and globe maxima become facts; potion charge/capacity require PotionButton agreement. Other named fields remain candidates. Qualified readings also produce resource-facts.json through the shared registry projection. --include-dance-entry-context exports structural PetGameTracks candidates only, never click or selection qualification.",
                    moduleObserve = "module-observe requires selected identity, --enable-client-observation and --enable-ui-tree. One UI-tree reading followed by current game-only capture and local OCR; no input or navigation. Records candidate prompt, menu controls and cleanup evidence without qualifying gameplay.",
                    idleCycle = "idle-cycle additionally requires --enable-module-actions and W101 foreground. --cycle-mode round-trip (default) begins in Idle and returns to Idle; wake leaves Idle and closes Options; idle enters Idle from awake/Options or verifies existing Idle. At most three actions/60 seconds under the same owner; loading pauses and takeover stops. No movement, purchases or quest progression.",
                    navigationProbe = "navigation-probe requires the same observation/action opt-ins, --key W|A|D and --pulse-ms 1..300. One explicitly selected teaching pulse with before/after evidence; no automatic pathfinding or arrival claim. W101 must be foreground, awake and outside menus/dialogue/loading.",
                    navigationVisit = "navigation-visit owns wake, one teaching pulse and return to Idle within 85 seconds. W101 must already be foreground in native Idle. No route arrival is inferred.",
                    idleRecovery = "navigation-visit: --idle-retries 0..2 (default 2), --resume-after-quiet true|false (default false), --quiet-seconds 2..10 (default 2). Retries use fresh Idle observations within 40 seconds. Quiet recovery resumes only cleanup, never gameplay. Escape, cancellation and uncertain cleanup stop input.",
                    conversation = "quest-conversation requires all three observation opt-ins, --enable-quest-actions and --selection <json>. Up to 12 selected dialogue pages by default, 60 seconds; changed pages required between Space pulses; verify a new objective after closing. Stops on desktop activity. No navigation, combat or Idle cleanup.",
                    manualCapture = "manual-capture requires exact window identity and --output; starts a persistent nonactivating overlay with Play/Pause/Stop hotkeys (Ctrl+Alt+P/S/R/Q). NORMAL is default; --safe-mode disables automatic resume. Bounded by --duration-seconds 1..60 or a fresh --stop-file. The host never dispatches game input; the pure gate requires quiet+countdown+verification and explicit pause/stop is never auto-cleared.",
                    note = "No window activation. Observation commands send no input; the separately enabled quest-conversation can advance the selected quest. Existing scheduler/control lease. Exit 0 completed, 2 run not completed, 3 cancelled, 1 command/setup error." };
            else if (options.Command == "windows")
                result = new { schemaVersion = 1, command = "windows", windows = runtime.Windows().Select(w => new
                    { handle = w.Handle.ToInt64(), processId = w.ProcessId, startedUtcTicks = w.StartedUtcTicks, processName = w.ProcessName, title = w.Title }).ToArray() };
            else if (options.Command == "status")
                result = new { schemaVersion = 1, command = "status", dataRoot = options.DataRoot, runs = OperationScheduler.History(options.DataRoot),
                    note = "Saved receipts only; status does not acquire control or clear recovery." };
            else if (options.Command == "mcp")
                result = new { schemaVersion = 1, command = "mcp", serverScript = "tools/maelstrom_mcp_server.py", protocol = "JSON-RPC 2.0", transport = "stdio",
                    tools = new[] { "list_game_workspaces", "get_w101_policy_state", "validate_routine_graph", "evaluate_combat_damage", "solve_potion_motion_grid", "plan_combat_turn", "compile_macro_demonstration", "maelstrom_get_capability_catalog",
                        "maelstrom_get_session_telemetry", "maelstrom_inspect_routine_deck", "maelstrom_deal_job_card", "maelstrom_step_deck_loop", "maelstrom_deck_command" },
                    launcher = "Launch Maelstrom MCP.cmd" };
            else if (options.Command == "manual-capture")
            {
                token.ThrowIfCancellationRequested();
                var selected = GameWindows.Read((nint)options.Handle) ?? throw new InvalidOperationException("The selected window is unavailable.");
                if (selected.ProcessId != options.ProcessId || selected.StartedUtcTicks != options.StartedUtcTicks ||
                    !string.Equals(selected.ProcessName, "WizardGraphicalClient", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The selected client identity changed. Refresh the windows report.");
                if (options.StopFile != null && File.Exists(options.StopFile)) throw new OperationCanceledException();
                var mode = options.SafeMode ? ManualCaptureMode.Safe : ManualCaptureMode.Normal;
                var timeline = options.CaptureSession == null ? null :
                    new ManualCaptureTimeline(options.CaptureSession, DateTime.UtcNow.Ticks, options.Workspace);
                await using var host = await ManualCaptureHost.StartAsync(selected, mode, token).ConfigureAwait(false);
                timeline?.Add(host.Snapshot, DateTime.UtcNow.Ticks);
                var deadline = DateTime.UtcNow.AddSeconds(options.DurationSeconds);
                var reason = "duration-limit";
                while (!token.IsCancellationRequested && DateTime.UtcNow < deadline &&
                       !(options.StopFile != null && File.Exists(options.StopFile)))
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                    timeline?.Add(host.Snapshot, DateTime.UtcNow.Ticks);
                }
                timeline?.Write();
                if (token.IsCancellationRequested) reason = "cancelled";
                else if (options.StopFile != null && File.Exists(options.StopFile)) reason = "stop-file";
                var snapshot = host.Snapshot;
                result = new { schemaVersion = 1, command = options.Command, mode = mode.ToString(), reason,
                    target = new { handle = selected.Handle.ToInt64(), processId = selected.ProcessId, startedUtcTicks = selected.StartedUtcTicks, processName = selected.ProcessName },
                    hotkeysRegistered = host.RegisteredHotkeys, snapshot,
                    note = "Manual capture only. No game input dispatched; explicit pause/stop is never auto-cleared." };
                exit = reason == "cancelled" ? 3 : 0;
            }
            else
            {
                token.ThrowIfCancellationRequested();
                if (options.StopFile != null && File.Exists(options.StopFile)) throw new OperationCanceledException();
                var receipt = await runtime.Check(options, token).ConfigureAwait(false);
                exit = receipt.State == OperationState.Completed ? 0 : receipt.State == OperationState.Cancelled ? 3 : 2;
                result = new { schemaVersion = 1, command = options.Command, receipt };
            }

            await JsonSerializer.SerializeAsync(output, result, Json).ConfigureAwait(false);
            await output.FlushAsync().ConfigureAwait(false);
            return exit;
        }
        catch (Exception ex)
        {
            var cancelled = ex is OperationCanceledException;
            var message = cancelled ? "Stopped before starting a client check." : ex.Message;
            if (output != null)
            {
                try
                {
                    output.SetLength(0); output.Position = 0;
                    await JsonSerializer.SerializeAsync(output, new { schemaVersion = 1, status = cancelled ? "cancelled" : "command-error", message }, Json).ConfigureAwait(false);
                    await output.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException) { /* Retain the failure exit even if report storage fails. */ }
            }
            await error.WriteLineAsync(message).ConfigureAwait(false);
            return cancelled ? 3 : 1;
        }
        finally { if (output != null) await output.DisposeAsync().ConfigureAwait(false); }
    }

    private static async Task<OperationReceipt> CheckAsync(MaelstromCliOptions options, CancellationToken token)
    {
        var window = GameWindows.Read((nint)options.Handle);
        if (window == null || window.ProcessId != options.ProcessId || window.StartedUtcTicks != options.StartedUtcTicks || window.ProcessName != "WizardGraphicalClient")
            throw new InvalidOperationException("The selected W101 identity changed. List windows again; no client was opened.");
        var target = WizwalkerTarget.FromWindow(window);
        // Same durable scheduler and shared per-user control lease as the GUI.
        // If the GUI owns this data root, the scheduler refuses a second controller.
        await using var scheduler = new OperationScheduler(options.DataRoot);
        token.ThrowIfCancellationRequested();
        var launch = WizwalkerLaunch.FromWorkspace(options.Workspace!);

        if (options.Command is "world-gate-inspect" or "world-gate-close" or "world-gate-travel-current" or "travel-card" or "character-home" or "character-frame" or "character-verify" or "character-enter" or "character-roster" or "character-inspect" or "idle-cycle" or "navigation-probe" or "navigation-visit")
        {
            var moduleLaunch = launch with { Worker = Path.Combine(options.Workspace!, "tools", "w101_ui_tree.py") };
            var prepared = options.Command switch
            {
                "character-home" => W101CharacterOperation.PrepareHome(new(target, true), moduleLaunch, options.ModuleActions, options.SelectionPath!),
                "character-frame" => W101CharacterOperation.PrepareFrame(new(target, true), moduleLaunch, options.ModuleActions),
                "character-verify" => W101CharacterOperation.PrepareVerify(new(target, true), moduleLaunch, options.ModuleActions),
                "character-enter" => W101CharacterOperation.PrepareRoster(new(target, true), moduleLaunch, options.ModuleActions, options.SelectionPath!, true),
                "character-roster" => W101CharacterOperation.PrepareRoster(new(target, true), moduleLaunch, options.ModuleActions, options.SelectionPath!),
                "travel-card" => W101WorldGateVisit.Prepare(new(target, true), moduleLaunch, options.ModuleActions, options: W101TravelCard.Load(options.SelectionPath!)),
                "world-gate-travel-current" => W101WorldGateVisit.Prepare(new(target, true), moduleLaunch, options.ModuleActions, travelCurrent: true),
                "world-gate-close" => W101WorldGateVisit.Prepare(new(target, true), moduleLaunch, options.ModuleActions, closeOnly: true),
                "world-gate-inspect" => W101WorldGateVisit.Prepare(new(target, true), moduleLaunch, options.ModuleActions),
                "character-inspect" => W101CharacterOperation.Prepare(new(target, true), moduleLaunch, options.ModuleActions, options.SelectionPath!),
                "idle-cycle" => W101IdleCycleOperation.Prepare(new(target, true), moduleLaunch, options.ModuleActions, mode: options.CycleMode, activateWhenQuiet: options.VisitPolicy?.ActivateWhenQuiet == true),
                "navigation-visit" => W101NavigationVisit.Prepare(new(target, true), moduleLaunch, options.ModuleActions, options.NavigationKey!, options.PulseMs, policy: options.VisitPolicy, measurePose: options.IncludePoseContext, waypointDistance: options.WaypointDistance, feedbackWaypoint: options.FeedbackWaypoint, assistedHold: options.AssistedHold, walkUntilLoading: options.WalkUntilLoading, untilInteraction: options.UntilInteraction, headingRadians: options.HeadingRadians),
                _ => W101NavigationProbe.Prepare(new(target, true), moduleLaunch, options.ModuleActions, options.NavigationKey!, options.PulseMs)
            };
            var cycle = scheduler.Submit(prepared.Request, prepared.Execute);
            using var cancelled = token.Register(cycle.Cancel);
            while (!cycle.Completion.IsCompleted)
            {
                if (options.StopFile != null && File.Exists(options.StopFile)) cycle.Cancel();
                await Task.WhenAny(cycle.Completion, Task.Delay(100)).ConfigureAwait(false);
            }
            return await cycle.Completion.ConfigureAwait(false);
        }

        var ticket = options.Command switch
        {
            "module-observe" => W101ModuleProbe.Submit(scheduler, new(target, true),
                launch with { Worker = Path.Combine(options.Workspace!, "tools", "w101_ui_tree.py") }),
            "quest-conversation" => W101QuestConversationOperation.Submit(scheduler, new(target, true),
                launch with { Worker = Path.Combine(options.Workspace!, "tools", "w101_ui_tree.py") }, options.SelectionPath!, options.QuestActions),
            "ui-tree-watch" => W101UiTreeOperation.Submit(scheduler, new(target, true),
                launch with { Worker = Path.Combine(options.Workspace!, "tools", "w101_ui_tree.py") },
                uiTreeEnabled: true, durationSeconds: options.DurationSeconds, includeQuestContext: options.IncludeQuestContext, includeCombatContext: options.IncludeCombatContext, includePoseContext: options.IncludePoseContext, includeResourceContext: options.IncludeResourceContext, includeDanceEntryContext: options.IncludeDanceEntryContext, movingPoseSeconds: options.MovingPoseSeconds),
            "wizwalker-watch" => WizwalkerLoadingWatch.Submit(scheduler, new(target, true), launch, durationSeconds: options.DurationSeconds),
            _ => WizwalkerOperation.Submit(scheduler, new(target, true), launch)
        };
        using var registration = token.Register(ticket.Cancel);
        while (!ticket.Completion.IsCompleted)
        {
            if (options.StopFile != null && File.Exists(options.StopFile)) ticket.Cancel();
            await Task.WhenAny(ticket.Completion, Task.Delay(100)).ConfigureAwait(false);
        }
        return await ticket.Completion.ConfigureAwait(false);
    }
}
