namespace ProjectMaelstrom.Utilities;

/// <summary>Qualification visit: exact gate prompt, one X, menu evidence, close,
/// native Idle. Optional travel uses the current selection; never presses Home.</summary>
internal static class W101WorldGateVisit
{
    internal static bool PromptMatches(W101UiTreeResult reading, long now) =>
        W101UiTreeOperation.ValidInteractionTitle(reading, now) &&
        reading.InteractionTitle == "World Gate" && reading.StableInteractionTitle == "World Gate" &&
        reading.Loading == false && reading.States.GetValueOrDefault("dialogue") == false &&
        reading.States.GetValueOrDefault("menu") == false;

    internal static bool CanReinteractOverlay(W101UiTreeResult reading, long now) =>
        W101UiTreeOperation.ValidInteractionTitle(reading, now) && reading.Loading == false &&
        reading.States.GetValueOrDefault("nativeIdle") == false && reading.States.GetValueOrDefault("menu") == true &&
        reading.States.GetValueOrDefault("dialogue") == false &&
        reading.InteractionTitle == "World Gate" && reading.StableInteractionTitle == "World Gate";

    internal static bool MenuMatches(string text)
    {
        var normalized = string.Concat(text.ToUpperInvariant().Where(char.IsLetterOrDigit));
        return normalized.Contains("SPIRALMAP") &&
            normalized.Contains("GOTOWORLD") && normalized.Contains("EXIT");
    }

    internal static PreparedDeckJob Prepare(WizwalkerSession session, WizwalkerLaunch launch, bool enabled, bool closeOnly = false, bool travelCurrent = false, W101TravelOptions? options = null)
    {
        options?.Validate();
        var baseRequest = options?.ReturnHome == true
            ? W101NavigationVisit.Prepare(session, launch, enabled, "W", 40, measurePose: true).Request
            : W101IdleCycleOperation.Prepare(session, launch, enabled).Request;
        var request = baseRequest with
        { Operation = options is not null ? W101TravelCard.Id : travelCurrent ? "w101.world-gate-travel-current" : closeOnly ? "w101.world-gate-close" : "w101.world-gate-inspect", Version = "w101-world-gate-v3", TimeoutSeconds = options is null ? 110 : 300 };
        return new(request, async (context, token) =>
        {
            var target = session.Target;
            await using var scope = await W101ControlScope.CreateAsync(context, target.Window, GameWindows.ClientSize(target.Window), token, true, resumable: true).ConfigureAwait(false);
            var geometry = WindowObserver.GetCaptureSize(target.Window);
            OperationContext Stage(string name)
            {
                var directory = Path.Combine(context.Directory, name); Directory.CreateDirectory(directory);
                return context with { Directory = directory, Request = context.Request with { Id = Guid.NewGuid().ToString("N") } };
            }
            W101PosePoint? homeOrigin = null;
            string? selectionFailure = null;
            var idleRuntime = W101IdleCycleOperation.CreateRuntime(target, launch, scope, geometry);
            async Task<RoutineReceipt> Pulse(ModuleAction action, Func<bool> ready)
            {
                var proposal = new ModuleDecision("world-gate", "inspect", "explicit-gate-test", Guid.NewGuid().ToString("N"), action.ActionId, action);
                return await W101ModuleInput.DispatchAsync(proposal, context, ready,
                    (key, release) => W101ModuleInput.Send(target, key, release), FrameTimeline.TimestampMs, token, scope.Ready).ConfigureAwait(false);
            }
            var idleAttempted = false;
            async Task<OperationOutcome> RestoreIdle()
            {
                idleAttempted = true;
                var finalizer = await FinalizeIdleAsync(context, scope.Ready, scope.WaitForFocusAsync,
                    (attempt, cancellation) => W101IdleCycleOperation.RunOwnedAsync(Stage("idle-attempt-" + attempt), target, idleRuntime, cancellation, "idle"), token).ConfigureAwait(false);
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, "idle-finalizer.json"), finalizer);
                return finalizer.Outcome ?? new(OperationState.Failed, finalizer.SkippedReason ?? "Idle not observed.", [context.Directory]);
            }
            async Task<OperationOutcome> Body()
            {
            if (closeOnly)
            {
                var entry = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 5, token).ConfigureAwait(false);
                var entryTrace = Path.Combine(context.Directory, "read-05", "ui-observations.jsonl");
                if (entry.Loading == false && entry.States.GetValueOrDefault("nativeIdle") == true &&
                    W101CharacterOperation.VisibleControl(entryTrace, "spiralScene"))
                {
                    var woke = await Pulse(W101IdleCycle.Wake, () => scope.Ready() && DateTime.UtcNow.Ticks - entry.StatesObservedUtcTicks < TimeSpan.FromSeconds(2).Ticks).ConfigureAwait(false);
                    if (!woke.Accepted || !woke.InputReleased) return new(OperationState.Failed, "Gate Idle wake rejected.", [context.Directory]);
                    var options = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 6, token).ConfigureAwait(false);
                    var optionTrace = Path.Combine(context.Directory, "read-06", "ui-observations.jsonl");
                    if (options.Loading == false && new[] { "SettingPage", "SettingsPage" }.Any(name => W101CharacterOperation.VisibleControl(optionTrace, name)))
                    {
                        var dismissed = await Pulse(W101IdleCycle.CloseMenu, () => scope.Ready() && DateTime.UtcNow.Ticks - options.StatesObservedUtcTicks < TimeSpan.FromSeconds(2).Ticks).ConfigureAwait(false);
                        if (!dismissed.Accepted || !dismissed.InputReleased) return new(OperationState.Failed, "Settings dismissal rejected.", [context.Directory]);
                    }
                }
            }
            if (!closeOnly)
            {
            var wake = await W101IdleCycleOperation.RunOwnedAsync(Stage("wake"), target, idleRuntime, token, "wake").ConfigureAwait(false);
            if (wake.State != OperationState.Completed) return wake;
            if (options?.ReturnHome == true)
            {
                var home = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 9, token, true).ConfigureAwait(false);
                if (home.Loading != false || !W101CharacterOperation.VisibleControl(Path.Combine(context.Directory, "read-09", "ui-observations.jsonl"), "OpenObjectPlacement") ||
                    home.PoseCandidate?.Position is not { } origin || !W101UiTreeOperation.ValidPose(home.PoseCandidate, true, DateTime.UtcNow.Ticks))
                {
                    var cleanup = await RestoreIdle().ConfigureAwait(false);
                    return new(OperationState.Failed, "Home-return card requires a verified house origin; " + cleanup.Message, [context.Directory]);
                }
                homeOrigin = origin;
            }
            var reading = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 0, token).ConfigureAwait(false);
            if (!PromptMatches(reading, DateTime.UtcNow.Ticks))
                {
                    var cleanup = await RestoreIdle().ConfigureAwait(false);
                    return new(OperationState.Failed, "Exact World Gate prompt not verified; no X sent. " + cleanup.Message, [context.Directory]);
                }
            var receipt = await Pulse(new("game.interact", "keyboard-pulse", "X", 40, "w101.interaction"),
                () => scope.Ready() && PromptMatches(reading, DateTime.UtcNow.Ticks)).ConfigureAwait(false);
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "interaction-receipt.json"), receipt);
            if (!receipt.Accepted || !receipt.InputReleased) return new(OperationState.Failed, "Gate interaction was not confirmed.", [context.Directory]);
            await Task.Delay(500, token).ConfigureAwait(false);
            }
            if (closeOnly)
            {
                var overlap = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 7, token).ConfigureAwait(false);
                if (CanReinteractOverlay(overlap, DateTime.UtcNow.Ticks) &&
                    W101CharacterOperation.VisibleControl(Path.Combine(context.Directory, "read-07", "ui-observations.jsonl"), "spiralScene"))
                {
                    var reopened = await Pulse(new("game.interact", "keyboard-pulse", "X", 40, "w101.interaction"),
                        () => scope.Ready() && CanReinteractOverlay(overlap, DateTime.UtcNow.Ticks)).ConfigureAwait(false);
                    GameExperiments.WriteAtomic(Path.Combine(context.Directory, "overlap-reinteraction.json"), reopened);
                    if (!reopened.Accepted || !reopened.InputReleased) return new(OperationState.Failed, "Gate reinteraction rejected.", [context.Directory]);
                }
            }
            var after = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 1, token).ConfigureAwait(false);
            var frame = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(context.Directory, "gate-menu.png"), frame.Png, token).ConfigureAwait(false);
            var ocr = new PetDanceMenuOcr(); await ocr.PrepareAsync(token).ConfigureAwait(false);
            using var bytes = new MemoryStream(frame.Png); using var bitmap = new Bitmap(bytes);
            var text = await ocr.ReadAsync(bitmap, token).ConfigureAwait(false);
            var trace = Path.Combine(context.Directory, "read-01", "ui-observations.jsonl");
            var treeMenu = new[] { "spiralScene", "teleportButton", "cancelButton", "optionWindow" }.All(name => W101CharacterOperation.VisibleControl(trace, name));
            var menu = treeMenu && MenuMatches(text.Text);
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "gate-menu.json"), new { menuMatched = menu, treeMenu, loading = after.Loading, worldSelectionDispatched = false, menuLabels = text.Words.Where(w => w.Bounds.Left > geometry.Width * .2 && w.Bounds.Right < geometry.Width * .8).Select(w => w.Text).Take(60).ToArray() });
            if (!menu || after.Loading != false || !scope.Ready())
                return new(OperationState.Unknown, "Gate screen captured; menu qualification needs review. No destination selected.", [context.Directory]);
            if (options is not null)
            {
                async Task<W101TravelCard.MenuView> ReadMenu(string name)
                {
                    var child = Stage(name);
                    var state = await W101UiTreeOperation.ReadOwnedAsync(child, target, launch, 0, token).ConfigureAwait(false);
                    var picture = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(Path.Combine(child.Directory, "menu.png"), picture.Png, token).ConfigureAwait(false);
                    using var input = new MemoryStream(picture.Png); using var pixels = new Bitmap(input);
                    var labels = await ocr.ReadAsync(pixels, token).ConfigureAwait(false);
                    return new(state, picture, labels, Path.Combine(child.Directory, "read-00", "ui-observations.jsonl"));
                }
                async Task<bool> Select(W101TravelCard.MenuView view, RectangleF bounds)
                {
                    var receipt = await new PetDanceMenuInput(scope.Tag).DispatchAsync(new(PetDanceLifecycleAction.SelectWorldGate, true, "verified-world-menu-control", bounds), target.Window, geometry,
                        () => scope.Ready() && view.Reading.Loading == false && DateTime.UtcNow.Ticks - view.Reading.StatesObservedUtcTicks is >= 0 and <= 25000000 && FrameTimeline.TimestampMs() - view.Frame.CapturedMs < 2500, token, scope.Ready).ConfigureAwait(false);
                    GameExperiments.WriteAtomic(Path.Combine(context.Directory, "selection-" + Guid.NewGuid().ToString("N") + ".json"), receipt);
                    return receipt.Accepted && receipt.InputReleased;
                }
                var selected = await W101TravelCard.SelectAsync(new(after, frame, text, trace), options, ReadMenu, Select, scope.Ready).ConfigureAwait(false);
                after = selected.View.Reading; frame = selected.View.Frame; text = selected.View.Text; trace = selected.View.Trace;
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, "world-selection.json"), new { options.Destination, selected.Verified, selected.Reason });
                if (!selected.Verified) selectionFailure = selected.Reason;
            }
            if ((travelCurrent || options is not null) && selectionFailure is null)
            {
                if (after.Loading != false || !MenuMatches(text.Text) || options is not null && !W101TravelCard.SelectionVerified(trace, options.Destination))
                    return new(OperationState.Failed, "Selected destination menu changed; travel withheld.", [context.Directory]);
                var go = text.Words.Where(w => w.Text.Equals("GO", StringComparison.OrdinalIgnoreCase) && w.Bounds.Top > geometry.Height * .75 && w.Bounds.Left > geometry.Width * .55).ToArray();
                if (go.Length != 1) return new(OperationState.Unknown, "Unique Go To World control unavailable.", [context.Directory]);
                var travel = await new PetDanceMenuInput(scope.Tag).DispatchAsync(new(PetDanceLifecycleAction.TravelWorldGate, true, "current-selected-world", go[0].Bounds),
                    target.Window, geometry, () => scope.Ready() && FrameTimeline.TimestampMs() - frame.CapturedMs < 2500, token, scope.Ready).ConfigureAwait(false);
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, "travel-receipt.json"), travel);
                if (!travel.Accepted || !travel.InputReleased) return new(OperationState.Failed, "World travel input rejected.", [context.Directory]);
                var index = 0;
                var loadingLaunch = launch with { Worker = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py") };
                async Task<bool?> Loading(CancellationToken ct)
                {
                    var owner = Stage("transition-" + index++);
                    var outcome = await WizwalkerOperation.ReadOwnedAsync(owner, target, loadingLaunch, null, ct).ConfigureAwait(false);
                    if (outcome.State != OperationState.Completed) return null;
                    return System.Text.Json.JsonSerializer.Deserialize<WizwalkerResult>(GameExperiments.BoundedBytes(Path.Combine(owner.Directory, "adapter-result.json"), 16384))?.Loading;
                }
                var transition = await W101NavigationTransition.WaitAsync(Loading, scope.Ready, FrameTimeline.TimestampMs, token, requireLoading: true).ConfigureAwait(false);
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, "world-transition.json"), transition);
                if (!transition.Ready) return new(OperationState.Unknown, "World loading transition not verified; no Home sent.", [context.Directory]);
                var arrival = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 8, token).ConfigureAwait(false);
                var image = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(context.Directory, "world-arrival.png"), image.Png, token).ConfigureAwait(false);
                var arrived = arrival.Loading == false && arrival.States.GetValueOrDefault("menu") == false &&
                    W101CharacterOperation.VisibleControl(Path.Combine(context.Directory, "read-08", "ui-observations.jsonl"), "windowHUD");
                bool homeVerified = false;
                System.Diagnostics.Stopwatch? cooldownClock = null;
                var cooldownSamples = new List<object>();
                bool cooldownBlocked = false, cooldownCleared = false;
                void RecordCooldown(string trace)
                {
                    var available = W101TravelCard.HomeAvailable(trace);
                    cooldownSamples.Add(new { elapsedSeconds = cooldownClock!.Elapsed.TotalSeconds, observedUtcTicks = DateTime.UtcNow.Ticks, available, trace });
                    if (available == false) cooldownBlocked = true;
                    if (available == true && cooldownBlocked) cooldownCleared = true;
                    GameExperiments.WriteAtomic(Path.Combine(context.Directory, "home-cooldown.json"), new { cooldownBlocked, cooldownCleared, samples = cooldownSamples, timing = "sample intervals from Home dispatch; not exact expiry" });
                }
                string homeReason = "not-requested";
                if (arrived && options?.ReturnHome == true)
                {
                    var wait = System.Diagnostics.Stopwatch.StartNew();
                    for (var attempt = 0; attempt < 40; attempt++)
                    {
                        if (!await ReleasedCheckpointAsync(context, scope.Ready, scope.WaitForFocusAsync, token).ConfigureAwait(false))
                        { homeReason = "focus-resume-unavailable"; break; }
                        // Cooldown time continues during focus loss; acquire a new reading
                        // after resumption rather than reusing pre-pause button evidence.
                        var child = Stage("home-ready-" + attempt);
                        var state = await W101UiTreeOperation.ReadOwnedAsync(child, target, launch, 0, token).ConfigureAwait(false);
                        var available = state.Loading == false && state.States.GetValueOrDefault("menu") == false && state.States.GetValueOrDefault("dialogue") == false
                            ? W101TravelCard.HomeAvailable(Path.Combine(child.Directory, "read-00", "ui-observations.jsonl")) : null;
                        homeReason = W101TravelCard.HomeDecision(available, (int)wait.Elapsed.TotalSeconds, options.MaximumHomeWaitSeconds);
                        GameExperiments.WriteAtomic(Path.Combine(child.Directory, "availability.json"), new { available, decision = homeReason, elapsedSeconds = wait.Elapsed.TotalSeconds });
                        if (homeReason == "ready")
                        {
                            cooldownClock = System.Diagnostics.Stopwatch.StartNew();
                            var homeReceipt = await Pulse(W101CharacterOperation.GoHome, () => scope.Ready() && DateTime.UtcNow.Ticks - state.StatesObservedUtcTicks < TimeSpan.FromSeconds(2).Ticks).ConfigureAwait(false);
                            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "home-receipt.json"), homeReceipt);
                            if (!homeReceipt.Accepted || !homeReceipt.InputReleased) { homeReason = "input-rejected"; break; }
                            var homeTransition = await W101NavigationTransition.WaitAsync(Loading, scope.Ready, FrameTimeline.TimestampMs, token, requireLoading: true).ConfigureAwait(false);
                            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "home-transition.json"), homeTransition);
                            if (!homeTransition.Ready) { homeReason = "loading-unverified"; break; }
                            var home = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 10, token, true).ConfigureAwait(false);
                            if (options.CaptureHomeCooldown) RecordCooldown(Path.Combine(context.Directory, "read-10", "ui-observations.jsonl"));
                            var house = W101CharacterOperation.VisibleControl(Path.Combine(context.Directory, "read-10", "ui-observations.jsonl"), "OpenObjectPlacement");
                            homeVerified = home.Loading == false && house && homeOrigin is not null && home.PoseCandidate?.Position is { } point &&
                                W101UiTreeOperation.ValidPose(home.PoseCandidate, true, DateTime.UtcNow.Ticks) && W101TravelCard.NearOrigin(homeOrigin, point);
                            homeReason = homeVerified ? "house-origin-observed" : "house-origin-not-confirmed";
                            var homeImage = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
                            await File.WriteAllBytesAsync(Path.Combine(context.Directory, "home-arrival.png"), homeImage.Png, token).ConfigureAwait(false);
                            break;
                        }
                        if (homeReason != "wait") break;
                        await Task.Delay(2000, token).ConfigureAwait(false);
                    }
                }
                var finish = await RestoreIdle().ConfigureAwait(false);
                // Observe only after cleanup: no further port or wake input is sent.
                if (options?.CaptureHomeCooldown == true && homeVerified && finish.State == OperationState.Completed && cooldownClock is not null)
                {
                    for (var sampleIndex = 0; sampleIndex < 20 && cooldownClock.Elapsed.TotalSeconds < 45 && !cooldownCleared && scope.Ready(); sampleIndex++)
                    {
                        var child = Stage("cooldown-" + sampleIndex);
                        var state = await W101UiTreeOperation.ReadOwnedAsync(child, target, launch, 0, token).ConfigureAwait(false);
                        if (state.Loading != false || state.States.GetValueOrDefault("nativeIdle") != true || state.StableStates.GetValueOrDefault("nativeIdle") != true) break;
                        RecordCooldown(Path.Combine(child.Directory, "read-00", "ui-observations.jsonl"));
                        if (!cooldownCleared) await Task.Delay(500, token).ConfigureAwait(false);
                    }
                }
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, "travel-result.json"), new { transition, worldGameplayObserved = arrived, destinationIdentityVerified = false, selectedWorldVerified = options is not null, homeVerified, homeReason, idleVerified = finish.State == OperationState.Completed });
                return new(arrived && (options?.ReturnHome != true || homeVerified) && finish.State == OperationState.Completed ? OperationState.Completed : OperationState.Failed,
                    "Current selected world travel: loading and gameplay observed=" + arrived + "; " + finish.Message + ". Destination identity needs arrival evidence review; Home: " + homeReason, [context.Directory]);
            }
            if (after.Loading != false || !MenuMatches(text.Text) ||
                !new[] { "spiralScene", "teleportButton", "cancelButton", "optionWindow" }.All(name => W101CharacterOperation.VisibleControl(trace, name)))
                return new(OperationState.Unknown, "Latest menu is not a qualified World Gate; close withheld.", [context.Directory]);
            var exits = text.Words.Where(w => w.Text.Equals("EXIT", StringComparison.OrdinalIgnoreCase) && w.Bounds.Top > geometry.Height * .65 && w.Bounds.Left < geometry.Width * .5).ToArray();
            RectangleF? exitBounds = exits.Length == 1 ? exits[0].Bounds : null;
            if (exitBounds is null) return new(OperationState.Unknown, "Unique World Gate Exit control unavailable.", [context.Directory]);
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "exit-target.json"), new { bounds = exitBounds, geometry });
            var close = await new PetDanceMenuInput(scope.Tag).DispatchAsync(new(PetDanceLifecycleAction.ExitWorldGate, true, "verified-spiral-exit", exitBounds),
                target.Window, geometry, () => scope.Ready() && FrameTimeline.TimestampMs() - frame.CapturedMs < 2500, token, scope.Ready).ConfigureAwait(false);
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "close-receipt.json"), close);
            if (!close.Accepted || !close.InputReleased) return new(OperationState.Failed, "Gate close unconfirmed.", [context.Directory]);
            var closed = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 4, token).ConfigureAwait(false);
            if (closed.Loading != false || closed.States.GetValueOrDefault("menu") != false)
                return new(OperationState.Failed, "World Gate Exit outcome not verified.", [context.Directory]);
            var idle = await RestoreIdle().ConfigureAwait(false);
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "gate-result.json"), new { promptVerified = !closeOnly, menuVerified = menu, worldSelectionDispatched = false, idleVerified = idle.State == OperationState.Completed });
            return new(selectionFailure is null ? idle.State : OperationState.Failed, selectionFailure is not null ? "Destination selection failed: " + selectionFailure + "; " + idle.Message : idle.State == OperationState.Completed ? "World Gate menu verified; closed and native Idle restored. No world-selection input sent." : idle.Message, [context.Directory]);
            }
            try { return await Body().ConfigureAwait(false); }
            finally { if (!idleAttempted) await RestoreIdle().ConfigureAwait(false); }
        });
    }
    // No input may remain held at this checkpoint. Focus return is not evidence
    // that the target is still usable: recheck readiness and recovery afterwards.
    internal static async Task<bool> ReleasedCheckpointAsync(OperationContext context, Func<bool> ready,
        Func<CancellationToken, Task<bool>> waitForFocus, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.Input is not { CanDispatch: true } || !ReferenceEquals(context.Input, GameInputLease.Current)) return false;
        if (!ready() && !await waitForFocus(token).ConfigureAwait(false)) return false;
        token.ThrowIfCancellationRequested();
        return context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current) && ready();
    }

    // Called only after travel inputs have drained. Resume cleanup from a new
    // observation; never repeat X, world selection, teleport or Home dispatch.
    internal static Task<W101IdleFailsafeResult> FinalizeIdleAsync(OperationContext context, Func<bool> ready,
        Func<CancellationToken, Task<bool>> waitForFocus,
        Func<int, CancellationToken, Task<OperationOutcome>> attempt, CancellationToken token) =>
        W101IdleFailsafe.RunAsync(context, new() { ResumeAfterQuiet = true }, ready, attempt,
            (_, cancellation) => waitForFocus(cancellation), token);
}
