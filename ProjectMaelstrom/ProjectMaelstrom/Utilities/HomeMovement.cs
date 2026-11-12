using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record HomeView(Size Size, byte[] Pixels)
{
    internal const int Width = 160, Height = 90;
    public static HomeView From(Bitmap image)
    {
        WindowObserver.CheckSize(image.Width, image.Height);
        using var small = new Bitmap(Width, Height);
        using (var graphics = Graphics.FromImage(small)) { graphics.InterpolationMode = InterpolationMode.Bilinear; graphics.DrawImage(image, 0, 0, Width, Height); }
        var pixels = new byte[Width * Height * 3];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var color = small.GetPixel(x, y); var i = (y * Width + x) * 3;
                pixels[i] = color.R; pixels[i + 1] = color.G; pixels[i + 2] = color.B;
            }
        return new(image.Size, pixels);
    }

    internal double Difference(HomeView other, Rectangle region)
    {
        if (Size != other.Size || Pixels.Length != Width * Height * 3 || other.Pixels.Length != Pixels.Length) return 1;
        long difference = 0;
        for (var y = region.Top; y < region.Bottom; y++)
            for (var x = region.Left; x < region.Right; x++)
                for (var c = 0; c < 3; c++) difference += Math.Abs(Pixels[(y * Width + x) * 3 + c] - other.Pixels[(y * Width + x) * 3 + c]);
        return difference / (region.Width * region.Height * 3.0 * 255);
    }
    internal bool HasDetail(Rectangle region)
    {
        byte min = 255, max = 0;
        for (var y = region.Top; y < region.Bottom; y++)
            for (var x = region.Left; x < region.Right; x++)
            {
                var value = Pixels[(y * Width + x) * 3]; min = Math.Min(min, value); max = Math.Max(max, value);
            }
        return max - min >= 35;
    }
}

/// <summary>Conservative visual change guard; home identity is explicitly confirmed by the user.</summary>
internal sealed class HomeReference(HomeView reference, Size? clientSize = null)
{
    // Compact health/mana and spellbook anchors exclude the animated world behind
    // the HUD, including with W101's Tiny UI at 1280x720. A broad corner includes
    // floor pixels and can reject the very movement we are checking.
    internal static readonly Rectangle LeftHud = new(1, 80, 16, 9), RightHud = new(151, 82, 6, 7), Scene = new(20, 14, 120, 58);
    public HomeView View { get; } = reference;
    // WGC can include the title bar; foreground input checks must use the separate client size.
    public Size ClientSize { get; } = clientSize ?? reference.Size;
    public bool Usable => View.Pixels.Length == HomeView.Width * HomeView.Height * 3 && View.Size.Width >= 640 && View.Size.Height >= 480 && View.HasDetail(LeftHud) && View.HasDetail(RightHud) && View.HasDetail(Scene);
    public string? Reject(HomeView current)
    {
        var hud = RejectHud(current);
        if (hud != null) return hud;
        if (View.Difference(current, Scene) > .12) return "home-scene-changed";
        for (var y = Scene.Top; y < Scene.Bottom; y += 29)
            for (var x = Scene.Left; x < Scene.Right; x += 30)
                if (View.Difference(current, new Rectangle(x, y, 30, 29)) > .22) return "home-scene-region-changed";
        return null;
    }
    public string? RejectHud(HomeView current)
    {
        if (!Usable) return "reference-needs-visible-hud-and-scene";
        if (current.Size != View.Size) return "game-size-changed";
        if (View.Difference(current, LeftHud) > .06 || View.Difference(current, RightHud) > .06) return "home-hud-changed";
        return null;
    }
    internal object Measurements(HomeView current) => new
    {
        left_hud = View.Difference(current, LeftHud),
        right_hud = View.Difference(current, RightHud),
        scene = View.Difference(current, Scene),
        rejection = Reject(current)
    };
}

[JsonConverter(typeof(JsonStringEnumConverter<HomeIdleMode>))]
internal enum HomeIdleMode { NativeIdle, SpinLeft, SpinRight, OpposingTurns, WalkPairs, Routine }

internal sealed record HomeMovementOptions(int IntervalSeconds = 90, int SessionMinutes = 15, int PulseMs = 60, HomeIdleMode Mode = HomeIdleMode.SpinLeft, string[]? RoutineActions = null)
{
    [JsonIgnore] public bool RotatesOneWay => Mode is HomeIdleMode.SpinLeft or HomeIdleMode.SpinRight;
    [JsonIgnore] public bool UsesRollingScene => RotatesOneWay || Mode == HomeIdleMode.Routine;
    [JsonIgnore] public bool RequiresHome => Mode == HomeIdleMode.WalkPairs || (Mode == HomeIdleMode.Routine && Keys.Any(k => k is "W" or "S"));
    [JsonIgnore]
    public string[] Keys => Mode switch
    {
        HomeIdleMode.SpinLeft => ["A"],
        HomeIdleMode.SpinRight => ["D"],
        HomeIdleMode.OpposingTurns => ["A", "D"],
        HomeIdleMode.WalkPairs => ["W", "S"],
        HomeIdleMode.Routine => (RoutineActions ?? []).Select(id => W101Routine.Resolve(id).Key).ToArray(),
        _ => []
    };
    public void Validate()
    {
        if (RoutineActions != null) W101Routine.ValidateActions(RoutineActions);
        if (!Enum.IsDefined(Mode) || IntervalSeconds is < 60 or > 300 || SessionMinutes is < 1 or > 30 || PulseMs is < 40 or > 150)
            throw new ArgumentOutOfRangeException(nameof(IntervalSeconds), "Home movement requires a 60–300 second interval, 1–30 minute session and 40–150 ms steps.");
        if (Mode == HomeIdleMode.Routine)
        {
            W101Routine.ValidateActions(RoutineActions);
            if (RoutineActions!.Length * PulseMs > 600) throw new InvalidDataException("A motion routine may hold keys for at most 600 ms per cycle. Shorten the pulse or remove actions.");
        }
    }
    public void Save(string dataRoot) { Validate(); Directory.CreateDirectory(dataRoot); GameExperiments.WriteAtomic(Path.Combine(dataRoot, "auto-idle-settings.json"), this); }
    public static HomeMovementOptions Load(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "auto-idle-settings.json");
        if (!File.Exists(path)) return new();
        var options = JsonSerializer.Deserialize<HomeMovementOptions>(GameExperiments.BoundedBytes(path, 16384)) ?? throw new InvalidDataException("Invalid Auto-Idle settings.");
        options.Validate(); return options;
    }
}

internal sealed record HomeMovementSignal(double NowMs, bool TargetReady, bool KeysHeld, long ActivityRevision, double LastActivityMs, bool Escape, bool Enabled);
internal enum HomeMovementState { Arming, Running, Paused, Stopped }

/// <summary>Pure session gate. Pauses are latched and never automatically regain focus or resume.</summary>
internal sealed class HomeMovementGate
{
    private readonly HomeMovementOptions _options;
    private readonly double _started;
    private double _due;
    private long _acceptedActivity;
    internal long? ActivityBaseline { get; private set; }
    private double? _readySince;
    public HomeMovementState State { get; private set; } = HomeMovementState.Arming;
    public string Reason { get; private set; } = "Switch to Wizard101 and leave the keyboard and mouse quiet for five seconds.";
    public int Cycles { get; private set; }
    public HomeMovementGate(HomeMovementOptions options, double now) { options.Validate(); _options = options; _started = now; }
    public bool Evaluate(HomeMovementSignal signal)
    {
        if (State is HomeMovementState.Stopped or HomeMovementState.Paused) return false;
        if (!double.IsFinite(signal.NowMs) || !double.IsFinite(signal.LastActivityMs) || signal.NowMs < _started || signal.LastActivityMs > signal.NowMs)
        { Pause("clock-or-activity-unavailable"); return false; }
        if (!signal.Enabled) { Pause("session-controls-disabled"); return false; }
        if (signal.Escape) { Pause("escape"); return false; }
        if (signal.NowMs - _started >= _options.SessionMinutes * 60_000 || Cycles >= 20) { Stop("session-limit"); return false; }
        if (State == HomeMovementState.Arming)
        {
            if (signal.NowMs - _started >= 60_000) { Pause("game-not-ready-before-countdown-ended"); return false; }
            if (!signal.TargetReady || signal.KeysHeld) { _readySince = null; return false; }
            _readySince ??= signal.NowMs;
            if (signal.NowMs - _readySince < 5000 || signal.NowMs - signal.LastActivityMs < 5000) return false;
            _acceptedActivity = signal.ActivityRevision;
            ActivityBaseline = _acceptedActivity;
            State = HomeMovementState.Running; Reason = "Running · waiting for the next bounded cycle."; _due = signal.NowMs;
        }
        if (!signal.TargetReady) { Pause("game-lost-focus-or-changed"); return false; }
        if (signal.ActivityRevision != _acceptedActivity || signal.KeysHeld) { Pause("desktop-activity"); return false; }
        return signal.NowMs >= _due;
    }
    public bool PermitsPulse(long revision, bool targetReady, bool enabled, bool escape) =>
        State == HomeMovementState.Running && revision == _acceptedActivity && targetReady && enabled && !escape;
    public void CompletedPair(double now) { if (State != HomeMovementState.Running) return; Cycles++; _due = now + _options.IntervalSeconds * 1000; }
    public void Pause(string reason) { if (State == HomeMovementState.Stopped) return; State = HomeMovementState.Paused; Reason = reason; }
    public void Stop(string reason = "stopped-by-user") { State = HomeMovementState.Stopped; Reason = reason; }
    internal static bool IsWizard101(GameWindow target) => target.ProcessName.Equals("WizardGraphicalClient", StringComparison.OrdinalIgnoreCase);
}

internal static class HomeMovementMessages
{
    public static string Explain(string reason) => reason switch
    {
        "desktop-activity" or "desktop-activity-or-target-change" => "Keyboard, mouse, or another app took over",
        "game-lost-focus-or-changed" => "Wizard101 lost focus, closed, or changed size",
        "escape" => "Escape was pressed",
        "session-limit" => "The session time or movement limit was reached",
        "stopped-by-user" => "Stopped by you",
        "session-controls-disabled" => "Session controls or input monitoring became unavailable",
        "game-not-ready-before-countdown-ended" => "Wizard101 was not ready within one minute",
        "home-hud-changed" or "home-scene-changed" or "home-scene-region-changed" or "game-size-changed" => "The game view changed; check it and capture a new home reference if needed",
        "movement-unconfirmed" => "No clear visual change followed the step; check for a menu or obstruction",
        "fresh-game-frame-unavailable" or "post-input-frame-unavailable" or "frame-aged-before-input" => "A fresh game image was unavailable",
        _ => reason.Replace('-', ' ')
    };
}
