using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Wizard-scoped preferences. Unknown houses never inherit another wizard's routes.</summary>
internal sealed record WizardProfile(int SchemaVersion, string Id, string Name, string School,
    int LastKnownLevel, string? VerifiedCharacterId = null, string? ActiveHouseId = null,
    WizardHouseProfile[]? Houses = null);

internal static class WizardProfileStore
{
    internal static void Validate(WizardProfile profile)
    {
        if (profile.SchemaVersion != 1 || !Guid.TryParseExact(profile.Id, "N", out _) ||
            string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 100 ||
            string.IsNullOrWhiteSpace(profile.School) || profile.School.Length > 30 ||
            profile.LastKnownLevel is < 1 or > 1000 || (profile.Houses?.Length ?? 0) > 50)
            throw new InvalidDataException("Invalid wizard profile.");
        var houses = profile.Houses ?? [];
        if (houses.Any(h => string.IsNullOrWhiteSpace(h.Id) || string.IsNullOrWhiteSpace(h.HouseId) ||
            h.Wizard.Name != profile.Name || h.Wizard.School != profile.School) ||
            houses.Select(h => h.Id).Distinct(StringComparer.Ordinal).Count() != houses.Length ||
            profile.ActiveHouseId is not null && !houses.Any(h => h.Id == profile.ActiveHouseId))
            throw new InvalidDataException("A house must belong to this wizard; unknown houses remain unconfigured.");
    }

    internal static WizardProfile Load(string path)
    {
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Wizard profile exceeds size limit.");
        var profile = JsonSerializer.Deserialize<WizardProfile>(File.ReadAllBytes(path), JobDeckRules.Json)
            ?? throw new InvalidDataException("Missing wizard profile.");
        Validate(profile);
        return profile;
    }

    internal static void Save(string directory, WizardProfile profile)
    {
        Validate(profile);
        Directory.CreateDirectory(directory);
        GameExperiments.WriteAtomic(Path.Combine(directory, profile.Id + ".json"), profile);
    }

    internal static WizardProfile RefreshLevel(string directory, WizardProfile observed)
    {
        Validate(observed);
        var path = Path.Combine(directory, observed.Id + ".json");
        var existing = File.Exists(path) ? Load(path) : observed;
        if (!existing.Name.Equals(observed.Name, StringComparison.OrdinalIgnoreCase) ||
            !existing.School.Equals(observed.School, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Profile ID already belongs to another wizard.");
        var updated = existing with { LastKnownLevel = observed.LastKnownLevel };
        Save(directory, updated);
        return updated;
    }
}
