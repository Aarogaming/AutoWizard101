namespace ProjectMaelstrom.Utilities;

internal sealed record CardScanPlan(string[] Fields, string[] Unavailable, bool Ui, bool Quest, string[]? Groups = null, bool Resources = false);

/// <summary>Pure union of active consumers. Execution stays with the existing serialized observer.</summary>
internal static class CardScanDemand
{
    internal static CardScanPlan Build(JobDeckState state, bool learning = false, JobFacts? context = null, DateTime? now = null, bool resourcesEnabled = false)
    {
        var requested = new HashSet<string>(StringComparer.Ordinal);
        var groups = new HashSet<string>(StringComparer.Ordinal);
        if (learning)
        {
            groups.Add("learning");
            foreach (var field in JobFactCatalog.All.Where(f => f.ProducerAvailable)) requested.Add(field.Id);
        }
        foreach (var card in state.Deck.Cards.Where(c => c.InDeck))
        {
            var progress = state.Progress.GetValueOrDefault(card.Id);
            if (progress?.Completed >= card.Runs || progress?.Attempts >= card.AttemptLimit) continue;
            foreach (var id in (card.DataRequests ?? []).Concat((card.Conditions ?? []).Select(c => c.Fact)).Concat((card.Filters ?? []).Select(f => f.Fact)))
                requested.Add(JobFactCatalog.Find(id)?.Id ?? id);
            if (card.RoutineId == "w101.questing")
            {
                groups.Add("quest");
                foreach (var id in new[] { "dialogue", "quest.objective", "quest.action", "quest.area", "quest.target" }) requested.Add(id);
            }
        }
        // Prediction changes demand only. No facts or action authority are synthesized.
        var clock = now ?? DateTime.UtcNow;
        if (requested.Count > 0 && context is not null && context.GameId == state.Deck.GameId &&
            !string.IsNullOrWhiteSpace(context.TargetId) && context.ObservedUtc <= clock &&
            clock - context.ObservedUtc <= TimeSpan.FromSeconds(2) && context.Values is not null)
        {
            bool On(string id) => context.Values.TryGetValue(id, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.True;
            if (On("interactionPrompt"))
            {
                groups.Add("interaction-preview");
                requested.UnionWith(["dialogue", "menu"]);
            }
            if (On("dialogue"))
            {
                groups.Add("dialogue");
                requested.UnionWith(["quest.objective", "quest.action"]);
            }
            // Only an active quest consumer may use quest intent to prefetch resources.
            if (groups.Contains("quest") && context.Values.TryGetValue("quest.action", out var action) &&
                action.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                if (action.GetString() is "Defeat" or "DefeatAndCollect")
                {
                    groups.Add("combat-preview");
                    requested.UnionWith(["health", "health.max", "mana", "mana.max", "potions"]);
                }
                else if (action.GetString() is "Destination" or "Explore" or "Talk" or "Collect")
                {
                    groups.Add("navigation-preview");
                    requested.UnionWith(["interactionPrompt", "menu", "nativeIdle"]);
                }
            }
        }
        var available = requested.Where(id => JobFactCatalog.Find(id) is { ProducerAvailable: true } field &&
            (field.Producer is not ("w101.resources" or "w101.wizard-verification") || resourcesEnabled)).Order().ToArray();
        return new(available, requested.Except(available).Order().ToArray(), available.Length > 0,
            available.Any(id => id.StartsWith("quest.", StringComparison.Ordinal)), groups.Order().ToArray(),
            available.Any(id => JobFactCatalog.Find(id)?.Producer is "w101.resources" or "w101.wizard-verification"));
    }
}
