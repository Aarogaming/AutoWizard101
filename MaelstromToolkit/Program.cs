using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MaelstromToolkit.Planning;
using MaelstromToolkit.Policy;

namespace MaelstromToolkit;

internal static class Program
{
    private static readonly string DefaultPolicyText = PolicyDefaults.DefaultPolicyText;
    private record CommandOptions(
        string Command,
        string Subcommand,
        Dictionary<string, string> Args,
        bool Force,
        bool DryRun,
        bool Verbose,
        bool ShowHelp,
        bool ShowVersion);

    private static int Main(string[] args)
    {
        var options = Parse(args);
        if (options == null || options.ShowHelp)
        {
            PrintUsage();
            return options == null ? 1 : 0;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        var root = options.Args.TryGetValue("out", out var outDir)
            ? Path.GetFullPath(outDir)
            : Directory.GetCurrentDirectory();

        var summary = new List<string>();
        var warnings = new List<string>();

        try
        {
            if (RequiresOut(options.Command) && !options.Args.ContainsKey("out"))
            {
                Console.Error.WriteLine("ERROR: --out <dir> is required for this command.");
                return 1;
            }
            if (RequiresOut(options.Command) && !ValidateOut(root, options))
            {
                return 2;
            }

            Console.WriteLine($"MaelstromToolkit {GetVersion()} | command={options.Command} {options.Subcommand}".Trim());

            switch (options.Command)
            {
                case "init":
                    RunInit(root, options, summary, warnings);
                    break;
                case "policy" when options.Subcommand == "init":
                    CopyTemplate(root, "POLICY_BOUNDARY.md", options, summary, warnings);
                    CopyTemplate(root, "policy.config.sample", options, summary, warnings);
                    break;
                case "policy" when options.Subcommand == "validate":
                    return RunPolicyValidate(root, options);
                case "policy" when options.Subcommand == "effective":
                    return RunPolicyEffective(root, options);
                case "policy" when options.Subcommand == "watch":
                    return RunPolicyWatch(root, options);
                case "tags" when options.Subcommand == "init":
                    CopyTemplate(root, "TAG_POLICY.md", options, summary, warnings);
                    break;
                case "stewardship" when options.Subcommand == "init":
                    CopyTemplate(root, "STEWARDSHIP_CHECKLIST.md", options, summary, warnings);
                    CopyTemplate(root, "FEEDBACK_LOG.md", options, summary, warnings);
                    break;
                case "ux" when options.Subcommand == "init":
                    var framework = options.Args.TryGetValue("framework", out var fw) ? fw : "winforms";
                    CopyTemplate(root, "UX_MAINTENANCE.md", options, summary, warnings);
                    CopyTemplate(root, "UX_STYLE_GUIDE.md", options, summary, warnings, framework);
                    CopyTemplate(root, "UX_CHANGELOG.md", options, summary, warnings, framework);
                    CopyTemplate(root, "UX_TOKENS.md", options, summary, warnings, framework);
                    break;
                case "ci" when options.Subcommand == "add":
                    var provider = options.Args.TryGetValue("provider", out var p) ? p : "github";
                    var profile = options.Args.TryGetValue("profile", out var pr) ? pr : "tools-only";
                    CopyTemplate(root, $"{provider}_{profile}_workflow.yml", options, summary, warnings);
                    break;
                case "handoff":
                    CopyTemplate(root, "README.md", options, summary, warnings);
                    break;
                case "selftest":
                    return RunSelftest(options, summary, warnings);
                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }

        if (summary.Count > 0)
        {
            Console.WriteLine("Generated:");
            foreach (var line in summary) Console.WriteLine($"- {line}");
        }
        if (warnings.Count > 0)
        {
            Console.WriteLine("Warnings:");
            foreach (var line in warnings) Console.WriteLine($"- {line}");
        }

        return 0;
    }

    private static CommandOptions? Parse(string[] args)
    {
        if (args.Length == 0) return null;
        var showHelp = args.Contains("--help", StringComparer.OrdinalIgnoreCase);
        var showVersion = args.Contains("--version", StringComparer.OrdinalIgnoreCase);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();
        var force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
        var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var verbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var key = arg[2..];
                if (key is "force" or "dry-run" or "verbose" or "help" or "version") continue;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    dict[key] = args[i + 1];
                }
                continue;
            }
            positional.Add(arg);
        }

        if (positional.Count == 0) return null;
        string command = positional[0];
        string sub = positional.Count > 1 ? positional[1] : string.Empty;
        if (command.Equals("aas", StringComparison.OrdinalIgnoreCase) && positional.Count > 1)
        {
            command = positional[1];
            sub = positional.Count > 2 ? positional[2] : string.Empty;
        }

        return new CommandOptions(command, sub, dict, force, dryRun, verbose, showHelp, showVersion);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("MaelstromToolkit (net8.0) - safe, deterministic scaffolds");
        Console.WriteLine("Usage (examples):");
        Console.WriteLine("  maelstromtoolkit init --out ./out");
        Console.WriteLine("  maelstromtoolkit policy init --out ./out");
        Console.WriteLine("  maelstromtoolkit policy validate --out ./out [--file ./aas.policy.txt]");
        Console.WriteLine("  maelstromtoolkit policy effective --out ./out [--file ./aas.policy.txt] [--pack default] [--scenario id]");
        Console.WriteLine("  maelstromtoolkit policy watch --out ./out [--file ./aas.policy.txt]");
        Console.WriteLine("  maelstromtoolkit tags init --out ./out");
        Console.WriteLine("  maelstromtoolkit stewardship init --out ./out");
        Console.WriteLine("  maelstromtoolkit ux init --framework winforms --out ./out");
        Console.WriteLine("  maelstromtoolkit ci add --provider github --profile tools-only --out ./out");
        Console.WriteLine("  maelstromtoolkit handoff --out ./out");
        Console.WriteLine("  maelstromtoolkit selftest");
        Console.WriteLine("Flags: --force (overwrite), --dry-run, --verbose, --help, --version, --out <dir>");
    }

    private static void RunInit(string root, CommandOptions options, List<string> summary, List<string> warnings)
    {
        CopyTemplate(root, "POLICY_BOUNDARY.md", options, summary, warnings);
        CopyTemplate(root, "policy.config.sample", options, summary, warnings);
        CopyTemplate(root, "TAG_POLICY.md", options, summary, warnings);
        CopyTemplate(root, "STEWARDSHIP_CHECKLIST.md", options, summary, warnings);
        CopyTemplate(root, "FEEDBACK_LOG.md", options, summary, warnings);
        CopyTemplate(root, "UX_MAINTENANCE.md", options, summary, warnings);
    }

    private static void CopyTemplate(string targetRoot, string templateName, CommandOptions options, List<string> summary, List<string> warnings, string? framework = null)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Templates", templateFolderFor(templateName), templateName);
        if (!File.Exists(source))
        {
            warnings.Add($"Template missing: {templateName}");
            return;
        }
        var content = File.ReadAllText(source);
        if (!string.IsNullOrWhiteSpace(framework))
        {
            content = content.Replace("{{FRAMEWORK}}", framework, StringComparison.OrdinalIgnoreCase);
        }
        var dest = Path.Combine(targetRoot, templateName);
        if (File.Exists(dest) && !options.Force)
        {
            warnings.Add($"Skipped existing file: {dest}");
            return;
        }
        WriteFile(dest, content, options, summary, File.Exists(dest));
    }

    private static int RunSelftest(CommandOptions options, List<string> summary, List<string> warnings)
    {
        var templates = new[]
        {
            ("Policy","POLICY_BOUNDARY.md"),
            ("Policy","policy.config.sample"),
            ("Tags","TAG_POLICY.md"),
            ("Stewardship","STEWARDSHIP_CHECKLIST.md"),
            ("Stewardship","FEEDBACK_LOG.md"),
            ("UX","UX_MAINTENANCE.md"),
            ("UX","UX_STYLE_GUIDE.md"),
            ("UX","UX_CHANGELOG.md"),
            ("UX","UX_TOKENS.md"),
            ("CI","github_tools-only_workflow.yml"),
            ("Handoff","README.md"),
        };

        var schemaVersion = Path.Combine(AppContext.BaseDirectory, "Templates", "schema_version.txt");
        var manifest = Path.Combine(AppContext.BaseDirectory, "Templates", "manifest.json");
        if (!File.Exists(schemaVersion))
        {
            Console.Error.WriteLine("SELFTEST FAIL: missing schema_version.txt");
            return 1;
        }
        if (!File.Exists(manifest))
        {
            Console.Error.WriteLine("SELFTEST FAIL: missing manifest.json");
            return 1;
        }

        foreach (var (folder, name) in templates)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Templates", folder, name);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"SELFTEST FAIL: missing template {folder}/{name}");
                return 1;
            }
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"maelstromtoolkit_selftest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var testOptions = options with { Force = false };
        CopyTemplate(tempDir, "POLICY_BOUNDARY.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "policy.config.sample", testOptions, summary, warnings);
        CopyTemplate(tempDir, "TAG_POLICY.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "STEWARDSHIP_CHECKLIST.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "FEEDBACK_LOG.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "UX_MAINTENANCE.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "UX_STYLE_GUIDE.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "UX_CHANGELOG.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "UX_TOKENS.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "github_tools-only_workflow.yml", testOptions, summary, warnings);
        CopyTemplate(tempDir, "README.md", testOptions, summary, warnings);

        try { Directory.Delete(tempDir, true); } catch { /* ignore */ }

        Console.WriteLine("SELFTEST PASS");
        return 0;
    }

    private static int RunPolicyValidate(string root, CommandOptions options)
    {
        var (policyPath, outRoot) = GetPolicyPaths(root, options);
        if (!OutIsSafe(outRoot))
        {
            Console.Error.WriteLine("ERROR: --out must contain the segment \"--out\" (case-insensitive). No files written.");
            return 1;
        }

        Directory.CreateDirectory(Path.Combine(outRoot, "system"));

        var parserFacade = new PolicyTxtParserFacade();
        var validator = new PolicyValidator();
        var diagnostics = new List<PolicyDiagnostic>();
        string policyText = string.Empty;

        if (!File.Exists(policyPath))
        {
            diagnostics.Add(new PolicyDiagnostic("AASPOL001", DiagnosticSeverity.Error, "file", "missing", null, $"File not found: {policyPath}"));
        }
        else
        {
            policyText = File.ReadAllText(policyPath);
        }

        PolicySnapshot? snapshot = null;
        if (!string.IsNullOrEmpty(policyText) || diagnostics.Count == 0)
        {
            var parsed = parserFacade.Parse(policyText);
            diagnostics.AddRange(parsed.Diagnostics);
            if (parsed.Snapshot != null)
            {
                var validation = validator.Validate(parsed.Snapshot);
                diagnostics.AddRange(validation.Diagnostics);
                snapshot = parsed.Snapshot;
            }
        }

        var ordered = diagnostics
            .OrderBy(d => d.Code, StringComparer.Ordinal)
            .ThenBy(d => d.Section, StringComparer.Ordinal)
            .ThenBy(d => d.Key, StringComparer.Ordinal)
            .ThenBy(d => d.LineNumber ?? int.MaxValue)
            .ThenBy(d => d.Message, StringComparer.Ordinal)
            .ToList();

        var hasErrors = ordered.Any(d => d.Severity == DiagnosticSeverity.Error) || snapshot == null;
        var hash = string.IsNullOrEmpty(policyText) ? "none" : ComputeSha256(policyText);
        var activeProfile = snapshot?.Global.ActiveProfile ?? "unknown";

        var mode = "UNKNOWN";
        var liveStatus = "N/A";
        PolicyValidationResult? validationResult = null;
        if (snapshot != null)
        {
            validationResult = validator.Validate(snapshot);
            mode = validationResult.OperatingMode;
            liveStatus = validationResult.LiveStatus;
        }

        WriteValidateFile(outRoot, policyPath, hash, activeProfile, mode, liveStatus, ordered, !hasErrors);

        if (!hasErrors)
        {
            if (validationResult == null || validationResult.Snapshot == null)
            {
                Console.Error.WriteLine("ERROR: Validation unexpectedly missing snapshot.");
                return 1;
            }
            var prevLkgPath = Path.Combine(outRoot, "system", "policy.lkg.txt");
            var prevHashPath = Path.Combine(outRoot, "system", "policy.lkg.sha256");
            var prevText = File.Exists(prevLkgPath) ? File.ReadAllText(prevLkgPath) : null;
            var prevHash = File.Exists(prevHashPath) ? File.ReadAllText(prevHashPath) : "none";
            WriteAtomic(Path.Combine(outRoot, "system", "policy.lkg.txt"), policyText);
            WriteAtomic(Path.Combine(outRoot, "system", "policy.lkg.sha256"), hash);
            var effectiveResult = new PolicyEffectiveResult(
                Source: "FILE",
                Hash: hash,
                Snapshot: validationResult!.Snapshot!,
                ActiveProfile: validationResult.Snapshot.Global.ActiveProfile,
                ProfileMode: validationResult.Snapshot.Profiles.TryGetValue(validationResult.Snapshot.Global.ActiveProfile, out var profile) ? profile.Mode.ToUpperInvariant() : "UNKNOWN",
                OperatingMode: validationResult.OperatingMode,
                LiveStatus: validationResult.LiveStatus,
                Reasons: validationResult.Reasons,
                Diagnostics: ordered,
                FileDiagnostics: Array.Empty<PolicyDiagnostic>(),
                LkgDiagnostics: Array.Empty<PolicyDiagnostic>(),
                RawText: NormalizeLineEndings(policyText));
            var recorder = new PolicyApplyRecorder();
            recorder.Record(outRoot, policyPath, effectiveResult, NormalizeLineEndings(policyText), prevText, prevHash);
            Console.WriteLine($"VALID hash={hash} activeProfile={activeProfile} mode={mode} liveStatus={liveStatus} diagCount={ordered.Count}");
            return 0;
        }

        WriteRejected(outRoot, ordered);
        var top5 = string.Join(",", ordered.Take(5).Select(d => d.Code));
        Console.WriteLine($"INVALID hash={hash} activeProfile={activeProfile} mode={mode} liveStatus={liveStatus} diagCount={ordered.Count} top5={top5}");
        return 1;
    }

    private static int RunPolicyEffective(string root, CommandOptions options)
    {
        var format = options.Args.TryGetValue("format", out var fmt) ? fmt.ToLowerInvariant() : "text";
        if (format is not ("text" or "json"))
        {
            Console.Error.WriteLine("ERROR: --format must be text|json");
            return 1;
        }

        var (policyPath, outRoot) = GetPolicyPaths(root, options);
        if (!OutIsSafe(outRoot))
        {
            Console.Error.WriteLine("ERROR: --out must contain \"--out\" segment (safety guard).");
            return 1;
        }

        var fileText = File.Exists(policyPath) ? File.ReadAllText(policyPath) : string.Empty;
        var lkgPath = Path.Combine(outRoot, "system", "policy.lkg.txt");
        var lkgText = File.Exists(lkgPath) ? File.ReadAllText(lkgPath) : null;

        var resolver = new PolicyEffectiveResolver(DefaultPolicyText);
        var effective = resolver.Resolve(fileText, lkgText);

        var fileTop = effective.FileDiagnostics.Select(d => d.Code).Distinct().Take(5).ToList();
        var lkgTop = effective.LkgDiagnostics.Select(d => d.Code).Distinct().Take(3).ToList();
        var reasons = effective.Reasons.Count > 0 ? string.Join(",", effective.Reasons) : "none";
        var fileTopCodes = fileTop.Count > 0 ? string.Join(",", fileTop) : "none";
        var lkgTopCodes = lkgTop.Count > 0 ? string.Join(",", lkgTop) : "none";

        Console.WriteLine($"Source={effective.Source} hash={effective.Hash} activeProfile={effective.ActiveProfile} profileMode={effective.ProfileMode} operatingMode={effective.OperatingMode} liveStatus={effective.LiveStatus} reasons={reasons} diagCount={effective.Diagnostics.Count} fileTop={fileTopCodes} lkgTop={lkgTopCodes}");

        WriteEffectiveText(outRoot, policyPath, effective, fileTopCodes, lkgTopCodes);
        if (format == "json")
        {
            WriteEffectiveJson(outRoot, policyPath, effective, fileTop, lkgTop);
        }

        return 0;
    }

    private static int RunPolicyWatch(string root, CommandOptions options)
    {
        var (policyPath, outRoot) = GetPolicyPaths(root, options);
        if (!OutIsSafe(outRoot))
        {
            Console.Error.WriteLine("ERROR: --out must contain \"--out\" segment (safety guard).");
            return 1;
        }

        var runner = new PolicyWatchRunner(policyPath, outRoot, DefaultPolicyText);
        Console.WriteLine($"Watching {policyPath} (Ctrl+C to stop)...");
        return runner.Run();
    }

    private static (string policyPath, string outRoot) GetPolicyPaths(string root, CommandOptions options)
    {
        var policyPath = options.Args.TryGetValue("file", out var path)
            ? Path.GetFullPath(path)
            : Path.Combine(Directory.GetCurrentDirectory(), "aas.policy.txt");
        var outRoot = options.Args.TryGetValue("out", out var o) ? Path.GetFullPath(o) : root;
        return (policyPath, outRoot);
    }

    private static void WriteFile(string path, string content, CommandOptions options, List<string> summary, bool existed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, NormalizeLineEndings(content), utf8);
        if (options.DryRun)
        {
            File.Delete(tmp);
            summary.Add($"(dry-run) {(existed ? "would overwrite" : "would create")} {path}");
            return;
        }
        File.Move(tmp, path, overwrite: true);
        summary.Add(path);
        if (options.Verbose)
        {
            Console.WriteLine($"Wrote {path}");
        }
    }

    private static string NormalizeLineEndings(string input) =>
        input.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private static bool OutIsSafe(string outRoot) =>
        outRoot.IndexOf("--out", StringComparison.OrdinalIgnoreCase) >= 0;

    private static void WriteValidateFile(string outRoot, string policyPath, string hash, string activeProfile, string mode, string liveStatus, IReadOnlyList<PolicyDiagnostic> diagnostics, bool isValid)
    {
        var systemDir = Path.Combine(outRoot, "system");
        Directory.CreateDirectory(systemDir);
        var path = Path.Combine(systemDir, "policy.validate.txt");
        var sb = new StringBuilder();
        sb.AppendLine(isValid ? "VALID" : "INVALID");
        sb.AppendLine($"File: {policyPath}");
        sb.AppendLine($"Hash: {hash}");
        sb.AppendLine($"ActiveProfile: {activeProfile}");
        sb.AppendLine($"OperatingMode: {mode}");
        sb.AppendLine($"LiveStatus: {liveStatus}");
        sb.AppendLine($"Diagnostics: {diagnostics.Count}");
        foreach (var d in diagnostics)
        {
            var line = d.LineNumber.HasValue ? d.LineNumber.Value.ToString() : "-";
            sb.AppendLine($"{d.Code} | {d.Severity} | {d.Section}.{d.Key} | {line} | {d.Message}");
        }
        WriteAtomic(path, sb.ToString());
    }

    private static void WriteRejected(string outRoot, IReadOnlyList<PolicyDiagnostic> diagnostics)
    {
        var systemDir = Path.Combine(outRoot, "system");
        Directory.CreateDirectory(systemDir);
        var path = Path.Combine(systemDir, "policy.rejected.txt");
        var sb = new StringBuilder();
        foreach (var d in diagnostics)
        {
            var line = d.LineNumber.HasValue ? d.LineNumber.Value.ToString() : "-";
            sb.AppendLine($"{d.Code} | {d.Severity} | {d.Section}.{d.Key} | {line} | {d.Message}");
        }
        WriteAtomic(path, sb.ToString());
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tmp, NormalizeLineEndings(content), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static void WriteEffectiveText(string outRoot, string policyPath, PolicyEffectiveResult effective, string fileTopCodes, string lkgTopCodes)
    {
        var systemDir = Path.Combine(outRoot, "system");
        Directory.CreateDirectory(systemDir);
        var path = Path.Combine(systemDir, "policy.effective.txt");
        var sb = new StringBuilder();
        sb.AppendLine($"Source: {effective.Source}");
        sb.AppendLine($"File: {policyPath}");
        sb.AppendLine($"Hash: {effective.Hash}");
        sb.AppendLine($"ActiveProfile: {effective.ActiveProfile}");
        sb.AppendLine($"ProfileMode: {effective.ProfileMode}");
        sb.AppendLine($"OperatingMode: {effective.OperatingMode}");
        sb.AppendLine($"LiveStatus: {effective.LiveStatus}");
        sb.AppendLine($"LiveReasons: {(effective.Reasons.Count == 0 ? "none" : string.Join(",", effective.Reasons))}");
        sb.AppendLine($"AI: enabled={(effective.Snapshot?.Ai.Enabled ?? false).ToString().ToLowerInvariant()} provider={effective.Snapshot?.Ai.Provider ?? "none"} model={effective.Snapshot?.Ai.Model ?? string.Empty} temperature={(effective.Snapshot?.Ai.Temperature ?? 0).ToString(CultureInfo.InvariantCulture)} apiKeyEnv={effective.Snapshot?.Ai.ApiKeyEnv ?? string.Empty} allowSendScreenshotsToModel={(effective.Snapshot?.Ai.AllowSendScreenshotsToModel ?? false).ToString().ToLowerInvariant()} allowSendAudioToModel={(effective.Snapshot?.Ai.AllowSendAudioToModel ?? false).ToString().ToLowerInvariant()}");
        sb.AppendLine($"Ethics: purpose={effective.Snapshot?.Ethics.Purpose ?? string.Empty} requireConsentForEnvironmentControl={(effective.Snapshot?.Ethics.RequireConsentForEnvironmentControl ?? false).ToString().ToLowerInvariant()} prohibit={effective.Snapshot?.Ethics.Prohibit ?? string.Empty} privacy.storeScreenshots={(effective.Snapshot?.Ethics.PrivacyStoreScreenshots ?? false).ToString().ToLowerInvariant()} privacy.storeAudio={(effective.Snapshot?.Ethics.PrivacyStoreAudio ?? false).ToString().ToLowerInvariant()}");
        sb.AppendLine($"Diagnostics: {effective.Diagnostics.Count}");
        foreach (var d in effective.Diagnostics)
        {
            var line = d.LineNumber.HasValue ? d.LineNumber.Value.ToString() : "-";
            sb.AppendLine($"{d.Code} | {d.Severity} | {d.Section}.{d.Key} | {line} | {d.Message}");
        }
        sb.AppendLine($"FileDiagnosticsTop: {fileTopCodes}");
        sb.AppendLine($"LkgDiagnosticsTop: {lkgTopCodes}");
        WriteAtomic(path, sb.ToString());
    }

    private static void WriteEffectiveJson(string outRoot, string policyPath, PolicyEffectiveResult effective, IReadOnlyList<string> fileTop, IReadOnlyList<string> lkgTop)
    {
        var systemDir = Path.Combine(outRoot, "system");
        Directory.CreateDirectory(systemDir);
        var dto = new
        {
            source = effective.Source,
            file = policyPath,
            hash = effective.Hash,
            activeProfile = effective.ActiveProfile,
            profileMode = effective.ProfileMode,
            operatingMode = effective.OperatingMode,
            liveStatus = effective.LiveStatus,
            liveReasons = effective.Reasons.ToArray(),
            ai = new
            {
                enabled = effective.Snapshot?.Ai.Enabled ?? false,
                provider = effective.Snapshot?.Ai.Provider ?? "none",
                model = effective.Snapshot?.Ai.Model ?? string.Empty,
                temperature = effective.Snapshot?.Ai.Temperature ?? 0,
                apiKeyEnv = effective.Snapshot?.Ai.ApiKeyEnv ?? string.Empty,
                allowSendScreenshotsToModel = effective.Snapshot?.Ai.AllowSendScreenshotsToModel ?? false,
                allowSendAudioToModel = effective.Snapshot?.Ai.AllowSendAudioToModel ?? false
            },
            ethics = new
            {
                purpose = effective.Snapshot?.Ethics.Purpose ?? string.Empty,
                requireConsentForEnvironmentControl = effective.Snapshot?.Ethics.RequireConsentForEnvironmentControl ?? false,
                prohibit = effective.Snapshot?.Ethics.Prohibit ?? string.Empty,
                privacyStoreScreenshots = effective.Snapshot?.Ethics.PrivacyStoreScreenshots ?? false,
                privacyStoreAudio = effective.Snapshot?.Ethics.PrivacyStoreAudio ?? false
            },
            diagnostics = effective.Diagnostics.Select(d => new
            {
                code = d.Code,
                severity = d.Severity.ToString(),
                section = d.Section,
                key = d.Key,
                line = d.LineNumber,
                message = d.Message
            }).ToArray(),
            fileDiagnosticsTop = fileTop.ToArray(),
            lkgDiagnosticsTop = lkgTop.ToArray()
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        var path = Path.Combine(systemDir, "policy.effective.json");
        WriteAtomic(path, json);
    }

    private static string ComputeSha256(string text)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(NormalizeLineEndings(text));
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string templateFolderFor(string templateName)
    {
        if (templateName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) return "CI";
        if (templateName.Contains("POLICY", StringComparison.OrdinalIgnoreCase) || templateName.Contains("policy", StringComparison.OrdinalIgnoreCase)) return "Policy";
        if (templateName.Contains("TAG", StringComparison.OrdinalIgnoreCase)) return "Tags";
        if (templateName.Contains("FEEDBACK", StringComparison.OrdinalIgnoreCase) || templateName.Contains("STEWARDSHIP", StringComparison.OrdinalIgnoreCase)) return "Stewardship";
        if (templateName.StartsWith("UX_", StringComparison.OrdinalIgnoreCase) || templateName.StartsWith("UX ", StringComparison.OrdinalIgnoreCase) || templateName.Contains("UX", StringComparison.OrdinalIgnoreCase)) return "UX";
        if (templateName.Equals("README.md", StringComparison.OrdinalIgnoreCase)) return "Handoff";
        return string.Empty;
    }

    private static bool RequiresOut(string command) =>
        command is "init" or "policy" or "tags" or "stewardship" or "ux" or "ci" or "handoff";

    private static bool ValidateOut(string outPath, CommandOptions options)
    {
        var full = Path.GetFullPath(outPath);
        var root = Path.GetPathRoot(full);
        if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("ERROR: --out cannot be a filesystem root.");
            return false;
        }
        if (IsSymlink(full))
        {
            Console.Error.WriteLine("ERROR: --out cannot be a symlinked directory.");
            return false;
        }
        if (options.Verbose)
        {
            Console.WriteLine($"Using output directory: {full}");
        }
        return true;
    }

    private static bool IsSymlink(string path)
    {
        var dirInfo = new DirectoryInfo(path);
        if (!dirInfo.Exists) return false;
        return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}
