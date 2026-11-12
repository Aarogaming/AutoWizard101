using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal static class Program
{
    private static void TestTheme()
    {
        Console.WriteLine("Testing Theme Detection...");
        var theme = ThemeManager.GetSystemTheme();
        Console.WriteLine($"Detected theme: {theme}");
        Console.WriteLine($"Is Dark Mode: {ThemeManager.IsDarkMode}");
        Console.WriteLine($"Background Color: {ThemeManager.GetBackgroundColor()}");
        Console.WriteLine($"Foreground Color: {ThemeManager.GetForegroundColor()}");
        Console.WriteLine("Theme test completed.");
    }

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] is "--deck-api" or "--deck-host" or "--render-job-deck")
        {
            try { RunDeckCommandAsync(args).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--cli")
        {
            // Match captured physical pixels to Win32 client/screen coordinates.
            // CLI returns before the GUI's ApplicationConfiguration.Initialize,
            // so it must establish DPI awareness before creating any windows.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler stop = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += stop;
            try { Environment.ExitCode = MaelstromCli.RunAsync(args, Console.Error, cancellation.Token).GetAwaiter().GetResult(); }
            finally { Console.CancelKeyPress -= stop; }
            return;
        }
        if (args.Length > 0 && args[0] == "--review-module-screens")
        {
            try
            {
                if (args.Length != 3) throw new ArgumentException("Expected a saved screen manifest and a new isolated run directory.");
                var output = Path.GetFullPath(args[2]);
                if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Choose a new run directory.");
                var snapshot = ModuleScreenReviewSnapshot.Load(Path.GetFullPath(args[1]));
                RunScheduledScreenReview(snapshot, output).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--replay-modules")
        {
            try
            {
                if (args.Length != 4) throw new ArgumentException("Expected module bundle JSON, session manifest and a new report path.");
                if (!ModuleReplay.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]))) Environment.ExitCode = 2;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--review-pet-dance-entry-images")
        {
            try
            {
                if (args.Length != 3) throw new ArgumentException("Expected a saved-image manifest and a new report path.");
                if (!PetDanceEntryImageReview.RunAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])).GetAwaiter().GetResult())
                    Environment.ExitCode = 2;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--replay-pet-dance-visit")
        {
            try
            {
                if (args.Length != 3) throw new ArgumentException("Expected saved visit observations and a new report path.");
                if (!PetDanceVisitReplay.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]))) Environment.ExitCode = 2;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--replay-routine")
        {
            try
            {
                if (args.Length != 4) throw new ArgumentException("Expected routine JSON, session manifest and a new report path.");
                if (!RoutineReplay.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]))) Environment.ExitCode = 2;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--render-pet-workspace")
        {
            try
            {
                if (args.Length != 2) throw new ArgumentException("Expected a new offline review folder.");
                ApplicationConfiguration.Initialize();
                PetTrainingUiReview.Run(Path.GetFullPath(args[1]));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--review-pet-dance-menus")
        {
            try
            {
                if (args.Length < 3) throw new ArgumentException("Expected a new report path and saved image files.");
                PetDanceMenuOcr.ReviewAsync(Path.GetFullPath(args[1]), args.Skip(2).Select(Path.GetFullPath)).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--review-pet-dance-visual-sequences")
        {
            try
            {
                if (args.Length != 3) throw new ArgumentException("Expected saved visual observations and new review report.");
                PetDanceVisualSequenceReview.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--review-pet-dance-sound-sequences")
        {
            try
            {
                if (args.Length != 3) throw new ArgumentException("Expected saved sound-trial report and new review report.");
                PetDanceSoundSequence.Review(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--pet-dance-visual-trial")
        {
            try
            {
                if (args.Length < 4) throw new ArgumentException("Expected window, region visual profile and new trial folder.");
                var options = PetDanceTrialOptions.Parse(args.Skip(4));
                ApplicationConfiguration.Initialize();
                PetDanceLiveTrial.Run((nint)long.Parse(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]), interactive: true,
                    requireControlSetting: options.RespectControlSetting, expectedTarget: options.ExpectedTarget,
                    returnToNativeIdle: options.ReturnToNativeIdle, arena: options.Arena, clientLoadingWorkspace: options.ClientLoadingWorkspace);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--pet-dance-sound-trial")
        {
            try
            {
                if (args.Length < 5) throw new ArgumentException("Expected window, raw visual profile, frozen audio comparison and new trial folder.");
                var options = PetDanceTrialOptions.Parse(args.Skip(5));
                ApplicationConfiguration.Initialize();
                PetDanceLiveTrial.Run((nint)long.Parse(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[4]), Path.GetFullPath(args[3]), interactive: true,
                    requireControlSetting: options.RespectControlSetting, expectedTarget: options.ExpectedTarget,
                    returnToNativeIdle: options.ReturnToNativeIdle, arena: options.Arena, clientLoadingWorkspace: options.ClientLoadingWorkspace);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--review-pet-dance-raw-frames")
        {
            try
            {
                if (args.Length != 4) throw new ArgumentException("Expected profile, saved raw-frame folder and new report.");
                PetDanceRawFrameReview.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        // Finite research workers do not need owner/network checks or plugin startup.
        if (args.Length > 0 && args[0] == "--scan-pet-dance-audio")
        {
            try
            {
                if (args.Length != 5) throw new ArgumentException("Expected WAV, timing, frozen comparison and new report paths.");
                PetDanceAudioFileReview.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]), Path.GetFullPath(args[4]));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--observe-pet-dance-audio")
        {
            try
            {
                if (args.Length != 6) throw new ArgumentException("Expected window, visual profile, frozen audio comparison, new folder and seconds.");
                ApplicationConfiguration.Initialize();
                PetDanceSoundObserver.Run((nint)long.Parse(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]), Path.GetFullPath(args[4]), int.Parse(args[5]));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        // This finite audio worker needs neither plugins nor normal UI startup.
        // Keep its deadline independent of optional owner/theme/service initialization.
        if (args.Length > 0 && args[0] == "--verify-process-audio-recording")
        {
            try
            {
                if (args.Length != 7) throw new ArgumentException("Expected PID, process start ticks, new directory, milliseconds and two test frequencies.");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                Task.Run(() => GameAudioCaptureVerification.RunAsync(int.Parse(args[1]), long.Parse(args[2]), Path.GetFullPath(args[3]),
                    int.Parse(args[4]), double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture), timeout.Token)).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        DevMode.EnsureInitialized();

        // Check for theme test
        if (args.Length > 0 && args[0] == "--test-theme")
        {
            TestTheme();
            return;
        }

        // Check for utility tests
        if (args.Length > 0 && args[0] == "--run-tests")
        {
            ProjectMaelstrom.Tests.UtilityTests.RunAllTests();
            return;
        }

        AppBootstrap.Initialize();

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.

        Directory.CreateDirectory(Path.Combine(WorkshopHost.DataRoot(AppContext.BaseDirectory), "screenshots"));

        // Hook global exception logging in dev mode for quicker diagnostics.
        if (DevMode.IsEnabled)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, evt) =>
            {
                try
                {
                    Logger.LogError("[DevMode] Unhandled exception", evt.ExceptionObject as Exception);
                }
                catch { /* ignore */ }
            };
            Application.ThreadException += (_, evt) =>
            {
                try
                {
                    Logger.LogError("[DevMode] UI thread exception", evt.Exception);
                }
                catch { /* ignore */ }
            };
        }

        // Load theme preference
        ThemeManager.SetModeFromString(Properties.Settings.Default["THEME_MODE"]?.ToString());

        ApplicationConfiguration.Initialize();
        if (args.Length == 4 && args[0] == "--import-pet-dance-teacher")
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var pack = PetDanceAssets.Import(args[1], args[2], timeout.Token);
                GameExperiments.WriteAtomic(args[3], new { imported = true, pack, assets = 8, calibration_installed = false, inputs_sent = 0 });
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 4 && args[0] == "--live-pet-dance-teacher")
        {
            try { PetDanceLiveTrial.Run((nint)long.Parse(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3])); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 4 && args[0] is "--replay-pet-dance-panels" or "--evaluate-pet-dance-panels")
        {
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); Task.Run(() => PetDancePanelReplay.RunAsync(args[1], args[2], args[3], timeout.Token, evaluateDifferentVideo: args[0] == "--evaluate-pet-dance-panels")).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--calibrate-pet-dance-recording")
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                Task.Run(() => PetDanceCalibrationReview.RunAsync(args[1], args[2], args[3], args[4], timeout.Token)).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 3 && args[0] == "--verify-pet-dance-teacher")
        {
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); Task.Run(() => PetDanceVerification.RunAsync(args[1], args[2], timeout.Token)).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Directory.CreateDirectory(args[2]); File.WriteAllText(Path.Combine(args[2], "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-pet-dance-ui")
        {
            try { using var form = new PetDanceTeacherForm(args[1]); form.RenderForVerification(args[1]); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-w101-routine-pack")
        {
            try
            {
                if (Directory.Exists(args[1])) throw new IOException("Use a new verification folder.");
                var routine = new W101Routine("verify-motion", "Verified export sample", "Auto-Idle", ["turn-left", "turn-left", "turn-right"]);
                routine.Save(args[1]); Console.WriteLine(routine.ExportPack(args[1]));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 4 && args[0] is "--record-w101-demonstration" or "--record-w101-lifecycle" or "--record-w101-lifecycle-audio")
        {
            try { W101DemonstrationRecorder.Run((nint)long.Parse(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]), supervisedUi: args[0] != "--record-w101-demonstration", includeGameAudio: args[0] == "--record-w101-lifecycle-audio"); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 6 && args[0] == "--sample-window-stills")
        {
            try
            {
                var target = GameWindows.Read((nint)long.Parse(args[1])) ?? throw new InvalidOperationException("The selected window is unavailable.");
                Console.WriteLine(Task.Run(() => PassiveTeachingSampler.RunAsync(target, args[2], args[3], int.Parse(args[4]), int.Parse(args[5]), CancellationToken.None)).GetAwaiter().GetResult());
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-home-movement-monitor")
        {
            try { HomeMovementVerification.Run(args[1]); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 3 && args[0] == "--verify-home-movement-ui")
        {
            try { using var form = new HomeMovementForm(args[1]); form.RenderForVerification(args[2]); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--create-footage-study")
        {
            try { Console.WriteLine(new GameExperiments(args[1]).CreateFootageStudy(args[3], args[4], args[2])); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 4 && args[0] == "--preserve-experiment")
        {
            try { Console.WriteLine(new GameExperiments(args[1]).Preserve(args[2], args[3])); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 7 && args[0] == "--import-experiment-video")
        {
            try
            {
                var store = new GameExperiments(args[1]); store.Load(args[2]);
                var recording = VideoTeachingImport.RunAsync(args[1], "experiment-" + args[2], args[3], args[4], CancellationToken.None, int.Parse(args[5]), int.Parse(args[6])).GetAwaiter().GetResult();
                var assessment = FootageAssessment.Attach(store, args[2], recording, "Unreviewed local footage imported by command. Visual examples; no verified action labels.");
                Console.WriteLine(assessment);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(FootageAssessment.Assess(recording.Frames)));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 3 && args[0] == "--verify-experiments-ui")
        {
            try { using var form = new ExperimentsForm(args[1]); form.RenderForVerification(args[2]); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 6 && args[0] == "--create-experiment")
        {
            try { Console.WriteLine(new GameExperiments(args[1]).CreateDodge(args[5], "Compare the candidate with the reference and validate on two fresh sets.", args[2], args[3], args[4], DodgeExperimentRunner.Fingerprints(args[2]))); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--run-experiment")
        {
            try
            {
                var store = new GameExperiments(args[1]);
                DodgeExperimentRunner.RunAsync(store, args[2], int.Parse(args[3]), int.Parse(args[4]), new Progress<string>(Console.WriteLine), CancellationToken.None).GetAwaiter().GetResult();
                var plan = store.Load(args[2]);
                Console.WriteLine(GameExperiments.Verdict(plan, store.Results(plan)));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--benchmark-wsl-dodge")
        {
            try { Console.WriteLine(DodgeWslVerification.RunAsync(args[1], args[2], CancellationToken.None, int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), true, double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture), endurance: true, benchmark: true).GetAwaiter().GetResult()); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--train-wsl-dodge")
        {
            try { Console.WriteLine(DodgeWslVerification.RunAsync(args[1], args[2], CancellationToken.None, int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), true, double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture), endurance: true).GetAwaiter().GetResult()); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--verify-wsl-dodge-step")
        {
            try { Console.WriteLine(DodgeWslVerification.RunAsync(args[1], args[2], CancellationToken.None, int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), true, double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture)).GetAwaiter().GetResult()); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length is 3 or 4 && args[0] == "--verify-wsl-dodge")
        {
            try { Console.WriteLine(DodgeWslVerification.RunAsync(args[1], args[2], CancellationToken.None, args.Length == 4 ? int.Parse(args[3]) : null).GetAwaiter().GetResult()); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 6 && args[0] == "--verify-native-dodge-setup")
        {
            try
            {
                var outputRoot = Path.GetFullPath(args[5]);
                var project = DodgeNativeVerification.PrepareProject(Path.GetFullPath(args[2]), Path.GetFullPath(args[3]), outputRoot, CancellationToken.None);
                Console.WriteLine(DodgeNativeVerification.RunAsync(Path.GetFullPath(args[1]), project, Path.GetFullPath(args[4]), outputRoot, CancellationToken.None).GetAwaiter().GetResult());
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--verify-native-dodge")
        {
            try { Console.WriteLine(DodgeNativeVerification.RunAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]), Path.GetFullPath(args[4]), CancellationToken.None).GetAwaiter().GetResult()); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--verify-owned-dodge")
        {
            try { DodgeOwnedVerification.RunAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]), Path.GetFullPath(args[4])).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 3 && args[0] == "--export-dodge-session")
        {
            try
            {
                var recording = TeachingRecording.Open(Path.GetFullPath(args[1]));
                Console.WriteLine(DodgeSessionExport.RunAsync(recording, Path.GetFullPath(args[2]), CancellationToken.None).GetAwaiter().GetResult());
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 3 && args[0] == "--verify-local-vision")
        {
            try
            {
                var recording = TeachingRecording.Open(Path.GetFullPath(args[1]));
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                var label = LocalVision.SuggestAsync(recording, int.Parse(args[2])).GetAwaiter().GetResult();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { label, elapsed_ms = elapsed.Elapsed.TotalMilliseconds }));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-teaching-review")
        {
            TeachingReviewVerification.Run(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-review-suggestions")
        {
            try
            {
                var recording = TeachingRecording.Open(Path.GetFullPath(args[1]));
                var suggestions = ReviewSuggestionsFile.Load(recording);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { suggestions = suggestions.Count, labeled = recording.Frames.Count(frame => !string.IsNullOrWhiteSpace(frame.State)) }));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 4 && args[0] == "--verify-external-video-only")
        {
            try
            {
                var target = GameWindows.Read((nint)long.Parse(args[1])) ?? throw new InvalidOperationException("The selected window is not available.");
                var seconds = int.Parse(args[3]);
                if (seconds is < 2 or > 60) throw new ArgumentOutOfRangeException(nameof(seconds));
                GpuVideoRecording.RunAsync(target, Path.GetFullPath(args[2]), TimeSpan.FromSeconds(seconds), CancellationToken.None, () => { }).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 7 && args[0] == "--verify-reviewed-pack")
        {
            try { Console.WriteLine(ExternalObserveVerification.ExportReviewed(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]), args[4], int.Parse(args[5]), args[6])); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length is 3 or 4 or 5 && args[0] == "--verify-external-observe")
        {
            try
            {
                if (args.Length == 5 && args[4] != "--prioritize-video") throw new ArgumentException("Unknown capture option.");
                ExternalObserveVerification.Run((nint)long.Parse(args[1]), Path.GetFullPath(args[2]), args.Length >= 4 ? int.Parse(args[3]) : 3, args.Length == 5);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 4 && args[0] == "--verify-external-input")
        {
            try { ExternalObserveVerification.Run((nint)long.Parse(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]), recordInput: true); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--capture-target-server")
        {
            CaptureTargetServer.Run(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-gpu-separate-720")
        {
            try { SeparateCaptureVerification.RunAsync(Path.GetFullPath(args[1]), 60).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-gpu-separate-occlusion")
        {
            try { SeparateCaptureVerification.RunAsync(Path.GetFullPath(args[1]), 10, cover: true).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 3 && args[0] == "--warmup-native-encoder")
        {
            try { NativeEncoderWarmup.RunAsync(int.Parse(args[1]), int.Parse(args[2]), CancellationToken.None).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-transitions")
        {
            try
            {
                var recording = TeachingRecording.Open(Path.GetFullPath(args[1]));
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(TransitionSuggestions.Suggest(recording, CancellationToken.None)));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length is 4 or 5 && args[0] == "--verify-video-import")
        {
            try
            {
                var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")) ?? throw new InvalidOperationException("FFmpeg is unavailable.");
                var result = VideoTeachingImport.RunAsync(Path.GetFullPath(args[2]), "Offline video test", Path.GetFullPath(args[1]), ffmpeg, CancellationToken.None, int.Parse(args[3]), args.Length == 5 ? int.Parse(args[4]) : 0).GetAwaiter().GetResult();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { recording = result.DirectoryPath, frames = result.Frames.Count }));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 7 && args[0] == "--verify-motion-track")
        {
            try
            {
                var recording = TeachingRecording.Open(Path.GetFullPath(args[1]));
                var region = new Rectangle(int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]));
                var result = MotionSuggestions.Track(recording, int.Parse(args[2]), region, CancellationToken.None);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 7 && args[0] == "--verify-motion-pair")
        {
            try
            {
                using var earlier = new Bitmap(args[1]);
                using var later = new Bitmap(args[2]);
                var region = new Rectangle(int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]));
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(MotionSuggestions.Compare(earlier, later, region)));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-video-probe")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-observe-video")
        {
            ObserveVideoVerification.Run(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-observe-input")
        {
            ObserveVideoVerification.Run(Path.GetFullPath(args[1]), checkInputCapture: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-observe-input-interactive")
        {
            ObserveVideoVerification.Run(Path.GetFullPath(args[1]), checkInputCapture: true, requireTestInput: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-prioritized-pause")
        {
            ObserveVideoVerification.Run(Path.GetFullPath(args[1]), checkPrioritizedPause: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-static-observe")
        {
            ObserveVideoVerification.Run(Path.GetFullPath(args[1]), checkStaticTarget: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-static-video")
        {
            ObserveVideoVerification.Run(Path.GetFullPath(args[1]), checkStaticVideo: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-static-video-no-change")
        {
            ObserveVideoVerification.Run(Path.GetFullPath(args[1]), checkStaticVideo: true, initialChange: false);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-native-window-video")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-native-window-720")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true, hd: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-native-window-720-sustained")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true, hd: true, sustained: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-gpu-window-720-sustained")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true, hd: true, sustained: true, gpu: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-gpu-paced-720")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true, hd: true, sustained: true, gpu: true, paced: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-gpu-warm-720")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true, hd: true, sustained: true, gpu: true, paced: true, warm: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-gpu-direct-720")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true, hd: true, sustained: true, gpu: true, paced: true, warm: true, direct: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-gpu-direct-720-long")
        {
            VideoProbeVerification.Run(Path.GetFullPath(args[1]), native: true, hd: true, sustained: true, gpu: true, paced: true, warm: true, direct: true, seconds: 60);
            return;
        }
        if (args.Length == 3 && args[0] == "--verify-gpu-lifecycle")
        {
            GpuLifecycleVerification.Run(Path.GetFullPath(args[1]), args[2]);
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-native-window-loss")
        {
            NativeVideoLossVerification.Run(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-native-timestamps")
        {
            try { NativeTimestampProbe.RunAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-observe")
        {
            ObserveVerification.Run(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Length == 2 && args[0] is "--workspace-preview" or "--sampler-preview")
        {
            using Form preview = args[0] == "--sampler-preview" ? new GameplaySamplerForm(null) : new GameWorkspaceForm();
            preview.StartPosition = FormStartPosition.Manual;
            preview.Location = new Point(-30000, -30000);
            preview.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, preview.Size));
            bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
            preview.Close();
            return;
        }
        if (args.Length == 2 && args[0] is "--activity-preview" or "--activity-preview-pet" or "--activity-preview-checks" or "--activity-preview-review" or "--activity-preview-farming")
        {
            using var service = new ToolIntegrationService();
            var states = service.CheckAsync().GetAwaiter().GetResult().ToDictionary(state => state.Tool);
            var activity = args[0] == "--activity-preview-pet" ? "pet_dance" : args[0] == "--activity-preview-farming" ? "farming" : "questing";
            using var preview = new GuidedActivityForm(service, states, () => null, activity);
            preview.ApplyProfile(new Models.ActivityProfile { Activity = activity, CombatStyle = "aoe", Games = 3 });
            preview.PreviewStep(args[0] == "--activity-preview-checks" ? 2 : args[0] == "--activity-preview-review" ? 3 : 1);
            preview.StartPosition = FormStartPosition.Manual;
            preview.Location = new Point(-30000, -30000);
            preview.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, preview.Size));
            bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
            preview.Close();
            Application.DoEvents();
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-tools")
        {
            try { ToolCenterSmokeCheck.RunAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                File.WriteAllText(Path.GetFullPath(args[1]) + ".error.txt", ex.ToString());
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.Length == 2 && args[0] is "--tools-preview" or "--tools-preview-small" or "--tools-preview-maintenance" or "--tools-preview-details")
        {
            using var service = new ToolIntegrationService();
            var states = service.CheckAsync().GetAwaiter().GetResult();
            using var preview = new ToolCenterForm(service: service, checkOnShow: false);
            preview.ApplyReadiness(states);
            if (args[0] == "--tools-preview-small") preview.Size = new Size(860, 680);
            if (args[0] == "--tools-preview-maintenance") preview.SelectPreviewPage(1);
            if (args[0] == "--tools-preview-details") preview.SelectPreviewPage(2);
            preview.StartPosition = FormStartPosition.Manual;
            preview.Location = new Point(-30000, -30000);
            preview.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, preview.Size));
            bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
            preview.Close();
            return;
        }
        if (args.Length > 0 && args[0] == "--tools")
            Application.Run(new ToolCenterForm());
        else if (args.Contains("--legacy"))
            Application.Run(new Main());
        else
            Application.Run(new MaelstromHomeForm());
        ApplicationJobDeck.StopAsync().GetAwaiter().GetResult();
        ApplicationOperations.StopAllAsync().GetAwaiter().GetResult();
    }

    private static async Task RunDeckCommandAsync(string[] args)
    {
        if (args[0] == "--deck-api")
        {
            if (args.Length != 4) throw new ArgumentException("--deck-api <data-root> <request.json> <new-result.json>");
            using var output = new FileStream(Path.GetFullPath(args[3]), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var result = await Maelstrom.DeckApi.JobDeckWire.CallAsync(Path.GetFullPath(args[1]), GameExperiments.BoundedBytes(args[2], 262144));
            await System.Text.Json.JsonSerializer.SerializeAsync(output, result, JobDeckRules.Json);
            if (!result.GetProperty("ok").GetBoolean()) Environment.ExitCode = 2;
            return;
        }
        if (args[0] == "--render-job-deck")
        {
            if (args.Length is not (3 or 4) || args.Length == 4 && args[3] != "back") throw new ArgumentException("--render-job-deck <new-data-root> <new-image.png> [back]");
            if (Directory.Exists(args[1]) || File.Exists(args[2])) throw new IOException("Use new preview paths.");
            ApplicationConfiguration.Initialize();
            await using var scheduler = new OperationScheduler(args[1], Path.Combine(args[1], "locks"));
            await using var host = new JobDeckHost(args[1], scheduler);
            using var form = new JobDeckForm(host);
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000);
            using var surface = new Panel { Size = form.ClientSize, BackColor = form.BackColor, ForeColor = form.ForeColor };
            foreach (var child in form.Controls.Cast<Control>().ToArray()) surface.Controls.Add(child);
            void Layout(Control control) { control.CreateControl(); control.PerformLayout(); foreach (Control child in control.Controls) Layout(child); }
            Layout(surface);
            if (args.Length == 4)
            {
                IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
                var card = Descendants(surface).OfType<JobCardView>().Single(c => c.RoutineId == "w101.questing");
                card.PreviewFlip();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (!card.ShowingBack && timer.ElapsedMilliseconds < 2000) { Application.DoEvents(); Thread.Sleep(5); }
                if (!card.ShowingBack) throw new InvalidOperationException("Card flip did not finish.");
                Layout(surface);
            }
            using var bitmap = new Bitmap(surface.Width, surface.Height); surface.DrawToBitmap(bitmap, new Rectangle(Point.Empty, surface.Size)); bitmap.Save(args[2]);
            SynchronizationContext.SetSynchronizationContext(null);
            return;
        }
        if (args.Length != 3) throw new ArgumentException("--deck-host <data-root> <stop-file>");
        var root = Path.GetFullPath(args[1]); var stopFile = Path.GetFullPath(args[2]);
        if (File.Exists(stopFile)) return;
        await using var owner = new OperationScheduler(root);
        await using var server = new JobDeckHost(root, owner);
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { while (!File.Exists(stopFile) && !stop.IsCancellationRequested) await Task.Delay(250); }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static async Task RunScheduledScreenReview(ModuleScreenReviewSnapshot snapshot, string output)
    {
        await using var scheduler = new OperationScheduler(output);
        var ticket = ModuleOperation.SubmitOffline(scheduler, snapshot);
        var receipt = await ticket.Completion.ConfigureAwait(false);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(receipt, OperationModules.Json));
        if (receipt.State != OperationState.Completed) Environment.ExitCode = 2;
    }
}
