using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HandoffBridge;

internal static class Program
{
    private sealed record Config(
        string Command,
        string Root,
        bool Clean,
        bool IncludeDiffs,
        bool IncludeFiles,
        bool AllowNoScan,
        bool Verbose,
        string Profile,
        bool NoRotate,
        int MaxArchives);

    private static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    private static string RepoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    private static string HandoffRoot => Path.Combine(RepoRoot, "artifacts", "handoff");
    private static string ToCodexRoot => Path.Combine(HandoffRoot, "to_codex");
    private static string FromCodexRoot => Path.Combine(HandoffRoot, "from_codex");
    private static string ReportsRoot => Path.Combine(HandoffRoot, "reports");
    private static string SecretScanReport => Path.Combine(ReportsRoot, "SECRET_SCAN.txt");

    public static int Main(string[] args)
    {
        try
        {
            var cfg = ParseArgs(args);
            if (cfg.Command == "version")
            {
                Console.WriteLine(Version);
                return 0;
            }

            RepoRoot = cfg.Root;
            Directory.CreateDirectory(ToCodexRoot);
            Directory.CreateDirectory(FromCodexRoot);
            Directory.CreateDirectory(ReportsRoot);

            var result = cfg.Command switch
            {
                "export" => Export(cfg),
                "import" => Import(cfg),
                "help" => Usage(),
                _ => Usage()
            };
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Error] {ex.Message}");
            return 1;
        }
    }

    private static Config ParseArgs(string[] args)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("Command required (export|import|help|--version).");

        var command = args[0].ToLowerInvariant();
        if (command == "--version") command = "version";

        var root = Path.GetFullPath(Directory.GetCurrentDirectory());
        var clean = false;
        var includeDiffs = false;
        var includeFiles = true;
        var allowNoScan = false;
        var verbose = false;
        var profile = "docs";
        var noRotate = false;
        var maxArchives = 10;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root":
                    if (i + 1 >= args.Length) throw new InvalidOperationException("--root requires a path");
                    root = Path.GetFullPath(args[++i]);
                    break;
                case "--clean":
                    clean = true;
                    break;
                case "--include-diffs":
                    includeDiffs = true;
                    break;
                case "--no-files":
                    includeFiles = false;
                    break;
                case "--allow-no-scan":
                    allowNoScan = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--profile":
                    if (i + 1 >= args.Length) throw new InvalidOperationException("--profile requires value (docs|ux)");
                    profile = args[++i].ToLowerInvariant();
                    if (profile != "docs" && profile != "ux") throw new InvalidOperationException("Profile must be docs or ux");
                    break;
                case "--no-rotate":
                    noRotate = true;
                    break;
                case "--max-archives":
                    if (i + 1 >= args.Length) throw new InvalidOperationException("--max-archives requires a number");
                    if (!int.TryParse(args[++i], out maxArchives) || maxArchives < 0 || maxArchives > 200) throw new InvalidOperationException("max-archives must be between 0 and 200");
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option: {args[i]}");
            }
        }

        return new Config(command, root, clean, includeDiffs, includeFiles, allowNoScan, verbose, profile, noRotate, maxArchives);
    }

    private static int Usage()
    {
        Console.WriteLine("HandoffBridge commands:");
        Console.WriteLine("  export [--root <path>] [--clean] [--include-diffs] [--no-files] [--allow-no-scan] [--verbose] [--profile docs|ux] [--no-rotate] [--max-archives N]");
        Console.WriteLine("  import [--root <path>] [--allow-no-scan] [--verbose] [--profile docs|ux] [--no-rotate] [--max-archives N]");
        Console.WriteLine("  --version");
        return 1;
    }

    private static int Export(Config cfg)
    {
        var handoffPath = Path.Combine(ToCodexRoot, "HANDOFF_TO_CODEX.md");
        var contextPath = Path.Combine(ToCodexRoot, "CONTEXT_SNAPSHOT.md");
        var statusPath = Path.Combine(ToCodexRoot, "REPO_STATUS.txt");

        if (cfg.Clean)
        {
            SafeDelete(ToCodexRoot);
            Directory.CreateDirectory(ToCodexRoot);
        }

        var git = GetGitInfo(cfg);
        File.WriteAllText(statusPath, $"Branch: {git.Branch}{Environment.NewLine}Commit: {git.Commit}{Environment.NewLine}Status:{Environment.NewLine}{git.Status}");

        var context = new StringBuilder();
        context.AppendLine(Header("Context Snapshot", cfg));
        context.AppendLine("- POLICY_BOUNDARY.md : policy boundary");
        context.AppendLine("- SUBMISSION.md : submission guide");
        context.AppendLine("- VERIFY.md : verification guide");
        context.AppendLine("- artifacts/submission/INDEX.txt : artifact inventory");
        context.AppendLine("- artifacts/submission/REPRO_STAMP.txt : reproducibility stamp");
        File.WriteAllText(contextPath, context.ToString());

        var fileList = cfg.IncludeFiles ? GetFileList(cfg) : Array.Empty<string>();
        var diffs = cfg.IncludeDiffs ? GetDiffs(cfg) : Array.Empty<string>();

        var prompt = new StringBuilder();
        prompt.AppendLine(Header("Handoff to Codex", cfg));
        prompt.AppendLine("```");
        prompt.AppendLine($"You are continuing Project Maelstrom (branch: {git.Branch}).");
        prompt.AppendLine();
        prompt.AppendLine("Context files:");
        prompt.AppendLine("- POLICY_BOUNDARY.md");
        prompt.AppendLine("- SUBMISSION.md");
        prompt.AppendLine("- VERIFY.md");
        prompt.AppendLine("- artifacts/submission/INDEX.txt");
        prompt.AppendLine("- artifacts/submission/REPRO_STAMP.txt");
        prompt.AppendLine();
        prompt.AppendLine("Git:");
        prompt.AppendLine($"- Branch: {git.Branch}");
        prompt.AppendLine($"- Commit: {git.Commit}");
        prompt.AppendLine("- Status:");
        prompt.AppendLine(git.Status);
        if (fileList.Length > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Recent files (names only):");
            foreach (var f in fileList) prompt.AppendLine($"- {f}");
        }
        if (diffs.Length > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Recent diffs (name-only):");
            foreach (var d in diffs) prompt.AppendLine($"- {d}");
        }
        prompt.AppendLine();
        if (cfg.Profile == "docs")
        {
            prompt.AppendLine("DO NOT TOUCH: runtime, executors, policies, packaging, tests, or UI visuals.");
        }
        else
        {
            prompt.AppendLine("DO NOT TOUCH: runtime, executors, policies, packaging, tests. UI cosmetic changes only.");
        }
        prompt.AppendLine("Required output: write response to artifacts/handoff/from_codex/RESULT.md with EXACTLY ONE fenced code block.");
        prompt.AppendLine();
        prompt.AppendLine("Instructions:");
        prompt.AppendLine("- IMPORTANT: Put your entire final response in a single code block.");
        prompt.AppendLine("- Summarize changes, files touched, tests run/results, warnings/errors.");
        prompt.AppendLine("```");
        File.WriteAllText(handoffPath, prompt.ToString());

        EnsureSingleCodeBlock(File.ReadAllText(handoffPath));
        EnsureCodeBlockNotEmpty(File.ReadAllText(handoffPath));

        RunSecretScan(cfg, handoffPath);
        RunSecretScan(cfg, contextPath);

        Console.WriteLine($"Export complete (v{Version}, profile={cfg.Profile}):");
        Console.WriteLine($"- {handoffPath}");
        Console.WriteLine($"- {contextPath}");
        Console.WriteLine($"- {statusPath}");
        Console.WriteLine($"- Secret scan: {SecretScanReport}");
        return 0;
    }

    private static int Import(Config cfg)
    {
        var resultPath = Path.Combine(FromCodexRoot, "RESULT.md");
        if (!File.Exists(resultPath))
            throw new FileNotFoundException("Missing required RESULT.md", resultPath);

        var text = File.ReadAllText(resultPath);
        EnsureSingleCodeBlock(text);
        EnsureCodeBlockNotEmpty(text);

        var redacted = RedactSecrets(text);
        var reportPath = Path.Combine(ReportsRoot, "CODEX_REPORT.md");
        if (!cfg.NoRotate) ArchiveLatestFile(reportPath, "CODEX_REPORT", ".md", cfg);
        File.WriteAllText(reportPath, $"{Header("CODEX REPORT", cfg)}{Environment.NewLine}{redacted}");

        var filesPath = Path.Combine(FromCodexRoot, "FILES_TOUCHED.txt");
        var testsPath = Path.Combine(FromCodexRoot, "TESTS_RUN.txt");
        var notesPath = Path.Combine(FromCodexRoot, "NOTES.md");
        var summaryPath = Path.Combine(ReportsRoot, "CODEX_SUMMARY.md");
        if (!cfg.NoRotate) ArchiveLatestFile(summaryPath, "CODEX_SUMMARY", ".md", cfg);
        var summary = new StringBuilder();
        summary.AppendLine(Header("CODEX SUMMARY", cfg));
        summary.AppendLine();
        summary.AppendLine("Files touched:");
        summary.AppendLine(File.Exists(filesPath) ? File.ReadAllText(filesPath) : "(not provided)");
        summary.AppendLine();
        summary.AppendLine("Tests run:");
        summary.AppendLine(File.Exists(testsPath) ? File.ReadAllText(testsPath) : "(not provided)");
        summary.AppendLine();
        summary.AppendLine("Notes:");
        summary.AppendLine(File.Exists(notesPath) ? File.ReadAllText(notesPath) : "(not provided)");
        summary.AppendLine();
        summary.AppendLine("Warnings/Errors detected from RESULT.md:");
        summary.AppendLine(ExtractWarnings(text));
        File.WriteAllText(summaryPath, summary.ToString());

        try
        {
            RunSecretScan(cfg, reportPath);
            PrependReportHeader(reportPath, "> SCAN PASS - SAFE TO PASTE");
        }
        catch
        {
            PrependReportHeader(reportPath, "> SCAN FAILED - DO NOT PASTE");
            throw;
        }

        UpdateIndex(cfg);

        Console.WriteLine($"Import complete (v{Version}, profile={cfg.Profile}):");
        Console.WriteLine($"- Report: {reportPath}");
        Console.WriteLine($"- Summary: {summaryPath}");
        Console.WriteLine($"- Secret scan: {SecretScanReport}");
        return 0;
    }

    private static (string Branch, string Commit, string Status) GetGitInfo(Config cfg)
    {
        string branch = "(unknown)";
        string commit = "(unknown)";
        string status = "(git unavailable)";
        try
        {
            branch = RunProcess("git", "rev-parse --abbrev-ref HEAD", cfg).Trim();
            commit = RunProcess("git", "rev-parse --short HEAD", cfg).Trim();
            status = RunProcess("git", "status --short", cfg);
        }
        catch { }
        return (branch, commit, status);
    }

    private static string RunProcess(string fileName, string args, Config cfg)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoRoot
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (cfg.Verbose)
            Console.WriteLine($"[cmd] {fileName} {args} (exit {p.ExitCode})");
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Process {fileName} {args} failed: {error}");
        return output;
    }

    private static void EnsureSingleCodeBlock(string content)
    {
        var lines = content.Split('\n');
        var firstFenceIndex = -1;
        string? fence = null;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith("```") || line.StartsWith("~~~"))
            {
                firstFenceIndex = i;
                fence = line.StartsWith("```") ? "```" : "~~~";
                break;
            }
        }
        if (firstFenceIndex == -1)
            throw new InvalidOperationException("No fenced code block found.");

        var closeIndex = -1;
        for (int i = firstFenceIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith(fence!))
            {
                closeIndex = i;
                break;
            }
            if (line.StartsWith("```") || line.StartsWith("~~~"))
                throw new InvalidOperationException("Nested or mismatched fence detected inside block.");
        }
        if (closeIndex == -1)
            throw new InvalidOperationException("Closing fence not found.");

        for (int i = closeIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith("```") || line.StartsWith("~~~"))
                throw new InvalidOperationException("Extra fence detected outside the single block.");
        }
    }

    private static void EnsureCodeBlockNotEmpty(string content)
    {
        var match = Regex.Match(content, "(```|~~~)(.*?)(```|~~~)", RegexOptions.Singleline);
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[2].Value))
            throw new InvalidOperationException("Code block is empty or missing.");
    }

    private static string RedactSecrets(string content)
    {
        var patterns = new[]
        {
            "ghp_[A-Za-z0-9]+",
            "github_pat_[A-Za-z0-9_]+",
            "AIza[0-9A-Za-z\\-_]{20,}",
            "-----BEGIN PRIVATE KEY-----.+?-----END PRIVATE KEY-----",
            "BEGIN RSA PRIVATE KEY",
            "AKIA[0-9A-Z]{16}",
            "ASIA[0-9A-Z]{16}",
            "SK[0-9A-Za-z]{32}"
        };
        var redacted = content;
        foreach (var pattern in patterns)
            redacted = Regex.Replace(redacted, pattern, "REDACTED_SECRET", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return redacted;
    }

    private static string ExtractWarnings(string content)
    {
        var warnings = new List<string>();
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, "(warn|error|fail)", RegexOptions.IgnoreCase))
                warnings.Add(line.Trim());
        }
        return warnings.Count == 0 ? "(none detected)" : string.Join(Environment.NewLine, warnings);
    }

    private static void RunSecretScan(Config cfg, string targetPath)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var psScan = Path.Combine(RepoRoot, "scripts", "scan_for_secrets.ps1");
        var shScan = Path.Combine(RepoRoot, "scripts", "scan_for_secrets.sh");
        var builder = new StringBuilder();

        int exit = 0;
        if (!cfg.NoRotate) ArchiveLatestFile(SecretScanReport, "SECRET_SCAN", ".txt", cfg);
        if (isWindows && File.Exists(psScan))
        {
            exit = RunScanner("powershell", $"-ExecutionPolicy Bypass -File \"{psScan}\" -Path \"{targetPath}\"", builder, cfg);
            if (exit != 0)
            {
                exit = RunScanner("powershell", $"-ExecutionPolicy Bypass -File \"{psScan}\"", builder, cfg);
            }
        }
        else if (!isWindows && File.Exists(shScan))
        {
            exit = RunScanner("bash", $"\"{shScan}\" \"{targetPath}\"", builder, cfg);
            if (exit != 0)
            {
                exit = RunScanner("bash", $"\"{shScan}\"", builder, cfg);
            }
        }
        else
        {
            if (!cfg.AllowNoScan)
            {
                exit = 1;
                builder.AppendLine("Secret scan unavailable and --allow-no-scan not set.");
            }
            else
            {
                var content = File.ReadAllText(targetPath);
                if (Regex.IsMatch(content, "ghp_[A-Za-z0-9]+|github_pat_[A-Za-z0-9_]+|AIza[0-9A-Za-z\\-_]{20,}|-----BEGIN PRIVATE KEY-----|BEGIN RSA PRIVATE KEY|AKIA[0-9A-Z]{16}|ASIA[0-9A-Z]{16}|SK[0-9A-Za-z]{32}", RegexOptions.Singleline))
                {
                    exit = 1;
                    builder.AppendLine("Fallback scan: potential secret pattern detected.");
                }
                else
                {
                    builder.AppendLine("Fallback scan: no secrets detected.");
                }
            }
        }

        File.WriteAllText(SecretScanReport, $"{Header("SECRET SCAN", cfg)}{Environment.NewLine}{builder}");
        UpdateIndex(cfg);
        if (exit != 0)
            throw new InvalidOperationException("Secret scan failed or detected potential secrets. See SECRET_SCAN.txt.");
    }

    private static int RunScanner(string fileName, string arguments, StringBuilder log, Config cfg)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoRoot
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        log.AppendLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
            log.AppendLine(stderr);
        if (cfg.Verbose)
            Console.WriteLine($"[scan] {fileName} {arguments} (exit {p.ExitCode})");
        return p.ExitCode;
    }

    private static void SafeDelete(string directory)
    {
        if (!Directory.Exists(directory)) return;
        Directory.Delete(directory, true);
    }

    private static string[] GetFileList(Config cfg)
    {
        try
        {
            var output = RunProcess("git", "ls-files", cfg);
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(20).ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    private static string[] GetDiffs(Config cfg)
    {
        try
        {
            var output = RunProcess("git", "diff --name-only", cfg);
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(20).ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    private static void PrependReportHeader(string path, string header)
    {
        var existing = File.ReadAllText(path);
        File.WriteAllText(path, $"{header}{Environment.NewLine}{existing}");
    }

    private static string Header(string title, Config cfg)
    {
        return $"# {title} (v{Version}, profile={cfg.Profile}, ts={DateTime.UtcNow:O})";
    }

    private static string TimestampUtc() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

    private static void ArchiveLatestFile(string latestPath, string prefix, string extension, Config cfg)
    {
        try
        {
            if (!File.Exists(latestPath)) return;
            Directory.CreateDirectory(ReportsRoot);
            var ts = TimestampUtc();
            var archivePath = Path.Combine(ReportsRoot, $"{prefix}_{ts}{extension}");
            int counter = 2;
            while (File.Exists(archivePath))
            {
                archivePath = Path.Combine(ReportsRoot, $"{prefix}_{ts}_{counter}{extension}");
                counter++;
            }
            File.Copy(latestPath, archivePath, overwrite: false);
            PruneArchives(prefix, extension, cfg);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[warn] Archive skipped for {latestPath}: {ex.Message}");
        }
    }

    private static void PruneArchives(string prefix, string extension, Config cfg)
    {
        if (cfg.NoRotate) return;
        var files = Directory.Exists(ReportsRoot)
            ? Directory.GetFiles(ReportsRoot, $"{prefix}_*{extension}", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
        var ordered = files
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();
        var toPrune = ordered.Skip(cfg.MaxArchives).ToList();
        int pruned = 0;
        foreach (var f in toPrune)
        {
            try
            {
                f.Delete();
                pruned++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[warn] Prune skipped for {f.Name}: {ex.Message}");
            }
        }
        if (pruned > 0 && cfg.Verbose)
        {
            Console.WriteLine($"[rotate] Pruned {pruned} archives for {prefix}");
        }
    }

    private static IEnumerable<(string File, long Size, DateTime LastWriteUtc)> CollectLatestFiles()
    {
        var list = new List<(string, long, DateTime)>();
        void AddIfExists(string path)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                list.Add((Path.GetFileName(path), info.Length, info.LastWriteTimeUtc));
            }
        }
        AddIfExists(Path.Combine(ReportsRoot, "CODEX_REPORT.md"));
        AddIfExists(Path.Combine(ReportsRoot, "CODEX_SUMMARY.md"));
        AddIfExists(PathCombine(ReportsRoot, "SECRET_SCAN.txt"));
        return list;
    }

    private static string PathCombine(string root, string name) => Path.Combine(root, name);

    private static IEnumerable<string> CollectArchives(string prefix, string extension)
    {
        if (!Directory.Exists(ReportsRoot)) return Array.Empty<string>();
        return Directory.GetFiles(ReportsRoot, $"{prefix}_*{extension}", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
            .Select(f => Path.GetFileName(f) ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n));
    }

    private static void UpdateIndex(Config cfg)
    {
        try
        {
            Directory.CreateDirectory(ReportsRoot);
            var tmp = Path.Combine(ReportsRoot, "INDEX.tmp");
            var idx = Path.Combine(ReportsRoot, "INDEX.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"HandoffBridge INDEX (v{Version}, profile={cfg.Profile}, ts={DateTime.UtcNow:O})");
            sb.AppendLine($"Rotation: {(cfg.NoRotate ? "disabled" : "enabled")} (max-archives={cfg.MaxArchives})");
            sb.AppendLine();
            sb.AppendLine("Latest:");
            foreach (var item in CollectLatestFiles())
            {
                sb.AppendLine($"- {item.File} | {item.Size} bytes | {item.LastWriteUtc:O}");
            }
            sb.AppendLine();
            void WriteArchives(string label, string prefix, string ext)
            {
                sb.AppendLine($"{label}:");
                var archives = CollectArchives(prefix, ext).ToList();
                if (archives.Count == 0)
                {
                    sb.AppendLine("  (none)");
                }
                else
                {
                    foreach (var a in archives)
                    {
                        var info = new FileInfo(Path.Combine(ReportsRoot, a));
                        sb.AppendLine($"  - {a} | {info.Length} bytes | {info.LastWriteTimeUtc:O}");
                    }
                }
                sb.AppendLine();
            }

            WriteArchives("CODEX_REPORT archives", "CODEX_REPORT", ".md");
            WriteArchives("CODEX_SUMMARY archives", "CODEX_SUMMARY", ".md");
            WriteArchives("SECRET_SCAN archives", "SECRET_SCAN", ".txt");

            File.WriteAllText(tmp, sb.ToString());
            File.Move(tmp, idx, true);
            if (cfg.Verbose)
            {
                Console.WriteLine($"[index] Updated {idx}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[warn] Failed to update INDEX.txt: {ex.Message}");
        }
    }
}
