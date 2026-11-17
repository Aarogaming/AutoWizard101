using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace ProjectMaelstrom.Utilities;

/// <summary>Production implementor of <see cref="IPetDanceVisitRuntime"/> and
/// <see cref="IPrimedPetDanceVisitRuntime"/> for Wizard City Pet Dance visits.
/// It delegates gameplay to <see cref="PetDanceLiveTrial.RunSpecialistAsync"/>, enforces
/// fail-closed geometry and arena guards, and withholds energy spend approval pending a
/// verified policy. No secondary capture session, specialist loop, or independent input
/// lease is created.</summary>
internal sealed class PetDanceProductionRuntime : IPrimedPetDanceVisitRuntime, IDisposable
{
    public const string EntryCostWithheldReason = "entry-cost-policy-unwired";

    private readonly GameWindow _target;
    private readonly PetDanceVisitOptions _options;
    private readonly string _profilePath;
    private readonly string _outputPath;
    private readonly nuint _inputTag;
    private readonly PetDanceLevelSelectionVision? _levelVision;
    private readonly PetDancePlayButtonVision? _playVision;
    private readonly PetDanceMenuInput _menuInput;
    private readonly DesktopActivityMonitor? _monitor;
    private readonly bool _ownsMonitor;
    private readonly Func<bool>? _controlPermission;
    private readonly string? _audioComparisonPath;
    private readonly Func<Task<CapturedWindowFrame?>> _readFrame;
    private readonly Func<Bitmap, CancellationToken, Task<PetDanceMenuText>> _readOcr;
    private readonly Func<OperationContext, GameWindow, Size, string, string, PetDanceSpecialistControl, CancellationToken, string?, Func<bool>?, Func<CancellationToken, Task<PetDanceStartReceipt>>?, Action<Bitmap, double>?, Task<PetDanceSpecialistResult>> _runSpecialist;
    private readonly Func<GameWindow, bool> _isTargetCurrent;
    private readonly Func<GameWindow, Size> _getClientSize;
    private readonly Func<double> _clock;
    private readonly Func<int, CancellationToken, Task> _delay;
    private readonly Func<double>? _quietMs;

    private WindowObserver? _internalObserver;
    private PetDanceMenuOcr? _internalOcr;
    private bool _specialistCompleted;
    private bool _disposed;

    public bool UsesGameInput => true;
    public IReadOnlyDictionary<string, string> Pins { get; }
    public string EntryCostPolicyReason => EntryCostWithheldReason;

    public PetDanceProductionRuntime(
        GameWindow target,
        PetDanceVisitOptions options,
        string profilePath,
        string outputPath,
        nuint? inputTag = null,
        DesktopActivityMonitor? monitor = null,
        Func<bool>? controlPermission = null,
        string? audioComparisonPath = null)
        : this(
            target,
            options,
            profilePath,
            outputPath,
            inputTag ?? (nuint)Random.Shared.NextInt64(1, long.MaxValue),
            levelVision: null,
            playVision: null,
            menuInput: null,
            monitor: monitor,
            controlPermission: controlPermission,
            audioComparisonPath: audioComparisonPath,
            readFrame: null,
            readOcr: null,
            runSpecialist: null)
    {
    }

    internal PetDanceProductionRuntime(
        GameWindow target,
        PetDanceVisitOptions options,
        string profilePath,
        string outputPath,
        nuint inputTag,
        PetDanceLevelSelectionVision? levelVision,
        PetDancePlayButtonVision? playVision,
        PetDanceMenuInput? menuInput,
        DesktopActivityMonitor? monitor,
        Func<bool>? controlPermission,
        string? audioComparisonPath,
        Func<Task<CapturedWindowFrame?>>? readFrame,
        Func<Bitmap, CancellationToken, Task<PetDanceMenuText>>? readOcr,
        Func<OperationContext, GameWindow, Size, string, string, PetDanceSpecialistControl, CancellationToken, string?, Func<bool>?, Func<CancellationToken, Task<PetDanceStartReceipt>>?, Action<Bitmap, double>?, Task<PetDanceSpecialistResult>>? runSpecialist,
        Func<GameWindow, bool>? isTargetCurrent = null,
        Func<GameWindow, Size>? getClientSize = null,
        Func<double>? clock = null,
        Func<int, CancellationToken, Task>? delay = null,
        Func<double>? quietMs = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.TargetId) || string.IsNullOrWhiteSpace(options.ContextId) ||
            options.Geometry.Width <= 0 || options.Geometry.Height <= 0 || !Enum.IsDefined(options.Arena))
            throw new ArgumentException("Bind a valid target, positive geometry, context, and defined arena.", nameof(options));

        _target = target;
        _options = options;
        _profilePath = profilePath ?? string.Empty;
        _outputPath = outputPath ?? string.Empty;
        _inputTag = inputTag;
        _levelVision = levelVision;
        _playVision = playVision;
        _menuInput = menuInput ?? new PetDanceMenuInput(inputTag);
        _monitor = monitor;
        _ownsMonitor = false;
        _controlPermission = controlPermission;
        _audioComparisonPath = audioComparisonPath;
        _isTargetCurrent = isTargetCurrent ?? GameWindows.IsCurrent;
        _getClientSize = getClientSize ?? GameWindows.ClientSize;
        _clock = clock ?? FrameTimeline.TimestampMs;
        _delay = delay ?? Task.Delay;
        _quietMs = quietMs;

        _readFrame = readFrame ?? (() => GetObserver().ReadTimestampedAsync(latestOnly: true));
        _readOcr = readOcr ?? ((image, ct) => GetOcr().ReadAsync(image, ct));
        _runSpecialist = runSpecialist ?? PetDanceLiveTrial.RunSpecialistAsync;

        var pins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["target"] = options.TargetId,
            ["geometry"] = FormattableString.Invariant($"{options.Geometry.Width}x{options.Geometry.Height}"),
            ["arena"] = options.Arena.ToString(),
            ["entry_cost_policy"] = "unwired"
        };
        if (!string.IsNullOrWhiteSpace(profilePath) && File.Exists(profilePath))
            pins[profilePath] = GameExperiments.Digest(profilePath);
        Pins = pins;
    }

    public async Task<bool> CountdownAsync(OperationContext context, CancellationToken token)
    {
        ThrowIfDisposed();
        if (token.IsCancellationRequested) return false;
        if (_options.Geometry != PetDanceLevelSelectionVision.QualifiedGeometry || _options.Arena != PetDanceArena.WizardCity)
            return false;
        if (!_isTargetCurrent(_target) || _getClientSize(_target) != _options.Geometry)
            return false;
        if (UsesGameInput && !PetDanceSpecialistAdmission.OwnsInput(context, _target))
            return false;
        if (DesktopActivityMonitor.AnyKeyHeld())
            return false;
        if (_monitor != null && (_monitor.EscapeRequested || !_monitor.Installed))
            return false;

        var quiet = _quietMs?.Invoke() ?? (_monitor != null ? _clock() - _monitor.LastActivityMs : 500);
        if (quiet < 500)
        {
            for (var i = 0; i < 50; i++)
            {
                if (token.IsCancellationRequested) return false;
                await _delay(10, token).ConfigureAwait(false);
                if (_monitor != null && _monitor.EscapeRequested) return false;
                if (DesktopActivityMonitor.AnyKeyHeld()) return false;
                var currentQuiet = _quietMs?.Invoke() ?? (_monitor != null ? _clock() - _monitor.LastActivityMs : 500);
                if (currentQuiet >= 500) break;
            }
        }
        return !token.IsCancellationRequested && !DesktopActivityMonitor.AnyKeyHeld() && (_monitor == null || !_monitor.EscapeRequested);
    }

    public async Task<PetDanceVisitObservation> ObserveAsync(OperationContext context, CancellationToken token)
    {
        ThrowIfDisposed();
        var now = _clock();
        if (token.IsCancellationRequested)
            return new(now, new string('0', 64), PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId, Cancelled: true);

        if (_options.Geometry != PetDanceLevelSelectionVision.QualifiedGeometry)
        {
            var hash = new string('0', 64);
            var entry = new PetDanceEntryScene(PetDanceScene.Unknown, hash, now, _options.Geometry, "geometry-unqualified");
            return new(now, hash, PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId,
                Entry: entry, Fresh: false, TargetReady: false, ContextVerified: false, InputOwned: false);
        }

        if (_options.Arena != PetDanceArena.WizardCity)
        {
            var hash = new string('0', 64);
            var entry = new PetDanceEntryScene(PetDanceScene.Unknown, hash, now, _options.Geometry, "arena-unqualified");
            return new(now, hash, PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId,
                Entry: entry, Fresh: false, TargetReady: false, ContextVerified: false, InputOwned: false);
        }

        var targetReady = _isTargetCurrent(_target) && _getClientSize(_target) == _options.Geometry;
        var inputOwned = !UsesGameInput || PetDanceSpecialistAdmission.OwnsInput(context, _target);
        if (!targetReady)
        {
            var hash = new string('0', 64);
            var entry = new PetDanceEntryScene(PetDanceScene.Unknown, hash, now, _options.Geometry, "target-not-ready");
            return new(now, hash, PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId,
                Entry: entry, Fresh: false, TargetReady: false, ContextVerified: false, InputOwned: inputOwned);
        }

        using var frame = await _readFrame().ConfigureAwait(false);
        if (frame == null)
        {
            return new(now, new string('0', 64), PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId,
                Fresh: false, TargetReady: targetReady, ContextVerified: true, InputOwned: inputOwned);
        }

        var capturedMs = frame.CaptureTime?.TotalMilliseconds ?? now;
        var frameAge = now - capturedMs;
        var isFresh = double.IsFinite(capturedMs) && capturedMs >= 0 && frameAge >= 0 && frameAge <= 300;

        if (frame.Image.Size != _options.Geometry)
        {
            var imageHash = ComputeSha256(frame.Image);
            var entry = new PetDanceEntryScene(PetDanceScene.Unknown, imageHash, capturedMs, frame.Image.Size, "geometry-unqualified");
            return new(capturedMs, imageHash, PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId,
                Entry: entry, Fresh: false, TargetReady: false, ContextVerified: false, InputOwned: inputOwned);
        }

        var hashString = ComputeSha256(frame.Image);
        if (!isFresh)
        {
            return new(capturedMs, hashString, PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId,
                Fresh: false, TargetReady: targetReady, ContextVerified: true, InputOwned: inputOwned);
        }

        var text = await _readOcr(frame.Image, token).ConfigureAwait(false);
        var postgame = PetDancePostGameClassifier.Classify(text);

        if (postgame.Scene == PetDanceScene.NativeIdle)
        {
            return new(capturedMs, hashString, PetDanceScene.NativeIdle, _options.TargetId, _options.Geometry, _options.ContextId,
                Fresh: true, TargetReady: targetReady, ContextVerified: true, InputOwned: inputOwned,
                ControlBounds: postgame.ActionBounds);
        }

        if (postgame.Scene is PetDanceScene.GameResult or PetDanceScene.SnackOffer or PetDanceScene.MenuObserved)
        {
            return new(capturedMs, hashString, postgame.Scene, _options.TargetId, _options.Geometry, _options.ContextId,
                Fresh: true, TargetReady: targetReady, ContextVerified: true, InputOwned: inputOwned,
                ControlBounds: postgame.ActionBounds,
                WholeGameResultVerified: postgame.Scene == PetDanceScene.GameResult && _specialistCompleted);
        }

        var initialEntry = PetDanceEntryVision.Classify(text, hashString, capturedMs);
        if (initialEntry.Scene is PetDanceScene.DanceSigil)
        {
            return new(capturedMs, hashString, PetDanceScene.DanceSigil, _options.TargetId, _options.Geometry, _options.ContextId,
                Entry: initialEntry, Fresh: true, TargetReady: targetReady, ContextVerified: true, InputOwned: inputOwned);
        }

        if (initialEntry.Scene is PetDanceScene.InstructionsUnselected or PetDanceScene.InstructionsReady)
        {
            var control = new PetDanceLevelControl(PetDanceArena.WizardCity, PetDanceLevelSelectionVision.QualifiedControlBounds, IdentityVerified: true);
            PetDancePlayButtonResult? playResult = null;
            if (initialEntry.PlayBounds is { } playBounds && _playVision != null)
                playResult = _playVision.Classify(frame.Image, playBounds);

            PetDanceLevelSelectionResult? levelResult = null;
            if (_levelVision != null)
            {
                var binding = new PetDanceLevelSelectionBinding(hashString, capturedMs, frame.Image.Size, control,
                    InstructionsVerified: true,
                    PointerAwayVerified: true,
                    PlayEnabledVerified: playResult?.State == PetDancePlayState.Enabled);
                levelResult = _levelVision.Classify(frame.Image, hashString, capturedMs, binding);
            }

            var selectedArena = levelResult?.SelectionVerified == true ? levelResult.Arena :
                (initialEntry.Scene == PetDanceScene.InstructionsReady ? PetDanceArena.WizardCity : (PetDanceArena?)null);
            var selectionVerified = levelResult?.SelectionVerified == true || initialEntry.Scene == PetDanceScene.InstructionsReady;
            var playEnabledVerified = playResult?.State == PetDancePlayState.Enabled || initialEntry.Scene == PetDanceScene.InstructionsReady;

            var evidence = new PetDanceEntryEvidence(hashString, capturedMs, frame.Image.Size,
                LevelControls: [control],
                SelectedArena: selectedArena,
                SelectionHighlightVerified: selectionVerified,
                PlayEnabledVerified: playEnabledVerified);

            var refinedEntry = PetDanceEntryVision.Classify(text, hashString, capturedMs, evidence);
            return new(capturedMs, hashString, refinedEntry.Scene, _options.TargetId, _options.Geometry, _options.ContextId,
                Entry: refinedEntry, Fresh: true, TargetReady: targetReady, ContextVerified: true, InputOwned: inputOwned,
                EntryCostApproved: false); // STRICTLY FALSE without verified policy
        }

        return new(capturedMs, hashString, PetDanceScene.Unknown, _options.TargetId, _options.Geometry, _options.ContextId,
            Fresh: true, TargetReady: targetReady, ContextVerified: true, InputOwned: inputOwned);
    }

    public async Task<PetDanceVisitDispatch> DispatchAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plan);
        token.ThrowIfCancellationRequested();

        var now = _clock();
        if (_options.Geometry != PetDanceLevelSelectionVision.QualifiedGeometry || _options.Arena != PetDanceArena.WizardCity)
            return new(plan.ProposalId ?? "unsolicited", plan.Action, now, Accepted: false, InputReleased: true);
        if (!plan.IsProposal)
            return new(plan.ProposalId ?? "unsolicited", plan.Action, now, Accepted: false, InputReleased: true);

        if (plan.ActionBounds is { } bounds && !PetDancePostGameClassifier.ValidBounds(bounds, _options.Geometry))
            return new(plan.ProposalId ?? "unsolicited", plan.Action, now, Accepted: false, InputReleased: true);

        var postPlan = new PetDancePostGamePlan(plan.Action, true, plan.Reason, plan.ActionBounds);
        var receipt = await _menuInput.DispatchAsync(postPlan, _target, _options.Geometry,
            () => Ready(context), token, () => Continuing(context)).ConfigureAwait(false);

        return new(plan.ProposalId!, plan.Action, _clock(), receipt.Accepted, receipt.InputReleased);
    }

    public async Task<PetDanceSpecialistReceipt> PlayPrimedAsync(
        OperationContext context,
        PetDanceVisitPlan playPlan,
        Func<PetDanceVisitDispatch, PetDanceVisitObservation, PetDanceVisitPlan> bindHandoff,
        CancellationToken token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(playPlan);
        ArgumentNullException.ThrowIfNull(bindHandoff);
        token.ThrowIfCancellationRequested();

        if (_options.Geometry != PetDanceLevelSelectionVision.QualifiedGeometry)
            throw new InvalidOperationException("geometry-unqualified");
        if (_options.Arena != PetDanceArena.WizardCity)
            throw new InvalidOperationException("arena-unqualified");
        if (!playPlan.IsProposal || playPlan.Action != PetDanceLifecycleAction.Play)
            throw new InvalidOperationException("Primed play requires an active Play proposal.");

        // Cleanly dispose our observation session so there is zero overlap with specialist capture
        await DisposeObserverAsync().ConfigureAwait(false);

        PetDanceVisitDispatch? playDispatch = null;
        PetDanceVisitPlan? handoffPlan = null;
        var handoffGate = new object();
        var handoffBound = false;
        var handoffCalls = 0;

        async Task<PetDanceStartReceipt> StartAfterCaptureReady(CancellationToken ct)
        {
            var postPlan = new PetDancePostGamePlan(PetDanceLifecycleAction.Play, true, playPlan.Reason, playPlan.ActionBounds);
            var dispatchReceipt = await _menuInput.DispatchAsync(postPlan, _target, _options.Geometry,
                () => Ready(context), ct, () => Continuing(context)).ConfigureAwait(false);
            var dispatchTime = _clock();
            playDispatch = new PetDanceVisitDispatch(playPlan.ProposalId!, PetDanceLifecycleAction.Play,
                dispatchTime, dispatchReceipt.Accepted, dispatchReceipt.InputReleased);
            return new PetDanceStartReceipt(dispatchReceipt.Accepted, dispatchReceipt.InputReleased);
        }

        void FirstWatchObserved(Bitmap image, double time)
        {
            lock (handoffGate)
            {
                if (handoffBound)
                    throw new InvalidOperationException("Watch handoff was already invoked.");
                if (playDispatch == null)
                    throw new InvalidOperationException("Play dispatch receipt is missing at Watch onset.");

                var hash = ComputeSha256(image);
                var watchObservation = new PetDanceVisitObservation(
                    TimeMs: time,
                    ImageSha256: hash,
                    Scene: PetDanceScene.Watch,
                    TargetId: _options.TargetId,
                    Geometry: _options.Geometry,
                    ContextId: _options.ContextId,
                    Fresh: true,
                    TargetReady: _isTargetCurrent(_target) && _getClientSize(_target) == _options.Geometry,
                    ContextVerified: true,
                    InputOwned: !UsesGameInput || PetDanceSpecialistAdmission.OwnsInput(context, _target),
                    EntryCostApproved: false,
                    WatchStartVerified: true,
                    SpecialistQualified: true,
                    WholeGameResultVerified: false,
                    UserActive: _monitor != null && _monitor.EscapeRequested,
                    Cancelled: token.IsCancellationRequested,
                    FocusPaused: false);

                handoffPlan = bindHandoff(playDispatch, watchObservation);
                handoffCalls++;
                handoffBound = true;
            }
        }

        var control = CreateSpecialistControl(context, token);
        var specialistResult = await _runSpecialist(
            context, _target, _options.Geometry, _profilePath, _outputPath,
            control, token, _audioComparisonPath, _controlPermission,
            StartAfterCaptureReady, FirstWatchObserved).ConfigureAwait(false);

        lock (handoffGate)
        {
            if (handoffCalls != 1 || handoffPlan == null)
                throw new InvalidOperationException("Specialist completed without exactly one bound Watch handoff.");
        }

        _specialistCompleted = specialistResult.Armed && specialistResult.MayContinuePostGame && specialistResult.InputReleaseConfirmed;
        return PetDanceScheduledVisit.SpecialistReceipt(_options, handoffPlan, specialistResult, _clock());
    }

    public async Task<PetDanceSpecialistReceipt> PlayAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plan);
        token.ThrowIfCancellationRequested();

        if (_options.Geometry != PetDanceLevelSelectionVision.QualifiedGeometry)
            throw new InvalidOperationException("geometry-unqualified");
        if (_options.Arena != PetDanceArena.WizardCity)
            throw new InvalidOperationException("arena-unqualified");

        await DisposeObserverAsync().ConfigureAwait(false);
        var control = CreateSpecialistControl(context, token);
        var specialistResult = await _runSpecialist(
            context, _target, _options.Geometry, _profilePath, _outputPath,
            control, token, _audioComparisonPath, _controlPermission,
            null, null).ConfigureAwait(false);

        _specialistCompleted = specialistResult.Armed && specialistResult.MayContinuePostGame && specialistResult.InputReleaseConfirmed;
        return PetDanceScheduledVisit.SpecialistReceipt(_options, plan, specialistResult, _clock());
    }

    public Task<PetDanceVisitCleanup> FinishAsync(OperationContext context, bool idleAlreadyVerified, CancellationToken token)
    {
        ThrowIfDisposed();
        var inputReleased = true;
        if (GameInputLease.Current != null && ReferenceEquals(GameInputLease.Current, context.Input))
            inputReleased = !DesktopActivityMonitor.AnyKeyHeld();

        var reason = idleAlreadyVerified
            ? "Visit finished with verified native Idle."
            : "Visit finished without verified native Idle.";

        return Task.FromResult(new PetDanceVisitCleanup(idleAlreadyVerified, inputReleased, reason));
    }

    internal static string ComputeSha256(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private bool Ready(OperationContext context) =>
        !_disposed && _isTargetCurrent(_target) && _getClientSize(_target) == _options.Geometry &&
        (!UsesGameInput || PetDanceSpecialistAdmission.OwnsInput(context, _target)) &&
        !DesktopActivityMonitor.AnyKeyHeld() && (_monitor == null || !_monitor.EscapeRequested);

    private bool Continuing(OperationContext context) =>
        !_disposed && _isTargetCurrent(_target) && _getClientSize(_target) == _options.Geometry &&
        (!UsesGameInput || PetDanceSpecialistAdmission.OwnsInput(context, _target)) &&
        (_monitor == null || !_monitor.EscapeRequested);

    private PetDanceSpecialistControl CreateSpecialistControl(OperationContext context, CancellationToken token)
    {
        var baseline = _monitor?.Revision ?? 0;
        return new PetDanceSpecialistControl(
            InputTag: _inputTag,
            InitialRevision: baseline,
            ObservationReady: () => !token.IsCancellationRequested && _isTargetCurrent(_target) && _getClientSize(_target) == _options.Geometry && !GameWindows.Minimized(_target),
            InputReady: () => !token.IsCancellationRequested && (!UsesGameInput || PetDanceSpecialistAdmission.OwnsInput(context, _target)),
            ActivityRevision: () => _monitor?.Revision ?? baseline,
            NoKeysHeld: () => !DesktopActivityMonitor.AnyKeyHeld(),
            QuietMs: () => _quietMs?.Invoke() ?? (_monitor != null ? _clock() - _monitor.LastActivityMs : 500));
    }

    private WindowObserver GetObserver() => _internalObserver ??= new WindowObserver(_target);
    private PetDanceMenuOcr GetOcr() => _internalOcr ??= new PetDanceMenuOcr();

    private async Task DisposeObserverAsync()
    {
        if (_internalObserver != null)
        {
            var observer = _internalObserver;
            _internalObserver = null;
            await observer.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_internalObserver != null)
        {
            try { _internalObserver.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch { }
            _internalObserver = null;
        }
        if (_ownsMonitor) _monitor?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PetDanceProductionRuntime));
    }
}
