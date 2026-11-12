using System.Globalization;

namespace ProjectMaelstrom.Utilities;

/// <summary>The selected process identity survives the launcher/child boundary.
/// A recycled window handle must not silently select a different game process.</summary>
internal sealed record PetDanceTargetIdentity(int ProcessId, long StartedUtcTicks)
{
    public bool Matches(GameWindow target) => ProcessId > 0 && StartedUtcTicks > 0 &&
        target.ProcessId == ProcessId && target.StartedUtcTicks == StartedUtcTicks;
}

internal sealed record PetDanceTrialOptions(bool RespectControlSetting, PetDanceTargetIdentity? ExpectedTarget,
    bool ReturnToNativeIdle = true, PetDanceArena Arena = PetDanceArena.WizardCity, string? ClientLoadingWorkspace = null)
{
    public static PetDanceTrialOptions Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        const string identityPrefix = "--target-process=";
        var respect = false;
        bool? nativeIdle = null;
        PetDanceArena? arena = null;
        PetDanceTargetIdentity? identity = null;
        string? clientWorkspace = null;
        foreach (var argument in arguments)
        {
            if (argument == "--respect-control-setting" && !respect) respect = true;
            else if (argument == "--return-native-idle" && nativeIdle is null) nativeIdle = true;
            else if (argument == "--leave-awake" && nativeIdle is null) nativeIdle = false;
            else if (argument != null && argument.StartsWith("--client-loading-workspace=", StringComparison.Ordinal) && clientWorkspace == null)
            {
                var value = argument["--client-loading-workspace=".Length..];
                if (!Path.IsPathFullyQualified(value)) throw new ArgumentException("Use an absolute workspace for optional client loading.");
                clientWorkspace = Path.GetFullPath(value);
            }
            else if (argument != null && argument.StartsWith("--arena=", StringComparison.Ordinal) && arena == null)
            {
                var value = argument["--arena=".Length..];
                if (!Enum.GetNames<PetDanceArena>().Contains(value) || !Enum.TryParse<PetDanceArena>(value, out var selected))
                    throw new ArgumentException("Unknown Dance arena.");
                arena = selected;
            }
            else if (argument != null && argument.StartsWith(identityPrefix, StringComparison.Ordinal) && identity == null)
            {
                var fields = argument[identityPrefix.Length..].Split(':');
                if (fields.Length != 2 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0 ||
                    !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks <= 0)
                    throw new ArgumentException("Invalid selected game process identity.");
                identity = new(pid, ticks);
            }
            else throw new ArgumentException("Unknown or duplicate Pet Dance player option.");
        }
        if (respect && identity == null)
            throw new ArgumentException("An installed player launch must include the selected game process identity.");
        if (clientWorkspace != null && (identity == null || nativeIdle == false))
            throw new ArgumentException("Client loading requires an explicit target and the native-Idle continuation.");
        return new(respect, identity, nativeIdle ?? true, arena ?? PetDanceArena.WizardCity, clientWorkspace);
    }
}
