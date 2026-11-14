using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Code-owned adapter and readiness evaluator; saved cards cannot supply either.</summary>
internal sealed record W101ModuleCardAdapter(
    Func<string, JobCardSpec, JobFacts?, PreparedDeckJob> Prepare,
    Func<string, JobCardSpec, JobFacts?, string?> Readiness);

/// <summary>Trusted adapters only. A catalog entry is not live qualification.
/// The collection includes future cards so users can prepare configurations,
/// but those cards cannot enter an input path until an adapter is registered.</summary>
internal static class JobDeckRoutines
{
    internal static JobRoutine[] Create(Func<JobCardSpec, JobFacts?, PreparedDeckJob>? quest = null,
        W101ModuleCardAdapter? w101Modules = null, Func<JobCardSpec, JobFacts?, PreparedDeckJob>? travel = null,
        Func<JobCardSpec, JobFacts?, PreparedDeckJob>? dance = null)
    {
        var routines = new List<JobRoutine>
        {
            new(new("review.routine", "Replay", "Offline Review", "replay", "*", "Offline replay · no game inputs", true, false,
                [new("graphFile", "Routine file", "file", true), new("sessionFile", "Recorded session", "file", true)],
                "Test a routine against recorded observations and receipts. Success means the saved replay completed, not that a live game was played."),
                _ => { }, PrepareReplay),
            new(new("w101.questing", "Quest", "Questing", "quest", "wizard101", "Selected conversation only · navigation and combat pending", quest != null, true,
                [new("mode", "Quest source", "choice:active-objective,selected-conversation", false, "active-objective"),
                    new("selectionFile", "Selected-conversation file (optional)", "file"), new("maxPages", "Conversation page limit", "text", false, "12")],
                "Auto-fill from the active objective, apply your filters, and choose a qualified action. Open conversations can run; navigation, combat and collecting remain pending."),
                card =>
                {
                    if (!int.TryParse(card.Settings?.GetValueOrDefault("maxPages", "12") ?? "12", out var pages) || pages is < 1 or > 16)
                        throw new InvalidDataException("Conversation page limit must be 1–16.");
                    if (QuestMode(card) == "selected-conversation" && string.IsNullOrWhiteSpace(card.Settings?.GetValueOrDefault("selectionFile")))
                        throw new InvalidDataException("Choose a conversation file or use the active objective.");
                }, quest ?? ((_, _) => throw new InvalidOperationException("No quest adapter is connected.")), QuestBlock)
        };
        foreach (var (kind, title, icon) in new[] { ("navigation", "Navigation", "replay"), ("sigil", "Interact / Sigil", "quest"),
            ("menu", "Menu", "cards"), ("auto-idle", "Auto-Idle", "idle") })
        {
            var moduleKind = kind;
            routines.Add(new(new("module." + kind, title + " · Offline", "Shared Modules", icon, "*",
                "Saved module rehearsal only · no game inputs", true, false,
                [new("mode", "Evidence mode", "choice:offline-replay,screen-review", false, "offline-replay"),
                    new("bundleFile", "Module bundle (labelled replay)", "file"), new("sessionFile", "Saved session manifest", "file", true),
                    new("timeoutSeconds", "Review timeout (1–300 seconds)", "text", false, "60")],
                "Rehearse the existing " + kind + " module through the shared scheduler. Completion verifies recorded evidence only; it does not enable live movement or menus."),
                ValidateModuleCard, (card, _) => PrepareModuleCard(card, moduleKind)));
        }
        void Planned(string id, string title, string category, string icon, string description, params JobOption[] options) =>
            routines.Add(new(new(id, title, category, icon, "wizard101", "Not connected to the deck · preparation only", false, true, options, description),
                _ => { }, (_, _) => throw new InvalidOperationException("This card has no qualified deck adapter.")));
        void ValidateDance(JobCardSpec card)
        {
            if (!int.TryParse(card.Settings?.GetValueOrDefault("level", "1") ?? "1", out var level) || level is < 1 or > 5)
                throw new InvalidDataException("Choose Dance level 1–5.");
            if ((card.Settings?.GetValueOrDefault("snacks", "none") ?? "none") != "none")
                throw new InvalidDataException("This visit supports no-feed exit only.");
        }
        routines.Add(new(new("w101.pet.dance", "Pet Dance", "Pet Games", "dance", "wizard101",
            dance is null ? "Preparation only · live visit runtime not connected" : "Test candidate · exact visit evidence required",
            dance is not null, true, [new("level", "Level", "text", false, "1"),
                new("snacks", "Snack policy", "choice:none", false, "none")],
            "Play one verified visit through the shared owner, specialist and no-feed Idle exit."),
            ValidateDance, (card, facts) =>
            {
                ValidateDance(card);
                return dance?.Invoke(card, facts) ?? throw new InvalidOperationException("No trusted Dance visit runtime is connected.");
            }));
        Planned("w101.pet.maze", "Pet Maze", "Pet Games", "paw", "Maze training.", new JobOption("snacks", "Snack policy", "choice:none,suggest", false, "none"));
        Planned("w101.pet.cannon", "Pet Cannon", "Pet Games", "fire", "Cannon training.", new JobOption("snacks", "Snack policy", "choice:none,suggest", false, "none"));
        Planned("w101.pet.dropping", "Gobbler Drop", "Pet Games", "paw", "Catch the falling food.", new JobOption("snacks", "Snack policy", "choice:none,suggest", false, "none"));
        foreach (var (id, title, icon) in new[] { ("skull-riders", "Skull Riders", "skull"), ("sorcery-stones", "Sorcery Stones", "stones"),
            ("doodle-doug", "Doodle Doug", "shovel"), ("conjuration", "Conjuration", "cards"), ("hot-shots", "Hot Shots", "fire"),
            ("choo-choo-zoo", "Choo-Choo Zoo", "train"), ("potion-motion", "Potion Motion", "potion"), ("dueling-diego", "Dueling Diego", "sword") })
            Planned("w101.minigame." + id, title, "Minigames", icon, "A separate minigame card. Recognition and full-visit qualification are required before deck execution.");
        void BoundModule(string id, string title, string kind, string icon, string description, params JobOption[] options)
        {
            string? Ready(JobCardSpec card, JobFacts? facts) =>
                kind == "native-idle" && (card.Settings?.GetValueOrDefault("method") ?? "native-idle") != "native-idle"
                    ? "Only native Idle entry is bound here; movement methods remain preparation-only."
                    : w101Modules?.Readiness(kind, card, facts) ?? (w101Modules is null ? "No qualified W101 module runtime is connected." : null);
            routines.Add(new(new(id, title, "W101 Operations", icon, "wizard101",
                w101Modules is null ? "W101 binding prepared · live runtime not connected" : "Trusted W101 module adapter · current readiness checks required",
                w101Modules is not null, true, options, description),
                card => ValidateW101ModuleCard(card, kind), (card, facts) =>
                {
                    ValidateW101ModuleCard(card, kind);
                    if (Ready(card, facts) is { } reason) throw new InvalidOperationException(reason);
                    return w101Modules!.Prepare(kind, card, facts);
                }, Ready));
        }
        BoundModule("w101.interact", "Interact", "sigil", "quest", "Interact with the selected verified prompt using X; verify the expected response. No walking or automatic dialogue advancement.",
            new("prompt", "Expected prompt text", "text", true), new("timeoutSeconds", "Time limit (seconds)", "text", false, "30"));
        BoundModule("w101.menu", "Game Menu", "menu", "cards", "Open the recognized game options menu from verified outside context. Arbitrary menu clicks, shops and dialogue are excluded.",
            new("action", "Menu action", "choice:open-menu", false, "open-menu"), new("timeoutSeconds", "Time limit (seconds)", "text", false, "30"));
        BoundModule("w101.auto-idle", "Auto-Idle", "native-idle", "idle", "Enter verified native Idle through the game menu. Wake/resume and automatic between-run scheduling are separate operations.",
            new("method", "Method", "choice:native-idle,spin,step,routine", false, "native-idle"), new("timeoutSeconds", "Time limit (seconds)", "text", false, "30"));
        routines.Add(new(new(W101TravelCard.Id, "Travel", "W101 Operations", "replay", "wizard101",
            "Bounded World Gate selection � live qualification required", travel is not null, true,
            [new("destination", "Destination world", "text", true, "Zafaria"),
                new("returnHome", "Return Home afterward", "choice:true,false", false, "true"),
                new("maximumHomeWaitSeconds", "Home cooldown wait limit (seconds)", "text", false, "60"),
                new("maximumPages", "World menu page limit", "text", false, "8"),
                new("captureHomeCooldown", "Observe Home cooldown after return", "choice:true,false", false, "false")],
            "Start at a World Gate. Verify the selected world, travel, optionally wait for Home availability, return and enter Idle. Home return currently requires starting at your house gate."),
            card => W101TravelCard.Options(card), travel ?? ((_, _) => throw new InvalidOperationException("No travel adapter connected."))));
        Planned("w101.character-switch", "Switch Wizard", "W101 Operations", "cards",
            "Return to character selection, verify the configured wizard after loading, associate its house profile, and verify native Idle. Live identity and selection bindings are pending; this card cannot yet dispatch.",
            new("wizardName", "Exact wizard name", "text", true),
            new("school", "School", "text", true), new("level", "Level", "text", true),
            new("houseProfile", "Wizard house profile", "text", true));
        Planned("w101.potions", "Potions", "Maintenance", "potion", "Use a potion only with verified resources and outcome.");
        Planned("w101.inventory", "Inventory", "Maintenance", "cards", "Protected-item selling and restocking; not enabled by this card.");
        Planned("w101.farming", "Farming", "Farming", "shovel", "Resource-aware collection and farming. Configure allowed areas and targets.");
        Planned("w101.battle", "Battle", "Combat", "sword", "A future combat specialist using verified turns and outcomes.");
        return routines.ToArray();
    }
    internal static JobCardSpec NewCard(JobRoutineInfo info) => new(Guid.NewGuid().ToString("N"), info.Id, info.Title,
        Settings: info.Options.ToDictionary(o => o.Name, o => o.Default ?? ""));
    internal static JobDeckDocument Initial(IEnumerable<JobRoutine> routines) => new("My Routine Deck", "wizard101",
        routines.Select(r => NewCard(r.Info)).ToArray());

    private static void ValidateW101ModuleCard(JobCardSpec card, string kind)
    {
        if (!int.TryParse(card.Settings?.GetValueOrDefault("timeoutSeconds") ?? "30", out var seconds) || seconds is < 1 or > 60)
            throw new InvalidDataException("W101 module cards require a 1–60 second limit.");
        if (kind == "sigil" && (string.IsNullOrWhiteSpace(card.Settings?.GetValueOrDefault("prompt")) || card.Settings!["prompt"].Length > 160))
            throw new InvalidDataException("Choose the exact expected interaction prompt (up to 160 characters).");
        if (kind == "menu" && (card.Settings?.GetValueOrDefault("action") ?? "open-menu") != "open-menu")
            throw new InvalidDataException("Only the verified game-menu opening route is supported.");
    }

    private static void ValidateModuleCard(JobCardSpec card)
    {
        var settings = card.Settings;
        var mode = settings?.GetValueOrDefault("mode") ?? "offline-replay";
        if (mode is not ("offline-replay" or "screen-review") ||
            !int.TryParse(settings?.GetValueOrDefault("timeoutSeconds") ?? "60", out var timeout) || timeout is < 1 or > 300)
            throw new InvalidDataException("Choose a supported offline evidence mode and a 1–300 second review limit.");
        if (string.IsNullOrWhiteSpace(settings?.GetValueOrDefault("sessionFile")) ||
            mode == "offline-replay" && string.IsNullOrWhiteSpace(settings?.GetValueOrDefault("bundleFile")))
            throw new InvalidDataException("Choose a saved session, plus its module bundle for labelled replay.");
        if (mode == "screen-review" && !string.IsNullOrWhiteSpace(settings?.GetValueOrDefault("bundleFile")))
            throw new InvalidDataException("Screen review uses the bundle pinned by its manifest; clear the separate bundle field.");
    }

    private static PreparedDeckJob PrepareModuleCard(JobCardSpec card, string kind)
    {
        ValidateModuleCard(card);
        var settings = card.Settings!;
        var timeout = int.Parse(settings.GetValueOrDefault("timeoutSeconds") ?? "60");
        return (settings.GetValueOrDefault("mode") ?? "offline-replay") == "screen-review"
            ? ModuleJobAdapter.PrepareOffline(settings["sessionFile"], kind, timeout)
            : ModuleJobAdapter.PrepareReplay(settings["bundleFile"], settings["sessionFile"], kind, timeout);
    }

    internal static string? QuestBlock(JobCardSpec card, JobFacts? facts)
    {
        if (QuestMode(card) == "selected-conversation") return null;
        if (facts?.Values.GetValueOrDefault("dialogue").ValueKind == JsonValueKind.True) return null;
        var objective = facts?.Values.GetValueOrDefault("quest.objective");
        var intent = W101QuestIntent.Parse(objective is { ValueKind: JsonValueKind.String } text ? text.GetString() : null);
        return intent.IsRecognized ? $"Quest auto-filled: {intent.Target} in {intent.Area}. Required {string.Join(", ", intent.ExecutionNeeds)} adapter is not yet qualified for this deck." :
            "Waiting for a recognized active objective or an open selected conversation.";
    }

    internal static string QuestMode(JobCardSpec card) => card.Settings?.GetValueOrDefault("mode") ??
        (!string.IsNullOrWhiteSpace(card.Settings?.GetValueOrDefault("selectionFile")) ? "selected-conversation" : "active-objective");

    private static PreparedDeckJob PrepareReplay(JobCardSpec card, JobFacts? facts)
    {
        var graphPath = Path.GetFullPath(card.Settings!["graphFile"]);
        var sessionPath = Path.GetFullPath(card.Settings["sessionFile"]);
        var graph = GameExperiments.BoundedBytes(graphPath, 65536);
        var manifest = GameExperiments.BoundedBytes(sessionPath, 65536);
        using var doc = JsonDocument.Parse(manifest);
        var eventsName = doc.RootElement.GetProperty("events_file").GetString();
        if (string.IsNullOrWhiteSpace(eventsName) || eventsName is "." or ".." || eventsName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            eventsName is "graph.json" or "session.json" or "replay.json") throw new InvalidDataException("Invalid saved events filename.");
        var eventsPath = Path.Combine(Path.GetDirectoryName(sessionPath)!, eventsName);
        var events = GameExperiments.BoundedBytes(eventsPath, 16 * 1024 * 1024);
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), "deck.review.routine", "1", 60, false, "saved-session",
            new Dictionary<string, string> { [graphPath] = RoutineReplay.Digest(graph), [sessionPath] = RoutineReplay.Digest(manifest), [eventsPath] = RoutineReplay.Digest(events) });
        return new(request, (context, token) =>
        {
            token.ThrowIfCancellationRequested();
            var folder = Path.Combine(context.Directory, "replay-inputs"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "graph.json"), graph);
            File.WriteAllBytes(Path.Combine(folder, "session.json"), manifest);
            File.WriteAllBytes(Path.Combine(folder, eventsName), events);
            var report = Path.Combine(context.Directory, "replay.json");
            var complete = RoutineReplay.Run(Path.Combine(folder, "graph.json"), Path.Combine(folder, "session.json"), report);
            return Task.FromResult(new OperationOutcome(complete ? OperationState.Completed : OperationState.Failed,
                complete ? "Saved routine replay completed. No live inputs were sent." : "Saved replay did not complete; inspect evidence.", [report]));
        });
    }
}
