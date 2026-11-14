using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace ProjectMaelstrom.Utilities;

internal sealed record W101ModuleImage(byte[] Png, Size Geometry, double CapturedMs);
internal sealed record W101ModuleDiagnostics(string Stage, double? ElapsedMs, int ExpectedWidth, int ExpectedHeight,
    int? CapturedWidth, int? CapturedHeight, int? PngBytes, double? CapturedMs, double? ObservedMs,
    double? FrameAgeMs, double? TreeAgeMs);
internal sealed record W101ModuleObservation(W101ModuleSample Sample, ModuleVisualRecognition Recognition,
    string ReadinessReason = "unqualified", W101ModuleDiagnostics? Diagnostics = null)
{
    internal Size Geometry => Recognition.Frame.Geometry;
}
internal sealed record W101ModuleObserverRuntime(
    Func<OperationContext, WizwalkerTarget, WizwalkerLaunch, int, CancellationToken, Task<W101UiTreeResult>> Read,
    Func<WizwalkerTarget, Size, Func<bool>, CancellationToken, Task<W101ModuleImage>> Capture,
    Func<byte[], ModuleVisualFrame, CancellationToken, Task<ModuleVisualRecognition>> Recognize,
    Func<double> Clock, Func<long> UtcTicks);

/// <summary>One bounded, game-window-only observation under the current input
/// owner. UI hooks are disconnected before capture/OCR; capture is disposed
/// before return. No input, outside-world inference or proposed-outcome echo.</summary>
internal static class W101ModuleObserver
{
    private static readonly W101ModuleObserverRuntime Runtime = new(W101UiTreeOperation.ReadOwnedAsync,
        CaptureAsync, W101MenuRecognition.ReadPngAsync, FrameTimeline.TimestampMs, () => DateTime.UtcNow.Ticks);

    internal static async Task<W101ModuleObservation> ReadAsync(OperationContext context, WizwalkerTarget target,
        WizwalkerLaunch launch, int index, RoutineBinding binding, Size expectedGeometry, Func<bool> ready,
        CancellationToken token, W101ModuleObserverRuntime? runtime = null, bool recognize = true)
    {
        var useLocalRecognizer = runtime is null;
        runtime ??= Runtime;
        var stage = "admission";
        var started = runtime.Clock();
        W101ModuleImage? captured = null;
        W101UiTreeResult? observed = null;
        try
        {
        if (index is < 0 or > 23 || !ModuleRecognition.ValidGeometry(expectedGeometry) ||
            binding.TargetId != "pid-" + target.ProcessId + "-start-" + target.StartedUtcTicks || context.Request.Target != binding.TargetId)
            throw new InvalidDataException("Bind a bounded observation to the selected target and capture geometry.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        bool Owned() => !deadline.IsCancellationRequested && context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current) && ready();
        void Check()
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (!Owned()) throw new InvalidOperationException("Selected target, user takeover or ownership changed.");
        }
        Check();
        if (useLocalRecognizer && recognize)
        {
            stage = "recognizer-startup";
            await W101MenuRecognition.PrepareAsync(deadline.Token).ConfigureAwait(false);
            Check();
        }
        stage = "ui-read";
        W101UiTreeResult reading;
        try { reading = await runtime.Read(context, target, launch, index, deadline.Token).ConfigureAwait(false); }
        catch (W101UiObservationUnavailableException) { throw; }
        catch
        {
            context.Input!.RequireRecovery("Module UI reader ended without a cleanup receipt.");
            throw;
        }
        observed = reading;
        stage = "ui-cleanup";
        if (!reading.CleanupConfirmed || !reading.HookRestored)
        {
            context.Input!.RequireRecovery("Module observation did not confirm UI hook cleanup.");
            throw new InvalidDataException("UI observation cleanup is unconfirmed.");
        }
        Check();
        stage = "ui-identity";
        if (reading.Target != target || reading.Operation != W101UiTreeOperation.Version ||
            reading.SourceSha256 != WizwalkerOperation.SourceSha256 || reading.SchemaVersion != 1 || reading.Status != "observed")
            throw new InvalidDataException("UI observation identity is invalid.");
        stage = "capture";
        var image = await runtime.Capture(target, expectedGeometry, Owned, deadline.Token).ConfigureAwait(false);
        captured = image;
        Check();
        stage = "image-admission";
        if (image.Geometry != expectedGeometry || image.Png.Length is < 33 or > W101MenuRecognition.MaximumPngBytes ||
            !double.IsFinite(image.CapturedMs) || image.CapturedMs < 0 || image.CapturedMs > runtime.Clock())
            throw new InvalidDataException("Captured image has invalid geometry, size or timestamp.");
        var digest = Convert.ToHexString(SHA256.HashData(image.Png)).ToLowerInvariant();
            // One bounded frame per indexed read, preserved for offline diagnosis.
            // Never overwrite earlier evidence or launch another capture for review.
            var imagePath = Path.Combine(context.Directory, "module-frame-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            using (var saved = new FileStream(imagePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                await saved.WriteAsync(image.Png, deadline.Token).ConfigureAwait(false);
        var frame = new ModuleVisualFrame(digest, image.CapturedMs, image.Geometry, binding, "local-windows-ocr");
        stage = "recognition";
        var recognition = recognize
            ? await runtime.Recognize(image.Png, frame, deadline.Token).ConfigureAwait(false)
            : new ModuleVisualRecognition(frame with { TextOrigin = "selected-window-capture" }, "Unknown", [], [], "capture-only-no-visual-facts");
        Check();
        stage = "recognition-identity";
        if (recognition.Frame.ImageSha256 != digest || recognition.Frame.CapturedMs != image.CapturedMs)
            throw new InvalidDataException("Recognition does not describe the captured image.");
        var now = runtime.UtcTicks();
        stage = "tree-freshness-after-recognition";
        var loadingWithoutTree = reading.Loading == true && reading.StatesObservedUtcTicks == 0 &&
            reading.States.Values.All(value => value is null) && reading.StableStates.Values.All(value => value is null);
        if ((!loadingWithoutTree && reading.StatesObservedUtcTicks <= 0) || reading.StatesObservedUtcTicks > reading.ObservedUtcTicks || reading.ObservedUtcTicks > now)
            throw new InvalidDataException("UI observation timestamp ordering is invalid.");
        var treeAge = loadingWithoutTree ? double.PositiveInfinity : (now - reading.StatesObservedUtcTicks) / (double)TimeSpan.TicksPerMillisecond;
        var treeFresh = treeAge <= 2000;
        stage = "visual-context";
        var clockAtEnd = runtime.Clock();
        var contextFacts = treeFresh ? W101ModuleBindings.FromVisual(recognition, binding, expectedGeometry, clockAtEnd) : new();
        var prompt = W101UiTreeOperation.ValidInteractionTitle(reading, now)
            ? reading.StableInteractionTitle : null;
        var readinessReason = loadingWithoutTree ? "paused-for-loading" : !treeFresh ? "stale-ui-tree" :
            !double.IsFinite(clockAtEnd) || clockAtEnd < image.CapturedMs || clockAtEnd - image.CapturedMs > ModuleRecognition.MaximumAgeMs
                ? "stale-or-invalid-image-clock" : "candidate-observation-only";
        static double? Number(double value) => double.IsFinite(value) ? value : null;
        return new(new(reading, contextFacts, InteractionPrompt: prompt), recognition, readinessReason,
            new("completed", Number(clockAtEnd - started), expectedGeometry.Width, expectedGeometry.Height,
                image.Geometry.Width, image.Geometry.Height, image.Png.Length, Number(image.CapturedMs), Number(clockAtEnd),
                Number(clockAtEnd - image.CapturedMs), treeAge));
        }
        catch (Exception error)
        {
            // Local diagnostics deliberately exclude pixels, UI text and credentials.
            // Preserve the original exception type so existing recovery handling is unchanged.
            static double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;
            var clock = runtime.Clock();
            error.Data["w101ModuleDiagnostics"] = new W101ModuleDiagnostics(stage, Finite(clock - started),
                expectedGeometry.Width, expectedGeometry.Height, captured?.Geometry.Width, captured?.Geometry.Height,
                captured?.Png.Length, Finite(captured?.CapturedMs), Finite(clock), Finite(clock - captured?.CapturedMs),
                observed is null ? null : Finite((runtime.UtcTicks() - observed.StatesObservedUtcTicks) / (double)TimeSpan.TicksPerMillisecond));
            throw;
        }
    }

    internal static async Task<W101ModuleImage> CaptureAsync(WizwalkerTarget target, Size expected,
        Func<bool> ready, CancellationToken token)
    {
        if (!ready() || !target.IsCurrent()) throw new InvalidOperationException("Selected window is not ready.");
        var observer = new WindowObserver(target.Window);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(3000);
        try
        {
            if (new Size(observer.Width, observer.Height) != expected) throw new InvalidDataException("Capture geometry changed.");
            for (var attempt = 0; attempt < 8; attempt++)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!ready() || !target.IsCurrent()) throw new InvalidOperationException("Selected window changed during capture.");
                using var frame = await PetDanceObservationRead.ReadAsync(
                    () => observer.ReadTimestampedAsync(latestOnly: true), deadline.Token).ConfigureAwait(false);
                if (frame is not null && frame.CaptureTime is { } stamp)
                {
                    if (frame.Image.Size != expected) throw new InvalidDataException("Capture geometry changed.");
                    using var encoded = new MemoryStream();
                    frame.Image.Save(encoded, ImageFormat.Png);
                    if (encoded.Length > W101MenuRecognition.MaximumPngBytes) throw new InvalidDataException("Capture exceeds image budget.");
                    return new(encoded.ToArray(), frame.Image.Size, stamp.TotalMilliseconds);
                }
                await observer.WaitForFrameAsync(deadline.Token).ConfigureAwait(false);
            }
            throw new InvalidOperationException("No timestamped game frame arrived within the capture budget.");
        }
        finally
        {
            var cleanup = observer.DisposeAsync().AsTask();
            try { await cleanup.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
            catch
            {
                _ = cleanup.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                GameInputLease.Current?.RequireRecovery("Module capture cleanup remains pending or failed.");
                throw;
            }
        }
    }
}
