using System.Diagnostics;
using System.Globalization;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDancePlayerLaunch(ProcessStartInfo StartInfo, string ReportDirectory);

/// <summary>Builds a launch for the same installed native application. It starts no
/// process, creates no run folder and writes no Start/Idle confirmation requests.</summary>
internal static class PetDancePlayerLauncher
{
    public static PetDancePlayerLaunch BuildStartInfo(string executablePath, PetDancePreset preset,
        GameWindow target, string dataRoot, bool includeAudio, PetTrainingPreferences? training = null)
    {
        ArgumentNullException.ThrowIfNull(preset); ArgumentNullException.ThrowIfNull(target);
        training = PetTrainingPreferences.Normalize(training ?? new());
        if (includeAudio && training.Arena != PetDanceArena.WizardCity)
            throw new InvalidOperationException("Audio backup is qualified only for Wizard City. Turn it off for another arena.");
        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
            throw new InvalidOperationException("The installed Maelstrom application is unavailable.");
        if (!Path.IsPathFullyQualified(dataRoot)) throw new ArgumentException("Use an absolute data folder.", nameof(dataRoot));
        if (target.Handle == 0 || target.ProcessId <= 0 || target.StartedUtcTicks <= 0 || !HomeMovementGate.IsWizard101(target))
            throw new InvalidOperationException("Select a current Wizard101 window.");
        foreach (var path in new[] { preset.ProfilePath, preset.AudioComparisonPath })
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                throw new InvalidOperationException("The included Pet Dance player files are missing. Repair or reinstall Maelstrom.");
        var report = Path.Combine(Path.GetFullPath(dataRoot), "reports", "pet-dance",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Normal
        };
        info.ArgumentList.Add(includeAudio ? "--pet-dance-sound-trial" : "--pet-dance-visual-trial");
        info.ArgumentList.Add(target.Handle.ToInt64().ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(preset.ProfilePath);
        if (includeAudio) info.ArgumentList.Add(preset.AudioComparisonPath);
        info.ArgumentList.Add(report);
        info.ArgumentList.Add("--respect-control-setting");
        info.ArgumentList.Add("--arena=" + training.Arena);
        if (training.ReturnToNativeIdle) info.ArgumentList.Add("--return-native-idle");
        else info.ArgumentList.Add("--leave-awake");
        info.ArgumentList.Add("--target-process=" + target.ProcessId.ToString(CultureInfo.InvariantCulture) + ":" +
            target.StartedUtcTicks.ToString(CultureInfo.InvariantCulture));
        return new(info, report);
    }
}
