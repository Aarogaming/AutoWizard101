using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Observed wizard identity; level is mutable and excluded. Generation
/// belongs to the observation session, not a name or process identifier.</summary>
internal sealed record W101WizardSubject(string ProfileId, string Name, string School, long Generation,
    long ObservedUtcTicks, string? VerifiedCharacterId = null);
internal sealed record W101WizardReference(WizwalkerTarget Target, W101WizardSubject Subject,
    long ObservedUtcTicks, int Gold, int Level, string ImageSha256);

/// <summary>Same-subject visual corroboration. Historical matches never become a live cache.</summary>
internal static class W101WizardFacts
{
    /// <summary>Current-build owner/character-ID-qualified projection. This path
    /// never reuses saved profile values or treats a school/level as identity.</summary>
    internal static W101WizardSubject? AddObserved(Dictionary<string, JsonElement> values, W101UiTreeResult reading, long now)
    {
        values.Remove("gold.current"); values.Remove("wizard.level");
        if (reading.ResourceCandidate is not { } candidate || reading.Resources is null ||
            !W101UiTreeOperation.ValidResources(reading, true, now) || reading.Target.StartedUtcTicks <= 0 ||
            reading.Target.ImageSha256 != "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a" ||
            now - reading.StatesObservedUtcTicks > TimeSpan.FromSeconds(2).Ticks) return null;
        try
        {
            var identity = candidate.GetProperty("wizardIdentity");
            var key = identity.GetProperty("characterKey").GetString();
            var stamp = candidate.GetProperty("observedUtcTicks").GetInt64();
            if (identity.GetProperty("status").GetString() != "observed" ||
                identity.GetProperty("source").GetString() != "client-object-registry-v1" ||
                !identity.GetProperty("statsMatched").GetBoolean() || key is null || key.Length != 64 || !key.All(Uri.IsHexDigit) ||
                identity.GetProperty("readCalls").GetInt32() is < 1 or > 256 ||
                identity.GetProperty("readBytes").GetInt32() is < 1 or > 16384 ||
                stamp < reading.StatesObservedUtcTicks || stamp > now || now - stamp > TimeSpan.FromSeconds(2).Ticks ||
                candidate.GetProperty("status").GetString() != "candidate" ||
                candidate.GetProperty("layout").GetString() != "d410-property-registration-candidate") return null;
            var fields = candidate.GetProperty("namedFields");
            if (fields.GetArrayLength() > 128) return null;
            long Scalar(string name, int offset, string type)
            {
                var matches = fields.EnumerateArray().Where(f => f.GetProperty("name").GetString() == name).ToArray();
                if (matches.Length != 1 || matches[0].GetProperty("quality").GetString() != "candidate" ||
                    matches[0].GetProperty("offset").GetInt32() != offset || matches[0].GetProperty("dataType").GetString() != type)
                    throw new InvalidOperationException("Unqualified scalar.");
                return matches[0].GetProperty("value").GetInt64();
            }
            var gold = Scalar("m_currentGold", 116, "int32"); var level = Scalar("m_referenceLevel", 324, "int32");
            var school = Scalar("m_schoolID", 328, "uint32") switch
            { 2343174 => "Fire", 72777 => "Ice", 83375795 => "Storm", 2448141 => "Myth", 2330892 => "Life", 78318724 => "Death", 1027491821 => "Balance", _ => null };
            if (gold is < 0 or > int.MaxValue || level is < 1 or > 1000 || school is null) return null;
            var subject = new W101WizardSubject(key[..32], "character:" + key[..12], school, reading.Target.StartedUtcTicks, stamp, key);
            values["gold.current"] = JsonSerializer.SerializeToElement((int)gold);
            values["wizard.level"] = JsonSerializer.SerializeToElement((int)level);
            return subject;
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return null; }
    }

    internal static bool ValidSubject(W101WizardSubject? subject) => subject is not null &&
        Guid.TryParseExact(subject.ProfileId, "N", out _) && subject.Generation > 0 && subject.ObservedUtcTicks > 0 &&
        !string.IsNullOrWhiteSpace(subject.Name) && subject.Name.Length <= 100 &&
        subject.School is "Fire" or "Ice" or "Storm" or "Myth" or "Life" or "Death" or "Balance" &&
        (subject.VerifiedCharacterId is null || subject.VerifiedCharacterId.Length is > 0 and <= 100);

    internal static bool SameSubject(W101WizardSubject? left, W101WizardSubject? right) =>
        ValidSubject(left) && ValidSubject(right) && left!.ProfileId == right!.ProfileId &&
        left.Generation == right.Generation && left.Name == right.Name && left.School == right.School &&
        left.VerifiedCharacterId == right.VerifiedCharacterId;

    internal static bool Add(Dictionary<string, JsonElement> values, W101UiTreeResult reading,
        W101WizardSubject? subject, W101WizardReference reference, long now)
    {
        values.Remove("gold.current"); values.Remove("wizard.level");
        bool Fresh(long ticks) => ticks > 0 && ticks <= now && now - ticks <= TimeSpan.FromSeconds(2).Ticks;
        if (!SameSubject(subject, reference.Subject) || reading.Target != reference.Target ||
            reading.Target.ImageSha256 != "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a" ||
            reading.Status != "observed" || reading.Loading != false || !reading.CleanupConfirmed || !reading.HookRestored ||
            !Fresh(reading.StatesObservedUtcTicks) || !Fresh(subject!.ObservedUtcTicks) || !Fresh(reference.Subject.ObservedUtcTicks) || !Fresh(reference.ObservedUtcTicks) ||
            reference.Gold < 0 || reference.Level is < 1 or > 1000 || reference.ImageSha256.Length != 64 ||
            !reference.ImageSha256.All(Uri.IsHexDigit) || reading.ResourceCandidate is not { } candidate) return false;
        try
        {
            if (candidate.GetProperty("status").GetString() != "candidate" ||
                candidate.GetProperty("layout").GetString() != "d410-property-registration-candidate" ||
                !Fresh(candidate.GetProperty("observedUtcTicks").GetInt64())) return false;
            var fields = candidate.GetProperty("namedFields");
            if (fields.GetArrayLength() > 128) return false;
            bool Matches(string name, int expected, int offset)
            {
                var matches = fields.EnumerateArray().Where(f => f.GetProperty("name").GetString() == name).ToArray();
                return matches.Length == 1 && matches[0].GetProperty("quality").GetString() == "candidate" &&
                    matches[0].GetProperty("offset").GetInt32() == offset && matches[0].GetProperty("dataType").GetString() == "int32" &&
                    matches[0].GetProperty("value").GetInt32() == expected;
            }
            if (!Matches("m_currentGold", reference.Gold, 116) || !Matches("m_referenceLevel", reference.Level, 324)) return false;
            values["gold.current"] = JsonSerializer.SerializeToElement(reference.Gold);
            values["wizard.level"] = JsonSerializer.SerializeToElement(reference.Level);
            return true;
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return false; }
    }
}
