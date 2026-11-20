using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record GameplayInputEvent(double TimeMs, string Type, string? Control = null,
    int? X = null, int? Y = null, int? Wheel = null, bool Injected = false);

/// <summary>Bounded, target-scoped input observations relative to the video capture clock.</summary>
internal sealed class GameplayInputTimeline
{
    private readonly GameplayInputProfile _profile;
    public GameplayInputTimeline(GameplayInputProfile profile = GameplayInputProfile.Movement)
    {
        _ = GameplayInputRecorder.KeyNamesFor(profile);
        _profile = profile;
    }
    internal const int MaximumEvents = 150_000;
    private readonly object _gate = new();
    private readonly List<GameplayInputEvent> _events = new();
    private double? _origin, _arrival, _end;
    private bool _focused, _finished, _truncated;

    public void Start(double captureTime, double arrivalTime, bool focused)
    {
        lock (_gate)
        {
            if (_finished || _origin.HasValue) return;
            _origin = captureTime;
            _arrival = arrivalTime;
            _focused = focused;
            Append(new(Math.Max(0, arrivalTime - captureTime), focused ? "focus_gained" : "focus_lost"));
        }
    }

    public void Focus(double now, bool focused)
    {
        lock (_gate)
        {
            if (!_origin.HasValue || _finished || focused == _focused) return;
            _focused = focused;
            Append(new(Math.Max(0, now - _origin.Value), focused ? "focus_gained" : "focus_lost"));
        }
    }

    public void Input(double now, bool focused, string type, string? control = null,
        int? x = null, int? y = null, int? wheel = null, bool injected = false)
    {
        lock (_gate)
        {
            if (!_origin.HasValue || _finished) return;
            if (focused != _focused)
            {
                _focused = focused;
                Append(new(Math.Max(0, now - _origin.Value), focused ? "focus_gained" : "focus_lost"));
            }
            if (focused) Append(new(Math.Max(0, now - _origin.Value), type, control, x, y, wheel, injected));
        }
    }

    public void Stop(double now)
    {
        lock (_gate) { if (!_finished) { _end = now; _finished = true; } }
    }

    private void Append(GameplayInputEvent entry)
    {
        if (_events.Count >= MaximumEvents) { _truncated = true; return; }
        _events.Add(entry);
    }

    public void Save(string directory, GameWindow target, bool videoVerified = false)
    {
        lock (_gate)
        {
            if (!_origin.HasValue) return;
            var video = Path.Combine(directory, "capture.mp4");
            string? videoSha256 = null;
            if (videoVerified)
            {
                using var input = File.OpenRead(video);
                videoSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
            }
            var data = new
            {
                Version = 1,
                Kind = "observed-game-input",
                CaptureClock = "Windows QPC milliseconds; video offset uses first captured frame",
                VideoCaptureOriginMs = _origin.Value,
                FirstFrameCallbackMs = _arrival,
                FirstFrameDeliveryOffsetMs = _arrival - _origin,
                CaptureEndMs = _end,
                VideoFile = "capture.mp4",
                VideoVerified = videoVerified,
                VideoSha256 = videoSha256,
                TargetProcessId = target.ProcessId,
                TargetProcessStartUtcTicks = target.StartedUtcTicks,
                TargetProcessName = target.ProcessName,
                Coordinates = "selected window client pixels",
                Scope = "selected foreground window; selected game keys and mouse only",
                InputProfile = _profile.ToString(),
                KeyAllowlist = GameplayInputRecorder.KeyNamesFor(_profile),
                Truncated = _truncated,
                Events = _events.ToArray()
            };
            using var file = new FileStream(Path.Combine(directory, "input-events.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(file, data, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
