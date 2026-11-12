using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record GameplaySamplePlan(string Purpose, string Goal, string? PackHash, string? PackName, GameResearch? Research = null)
{
    public void Save(string recordingDirectory)
    {
        Validate();
        File.WriteAllText(Path.Combine(recordingDirectory, "sampler.json"), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Validate()
    {
        if (Purpose is not ("teaching" or "check") || string.IsNullOrWhiteSpace(Goal) || Goal.Length > 240)
            throw new InvalidDataException("Choose a sample purpose and a short learning goal.");
        if (Purpose == "check" && string.IsNullOrWhiteSpace(PackHash))
            throw new InvalidDataException("Learn a teaching pack before collecting an independent check.");
        if (Research != null)
        {
            if (!Guid.TryParseExact(Research.Id, "N", out _)) throw new InvalidDataException("Invalid research ID.");
            _ = GameResearch.Create(Research.Game, Research.SourceUrl, Research.Hypothesis);
        }
    }

    public static GameplaySamplePlan? Load(string recordingDirectory)
    {
        var path = Path.Combine(recordingDirectory, "sampler.json");
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 8192) throw new InvalidDataException("Invalid sampler plan.");
        var plan = JsonSerializer.Deserialize<GameplaySamplePlan>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid sampler plan.");
        plan.Validate();
        return plan;
    }
}
