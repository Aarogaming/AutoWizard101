using System.Globalization;

namespace MaelstromToolkit;

internal static class Program
{
    private record CommandOptions(string Command, string Subcommand, Dictionary<string, string> Args, bool Force);

    private static int Main(string[] args)
    {
        var options = Parse(args);
        if (options == null)
        {
            PrintUsage();
            return 1;
        }

        var root = options.Args.TryGetValue("out", out var outDir)
            ? outDir
            : Directory.GetCurrentDirectory();

        var summary = new List<string>();
        var warnings = new List<string>();

        try
        {
            switch (options.Command)
            {
                case "init":
                    RunInit(root, options.Force, summary, warnings);
                    break;
                case "policy" when options.Subcommand == "init":
                    CopyTemplate(root, "Policy", "POLICY_BOUNDARY.md", options.Force, summary, warnings);
                    CopyTemplate(root, "Policy", "policy.config.sample", options.Force, summary, warnings);
                    break;
                case "tags" when options.Subcommand == "init":
                    CopyTemplate(root, "Tags", "TAG_POLICY.md", options.Force, summary, warnings);
                    break;
                case "stewardship" when options.Subcommand == "init":
                    CopyTemplate(root, "Stewardship", "STEWARDSHIP_CHECKLIST.md", options.Force, summary, warnings);
                    CopyTemplate(root, "Stewardship", "FEEDBACK_LOG.md", options.Force, summary, warnings);
                    break;
                case "ux" when options.Subcommand == "init":
                    var framework = options.Args.TryGetValue("framework", out var fw) ? fw : "winforms";
                    CopyTemplate(root, "UX", "UX_MAINTENANCE.md", options.Force, summary, warnings);
                    CopyTemplate(root, "UX", "UX_STYLE_GUIDE.md", options.Force, summary, warnings, framework);
                    CopyTemplate(root, "UX", "UX_CHANGELOG.md", options.Force, summary, warnings, framework);
                    CopyTemplate(root, "UX", "UX_TOKENS.md", options.Force, summary, warnings, framework);
                    break;
                case "ci" when options.Subcommand == "add":
                    var provider = options.Args.TryGetValue("provider", out var p) ? p : "github";
                    var profile = options.Args.TryGetValue("profile", out var pr) ? pr : "tools-only";
                    CopyTemplate(root, "CI", $"{provider}_{profile}_workflow.yml", options.Force, summary, warnings);
                    break;
                case "handoff":
                    CopyTemplate(root, "Handoff", "README.md", options.Force, summary, warnings);
                    break;
                case "selftest":
                    return RunSelftest(summary, warnings);
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
        var command = args[0];
        var sub = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal) ? args[1] : string.Empty;
        var force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            if (string.Equals(key, "force", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                dict[key] = args[i + 1];
            }
        }
        return new CommandOptions(command, sub, dict, force);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("MaelstromToolkit (net8.0) - safe, deterministic scaffolds");
        Console.WriteLine("Usage:");
        Console.WriteLine("  maelstromtoolkit init --out <dir> [--force]");
        Console.WriteLine("  maelstromtoolkit policy init --out <dir> [--force]");
        Console.WriteLine("  maelstromtoolkit tags init --out <dir> [--force]");
        Console.WriteLine("  maelstromtoolkit stewardship init --out <dir> [--force]");
        Console.WriteLine("  maelstromtoolkit ux init --framework winforms|wpf|avalonia|winui --out <dir> [--force]");
        Console.WriteLine("  maelstromtoolkit ci add --provider github --profile tools-only --out <dir> [--force]");
        Console.WriteLine("  maelstromtoolkit handoff --out <dir> [--force]");
        Console.WriteLine("  maelstromtoolkit selftest");
    }

    private static void RunInit(string root, bool force, List<string> summary, List<string> warnings)
    {
        CopyTemplate(root, "Policy", "POLICY_BOUNDARY.md", force, summary, warnings);
        CopyTemplate(root, "Tags", "TAG_POLICY.md", force, summary, warnings);
        CopyTemplate(root, "Stewardship", "STEWARDSHIP_CHECKLIST.md", force, summary, warnings);
        CopyTemplate(root, "Stewardship", "FEEDBACK_LOG.md", force, summary, warnings);
        CopyTemplate(root, "UX", "UX_MAINTENANCE.md", force, summary, warnings);
    }

    private static void CopyTemplate(string targetRoot, string templateFolder, string templateName, bool force, List<string> summary, List<string> warnings, string? framework = null)
    {
        var baseDir = AppContext.BaseDirectory;
        var source = Path.Combine(baseDir, "Templates", templateFolder, templateName);
        if (!File.Exists(source))
        {
            warnings.Add($"Template missing: {templateFolder}/{templateName}");
            return;
        }
        var content = File.ReadAllText(source);
        if (!string.IsNullOrWhiteSpace(framework))
        {
            content = content.Replace("{{FRAMEWORK}}", framework, StringComparison.OrdinalIgnoreCase);
        }
        var dest = Path.Combine(targetRoot, templateName);
        if (File.Exists(dest) && !force)
        {
            warnings.Add($"Skipped existing file: {dest}");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllText(dest, content);
        summary.Add(dest);
    }

    private static int RunSelftest(List<string> summary, List<string> warnings)
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

        var baseDir = AppContext.BaseDirectory;
        foreach (var (folder, name) in templates)
        {
            var path = Path.Combine(baseDir, "Templates", folder, name);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"SELFTEST FAIL: missing template {folder}/{name}");
                return 1;
            }
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"maelstromtoolkit_selftest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        CopyTemplate(tempDir, "Policy", "POLICY_BOUNDARY.md", false, summary, warnings);
        CopyTemplate(tempDir, "Tags", "TAG_POLICY.md", false, summary, warnings);
        CopyTemplate(tempDir, "Stewardship", "STEWARDSHIP_CHECKLIST.md", false, summary, warnings);
        CopyTemplate(tempDir, "UX", "UX_MAINTENANCE.md", false, summary, warnings, "winforms");
        CopyTemplate(tempDir, "CI", "github_tools-only_workflow.yml", false, summary, warnings);
        CopyTemplate(tempDir, "Handoff", "README.md", false, summary, warnings);

        // Clean temp directory for determinism.
        try { Directory.Delete(tempDir, true); } catch { /* ignore */ }

        Console.WriteLine("SELFTEST PASS");
        return 0;
    }
}
