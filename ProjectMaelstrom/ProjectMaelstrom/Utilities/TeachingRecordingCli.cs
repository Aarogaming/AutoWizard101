using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record TeachingRecordingRequest(TeachingTarget Target, string DataRoot, int Minutes,
    string StopFile, bool RecordInputs = false, bool EnableW101Telemetry = false, string? Workspace = null,
    string InputProfile = "Movement");

internal static class TeachingRecordingCli
{
    private sealed class Host : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08000000; return value; }
        }
    }

    internal static void Validate(TeachingRecordingRequest request)
    {
        _ = LongTeachingSession.Plan(request.Minutes);
        if (request.InputProfile is not ("Movement" or "Wizard101Navigation") ||
            request.InputProfile == "Wizard101Navigation" && request.Target?.ProcessName != "WizardGraphicalClient")
            throw new InvalidDataException("Select a supported, target-specific input profile.");
        if (request.Target is null || request.Target.ProcessId <= 0 || request.Target.StartedUtcTicks <= 0 || request.Target.WindowHandle <= 0 ||
            !Path.IsPathFullyQualified(request.DataRoot) || !Path.IsPathFullyQualified(request.StopFile) ||
            request.EnableW101Telemetry && (request.Target.ProcessName != "WizardGraphicalClient" || request.Workspace == null || !Path.IsPathFullyQualified(request.Workspace)))
            throw new InvalidDataException("Use an exact selected target, absolute paths and an explicit W101 workspace for optional telemetry.");
        if (File.Exists(request.StopFile)) throw new InvalidOperationException("Stop request already exists. Use a fresh stop-file path.");
    }

    public static void Run(string requestPath, string reportPath)
    {
        if (new FileInfo(requestPath).Length > 65536 || File.Exists(reportPath)) throw new InvalidDataException("Request too large or report already exists.");
        var request = JsonSerializer.Deserialize<TeachingRecordingRequest>(File.ReadAllText(requestPath)) ?? throw new InvalidDataException("Missing recording request.");
        Validate(request);
        var target = GameWindows.Read((nint)request.Target.WindowHandle);
        if (target == null || target.ProcessId != request.Target.ProcessId || target.StartedUtcTicks != request.Target.StartedUtcTicks || target.ProcessName != request.Target.ProcessName)
            throw new InvalidOperationException("Selected game identity changed. No recording started.");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var host = new Host { ShowInTaskbar = false, Location = new(-30000, -30000), StartPosition = FormStartPosition.Manual, Size = new(1, 1) };
        host.Shown += async (_, _) =>
        {
            using var timeline = new SessionTimelineForm(request.DataRoot, target);
            try
            {
                await timeline.RecordHeadlessAsync(request.Minutes, request.RecordInputs, request.EnableW101Telemetry ? request.Workspace : null, request.StopFile,
                    Enum.Parse<GameplayInputProfile>(request.InputProfile));
                using var file = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(file, new { session = timeline.LastSessionPath, failure = timeline.RecordingFailure,
                    actor = "observed-user", sends_input = false, model_required = false }, LongTeachingSession.Json);
                if (timeline.RecordingFailure != null) Environment.ExitCode = 2;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
            finally { host.Close(); }
        };
        Application.Run(host);
    }
}
