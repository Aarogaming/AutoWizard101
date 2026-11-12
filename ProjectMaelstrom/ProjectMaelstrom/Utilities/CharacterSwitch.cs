namespace ProjectMaelstrom.Utilities;

/// <summary>Exact configured identity; screen position alone is never identity.</summary>
internal sealed record WizardIdentity(string Name, string School, int Level);
internal sealed record WizardHouseProfile(string Id, WizardIdentity Wizard, string HouseId,
    bool LayoutQualified = false, string? EvidenceDirectory = null, string Provenance = "unqualified");
internal sealed record CharacterSwitchObservation(long Sequence, bool Fresh, string Scene,
    WizardIdentity? Wizard = null, bool NativeIdle = false);

/// <summary>Offline transition contract. Outputs are proposals, never input receipts.
/// The runtime must supply fresh target-bound observations under its existing owner.</summary>
internal sealed class CharacterSwitch
{
    private readonly WizardHouseProfile _profile;
    private long _sequence = -1;
    private int _confirmations;
    public string State { get; private set; } = "menu";
    public WizardHouseProfile? ActiveProfile { get; private set; }
    public bool PreviousContextInvalidated { get; private set; }
    private bool Matches(WizardIdentity? wizard) => wizard is not null &&
        wizard.Name.Equals(_profile.Wizard.Name, StringComparison.OrdinalIgnoreCase) &&
        wizard.School.Equals(_profile.Wizard.School, StringComparison.OrdinalIgnoreCase);

    public CharacterSwitch(WizardHouseProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.HouseId) ||
            string.IsNullOrWhiteSpace(profile.Wizard.Name) || string.IsNullOrWhiteSpace(profile.Wizard.School) || profile.Wizard.Level < 1)
            throw new ArgumentException("Choose an exact wizard and its own house profile.");
        _profile = profile;
    }

    public string Observe(CharacterSwitchObservation observation)
    {
        if (State is "stopped" or "complete") return State;
        if (!observation.Fresh || observation.Sequence <= _sequence) return "wait";
        _sequence = observation.Sequence;
        if (observation.Scene is "login" or "disconnected" or "takeover") return Stop();
        if (State == "menu" && observation.Scene == "menu") return "quit-to-selection";
        if (State == "menu" && observation.Scene == "selection")
        {
            PreviousContextInvalidated = true;
            State = "selection";
        }
        if (State == "selection")
        {
            if (observation.Scene == "selection")
                return Matches(observation.Wizard) ? "play-selected-wizard" : "select-configured-wizard";
            if (observation.Scene != "loading") return Stop();
            State = "loading";
        }
        if (observation.Scene == "loading") { _confirmations = 0; return "wait"; }
        if (State is "loading" or "verify")
        {
            State = "verify";
            if (observation.Scene != "world" || observation.Wizard is null) { _confirmations = 0; return "wait"; }
            if (!Matches(observation.Wizard)) return Stop();
            if (++_confirmations < 2) return "wait";
            ActiveProfile = _profile;
            State = "idle";
            return "return-to-idle";
        }
        if (State == "idle")
        {
            if (!Matches(observation.Wizard)) return Stop();
            if (observation.NativeIdle) { State = "complete"; return State; }
            return "return-to-idle";
        }
        return "open-menu";
    }

    public string Stop() { ActiveProfile = null; State = "stopped"; return State; }
}
