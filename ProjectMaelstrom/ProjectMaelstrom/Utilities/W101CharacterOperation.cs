namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded selection-screen qualification, under the existing owner.
/// Never clicks Play, Delete, New or Exit from the selection screen.</summary>
internal static class W101CharacterOperation
{
    internal static readonly ModuleAction CharacterInfo = new("game.character-info", "keyboard-pulse", "C", 40, "w101.character-info");
    internal static readonly ModuleAction GoCommons = new("game.go-commons", "keyboard-pulse", "END", 40, "w101.go-commons");
    internal static readonly ModuleAction GoHome = new("game.go-home", "keyboard-pulse", "HOME", 40, "w101.go-home");
    internal static long CaptureUtcTicks(double capturedMs, double nowMs, long utcTicks)
    {
        if (!double.IsFinite(capturedMs) || !double.IsFinite(nowMs) || capturedMs < 0 ||
            capturedMs > nowMs || nowMs - capturedMs > 3000 || utcTicks <= 0)
            throw new InvalidDataException("Character image timestamp is invalid or stale.");
        var stamp = utcTicks - (long)((nowMs - capturedMs) * TimeSpan.TicksPerMillisecond);
        return stamp > 0 ? stamp : throw new InvalidDataException("Character image timestamp predates the clock.");
    }
    private static readonly IReadOnlyDictionary<string, string> SchoolTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Fire"] = "Pyromancer", ["Ice"] = "Thaumaturge", ["Storm"] = "Diviner",
        ["Life"] = "Theurgist", ["Myth"] = "Conjurer", ["Death"] = "Necromancer", ["Balance"] = "Sorcerer"
    };

    // Level is mutable metadata. Identity requires one name and one unambiguous school.
    private static bool SchoolMatches(string text, string school)
    {
        if (!SchoolTitles.ContainsKey(school)) return false;
        var found = SchoolTitles.SelectMany(pair => new[] { pair.Key, pair.Value }
            .SelectMany(label => System.Text.RegularExpressions.Regex.Matches(text,
                @"\b" + System.Text.RegularExpressions.Regex.Escape(label) + @"\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Select(_ => pair.Key))).ToArray();
        return found.Length == 1 && found[0].Equals(school, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool StatsMatch(PetDanceMenuText text, WizardProfile profile)
    {
        var header = string.Join(" ", text.Words.Where(w => w.Bounds.Top < text.ImageSize.Height * .3f).Select(w => w.Text));
        var name = string.Join(@"\s+", profile.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(System.Text.RegularExpressions.Regex.Escape));
        if (name.Length == 0) return false;
        var names = System.Text.RegularExpressions.Regex.Matches(header, @"\b" + name + @"\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var levels = System.Text.RegularExpressions.Regex.Matches(header, @"\bLEVEL\s+[1-9][0-9]{0,2}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return names.Count == 1 && levels.Count == 1 && SchoolMatches(header, profile.School);
    }

    internal static bool CanFinalizeFailure(bool world, bool cancelled, bool ready, bool writesGame,
        bool delegated, bool canDispatch, bool sameOwner, bool recoveryRequired, bool inputReleased) =>
        world && !cancelled && ready && writesGame && !delegated && canDispatch && sameOwner &&
        !recoveryRequired && inputReleased;

    private static async Task<OperationOutcome> FinalizeFailureAsync(OperationOutcome original,
        OperationContext context, WizwalkerTarget target, WizwalkerLaunch launch, W101ControlScope scope,
        Size geometry, bool world, bool inputReleased, CancellationToken token)
    {
        var eligible = CanFinalizeFailure(world, token.IsCancellationRequested, scope.Ready(),
            context.Request.WritesGame, context.Request.DelegatedInput, context.Input?.CanDispatch == true,
            ReferenceEquals(context.Input, GameInputLease.Current), context.Input?.RecoveryRequired != false, inputReleased);
        OperationOutcome? idleResult = null;
        if (eligible)
        {
            var directory = Path.Combine(context.Directory, "idle-failure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var child = context with { Directory = directory, Request = context.Request with { Id = Guid.NewGuid().ToString("N") } };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                idleResult = await W101IdleCycleOperation.RunOwnedAsync(child, target,
                    W101IdleCycleOperation.CreateRuntime(target, launch, scope, geometry), deadline.Token, "idle").ConfigureAwait(false);
            }
            catch (OperationCanceledException) { idleResult = new(OperationState.Cancelled, "Idle finalization cancelled.", []); }
            catch (Exception error)
            {
                context.Input?.RequireRecovery("Character Idle finalization escaped without a cleanup receipt.");
                idleResult = new(OperationState.CleanupFailed, error.GetType().Name, []);
            }
            if (idleResult.State == OperationState.CleanupFailed)
                context.Input?.RequireRecovery("Character Idle cleanup was not confirmed.");
        }
        var verified = idleResult?.State == OperationState.Completed;
        var report = Path.Combine(context.Directory, "idle-failure-result-" + Guid.NewGuid().ToString("N") + ".json");
        GameExperiments.WriteAtomic(report, new { original, eligible, world, inputReleased, idleResult, idleVerified = verified });
        return original with { State = idleResult?.State == OperationState.CleanupFailed ? OperationState.CleanupFailed : original.State,
            Message = original.Message + (verified ? " Native Idle verified." : " Native Idle not verified."),
            Artifacts = [.. original.Artifacts, report] };
    }

    internal static PreparedDeckJob PrepareHome(WizwalkerSession session, WizwalkerLaunch launch, bool enabled, string profilePath)
    {
        var profile = WizardProfileStore.Load(profilePath);
        var request = W101IdleCycleOperation.Prepare(session, launch, enabled).Request;
        return new(request with { Operation = "w101.character-home", TimeoutSeconds = 110 }, async (context, token) =>
        {
            var target = session.Target;
            await using var scope = await W101ControlScope.CreateAsync(context, target.Window, GameWindows.ClientSize(target.Window), token, true).ConfigureAwait(false);
            var geometry = WindowObserver.GetCaptureSize(target.Window);
            var reader = new PetDanceMenuOcr(); await reader.PrepareAsync(token).ConfigureAwait(false);
            async Task<PetDanceMenuText> Read(string name)
            {
                var frame = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(context.Directory, name + ".png"), frame.Png, token).ConfigureAwait(false);
                using var bytes = new MemoryStream(frame.Png); using var bitmap = new Bitmap(bytes);
                var words = await reader.ReadAsync(bitmap, token).ConfigureAwait(false);
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, name + ".json"), words);
                if (!scope.Ready() || FrameTimeline.TimestampMs() - frame.CapturedMs > 2500) throw new InvalidOperationException("Character evidence expired.");
                return words;
            }
            async Task Pulse(ModuleAction action)
            {
                var decision = new ModuleDecision("character", "home", "verified-panel-transition", Guid.NewGuid().ToString("N"), action.ActionId, action);
                var result = await W101ModuleInput.DispatchAsync(decision, context, scope.Ready, (key, release) => W101ModuleInput.Send(target, key, release), FrameTimeline.TimestampMs, token, scope.Ready).ConfigureAwait(false);
                if (!result.Accepted || !result.InputReleased) throw new InvalidOperationException("Character input rejected.");
                await Task.Delay(400, token).ConfigureAwait(false);
            }
            var worldReading = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 20, token).ConfigureAwait(false);
            var knownWorld = worldReading.CleanupConfirmed && worldReading.HookRestored && worldReading.Loading == false &&
                VisibleControl(Path.Combine(context.Directory, "read-20", "ui-observations.jsonl"), "windowHUD");
            if (!StatsMatch(await Read("identity-1"), profile) || !StatsMatch(await Read("identity-2"), profile))
                return await FinalizeFailureAsync(new(OperationState.Failed, "loaded-character-identity-not-confirmed", [context.Directory]),
                    context, target, launch, scope, geometry, knownWorld, true, token).ConfigureAwait(false);
            await Pulse(CharacterInfo).ConfigureAwait(false);
            var panels = await Read("after-character-close").ConfigureAwait(false);
            if (panels.Text.Contains("CROWN SHOP", StringComparison.OrdinalIgnoreCase))
                await Pulse(W101IdleCycle.CloseMenu).ConfigureAwait(false);
            var outside = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 0, token).ConfigureAwait(false);
            if (outside.Loading != false || outside.States.GetValueOrDefault("menu") != false || outside.States.GetValueOrDefault("dialogue") != false)
                return await FinalizeFailureAsync(new(OperationState.Failed, "home-port-needs-outside-context", [context.Directory]),
                    context, target, launch, scope, geometry, knownWorld && outside.Loading == false, true, token).ConfigureAwait(false);
            await Pulse(GoHome).ConfigureAwait(false);
            bool house = false;
            var homeTransition = new LoadingTransitionGate(FrameTimeline.TimestampMs(), 45000);
            for (int i = 1; i <= 10; i++)
            {
                await Task.Delay(700, token).ConfigureAwait(false);
                W101UiTreeResult state;
                try { state = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, i, token).ConfigureAwait(false); }
                catch (W101UiObservationUnavailableException)
                {
                    if (homeTransition.Observe(FrameTimeline.TimestampMs(), null, false, true) == "timed-out") break;
                    continue;
                }
                var trace = Path.Combine(context.Directory, $"read-{i:00}", "ui-observations.jsonl");
                var transition = homeTransition.Observe(FrameTimeline.TimestampMs(), state.Loading, VisibleControl(trace, "OpenObjectPlacement"), true);
                if (transition == "ready") { house = true; break; }
                if (transition == "timed-out") break;
            }
            var arrival = await Read("home-arrival").ConfigureAwait(false);
            var idleDirectory = Path.Combine(context.Directory, "idle"); Directory.CreateDirectory(idleDirectory);
            var idle = context with { Directory = idleDirectory, Request = context.Request with { Id = Guid.NewGuid().ToString("N") } };
            var outcome = await W101IdleCycleOperation.RunOwnedAsync(idle, target, W101IdleCycleOperation.CreateRuntime(target, launch, scope, geometry), token, "idle").ConfigureAwait(false);
            if (!house || outcome.State != OperationState.Completed) return new(OperationState.Failed, "home-or-idle-not-verified: " + outcome.Message, [context.Directory]);
            var houseProfile = new WizardHouseProfile("equipped-house", new(profile.Name, profile.School, profile.LastKnownLevel), "equipped-house", false, context.Directory);
            var updated = profile with { ActiveHouseId = houseProfile.Id, Houses = [.. (profile.Houses ?? []).Where(h => h.Id != houseProfile.Id), houseProfile] };
            var profiles = Path.Combine(Directory.GetParent(context.Directory)!.Parent!.FullName, "wizard-profiles");
            WizardProfileStore.Save(profiles, updated);
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "home-result.json"), new { identityVerified = true, houseVerified = house, idleVerified = true, profilePath = Path.Combine(profiles, profile.Id + ".json"), layoutQualified = false });
            return new(OperationState.Completed, "Loaded wizard verified; equipped house observed; own empty house profile saved; native Idle verified. Routes remain unqualified.", [context.Directory]);
        });
    }

    internal static bool VisibleControl(string trace, string name)
    {
        int hits = 0;
        foreach (var line in File.ReadLines(trace))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("nodes", out var nodes)) continue;
            if (nodes.EnumerateArray().Any(n => n.GetProperty("name").GetString() == name && n.GetProperty("visible").GetBoolean() && n.GetProperty("enabled").GetBoolean())) hits++;
        }
        return hits >= 2;
    }
    internal static PreparedDeckJob PrepareFrame(WizwalkerSession session, WizwalkerLaunch launch, bool enabled)
    {
        var request = W101IdleCycleOperation.Prepare(session, launch, enabled).Request;
        return new(request with { Operation = "w101.character-frame", TimeoutSeconds = 20 }, async (context, token) =>
        {
            var target = session.Target;
            bool Ready() => target.IsCurrent() && context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current);
            var image = await W101ModuleObserver.CaptureAsync(target, WindowObserver.GetCaptureSize(target.Window), Ready, token).ConfigureAwait(false);
            var path = Path.Combine(context.Directory, "frame.png");
            await File.WriteAllBytesAsync(path, image.Png, token).ConfigureAwait(false);
            return new(OperationState.Completed, "Game window captured; no input or focus changes.", [path]);
        });
    }

    private static async Task<OperationOutcome> EnterAsync(OperationContext parent, WizwalkerSession session, WizwalkerLaunch launch,
        W101ControlScope scope, WizardProfile profile, CancellationToken token)
    {
        var directory = Path.Combine(parent.Directory, "enter"); Directory.CreateDirectory(directory);
        var context = parent with { Directory = directory, Request = parent.Request with { Id = Guid.NewGuid().ToString("N") } };
        var target = session.Target; var geometry = WindowObserver.GetCaptureSize(target.Window);
        var reader = new PetDanceMenuOcr(); await reader.PrepareAsync(token).ConfigureAwait(false);
        var image = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
        using var stream = new MemoryStream(image.Png); using var bitmap = new Bitmap(stream);
        var words = await reader.ReadAsync(bitmap, token).ConfigureAwait(false);
        var play = words.Words.Where(w => w.Text.Trim().Equals("PLAY", StringComparison.OrdinalIgnoreCase) && w.Bounds.Top > geometry.Height * .85f).ToArray();
        if (play.Length == 0 || play.Any(w => RectangleF.Intersect(w.Bounds, play[0].Bounds).Width <= 0) || FrameTimeline.TimestampMs() - image.CapturedMs > 2500)
            return new(OperationState.Failed, "play-control-unverified", [directory]);
        var admission = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 0, token).ConfigureAwait(false);
        var banner = ReadBanner(Path.Combine(directory, "read-00", "ui-observations.jsonl"));
        if (banner is null || !Matches(banner, profile)) return new(OperationState.Failed, "selected-identity-changed", [directory]);
        var receipt = await new PetDanceMenuInput(scope.Tag).DispatchAsync(new(PetDanceLifecycleAction.EnterSelectedWizard, true, "verified-selected-wizard", play[0].Bounds),
            target.Window, geometry, scope.Ready, token, scope.Ready).ConfigureAwait(false);
        if (!receipt.Accepted || !receipt.InputReleased) return new(OperationState.Failed, "play-input-rejected", [directory]);
        return await VerifyLoadedAsync(context, target, launch, scope, banner, token).ConfigureAwait(false);
    }

    private static async Task<OperationOutcome> VerifyLoadedAsync(OperationContext context, WizwalkerTarget target, WizwalkerLaunch launch, W101ControlScope scope, Banner? banner, CancellationToken token)
    {
        var directory = context.Directory; var geometry = WindowObserver.GetCaptureSize(target.Window);
        var statsOcr = new PetDanceMenuOcr();
        await statsOcr.PrepareAsync(token).ConfigureAwait(false);
        bool world = false;
        var worldTransition = new LoadingTransitionGate(FrameTimeline.TimestampMs(), 45000);
        for (int i = 1; i <= 8; i++)
        {
            await Task.Delay(700, token).ConfigureAwait(false);
            W101UiTreeResult reading;
            try { reading = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, i, token).ConfigureAwait(false); }
            catch (W101UiObservationUnavailableException)
            {
                if (worldTransition.Observe(FrameTimeline.TimestampMs(), null, false, true) == "timed-out") break;
                continue;
            }
            var trace = Path.Combine(directory, $"read-{i:00}", "ui-observations.jsonl");
            var transition = worldTransition.Observe(FrameTimeline.TimestampMs(), reading.Loading, VisibleControl(trace, "windowHUD"), true);
            if (transition == "ready") { world = true; break; }
            if (transition == "timed-out") break;
        }
        if (!world) return new(OperationState.Failed, "world-loading-not-confirmed", [directory]);
        var action = CharacterInfo;
        var proposal = new ModuleDecision("character", "info", "verify-loaded-wizard", Guid.NewGuid().ToString("N"), action.ActionId, action);
        var info = await W101ModuleInput.DispatchAsync(proposal, context, scope.Ready, (key, release) => W101ModuleInput.Send(target, key, release), FrameTimeline.TimestampMs, token, scope.Ready).ConfigureAwait(false);
        if (!info.Accepted || !info.InputReleased)
            return await FinalizeFailureAsync(new(OperationState.Failed, "character-info-input-rejected", [directory]),
                context, target, launch, scope, geometry, world, info.InputReleased, token).ConfigureAwait(false);
        await Task.Delay(400, token).ConfigureAwait(false);
        try { _ = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, 10, token, false, includeResourceContext: true).ConfigureAwait(false); }
        catch (W101UiObservationUnavailableException)
        {
            // A clean unavailable panel tree does not prevent visual evidence
            // or closing the panel we opened. Uncertain hook cleanup still propagates.
            GameExperiments.WriteAtomic(Path.Combine(directory, "character-tree-unavailable.json"), new { visualFallback = true, cleanupConfirmed = true });
        }
        var stats = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
        var capturedUtcTicks = CaptureUtcTicks(stats.CapturedMs, FrameTimeline.TimestampMs(), DateTime.UtcNow.Ticks);
        await File.WriteAllBytesAsync(Path.Combine(directory, "loaded-character.png"), stats.Png, token).ConfigureAwait(false);
        try
        {
            using var stream = new MemoryStream(stats.Png);
            using var bitmap = new Bitmap(stream);
            var words = await statsOcr.ReadAsync(bitmap, token).ConfigureAwait(false);
            GameExperiments.WriteAtomic(Path.Combine(directory, "character-ocr.json"), new
            {
                schemaVersion = 1, target, observedUtcTicks = capturedUtcTicks, completedUtcTicks = DateTime.UtcNow.Ticks,
                source = "local-windows-ocr", imageSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stats.Png)).ToLowerInvariant(),
                words, candidateOnly = true
            });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            GameExperiments.WriteAtomic(Path.Combine(directory, "character-ocr-unavailable.json"), new { error = error.GetType().Name, candidateOnly = true });
        }
        var close = await W101ModuleInput.DispatchAsync(proposal with { ProposalId = Guid.NewGuid().ToString("N") }, context, scope.Ready,
            (key, release) => W101ModuleInput.Send(target, key, release), FrameTimeline.TimestampMs, token, scope.Ready).ConfigureAwait(false);
        if (!close.Accepted || !close.InputReleased)
            return await FinalizeFailureAsync(new(OperationState.Failed, "character-info-close-rejected", [directory]),
                context, target, launch, scope, geometry, world, close.InputReleased, token).ConfigureAwait(false);
        var idleDirectory = Path.Combine(directory, "idle"); Directory.CreateDirectory(idleDirectory);
        var idle = context with { Directory = idleDirectory, Request = context.Request with { Id = Guid.NewGuid().ToString("N") } };
        var outcome = await W101IdleCycleOperation.RunOwnedAsync(idle, target, W101IdleCycleOperation.CreateRuntime(target, launch, scope, geometry), token, "idle").ConfigureAwait(false);
        GameExperiments.WriteAtomic(Path.Combine(directory, "entry-result.json"), new { selected = banner, enteredWorld = true, profileActivated = false, idleVerified = outcome.State == OperationState.Completed });
        return new(outcome.State == OperationState.Completed ? OperationState.Unknown : outcome.State,
            "Entered selected wizard; character evidence saved; profile activation awaits post-load identity qualification. " + outcome.Message, [directory]);
    }

    internal static PreparedDeckJob PrepareVerify(WizwalkerSession session, WizwalkerLaunch launch, bool enabled)
    {
        var request = W101IdleCycleOperation.Prepare(session, launch, enabled).Request;
        return new(request with { Operation = "w101.character-verify", TimeoutSeconds = 100 }, async (context, token) =>
        {
            await using var scope = await W101ControlScope.CreateAsync(context, session.Target.Window, GameWindows.ClientSize(session.Target.Window), token, true).ConfigureAwait(false);
            return await VerifyLoadedAsync(context, session.Target, launch, scope, null, token).ConfigureAwait(false);
        });
    }

    internal sealed record Banner(string Name, string Level, string Location);
    internal static Banner? ReadBanner(string trace)
    {
        var banners = new List<Banner>();
        foreach (var line in File.ReadLines(trace))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "tree" ||
                !root.TryGetProperty("complete", out var complete) || !complete.GetBoolean()) continue;
            var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
            if (nodes.Count(n => n.GetProperty("name").GetString() == "btnPlay" &&
                n.GetProperty("visible").GetBoolean() && n.GetProperty("enabled").GetBoolean()) != 1) continue;
            string? Text(string name, string parent)
            {
                var matches = nodes.Where(n => n.GetProperty("name").GetString() == name &&
                    n.GetProperty("visible").GetBoolean() && n.GetProperty("parent").ValueKind == System.Text.Json.JsonValueKind.Number &&
                    nodes.Any(p => p.GetProperty("index").GetInt32() == n.GetProperty("parent").GetInt32() && p.GetProperty("name").GetString() == parent)).ToArray();
                if (matches.Length != 1 || matches[0].GetProperty("text").ValueKind != System.Text.Json.JsonValueKind.String) return null;
                return System.Text.RegularExpressions.Regex.Replace(matches[0].GetProperty("text").GetString()!, "<[^>]+>", "").Trim();
            }
            var name = Text("txtName", "sprBanner"); var level = Text("txtLevel", "sprSubBanner"); var location = Text("txtLocation", "sprSubBanner");
            if (name is not null && level is not null && location is not null) banners.Add(new(name, level, location));
        }
        return banners.Count >= 2 && banners[^1] == banners[^2] ? banners[^1] : null;
    }

    internal static bool Matches(Banner banner, WizardProfile profile) =>
        banner.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase) && SchoolMatches(banner.Level, profile.School) &&
        System.Text.RegularExpressions.Regex.IsMatch(banner.Level, @"^Level\s+[1-9][0-9]{0,2}\s+\([^)]*\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    internal static WizardProfile RefreshObservedLevel(Banner banner, WizardProfile profile)
    {
        if (!Matches(banner, profile)) throw new InvalidDataException("Wizard identity does not match this profile.");
        var level = System.Text.RegularExpressions.Regex.Match(banner.Level, @"^Level\s+([1-9][0-9]{0,2})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return profile with { LastKnownLevel = int.Parse(level.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) };
    }

    internal static PreparedDeckJob PrepareRoster(WizwalkerSession session, WizwalkerLaunch launch, bool enabled, string profilePath, bool enter = false)
    {
        var profile = WizardProfileStore.Load(profilePath);
        var admitted = W101IdleCycleOperation.Prepare(session, launch, enabled).Request;
        return new(admitted with { Operation = enter ? "w101.character-enter" : "w101.character-roster", Version = "w101-character-roster-v1", TimeoutSeconds = 150 }, async (context, token) =>
        {
            var target = session.Target;
            await using var scope = await W101ControlScope.CreateAsync(context, target.Window, GameWindows.ClientSize(target.Window), token, true).ConfigureAwait(false);
            var geometry = WindowObserver.GetCaptureSize(target.Window);
            // Qualification-only layout from selection-2.png in ef7b2979...;
            // these observed occupied seats are NOT a resolution-independent roster detector.
            if (geometry != new Size(1282, 759)) return new(OperationState.Failed, "selection-layout-not-qualified", []);
            Point[] seats = [new(1040, 400), new(920, 300), new(435, 240), new(355, 300), new(270, 550)];
            var input = new PetDanceMenuInput(scope.Tag);
            for (int i = 0; i <= seats.Length; i++)
            {
                var reading = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, i, token).ConfigureAwait(false);
                var banner = ReadBanner(Path.Combine(context.Directory, $"read-{i:00}", "ui-observations.jsonl"));
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, $"banner-{i}.json"), new { reading.CleanupConfirmed, banner });
                if (banner is null || !scope.Ready()) return new(OperationState.Failed, "selection-banner-unavailable", [context.Directory]);
                if (Matches(banner, profile))
                {
                    profile = RefreshObservedLevel(banner, profile);
                    profile = WizardProfileStore.RefreshLevel(Path.Combine(Directory.GetParent(context.Directory)!.Parent!.FullName, "wizard-profiles"), profile);
                    if (enter) return await EnterAsync(context, session, launch, scope, profile, token).ConfigureAwait(false);
                    return new(OperationState.Completed, "Target wizard selected and banner verified; profile level refreshed; Play not pressed.", [context.Directory]);
                }
                if (i == seats.Length) break;
                var point = seats[i];
                var receipt = await input.DispatchAsync(new(PetDanceLifecycleAction.SelectWizardCandidate, true, "reviewed-selection-seat", new(point.X - 5, point.Y - 5, 10, 10)), target.Window, geometry,
                    () => scope.Ready() && DateTime.UtcNow.Ticks - reading.ObservedUtcTicks < TimeSpan.FromSeconds(2).Ticks, token, scope.Ready).ConfigureAwait(false);
                if (!receipt.Accepted || !receipt.InputReleased) return new(OperationState.Failed, "selection-input-rejected", [context.Directory]);
                await Task.Delay(600, token).ConfigureAwait(false);
            }
            return new(OperationState.Failed, "target-wizard-not-found", [context.Directory]);
        });
    }

    internal static RectangleF? QuitControl(PetDanceMenuText text)
    {
        string Normalize(string value) => new(value.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
        if (!text.Words.Any(w => Normalize(w.Text) == "IDLE") ||
            !text.Words.Any(w => Normalize(w.Text) == "GRAPHICS")) return null;
        var candidates = text.Words.Where(w => Normalize(w.Text) == "QUIT" &&
            w.Bounds.Top > text.ImageSize.Height * .5f && w.Bounds.Bottom < text.ImageSize.Height * .95f)
            .Select(w => w.Bounds).ToArray();
        if (candidates.Length == 0) return null;
        var first = candidates[0];
        return candidates.All(b => RectangleF.Intersect(first, b).Width > 0) ? first : null;
    }

    internal static PreparedDeckJob Prepare(WizwalkerSession session, WizwalkerLaunch launch, bool enabled, string profilePath)
    {
        var profile = WizardProfileStore.Load(profilePath);
        var admitted = W101IdleCycleOperation.Prepare(session, launch, enabled).Request;
        return new(admitted with { Operation = "w101.character-inspect", Version = "w101-character-inspect-v1", TimeoutSeconds = 100 },
            async (context, token) =>
            {
                var target = session.Target;
                await using var scope = await W101ControlScope.CreateAsync(context, target.Window,
                    GameWindows.ClientSize(target.Window), token, true).ConfigureAwait(false);
                var geometry = WindowObserver.GetCaptureSize(target.Window);
                var runtime = W101IdleCycleOperation.CreateRuntime(target, launch, scope, geometry);
                var directory = Path.Combine(context.Directory, "wake"); Directory.CreateDirectory(directory);
                var wake = context with { Directory = directory, Request = context.Request with { Id = Guid.NewGuid().ToString("N") } };
                var awake = await W101IdleCycleOperation.RunOwnedAsync(wake, target, runtime, token, "wake").ConfigureAwait(false);
                if (awake.State != OperationState.Completed) return awake;
                var input = new PetDanceMenuInput(scope.Tag);
                var opened = await input.DispatchAsync(new(PetDanceLifecycleAction.OpenMenu, true, "awake-options", null),
                    target.Window, geometry, scope.Ready, token, scope.Ready).ConfigureAwait(false);
                if (!opened.Accepted || !opened.InputReleased) return new(OperationState.Failed, "menu-input-rejected", []);
                var reader = new PetDanceMenuOcr();
                await reader.PrepareAsync(token).ConfigureAwait(false);
                await Task.Delay(250, token).ConfigureAwait(false);
                var image = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(context.Directory, "options.png"), image.Png, token).ConfigureAwait(false);
                using var stream = new MemoryStream(image.Png); using var bitmap = new Bitmap(stream);
                var text = await reader.ReadAsync(bitmap, token).ConfigureAwait(false);
                GameExperiments.WriteAtomic(Path.Combine(context.Directory, "options-text.json"), text);
                var quit = QuitControl(text);
                if (quit is null || !scope.Ready() || FrameTimeline.TimestampMs() - image.CapturedMs > 2500)
                    return new(OperationState.Failed, "quit-control-not-qualified", [context.Directory]);
                var clicked = await input.DispatchAsync(new(PetDanceLifecycleAction.QuitToCharacterSelection, true, "verified-options-quit", quit),
                    target.Window, geometry, scope.Ready, token, scope.Ready).ConfigureAwait(false);
                if (!clicked.Accepted || !clicked.InputReleased) return new(OperationState.Failed, "quit-input-rejected", [context.Directory]);
                // Selection has no native Idle. Do not send Escape/Idle cleanup into it.
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                    var reading = await W101UiTreeOperation.ReadOwnedAsync(context, target, launch, i, token).ConfigureAwait(false);
                    var snapshot = await W101ModuleObserver.CaptureAsync(target, geometry, scope.Ready, token).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(Path.Combine(context.Directory, $"selection-{i}.png"), snapshot.Png, token).ConfigureAwait(false);
                    using var bytes = new MemoryStream(snapshot.Png); using var screen = new Bitmap(bytes);
                    var words = await reader.ReadAsync(screen, token).ConfigureAwait(false);
                    GameExperiments.WriteAtomic(Path.Combine(context.Directory, $"selection-{i}.json"), new { reading, words, profile.Name, profile.School });
                }
                return new(OperationState.Completed, "Selection evidence captured; no character selected, no profile activated, Idle unavailable in selection.", [context.Directory]);
            });
    }
}
