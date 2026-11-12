using System.Text.Json;

namespace ProjectMaelstrom.Models;

internal sealed class ActivityProfile
{
    public int Version { get; set; } = 1;
    public string Activity { get; set; } = "questing";
    public string Label { get; set; } = "";
    public int MaxMinutes { get; set; } = 15;
    public string CombatStyle { get; set; } = "damage";
    public string CustomCombat { get; set; } = "";
    public int MinHealthPercent { get; set; } = 20;
    public int MinManaPercent { get; set; } = 10;
    public bool UsePotions { get; set; } = true;
    public bool BuyPotions { get; set; }
    public bool TeamUp { get; set; }
    public int Games { get; set; } = 1;
    public string Resolution { get; set; } = "1280x800";
    public bool[] Locations { get; set; } = new[] { true, false, false, false, false };
    public bool[] Snacks { get; set; } = new bool[5];
    public string Tool => Activity == "pet_dance" ? "dance" : "deimos";
    public string Title => Activity switch { "questing" => "Follow my tracked quest", "farming" => "Farm this dungeon entrance", "pet_dance" => "Play pet dance", _ => "Activity unavailable" };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void Validate()
    {
        if (Version != 1) throw new InvalidOperationException("This setup version is not supported.");
        if (Activity is not ("questing" or "farming" or "pet_dance")) throw new InvalidOperationException("This activity has no working adapter yet.");
        if (MaxMinutes is < 1 or > 180) throw new InvalidOperationException("Choose a time limit between 1 and 180 minutes.");
        if (Locations == null || Locations.Length != 5 || Snacks == null || Snacks.Length != 5) throw new InvalidOperationException("This setup has invalid pet-game options.");
        if (Games is < 1 or > 100 || Resolution is not ("800x600" or "1280x800")) throw new InvalidOperationException("This setup has invalid pet-game settings.");
        if (Activity == "pet_dance")
        {
            if (Games is < 1 or > 100) throw new InvalidOperationException("Choose between 1 and 100 games.");
            if (Resolution is not ("800x600" or "1280x800")) throw new InvalidOperationException("Choose 800x600 or 1280x800 for pet dance.");
            if (Locations == null || Locations.Length != 5 || !Locations.Any(value => value)) throw new InvalidOperationException("Choose at least one dance location.");
            if (Snacks == null || Snacks.Length != 5) throw new InvalidOperationException("Choose valid snack positions.");
            if (Resolution == "800x600" && Snacks.Any(value => value)) throw new InvalidOperationException("Snack feeding is available only at 1280x800.");
        }
        else
        {
            if (MinHealthPercent is < 0 or > 100 || MinManaPercent is < 0 or > 100) throw new InvalidOperationException("Resource thresholds must be between 0 and 100 percent.");
            if (CombatStyle is not ("damage" or "aoe" or "custom")) throw new InvalidOperationException("Choose a combat preference.");
            if (CombatStyle == "custom" && (string.IsNullOrWhiteSpace(CustomCombat) || CustomCombat.Length > 16000)) throw new InvalidOperationException("Enter a custom combat strategy, up to 16,000 characters.");
        }
    }

    public string Summary()
    {
        var lines = new List<string> { Title, $"Time limit: {MaxMinutes} minutes" };
        if (!string.IsNullOrWhiteSpace(Label)) lines.Add($"Run label: {Label}");
        if (Activity == "pet_dance")
        {
            var names = new[] { "Wizard City", "Krokotopia", "Marleybone", "Mooshu", "Dragonspyre" };
            lines.Add($"Games: {Games} · resolution: {Resolution} · UI size: HUGE");
            lines.Add("Locations: " + string.Join(", ", names.Where((_, index) => Locations[index])));
            lines.Add("Snack positions: " + (Snacks.Any(value => value) ? string.Join(", ", Enumerable.Range(1, 5).Where(index => Snacks[index - 1])) : "None"));
            lines.Add("Stand near the dance spiral with its menu closed. The helper brings the game forward and moves it to the top-left.");
        }
        else
        {
            lines.Add("Combat: " + (CombatStyle == "aoe" ? "Area damage first, then other damage" : CombatStyle == "custom" ? "Your custom strategy" : "Damage cards"));
            lines.Add($"Stop outside battle below health {MinHealthPercent}% / mana {MinManaPercent}% (0 disables a threshold).");
            lines.Add($"Use potions: {(UsePotions ? "Yes" : "No")} · buy potions: {(BuyPotions ? "Yes" : "No")}");
            lines.Add(Activity == "questing" ? "Follows the quest already tracked in the game. Your equipped deck is used." : $"Starts from your current dungeon entrance. Team Up: {(TeamUp ? "Yes" : "No")}. A run label does not navigate to a destination.");
        }
        lines.Add("Current game compatibility must be checked live. Progress reports observations, not estimated quest completion.");
        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static ActivityProfile Load(string path)
    {
        var profile = JsonSerializer.Deserialize<ActivityProfile>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidOperationException("This setup file is empty.");
        profile.Validate();
        return profile;
    }
}

internal sealed class ActivityProgress
{
    public string Phase { get; set; } = "starting";
    public string Message { get; set; } = "Starting the backend…";
    public int ElapsedSeconds { get; set; }
    public string? Zone { get; set; }
    public int? Health { get; set; }
    public int? MaxHealth { get; set; }
    public int? Mana { get; set; }
    public int? MaxMana { get; set; }
    public int? BattlesEnded { get; set; }
    public int? GamesPlayed { get; set; }
    public int? GamesRequested { get; set; }
    public int? DanceRound { get; set; }
}

internal sealed record GuidedActivitySession(string Tool, string ProfilePath, string EventsPath, string ControlPath, ActivityProfile Profile);
