using System.Diagnostics;
using ProjectMaelstrom.Utilities;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ProjectMaelstrom.Tests;

/// <summary>Test classes that read pixels with the real Windows OCR engine share this collection, so they run
/// one after another and only after <see cref="WindowsOcrWarmup"/> has started the engine.</summary>
[CollectionDefinition("Windows OCR")]
public sealed class WindowsOcrCollection : ICollectionFixture<WindowsOcrWarmup>
{
}

/// <summary>Creates the one real Windows OCR engine before the first test in the "Windows OCR" collection starts
/// a timed read. Production does the same in its separate recognizer-startup stage
/// (<c>W101MenuRecognition.PrepareAsync</c>, called before any read); a test that skips it makes the engine's
/// cold start count against the 5 s read deadline, which failed CI twice with a read that ended at exactly 5 s.
/// Best effort: when the engine is unavailable the tests that need it fail with their own message.</summary>
public sealed class WindowsOcrWarmup(IMessageSink diagnostics) : IAsyncLifetime
{
    /// <summary>What the warm-up did, for assertion messages. Set once, before the first test in the collection.</summary>
    public static string Summary { get; private set; } = "OCR warm-up not run";

    public async Task InitializeAsync()
    {
        var watch = Stopwatch.StartNew();
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await W101MenuRecognition.PrepareAsync(limit.Token);
            Summary = $"OCR warm-up took {watch.ElapsedMilliseconds} ms";
        }
        catch (Exception error)
        {
            Summary = $"OCR warm-up failed after {watch.ElapsedMilliseconds} ms ({error.GetType().Name})";
        }
        diagnostics.OnMessage(new DiagnosticMessage(Summary));
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
